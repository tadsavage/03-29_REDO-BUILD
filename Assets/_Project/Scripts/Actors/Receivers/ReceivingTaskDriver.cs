using UnityEngine;
using UnityEngine.AI;
using GameCore.Services;
using GameCore.Labor;
using GameCore.Inventory;

namespace GameCore.Actors
{
    /// <summary>
    /// Drives the "Go Receive Inbound" assignment for a dynamically-assigned employee — there is no
    /// dedicated Receiver prefab (RF gun + clipboard are equipped/unequipped by ReceivingEquipmentService
    /// instead). Added by EmployeeAssignmentService when an employee is assigned ReceiveInbound, removed
    /// when reassigned elsewhere.
    ///
    /// While idle, polls WorkQueueSystem for a Receive task once per second. On claiming one, walks the
    /// employee to the pallet via AiNavigation.SeekPosition (NOT a second NavMeshAgent — see that
    /// method's comment) and hands off to ReceiverReceivingWorkflow for the animation/fill-bar/data
    /// portion once arrived. Returns to normal patrol between tasks and whenever none are available —
    /// per design this employee stays assigned and keeps polling even with an empty queue.
    /// </summary>
    public class ReceivingTaskDriver : MonoBehaviour
    {
        private const float TaskPollInterval = 1f;

        // Sides are the pallet's own LOCAL right/left, not a hardcoded world axis — pallets are now
        // rotated to match each dock's lane direction (see TrailerOffloadController.DropPallet), and
        // docks can face either world axis depending on which wall they're on. A fixed Vector3.right/
        // left only happened to line up with "beside" the pallet at docks whose lane runs one
        // particular way; at the other orientation it landed the receiver in front of/behind it,
        // in the dock stocker's fork path. Local right/left is always perpendicular to the pallet's
        // own facing, regardless of which way that dock's lane runs.
        private const float StandoffDistance = 1.75f; // offset from the pallet's pivot, per Tad
        private const float NavSampleRadius = 1f;

        // How long to idle near the dock waiting for the next pallet before giving up and
        // resuming normal patrol — a safety valve in case MoreReceivingWorkExpected() guesses
        // wrong (e.g. the truck that looked docked departs without producing another task).
        private const float MaxHoldDuration = 20f;

        private AiNavigation _nav;
        private ReceiverReceivingWorkflow _workflow;
        private WorkQueueSystem _workQueue;

        private float _pollTimer;
        private bool _taskInProgress;
        private bool _holdingNearDock;
        private float _holdTimer;

        private void Awake()
        {
            _nav = GetComponent<AiNavigation>();
            _workflow = GetComponent<ReceiverReceivingWorkflow>();
            if (_workflow == null)
                _workflow = gameObject.AddComponent<ReceiverReceivingWorkflow>();

            _workflow.OnWorkflowComplete += HandleWorkflowComplete;
            ServiceLocator.TryGet(out _workQueue);
        }

        private void OnDestroy()
        {
            if (_workflow != null)
                _workflow.OnWorkflowComplete -= HandleWorkflowComplete;
        }

        private void Update()
        {
            if (_taskInProgress || _nav == null) return;

            if (_holdingNearDock)
            {
                _holdTimer += Time.deltaTime;
                if (_holdTimer >= MaxHoldDuration)
                {
                    // Nothing showed up in time — whatever we thought was still docked must have
                    // left without producing another task. Stop blocking patrol indefinitely.
                    _holdingNearDock = false;
                    _nav.SetTaskBusy(false);
                    _nav.Patrol();
                }
            }

            if (_workQueue == null)
            {
                ServiceLocator.TryGet(out _workQueue);
                if (_workQueue == null) return;
            }

            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = TaskPollInterval;

            if (!TryClaimNearestReceiveTask(out var task, out var palletTransform))
                return;

            _holdingNearDock = false;
            _taskInProgress = true;
            _nav.SetTaskBusy(true);
            var standPosition = GetStandPosition(palletTransform);
            _nav.SeekPosition(standPosition, () => _workflow.BeginReceivingAt(task, palletTransform));
        }

        /// <summary>Claims the CLOSEST pending Receive task to this receiver's current position,
        /// not the oldest (WorkQueueSystem.TryClaimNextTask is plain FIFO across the whole
        /// warehouse). Without this, a receiver standing at a lane with two more pallets sitting
        /// right in front of them would instead walk clear across the warehouse for an older
        /// pending task from a different door — exactly the "wanders off instead of finishing
        /// what's right there" behavior Tad reported 2026-07-05. Skips (and lets the normal
        /// cleanup path handle) any task whose physical pallet is already gone.</summary>
        private bool TryClaimNearestReceiveTask(out WorkTask task, out Transform palletTransform)
        {
            task = null;
            palletTransform = null;

            var pending = _workQueue.GetPendingTasksForRole(EmployeeRole.Receiver);
            if (pending.Count == 0) return false;

            Vector3 myPos = transform.position;
            float bestSqr = float.PositiveInfinity;
            WorkTask bestTask = null;
            Transform bestTransform = null;
            System.Collections.Generic.List<WorkTask> orphaned = null;

            foreach (var candidate in pending)
            {
                var link = PalletMasterLink.Find(candidate.PalletId);
                if (link == null)
                {
                    // Pallet's physical GameObject is gone but its task is still Pending. The old
                    // FIFO claim path used to self-heal this (claim -> notice link is null ->
                    // complete the task) — this nearest-task path replaced FIFO as the ONLY caller,
                    // so nothing was doing that cleanup anymore and orphaned tasks piled up in the
                    // pending list forever, silently skipped every poll. Enough of those (or the
                    // wrong ones) starves real work: exactly the "dock full of product but the
                    // receiver just stopped and wandered off" symptom Tad reported 2026-07-05.
                    (orphaned ??= new System.Collections.Generic.List<WorkTask>()).Add(candidate);
                    continue;
                }
                float sqr = (link.transform.position - myPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestTask = candidate;
                    bestTransform = link.transform;
                }
            }

            if (orphaned != null)
            {
                foreach (var stale in orphaned)
                {
                    Debug.LogWarning($"[ReceivingTaskDriver] Pending Receive task for pallet {stale.PalletId} has no physical pallet — completing without receiving.");
                    ReceivingService.CompleteReceiveTask(stale.TaskId);
                }
            }

            if (bestTask == null) return false;

            var identity = GetComponent<EmployeeIdentity>();
            string guid = identity?.Record?.employeeGuid;
            if (string.IsNullOrEmpty(guid)) return false;

            if (!_workQueue.TryClaimSpecificTask(bestTask, guid)) return false; // lost a race to another receiver

            task = bestTask;
            palletTransform = bestTransform;
            return true;
        }

        private void HandleWorkflowComplete()
        {
            _taskInProgress = false;

            if (MoreReceivingWorkExpected())
            {
                // Stay right where we are, near the dock/staging lane, instead of wandering off on
                // patrol — a dock stocker is still (or about to be) dropping more pallets, and
                // walking clear across the warehouse just to walk straight back doesn't keep up.
                // Keeping AiNavigation's task-busy flag set also stops AgentAnimation's own idle
                // wander coroutine from sending the agent off after its short idleDelay.
                _holdingNearDock = true;
                _holdTimer = 0f;
                return;
            }

            _holdingNearDock = false;
            _nav?.SetTaskBusy(false);
            // Resume the normal patrol loop between tasks — the next Update() tick will immediately
            // start polling for the next Receive task again.
            _nav?.Patrol();
        }

        /// <summary>True if a dock stocker is still (or about to be) unloading a truck, or a Receive
        /// task is already queued — either way, another pallet is likely imminent and the receiver
        /// should hold position rather than patrol away.</summary>
        private bool MoreReceivingWorkExpected()
        {
            if (_workQueue != null && _workQueue.GetPendingTasksForRole(EmployeeRole.Receiver).Count > 0)
                return true;

            foreach (var truck in FindObjectsByType<TruckController>())
            {
                if (truck.State == TruckController.TruckState.Docked)
                    return true;
            }

            return false;
        }

        /// <summary>Picks a point on the pallet's local +right or -right side (never its front/back,
        /// which is where the dock stocker's forks travel) to stand while receiving, preferring
        /// whichever side has no other tracked pallet nearby. Falls back to the first side whose
        /// candidate point actually resolves on the NavMesh.</summary>
        private Vector3 GetStandPosition(Transform palletTransform)
        {
            Vector3[] sideDirections = { palletTransform.right, -palletTransform.right };

            Vector3 fallback = palletTransform.position + sideDirections[0] * StandoffDistance;
            bool haveFallback = false;
            Vector3 best = fallback;
            float bestClearance = float.NegativeInfinity;

            foreach (var dir in sideDirections)
            {
                Vector3 candidate = palletTransform.position + dir * StandoffDistance;

                if (!NavMesh.SamplePosition(candidate, out var hit, NavSampleRadius, NavMesh.AllAreas))
                    continue; // not walkable at all — never pick this side
                candidate = hit.position;

                if (!haveFallback) { fallback = candidate; haveFallback = true; }

                float nearestOtherPallet = float.PositiveInfinity;
                foreach (var link in PalletMasterLink.All)
                {
                    if (link == null || link.transform == palletTransform) continue;
                    float d = Vector3.Distance(candidate, link.transform.position);
                    if (d < nearestOtherPallet) nearestOtherPallet = d;
                }

                if (nearestOtherPallet > bestClearance)
                {
                    bestClearance = nearestOtherPallet;
                    best = candidate;
                }
            }

            return haveFallback ? best : fallback;
        }
    }
}
