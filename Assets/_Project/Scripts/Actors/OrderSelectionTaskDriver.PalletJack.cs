using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameCore.Labor;
using GameCore.Inventory;

namespace GameCore.Actors
{
    /// <summary>
    /// ORDER SELECTOR ON A PALLET JACK (Tad, 2026-10-16).
    ///
    /// When a selector claims an OrderSelect task and a free pallet jack exists:
    ///   1. walk to the jack and mount it (hands on the handlebars = the Drive_PalletJack pose),
    ///   2. an empty CHEP sits on the forks,
    ///   3. per pick: drive down the aisle and park beside the pick slot, step off, walk to the slot, take a
    ///      case in hand (CarryCase pose + a real case model), turn round, set it on the pallet, repeat,
    ///      then step back on,
    ///   4. when done: drive to the staging lane's open end, turn to face away from the door, back in
    ///      forks-first, lower and leave the pallet(s) (unparented, Staged), drive out, step off and leave
    ///      the jack parked at the lane mouth.
    ///
    /// Same order logic as the on-foot path (same slot choice, short-pick and pallet-pick rules, same staging
    /// lane rules, same PlacePalletsAtStagingSlot). With NO free jack, or if the jack can't path somewhere, the
    /// selector falls back to the old on-foot behaviour (pallets ride the selector) so the order never stalls.
    ///
    /// The jack is driven by its own NavMeshAgent (DriveJackTo / JackNavLeg) to every stop. Only the turn in place (JackFace) and
    /// the short straight slides (JackMoveStraight, for reversing into a lane) are scripted, and both pass the solid guard
    /// (MachineClearance.StepBlocked). While mounted, the selector's own navigation is off (MHEOperatorSlot), and the short walks
    /// off the jack are scripted too, so their positions are exact. The queue / pass / walk-out traffic rules were removed (2026-11-16).
    /// Geometry constants below were measured on PltJack_FULL (forks run 0.2–2.54 m behind the operator anchor).
    /// </summary>
    public partial class OrderSelectionTaskDriver
    {
        // ── Tunables ────────────────────────────────────────────────────────────────────────────────
        private const float BaseJackDriveSpeed   = 1.8f;   // m/s, scripted forward legs
        private const float BaseJackReverseSpeed = 0.9f;   // m/s, backing into the lane
        private const float BaseJackTurnSpeed    = 110f;   // deg/s, turning in place
        // Doubled while the jack finishes delivering driverless after its battery died (EquipmentBattery).
        private float JackSpeedMul => _jack != null ? EquipmentBattery.SpeedMultiplierFor(_jack) : 1f;
        private float JackDriveSpeed   => BaseJackDriveSpeed * JackSpeedMul;
        private float JackReverseSpeed => BaseJackReverseSpeed * JackSpeedMul;
        private float JackTurnSpeed    => BaseJackTurnSpeed * JackSpeedMul;

        /// <summary>A selector currently holds this jack (EquipmentBattery waits for this before driving it to a charger).</summary>
        public static bool IsJackClaimed(MHEOperatorSlot jack)
            => jack != null && s_jackClaims.TryGetValue(jack, out var owner) && owner != null;

        /// <summary>The jack's battery went flat mid-run (EquipmentBattery.NeedsCharge).</summary>
        private bool JackOutOfPower => _jack != null && _jack.GetComponent<EquipmentBattery>() is EquipmentBattery b && b.NeedsCharge;

        /// <summary>This selector climbed off a flat jack: the jack finishes the delivery on its own, the selector patrols.</summary>
        private bool _ejectedFromJack;

        /// <summary>The battery went flat (or Change Battery was chosen) mid-run: the selector STAYS aboard, stops picking, stages what
        /// is on the pallet, then parks the jack in a charging bay and steps off (Tad, 2026-10-18). Still owes the order a trip.</summary>
        private bool _flatBatteryStop;

        /// <summary>Flat battery (Tad, 2026-10-17): the selector climbs off and goes back to patrolling; the jack carries
        /// on alone at double speed to stage what is on its forks, then EquipmentBattery takes it to a charger.</summary>
        private void EjectFromJack()
        {
            if (_ejectedFromJack || _jack == null) return;
            _ejectedFromJack = true;
            DestroyHeldCase();
            SetLean(0f);
            SetAnimBool("CarryCase", false);
            var identity = GetComponent<EmployeeIdentity>();
            if (_jack.CurrentOperator == identity) _jack.EjectRider();
            EndScriptedFoot();   // if they were off the jack walking by script, hand their body back to their own navigation
            _jack.GetComponent<EquipmentBattery>()?.BeginDriverlessRun();
            _nav.SetTaskBusy(false);
            _nav.Patrol();
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': pallet jack battery died on order {_currentOrder?.OrderNumber} - climbed off; the jack stages what's aboard by itself.");
        }
        private const float FootWalkSpeed    = 1.4f;   // matches the staff walk speed
        private const float FootTurnSpeed    = 480f;   // deg/s
        private const float GrabSeconds      = 0.35f;  // reaching into the slot
        private const float PlaceSeconds     = 0.30f;  // setting the case down on the pallet
        private const float ParkOffset       = 2.1f;   // jack centreline distance from the pick face (into the aisle)
        private const float MaxPickReach = 5f;         // a case is never carried from a slot further than this from its pallet
        private const float PickFaceStandOff = 0.65f;  // fallback distance from a slot pivot (the rack's depth centre) to the rack's aisle face
        private const float RackFaceClearance = 0.6f;  // selector's body (0.5 m radius) plus a margin: they stand this far OUT from the face

        /// <summary>How far out from a slot's pivot (the rack's depth centre) a selector stands to pick from it: the rack's aisle-facing
        /// face plus body clearance. The pivot is inside the rack, so walking to it, or to a point only a face-width out, clips the selector
        /// into the uprights and beams (Tad, 2026-11-03). The face distance comes from the slot's LocApproachAnchor, which sits 3 m out from
        /// the face on the aisle side; where that isn't usable it falls back to PickFaceStandOff.</summary>
        private static float PickStandOff(LocationData location)
        {
            float face = PickFaceStandOff;
            var anchor = location != null ? location.transform.Find("LocApproachAnchor") : null;
            if (anchor != null)
            {
                Vector3 d = anchor.position - location.transform.position; d.y = 0f;
                float fromFace = d.magnitude - 3f;
                if (fromFace >= 0.3f && fromFace <= 1.3f) face = fromFace;
            }
            return face + RackFaceClearance;
        }
        private const float PalletSideOff    = 1.20f;  // where the selector stands to load the pallet
        private const float ForkLift         = 0.25f;  // pallet raised this high while it rides the jack (Tad, 2026-10-16)
        private const float ForkMoveSeconds  = 0.6f;   // time to raise / lower
        private const float LaneEntryMargin  = 1.0f;   // (older line-up rule, still used by the dock stocker helpers) outside the lane's last slot edge
        /// <summary>Distance from a lane's open end to its pivot point: the point the jack turns on before reversing in (Tad, 2026-11-17).</summary>
        private const float StageAnchorOut   = 2.5f;

        /// <summary>The lane's pivot point: on its centre line, <see cref="StageAnchorOut"/> past the open edge of the end slot.</summary>
        private static Vector3 LaneAnchorPoint(Vector3 endSlot, Vector3 outward, float y)
            => Flat(endSlot + outward * (StagingCellPitch * 0.5f + StageAnchorOut), y);

        // ── Picking in aisles (Tad, 2026-11-17) ──
        private const float AisleHeadingMaxDeg = 25f;   // inside an aisle a jack is never more than this off the aisle's direction
        private const float StraightHopBays    = 3f;    // next stop this many bays ahead in the SAME aisle: a straight move, not a nav leg
        private const float ParkMovedMeters    = 0.5f;  // a parked jack found this far from where it stopped has been hit: it drives back
        private const float ParkMovedDegrees   = 12f;
        private AisleLane _parkLane;                    // the aisle of the stop ComputeBayPark last worked out
        private bool _hasParkLane;
        private Vector3 _parkPos;                       // where the jack stopped when the selector stepped off to pick
        private Quaternion _parkRot;
        private bool _parkPoseSet;
        private const float MountWalkTimeout = 60f;
        private const float StagingCellPitch = 1.33f;
        private const float NavLegArriveTolerance = 3f;   // a finished nav leg must leave the jack this close to its stop
        private const int   NavLegRetries = 6;
        private const float ReverseMaxDistance = 8f;   // ~3 bays (a bay is two 1.33 m cells): shorter backward moves reverse instead of turning

        /// <summary>Pallet positions on the jack's double-length forks, local Z behind the operator anchor:
        /// first pallet at the fork TIPS (so it goes deepest into a lane), second next to the operator.
        /// A pallet's 40" side runs along the forks, so its rotation matches the jack's.</summary>
        private static readonly float[] ForkPalletZ = { -2.00f, -0.95f };
        /// <summary>Hands in the Carry_Case pose sit ~1.16 m up and 0.40 m in front of the feet (measured on the
        /// male rig), so a held case is centred there; its pivot is at its base.</summary>
        private const float HeldHandsHeight = 1.16f, HeldHandsForward = 0.40f;

        // ── Claims: one selector per jack ─────────────────────────────────────────────────────────────
        private static readonly Dictionary<MHEOperatorSlot, OrderSelectionTaskDriver> s_jackClaims = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetJackStatics() { s_jackClaims.Clear(); s_loadedIdleSince.Clear(); s_warnedNoJackDay = -1; s_warnedNoParking = false; }

        // ── Rescue of a stranded loaded jack (2026-11-07, OG2892) ─────────────────────────────────────────
        // A pallet jack can be left unmanned with a selector's pallet still aboard (its run was torn down or timed out); nothing then owns the
        // pallet, and a dock stocker only loads pallets set down on a lane floor, so the order's last pallet sat on the jack for ever. An idle
        // selector who finds such a jack (unmanned, unclaimed, a pallet of a live order aboard for 15+ real seconds) takes it over and
        // delivers: the same resume path a save/load uses (OutboundPalletPersistence), mount walk included.
        private static readonly Dictionary<MHEOperatorSlot, float> s_loadedIdleSince = new();
        private const float StrandedJackGraceSeconds = 15f;

        private bool TryRescueStrandedJack()
        {
            if (_workQueue == null || _orderService == null || _taskInProgress || _jack != null) return false;
            var self = GetComponent<EmployeeIdentity>();
            string guid = self?.Record?.employeeGuid;
            if (string.IsNullOrEmpty(guid)) return false;

            MHEOperatorSlot best = null; OrderData bestOrder = null; WorkTask bestTask = null;
            float bestD = float.MaxValue;
            foreach (var slot in FindObjectsByType<MHEOperatorSlot>())
            {
                if (slot == null || !slot.isActiveAndEnabled || !slot.IsPalletJack || slot.IsOccupied) continue;
                if (slot.GetComponent<PlacedObject>() == null) continue;   // build-menu ghost
                if (s_jackClaims.TryGetValue(slot, out var owner) && owner != null) { s_loadedIdleSince.Remove(slot); continue; }

                OutboundPalletBuilder load = null;
                foreach (var p in slot.GetComponentsInChildren<OutboundPalletBuilder>(true))
                    if (p != null && !string.IsNullOrEmpty(p.OrderId)) { load = p; break; }
                if (load == null) { s_loadedIdleSince.Remove(slot); continue; }

                if (!s_loadedIdleSince.TryGetValue(slot, out float since)) { s_loadedIdleSince[slot] = Time.unscaledTime; continue; }
                if (Time.unscaledTime - since < StrandedJackGraceSeconds) continue;

                var order = _orderService.ActiveOrders.FirstOrDefault(o => o.OrderId == load.OrderId);
                if (order == null)
                {
                    // Freight of an order that has shipped or been cancelled can never be staged: it used to sit on the jack for ever
                    // (the jack stayed "loaded", so every later run skipped it). Strip those pallets so the jack is usable again.
                    int stripped = 0;
                    foreach (var p in slot.GetComponentsInChildren<OutboundPalletBuilder>(true))
                    {
                        if (p == null || string.IsNullOrEmpty(p.OrderId)) continue;
                        string pid = p.OrderId;
                        if (_orderService.ActiveOrders.Any(o => o.OrderId == pid)) continue;   // another live order's pallet stays
                        Destroy(p.gameObject);
                        stripped++;
                    }
                    Debug.LogWarning($"[OrderSelectionTaskDriver] Pallet jack '{slot.name}' was left loaded with {stripped} pallet(s) of order {load.OrderId}, which is no longer active - removed them so the jack can work again.");
                    s_loadedIdleSince.Remove(slot);
                    continue;
                }
                // A COMPLETE select task is not work any more: only a live one (open, available, or assigned to nobody or to this selector) rescues.
                var task = _workQueue.Tasks.LastOrDefault(t => t.OrderId == order.OrderId && t.Type == WorkTaskType.OrderSelect
                                                               && t.Status != WorkTaskStatus.Cancelled && t.Status != WorkTaskStatus.Complete);
                if (task == null)
                {
                    ThrottledLog.Warn("rescue-notask-" + slot.name, $"[OrderSelectionTaskDriver] Pallet jack '{slot.name}' is left loaded for order {order.OrderNumber} with no live select task - not rescued.", 60f);
                    continue;
                }
                if (task.Status == WorkTaskStatus.Assigned && !string.IsNullOrEmpty(task.AssignedToEmployeeGuid) && task.AssignedToEmployeeGuid != guid) continue;

                float d = PlanarDist(slot.transform.position, transform.position);
                if (d < bestD) { bestD = d; best = slot; bestOrder = order; bestTask = task; }
            }
            if (best == null) return false;

            bestTask.Status = WorkTaskStatus.Assigned;
            bestTask.AssignedToEmployeeGuid = guid;
            bestTask.AssignedAtRealtime = Time.realtimeSinceStartup;
            s_loadedIdleSince.Remove(best);
            if (!ResumeJackRun(best, bestOrder, bestTask)) return false;
            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' is taking over a pallet jack left unmanned with a pallet of order {bestOrder.OrderNumber} aboard - delivering it.");
            return true;
        }

        private MHEOperatorSlot _jack;
        private AiNavigation _jackNav;
        private NavMeshAgent _jackAgent;
        private Coroutine _jackRun;
        private GameObject _heldCase;
        private (int Door, string Lane)? _jackLaneLock;
        /// <summary>The lane this jack claimed as its staging destination (InventoryService.TryClaimLane), held from the choice until it has left the mouth.</summary>
        private (int Door, string Lane)? _jackLaneClaim;
        private bool _jackLegOk;
        private float _forkY;          // current pallet height on the forks (0 = lowered)
        private bool _footScripted;    // own navigation switched off so script can walk the body precisely

        private bool OnJack => _jack != null;

        /// <summary>The jack carries at least one case (its pallets hold stock).</summary>
        private bool JackLoaded => _pallets.Exists(p => p != null && p.TotalCases > 0);
        private int _bayLegFailures;                 // consecutive failed bay legs (retried before abandoning the jack)
        private const int BayLegRetries = 5;

        /// <summary>Called right after a task is claimed. True = the jack run owns this order now.</summary>
        private bool TryStartJackRun()
        {
            // Already aboard a jack that is carrying this order's pallet (a resume after a save/load): that run has to go on
            // whether or not anything is left to pick - it may be the delivery of a finished pallet. Without this, the claim
            // returned here with the rider still mounted, and the stranded-on-a-jack watchdog dismounted them after 8 s.
            var own = OwnJackWithOrderPallet();
            // Nothing a case picker can take right now (no stock, or the rest is Reach Truck pallets): don't fetch
            // a jack for it. The on-foot path makes the exact same out-of-stock decisions, with no walk.
            if (own == null && !TryFindBestPickLocation(_currentOrder, out _, out _, out _)) return false;

            var jack = own ?? FindFreeJack();
            if (jack == null)
            {
                WarnNoJack(true);
                return false;
            }

            _jack = jack;
            _ejectedFromJack = false;
            _flatBatteryStop = false;
            _jackNav = jack.GetComponent<AiNavigation>();
            _jackAgent = jack.GetComponent<NavMeshAgent>();
            if (_jackNav != null) _jackNav.DriverOwnsMovement = true;   // this driver routes the jack: its own stuck/patrol logic stays out (cleared in ReleaseJack)
            s_jackClaims[jack] = this;
            PalletJackParkingSpot.ReleaseJack(jack);   // leaving its bay (if it was parked in one)
            _jackRun = StartCoroutine(JackOrderRoutine());
            return true;
        }

        /// <summary>Gate before claiming a pick (Tad, 2026-11-05: "she shouldn't be picking without a pallet jack"). True when this
        /// selector can get a jack (already aboard one, or one is free), or when the building has NO jack at all (the hand-pick
        /// fallback stays only so a warehouse without jacks doesn't stall). Every jack placed but busy = wait.</summary>
        private bool JackAvailableForNextPick()
        {
            bool anyJack = false;
            foreach (var s in FindObjectsByType<MHEOperatorSlot>())
                if (s != null && s.isActiveAndEnabled && s.IsPalletJack && s.GetComponent<PlacedObject>() != null) { anyJack = true; break; }
            if (!anyJack) { WarnNoJack(true); return true; }
            if (FindFreeJack() != null) return true;
            WarnNoJack(false);
            return false;
        }

        // Once per game day (it used to fire once per SESSION, so on day 5 nobody knew a 4th selector was picking by hand).
        private static int s_warnedNoJackDay = -1;
        private void WarnNoJack(bool noneAtAll)
        {
            int day = GameCore.Services.ServiceLocator.TryGet<GameCore.Economy.SimulationTimeService>(out var time) && time != null ? time.Day : 0;
            if (s_warnedNoJackDay == day) return;
            s_warnedNoJackDay = day;
            string who = GetComponent<EmployeeIdentity>()?.Record?.employeeName ?? name;
            string msg = noneAtAll
                ? "Order Selectors need a Pallet Jack - picking by hand until one is placed (Vehicles)."
                : $"More order selectors than pallet jacks - {who} is waiting for a free jack. Buy another Pallet Jack (Vehicles).";
            Debug.LogWarning("[OrderSelectionTaskDriver] " + msg);
            UIToast.Show(msg);
            SystemsLogWindow.LogWarning(msg);
        }

        /// <summary>After a save/load, a selector who was riding a jack with a pallet of this order goes straight back to
        /// that jack: the same run as a fresh claim, except the mount step is skipped (they are already aboard). The pallet
        /// on the forks is adopted by EnsurePallet, so the picked cases are not lost. Called by OutboundPalletPersistence.</summary>
        public bool ResumeJackRun(MHEOperatorSlot jack, OrderData order, WorkTask task)
        {
            if (jack == null || order == null || task == null) return false;
            if (_currentOrder != null || _jack != null) return false;   // already doing something

            _currentOrder = order;
            _currentTask = task;
            _jack = jack;
            _ejectedFromJack = false;
            _flatBatteryStop = false;
            _jackNav = jack.GetComponent<AiNavigation>();
            _jackAgent = jack.GetComponent<NavMeshAgent>();
            if (_jackNav != null) _jackNav.DriverOwnsMovement = true;
            s_jackClaims[jack] = this;
            // Same state a fresh claim sets (see Update): without _taskInProgress the next poll takes this driver for idle and
            // TryClaimNextOrderTask overwrites the resumed order with null, which ended the run at once.
            _taskInProgress = true;
            _pickFloorKey = long.MinValue;
            _nav.SetTaskBusy(true);
            PalletJackParkingSpot.ReleaseJack(jack);
            _jackRun = StartCoroutine(JackOrderRoutine());
            return true;
        }

        /// <summary>The pallet jack this selector is already riding that holds a pallet of the current order, or null.</summary>
        private MHEOperatorSlot OwnJackWithOrderPallet()
        {
            if (_currentOrder == null) return null;
            var self = GetComponent<EmployeeIdentity>();
            if (self == null) return null;
            foreach (var jack in FindObjectsByType<MHEOperatorSlot>())
            {
                if (jack == null || !jack.isActiveAndEnabled || !jack.IsPalletJack || jack.CurrentOperator != self) continue;
                if (s_jackClaims.TryGetValue(jack, out var holder) && holder != null && holder != this) continue;
                foreach (var p in jack.GetComponentsInChildren<OutboundPalletBuilder>(true))
                    if (p != null && p.OrderId == _currentOrder.OrderId) return jack;
            }
            return null;
        }

        private MHEOperatorSlot FindFreeJack()
        {
            // Already aboard a jack (a selector resumed after a save/load, or a rider who was left on one): that jack is theirs.
            // Without this their own occupied jack looked taken and they picked by hand with the pallet still on the forks.
            var self = GetComponent<EmployeeIdentity>();
            if (self != null)
                foreach (var own in FindObjectsByType<MHEOperatorSlot>())
                    if (own != null && own.isActiveAndEnabled && own.IsPalletJack && own.CurrentOperator == self
                        && !(s_jackClaims.TryGetValue(own, out var holder) && holder != null && holder != this))
                    {
                        // A flat jack is never claimed for a new pick, not even the one they are sitting on (2026-11-07, Jose King): it goes
                        // to a charger first (EquipmentBattery drives it there with them aboard). Picking anything else while still riding
                        // it would walk them off with the jack attached, so: no jack for now.
                        var ownBatt = own.GetComponent<EquipmentBattery>();
                        if (ownBatt != null && (ownBatt.IsDead || ownBatt.NeedsCharge)) return null;
                        return own;
                    }

            MHEOperatorSlot best = null;
            float bestD = float.MaxValue;
            foreach (var slot in FindObjectsByType<MHEOperatorSlot>())
            {
                if (slot == null || !slot.isActiveAndEnabled || !slot.IsPalletJack) continue;
                // A jack can be left listing a selector as its operator long after they walked away (a run torn down without the vacate).
                // Everyone then saw it as occupied, found no free jack and picked by hand with pallets on their back. If nobody claims it
                // and its "operator" isn't physically on it, the link is stale: clear it and the jack is free again.
                if (slot.IsOccupied)
                {
                    var op = slot.CurrentOperator;
                    bool claimed = s_jackClaims.TryGetValue(slot, out var claimant) && claimant != null;
                    bool physicallyOn = op != null && op.transform.IsChildOf(slot.transform);
                    if (claimed || physicallyOn) continue;
                    Debug.LogWarning($"[OrderSelectionTaskDriver] Pallet jack still listed '{(op != null ? op.name : "?")}' as its operator though nobody is on it or running it - clearing the stale link.");
                    slot.VacateOperator();
                }
                if (slot.GetComponent<AiNavigation>() == null || slot.GetComponent<PlacedObject>() == null) continue; // build-menu ghost
                if (s_jackClaims.TryGetValue(slot, out var owner) && owner != null && owner != this) continue;
                // Battery: a dead jack stays on its charger; a low one (under an hour left) only if nothing better is free.
                var batt = slot.GetComponent<EquipmentBattery>();
                if (batt != null && (batt.IsDead || batt.NeedsCharge)) continue;   // flat, or on its way to / sitting on a charger
                if (batt != null && batt.VacatingBay) continue;                     // someone is being sent to drive it to a station
                float d = PlanarDist(slot.transform.position, transform.position);
                if (batt != null && batt.HoursLeft <= EquipmentBattery.LowHours) d += 1000f;
                if (d < bestD) { bestD = d; best = slot; }
            }
            return best;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // The run
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        private IEnumerator JackOrderRoutine()
        {
            var identity = GetComponent<EmployeeIdentity>();
            float mountStart = Time.time;   // time study: claim until aboard with the forks up (OpsMetrics.JackMount)

            // 1. Walk to the jack's nearer mount point (MountLeft / MountRight), then step in. Skipped when this selector
            // is already aboard (a resume after a save/load): the mount is done, only the pallet is still to adopt.
            bool alreadyAboard = _jack != null && _jack.CurrentOperator == identity;
            if (!alreadyAboard)
            {
                bool reached = false;
                _nav.CancelSeekPosition();
                _nav.SeekPosition(MountPointNear(transform.position), () => reached = true);
                float waited = 0f;
                while (!reached && waited < MountWalkTimeout)
                {
                    if (_jack == null || _jack.IsOccupied) break;
                    waited += Time.deltaTime;
                    yield return null;
                }
                if (!reached || _jack == null || _jack.IsOccupied)
                {
                    Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' could not get to pallet jack — picking order {_currentOrder.OrderId} by hand.");
                    ReleaseJack();
                    AdvanceToNextPick();
                    yield break;
                }
                _nav.CancelSeekPosition();

                // Scripted from here so the step-in is exact: mount point → turn to the operator spot → step in.
                BeginScriptedFoot();
                yield return StepIn();
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }
                _jack.AssignOperator(identity);
                _footScripted = false;   // the MHE slot owns the body's navigation state while riding
            }
            // The run drives this jack now, by script: its own AiNavigation must be off. A RESUMED run (already aboard after a save/load)
            // used to skip this, so the jack's agent kept patrolling by itself - carrying the selector off, or leaving them walking behind
            // it while it drove through whatever was in the way (2026-11-07, three resumed selectors on OG3901).
            SetJackDrive(false, alreadyAboard ? "resumed run" : "selector mounted");
            // Empty pallets come from the chep stacks now, two per jack (OrderSelectionTaskDriver.ChepStack.cs). A resumed run that still
            // has its pallet on the forks takes that over instead.
            if (_pallets.Count == 0 && !AdoptPalletOnJack())
            {
                bool fetched = false;
                yield return FetchEmptiesFromChepStack(r => fetched = r);
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }
                if (!fetched && _offJack)
                {
                    yield return StepIn();
                    if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }
                    _jack.StepOn();
                    _offJack = false;
                }
                if (!fetched) SpawnSpareChep();
            }
            EnsurePallet();
            _offJack = false; _parkedBay = null; _parkedPalletIndex = -1;
            yield return MoveForks(ForkLift);
            OpsMetrics.Record(OpsMetrics.Kind.JackMount, Time.time - mountStart, 1, (int)EmployeeRole.OrderSelector);

            // 2. Pick, slot by slot.
            OrderData.OrderStatus completion = OrderData.OrderStatus.Staged;
            while (true)
            {
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }

                // The order was cleared under this run (returned to Open, or the trip was torn down): stop, don't dereference it.
                if (_currentOrder == null) { ReleaseJack(); yield break; }

                // Route closed mid-pick: put it all back (OrderSelectionTaskDriver.Returns.cs).
                if (ReturnsRequested) { yield return MidPickReturnRoutine(); yield break; }

                // Flat battery: climb off; the jack stages what it already has by itself, the rest of the order
                // becomes another trip (FinishOrderCleanup files it) for a selector with a charged jack.
                if (JackOutOfPower)
                {
                    _flatBatteryStop = true;   // stay aboard: stage, park in a charging bay, step off
                    if (!_currentOrder.IsFullyPicked) { MarkPartialUnlessLoading(_currentOrder); _cubingCapHit = true; }
                    completion = OrderData.OrderStatus.Staged;
                    break;
                }

                if (!_currentOrder.IsFullyPicked && PlanTripDone())
                {
                    Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) pallet {_currentTask.PlanNumber} of {_currentTask.PlanTotal} built on the pallet jack.");
                    if (TryJoinSameCustomerPlan()) continue;   // jack has room and another same-customer plan is waiting
                    Debug.Log("Driving to staging.");
                    break;
                }

                if (_currentOrder.IsFullyPicked)
                {
                    if (HelperNotNeeded()) break;   // nothing of ours aboard: JackStageRoutine just parks + finishes
                    // Say WHY the trip ended when this pallet's own pick list still owes cases (Tad, 2026-11-22: a selector "walked away with
                    // 3 cases left" - the order total was reached, so the leftover plan lines were credited to another pallet / selector).
                    if (_currentTask != null && _currentTask.HasPlan && !_orderService.PlanComplete(_currentTask))
                    {
                        var owed = new System.Text.StringBuilder();
                        foreach (var kv in _currentTask.PlanLines())
                        {
                            int rem = _orderService.PlanRemaining(_currentTask, kv.Key);
                            if (rem > 0) owed.Append(kv.Key).Append(" x").Append(rem).Append(' ');
                        }
                        var lines = new System.Text.StringBuilder();
                        foreach (var li in _currentOrder.LineItems)
                            if (li != null) lines.Append(li.SkuId).Append(' ').Append(li.QuantityPicked).Append("/").Append(li.QuantityNeeded).Append("  ");
                        Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': order {_currentOrder.OrderNumber} is fully picked by line totals, but this pallet's pick list still owes: {owed}" +
                                         $"(the order's lines picked/ordered: {lines}) - the order total was reached through other pallets / selectors, so nothing more is taken.");
                    }
                    Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) fully picked on the pallet jack — {TotalCasesOnPallets()} case(s). Driving to staging.");
                    _orderService.MarkOrderFulfilled(_currentOrder.OrderId);
                    break;
                }

                if (!TryFindBestPickLocation(_currentOrder, out var location, out var lineItem, out int takeQty))
                {
                    // Everything left is being fetched by other selectors on this order.
                    if (HelperNotNeeded()) break;
                    // Same exits as AdvanceToNextPick, in the same order.
                    if (_orderService.RemainingIsAllPalletPick(_currentOrder))
                    {
                        completion = OrderData.OrderStatus.PartiallyPicked;
                        break;
                    }
                    if (TotalCasesOnPallets() > 0)
                    {
                        LogShortTrip(_currentOrder);
                        break;
                    }
                    if (_currentTask != null && _currentTask.HasPlan && _currentOrder.TotalUnitsPicked < _currentOrder.TotalUnits)
                    {
                        // Every pick face is empty: give the planned pallet back instead of completing it empty (see HandPlanTaskBack).
                        DestroyEmptyPallets();
                        ReleaseJack();
                        HandPlanTaskBack();
                        yield break;
                    }
                    if (_orderService.HasLivePalletPicks(_currentOrder))
                    {
                        completion = OrderData.OrderStatus.PartiallyPicked;
                        break;
                    }
                    // Nothing picked at all: put the jack back and hand the order back to the player.
                    Debug.LogWarning($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) has no reachable pick location for any line item and nothing was picked — returning it to Open.");
                    DestroyEmptyPallets();
                    ReleaseJack();
                    ReturnOrderToOpen();
                    yield break;
                }

                // The fork position of the pallet being built (front = 0): the jack parks so THAT pallet is level with the slot.
                int palletIndex = ForkIndexOf(ActivePallet());
                if (palletIndex < 0) palletIndex = Mathf.Clamp(_pallets.Count - 1, 0, ForkPalletZ.Length - 1);
                if (!TryGetPickGeometry(location, out Vector3 slotFlat, out Vector3 toAisle))
                {
                    yield return AbandonJackOnFoot($"pick slot {location.Address} has no usable position");
                    yield break;
                }

                // BAY STOPS (Tad, 2026-10-17): the jack parks ONCE per bay, at the bay's first position, and the selector
                // walks to every slot they need in that bay before the jack moves on. Same bay + same pallet = stay off.
                string bayKey = BayKey(location.Address);

                // Never pick from a slot more than MaxPickReach from the pallet: if the parked jack is too far away, re-park
                // right in front of THIS slot instead of walking the cases across the warehouse (Tad, 2026-10-21).
                var builtPallet = ActivePallet();
                bool slotTooFar = _offJack && builtPallet != null &&
                                  PlanarDist(builtPallet.transform.position, slotFlat) > MaxPickReach;
                if (slotTooFar)
                    Debug.Log($"[OrderSelectionTaskDriver] '{name}': {location.Address} is more than {MaxPickReach:0}m from the parked jack - re-parking in front of it.");
                if (slotTooFar || !(_offJack && bayKey == _parkedBay && palletIndex == _parkedPalletIndex))
                {
                    if (_offJack)
                    {
                        yield return StepIn();
                        if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }
                        _jack.StepOn();
                        _offJack = false;
                    }
                    Vector3 bayFlat = slotTooFar ? slotFlat : BayFirstPositionFlat(location, slotFlat);
                    float bayMoveStart = OpsMetrics.Now;
                    ComputeBayPark(bayFlat, toAisle, location, palletIndex, out Vector3 park, out Vector3 parkHeading);
                    yield return DriveBayLeg(park, parkHeading, $"bay {bayKey}", slotTooFar);
                    if (_jackLegOk) OpsMetrics.Record(OpsMetrics.Kind.JackBayMove, OpsMetrics.Now - bayMoveStart);
                    if (!_jackLegOk)
                    {
                        // Carrying on BY HAND glues the outbound pallets to the avatar (Tad, 2026-11-10: pallet parented to Ronald Wright and
                        // dragged down the pick path). A failed leg is usually traffic: wait a moment and try again before giving up.
                        if (++_bayLegFailures <= BayLegRetries)
                        {
                            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': could not drive the jack to bay {bayKey} (try {_bayLegFailures}/{BayLegRetries}) - waiting, then trying again.");
                            yield return new WaitForSecondsRealtime(2f);
                            continue;
                        }
                        _bayLegFailures = 0;
                        // NEVER carry on by hand from here (block 2026-11-10 run 2: five failed legs, then the pallets were parented to
                        // Ronald Wright again). Something on the forks: drive what we have to staging (a short pick, the rest is re-filed
                        // like any short pick). Nothing yet: park the jack and hand the task back.
                        if (TotalCasesOnPallets() > 0)
                        {
                            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': gave up reaching bay {bayKey} after {BayLegRetries} tries - staging the {TotalCasesOnPallets()} case(s) already on the jack.");
                            completion = OrderData.OrderStatus.PartiallyPicked;
                            break;
                        }
                        GiveUpTripNothingPicked($"could not reach bay {bayKey} after {BayLegRetries} tries");
                        yield break;
                    }
                    _bayLegFailures = 0;
                    if (JackOutOfPower) continue;   // died on the way here: handled at the top of the loop

                    {
                        // VERIFY THE STOP (Tad, 2026-11-03): the first stop of a bay is never run through the reach check above, and DriveJackTo
                        // leaves the jack where it is when the stop is "behind" it (never back up), so the selector walked up to ~10 m with every
                        // case. If the pallet still isn't within reach of the slot, pull up (reversing if need be) in front of the slot itself.
                        if (!slotTooFar && builtPallet != null &&
                            PlanarDist(builtPallet.transform.position, slotFlat) > MaxPickReach)
                        {
                            Debug.Log($"[OrderSelectionTaskDriver] '{name}': jack stopped {PlanarDist(builtPallet.transform.position, slotFlat):0.0}m from {location.Address} - re-parking in front of it.");
                            ComputeBayPark(slotFlat, toAisle, location, palletIndex, out park, out parkHeading);
                            yield return DriveBayLeg(park, parkHeading, $"bay {bayKey} (reach)", true);
                            if (!_jackLegOk)
                            {
                                if (++_bayLegFailures <= BayLegRetries)
                                {
                                    Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': could not re-park the jack at bay {bayKey} (try {_bayLegFailures}/{BayLegRetries}) - waiting, then trying again.");
                                    yield return new WaitForSecondsRealtime(2f);
                                    continue;
                                }
                                _bayLegFailures = 0;
                                if (TotalCasesOnPallets() > 0)
                                {
                                    Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': gave up re-parking at bay {bayKey} after {BayLegRetries} tries - staging the {TotalCasesOnPallets()} case(s) already on the jack.");
                                    completion = OrderData.OrderStatus.PartiallyPicked;
                                    break;
                                }
                                GiveUpTripNothingPicked($"could not re-park at bay {bayKey} after {BayLegRetries} tries");
                                yield break;
                            }
                            if (JackOutOfPower) continue;
                        }
                        // Off the jack on the rack side (via that side's mount point).
                        yield return StepOut(slotFlat);
                    }
                    _offJack = true;
                    _parkedBay = bayKey;
                    _parkedPalletIndex = palletIndex;
                    if (_jack != null) { _parkPos = _jack.transform.position; _parkRot = _jack.transform.rotation; _parkPoseSet = true; }   // where a hit jack drives back to
                }

                yield return PickCasesFromSlot(location, lineItem, takeQty, slotFlat, toAisle);
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }

                if (_capReached)
                {
                    _capReached = false;
                    Debug.Log($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) — both pallets on the jack are full ({_currentOrder.TotalUnitsPicked}/{_currentOrder.TotalUnits}). Driving to staging.");
                    MarkPartialUnlessLoading(_currentOrder);
                    completion = OrderData.OrderStatus.Staged;
                    _cubingCapHit = true;
                    break;
                }
            }

            // Back on the jack before driving to staging (picking leaves the selector off it, at the last bay).
            // Not after a flat battery: the jack drives to staging alone.
            if (_offJack && _jack != null && !_ejectedFromJack)
            {
                yield return StepIn();
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared"); yield break; }
                _jack.StepOn();
                _offJack = false;
            }

            // 3. Stage.
            float stageStart = OpsMetrics.Now;
            yield return JackStageRoutine(completion);
            OpsMetrics.Record(OpsMetrics.Kind.JackStaging, OpsMetrics.Now - stageStart);
        }

        /// <summary>The NavMeshAgent only plans the open-floor legs and stops short of the target; the last metres of a
        /// bay stop are scripted straight onto the park pose, and the agent's radius covers its centre, not the 2.5 m of
        /// body + forks. So a pick at the end of an aisle drove the jack's body through the wall. Pull the park pose
        /// away from any navmesh edge that the body's rear / sides would touch.</summary>
        private static Vector3 KeepJackBodyOffWalls(Vector3 park, Vector3 heading)
        {
            const float Margin = 0.5f;
            Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
            for (int pass = 0; pass < 4; pass++)
            {
                Vector3 push = Vector3.zero;
                foreach (Vector3 probe in new[] { park - heading * 1.4f, park - heading * 0.7f, park - heading * 1.0f + right * 0.45f, park - heading * 1.0f - right * 0.45f })
                {
                    if (!UnityEngine.AI.NavMesh.SamplePosition(probe, out var onMesh, 1.5f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                    if (UnityEngine.AI.NavMesh.FindClosestEdge(onMesh.position, out var edge, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        float off = Vector3.Distance(probe, onMesh.position);   // probe already outside the mesh
                        float need = Margin - edge.distance + off;
                        if (need > 0.02f) { Vector3 n = edge.normal; n.y = 0f; push += n.normalized * need; }
                    }
                }
                if (push.sqrMagnitude < 0.0004f) break;
                park += push * 0.6f;
            }
            return park;
        }

        /// <summary>The jack's park pose for a stop whose bay line starts at <paramref name="bayFlat"/>: out in the aisle, forks aligned
        /// with the pallet being loaded, turned out of a dead end, kept off walls and other jacks.</summary>
        private void ComputeBayPark(Vector3 bayFlat, Vector3 toAisle, LocationData location, int palletIndex, out Vector3 park, out Vector3 parkHeading)
        {
            // PICK STOP (Tad, 2026-11-18): the jack pulls up beside the pick face, parallel to the aisle and facing the aisle's chevron
            // direction, its side MhePlanner.FaceGap (0.5 m) off the rack face, with the pallet being loaded level with the slot. The
            // selector works in that gap: take a case off the face, turn round, set it on the pallet.
            _hasParkLane = false;
            Vector3 heading = PickHeading(toAisle, location);
            _aisleHeading = heading;   // straight down the aisle: what a queue stop squares up to (SquareUpInAisle)
            park = bayFlat + toAisle * ParkOffset - heading * ForkPalletZ[palletIndex];

            // End of the aisle: the jack would be left nose-first into the wall. Turn it ~45 degrees out toward the open
            // aisle instead (Tad, 2026-10-18: "turning towards the direction of travel, not driving into the wall").
            parkHeading = heading;
            if (UnityEngine.AI.NavMesh.Raycast(park + Vector3.up * 0.05f, park + heading * 2.5f, out _, UnityEngine.AI.NavMesh.AllAreas))
                parkHeading = (heading + toAisle).normalized;
            park = KeepJackBodyOffWalls(park, parkHeading);
                    }

        private bool _capReached;
        private bool _offJack;            // selector standing at a bay, jack parked
        private string _parkedBay;        // "AA-BB" of the bay the jack is parked at
        private int _parkedPalletIndex = -1;

        /// <summary>"AA-BB" part of a slot address: every slot in one bay shares it.</summary>
        /// <summary>Where the jack stops (Tad, 2026-10-18): one stop per PAIR of bays facing each other across the aisle
        /// (01 and 02, 03 and 04...). The selector picks every needed slot of both bays on foot, stepping across the
        /// aisle, then the jack pulls FORWARD to the next pair that has a needed slot. Odd bays are one side, even bays the other.</summary>
        private static string BayKey(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 5) return address;
            if (!int.TryParse(address.Substring(0, 2), out int aisle) || !int.TryParse(address.Substring(3, 2), out int bay)) return address.Substring(0, 5);
            return $"{aisle:00}-{(bay + 1) / 2:00}";
        }

        /// <summary>Where the bay starts in walking direction (position 0 in an odd aisle, position 1 in an even one, the
        /// serpentine sequence's first slot of the bay), at the same level as the pick. Falls back to the pick slot.</summary>
        private static Vector3 BayFirstPositionFlat(LocationData pick, Vector3 pickFlat)
        {
            var a = pick != null ? pick.Address : null;
            if (string.IsNullOrEmpty(a) || a.Length < 7 || !int.TryParse(a.Substring(0, 2), out int aisle)) return pickFlat;
            char firstPos = '0';   // every aisle ascends: the bay's first slot in walking direction is always position 0
            string first = a.Substring(0, 7) + firstPos;
            if (first == a || !LocationRegistry.TryGet(first, out var loc)) return pickFlat;
            return TryGetPickGeometry(loc, out var f, out _) ? f : pickFlat;
        }

        /// <summary>The round trips between the slot and the pallet. Stock and pick counts change at the moment
        /// each case lands on the pallet, so an interrupted trip never counts a case that isn't there.</summary>
        private IEnumerator PickCasesFromSlot(LocationData location, OrderLineItem lineItem, int takeQty, Vector3 slotFlat, Vector3 toAisle)
        {
            var sku = _inventoryService.GetSkuData(lineItem.SkuId);
            if (sku == null || sku.Prefab == null)
            {
                Debug.LogWarning($"[OrderSelectionTaskDriver] SKU {lineItem.SkuId} missing SkuData/prefab — skipping this pick at {location.Address}.");
                yield break;
            }
            Vector3 caseDim = new Vector3(sku.CaseWidth, sku.CaseHeight, sku.CaseLength);
            int ti = Mathf.Max(1, sku.Ti);
            int hi = Mathf.Max(1, sku.Hi);

            float y = transform.position.y;
            Vector3 faceSpot = Flat(slotFlat + toAisle * PickStandOff(location), y);
            // Jack parked at the face (Tad, 2026-11-18): the selector works in the 0.5 m gap between the rack face and the pallet.
            if (_metTripStart < 0f) _metTripStart = OpsMetrics.Now;   // the trip time study (the jack path never started it, so it logged almost nothing)

            for (int i = 0; i < takeQty; i++)
            {
                if (_jack == null || ReturnsRequested) yield break;
                yield return RecoverIfMovedFromPark();   // hit by something since the last case: drive back to the stop first
                if (_jack == null) yield break;
                float caseStart = OpsMetrics.Now;

                // To the slot, reach in, take a case. Round the jack's end if it stands in between (never through it).
                yield return WalkRoundJack(faceSpot);
                yield return FaceDir(-toAisle);
                // Cases in the back half of the slot: lean in to reach them.
                bool reach = NextPickIsDeep(location, slotFlat, toAisle);
                if (reach) SetLean(1f);
                yield return new WaitForSeconds(GrabSeconds + (reach ? 0.3f : 0f));
                // The case leaves the slot (top layer first) the moment it appears in their hands.
                string facePalletId = location.PalletId;
                if (!string.IsNullOrEmpty(facePalletId))
                    _inventoryService.PickFromPallet(facePalletId, 1);
                location.Pick(1);
                RemoveCaseFromPickFace(location, facePalletId);
                SpawnHeldCase(sku.Prefab, caseDim.y);
                SetLean(0f);
                SetAnimBool("CarryCase", true);

                // Turn round, carry it to the pallet, set it down.
                var activePallet = ActivePallet();
                if (activePallet == null) { DestroyHeldCase(); yield break; }
                Vector3 palletPos = activePallet.transform.position;
                Vector3 palletSide = Flat(palletPos - toAisle * (ParkOffset - PalletSideOff), y);
                yield return WalkRoundJack(palletSide);
                yield return FaceDir(Flat(palletPos - transform.position, 0f));
                yield return new WaitForSeconds(PlaceSeconds);

                DestroyHeldCase();
                SetAnimBool("CarryCase", false);

                lineItem.QuantityPicked++;
                _orderService.NotePickedCase(_currentTask?.TaskId, lineItem.SkuId);
                activePallet.AddCase(lineItem.SkuId, sku.Prefab, caseDim, ti, hi, location.Address);
                _metTripCases++;
                OpsMetrics.Record(OpsMetrics.Kind.CaseCycle, OpsMetrics.Now - caseStart);

                if (_currentOrder.IsFullyPicked) yield break;

                // A planned pallet is finished when its planned cases are on it (the loop above then drives it to staging).
                if (_currentTask != null && _currentTask.HasPlan)
                {
                    if (PlanTripDone()) yield break;
                    continue;
                }

                if (activePallet.IsCubedOut)
                {
                    if (_pallets.Count < MaxPalletsPerOrder)
                    {
                        StartNextPallet();
                        // The next pallet sits elsewhere on the forks — finish this line from the next visit,
                        // when the jack re-parks with the new pallet level with the slot.
                        yield break;
                    }
                    _capReached = true;
                    yield break;
                }

            }
        }

        /// <summary>Lane resolve, drive to the lane's open end, back in, leave the pallets, drive out.</summary>
        private IEnumerator JackStageRoutine(OrderData.OrderStatus completion)
        {
            // A joined trip stages into the anchor's lane, so the anchor is the trip's order/task for staging.
            var order = _anchorOrder ?? _currentOrder;
            var task = _anchorTask ?? _currentTask;
            _currentOrder = order;
            _currentTask = task;

            DestroyEmptyPallets();
            if (LivePalletCount() == 0)
            {
                // Nothing physical to stage (the rest of the order is pallet picks, or this was a helper trip the
                // other selectors finished first): park the jack and finish. A helper leaves the order's status alone.
                if (_orderService.OtherSelectorsOnOrder(order, task) == 0)
                    order.Status = TripEndStatus(order, completion, task);
                if (!_ejectedFromJack && !_flatBatteryStop) _cubingCapHit = false;   // a flat-battery stop still owes the rest of the order a trip
                if (_jack != null && !_ejectedFromJack)
                {
                    yield return ParkJack();
                    yield return StayAboardWhileCharging();
                    if (_jack != null) yield return StepOut(_jack.transform.position - _jack.transform.right);
                }
                FinishJoinedTrips(0, null, completion);
                ReleaseJack();
                FinishOrderCleanup(task);
                yield break;
            }

            // From here the jack is loaded and on its way to stage: the no-progress watchdog no longer abandons it (see CheckNoProgress).
            _stagingLoaded = true;

            // Lane + slot, retrying while every lane is full (the selector waits on the jack).
            string resolvedLane = null;
            LaneNamingService.LaneSlot slot = default;
            Vector3 slotPos = default, depthAxis = Vector3.forward;
            bool warned = false;
            while (true)
            {
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared", completion); yield break; }
                string fail;
                if (TryResolveStagingTarget(order, out resolvedLane, out slot, out slotPos, out depthAxis, out fail))
                {
                    // CLAIM the lane now, before the drive to the mouth (InventoryService.TryClaimLane). Nothing yields between the
                    // resolve and this claim, so the slot is still ours. While another machine holds the lane we wait and re-resolve.
                    if (_inventoryService.TryClaimLane(slot.DoorNumber, resolvedLane, _jack,
                            InventoryService.VehicleLabel(_jack.transform, "pallet jack", "staging")))
                    {
                        _jackLaneClaim = (slot.DoorNumber, resolvedLane);
                        break;
                    }
                    fail = $"lane {slot.DoorNumber}{resolvedLane} is claimed by another machine";
                }
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning($"[OrderSelectionTaskDriver] Order {order.OrderId} ({order.CustomerName}) can't stage yet — {fail}. Waiting on the pallet jack.");
                    UIToast.Show($"Order Selector can't stage order for {order.CustomerName} - no free staging lane. Waiting to retry.");
                }
                yield return new WaitForSeconds(StagingRetryInterval);
            }

            int door = slot.DoorNumber;   // may be a neighbouring door's lane (overflow)
            if (!LaneNamingService.TryGetLaneGeometry(door, resolvedLane, out var geo))
            {
                yield return AbandonJackOnFoot($"lane {door}{resolvedLane} has no geometry", completion);
                yield break;
            }
            Vector3 axis = geo.DepthAxis.sqrMagnitude > 0.0001f ? geo.DepthAxis.normalized : depthAxis;

            // Normal approach: from the far (open-floor) end, facing AWAY from the door, forks toward it, backing
            // toward the door. Never drive through freight: if anything stands between the open end and the target
            // slot, come in from the door end instead (facing the door, backing away from it); if both ends are
            // blocked, don't force it — the old placement takes over.
            float driveY = _jack.transform.position.y;
            var tipGo = _pallets.FirstOrDefault(p => p != null)?.gameObject;
            // EXIT END ONLY (Tad, 2026-11-14, block 59): a jack never stages from the door end - that is the dock stockers' side.
            // OFF (StageFromExitEndOnly) while isolating the block 58-60 jams; off = block 57 behaviour (door end when the exit side is blocked).
            bool farBlocked = LaneSlotsBlocked(door, resolvedLane, s => s > slot.Slot, tipGo);
            if (StageFromExitEndOnly && farBlocked)
            {
                ThrottledLog.Warn("stage-exit-" + name, $"[OrderSelectionTaskDriver] '{name}': freight now stands between the exit end of lane {door}{resolvedLane} and slot {slot.Name} - not staging from the dock end; resolving again.", 30f);
                ReleaseJackLaneLock();   // hand back this claim: the retry claims whichever lane it resolves to
                yield return new WaitForSeconds(StagingRetryInterval);
                yield return JackStageRoutine(completion);
                yield break;
            }
            if (!StageFromExitEndOnly && farBlocked && LaneSlotsBlocked(door, resolvedLane, s => s < slot.Slot, tipGo))
            {
                yield return AbandonJackOnFoot($"lane {door}{resolvedLane} has freight on both sides of slot {slot.Name}", completion);
                yield break;
            }
            if (StageFromExitEndOnly) farBlocked = false;
            Vector3 forward = farBlocked ? -axis : axis;

            // THE LANE'S PIVOT POINT (Tad, 2026-11-17): an invisible anchor on the centre line of the lane, StageAnchorOut (2.5 m) out from
            // its open end. The jack drives there on its NavMeshAgent, turns on the spot to face away from the lane, reverses in until the
            // pallet on the forks sits on the target cell's centre, lowers it, raises the forks and drives straight forward back to this
            // same point, where its own navigation takes over again.
            var laneSlots = LaneNamingService.GetLane(door, resolvedLane);
            Vector3 endSlot = Vector3.zero;
            bool haveEnd = laneSlots.Count > 0 &&
                           LaneNamingService.TryGetSlotWorldPos((farBlocked ? laneSlots[0] : laneSlots[laneSlots.Count - 1]).Cell, out endSlot);
            if (!haveEnd) endSlot = farBlocked ? geo.EntryPoint : geo.ExitPoint - axis * 2f;
            Vector3 mouth = LaneAnchorPoint(endSlot, forward, driveY);

            s_stagingClaims[this] = mouth;
            // Close the lane mouth to every other machine from now until this jack has pulled out to the pivot again (MheLaneZone).
            MheLaneZone.Hold(_jack, mouth, forward);

            // BACK IN, NEVER NOSE IN (Tad, 2026-11-14, block 57 screenshot): the jack must arrive at the line-up facing AWAY from the lane.
            // Then it is checked, and a jack still pointing into the lane goes round again rather
            // than "reversing" in forwards (which is what happened when the 180-degree turn at the mouth was stopped by the clearance guard).
            // OFF (LineUpFacingOut) while isolating the block 58-60 jams: off = block 57 behaviour, one line-up drive and back in.
            bool linedUp = !LineUpFacingOut;
            if (!LineUpFacingOut)
            {
                // ONE target: the lane mouth (just outside the open end, on the lane's centre line). The NavMeshAgent stops there, the jack
                // turns on the spot to face away from the lane, then backs in. A leg that stalls in traffic is retried here instead of
                // dropping a loaded jack: the claim on the lane is held the whole time.
                for (int leg = 0; leg < StageLegAttempts; leg++)
                {
                    // To the pivot point with the jack's CENTRE on it (it turns about its centre there), any heading.
                    yield return DriveJackCentreTo(mouth, $"staging lane {door}{resolvedLane} pivot point");
                    if (_jack == null || _jackLegOk) break;
                    if (leg + 1 < StageLegAttempts)
                    {
                        Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': could not reach the mouth of lane {door}{resolvedLane} (try {leg + 1}/{StageLegAttempts}) - waiting, then trying again.");
                        yield return new WaitForSecondsRealtime(2f);
                    }
                }
                // Never reverse in nose-first: at the pivot the jack must face away from the lane. One more turn if it is off; a jack that
                // still is not facing out is given up (below) rather than slid in the wrong way round.
                if (_jack != null && _jackLegOk && Vector3.Angle(Flat(_jack.transform.forward, 0f), forward) > StageLineUpMaxDeg)
                    yield return JackFace(forward);
                // On the lane's centre line before reversing in (the lanes are 1.33 m apart, the jack 1.22 m wide): off it = once more.
                if (_jack != null && _jackLegOk)
                {
                    float off = Mathf.Abs(Vector3.Cross(forward.normalized, Flat(_jack.transform.position - mouth, 0f)).y);
                    if (off > 0.15f)
                    {
                        Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': {off:0.00} m off the centre line of lane {door}{resolvedLane} at the pivot - lining up again.");
                        yield return DriveJackCentreTo(mouth, $"staging lane {door}{resolvedLane} pivot point (again)");
                        if (_jack != null && _jackLegOk) yield return JackFace(forward);
                    }
                }
                linedUp = _jack != null && Vector3.Angle(Flat(_jack.transform.forward, 0f), forward) <= StageLineUpMaxDeg;
            }
            for (int attempt = 0; LineUpFacingOut && attempt < StageLineUpAttempts && _jack != null && !linedUp; attempt++)
            {
                if (attempt > 0) yield return DriveJackTo(mouth + forward * StageLineUpLoop, forward, $"staging lane {door}{resolvedLane} line-up (again)");
                yield return DriveJackTo(mouth, forward, $"staging lane {door}{resolvedLane} line-up");
                if (!_jackLegOk || _jack == null) break;
                if (Vector3.Angle(Flat(_jack.transform.forward, 0f), forward) > StageLineUpMaxDeg) yield return JackFace(forward);
                linedUp = _jack != null && Vector3.Angle(Flat(_jack.transform.forward, 0f), forward) <= StageLineUpMaxDeg;
                if (!linedUp && _jack != null)
                    ThrottledLog.Warn("stage-lineup-" + name, $"[OrderSelectionTaskDriver] '{name}': not facing out of staging lane {door}{resolvedLane} at the line-up ({Vector3.Angle(Flat(_jack.transform.forward, 0f), forward):0} degrees off) - going round again, not driving in nose first.", 30f);
            }
            if (!_jackLegOk || !linedUp)
            {
                s_stagingClaims.Remove(this);
                MheLaneZone.Release(_jack);
                yield return AbandonJackOnFoot(linedUp ? $"could not drive the jack to staging lane {door}{resolvedLane}"
                                                       : $"could not line the jack up facing out of staging lane {door}{resolvedLane}", completion);
                yield break;
            }

            // One machine in a lane at a time (same lock as the reach trucks and dock stockers).
            // Our own claim is what keeps the lane free for us; this succeeds at once unless the jack has gone (the claim is then dead too).
            while (_jack != null && !_inventoryService.TryEnterLaneForDelivery(door, resolvedLane, InventoryService.VehicleLabel(_jack.transform, "pallet jack", "staging"), _jack))
                yield return null;
            if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared", completion); yield break; }
            _jackLaneLock = (door, resolvedLane);

            // Back in until the tip pallet is over the target slot. If that slot already holds a pallet (the order
            // is stacking onto it), stop one cell short — a pallet jack can't lift onto a stack, the drop snaps it.
            Vector3 dropAt = Flat(slotPos, driveY);
            if (CellHasFreight(door, resolvedLane, slot.Cell, tipGo)) dropAt += forward * StagingCellPitch;
            Vector3 backTo = dropAt - forward * ForkPalletZ[0];
            yield return JackMoveStraight(backTo, JackReverseSpeed);

            // Lower and leave them.
            yield return MoveForks(0f);
            PlacePalletsAtStagingSlot(order, door, resolvedLane, slotPos, axis);
            order.Status = TripEndStatus(order, completion, task);
            FinishJoinedTrips(door, resolvedLane, completion);
            _pallets.Clear();
            yield return MoveForks(ForkLift);   // forks back up before it drives out

            // Straight forward out to the pivot point, release the lane there, and hand the jack back to its own navigation.
            yield return JackMoveStraight(mouth, JackDriveSpeed);
            ReleaseJackLaneLock();
            s_stagingClaims.Remove(this);
            MheLaneZone.Release(_jack);   // pulled out to the pivot: the mouth is open again

            Debug.Log($"[OrderSelectionTaskDriver] Order {order.OrderId} ({order.CustomerName}) staged in {door}{resolvedLane} by pallet jack.");

            // Park it in a Pallet Jack Parking bay instead of leaving it across the lane mouth. A flat jack running
            // driverless skips this: once released, EquipmentBattery drives it to a charging station itself.
            if (!_ejectedFromJack)
            {
                yield return ParkJack();
                yield return StayAboardWhileCharging();
                if (_jack != null) yield return StepOut(_jack.transform.position - _jack.transform.right);
            }
            ReleaseJack();
            FinishOrderCleanup(task);
        }

        private static bool s_warnedNoParking;

        // ── Staging lane mouths: one jack at a time per area (see JackStageRoutine) ──
        private const float StageClaimRadius = 5f;    // another jack working a lane mouth this close holds us off
        private const float StageWaitBack    = 4f;    // where to wait: this far further out from our lane mouth
        private const float StageClearOut    = 2.5f;  // a finished jack pulls this far on out before releasing the area
        private const float StageWaitMaxReal = 90f;   // never wait for ever (a claim left by a stuck jack)
        /// <summary>Block 58/59 staging changes, OFF since 2026-11-14 while isolating the block 58-60 jams (off = block 57 behaviour).</summary>
        public static bool StageFromExitEndOnly = false, LineUpFacingOut = false;
        private const int   StageLegAttempts = 4;     // NavMesh legs to the lane mouth before a loaded jack is given up
        private const int   StageLineUpAttempts = 3;  // line-up tries before the selector gives the jack up (never noses in)
        private const float StageLineUpMaxDeg = 15f;  // facing this close to straight out of the lane counts as lined up
        private const float StageLineUpLoop = 4f;     // a retry first drives this far further out, then comes back round
        private static readonly Dictionary<OrderSelectionTaskDriver, Vector3> s_stagingClaims = new();

        private const float StageWaitSide = 3.5f;          // the wait spot sits this far to the side of the lane line (jack ~1 m wide + buffer + room)
        private const float StageMakeWayAfterReal = 1.5f;  // something held up behind a waiting jack this long (REAL s): the waiting jack moves
        private const float StageMakeWayCooldown = 5f;
        private const int   StageMakeWayMax = 3;

        /// <summary>Parked on a charger and charging: the selector stays on the jack until it is full (Tad, 2026-10-18), then steps off.
        /// Gives up after 90 real seconds so a stuck charge can never hold the selector forever.</summary>
        private IEnumerator StayAboardWhileCharging()
        {
            if (_jack == null) yield break;
            var b = _jack.GetComponent<EquipmentBattery>();
            if (b == null) yield break;
            yield return null;   // let the battery notice it is on the pad
            float waited = 0f;
            while (_jack != null && b != null && b.IsCharging && !b.IsFull && waited < 90f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Drives the empty jack to a free MHE station. If a bay cannot be reached, try the other free bays.</summary>
        private IEnumerator ParkJack()
        {
            if (_jack == null) yield break;
            var attempted = new HashSet<PalletJackParkingSpot>();
            while (_jack != null)
            {
                var spot = PalletJackParkingSpot.FindNearestFree(_jack.transform.position, _jack, attempted);
                if (spot == null)
                {
                    if (attempted.Count == 0 && !s_warnedNoParking)
                    {
                        s_warnedNoParking = true;
                        UIToast.Show(PalletJackParkingSpot.FindNearest(_jack.transform.position) == null
                            ? "Pallet jacks have nowhere to park - place an MHE Station (Vehicles)."
                            : "All MHE Stations are occupied - add a station to park more pallet jacks.");
                    }
                    else if (attempted.Count > 0)
                        Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' couldn't reach any free MHE station; the idle jack can be retrieved when a station is reachable.");
                    yield break;
                }
                attempted.Add(spot);
                spot.Reserve(_jack);
                spot.ParkPose(_jack.transform.forward, out Vector3 origin, out Vector3 forward);
                yield return DriveJackTo(origin, forward, "pallet jack parking", allowReverse: true);
                if (_jack != null && _jackLegOk &&
                    PlanarDist(_jack.transform.position, origin) <= NavLegArriveTolerance)
                {
                    Vector3 to = origin - _jack.transform.position; to.y = 0f;
                    if (to.magnitude > 0.05f)
                    {
                        yield return JackFace(to, true);
                        yield return JackMoveStraight(origin, JackDriveSpeed);
                    }
                    if (_jack != null && PlanarDist(_jack.transform.position, origin) <= 0.2f)
                    {
                        yield return JackFace(forward, true);
                        yield break;
                    }
                }
                if (_jack != null) spot.Release(_jack);
                Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' couldn't reach a pallet jack parking station.");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Fallback + release
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Gives the jack up. PALLETS NEVER RIDE THE SELECTOR (Tad, 2026-11-10: "the palette is parented to the avatar - that
        /// should never happen"): the old placeholder re-parented every pallet onto the employee and carried on by hand. Now the picked
        /// pallets STAY ON THE JACK, the task goes back on the queue, and the stranded-jack rescue (TryRescueStrandedJack) has the next
        /// free selector take the loaded jack over and finish it. Nothing picked: the empty pallets are thrown away instead.</summary>
        private IEnumerator AbandonJackOnFoot(string reason, OrderData.OrderStatus? stagingWith = null)
        {
            DestroyHeldCase();
            if (TotalCasesOnPallets() == 0 || _jack == null)
            {
                GiveUpTripNothingPicked(reason);
                yield break;
            }
            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' leaving the pallet jack ({reason}) - its {TotalCasesOnPallets()} case(s) stay on the jack " +
                             $"for the next free selector to take over (order {_currentOrder?.OrderNumber}).");
            yield return ClearJackOffLaneMouth();
            ReleaseJack();   // pallets stay parented to the jack
            if (_currentTask != null)
            {
                _orderService?.ReleasePick(_currentTask.TaskId);
                _currentTask.Status = WorkTaskStatus.Available;
                _currentTask.AssignedToEmployeeGuid = null;
            }
            _pallets.Clear();
            _currentTask = null;
            _currentOrder = null;
            _taskInProgress = false;
            _nav.SetTaskBusy(false);
            _nav.Patrol();
        }

        /// <summary>A jack about to be left behind that is standing on a staging lane mouth drives clear of it first, so the next machine
        /// working that lane never finds it parked on the pivot (the pivot standoff of 2026-11-22). Best effort: one leg, no retry.</summary>
        private IEnumerator ClearJackOffLaneMouth()
        {
            if (_jack == null) yield break;
            MheLaneZone.Release(_jack);   // our own claim must not make the mouth look closed to us
            if (!MheLaneZone.TryClearPoint(_jack.transform.position, out var clear)) yield break;
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': moving the pallet jack off a staging lane mouth before leaving it.");
            yield return DriveJackTo(clear, Vector3.zero, "clearing a lane mouth");
        }

        /// <summary>A jack trip that never picked a case and can't get to its first bay: get off, leave the jack, throw the empty pallet
        /// away and put the task back on the queue for anyone (this selector included) - never on foot with a pallet on the back.</summary>
        private void GiveUpTripNothingPicked(string reason)
        {
            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': {reason} with nothing picked - leaving the jack and handing the task back.");
            ReleaseJack();
            if (_currentTask != null)
            {
                _orderService?.ReleasePick(_currentTask.TaskId);
                _currentTask.Status = WorkTaskStatus.Available;
                _currentTask.AssignedToEmployeeGuid = null;
            }
            foreach (var p in _pallets) if (p != null) Destroy(p.gameObject);
            _pallets.Clear();
            _currentTask = null;
            _currentOrder = null;
            _taskInProgress = false;
            _nav.SetTaskBusy(false);
            _nav.Patrol();
        }

        /// <summary>Gets off (if still on), hands the jack back to its parked idle state and drops the claim.</summary>
        private void ReleaseJack()
        {
            _stagingLoaded = false;
            ReleaseJackLaneLock();
            DestroyHeldCase();
            DropSpareChep();
            SetLean(0f);
            SetAnimBool("CarryCase", false);
            SetAnimBool("IsWalking", false);

            var identity = GetComponent<EmployeeIdentity>();
            if (_jack != null)
            {
                if (_jack.CurrentOperator == identity) { _jack.VacateOperator(); _footScripted = false; }
                s_jackClaims.Remove(_jack);
                MheLaneZone.Release(_jack);
            }
            EndScriptedFoot();   // walked by script but never mounted (e.g. the jack vanished mid step-in)
            if (_jackNav != null) { _jackNav.HeldInQueue = false; _jackNav.DriverOwnsMovement = false; }
            _parkPoseSet = false;
            _jack = null;
            _jackNav = null;
            _jackAgent = null;
            _jackRun = null;
            _forkY = 0f;
            s_stagingClaims.Remove(this);   // never leave the lane mouths claimed behind us

            // Own navigation is back on: resync the agent to where the script left the body.
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled) agent.Warp(transform.position);
        }

        private float _strandedSeconds;

        /// <summary>Watchdog (2026-10-23): a selector can end up still MOUNTED on a pallet jack while the driver has already lost
        /// the jack (<c>_jack</c> null, no routine running) - the run was torn down but the vacate never happened. The task stays
        /// claimed and flagged in progress, the stale-claim sweep never frees it (the driver refreshes it every frame), and the
        /// selector sits on the charger "picking" forever. Mounted + no jack for ~8 s = dismount, stage what was picked or free
        /// the task, and go back to work.</summary>
        private void CheckStrandedOnJack()
        {
            if (!_taskInProgress || _jack != null) { _strandedSeconds = 0f; return; }
            var slot = GetComponentInParent<MHEOperatorSlot>();
            var identity = GetComponent<EmployeeIdentity>();
            if (slot == null || !slot.IsPalletJack || identity == null || slot.CurrentOperator != identity) { _strandedSeconds = 0f; return; }
            _strandedSeconds += Time.deltaTime;
            if (_strandedSeconds < 8f) return;
            _strandedSeconds = 0f;

            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' was stranded on a pallet jack with no run driving it - dismounting and recovering the task.");
            RecoverRiderFromJack(slot);
        }

        // ── Dead-jack rider watchdog (2026-11-07, Jose King on OG4466) ──
        // A flat jack is meant to be driven to staging / a charger and stepped off within minutes (_flatBatteryStop). Jose sat aboard a
        // dead jack for hours "picking" OG4466 with an empty pallet while the jack wandered its patrol route, the loaded trailer was held
        // for that pallet and the order was fined. Aboard a flat jack that isn't charging for this long = the run is stuck: log why, stop
        // the jack, dismount, stage what was picked or hand the task back. The battery then sends someone to take the jack to a charger.
        private const float DeadJackRiderGameMinutes = 60f;
        private float _deadJackRiderSeconds;

        private void CheckDeadJackRider()
        {
            var identity = GetComponent<EmployeeIdentity>();
            var slot = GetComponentInParent<MHEOperatorSlot>();
            var batt = slot != null ? slot.GetComponent<EquipmentBattery>() : null;
            if (identity == null || slot == null || !slot.IsPalletJack || slot.CurrentOperator != identity || batt == null
                || !batt.NeedsCharge || batt.IsCharging)
            { _deadJackRiderSeconds = 0f; return; }

            _deadJackRiderSeconds += GameClock.DeltaMinutes;   // game minutes
            if (_deadJackRiderSeconds < DeadJackRiderGameMinutes) return;
            _deadJackRiderSeconds = 0f;

            var nav = slot.GetComponent<AiNavigation>();
            bool claimed = s_jackClaims.TryGetValue(slot, out var holder) && holder != null;
            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' sat on a flat pallet jack for {DeadJackRiderGameMinutes:0} game min " +
                             $"(order {_currentOrder?.OrderNumber ?? "none"}, taskInProgress={_taskInProgress}, ownsJack={_jack == slot}, claimed={claimed}" +
                             $"{(claimed && holder != this ? " by " + holder.name : "")}, flatStop={_flatBatteryStop}, jackNav={(nav != null && nav.enabled ? (nav.IsSeekingTask ? "seeking" : "WANDERING") : "off")}, " +
                             $"chargeTrip={batt.OnChargeTrip}, charge={batt.Charge01:P0}) - stopping the jack and stepping off.");

            if (_jack == slot)
            {
                // Our own run is stuck somewhere on this jack: end it the same way the no-progress watchdog does.
                SetJackDrive(false, "dead-jack watchdog");
                if (_currentOrder != null && _currentTask != null) { RecoverFromNoProgress(); return; }
                ReleaseJack();
                _taskInProgress = false; _nav.SetTaskBusy(false); _nav.Patrol();
                return;
            }
            if (claimed && holder != this) return;   // another selector's run holds it: that run's own watchdogs own it
            RecoverRiderFromJack(slot);
        }

        /// <summary>Gets this selector off <paramref name="slot"/> when no run of theirs is driving it, then stages what was picked
        /// (on foot) or hands the task back.</summary>
        private void RecoverRiderFromJack(MHEOperatorSlot slot)
        {
            slot.VacateOperator();
            s_jackClaims.Remove(slot);
            _footScripted = false;
            ReleaseJackLaneLock();
            DestroyHeldCase();
            SetLean(0f);
            SetAnimBool("CarryCase", false);
            EndScriptedFoot();

            int picked = _pallets.Sum(p => p != null ? p.TotalCases : 0);
            if (_currentOrder != null && _currentTask != null && picked > 0)
            {
                FinishOrder(OrderData.OrderStatus.PartiallyPicked);   // stage what it already picked, exactly like a short pick
                return;
            }

            // Nothing picked: hand the task back so any selector (this one included) can claim it fresh.
            if (_currentTask != null)
            {
                _orderService?.ReleasePick(_currentTask.TaskId);
                _currentTask.Status = WorkTaskStatus.Available;
                _currentTask.AssignedToEmployeeGuid = null;
            }
            foreach (var p in _pallets) if (p != null) Destroy(p.gameObject);
            _pallets.Clear();
            _currentTask = null;
            _currentOrder = null;
            _taskInProgress = false;
            _nav.SetTaskBusy(false);
            _nav.Patrol();
        }

        // ── No-progress watchdog (2026-11-01, HAL block 29) ──
        // A selector froze on a continuation trip (a jack leg / seek to an unreachable spot never called back) and held the
        // order's last OrderSelect task for five game days. Update refreshes the claim every frame, so the stale-claim sweep
        // never freed it; the order could not close, its trailer held door 1, and five later orders went late behind it.
        // 10 (was 30, Tad 2026-11-05): OG4856's two selectors sat in the charger jam and the order was freed far too late to make its
        // door - fined $581, two in-transit pallets missed the trailer.
        private const float NoProgressGameMinutes = 10f;   // counted with GameClock.DeltaMinutes (20 game s per real s: 30 real s at 1x)
        private float _noProgressSeconds;
        private Vector3 _progressAnchor;
        private Vector3 _progressBodyAnchor;
        private int _progressPicked = -1;
        private WorkTask _progressTask;
        /// <summary>Set once JackStageRoutine has live pallets to stage; cleared when the pallets are dropped or the jack is let go.</summary>
        private bool _stagingLoaded;
        private string _stagingHoldLoggedFor;

        private void CheckNoProgress()
        {
            if (!_taskInProgress || _waitingForStagingSpace || _currentTask == null || _currentOrder == null
                || _currentTask.Type != WorkTaskType.OrderSelect || _currentOrder.ReturnsMode)
            { _noProgressSeconds = 0f; _progressTask = null; return; }

            // Charging aboard a jack is a legitimate long stop.
            if (_jack != null && _jack.GetComponent<EquipmentBattery>() is EquipmentBattery b && b.IsCharging)
            { _noProgressSeconds = 0f; return; }
            // Watch the jack (it carries the work once aboard) AND the selector's own body: walking to a parked jack 40 m away
            // moves only the selector, and watching the jack alone handed the order back mid-walk, for ever (2026-11-07, OG3875).
            Vector3 pos = _jack != null ? _jack.transform.position : transform.position;
            Vector3 body = transform.position;
            int picked = _currentOrder.TotalUnitsPicked;
            // A jack wandering its own patrol route is NOT progress (2026-11-07, Jose King on OG4466): its AiNavigation was left on with
            // no task, it ambled between waypoints with him aboard, and every metre reset this watchdog, so the order was never freed.
            // Movement only counts while the run itself drives the jack (nav off = scripted, or seeking the run's target).
            var rideSlot = _jack != null ? _jack : GetComponentInParent<MHEOperatorSlot>();
            var rideNav = _jack != null ? _jackNav : (rideSlot != null ? rideSlot.GetComponent<AiNavigation>() : null);
            bool wandering = rideSlot != null && rideNav != null && rideNav.enabled && !rideNav.IsSeekingTask;
            if (wandering) { pos = _progressAnchor; body = _progressBodyAnchor; }
            if (_progressTask != _currentTask || picked != _progressPicked || PlanarDist(pos, _progressAnchor) > 1f || PlanarDist(body, _progressBodyAnchor) > 1f)
            {
                _progressTask = _currentTask; _progressPicked = picked; _progressAnchor = pos; _progressBodyAnchor = body; _noProgressSeconds = 0f;
                return;
            }
            _noProgressSeconds += GameClock.DeltaMinutes;   // game minutes (the constant above)
            if (_noProgressSeconds < NoProgressGameMinutes) return;
            _noProgressSeconds = 0f; _progressTask = null;
            // A LOADED jack queued to stage is never abandoned (Tad, 2026-11-14, block 67 OG2624): leaving it by the lane mouth only parked
            // another loaded jack in the most crowded spot, it counted as "a pallet being moved" and held the order's close-out, and the next
            // selector who took it over joined the same queue. The selector stays aboard and stages when the lane clears; this is only logged.
            if (_stagingLoaded && _jack != null && JackLoaded)
            {
                string num = _currentOrder.OrderNumber ?? _currentOrder.OrderId;
                // Info, once per order: this is the intended behaviour, and a warning every 10 game minutes read as a REPEATING bug (block 68).
                if (_stagingHoldLoggedFor != num)
                {
                    _stagingHoldLoggedFor = num;
                    Debug.Log($"[OrderSelectionTaskDriver] '{name}' made no progress staging order {num} for {NoProgressGameMinutes:0} game minutes - staying aboard the loaded jack (StagingHold).");
                }
                return;
            }
            RecoverFromNoProgress();
        }

        /// <summary>Stops whatever the selector was stuck in and frees the order: picked cases are staged (the remainder becomes
        /// another trip, as with any short trip), and a trip with nothing picked goes back to the queue for any selector.</summary>
        private void RecoverFromNoProgress()
        {
            string number = _currentOrder.OrderNumber ?? _currentOrder.OrderId;
            int picked = _pallets.Sum(p => p != null ? p.TotalCases : 0);
            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' made no progress on order {number} for {NoProgressGameMinutes:0} game minutes - " +
                             (picked > 0 ? $"staging the {picked} case(s) picked and freeing the rest." : "handing the task back to the queue."));
            SystemsLogWindow.LogWarning($"{name.Replace("Employee_", "")} was stuck on order {number} for {NoProgressGameMinutes:0} minutes - " +
                                        (picked > 0 ? "staging what was picked; the rest goes back on the queue." : "the pick goes back on the queue."));

            StopAllCoroutines();
            _nav.CancelSeekPosition();
            DestroyHeldCase();
            SetLean(0f);
            SetAnimBool("CarryCase", false);

            if (picked > 0)
            {
                // Same as losing the jack: the pallets stay on the jack for the next free selector (never on the employee).
                if (_jack != null) { StartCoroutine(AbandonJackOnFoot("no progress", OrderData.OrderStatus.PartiallyPicked)); return; }
                EndScriptedFoot();
                FinishOrder(OrderData.OrderStatus.PartiallyPicked);
                return;
            }

            if (_jack != null) ReleaseJack(); else { ReleaseJackLaneLock(); EndScriptedFoot(); }
            _orderService?.ReleasePick(_currentTask.TaskId);
            _currentTask.Status = WorkTaskStatus.Available;
            _currentTask.AssignedToEmployeeGuid = null;
            foreach (var p in _pallets) if (p != null) Destroy(p.gameObject);
            _pallets.Clear();
            ClearJoinState();
            _currentTask = null;
            _currentOrder = null;
            _taskInProgress = false;
            _nav.SetTaskBusy(false);
            _nav.Patrol();
        }

        private void ReleaseJackLaneLock()
        {
            // The claim (taken when the lane was chosen) and the insertion lock (taken at the mouth) are both handed back here.
            // The claim is released first and only for this jack; the lock can only be ours while our claim stood.
            if (_jackLaneClaim != null)
            {
                _inventoryService?.ReleaseLaneClaim(_jackLaneClaim.Value.Door, _jackLaneClaim.Value.Lane, _jack);
                _jackLaneClaim = null;
            }
            if (_jackLaneLock == null) return;
            _inventoryService?.ReleaseLaneEntry(_jackLaneLock.Value.Door, _jackLaneLock.Value.Lane);
            _jackLaneLock = null;
        }

        private void OnDisable()
        {
            // Assignment changed / fired / destroyed mid-run: never leave a lane locked or a jack claimed.
            ReleaseJackLaneLock();
            DestroyHeldCase();
            if (_jackNav != null) _jackNav.HeldInQueue = false;
            if (_jack != null)
            {
                var identity = GetComponent<EmployeeIdentity>();
                if (gameObject.activeInHierarchy && _jack.CurrentOperator == identity) { _jack.VacateOperator(); _footScripted = false; }
                s_jackClaims.Remove(_jack);
                _jack = null;
            }
            if (gameObject.activeInHierarchy) EndScriptedFoot();
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Pallets on the forks
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Puts a freshly spawned pallet on the jack's forks (called from StartNextPallet).</summary>
        private void SeatPalletOnJack(OutboundPalletBuilder pallet, int index)
        {
            pallet.transform.SetParent(_jack.transform, false);
            pallet.transform.localPosition = new Vector3(0f, _forkY, ForkPalletZ[Mathf.Min(index, ForkPalletZ.Length - 1)]);
            pallet.transform.localRotation = Quaternion.identity;
            pallet.SetNavObstacleActive(false);
        }

        /// <summary>Raises / lowers every pallet riding the forks to <paramref name="targetY"/> over ForkMoveSeconds.
        /// Up while it carries a pallet and picks; down to set it in the lane.</summary>
        private IEnumerator MoveForks(float targetY)
        {
            float start = _forkY;
            for (float t = 0f; t < ForkMoveSeconds; t += Time.deltaTime)
            {
                ApplyForkY(Mathf.Lerp(start, targetY, Mathf.SmoothStep(0f, 1f, t / ForkMoveSeconds)));
                yield return null;
            }
            ApplyForkY(targetY);
        }

        private void ApplyForkY(float y)
        {
            _forkY = y;
            if (_jack == null) return;
            if (_spareChep != null && _spareChep.transform.parent == _jack.transform)
            {
                var sp = _spareChep.transform.localPosition;
                _spareChep.transform.localPosition = new Vector3(sp.x, y, sp.z);
            }
            foreach (var p in _pallets)
            {
                if (p == null || p.transform.parent != _jack.transform) continue;
                var lp = p.transform.localPosition;
                p.transform.localPosition = new Vector3(lp.x, y, lp.z);
            }
        }

        private void DestroyEmptyPallets()
        {
            for (int i = _pallets.Count - 1; i >= 0; i--)
            {
                var p = _pallets[i];
                if (p != null && p.TotalCases > 0) continue;
                if (p != null) Destroy(p.gameObject);
                _pallets.RemoveAt(i);
            }
        }

        private int TotalCasesOnPallets() => _pallets.Sum(p => p != null ? p.TotalCases : 0);

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Held case + animator
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        private void SpawnHeldCase(GameObject prefab, float caseHeight)
        {
            DestroyHeldCase();
            var go = Instantiate(prefab, transform);
            go.name = "HeldCase";
            go.transform.localPosition = new Vector3(0f, HeldHandsHeight - caseHeight * 0.5f, HeldHandsForward);
            go.transform.localRotation = Quaternion.identity;
            // Same stripping OutboundPalletBuilder.AddCase does: a build-menu prefab must not self-register.
            var po = go.GetComponent<PlacedObject>();
            if (po != null) { po.enabled = false; Destroy(po); }
            var bd = go.GetComponent<BuildingData>();
            if (bd != null) Destroy(bd);
            var bh = go.GetComponent<BuildingHighlighter>();
            if (bh != null) Destroy(bh);
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            _heldCase = go;
        }

        private void DestroyHeldCase()
        {
            if (_heldCase != null) Destroy(_heldCase);
            _heldCase = null;
        }

        /// <summary>Sets a bool on every Animator in the body (the base rig and the modular avatar), if it exists.</summary>
        private void SetAnimBool(string param, bool value)
        {
            foreach (var anim in GetComponentsInChildren<Animator>(true))
            {
                if (anim == null || anim.runtimeAnimatorController == null) continue;
                foreach (var p in anim.parameters)
                    if (p.name == param && p.type == AnimatorControllerParameterType.Bool) { anim.SetBool(param, value); break; }

                // The Carry layer sits at weight 0 and is only switched on while a case is held. On a humanoid
                // rig an empty state on a full-weight override layer still forces the arms into a reference pose,
                // which froze every walking employee's arms (2026-10-17).
                if (param == "CarryCase")
                {
                    int layer = anim.GetLayerIndex("Carry");
                    if (layer >= 0) anim.SetLayerWeight(layer, value ? 1f : 0f);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Scripted movement — selector on foot
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        // ── Mounting: MountLeft / MountRight on the jack prefab ─────────────────────────────────────────
        // The operator never cuts across the machine: off = anchor → a mount point → wherever; on = nearer
        // mount point → turn to face the operator spot → step in.

        private Vector3 MountPointNear(Vector3 near)
        {
            if (_jack == null) return near;
            Transform jt = _jack.transform;
            Transform l = jt.Find("MountLeft"), r = jt.Find("MountRight");
            Vector3 lp = l != null ? l.position : _jack.OperatorAnchor.position - jt.right;
            Vector3 rp = r != null ? r.position : _jack.OperatorAnchor.position + jt.right;
            return PlanarDist(lp, near) <= PlanarDist(rp, near) ? lp : rp;
        }

        /// <summary>From wherever the selector stands (their navigation already off): nearer mount point, turn to
        /// face the operator spot, step onto it, face the way the jack points. Caller then mounts/StepOn()s.</summary>
        private IEnumerator StepIn()
        {
            if (_jack == null) yield break;
            yield return WalkTo(MountPointNear(transform.position));
            if (_jack == null) yield break;
            Vector3 anchor = _jack.OperatorAnchor.position;
            yield return FaceDir(anchor - transform.position);
            yield return WalkTo(anchor);
            if (_jack == null) yield break;
            yield return FaceDir(_jack.transform.forward);
        }

        /// <summary>Off the jack on the side nearest <paramref name="toward"/>: anchor → that mount point.</summary>
        private IEnumerator StepOut(Vector3 toward)
        {
            if (_jack == null) yield break;
            _jack.StepOff();
            yield return WalkTo(MountPointNear(toward));
        }

        /// <summary>Own navigation off (as if riding) so the script can walk the body to exact points.</summary>
        private void BeginScriptedFoot()
        {
            if (_footScripted) return;
            _nav.CancelSeekPosition();
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
                agent.enabled = false;
            }
            _nav.enabled = false;
            GetComponent<AgentAnimation>()?.SetRidingMHE(true);
            _footScripted = true;
        }

        private void EndScriptedFoot()
        {
            if (!_footScripted) return;
            _footScripted = false;
            SetAnimBool("IsWalking", false);
            GetComponent<AgentAnimation>()?.SetRidingMHE(false);
            if (_nav != null) _nav.enabled = true;
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                MheStandingObstacle.Suspend(agent);
                agent.enabled = true;
                agent.Warp(transform.position);
            }
        }

        /// <summary>Distance from the operator anchor back to the jack's LaneLineUp transform (rear, fork tips).</summary>
        private float LineUpDistance()
        {
            Transform lu = _jack != null ? _jack.transform.Find("LaneLineUp") : null;
            return lu != null ? Mathf.Abs(_jack.transform.InverseTransformPoint(lu.position).z) : 2.68f;
        }

        // ── Pick face: cases disappear as they're picked ────────────────────────────────────────────────

        /// <summary>The pick slot's pallet shows exactly as many cases as it holds: picked cases vanish from the TOP
        /// layer down. When it's empty the empty pallet goes too, which frees the slot (LocationData.Pick already
        /// released it) for ReplenishmentService to file a reach-truck replenishment through the Work Queue.
        /// An empty pallet left in the rack would sit where the replenishment drops, and on reload the tracker
        /// would rebuild it from its PalletData as full phantom stock.</summary>
        private void RemoveCaseFromPickFace(LocationData location, string palletId)
        {
            GameObject palletGo = FindPickFacePallet(location, palletId);
            if (palletGo == null) return;

            // Remaining cases: the pallet's record when it has one (gone = empty); without a record, the slot's own count.
            int remaining = string.IsNullOrEmpty(palletId)
                ? location.Quantity
                : (_inventoryService.GetPallet(palletId)?.Quantity ?? 0);
            palletGo.GetComponent<PalletData>()?.ReduceQuantity(1);

            if (remaining <= 0)
            {
                Destroy(palletGo);
                return;
            }

            var visible = PickOrder(palletGo);
            for (int i = 0; i < visible.Count - remaining; i++) visible[i].gameObject.SetActive(false);
        }

        /// <summary>The visible cases on a pick-face pallet in the order a selector takes them: top layer first, and
        /// within a layer the ones nearest the selector first (front half, then reaching for the back half).</summary>
        private List<Transform> PickOrder(GameObject palletGo)
        {
            var visible = new List<Transform>();
            Transform load = palletGo != null ? palletGo.transform.Find("PalletLoad") : null;
            if (load == null) return visible;
            foreach (Transform c in load) if (c.gameObject.activeSelf) visible.Add(c);
            Vector3 me = transform.position;
            visible.Sort((a, b) =>
            {
                int layer = Mathf.RoundToInt(b.localPosition.y * 100f).CompareTo(Mathf.RoundToInt(a.localPosition.y * 100f));
                return layer != 0 ? layer : PlanarDist(a.position, me).CompareTo(PlanarDist(b.position, me));
            });
            return visible;
        }

        /// <summary>True when the next case to come off this pick face is in the back half (past the slot pivot,
        /// away from the selector) — they lean in to reach it.</summary>
        private bool NextPickIsDeep(LocationData location, Vector3 slotFlat, Vector3 toAisle)
        {
            var order = PickOrder(FindPickFacePallet(location, location.PalletId));
            if (order.Count == 0) return false;
            return Vector3.Dot(Flat(order[0].position - slotFlat, 0f), toAisle) < 0f;
        }

        private void SetLean(float weight)
        {
            var lean = GetComponent<BodyLean>();
            if (lean == null) { if (weight <= 0f) return; lean = gameObject.AddComponent<BodyLean>(); }
            lean.SetLean(weight);
        }

        private static GameObject FindPickFacePallet(LocationData location, string palletId)
        {
            var link = PalletMasterLink.Find(palletId);
            if (link != null) return link.gameObject;

            // Fallback: the pallet standing on this slot (same tolerance the occupancy reconcile uses).
            Vector3 at = location.transform.position;
            GameObject best = null;
            float bestD = 0.9f;
            foreach (var pd in FindObjectsByType<PalletData>())
            {
                if (pd == null || pd.GetComponent<OutboundPalletBuilder>() != null) continue;
                Vector3 p = pd.transform.position;
                if (Mathf.Abs(p.y - at.y) > 0.6f) continue;
                float d = PlanarDist(p, at);
                if (d < bestD) { bestD = d; best = pd.gameObject; }
            }
            return best;
        }

        /// <summary>Where the selector stands in the gap between a rack face and the parked jack: this far out from the face.</summary>
        private const float SelectorGapStand = 0.26f;

        /// <summary>Walks to <paramref name="target"/> (the NavMesh routes the selector round the parked jack).</summary>
        private IEnumerator WalkRoundJack(Vector3 target) { yield return WalkTo(target); }

        private IEnumerator WalkTo(Vector3 target)
        {
            Transform t = transform;
            Vector3 flat = Flat(target, t.position.y);
            Vector3 to = flat - t.position;
            if (to.magnitude < 0.05f) yield break;
            yield return FaceDir(to);
            SetAnimBool("IsWalking", true);
            while (PlanarDist(t.position, flat) > 0.03f)
            {
                t.position = Vector3.MoveTowards(t.position, flat, FootWalkSpeed * Time.deltaTime);
                Vector3 d = flat - t.position; d.y = 0f;
                if (d.sqrMagnitude > 0.0004f)
                    t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(d.normalized), FootTurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.position = flat;
            SetAnimBool("IsWalking", false);
        }

        private IEnumerator FaceDir(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) yield break;
            Transform t = transform;
            Quaternion goal = Quaternion.LookRotation(dir.normalized);
            while (Quaternion.Angle(t.rotation, goal) > 1f)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, goal, FootTurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.rotation = goal;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Driving the jack
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Drives the pallet jack to <paramref name="target"/> (its root) on a plain NavMeshAgent leg (<see cref="MheTravel"/>), then
        /// turns it to <paramref name="finalForward"/> (zero = any). Sets _jackLegOk.</summary>
        private IEnumerator DriveJackTo(Vector3 target, Vector3 finalForward, string phase, bool allowReverse = false)
        {
            _jackLegOk = false;
            if (_jack == null) yield break;
            SetJackDrive(false, phase);
            bool ok = false;
            yield return MheTravel.Leg(_jack.transform, Flat(target, _jack.transform.position.y), phase, r => ok = r);
            if (_jack == null) yield break;
            _jackLegOk = ok;
            if (!ok) { Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': pallet jack could not finish '{phase}'."); yield break; }
            if (finalForward.sqrMagnitude > 0.0001f) yield return MheTravel.Turn(_jack.transform, finalForward);
        }

        /// <summary>Drives to the point (a pivot point). Plain leg: the jack's root goes there.</summary>
        private IEnumerator DriveJackCentreTo(Vector3 centre, string phase)
        {
            yield return DriveJackTo(centre, Vector3.zero, phase);
        }

        /// <summary>A pick stop.</summary>
        private IEnumerator DriveBayLeg(Vector3 park, Vector3 heading, string phase, bool allowReverse)
        {
            yield return DriveJackTo(park, heading, phase, allowReverse);
        }

        /// <summary>A parked jack is found away from where it stopped (hit by another machine): it drives back to its stop
        /// and squares up again before the next case. Checked between cases.</summary>
        private IEnumerator RecoverIfMovedFromPark()
        {
            if (_jack == null || !_parkPoseSet || !_offJack) yield break;
            Transform jt = _jack.transform;
            float moved = PlanarDist(jt.position, _parkPos);
            float turned = Quaternion.Angle(jt.rotation, _parkRot);
            if (moved < ParkMovedMeters && turned < ParkMovedDegrees) yield break;
            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': the pallet jack was moved {moved:0.0} m / {turned:0} degrees from its pick stop - driving back to it.");
            yield return DriveJackTo(_parkPos, _parkRot * Vector3.forward, "back to the pick stop", true);
            if (_jack != null && !_jackLegOk)
                Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}': could not drive the jack back to its pick stop - carrying on from where it stands.");
        }

        /// <summary>The aisle direction of the bay stop being driven to (ComputeBayPark), before any end-of-aisle angle.</summary>
        private Vector3 _aisleHeading;

        /// <summary>Turns the jack on the spot to <paramref name="dir"/>.</summary>
        private IEnumerator JackFace(Vector3 dir, bool aboutRoot = false)
        {
            dir.y = 0f;
            if (_jack == null || dir.sqrMagnitude < 0.0001f) yield break;
            SetJackDrive(false, "turn");
            yield return MheTravel.Turn(_jack.transform, dir);
        }

        /// <summary>Straight along the jack's own axis to <paramref name="target"/> (its root): reversing into a lane, pulling out.</summary>
        private IEnumerator JackMoveStraight(Vector3 target, float speed)
        {
            if (_jack == null) yield break;
            SetJackDrive(false, "straight");
            yield return MheTravel.Straight(_jack.transform, Flat(target, _jack.transform.position.y), speed);
        }

        /// <summary>Script owns the jack (manual) vs its own NavMeshAgent drives it. Same rules as
        /// ReachTruckOperator.SetDriveMode: always reassert, always Warp when handing back to the agent.</summary>
        private void SetJackDrive(bool ai, string phase)
        {
            if (_jack == null) return;
            if (!ai)
            {
                if (_jackNav != null) { _jackNav.CancelSeekPosition(); _jackNav.enabled = false; }
                if (_jackAgent != null)
                {
                    if (_jackAgent.isActiveAndEnabled && _jackAgent.isOnNavMesh) _jackAgent.isStopped = true;
                    _jackAgent.enabled = false;
                }
            }
            else
            {
                if (_jackAgent != null)
                {
                    MheStandingObstacle.Suspend(_jack);
                    _jackAgent.enabled = true;
                    _jackAgent.Warp(_jack.transform.position);
                    if (_jackAgent.isActiveAndEnabled && _jackAgent.isOnNavMesh) _jackAgent.isStopped = false;
                }
                if (_jackNav != null) _jackNav.enabled = true;
            }
        }

        /// <summary>Slot position on the floor and the unit direction from the rack face into the aisle
        /// (from the slot's LocApproachAnchor, which every rack prefab carries).</summary>
        private static bool TryGetPickGeometry(LocationData location, out Vector3 slotFlat, out Vector3 toAisle)
        {
            slotFlat = default; toAisle = default;
            if (location == null) return false;
            Vector3 slot = location.transform.position;
            Transform anchor = location.transform.Find("LocApproachAnchor");
            Vector3 dir = anchor != null ? anchor.position - slot : location.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = location.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            toAisle = dir.normalized;
            slotFlat = slot;
            return true;
        }

        /// <summary>Aisle direction to park along: the way the pick route travels through this aisle (bays going UP
        /// in an odd aisle, DOWN in an even one), so the jack drives forward bay to bay and only reverses for a short
        /// backtrack. It used to keep whatever heading it arrived with, so a jack that entered an aisle backwards
        /// reversed the whole length of it (Tad, 2026-10-17). Falls back to the current heading.</summary>
        private Vector3 PickHeading(Vector3 toAisle, LocationData pick)
        {
            Vector3 along = Vector3.Cross(Vector3.up, toAisle).normalized;
            Vector3 fwd = TryAisleTravelDir(pick, out var travel) ? travel
                        : (_jack != null ? _jack.transform.forward : transform.forward);
            return Vector3.Dot(along, fwd) >= 0f ? along : -along;
        }

        /// <summary>World direction of travel through the pick's aisle: toward higher bay numbers in an odd aisle,
        /// lower in an even one (the serpentine sequence, OrderPickPath.SequenceKey).</summary>
        private static bool TryAisleTravelDir(LocationData pick, out Vector3 dir) => TryAisleTravelDir(pick, out dir, out _);

        /// <summary>As above, also giving the bay pitch along the aisle (bay N to bay N+2, the next bay on the same side).</summary>
        private static bool TryAisleTravelDir(LocationData pick, out Vector3 dir, out float bayPitch)
        {
            dir = Vector3.zero; bayPitch = 0f;
            string a = pick != null ? pick.Address : null;
            if (string.IsNullOrEmpty(a) || a.Length < 8 ||
                !int.TryParse(a.Substring(0, 2), out int aisle) || !int.TryParse(a.Substring(3, 2), out int bay)) return false;
            string prefix = a.Substring(0, 3), suffix = a.Substring(5);
            // SAME SIDE ONLY (2026-11-08): bays N and N+1 face each other ACROSS the aisle (odd side 1,3,5.., even side 2,4,6..,
            // paired by RenumberAisle), so stepping 1 bay used to measure straight across the aisle. That vector has no length
            // along the aisle, the sign test in PickHeading was left to rounding noise, and jacks parked facing either way.
            for (int d = 2; d <= 8; d += 2)
            {
                foreach (int sign in new[] { 1, -1 })
                {
                    int other = bay + sign * d;
                    if (other < 1 || other > 99) continue;
                    if (!LocationRegistry.TryGet($"{prefix}{other:00}{suffix}", out var loc) || loc == null) continue;
                    Vector3 v = loc.WorldPosition - pick.WorldPosition; v.y = 0f;
                    if (v.sqrMagnitude < 0.01f) continue;
                    dir = v.normalized * sign;                 // toward higher bay numbers, in EVERY aisle
                    bayPitch = v.magnitude / (d / 2f);
                    return true;
                }
            }
            return false;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // AISLE TRAFFIC (Tad, 2026-11-08): "the rule for pallet jacks is they have to FOLLOW the direction of traffic in the aisle.
        // They enter at the lowest numbered locations and travel down the right side. If they need to pass they can pass just like a
        // car does on a highway and drive around them." Reach trucks may go either way (ReachTruckOperator, untouched).
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Tad, 2026-11-10: the jack's centre drives 0.5 m off the rack FACE (face = slot line - ~0.75 m rack half depth, so 1.25 m from
        // the slot line; was 1.7 / ~1 m). To pass it swings out to 1.5 m from the jack ahead, then pulls back to 0.5 m from the rack.
        private const float LaneMinFromSlot   = 2.0f;   // the lane line keeps this far from a rack's slot line: 1.25 m off the rack face (Tad 2026-11-10: 0.5 -> 0.75 -> 1.25)
        private const float LaneMaxOffset     = 2.0f;   // right lane at most this far right of the aisle centre (effectively: hug the rack)
        private const float LaneMinPassOffset = 0.6f;   // pass lane at least this far left of the right lane, or there is no room to overtake
        private const float PassSideGap       = 1.5f;   // passing: centre line this far (sideways) from the jack being passed
        private const float AisleEntryLead    = 3.0f;   // enter the aisle from this far before its lowest bay
        private const float AisleInsideMargin = 0.4f;   // "inside the aisle" = within (half width - this) of the centre line

        /// <summary>One aisle's traffic geometry, from a pick location: travel direction (toward higher bay numbers), the centre line
        /// and the right-lane offset. Along/Lateral are measured from <see cref="Centre"/>.</summary>
        internal struct AisleLane
        {
            public Vector3 Travel, Right, Centre;
            public float Offset, Half, AlongMin, AlongMax, BayPitch;
            public bool HasMin, HasMax;
            public string Prefix;   // "AA-" of the aisle
            public Vector3 Lane(float along) => Centre + Right * Offset + Travel * along;
            /// <summary>Lateral line used to pass a jack in the right lane: PassSideGap (1.5 m) to its left, but never closer than the
            /// lane clearance to the left-hand rack.</summary>
            public float PassOffset => Offset <= 0.01f ? Offset : Mathf.Max(-(Half - LaneMinFromSlot), Offset - PassSideGap);   // one-sided aisle: no pass lane
            public Vector3 OtherLane(float along) => Centre + Right * PassOffset + Travel * along;
            public float Along(Vector3 p) { p -= Centre; p.y = 0f; return Vector3.Dot(p, Travel); }
            public float Lateral(Vector3 p) { p -= Centre; p.y = 0f; return Vector3.Dot(p, Right); }
        }


        private static bool TryParseAddress(string a, out string prefix, out int bay, out string suffix)
        {
            prefix = suffix = null; bay = 0;
            if (string.IsNullOrEmpty(a) || a.Length < 8 || !int.TryParse(a.Substring(0, 2), out _) || !int.TryParse(a.Substring(3, 2), out bay)) return false;
            prefix = a.Substring(0, 3); suffix = a.Substring(5);
            return true;
        }

        private static bool TryGetAisleLane(LocationData pick, out AisleLane lane)
        {
            lane = default;
            if (pick == null || !TryParseAddress(pick.Address, out string prefix, out int bay, out string suffix)) return false;
            if (!TryAisleTravelDir(pick, out Vector3 travel, out float bayPitch)) return false;
            if (!TryGetPickGeometry(pick, out Vector3 slotFlat, out Vector3 toAisle)) return false;
            travel.y = 0f;
            if (travel.sqrMagnitude < 0.0001f) return false;
            travel.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, travel).normalized;

            // The bay directly across (odd N <-> even N+1, same level and position) gives the aisle's real width and centre.
            Vector3 centre; float half;
            int facingBay = (bay % 2 == 1) ? bay + 1 : bay - 1;
            if (facingBay >= 1 && LocationRegistry.TryGet($"{prefix}{facingBay:00}{suffix}", out var facing) && facing != null)
            {
                float across = Vector3.Dot(Flat(facing.transform.position - slotFlat, 0f), right);
                if (Mathf.Abs(across) < 1f) return false;   // not really across the aisle: keep the old behaviour
                centre = slotFlat + right * (across * 0.5f);
                half = Mathf.Abs(across) * 0.5f;
            }
            else
            {
                // One-sided aisle: centre where the old park sat (ParkOffset out from the face), no room assumed for two lanes.
                centre = slotFlat + toAisle * ParkOffset;
                half = ParkOffset;
            }
            centre.y = slotFlat.y;
            bool oneSided = !(facingBay >= 1 && LocationRegistry.TryGet($"{prefix}{facingBay:00}{suffix}", out _));
            // One-sided aisle (racks on one side, a wall or the floor edge on the other - aisle 02 in "three doors"): no second lane to pass in,
            // and "right" can point at the wall, so the jack drives the old park line (ParkOffset off the rack face). Tad, 2026-11-08: a selector
            // with 4 cases of pickled sat "so tight to the right that he seems to be stuck".
            lane = new AisleLane { Travel = travel, Right = right, Centre = centre, Half = half, Prefix = prefix, BayPitch = bayPitch,
                                   Offset = oneSided ? 0f : Mathf.Clamp(half - LaneMinFromSlot, 0f, LaneMaxOffset) };

            // Where it ends: the highest-numbered bay at this level and position.
            for (int b = 99; b >= bay; b--)
            {
                if (!LocationRegistry.TryGet($"{prefix}{b:00}{suffix}", out var last) || last == null) continue;
                lane.AlongMax = lane.Along(last.transform.position);
                lane.HasMax = true;
                break;
            }

            // Where the aisle starts: the lowest-numbered bay on either side, at this level and position.
            for (int b = 1; b <= bay; b++)
            {
                if (!LocationRegistry.TryGet($"{prefix}{b:00}{suffix}", out var first) || first == null) continue;
                lane.AlongMin = lane.Along(first.transform.position);
                lane.HasMin = true;
                break;
            }
            return true;
        }

        private const float AislePathWaitMaxReal = 30f;   // JackNavLeg waits this long (REAL s) for a way that does not run through an aisle

        private const float MouthHopMax = 16f;   // a hop between aisle mouths this long or shorter is driven round the rack end, not pathed

        private const int   MouthHopAttempts   = 3;
        private const float MouthHopRetryWait   = 1.5f;   // real seconds between tries of a blocked rack-end hop
        private const float MouthLineUpBack     = 1.5f;   // the NavMesh line-up point sits this much further out than the mouth

        private const float JackForkTrail     = 2.8f;   // the forks trail this far behind the jack's root
        private const float SideLineUpOffset  = 4.0f;   // lining up beside a mouth: this far to the side of its lane line

        private const float RackEndClearance = 2.5f;   // drive on this far past the aisle exit before turning: the forks trail 2.8 m behind
        private const float UTurnPastLastPick = 2.75f;  // the turn starts this far past the centre of the last pick on the turn side (Tad: 2.5-3 m)
        private const float UTurnCornerRadius = 1.5f;   // each 90 degree corner is rounded to this radius

        private const float CurveLookAhead = 0.6f;   // FollowCurveGuarded aims at the curve this far ahead of the jack

        /// <summary>The aisle <paramref name="p"/> stands in, between its first and last bay (with a little slack at each end).</summary>
        private static bool TryFindAisleAt(Vector3 p, out AisleLane lane) => TryFindAisleAt(p, 1.5f, out lane);

        /// <summary>As above, counting <paramref name="endSlack"/> metres beyond the first and last bay as still in the aisle.</summary>
        internal static bool TryFindAisleAt(Vector3 p, float endSlack, out AisleLane lane)
        {
            lane = default;
            var seen = new HashSet<string>();
            foreach (var loc in LocationRegistry.All)
            {
                if (loc == null || !TryParseAddress(loc.Address, out string pre, out _, out _) || !seen.Add(pre)) continue;
                if (!TryGetAisleLane(loc, out var a) || !a.HasMin || !a.HasMax) continue;
                float along = a.Along(p);
                if (Mathf.Abs(a.Lateral(p)) > a.Half - AisleInsideMargin || along < a.AlongMin - endSlack || along > a.AlongMax + endSlack) continue;
                lane = a;
                return true;
            }
            return false;
        }




        /// <summary>Fix B switch (dev / A-B testing with JackTrafficRecorder). On by default.</summary>
        public static bool DoorBypassEnabled = true;
        private const float DoorZoneBays = 2f;




        // ── Keeping pallet jacks apart (Tad, 2026-10-17: two selectors on one order drove over each other) ──
        private const float JackCentreBack = 1.0f;     // the forks trail behind the body: the jack's middle is ~1 m back

        private static Vector3 JackCentre(Transform t) => t.position - t.forward * JackCentreBack;

        /// <summary>Resolves the lane + slot this order stages into. Shared by both paths (same rules as FinishOrder).</summary>
        private bool TryResolveStagingTarget(OrderData order, out string resolvedLane, out LaneNamingService.LaneSlot slot,
                                             out Vector3 pos, out Vector3 depthAxis, out string failReason)
        {
            resolvedLane = null; slot = default; pos = default; depthAxis = Vector3.forward; failReason = null;
            if (order.EffectiveStagingDoor <= 0 || string.IsNullOrEmpty(order.AssignedLane))
            { failReason = "order was never released to a staging lane"; return false; }
            if (!StagingLaneAssignmentService.TryFindTripLane(_orderService, _inventoryService, order, LivePalletCount(), out int door, out resolvedLane))
            { failReason = $"no lane at Stage {order.EffectiveStagingDoor} or any nearby door has room for {LivePalletCount()} pallet(s)"; return false; }
            if (!_inventoryService.TryFindStagingSlotInLane(door, resolvedLane, out slot, order.OrderId))
            { failReason = $"lane {door}{resolvedLane} reported room but handed back no slot"; return false; }
            if (!LaneNamingService.TryGetSlotWorldPos(slot.Cell, out pos))
            { failReason = $"slot {slot.Name} has no world position yet"; return false; }
            if (LaneNamingService.TryGetLaneGeometry(slot.DoorNumber, slot.Lane, out var geo)) depthAxis = geo.DepthAxis;
            if (!string.IsNullOrEmpty(resolvedLane) && !(door == order.EffectiveStagingDoor && resolvedLane == order.AssignedLane))
                order.AddOverflowLane(door, resolvedLane);
            return true;
        }

        /// <summary>Anything standing in this lane cell (inbound stock or staged outbound)? Same measurement the
        /// stacking code uses: the top of what's there sits above the bare lane surface (1.15).</summary>
        private static bool CellHasFreight(int door, string lane, Vector2Int cell, GameObject self)
            => TrailerOffloadController.StagingDropBaseY(door, lane, cell, self) > 1.15f + 0.05f;

        private static bool LaneSlotsBlocked(int door, string lane, System.Func<int, bool> slotFilter, GameObject self)
        {
            foreach (var s in LaneNamingService.GetLane(door, lane))
                if (slotFilter(s.Slot) && CellHasFreight(door, lane, s.Cell, self)) return true;
            return false;
        }

        /// <summary>True while the jack is between this aisle's racks (not out on the open floor beyond either end).</summary>
        /// <summary>(Also short of the LAST bay, Tad 2026-11-08: a jack at the docks, just past aisle 02's far end, counted as inside it, so its
        /// first stop read as "behind" - one selector reversed the whole aisle, another parked at the end and walked up it.)</summary>
        private static bool InsideAisle(AisleLane lane, Vector3 p)
            => Mathf.Abs(lane.Lateral(p)) <= lane.Half - AisleInsideMargin && (!lane.HasMin || lane.Along(p) >= lane.AlongMin - 1.5f)
               && (!lane.HasMax || lane.Along(p) <= lane.AlongMax + 1.5f);

        /// <summary>True when <paramref name="p"/> stands inside an aisle (between its racks, first bay to last); names it.</summary>
        internal static bool InAnyAisle(Vector3 p, out string aisle)
        {
            aisle = null;
            if (!TryFindAisleAt(p, out var lane)) return false;
            aisle = lane.Prefix != null ? lane.Prefix.TrimEnd('-') : "?";
            return true;
        }

        /// <summary>The point is inside the aisle, or in its front zone (from the line-up point, AisleEntryLead + 3 m before the first bay, to the last bay).</summary>
        private static bool DestInAisleOrFront(AisleLane a, Vector3 d)
            => a.HasMin && Mathf.Abs(a.Lateral(d)) <= a.Half + 1.5f && a.Along(d) >= a.AlongMin - AisleEntryLead - 3f && a.Along(d) <= a.AlongMax + 1.5f;

        /// <summary>For machines that aren't driven by this class (dock stockers, reach trucks): if <paramref name="p"/> is inside an aisle,
        /// the point up to <paramref name="maxStep"/> metres FORWARD along that aisle's traffic direction, in its right lane, capped just past
        /// the far end. A machine stuck in an aisle walks out this way instead of wherever the nearest mesh is (Tad, 2026-11-08: dock stockers
        /// "driving up and down the aisles" were walk-outs drifting deeper each time). Also names the aisle.</summary>
        internal static bool TryAisleWalkOut(Vector3 p, float maxStep, out Vector3 point, out string aisle)
        {
            point = p; aisle = null;
            if (!TryFindAisleAt(p, out var lane)) return false;
            float to = Mathf.Min(lane.Along(p) + maxStep, lane.AlongMax + AisleEntryLead);
            point = lane.Lane(to); point.y = p.y;
            aisle = lane.Prefix != null ? lane.Prefix.TrimEnd('-') : "?";
            return true;
        }

        /// <summary>REACH TRUCK DEFERRAL (Tad, 2026-11-16): how many manned pallet jacks are working aisle <paramref name="aisleNo"/> or queued at
        /// its mouth (within <paramref name="mouthRadius"/> of <paramref name="gatePoint"/>). A truck about to go in to replenish waits outside
        /// while this is 2 or more, instead of driving into the middle of the group.</summary>
        internal static int JacksCrowdingAisle(int aisleNo, Vector3 gatePoint, float mouthRadius)
        {
            int n = 0;
            string want = aisleNo.ToString("00") + "-";
            foreach (var m in MachineClearance.AllMachines())
            {
                if (m == null || !m.isActiveAndEnabled || !m.IsPalletJack || !m.IsOccupied) continue;
                Vector3 c = m.transform.position;
                if ((TryFindAisleAt(c, 3f, out var a) && a.Prefix == want) || PlanarDist(c, gatePoint) <= mouthRadius) n++;
            }
            return n;
        }

        private static bool FloorOpen(Vector3 p) => !MachineClearance.SolidAt(p, out _) && !InDockApron(p);

        private static bool InDockApron(Vector3 p)
        {
            if (!DockApron.Any) return false;
            if (s_chepGrid == null) s_chepGrid = FindAnyObjectByType<PlacementGrid>();   // reset on Play entry (ResetChepStatics)
            return s_chepGrid != null && DockApron.ContainsCell(s_chepGrid.WorldToCell(p));
        }

        /// <summary>A step from <paramref name="p"/> along <paramref name="dir"/> that leaves the aisle <paramref name="p"/> is in through one
        /// of its ends (not sideways). Shared with the reach trucks.</summary>
        internal static bool IsAisleExitStep(Vector3 p, Vector3 dir, float look, out string prefix)
        {
            prefix = null;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            if (!TryFindAisleAt(p, 0f, out var cur)) return false;
            if (TryFindAisleAt(p + dir * look, 0f, out _)) return false;
            if (Mathf.Abs(Vector3.Dot(dir, cur.Travel)) < 0.5f) return false;
            prefix = cur.Prefix;
            return true;
        }

        private static Vector3 Flat(Vector3 p, float y) => new Vector3(p.x, y, p.z);
        private static float PlanarDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    }
}
