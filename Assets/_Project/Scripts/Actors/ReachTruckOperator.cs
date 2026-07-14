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
    /// OPERATOR GATE: The vehicle only polls for tasks while its <see cref="MHEOperatorSlot"/>
    /// is occupied by an employee whose role is <see cref="EmployeeRole.ReachTruckOperator"/>.
    ///
    /// FULL SEQUENCE per putaway task
    /// ─────────────────────────────────────────────────────────────────────
    ///  LEG 1  NavAgent travels to the lane's ExitPoint.
    ///
    ///  PHASE 1 — Commandeered (lane pickup)
    ///   1.  Find the pallet closest to the lane exit (scan from exit inward).
    ///   2.  Drive forks-first to the exit-facing ChepAnchorFront or ChepAnchorRear.
    ///   3.  Rotate to face the pallet center.
    ///   4.  Raise forks to pallet.y + PalletHalfHeight.
    ///   5.  Drive in until forks are within GrabVariance of pallet XZ → parent to forks.
    ///   6.  Call PutawayLogic.AssignPutawayDestination — reserves the TO slot.
    ///   7.  Reverse back to lane ExitPoint (no turn).
    ///   8.  Pivot toward rack (LocApproachAnchor), lower forks to ForkTravelHeight.
    ///
    ///  LEG 2  NavAgent travels to the TO slot's LocApproachAnchor (pallet rides on forks).
    ///
    ///  PHASE 2 — Commandeered (rack putdown)
    ///   9.  Rotate to face the Location transform.
    ///  10.  Raise forks to location.y + ForkRackClearance.
    ///  11.  Extend forks (local Z) until pallet is within PlaceVariance of location XZ.
    ///  12.  Lower forks by ForkDepositDrop → pallet rests on rack beam.
    ///  13.  Transfer pallet to location; call LocationData.Occupy + PutawayLogic.CompletePutaway.
    ///  14.  Retract forks, lower to rest → complete WorkTask, Restore NavAgent.
    /// ─────────────────────────────────────────────────────────────────────
    /// FORK AXIS: _forkAxisSign is auto-detected from the Forks child local Z sign at Start.
    ///   +1  forks on vehicle +Z (front).
    ///   -1  forks on vehicle -Z (rear), matching the Dock Stocker convention.
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

        /// <summary>Y added to pallet.position.y when raising forks to grab (half-pallet height).</summary>
        private const float PalletHalfHeight    = 0.08f;

        /// <summary>Fork height when travelling between lane exit and rack.</summary>
        private const float ForkTravelHeight    = 0.4f;

        /// <summary>Y added to location.position.y so forks clear the rack beam before extending.</summary>
        private const float ForkRackClearance   = 0.12f;

        /// <summary>Amount forks lower after reaching place variance, depositing the pallet on the beam.</summary>
        private const float ForkDepositDrop     = 0.05f;

        /// <summary>Safety cap on forks-first drive into lane (prevents infinite loop).</summary>
        private const float MaxLaneInsertTravel = 8f;

        /// <summary>Safety cap on fork extension into rack slot.</summary>
        private const float MaxForkExtend       = 2.5f;

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
        [SerializeField, Range(0f, 0.1f)]  private float _grabVariance  = 0.08f;

        [Tooltip("Maximum XZ distance (m) between pallet and slot centre before the pallet is released.")]
        [SerializeField, Range(0f, 0.25f)] private float _placeVariance = 0.1f;

        // ── Runtime ───────────────────────────────────────────────────────────────────────────────

        private MHEOperatorSlot  _operatorSlot;
        private AiNavigation     _vehicleNav;
        private NavMeshAgent     _vehicleAgent;
        private Transform        _forks;
        private Transform        _palletAnchor;
        private float            _forkRestLocalY;
        private float            _forkRestLocalZ;

        /// <summary>+1 if forks are on local +Z; -1 if on local -Z.</summary>
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
                Debug.Log($"[ReachTruckOperator] '{name}' forks localZ={_forks.localPosition.z:F3} → _forkAxisSign={_forkAxisSign:+0;-0}");
            }
            else
            {
                Debug.LogWarning($"[ReachTruckOperator] '{name}' has no '{ForkChildName}' child — fork movement disabled.");
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

        // ── Task Claiming ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans all pending putaway tasks, picks the one whose lane exit is nearest to this
        /// vehicle, claims it, and starts the full putaway coroutine.
        /// </summary>
        private void TryClaimAndStart()
        {
            var pending = _workQueue.GetPendingTasksForRole(EmployeeRole.ReachTruckOperator);
            WorkTask best    = null;
            float    bestSqr = float.PositiveInfinity;

            foreach (var t in pending)
            {
                if (t.Type != WorkTaskType.Putaway) continue;
                if (!TryParseLaneName(t.FromLocation, out int d, out string l)) continue;
                if (!LaneNamingService.TryGetLaneGeometry(d, l, out var g)) continue;

                float sqr = (new Vector3(g.ExitPoint.x, transform.position.y, g.ExitPoint.z)
                             - transform.position).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = t; }
            }

            if (best == null) return;
            if (!_workQueue.TryClaimSpecificTask(best)) return;

            _busy = true;
            StartCoroutine(PutawayRoutine(best));
        }

        // ── Main Coroutine ────────────────────────────────────────────────────────────────────────

        private IEnumerator PutawayRoutine(WorkTask task)
        {
            // Resolve lane geometry ------------------------------------------------------------------
            if (!TryParseLaneName(task.FromLocation, out int door, out string lane) ||
                !LaneNamingService.TryGetLaneGeometry(door, lane, out var geo))
            {
                Debug.LogWarning($"[ReachTruckOperator] Cannot resolve lane geometry for '{task.FromLocation}' — aborting.");
                yield return AbortRoutine(task, null, null);
                yield break;
            }

            Vector3 exitPoint = new Vector3(geo.ExitPoint.x, transform.position.y, geo.ExitPoint.z);

            // ════════════════════════════════════════════════════════════════════════════════════════
            //  LEG 1  —  NavAgent to lane exit
            // ════════════════════════════════════════════════════════════════════════════════════════
            bool arrivedExit = false;
            _vehicleNav.CancelSeekPosition();
            _vehicleNav.SeekPosition(exitPoint, () => arrivedExit = true);
            yield return new WaitUntil(() => arrivedExit);

            // ════════════════════════════════════════════════════════════════════════════════════════
            //  PHASE 1  —  Commandeered: lane pickup
            // ════════════════════════════════════════════════════════════════════════════════════════
            Commandeer();

            // Find the pallet closest to the exit (scan from exit inward) ---------------------------
            Transform pallet = FindExitPallet(door, lane, out string palletId);
            if (pallet == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] No pallet in lane '{door}{lane}' — aborting.");
                yield return AbortRoutine(task, null, null);
                yield break;
            }

            // Move pallet under the Inventory container for hierarchy clarity ----------------------
            EnsureUnderContainer(pallet, InventoryContainerName);

            // Pick the ChepAnchor that faces the exit (further along DepthAxis) -------------------
            Transform anchorFront = FindDeepChild(pallet, ChepAnchorFrontName);
            Transform anchorRear  = FindDeepChild(pallet, ChepAnchorRearName);
            Transform exitAnchor  = PickExitFacingAnchor(anchorFront, anchorRear, geo.DepthAxis);
            if (exitAnchor == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Pallet '{palletId}' has no ChepAnchor children — aborting.");
                yield return AbortRoutine(task, null, null);
                yield break;
            }

            // Drive forks-first to the exit-facing anchor ------------------------------------------
            yield return DriveForksFirst(transform, exitAnchor.position);

            // Rotate to face the pallet center -----------------------------------------------------
            yield return FaceForks(transform, Flat(pallet.position - transform.position));

            // Raise forks to pallet Y + half-height offset ----------------------------------------
            if (_forks != null)
                yield return LiftForksToWorldY(_forks, pallet.position.y + PalletHalfHeight);

            // Drive in until forks reach grab variance --------------------------------------------
            yield return DriveToGrab(pallet);

            // Parent pallet to PalletAnchor (or Forks as fallback) --------------------------------
            Transform carrier = _palletAnchor != null ? _palletAnchor
                              : _forks        != null ? _forks
                              : transform;
            pallet.SetParent(carrier, worldPositionStays: true);
            Debug.Log($"[ReachTruckOperator] Pallet '{palletId}' parented to forks — in RTO inventory.");

            // Assign putaway destination (reserves the TO slot) -----------------------------------
            Vector2 pickupXZ = new Vector2(transform.position.x, transform.position.z);
            string toAddress = _putawayLogic?.AssignPutawayDestination(palletId, pickupXZ);
            if (string.IsNullOrEmpty(toAddress))
            {
                Debug.LogWarning($"[ReachTruckOperator] No putaway destination for '{palletId}' — aborting.");
                pallet.SetParent(null, worldPositionStays: true);
                yield return AbortRoutine(task, palletId, null);
                yield break;
            }
            task.AssignToLocation(toAddress);

            // Reverse back to lane exit (no turn) -------------------------------------------------
            yield return ReverseToPoint(transform, exitPoint);

            // Resolve LocApproachAnchor so we know which direction to face before we pivot --------
            Transform locApproach = FindLocApproachAnchor(toAddress);
            if (locApproach == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] LocApproachAnchor not found for '{toAddress}' — aborting.");
                pallet.SetParent(null, worldPositionStays: true);
                yield return AbortRoutine(task, palletId, toAddress);
                yield break;
            }

            // Pivot toward rack, lower forks to travel height -------------------------------------
            yield return RotateTo(transform, Flat(locApproach.position - transform.position));
            if (_forks != null)
                yield return LiftForks(_forks, ForkTravelHeight);

            // ════════════════════════════════════════════════════════════════════════════════════════
            //  LEG 2  —  NavAgent to LocApproachAnchor (pallet rides on forks)
            // ════════════════════════════════════════════════════════════════════════════════════════
            EnableNavAgent();

            bool arrivedRack = false;
            _vehicleNav.CancelSeekPosition();
            _vehicleNav.SeekPosition(locApproach.position, () => arrivedRack = true);
            yield return new WaitUntil(() => arrivedRack);

            // ════════════════════════════════════════════════════════════════════════════════════════
            //  PHASE 2  —  Commandeered: rack putdown
            // ════════════════════════════════════════════════════════════════════════════════════════
            Commandeer();

            // Find the Location transform (named after the address by AisleInitializer) -----------
            Transform locationTr = FindLocationTransform(toAddress);
            if (locationTr == null)
            {
                Debug.LogWarning($"[ReachTruckOperator] Location transform '{toAddress}' not found — aborting.");
                pallet.SetParent(null, worldPositionStays: true);
                yield return AbortRoutine(task, palletId, toAddress);
                yield break;
            }

            // Rotate to face the slot (forks point toward location) --------------------------------
            yield return FaceForks(transform, Flat(locationTr.position - transform.position));

            // Raise forks to slot Y + clearance ---------------------------------------------------
            if (_forks != null)
                yield return LiftForksToWorldY(_forks, locationTr.position.y + ForkRackClearance);

            // Extend forks until pallet reaches place variance ------------------------------------
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

                float xzDist = new Vector2(
                    pallet.position.x - locationTr.position.x,
                    pallet.position.z - locationTr.position.z).magnitude;

                if (xzDist <= _placeVariance) { varianceMet = true; break; }
                yield return null;
            }

            if (!varianceMet)
            {
                Debug.LogWarning($"[ReachTruckOperator] Place variance not met for '{toAddress}' — aborting.");
                pallet.SetParent(null, worldPositionStays: true);
                yield return AbortRoutine(task, palletId, toAddress);
                yield break;
            }

            // Lower forks by deposit drop — pallet settles onto rack beam -------------------------
            if (_forks != null)
                yield return LiftForks(_forks, _forks.localPosition.y - ForkDepositDrop);

            // Transfer pallet to location ---------------------------------------------------------
            pallet.SetParent(null, worldPositionStays: true);
            pallet.position = new Vector3(locationTr.position.x, pallet.position.y, locationTr.position.z);

            var locationData = locationTr.GetComponent<LocationData>();
            if (locationData != null)
            {
                var record = _inventoryService?.GetPallet(palletId);
                locationData.Occupy(palletId, record?.SkuId ?? string.Empty, record?.Quantity ?? 0);
            }
            else
            {
                Debug.LogWarning($"[ReachTruckOperator] No LocationData on '{toAddress}' — inventory transfer skipped.");
            }

            // Update inventory registry and complete the work task --------------------------------
            Vector2Int toGrid = Vector2Int.zero;
            if (SlotRegistry.TryGet(toAddress, out var toSlot) && toSlot.Rack != null)
                toGrid = new Vector2Int(toSlot.Rack.gridX, toSlot.Rack.gridY);

            _putawayLogic?.CompletePutaway(palletId, toAddress, toGrid);
            _workQueue?.CompleteTask(task.TaskId);
            Debug.Log($"[ReachTruckOperator] Putaway complete — '{palletId}' at '{toAddress}'.");

            // Retract forks, lower to rest --------------------------------------------------------
            if (_forks != null)
            {
                yield return RetractForks(_forks, _forkRestLocalZ);
                yield return LiftForks(_forks, _forkRestLocalY);
            }

            Restore();
        }

        // ── Abort ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>Cancels a task in progress: releases any reserved slot, resets the task to
        /// Pending, retracts forks to rest, and restores the NavAgent.</summary>
        private IEnumerator AbortRoutine(WorkTask task, string palletId, string reservedAddress)
        {
            _putawayLogic?.CancelPutaway(reservedAddress);
            if (task != null) task.Status = WorkTaskStatus.Pending;

            if (_forks != null)
            {
                yield return RetractForks(_forks, _forkRestLocalZ);
                yield return LiftForks(_forks, _forkRestLocalY);
            }

            Restore();
        }

        // ── NavAgent Control ──────────────────────────────────────────────────────────────────────

        /// <summary>Disable NavAgent and AiNavigation for direct-Transform control.</summary>
        private void Commandeer()
        {
            if (_vehicleNav != null)
            {
                _vehicleNav.CancelSeekPosition();
                _vehicleNav.enabled = false;
            }
            if (_vehicleAgent != null)
            {
                _vehicleAgent.isStopped = true;
                _vehicleAgent.enabled   = false;
            }
        }

        /// <summary>Re-enable NavAgent and AiNavigation without resetting _busy.
        /// Used for mid-task transitions between commandeered and NavAgent legs.</summary>
        private void EnableNavAgent()
        {
            if (_vehicleAgent != null)
            {
                _vehicleAgent.enabled = true;
                if (_vehicleAgent.isActiveAndEnabled && _vehicleAgent.isOnNavMesh)
                {
                    _vehicleAgent.Warp(transform.position);
                    _vehicleAgent.isStopped = false;
                }
            }
            if (_vehicleNav != null)
                _vehicleNav.enabled = true;
        }

        /// <summary>Fully restore NavAgent patrol and mark this operator as free.</summary>
        private void Restore()
        {
            EnableNavAgent();
            _vehicleNav?.GoToRandomWaypoint();
            _busy = false;
        }

        // ── Movement Primitives ───────────────────────────────────────────────────────────────────

        /// <summary>Rotate in place so forks face the target, then drive straight to it.</summary>
        private IEnumerator DriveForksFirst(Transform t, Vector3 target)
        {
            Vector3 flat = new Vector3(target.x, t.position.y, target.z);
            Vector3 to   = flat - t.position; to.y = 0f;
            if (to.magnitude <= ArriveThreshold) { t.position = flat; yield break; }

            // Rotate in place: body forward = BodyForwardForForks(direction to target)
            Quaternion want = Quaternion.LookRotation(BodyForwardForForks(to.normalized));
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.rotation = want;

            // Drive straight (forks leading)
            while (true)
            {
                flat = new Vector3(target.x, t.position.y, target.z);
                to   = flat - t.position; to.y = 0f;
                if (to.magnitude <= ArriveThreshold) { t.position = flat; break; }
                t.position += t.forward * _forkAxisSign * DriveSpeed * Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>Rotate in place so the forks point along <paramref name="worldForkDir"/>.</summary>
        private IEnumerator FaceForks(Transform t, Vector3 worldForkDir)
        {
            if (worldForkDir.sqrMagnitude < 1e-6f) yield break;
            Quaternion want = Quaternion.LookRotation(BodyForwardForForks(worldForkDir.normalized));
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.rotation = want;
        }

        /// <summary>Rotate in place so the vehicle body faces <paramref name="worldDir"/>
        /// (used for travel pivots where the forks axis is not relevant).</summary>
        private IEnumerator RotateTo(Transform t, Vector3 worldDir)
        {
            if (worldDir.sqrMagnitude < 1e-6f) yield break;
            Quaternion want = Quaternion.LookRotation(worldDir.normalized);
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.rotation = want;
        }

        /// <summary>Translate directly toward <paramref name="target"/> without rotating.
        /// Used when reversing out of a lane after grabbing a pallet.</summary>
        private IEnumerator ReverseToPoint(Transform t, Vector3 target)
        {
            Vector3 flat = new Vector3(target.x, t.position.y, target.z);
            while (true)
            {
                Vector3 to = flat - t.position; to.y = 0f;
                if (to.magnitude <= ArriveThreshold) { t.position = flat; break; }
                t.position = Vector3.MoveTowards(t.position, flat, DriveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>Drive forks-first until the forks are within grab variance of the pallet XZ.
        /// A safety cap of <see cref="MaxLaneInsertTravel"/> prevents an infinite loop.</summary>
        private IEnumerator DriveToGrab(Transform pallet)
        {
            Vector3 origin = transform.position;
            while ((transform.position - origin).magnitude < MaxLaneInsertTravel)
            {
                Vector3 forkPos = _forks != null ? _forks.position : transform.position;
                float   xzDist  = new Vector2(
                    forkPos.x - pallet.position.x,
                    forkPos.z - pallet.position.z).magnitude;

                if (xzDist <= _grabVariance) break;

                transform.position += transform.forward * _forkAxisSign * DriveSpeed * Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>Animate forks to <paramref name="targetLocalY"/> in local space.</summary>
        private IEnumerator LiftForks(Transform forks, float targetLocalY)
        {
            while (Mathf.Abs(forks.localPosition.y - targetLocalY) > 0.005f)
            {
                Vector3 lp = forks.localPosition;
                lp.y = Mathf.MoveTowards(lp.y, targetLocalY, _forkLiftSpeed * Time.deltaTime);
                forks.localPosition = lp;
                yield return null;
            }
            Vector3 final = forks.localPosition;
            final.y = targetLocalY;
            forks.localPosition = final;
        }

        /// <summary>Animate forks until their world Y equals <paramref name="targetWorldY"/>.</summary>
        private IEnumerator LiftForksToWorldY(Transform forks, float targetWorldY)
        {
            float targetLocalY = forks.parent != null
                ? forks.parent.InverseTransformPoint(
                    new Vector3(forks.position.x, targetWorldY, forks.position.z)).y
                : targetWorldY;
            yield return LiftForks(forks, targetLocalY);
        }

        /// <summary>Retract forks to their resting local Z position.</summary>
        private IEnumerator RetractForks(Transform forks, float targetLocalZ)
        {
            while (Mathf.Abs(forks.localPosition.z - targetLocalZ) > 0.005f)
            {
                Vector3 lp = forks.localPosition;
                lp.z = Mathf.MoveTowards(lp.z, targetLocalZ, _forkExtendSpeed * Time.deltaTime);
                forks.localPosition = lp;
                yield return null;
            }
            Vector3 final = forks.localPosition;
            final.z = targetLocalZ;
            forks.localPosition = final;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────────────────

        /// <summary>The vehicle body forward direction that places forks along
        /// <paramref name="worldForkDir"/>.</summary>
        private Vector3 BodyForwardForForks(Vector3 worldForkDir) => worldForkDir * _forkAxisSign;

        /// <summary>Zero the Y component of <paramref name="v"/> and normalize.
        /// Returns <see cref="Vector3.forward"/> for near-zero inputs.</summary>
        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        /// <summary>
        /// Parse lane identifiers in any of the following formats into a door number and lane letter:
        /// <c>"STG1A"</c>, <c>"1A"</c>, <c>"STG1A-3"</c>, <c>"1A-3"</c>.
        /// </summary>
        private static bool TryParseLaneName(string raw, out int door, out string lane)
        {
            door = 0;
            lane = null;
            if (string.IsNullOrEmpty(raw)) return false;

            // Strip optional "STG" prefix
            string s = raw.StartsWith("STG", System.StringComparison.OrdinalIgnoreCase)
                ? raw.Substring(3) : raw;

            // Strip optional "-N" slot suffix
            int dash = s.IndexOf('-');
            if (dash >= 0) s = s.Substring(0, dash);

            // Split leading digits (door) from trailing letters (lane)
            int numEnd = 0;
            while (numEnd < s.Length && char.IsDigit(s[numEnd])) numEnd++;
            if (numEnd == 0 || numEnd >= s.Length) return false;

            if (!int.TryParse(s.Substring(0, numEnd), out door)) return false;
            lane = s.Substring(numEnd);
            return !string.IsNullOrEmpty(lane);
        }

        /// <summary>Scans the lane from the exit inward and returns the first pallet found
        /// (i.e., the one closest to the exit and reachable without obstruction).</summary>
        private Transform FindExitPallet(int door, string lane, out string palletId)
        {
            palletId = null;
            List<LaneNamingService.LaneSlot> slots = LaneNamingService.GetLane(door, lane);

            for (int i = slots.Count - 1; i >= 0; i--)
            {
                List<PalletMasterRecord> pallets = _inventoryService?.GetPalletsAtLocation(slots[i].Cell);
                if (pallets == null || pallets.Count == 0) continue;

                // Topmost pallet is the last element (added last = on top of the stack)
                PalletMasterRecord topmost = pallets[pallets.Count - 1];
                PalletMasterLink   link    = PalletMasterLink.Find(topmost.PalletId);
                if (link == null) continue;

                palletId = topmost.PalletId;
                return link.transform;
            }

            return null;
        }

        /// <summary>Returns the ChepAnchor whose world position has the larger dot product along
        /// <paramref name="depthAxis"/> (i.e., the anchor closer to the lane exit).</summary>
        private static Transform PickExitFacingAnchor(Transform front, Transform rear, Vector3 depthAxis)
        {
            if (front == null && rear == null) return null;
            if (front == null) return rear;
            if (rear  == null) return front;
            return Vector3.Dot(front.position, depthAxis) >= Vector3.Dot(rear.position, depthAxis)
                ? front : rear;
        }

        /// <summary>Find a Location child by its address name using a scene-wide
        /// <see cref="GameObject.Find"/> (names are unique slot addresses like "01-02-A0").</summary>
        private static Transform FindLocationTransform(string address)
        {
            GameObject go = GameObject.Find(address);
            return go != null ? go.transform : null;
        }

        /// <summary>Returns the LocApproachAnchor child of the named Location GameObject.</summary>
        private static Transform FindLocApproachAnchor(string address)
        {
            Transform loc = FindLocationTransform(address);
            return loc != null ? loc.Find(LocApproachAnchorName) : null;
        }

        /// <summary>Parents <paramref name="obj"/> to a root container of <paramref name="containerName"/>,
        /// creating it at the scene root if it doesn't yet exist.</summary>
        private static void EnsureUnderContainer(Transform obj, string containerName)
        {
            if (obj == null) return;
            if (obj.parent != null && obj.parent.name == containerName) return;

            GameObject container = GameObject.Find(containerName);
            if (container == null)
            {
                container = new GameObject(containerName);
                container.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            obj.SetParent(container.transform, worldPositionStays: true);
        }

        /// <summary>Depth-first child search by exact name across the entire sub-hierarchy.</summary>
        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;
                Transform found = FindDeepChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
