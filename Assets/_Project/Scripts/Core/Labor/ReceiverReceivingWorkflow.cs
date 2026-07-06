using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;
using GameCore.Events;
using GameCore.Economy;

namespace GameCore.Labor
{
    /// <summary>
    /// Orchestrates the animation/fill-bar/data portion of a single Receive task, once the employee
    /// has already arrived at the pallet. Movement is NOT this class's job — any employee can be
    /// dynamically assigned "Go Receive Inbound" (see EmployeeAssignmentService/ReceivingTaskDriver),
    /// so walking to the pallet goes through the employee's own AiNavigation (AiNavigation.SeekPosition)
    /// instead of a dedicated NavMeshAgent here. ReceivingTaskDriver calls BeginReceivingAt() once
    /// AiNavigation reports arrival.
    ///
    /// Flow (from BeginReceivingAt):
    /// 1. Play receiving animation + start fill bar
    /// 2. At 100% fill:
    ///    - Assign a Load ID (first time this pallet is actually received)
    ///    - Create and attach PalletData script to the pallet
    ///    - Change material (ghost → solid case material)
    ///    - Spawn dust poof particle
    ///    - Fire OnPalletReceived, create the follow-up Putaway task
    /// 3. Complete work task and notify the caller (OnWorkflowComplete) so it can resume patrol/polling
    /// </summary>
    public class ReceiverReceivingWorkflow : MonoBehaviour
    {
        private enum ReceivingState { Idle, Receiving, Completing }

        [SerializeField] private ReceivingFillBar _fillBar;
        [SerializeField] private Animator _animator;
        [SerializeField] private AgentAnimation _agentAnimation;

        [SerializeField] private Material _solidCaseMaterial; // AA Low Poly Common
        [SerializeField] private string _dustProofPrefabPath = "Assets/_Project/Prefabs/FX/DustPoofParent.prefab";

        private ReceivingState _state = ReceivingState.Idle;
        private WorkTask _currentTask;
        private Transform _targetPallet;
        private PalletMasterRecord _palletMasterRecord;
        private EventManager _eventManager;
        private InventoryService _inventoryService;
        private WorkQueueSystem _workQueue;
        private MoneyService _moneyService;

        // Track animation state to ensure it loops while receiving
        private float _animationCheckTimer;
        private const float AnimationLoopCheckInterval = 0.1f; // Check animation state 10x per second

        // The RF gun's "Infra-Red" beam (LineRenderer child) — off by default on the prop prefab,
        // only switched on for the duration of the receiving animation. Looked up lazily rather than
        // cached at Awake() since the RF gun is equipped by ReceivingEquipmentService separately and
        // may not exist yet when this component is added.
        private GameObject _infraRedBeam;
        private bool _infraRedLookupDone;

        // Event fired when this workflow completes (caller can return to patrol/poll for next task)
        public event System.Action OnWorkflowComplete;

        private void Awake()
        {
            if (_fillBar == null)
                _fillBar = GetComponent<ReceivingFillBar>();
            if (_fillBar == null)
                _fillBar = gameObject.AddComponent<ReceivingFillBar>();
            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_agentAnimation == null)
                _agentAnimation = GetComponent<AgentAnimation>();

            // This component is added dynamically at runtime (no dedicated Receiver prefab to hold
            // an Inspector assignment), so _solidCaseMaterial can never be hand-set — load it here
            // the same way TruckController loads _ghostMaterial. AA_LowPolyCommon.mat lives outside
            // any Resources folder, so the Resources.Load fallback only works once/if it's copied
            // into one for a built player — fine for Editor testing, a known gap otherwise.
            if (_solidCaseMaterial == null)
            {
                #if UNITY_EDITOR
                _solidCaseMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/AA_LowPolyCommon.mat");
                #endif
                if (_solidCaseMaterial == null)
                    _solidCaseMaterial = Resources.Load<Material>("AA_LowPolyCommon");
                if (_solidCaseMaterial == null)
                    Debug.LogWarning("[ReceiverReceivingWorkflow] Could not load AA_LowPolyCommon material — received pallets will stay ghosted.");
            }

            _eventManager = EventManager.Instance;
            ServiceLocator.TryGet<InventoryService>(out _inventoryService);
            ServiceLocator.TryGet<WorkQueueSystem>(out _workQueue);
            ServiceLocator.TryGet<MoneyService>(out _moneyService);
        }

        private void Update()
        {
            if (_state == ReceivingState.Receiving)
            {
                UpdateReceiving();
                EnsureAnimationLooping();
            }
        }

        /// <summary>Ensures the receiving animation loops continuously while receiving is active.
        /// Some animator controllers may not have the receiving animation set to loop, so this
        /// periodically checks and restarts the animation if it has stopped.</summary>
        private void EnsureAnimationLooping()
        {
            if (_animator == null) return;

            _animationCheckTimer -= Time.deltaTime;
            if (_animationCheckTimer > 0) return;
            _animationCheckTimer = AnimationLoopCheckInterval;

            // Get the current animator state info for the base layer
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

            // If we're in a receiving state, check if animation is actually playing
            // If the animation has finished (normalizedTime >= 1 and not looping), restart it
            if (_animator.GetBool("isReceiving"))
            {
                // If the normalized time is past 1.0, the animation finished without looping
                if (stateInfo.normalizedTime > 1.0f)
                {
                    // Force the animation to restart by briefly disabling and re-enabling the parameter
                    _animator.SetBool("isReceiving", false);
                    _animator.SetBool("isReceiving", true);
                    Debug.LogWarning("[ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop");
                }
            }
        }

        /// <summary>Called by ReceivingTaskDriver once the employee has arrived at the pallet.</summary>
        public void BeginReceivingAt(WorkTask task, Transform palletTransform)
        {
            // Allow immediate transitions between receive tasks (Completing → Receiving) without going through Idle.
            // This prevents the receiving animation from dropping to idle between consecutive pallets.
            if (_state != ReceivingState.Idle && _state != ReceivingState.Completing)
            {
                Debug.LogWarning("[ReceiverReceivingWorkflow] Already in a workflow, cannot start new task");
                return;
            }

            if (palletTransform == null)
            {
                Debug.LogError($"[ReceiverReceivingWorkflow] No pallet transform for task {task?.PalletId}");
                CompleteWorkflow();
                return;
            }

            _currentTask = task;
            _targetPallet = palletTransform;

            if (_inventoryService != null)
                _palletMasterRecord = _inventoryService.GetPallet(task.PalletId);

            if (_palletMasterRecord == null)
            {
                Debug.LogError($"[ReceiverReceivingWorkflow] No master record for pallet {task.PalletId}");
                CompleteWorkflow();
                return;
            }

            Debug.Log($"[ReceiverReceivingWorkflow] Starting receive task for pallet {task.PalletId}");
            _state = ReceivingState.Receiving;
            StartReceiving();
        }

        private void StartReceiving()
        {
            // Play receiving animation (loop)
            if (_animator != null)
                _animator.SetBool("isReceiving", true);

            // Reset animation check timer to start checking immediately
            _animationCheckTimer = 0f;

            // Start fill bar
            if (_fillBar != null)
                _fillBar.StartReceiving();

            SetInfraRedBeam(true);

            // Turn to face the pallet for the duration of the animation — AgentAnimation normally
            // only faces movement direction, so without this the receiver is left facing whichever
            // way they happened to be walking when they arrived at the stand position, not the pallet.
            if (_agentAnimation != null && _targetPallet != null)
                _agentAnimation.FaceTowards(_targetPallet.position);

            Debug.Log("[ReceiverReceivingWorkflow] Starting receiving animation and fill bar");
        }

        /// <summary>Turns the RF gun's infra-red beam on/off — on only for the duration of the
        /// receiving animation, per Tad. Looked up on first use (see field comment) and cached after.
        /// Must search under the equipped RF gun instance specifically, NOT GetComponentInChildren on
        /// this employee's own root — the root also carries NavAgentGuidance's path-guidance
        /// LineRenderer (added directly onto it in NavAgentGuidance.Awake), which a root-wide search
        /// matches first since GetComponentInChildren checks the calling object before its
        /// descendants. That was silently toggling the wrong (already-active) LineRenderer while the
        /// real Infra-Red child under _Scan_Gun stayed disabled.</summary>
        private void SetInfraRedBeam(bool on)
        {
            if (!_infraRedLookupDone)
            {
                _infraRedLookupDone = true;
                var identity = GetComponent<EmployeeIdentity>();
                var rfGun = ReceivingEquipmentService.GetRfGunInstance(identity);
                var lineRenderer = rfGun != null ? rfGun.GetComponentInChildren<LineRenderer>(true) : null;
                _infraRedBeam = lineRenderer != null ? lineRenderer.gameObject : null;
                if (_infraRedBeam == null)
                    Debug.LogWarning("[ReceiverReceivingWorkflow] No LineRenderer found under the equipped RF gun — infra-red beam won't show.");
            }

            _infraRedBeam?.SetActive(on);
        }

        private void UpdateReceiving()
        {
            // Check if fill bar is complete
            if (_fillBar == null || _fillBar.IsReceiving)
                return;

            // Fill bar completed
            Debug.Log("[ReceiverReceivingWorkflow] Fill bar complete, applying PalletData");
            _state = ReceivingState.Completing;
            ApplyPalletDataAndComplete();
        }

        private void ApplyPalletDataAndComplete()
        {
            if (_targetPallet == null || _palletMasterRecord == null)
            {
                CompleteWorkflow();
                return;
            }

            // License plate is assigned HERE, at actual receiving — not at the earlier staging drop.
            if (string.IsNullOrEmpty(_palletMasterRecord.LoadId))
                _palletMasterRecord.LoadId = GameCore.Labor.LoadIDGenerator.Generate();

            // Pay the vendor for the wholesale cost of the goods — the moment a pallet is actually
            // received (not when it's staged) is when we're on the hook for it. Lump-summed under
            // FinanceCategory.PurchasedGoods for now; per Tad, break it out further (by vendor/SKU)
            // later.
            ChargeForGoods();

            // Add PalletData script to the pallet
            var sku = _inventoryService?.GetSkuData(_palletMasterRecord.SkuId);
            var palletData = _targetPallet.gameObject.AddComponent<PalletData>();
            palletData.Initialize(
                _palletMasterRecord.LoadId,
                _palletMasterRecord.SkuId,
                _palletMasterRecord.Quantity,
                _palletMasterRecord.ExpirationDayNumber,
                sku != null ? sku.StorageArea : PalletData.AreaCategory.Grocery,
                sku != null ? sku.Icon : null,
                _palletMasterRecord.CurrentLocation);

            // Restore each case's ORIGINAL material (saved by PalletBuilder.GhostCases when this
            // pallet was loaded into the trailer) — not a single hardcoded solid material, since
            // different SKUs' cases can use different materials. Falls back to the hardcoded
            // _solidCaseMaterial only if this pallet has no PalletBuilder (shouldn't normally happen —
            // TruckController.LoadShipment always builds cargo pallets via PalletBuilder).
            var builder = _targetPallet.GetComponentInChildren<PalletBuilder>();
            if (builder != null && builder.OriginalCaseMaterial != null)
                builder.RestoreCaseMaterial();
            else
                ChangePalletMaterial(_targetPallet.gameObject, _solidCaseMaterial);

            // Spawn dust poof effect
            SpawnDustProof(_targetPallet.position);

            // Fire events + queue the follow-up Putaway task
            FireCompletionEvents();
            CreatePutawayTask();

            // Complete work queue task
            if (_currentTask != null)
                ReceivingService.CompleteReceiveTask(_currentTask.TaskId);

            Debug.Log($"[ReceiverReceivingWorkflow] Pallet {_palletMasterRecord.PalletId} receiving complete");

            CompleteWorkflow();
        }

        /// <summary>Deducts quantity x SkuData.UnitCost from capital — the wholesale cost owed to the
        /// vendor for this pallet — and shows the same floating "-$X" popup used for build-menu
        /// purchases. No-op (with a warning) if the SKU can't be resolved, so a bad/test SKU doesn't
        /// throw away the whole receiving flow over a missing cost.</summary>
        private void ChargeForGoods()
        {
            if (_moneyService == null) return;

            var sku = _inventoryService?.GetSkuData(_palletMasterRecord.SkuId);
            if (sku == null)
            {
                Debug.LogWarning($"[ReceiverReceivingWorkflow] No SkuData for '{_palletMasterRecord.SkuId}' — cannot charge for goods on pallet {_palletMasterRecord.PalletId}.");
                return;
            }

            int totalCost = Mathf.RoundToInt(sku.BuyValue * _palletMasterRecord.Quantity);
            if (totalCost <= 0) return;

            _moneyService.Deduct(totalCost, FinanceCategory.PurchasedGoods);
            FloatingMoneyText.Show(_targetPallet.position + Vector3.up * 1.5f, -totalCost);
        }

        private void ChangePalletMaterial(GameObject palletGO, Material newMaterial)
        {
            if (newMaterial == null)
            {
                Debug.LogWarning("[ReceiverReceivingWorkflow] Solid case material not assigned, skipping material change");
                return;
            }

            // Find all Renderers in the pallet prefab and change material — fill every submesh slot
            // (not just slot 0 via the singular .material setter) so multi-material meshes fully
            // switch over instead of leaving later slots on the ghost material. includeInactive:true
            // for the same reason TruckController's ghosting pass uses it.
            var renderers = palletGO.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var solids = new Material[renderer.sharedMaterials.Length];
                for (int m = 0; m < solids.Length; m++) solids[m] = newMaterial;
                renderer.sharedMaterials = solids;
            }

            Debug.Log($"[ReceiverReceivingWorkflow] Changed material on {renderers.Length} renderer(s) to solid");
        }

        private void SpawnDustProof(Vector3 position)
        {
            #if UNITY_EDITOR
            var dustProofPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(_dustProofPrefabPath);
            #else
            var dustProofPrefab = Resources.Load<GameObject>("Prefabs/FX/DustPoofParent");
            #endif

            if (dustProofPrefab == null)
            {
                Debug.LogWarning("[ReceiverReceivingWorkflow] DustProofParent prefab not found");
                return;
            }

            Instantiate(dustProofPrefab, position, Quaternion.identity);
            Debug.Log("[ReceiverReceivingWorkflow] Spawned dust poof effect");
        }

        private void FireCompletionEvents()
        {
            // Notify inventory/shipment tracking that pallet is now live
            _eventManager?.Publish(GameEvents.Inventory.OnPalletReceived, _palletMasterRecord);
        }

        /// <summary>Files the Putaway task now that the pallet is solid/received — this used to happen
        /// immediately on staging drop (old TrailerOffloadController flow), but a pallet must be
        /// received first now.</summary>
        private void CreatePutawayTask()
        {
            if (_workQueue == null) return;

            string address = LaneNamingService.AddressAt(_palletMasterRecord.CurrentLocation)
                              ?? _palletMasterRecord.CurrentLocation.ToString();

            var sku = _inventoryService?.GetSkuData(_palletMasterRecord.SkuId);
            var area = sku != null ? sku.StorageArea : PalletData.AreaCategory.Grocery;

            _workQueue.CreateTask(
                WorkTaskType.Putaway,
                EmployeeRole.ReachTruckOperator,
                _palletMasterRecord.PalletId,
                $"Putaway pallet [{_palletMasterRecord.LoadId}] staged at {address}",
                fromLocation: $"STG{address}",   // picked up from the staging lane
                toLocation: null,                // reserve/pick slot not resolved until putaway logic (Chunk 2)
                area: area);
        }

        private void CompleteWorkflow()
        {
            _state = ReceivingState.Idle;
            _currentTask = null;
            _targetPallet = null;
            _palletMasterRecord = null;

            SetInfraRedBeam(false);
            _agentAnimation?.ClearFaceOverride();

            OnWorkflowComplete?.Invoke();

            // Defer stopping the animation by one frame to allow back-to-back receive tasks to start
            // without the receiver dropping into idle between them. If a new task starts in
            // BeginReceivingAt() before this deferred call, it will set isReceiving=true,
            // preventing the animation glitch between consecutive pallets.
            if (_animator != null)
                Invoke(nameof(StopReceivingAnimation), 0.016f); // ~one frame at 60fps
        }

        private void StopReceivingAnimation()
        {
            // Only stop if we truly went idle (no new task restarted us)
            if (_state == ReceivingState.Idle && _animator != null)
                _animator.SetBool("isReceiving", false);
        }
    }
}
