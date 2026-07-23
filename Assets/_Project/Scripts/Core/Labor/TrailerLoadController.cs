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
    /// restores the DS to patrol and tells the truck it may depart. A docked truck with no claimable
    /// task yet just waits (KeepDockAlive) instead of timing out empty.
    ///
    /// The movement primitives (drive/face/lift helpers) are intentionally DUPLICATED from
    /// TrailerOffloadController rather than shared — that file is a delicate, heavily-tuned system,
    /// and touching it to extract a shared utility isn't worth the regression risk for a first pass.
    /// Values are kept identical so both controllers move the same physical dock stockers at the same
    /// speed/feel.
    ///
    /// Once every staged pallet for this truck's door is aboard, bills each distinct order
    /// represented (SellingPrice x QuantityPicked per line item, via OrderService.ShipOrder — D2)
    /// before releasing the dock stocker and flipping CompleteLoad().
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
        private const float PivotFrontDistance = 2.0f;
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

            foreach (var task in workQueue.GetPendingTasksForRole(EmployeeRole.Loader))
            {
                if (!TryParseLaneAddress(task.FromLocation, out int door, out string lane)) continue;

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
            int totalValue = 0;
            int startSlotIndex = truck.LoadContainer != null ? truck.LoadContainer.childCount : 0;
            for (int i = 0; i < pallets.Count; i++)
            {
                var pallet = pallets[i];
                if (pallet == null) continue;

                int slotIndex = startSlotIndex + i;
                if (slotIndex >= TruckController.PalletSlotCount)
                {
                    Debug.LogWarning($"[TrailerLoad] {truck.name} cargo full ({TruckController.PalletSlotCount} slots) — leaving {pallets.Count - i} pallet(s) staged for a later truck.");
                    break;
                }

                if (!string.IsNullOrEmpty(pallet.OrderId)) loadedOrderIds.Add(pallet.OrderId);

                var order = orderService?.ActiveOrders.FirstOrDefault(o => o.OrderId == pallet.OrderId);
                if (order != null) totalValue += pallet.CalculateSaleValue(order);

                yield return LoadOnePallet(ds, forks, forkRestY, truck, pallet, slotIndex);
            }

            // ── D2: bill and ship every order this truck just finished loading ──
            if (orderService != null)
            {
                foreach (var orderId in loadedOrderIds)
                    orderService.ShipOrder(orderId);
            }

            // One green "$" popup for the WHOLE load's value, hovering above the door -- same
            // Mario-coin FloatingMoneyText effect already used for damage/spoilage refunds, per
            // Tad's spec (a single total, not one per pallet).
            if (totalValue > 0 && truck.DockedAt != null)
                FloatingMoneyText.Show(truck.DockedAt.transform.position + Vector3.up * 2.5f, totalValue);

            // ── Restore the DS to patrol ──
            if (forks != null) SetForkLocalY(forks, forkRestY);
            if (agent != null)
            {
                agent.enabled = agentWas;
                if (agent.isActiveAndEnabled) { agent.Warp(ds.position); agent.isStopped = false; }
            }
            if (nav != null)
            {
                nav.enabled = navWas;
                nav.GoToRandomWaypoint();
            }

            DockEquipmentCommandeerRegistry.Release(slot);
            workQueue?.CompleteTask(task.TaskId);
            truck.CompleteLoad();
            Debug.Log($"[TrailerLoad] {truck.name} fully loaded — released dock stocker to patrol.");
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

            // Farthest-from-door slot loads FIRST (e.g. 2A-6 before 2A-1) -- the mirror image of
            // the door-outward staging fill order, per Tad's 2026-07-23 spec.
            found.Sort((a, b) => b.slotIndex.CompareTo(a.slotIndex));
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
            Vector3 entryPivot = new Vector3(entryW.x, driveY, entryW.z) - downLane * PivotFrontDistance;

            // 1. Pull up to a pivot just in front of the lane entry (outside the lane, door side).
            yield return DriveTailFirst(ds, entryPivot);
            // 2. Spin so the forks face straight down the lane — forks FIRST.
            yield return FaceForks(ds, downLane);
            // 3. Drive in forks-first: lower the forks once close, stop when they reach the pallet.
            yield return DriveInToGrab(ds, forks, forkRestY, palletT, downLane);
            // 4. Seat the pallet on the forks (fixed carry pose), then lift it.
            Transform carrier = forks != null ? forks : ds;
            palletT.SetParent(carrier, worldPositionStays: false);
            palletT.localPosition = ForkCarryLocalPos;
            palletT.localRotation = Quaternion.Euler(ForkCarryLocalEuler);
            if (forks != null) yield return LiftForks(forks, forkRestY + ForkLiftHeight);
            // 5. Reverse straight back out to the entry pivot — cab-first, no spin.
            yield return DriveTailFirst(ds, entryPivot);

            // ── CARRY to the trailer and place in the next open cargo slot ───────────────────
            Vector3 into = TrailerIntoDir(truck);
            Vector3 rightAxis = Vector3.Cross(Vector3.up, into);

            // "Opening" reference: slot 0's theoretical world position stands in for the
            // "rearmost pallet" OffloadOnePallet uses to locate the trailer opening — a fixed
            // geometric point that works whether the trailer has any cargo yet or not (offload
            // always has real pallets to reference; a truck that just arrived to be loaded doesn't).
            Vector3 openingRef = truck.LoadContainer.TransformPoint(truck.SlotLocalPosition(0, 0, null));
            float openingLong = Vector3.Dot(openingRef, into);

            Vector3 targetLocal = truck.SlotLocalPosition(slotIndex, 0, null);
            Vector3 targetWorld = truck.LoadContainer.TransformPoint(targetLocal);
            float targetLat = Vector3.Dot(targetWorld, rightAxis);

            Vector3 trailerPivot = into * (openingLong - PivotFrontDistance)
                                  + rightAxis * targetLat
                                  + Vector3.up * ds.position.y;

            // 6. Pull up to a pivot in front of the trailer opening, on this slot's row.
            yield return DriveTailFirst(ds, trailerPivot);
            // 7. Spin so the forks (and the carried pallet) face straight into the trailer.
            yield return FaceForks(ds, into);
            // 8. Drive forward until the carried pallet's XZ lines up over the target slot.
            Vector3 palletOffset = palletT.position - ds.position; palletOffset.y = 0f;
            Vector3 targetXZ = new Vector3(targetWorld.x, driveY, targetWorld.z);
            yield return DriveForksFirst(ds, targetXZ - palletOffset);
            // 9. Lower the forks back to rest, then set the pallet down in its cargo slot.
            if (forks != null) yield return LiftForks(forks, forkRestY);
            palletT.SetParent(truck.LoadContainer, worldPositionStays: false);
            palletT.localPosition = targetLocal;
            palletT.localRotation = Quaternion.identity;

            Debug.Log($"[TrailerLoad][PLACE] {pallet.name} (order {pallet.OrderId}) -> {truck.name} slot {slotIndex}.");

            // 10. Reverse straight back out of the trailer to the pivot — cab-first, no spin.
            yield return DriveTailFirst(ds, trailerPivot);
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

        // ── Movement primitives (duplicated from TrailerOffloadController) ───────────────────────

        private IEnumerator DriveInToGrab(Transform ds, Transform forks, float forkRestY, Transform pallet, Vector3 into)
        {
            Quaternion face = Quaternion.LookRotation(Flat(BodyForwardForForks(into)));
            Vector3 start = ds.position;
            const float maxTravel = 8f;

            while (true)
            {
                Vector3 grab = forks != null ? forks.TransformPoint(ForkCarryLocalPos) : ds.position + into;
                Vector3 gd = pallet.position - grab; gd.y = 0f;
                if (gd.magnitude <= GrabThreshold) break;

                Vector3 dsToP = pallet.position - ds.position; dsToP.y = 0f;
                if (forks != null && dsToP.magnitude <= ForkLowerDistance)
                {
                    Vector3 lp = forks.localPosition;
                    lp.y = Mathf.MoveTowards(lp.y, forkRestY + ForkPickupMatchY, ForkLiftSpeed * Time.deltaTime);
                    forks.localPosition = lp;
                }

                ds.rotation = Quaternion.RotateTowards(ds.rotation, face, TurnSpeed * Time.deltaTime);
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
            Quaternion want = Quaternion.LookRotation(Flat(dir));
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.rotation = want;
        }

        private IEnumerator FaceForks(Transform t, Vector3 worldForkDir) => FaceDir(t, BodyForwardForForks(worldForkDir));

        private static Vector3 BodyForwardForForks(Vector3 worldForkDir) => worldForkDir * ForkAxisSign;

        private IEnumerator LiftForks(Transform forks, float targetLocalY)
        {
            Vector3 lp = forks.localPosition;
            while (Mathf.Abs(lp.y - targetLocalY) > 0.001f)
            {
                lp.y = Mathf.MoveTowards(lp.y, targetLocalY, ForkLiftSpeed * Time.deltaTime);
                forks.localPosition = lp;
                yield return null;
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
