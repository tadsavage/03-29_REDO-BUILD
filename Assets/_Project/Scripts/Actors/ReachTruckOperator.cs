using System.Collections;
using System.Collections.Generic;
using System.Linq;

using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;
using UnityEngine;
using UnityEngine.AI;

namespace GameCore.Actors
{
    /// <summary>
    /// Scripted putaway controller for a Reach Truck vehicle.
    ///
    /// REVISION: Implements stacking logic (wait for top pallet), received-only checks,
    /// and accessibility filtering during task selection.
    /// </summary>
    [RequireComponent(typeof(AiNavigation))]
    [RequireComponent(typeof(MHEOperatorSlot))]
    public class ReachTruckOperator : MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────────────────────────

        private const float DriveSpeed          = 2.5f;
        private const float TurnSpeed           = 120f;
        private const float ArriveThreshold     = 0.15f;

        /// <summary>Tolerance for the final chassis alignment onto a rack location's approach anchor,
        /// run manually right before the fork sequence. Tighter than the general
        /// <see cref="ArriveThreshold"/> because any residual offset here shows up directly as the
        /// pallet entering the bay at an angle.</summary>
        private const float PrecisePlaceThreshold = 0.10f;
        private const float FaceThreshold       = 2f;
        private const float TaskPollInterval    = 1f;

        private const float PalletHalfHeight    = 0.085f;
        private const float ForkBladeHeightOffset = 0.08f;
        private const float ForkTravelHeight    = 0.5f;
        private const float ForkGrabProximity  = 0.15f;
        private const float ForkRackClearance   = 0.15f;
        private const float ForkDepositDrop     = 0.12f;

        private const float MaxLaneInsertTravel = 8f;

        /// <summary>How long a pallet is skipped for re-claim after a putaway couldn't find any rack
        /// destination (warehouse full). Stops the pick-up→no-space→strand→re-pick livelock from
        /// hammering every poll while still retrying periodically in case space frees up.</summary>
        private const float NoDestinationBackoff = 15f;
        
        /// <summary>Safety cap on fork extension. Increased to 5.0 to reach center of deep racks.</summary>
        private const float MaxForkExtend       = 5.0f;


        private const string ForkChildName          = "Forks";
        private const string PalletAnchorChildName  = "PalletAnchor";
        private const string ChepAnchorFrontName    = "ChepAnchorFront";
        private const string ChepAnchorRearName     = "ChepAnchorRear";
        private const string LocApproachAnchorName  = "LocApproachAnchor";
        private const string InventoryContainerName = "Inventory";

        // ── Inspector ─────────────────────────────────────────────────────────────────────────────

        [Header("Fork Movement")]
        [Tooltip("Speed (m/s) at which the forks raise and lower.")]
        [SerializeField] private float _forkLiftSpeed   = 1.0f;

        [Tooltip("Speed (m/s) at which the forks extend and retract along their local Z axis.")]
        [SerializeField] private float _forkExtendSpeed = 1.5f;

        [Header("Variance")]
        [Tooltip("Maximum XZ distance (m) between forks and pallet pivot before the pallet is grabbed.")]
        [SerializeField, Range(0f, 0.25f)] private float _grabVariance  = 0.15f;

        [Tooltip("Maximum XZ distance (m) between pallet and slot centre before the pallet is released.")]
        [SerializeField, Range(0f, 0.5f)]  private float _placeVariance = 0.25f;

        // ── Runtime ───────────────────────────────────────────────────────────────────────────────

        private MHEOperatorSlot  _operatorSlot;
        private AiNavigation     _vehicleNav;
        private NavMeshAgent     _vehicleAgent;
        private Transform        _forks;
        private Transform        _palletAnchor;
        private float            _forkRestLocalY;
        private float            _forkRestLocalZ;

        private float _forkAxisSign = -1f;

        private WorkQueueSystem  _workQueue;
        private PutawayLogic     _putawayLogic;
        private InventoryService _inventoryService;

        private float _pollTimer;
        private bool  _busy;

        // Throttled state dump so a stalled truck is diagnosable without flooding the console —
        // fires regardless of _busy/occupied/role so it can reveal exactly which precondition
        // is blocking Update() from ever reaching TryClaimAndStart().
        private float _diagTimer;
        private const float DiagInterval = 5f;

        // palletId → earliest Time.time it may be re-claimed. Populated when a putaway can't find any
        // rack destination, so the truck doesn't livelock re-picking an un-storable pallet.
        private readonly Dictionary<string, float> _blockedUntil = new Dictionary<string, float>();

        // The carried pallet's resting pose, captured just before pickup. ANY abort that un-parents the
        // pallet (including the rack-delivery legs in DeliverPalletToRack) restores it to this pose so a
        // failed putaway drops the pallet exactly where it started instead of stranding it at the forks'
        // elevated carry height — which would otherwise ratchet it higher on every re-pick attempt.
        private bool       _carryOriginValid;
        private Vector3    _carryOriginPos;
        private Quaternion _carryOriginRot;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _operatorSlot = GetComponent<MHEOperatorSlot>();
            _vehicleNav   = GetComponent<AiNavigation>();
            _vehicleAgent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            _forks = FindDeepChild(transform, ForkChildName);
            if (_forks != null)
            {
                _forkRestLocalY = _forks.localPosition.y;
                _forkRestLocalZ = _forks.localPosition.z;
                if (Mathf.Abs(_forks.localPosition.z) > 0.05f)
                    _forkAxisSign = _forks.localPosition.z > 0f ? 1f : -1f;
            }
            _palletAnchor = FindDeepChild(transform, PalletAnchorChildName);
        }

        private void Update()
        {
            _diagTimer -= Time.deltaTime;
            if (_diagTimer <= 0f)
            {
                _diagTimer = DiagInterval;
                LogDiagnostics();
            }

            if (_busy) return;
            if (!_operatorSlot.IsOccupied) return;

            var op = _operatorSlot.CurrentOperator;
            if (op?.Record == null || op.Record.role != EmployeeRole.ReachTruckOperator) return;

            ResolveServices();
            if (_workQueue == null) return;

            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = TaskPollInterval;

            TryClaimAndStart();
        }

        /// <summary>
        /// Throttled snapshot of every precondition Update() checks before it will even attempt
        /// TryClaimAndStart() — printed on a timer (not gated by _busy) so a truck that's silently
        /// stuck (e.g. _busy stuck true after a save/load resume coroutine died) is diagnosable
        /// instead of producing zero console output.
        /// </summary>
        private void LogDiagnostics()
        {
            string guid = _operatorSlot?.CurrentOperator?.Record?.employeeGuid;
            bool occupied = _operatorSlot != null && _operatorSlot.IsOccupied;
            var role = _operatorSlot?.CurrentOperator?.Record?.role;

            int availablePutawayOrReplenish = _workQueue?.Tasks.Count(t =>
                t.RequiredRole == EmployeeRole.ReachTruckOperator &&
                t.Status == WorkTaskStatus.Available) ?? -1;

            int assignedToMe = _workQueue?.Tasks.Count(t =>
                t.AssignedToEmployeeGuid == guid && t.Status == WorkTaskStatus.Assigned) ?? -1;

            Debug.Log($"[ReachTruckOperator] '{name}' diag: busy={_busy} occupied={occupied} " +
                $"role={role} workQueueNull={_workQueue == null} " +
                $"availablePutawayReplenish={availablePutawayOrReplenish} assignedToMe={assignedToMe}");
        }

        private void TryClaimAndStart()
        {
            // Self-healing: release any task stuck in Assigned whose claiming operator/coroutine
            // died mid-task without completing or aborting it — otherwise it's permanently
            // invisible (GetPendingTasksForRole only returns Pending, and "resume mine" below
            // only matches the exact same guid), silently swallowing pallets that are physically
            // accessible. Cheap no-op when nothing is actually stale.
            _workQueue.ReleaseStaleAssignments();

            string guid = _operatorSlot.CurrentOperator?.Record?.employeeGuid;
            if (string.IsNullOrEmpty(guid)) return;

            // FIRST: Check if this operator already has an Assigned task that they should be working on.
            // This handles manual assignments or tasks that were assigned but not yet active.
            WorkTask alreadyAssigned = _workQueue.Tasks.FirstOrDefault(t =>
                t.RequiredRole == EmployeeRole.ReachTruckOperator &&
                t.Status == WorkTaskStatus.Assigned &&
                t.AssignedToEmployeeGuid == guid);

            if (alreadyAssigned != null && IsPalletBlocked(alreadyAssigned.PalletId))
            {
                Debug.LogWarning($"[ReachTruckOperator] '{name}' assigned task {alreadyAssigned.TaskId} " +
                    $"(pallet {alreadyAssigned.PalletId}) is backed off — no other tasks will be claimed until it clears.");
                return; // this operator's assigned pallet has no rack space yet — wait out the backoff
            }

            if (alreadyAssigned != null)
            {
                Debug.Log($"[ReachTruckOperator] '{name}' resuming already assigned task {alreadyAssigned.TaskId}");
                _busy = true;
                if (alreadyAssigned.Type == WorkTaskType.Replenish)
                    StartCoroutine(ReplenishRoutine(alreadyAssigned));
                else if (alreadyAssigned.Type == WorkTaskType.PalletPick)
                    StartCoroutine(PalletPickRoutine(alreadyAssigned));
                else
                    StartCoroutine(PutawayRoutine(alreadyAssigned));
                return;
            }

            var pending = _workQueue.GetPendingTasksForRole(EmployeeRole.ReachTruckOperator);
            if (pending.Count == 0)
            {
                Debug.LogWarning($"[ReachTruckOperator] '{name}' found no Available Putaway/Replenish tasks this poll.");
                return;
            }

            // Selection rule: highest WorkTask.Priority wins (higher = more urgent — Replenish defaults
            // to 250, Putaway to 100, an empty pick slot blocks picking so it jumps the queue); ties keep
            // whichever candidate was found first, which is the earliest-created task since
            // GetPendingTasksForRole preserves WorkQueueSystem's creation order — i.e. FIFO within a
            // priority tier. Proximity/lane-slot-number scoring was deliberately removed: pick order
            // should be driven by priority only, not by which lane happens to be closest to this truck.
            WorkTask best = null;

            foreach (var t in pending)
            {
                // CRITICAL: Reach Trucks do Putaway (staging lane -> rack), Replenish (reserve -> pick
                // slot) and PalletPick (reserve -> outbound staging lane). They MUST NOT claim
                // Receive/OrderSelect/Load tasks (other roles).
                if (t.Type != WorkTaskType.Putaway &&
                    t.Type != WorkTaskType.Replenish &&
                    t.Type != WorkTaskType.PalletPick) continue;

                if (t.Type == WorkTaskType.PalletPick)
                {
                    // Destination is stamped at release. An unreleased pallet pick shouldn't be
                    // claimable at all (it's Open, and GetPendingTasksForRole only returns Available),
                    // so a missing lane here means something released it wrong — say so rather than
                    // driving a pallet to nowhere.
                    if (!TryParseLaneName(t.ToLocation, out int pd, out string pl))
                    {
                        Debug.LogWarning($"[ReachTruckOperator] PalletPick {t.TaskId} has no parseable " +
                                         $"destination lane ('{t.ToLocation}'). Skipping.");
                        continue;
                    }
                    if (!LaneNamingService.TryGetLaneGeometry(pd, pl, out _)) continue;
                    // Only claimable while a pallet of this SKU actually exists SOMEWHERE the truck can
                    // take it from. Must ask the same question TryBindPalletPickSource does — this gate
                    // was still reserve-only after the bind widened to pick faces, so a pallet standing
                    // in a pick slot was rejected here and the widened bind could never be reached.
                    // Checked WITHOUT reserving: reserving here would lock a slot for a task this truck
                    // may yet lose to a higher-priority one on the same poll.
                    //
                    // Skipped entirely when PalletId is already set: OrderService.FileReleasedOrderTasks
                    // reserves the source pallet at release time now, not at claim time. That reserved
                    // location sits at LocationStatus.Reserved (not Occupied), so re-running this same
                    // query here would find nothing and wrongly reject a task that's already spoken for.
                    if (string.IsNullOrEmpty(t.PalletId) &&
                        !ReplenishmentService.TryFindOldestPalletAnywhere(_inventoryService, t.SkuId, out _)) continue;
                }

                if (t.Type == WorkTaskType.Putaway)
                {
                    if (string.IsNullOrEmpty(t.FromLocation) || t.FromLocation == "STG" ||
                        t.FromLocation.Contains("(") || t.FromLocation.Contains(")"))
                    {
                        Debug.LogWarning($"[ReachTruckOperator] Task {t.TaskId} has invalid FromLocation '{t.FromLocation}'. Skipping.");
                        continue;
                    }

                    if (!TryParseLaneName(t.FromLocation, out int d, out string l))
                    {
                        Debug.LogWarning($"[ReachTruckOperator] Task {t.TaskId} FromLocation '{t.FromLocation}' failed to parse as a lane name. Skipping.");
                        continue;
                    }
                    if (!LaneNamingService.TryGetLaneGeometry(d, l, out _))
                    {
                        Debug.LogWarning($"[ReachTruckOperator] Task {t.TaskId} lane {d}{l} has no resolved geometry yet. Skipping this poll.");
                        continue;
                    }

                    // RULE: Only claim tasks for pallets that are currently accessible (topmost and received).
                    if (!IsPalletAccessible(t.PalletId, d, l))
                    {
                        Debug.LogWarning($"[ReachTruckOperator] Task {t.TaskId} pallet {t.PalletId} is not the exit-most accessible pallet in lane {d}{l} (buried or not yet received). Skipping.");
                        continue;
                    }
                }
                // Replenish tasks already know both endpoints (rack addresses resolved and reserved
                // by ReplenishmentService at creation) — no lane/accessibility check needed here.

                // Skip pallets that recently found no rack destination (warehouse full) — retried after backoff.
                if (IsPalletBlocked(t.PalletId)) continue;

                // Higher Priority value wins (Replenish defaults to 250, Putaway to 100).
                if (best == null || t.Priority > best.Priority)
                    best = t;
            }

            if (best == null) return;

            if (!_workQueue.TryClaimSpecificTask(best, guid)) return;

            // NOW the source pallet is chosen and locked, once this truck definitely owns the task.
            // Doing it during the scan above would reserve a slot for every candidate considered.
            //
            // Skipped when PalletId is already set: release-time reservation (see
            // OrderService.FileReleasedOrderTasks) already picked and locked this task's source, so
            // there's nothing left to bind. This only runs at all for a PalletPick that was filed
            // unassigned because no stock existed yet at release — the fallback path this used to be
            // the only path for.
            if (best.Type == WorkTaskType.PalletPick && string.IsNullOrEmpty(best.PalletId) && !TryBindPalletPickSource(best))
            {
                // Someone took the last reserve pallet between the scan and the claim. Hand the task
                // straight back rather than starting a routine that has nothing to fetch.
                best.Status = WorkTaskStatus.Available;
                best.AssignedToEmployeeGuid = null;
                return;
            }

            _busy = true;
            if (best.Type == WorkTaskType.Replenish)
                StartCoroutine(ReplenishRoutine(best));
            else if (best.Type == WorkTaskType.PalletPick)
                StartCoroutine(PalletPickRoutine(best));
            else
                StartCoroutine(PutawayRoutine(best));
        }

        /// <summary>
        /// Resolves and locks the reserve pallet a PalletPick will take, stamping it onto the task.
        ///
        /// The FIFO rule is ReplenishmentService's, shared rather than copied so inbound replenishment
        /// and outbound pallet picking rotate stock the same way. Reserving the slot immediately is
        /// what stops a second truck — or the replenishment scanner — being handed the same pallet.
        /// </summary>
        private bool TryBindPalletPickSource(WorkTask task)
        {
            // Reserve first, then a pick face — a whole pallet is a whole pallet wherever it's standing,
            // and refusing to take one off a pick slot stalled orders with stock plainly on the shelf.
            // See ReplenishmentService.TryFindOldestPalletAnywhere for why reserve keeps priority.
            if (!ReplenishmentService.TryFindOldestPalletAnywhere(_inventoryService, task.SkuId, out var reserve))
            {
                Debug.LogWarning($"[ReachTruckOperator] '{name}' claimed PalletPick {task.TaskId} for SKU " +
                                 $"{task.SkuId} but no reserve or pick slot holds a pallet of it any more.");
                return false;
            }

            reserve.Reserve();
            task.PalletId = reserve.PalletId;
            task.AssignFromLocation(reserve.Address);
            return true;
        }

        /// <summary>
        /// A pallet is accessible if it is the topmost pallet in the first slot (from exit) 
        /// that contains pallets in its staging lane, and it has been received (has PalletData).
        /// </summary>
        /// <summary>
        /// Finds the ONE pallet pickable from this lane right now: the topmost pallet in the exit-most
        /// occupied slot, provided it's been received (has PalletData). Anything behind or beneath it is
        /// blocked. This is the single source of truth shared by claim-time accessibility
        /// (IsPalletAccessible) and the routine's physical pickup (FindExitPallet) so the two can NEVER
        /// disagree — a prior divergence (an extra slot-world-position distance gate that only the pickup
        /// path applied) let the truck claim a task then instantly abort it as "no longer accessible",
        /// looping forever in place. We deliberately do NOT gate on the pallet's distance from its
        /// COMPUTED slot centre: the forks drive to the pallet's own child anchors, so pickup always
        /// targets the pallet's real transform wherever it physically sits. Returns null (palletId null)
        /// if the lane is empty or its exit-most pallet hasn't been received yet.
        /// </summary>
        private PalletMasterLink FindExitAccessiblePallet(int door, string lane, out string palletId)
        {
            palletId = null;
            List<LaneNamingService.LaneSlot> slots = LaneNamingService.GetLane(door, lane);
            if (slots.Count == 0) return null;

            // Iterate from the exit end of the lane inward; the first occupied slot is the only reachable one.
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                List<PalletMasterRecord> pallets = _inventoryService?.GetPalletsAtLocation(slots[i].Cell);
                if (pallets == null || pallets.Count == 0) continue;

                PalletMasterRecord topmost = pallets[pallets.Count - 1];
                PalletMasterLink   link    = PalletMasterLink.Find(topmost.PalletId);
                if (link == null) return null;

                // RULE: the exit-most pallet must be received before an RTO can take it.
                if (link.GetComponent<PalletData>() == null) return null;

                palletId = topmost.PalletId;
                return link;
            }
            return null;
        }

        /// <summary>True if <paramref name="targetPalletId"/> is the exit-most reachable, received pallet
        /// in the lane — i.e. this task can be claimed AND physically executed right now.</summary>
        private bool IsPalletAccessible(string targetPalletId, int door, string lane)
        {
            FindExitAccessiblePallet(door, lane, out string exitId);
            return exitId != null && exitId == targetPalletId;
        }

        private IEnumerator PutawayRoutine(WorkTask task)
        {
            // NOTE: deliberately NOT Commandeer()'d here — the vehicle stays on its own
            // NavMeshAgent (whatever patrol left it on) until the first long-distance leg below,
            // which hands off to manual control itself once it arrives. See SeekViaNavMesh.
            _carryOriginValid = false; // no pallet on the forks yet — the carry pose becomes valid at pickup.
            Debug.Log($"[ReachTruckOperator] '{name}' STARTING task {task.TaskId} for pallet {task.PalletId} from {task.FromLocation}");

            // 1. Resolve Lane ----------------------------------------------------------------------
            if (!TryParseLaneName(task.FromLocation, out int door, out string lane) ||
                !LaneNamingService.TryGetLaneGeometry(door, lane, out var geo))
            {
                Debug.LogWarning($"[ReachTruckOperator] Cannot resolve geometry for {task.FromLocation}.");
                yield return AbortRoutine(task, null, null);
                yield break;
            }

            Vector3 exitPoint = new Vector3(geo.ExitPoint.x, transform.position.y, geo.ExitPoint.z);

            // 2. Find the target pallet — BEFORE driving or lifting anything.
            // REVISION (Substitution): If the target pallet is buried, we can substitute with the
            // physically accessible pallet at the front of the same lane.
            Transform pallet = FindExitPallet(door, lane, out string actualPalletId);
            if (pallet == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Lane {door}{lane} is empty or unreceived. Aborting.");
                yield return AbortRoutine(task, null, null);
                yield break;
            }

            if (actualPalletId != task.PalletId)
            {
                Debug.Log($"[ReachTruckOperator] Substitution: target {task.PalletId} buried — picking accessible {actualPalletId} from same lane.");
                task.PalletId = actualPalletId;
            }

            // 3. Resolve AND reserve the rack destination BEFORE the physical pickup.
            string palletId = task.PalletId;
            Vector2 approachXZ = new Vector2(pallet.position.x, pallet.position.z);
            string toAddress = _putawayLogic?.AssignPutawayDestination(palletId, approachXZ);
            if (string.IsNullOrEmpty(toAddress))
            {
                Debug.LogWarning($"[ReachTruckOperator] No rack destination for {palletId} (warehouse full) — leaving it in lane {door}{lane}, retrying in {NoDestinationBackoff:F0}s.");
                _blockedUntil[palletId] = Time.time + NoDestinationBackoff;
                if (task != null) task.Status = WorkTaskStatus.Available;
                Restore();
                yield break;
            }
            // NOTE: the slot is now RESERVED (LocationStatusRegistry), but we deliberately do NOT stamp
            // task.ToLocation until the pallet is physically on the forks (below). Save/load keys the
            // "resume delivery vs. restart" decision off task.ToLocation ⇔ a carried-pallet snapshot;
            // setting it before pickup would misroute a save taken in this pre-pickup window.

            // DELIBERATELY NOT re-parenting the pallet under the Inventory container here. Doing so at
            // claim time — while the pallet is still physically resting in the lane cell — made
            // TrailerOffloadController.ComputeDropBaseY (which skips any PARENTED pallet, treating a
            // parent as "on a carrier") measure the cell as empty, so the next dock stocker dropped its
            // pallet on top of this one at ground Y (two pallets in the same cell on the deepest lane
            // slot). The pallet now stays unparented while it rests, and only becomes a child of the
            // Inventory container once it is actually placed at the rack (see the completion leg in
            // DeliverPalletToRack). Between claim and pickup it is parented to the carrier at seating.

            // ── PHASE: AI travel → staging-lane exit ──────────────────────────────────────────────
            // Open-floor leg (truck's current position, e.g. mid-patrol anywhere in the warehouse →
            // this lane's exit point). Real NavMeshAgent travel, so racks with a carving
            // NavMeshObstacle are respected. Everything after this until we're back out of the lane
            // is Manual (precision) work.
            bool reachedLane = false;
            yield return SeekViaNavMesh(exitPoint, $"putaway: → lane {door}{lane} exit", r => reachedLane = r);
            if (!reachedLane)
            {
                // No path — do NOT plow through. Release the reserved slot and re-queue the task.
                _putawayLogic?.CancelPutaway(toAddress);
                _blockedUntil[palletId] = Time.time + NoDestinationBackoff;
                yield return AbortRoutine(task, null, null);
                yield break;
            }

            Transform anchorFront = FindDeepChild(pallet, ChepAnchorFrontName);
            Transform anchorRear  = FindDeepChild(pallet, ChepAnchorRearName);
            Transform exitAnchor  = PickExitFacingAnchor(anchorFront, anchorRear, geo.DepthAxis);

            if (exitAnchor == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Pallet {palletId} missing anchors.");
                _putawayLogic?.CancelPutaway(toAddress);
                yield return AbortRoutine(task, palletId, toAddress);
                yield break;
            }

            // Capture the pallet's original resting pose so ANY post-pickup abort restores it exactly
            // rather than stranding it wherever the forks are (belt-and-suspenders vs. the stairwell).
            // Mirrored onto fields so the rack-delivery legs (DeliverPalletToRack) can restore it too.
            Vector3    originalPalletPos = pallet.position;
            Quaternion originalPalletRot = pallet.rotation;
            _carryOriginPos   = originalPalletPos;
            _carryOriginRot   = originalPalletRot;
            _carryOriginValid = true;

            // ── Closed-loop acquire (chassis aligns with ChepAnchorFront -- forks alone extend) ───
            // The CHASSIS drives to align its transform with the pallet's exit-facing ChepAnchorFront
            // (z = 2.5), then stops. From that parked pose the FORKS ALONE extend forward until the
            // pallet's PalletAnchor is within ForkGrabProximity (0.15m) of the truck's PalletAnchor.
            // If grab fails, back off and retry; if it truly can't seat after retries, abort cleanly.
            const int MaxGrabAttempts = 3;
            bool grabbed = false;

            for (int attempt = 0; attempt < MaxGrabAttempts && !grabbed; attempt++)
            {
                Debug.Log($"[ReachTruckOperator] Grab attempt {attempt + 1}/{MaxGrabAttempts} starting for pallet {palletId}");
                // Re-measure every attempt to ensure accurate positioning for retry
                Vector3 outward = Flat(exitPoint - pallet.position);

                // 1. Park the chassis so its transform aligns with the pallet's exit-facing anchor
                //    (ChepAnchorFront at z=2.5 or ChepAnchorRear at z=-2.5, depending on entry direction)
                Debug.Log($"[ReachTruckOperator] Driving to exitAnchor: {exitAnchor.position}");
                yield return DriveToPoint(transform, exitAnchor.position);

                // 2. Square up: forks point straight into the lane, down the pallet's centre line.
                Debug.Log($"[ReachTruckOperator] Facing forks toward: {-outward}");
                yield return FaceForks(transform, -outward);

                // 3. Lift to the pallet's fork-pocket height (correct even for a top-of-stack pallet).
                if (_forks != null)
                {
                    float targetY = pallet.position.y + PalletHalfHeight;
                    Debug.Log($"[ReachTruckOperator] Lifting forks to Y={targetY}");
                    yield return LiftForksToWorldY(_forks, targetY);
                }
                else
                {
                    Debug.LogWarning($"[ReachTruckOperator] Forks are NULL!");
                }

                // 4. Extend the forks alone the rest of the way in. Reports whether the pallet's
                //    PalletAnchor actually reached the truck's PalletAnchor within ForkGrabProximity.
                Debug.Log($"[ReachTruckOperator] Starting InsertToGrab for {palletId}");
                bool ok = false;
                yield return InsertToGrab(pallet, r => ok = r);
                grabbed = ok;
                Debug.Log($"[ReachTruckOperator] InsertToGrab result: {grabbed}");

                if (!grabbed)
                {
                    Debug.LogWarning($"[ReachTruckOperator] Grab attempt {attempt + 1}/{MaxGrabAttempts} for {palletId} missed proximity. Backing off to re-measure.");
                    yield return ReverseToPoint(transform, exitAnchor.position);
                }
            }

            if (!grabbed)
            {
                Debug.LogWarning($"[ReachTruckOperator] Could not seat {palletId} after {MaxGrabAttempts} attempts — restoring it and aborting.");
                ReleaseCarriedPalletToOrigin(pallet);
                _putawayLogic?.CancelPutaway(toAddress);
                yield return AbortRoutine(task, palletId, toAddress);
                yield break;
            }

            Transform carrier = _palletAnchor != null ? _palletAnchor : (_forks != null ? _forks : transform);

            // RULE: Parent the pallet to the PalletAnchor.
            // Snapping to local offset ensures the pallet is perfectly centered on the forks' intended carry point,
            // compensating for the model's visual blade offset and the desired 0.085m fork-pocket height.
            pallet.SetParent(carrier, worldPositionStays: false);
            
            float anchorY = _palletAnchor != null ? _palletAnchor.localPosition.y : 0f;
            float verticalOffset = (ForkBladeHeightOffset - PalletHalfHeight) - anchorY;
            pallet.localPosition = new Vector3(0, verticalOffset, 0);
            // Keep the pallet's own resting yaw instead of snapping to the carrier's (which faces
            // INTO the lane, toward the pallet, i.e. 180° from the pallet's own forward) — that
            // mismatch is what made every picked-up pallet visibly spin 180° on Y at grab.
            pallet.rotation = originalPalletRot;

            // Disable NavMesh obstacle and modifier to stop carving
            NavMeshObstacle obstacle = pallet.GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;

            // Also disable NavMeshModifier (via reflection since it may not be in imports)
            var modifierType = System.Type.GetType("UnityEngine.AI.NavMeshModifier, Assembly-CSharp");
            if (modifierType == null)
                modifierType = System.Type.GetType("UnityEngine.AI.NavMeshModifier");
            if (modifierType != null)
            {
                var modifier = pallet.GetComponent(modifierType);
                if (modifier != null)
                {
                    modifierType.GetProperty("enabled").SetValue(modifier, false);
                }
            }

            // CRITICAL: Move the pallet to a transit location in InventoryService.
            // This removes it from the staging lane slot, allowing other RTOs to access the pallet behind it.
            _inventoryService?.MovePallet(palletId, new Vector2Int(-1, -1));

            // NOTE: nothing further is needed here. Pallets carve now (BuildingData.ConfigureObstacle),
            // and the obstacle.enabled = false above removes this pallet's carve immediately — the cell
            // becomes walkable the same frame, with no rebake and nothing to batch.
            //
            // This used to branch into DestroyObstaclesInCell() when the cell emptied, which destroyed
            // the NavMeshObstacle AND NavMeshModifier of every PlacedObject sharing that grid cell —
            // including the staging-lane FLOOR tile, permanently stripping its area = 3 ("MHE Lane")
            // override and its build settings, which never came back. The other branch queued the
            // obstacle for NavMeshRebuildQueue, which destroyed the component outright so the pallet
            // could never block again after its first pickup. Both are gone.

            // Cargo-in-transit convention (same as TruckController/MHEOperatorPersistenceService):
            // disable PlacedObject while it rides. Its gridX/gridY still hold the ORIGINAL staging-lane
            // cell — without disabling it, PalletInventoryTracker's 1s heartbeat treats that stale cell
            // as ground truth and reverts InventoryService's CurrentLocation right back to the lane the
            // instant this pallet is placed anywhere else, permanently poisoning that lane's exit slot
            // for every subsequent putaway (this was the root cause of the RTO stalling after ~2 tasks).
            PlacedObject palletPO = pallet.GetComponent<PlacedObject>();
            if (palletPO != null) palletPO.enabled = false;

            // Now that the pallet is physically seated on the forks, stamp the (already-reserved)
            // destination onto the task — this is the point where save/load should resume delivery.
            task.AssignToLocation(toAddress);

            // Retract the forks back to rest and raise to travel height before backing out -- clears
            // the load from the lane below/behind it so the reverse leg can't snag a neighbouring stack.
            if (_forks != null)
            {
                yield return RetractForks(_forks, _forkRestLocalZ);
                yield return LiftForks(_forks, ForkTravelHeight);
            }

            // 5. Leg 1 Back-out — destination was resolved + reserved before pickup (step 3).
            yield return ReverseToPoint(transform, exitPoint);

            // 7-9. Leg 2 rack travel + putdown — shared with ResumeDeliverToRack (a save/load
            // mid-carry restores the pallet already seated on the forks with toAddress already
            // known, so it re-enters here directly instead of repeating the lane pickup above).
            yield return DeliverPalletToRack(pallet, palletId, toAddress, task, obstacle);
        }

        /// <summary>
        /// Steps 7-9 of the putaway sequence: drive to the rack, extend forks to the slot, release
        /// the pallet, complete the task. Factored out so a save/load mid-carry can resume directly
        /// here (see ResumeDeliverToRack) without repeating the lane pickup (steps 1-6).
        /// </summary>
        private IEnumerator DeliverPalletToRack(Transform pallet, string palletId, string toAddress, WorkTask task, NavMeshObstacle obstacle)
        {
            // 7. Leg 2 Rack Travel ----------------------------------------------------------------
            Transform locApproach = FindLocApproachAnchor(toAddress);
            if (locApproach == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Approach anchor not found for {toAddress}.");
                ReleaseCarriedPalletToOrigin(pallet);
                if (obstacle != null) obstacle.enabled = true;
                yield return AbortRoutine(task, palletId, toAddress);
                yield break;
            }

            // ── PHASE: AI travel → target rack's approach anchor ──────────────────────────────────
            // Open-floor leg (staging-lane exit, or a reserve slot just picked from → the TARGET
            // rack's approach anchor, often a different aisle). Forks go to travel height first,
            // then real NavMeshAgent travel; back to Manual for the putdown below.
            // (No pre-rotate needed — FaceForks right after arrival sets the final orientation.)
            if (_forks != null) yield return LiftForks(_forks, ForkTravelHeight);

            bool reachedRack = false;
            yield return SeekViaNavMesh(locApproach.position, $"deliver: → location {toAddress} anchor", r => reachedRack = r);
            if (!reachedRack)
            {
                // No path — do NOT plow through. Put the pallet back where it came from and abort.
                ReleaseCarriedPalletToOrigin(pallet);
                if (obstacle != null) obstacle.enabled = true;
                yield return AbortRoutine(task, palletId, toAddress);
                yield break;
            }

            // 8. Putdown Sequence ------------------------------------------------------------------
            Transform locationTr = FindLocationTransform(toAddress);
            if (locationTr == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Location {toAddress} not found.");
                ReleaseCarriedPalletToOrigin(pallet);
                if (obstacle != null) obstacle.enabled = true;
                yield return AbortRoutine(task, palletId, toAddress);
                yield break;
            }

            // ── PRECISE FINAL ALIGNMENT (load-bearing — do not drop) ──────────────────────────────
            // The NavMesh leg above only guarantees arrival within the MHE agent's stoppingDistance
            // (1.0m) — AiNavigation's seek-arrival check fires at `remainingDistance <= 1.0`. Facing
            // and extending from up to a metre off-centre is exactly why pallets went into the bay
            // crooked and then visibly snapped square on release. So close the last metre manually,
            // squaring the chassis onto the approach anchor's exact X/Z (DriveToPoint snaps to the
            // target XZ on completion, so the residual error is zero, well inside the 0.1m ask).
            // This is a short, local move entirely within the aisle — it cannot cut through racking.
            yield return DriveToPoint(transform, locApproach.position, PrecisePlaceThreshold);

            // Only NOW rotate to aim the forks at the location, from a correctly-centred position.
            yield return FaceForks(transform, Flat(locationTr.position - transform.position));

            if (_forks != null)
                yield return LiftForksToWorldY(_forks, locationTr.position.y + ForkRackClearance);

            Debug.Log($"[ReachTruckOperator] Extending forks to {toAddress}. Target distance: {Vector3.Distance(pallet.position, locationTr.position):F2}m");

            bool varianceMet = false;
            float extended   = 0f;
            while (extended < MaxForkExtend)
            {
                if (_forks != null)
                {
                    Vector3 lp = _forks.localPosition;
                    lp.z += _forkAxisSign * _forkExtendSpeed * Time.deltaTime;
                    _forks.localPosition = lp;
                }
                extended += _forkExtendSpeed * Time.deltaTime;

                float xzDist = new Vector2(pallet.position.x - locationTr.position.x, pallet.position.z - locationTr.position.z).magnitude;
                // Tighten the drop variance to 0.05m to minimize visual "snapping" when the pallet is released.
                if (xzDist <= 0.05f) { varianceMet = true; break; }
                yield return null;
            }

            if (!varianceMet)
            {
                Debug.LogWarning($"[ReachTruckOperator] MISS! Variance {new Vector2(pallet.position.x - locationTr.position.x, pallet.position.z - locationTr.position.z).magnitude:F2}m > {_placeVariance}m. Aborting.");
                ReleaseCarriedPalletToOrigin(pallet);
                if (obstacle != null) obstacle.enabled = true;
                yield return AbortRoutine(task, palletId, toAddress);
                yield break;
            }

            // 9. Completion ------------------------------------------------------------------------
            if (_forks != null)
                yield return LiftForks(_forks, _forks.localPosition.y - ForkDepositDrop);

            pallet.SetParent(null, worldPositionStays: true);

            // Seat the pallet ON the location transform — BOTH position and rotation. Setting only
            // position left the pallet holding whatever heading it had while riding the forks (i.e.
            // the truck's heading at the moment of insertion), which is why put-away pallets sat at
            // arbitrary angles in the bay instead of square to the rack. The location transform (the
            // one authored as a child of the rack's label group) already carries the correct facing
            // for its slot, so adopting its rotation squares the pallet to the rack every time,
            // regardless of how the truck happened to be oriented on approach.
            pallet.position = locationTr.position;
            pallet.rotation = locationTr.rotation;
            // Now — and ONLY now, with the pallet physically placed at the rack and off the forks — move
            // it under the Inventory container (its organizational home). Deferring the re-parent to this
            // point (instead of at claim time) is what keeps a still-resting, claimed pallet unparented so
            // ComputeDropBaseY can see it and stack the next drop on top of it correctly.
            EnsureUnderContainer(pallet, InventoryContainerName);
            if (obstacle != null) obstacle.enabled = true;
            _carryOriginValid = false; // pallet is safely placed — the captured lane pose is no longer a fallback.

            // Record the slot's CONTENTS, not just that it's occupied. Without a real Occupy() call
            // the slot still reads "Occupied" in the Inspector (LocationRegistry mirrors status from
            // LocationStatusRegistry, which CompletePutaway marks below) but with a blank Pallet Id /
            // Sku Id / Quantity — an occupied-but-empty slot that nothing downstream can reason about.
            // So resolve the record and fail LOUDLY rather than silently writing blanks.
            var locationData = locationTr.GetComponent<LocationData>();
            var record = _inventoryService?.GetPallet(palletId);
            if (locationData == null)
            {
                Debug.LogError($"[ReachTruckOperator] '{name}': location '{toAddress}' resolved to " +
                    $"'{locationTr.name}' which has NO LocationData component — slot contents cannot be " +
                    $"recorded. It will show Occupied with empty contents.");
            }
            else if (record == null)
            {
                // The inventory record went away mid-carry (picked to empty, contaminated, or cleared
                // wholesale). By this point the pallet is already physically seated in the rack — there
                // is no backing out of the putaway here — so recover the contents from the pallet's OWN
                // PalletData instead of committing a slot that reads Occupied with a blank SKU and
                // quantity 0, which nothing downstream can reason about. PalletData is the same source
                // TrailerOffloadController.RegisterAndQueue trusts when it first registers the pallet,
                // so the recovered values agree with what the master record would have held.
                var pdata = pallet.GetComponent<PalletData>();
                if (pdata != null && !string.IsNullOrEmpty(pdata.ItemNumber))
                {
                    string pExpiry = pdata.ExpirationDay >= 0 ? pdata.ExpirationDay.ToString() : null;
                    Debug.LogWarning($"[ReachTruckOperator] '{name}': no InventoryService record for pallet " +
                        $"'{palletId}' — recovered '{toAddress}' contents from the pallet's own PalletData " +
                        $"(sku={pdata.ItemNumber} qty={pdata.CaseQuantity}).");
                    locationData.Occupy(palletId, pdata.ItemNumber, pdata.CaseQuantity, pExpiry, pdata.LoadId);
                }
                else
                {
                    Debug.LogError($"[ReachTruckOperator] '{name}': no InventoryService record AND no usable " +
                        $"PalletData for pallet '{palletId}' — leaving '{toAddress}' contents UNRECORDED " +
                        $"rather than writing a blank occupancy.");
                }
            }
            else
            {
                // ExpirationDayNumber is -1 for non-perishables; only surface a real date.
                string expiry = record.ExpirationDayNumber >= 0
                    ? record.ExpirationDayNumber.ToString()
                    : null;
                // LoadId is the human-readable "license plate" — it's what cross-references against
                // the pallet's own PalletData.LoadId in the Inspector.
                locationData.Occupy(palletId, record.SkuId, record.Quantity, expiry, record.LoadId);
                Debug.Log($"[ReachTruckOperator] '{toAddress}' contents recorded: load={record.LoadId} " +
                    $"sku={record.SkuId} qty={record.Quantity} expiry={(expiry ?? "n/a")} (palletId={palletId})");
            }

            // Resolve grid position robustly
            Vector2Int toGrid = Vector2Int.zero;
            PlacedObject rackPO = locationTr.GetComponentInParent<PlacedObject>();
            if (rackPO != null) toGrid = new Vector2Int(rackPO.gridX, rackPO.gridY);
            else if (SlotRegistry.TryGet(toAddress, out var toSlot) && toSlot.Rack != null)
                toGrid = new Vector2Int(toSlot.Rack.gridX, toSlot.Rack.gridY);

            // Re-enable the pallet's PlacedObject now that it has a real resting cell (toGrid), writing
            // that cell in FIRST so PalletInventoryTracker's next heartbeat sees it already agreeing with
            // InventoryService instead of reverting CompletePutaway's move back to the stale lane cell.
            PlacedObject palletPO = pallet.GetComponent<PlacedObject>();
            if (palletPO != null)
            {
                palletPO.gridX = toGrid.x;
                palletPO.gridY = toGrid.y;
                palletPO.enabled = true;
            }

            // Keep the pallet's OWN PalletData in step with where it now physically lives. Without
            // this its CurrentLocation stays pointing at the staging-lane cell it was received into,
            // so the hover popup / any inspector read of a racked pallet reports a stale dock
            // location forever. (TrailerOffloadController does the equivalent sync in
            // RegisterAndQueue when it stages a pallet; the putaway path was missing it.)
            // Record BOTH the grid cell and the readable slot address, so a pallet inspected in the
            // scene names the exact slot it's in ("01-01-A0") rather than only an ambiguous grid
            // cell — a whole rack bay shares one cell across both positions and every level.
            var palletData = pallet.GetComponent<PalletData>();
            if (palletData != null) palletData.SetLocation(toGrid, toAddress);

            _putawayLogic?.CompletePutaway(palletId, toAddress, toGrid);
            _workQueue?.CompleteTask(task.TaskId);
            Debug.Log($"[ReachTruckOperator] SUCCESS: {palletId} put away at {toAddress}");

            if (_forks != null)
            {
                // Slowly retract the forks to the mast for better visual fidelity (approx 0.6m/s).
                yield return RetractForks(_forks, _forkRestLocalZ, _forkExtendSpeed * 0.4f);
                yield return LiftForks(_forks, _forkRestLocalY);
            }

            Restore();
        }

        // ── Replenishment (reserve -> pick slot) ─────────────────────────────────────────────────────

        /// <summary>
        /// Physical sequence for a Replenish task: extract the FIFO-oldest reserve pallet already
        /// resolved by ReplenishmentService (task.PalletId/FromLocation/ToLocation are all known up
        /// front — no lane search, no destination resolution) and deliver it to the empty pick slot.
        /// The "pickup" leg pulls FROM a rack address (see PickupFromReserve, the mirror image of a
        /// rack putdown); the "delivery" leg reuses DeliverPalletToRack verbatim since placing a
        /// pallet at a pick-slot address is physically identical to placing it at a reserve address.
        /// </summary>
        private IEnumerator ReplenishRoutine(WorkTask task)
        {
            // NOTE: deliberately NOT Commandeer()'d here -- same reasoning as PutawayRoutine. The
            // vehicle stays on its own NavMeshAgent until PickupFromReserve's SeekViaNavMesh leg
            // hands off to manual control on arrival.
            _carryOriginValid = false;
            string reserveAddress = task.FromLocation;
            string pickAddress    = task.ToLocation;
            string palletId       = task.PalletId;

            Debug.Log($"[ReachTruckOperator] '{name}' STARTING replenish task {task.TaskId}: {palletId} {reserveAddress} -> {pickAddress}");

            var link = PalletMasterLink.Find(palletId);
            Transform pallet = link != null ? link.transform : null;

            LocationData reserveLoc = FindLocationTransform(reserveAddress)?.GetComponent<LocationData>();
            LocationData pickLoc    = FindLocationTransform(pickAddress)?.GetComponent<LocationData>();

            if (pallet == null || reserveLoc == null || pickLoc == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Replenish: missing pallet ({pallet != null}), reserveLoc ({reserveLoc != null}) or pickLoc ({pickLoc != null}). Aborting.");
                pickLoc?.Release();
                // Nothing physically moved — revert the reserve slot's lock using its own still-intact fields.
                if (reserveLoc != null) reserveLoc.Occupy(reserveLoc.PalletId, reserveLoc.SkuId, reserveLoc.Quantity, reserveLoc.ExpirationDate);
                if (task != null) task.Status = WorkTaskStatus.Available;
                Restore();
                yield break;
            }

            // Capture the pallet's resting pose for abort-safety, same convention as PutawayRoutine.
            _carryOriginPos   = pallet.position;
            _carryOriginRot   = pallet.rotation;
            _carryOriginValid = true;

            bool grabbed = false;
            yield return PickupFromReserve(reserveAddress, pallet, r => grabbed = r);

            if (!grabbed)
            {
                Debug.LogWarning($"[ReachTruckOperator] Replenish: could not extract {palletId} from {reserveAddress}. Reverting reservations.");
                pickLoc.Release();
                reserveLoc.Occupy(reserveLoc.PalletId, reserveLoc.SkuId, reserveLoc.Quantity, reserveLoc.ExpirationDate);
                if (task != null) task.Status = WorkTaskStatus.Available;
                _carryOriginValid = false;
                Restore();
                yield break;
            }

            // Extraction succeeded — the reserve slot is now physically empty. NOTE: if the delivery
            // leg below fails, the pallet drops back at this reserve position (ReleaseCarriedPalletToOrigin)
            // but this LocationData stays cleared/Available — a rare double-failure edge case (both the
            // extraction AND a static-address delivery would have to fail) accepted for now rather than
            // adding a success callback to the shared DeliverPalletToRack just to guard it.
            reserveLoc.Release();

            // Cargo-in-transit convention shared with PutawayRoutine: disable NavMeshObstacle/Modifier
            // and PlacedObject while carried, and park InventoryService at the transit sentinel until
            // delivery lands it on the pick slot's cell.
            NavMeshObstacle obstacle = pallet.GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;

            var modifierType = System.Type.GetType("UnityEngine.AI.NavMeshModifier, Assembly-CSharp")
                                ?? System.Type.GetType("UnityEngine.AI.NavMeshModifier");
            if (modifierType != null)
            {
                var modifier = pallet.GetComponent(modifierType);
                if (modifier != null) modifierType.GetProperty("enabled").SetValue(modifier, false);
            }

            _inventoryService?.MovePallet(palletId, new Vector2Int(-1, -1));

            PlacedObject palletPO = pallet.GetComponent<PlacedObject>();
            if (palletPO != null) palletPO.enabled = false;

            // Retract to rest and raise to carry/travel height before driving off to the pick slot.
            if (_forks != null)
            {
                yield return RetractForks(_forks, _forkRestLocalZ);
                yield return LiftForks(_forks, ForkTravelHeight);
            }

            // Delivery leg is physically identical to a normal putaway rack delivery.
            yield return DeliverPalletToRack(pallet, palletId, pickAddress, task, obstacle);
        }

        // ── Pallet pick (reserve -> outbound staging lane) ───────────────────────────────────────

        /// <summary>
        /// Takes one FULL pallet out of reserve and stands it in an outbound staging lane, for a bulk
        /// order that asked for full-pallet quantities.
        ///
        /// The extraction half is Replenish's, verbatim — same PickupFromReserve, same cargo-in-transit
        /// conventions. Only the delivery differs: a staging lane slot instead of a rack bay, which is
        /// the outbound mirror of what PutawayRoutine does in reverse.
        /// </summary>
        private IEnumerator PalletPickRoutine(WorkTask task)
        {
            _carryOriginValid = false;
            string reserveAddress = task.FromLocation;
            string palletId       = task.PalletId;

            if (!TryParseLaneName(task.ToLocation, out int door, out string lane))
            {
                Debug.LogWarning($"[ReachTruckOperator] PalletPick {task.TaskId}: destination '{task.ToLocation}' " +
                                 $"is not a lane. Aborting.");
                yield return AbortRoutine(task, palletId, reserveAddress);
                yield break;
            }

            Debug.Log($"[ReachTruckOperator] '{name}' STARTING pallet pick {task.TaskId}: {palletId} " +
                      $"{reserveAddress} -> {door}{lane}");

            var link = PalletMasterLink.Find(palletId);
            Transform pallet = link != null ? link.transform : null;
            LocationData reserveLoc = FindLocationTransform(reserveAddress)?.GetComponent<LocationData>();

            if (pallet == null || reserveLoc == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] PalletPick: missing pallet ({pallet != null}) or " +
                                 $"reserve location ({reserveLoc != null}). Aborting.");
                // Nothing physically moved — put the reserve slot back the way it was, using its own
                // still-intact fields, exactly as ReplenishRoutine does.
                if (reserveLoc != null)
                    reserveLoc.Occupy(reserveLoc.PalletId, reserveLoc.SkuId, reserveLoc.Quantity, reserveLoc.ExpirationDate);
                task.PalletId = null;   // re-resolved on the next claim
                if (task != null) task.Status = WorkTaskStatus.Available;
                Restore();
                yield break;
            }

            // Captured BEFORE the slot is released — once it's cleared these fields are gone, and the
            // case count is what gets credited to the order and written onto the outbound pallet.
            int palletCases = reserveLoc.Quantity;
            string skuId = task.SkuId;

            _carryOriginPos   = pallet.position;
            _carryOriginRot   = pallet.rotation;
            _carryOriginValid = true;

            bool grabbed = false;
            yield return PickupFromReserve(reserveAddress, pallet, r => grabbed = r);

            if (!grabbed)
            {
                Debug.LogWarning($"[ReachTruckOperator] PalletPick: could not extract {palletId} from " +
                                 $"{reserveAddress}. Reverting reservation.");
                reserveLoc.Occupy(reserveLoc.PalletId, reserveLoc.SkuId, reserveLoc.Quantity, reserveLoc.ExpirationDate);
                task.PalletId = null;
                task.Status = WorkTaskStatus.Available;
                _carryOriginValid = false;
                Restore();
                yield break;
            }

            reserveLoc.Release();

            // Cargo-in-transit convention shared with Putaway/Replenish.
            NavMeshObstacle obstacle = pallet.GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;

            var modifierType = System.Type.GetType("UnityEngine.AI.NavMeshModifier, Assembly-CSharp")
                                ?? System.Type.GetType("UnityEngine.AI.NavMeshModifier");
            if (modifierType != null)
            {
                var modifier = pallet.GetComponent(modifierType);
                if (modifier != null) modifierType.GetProperty("enabled").SetValue(modifier, false);
            }

            _inventoryService?.MovePallet(palletId, new Vector2Int(-1, -1));

            PlacedObject palletPO = pallet.GetComponent<PlacedObject>();
            if (palletPO != null) palletPO.enabled = false;

            if (_forks != null)
            {
                yield return RetractForks(_forks, _forkRestLocalZ);
                yield return LiftForks(_forks, ForkTravelHeight);
            }

            yield return DeliverPalletToStagingLane(pallet, palletId, skuId, palletCases, door, lane, task, obstacle);
        }

        /// <summary>
        /// Drives a carried pallet into an outbound staging lane slot and sets it down as order freight.
        ///
        /// Unlike a rack bay, a lane slot has no authored approach anchor or location transform — the
        /// geometry comes from LaneNamingService, the same source OrderSelectionTaskDriver uses when a
        /// selector delivers its built pallets. Overflow within the stage (A -> B -> C) is resolved
        /// here through InventoryService.TryFindStagingLaneForPallets rather than being forced into the
        /// lane the order was released to, since a lane can fill between release and arrival.
        /// </summary>
        private IEnumerator DeliverPalletToStagingLane(Transform pallet, string palletId, string skuId,
                                                       int palletCases, int door, string lane,
                                                       WorkTask task, NavMeshObstacle obstacle)
        {
            if (!_inventoryService.TryFindStagingLaneForPallets(door, lane, 1, out string resolvedLane) ||
                !_inventoryService.TryFindStagingSlotInLane(door, resolvedLane, out var slot) ||
                !LaneNamingService.TryGetSlotWorldPos(slot.Cell, out Vector3 slotPos))
            {
                Debug.LogWarning($"[ReachTruckOperator] PalletPick: no free staging slot in Stage {door} " +
                                 $"(started at {door}{lane}). Putting the pallet back and retrying in {NoDestinationBackoff:F0}s.");
                UIToast.Show($"Reach Truck can't deliver — Stage {door}{lane} is full.");
                _blockedUntil[palletId] = Time.time + NoDestinationBackoff;
                ReleaseCarriedPalletToOrigin(pallet);
                if (obstacle != null) obstacle.enabled = true;
                if (palletPOWasDisabled(pallet, out var po)) po.enabled = true;
                yield return AbortRoutine(task, palletId, null);
                yield break;
            }

            // Claim it NOW, not on arrival. Nothing yields between the resolve above and this line, so
            // no other delivery can interleave and be handed the same cell; from here until the pallet
            // is standing in it, TryGetNextFreeSlot/CountFreeStagingSlotsInLane skip it. Released in
            // Restore() (success) and AbortRoutine() (failure).
            _inventoryService.TryReserveStagingSlot(slot.Cell);
            _reservedStagingCell = slot.Cell;

            Vector3 depthAxis = Vector3.forward;
            bool haveGeo = LaneNamingService.TryGetLaneGeometry(door, resolvedLane, out var geo);
            if (haveGeo)
                depthAxis = geo.DepthAxis.sqrMagnitude > 0.0001f ? geo.DepthAxis.normalized : Vector3.forward;

            // Open-floor leg to the lane, entering from the LANE EXIT — the open far end — not the
            // dock end. DepthAxis points FROM the dock wall outward through the lane (slot 1 is
            // nearest the door), so the exit side of any slot is +depthAxis and the door side is
            // -depthAxis. Backing off along -depthAxis put the truck between the slot and the dock,
            // which meant driving in over the trailer/door end of the lane: through whatever is
            // already staged there, and straight across the loader's working side.
            //
            // Entering from the exit is also always the clear side: TryGetNextFreeSlot fills a lane
            // door-outward, so the free slot this pallet is going into is by construction the
            // exit-most one and nothing stands between it and the open floor.
            // Staging point is the LANE'S OWN ExitPoint (2m past the far slot) when geometry is
            // available, NOT a standoff measured off the target slot. For a slot deep in the lane
            // those are very different places: a standoff off slot 1 sits INSIDE the lane, so the
            // NavMesh leg dropped the truck partway down it and the run-in started from a diagonal.
            // Squaring up at the mouth means the whole insertion is one straight push along the lane
            // axis however deep the slot is.
            Vector3 laneInward = -depthAxis;                       // forks point toward the dock
            Vector3 approach = haveGeo
                ? geo.ExitPoint
                : slotPos + depthAxis * LaneApproachStandoff;
            bool reachedLane = false;
            yield return SeekViaNavMesh(approach, $"pallet pick: → lane {door}{resolvedLane}", r => reachedLane = r);
            if (!reachedLane)
            {
                Debug.LogWarning($"[ReachTruckOperator] PalletPick: no path to lane {door}{resolvedLane}. " +
                                 $"Putting the pallet back and retrying in {NoDestinationBackoff:F0}s.");
                UIToast.Show($"Reach Truck can't reach Stage {door}{resolvedLane} — putting the pallet back.");
                _blockedUntil[palletId] = Time.time + NoDestinationBackoff;
                ReleaseCarriedPalletToOrigin(pallet);
                if (obstacle != null) obstacle.enabled = true;
                if (palletPOWasDisabled(pallet, out var po2)) po2.enabled = true;
                yield return AbortRoutine(task, palletId, null);
                yield break;
            }

            // ── Wait for exclusive lane entry ─────────────────────────────────────────────────
            // Truck is parked at the lane mouth now — outside the slot area, on the open dock floor.
            // If another delivery is currently mid-insertion into this SAME lane, its pallet may still
            // be on forks (invisible to StagingDropBaseY — see InventoryService.TryEnterLaneForDelivery)
            // so entering now would read the cell as it looked before that pallet landed and compute
            // the same drop height. Sit right here and wait, exactly like a real truck queuing at a
            // busy aisle, until the lane is free — then check the height fresh and deliver correctly.
            while (!_inventoryService.TryEnterLaneForDelivery(door, resolvedLane))
                yield return null;
            _heldLaneLock = (door, resolvedLane);

            // Close the last stretch manually and square up on the slot, same reasoning as the rack
            // delivery: the NavMesh leg only guarantees arrival within the agent's stopping distance,
            // and setting a pallet down from a metre off-centre is what puts it across two cells.
            yield return DriveToPoint(transform, approach, PrecisePlaceThreshold);
            yield return FaceForks(transform, laneInward);

            // Where this pallet will actually come to rest: the lane floor if the cell is empty, or the
            // measured top of whatever is already stacked there. Resolved HERE, once the truck is at the
            // lane mouth, so it reflects the cell as it is now — and reused for both the lift and the
            // set-down so the mast height and the final resting height can never disagree.
            float dropBaseY = TrailerOffloadController.StagingDropBaseY(door, resolvedLane, slot.Cell,
                                                                       pallet != null ? pallet.gameObject : null);

            if (_forks != null)
                yield return LiftForksToWorldY(_forks, dropBaseY + ForkRackClearance);

            // Down the lane FORKS FIRST — DriveForksFirst, never DriveToPoint. DriveToPoint opens with
            // RotateTo, which aims the BODY at the target and so instantly throws away the fork
            // heading FaceForks just set: the truck spun round at the lane mouth and reversed in
            // chassis-first. DriveForksFirst travels along the fork axis instead, steering gently to
            // hold the lane centreline, so the pallet leads the way in exactly as it does when the
            // truck presents at a rack bay.
            Vector3 setDown = slotPos + depthAxis * ForkSetDownStandoff;
            // Travel cap sized to THIS run rather than the default 8m: from the lane mouth to slot 1
            // of a deep lane is longer than any in-aisle insertion, and the default would have cut
            // the push short and left the pallet standing in the wrong slot.
            float insertCap = PlanarDist(transform.position, setDown) + 2f;
            yield return DriveForksFirst(transform, setDown, insertCap);

            if (_forks != null)
                yield return LiftForks(_forks, _forks.localPosition.y - ForkDepositDrop);

            // ── Set down as OUTBOUND FREIGHT ─────────────────────────────────────────────────────
            //
            // Left UNPARENTED at the world root, deliberately — do NOT EnsureUnderContainer this the
            // way a rack putaway does. Both systems that find staged freight skip any pallet with a
            // parent (InventoryService.OutboundOccupiedCells and
            // TrailerLoadController.FindStagedPalletsInLane both guard on `transform.parent != null`,
            // because a parented pallet is one still riding a selector or a set of forks). Filing it
            // under the Inventory container made the pallet invisible to both: it stood in the lane
            // looking perfectly correct, the cell still read as free, and no loader would ever take
            // it. OrderSelectionTaskDriver leaves its staged pallets unparented for the same reason.
            pallet.SetParent(null, worldPositionStays: true);
            // XZ from the slot, Y from the stack — a second pallet into the same cell lands on top of
            // the first instead of inside it.
            pallet.position = new Vector3(slotPos.x, dropBaseY, slotPos.z);
            pallet.rotation = Quaternion.LookRotation(depthAxis, Vector3.up);
            _carryOriginValid = false;

            var palletPO = pallet.GetComponent<PlacedObject>();
            if (palletPO != null) palletPO.enabled = true;

            // THE load-bearing step. TrailerLoadController.FindStagedPalletsInLane and
            // InventoryService.OutboundOccupiedCells both find staged freight by scanning for this
            // component and nothing else — without it the pallet stands in the lane looking perfectly
            // correct, never gets loaded, and the next order stages a pallet straight through it.
            var outbound = pallet.GetComponent<OutboundPalletBuilder>() ?? pallet.gameObject.AddComponent<OutboundPalletBuilder>();
            outbound.AdoptFullPallet(task.OrderId, palletCases);
            outbound.SetNavObstacleActive(true);
            if (obstacle != null) obstacle.enabled = true;

            // ORDER MATTERS. CompleteTask first, THEN drop the pallet from inventory: WorkQueueSystem
            // subscribes to InventoryService.OnPalletDestroyed and cancels every unfinished task for a
            // removed pallet — reversing these two lines makes this routine cancel its own task on the
            // last line of a successful run.
            _workQueue.CompleteTask(task.TaskId);
            _inventoryService?.DestroyPallet(palletId);

            // Credit the order last, once the freight is genuinely standing in the lane. This is what
            // flips a bulk order to Staged when its final pallet lands.
            if (ServiceLocator.TryGet<OrderService>(out var orderService) && orderService != null)
                orderService.NotePalletPicked(task.OrderId, skuId, palletCases);

            Debug.Log($"[ReachTruckOperator] '{name}' pallet pick complete: {palletCases} cs of {skuId} " +
                      $"staged at {door}{resolvedLane}-{slot.Slot} for order {task.OrderId}.");

            Restore();
        }

        /// <summary>How far outside a staging slot to stop before setting a pallet down. Two separate
        /// standoffs because the truck approaches from clear of the lane and then noses in.</summary>
        private const float LaneApproachStandoff = 3.0f;
        private const float ForkSetDownStandoff  = 1.2f;

        /// <summary>Small helper for the abort paths: a carried pallet had its PlacedObject switched
        /// off at pickup, and every path that puts it back down has to switch it on again.</summary>
        private static bool palletPOWasDisabled(Transform pallet, out PlacedObject po)
        {
            po = pallet != null ? pallet.GetComponent<PlacedObject>() : null;
            return po != null && !po.enabled;
        }

        /// <summary>
        /// Extracts a pallet already resting at a reserve rack address: drives to the slot's approach
        /// anchor, faces it, lifts forks to the pallet's fork-pocket height, extends forks in until
        /// the truck's PalletAnchor is within ForkGrabProximity of the pallet, parents the pallet to
        /// the forks, lifts it clear of the shelf, and reports success. On failure the forks are
        /// retracted and the pallet is left untouched at the reserve slot.
        /// </summary>
        private IEnumerator PickupFromReserve(string reserveAddress, Transform pallet, System.Action<bool> onDone)
        {
            if (_forks == null || _palletAnchor == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Replenish: missing forks or palletAnchor.");
                onDone?.Invoke(false);
                yield break;
            }

            Transform locApproach = FindLocApproachAnchor(reserveAddress);
            if (locApproach == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Replenish: approach anchor not found for {reserveAddress}.");
                onDone?.Invoke(false);
                yield break;
            }

            // ── PHASE: AI travel → reserve rack's approach anchor ─────────────────────────────────
            // Open-floor leg (truck's current position → the RESERVE rack's approach anchor). Real
            // NavMeshAgent travel; back to Manual for the extract below. On failure, report false to
            // the caller (ReplenishRoutine reverts both slot reservations) rather than straight-lining.
            yield return LiftForks(_forks, ForkTravelHeight);

            bool reachedReserve = false;
            yield return SeekViaNavMesh(locApproach.position, $"replenish: → reserve {reserveAddress} anchor", r => reachedReserve = r);
            if (!reachedReserve)
            {
                onDone?.Invoke(false);
                yield break;
            }

            Transform locationTr = FindLocationTransform(reserveAddress);
            if (locationTr == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Replenish: location transform not found for {reserveAddress}.");
                onDone?.Invoke(false);
                yield break;
            }

            // Same precise final alignment as the delivery leg — the NavMesh arrival is only good to
            // the MHE stoppingDistance (1.0m), and extracting from a metre off-centre is what makes
            // the fork insertion miss and the pallet come out skewed.
            yield return DriveToPoint(transform, locApproach.position, PrecisePlaceThreshold);

            yield return FaceForks(transform, Flat(locationTr.position - transform.position));
            // PalletHalfHeight (0.085), not ForkRackClearance (0.15) -- ForkRackClearance exactly
            // equals ForkGrabProximity below, so lifting the full clearance amount here left zero
            // margin for any XZ residual and made the grab fail almost every time. ForkRackClearance
            // is still used for the post-grab lift-clear a few lines down.
            yield return LiftForksToWorldY(_forks, locationTr.position.y + PalletHalfHeight);

            _forks.localPosition = new Vector3(_forks.localPosition.x, _forks.localPosition.y, _forkRestLocalZ);

            bool got = false;
            float extended = 0f;
            while (extended < MaxForkExtend)
            {
                float dist = Vector3.Distance(pallet.position, _palletAnchor.position);
                if (dist <= ForkGrabProximity) { got = true; break; }

                Vector3 lp = _forks.localPosition;
                lp.z += _forkAxisSign * _forkExtendSpeed * Time.deltaTime;
                _forks.localPosition = lp;
                extended += _forkExtendSpeed * Time.deltaTime;
                yield return null;
            }

            if (!got)
            {
                Debug.LogWarning($"[ReachTruckOperator] Replenish: could not reach pallet at {reserveAddress} within {ForkGrabProximity}m.");
                _forks.localPosition = new Vector3(_forks.localPosition.x, _forks.localPosition.y, _forkRestLocalZ);
                onDone?.Invoke(false);
                yield break;
            }

            // Seat the pallet on the forks (same snap-to-carry-pose used by the lane pickup).
            // Captured BEFORE SetParent — the pallet hasn't moved since resting on the shelf, so
            // this is its resting yaw, preserved through the reparent instead of snapping to the
            // carrier's (which faces INTO the rack, toward the pallet, i.e. 180° off), which is
            // what made every extracted pallet visibly spin 180° on Y at grab.
            Quaternion restingRot = pallet.rotation;
            pallet.SetParent(_palletAnchor, worldPositionStays: false);
            float verticalOffset = (ForkBladeHeightOffset - PalletHalfHeight) - _palletAnchor.localPosition.y;
            pallet.localPosition = new Vector3(0, verticalOffset, 0);
            pallet.rotation = restingRot;

            // Lift a little to clear the shelf lip before the caller retracts.
            yield return LiftForks(_forks, _forks.localPosition.y + ForkRackClearance);

            onDone?.Invoke(true);
        }

        // ── Save/load resume entry points ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by MHEOperatorPersistenceService right after a save/load restores this operator's
        /// vehicle and re-seats a pallet on its forks/anchor mid-putaway. The task's ToLocation was
        /// already resolved and saved BEFORE the pallet was picked up (see PutawayRoutine step 5),
        /// so this skips straight to the rack-delivery leg instead of re-deriving anything.
        /// </summary>
        public void ResumeDeliverToRack(WorkTask task, Transform pallet)
        {
            if (task == null || pallet == null || string.IsNullOrEmpty(task.ToLocation)) return;
            ResolveServices();

            // Save/load restored this vehicle mid-carry with the pallet already on its forks. Start
            // in Manual so nothing moves until DeliverPalletToRack's own AI leg takes over.
            SetDriveMode(DriveMode.Manual, "resume mid-carry after save/load");
            _busy = true;
            NavMeshObstacle obstacle = pallet.GetComponent<NavMeshObstacle>();
            Debug.Log($"[ReachTruckOperator] '{name}' RESUMING task {task.TaskId} — delivering '{task.PalletId}' to {task.ToLocation}.");
            StartCoroutine(DeliverPalletToRack(pallet, task.PalletId, task.ToLocation, task, obstacle));
        }

        /// <summary>
        /// Called by MHEOperatorPersistenceService when this operator has an Assigned task but
        /// hadn't picked up the pallet yet at save time (ToLocation still null) — nothing physical
        /// happened, so the full routine re-derives everything from the lane exactly like a fresh
        /// claim would.
        /// </summary>
        public void ResumeTask(WorkTask task)
        {
            if (task == null) return;
            ResolveServices();

            _busy = true;
            StartCoroutine(PutawayRoutine(task));
        }

        private void ResolveServices()
        {
            if (_workQueue        == null) ServiceLocator.TryGet(out _workQueue);
            if (_putawayLogic     == null) ServiceLocator.TryGet(out _putawayLogic);
            if (_inventoryService == null) ServiceLocator.TryGet(out _inventoryService);
        }

        /// <summary>True while <paramref name="palletId"/> is in its post-"no rack space" backoff and
        /// should not be re-claimed yet. Expired entries are cleared on read.</summary>
        private bool IsPalletBlocked(string palletId)
        {
            if (string.IsNullOrEmpty(palletId)) return false;
            if (_blockedUntil.TryGetValue(palletId, out float until))
            {
                if (Time.time < until) return true;
                _blockedUntil.Remove(palletId);
            }
            return false;
        }

        /// <summary>
        /// Un-parents the carried pallet and restores it to the resting pose captured at pickup
        /// (<see cref="_carryOriginPos"/>/<see cref="_carryOriginRot"/>). When no valid origin was
        /// captured — e.g. a save/load resume that re-seated the pallet mid-carry — it falls back to
        /// leaving the pallet at its current world pose. This is what prevents a failed rack-delivery
        /// abort from stranding the pallet at the forks' elevated carry height, which would otherwise
        /// ratchet it higher (and further out) on every subsequent re-pick attempt.
        /// </summary>
        private void ReleaseCarriedPalletToOrigin(Transform pallet)
        {
            if (pallet == null) return;
            pallet.SetParent(null, worldPositionStays: true);
            if (_carryOriginValid)
            {
                pallet.position = _carryOriginPos;
                pallet.rotation = _carryOriginRot;
            }
        }

        private IEnumerator AbortRoutine(WorkTask task, string palletId, string reservedAddress)
        {
            Debug.Log($"[ReachTruckOperator] ABORTING task. Pallet: {palletId}, Location: {reservedAddress}");

            // Staging cells are reserved the same way rack addresses are (below) — give ours back so an
            // aborted run doesn't strand a slot nothing can ever stage into again.
            ReleaseStagingReservation();
            ReleaseLaneLock();

            // Release the reserved destination. Prefer the LocationData instance (keeps its own
            // serialized _status field in sync) over PutawayLogic.CancelPutaway, which only touches
            // the separate LocationStatusRegistry dictionary and would otherwise leave the component's
            // local field stuck on Reserved forever (a stranded slot ReplenishmentService/PutawayLogic
            // would never see as Available again).
            if (!string.IsNullOrEmpty(reservedAddress))
            {
                var locData = FindLocationTransform(reservedAddress)?.GetComponent<LocationData>();
                if (locData != null) locData.Release();
                else _putawayLogic?.CancelPutaway(reservedAddress);
            }
            if (task != null) task.Status = WorkTaskStatus.Available;

            // If holding a pallet, retract and re-enable obstacle
            if (_forks != null)
            {
                yield return RetractForks(_forks, _forkRestLocalZ, -1f);
                yield return LiftForks(_forks, _forkRestLocalY);
            }

            if (!string.IsNullOrEmpty(palletId))
            {
                var link = PalletMasterLink.Find(palletId);
                if (link != null)
                {
                    if (link.TryGetComponent<NavMeshObstacle>(out var obstacle))
                        obstacle.enabled = true;

                    // Defensive re-enable for every abort path: the pallet was disabled at pickup (see
                    // PutawayRoutine) and still holds its ORIGINAL staging-lane gridX/gridY, so re-enabling
                    // here lets PalletInventoryTracker's heartbeat resync InventoryService's CurrentLocation
                    // back to that lane cell instead of leaving the pallet stuck at the transit sentinel
                    // (-1,-1) forever. Idempotent no-op if a caller already re-enabled it.
                    if (link.TryGetComponent<PlacedObject>(out var po))
                        po.enabled = true;
                }
            }

            Restore();
            _carryOriginValid = false; // routine is over — don't let a stale carry pose leak into the next task.
        }

        // ── Drive-mode chokepoint ─────────────────────────────────────────────────────────────────
        //
        // WHO IS DRIVING THIS VEHICLE is explicit state, and SetDriveMode is the ONLY code allowed
        // to flip it. Nothing else in this file may touch _vehicleNav.enabled / _vehicleAgent.enabled.
        //
        // Why this matters: AiNavigation.LateUpdate() writes transform.position = agent.nextPosition
        // EVERY FRAME while its agent is enabled, and AiNavigation.Update() can re-issue a patrol
        // destination on its own (GoToRandomWaypoint on a NavMesh rebake, the waypoint-progression
        // fallback, the stuck-detector). So if the agent is live during a manual/precision step, the
        // two systems fight over the transform frame-by-frame; if BOTH are off when a leg expects to
        // move, the vehicle just sits there. Routing every transition through one method makes both
        // states impossible to reach by accident, and logs the phase so a misbehaving run can be read
        // straight from the console instead of guessed at.
        private enum DriveMode
        {
            /// <summary>The vehicle's own NavMeshAgent/AiNavigation owns the transform — real
            /// pathfinding, obstacle avoidance, NavMeshObstacle-aware (rack carving).</summary>
            Ai,
            /// <summary>This controller owns the transform directly (DriveToPoint/FaceForks/
            /// InsertToGrab/fork extension). Precision work only, always short and local.</summary>
            Manual
        }

        private DriveMode _mode = DriveMode.Ai;

        /// <summary>The ONLY method permitted to enable/disable _vehicleNav / _vehicleAgent.
        /// <paramref name="phase"/> is a short human label for the console trail.</summary>
        private void SetDriveMode(DriveMode mode, string phase)
        {
            // Deliberately NOT early-returning when _mode already equals `mode`: external code also
            // toggles these components (MHEOperatorSlot boarding/vacating via AiNavigation.GoActive/
            // GoIdle), so _mode can drift out of sync with the components' real enabled state. If we
            // skipped the work on a "no change", a vehicle whose agent was disabled behind our back
            // would silently never move on its next AI leg. Always reassert; only the log is gated.
            bool changed = _mode != mode;
            _mode = mode;

            if (mode == DriveMode.Manual)
            {
                if (_vehicleNav != null) { _vehicleNav.CancelSeekPosition(); _vehicleNav.enabled = false; }
                if (_vehicleAgent != null)
                {
                    if (_vehicleAgent.isActiveAndEnabled && _vehicleAgent.isOnNavMesh) _vehicleAgent.isStopped = true;
                    _vehicleAgent.enabled = false;
                }
            }
            else
            {
                if (_vehicleAgent != null)
                {
                    _vehicleAgent.enabled = true;

                    // ALWAYS Warp — this is a hard resync, not an optimization (do not re-add an
                    // isOnNavMesh guard around it). The agent runs with updatePosition = false for
                    // this project's whole lifetime (see AiNavigation.Awake), so its INTERNAL
                    // simulated position only tracks the transform when something syncs them. Manual
                    // phases here write transform.position directly and never touch nextPosition, so
                    // by the time we hand back to AI the agent still believes it is wherever it was
                    // when we last took over. Observed live: a truck physically 28m from its target
                    // reported remainingDistance = 1.47 and drove AWAY from it, because
                    // AiNavigation.LateUpdate was faithfully dragging the transform to a stale
                    // simulation. The old code guarded this Warp on isOnNavMesh, which skipped it in
                    // exactly the common case (agent already on-mesh), so the resync never happened.
                    _vehicleAgent.Warp(transform.position);
                    if (_vehicleAgent.isActiveAndEnabled && _vehicleAgent.isOnNavMesh)
                        _vehicleAgent.isStopped = false;
                }
                if (_vehicleNav != null) _vehicleNav.enabled = true;
            }

            if (changed)
                Debug.Log($"[ReachTruckOperator] '{name}' drive mode → {mode} ({phase}) @ {transform.position}");
        }

        /// <summary>Hands the vehicle back to its normal patrol AI and ends the task (frees _busy).</summary>
        /// <summary>Staging cell this truck has claimed while driving a pallet to it, if any. See
        /// InventoryService.TryReserveStagingSlot — held from the moment the slot is resolved until the
        /// pallet is actually standing in it (or the run aborts), so a second delivery resolving during
        /// the drive is handed the NEXT slot instead of this one.</summary>
        private Vector2Int? _reservedStagingCell;

        /// <summary>Hands back <see cref="_reservedStagingCell"/> if one is held. Called from both
        /// choke points every path out of a delivery passes through — Restore() on success and
        /// AbortRoutine() on failure — because a leaked reservation blocks that slot permanently.</summary>
        private void ReleaseStagingReservation()
        {
            if (_reservedStagingCell == null) return;
            _inventoryService?.ReleaseStagingSlot(_reservedStagingCell.Value);
            _reservedStagingCell = null;
        }

        /// <summary>Lane this truck currently holds exclusive physical-entry rights to, if any. See
        /// InventoryService.TryEnterLaneForDelivery — held from the moment the truck is let in at the
        /// lane mouth until the pallet is standing on the ground (or the run aborts before entering),
        /// so a second delivery arriving mid-insertion waits instead of racing the height check.</summary>
        private (int Door, string Lane)? _heldLaneLock;

        /// <summary>Hands back <see cref="_heldLaneLock"/> if one is held. Same two choke points as
        /// ReleaseStagingReservation, for the same reason: skip either and the lane is stuck locked for
        /// the rest of the session.</summary>
        private void ReleaseLaneLock()
        {
            if (_heldLaneLock == null) return;
            _inventoryService?.ReleaseLaneEntry(_heldLaneLock.Value.Door, _heldLaneLock.Value.Lane);
            _heldLaneLock = null;
        }

        private void Restore()
        {
            ReleaseStagingReservation();
            ReleaseLaneLock();
            SetDriveMode(DriveMode.Ai, "task complete — resuming patrol");
            _vehicleNav?.GoToRandomWaypoint();
            _busy = false;
        }

        private IEnumerator DriveToPoint(Transform t, Vector3 target, float arriveThreshold = -1f)
        {
            float threshold = arriveThreshold > 0f ? arriveThreshold : ArriveThreshold;
            Vector3 flat = new Vector3(target.x, t.position.y, target.z);
            Vector3 to = flat - t.position; to.y = 0f;
            if (to.magnitude <= threshold) { t.position = flat; yield break; }
            yield return RotateTo(t, to);
            while (PlanarDist(t.position, target) > threshold)
            {
                t.position = Vector3.MoveTowards(t.position, flat, DriveSpeed * Time.deltaTime);
                yield return null;
            }
            t.position = flat;
        }

        /// <summary>
        /// AI leg of the hybrid drive: travel from wherever the vehicle is to <paramref name="target"/>
        /// (a staging-lane exit, or a rack location's approach anchor) under the vehicle's OWN
        /// NavMeshAgent, via <see cref="AiNavigation.SeekPosition"/> -- real pathfinding, agent
        /// avoidance, and NavMeshObstacle-awareness (so a hand-authored Carve=true obstacle on a rack
        /// is actually respected). Leaves the vehicle in <see cref="DriveMode.Manual"/> on return so
        /// the caller's next precision step owns the transform.
        ///
        /// CRITICAL -- there is deliberately NO straight-line fallback on failure. An earlier version
        /// fell back to DriveToPoint when the NavMesh leg failed or timed out, which silently drove
        /// the vehicle straight THROUGH whatever was in the way (racks included) and looked exactly
        /// like "the obstacle is being ignored." A pathing failure is now a hard, loud failure: this
        /// returns false, the caller aborts/re-queues the task, and the console says which phase and
        /// which target broke. A stuck truck with a clear log line is strictly better than a truck
        /// ghosting through the racking.
        /// </summary>
        /// <returns>True if the vehicle actually arrived; false if it could not path there.</returns>
        private IEnumerator SeekViaNavMesh(Vector3 target, string phase, System.Action<bool> onDone)
        {
            if (_vehicleNav == null || _vehicleAgent == null)
            {
                Debug.LogError($"[ReachTruckOperator] '{name}' {phase}: no AiNavigation/NavMeshAgent on this vehicle — cannot do an AI travel leg.");
                onDone?.Invoke(false);
                yield break;
            }

            SetDriveMode(DriveMode.Ai, $"AI leg → {phase}");

            // ── WAIT FOR THE AGENT TO REGISTER WITH THE NAVMESH (load-bearing — do not remove) ────
            // SetDriveMode just re-enabled the NavMeshAgent. A freshly-enabled agent is NOT on the
            // navmesh in the same frame — isOnNavMesh stays false until Unity's navigation update
            // runs. That mattered enormously: AiNavigation.SeekPosition sets _seekingTask = true and
            // THEN calls SetDestinationSnapped, which opens with
            // `if (!agent.isOnNavMesh) return false;` — so the destination was silently never set
            // while _seekingTask stayed true, and this method's wait loop then burned its entire 30s
            // timeout waiting for an arrival that had never actually been requested. That is the
            // "AI thing times out" Tad kept seeing (Editor.log: agentOnMesh=False on every failure,
            // even though the same vehicles sample valid MHE navmesh under them a moment later).
            const float MaxRegisterWait = 1f;
            float registerWaited = 0f;
            while (!_vehicleAgent.isOnNavMesh && registerWaited < MaxRegisterWait)
            {
                // Warp actively places the agent on the nearest navmesh; retry it as we wait rather
                // than only trying once on a not-yet-registered agent (where it can no-op).
                _vehicleAgent.Warp(transform.position);
                registerWaited += Time.deltaTime;
                yield return null;
            }

            if (!_vehicleAgent.isOnNavMesh)
            {
                Debug.LogError($"[ReachTruckOperator] '{name}' {phase}: agent could not register on the NavMesh " +
                    $"at {transform.position} after {registerWaited:F2}s (agentType=MHE). No AI leg possible — aborting. " +
                    $"Is there MHE-type navmesh under the vehicle?");
                _vehicleNav.SetTaskBusy(false);
                SetDriveMode(DriveMode.Manual, $"failed to register → {phase}");
                onDone?.Invoke(false);
                yield break;
            }

            // ── FLATTEN THE TARGET TO DRIVE HEIGHT (load-bearing — do not remove) ──────────────────
            // A rack location's approach anchor sits at ITS OWN LEVEL's height: an upper-level slot's
            // anchor is metres up in the air on the shelf (Editor.log showed real targets at Y=5.11
            // and Y=7.11 while the floor is Y≈1.1). A forklift drives on the FLOOR, so handing that
            // raw 3D point to SeekPosition is unpathable — SetDestinationSnapped's NavMesh.SamplePosition
            // uses a 2m radius and can never reach floor navmesh 4–6m below, so the agent never
            // arrives and the leg burns its full 30s timeout. (The manual DriveToPoint path never hit
            // this because it silently rewrites target.y to the truck's own height; only the NavMesh
            // leg was faithful to the anchor's altitude.) Take the anchor's XZ, drop it to the
            // vehicle's drive height, then snap that to real navmesh.
            Vector3 driveTarget = new Vector3(target.x, transform.position.y, target.z);
            if (NavMesh.SamplePosition(driveTarget, out var groundHit, 4f, _vehicleAgent.areaMask))
            {
                driveTarget = groundHit.position;
            }
            else
            {
                Debug.LogWarning($"[ReachTruckOperator] '{name}' {phase}: no navmesh within 4m of the " +
                    $"floor-projected target {driveTarget} (anchor was {target}) — pathing to it raw.");
            }

            // Clear any stale seek before starting a new one. AiNavigation.SeekPosition opens with
            // `if (_seekingTask) return;` — it SILENTLY does nothing when a previous seek is still
            // flagged: no destination set, no callback wired. This loop would then see IsSeekingTask
            // still true with arrived never firing and burn the entire 30s timeout for a target it
            // never even attempted. CancelSeekPosition is a cheap no-op when nothing is in flight.
            _vehicleNav.CancelSeekPosition();

            // Final hard resync immediately before issuing the destination, so the path is computed
            // from where the vehicle ACTUALLY is rather than from a stale internal simulation (see
            // the long note in SetDriveMode). Cheap, and it makes the leg independent of whatever
            // sequence of manual/AI phases ran before it.
            _vehicleAgent.Warp(transform.position);

            // ── CLAIM THE AGENT FOR THIS LEG (load-bearing — do not remove) ───────────────────────
            // AiNavigation.Update() has a waypoint-progression fallback for agents with no
            // AgentAnimation — which is every Forklift-role vehicle — that fires
            // GoToRandomWaypoint() as soon as `remainingDistance <= stoppingDistance + 0.1`. MHE
            // stoppingDistance is 1.0, so that trips at 1.1, while the seek-arrival check in the
            // same Update() requires `<= 1.0`. Any leg that lands in that 1.0–1.1 window gets its
            // destination HIJACKED to a random patrol waypoint before arrival ever registers:
            // _seekingTask stays true, the callback never fires, this loop times out at 30s, and the
            // truck ends up somewhere random (Editor.log showed exactly that — failed lane-exit legs
            // ending 30m+ away at arbitrary positions). SetTaskBusy is the public guard AiNavigation
            // provides for precisely this ("so the generic waypoint-progression fallback doesn't send
            // it off to a random waypoint"). Cleared in every exit path below.
            _vehicleNav.SetTaskBusy(true);

            bool arrived = false;
            _vehicleNav.SeekPosition(driveTarget, () => arrived = true);

            // Confirm the destination actually took. SetDestinationSnapped can still return false
            // (target not sampleable, agent knocked off-mesh) while SeekPosition has already latched
            // _seekingTask = true — which would otherwise hang this leg for the full 30s on a path
            // that was never issued. One frame for the path request to register, then verify.
            yield return null;
            if (!arrived && !_vehicleAgent.pathPending && !_vehicleAgent.hasPath)
            {
                Debug.LogError($"[ReachTruckOperator] '{name}' {phase}: destination did not take — " +
                    $"no path and none pending for driveTarget={driveTarget} (anchor={target}, " +
                    $"from={transform.position}, onMesh={_vehicleAgent.isOnNavMesh}). Aborting leg immediately " +
                    $"instead of waiting out the timeout.");
                _vehicleNav.CancelSeekPosition();
                _vehicleNav.SetTaskBusy(false);
                SetDriveMode(DriveMode.Manual, $"no path issued → {phase}");
                onDone?.Invoke(false);
                yield break;
            }

            // ── RESILIENT WAIT (load-bearing — do not simplify back to a plain timer) ─────────────
            // The leg must survive a NavMesh rebake. NavMeshManager.BakeSynchronous() swaps the mesh
            // wholesale (RemoveData → navMeshData = newData → AddData), and rebakes fire on EVERY
            // placement/deletion via MarkDirty() — pallets being put away constantly retrigger it.
            // A swap knocks every agent off the mesh and silently drops its path: isOnNavMesh goes
            // false, hasPath goes false, no arrival callback ever fires, and _seekingTask stays true.
            // That is why legs were still burning the full 30s with agentOnMesh=False at the end even
            // after the registration fix — the agent DID start on-mesh with a valid path, then lost
            // both mid-travel. So re-establish rather than wait it out: warp back on when knocked
            // off, and re-issue the destination when the path is gone.
            float elapsed = 0f;
            float sinceRecheck = 0f;
            int reissues = 0;
            const float MaxSeekTime = 30f;
            const float RecheckInterval = 0.5f;
            const int MaxReissues = 10;

            while (!arrived && _vehicleNav.IsSeekingTask && elapsed < MaxSeekTime)
            {
                elapsed += Time.deltaTime;
                sinceRecheck += Time.deltaTime;

                if (sinceRecheck >= RecheckInterval)
                {
                    sinceRecheck = 0f;

                    if (!_vehicleAgent.isOnNavMesh)
                    {
                        // Knocked off by a rebake — put it back and re-issue below.
                        _vehicleAgent.Warp(transform.position);
                    }
                    else if (!_vehicleAgent.pathPending && !_vehicleAgent.hasPath && reissues < MaxReissues)
                    {
                        // On the mesh but the path evaporated (rebake dropped it). Re-issue.
                        reissues++;
                        Debug.LogWarning($"[ReachTruckOperator] '{name}' {phase}: path lost mid-leg " +
                            $"(likely a NavMesh rebake) — re-issuing destination (attempt {reissues}/{MaxReissues}).");
                        _vehicleNav.CancelSeekPosition();
                        _vehicleNav.SeekPosition(driveTarget, () => arrived = true);
                    }
                }

                yield return null;
            }

            // Release the agent claim and hand the transform back. The caller's next step is
            // precision work and must not be fighting a live agent; an aborting caller needs a
            // settled vehicle too.
            _vehicleNav.SetTaskBusy(false);
            SetDriveMode(DriveMode.Manual, arrived ? $"arrived → {phase}" : $"leg FAILED → {phase}");

            if (!arrived)
            {
                Debug.LogError($"[ReachTruckOperator] '{name}' {phase}: NO NAVMESH PATH. " +
                    $"anchor={target} → driveTarget={driveTarget}, from={transform.position}, " +
                    $"elapsed={elapsed:F1}s, timedOut={elapsed >= MaxSeekTime}, " +
                    $"agentOnMesh={_vehicleAgent.isOnNavMesh}, pathStatus={(_vehicleAgent.isOnNavMesh ? _vehicleAgent.pathStatus.ToString() : "n/a")}. " +
                    $"Refusing to straight-line through obstacles — aborting this leg.");
            }

            onDone?.Invoke(arrived);
        }

        // Forks-first insertion drive. Two design points here are load-bearing and MUST NOT be
        // reverted to a plain "drive t.forward until PlanarDist <= threshold" loop:
        //
        //  1. TERMINATION IS BY FORWARD PROJECTION, NOT RADIUS. We stop when the target is no longer
        //     ahead of the forks along the drive axis (Dot(remaining, forkDir) <= ArriveThreshold),
        //     not when the truck is within a 0.15m circle of the point. A radius test is unreachable
        //     the moment the straight-line lateral miss exceeds the radius — which happens naturally
        //     as the pallet gets deeper (miss ≈ depth·sin(aimError)). That made the loop spin forever
        //     and ram the truck through the stack ("hanging in space"), and it got worse the deeper
        //     the pallet sat. Projection always terminates because it only cares about depth reached.
        //
        //  2. WE CONTINUOUSLY STEER toward the anchor while driving, so a residual aim error (up to
        //     FaceThreshold) can't accumulate into a lateral miss over a long lane. Without this the
        //     forks arrive off-centre on deep pallets and the subsequent grab never satisfies its
        //     variance, ramming instead of picking.
        //
        //  3. A hard travel cap (MaxLaneInsertTravel) is a final backstop so nothing here can ever
        //     drive to infinity again, mirroring DriveToGrab.
        private IEnumerator DriveForksFirst(Transform t, Vector3 target, float maxTravel = -1f)
        {
            float travelCap = maxTravel > 0f ? maxTravel : MaxLaneInsertTravel;
            Vector3 flat = new Vector3(target.x, t.position.y, target.z);
            Vector3 to = flat - t.position; to.y = 0f;
            if (to.magnitude <= ArriveThreshold) { t.position = flat; yield break; }
            if (to.sqrMagnitude > 0.01f) yield return FaceForks(t, to);

            Vector3 startPos = t.position;
            while (true)
            {
                Vector3 remaining = flat - t.position; remaining.y = 0f;
                Vector3 forkDir = (t.forward * _forkAxisSign);

                // Stop once the target has reached / passed the fork plane (depth reached).
                if (Vector3.Dot(remaining, forkDir) <= ArriveThreshold) break;

                // Hard backstop: never drive past the lane insertion limit, whatever the aim.
                if (Vector3.Distance(startPos, t.position) >= travelCap) break;

                // Gentle lateral correction so heading error can't compound with depth. Gated to
                // avoid jitter/spin when we're already essentially on-axis and close.
                if (remaining.sqrMagnitude > 0.04f)
                {
                    Quaternion want = Quaternion.LookRotation(BodyForwardForForks(remaining.normalized));
                    t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                }

                t.position += t.forward * _forkAxisSign * DriveSpeed * Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator FaceForks(Transform t, Vector3 worldForkDir)
        {
            if (worldForkDir.sqrMagnitude < 0.01f) yield break;
            Quaternion want = Quaternion.LookRotation(BodyForwardForForks(worldForkDir.normalized));
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.rotation = want;
        }

        private IEnumerator RotateTo(Transform t, Vector3 worldDir)
        {
            if (worldDir.sqrMagnitude < 0.01f) yield break;
            Quaternion want = Quaternion.LookRotation(worldDir.normalized);
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.rotation = want;
        }

        private IEnumerator ReverseToPoint(Transform t, Vector3 target)
        {
            Vector3 flat = new Vector3(target.x, t.position.y, target.z);
            while (PlanarDist(t.position, target) > ArriveThreshold)
            {
                t.position = Vector3.MoveTowards(t.position, flat, DriveSpeed * Time.deltaTime);
                yield return null;
            }
            t.position = flat;
        }

        private IEnumerator DriveToGrab(Transform pallet)
        {
            Vector3 origin = transform.position;
            while (Vector3.Distance(origin, transform.position) < MaxLaneInsertTravel)
            {
                Vector3 forkPos = _forks != null ? _forks.position : transform.position;
                float xzDist = new Vector2(forkPos.x - pallet.position.x, forkPos.z - pallet.position.z).magnitude;
                if (xzDist <= _grabVariance) break;
                transform.position += transform.forward * _forkAxisSign * DriveSpeed * Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Extends the forks alone toward the pallet -- the chassis has already parked at the exit
        /// anchor (see the caller) and does not move again here. Retracts to rest first so every
        /// attempt starts from a clean pose, then advances _forks.localPosition.z until the pallet's
        /// PalletAnchor is within <see cref="ForkGrabProximity"/> (0.15m) of the truck's PalletAnchor,
        /// capped by <see cref="MaxForkExtend"/> so it can never reach through the pallet. Reports
        /// success/failure through <paramref name="onDone"/> so the caller can retry from a fresh
        /// pose instead of blindly parenting a mis-aligned pallet.
        /// </summary>
        private IEnumerator InsertToGrab(Transform pallet, System.Action<bool> onDone)
        {
            if (_forks == null || _palletAnchor == null)
            {
                Debug.LogWarning($"[InsertToGrab] Missing forks={_forks} or palletAnchor={_palletAnchor}");
                onDone?.Invoke(false);
                yield break;
            }

            // Start every attempt from a clean, fully-retracted pose -- the chassis has already
            // parked at the exit anchor (see the caller) and does not move again from here.
            _forks.localPosition = new Vector3(_forks.localPosition.x, _forks.localPosition.y, _forkRestLocalZ);

            bool got = false;
            float extended = 0f;
            float initialDist = Vector3.Distance(pallet.position, _palletAnchor.position);
            Debug.Log($"[InsertToGrab] Starting grab: initial distance = {initialDist:F3}m, threshold = {ForkGrabProximity:F3}m, pallet={pallet.name}, forkAnchor={_palletAnchor.name}");

            while (extended < MaxForkExtend)
            {
                // Measure distance between pallet's root transform and truck's PalletAnchor
                float dist = Vector3.Distance(pallet.position, _palletAnchor.position);
                if (dist <= ForkGrabProximity)
                {
                    Debug.Log($"[InsertToGrab] Grab successful at distance {dist:F3}m after extending {extended:F3}m");
                    got = true;
                    break;
                }

                Vector3 lp = _forks.localPosition;
                lp.z += _forkAxisSign * _forkExtendSpeed * Time.deltaTime;
                _forks.localPosition = lp;
                extended += _forkExtendSpeed * Time.deltaTime;
                yield return null;
            }

            if (!got)
                Debug.LogWarning($"[InsertToGrab] Grab failed - reached max extend {MaxForkExtend}m without achieving proximity. Final distance: {Vector3.Distance(pallet.position, _palletAnchor.position):F3}m");

            // Miss -- retract so a retry (or the reverse-and-backoff caller) doesn't drag the
            // forks around still extended.
            if (!got)
                _forks.localPosition = new Vector3(_forks.localPosition.x, _forks.localPosition.y, _forkRestLocalZ);

            onDone?.Invoke(got);
        }

        private IEnumerator LiftForks(Transform forks, float targetLocalY)
        {
            while (Mathf.Abs(forks.localPosition.y - targetLocalY) > 0.005f)
            {
                forks.localPosition = new Vector3(forks.localPosition.x, Mathf.MoveTowards(forks.localPosition.y, targetLocalY, _forkLiftSpeed * Time.deltaTime), forks.localPosition.z);
                yield return null;
            }
            forks.localPosition = new Vector3(forks.localPosition.x, targetLocalY, forks.localPosition.z);
        }

        private IEnumerator LiftForksToWorldY(Transform forks, float targetWorldY)
        {
            Transform reference = _palletAnchor != null ? _palletAnchor : forks;
            float deltaY = targetWorldY - reference.position.y;
            yield return LiftForks(forks, forks.localPosition.y + deltaY);
        }

        private IEnumerator RetractForks(Transform forks, float targetLocalZ, float speedOverride = -1f)
        {
            float speed = speedOverride > 0f ? speedOverride : _forkExtendSpeed;
            while (Mathf.Abs(forks.localPosition.z - targetLocalZ) > 0.005f)
            {
                forks.localPosition = new Vector3(forks.localPosition.x, forks.localPosition.y, Mathf.MoveTowards(forks.localPosition.z, targetLocalZ, speed * Time.deltaTime));
                yield return null;
            }
            forks.localPosition = new Vector3(forks.localPosition.x, forks.localPosition.y, targetLocalZ);
        }

        private Vector3 BodyForwardForForks(Vector3 worldForkDir) => worldForkDir * _forkAxisSign;

        // NOTE: Flat() NORMALIZES — it returns a unit DIRECTION with Y zeroed, for feeding rotation
        // helpers (FaceForks/RotateTo). Do NOT use it inside a distance check: normalizing both
        // positions collapses them onto the unit sphere, so far-from-origin points (the whole
        // warehouse) read as ~0 apart and drive loops exit instantly = teleport/snap. Use PlanarDist
        // for "how far apart are these two points on the XZ plane".
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
        private static Vector2 FlatV2(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>Horizontal (XZ) distance between two world points, ignoring Y. Non-normalizing —
        /// this is the correct measure for "have we arrived at the target" in the drive coroutines.</summary>
        private static float PlanarDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static bool TryParseLaneName(string raw, out int door, out string lane)
        {
            door = 0; lane = null; 
            if (string.IsNullOrEmpty(raw)) return false;

            // Handle "STG" prefix if present
            string s = raw.StartsWith("STG", System.StringComparison.OrdinalIgnoreCase) ? raw.Substring(3) : raw;

            // Try the robust parser in LaneNamingService first (handles DoorLetter-Slot format)
            if (LaneNamingService.TryParseLaneAddress(s, out door, out lane, out _))
            {
                return true;
            }

            // Fallback: simplified parsing for "{Door}{Lane}" or "{Door}" formats. A Zone's pseudo
            // door number is negative (see ZoneRegistry), so the digit scan has to step over an
            // optional leading '-' or a WorkTask.ToLocation like "-3A" would fail to parse entirely.
            int numStart = s.Length > 0 && s[0] == '-' ? 1 : 0;
            int numEnd = numStart;
            while (numEnd < s.Length && char.IsDigit(s[numEnd])) numEnd++;
            if (numEnd == numStart) return false;
            if (!int.TryParse(s.Substring(0, numEnd), out door)) return false;
            lane = s.Substring(numEnd);
            return true;
        }

        private Transform FindExitPallet(int door, string lane, out string palletId)
        {
            PalletMasterLink link = FindExitAccessiblePallet(door, lane, out palletId);
            return link != null ? link.transform : null;
        }

        private static Transform PickExitFacingAnchor(Transform front, Transform rear, Vector3 depthAxis)
        {
            if (front == null && rear == null) return null;
            if (front == null) return rear;
            if (rear  == null) return front;
            return Vector3.Dot(front.position, depthAxis) >= Vector3.Dot(rear.position, depthAxis) ? front : rear;
        }

        private static Transform FindLocationTransform(string address)
        {
            // The addressable location is the slot GameObject that actually carries a LocationData
            // component AND the LocApproachAnchor child — it lives NESTED under the rack's active
            // aisle-facing label group (LabelFront.*/LabelRear.*), NOT as the bare direct child of the
            // rack root. That bare same-named direct child has no LocationData and no anchor, so
            // resolving it (the old slot.Rack.transform.Find(address)) made FindLocApproachAnchor return
            // null and every rack delivery abort at "approach anchor not found" — dropping the carried
            // pallet back into its lane and looping forever between the two stacked pallets. Locate the
            // LocationData-bearing descendant named `address`, preferring the one on the ACTIVE face
            // (AisleInitializer deactivates the far face's label group entirely).
            if (SlotRegistry.TryGet(address, out var slot) && slot.Rack != null)
            {
                LocationData fallback = null;
                foreach (LocationData ld in slot.Rack.GetComponentsInChildren<LocationData>(true))
                {
                    if (ld.name != address) continue;
                    if (ld.gameObject.activeInHierarchy) return ld.transform; // active aisle face — the real one
                    fallback ??= ld;                                          // remember an inactive match just in case
                }
                if (fallback != null) return fallback.transform;

                // Last resort within the rack: the bare direct child (no anchor, but preserves prior behaviour).
                Transform direct = slot.Rack.transform.Find(address);
                if (direct != null) return direct;
            }
            // Fallback to global search if SlotRegistry lookup fails
            return GameObject.Find(address)?.transform;
        }
        private static Transform FindLocApproachAnchor(string address) => FindLocationTransform(address)?.Find(LocApproachAnchorName);

        private static void EnsureUnderContainer(Transform obj, string containerName)
        {
            if (obj == null) return;
            if (obj.parent != null && obj.parent.name == containerName) return;
            GameObject container = GameObject.Find(containerName) ?? new GameObject(containerName);
            obj.SetParent(container.transform, worldPositionStays: true);
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent) { if (child.name == childName) return child; Transform found = FindDeepChild(child, childName); if (found != null) return found; }
            return null;
        }

        // REMOVED: DestroyObstaclesInCell(). It destroyed the NavMeshObstacle AND NavMeshModifier of
        // every PlacedObject sharing a grid cell — which included the staging-lane floor tile, whose
        // modifier carries the area = 3 ("MHE Lane") override and its build settings. Those components
        // were never recreated, so each emptied cell permanently degraded that lane's NavMesh setup.
        // Nothing replaces it: pallets carve now, and disabling the carving obstacle at pickup frees
        // the cell the same frame.
    }
}
