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
    /// Self-bootstrapping like TrailerOffloadController (no scene wiring). Driven by an explicit
    /// Load WorkTask (created by OrderService.ReleaseOrdersToLoading when the player releases a
    /// customer's Staged orders to a door via the Work Queue panel) rather than any automatic
    /// pallet-count threshold — polls for an Available Load task whose FromLocation ("3A") names a
    /// lane with a docked, AwaitingLoad truck at that door, claims it, commandeers an idle manned
    /// dock stocker the same way TrailerOffloadController does (disabling its patrol AiNavigation +
    /// NavMeshAgent so this controller can move the transform directly), runs the sequence, then
    /// restores the DS to patrol — the truck itself stays docked, awaiting the player's close-out.
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
    public class TrailerLoadController : MonoBehaviour
    {
        // ── Tunable choreography — values mirror TrailerOffloadController for a consistent feel ──
        private const float DriveSpeed = 3.0f;
        private const float TurnSpeed = 140f;
        private const float ArriveThreshold = 0.15f;
        private const float FaceThreshold = 3f;
        private const string ForkChildName = "Forks";
        private const float ForkLiftHeight = 1.0f;
        private const float ForkLiftSpeed = 0.6f;
        private const float ForkPickupMatchY = 0f;
        private const float ForkLowerDistance = 1.0f;
        private const float GrabThreshold = 0.2f;
        private const float PivotFrontDistance = 2.0f;   // legacy fallback when there's no docked door to measure from
        // Mirrors of TrailerOffloadController's tuning, per Tad's spec for the loading side:
        private const float PalletLiftClearance = 0.15f; // lift a grabbed pallet this far off the deck, and lower by the same to set it down
        private const float LanePivotDistance   = 1.0f;  // the staging-lane pivot sits this far OUT from the lane entry
        private const float PivotDoorOffset     = 1.0f;  // the SHIPPING DOOR pivot sits this far out from the dock door — every trailer-side turn happens here
        private const float ForkRaiseStandoff   = 1.0f;  // halt this far short of the pallet (fork carry point → pallet, XZ) and raise the forks THERE, stopped
        private const int   OutboundStackTier   = 0;     // outbound cargo is stacked ONE high — always tier 0
        private const bool InvertTrailerAxis = false; // must match TrailerOffloadController's setting

        private static readonly Vector3 ForkCarryLocalPos = new Vector3(0f, 0f, -0.2f);
        private static readonly Vector3 ForkCarryLocalEuler = Vector3.zero;
        private const float ForkAxisSign = -1f;

        private static TrailerLoadController _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[TrailerLoadController]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TrailerLoadController>();
        }

        private PlacementGrid _grid;
        private float _nextScan;

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;

            if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
            if (_grid == null) return;

            // A docked outbound truck waits indefinitely for the player to release orders to its
            // door via the Work Queue panel -- keep resetting the idle clock so it never times out
            // and departs empty just because nobody has loaded it yet.
            foreach (var t in FindObjectsByType<TruckController>())
                if (t.AwaitingLoad) t.KeepDockAlive();

            if (!ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue) || workQueue == null) return;

            // Work a door's lanes in A -> B -> C order. GetPendingTasksForRole hands tasks back in
            // CREATION order, which is just the order the player happened to release lanes in — so a
            // stage whose B lane was released before its A lane got emptied B first, against the
            // "finish lane A, then move on to lane B" rule. Sorting here (rather than at release time)
            // keeps it right no matter what order the releases arrive in.
            var byLane = new List<(WorkTask task, int door, string lane)>();
            foreach (var t in workQueue.GetPendingTasksForRole(EmployeeRole.Loader))
                if (TryParseLaneAddress(t.FromLocation, out int d, out string l))
                    byLane.Add((t, d, l));
            byLane.Sort((a, b) => a.door != b.door
                ? a.door.CompareTo(b.door)
                : string.CompareOrdinal(a.lane, b.lane));

            foreach (var (task, door, lane) in byLane)
            {
                var truck = FindDockedOutboundTruck(door);
                if (truck == null || !truck.AwaitingLoad) continue; // no trailer there (yet) -- try the next task

                var slot = FindAvailableDockStocker();
                if (slot == null) return; // no manned DS free anywhere right now -- try again next poll

                string opGuid = slot.CurrentOperator?.Record?.employeeGuid;
                if (!workQueue.TryClaimSpecificTask(task, opGuid)) continue;

                StartCoroutine(LoadRoutine(truck, slot, task, door, lane));
                return;
            }
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
        /// Direction check (measured off the live rig, not assumed): the truck root carries a 180° Y
        /// rotation so `TrailerIntoDir` = truck.forward = −Z, while the Load container has no Y
        /// rotation and its local +Z is +Z world — i.e. local +Z runs OPPOSITE to `into`. Cross-checked
        /// against TrailerOffloadController, which unloads smallest-Dot(pos, into) first as "nearest
        /// the rear opening": with into = −Z that is the LARGEST local z. So col 5 is the doors and
        /// col 0 is the nose, and ascending depth here loads nose-first — the correct order, and one
        /// where the DS never has to drive past a pallet it already set down.
        ///
        /// (Note the stale comment on `openingRef` below calling slot 0 the "rearmost pallet" — by this
        /// geometry slot 0 is the deepest point. It only matters as the no-docked-door fallback now.)
        /// </summary>
        private static int CargoSlotForSequence(int sequence)
        {
            int depth = sequence / 2;   // 0 = nose … 5 = doors
            int side  = sequence % 2;   // alternate left / right
            return side * TruckController.PalletsPerRow + depth;
        }

        private static TruckController FindDockedOutboundTruck(int doorNumber)
        {
            foreach (var t in FindObjectsByType<TruckController>())
                if (t.IsOutbound && t.DockedAt != null && t.DockedAt.DoorNumber == doorNumber) return t;
            return null;
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
                if (!slot.IsOccupied || DockEquipmentCommandeerRegistry.IsCommandeered(slot)) continue;
                var op = slot.CurrentOperator;
                if (op == null || op.Record == null) continue;
                if (op.Record.role != EmployeeRole.DockStockerOperator && op.Record.role != EmployeeRole.Loader) continue;
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

            var pallets = FindStagedPalletsInLane(doorNumber, lane);

            Debug.Log($"[TrailerLoad] Loading {pallets.Count} staged pallet(s) from lane {doorNumber}{lane} onto {truck.name} with dock stocker {ds.name}.");

            ServiceLocator.TryGet<OrderService>(out var orderService);
            ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue);

            var loadedOrderIds = new HashSet<string>();
            int startSlotIndex = truck.LoadContainer != null ? truck.LoadContainer.childCount : 0;
            for (int i = 0; i < pallets.Count; i++)
            {
                var pallet = pallets[i];
                if (pallet == null) continue;
                // Rig destroyed mid-run — stop dispatching pallets and FALL THROUGH to the restore
                // block below, rather than piling up more exceptions and skipping cleanup entirely.
                if (ds == null) break;

                // Bound the SEQUENCE, not the mapped slot. The pair interleave isn't monotonic, so a
                // sequence past the end folds back onto a low slot index (sequence 12 → slot 6) and a
                // slot-index check would silently double-place on top of an already-loaded pallet
                // instead of stopping.
                int sequence = startSlotIndex + i;
                if (sequence >= TruckController.PalletSlotCount)
                {
                    Debug.LogWarning($"[TrailerLoad] {truck.name} cargo full ({TruckController.PalletSlotCount} slots) — leaving {pallets.Count - i} pallet(s) staged for a later truck.");
                    break;
                }
                int slotIndex = CargoSlotForSequence(sequence);

                if (!string.IsNullOrEmpty(pallet.OrderId)) loadedOrderIds.Add(pallet.OrderId);

                yield return LoadOnePallet(ds, forks, forkRestY, truck, pallet, slotIndex);
            }

            // Mark every order this truck just finished loading Loaded (not shipped/billed yet —
            // that's the player's explicit close-out, see OrderService.CloseOutOrders).
            if (orderService != null)
            {
                foreach (var orderId in loadedOrderIds)
                    orderService.MarkOrderLoaded(orderId);

                // Anything at this lane still sitting in Loading that we did NOT put aboard had no
                // findable pallet here. Send it back to Staged rather than leaving it stranded — the
                // panel offers no action on a Loading row, so it would be stuck for good.
                int stranded = orderService.RevertUnloadedOrdersToStaged(doorNumber, lane, loadedOrderIds);
                if (stranded > 0)
                    Debug.LogWarning($"[TrailerLoad] {stranded} order(s) at {doorNumber}{lane} had no staged pallets to load — reverted to Staged.");
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
                nav.GoToRandomWaypoint();
            }

            DockEquipmentCommandeerRegistry.Release(slot);
            // Hand the truck back so a LATER Load task at this door can still run against it. It is
            // still docked with cargo space; only close-out (CompleteLoad) should retire it. Leaving
            // the claim set is what deadlocked door 1 — every subsequent task stayed Available and its
            // orders stuck in Loading, which the panel gives the player no way to act on.
            truck.ReleaseLoadClaim();
            workQueue?.CompleteTask(task.TaskId);
            Debug.Log($"[TrailerLoad] {truck.name} fully loaded — released dock stocker to patrol. Awaiting close-out to depart.");
        }

        /// <summary>Every staged OutboundPalletBuilder whose current cell belongs to this SPECIFIC
        /// lane — mirrors how offload scopes its lane search to a door, just reading back positions
        /// Order Selection already wrote instead of writing new ones. Excludes anything still
        /// parented (mid-carry, either still riding a selector or already on another DS's forks)
        /// since that pallet isn't actually staged yet/still.</summary>
        private List<OutboundPalletBuilder> FindStagedPalletsInLane(int doorNumber, string lane)
        {
            var found = new List<(OutboundPalletBuilder pallet, int slotIndex)>();
            foreach (var pallet in FindObjectsByType<OutboundPalletBuilder>())
            {
                if (pallet == null || pallet.transform.parent != null) continue;
                var cell = _grid.WorldToCell(pallet.transform.position);
                if (!LaneNamingService.TryGetSlot(cell, out var slot)) continue;
                if (slot.DoorNumber != doorNumber || slot.Lane != lane) continue;
                found.Add((pallet, slot.Slot));
            }

            // Slot 1 loads FIRST, then 2, on out to the end of the lane (e.g. 2A-1 before 2A-6) --
            // the SAME door-outward order staging fills in, deliberately NOT the offloader's
            // reversed sequence.
            found.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));

            // The pick order was previously invisible in the log — only the destination cargo slot was
            // recorded — so "the DS grabbed lane position 6 first" could be neither confirmed nor ruled
            // out after the fact. This states the lane sequence outright.
            Debug.Log($"[TrailerLoad][PICK ORDER] {doorNumber}{lane}: " +
                      string.Join(" -> ", found.Select(f => $"pos{f.slotIndex}")));

            return found.Select(f => f.pallet).ToList();
        }

        private IEnumerator LoadOnePallet(Transform ds, Transform forks, float forkRestY,
                                          TruckController truck, OutboundPalletBuilder pallet, int slotIndex)
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
            LaneEntryGeometry(LaneNamingService.GetLane(laneSlot.DoorNumber, laneSlot.Lane), doorPos, out Vector3 entryW, out Vector3 downLane);
            float driveY = ds.position.y;
            // THE STAGING-LANE PIVOT — LanePivotDistance out from the lane entry, on the door side.
            // Every lane-side turn happens here, never inside the lane.
            Vector3 entryPivot = new Vector3(entryW.x, driveY, entryW.z) - downLane * LanePivotDistance;

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
            // 2. Spin so the forks face straight down the lane — forks FIRST. All turning happens out
            //    here at the pivot; the drive-in below never steers.
            yield return FaceForks(ds, downLane);
            // 3. Forks down to rest for the approach, then drive in from the lane ENTRY end.
            if (forks != null) yield return LiftForks(forks, forkRestY);
            yield return DriveInToGrab(ds, forks, forkRestY, palletT, downLane);
            // 4. Seat the pallet on the forks (fixed carry pose).
            Transform carrier = forks != null ? forks : ds;
            palletT.SetParent(carrier, worldPositionStays: false);
            palletT.localPosition = ForkCarryLocalPos;
            palletT.localRotation = Quaternion.Euler(ForkCarryLocalEuler);
            // Riding the forks now — stop carving, or the pallet cuts a moving trench across the
            // NavMesh and shoves every agent it passes.
            pallet.SetNavObstacleActive(false);
            // 4b. Lift to carry height — PalletLiftClearance off the deck, NOT the old fixed 1m.
            if (forks != null) yield return LiftForks(forks, forks.localPosition.y + PalletLiftClearance);
            // 5. Reverse straight back out to the staging-lane pivot — cab-first, no spin.
            yield return DriveTailFirst(ds, entryPivot);

            // ── CARRY to the trailer and place in the next open cargo slot ───────────────────
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

            // 6. Turn to face the way we're about to travel BEFORE setting off, then pull up to the
            //    shipping door pivot. (Turning while already rolling reads as the DS slewing sideways
            //    across the dock.)
            yield return FaceDir(ds, shippingDoorPivot - ds.position);
            yield return DriveTailFirst(ds, shippingDoorPivot);
            // 7. Spin so the forks (and the carried pallet) face straight into the trailer.
            yield return FaceForks(ds, into);
            // 8. Drive straight in until the carried pallet's XZ lines up over its cargo slot.
            Vector3 palletOffset = palletT.position - ds.position; palletOffset.y = 0f;
            Vector3 targetXZ = new Vector3(targetWorld.x, driveY, targetWorld.z);
            yield return DriveForksFirst(ds, targetXZ - palletOffset);
            // 9. Lower by exactly the PalletLiftClearance it was raised, so the pallet settles onto
            //    the trailer deck rather than being dropped from carry height.
            if (forks != null) yield return LiftForks(forks, forks.localPosition.y - PalletLiftClearance);
            // 10. Unparent off the forks into the trailer's cargo container.
            palletT.SetParent(truck.LoadContainer, worldPositionStays: false);
            palletT.localPosition = targetLocal;
            palletT.localRotation = Quaternion.identity;
            // Cargo inside a trailer must NOT carve — it would cut a hole in the dock NavMesh where
            // the trailer is parked.
            pallet.SetNavObstacleActive(false);

            // "cargoSlot" spelled out because these numbers run 0-11 over the TRAILER's 12-slot bed
            // (nose-first, alternating left/right — see CargoSlotForSequence) and read nothing like the
            // 1-N staging-lane positions the pallet was picked FROM. Logging a bare "slot 6" next to a
            // lane load made the interleave look like the DS was jumping to lane position 6.
            Debug.Log($"[TrailerLoad][PLACE] {pallet.name} (order {pallet.OrderId}) -> {truck.name} cargoSlot {slotIndex} (tier {OutboundStackTier}).");

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

        // ── Movement primitives (duplicated from TrailerOffloadController) ───────────────────────

        // Mirrors TrailerOffloadController.DriveInToGrab: three DISCRETE phases — approach low, stop
        // and lift, then enter — so the tines are never moving vertically and horizontally at once.
        // The old single-loop version raised the forks WHILE driving into the pallet, which dragged
        // them up through the load it was about to pick, and it also steered on the way in (the DS is
        // already squared up at the pivot, and rotating inside a lane looks wrong).
        private IEnumerator DriveInToGrab(Transform ds, Transform forks, float forkRestY, Transform pallet, Vector3 into)
        {
            Vector3 start = ds.position;
            const float maxTravel = 8f;

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
            // The pivot sits only LanePivotDistance (1m) outside the lane while the tines reach further
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

                ds.position += into * (DriveSpeed * Time.deltaTime);
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

                ds.position += into * (DriveSpeed * Time.deltaTime);
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
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(bodyFwd), TurnSpeed * Time.deltaTime);
                t.position = Vector3.MoveTowards(t.position, flat, DriveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator FaceDir(Transform t, Vector3 dir)
        {
            if (t == null) yield break;
            Quaternion want = Quaternion.LookRotation(Flat(dir));
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                yield return null;
                if (t == null) yield break; // destroyed mid-turn — see DriveInternal
            }
            t.rotation = want;
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
