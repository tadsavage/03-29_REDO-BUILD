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
    /// The jack is driven the same hybrid way as the Reach Truck: its own NavMeshAgent for the open-floor legs,
    /// scripted transform moves for the precise parking/backing. While mounted, the selector's own navigation is
    /// off (MHEOperatorSlot), and the short walks off the jack are scripted too, so their positions are exact.
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
        private const float LaneEntryMargin  = 1.0f;   // LaneLineUp stops this far outside the lane's last slot edge before reversing in
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
        private static void ResetJackStatics() { s_jackClaims.Clear(); s_loadedIdleSince.Clear(); s_warnedNoJackDay = -1; s_warnedNoParking = false; s_allJacks.Clear(); s_allJacksAt = -10f; }

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
                if (order == null) continue;
                var task = _workQueue.Tasks.LastOrDefault(t => t.OrderId == order.OrderId && t.Type == WorkTaskType.OrderSelect
                                                               && t.Status != WorkTaskStatus.Cancelled);
                if (task == null) continue;
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
        private bool _jackLegOk;
        private float _forkY;          // current pallet height on the forks (0 = lowered)
        private bool _footScripted;    // own navigation switched off so script can walk the body precisely

        private bool OnJack => _jack != null;
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
            EnsurePallet();
            _offJack = false; _parkedBay = null; _parkedPalletIndex = -1;
            _queuedStop = false; _queueProbe = null; EndTravel();
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
                        Debug.LogWarning($"[OrderSelectionTaskDriver] Order {_currentOrder.OrderId} ({_currentOrder.CustomerName}) has no reachable pick location for its remaining line item(s) — staging short at {_currentOrder.TotalUnitsPicked}/{_currentOrder.TotalUnits}.");
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

                int palletIndex = Mathf.Clamp(_pallets.Count - 1, 0, ForkPalletZ.Length - 1);
                if (!TryGetPickGeometry(location, out Vector3 slotFlat, out Vector3 toAisle))
                {
                    yield return AbandonJackOnFoot($"pick slot {location.Address} has no usable position");
                    yield break;
                }

                // BAY STOPS (Tad, 2026-10-17): the jack parks ONCE per bay, at the bay's first position, and the selector
                // walks to every slot they need in that bay before the jack moves on. Same bay + same pallet = stay off.
                string bayKey = BayKey(location.Address);

                // QUEUE STOP (Tad, 2026-11-07, JackTraffic.cs): the jack is parked in a queue behind another jack and the selector is on
                // foot. While the way is still blocked, walk to anything within QueueWalkMax (10 m) of the jack rather than stand there;
                // once it clears, or the next pick is further than that, step back on and drive as normal.
                QueueWalk queueWalk = null;
                if (_queuedStop && _offJack)
                {
                    // FLUID CASE PICK (Tad, 2026-11-10): the walk-or-drive decision is made here, right after the last case went on the
                    // pallet - and it no longer sends the selector back onto the jack just because the jack ahead moved a little. Keep picking
                    // on foot while this pick is within walking reach; get on and drive only when it isn't, or when the way ahead is clear
                    // AND the walk is longer than a normal pick (MaxPickReach).
                    if (!TryPlanQueueWalk(location, out queueWalk)) { queueWalk = null; _queuedStop = false; }
                    else if (queueWalk.Length > MaxPickReach && RoomToDriveTo(slotFlat))
                    {
                        Debug.Log($"[OrderSelectionTaskDriver] '{name}': the way ahead has cleared and {location.Address} is {queueWalk.Length:0.0}m away - back on the pallet jack to drive up to it.");
                        queueWalk = null; _queuedStop = false;
                    }
                }

                // Never pick from a slot more than MaxPickReach from the pallet: if the parked jack is too far away, re-park
                // right in front of THIS slot instead of walking the cases across the warehouse (Tad, 2026-10-21).
                bool slotTooFar = queueWalk == null && _offJack && palletIndex < _pallets.Count && _pallets[palletIndex] != null &&
                                  PlanarDist(_pallets[palletIndex].transform.position, slotFlat) > MaxPickReach;
                if (slotTooFar)
                    Debug.Log($"[OrderSelectionTaskDriver] '{name}': {location.Address} is more than {MaxPickReach:0}m from the parked jack - re-parking in front of it.");
                if (queueWalk == null && (slotTooFar || !(_offJack && bayKey == _parkedBay && palletIndex == _parkedPalletIndex)))
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
                    _queueProbe = location;   // a long wait on the way lets the selector walk to this slot instead
                    yield return DriveBayLeg(park, parkHeading, $"bay {bayKey}", slotTooFar);
                    _queueProbe = null;
                    if (TakeReplanAfterPass(location)) continue;   // passed a jack: the next pick is chosen from in front of it
                    if (_jackLegOk && !_jackLegQueued) OpsMetrics.Record(OpsMetrics.Kind.JackBayMove, OpsMetrics.Now - bayMoveStart);
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

                    if (_jackLegQueued)
                    {
                        _jackLegQueued = false;
                        // Re-plan from where the jack actually stopped. If it no longer works out, go round again (drive / wait).
                        if (!TryPlanQueueWalk(location, out queueWalk)) continue;
                        BeginQueueStop(bayKey, location, queueWalk);
                        yield return StepOut(queueWalk.Face);
                    }
                    else
                    {
                        // VERIFY THE STOP (Tad, 2026-11-03): the first stop of a bay is never run through the reach check above, and DriveJackTo
                        // leaves the jack where it is when the stop is "behind" it (never back up), so the selector walked up to ~10 m with every
                        // case. If the pallet still isn't within reach of the slot, pull up (reversing if need be) in front of the slot itself.
                        if (!slotTooFar && palletIndex < _pallets.Count && _pallets[palletIndex] != null &&
                            PlanarDist(_pallets[palletIndex].transform.position, slotFlat) > MaxPickReach)
                        {
                            Debug.Log($"[OrderSelectionTaskDriver] '{name}': jack stopped {PlanarDist(_pallets[palletIndex].transform.position, slotFlat):0.0}m from {location.Address} - re-parking in front of it.");
                            ComputeBayPark(slotFlat, toAisle, location, palletIndex, out park, out parkHeading);
                            _queueProbe = location;
                            yield return DriveBayLeg(park, parkHeading, $"bay {bayKey} (reach)", true);
                            _queueProbe = null;
                            if (TakeReplanAfterPass(location)) continue;
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
                        if (_jackLegQueued)
                        {
                            // Held up re-parking: walk from the queue instead.
                            _jackLegQueued = false;
                            if (!TryPlanQueueWalk(location, out queueWalk)) continue;
                            BeginQueueStop(bayKey, location, queueWalk);
                            yield return StepOut(queueWalk.Face);
                        }
                        else
                        {
                            // Off the jack on the rack side (via that side's mount point).
                            yield return StepOut(slotFlat);
                        }
                    }
                    _offJack = true;
                    // A queue stop is not parked AT this bay: once the queue clears, the next pick must re-park, not walk from here.
                    _parkedBay = queueWalk != null ? null : bayKey;
                    _parkedPalletIndex = palletIndex;
                }

                yield return PickCasesFromSlot(location, lineItem, takeQty, slotFlat, toAisle, queueWalk);
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
            _queuedStop = false;

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
            // AISLE TRAFFIC (Tad, 2026-11-08): a jack in an aisle drives in the RIGHT-hand lane, toward the higher bay numbers,
            // dead straight - never parked across the aisle at an angle. Every stop is on that lane line, whichever side the pick is on.
            if (TryGetAisleLane(location, out var lane))
            {
                _lane = lane;
                _laneValid = true;
                _aisleHeading = lane.Travel;
                parkHeading = lane.Travel;
                park = lane.Lane(lane.Along(bayFlat) - ForkPalletZ[palletIndex]);
                park.y = bayFlat.y;
                // Wall / end-of-aisle push and other-jack spacing may only move the stop ALONG the lane, never off it.
                park = lane.Lane(lane.Along(KeepJackBodyOffWalls(park, parkHeading))); park.y = bayFlat.y;
                park = KeepClearOfOtherJacks(park, parkHeading);
                return;
            }
            _laneValid = false;

            Vector3 heading = PickHeading(toAisle, location);
            _aisleHeading = heading;   // straight down the aisle: what a queue stop squares up to (SquareUpInAisle)
            park = bayFlat + toAisle * ParkOffset - heading * ForkPalletZ[palletIndex];

            // End of the aisle: the jack would be left nose-first into the wall. Turn it ~45 degrees out toward the open
            // aisle instead (Tad, 2026-10-18: "turning towards the direction of travel, not driving into the wall").
            parkHeading = heading;
            if (UnityEngine.AI.NavMesh.Raycast(park + Vector3.up * 0.05f, park + heading * 2.5f, out _, UnityEngine.AI.NavMesh.AllAreas))
                parkHeading = (heading + toAisle).normalized;
            park = KeepJackBodyOffWalls(park, parkHeading);
            park = KeepClearOfOtherJacks(park, parkHeading);
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
        private IEnumerator PickCasesFromSlot(LocationData location, OrderLineItem lineItem, int takeQty, Vector3 slotFlat, Vector3 toAisle,
                                              QueueWalk queueWalk = null)
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
            if (_metTripStart < 0f) _metTripStart = OpsMetrics.Now;   // the trip time study (the jack path never started it, so it logged almost nothing)

            for (int i = 0; i < takeQty; i++)
            {
                if (_jack == null || ReturnsRequested) yield break;
                float caseStart = OpsMetrics.Now;

                // To the slot, reach in, take a case. From a queue stop the walk is routed (round racks, jacks and pallets).
                // Straight to the slot: a selector who just stepped off the jack used to walk back to the pallet first (the route starts
                // beside it) - "a touch" to the pallet before every walk (Tad, 2026-11-10). Skip route points already behind them.
                if (queueWalk != null) yield return WalkRouteFromHere(queueWalk.Route);
                else yield return WalkTo(faceSpot);
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
                var activePallet = _pallets[_pallets.Count - 1];
                if (activePallet == null) { DestroyHeldCase(); yield break; }
                Vector3 palletPos = activePallet.transform.position;
                if (queueWalk != null)
                {
                    yield return WalkRoute(queueWalk.Route, true);
                    yield return FaceDir(palletPos - transform.position);
                }
                else
                {
                    Vector3 palletSide = Flat(palletPos - toAisle * (ParkOffset - PalletSideOff), y);
                    yield return WalkTo(palletSide);
                    yield return FaceDir(toAisle);
                }
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
                    if (i < takeQty - 1 && QueueCleared(queueWalk, slotFlat)) yield break;
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

                if (i < takeQty - 1 && QueueCleared(queueWalk, slotFlat)) yield break;
            }
        }

        /// <summary>Walking cases from a queue stop (Tad, 2026-11-08): after EVERY case set on the pallet, check once whether the way ahead
        /// has cleared. If it has, stop walking: the pick loop sees the queue is over (StillQueued false), the selector gets back on, drives
        /// up to the bay and carries on as normal. The cases already walked are on the pallet; the rest of the line is picked from the bay.</summary>
        /// <summary>"Enough room to drive forward" (Tad, 2026-11-10): no jack at all - manned, parked or picking - in the rack lane between
        /// this jack and a jack-length past the slot. StillQueued only looked 4 m ahead at that instant, so the selector got back on as soon
        /// as the jack ahead edged forward, drove a metre, was held again and stepped off again.</summary>
        private bool RoomToDriveTo(Vector3 slotFlat)
        {
            if (_jack == null) return false;
            if (!_laneValid) return !StillQueued(slotFlat);
            var lane = _lane;
            float my = lane.Along(_jack.transform.position) + JackNoseReach;
            float until = lane.Along(slotFlat) + JackClearAhead;
            RefreshJackList();
            foreach (var o in s_allJacks)
            {
                if (o == null || o == _jack) continue;
                if (!JackSpan(lane, o.transform, lane.Offset, JackClearSide, out float rear, out float front)) continue;
                if (front > my && rear < until) return false;
            }
            return !StillQueued(slotFlat);
        }

        /// <summary>(2026-11-10) Only worth getting back on when the walk is longer than a normal pick: a short walk keeps going on foot
        /// (re-mounting after every case was the stop-start Tad saw: off, touch the pallet, walk, back, on, off again...).</summary>
        private bool QueueCleared(QueueWalk walk, Vector3 slotFlat)
        {
            if (walk == null || !_queuedStop || walk.Length <= MaxPickReach || !RoomToDriveTo(slotFlat)) return false;
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': the way ahead has cleared and the walk is {walk.Length:0.0}m - back on the pallet jack to drive up to the bay.");
            return true;
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

            // Lane + slot, retrying while every lane is full (the selector waits on the jack).
            string resolvedLane = null;
            LaneNamingService.LaneSlot slot = default;
            Vector3 slotPos = default, depthAxis = Vector3.forward;
            bool warned = false;
            while (true)
            {
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared", completion); yield break; }
                if (TryResolveStagingTarget(order, out resolvedLane, out slot, out slotPos, out depthAxis, out string fail))
                    break;
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
            bool farBlocked  = LaneSlotsBlocked(door, resolvedLane, s => s > slot.Slot, tipGo);
            bool nearBlocked = LaneSlotsBlocked(door, resolvedLane, s => s < slot.Slot, tipGo);
            if (farBlocked && nearBlocked)
            {
                yield return AbandonJackOnFoot($"lane {door}{resolvedLane} has freight on both sides of slot {slot.Name}", completion);
                yield break;
            }
            Vector3 forward = farBlocked ? -axis : axis;

            // LINE UP (Tad, 2026-10-16): the jack's LaneLineUp transform (rear, at the fork tips) is placed on the
            // lane's centre line just outside its open end, jack facing away from the lane — THEN it reverses
            // straight in. So the jack sits at  lineUp + forward * (anchor→LaneLineUp distance). Turning in place
            // there sweeps the forks round a circle that stays LaneEntryMargin clear of the lane ends, so it never
            // swings over neighbouring lanes, and the reverse starts at the lane's actual entry.
            var laneSlots = LaneNamingService.GetLane(door, resolvedLane);
            Vector3 endSlot = Vector3.zero;
            bool haveEnd = laneSlots.Count > 0 &&
                           LaneNamingService.TryGetSlotWorldPos((farBlocked ? laneSlots[0] : laneSlots[laneSlots.Count - 1]).Cell, out endSlot);
            if (!haveEnd) endSlot = farBlocked ? geo.EntryPoint : geo.ExitPoint - axis * 2f;
            Vector3 lineUpPt = Flat(endSlot + forward * (StagingCellPitch * 0.5f + LaneEntryMargin), driveY);
            Vector3 mouth = lineUpPt + forward * LineUpDistance();

            // ONE JACK AT A TIME AT THE LANE MOUTHS (Tad, 2026-11-08: jacks pulling back out of a lane kept hitting jacks coming in to drop
            // off). Drive to a waiting spot further out, wait while another jack is lining up / backing in / pulling out near this mouth,
            // then claim the area and go in. The claim is released once this jack has pulled back out (or lets go of the jack).
            if (OtherStagingNear(mouth, out var ahead))
            {
                Vector3 waitAt = mouth + forward * StageWaitBack;
                yield return DriveJackTo(waitAt, -forward, $"staging lane {door}{resolvedLane} wait");
                float waited = 0f;
                bool told = false;
                while (_jack != null && OtherStagingNear(mouth, out ahead) && waited < StageWaitMaxReal)
                {
                    if (!told) { told = true; Debug.Log($"[OrderSelectionTaskDriver] '{name}': waiting for {ahead} to clear the staging lanes by {door}{resolvedLane}."); }
                    if (Time.timeScale > 0f) waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (waited >= StageWaitMaxReal)
                    ThrottledLog.Warn("stage-wait-" + name, $"[OrderSelectionTaskDriver] '{name}': waited {StageWaitMaxReal:0}s for {ahead} at the staging lanes by {door}{resolvedLane} - going in.");
                if (_jack == null) { yield return AbandonJackOnFoot("the pallet jack disappeared", completion); yield break; }
            }
            s_stagingClaims[this] = mouth;

            yield return DriveJackTo(mouth, forward, $"staging lane {door}{resolvedLane} line-up");
            if (!_jackLegOk)
            {
                yield return AbandonJackOnFoot($"could not drive the jack to staging lane {door}{resolvedLane}", completion);
                yield break;
            }

            // One machine in a lane at a time (same lock as the reach trucks and dock stockers).
            while (!_inventoryService.TryEnterLaneForDelivery(door, resolvedLane, InventoryService.VehicleLabel(_jack != null ? _jack.transform : null, "pallet jack", "staging")))
                yield return null;
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

            // Drive back out to the line-up spot, release the lane, step off (via a mount point) and leave the jack.
            yield return JackMoveStraight(mouth, JackDriveSpeed);
            ReleaseJackLaneLock();
            // Clear of the lane mouth: pull on out of the way (toward the open floor) before letting the next jack in.
            yield return JackMoveStraight(mouth + forward * StageClearOut, JackDriveSpeed);
            s_stagingClaims.Remove(this);

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
        private static readonly Dictionary<OrderSelectionTaskDriver, Vector3> s_stagingClaims = new();

        /// <summary>Another selector's jack is lining up / backing in / pulling out at a lane mouth within StageClaimRadius of ours.</summary>
        private bool OtherStagingNear(Vector3 mouth, out string who)
        {
            who = null;
            foreach (var kv in s_stagingClaims)
            {
                if (kv.Key == null || kv.Key == this || kv.Key._jack == null) continue;
                if (PlanarDist(kv.Value, mouth) > StageClaimRadius) continue;
                who = kv.Key.name;
                return true;
            }
            return false;
        }

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
                if (_jack != null && _jackLegOk && !_jackLegQueued &&
                    PlanarDist(_jack.transform.position, origin) <= NavLegArriveTolerance)
                {
                    Vector3 to = origin - _jack.transform.position; to.y = 0f;
                    if (to.magnitude > 0.05f)
                    {
                        yield return JackFace(to);
                        yield return JackMoveStraight(origin, JackDriveSpeed);
                    }
                    if (_jack != null && PlanarDist(_jack.transform.position, origin) <= 0.2f)
                    {
                        yield return JackFace(forward);
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
            ReleaseJackLaneLock();
            DestroyHeldCase();
            SetLean(0f);
            SetAnimBool("CarryCase", false);
            SetAnimBool("IsWalking", false);

            var identity = GetComponent<EmployeeIdentity>();
            if (_jack != null)
            {
                if (_jack.CurrentOperator == identity) { _jack.VacateOperator(); _footScripted = false; }
                s_jackClaims.Remove(_jack);
            }
            EndScriptedFoot();   // walked by script but never mounted (e.g. the jack vanished mid step-in)
            if (_jackNav != null) _jackNav.HeldInQueue = false;
            _jack = null;
            _jackNav = null;
            _jackAgent = null;
            _jackRun = null;
            _forkY = 0f;
            _queuedStop = false; _queueProbe = null; _jackLegQueued = false;
            _exitingAisle = false;   // a stopped coroutine never runs ExitAisleTowards' finally
            _passStopAlong = float.MaxValue; _passing = false; _replanFloorKey = long.MinValue; _lastPassFront = float.NaN;
            s_stagingClaims.Remove(this);   // never leave the lane mouths claimed behind us
            EndTravel();

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

        private void CheckNoProgress()
        {
            if (!_taskInProgress || _waitingForStagingSpace || _currentTask == null || _currentOrder == null
                || _currentTask.Type != WorkTaskType.OrderSelect || _currentOrder.ReturnsMode)
            { _noProgressSeconds = 0f; _progressTask = null; return; }

            // Charging aboard a jack is a legitimate long stop.
            if (_jack != null && _jack.GetComponent<EquipmentBattery>() is EquipmentBattery b && b.IsCharging)
            { _noProgressSeconds = 0f; return; }

            // So is waiting in a queue behind a jack that is itself working (JackTraffic.cs). At the bot's 3x speed this watchdog fires
            // after ~3 real seconds, before the 5 s step-off-and-walk, so without this a short queue handed the order back.
            if (_jack != null && QueuedBehindWorkingJack())
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
            _travelDir = Vector3.zero;
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

        /// <summary>NavMesh leg to near <paramref name="target"/>, then a precise scripted finish onto it,
        /// ending with the jack's forward along <paramref name="finalForward"/>. Sets _jackLegOk.</summary>
        private IEnumerator DriveJackTo(Vector3 target, Vector3 finalForward, string phase, bool allowReverse = false)
        {
            _jackLegOk = false;
            _jackLegQueued = false;   // set by a leg that ended in a queue (bay legs only: _queueProbe)
            if (_jack == null) yield break;
            Transform jt = _jack.transform;
            target = Flat(target, jt.position.y);

            // SHORT MOVE ALONG THE AISLE (Tad, 2026-10-17): no turning round to go back a bay or two. Face the aisle
            // heading and drive straight there - forward, or in REVERSE when the target is behind. Only a longer
            // trip (more than ~3 bays) goes through the normal drive-and-turn below.
            Vector3 toT = target - jt.position; toT.y = 0f;
            Vector3 headF = finalForward; headF.y = 0f;
            if (headF.sqrMagnitude > 0.0001f && toT.magnitude <= ReverseMaxDistance &&
                (toT.magnitude < 0.05f || Mathf.Abs(Vector3.Dot(toT.normalized, headF.normalized)) > 0.8f))
            {
                // NEVER BACK UP (Tad, 2026-10-18): a stop behind the jack is not driven to - the selector just walks there
                // on foot. The jack only ever moves forward through an aisle.
                // ...unless the selector would then walk further than MaxPickReach with every case (allowReverse): then back up the short way.
                if (Vector3.Dot(toT, headF) < 0f && toT.magnitude > 0.3f)
                {
                    if (!allowReverse)
                    {
                        _jackLegOk = _jack != null;
                        yield break;
                    }
                    yield return JackFace(headF);
                    yield return JackMoveStraight(target, JackDriveSpeed);   // slides without turning = reverse
                    _jackLegOk = _jack != null;
                    yield break;
                }
                // Face the actual travel vector before moving; otherwise an offset target makes
                // the jack translate diagonally relative to its local Z axis.
                yield return JackFace(toT);
                yield return JackMoveStraight(target, JackDriveSpeed);
                if (_jackLegQueued) { yield return SquareUpInAisle(headF); _jackLegOk = _jack != null; yield break; }   // stopped in a queue: line up, the selector walks
                yield return JackFace(headF);
                _jackLegOk = _jack != null;
                yield break;
            }

            // ONE WAY OUT (Tad, 2026-11-08): "no order selector should ever have a reason to drive down an aisle going in the wrong direction".
            // A NavMesh leg takes the shortest way, which from the far end of an aisle is straight back up it (Paul Cartwright, end of aisle 01,
            // turned round to drive to staging). Leave the aisle forward first, then loop round through an aisle that runs the right way.
            yield return ExitAisleTowards(target, phase);
            if (_jack == null) yield break;

            yield return JackNavLeg(target, phase);
            if (_jackLegQueued) { yield return SquareUpInAisle(finalForward); yield break; }   // _jackLegOk already true: parked in a queue
            if (!_jackLegOk || _jack == null) yield break;

            // TRUST BUT VERIFY (2026-10-23): the nav leg reports "arrived" from AiNavigation's callbacks, which also fire for a
            // partial path or a no-progress timeout (e.g. while this jack was held behind another one). The old code then parked the
            // jack wherever it stood - 15 m from the bay - and the selector walked every case across the warehouse. Re-issue the leg
            // until the jack is really close; if it never gets there, say so instead of pretending.
            for (int retry = 0; retry < NavLegRetries && _jack != null && PlanarDist(jt.position, target) > NavLegArriveTolerance; retry++)
            {
                Debug.LogWarning($"[OrderSelectionTaskDriver] Pallet jack stopped {PlanarDist(jt.position, target):0.0}m short of '{phase}' - re-issuing the drive ({retry + 1}/{NavLegRetries}).");
                yield return JackNavLeg(target, phase);
                if (_jackLegQueued) { yield return SquareUpInAisle(finalForward); yield break; }
                if (!_jackLegOk || _jack == null) yield break;
            }
            if (_jack != null && PlanarDist(jt.position, target) > NavLegArriveTolerance)
            {
                Debug.LogError($"[OrderSelectionTaskDriver] Pallet jack never got within {NavLegArriveTolerance:0}m of '{phase}' ({PlanarDist(jt.position, target):0.0}m short) - giving up on this stop.");
                _jackLegOk = false;
                yield break;
            }

            // Close the last metre along the jack's local Z, then square up to the requested final heading.
            Vector3 to = target - jt.position; to.y = 0f;
            if (to.magnitude > 0.05f && Vector3.Dot(to, finalForward) >= 0f)   // forward only: never reverse the last bit
            {
                yield return JackFace(to);
                yield return JackMoveStraight(target, JackDriveSpeed);
                if (_jackLegQueued) { yield return SquareUpInAisle(finalForward); _jackLegOk = _jack != null; yield break; }
            }
            yield return JackFace(finalForward);
            _jackLegOk = _jack != null;
        }

        /// <summary>The aisle direction of the bay stop being driven to (ComputeBayPark), before any end-of-aisle angle.</summary>
        private Vector3 _aisleHeading;

        /// <summary>QUEUE STOPS PARK STRAIGHT (Tad, 2026-11-07: "pallet jacks parking at an angle in the aisles cause blockages"). A leg
        /// held behind another jack used to end wherever the jack stood - often mid-turn, diagonal across the aisle mouth - and the selector
        /// walked off leaving it there, blocking the jacks behind. Turn in place to the aisle line first (whichever end is nearer, so never
        /// more than 90 degrees). JackFace checks every step of the swing and keeps the heading if the forks would hit anything.</summary>
        private IEnumerator SquareUpInAisle(Vector3 fallback)
        {
            if (_jack == null) yield break;
            // In an aisle there is only one right way to face: the traffic direction (Tad, 2026-11-09). Turned round = righted now.
            if (TryFindAisleAt(JackCentre(_jack.transform), 1.5f, out _)) { yield return RightInAisle("queue stop"); yield break; }
            Vector3 axis = _aisleHeading.sqrMagnitude > 0.0001f ? _aisleHeading : fallback;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.0001f) yield break;
            axis.Normalize();
            Vector3 fwd = _jack.transform.forward; fwd.y = 0f;
            if (Vector3.Dot(fwd, axis) < 0f) axis = -axis;
            if (Vector3.Angle(fwd, axis) < 3f) yield break;
            yield return JackFace(axis);
        }

        private const float InAisleMaxYaw = 30f;   // a jack in an aisle never points further than this off the traffic direction

        /// <summary>RIGHTED IMMEDIATELY (Tad, 2026-11-09): a jack in an aisle pointing more than InAisleMaxYaw off the traffic direction (turned
        /// round, or sideways) turns back to it before doing anything else.</summary>
        private IEnumerator RightInAisle(string when)
        {
            if (_jack == null) yield break;
            Transform jt = _jack.transform;
            if (!TryFindAisleAt(JackCentre(jt), 1.5f, out var a)) yield break;
            Vector3 f = jt.forward; f.y = 0f;
            float off = Vector3.Angle(f, a.Travel);
            if (off <= InAisleMaxYaw) yield break;
            ThrottledLog.Warn("jack-righting-" + name, $"[OrderSelectionTaskDriver] '{name}': pallet jack was pointing {off:0} degrees off the traffic direction in aisle {a.Prefix.TrimEnd('-')} ({when}) - righting it.", 30f);
            yield return JackFace(a.Travel);
        }

        /// <summary>Turns the jack in place. Turning swings 2.5 m of forks round the operator, so every step is checked: a turn that
        /// would sweep into a rack, wall, machine or pallet tries the other way round, and if that is blocked too the jack gives the
        /// turn up after TurnBlockedGiveUpReal and keeps its heading (an aisle is too narrow to spin a jack in - that was the
        /// "turning round" through the racking).</summary>
        private IEnumerator JackFace(Vector3 dir)
        {
            dir.y = 0f;
            if (_jack == null || dir.sqrMagnitude < 0.0001f) yield break;
            Transform jt = _jack.transform;
            dir.Normalize();
            // NEVER TURN ROUND IN AN AISLE (Tad, 2026-11-08: "DO NOT ALLOW IT"). Inside an aisle the jack only ever faces within 100 degrees
            // of that aisle's traffic direction; anything further round is refused and it squares up to the traffic direction instead.
            if (TryFindAisleAt(JackCentre(jt), 1.5f, out var inAisle))
            {
                float off = Vector3.Angle(dir, inAisle.Travel);
                if (off > 100f)
                {
                    ThrottledLog.Warn("jack-noturn-" + name, $"[OrderSelectionTaskDriver] '{name}': refused to turn the pallet jack round in aisle {inAisle.Prefix.TrimEnd('-')} - facing the traffic direction instead.", 30f);
                    dir = inAisle.Travel;
                }
                // NEVER SIDEWAYS (Tad, 2026-11-09): "they should always point down the aisle". The only angle off the aisle line a jack
                // ever takes is a lane change (AisleTraffic.PassAngleDeg); a short sideways shuffle slides at that angle instead of turning.
                else if (off > InAisleMaxYaw)
                    dir = Vector3.RotateTowards(inAisle.Travel, dir, InAisleMaxYaw * Mathf.Deg2Rad, 0f);
            }
            Quaternion goal = Quaternion.LookRotation(dir);
            float sign = 0f, blockedFor = 0f;
            bool flipped = false;
            while (_jack != null && Quaternion.Angle(jt.rotation, goal) > 0.5f)
            {
                Vector3 fwd = jt.forward; fwd.y = 0f;
                float signed = Vector3.SignedAngle(fwd, dir, Vector3.up);
                if (sign == 0f) sign = signed >= 0f ? 1f : -1f;                       // the short way first
                float left = sign > 0f ? (signed >= 0f ? signed : signed + 360f) : (signed <= 0f ? -signed : 360f - signed);
                float step = Mathf.Min(JackTurnSpeed * Time.deltaTime, left);
                Quaternion next = Quaternion.AngleAxis(sign * step, Vector3.up) * jt.rotation;
                if (step > 0f && StepBlocked(jt.position, next, out string what))
                {
                    if (!flipped) { flipped = true; sign = -sign; continue; }           // try swinging the other way round
                    blockedFor += RealDt;
                    if (blockedFor >= TurnBlockedGiveUpReal)
                    {
                        WarnSolid("turning", what);
                        yield break;                                                    // keep the heading it has
                    }
                    yield return null;
                    continue;
                }
                blockedFor = 0f;
                jt.rotation = next;
                if (step >= left - 0.01f) break;
                yield return null;
            }
            if (_jack != null && Quaternion.Angle(jt.rotation, goal) <= 1f) jt.rotation = goal;
        }

        /// <summary>Slides the jack in a straight line without turning (forward or backward). Waits behind a manned jack in its lane of
        /// travel (no time limit: see JackTraffic.cs), and never steps into a rack, wall, machine or pallet. On the way to a bay
        /// (_queueProbe set) a wait of QueueWaitRealSeconds ends the move early (_jackLegQueued) so the selector can walk.</summary>
        private IEnumerator JackMoveStraight(Vector3 target, float speed)
        {
            if (_jack == null) yield break;
            Transform jt = _jack.transform;
            Vector3 flat = Flat(target, jt.position.y);
            float solidFor = 0f;
            while (_jack != null && PlanarDist(jt.position, flat) > 0.02f)
            {
                Vector3 move = flat - jt.position; move.y = 0f;
                _travelDir = move.normalized;
                Vector3 next = Vector3.MoveTowards(jt.position, Flat(flat, jt.position.y), speed * Time.deltaTime);
                bool jackHold = JackAhead(move, out var blocker);
                if (!jackHold && JackBufferHold(next, jt.rotation, out var tooClose)) { jackHold = true; blocker = tooClose; }   // 1 m apart (Tad, 2026-11-10)
                string what = null;
                // Reach trucks have right of way: never come within a metre of a manned one (Tad, 2026-11-09).
                MHEOperatorSlot truck = null;
                bool rtHold = !jackHold && MachineClearance.ReachTruckBufferBlocked(_jack, next, jt.rotation, AisleTraffic.ReachTruckBuffer, out truck);
                if (rtHold) { what = $"reach truck {truck.name} (right of way)"; WarnYield(truck); }
                bool solidHold = !jackHold && !rtHold && StepBlocked(next, jt.rotation, out what);
                bool yieldHold = false;
                if (!jackHold) TrafficBlocker(move.normalized, solidHold, ref blocker, out yieldHold);
                if (AisleExitDue(move, out var exitPrefix, out var exitPoint))
                {
                    yield return JackAisleExitCheck(exitPrefix, exitPoint, move);
                    continue;
                }
                if (jackHold || rtHold || solidHold || yieldHold)
                {
                    NoteHeld(true, blocker, what);
                    if (blocker != null && !jackHold) yield return JackYieldReverse(move, blocker);
                    if (yieldHold) { yield return null; continue; }
                    _legTarget = flat;
                    if (ShouldBackUpFor(out var yieldTo)) { yield return BackUpAndWait(yieldTo); continue; }   // two jacks stuck: the loser backs up 1.5 m
                    var held = jackHold ? DecideHeldInAisle(blocker, flat) : HeldChoice.Wait;
                    // Stopped by the solid guard on a jack poking into the lane (parked at an angle, or with nobody aboard): same choice.
                    if (solidHold && _laneValid && FindParkedJackAhead(_lane, flat, out var stuckIn)) { blocker = stuckIn; held = DecideHeldInAisle(stuckIn, flat); }
                    if (held == HeldChoice.Pass) { _overtakeBlocker = blocker; break; }   // pass it in the other lane (LaneDriveTo)
                    if (held == HeldChoice.Walk) { _jackLegQueued = true; break; }       // the selector walks to the pick
                    if (ShouldStepOffAndWalk()) { _jackLegQueued = true; break; }
                    if (solidHold)
                    {
                        solidFor += RealDt;
                        if (solidFor >= SolidGiveUpReal) { WarnSolid("straight move", what); break; }   // stop here rather than drive through it
                    }
                    yield return null;
                    continue;
                }
                NoteHeld(false, null, null);
                solidFor = 0f;
                jt.position = next;
                yield return null;
            }
            if (_jack != null && PlanarDist(jt.position, flat) <= 0.02f) jt.position = Flat(flat, jt.position.y);
            EndTravel();
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
                    _jackAgent.enabled = true;
                    _jackAgent.Warp(_jack.transform.position);
                    if (_jackAgent.isActiveAndEnabled && _jackAgent.isOnNavMesh) _jackAgent.isStopped = false;
                }
                if (_jackNav != null) _jackNav.enabled = true;
            }
        }

        /// <summary>The jack's own NavMeshAgent carries it across open floor (avoids racks). Mirrors
        /// ReachTruckOperator.SeekViaNavMesh, including its hard-won guards. Leaves the jack in manual mode.</summary>
        /// <summary>For the [JackStall] line: what is physically around a stuck jack (2026-11-07, block 1088: jacks stopped 6-13 m from a bay with a
        /// complete path and nothing the old line could name). Other agents (and whether they are stopped), solid colliders within reach of the
        /// jack's nose, and the gap between where it points and where the agent wants to go.</summary>
        private string DescribeJackSurroundings()
        {
            if (_jack == null) return "";
            Transform jt = _jack.transform;
            Vector3 c = JackCentre(jt);
            var sb = new System.Text.StringBuilder();
            Vector3 dv = _jackAgent != null ? _jackAgent.desiredVelocity : Vector3.zero; dv.y = 0f;
            if (dv.sqrMagnitude > 0.0001f) { Vector3 fw = jt.forward; fw.y = 0f; sb.Append($"turnNeeded={Vector3.SignedAngle(fw, dv, Vector3.up):0}deg "); }
            sb.Append($"avoidPrio={(_jackAgent != null ? _jackAgent.avoidancePriority : -1)} vel={(_jackAgent != null ? _jackAgent.velocity.magnitude : 0f):0.00} ");
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var a in FindObjectsByType<NavMeshAgent>())
            {
                if (a == null || !a.enabled || a.transform.IsChildOf(jt) || a.transform == jt) continue;
                float d = PlanarDist(a.transform.position, c);
                if (d > 4f) continue;
                if (seen.Add(a.name)) sb.Append($"[agent {a.name} {d:0.0}m stopped={a.isStopped} vel={a.velocity.magnitude:0.0}] ");
            }
            var cols = Physics.OverlapSphere(c + jt.forward * 1.2f + Vector3.up * 0.6f, 1.8f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in cols)
            {
                if (col == null || col.transform.IsChildOf(jt) || col.transform == jt) continue;
                if (col.GetComponentInParent<EmployeeIdentity>() != null) continue;
                if (col.GetComponentInParent<PlacedObject>() is PlacedObject po && po.data != null && po.data.isFloor) continue;
                if (seen.Add(col.transform.root.name + col.name)) sb.Append($"[solid {col.transform.root.name}/{col.name}] ");
            }
            return sb.ToString();
        }

        private IEnumerator JackNavLeg(Vector3 target, string phase)
        {
            _jackLegOk = false;
            if (_jack == null || _jackNav == null || _jackAgent == null) yield break;
            Transform jt = _jack.transform;

            // A short hop gains nothing from the mesh and is where it fails (carved slivers). Scripted finish does it.
            if (PlanarDist(jt.position, target) < 2f) { _jackLegOk = true; yield break; }

            SetJackDrive(true, phase);
            float reg = 0f;
            while (_jack != null && !_jackAgent.isOnNavMesh && reg < 1f)
            {
                _jackAgent.Warp(jt.position);
                reg += Time.deltaTime;
                yield return null;
            }
            if (_jack == null || !_jackAgent.isOnNavMesh)
            {
                Debug.LogError($"[OrderSelectionTaskDriver] Pallet jack could not register on the NavMesh for '{phase}'.");
                SetJackDrive(false, phase);
                yield break;
            }

            Vector3 driveTarget = Flat(target, jt.position.y);
            if (NavSample.OnAgentMesh(_jackAgent, driveTarget, 4f, out var hit)) driveTarget = hit.position;
            _legTarget = driveTarget;

            _jackNav.CancelSeekPosition();
            _jackAgent.Warp(jt.position);
            _jackNav.SetTaskBusy(true);

            bool arrived = false;
            _jackNav.SeekPosition(driveTarget, () => arrived = true);
            yield return null;
            // A carve update or rebake can drop a freshly issued path for a frame or two: re-issue for up to a second first.
            for (float tw = 0f; _jack != null && !arrived && !_jackAgent.pathPending && !_jackAgent.hasPath && tw < 1f; tw += Time.unscaledDeltaTime)
            {
                _jackAgent.Warp(jt.position);
                _jackAgent.SetDestination(driveTarget);
                yield return null;
            }
            if (_jack != null && !arrived && !_jackAgent.pathPending && !_jackAgent.hasPath)
            {
                Debug.LogError($"[OrderSelectionTaskDriver] Pallet jack: no path to {driveTarget} for '{phase}'.");
                _jackNav.CancelSeekPosition();
                _jackNav.SetTaskBusy(false);
                SetJackDrive(false, phase);
                yield break;
            }

            float elapsed = 0f, sinceRecheck = 0f, stillFor = 0f, solidFor = 0f;
            int reissues = 0, creeps = 0;
            Vector3 lastPos = jt.position;
            bool stallLogged = false, solidGaveUp = false;
            while (_jack != null && !arrived && _jackNav.IsSeekingTask && elapsed < 45f)
            {
                // Time spent waiting in a queue is not driving time: a long wait behind a working jack must not time the leg out.
                if (_heldRealSeconds <= 0f) elapsed += Time.deltaTime;
                sinceRecheck += Time.deltaTime;

                // Diagnostic: log each stall of 3s+ (not moving > 5 cm), to locate the stuck spot. 0.5s was firing on every pull-away
                // and turn (block 1024: x25 REPEATING at elapsed=1.2s), which buried the real stalls.
                // Measured from an anchor, not frame to frame: a jack at walking pace moves ~2 cm a frame and read as "still".
                if (PlanarDist(jt.position, lastPos) < 0.05f) stillFor += Time.deltaTime;
                else { stillFor = 0f; lastPos = jt.position; }
                if (stillFor >= 3f && !stallLogged)
                {
                    stallLogged = true;
                    Debug.LogWarning($"[JackStall] '{phase}' at {jt.position} (target {driveTarget}) " +
                        $"remaining={_jackAgent.remainingDistance:F2} pathStatus={_jackAgent.pathStatus} " +
                        $"hasPath={_jackAgent.hasPath} pathPending={_jackAgent.pathPending} " +
                        $"desiredVel={_jackAgent.desiredVelocity} stopped={_jackAgent.isStopped} " +
                        $"onMesh={_jackAgent.isOnNavMesh} jackBlocked={JackPathBlocked(_jackAgent.desiredVelocity.sqrMagnitude > 0.01f ? _jackAgent.desiredVelocity : jt.forward)} " +
                        $"elapsed={elapsed:F1}s reissues={reissues} " + DescribeJackSurroundings());
                }
                else if (stillFor == 0f) stallLogged = false;

                // QUEUE + GUARD (Tad, 2026-11-07, JackTraffic.cs). A manned jack in this jack's lane of travel: hold still behind it,
                // for as long as it takes (no "go anyway" - that weaved and drove through). And the agent only steers the jack's centre,
                // so check the whole outline a couple of frames ahead: a step that would put the body or forks into a rack, wall,
                // machine or pallet is not taken. Held velocity is zeroed so a waiting jack doesn't coast or swing round.
                if (_jackAgent.isActiveAndEnabled && _jackAgent.isOnNavMesh)
                {
                    Vector3 dv = _jackAgent.desiredVelocity; dv.y = 0f;
                    Vector3 heading = dv.sqrMagnitude > 0.01f ? dv : jt.forward;
                    _travelDir = heading.normalized;
                    bool jackHold = JackAhead(heading, out var blocker);
                    string what = null;
                    bool solidHold = false, rtHold = false;
                    if (dv.sqrMagnitude > 0.01f)
                    {
                        float look = Mathf.Max(_jackAgent.speed * Time.deltaTime * 2f, 0.15f);
                        Vector3 predPos = jt.position + dv.normalized * look;
                        Quaternion predRot = Quaternion.RotateTowards(jt.rotation, Quaternion.LookRotation(dv.normalized), _jackAgent.angularSpeed * Time.deltaTime * 2f);
                        // Keep a metre from every other manned jack, at any angle (Tad, 2026-11-10: they stuck to each other on open floor).
                        if (!jackHold && JackBufferHold(predPos, predRot, out var tooClose)) { jackHold = true; blocker = tooClose; }
                    }
                    if (!jackHold && dv.sqrMagnitude > 0.01f)
                    {
                        float look = Mathf.Max(_jackAgent.speed * Time.deltaTime * 2f, 0.15f);
                        Vector3 predPos = jt.position + dv.normalized * look;
                        Quaternion predRot = Quaternion.RotateTowards(jt.rotation, Quaternion.LookRotation(dv.normalized), _jackAgent.angularSpeed * Time.deltaTime * 2f);
                        // Reach trucks have right of way: keep a metre off a manned one (Tad, 2026-11-09).
                        rtHold = MachineClearance.ReachTruckBufferBlocked(_jack, predPos, predRot, AisleTraffic.ReachTruckBuffer, out var truck);
                        if (rtHold) { what = $"reach truck {truck.name} (right of way)"; WarnYield(truck); }
                        else solidHold = StepBlocked(predPos, predRot, out what);
                    }
                    bool yieldHold = false;
                    if (!jackHold) TrafficBlocker(heading.normalized, solidHold, ref blocker, out yieldHold);
                    if (AisleExitDue(heading, out var exitPrefix, out var exitPoint))
                    {
                        _jackAgent.isStopped = true; _jackAgent.velocity = Vector3.zero;
                        yield return JackAisleExitCheck(exitPrefix, exitPoint, heading);
                        if (_jack == null) yield break;
                        _jackAgent.isStopped = false;
                    }
                    bool hold = jackHold || solidHold || rtHold || yieldHold;
                    NoteHeld(hold, blocker, what);
                    if (hold && blocker != null && !jackHold)
                    {
                        _jackAgent.isStopped = true; _jackAgent.velocity = Vector3.zero;
                        yield return JackYieldReverse(heading, blocker);
                        if (_jack == null) yield break;
                    }
                    // Held (behind a jack OR by a solid): AiNavigation's 10 s no-progress backstop must not end the leg. It counts GAME seconds
                    // (2 real s at 5x), so a jack held by an UNMANNED jack parked in the aisle was handed back long before the 5-real-s
                    // step-off-and-walk could fire; after 6 re-issues the selector abandoned this jack beside it too (Tad, 2026-11-07:
                    // two empty jacks side by side at the end of aisle 02). Solid holds are bounded by the walk / creeps / SolidGiveUpReal.
                    _jackNav.HeldInQueue = hold;
                    if (_jackAgent.isStopped != hold) _jackAgent.isStopped = hold;
                    if (hold) _jackAgent.velocity = Vector3.zero;

                    if (solidHold)
                    {
                        solidFor += RealDt;
                        // Body / forks would clip something (a corner cut into an aisle, typically): nudge straight on or back a little
                        // so the next attempt swings wider, a few times; then give the leg up rather than drive through.
                        if (solidFor >= SolidCreepAfterReal && creeps < MaxCreepsPerLeg && TryCreep(heading)) { creeps++; solidFor = SolidCreepAfterReal * 0.5f; }
                        if (solidFor >= SolidGiveUpReal) { WarnSolid(phase, what); solidGaveUp = true; break; }
                    }
                    else solidFor = 0f;

                    // Two jacks stuck on each other (aisle mouth): the one further from its goal backs up 1.5 m and waits (Tad, 2026-11-10).
                    if (hold && ShouldBackUpFor(out var yieldTo)) { yield return BackUpAndWait(yieldTo); continue; }

                    // Queued long enough on the way to a bay, and the slot is within walking reach: park here, the selector walks.
                    if (hold && ShouldStepOffAndWalk()) { _jackLegQueued = true; break; }
                }

                if (sinceRecheck >= 0.5f)
                {
                    sinceRecheck = 0f;
                    if (!_jackAgent.isOnNavMesh) _jackAgent.Warp(jt.position);
                    else if (!_jackAgent.pathPending && !_jackAgent.hasPath && reissues < 10)
                    {
                        reissues++;
                        _jackNav.CancelSeekPosition();
                        _jackNav.SeekPosition(driveTarget, () => arrived = true);
                    }
                }
                yield return null;
            }

            EndTravel();
            if (_jackNav != null) _jackNav.HeldInQueue = false;
            if (_jack == null) yield break;
            _jackNav.SetTaskBusy(false);
            SetJackDrive(false, phase);
            if (_jackLegQueued) { _jackLegOk = true; yield break; }   // parked in a queue: the caller has the selector walk
            if (!arrived)
                Debug.LogError($"[OrderSelectionTaskDriver] Pallet jack did not reach '{phase}' ({driveTarget}) after {elapsed:F1}s" +
                               (solidGaveUp ? " - it would have had to drive through something to get there." : "."));
            _jackLegOk = arrived;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Geometry helpers
        // ══════════════════════════════════════════════════════════════════════════════════════════════

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

        private AisleLane _lane;            // the aisle of the bay stop being driven to (ComputeBayPark)
        private bool _laneValid;
        private MHEOperatorSlot _overtakeBlocker;   // set by JackMoveStraight: a parked jack ahead in the lane, to be passed

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

        /// <summary>True while the jack is between this aisle's racks (not out on the open floor beyond either end).</summary>
        /// <summary>(Also short of the LAST bay, Tad 2026-11-08: a jack at the docks, just past aisle 02's far end, counted as inside it, so its
        /// first stop read as "behind" - one selector reversed the whole aisle, another parked at the end and walked up it.)</summary>
        private static bool InsideAisle(AisleLane lane, Vector3 p)
            => Mathf.Abs(lane.Lateral(p)) <= lane.Half - AisleInsideMargin && (!lane.HasMin || lane.Along(p) >= lane.AlongMin - 1.5f)
               && (!lane.HasMax || lane.Along(p) <= lane.AlongMax + 1.5f);

        /// <summary>A bay leg in an aisle (see ComputeBayPark). Outside the aisle: drive to its low-numbered end first and enter facing
        /// the traffic direction. Inside: straight up the right lane, overtaking a parked jack in the left lane. A stop BEHIND the jack
        /// is not driven to (the selector walks) unless <paramref name="allowReverse"/>, when it backs straight down the lane.
        /// Falls back to DriveJackTo when the aisle's geometry is unknown.</summary>
        private IEnumerator DriveBayLeg(Vector3 park, Vector3 heading, string phase, bool allowReverse)
        {
            if (!_laneValid || _jack == null) { yield return DriveJackTo(park, heading, phase, allowReverse); yield break; }
            var lane = _lane;
            Transform jt = _jack.transform;
            _jackLegOk = false;
            _jackLegQueued = false;
            yield return RightInAisle("before a bay leg");   // turned round / sideways in an aisle: righted before it moves
            if (_jack == null) yield break;

            if (!InsideAisle(lane, JackCentre(jt)))
            {
                if (!lane.HasMin) { yield return DriveJackTo(park, heading, phase, allowReverse); yield break; }
                Vector3 entry = lane.Lane(lane.AlongMin - AisleEntryLead); entry.y = park.y;

                // On the far side of the low end (block 1090 log: every "aisle entry" stall): the plain NavMesh route to the entry runs
                // back through an aisle the WRONG way, head-on into its traffic. Loop round like traffic does: up an aisle whose
                // travel runs back toward this aisle's low end, out of its far end, then in here.
                if (lane.Along(jt.position) > lane.AlongMin - AisleEntryLead + 1f)
                {
                    if (TryFindLoopAisle(lane, jt.position, entry, out var via))
                    {
                        var keep = _lane;
                        _lane = via;   // overtaking in the loop aisle is judged against its own lanes
                        _jackLegOk = true;
                        if (!InsideAisle(via, JackCentre(jt)))
                            yield return DriveToAisleMouth(via, phase + " (loop in)");
                        if (_jackLegOk && !_jackLegQueued && _jack != null)
                        {
                            Vector3 vOut = via.Lane(via.AlongMax + AisleEntryLead); vOut.y = park.y;
                            yield return LaneDriveTo(via, vOut);
                        }
                        _lane = keep;
                        if (_jackLegQueued || _jack == null) yield break;
                        _jackLegOk = false;
                    }
                    else
                    {
                        ThrottledLog.Warn("jack-loop-" + name, $"[OrderSelectionTaskDriver] '{name}': no aisle runs back toward the low end of aisle {lane.Prefix} - driving straight to '{phase}'.");
                        yield return DriveJackTo(park, heading, phase, allowReverse);
                        yield break;
                    }
                }
                // DOOR BYPASS (Tad, 2026-11-10, fix B of JackTraffic_2026-11-10.md): a jack parked in the doorway (picking the first
                // bays) shut the only entrance; jacks bound further in queued outside it, where passing isn't allowed. With the
                // passing lane clear, line up on it at the mouth, drive in past the parked jack and pull back in to the rack lane.
                bool bypass = DoorBlocked(lane, park, out var doorJack, out float clearIn, out bool stayOut, out float approachAlong);
                if (bypass)
                {
                    Debug.Log($"[OrderSelectionTaskDriver] '{name}': {doorJack.name} is parked in the door of aisle {lane.Prefix.TrimEnd('-')} - going in on the passing lane.");
                    _passing = true;   // the 1 m jack buffer is off for a deliberate pass (JackBufferHold)
                    try
                    {
                        yield return DriveToAisleMouth(lane, phase + " (aisle entry, passing lane)", true, approachAlong);
                        if (_jackLegQueued || _jack == null) yield break;
                        if (_jackLegOk)
                        {
                            Vector3 w = Flat(lane.OtherLane(clearIn), jt.position.y);
                            yield return JackFace(Flat(w - jt.position, 0f));
                            yield return JackMoveStraight(w, JackDriveSpeed);
                            if (_jackLegQueued || _jack == null) yield break;
                            bool replan = PassReplanFloor(lane, out long floor);
                            _passStopAlong = replan ? float.MaxValue : lane.Along(park);
                            if (stayOut) yield return JackFace(lane.Travel);
                            else yield return SteerIntoLane(lane);   // centre past its front: smooth right turn back in
                            _passStopAlong = float.MaxValue;
                            if (replan && _jack != null) { _replanFloorKey = floor; _jackLegOk = true; yield break; }
                            if (stayOut && _jack != null)
                            {
                                // Parked level with the stop on the passing lane: this leg is done (LaneDriveTo would swing it back
                                // across into the rack lane, i.e. into the parked jack's side).
                                _jackLegOk = true;
                                yield break;
                            }
                        }
                    }
                    finally { _passing = false; }
                }
                else
                    yield return DriveToAisleMouth(lane, phase + " (aisle entry)");
                if (_jackLegQueued || _jack == null) yield break;
                if (!_jackLegOk)
                {
                    ThrottledLog.Warn("jack-entry-" + name, $"[OrderSelectionTaskDriver] '{name}': could not reach the low end of the aisle for '{phase}' - driving straight to the stop instead.");
                    yield return DriveJackTo(park, heading, phase, allowReverse);
                    yield break;
                }
            }

            float ahead = lane.Along(park) - lane.Along(jt.position);
            // Far behind (a new trip, or a stop the jack has already passed): never reverse down the aisle or walk the cases a long way.
            // Drive forward out of the far end and come round to the low end like everyone else (the outside-the-aisle branch above).
            if (ahead < -(allowReverse ? ReverseMaxDistance : MaxPickReach) && !_exitingAisle)
            {
                Debug.Log($"[OrderSelectionTaskDriver] '{name}': '{phase}' is {-ahead:0.0}m behind in aisle {lane.Prefix.TrimEnd('-')} - driving out forward and coming round, not reversing.");
                Vector3 exitFwd = lane.Lane(lane.AlongMax + AisleEntryLead); exitFwd.y = park.y;
                yield return LaneDriveTo(lane, exitFwd);
                if (_jack == null || _jackLegQueued) yield break;
                yield return JackFace(lane.Travel);
                yield return DriveBayLeg(park, heading, phase, allowReverse);   // now outside, past the far end: loops round
                yield break;
            }
            if (ahead < -0.3f)
            {
                // Behind the jack: never turn round in an aisle. Back straight down the lane only when the walk would be too long.
                if (allowReverse)
                {
                    yield return JackFace(lane.Travel);
                    yield return JackMoveStraight(park, JackDriveSpeed);   // slides without turning = reverse
                }
                _jackLegOk = _jack != null;
                yield break;
            }

            yield return LaneDriveTo(lane, park);
            if (_jack == null) yield break;
            if (_replanFloorKey != long.MinValue) { _jackLegOk = true; yield break; }   // passed: the pick loop re-chooses ahead of it
            if (_jackLegQueued) { yield return SquareUpInAisle(lane.Travel); _jackLegOk = _jack != null; yield break; }
            yield return JackFace(lane.Travel);
            _jackLegOk = _jack != null;
        }

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

        /// <summary>True when <paramref name="p"/> stands inside an aisle (between its racks, first bay to last); names it.</summary>
        internal static bool InAnyAisle(Vector3 p, out string aisle)
        {
            aisle = null;
            if (!TryFindAisleAt(p, out var lane)) return false;
            aisle = lane.Prefix != null ? lane.Prefix.TrimEnd('-') : "?";
            return true;
        }

        private const float MouthHopMax = 16f;   // a hop between aisle mouths this long or shorter is driven round the rack end, not pathed

        /// <summary>To the low-numbered mouth of <paramref name="lane"/>, facing into it. From a nearby aisle end (the far end of the aisle just
        /// left) this is a scripted hop round the end of the rack - turn across, drive across beyond the rack ends, turn into the aisle - because
        /// the NavMesh does not see room to turn there and sent jacks back down the aisle they had just left (Tad, 2026-11-08 screenshot: two
        /// jacks driving back down aisle 01 into a reach truck on the way to aisle 02). Falls back to DriveJackTo when the hop is long or blocked.</summary>
        private IEnumerator DriveToAisleMouth(AisleLane lane, string phase, bool passLane = false, float alongOverride = float.NaN)
        {
            if (_jack == null) yield break;
            Transform jt = _jack.transform;
            float at = float.IsNaN(alongOverride) ? lane.AlongMin - AisleEntryLead : alongOverride;
            Vector3 mouth = passLane ? lane.OtherLane(at) : lane.Lane(at);
            mouth.y = jt.position.y;
            if (PlanarDist(jt.position, mouth) <= MouthHopMax)
            {
                yield return JackFace(Flat(mouth - jt.position, 0f));
                yield return JackMoveStraight(mouth, JackDriveSpeed);
                if (_jack == null) yield break;
                if (PlanarDist(jt.position, mouth) <= 0.5f)
                {
                    yield return JackFace(lane.Travel);
                    _jackLegOk = _jack != null;
                    yield break;
                }
                ThrottledLog.Warn("jack-mouth-hop-" + name, $"[OrderSelectionTaskDriver] '{name}': could not drive round the rack end to aisle {lane.Prefix.TrimEnd('-')} ({PlanarDist(jt.position, mouth):0.0}m short) - pathing there instead.");
            }
            yield return DriveJackTo(mouth, lane.Travel, phase);
        }

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

        private bool _exitingAisle;   // ExitAisleTowards drives DriveJackTo legs itself: no re-entry

        /// <summary>Before a NavMesh leg: if the jack is in an aisle, drive FORWARD (the aisle's traffic direction) out of its far end. If the
        /// destination is back the way it came, loop round through an aisle whose traffic runs that way (TryFindLoopAisle) rather than letting
        /// the NavMesh send it back down this one. A destination ahead in this same aisle is left to the caller.</summary>
        /// <summary>PLANNED ROUTE OUT (Tad, 2026-11-08: "when selectors finish selecting their pallet they need to plan their route out and
        /// follow the rules - not just take the shortest path back to the staging lane"). Before any NavMesh leg:
        /// 1. In an aisle: drive forward out of its far end in the right lane.
        /// 2. Look at the shortest path from there. If it runs through an aisle, the NavMesh may not drive it (it ignores lanes and traffic
        ///    direction): use that aisle ourselves if its traffic heads toward the destination, otherwise an aisle whose traffic does
        ///    (TryFindLoopAisle) - round the rack end into its mouth, up its right lane, out of its far end. Repeat until the path is clear of aisles.
        /// A destination inside the aisle the jack is in, ahead of it, is left to the caller (bay legs drive those).</summary>
        private IEnumerator ExitAisleTowards(Vector3 dest, string phase)
        {
            if (_exitingAisle || _jack == null) yield break;
            Transform jt = _jack.transform;

            _exitingAisle = true;
            var keepLane = _lane; bool keepValid = _laneValid;
            var keepProbe = _queueProbe; _queueProbe = null;   // not a bay leg: a queue here is waited out, the selector doesn't walk off
            try
            {
                // 1. Leave the aisle the jack is in, forward. The aisle MOUTH counts: a jack parked at the first bay pair has its centre ~1 m
                // behind its nose, short of the first bay, and used to read as "not in an aisle" and turn round to drive back out the low end.
                if (TryFindAisleAt(JackCentre(jt), AisleEntryLead, out var cur) || TryFindAisleAt(jt.position, AisleEntryLead, out cur))
                {
                    float myAlong = cur.Along(jt.position);
                    if (Mathf.Abs(cur.Lateral(dest)) <= cur.Half && cur.Along(dest) >= myAlong - 0.3f && cur.Along(dest) <= cur.AlongMax + 1.5f)
                        yield break;   // the destination is ahead in this aisle: the caller drives it
                    _lane = cur; _laneValid = true;
                    Vector3 exit = cur.Lane(cur.AlongMax + AisleEntryLead); exit.y = jt.position.y;
                    Debug.Log($"[OrderSelectionTaskDriver] '{name}': leaving aisle {cur.Prefix.TrimEnd('-')} forward, out of its far end, on the way to '{phase}'.");
                    yield return RightInAisle("leaving the aisle");
                    if (_jack == null) yield break;
                    yield return LaneDriveTo(cur, exit);
                    if (_jack == null) yield break;
                    yield return JackFace(cur.Travel);
                }

                // 2. Any aisle still on the way is driven by the rules, not by the NavMesh.
                string lastUsed = null;
                for (int hop = 0; hop < 4 && _jack != null; hop++)
                {
                    if (!FirstAisleOnPath(jt.position, dest, out var crossed)) yield break;   // the rest of the way stays out of the aisles
                    if (Mathf.Abs(crossed.Lateral(dest)) <= crossed.Half && crossed.Along(dest) >= crossed.AlongMin - 1.5f && crossed.Along(dest) <= crossed.AlongMax + 1.5f)
                        yield break;   // the destination is in that aisle: the caller's bay leg enters it properly

                    AisleLane use = crossed;
                    if (Vector3.Dot(crossed.Travel, Flat(dest - jt.position, 0f)) <= 0f && !TryFindLoopAisle(crossed, jt.position, dest, out use))
                    {
                        ThrottledLog.Warn("jack-noroute-" + name, $"[OrderSelectionTaskDriver] '{name}': the way to '{phase}' runs down aisle {crossed.Prefix.TrimEnd('-')} the wrong way and no aisle runs the right way - driving the shortest path.");
                        yield break;
                    }
                    if (use.Prefix == lastUsed) yield break;   // already drove it: don't go round in circles
                    lastUsed = use.Prefix;

                    _lane = use; _laneValid = true;
                    Debug.Log($"[OrderSelectionTaskDriver] '{name}': routing to '{phase}' through aisle {use.Prefix.TrimEnd('-')} with its traffic.");
                    if (!InsideAisle(use, JackCentre(jt)))
                    {
                        yield return DriveToAisleMouth(use, phase + " (route)");
                        if (_jack == null || !_jackLegOk) yield break;
                    }
                    Vector3 uOut = use.Lane(use.AlongMax + AisleEntryLead); uOut.y = jt.position.y;
                    yield return LaneDriveTo(use, uOut);
                    if (_jack == null) yield break;
                    yield return JackFace(use.Travel);
                }
            }
            finally
            {
                _lane = keepLane; _laneValid = keepValid; _queueProbe = keepProbe;
                _exitingAisle = false;
            }
        }

        /// <summary>The first aisle the jack's shortest NavMesh path from <paramref name="from"/> to <paramref name="dest"/> runs through
        /// (between its racks, not just past an end).</summary>
        private bool FirstAisleOnPath(Vector3 from, Vector3 dest, out AisleLane aisle)
        {
            aisle = default;
            if (_jackAgent == null) return false;
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = _jackAgent.agentTypeID, areaMask = _jackAgent.areaMask };
            if (!UnityEngine.AI.NavMesh.SamplePosition(from, out var a, 2f, filter) || !UnityEngine.AI.NavMesh.SamplePosition(dest, out var b, 4f, filter)) return false;
            var path = new UnityEngine.AI.NavMeshPath();
            if (!UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, filter, path) || path.corners.Length < 2) return false;
            var c = path.corners;
            for (int i = 1; i < c.Length; i++)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(PlanarDist(c[i - 1], c[i]) / 0.75f));
                for (int k = 0; k <= n; k++)
                    if (TryFindAisleAt(Vector3.Lerp(c[i - 1], c[i], k / (float)n), 0f, out aisle)) return true;
            }
            return false;
        }

        /// <summary>An aisle (not <paramref name="target"/>'s) whose traffic runs the opposite way, so driving it carries a jack from the
        /// far side back round to the target's low end. Cheapest by distance to its entry (0 when already in it) + its length + exit to
        /// <paramref name="entry"/>.</summary>
        private static bool TryFindLoopAisle(AisleLane target, Vector3 from, Vector3 entry, out AisleLane via)
        {
            via = default;
            bool found = false;
            float best = float.MaxValue;
            var seen = new HashSet<string>();
            foreach (var loc in LocationRegistry.All)
            {
                if (loc == null || !TryParseAddress(loc.Address, out string p, out _, out _)) continue;
                if (p == target.Prefix || !seen.Add(p)) continue;
                if (!TryGetAisleLane(loc, out var b) || !b.HasMin || !b.HasMax) continue;
                if (Vector3.Dot(b.Travel, target.Travel) > -0.7f) continue;   // not running back the other way
                Vector3 bIn = b.Lane(b.AlongMin - AisleEntryLead), bOut = b.Lane(b.AlongMax + AisleEntryLead);
                float cost = (InsideAisle(b, from) ? 0f : Flat(bIn - from, 0f).magnitude)
                           + (b.AlongMax - b.AlongMin) + Flat(entry - bOut, 0f).magnitude;
                if (cost < best) { best = cost; via = b; found = true; }
            }
            return found;
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // FOLLOW, PASS, WALK, LEAVE (Tad, 2026-11-09)
        //  - Follow the jack in front at 1 m (JackClearAhead).
        //  - Held behind a STOPPED jack: if the next pick is more than PassMinPickAhead (4 m) beyond its front, PASS it; otherwise (or if
        //    there is no clean pass) the selector leaves the jack queued behind it and WALKS to the pick.
        //  - The pass: turn 25 degrees left, over to the left side, straighten, drive on up to PassLeftRunBays bays (pulling back in at the
        //    stop if that comes first, never before the tail is clear), turn 25 degrees right, over to the rack, straighten. From 1 m
        //    behind a 25-degree turn would clip its pallet, so the jack backs straight up first when it has to.
        //  - Leaving an aisle: stop at the end, pause, look for cross traffic, pull out (AisleExitStop).
        // ══════════════════════════════════════════════════════════════════════════════════════════════
        private const float PassAngleDeg  = AisleTraffic.PassAngleDeg;   // never turn more than this off the lane to change lanes (Tad: 25)
        private const float PassLookAhead = 14f;    // FindParkedJackAhead: a jack poking into the lane this far ahead counts
        private const float JackNoseReach = 0.8f;   // along the lane, from a jack's pivot to its nose...
        private const float JackTailReach = 2.8f;   // ...and to the back of the pallet on its forks
        private const float PassGap       = 0.8f;   // clearance kept tail-to-nose when pulling back in
        private const float PassMinPickAhead = 4f;  // next pick further than this beyond the jack ahead's front -> pass; nearer -> walk
        private const float PassLeftRunBays  = 4f;  // stay in the left lane for up to this many bays before pulling back in
        private const float PassDecideAfterReal = 0.4f;   // held this long (REAL s) behind a stopped jack before choosing pass / walk
        private const float PassSideMargin = 0.15f; // side clearance kept from the passed jack's pallet while pulling out
        private const float PassMaxBackUp  = 2.5f;  // back straight up at most this far to make room to pull out
        private const float ExitStopLead   = 0.5f;  // a leaving jack stops with its pivot this far past the last bay (nose at the rack end)

        private enum HeldChoice { Wait, Pass, Walk }
        private float _nextHeldChoiceReal;
        private bool _passing;   // DrivePass is running: no new pass / walk decisions until it is done

        /// <summary>Metres of travel a lane change takes at PassAngleDeg.</summary>
        private static float PassRunFor(AisleLane lane) => (lane.Offset - lane.PassOffset) / Mathf.Tan(PassAngleDeg * Mathf.Deg2Rad);

        private static bool IsDriving(MHEOperatorSlot jack)
            => s_jackClaims.TryGetValue(jack, out var od) && od != null && od._travelDir.sqrMagnitude > 0.0001f;

        /// <summary>Held up in an aisle behind <paramref name="blocker"/>: wait, pass it, or (on the way to a pick) step off and walk.</summary>
        private HeldChoice DecideHeldInAisle(MHEOperatorSlot blocker, Vector3 target)
        {
            if (_passing || !_laneValid || blocker == null || _jack == null) return HeldChoice.Wait;
            if (_heldRealSeconds < PassDecideAfterReal || Time.unscaledTime < _nextHeldChoiceReal) return HeldChoice.Wait;
            // Fix B: waiting AT the mouth behind a jack parked in the door counts too - the door check in DriveBayLeg is made once, at the
            // start of the leg, so a jack that parked in the door while this one was on its way left it queued outside (run 4).
            if (!InsideAisle(_lane, JackCentre(_jack.transform)) && !(DoorBypassEnabled && AtAisleMouth(_lane, _jack.transform.position)))
                return HeldChoice.Wait;
            if (IsDriving(blocker)) return HeldChoice.Wait;   // it is moving: follow it at a metre
            _nextHeldChoiceReal = Time.unscaledTime + 1f;

            // Not on the way to a pick (driving out of the aisle, to staging): pass whenever a clean pass exists, else wait.
            if (_queueProbe == null) return TryPlanPass(_lane, blocker, target, out _, out _) ? HeldChoice.Pass : HeldChoice.Wait;

            AisleTraffic.Span(_lane, blocker, out _, out float bFront, out _, out _);
            float pickAhead = _lane.Along(_queueProbe.WorldPosition) - bFront;
            string address = _queueProbe.Address;
            if (pickAhead > PassMinPickAhead)
            {
                if (TryPlanPass(_lane, blocker, target, out _, out _))
                {
                    Debug.Log($"[OrderSelectionTaskDriver] '{name}': next pick {address} is {pickAhead:0.0}m beyond {blocker.name} - passing it.");
                    return HeldChoice.Pass;
                }
            }
            if (TryPlanQueueWalk(_queueProbe, out var walk))
            {
                Debug.Log($"[OrderSelectionTaskDriver] '{name}': behind {blocker.name}, next pick {address} is {pickAhead:0.0}m beyond it" +
                          (pickAhead > PassMinPickAhead ? " but there is no clean pass" : "") + $" - leaving the jack queued and walking {walk.Length:0.0}m to it.");
                _queueAbortBySolid = false;
                return HeldChoice.Walk;
            }
            return HeldChoice.Wait;
        }

        /// <summary>LEAVING AN AISLE (rule 3, Tad 2026-11-09): any drive out through the far end first stops at the end of the aisle, pauses
        /// and checks the cross-aisle before pulling out.</summary>
        private IEnumerator LaneDriveTo(AisleLane lane, Vector3 park)
        {
            if (_jack == null) yield break;
            Transform jt = _jack.transform;
            float stopAlong = lane.AlongMax + ExitStopLead;
            if (lane.HasMax && lane.Along(park) > stopAlong + 0.5f && lane.Along(jt.position) < stopAlong - 0.3f)
            {
                Vector3 stop = lane.Lane(stopAlong); stop.y = park.y;
                yield return LaneDriveCore(lane, stop);
                if (_jack == null || _jackLegQueued || _replanFloorKey != long.MinValue) yield break;
                if (lane.Along(jt.position) >= stopAlong - 0.5f)
                {
                    yield return JackFace(lane.Travel);
                    yield return AisleExitStop(lane);
                    if (_jack == null) yield break;
                }
            }
            yield return LaneDriveCore(lane, park);
        }

        /// <summary>Stopped at the end of an aisle: pause AisleTraffic.ExitPauseGame, then wait while another machine is moving through the
        /// cross-aisle in front (capped at ExitWaitMaxReal; the clearance guard still stops the jack short of anything when it goes).</summary>
        private IEnumerator AisleExitStop(AisleLane lane)
        {
            if (_jack == null) yield break;
            string aisle = lane.Prefix != null ? lane.Prefix.TrimEnd('-') : "?";
            Vector3 zone = lane.Lane(lane.AlongMax + AisleEntryLead + 1.5f);
            AisleTraffic.BeginExitWait(_jack);
            float paused = 0f, waited = 0f;
            bool logged = false;
            while (_jack != null)
            {
                paused += Time.deltaTime;
                bool blocked = AisleTraffic.ExitBlocked(_jack, zone, lane.Prefix, out string who);
                if (!blocked && paused >= AisleTraffic.ExitPauseGame) break;
                if (blocked)
                {
                    waited += RealDt;
                    if (!logged) { logged = true; Debug.Log($"[OrderSelectionTaskDriver] '{name}': stopped at the end of aisle {aisle} - waiting for {who} to clear the cross-aisle."); }
                    if (waited >= AisleTraffic.ExitWaitMaxReal)
                    {
                        ThrottledLog.Warn("jack-exitwait-" + name, $"[OrderSelectionTaskDriver] '{name}': waited {AisleTraffic.ExitWaitMaxReal:0}s at the end of aisle {aisle} for {who} - pulling out.", 60f);
                        break;
                    }
                }
                yield return null;
            }
            if (_jack != null) AisleTraffic.EndExitWait(_jack);
        }

        /// <summary>Forward up the right lane to <paramref name="park"/>. Follows a moving jack at a metre; a stopped one is passed or walked
        /// round (DecideHeldInAisle, inside JackMoveStraight). Every move still goes through the traffic and clearance checks.</summary>
        private IEnumerator LaneDriveCore(AisleLane lane, Vector3 park)
        {
            Transform jt = _jack.transform;
            for (int pass = 0; pass < 6 && _jack != null; pass++)
            {
                _overtakeBlocker = null;
                yield return JackFace(Flat(park - jt.position, 0f));
                yield return JackMoveStraight(park, JackDriveSpeed);
                var blocker = _overtakeBlocker;
                _overtakeBlocker = null;
                if (blocker == null || _jackLegQueued || _jack == null) yield break;
                if (!TryPlanPass(lane, blocker, park, out var route, out float backUp)) continue;
                bool replan = PassReplanFloor(lane, out long floor);
                _passStopAlong = replan ? float.MaxValue : lane.Along(park);
                yield return DrivePass(lane, route, backUp, blocker);
                _passStopAlong = float.MaxValue;
                if (replan && _jack != null && !_jackLegQueued) { _replanFloorKey = floor; yield break; }
                if (_jack == null || _jackLegQueued) yield break;
                // Pulled back in just past the stop (the pick was only a little beyond the jack passed): stop here, never reverse up the
                // aisle. The pick is still within reach; if not, the caller re-parks.
                if (lane.Along(park) < lane.Along(jt.position) - 0.3f)
                {
                    Debug.Log($"[OrderSelectionTaskDriver] '{name}': pulled back in {lane.Along(jt.position) - lane.Along(park):0.0}m past the stop after passing - stopping here.");
                    yield break;
                }
            }
        }

        /// <summary>Where another jack sits along the lane, from its WHOLE length (nose, centre, tail) - not just its pivot. A jack parked at an
        /// angle or off the lane line (old 45-degree end-of-aisle parks, abandoned jacks) pokes into the lane with its tail or nose while its
        /// centre is outside it; judging by the centre alone missed it, and the solid guard then stopped the jack behind with no way round
        /// (block 1090: Paul Cartwright held at (-25.5, 41.9) "would have driven it into PltJack_FULL").</summary>
        private static bool JackSpan(AisleLane lane, Transform o, float lateral, float halfWidth, out float rear, out float front)
        {
            Vector3 f = o.forward; f.y = 0f; f = f.sqrMagnitude > 0.0001f ? f.normalized : lane.Travel;
            Vector3 nose = o.position + f * JackNoseReach, tail = o.position - f * JackTailReach, mid = JackCentre(o);
            rear = Mathf.Min(lane.Along(nose), lane.Along(tail));
            front = Mathf.Max(lane.Along(nose), lane.Along(tail));
            float ln = lane.Lateral(nose), lt = lane.Lateral(tail), lm = lane.Lateral(mid);
            // Closest the jack's centre line comes to the given lateral line.
            float lo = Mathf.Min(ln, Mathf.Min(lt, lm)), hi = Mathf.Max(ln, Mathf.Max(lt, lm));
            float gap = lateral < lo ? lo - lateral : lateral > hi ? lateral - hi : 0f;
            return gap < halfWidth;
        }

        /// <summary>The nearest jack standing still in THIS jack's lane, ahead of it within PassLookAhead and short of the stop.</summary>
        private bool FindParkedJackAhead(AisleLane lane, Vector3 park, out MHEOperatorSlot blocker)
        {
            blocker = null;
            if (_jack == null || lane.Offset - lane.PassOffset < LaneMinPassOffset) return false;
            RefreshJackList();
            float my = lane.Along(_jack.transform.position), stop = lane.Along(park), best = float.MaxValue;
            foreach (var o in s_allJacks)
            {
                if (o == null || o == _jack) continue;
                if (!JackSpan(lane, o.transform, lane.Offset, JackClearSide, out float rear, out float front)) continue;   // not in my lane
                if (front <= my + JackNoseReach || rear - my > PassLookAhead || rear > stop) continue;
                if (IsDriving(o)) continue;                                                                                   // moving: queue, don't pass
                if (rear < best) { best = rear; blocker = o; }
            }
            return blocker != null;
        }

        /// <summary>The pass round <paramref name="blocker"/> (see the block comment above): out to the left lane at PassAngleDeg, along it,
        /// back in at PassAngleDeg. <paramref name="backUp"/> = metres to reverse straight first. False when the left lane isn't clear for the
        /// whole pass, there's no room to back up, or pulling back in would end beyond pick reach of the stop.</summary>
        private bool TryPlanPass(AisleLane lane, MHEOperatorSlot blocker, Vector3 park, out List<Vector3> route, out float backUp)
        {
            route = null; backUp = 0f;
            if (_jack == null || blocker == null || lane.Offset - lane.PassOffset < LaneMinPassOffset) return false;
            Transform jt = _jack.transform;
            // The pass lane must clear it too: a jack parked across both lanes can't be passed.
            if (JackSpan(lane, blocker.transform, lane.PassOffset, JackClearSide * 0.75f, out _, out _)) return false;

            var myF = MachineClearance.FootprintOf(_jack);
            float my = lane.Along(jt.position);
            AisleTraffic.Span(lane, blocker, out float bRear, out float bFront, out float bLatMin, out _);
            float run = PassRunFor(lane);
            float ang = PassAngleDeg * Mathf.Deg2Rad;

            // Out: the front right corner must be clear of the blocker's left side before the nose reaches its tail. Turning 25 degrees in
            // place swings that corner left by MaxX(1 - cos) + MaxZ sin; the rest comes from driving forward at 25 degrees.
            float cornerShift = myF.MaxX * (1f - Mathf.Cos(ang)) + myF.MaxZ * Mathf.Sin(ang);
            float needLat = (lane.Lateral(jt.position) + myF.MaxX - cornerShift) - (bLatMin - PassSideMargin);
            float needRun = Mathf.Max(0f, needLat) / Mathf.Tan(ang);
            float gap = bRear - (my + myF.MaxZ) - PassSideMargin;
            backUp = Mathf.Max(0f, needRun - Mathf.Max(0f, gap));
            if (backUp > PassMaxBackUp) return false;
            if (backUp > 0.05f)
            {
                Vector3 back = jt.position - lane.Travel * backUp;
                if (JackBehindWithin(lane, my + myF.MinZ - backUp - 1f, my)) return false;   // someone right behind: no room to back up
                if (!MachineClearance.StraightSlideClear(_jack, back)) return false;
            }
            float outStart = my - backUp, outEnd = outStart + run;

            // Back in (Tad, 2026-11-10): as soon as this jack's CENTRE is past the front of the jack being passed, a smooth right turn
            // back into the rack lane (SteerIntoLane), then on until parallel with the rack faces again. inEnd is only for the checks below.
            bFront = ParkedGroupFront(lane, bFront, blocker);
            _lastPassFront = bFront;
            float inStart = Mathf.Max(outEnd, bFront + JackCentreBack);
            float inEnd = inStart + run * 1.6f;
            if (inEnd > lane.Along(park) + MaxPickReach - 1f) return false;

            // The left lane, the whole way: no other machine in it (jacks, reach trucks, anything).
            float half = myF.MaxX + 0.1f;
            foreach (var o in AisleTraffic.Machines)
            {
                if (o == null || o == _jack || o == blocker) continue;
                AisleTraffic.Span(lane, o, out float oRear, out float oFront, out float oLatMin, out float oLatMax);
                if (oFront < outStart - 1f || oRear > inEnd + 1f) continue;
                if (oLatMax < lane.PassOffset - half || oLatMin > lane.PassOffset + half) continue;
                return false;
            }

            route = new List<Vector3> { lane.OtherLane(outEnd), lane.OtherLane(inStart) };   // DrivePass then steers back in
            return true;
        }

        /// <summary>Fix B switch (dev / A-B testing with JackTrafficRecorder). On by default.</summary>
        public static bool DoorBypassEnabled = true;
        private const float DoorZoneBays = 2f;

        /// <summary>Standing at the low-numbered mouth of the aisle, lined up to go in (from the entry point to the first bay).</summary>
        private static bool AtAisleMouth(AisleLane lane, Vector3 p)
            => lane.HasMin && Mathf.Abs(lane.Lateral(p)) <= lane.Half && lane.Along(p) >= lane.AlongMin - AisleEntryLead - 1.5f
               && lane.Along(p) <= lane.AlongMin + 0.5f;   // a jack parked within this many bays of an aisle's first bay is "in the door"

        /// <summary>Fix B (DriveBayLeg): a STOPPED jack sits in the right lane in the door of <paramref name="lane"/>, this jack's stop is
        /// past it, and the passing lane is clear of every machine from the mouth until this jack is back in. <paramref name="clearIn"/> =
        /// along-position where this jack's tail has cleared the parked jack(s) and it can start pulling back in.</summary>
        private bool DoorBlocked(AisleLane lane, Vector3 park, out MHEOperatorSlot blocker, out float clearIn, out bool stayOut)
            => DoorBlocked(lane, park, out blocker, out clearIn, out stayOut, out _);

        /// <summary>As above; <paramref name="approach"/> = along-position to line up on the passing lane: the usual mouth, or further back
        /// when a parked jack's forks stick out of the door (getting onto the passing lane level with them meant crossing them).</summary>
        private bool DoorBlocked(AisleLane lane, Vector3 park, out MHEOperatorSlot blocker, out float clearIn, out bool stayOut, out float approach)
        {
            blocker = null; clearIn = 0f; stayOut = false; approach = lane.AlongMin - AisleEntryLead;
            if (!DoorBypassEnabled || _jack == null || !lane.HasMin || lane.Offset - lane.PassOffset < LaneMinPassOffset) return false;
            float pitch = lane.BayPitch > 0.5f ? lane.BayPitch : 2.65f;
            float mouth = lane.AlongMin - AisleEntryLead;
            float frontMax = float.MinValue, rearMin = float.MaxValue;
            RefreshJackList();
            foreach (var o in s_allJacks)
            {
                if (o == null || o == _jack) continue;
                if (!JackSpan(lane, o.transform, lane.Offset, JackClearSide, out float rear, out float front)) continue;   // not in the rack lane
                if (rear > lane.AlongMin + DoorZoneBays * pitch || front < mouth - 1f) continue;                          // not in the door
                if (IsDriving(o)) continue;                                                                                 // moving: just follow it
                if (front > frontMax) { frontMax = front; blocker = o; }
                rearMin = Mathf.Min(rearMin, rear);
            }
            if (blocker == null) return false;
            // Lining up further back (behind forks that stick out of the door) put the approach among the staged pallets at the dock and
            // the jack sat there (2026-11-10 pass test). Only bypass when the parked jack's tail is clear of the mouth.
            if (rearMin < mouth + 1f) return false;

            var myF = MachineClearance.FootprintOf(_jack);
            float run = PassRunFor(lane);
            frontMax = ParkedGroupFront(lane, frontMax, null);
            _lastPassFront = frontMax;
            clearIn = frontMax + JackCentreBack;   // pull back in once this jack's CENTRE is past the parked jack's front (Tad)
            stayOut = false;
            float stop = lane.Along(park);
            // The stop is beside the parked jack: queue / walk as before. (Parking on the passing lane level with it was tried and taken
            // out the same day: Elizabeth parked in the passing lane beside Susan, then both lanes were shut and Ronald sat 20 s at the door.)
            if (stop < clearIn + run - 0.5f) return false;

            // The passing lane, mouth to pull-in: nothing else in it (other jacks, reach trucks, dock stockers).
            float half = myF.MaxX + 0.1f;
            foreach (var o in AisleTraffic.Machines)
            {
                if (o == null || o == _jack || o == blocker) continue;
                AisleTraffic.Span(lane, o, out float oRear, out float oFront, out float oLatMin, out float oLatMax);
                if (oFront < mouth - 2f || oRear > clearIn + run + 1f) continue;
                if (oLatMax < lane.PassOffset - half || oLatMin > lane.PassOffset + half) continue;
                return false;
            }
            return true;
        }

        /// <summary>A jack in this jack's lane behind it, with its front beyond <paramref name="limit"/> (and behind <paramref name="my"/>).</summary>
        private bool JackBehindWithin(AisleLane lane, float limit, float my)
        {
            RefreshJackList();
            foreach (var o in s_allJacks)
            {
                if (o == null || o == _jack) continue;
                if (!JackSpan(lane, o.transform, lane.Offset, JackClearSide, out float rear, out float front)) continue;
                if (front > limit && rear < my) return true;
            }
            return false;
        }

        private IEnumerator DrivePass(AisleLane lane, List<Vector3> route, float backUp, MHEOperatorSlot blocker)
        {
            Transform jt = _jack.transform;
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': passing {(blocker != null ? blocker.name : "the jack ahead")} on the left" +
                      (backUp > 0.05f ? $" (backing up {backUp:0.0}m first to turn out cleanly)" : "") + ".");
            _passing = true;
            try
            {
                if (backUp > 0.05f)
                {
                    yield return JackMoveStraight(jt.position - lane.Travel * backUp, JackReverseSpeed);   // straight back, no turning
                    if (_jack == null || _jackLegQueued) yield break;
                }
                foreach (var w0 in route)
                {
                    Vector3 w = Flat(w0, jt.position.y);
                    _overtakeBlocker = null;
                    yield return JackFace(Flat(w - jt.position, 0f));
                    yield return JackMoveStraight(w, JackDriveSpeed);
                    if (_jack == null || _jackLegQueued) yield break;
                }
                if (_jack != null) yield return SteerIntoLane(lane);   // smooth right turn back in, on until parallel with the racks
            }
            finally { _passing = false; }
        }

        /// <summary>The front of the group of STOPPED jacks in the rack lane starting at <paramref name="front"/>: a parked jack whose rear
        /// is within a pull-in of it is part of the same obstacle, so the passer stays out until it is past that one too (2026-11-10:
        /// Ronald passed Elizabeth and steered back in straight at Susan, parked 4.3 m further on).</summary>
        private float ParkedGroupFront(AisleLane lane, float front, MHEOperatorSlot except)
        {
            float pullIn = PassRunFor(lane) * 1.6f + PassGap;
            RefreshJackList();
            for (int guard = 0; guard < 6; guard++)
            {
                bool grew = false;
                foreach (var o in s_allJacks)
                {
                    if (o == null || o == _jack || o == except || IsDriving(o)) continue;
                    if (!JackSpan(lane, o.transform, lane.Offset, JackClearSide, out float rear, out float f)) continue;
                    if (f <= front || rear > front + JackCentreBack + pullIn) continue;
                    front = f; grew = true;
                }
                if (!grew) break;
            }
            return front;
        }

        private const float SteerLookAhead = 2.2f;    // aims at the rack-lane line this far ahead: bigger = gentler pull-in (3.0 took ~10 m)
        private const float SteerTurnRate  = 40f;     // deg per game second while driving (a jack turning in place does 110)
        private float _passStopAlong = float.MaxValue;   // the stop being driven to: a pull-in ends there, never past it
        private float _lastPassFront = float.NaN;         // along-front of the jack (group) the current pass goes round
        private long _replanFloorKey = long.MinValue;     // set after a pass: the pick loop re-chooses its next pick from here on

        /// <summary>LEAPFROG (Tad, 2026-11-10): "drive past the jack, then find the next available pick in front of the jack passed - the next
        /// facing, plus one more, to gain a little distance on him - and start picking there", instead of the old stop (behind or beside
        /// it), which made the passer drive out of the far end and come all the way round. True with the new sequence floor when the
        /// current stop is short of that point AND the order has a pick at or past it. Unplanned trips only (skipping strands a plan's lines).</summary>
        /// <summary>Pick loop side of the leapfrog: a pass set a new floor, so move this trip's sequence floor there and let the loop choose
        /// the next pick again (the old stop's reservation is replaced by the next ReservePick).</summary>
        private bool TakeReplanAfterPass(LocationData oldStop)
        {
            if (_replanFloorKey == long.MinValue) return false;
            long floor = _replanFloorKey;
            _replanFloorKey = long.MinValue;
            _lastPassFront = float.NaN;
            if (floor <= _pickFloorKey) return false;
            _pickFloorKey = floor;
            Debug.Log($"[OrderSelectionTaskDriver] '{name}': passed the jack ahead - skipping {oldStop?.Address} and picking on from the bay past it.");
            return true;
        }

        private bool PassReplanFloor(AisleLane lane, out long floor)
        {
            floor = long.MinValue;
            if (_queueProbe == null || float.IsNaN(_lastPassFront) || _currentOrder == null) return false;
            if (_currentTask != null && _currentTask.HasPlan) return false;
            if (!TryParseAddress(_queueProbe.Address, out string prefix, out _, out string suffix) || !int.TryParse(prefix.Substring(0, 2), out int aisle)) return false;
            int next = -1;
            for (int b = 1; b <= 99; b++)
            {
                if (!LocationRegistry.TryGet($"{prefix}{b:00}{suffix}", out var loc) || loc == null) continue;
                if (lane.Along(loc.transform.position) > _lastPassFront) { next = b; break; }
            }
            if (next < 0) return false;                       // nothing in front of it in this aisle: keep the stop
            int pairStart = next % 2 == 1 ? next : next - 1;   // facing bays share a number pair (odd / even sides)
            floor = ((long)aisle * 1000 + pairStart + 2) * 1000;   // the next facing, plus one more
            if (OrderPickPath.SequenceKey(_queueProbe.Address) >= floor) return false;   // the stop is already far enough on
            string tid = _currentTask != null ? _currentTask.TaskId : null;
            var order = _currentOrder;
            return OrderPickPath.ChooseNext(order, li => _orderService.SelectableRemaining(order, li, tid), out _, out _, out _, floor);
        }
        private const float SteerMaxRun    = 14f;     // never more than this far along the aisle to get back in

        /// <summary>PULLING BACK IN after a pass (Tad, 2026-11-10): drive FORWARD while turning gently right toward the rack lane (aiming at
        /// the lane line SteerLookAhead ahead), and keep going until the jack is on the lane line and parallel with the rack faces. Every
        /// step goes through the same jack / reach-truck / solid checks as a straight move; held for SolidGiveUpReal, it finishes with an
        /// in-place JackFace instead.</summary>
        private IEnumerator SteerIntoLane(AisleLane lane)
        {
            if (_jack == null) yield break;
            Transform jt = _jack.transform;
            float startAlong = lane.Along(jt.position), heldFor = 0f;
            while (_jack != null)
            {
                Vector3 p = jt.position;
                float along = lane.Along(p);
                float latErr = lane.Lateral(p) - lane.Offset;
                Vector3 fwd = Flat(jt.forward, 0f).normalized;
                float yawErr = Vector3.Angle(fwd, lane.Travel);
                if (Mathf.Abs(latErr) < 0.05f && yawErr < 1.5f) break;          // in the lane and parallel: done
                if (along - startAlong > SteerMaxRun) break;
                if (along >= _passStopAlong - 0.3f) break;                       // reached the stop mid pull-in: stop here, pick from here

                Vector3 aim = lane.Lane(along + SteerLookAhead); aim.y = p.y;
                Vector3 want = Flat(aim - p, 0f);
                if (want.sqrMagnitude < 0.0001f) want = lane.Travel;
                Quaternion nextRot = Quaternion.RotateTowards(jt.rotation, Quaternion.LookRotation(want.normalized), SteerTurnRate * JackSpeedMul * Time.deltaTime);
                Vector3 nextFwd = Flat(nextRot * Vector3.forward, 0f).normalized;
                Vector3 next = p + nextFwd * (JackDriveSpeed * Time.deltaTime);
                _travelDir = nextFwd;

                bool hold = JackAhead(nextFwd, out var blocker);
                MHEOperatorSlot truck = null;
                string what = null;
                if (!hold && MachineClearance.ReachTruckBufferBlocked(_jack, next, nextRot, AisleTraffic.ReachTruckBuffer, out truck)) { hold = true; WarnYield(truck); }
                if (!hold && StepBlocked(next, nextRot, out what)) hold = true;
                if (hold)
                {
                    NoteHeld(true, blocker, what);
                    heldFor += RealDt;
                    if (heldFor >= SolidGiveUpReal) break;
                    yield return null;
                    continue;
                }
                NoteHeld(false, null, null);
                heldFor = 0f;
                jt.position = next;
                jt.rotation = nextRot;
                yield return null;
            }
            EndTravel();
            if (_jack != null) yield return JackFace(lane.Travel);   // square up exactly (a no-op when the steer finished parallel)
        }

        // ── Keeping pallet jacks apart (Tad, 2026-10-17: two selectors on one order drove over each other) ──
        private const float JackCentreBack = 1.0f;     // the forks trail behind the body: the jack's middle is ~1 m back
        private const float JackClearAhead = 4.1f;     // centre-to-centre gap that counts as "in the way": ~1 m nose to tail (Tad, 2026-11-09: follow at a metre)
        private const float JackClearSide = 1.2f;      // ...and side to side (further off = the other lane is free: pass)
        // There is no "wait N seconds then go anyway" any more (it was 6 s): that is what drove jacks through each other. A head-on
        // meeting is settled by right of way (JackTraffic.cs), a long queue by stepping off and walking.
        private static readonly List<MHEOperatorSlot> s_allJacks = new List<MHEOperatorSlot>();
        private static float s_allJacksAt = -10f;

        private static Vector3 JackCentre(Transform t) => t.position - t.forward * JackCentreBack;

        /// <summary>Two selectors on one order park at the same bay: both jacks used to take the SAME stop and sit on top of each
        /// other. If another jack is within a jack's length of this stop, park one jack-length further back along the aisle instead
        /// (still within pick reach of the bay), as long as that spot is on the NavMesh.</summary>
        private Vector3 KeepClearOfOtherJacks(Vector3 park, Vector3 heading)
        {
            if (_jack == null) return park;
            heading.y = 0f; if (heading.sqrMagnitude < 0.0001f) return park; heading.Normalize();
            const float Spacing = 4.3f;   // a jack is ~3.3 m long + the 1 m JackBuffer (was 3.4: parked nose to tail)
            for (int attempt = 0; attempt < 3; attempt++)
            {
                bool taken = false;
                foreach (var other in FindObjectsByType<MHEOperatorSlot>())
                {
                    if (other == null || other == _jack || !other.isActiveAndEnabled || !other.IsPalletJack || other.GetComponent<PlacedObject>() == null) continue;
                    if (PlanarDist(JackCentre(other.transform), park - heading * JackCentreBack) < Spacing) { taken = true; break; }
                }
                if (!taken) break;
                Vector3 shifted = park - heading * Spacing;
                if (!UnityEngine.AI.NavMesh.SamplePosition(shifted, out var onMesh, 0.6f, UnityEngine.AI.NavMesh.AllAreas)) break;
                // Never pushed back OUT of an aisle into its doorway (2026-11-10: Elizabeth's bay 01-01 stop moved to the mouth and shut it).
                if (TryFindAisleAt(park, 0f, out _) && !TryFindAisleAt(shifted, 0f, out _)) break;
                park = shifted;
            }
            return park;
        }

        /// <summary>A manned pallet jack sits in this jack's lane of travel and this jack must wait for it (see JackAhead,
        /// OrderSelectionTaskDriver.JackTraffic.cs - right of way, passing, queueing).</summary>
        private bool JackPathBlocked(Vector3 moveDir) => JackAhead(moveDir, out _);

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

        private static Vector3 Flat(Vector3 p, float y) => new Vector3(p.x, y, p.z);
        private static float PlanarDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    }
}
