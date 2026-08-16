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
        private int _agentAreaMask = NavMesh.AllAreas;

        private float _pollTimer;
        private bool _taskInProgress;

        /// <summary>How long _taskInProgress may disagree with the workflow's real state before the
        /// watchdog in Update() treats it as a stuck flag and recovers. Generous enough to cover the
        /// legitimate window between claiming a task and the workflow actually starting (the receiver
        /// still has to walk to the pallet first).</summary>
        private const float StuckFlagTimeout = 12f;
        private float _stuckFlagSeconds;

        /// <summary>
        /// The task claimed by the most recent TryClaimNextReceiveTask, held until the workflow
        /// reports it done.
        ///
        /// Exists so the deadlock watchdog can HAND THE TASK BACK. Before this the driver kept no
        /// reference to what it had claimed — the task went straight into a lambda — so recovering
        /// from a stuck flag un-wedged the employee and silently orphaned the work: the task stayed
        /// Assigned to her forever while she stood idle, and GetPendingTasksForRole only ever returns
        /// Available, so she could never see it again. Observed live: two Receive tasks Assigned for
        /// 250s with _taskInProgress=false and the workflow Idle.
        /// </summary>
        private WorkTask _claimedTask;
        private bool _holdingNearDock;
        private float _holdTimer;

        // The lane (door+letter, e.g. "1A") this receiver is currently working through. Sticky across
        // claims so a lane empties from its EXIT end inward (position 6, then 5, 4, 3, 2, 1) instead of
        // whichever pallet happens to be nearest — set the first time a lane is picked, cleared once
        // that lane has no pending Receive tasks left, per Tad's spec.
        private string _currentLaneKey;

        private void Awake()
        {
            _nav = GetComponent<AiNavigation>();
            _workflow = GetComponent<ReceiverReceivingWorkflow>();
            if (_workflow == null)
                _workflow = gameObject.AddComponent<ReceiverReceivingWorkflow>();

            var agent = GetComponent<NavMeshAgent>();
            if (agent != null) _agentAreaMask = agent.areaMask;

            _workflow.OnWorkflowComplete += HandleWorkflowComplete;
            ServiceLocator.TryGet(out _workQueue);
        }

        private void OnDestroy()
        {
            if (_workflow != null)
                _workflow.OnWorkflowComplete -= HandleWorkflowComplete;

            // A receiver who is fired, or wiped by a scene clear, must not take her claim with her.
            // Without this the task stays Assigned to an employee who no longer exists — unclaimable
            // by anyone, and invisible to every other receiver.
            ReleaseClaimedTask("receiver removed");
        }

        private void Update()
        {
            if (_nav == null) return;

            // ── Deadlock watchdog (self-healing) ──────────────────────────────────────────────────
            // _taskInProgress is cleared by HandleWorkflowComplete, which relies on the workflow's
            // OnWorkflowComplete event landing. If that event is ever missed — the workflow returns
            // to Idle by some other path, the subscription isn't live at that instant, or the seek
            // callback that starts it never fires — this flag sticks TRUE forever. Update() then
            // returns on the very first line every frame: the receiver never claims another task,
            // never patrols, and AiNavigation's taskBusy stays set so it won't re-dispatch her
            // either. Observed live: a Receiver stood motionless inside a rack indefinitely with
            // _taskInProgress=true while her workflow read Idle with no current task.
            //
            // So verify the flag against the workflow's real state rather than trusting it.
            if (_taskInProgress && _workflow != null && !_workflow.IsBusy)
            {
                _stuckFlagSeconds += Time.deltaTime;
                if (_stuckFlagSeconds >= StuckFlagTimeout)
                {
                    Debug.LogWarning($"[ReceivingTaskDriver] {name}: task flagged in-progress but the " +
                        $"workflow is Idle — clearing the stale flag and resuming (deadlock recovered).");
                    _stuckFlagSeconds = 0f;
                    // Release the claim BEFORE clearing the flag. Recovering the employee without
                    // giving the task back just moves the deadlock from her to the queue: she goes
                    // back to polling, the task stays Assigned, and GetPendingTasksForRole (Available
                    // only) can never show it to her again. That is the exact state this was found in.
                    ReleaseClaimedTask("receiver deadlock recovery");
                    _taskInProgress = false;
                    _holdingNearDock = false;
                    _nav.SetTaskBusy(false);
                    _nav.Patrol();
                }
            }
            else
            {
                _stuckFlagSeconds = 0f;
            }

            if (_taskInProgress) return;

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

            if (!TryClaimNextReceiveTask(out var task, out var palletTransform))
                return;

            _holdingNearDock = false;
            _taskInProgress = true;
            _claimedTask = task;
            _nav.SetTaskBusy(true);
            var standPosition = GetStandPosition(palletTransform);
            // The workflow only starts inside this arrival callback, so every way a seek can fail to
            // arrive — AiNavigation.SeekPosition silently no-oping on an already-set _seekingTask, a
            // NavMesh rebake knocking the agent off mid-leg, patrol hijacking the destination — leaves
            // the task claimed with nothing running. That is what the watchdog above now cleans up.
            _nav.SeekPosition(standPosition, () => _workflow.BeginReceivingAt(task, palletTransform));
        }

        /// <summary>
        /// Claims the next pending Receive task using exit-first, lane-sticky ordering (per Tad's
        /// spec): stay in the lane already being worked and always take its EXIT-most (highest slot
        /// number) pallet, so a six-position lane empties 6, 5, 4, 3, 2, 1 rather than in whatever
        /// order tasks happen to be sitting in the queue. Only once the current lane has nothing
        /// pending left does it pick a new lane — whichever one's own exit-most pallet is nearest to
        /// this receiver — so it naturally moves to another door once its current one is drained.
        /// Skips (and lets the normal cleanup path handle) any task whose physical pallet is gone.
        /// </summary>
        private bool TryClaimNextReceiveTask(out WorkTask task, out Transform palletTransform)
        {
            task = null;
            palletTransform = null;

            var pending = _workQueue.GetPendingTasksForRole(EmployeeRole.Receiver);
            if (pending.Count == 0) return false;

            // Resolve each candidate's physical pallet + lane slot up front. Same self-healing the
            // old nearest-task claim did: a task whose physical pallet is gone gets completed here
            // instead of piling up in the pending list forever.
            var candidateTasks = new System.Collections.Generic.List<WorkTask>();
            var candidateTransforms = new System.Collections.Generic.List<Transform>();
            var candidateSlots = new System.Collections.Generic.List<LaneNamingService.LaneSlot>();
            var candidateHasSlot = new System.Collections.Generic.List<bool>();
            System.Collections.Generic.List<WorkTask> orphaned = null;

            foreach (var candidate in pending)
            {
                var link = PalletMasterLink.Find(candidate.PalletId);
                if (link == null)
                {
                    (orphaned ??= new System.Collections.Generic.List<WorkTask>()).Add(candidate);
                    continue;
                }

                var po = link.GetComponent<PlacedObject>();
                LaneNamingService.LaneSlot slot = default;
                bool hasSlot = po != null && po.enabled
                               && LaneNamingService.TryGetSlot(new Vector2Int(po.gridX, po.gridY), out slot);

                candidateTasks.Add(candidate);
                candidateTransforms.Add(link.transform);
                candidateSlots.Add(slot);
                candidateHasSlot.Add(hasSlot);
            }

            if (orphaned != null)
            {
                foreach (var stale in orphaned)
                {
                    Debug.LogWarning($"[ReceivingTaskDriver] Pending Receive task for pallet {stale.PalletId} has no physical pallet — completing without receiving.");
                    ReceivingService.CompleteReceiveTask(stale.TaskId);
                }
            }

            if (candidateTasks.Count == 0) return false;

            int bestIndex = -1;

            // 1. Stay in the lane already being worked, if it still has a pending pallet — always the
            //    highest slot number (exit-most) among them.
            if (_currentLaneKey != null)
            {
                for (int i = 0; i < candidateTasks.Count; i++)
                {
                    if (!candidateHasSlot[i] || LaneKey(candidateSlots[i]) != _currentLaneKey) continue;
                    if (bestIndex < 0 || candidateSlots[i].Slot > candidateSlots[bestIndex].Slot) bestIndex = i;
                }
            }

            // 2. Sticky lane is drained (or none chosen yet) — pick whichever lane's own exit-most
            //    pallet is nearest to this receiver, then take THAT lane's exit-most task. Candidates
            //    with no resolvable lane slot (shouldn't normally happen for staged pallets) fall back
            //    to competing on raw nearest-distance, same as every other frontier candidate.
            if (bestIndex < 0)
            {
                Vector3 myPos = transform.position;

                // Per-lane frontier: index of the highest-slot candidate seen so far, keyed by lane.
                var frontierKeys = new System.Collections.Generic.List<string>();
                var frontierIndex = new System.Collections.Generic.List<int>();

                for (int i = 0; i < candidateTasks.Count; i++)
                {
                    if (!candidateHasSlot[i]) continue;
                    string key = LaneKey(candidateSlots[i]);
                    int existing = frontierKeys.IndexOf(key);
                    if (existing < 0)
                    {
                        frontierKeys.Add(key);
                        frontierIndex.Add(i);
                    }
                    else if (candidateSlots[i].Slot > candidateSlots[frontierIndex[existing]].Slot)
                    {
                        frontierIndex[existing] = i;
                    }
                }

                float bestSqr = float.PositiveInfinity;
                string bestKey = null;
                for (int f = 0; f < frontierIndex.Count; f++)
                {
                    int i = frontierIndex[f];
                    float sqr = (candidateTransforms[i].position - myPos).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; bestIndex = i; bestKey = frontierKeys[f]; }
                }

                // Fallback: any candidate with no resolvable lane slot competes on raw distance too.
                for (int i = 0; i < candidateTasks.Count; i++)
                {
                    if (candidateHasSlot[i]) continue;
                    float sqr = (candidateTransforms[i].position - myPos).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; bestIndex = i; bestKey = null; }
                }

                if (bestIndex >= 0) _currentLaneKey = bestKey;
            }

            if (bestIndex < 0) return false;

            var identity = GetComponent<EmployeeIdentity>();
            string guid = identity?.Record?.employeeGuid;
            if (string.IsNullOrEmpty(guid)) return false;

            if (!_workQueue.TryClaimSpecificTask(candidateTasks[bestIndex], guid)) return false; // lost a race

            task = candidateTasks[bestIndex];
            palletTransform = candidateTransforms[bestIndex];
            return true;
        }

        private static string LaneKey(LaneNamingService.LaneSlot slot) => $"{slot.DoorNumber}{slot.Lane}";

        /// <summary>
        /// Hands a claimed-but-unworked task back to the queue as Available so someone can pick it up
        /// again. No-op if nothing is claimed, or if the task already finished normally.
        ///
        /// Deliberately clears AssignedToEmployeeGuid too: a task left pointing at an employee who
        /// isn't working it is what resume-on-load logic reads as "mine, in progress", which would
        /// re-strand it after a save/load rather than letting anyone claim it fresh.
        /// </summary>
        private void ReleaseClaimedTask(string why)
        {
            if (_claimedTask == null) return;

            if (_claimedTask.Status == WorkTaskStatus.Assigned)
            {
                _claimedTask.Status = WorkTaskStatus.Available;
                _claimedTask.AssignedToEmployeeGuid = null;
                Debug.LogWarning($"[ReceivingTaskDriver] {name}: released Receive task for pallet " +
                                 $"{_claimedTask.PalletId} back to the queue ({why}).");
            }
            _claimedTask = null;
        }

        private void HandleWorkflowComplete()
        {
            // Finished normally — the workflow completed the task itself, so there's nothing to hand
            // back; just stop tracking it so a later watchdog pass can't "release" a done task.
            _claimedTask = null;
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

                if (!NavMesh.SamplePosition(candidate, out var hit, NavSampleRadius, _agentAreaMask))
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
