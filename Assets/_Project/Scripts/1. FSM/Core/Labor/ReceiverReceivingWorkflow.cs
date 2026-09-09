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

        // Track animation state to ensure it loops while receiving
        private float _animationCheckTimer;
        private const float AnimationLoopCheckInterval = 0.1f; // Check animation state 10x per second

        // How close the receiver must actually be to the pallet to receive it — per Tad, "receiving
        // from a mile away" is a real bug, not a stylistic nitpick: NavMesh arrival callbacks in this
        // project have a documented history of firing early (PathPartial, patrol hijack, a rebake
        // knocking the agent off mid-leg — see the receiver-deadlock notes elsewhere in this class'
        // sibling ReceivingTaskDriver), and nothing here ever verified the callback's claim against
        // reality. Checked both at the start (BeginReceivingAt) and continuously while receiving, since
        // an already-in-progress receive could just as easily be knocked out of range mid-animation.
        //
        // Must comfortably clear ReceivingTaskDriver's StandoffDistance (1.75m from the pallet pivot)
        // PLUS AiNavigation.SeekPosition's own arrival tolerance (remainingDistance <= 1.0m from the
        // stand position) — worst case the arrival callback fires ~2.75m from the pallet. At the old
        // 2f this rejected a real chunk of legitimate arrivals (observed live: "2.1m away (max 2m)"),
        // which released the task back to the queue, got it immediately re-claimed by the same
        // lane-sticky receiver, and re-triggered the same near-instant arrival/rejection — the
        // receiver visibly shuffling back and forth instead of ever starting to receive.
        private const float MaxReceivingDistance = 3f;

        // The RF gun's "Infra-Red" beam (LineRenderer child) — off by default on the prop prefab,
        // only switched on for the duration of the receiving animation. Looked up lazily rather than
        // cached at Awake() since the RF gun is equipped by ReceivingEquipmentService separately and
        // may not exist yet when this component is added.
        private GameObject _infraRedBeam;
        private bool _infraRedLookupDone;

        // Event fired when this workflow completes (caller can return to patrol/poll for next task)
        public event System.Action OnWorkflowComplete;

        // Fired instead of OnWorkflowComplete when receiving is aborted because the employee strayed
        // outside MaxReceivingDistance — distinct from a normal completion so ReceivingTaskDriver knows
        // to hand the task BACK to the queue (it was never actually finished) rather than treat it as done.
        public event System.Action OnWorkflowCancelled;

        /// <summary>True while this workflow actually has a pallet in hand. Exposed so the driver
        /// that owns the "task in progress" flag can verify it against reality instead of trusting
        /// that OnWorkflowComplete always lands — if the event is ever missed, the driver would
        /// otherwise stay flagged busy forever and stop claiming work entirely.</summary>
        public bool IsBusy => _state != ReceivingState.Idle;

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
        }

        private void Update()
        {
            if (_state == ReceivingState.Receiving)
            {
                if (IsOutOfRange())
                {
                    CancelReceiving("strayed outside receiving range mid-animation");
                    return;
                }
                UpdateReceiving();
                EnsureAnimationLooping();
            }
        }

        /// <summary>True once the employee is more than MaxReceivingDistance from the pallet they're
        /// meant to be receiving. Planar (Y ignored) so standing at the correct XZ spot on a ramp or
        /// slightly uneven floor never trips it.</summary>
        private bool IsOutOfRange()
        {
            if (_targetPallet == null) return false;
            Vector3 delta = _targetPallet.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude > MaxReceivingDistance * MaxReceivingDistance;
        }

        /// <summary>Aborts an in-progress (or never-actually-started) receive because the employee is
        /// too far from the pallet. Stops the fill bar and animation IMMEDIATELY — not the usual
        /// one-frame-deferred stop CompleteWorkflow uses for back-to-back pallets, since per Tad the
        /// switch to walking has to be instant, not smoothed over for a same-task handoff that isn't
        /// happening here. Fires OnWorkflowCancelled (not OnWorkflowComplete) so the caller hands the
        /// task back to the queue instead of treating it as finished.</summary>
        private void CancelReceiving(string reason)
        {
            Debug.LogWarning($"[ReceiverReceivingWorkflow] Cancelling receive for pallet " +
                             $"{_currentTask?.PalletId} — {reason} (distance " +
                             $"{(_targetPallet != null ? Vector3.Distance(transform.position, _targetPallet.position).ToString("F1") : "?")}m, " +
                             $"max {MaxReceivingDistance}m).");

            _fillBar?.CompleteReceiving(); // stops _isReceiving and hides the canvas — same effect a cancel needs, name notwithstanding
            if (_animator != null) _animator.SetBool("isReceiving", false);
            SetInfraRedBeam(false);
            _agentAnimation?.ClearFaceOverride();

            _state = ReceivingState.Idle;
            _currentTask = null;
            _targetPallet = null;
            _palletMasterRecord = null;

            OnWorkflowCancelled?.Invoke();
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

            // The arrival callback that leads here (AiNavigation.SeekPosition's onArrived) has a
            // documented history of firing while the agent is still well short of its destination
            // (PathPartial, patrol hijack, a rebake knocking it off mid-leg) — verify it actually got
            // here rather than trusting the callback fired for the right reason.
            if (IsOutOfRange())
            {
                Debug.LogWarning($"[ReceiverReceivingWorkflow] Arrival callback fired for pallet " +
                                 $"{task.PalletId} but the employee is {Vector3.Distance(transform.position, palletTransform.position):F1}m " +
                                 $"away (max {MaxReceivingDistance}m) — refusing to start receiving.");
                _currentTask = null;
                _targetPallet = null;
                OnWorkflowCancelled?.Invoke();
                return;
            }

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

            // Goods are paid for at PO dispatch (ShipmentService.CreatePlayerPurchaseOrder), not here —
            // charging again on physical receipt double-billed the player for every pallet. See
            // ShipmentService's "MONEY: goods are paid for when ORDERED" doc comment.

            // Fill in the pallet's PalletData — REUSING the one TruckController already put on it when
            // this pallet was built as trailer cargo, rather than adding a second.
            //
            // A bare AddComponent here gave every received pallet TWO PalletData components: the
            // cargo one with an empty LoadId, and this one with the real plate. Measured live: 12
            // pallet objects carrying 22 components. GetComponent<PalletData> returns whichever was
            // added first, so half the callers in the project were reading the blank cargo record —
            // no LoadId, staging-time SKU — off a pallet that had been properly received. That is the
            // same shape as the old "hover tooltip stuck on one item" bug the cargo path was fixed for.
            var sku = _inventoryService?.GetSkuData(_palletMasterRecord.SkuId);
            var palletData = _targetPallet.gameObject.GetComponent<PalletData>();
            if (palletData == null) palletData = _targetPallet.gameObject.AddComponent<PalletData>();
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

            // RULE: Pallet must have real material (SKU) and quantity > 0 to be eligible for putaway.
            var sku = _inventoryService?.GetSkuData(_palletMasterRecord.SkuId);
            if (sku == null || _palletMasterRecord.Quantity <= 0)
            {
                Debug.LogWarning($"[ReceiverReceivingWorkflow] Skipping Putaway task for pallet {_palletMasterRecord.PalletId} — no valid material or zero quantity.");
                return;
            }

            string address = LaneNamingService.AddressAt(_palletMasterRecord.CurrentLocation)
                              ?? _palletMasterRecord.CurrentLocation.ToString();

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
