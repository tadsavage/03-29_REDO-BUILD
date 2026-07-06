using GameCore.Inventory;
using GameCore.Services;
using GameCore.Events;
using UnityEngine;

namespace GameCore.Labor
{
    /// <summary>
    /// Orchestrates the receiving workflow (CHUNK 2 — Offload / Receiving). When a ghosted pallet (no
    /// PalletData script) lands in a staging lane from the dock stocker offload, this service:
    ///
    /// 1. Detects the ghosted pallet via placement events
    /// 2. Creates a WorkQueueEntry of type Receive (targeted at Receiver role)
    /// 3. Receiver picks up the task, approaches pallet, performs receiving animation
    /// 4. At 100%, CompleteReceiveTask() is called, which marks the work queue task complete
    ///
    /// Self-bootstrapping service (hidden DontDestroyOnLoad object).
    /// </summary>
    public class ReceivingService : MonoBehaviour
    {
        private static ReceivingService _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ReceivingService]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ReceivingService>();
        }

        private bool _subscribed;
        private EventManager _eventManager;
        private WorkQueueSystem _workQueue;
        private InventoryService _inventoryService;

        // Event fired when a receive task is created (receivers listen to this)
        public static event System.Action<WorkTask> OnReceiveTaskCreated;

        private void Update()
        {
            if (!_subscribed) TrySubscribe();
        }

        private void TrySubscribe()
        {
            _eventManager = EventManager.Instance;
            if (_eventManager == null) return;

            if (!ServiceLocator.TryGet<WorkQueueSystem>(out _workQueue) || _workQueue == null)
                return;

            if (!ServiceLocator.TryGet<InventoryService>(out _inventoryService) || _inventoryService == null)
                return;

            _eventManager.Subscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnPalletPlaced);
            _subscribed = true;
        }

        /// <summary>When a pallet is placed, check if it's ghosted and create a receive task.</summary>
        private void OnPalletPlaced(string eventId, PlacedObject palletGO)
        {
            if (!IsGhostedPallet(palletGO))
                return;

            // Get the pallet's master record from inventory
            var cell = new Vector2Int(palletGO.gridX, palletGO.gridY);
            var palletsAtCell = _inventoryService.GetPalletsAtLocation(cell);
            if (palletsAtCell.Count == 0)
            {
                Debug.LogWarning("[ReceivingService] Pallet placed but no PalletMasterRecord found at cell " + cell);
                return;
            }

            var masterRecord = palletsAtCell[palletsAtCell.Count - 1]; // Topmost (most recent)
            CreateReceiveTask(masterRecord);
        }

        /// <summary>Check if a placed object is a ghosted pallet (Inventory category, no PalletData script).</summary>
        private bool IsGhostedPallet(PlacedObject po)
        {
            if (po == null || !po.gameObject.activeInHierarchy || po.data == null)
                return false;

            // Must be Inventory category
            if (po.data.category != "Inventory")
                return false;

            // Must NOT have PalletData script (that's what makes it "ghosted")
            if (po.GetComponent<PalletData>() != null)
                return false;

            return true;
        }

        /// <summary>
        /// Create a WorkQueueEntry of type Receive for this ghosted pallet.
        /// Task description includes SKU and case count.
        /// </summary>
        private void CreateReceiveTask(PalletMasterRecord masterRecord)
        {
            CreateReceiveTaskForPallet(masterRecord);
        }

        /// <summary>
        /// Public entry point for callers that spawn physical pallets outside the normal
        /// PlaceCommand/OnObjectPlaced pipeline — currently TrailerOffloadController, whose
        /// dock-stocker-carried cargo pallets have no PlacedObject component (see PalletMasterLink)
        /// and therefore never fire OnObjectPlaced for OnPalletPlaced above to catch.
        /// </summary>
        public static void CreateReceiveTaskForPallet(PalletMasterRecord masterRecord)
        {
            if (masterRecord == null) return;
            if (!ServiceLocator.TryGet<WorkQueueSystem>(out var queue) || queue == null) return;
            if (!ServiceLocator.TryGet<InventoryService>(out var inventory) || inventory == null) return;

            string description = $"Receive pallet {masterRecord.SkuId} ({masterRecord.Quantity} cases)";
            string staging = LaneNamingService.AddressAt(masterRecord.CurrentLocation);
            string stagingLabel = staging != null ? $"STG{staging}" : null;

            var sku = inventory.GetSkuData(masterRecord.SkuId);
            var area = sku != null ? sku.StorageArea : PalletData.AreaCategory.Grocery; // Default to Grocery if SKU not found

            var task = queue.CreateTask(
                WorkTaskType.Receive,
                EmployeeRole.Receiver,
                masterRecord.PalletId,
                description,
                fromLocation: null,           // freight arrives on the trailer — no warehouse "from"
                toLocation: stagingLabel,     // received into its staging lane
                area: area);

            OnReceiveTaskCreated?.Invoke(task);
            Debug.Log($"[ReceivingService] Receive task created for pallet {masterRecord.PalletId} at staging lane (area: {area})");
        }

        /// <summary>
        /// Called by the receiver when receiving completes (fill bar reaches 100%).
        /// Marks the task complete so the receiver can pick up the next one.
        /// </summary>
        public static void CompleteReceiveTask(string taskId)
        {
            if (!ServiceLocator.TryGet<WorkQueueSystem>(out var queue) || queue == null)
                return;

            queue.CompleteTask(taskId);
            Debug.Log($"[ReceivingService] Receive task {taskId} completed");
        }
    }
}
