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

                if (!TryFindOldestReserve(skuId, out LocationData reserve)) continue;

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

        /// <summary>FIFO: the Occupied reserve slot holding this SKU whose pallet was received earliest.</summary>
        private bool TryFindOldestReserve(string skuId, out LocationData best)
        {
            best = null;
            int bestDay = int.MaxValue;

            foreach (var reserve in LocationRegistry.ReserveLocations)
            {
                if (reserve.Status != LocationStatus.Occupied) continue;
                if (reserve.SkuId != skuId) continue;
                if (string.IsNullOrEmpty(reserve.PalletId)) continue;

                var record = _inventoryService.GetPallet(reserve.PalletId);
                if (record == null) continue;

                if (record.ReceivedDayNumber < bestDay)
                {
                    bestDay = record.ReceivedDayNumber;
                    best = reserve;
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
