using System.Linq;
using GameCore.Inventory;
using GameCore.Services;
using UnityEngine;

namespace GameCore.Labor
{
    /// <summary>
    /// Scans pick slots for replenishment need: an empty pick slot that has a SKU assigned (via
    /// SlotAssignmentService) but no pallet. When a reserve slot somewhere holds that SKU, creates
    /// a Replenish WorkTask targeting the FIFO-oldest matching reserve pallet (by
    /// PalletMasterRecord.ReceivedDayNumber) as the source and the empty pick slot as the
    /// destination. Both endpoints are locked (Reserved) immediately so the same reserve pallet or
    /// pick slot can't be double-assigned by a later scan tick before a Reach Truck claims the task.
    ///
    /// Self-bootstrapping (same pattern as ReceivingService/TrailerOffloadController).
    /// </summary>
    public class ReplenishmentService : MonoBehaviour
    {
        private const float ScanInterval = 2f;

        /// <summary>Replenish tasks default higher than Putaway's 100 — an empty pick slot blocks
        /// order picking immediately, so it should be worked before routine putaways.</summary>
        public const int ReplenishPriority = 250;

        private static ReplenishmentService _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ReplenishmentService]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ReplenishmentService>();
        }

        private WorkQueueSystem _workQueue;
        private InventoryService _inventoryService;
        private float _nextScan;

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;

            if (!ServiceLocator.TryGet(out _workQueue) || _workQueue == null) return;
            if (!ServiceLocator.TryGet(out _inventoryService) || _inventoryService == null) return;

            ScanForReplenishmentNeeds();
        }

        private void ScanForReplenishmentNeeds()
        {
            foreach (var pick in LocationRegistry.PickLocations)
            {
                if (!pick.IsAvailable) continue; // occupied, reserved, or on hold — nothing to do

                if (!SlotAssignmentService.TryGetSku(pick.Address, out string skuId) || string.IsNullOrEmpty(skuId))
                    continue; // no item assigned to this pick face

                if (HasPendingReplenishment(pick.Address)) continue; // already in flight

                if (!TryFindOldestReserve(_inventoryService, skuId, out LocationData reserve)) continue;

                CreateReplenishTask(pick, reserve, skuId);
            }
        }

        private bool HasPendingReplenishment(string pickAddress)
        {
            // Cancelled counts as finished here, not pending — a cancelled replenish left in the list
            // would otherwise block this pick address from ever getting a new one.
            return _workQueue.Tasks.Any(t =>
                t.Type == WorkTaskType.Replenish &&
                t.Status != WorkTaskStatus.Complete &&
                t.Status != WorkTaskStatus.Cancelled &&
                t.ToLocation == pickAddress);
        }

        /// <summary>
        /// FIFO: the Occupied reserve slot holding this SKU whose pallet was received earliest.
        ///
        /// Static and public because outbound PalletPick needs the identical question answered — a
        /// Reach Truck taking a full pallet to a staging lane picks its source exactly the way
        /// replenishment does. Two copies of this rule would eventually disagree about which pallet is
        /// next and rotate stock differently depending on where it was going.
        ///
        /// Occupied specifically, never Reserved: a Reserved slot is already promised to another task,
        /// and handing it out twice is how two trucks get sent for one pallet.
        /// </summary>
        public static bool TryFindOldestReserve(InventoryService inventory, string skuId, out LocationData best)
            => TryFindOldest(inventory, skuId, LocationRegistry.ReserveLocations, out best);

        /// <summary>
        /// A full pallet of this SKU anywhere in the racking — reserve first, then a PICK FACE.
        ///
        /// Outbound pallet picking asks this rather than TryFindOldestReserve, because a customer
        /// ordering whole pallets should get whole pallets moved by a Reach Truck wherever they happen
        /// to be standing. Refusing to take one off a pick face meant an order for three pallets could
        /// stall with a full pallet sitting in a pick slot, and the alternative — a selector walking off
        /// 120 cases by hand from that same face — is the slow way to move the identical freight.
        ///
        /// Reserve is still tried FIRST, and that ordering is the whole balance of it: pick faces exist
        /// so case picking has somewhere to go, and stripping one to fill a pallet order strands every
        /// case pick for that SKU until replenishment catches up. Reserve is the right source when it
        /// exists; a pick face is the right source when it doesn't.
        ///
        /// NOT included: pallets already standing in a staging lane. Those are reachable in principle
        /// (see ReachTruckOperator.FindExitAccessiblePallet) but the pickup path is a different
        /// routine, and only the exit-most pallet of a lane can be taken at all — so it needs its own
        /// work rather than being folded in here silently.
        /// </summary>
        public static bool TryFindOldestPalletAnywhere(InventoryService inventory, string skuId, out LocationData best)
        {
            if (TryFindOldest(inventory, skuId, LocationRegistry.ReserveLocations, out best)) return true;
            return TryFindOldest(inventory, skuId, LocationRegistry.PickLocations, out best);
        }

        /// <summary>
        /// A pallet of this SKU that's still sitting in a staging lane, its Putaway task not yet
        /// claimed by anyone — tried FIRST, ahead of TryFindOldestPalletAnywhere, so an outbound order
        /// released the moment freight for it comes off the truck can claim that freight directly
        /// instead of paying for a putaway trip immediately followed by a replenish trip back out.
        ///
        /// FIFO by ReceivedDayNumber, same rule as every other sourcing tier. Only pallets with a
        /// live (not Complete/Cancelled) Putaway task qualify — a pallet already staged as some OTHER
        /// order's picked outbound freight carries no Putaway task at all, so it can never be poached
        /// here. Deliberately does not verify the pallet is currently the lane's exit-most/reachable
        /// one — ReachTruckOperator's own pickup re-checks that immediately before grabbing anything
        /// and gracefully re-queues if it's since become buried, the same recovery Putaway itself
        /// already relies on.
        /// </summary>
        public static bool TryFindStagedPalletAwaitingPutaway(WorkQueueSystem workQueue, InventoryService inventory,
            string skuId, out string palletId, out string fromLocation)
        {
            palletId = null;
            fromLocation = null;
            if (workQueue == null || inventory == null || string.IsNullOrEmpty(skuId)) return false;

            int bestDay = int.MaxValue;

            foreach (var t in workQueue.Tasks)
            {
                if (t.Type != WorkTaskType.Putaway) continue;
                if (t.Status == WorkTaskStatus.Complete || t.Status == WorkTaskStatus.Cancelled) continue;
                if (string.IsNullOrEmpty(t.PalletId) || string.IsNullOrEmpty(t.FromLocation)) continue;

                var record = inventory.GetPallet(t.PalletId);
                if (record == null || record.SkuId != skuId) continue;

                if (record.ReceivedDayNumber < bestDay)
                {
                    bestDay = record.ReceivedDayNumber;
                    palletId = t.PalletId;
                    fromLocation = t.FromLocation;
                }
            }

            return palletId != null;
        }

        /// <summary>FIFO scan shared by both lookups above, so reserve and pick faces can't end up
        /// rotating stock by different rules.</summary>
        private static bool TryFindOldest(InventoryService inventory, string skuId,
                                          System.Collections.Generic.IEnumerable<LocationData> locations, out LocationData best)
        {
            best = null;
            if (inventory == null || string.IsNullOrEmpty(skuId) || locations == null) return false;

            int bestDay = int.MaxValue;

            foreach (var location in locations)
            {
                if (location.Status != LocationStatus.Occupied) continue;
                if (location.SkuId != skuId) continue;
                if (string.IsNullOrEmpty(location.PalletId)) continue;

                var record = inventory.GetPallet(location.PalletId);
                if (record == null) continue;

                if (record.ReceivedDayNumber < bestDay)
                {
                    bestDay = record.ReceivedDayNumber;
                    best = location;
                }
            }

            return best != null;
        }

        private void CreateReplenishTask(LocationData pick, LocationData reserve, string skuId)
        {
            // Lock both ends immediately — see class summary.
            pick.Reserve();
            reserve.Reserve();

            var area = _inventoryService.GetSkuData(skuId)?.StorageArea ?? PalletData.AreaCategory.Grocery;
            string description = $"Replenish {pick.Address} with SKU {skuId} from {reserve.Address}";

            _workQueue.CreateTask(
                WorkTaskType.Replenish,
                EmployeeRole.ReachTruckOperator,
                reserve.PalletId,
                description,
                fromLocation: reserve.Address,
                toLocation: pick.Address,
                area: area,
                priority: ReplenishPriority);
        }
    }
}
