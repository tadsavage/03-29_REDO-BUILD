using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Labor
{
    /// <summary>
    /// D1 (Loading) — the outbound mirror of TrailerOffloadController: drives a manned dock stocker
    /// through physically loading a docked, empty outbound trailer with the pallets an Order Selector
    /// staged in that door's lanes. For each staged pallet it lines the DS up with the lane entry,
    /// slides the forks under it, lifts, backs out onto the dock, spins around, drives forks-first to
    /// the trailer, backs into the next open cargo slot (reusing TruckController's own 12-slot cargo
    /// layout), sets the pallet down, and backs out again.
    ///
    /// Self-bootstrapping like TrailerOffloadController (no scene wiring). Driven by a Load WorkTask
    /// whose FromLocation ("3A") names a lane with a docked, AwaitingLoad truck at that door. The task
    /// is filed automatically (see AutoFileLoadTasks) the instant both are true — a Staged order and a
    /// trailer already waiting at its door — rather than requiring the player to click "Assign" in the
    /// Work Queue panel first; that manual action (OrderService.ReleaseOrdersToLoading) still exists for
    /// a player who wants to summon a trailer proactively. Once a Load task exists, this polls for one
    /// it can claim, commandeers an idle manned dock stocker the same way TrailerOffloadController does
    /// (disabling its patrol AiNavigation + NavMeshAgent so this controller can move the transform
    /// directly), runs the sequence, then restores the DS to patrol — the truck itself stays docked,
    /// awaiting the player's close-out.
    /// A docked truck with no claimable task yet just waits (KeepDockAlive) instead of timing out
    /// empty.
    ///
    /// The movement primitives (drive/face/lift helpers) are intentionally DUPLICATED from
    /// TrailerOffloadController rather than shared — that file is a delicate, heavily-tuned system,
    /// and touching it to extract a shared utility isn't worth the regression risk for a first pass.
    /// Values are kept identical so both controllers move the same physical dock stockers at the same
    /// speed/feel.
    ///
    /// Once every staged pallet for this truck's door is aboard, marks each distinct order
    /// represented Loaded (OrderService.MarkOrderLoaded) and releases the dock stocker back to
    /// patrol. Billing and the trailer's departure no longer happen automatically here — the player
    /// closes Loaded orders out explicitly from the Work Queue panel (OrderService.CloseOutOrders),
    /// which is what actually ships them and, once nothing else assigned to this door is still
    /// Loading/Loaded, releases the trailer to depart.
    ///
    /// NOT handled yet: staged-pallet persistence (a saved/reloaded game won't remember what's
    /// staged), tier-stacked cargo (loaded pallets always go in flat, one per slot, matching
    /// TruckController's legacy 12-slot fallback layout).
    /// </summary>
    public partial class TrailerLoadController : MonoBehaviour
    {
        // ── Tunable choreography — values mirror TrailerOffloadController for a consistent feel ──
        private const float DriveSpeed = 3.0f;
        private const float TurnSpeed = 140f;
        private const float ArriveThreshold = 0.15f;
        private const float FaceThreshold = 3f;
        private const string ForkChildName = "Forks";
        private const float ForkLiftHeight = 1.0f;
        /// <summary>Gap between the underside of a carried pallet and the pallet it is about to be set on.</summary>
        private const float StackApproachClearance = 0.10f;
        private const float ForkLiftSpeed = 0.6f;
        private const float ForkPickupMatchY = 0f;
        private const float ForkLowerDistance = 1.0f;
        private const float GrabThreshold = 0.2f;
        private const float PivotFrontDistance = 2.0f;   // legacy fallback when there's no docked door to measure from
        // Mirrors of TrailerOffloadController's tuning, per Tad's spec for the loading side:
        private const float PalletLiftClearance = 0.15f; // lift a grabbed pallet this far off the deck, and lower by the same to set it down
        private const float LanePivotDistance   = 2.5f;  // the staging-lane pivot sits this far OUT from the lane entry, on the door side
        private const float PivotDoorOffset     = 2.5f;  // the SHIPPING DOOR pivot sits this far out from the dock door — every trailer-side turn happens here. At 1.0 the forks/carried pallet clipped the trailer mouth on the spin; 1.75 was still tight.
        private const float ForkRaiseStandoff   = 1.0f;  // halt this far short of the pallet (fork carry point → pallet, XZ) and raise the forks THERE, stopped
        private const int   OutboundStackTier   = 0;     // outbound cargo is stacked ONE high — always tier 0
        private const bool InvertTrailerAxis = false; // must match TrailerOffloadController's setting

        private static readonly Vector3 ForkCarryLocalPos = new Vector3(0f, 0f, -0.2f);
        // Identity. Do NOT "fix" a flipped pallet by yawing this 180: the carry pose applies to EVERY
        // pallet the forks pick up, so it turns one wrong case into every other case being wrong
        // (tried 2026-07-29, reverted immediately). A pallet that visibly snaps on pickup was sitting
        // at the wrong rotation BEFORE the forks reached it — fix it where it was placed.
        private static readonly Vector3 ForkCarryLocalEuler = Vector3.zero;
        private const float ForkAxisSign = -1f;

        private static TrailerLoadController _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // HideInHierarchy + DontDestroyOnLoad, NOT HideAndDontSave: with Enter Play Mode Options (no domain
            // reload) a HideAndDontSave object survives exiting Play, so every session/recompile left another
            // copy running (6 found live 2026-09-23). This one is destroyed on Play exit; sweep any leftovers.
            foreach (var stale in Resources.FindObjectsOfTypeAll<TrailerLoadController>())
                if (stale != null && stale != _instance) DestroyImmediate(stale.gameObject);
            if (_instance != null) return;
            var go = new GameObject("[TrailerLoadController]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TrailerLoadController>();
        }

        private PlacementGrid _grid;
        private float _nextScan;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // TEMP-PROFILE (2026-10-23)
        private static readonly Unity.Profiling.ProfilerMarker _mTc1 = new("TLC.FindTrucks");
        private static readonly Unity.Profiling.ProfilerMarker _mTc2 = new("TLC.AutoFileLoadTasks");
        private static readonly Unity.Profiling.ProfilerMarker _mTc3 = new("TLC.TryStartDigAndLoad");

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;

            if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
            if (_grid == null) return;

            // A docked outbound truck waits indefinitely for the player to release orders to its
            // door via the Work Queue panel -- keep resetting the idle clock so it never times out
            // and departs empty just because nobody has loaded it yet.
            using (_mTc1.Auto()) foreach (var t in TrucksThisFrame())
                if (t.AwaitingLoad) t.KeepDockAlive();

            if (!ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue) || workQueue == null) return;

            // A Staged order with a trailer already waiting at its door needs no player action any
            // more — file its Load task automatically the instant both are true. See AutoFileLoadTasks.
            using (_mTc2.Auto()) AutoFileLoadTasks();
            ReleaseStaleLoadClaims();

            // Work a door's lanes in A -> B -> C order. GetPendingTasksForRole hands tasks back in
            // CREATION order, which is just the order the player happened to release lanes in — so a
            // stage whose B lane was released before its A lane got emptied B first, against the
            // "finish lane A, then move on to lane B" rule. Sorting here (rather than at release time)
            // keeps it right no matter what order the releases arrive in.
            // Duplicate Load tasks (same lane, same dock door) are pure churn: each one sends the loader out
            // to do an empty pass. A bug filed hundreds of them; keep the oldest and cancel the rest, so a
            // save that already carries them heals itself.
            var seenLoads = new HashSet<string>();
            foreach (var t in workQueue.Tasks)
            {
                if (t.Type != WorkTaskType.Load || t.Status != WorkTaskStatus.Available) continue;
                if (!seenLoads.Add($"{t.FromLocation}>{t.ToLocation}")) t.Status = WorkTaskStatus.Cancelled;
            }

            var byLane = new List<(WorkTask task, int door, string lane, int dock)>();
            foreach (var t in workQueue.GetPendingTasksForRole(EmployeeRole.DockStockerOperator))
                if (t.Type == WorkTaskType.Load && TryParseLaneAddress(t.FromLocation, out int d, out string l))
                    byLane.Add((t, d, l, int.TryParse(t.ToLocation, out int dd) && dd > 0 ? dd : d)); // ToLocation = DOCK door; FromLocation = lane (may be another door)
            byLane.Sort((a, b) => a.door != b.door
                ? a.door.CompareTo(b.door)
                : string.CompareOrdinal(a.lane, b.lane));

            foreach (var (task, door, lane, dockDoor) in byLane)
            {
                // The trailer is at the DOCK door; the lane being emptied may be at a different door.
                var truck = FindDockedOutboundTruck(dockDoor);
                if (truck == null || !truck.AwaitingLoad) continue; // no trailer there (yet) -- try the next task

                var slot = FindAvailableDockStocker();
                if (slot == null) return; // no manned DS free anywhere right now -- try again next poll

                string opGuid = slot.CurrentOperator?.Record?.employeeGuid;
                if (!workQueue.TryClaimSpecificTask(task, opGuid)) continue;

                StartCoroutine(LoadRoutine(truck, slot, task, door, lane));
                return;
            }

            // Nothing staged to load right now — see if a docked trailer is waiting on an order pallet
            // still sitting in an inbound lane that a dock stocker can dig out and load directly
            // (TrailerLoadController.DigOut.cs).
            using (_mTc3.Auto()) TryStartDigAndLoad(workQueue);
        }

        /// <summary>
        /// Maps "the Nth pallet loaded" to a cargo slot index, filling the trailer in LEFT/RIGHT PAIRS
        /// from the nose back toward the doors.
        ///
        /// TruckController.SlotLocalPosition splits its 12 slots as `row = slotIndex / PalletsPerRow`,
        /// so the rows are CONTIGUOUS BLOCKS, not interleaved: 0–5 are the whole left row and 6–11 the
        /// whole right row. Handing out slots sequentially therefore put the first six pallets down the
        /// entire left side before the right side got anything — which is why a part-loaded trailer sat
        /// with everything stacked along one wall. LoadShipment never showed it because it builds all
        /// 12 in one pass.
        ///
        /// Direction — MEASURED on truck_Savage_DevblendNEW.prefab 2026-09-23 (the previous version of
        /// this comment reasoned it out and got it backwards, so the loader filled door-end first):
        /// in the truck's own space the Tractor mesh spans z −0.9..+4.7 and the trailer z −8.3..+0.9.
        /// Slot column 5 sits at z −0.13 (0.6 m behind the cab — the NOSE) and column 0 at z −6.38
        /// (the rear DOORS). Slot positions are local to the trailer, so a docked truck's 180° turn
        /// doesn't change which column is the nose. The first pair therefore goes to column 5 and
        /// loading works back toward the doors, so the loader never drives past a pallet it already
        /// set down.
        /// </summary>
        private static int CargoSlotForSequence(int sequence)
        {
            int depth = (TruckController.PalletsPerRow - 1) - sequence / 2; // 5 = nose … 0 = doors
            int side  = sequence % 2;                                        // alternate left / right
            return side * TruckController.PalletsPerRow + depth;
        }

        /// <summary>Rendered height of a pallet (world metres).</summary>
        internal static float MeasurePalletHeight(Transform pallet)
        {
            const float Fallback = 1.3f;
            if (pallet == null) return Fallback;
            bool any = false; Bounds b = default;
            foreach (var r in pallet.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any && b.size.y > 0.2f ? b.size.y : Fallback;
        }

        /// <summary>
        /// Where does a pallet go? The SAME rule inbound planning uses (TrailerCapacity): the trailer is
        /// 2.5 m tall with 12 floor positions; a SHORT pallet (at most half the interior height) can have
        /// another short pallet on top of it, a TALL one rides alone, stacks are two high.
        ///
        /// So the loader builds up as it goes: a short pallet goes onto a position that holds one lone
        /// short pallet whenever there is one (that is what frees floor for the tall ones), and only
        /// otherwise takes the next empty floor position (nose first, left/right pairs).
        /// <paramref name="stackBaseY"/> is the container-local height of the surface it sits on (0 on
        /// the deck). False when it fits nowhere: the trailer is full for a pallet like this.
        /// </summary>
        internal static bool TryChooseCargoSlot(TruckController truck, float palletHeightWorld, out int slotIndex, out float stackBaseY)
        {
            slotIndex = -1; stackBaseY = 0f;
            if (truck == null || truck.LoadContainer == null) return false;

            float scaleY = Mathf.Max(0.01f, truck.LoadContainer.lossyScale.y);
            int slots = TruckController.PalletSlotCount;
            var tops = new float[slots];
            var counts = new int[slots];
            var bottomHeight = new float[slots];

            foreach (Transform child in truck.LoadContainer)
            {
                if (child == null || !child.gameObject.activeSelf) continue;
                int nearest = -1; float bestD = float.MaxValue;
                for (int s = 0; s < slots; s++)
                {
                    Vector3 sp = truck.SlotLocalPosition(s, 0, null);
                    float d = (child.localPosition.x - sp.x) * (child.localPosition.x - sp.x) +
                              (child.localPosition.z - sp.z) * (child.localPosition.z - sp.z);
                    if (d < bestD) { bestD = d; nearest = s; }
                }
                if (nearest < 0) continue;
                float h = MeasurePalletHeight(child) / scaleY;
                counts[nearest]++;
                if (counts[nearest] == 1 || child.localPosition.y < 0.05f) bottomHeight[nearest] = h;
                float top = child.localPosition.y + h;
                if (top > tops[nearest]) tops[nearest] = top;
            }

            float need = palletHeightWorld / scaleY;
            float roof = TrailerCapacity.SafeInteriorHeight / scaleY;
            float stackable = TrailerCapacity.StackableHeight / scaleY;

            // Walk the positions nose → doors and take the FIRST one that can accept this pallet:
            //   • an empty floor position, or
            //   • a position holding one lone short pallet this one can go on top of.
            // So every position is built two-high BEFORE the loader moves back to the next one, however
            // many pallets there are. Filling the whole floor first and coming back to stack (the old
            // order) sent the stocker driving over the pallets already down to reach the nose.
            for (int seq = 0; seq < slots; seq++)
            {
                int s = CargoSlotForSequence(seq);
                if (counts[s] == 0) { slotIndex = s; stackBaseY = 0f; return true; }
                if (need <= stackable && counts[s] == 1 && bottomHeight[s] <= stackable &&
                    tops[s] + TrailerCapacity.StackGap + need <= roof)
                {
                    slotIndex = s; stackBaseY = tops[s] + TrailerCapacity.StackGap;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Files the Load task for every Staged (or partially-picked-but-staged) order whose assigned
        /// door already has a trailer sitting there AwaitingLoad — the player no longer has to check
        /// it in the Work Queue panel and click "Assign" for this to happen. A trailer arriving at a
        /// door with freight already staged for it IS the trigger now, exactly like a real dock: any
        /// available dock stocker that can reach the lane just goes and gets it.
        ///
        /// Never summons a trailer on its own — OrderService.ReleaseOrdersToLoading has a branch that
        /// spawns one when none is docked, but it's unreachable from here, since FindDockedOutboundTruck
        /// + AwaitingLoad already proved one is. A door with nothing waiting at it yet is untouched;
        /// the manual "Assign" action in the Work Queue panel still exists for a player who wants to
        /// summon a trailer proactively rather than wait for its scheduled appointment.
        /// </summary>
        private void AutoFileLoadTasks()
        {
            if (!ServiceLocator.TryGet<OrderService>(out var orderService) || orderService == null) return;

            // Grouped by door, not by (door, customer): ReleaseOrdersToLoading itself refuses a
            // mixed-customer batch, and a door is single-customer while occupied by construction, so
            // grouping by door alone is enough and keeps every lane of a multi-lane stage together.
            // An order is marked Loaded when a load pass finishes, but pallets can still be arriving in its
            // lanes (reach trucks keep delivering while the loader already has a trailer). Found live
            // 2026-10-14: 2 pallets loaded, 6 more landed afterwards, and nothing ever came back for them
            // because only Staged orders were looked at. Reopen such an order so the next pass fetches them.
            if (ServiceLocator.TryGet<WorkQueueSystem>(out var reopenQueue) && reopenQueue != null)
            {
                foreach (var order in orderService.ActiveOrders)
                {
                    // Loading counts too: an order can sit in Loading with pallets still standing in its lane and NO Load task
                    // (the task was cancelled or released when a dock stocker's battery died mid-pass, or a save restored it that way).
                    // Nothing re-files a task for a Loading order, so it could never be loaded or closed out (found live 2026-10-18,
                    // door 2: "pallets still standing in its staging lane" with nobody coming). Only when a trailer is waiting at its dock.
                    bool loading = order.Status == OrderData.OrderStatus.Loading;
                    if (order.Status != OrderData.OrderStatus.Loaded && !loading) continue;
                    if (loading)
                    {
                        var dockTruck = FindDockedOutboundTruck(order.AssignedDoorNumber);
                        if (dockTruck == null || !dockTruck.AwaitingLoad) continue;
                    }
                    if (!HasStagedPalletForOrder(order.OrderId)) continue;
                    var addresses = new HashSet<string>(order.AllStagingLanes().Select(l => $"{l.door}{l.lane}"));
                    bool loadPending = reopenQueue.Tasks.Any(t => t.Type == WorkTaskType.Load &&
                        (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Assigned) &&
                        addresses.Contains(t.FromLocation));
                    if (loadPending) continue;
                    order.Status = OrderData.OrderStatus.Staged;
                    Debug.Log($"[TrailerLoad] Order {order.OrderNumber} has staged pallets again after its load pass (no Load task pending) — reopened for loading.");
                }
            }

            var orderIdsByDoor = new Dictionary<int, List<string>>();
            foreach (var order in orderService.ActiveOrders)
            {
                if (!orderService.CanStartLoading(order, out _)) continue;
                // Nothing of this order is physically standing in a lane — e.g. every pallet so far
                // was dug out and loaded straight onto the trailer. Releasing it would file a Load task
                // the loader can only fail (RevertUnloadedOrdersToStaged), then re-file every poll.
                if (!HasStagedPalletForOrder(order.OrderId)) continue;
                if (!orderIdsByDoor.TryGetValue(order.AssignedDoorNumber, out var ids))
                {
                    ids = new List<string>();
                    orderIdsByDoor[order.AssignedDoorNumber] = ids;
                }
                ids.Add(order.OrderId);
            }

            foreach (var (door, orderIds) in orderIdsByDoor)
            {
                var truck = FindDockedOutboundTruck(door);
                if (truck == null || !truck.AwaitingLoad) continue; // nothing waiting at this door yet

                // A DOOR may hold several customers at once (one per lane: 1A for one, 1B for another), but a TRAILER carries one customer's
                // freight. Handing every staged order of the door to ReleaseOrdersToLoading in one batch made it refuse the whole lot as
                // "more than one customer", every half second, and no Load task was ever filed - the truck sat docked while two customers'
                // pallets sat in their lanes, which in turn held those lanes and left the selectors' work Open (block 10, 2026-10-25).
                // So: load the orders that belong to THIS truck's appointment, and only one customer's at a time.
                var toLoad = orderIds;
                if (!string.IsNullOrEmpty(truck.OutboundAppointmentId) && ServiceLocator.TryGet<DockScheduleService>(out var sched) && sched != null)
                {
                    var appt = sched.FindById(truck.OutboundAppointmentId);
                    if (appt != null && appt.OrderIds != null && appt.OrderIds.Count > 0)
                    {
                        var mine = orderIds.Where(id => appt.OrderIds.Contains(id)).ToList();
                        if (mine.Count > 0) toLoad = mine;
                    }
                }
                string CustomerOf(string id) => orderService.ActiveOrders.FirstOrDefault(o => o != null && o.OrderId == id)?.CustomerId;
                var firstCustomer = CustomerOf(toLoad[0]);
                toLoad = toLoad.Where(id => CustomerOf(id) == firstCustomer).ToList();   // no appointment info: take one customer, the other loads on its own truck

                orderService.ReleaseOrdersToLoading(toLoad, door);
            }
        }

        // A load claim is a mutex held by a running LoadRoutine / dig-out. If that routine died (its dock stocker was destroyed, fired or
        // had its coroutine dropped) nothing ever released the claim: the truck then sat docked at the door, AwaitingLoad false, and
        // AutoFileLoadTasks skipped it forever while its orders waited staged (block 5, 2026-10-24: two empty trucks blocking both doors).
        // A claim is legitimate only while some dock machine is commandeered, so a claim with none for 15 real seconds is stale.
        private readonly Dictionary<TruckController, float> _claimIdleSince = new();
        private void ReleaseStaleLoadClaims()
        {
            bool anyDriving = DockEquipmentCommandeerRegistry.Any();
            foreach (var t in TrucksThisFrame())
            {
                if (t == null || !t.IsOutbound || !t.LoadClaimed) { if (t != null) _claimIdleSince.Remove(t); continue; }
                if (anyDriving) { _claimIdleSince.Remove(t); continue; }
                if (!_claimIdleSince.TryGetValue(t, out float since)) { _claimIdleSince[t] = Time.unscaledTime; continue; }
                if (Time.unscaledTime - since < 15f) continue;
                t.ReleaseLoadClaim();
                _claimIdleSince.Remove(t);
                Debug.LogWarning($"[TrailerLoad] {t.name} was marked as being loaded but no dock machine was working it for 15 s - released the stale claim so it can be loaded.");
            }
        }

        private static TruckController FindDockedOutboundTruck(int doorNumber)
        {
            foreach (var t in TrucksThisFrame())
                if (t != null && t.IsOutbound && t.DockedAt != null && t.DockedAt.DoorNumber == doorNumber) return t;
            return null;
        }

        // FindObjectsByType<T> scans every object in the scene (several ms here). The scan loop asked for trucks and for
        // staged pallets once per ORDER, several times per poll: ~35 ms hitches twice a second (2026-10-23 profile). One scan per frame is
        // plenty - these are looked up only inside one frame's poll, and a truck or pallet that appears next frame is seen next poll.
        private static int _trucksFrame = -1;
        private static TruckController[] _trucksCache = System.Array.Empty<TruckController>();
        internal static TruckController[] TrucksThisFrame()
        {
            if (_trucksFrame != Time.frameCount)
            {
                _trucksFrame = Time.frameCount;
                _trucksCache = FindObjectsByType<TruckController>();
            }
            return _trucksCache;
        }

        private static int _palletsFrame = -1;
        private static readonly HashSet<string> _stagedOrderIds = new HashSet<string>();
        /// <summary>Order ids that have at least one outbound pallet standing on the floor, from one scan per frame.</summary>
        internal static HashSet<string> StagedOrderIdsThisFrame()
        {
            if (_palletsFrame != Time.frameCount)
            {
                _palletsFrame = Time.frameCount;
                _stagedOrderIds.Clear();
                foreach (var p in FindObjectsByType<OutboundPalletBuilder>())
                    if (p != null && p.IsOnFloor && !string.IsNullOrEmpty(p.OrderId)) _stagedOrderIds.Add(p.OrderId);
            }
            return _stagedOrderIds;
        }

        /// <summary>Splits a lane address like "3A" (WorkTask.FromLocation) back into door number
        /// and lane letter. Lane letters are always a single trailing character (LaneNamingService),
        /// so everything before the last character is the door number.</summary>
        private static bool TryParseLaneAddress(string address, out int door, out string lane)
        {
            door = 0; lane = null;
            if (string.IsNullOrEmpty(address) || address.Length < 2) return false;
            lane = address.Substring(address.Length - 1);
            return int.TryParse(address.Substring(0, address.Length - 1), out door);
        }

        private MHEOperatorSlot FindAvailableDockStocker()
        {
            foreach (var slot in FindObjectsByType<MHEOperatorSlot>())
            {
                // DockEquipmentCommandeerRegistry is shared with TrailerOffloadController — see that
                // file's FindAvailableDockStocker for why AiNavigation.enabled isn't usable as an
                // "in use" signal (it's already disabled from the moment an operator boards, whether
                // idle or actively driven).
                if (!slot.IsOccupied || slot.BatteryFlat || DockEquipmentCommandeerRegistry.IsCommandeered(slot)) continue;
                var op = slot.CurrentOperator;
                if (op == null || op.Record == null) continue;
                if (op.Record.role != EmployeeRole.DockStockerOperator) continue;
                return slot;
            }
            return null;
        }

        private IEnumerator LoadRoutine(TruckController truck, MHEOperatorSlot slot, WorkTask task, int doorNumber, string lane)
        {
            truck.ClaimForLoad();
            DockEquipmentCommandeerRegistry.Commandeer(slot);

            Transform ds = slot.transform;
            // Where the DS stood BEFORE we took it over — a valid, patrol-reachable spot on the working
            // surface, kept as the fallback for the restore below.
            Vector3 homePos = ds.position;

            var nav = ds.GetComponent<AiNavigation>();
            var agent = ds.GetComponent<NavMeshAgent>();
            bool navWas = nav != null && nav.enabled;
            bool agentWas = agent != null && agent.enabled;
            if (agent != null) agent.enabled = false;
            if (nav != null) nav.enabled = false;

            Transform forks = FindDeepChild(ds, ForkChildName);
            float forkRestY = forks != null ? forks.localPosition.y : 0f;

            var pallets = FindStagedPalletsInLane(doorNumber, lane, truck.DockedAt != null ? truck.DockedAt.DoorNumber : doorNumber);

            Debug.Log($"[TrailerLoad] Loading {pallets.Count} staged pallet(s) from lane {doorNumber}{lane} onto {truck.name} with dock stocker {ds.name}.");

            ServiceLocator.TryGet<OrderService>(out var orderService);
            ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue);

            // Counted per order, not just collected as a set: the Completed tab reports how many
            // pallets a deal actually shipped on, and only the loader ever sees that number — the
            // order itself knows cases, never pallets.
            var loadedPalletsByOrder = new Dictionary<string, int>();
            bool cargoFull = false;
            bool batteryDied = false;
            var battery = slot.GetComponent<EquipmentBattery>();
            for (int i = 0; i < pallets.Count; i++)
            {
                var pallet = pallets[i];
                if (pallet == null) continue;
                // Rig destroyed mid-run — stop dispatching pallets and FALL THROUGH to the restore
                // block below, rather than piling up more exceptions and skipping cleanup entirely.
                if (ds == null) break;
                // Flat battery (Tad, 2026-10-17): the pallet that was on the forks has been delivered; take no more.
                // The rest stays staged and the Load task goes back on the queue for another dock stocker.
                if (battery != null && battery.NeedsCharge) { batteryDied = true; break; }

                // Bound the SEQUENCE, not the mapped slot. The pair interleave isn't monotonic, so a
                // sequence past the end folds back onto a low slot index (sequence 12 → slot 6) and a
                // slot-index check would silently double-place on top of an already-loaded pallet
                // instead of stopping.
                // The trailer is 2.5 m tall, so a pallet can ride ON another when it still clears the
                // roof: floor slot first, then the first stack with headroom. Nothing fits = full.
                if (!TryChooseCargoSlot(truck, MeasurePalletHeight(pallet.transform), out int slotIndex, out float stackBaseY))
                {
                    Debug.LogWarning($"[TrailerLoad] {truck.name} cargo full (12 floor positions, 2.5 m roof) — {pallets.Count - i} pallet(s) don't fit.");
                    cargoFull = true;
                    break;
                }

                if (!string.IsNullOrEmpty(pallet.OrderId))
                {
                    loadedPalletsByOrder.TryGetValue(pallet.OrderId, out int soFar);
                    loadedPalletsByOrder[pallet.OrderId] = soFar + 1;
                }

                yield return LoadOnePallet(ds, forks, forkRestY, truck, pallet, slotIndex, stackBaseY);
            }

            if (batteryDied)
            {
                if (forks != null) SetForkLocalY(forks, forkRestY);
                if (agent != null) { agent.enabled = agentWas; RestoreAgentToWorkingSurface(ds, agent, homePos); }
                if (nav != null) nav.enabled = navWas;   // no patrol: EquipmentBattery drives it to a charger
                DockEquipmentCommandeerRegistry.Release(slot);
                truck.ReleaseLoadClaim();
                task.Status = WorkTaskStatus.Available;
                task.AssignedToEmployeeGuid = null;
                Debug.Log($"[TrailerLoad] Dock stocker battery died loading {truck.name} - {loadedPalletsByOrder.Values.Sum()} pallet(s) " +
                          $"loaded, the rest stay staged; Load task released for another dock stocker.");
                yield break;
            }

            // Mark every order this truck just finished loading Loaded (not shipped/billed yet —
            // that's the player's explicit close-out, see OrderService.CloseOutOrders).
            if (orderService != null)
            {
                foreach (var kvp in loadedPalletsByOrder)
                    orderService.MarkOrderLoaded(kvp.Key, kvp.Value);

                // Anything at this lane still sitting in Loading that we did NOT put aboard had no
                // findable pallet here. Send it back to Staged rather than leaving it stranded — the
                // panel offers no action on a Loading row, so it would be stuck for good.
                int stranded = orderService.RevertUnloadedOrdersToStaged(doorNumber, lane, loadedPalletsByOrder.Keys);
                if (stranded > 0)
                    ThrottledLog.Warn("tl-stranded-" + doorNumber + lane, $"[TrailerLoad] {stranded} order(s) at {doorNumber}{lane} had no staged pallets to load — reverted to Staged.");
            }

            // ── Restore the DS to patrol ──
            if (forks != null) SetForkLocalY(forks, forkRestY);
            if (agent != null)
            {
                agent.enabled = agentWas;
                RestoreAgentToWorkingSurface(ds, agent, homePos);
            }
            if (nav != null)
            {
                nav.enabled = navWas;
                nav.GetComponent<EquipmentBattery>()?.NoteJobFinished();   // no patrol: park or charge (EquipmentBattery.IdleRoutine)
            }

            DockEquipmentCommandeerRegistry.Release(slot);
            // The trailer can be destroyed under us (it departed, the save was reloaded): Unity's fake-null makes truck.name throw.
            if (truck == null)
            {
                workQueue?.CompleteTask(task.TaskId);
                yield break;
            }
            // Hand the truck back so a LATER Load task at this door can still run against it. It is
            // still docked with cargo space; only close-out (CompleteLoad) should retire it. Leaving
            // the claim set is what deadlocked door 1 — every subsequent task stayed Available and its
            // orders stuck in Loading, which the panel gives the player no way to act on.
            truck.ReleaseLoadClaim();
            workQueue?.CompleteTask(task.TaskId);
            Debug.Log($"[TrailerLoad] {truck.name} fully loaded — released dock stocker to patrol.");

            // FULL TRAILER (2026-10-17 playtest): nothing more fits, and no second truck comes, so waiting for the lanes
            // to empty deadlocked the door until both orders went late. The trailer ships with what's aboard; whatever
            // didn't fit is not billed and goes back to stock (case picks are reverse-picked into their slots).
            if (cargoFull && orderService != null)
            {
                orderService.ShipCargoFullTrailer(truck);
                yield break;
            }

            // Loaded orders ship and pay on their own (no Close Out click) -- but only once the
            // dock stocker is clear of the trailer, so the driver can't pull away mid-restore.
            orderService?.AutoCloseLoadedOrders();
        }

        /// <summary>Every staged OutboundPalletBuilder whose current cell belongs to this SPECIFIC
        /// lane — mirrors how offload scopes its lane search to a door, just reading back positions
        /// Order Selection already wrote instead of writing new ones. Excludes anything still
        /// parented (mid-carry, either still riding a selector or already on another DS's forks)
        /// since that pallet isn't actually staged yet/still.</summary>
        private List<OutboundPalletBuilder> FindStagedPalletsInLane(int doorNumber, string lane, int dockDoor)
        {
            var found = new List<(OutboundPalletBuilder pallet, int slotIndex, float y)>();
            foreach (var pallet in FindObjectsByType<OutboundPalletBuilder>())
            {
                if (pallet == null || !pallet.IsOnFloor) continue;
                var cell = _grid.WorldToCell(pallet.transform.position);
                if (!LaneNamingService.TryGetSlot(cell, out var slot)) continue;
                if (slot.DoorNumber != doorNumber || slot.Lane != lane) continue;
                found.Add((pallet, slot.Slot, pallet.transform.position.y));
            }

            // Slot 1 loads FIRST, then 2, on out to the end of the lane (e.g. 2A-1 before 2A-6) --
            // the SAME door-outward order staging fills in, deliberately NOT the offloader's
            // reversed sequence.
            //
            // Within ONE slot, lane stacking puts pallets on top of each other: take the TOP one first.
            // Grabbing the bottom pallet first leaves the upper one hanging in mid-air (and offload
            // already works top-down for the same reason).
            found.Sort((a, b) => a.slotIndex != b.slotIndex
                ? a.slotIndex.CompareTo(b.slotIndex)
                : b.y.CompareTo(a.y));

            // Received (inbound) pallets in the same lane are invisible to the scan above, but they are just as
            // solid: one stacked ON an outbound pallet buries it, and one standing in a slot nearer the door
            // blocks everything behind it. Stop at the first outbound pallet that can't be reached without
            // pulling it out from under, or driving through, stock (mirror of the reach-truck fix, 2026-10-17).
            if (ServiceLocator.TryGet<InventoryService>(out var invForBlock) && invForBlock != null)
            {
                var stockTopBySlot = new Dictionary<int, float>();
                foreach (var s in LaneNamingService.GetLane(doorNumber, lane))
                {
                    float top = float.NegativeInfinity;
                    foreach (var rec in invForBlock.GetPalletsAtLocation(s.Cell))
                    {
                        var link = PalletMasterLink.Find(rec.PalletId);
                        if (link != null && OutboundPalletBuilder.IsOnFloorTransform(link.transform))
                            top = Mathf.Max(top, link.transform.position.y);
                    }
                    if (top > float.NegativeInfinity) stockTopBySlot[s.Slot] = top;
                }
                int cut = found.FindIndex(f =>
                    (stockTopBySlot.TryGetValue(f.slotIndex, out float t) && t > f.y + 0.05f) ||
                    stockTopBySlot.Keys.Any(k => k < f.slotIndex && !found.Any(o => o.slotIndex == k)));
                if (cut >= 0)
                {
                    Debug.LogWarning($"[TrailerLoad] {doorNumber}{lane}: received stock blocks the pallet at pos{found[cut].slotIndex} — loading only what's reachable.");
                    found = found.Take(cut).ToList();
                }
            }

            // Only the freight of orders actually being loaded at this door (Tad, 2026-09-23: it was
            // loading every pallet in the lane, whoever's it was). And STOP at the first pallet that
            // isn't theirs rather than skipping it — anything behind it can't be reached without
            // driving through it.
            if (ServiceLocator.TryGet<OrderService>(out var orders) && orders != null)
            {
                var loadingIds = new HashSet<string>(orders.ActiveOrders
                    .Where(o => o.AssignedDoorNumber == dockDoor && // dock door (the trailer), not the lane's door
                                (o.Status == OrderData.OrderStatus.Loading || o.Status == OrderData.OrderStatus.Loaded))
                    .Select(o => o.OrderId));
                int firstForeign = found.FindIndex(f => !loadingIds.Contains(f.pallet.OrderId));
                if (firstForeign >= 0)
                {
                    ThrottledLog.Warn("tl-foreign-" + doorNumber + lane, $"[TrailerLoad] {doorNumber}{lane}: pallet at pos{found[firstForeign].slotIndex} belongs to " +
                                     $"order {found[firstForeign].pallet.OrderId}, not one being loaded here — stopping before it.");
                    found = found.Take(firstForeign).ToList();
                }
            }

            // The pick order was previously invisible in the log — only the destination cargo slot was
            // recorded — so "the DS grabbed lane position 6 first" could be neither confirmed nor ruled
            // out after the fact. This states the lane sequence outright.
            Debug.Log($"[TrailerLoad][PICK ORDER] {doorNumber}{lane}: " +
                      string.Join(" -> ", found.Select(f => $"pos{f.slotIndex}")));

            return found.Select(f => f.pallet).ToList();
        }

        /// <summary>After a pallet leaves a stack, every pallet that was resting above it drops by the gap it left.
        /// Covers outbound pallets and received stock standing in the same cell.</summary>
        private void SettlePalletsAbove(Vector2Int cell, Transform removed, float removedY)
        {
            var above = new List<Transform>();
            foreach (var other in FindObjectsByType<OutboundPalletBuilder>())
            {
                if (other == null || other.transform == removed || !other.IsOnFloor) continue;
                if (_grid.WorldToCell(other.transform.position) != cell) continue;
                if (other.transform.position.y > removedY + 0.3f) above.Add(other.transform);
            }
            if (ServiceLocator.TryGet<InventoryService>(out var inv) && inv != null)
                foreach (var rec in inv.GetPalletsAtLocation(cell))
                {
                    var link = PalletMasterLink.Find(rec.PalletId);
                    if (link == null || link.transform == removed || !OutboundPalletBuilder.IsOnFloorTransform(link.transform)) continue;
                    if (link.transform.position.y > removedY + 0.3f && !above.Contains(link.transform)) above.Add(link.transform);
                }
            if (above.Count == 0) return;
            float lowest = float.MaxValue;
            foreach (var t in above) lowest = Mathf.Min(lowest, t.position.y);
            float drop = lowest - removedY;
            if (drop <= 0.01f) return;
            foreach (var t in above) t.position += Vector3.down * drop;
        }

        private IEnumerator LoadOnePallet(Transform ds, Transform forks, float forkRestY,
                                          TruckController truck, OutboundPalletBuilder pallet, int slotIndex, float stackBaseY = 0f)
        {
            Transform palletT = pallet.transform;

            // ── PICK UP from the lane (reverse of OffloadOnePallet's drop-into-lane) ──────────
            var cell = _grid.WorldToCell(palletT.position);
            if (!LaneNamingService.TryGetSlot(cell, out var laneSlot))
            {
                Debug.LogWarning($"[TrailerLoad] {pallet.name} no longer resolves to a lane slot — skipping.");
                yield break;
            }

            Vector3 doorPos = truck.DockedAt != null ? truck.DockedAt.transform.position : ds.position;
            // The lane may belong to a DIFFERENT door than the trailer's (orders can stage at another
            // door when the dock door's own lanes are busy). Its entry end is the one nearest ITS OWN
            // door, so measure from that door — not from the trailer's.
            bool crossDoor = truck.DockedAt != null && laneSlot.DoorNumber != truck.DockedAt.DoorNumber;
            Vector3 laneDoorPos = crossDoor ? DoorWorldPos(laneSlot.DoorNumber, doorPos) : doorPos;
            LaneEntryGeometry(LaneNamingService.GetLane(laneSlot.DoorNumber, laneSlot.Lane), laneDoorPos, out Vector3 entryW, out Vector3 downLane);
            float driveY = ds.position.y;
            // THE STAGING-LANE PIVOT — LanePivotDistance out from the lane entry, on the door side.
            // Every lane-side turn happens here, never inside the lane.
            Vector3 entryPivot = new Vector3(entryW.x, driveY, entryW.z) - downLane * LanePivotDistance;

            // Cross-door: the lane is across the dock from where the DS last was (the trailer's door). The
            // straight-line slides below only suit short, clear hops, so walk the open floor with the DS's
            // own NavMeshAgent first. Nothing is picked up yet, so on failure the pallet simply stays staged.
            if (crossDoor)
            {
                Vector3 flatToPivot = entryPivot - ds.position; flatToPivot.y = 0f;
                if (flatToPivot.magnitude > 3f)
                {
                    bool legOk = false;
                    yield return SeekViaNavMesh(ds, entryPivot,
                        $"cross-door load: to lane {laneSlot.DoorNumber}{laneSlot.Lane}", r => legOk = r);
                    if (!legOk)
                    {
                        Debug.LogError($"[TrailerLoad] Could not reach lane {laneSlot.DoorNumber}{laneSlot.Lane} from the trailer at door {truck.DockedAt.DoorNumber} — leaving {pallet.name} staged.");
                        yield break;
                    }
                }
            }

            // 1. Pull up to the staging-lane pivot, just outside the lane entry.
            //
            // Come out to the dock aisle FIRST if we're starting deeper than the pivot. DriveTailFirst
            // is a straight MoveTowards slide with no pathing, so a DS that begins its run parked deep
            // in the warehouse — which is exactly where patrol leaves it, and therefore only ever on
            // the FIRST pallet of a load — drove lengthwise THROUGH the lane to reach the pivot,
            // entering at the exit end and running over every staged pallet on the way. Pallets 2+
            // always start from shippingDoorPivot beside the door, which is why they looked fine.
            // Backing straight out to the pivot's depth before running along the aisle means the lane
            // is only ever entered from its door end.
            float depthPastPivot = Vector3.Dot(ds.position - entryPivot, downLane);
            if (depthPastPivot > 0f)
                yield return DriveTailFirst(ds, ds.position - downLane * depthPastPivot);

            yield return DriveTailFirst(ds, entryPivot);

            // ── Wait for exclusive lane entry ───────────────────────────────────────
            // New rule: a vehicle may only enter a staging lane (or manipulate a pallet inside one) if
            // no OTHER vehicle is currently in it — this loader is picking a pallet back OUT of the
            // same lane a reach truck or dock stocker might still be dropping into. Same (door, lane)
            // mutex ReachTruckOperator/TrailerOffloadController already wait on for the mirror-image
            // (dropping in) case, so it serialises every vehicle kind against every other one on a
            // shared lane. Receivers are exempt by design — they never call this, they work on foot.
            ServiceLocator.TryGet<InventoryService>(out var inv);
            if (inv != null)
                while (!inv.TryEnterLaneForDelivery(laneSlot.DoorNumber, laneSlot.Lane))
                    yield return null;

            try
            {
                // The wait above can span many frames — if the pallet was destroyed out from under this
                // coroutine in the meantime, bail rather than driving on with a stale reference.
                if (palletT == null)
                {
                    Debug.LogWarning("[TrailerLoad] Staged pallet was destroyed while waiting for lane entry — skipping it.");
                    yield break;
                }

                // 2. Spin so the forks face straight down the lane — forks FIRST. All turning happens out
                //    here at the pivot; the drive-in below never steers.
                yield return FaceForks(ds, downLane);
                // 3. Forks down to rest for the approach, then drive in from the lane ENTRY end.
                if (forks != null) yield return LiftForks(forks, forkRestY);
                yield return DriveInToGrab(ds, forks, forkRestY, palletT, downLane);
                // 4. Seat the pallet on the forks (fixed carry pose).
                Transform carrier = forks != null ? forks : ds;
                // Captured BEFORE parenting — once it's a child, its world rotation is already whatever
                // the carrier imposes and there's nothing left to compare against.
                float liftedFromY = palletT.position.y;
                Vector3 palletFacingBeforePickup = palletT.forward;
                palletT.SetParent(carrier, worldPositionStays: false);
                pallet.Stage = OutboundPalletBuilder.PalletStage.Loading; // lifted: Loading until it's down in the trailer
                palletT.localPosition = ForkCarryLocalPos;
                palletT.localRotation = NearestFacing(Quaternion.Euler(ForkCarryLocalEuler),
                                                      carrier.InverseTransformDirection(palletFacingBeforePickup));
                // Riding the forks now — stop carving, or the pallet cuts a moving trench across the
                // NavMesh and shoves every agent it passes.
                pallet.SetNavObstacleActive(false);
                // Anything stacked on this pallet would now hang in mid-air (the pick list is a snapshot, so a pallet
                // staged on top after it was built, or any bottom-first grab, strands the upper one): lower it.
                SettlePalletsAbove(cell, palletT, liftedFromY);
                // 4b. Lift to carry height — PalletLiftClearance off the deck, NOT the old fixed 1m.
                if (forks != null) yield return LiftForks(forks, forks.localPosition.y + PalletLiftClearance);
                // 5. Reverse straight back out to the staging-lane pivot — cab-first, no spin.
                yield return DriveTailFirst(ds, entryPivot);
            }
            finally
            {
                // Verified (see TrailerOffloadController's own note on this same pattern): Unity does
                // NOT run an IEnumerator's finally on StopCoroutine/destroy, only when an exception
                // propagates during active execution. Neither controller currently stops this coroutine
                // externally, so this is the same best-effort guarantee already accepted elsewhere.
                if (inv != null) inv.ReleaseLaneEntry(laneSlot.DoorNumber, laneSlot.Lane);
            }

            yield return PlaceCarriedInTrailer(ds, forks, truck, palletT, slotIndex, doorPos, driveY, crossDoor, stackBaseY);

            // "cargoSlot" spelled out because these numbers run 0-11 over the TRAILER's 12-slot bed
            // (nose-first, alternating left/right — see CargoSlotForSequence) and read nothing like the
            // 1-N staging-lane positions the pallet was picked FROM. Logging a bare "slot 6" next to a
            // lane load made the interleave look like the DS was jumping to lane position 6.
            Debug.Log($"[TrailerLoad][PLACE] {pallet.name} (order {pallet.OrderId}) -> {truck.name} cargoSlot {slotIndex} (tier {OutboundStackTier}).");
        }

        /// <summary>
        /// Steps 6–11 of a load: carry the pallet already riding the forks from the dock to the trailer,
        /// set it down in <paramref name="slotIndex"/>, and reverse back out to the shipping door pivot.
        /// Shared by LoadOnePallet (staged outbound pallets) and the dig-out route (inbound pallets
        /// loaded straight from their lane — TrailerLoadController.DigOut.cs).
        /// </summary>
        private IEnumerator PlaceCarriedInTrailer(Transform ds, Transform forks, TruckController truck,
                                                  Transform palletT, int slotIndex, Vector3 doorPos, float driveY,
                                                  bool crossDoorLeg = false, float stackBaseY = 0f)
        {
            // ── CARRY to the trailer and place in the next open cargo slot ───────────────────
            // Per-pallet stage: Loading from here (it's on the forks) until it is set down below. The
            // dig-out route enters here too, with a pallet it just adopted as outbound freight.
            var stagedPallet = palletT.GetComponent<OutboundPalletBuilder>();
            if (stagedPallet != null) stagedPallet.Stage = OutboundPalletBuilder.PalletStage.Loading;
            Vector3 into = TrailerIntoDir(truck);
            Vector3 rightAxis = Vector3.Cross(Vector3.up, into);

            // "Opening" reference: slot 0's theoretical world position stands in for the
            // "rearmost pallet" OffloadOnePallet uses to locate the trailer opening — a fixed
            // geometric point that works whether the trailer has any cargo yet or not (offload
            // always has real pallets to reference; a truck that just arrived to be loaded doesn't).
            Vector3 openingRef = truck.LoadContainer.TransformPoint(truck.SlotLocalPosition(0, OutboundStackTier, null));
            float openingLong = Vector3.Dot(openingRef, into);

            // Outbound cargo is ONE HIGH — always tier 0. SlotLocalPosition gives this slot's spot in
            // the trailer's own layout: its X is the left- or right-hand column, its depth is how far
            // in the row sits.
            Vector3 targetLocal = truck.SlotLocalPosition(slotIndex, OutboundStackTier, null);
            // Stacked on another pallet: the set-down height is that stack's top (container-local metres).
            targetLocal.y += stackBaseY;
            Vector3 targetWorld = truck.LoadContainer.TransformPoint(targetLocal);
            float targetLat = Vector3.Dot(targetWorld, rightAxis);

            // THE SHIPPING DOOR PIVOT — the single point where every trailer-side turn happens.
            // Longitudinal is anchored to the DOCK DOOR, PivotDoorOffset out onto the dock (`into`
            // points into the trailer, so subtract to move outward); lateral is this slot's own
            // left/right column, so the DS is already squared up on the row it is driving into and
            // needs no steering correction inside the trailer. Falls back to the old opening-derived
            // reference only if this truck somehow has no docked door to measure from.
            float pivotLong = truck.DockedAt != null
                            ? Vector3.Dot(doorPos, into) - PivotDoorOffset
                            : openingLong - PivotFrontDistance;
            Vector3 shippingDoorPivot = into * pivotLong
                                       + rightAxis * targetLat
                                       + Vector3.up * ds.position.y;

            // 5b. Cross-door: the pallet came out of a lane at ANOTHER door, so carry it across the open dock
            //     floor with the DS's own NavMeshAgent. If that fails the pallet is already on the forks —
            //     abandoning it mid-carry would strand it — so fall back to the old straight slide and say so.
            if (crossDoorLeg)
            {
                bool legOk = false;
                yield return SeekViaNavMesh(ds, shippingDoorPivot, "cross-door load: to trailer", r => legOk = r);
                if (!legOk)
                    Debug.LogError("[TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — " +
                                   "falling back to a straight slide to the shipping-door pivot.");
            }

            // 6. Turn to face the way we're about to travel BEFORE setting off, then pull up to the
            //    shipping door pivot. (Turning while already rolling reads as the DS slewing sideways
            //    across the dock.)
            yield return FaceDir(ds, shippingDoorPivot - ds.position);
            yield return DriveTailFirst(ds, shippingDoorPivot);
            // 7. Spin so the forks (and the carried pallet) face straight into the trailer.
            yield return FaceForks(ds, into);
            // 7b. Stacking on a pallet already in the slot: raise the forks by that stack's height BEFORE
            //     driving in so the carried pallet clears the one below (step 9 lowers by exactly
            //     PalletLiftClearance, leaving it resting on top).
            //     Raise only as far as needed: the carried pallet's base ends StackApproachClearance above
            //     the surface it is about to land on (measured from where it actually is now), not a fixed
            //     jump on top of whatever height it was already carried at — that overshoot is what sent
            //     the forks up several pallets high.
            bool stacking = stackBaseY > 0.01f && forks != null;
            if (stacking)
            {
                float raise = (targetWorld.y + StackApproachClearance) - palletT.position.y;
                if (raise > 0.001f) yield return LiftForks(forks, forks.localPosition.y + raise);
            }
            // 8. Drive straight in until the carried pallet's XZ lines up over its cargo slot.
            Vector3 palletOffset = palletT.position - ds.position; palletOffset.y = 0f;
            Vector3 targetXZ = new Vector3(targetWorld.x, driveY, targetWorld.z);
            yield return DriveForksFirst(ds, targetXZ - palletOffset);
            // 9. Lower so the pallet settles onto what it is landing on rather than being dropped.
            //    Stacking: down to the exact surface height. Floor: the usual PalletLiftClearance.
            if (forks != null)
            {
                float lower = stacking ? Mathf.Max(0f, palletT.position.y - targetWorld.y) : PalletLiftClearance;
                yield return LiftForks(forks, forks.localPosition.y - lower);
            }
            // 10. Unparent off the forks into the trailer's cargo container.
            Vector3 facingOnForks = palletT.forward; // world facing while still carried
            palletT.SetParent(truck.LoadContainer, worldPositionStays: false);
            palletT.localPosition = targetLocal;
            // Square to the trailer, but keep the 180 it already had — otherwise the pallet spins on
            // the deck at the moment of release, the same snap the pickup used to have.
            palletT.localRotation = NearestFacing(Quaternion.identity,
                                                  truck.LoadContainer.InverseTransformDirection(facingOnForks));
            // Cargo inside a trailer must NOT carve — it would cut a hole in the dock NavMesh where
            // the trailer is parked. (Same component OutboundPalletBuilder.SetNavObstacleActive toggles.)
            var obstacle = palletT.GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;
            if (stagedPallet != null) stagedPallet.Stage = OutboundPalletBuilder.PalletStage.Loaded; // down in the trailer

            // 11. Reverse straight back out of the trailer to the shipping door pivot — cab-first,
            //     forks trailing, no spin. The next pallet's run starts from here.
            yield return DriveTailFirst(ds, shippingDoorPivot);
        }

        // Resolves a lane's ENTRY (the end nearest the dock door) and its down-lane direction (entry
        // toward the far end) — identical to TrailerOffloadController.LaneEntryGeometry.
        private void LaneEntryGeometry(List<LaneNamingService.LaneSlot> slots, Vector3 doorPos,
                                       out Vector3 entryW, out Vector3 downLane)
        {
            Vector3 end0 = _grid.GetCellCenter(slots[0].Cell);
            Vector3 endN = _grid.GetCellCenter(slots[slots.Count - 1].Cell);
            bool zeroIsEntry = (end0 - doorPos).sqrMagnitude <= (endN - doorPos).sqrMagnitude;
            entryW = zeroIsEntry ? end0 : endN;
            Vector3 exitW = zeroIsEntry ? endN : end0;
            downLane = slots.Count > 1 ? Flat(exitW - entryW) : Flat(entryW - doorPos);
        }

        /// <summary>
        /// Hands the agent back onto a VALIDATED NavMesh position rather than blind-Warping to wherever
        /// the scripted run finished. Warp snaps to the NEAREST mesh, which near a dock edge can be the
        /// yard a metre below instead of the dock — exactly how a dock stocker ended a load run parked
        /// at ground level in the open yard, on a disconnected island with no route back. Require the
        /// sampled surface to be at the height the DS was working at; fall back to its pre-commandeer
        /// position otherwise. (Mirror of TrailerOffloadController's copy — these two files
        /// deliberately duplicate their primitives, see the class header.)
        /// </summary>
        private static void RestoreAgentToWorkingSurface(Transform ds, NavMeshAgent agent, Vector3 homePos)
        {
            if (ds == null || agent == null || !agent.isActiveAndEnabled) return;

            const float HeightTolerance = 0.5f;
            const float SampleRadius    = 2.0f;

            bool ok = NavMesh.SamplePosition(ds.position, out NavMeshHit hit, SampleRadius, agent.areaMask)
                      && Mathf.Abs(hit.position.y - homePos.y) <= HeightTolerance;

            if (!ok)
            {
                Debug.LogWarning($"[TrailerLoad] {ds.name} finished its run at {ds.position} with no NavMesh at its " +
                                 $"working height ({homePos.y:F2}) within {SampleRadius}m — returning it to {homePos} " +
                                 $"rather than stranding it off the dock.");
                if (!NavMesh.SamplePosition(homePos, out hit, SampleRadius, agent.areaMask))
                {
                    ds.position = homePos;
                    agent.Warp(homePos);
                    agent.isStopped = false;
                    return;
                }
            }

            ds.position = hit.position;
            agent.Warp(hit.position);
            agent.isStopped = false;
        }

        // ── Cross-door travel (staging lane at a different door than the trailer) ───────────────────

        /// <summary>World position of a numbered door, or <paramref name="fallback"/> if it can't be found.</summary>
        private static Vector3 DoorWorldPos(int doorNumber, Vector3 fallback)
        {
            foreach (var d in DockSlot.All)
                if (d != null && d.DoorNumber == doorNumber) return d.transform.position;
            return fallback;
        }

        /// <summary>Open-floor travel across the dock between two doors: the DS's own NavMeshAgent walks it
        /// to <paramref name="target"/>, then control returns to the scripted choreography. A copy of
        /// TrailerOffloadController.SeekViaNavMesh (these controllers deliberately duplicate their
        /// primitives) — including its hardening: register-on-mesh wait, floor-projected target, task-busy
        /// guard against AiNavigation's waypoint hijack, and a rebake-resilient wait. No straight-line
        /// fallback here: the caller decides what to do when it fails.</summary>
        private IEnumerator SeekViaNavMesh(Transform ds, Vector3 target, string phase, System.Action<bool> onDone)
        {
            var nav = ds != null ? ds.GetComponent<AiNavigation>() : null;
            var agent = ds != null ? ds.GetComponent<NavMeshAgent>() : null;
            if (nav == null || agent == null)
            {
                Debug.LogError($"[TrailerLoad] '{ds?.name}' {phase}: no AiNavigation/NavMeshAgent on this dock stocker — cannot do an AI travel leg.");
                onDone?.Invoke(false);
                yield break;
            }

            agent.enabled = true;
            nav.enabled = true;

            const float MaxRegisterWait = 1f;
            float registerWaited = 0f;
            while (!agent.isOnNavMesh && registerWaited < MaxRegisterWait)
            {
                agent.Warp(ds.position);
                registerWaited += Time.deltaTime;
                yield return null;
            }

            if (!agent.isOnNavMesh)
            {
                Debug.LogError($"[TrailerLoad] '{ds.name}' {phase}: agent could not register on the NavMesh at {ds.position} after {registerWaited:F2}s — aborting leg.");
                nav.SetTaskBusy(false);
                agent.enabled = false;
                nav.enabled = false;
                onDone?.Invoke(false);
                yield break;
            }

            Vector3 driveTarget = new Vector3(target.x, ds.position.y, target.z);
            if (NavMesh.SamplePosition(driveTarget, out var groundHit, 4f, agent.areaMask))
                driveTarget = groundHit.position;

            nav.CancelSeekPosition();
            agent.Warp(ds.position);
            nav.SetTaskBusy(true);

            bool arrived = false;
            nav.SeekPosition(driveTarget, () => arrived = true);

            yield return null;
            if (!arrived && !agent.pathPending && !agent.hasPath)
            {
                Debug.LogError($"[TrailerLoad] '{ds.name}' {phase}: destination did not take (no path) for {driveTarget} from {ds.position}.");
                nav.CancelSeekPosition();
                nav.SetTaskBusy(false);
                agent.enabled = false;
                nav.enabled = false;
                onDone?.Invoke(false);
                yield break;
            }

            float elapsed = 0f, sinceRecheck = 0f;
            int reissues = 0;
            const float MaxSeekTime = 30f, RecheckInterval = 0.5f;
            const int MaxReissues = 10;

            while (!arrived && nav.IsSeekingTask && elapsed < MaxSeekTime)
            {
                elapsed += Time.deltaTime;
                sinceRecheck += Time.deltaTime;
                if (sinceRecheck >= RecheckInterval)
                {
                    sinceRecheck = 0f;
                    if (!agent.isOnNavMesh) agent.Warp(ds.position);
                    else if (!agent.pathPending && !agent.hasPath && reissues < MaxReissues)
                    {
                        reissues++;
                        nav.CancelSeekPosition();
                        nav.SeekPosition(driveTarget, () => arrived = true);
                    }
                }
                yield return null;
            }

            nav.SetTaskBusy(false);
            agent.enabled = false;
            nav.enabled = false;

            if (!arrived)
                Debug.LogError($"[TrailerLoad] '{ds.name}' {phase}: NO NAVMESH PATH to {driveTarget} from {ds.position} (elapsed {elapsed:F1}s).");

            onDone?.Invoke(arrived);
        }

        // ── Movement primitives (duplicated from TrailerOffloadController) ───────────────────────

        // Mirrors TrailerOffloadController.DriveInToGrab: three DISCRETE phases — approach low, stop
        // and lift, then enter — so the tines are never moving vertically and horizontally at once.
        // The old single-loop version raised the forks WHILE driving into the pallet, which dragged
        // them up through the load it was about to pick, and it also steered on the way in (the DS is
        // already squared up at the pivot, and rotating inside a lane looks wrong).
        private IEnumerator DriveInToGrab(Transform ds, Transform forks, float forkRestY, Transform pallet, Vector3 into)
        {
            Vector3 start = ds.position;
            // Backstop, not a normal stopping condition: enough to reach the deepest slot in the lane
            // from the pivot, and nothing more. Derived from LanePivotDistance because the drive starts
            // AT the pivot — pulling the pivot further out from the lane lengthens every drive-in by the
            // same amount, and a hardcoded cap would start tripping on deep slots as soon as it moved.
            const float maxTravel = LanePivotDistance + 7f;

            // Fork local Y that puts the CARRY POINT (where the pallet actually seats) level with this
            // pallet. Measured once, before the forks move: the delta is world-vertical so it maps 1:1
            // onto the forks' local Y.
            float matchLocalY = forkRestY;
            if (forks != null)
            {
                Vector3 carryPoint = forks.TransformPoint(ForkCarryLocalPos);
                matchLocalY = forks.localPosition.y + (pallet.position.y - carryPoint.y);
            }

            // PHASE 1 — approach with the forks DOWN, stopping ForkRaiseStandoff short. Measured from
            // the fork carry point, not the DS root: the root sits well behind the tines, so a
            // root-based standoff can already have them buried in the pallet face.
            //
            // The stop test is the SIGNED distance along the travel direction, not the raw magnitude.
            // The pivot sits only LanePivotDistance outside the lane while the tines reach further
            // forward than that, so for a pallet in slot 1 the carry point can already be level with or
            // PAST it. Magnitude can't tell "1.5m ahead" from "1.5m behind", so the DS drove forward to
            // close a gap that was behind it — all the way down the lane to the 8m travel cap, where it
            // seated the slot-1 pallet anyway and the load appeared to snap onto the forks from nowhere.
            while (true)
            {
                if (ds == null || pallet == null) yield break; // destroyed mid-run — see DriveInternal
                Vector3 grab = forks != null ? forks.TransformPoint(ForkCarryLocalPos) : ds.position + into;
                Vector3 gd = pallet.position - grab; gd.y = 0f;
                if (Vector3.Dot(gd, into) <= ForkRaiseStandoff) break;

                ds.position += into * (DriveSpeed * EquipmentBattery.SpeedMultiplierFor(ds) * Time.deltaTime);
                if ((ds.position - start).magnitude >= maxTravel)
                {
                    Debug.LogWarning("[TrailerLoad] DriveInToGrab hit the travel cap on approach — lifting and seating anyway.");
                    break;
                }
                yield return null;
            }

            // PHASE 2 — STOPPED and clear: raise to this pallet's own height, and let the lift FINISH
            // before moving again.
            if (forks != null) yield return LiftForks(forks, matchLocalY);

            // PHASE 3 — at pocket height: close the last stretch straight in, no steering. Signed for
            // the same reason as phase 1 — a pallet already level with the tines must stop the drive,
            // not start an 8m one.
            while (true)
            {
                if (ds == null || pallet == null) yield break; // destroyed mid-run — see DriveInternal
                Vector3 grab = forks != null ? forks.TransformPoint(ForkCarryLocalPos) : ds.position + into;
                Vector3 gd = pallet.position - grab; gd.y = 0f;
                if (Vector3.Dot(gd, into) <= GrabThreshold) break;

                ds.position += into * (DriveSpeed * EquipmentBattery.SpeedMultiplierFor(ds) * Time.deltaTime);
                if ((ds.position - start).magnitude >= maxTravel)
                {
                    Debug.LogWarning("[TrailerLoad] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.");
                    break;
                }
                yield return null;
            }
        }

        private IEnumerator DriveForksFirst(Transform t, Vector3 target) => DriveInternal(t, target, forksLead: true);
        private IEnumerator DriveTailFirst(Transform t, Vector3 target) => DriveInternal(t, target, forksLead: false);

        private IEnumerator DriveInternal(Transform t, Vector3 target, bool forksLead)
        {
            while (true)
            {
                // Destroyed mid-run (equipment deleted, scene teardown, domain reload during play).
                // Without this the coroutine throws MissingReferenceException and dies PART WAY
                // THROUGH, skipping the restore at the end of LoadRoutine and leaving the DS
                // commandeered with its agent disabled — permanently frozen.
                if (t == null) yield break;
                Vector3 flat = new Vector3(target.x, t.position.y, target.z);
                Vector3 to = flat - t.position; to.y = 0f;
                if (to.magnitude <= ArriveThreshold) { t.position = flat; break; }
                Vector3 travel = Flat(to);
                Vector3 bodyFwd = forksLead ? BodyForwardForForks(travel) : travel;
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(bodyFwd), TurnSpeed * EquipmentBattery.SpeedMultiplierFor(t) * Time.deltaTime);
                t.position = Vector3.MoveTowards(t.position, flat, DriveSpeed * EquipmentBattery.SpeedMultiplierFor(t) * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator FaceDir(Transform t, Vector3 dir)
        {
            if (t == null) yield break;
            Quaternion want = Quaternion.LookRotation(Flat(dir));
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * EquipmentBattery.SpeedMultiplierFor(t) * Time.deltaTime);
                yield return null;
                if (t == null) yield break; // destroyed mid-turn — see DriveInternal
            }
            t.rotation = want;
        }

        /// <summary>
        /// The 180-degree variant of <paramref name="want"/> that is CLOSEST to how the pallet is
        /// already facing.
        ///
        /// A pallet is symmetric under a 180 yaw — the footprint is identical either way — so both
        /// variants seat it equally squarely. Which one you choose is therefore free, and choosing
        /// the nearer one means the pallet never visibly spins when it's picked up or set down.
        ///
        /// This exists because an ABSOLUTE carry pose cannot be right for both sources: cargo in a
        /// trailer is aligned to the trailer axis (pointing out of the building) and cargo in a lane
        /// to the lane's DepthAxis (pointing into it), a full 180 apart. A fixed pose suits one and
        /// flips the other — which is exactly what happened when a hardcoded 180 was tried here to
        /// fix lane pickups and broke every trailer pickup instead.
        /// </summary>
        private static Quaternion NearestFacing(Quaternion want, Vector3 currentWorldForward)
        {
            Vector3 wantFwd = Flat(want * Vector3.forward);
            Vector3 curFwd = Flat(currentWorldForward);
            if (curFwd.sqrMagnitude < 0.0001f || wantFwd.sqrMagnitude < 0.0001f) return want;
            return Vector3.Dot(wantFwd, curFwd) >= 0f ? want : want * Quaternion.Euler(0f, 180f, 0f);
        }

        private IEnumerator FaceForks(Transform t, Vector3 worldForkDir) => FaceDir(t, BodyForwardForForks(worldForkDir));

        private static Vector3 BodyForwardForForks(Vector3 worldForkDir) => worldForkDir * ForkAxisSign;

        private IEnumerator LiftForks(Transform forks, float targetLocalY)
        {
            if (forks == null) yield break;
            Vector3 lp = forks.localPosition;
            while (Mathf.Abs(lp.y - targetLocalY) > 0.001f)
            {
                lp.y = Mathf.MoveTowards(lp.y, targetLocalY, ForkLiftSpeed * Time.deltaTime);
                forks.localPosition = lp;
                yield return null;
                if (forks == null) yield break; // destroyed mid-lift — see DriveInternal
            }
        }

        private static void SetForkLocalY(Transform forks, float y)
        {
            Vector3 lp = forks.localPosition; lp.y = y; forks.localPosition = lp;
        }

        private static Vector3 TrailerIntoDir(TruckController truck)
        {
            Vector3 f = Flat(truck.transform.forward);
            return InvertTrailerAxis ? -f : f;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform c in parent)
            {
                if (c.name == childName) return c;
                var r = FindDeepChild(c, childName);
                if (r != null) return r;
            }
            return null;
        }
    }
}
