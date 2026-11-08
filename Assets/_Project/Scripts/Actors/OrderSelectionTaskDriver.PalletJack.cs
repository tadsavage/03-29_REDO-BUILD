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
                    if (!StillQueued(slotFlat) || !TryPlanQueueWalk(location, out queueWalk)) { queueWalk = null; _queuedStop = false; }
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
                    yield return DriveJackTo(park, parkHeading, $"bay {bayKey}", slotTooFar);
                    _queueProbe = null;
                    if (_jackLegOk && !_jackLegQueued) OpsMetrics.Record(OpsMetrics.Kind.JackBayMove, OpsMetrics.Now - bayMoveStart);
                    if (!_jackLegOk)
                    {
                        yield return AbandonJackOnFoot($"could not drive the jack to bay {bayKey}");
                        yield break;
                    }
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
                            yield return DriveJackTo(park, parkHeading, $"bay {bayKey} (reach)", true);
                            _queueProbe = null;
                            if (!_jackLegOk)
                            {
                                yield return AbandonJackOnFoot($"could not drive the jack to bay {bayKey}");
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
                if (queueWalk != null) yield return WalkRoute(queueWalk.Route, false);
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

        /// <summary>Drives the (empty) jack to the nearest free Pallet Jack Parking bay. With no bay placed it stays
        /// at the lane's line-up spot and the player is told once.</summary>
        private IEnumerator ParkJack()
        {
            if (_jack == null) yield break;
            var spot = PalletJackParkingSpot.FindNearestFree(_jack.transform.position, _jack);
            if (spot == null)
            {
                if (!s_warnedNoParking)
                {
                    s_warnedNoParking = true;
                    UIToast.Show("Pallet jacks have nowhere to park - place Pallet Jack Parking (Vehicles) so they stop blocking the lanes.");
                }
                yield break;
            }
            spot.Reserve(_jack);
            spot.ParkPose(_jack.transform.forward, out Vector3 origin, out Vector3 forward);
            yield return DriveJackTo(origin, forward, "pallet jack parking");
            if (!_jackLegOk)
            {
                if (_jack != null) spot.Release(_jack);
                Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' couldn't drive the pallet jack to its parking bay — leaving it where it is.");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════════════════════════
        // Fallback + release
        // ══════════════════════════════════════════════════════════════════════════════════════════════

        /// <summary>Gives the jack back and carries on ON FOOT with whatever is on the forks (the old
        /// placeholder: pallets ride the selector). The order is never lost to a jack problem.</summary>
        private IEnumerator AbandonJackOnFoot(string reason, OrderData.OrderStatus? stagingWith = null)
        {
            Debug.LogWarning($"[OrderSelectionTaskDriver] '{name}' leaving the pallet jack ({reason}) — carrying on by hand with order {_currentOrder?.OrderId}.");
            DestroyHeldCase();
            ReleaseJack();
            for (int i = 0; i < _pallets.Count; i++)
            {
                var p = _pallets[i];
                if (p == null) continue;
                p.transform.SetParent(transform, false);
                p.transform.localPosition = PalletOffsets[Mathf.Min(i, PalletOffsets.Length - 1)];
                p.transform.localRotation = Quaternion.identity;
            }
            yield return null; // let the selector's agent register again after re-enabling
            if (_currentOrder == null) yield break;
            if (stagingWith.HasValue) FinishOrder(stagingWith.Value);
            else AdvanceToNextPick();
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

            _deadJackRiderSeconds += Time.deltaTime;   // game minutes
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
        private const float NoProgressGameMinutes = 10f;   // Time.deltaTime is game minutes (1 game minute = 1 scaled second)
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
            _noProgressSeconds += Time.deltaTime;
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
                // Same as losing the jack: pallets ride the selector and are staged on foot.
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
            Vector3 axis = _aisleHeading.sqrMagnitude > 0.0001f ? _aisleHeading : fallback;
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.0001f) yield break;
            axis.Normalize();
            Vector3 fwd = _jack.transform.forward; fwd.y = 0f;
            if (Vector3.Dot(fwd, axis) < 0f) axis = -axis;
            if (Vector3.Angle(fwd, axis) < 3f) yield break;
            yield return JackFace(axis);
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
                string what = null;
                bool solidHold = !jackHold && StepBlocked(next, jt.rotation, out what);
                if (jackHold || solidHold)
                {
                    NoteHeld(true, blocker, what);
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
                    bool solidHold = false;
                    if (!jackHold && dv.sqrMagnitude > 0.01f)
                    {
                        float look = Mathf.Max(_jackAgent.speed * Time.deltaTime * 2f, 0.15f);
                        Vector3 predPos = jt.position + dv.normalized * look;
                        Quaternion predRot = Quaternion.RotateTowards(jt.rotation, Quaternion.LookRotation(dv.normalized), _jackAgent.angularSpeed * Time.deltaTime * 2f);
                        solidHold = StepBlocked(predPos, predRot, out what);
                    }
                    bool hold = jackHold || solidHold;
                    NoteHeld(hold, blocker, what);
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
        private static bool TryAisleTravelDir(LocationData pick, out Vector3 dir)
        {
            dir = Vector3.zero;
            string a = pick != null ? pick.Address : null;
            if (string.IsNullOrEmpty(a) || a.Length < 8 ||
                !int.TryParse(a.Substring(0, 2), out int aisle) || !int.TryParse(a.Substring(3, 2), out int bay)) return false;
            string prefix = a.Substring(0, 3), suffix = a.Substring(5);
            for (int d = 1; d <= 4; d++)
            {
                foreach (int sign in new[] { 1, -1 })
                {
                    int other = bay + sign * d;
                    if (other < 1 || other > 99) continue;
                    if (!LocationRegistry.TryGet($"{prefix}{other:00}{suffix}", out var loc) || loc == null) continue;
                    Vector3 v = loc.WorldPosition - pick.WorldPosition; v.y = 0f;
                    if (v.sqrMagnitude < 0.01f) continue;
                    dir = v.normalized * sign;                 // toward higher bay numbers, in EVERY aisle
                    return true;
                }
            }
            return false;
        }

        // ── Keeping pallet jacks apart (Tad, 2026-10-17: two selectors on one order drove over each other) ──
        private const float JackCentreBack = 1.0f;     // the forks trail behind the body: the jack's middle is ~1 m back
        private const float JackClearAhead = 3.6f;     // centre-to-centre gap along the path that counts as "in the way"
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
            const float Spacing = 3.4f;
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
