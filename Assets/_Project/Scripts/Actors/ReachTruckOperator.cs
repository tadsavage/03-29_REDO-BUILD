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
        private const float FaceThreshold       = 2f;
        private const float TaskPollInterval    = 1f;

        private const float PalletHalfHeight    = 0.085f;
        private const float ForkBladeHeightOffset = 0.08f;
        private const float ForkTravelHeight    = 0.4f;
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
                return; // this operator's assigned pallet has no rack space yet — wait out the backoff

            if (alreadyAssigned != null)
            {
                Debug.Log($"[ReachTruckOperator] '{name}' resuming already assigned task {alreadyAssigned.TaskId}");
                _busy = true;
                StartCoroutine(PutawayRoutine(alreadyAssigned));
                return;
            }

            var pending = _workQueue.GetPendingTasksForRole(EmployeeRole.ReachTruckOperator);
            if (pending.Count == 0) return;

            // Selection rule: lowest WorkTask.Priority wins (lower = more urgent); ties keep whichever
            // candidate was found first, which is the earliest-created task since GetPendingTasksForRole
            // preserves WorkQueueSystem's creation order — i.e. FIFO within a priority tier. Proximity/
            // lane-slot-number scoring was deliberately removed: pick order should be driven by priority
            // only, not by which lane happens to be closest to this particular truck.
            WorkTask best = null;

            foreach (var t in pending)
            {
                // CRITICAL: Reach Trucks ONLY do Putaway. They MUST NOT claim Receive tasks (which are for Receivers).
                if (t.Type != WorkTaskType.Putaway) continue;

                if (string.IsNullOrEmpty(t.FromLocation) || t.FromLocation == "STG" ||
                    t.FromLocation.Contains("(") || t.FromLocation.Contains(")"))
                {
                    Debug.LogWarning($"[ReachTruckOperator] Task {t.TaskId} has invalid FromLocation '{t.FromLocation}'. Skipping.");
                    continue;
                }

                if (!TryParseLaneName(t.FromLocation, out int d, out string l)) continue;
                if (!LaneNamingService.TryGetLaneGeometry(d, l, out _)) continue;

                // RULE: Only claim tasks for pallets that are currently accessible (topmost and received).
                if (!IsPalletAccessible(t.PalletId, d, l)) continue;

                // Skip pallets that recently found no rack destination (warehouse full) — retried after backoff.
                if (IsPalletBlocked(t.PalletId)) continue;

                if (best == null || t.Priority < best.Priority)
                    best = t;
            }

            if (best == null) return;

            if (!_workQueue.TryClaimSpecificTask(best, guid)) return;

            _busy = true;
            StartCoroutine(PutawayRoutine(best));
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
            Commandeer();
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
                if (task != null) task.Status = WorkTaskStatus.Pending;
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

            // 4. Drive to the lane exit, then resolve the pallet's grab anchors.
            yield return DriveToPoint(transform, exitPoint);

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

            // ── Closed-loop acquire (staging → square-up → short insert → verify → retry) ─────────
            // WHY this shape (and NOT a single long DriveForksFirst into the pallet): the old open-loop
            // drive covered the WHOLE lane depth in one fork-first motion, so a small heading error had
            // the whole depth to accumulate into a terminal lateral miss — worse the deeper the pallet,
            // which is exactly why it failed at the 4th slot in and either rammed the pallet up/away or
            // abandoned it mid-air. Here the long travel is a HOMING move to an exact staging point just
            // in front of the pallet (MoveTowards converges with zero drift at any distance), and the
            // only fork-first motion is a SHORT insert from that squared-up point, so its drift is
            // negligible regardless of lane depth. If an insert still misses grab variance we back off
            // and retry from a freshly measured pose; if it truly can't seat, we abort cleanly and leave
            // the pallet where it sits — never floating.
            const int   MaxGrabAttempts = 3;
            const float StagingGap      = 0.6f;   // stage this far in FRONT of the pallet before inserting
            bool grabbed = false;

            for (int attempt = 0; attempt < MaxGrabAttempts && !grabbed; attempt++)
            {
                // Re-measure every attempt — outward = from the pallet toward the lane exit (the side the
                // truck inserts from), derived from the exit point so there's no axis-sign guesswork.
                Vector3 outward = Flat(exitPoint - pallet.position);
                Vector3 anchorPos = exitAnchor.position;
                Vector3 stagingPoint = new Vector3(anchorPos.x, transform.position.y, anchorPos.z) + outward * StagingGap;

                // 1. Coarse: home exactly onto the staging point (no heading-dependent drift).
                yield return DriveToPoint(transform, stagingPoint);
                // 2. Square up: forks point straight into the lane, down the pallet's centre line.
                yield return FaceForks(transform, -outward);
                // 3. Lift to the pallet's fork-pocket height (correct even for a top-of-stack pallet).
                if (_forks != null)
                    yield return LiftForksToWorldY(_forks, pallet.position.y + PalletHalfHeight - ForkBladeHeightOffset);
                // 4. Short, capped, self-correcting insert. Reports whether the fork grab-point actually
                //    reached the pallet within _grabVariance — the checkpoint you asked for.
                bool ok = false;
                yield return InsertToGrab(pallet, StagingGap + 1.5f, r => ok = r);
                grabbed = ok;

                if (!grabbed)
                {
                    Debug.LogWarning($"[ReachTruckOperator] Grab attempt {attempt + 1}/{MaxGrabAttempts} for {palletId} missed variance. Backing off to re-measure.");
                    yield return ReverseToPoint(transform, stagingPoint);
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
            pallet.localRotation = Quaternion.identity;

            // Get the cell we're picking from BEFORE we move the pallet
            var pickupCell = new Vector2Int(pallet.GetComponent<PlacedObject>()?.gridX ?? 0,
                                             pallet.GetComponent<PlacedObject>()?.gridY ?? 0);

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

            // Check if the pickup cell is now completely empty (no other pallets, top or bottom)
            var remaining = _inventoryService?.GetPalletsAtLocation(pickupCell);
            if (remaining != null && remaining.Count == 0)
            {
                // Cell is empty — immediately destroy all NavMesh components in that cell to clear carving.
                // This auto-clears the NavMesh without needing an explicit rebake, making the cell instantly walkable.
                DestroyObstaclesInCell(pickupCell);
            }
            else if (obstacle != null)
            {
                // Cell still has other pallets — queue this pallet's obstacle for batched cleanup
                GameCore.Services.NavMeshRebuildQueue.QueueRebuild(obstacle);
            }

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

            yield return RotateTo(transform, Flat(locApproach.position - transform.position));
            if (_forks != null) yield return LiftForks(_forks, ForkTravelHeight);
            yield return DriveToPoint(transform, locApproach.position);

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
            pallet.position = locationTr.position;
            // Now — and ONLY now, with the pallet physically placed at the rack and off the forks — move
            // it under the Inventory container (its organizational home). Deferring the re-parent to this
            // point (instead of at claim time) is what keeps a still-resting, claimed pallet unparented so
            // ComputeDropBaseY can see it and stack the next drop on top of it correctly.
            EnsureUnderContainer(pallet, InventoryContainerName);
            if (obstacle != null) obstacle.enabled = true;
            _carryOriginValid = false; // pallet is safely placed — the captured lane pose is no longer a fallback.

            var locationData = locationTr.GetComponent<LocationData>();
            if (locationData != null)
            {
                var record = _inventoryService?.GetPallet(palletId);
                locationData.Occupy(palletId, record?.SkuId ?? string.Empty, record?.Quantity ?? 0);
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

            Commandeer();
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
            _putawayLogic?.CancelPutaway(reservedAddress);
            if (task != null) task.Status = WorkTaskStatus.Pending;

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

        private void Commandeer()
        {
            if (_vehicleNav != null) { _vehicleNav.CancelSeekPosition(); _vehicleNav.enabled = false; }
            if (_vehicleAgent != null) { _vehicleAgent.isStopped = true; _vehicleAgent.enabled = false; }
        }

        private void Restore()
        {
            if (_vehicleAgent != null)
            {
                _vehicleAgent.enabled = true;
                if (_vehicleAgent.isActiveAndEnabled && _vehicleAgent.isOnNavMesh) { _vehicleAgent.Warp(transform.position); _vehicleAgent.isStopped = false; }
            }
            if (_vehicleNav != null) _vehicleNav.enabled = true;
            _vehicleNav?.GoToRandomWaypoint();
            _busy = false;
        }

        private IEnumerator DriveToPoint(Transform t, Vector3 target)
        {
            Vector3 flat = new Vector3(target.x, t.position.y, target.z);
            Vector3 to = flat - t.position; to.y = 0f;
            if (to.magnitude <= ArriveThreshold) { t.position = flat; yield break; }
            yield return RotateTo(t, to);
            while (PlanarDist(t.position, target) > ArriveThreshold)
            {
                t.position = Vector3.MoveTowards(t.position, flat, DriveSpeed * Time.deltaTime);
                yield return null;
            }
            t.position = flat;
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
        private IEnumerator DriveForksFirst(Transform t, Vector3 target)
        {
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
                if (Vector3.Distance(startPos, t.position) >= MaxLaneInsertTravel) break;

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
        /// Short, capped, self-correcting fork insert used by the closed-loop acquire. Creeps forward
        /// from an already-squared-up staging pose, continuously nudging the heading so the fork tracks
        /// the pallet pivot, and stops the instant the fork grab-point is within <see cref="_grabVariance"/>
        /// of the pallet on the XZ plane. Reports success/failure through <paramref name="onDone"/> so the
        /// caller can retry from a fresh pose instead of blindly parenting a mis-aligned pallet. The travel
        /// cap (<paramref name="maxTravel"/>) guarantees it can never drive through the pallet — a miss
        /// simply ends the creep and returns false.
        /// </summary>
        private IEnumerator InsertToGrab(Transform pallet, float maxTravel, System.Action<bool> onDone)
        {
            Vector3 origin = transform.position;
            bool got = false;
            while (Vector3.Distance(origin, transform.position) < maxTravel)
            {
                Vector3 forkPos = _forks != null ? _forks.position : transform.position;
                float xzDist = new Vector2(forkPos.x - pallet.position.x, forkPos.z - pallet.position.z).magnitude;
                if (xzDist <= _grabVariance) { got = true; break; }

                // Gentle heading correction so the fork keeps tracking the pallet pivot over the insert.
                Vector3 want = pallet.position - transform.position; want.y = 0f;
                if (want.sqrMagnitude > 0.04f)
                {
                    Quaternion wantRot = Quaternion.LookRotation(BodyForwardForForks(want.normalized));
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, wantRot, TurnSpeed * Time.deltaTime);
                }

                transform.position += transform.forward * _forkAxisSign * DriveSpeed * Time.deltaTime;
                yield return null;
            }
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
            float targetLocalY = forks.parent != null ? forks.parent.InverseTransformPoint(new Vector3(forks.position.x, targetWorldY, forks.position.z)).y : targetWorldY;
            yield return LiftForks(forks, targetLocalY);
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

            // Fallback: simplified parsing for "{Door}{Lane}" or "{Door}" formats
            int numEnd = 0; 
            while (numEnd < s.Length && char.IsDigit(s[numEnd])) numEnd++;
            if (numEnd == 0) return false;
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

        /// <summary>
        /// When a cell becomes completely empty (no pallets, top or bottom), immediately destroy
        /// all NavMesh components (obstacle + modifier) in that cell. This auto-clears the NavMesh
        /// carving and triggers an immediate rebake, making the cell instantly walkable for MHE.
        /// </summary>
        private void DestroyObstaclesInCell(Vector2Int cell)
        {
            var modifierType = System.Type.GetType("UnityEngine.AI.NavMeshModifier, Assembly-CSharp");
            if (modifierType == null)
                modifierType = System.Type.GetType("UnityEngine.AI.NavMeshModifier");

            foreach (var po in PlacedObjectRegistry.All)
            {
                if (po == null || po.gameObject == null) continue;
                if (po.gridX != cell.x || po.gridY != cell.y) continue;

                var obstacle = po.GetComponent<NavMeshObstacle>();
                if (obstacle != null) Destroy(obstacle);

                if (modifierType != null)
                {
                    var modifier = po.GetComponent(modifierType);
                    if (modifier != null) Destroy(modifier);
                }
            }

            Debug.Log($"[ReachTruckOperator] Cell ({cell.x}, {cell.y}) is now empty — destroyed all NavMesh components for immediate rebake.");
        }
    }
}
