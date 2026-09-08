using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Events;
using GameCore.Inventory;
using GameCore.Services;
using UnityEngine;

namespace GameCore.Labor
{
    /// <summary>NOTE: append new values at the END — persisted by ordinal in the save file.
    /// PalletPick moves ONE full pallet from a reserve slot straight to an outbound staging lane,
    /// for a bulk order that asked for full-pallet quantities. It's the outbound mirror of Putaway,
    /// and shares Replenish's reserve-extraction half.</summary>
    public enum WorkTaskType { Receive, Putaway, Replenish, OrderSelect, Load, PalletPick }

    /// <summary>Open: exists but not yet claimable -- OrderSelect and PalletPick tasks start here,
    /// created "in limbo" until the player releases them to a staging lane via the Work Queue panel.
    /// Available: claimable by an operator (every other task type's normal starting state, and what
    /// an Open order task becomes once released). Assigned: claimed, in progress. Complete: done.
    /// Cancelled: the work can no longer be done because the pallet it targeted was removed from
    /// inventory — kept in the list (greyed in the Work Queue panel) rather than deleted so a job that
    /// got dropped out from under an operator stays visible. NOTE: append new values at the END; these
    /// are persisted by ordinal in the save file.</summary>
    public enum WorkTaskStatus { Open, Available, Assigned, Complete, Cancelled }

    /// <summary>A single unit of warehouse work, targeted at one EmployeeRole.</summary>
    public class WorkTask
    {
        public string TaskId { get; }
        public WorkTaskType Type { get; }
        public EmployeeRole RequiredRole { get; }
        public string PalletId { get; set; }
        public string Description { get; }
        public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Available;
        public string AssignedToEmployeeGuid { get; set; }  // for persistence and tracking

        /// <summary>Time.realtimeSinceStartup when this task was last claimed (Status set to
        /// Assigned). Used by <see cref="WorkQueueSystem.ReleaseStaleAssignments"/> to detect a
        /// claim whose owning coroutine died without ever calling CompleteTask/reverting to
        /// Pending (GameObject disabled mid-routine, employee fired/vehicle destroyed mid-task,
        /// an uncaught exception) — that task would otherwise be permanently invisible to every
        /// operator (GetPendingTasksForRole only returns Pending; the "resume mine" check only
        /// matches the exact same guid).</summary>
        public float AssignedAtRealtime { get; set; }

        /// <summary>Where the pallet/work starts and ends, as human location labels (e.g. "STG1A",
        /// a reserve/pick address, or a door). FromLocation is immutable once set at creation.
        /// ToLocation starts null for Putaway tasks and is assigned by PutawayLogic at RTO pickup
        /// via <see cref="AssignToLocation"/>.</summary>
        public string FromLocation { get; private set; }
        public string ToLocation { get; private set; }

        /// <summary>Assigns (or updates) the TO location. Used by PutawayLogic to lock the
        /// destination at RTO pickup time — the task is created with ToLocation null and filled
        /// in the moment the RTO physically claims the pallet.</summary>
        public void AssignToLocation(string address) => ToLocation = address;

        /// <summary>
        /// Assigns the FROM location, for PalletPick only — where the Reach Truck actually found the
        /// pallet, resolved at claim time (see <see cref="SkuId"/> for why it can't be known sooner).
        ///
        /// Guarded by type rather than left open. FromLocation is immutable for every other task
        /// because Putaway once shipped with a malformed address baked in and became permanently
        /// unclaimable; a general setter would put that back within reach. PalletPick is the one type
        /// whose source genuinely isn't decided at creation.
        /// </summary>
        public void AssignFromLocation(string address)
        {
            if (Type != WorkTaskType.PalletPick)
            {
                Debug.LogError($"[WorkTask] Refusing to reassign FromLocation on a {Type} task — only " +
                               $"PalletPick resolves its source after creation.");
                return;
            }
            FromLocation = address;
        }

        /// <summary>The storage area (Grocery, Perishable, or Frozen) of the item being worked on,
        /// pulled from the SKU's StorageArea. Used for routing/display and downstream employee specialization.</summary>
        public PalletData.AreaCategory Area { get; }

        /// <summary>Higher value = more urgent = claimed first (see ReachTruckOperator.TryClaimAndStart
        /// and WorkQueueSystem.TryClaimNextTask). Default 100 for Putaway; Replenish tasks default to
        /// 250 (ReplenishmentService.ReplenishPriority) since an empty pick slot blocks order picking
        /// and should jump the queue ahead of routine putaways. Exists so claim ordering is real data
        /// instead of a hardcoded UI string.</summary>
        public int Priority { get; private set; }

        public const int DefaultPriority = 100;

        /// <summary>Updates this task's priority after creation -- e.g. the player reprioritizing an
        /// order from the Work Queue panel's per-row priority stepper. See
        /// WorkQueuePanel.AdjustPriorityForSelected / AdjustTaskPriority.</summary>
        public void SetPriority(int priority) => Priority = priority;

        /// <summary>OrderData.OrderId this task is for — OrderSelect tasks only. Unlike
        /// Receive/Putaway/Replenish (which move one specific pallet, hence PalletId), an Order
        /// Selector works across multiple SKUs/locations building pallets FOR an order, so there's
        /// no single relevant PalletId at claim time — PalletId stays null for this task type and
        /// the driver looks the real OrderData (LineItems, etc.) up from OrderService by this id.</summary>
        public string OrderId { get; set; }

        /// <summary>SKU this task is for — PalletPick tasks only, and the reason it exists.
        ///
        /// A PalletPick is filed the moment a bulk order arrives, but the reserve pallet it will
        /// actually take can't be chosen until the player releases the order: reserve stock moves in
        /// between (replenishment pulls pallets, putaway adds them), so a PalletId picked at creation
        /// would routinely name a pallet that has left the slot. The SKU is the durable half of the
        /// request; PalletId and FromLocation are filled in at claim time by the Reach Truck.</summary>
        public string SkuId { get; set; }

        public WorkTask(WorkTaskType type, EmployeeRole requiredRole, string palletId, string description,
            string fromLocation = null, string toLocation = null, PalletData.AreaCategory area = PalletData.AreaCategory.Grocery,
            int priority = DefaultPriority, string orderId = null, string skuId = null)
        {
            TaskId = Guid.NewGuid().ToString();
            Type = type;
            RequiredRole = requiredRole;
            PalletId = palletId;
            Description = description;
            FromLocation = fromLocation;
            ToLocation = toLocation;
            Area = area;
            Priority = priority;
            OrderId = orderId;
            SkuId = skuId;
        }
    }

    /// <summary>
    /// Central task queue for the core gameplay loop. Every warehouse action (putaway, replenishment,
    /// order selection, loading) is represented as a WorkTask targeted at an EmployeeRole. This is the
    /// critical-path system all six gameplay-loop chunks depend on — CHUNK 1 (Inbound) is the first
    /// producer (Putaway tasks created by ReceivingService); later chunks both consume these tasks
    /// (assignment/animation) and produce new ones (Replenish, OrderSelect, Load).
    ///
    /// Assignment to a specific employee/animation is NOT implemented yet — that's Chunk 2+. For now
    /// this only tracks task existence/status so the pipeline is observable and testable end-to-end.
    /// </summary>
    public class WorkQueueSystem : IService
    {
        private readonly List<WorkTask> _tasks = new();
        public IReadOnlyList<WorkTask> Tasks => _tasks;

        public static event Action<WorkTask> OnTaskCreated;
        public static event Action<WorkTask> OnTaskCompleted;

        public void Initialize()
        {
            // A WorkTask holds only a PalletId STRING, so nothing stopped it outliving the pallet it
            // pointed at. When that happened the task stayed claimable forever and got re-claimed on a
            // loop — ReleaseStaleAssignments hands a dead claim back out every ~90s — and every attempt
            // failed: "[ReceiverReceivingWorkflow] No master record for pallet ...", or worse, the Reach
            // Truck Operator recorded a rack slot as Occupied with a blank SKU and quantity 0.
            // InventoryService already announced every removal on OnPalletDestroyed; the event simply
            // had no subscribers anywhere in the project. This is that subscriber.
            InventoryService.OnPalletDestroyed += HandlePalletDestroyed;

            // THE STALE-CLAIM SWEEP IS THE QUEUE'S OWN JOB, not a tenant's.
            //
            // ReleaseStaleAssignments used to be called from exactly one place in the entire project:
            // ReachTruckOperator's poll loop. So the safety net for the WHOLE queue only existed if
            // the player happened to have a reach truck operator hired. Found live with no RTO in the
            // scene and two Receive tasks Assigned-but-abandoned for 250s — nothing was ever going to
            // release them, and the receiver polls only for Available, so she stood idle beside the
            // pallets she was supposed to be receiving.
            //
            // Hooked to the hour tick rather than a new heartbeat: the sim runs one in-game hour per
            // real minute, so this fires about every 60s against a 90s threshold — often enough to
            // recover promptly, rare enough to cost nothing.
            _eventManager = EventManager.Instance;
            _eventManager?.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
        }

        private EventManager _eventManager;

        private void OnHourChanged(string eventId, int newHour)
        {
            ReleaseStaleAssignments();
            WarnAboutUnstaffedWork(newHour);
        }

        /// <summary>Roles already warned about today, so the player is told once rather than hourly.</summary>
        private readonly HashSet<EmployeeRole> _unstaffedWarned = new();
        private int _unstaffedWarnDay = -1;

        /// <summary>
        /// SAYS SO WHEN WORK CANNOT BE DONE BY ANYONE.
        ///
        /// A queue full of Available tasks and nobody employed who can take them is silent, invisible,
        /// and fatal to the loop. Observed live on a real save: an order sat Pending behind an
        /// OrderSelect task with no Order Selector on the payroll at all, while revenue for the week
        /// ran $1,300 against $38,746 of expenses. Nothing anywhere said the building had no one who
        /// could pick a case. The player's only clue was that the number never went up.
        ///
        /// Deliberately keyed on ROLE NOT HIRED rather than "task is old". A task waiting because
        /// everyone is busy is a queue working correctly; a task waiting because the role doesn't
        /// exist in the building is a dead end, and only the second one is worth interrupting for.
        /// </summary>
        private void WarnAboutUnstaffedWork(int hour)
        {
            var registry = EmployeeRegistry.Instance;
            if (registry == null) return;

            ServiceLocator.TryGet(out GameCore.Economy.SimulationTimeService clock);
            int today = clock?.Day ?? 0;
            if (today != _unstaffedWarnDay)
            {
                _unstaffedWarnDay = today;
                _unstaffedWarned.Clear();
            }

            foreach (var group in _tasks.Where(t => t.Status == WorkTaskStatus.Available)
                                        .GroupBy(t => t.RequiredRole))
            {
                var role = group.Key;
                if (_unstaffedWarned.Contains(role)) continue;

                // IsAvailableForWork, not just Active — it also excludes the injured, and an injured
                // employee is exactly as able to clear this queue as one who was never hired.
                bool anyoneHired = registry.All.Any(e => e != null && e.Record != null &&
                                                         CanServe(e.Record.role, role) &&
                                                         e.Record.IsAvailableForWork);
                if (anyoneHired) continue;

                _unstaffedWarned.Add(role);
                int count = group.Count();
                UIToast.Show($"{count} job(s) waiting that only a {Pretty(role)} can do — and you " +
                             $"haven't hired one. Nothing on that queue will move until you do.", 5f);
                Debug.LogWarning($"[WorkQueueSystem] {count} Available task(s) require role {role}, " +
                                 $"which no active employee holds. That work cannot progress.");
            }
        }

        /// <summary>
        /// Can an employee of <paramref name="employeeRole"/> actually do work filed as
        /// <paramref name="requiredRole"/>?
        ///
        /// A TASK'S RequiredRole IS NOT ALWAYS THE ONLY ROLE THAT CAN DO IT. Load tasks are filed as
        /// <see cref="EmployeeRole.Loader"/> by OrderService, but TrailerLoadController accepts a
        /// DockStockerOperator too — they drive the same equipment, and RoleSpecificAssignment maps
        /// both roles to DriveDockstalker. The authority for this is TrailerLoadController's operator
        /// check (`role != DockStockerOperator && role != Loader` → skip); this mirrors it.
        ///
        /// Without this, the unstaffed-work warning nagged "you haven't hired a Loader" every day at a
        /// player whose DockStockerOperator was perfectly capable of loading the trailer — a false
        /// alarm on a warning whose entire value is that it only fires when something is genuinely
        /// impossible. **If a controller ever learns to accept a substitute role, add it here too.**
        /// </summary>
        private static bool CanServe(EmployeeRole employeeRole, EmployeeRole requiredRole)
        {
            if (employeeRole == requiredRole) return true;

            // Loading: either dock-equipment role can run the trailer.
            if (requiredRole == EmployeeRole.Loader && employeeRole == EmployeeRole.DockStockerOperator)
                return true;

            return false;
        }

        /// <summary>Role name with spaces, so a toast reads "Order Selector" not "OrderSelector".</summary>
        private static string Pretty(EmployeeRole role)
            => System.Text.RegularExpressions.Regex.Replace(role.ToString(), "(?<!^)([A-Z])", " $1");

        public void Shutdown()
        {
            // OnPalletDestroyed is a STATIC event — without this unsubscribe the handler outlives the
            // service and accumulates across domain reloads / play sessions.
            InventoryService.OnPalletDestroyed -= HandlePalletDestroyed;
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            _tasks.Clear();
        }

        private void HandlePalletDestroyed(PalletMasterRecord pallet)
        {
            if (pallet != null) CancelTasksForPallet(pallet.PalletId);
        }

        /// <summary>Cancel every not-yet-finished task targeting this pallet. Called whenever the
        /// pallet's inventory record is removed (picked to empty, contaminated, or cleared wholesale by
        /// the Tools window / a save restore) so no operator can claim work that can never succeed.</summary>
        public int CancelTasksForPallet(string palletId)
        {
            if (string.IsNullOrEmpty(palletId)) return 0;

            int cancelled = 0;
            foreach (var t in _tasks)
            {
                if (t.PalletId != palletId) continue;
                if (t.Status == WorkTaskStatus.Complete || t.Status == WorkTaskStatus.Cancelled) continue;
                t.Status = WorkTaskStatus.Cancelled;
                t.AssignedToEmployeeGuid = null; // release the claim so nothing tries to "resume mine"
                cancelled++;
            }

            if (cancelled > 0)
                Debug.Log($"[WorkQueueSystem] Cancelled {cancelled} task(s) for pallet {palletId} — its inventory record was removed.");
            return cancelled;
        }

        public WorkTask CreateTask(WorkTaskType type, EmployeeRole requiredRole, string palletId, string description,
            string fromLocation = null, string toLocation = null, PalletData.AreaCategory area = PalletData.AreaCategory.Grocery,
            int priority = WorkTask.DefaultPriority, string orderId = null, string skuId = null)
        {
            // RULE: Putaway tasks can only be created for pallets that have been fully received (have PalletData).
            // They must also have a valid FromLocation (staging lane).
            if (type == WorkTaskType.Putaway)
            {
                // Reject any raw Vector2Int.ToString() leak ("(4, 7)"), not just the (0,0) origin
                // case — LaneNamingService.AddressAt() falls back to that format whenever a lane's
                // geometry hasn't (re)computed yet for the pallet's cell, and a task created with
                // that FromLocation baked in is permanently unparseable (WorkTask.FromLocation is
                // immutable) and therefore permanently unclaimable by any Reach Truck Operator.
                if (string.IsNullOrEmpty(fromLocation) || fromLocation == "STG" ||
                    fromLocation.Contains("(") || fromLocation.Contains(")"))
                {
                    Debug.LogWarning($"[WorkQueueSystem] Denying Putaway task for {palletId} - invalid FromLocation: '{fromLocation}'");
                    return null;
                }

                var link = PalletMasterLink.Find(palletId);
                if (link == null || link.GetComponent<PalletData>() == null)
                {
                    Debug.LogWarning($"[WorkQueueSystem] Denying Putaway task for {palletId} - pallet not fully received or physical link missing.");
                    return null;
                }
            }

            var task = new WorkTask(type, requiredRole, palletId, description, fromLocation, toLocation, area, priority, orderId, skuId);
            _tasks.Add(task);
            OnTaskCreated?.Invoke(task);
            Debug.Log($"[WorkQueueSystem] + {description} (role: {requiredRole.DisplayName()}, area: {area})");
            return task;
        }

        public List<WorkTask> GetPendingTasksForRole(EmployeeRole role)
            => _tasks.Where(t => t.RequiredRole == role && t.Status == WorkTaskStatus.Available).ToList();

        /// <summary>Claims the highest-priority available task for a role, ties broken oldest-first
        /// (OrderByDescending is a stable sort, so equal-priority tasks fall back to FIFO -- the whole
        /// queue's original behavior before priority became player-editable). Marks it Assigned.</summary>
        public bool TryClaimNextTask(EmployeeRole role, string employeeGuid, out WorkTask task)
        {
            task = _tasks.Where(t => t.RequiredRole == role && t.Status == WorkTaskStatus.Available)
                         .OrderByDescending(t => t.Priority)
                         .FirstOrDefault();
            if (task == null) return false;
            task.Status = WorkTaskStatus.Assigned;
            task.AssignedToEmployeeGuid = employeeGuid;
            task.AssignedAtRealtime = Time.realtimeSinceStartup;
            return true;
        }

        /// <summary>Claims a specific already-known task (e.g. one a caller picked by proximity
        /// rather than FIFO order — see ReceivingTaskDriver's nearest-pallet selection). Returns
        /// false without side effects if it's not Available (still Open, or already claimed by
        /// someone else).</summary>
        public bool TryClaimSpecificTask(WorkTask task, string employeeGuid)
        {
            if (task == null || task.Status != WorkTaskStatus.Available) return false;
            task.Status = WorkTaskStatus.Assigned;
            task.AssignedToEmployeeGuid = employeeGuid;
            task.AssignedAtRealtime = Time.realtimeSinceStartup;
            return true;
        }

        public void CompleteTask(string taskId)
        {
            var task = _tasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task == null) return;
            task.Status = WorkTaskStatus.Complete;
            OnTaskCompleted?.Invoke(task);
            _tasks.Remove(task);
        }

        /// <summary>
        /// Self-healing sweep: any task still Assigned long after a routine could plausibly take
        /// (default 90s — a full putaway leg is normally well under 30s) gets released back to
        /// Pending so it becomes claimable again. Guards against a claim whose owning
        /// operator/coroutine died mid-task without ever calling CompleteTask or reverting to
        /// Pending itself — see the doc comment on WorkTask.AssignedAtRealtime. Cheap to call
        /// from any consumer's own poll loop (e.g. ReachTruckOperator already polls once/second).
        /// </summary>
        public void ReleaseStaleAssignments(float maxAgeSeconds = 90f)
        {
            float now = Time.realtimeSinceStartup;
            foreach (var t in _tasks)
            {
                if (t.Status != WorkTaskStatus.Assigned) continue;
                if (now - t.AssignedAtRealtime < maxAgeSeconds) continue;

                Debug.LogWarning($"[WorkQueueSystem] Releasing stale Assigned task {t.TaskId} " +
                    $"({t.Description}) — claimed by '{t.AssignedToEmployeeGuid}' {now - t.AssignedAtRealtime:F0}s ago with no completion. Reverting to Available.");
                t.Status = WorkTaskStatus.Available;
                t.AssignedToEmployeeGuid = null;
            }
        }

        // ============ PERSISTENCE (SAVE/LOAD) ============

        /// <summary>Get all tasks for persistence.</summary>
        public List<WorkTask> GetAllTasks()
        {
            return new List<WorkTask>(_tasks);
        }

        /// <summary>Clear all tasks (used before restoring from save).</summary>
        public void ClearAllTasks()
        {
            _tasks.Clear();
        }

        /// <summary>Register a task restored from save.</summary>
        public void RegisterRestoredTask(WorkTask task)
        {
            if (task != null)
                _tasks.Add(task);
        }
    }
}
