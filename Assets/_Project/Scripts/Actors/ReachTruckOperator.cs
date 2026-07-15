using System.Collections;
using System.Collections.Generic;
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

        private const float PalletHalfHeight    = 0.08f;
        private const float ForkTravelHeight    = 0.4f;
        private const float ForkRackClearance   = 0.12f;
        private const float ForkDepositDrop     = 0.05f;

        private const float MaxLaneInsertTravel = 8f;
        
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

            if (_workQueue        == null) ServiceLocator.TryGet(out _workQueue);
            if (_putawayLogic     == null) ServiceLocator.TryGet(out _putawayLogic);
            if (_inventoryService == null) ServiceLocator.TryGet(out _inventoryService);

            if (_workQueue == null) return;

            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = TaskPollInterval;

            TryClaimAndStart();
        }

        private void TryClaimAndStart()
        {
            var pending = _workQueue.GetPendingTasksForRole(EmployeeRole.ReachTruckOperator);
            if (pending.Count == 0) return;

            WorkTask best    = null;
            int      bestSlot = -1;
            float    bestSqr  = float.PositiveInfinity;

            foreach (var t in pending)
            {
                // CRITICAL: Reach Trucks ONLY do Putaway. They MUST NOT claim Receive tasks (which are for Receivers).
                if (t.Type != WorkTaskType.Putaway) continue;
                
                if (string.IsNullOrEmpty(t.FromLocation) || t.FromLocation == "STG" || t.FromLocation.Contains("(0, 0)"))
                {
                    Debug.LogWarning($"[ReachTruckOperator] Task {t.TaskId} has invalid FromLocation '{t.FromLocation}'. Skipping.");
                    continue;
                }

                if (!TryParseLaneName(t.FromLocation, out int d, out string l)) continue;
                if (!LaneNamingService.TryGetLaneGeometry(d, l, out var g)) continue;

                // RULE: Only claim tasks for pallets that are currently accessible (topmost and received).
                if (!IsPalletAccessible(t.PalletId, d, l)) continue;

                // PRIORITY 1: Highest slot number (exit end) across all lanes.
                // We extract the slot number from the task's FromLocation if possible.
                int slot = 0;
                if (LaneNamingService.TryParseLaneAddress(t.FromLocation.StartsWith("STG") ? t.FromLocation.Substring(3) : t.FromLocation, out _, out _, out int s))
                {
                    slot = s;
                }

                if (slot > bestSlot)
                {
                    bestSlot = slot;
                    best = t;
                    bestSqr = (new Vector3(g.ExitPoint.x, transform.position.y, g.ExitPoint.z) - transform.position).sqrMagnitude;
                }
                else if (slot == bestSlot)
                {
                    // PRIORITY 2: If slot numbers are equal (or both zero), pick the closest lane exit.
                    float sqr = (new Vector3(g.ExitPoint.x, transform.position.y, g.ExitPoint.z) - transform.position).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = t;
                    }
                }
            }

            if (best == null) return;
            
            string guid = _operatorSlot.CurrentOperator?.Record?.employeeGuid;
            if (string.IsNullOrEmpty(guid)) return;

            if (!_workQueue.TryClaimSpecificTask(best, guid)) return;

            _busy = true;
            StartCoroutine(PutawayRoutine(best));
        }

        /// <summary>
        /// A pallet is accessible if it is the topmost pallet in the first slot (from exit) 
        /// that contains pallets in its staging lane, and it has been received (has PalletData).
        /// </summary>
        private bool IsPalletAccessible(string targetPalletId, int door, string lane)
        {
            List<LaneNamingService.LaneSlot> slots = LaneNamingService.GetLane(door, lane);
            if (slots.Count == 0) return false;

            // Iterate from the exit end of the lane inward.
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                List<PalletMasterRecord> pallets = _inventoryService?.GetPalletsAtLocation(slots[i].Cell);
                if (pallets == null || pallets.Count == 0) continue;

                // Found the exit-most occupied slot.
                PalletMasterRecord topmost = pallets[pallets.Count - 1];
                
                // If the pallet we want is the topmost one at the exit end, it's accessible.
                if (topmost.PalletId == targetPalletId)
                {
                    PalletMasterLink link = PalletMasterLink.Find(targetPalletId);
                    return link != null && link.GetComponent<PalletData>() != null;
                }

                // If we found a different pallet closer to the exit than our target, we are blocked.
                return false;
            }
            return false;
        }

        private IEnumerator PutawayRoutine(WorkTask task)
        {
            Commandeer();
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

            // 2. Drive to Lane Exit ---------------------------------------------------------------
            yield return DriveToPoint(transform, exitPoint);

            // 3. Find Pallet ----------------------------------------------------------------------
            Transform pallet = FindExitPallet(door, lane, out string palletId);
            
            // Safety: Double check if the pallet we found matches our task.
            if (pallet == null || palletId != task.PalletId)
            {
                Debug.LogWarning($"[ReachTruckOperator] Target pallet {task.PalletId} is no longer accessible at lane {door}{lane}. Aborting.");
                yield return AbortRoutine(task, null, null);
                yield break;
            }

            EnsureUnderContainer(pallet, InventoryContainerName);

            // 4. Pickup Sequence ------------------------------------------------------------------
            Transform anchorFront = FindDeepChild(pallet, ChepAnchorFrontName);
            Transform anchorRear  = FindDeepChild(pallet, ChepAnchorRearName);
            Transform exitAnchor  = PickExitFacingAnchor(anchorFront, anchorRear, geo.DepthAxis);
            
            if (exitAnchor == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Pallet {palletId} missing anchors.");
                yield return AbortRoutine(task, palletId, null);
                yield break;
            }

            yield return DriveForksFirst(transform, exitAnchor.position);
            yield return FaceForks(transform, Flat(pallet.position - transform.position));

            if (_forks != null)
                yield return LiftForksToWorldY(_forks, pallet.position.y + PalletHalfHeight);

            yield return DriveToGrab(pallet);

            Transform carrier = _palletAnchor != null ? _palletAnchor : (_forks != null ? _forks : transform);
            
            // RULE: Parent the pallet to the PalletAnchor. 
            // Snapping to local zero ensures the pallet is perfectly centered on the forks' intended carry point.
            pallet.SetParent(carrier, worldPositionStays: false);
            pallet.localPosition = Vector3.zero;
            pallet.localRotation = Quaternion.identity;

            NavMeshObstacle obstacle = pallet.GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;

            // 5. Assign Destination ---------------------------------------------------------------
            Vector2 pickupXZ = new Vector2(transform.position.x, transform.position.z);
            string toAddress = _putawayLogic?.AssignPutawayDestination(palletId, pickupXZ);
            if (string.IsNullOrEmpty(toAddress))
            {
                Debug.LogWarning($"[ReachTruckOperator] No putaway destination for {palletId}.");
                pallet.SetParent(null, worldPositionStays: true);
                if (obstacle != null) obstacle.enabled = true;
                yield return AbortRoutine(task, palletId, null);
                yield break;
            }
            task.AssignToLocation(toAddress);

            // 6. Leg 1 Back-out -------------------------------------------------------------------
            yield return ReverseToPoint(transform, exitPoint);

            // 7. Leg 2 Rack Travel ----------------------------------------------------------------
            Transform locApproach = FindLocApproachAnchor(toAddress);
            if (locApproach == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Approach anchor not found for {toAddress}.");
                pallet.SetParent(null, worldPositionStays: true);
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
                pallet.SetParent(null, worldPositionStays: true);
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
                if (xzDist <= _placeVariance) { varianceMet = true; break; }
                yield return null;
            }

            if (!varianceMet)
            {
                Debug.LogWarning($"[ReachTruckOperator] MISS! Variance {new Vector2(pallet.position.x - locationTr.position.x, pallet.position.z - locationTr.position.z).magnitude:F2}m > {_placeVariance}m. Aborting.");
                yield return AbortRoutine(task, palletId, toAddress);
                yield break;
            }

            // 9. Completion ------------------------------------------------------------------------
            if (_forks != null)
                yield return LiftForks(_forks, _forks.localPosition.y - ForkDepositDrop);

            pallet.SetParent(null, worldPositionStays: true);
            pallet.position = new Vector3(locationTr.position.x, pallet.position.y, locationTr.position.z);
            if (obstacle != null) obstacle.enabled = true;

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

            _putawayLogic?.CompletePutaway(palletId, toAddress, toGrid);
            _workQueue?.CompleteTask(task.TaskId);
            Debug.Log($"[ReachTruckOperator] SUCCESS: {palletId} put away at {toAddress}");

            if (_forks != null)
            {
                yield return RetractForks(_forks, _forkRestLocalZ);
                yield return LiftForks(_forks, _forkRestLocalY);
            }

            Restore();
        }

        private IEnumerator AbortRoutine(WorkTask task, string palletId, string reservedAddress)
        {
            Debug.Log($"[ReachTruckOperator] ABORTING task. Pallet: {palletId}, Location: {reservedAddress}");
            _putawayLogic?.CancelPutaway(reservedAddress);
            if (task != null) task.Status = WorkTaskStatus.Pending;

            // If holding a pallet, retract and re-enable obstacle
            if (_forks != null)
            {
                yield return RetractForks(_forks, _forkRestLocalZ);
                yield return LiftForks(_forks, _forkRestLocalY);
            }

            if (!string.IsNullOrEmpty(palletId))
            {
                var link = PalletMasterLink.Find(palletId);
                if (link != null && link.TryGetComponent<NavMeshObstacle>(out var obstacle))
                    obstacle.enabled = true;
            }

            Restore();
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
            while (Vector3.Distance(Flat(t.position), Flat(target)) > ArriveThreshold)
            {
                t.position = Vector3.MoveTowards(t.position, flat, DriveSpeed * Time.deltaTime);
                yield return null;
            }
            t.position = flat;
        }

        private IEnumerator DriveForksFirst(Transform t, Vector3 target)
        {
            Vector3 flat = new Vector3(target.x, t.position.y, target.z);
            Vector3 to = flat - t.position; to.y = 0f;
            if (to.magnitude <= ArriveThreshold) { t.position = flat; yield break; }
            if (to.sqrMagnitude > 0.01f) yield return FaceForks(t, to);
            while (Vector3.Distance(Flat(t.position), Flat(target)) > ArriveThreshold)
            {
                t.position += t.forward * _forkAxisSign * DriveSpeed * Time.deltaTime;
                yield return null;
            }
            t.position = flat;
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
            while (Vector3.Distance(Flat(t.position), Flat(target)) > ArriveThreshold)
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

        private IEnumerator RetractForks(Transform forks, float targetLocalZ)
        {
            while (Mathf.Abs(forks.localPosition.z - targetLocalZ) > 0.005f)
            {
                forks.localPosition = new Vector3(forks.localPosition.x, forks.localPosition.y, Mathf.MoveTowards(forks.localPosition.z, targetLocalZ, _forkExtendSpeed * Time.deltaTime));
                yield return null;
            }
            forks.localPosition = new Vector3(forks.localPosition.x, forks.localPosition.y, targetLocalZ);
        }

        private Vector3 BodyForwardForForks(Vector3 worldForkDir) => worldForkDir * _forkAxisSign;
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
        private static Vector2 FlatV2(Vector3 v) => new Vector2(v.x, v.z);

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
            palletId = null;
            List<LaneNamingService.LaneSlot> slots = LaneNamingService.GetLane(door, lane);
            if (slots.Count == 0) return null;

            for (int i = slots.Count - 1; i >= 0; i--)
            {
                List<PalletMasterRecord> pallets = _inventoryService?.GetPalletsAtLocation(slots[i].Cell);
                if (pallets == null || pallets.Count == 0) continue;

                PalletMasterRecord topmost = pallets[pallets.Count - 1];
                PalletMasterLink   link    = PalletMasterLink.Find(topmost.PalletId);
                if (link == null) continue;

                // RULE: Wait for the palette on top to be received.
                if (link.GetComponent<PalletData>() == null) return null;

                // Verify pallet is physically near the slot it's assigned to.
                // We compare to the specific slot's position rather than the lane's final exit point,
                // as a long lane might put the first few pallets > 20m from the exit.
                if (LaneNamingService.TryGetSlotWorldPos(slots[i].Cell, out var slotWorldPos))
                {
                    if (Vector3.Distance(link.transform.position, slotWorldPos) > 5.0f) continue;
                }

                palletId = topmost.PalletId;
                return link.transform;
            }
            return null;
        }

        private static Transform PickExitFacingAnchor(Transform front, Transform rear, Vector3 depthAxis)
        {
            if (front == null && rear == null) return null;
            if (front == null) return rear;
            if (rear  == null) return front;
            return Vector3.Dot(front.position, depthAxis) >= Vector3.Dot(rear.position, depthAxis) ? front : rear;
        }

        private static Transform FindLocationTransform(string address) => GameObject.Find(address)?.transform;
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
    }
}
