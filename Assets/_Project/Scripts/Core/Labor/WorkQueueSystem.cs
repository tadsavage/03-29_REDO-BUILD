using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using GameCore.Services;
using UnityEngine;

namespace GameCore.Labor
{
    public enum WorkTaskType { Receive, Putaway, Replenish, OrderSelect, Load }
    public enum WorkTaskStatus { Pending, Assigned, Complete }

    /// <summary>A single unit of warehouse work, targeted at one EmployeeRole.</summary>
    public class WorkTask
    {
        public string TaskId { get; }
        public WorkTaskType Type { get; }
        public EmployeeRole RequiredRole { get; }
        public string PalletId { get; set; }
        public string Description { get; }
        public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Pending;
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
        public string FromLocation { get; }
        public string ToLocation { get; private set; }

        /// <summary>Assigns (or updates) the TO location. Used by PutawayLogic to lock the
        /// destination at RTO pickup time — the task is created with ToLocation null and filled
        /// in the moment the RTO physically claims the pallet.</summary>
        public void AssignToLocation(string address) => ToLocation = address;

        /// <summary>The storage area (Grocery, Perishable, or Frozen) of the item being worked on,
        /// pulled from the SKU's StorageArea. Used for routing/display and downstream employee specialization.</summary>
        public PalletData.AreaCategory Area { get; }

        /// <summary>Higher value = more urgent = claimed first (see ReachTruckOperator.TryClaimAndStart).
        /// Default 100 for Putaway; Replenish tasks default to 250 (ReplenishmentService.ReplenishPriority)
        /// since an empty pick slot blocks order picking and should jump the queue ahead of routine
        /// putaways. Exists so claim ordering is real data instead of a hardcoded UI string.</summary>
        public int Priority { get; }

        public const int DefaultPriority = 100;

        /// <summary>OrderData.OrderId this task is for — OrderSelect tasks only. Unlike
        /// Receive/Putaway/Replenish (which move one specific pallet, hence PalletId), an Order
        /// Selector works across multiple SKUs/locations building pallets FOR an order, so there's
        /// no single relevant PalletId at claim time — PalletId stays null for this task type and
        /// the driver looks the real OrderData (LineItems, etc.) up from OrderService by this id.</summary>
        public string OrderId { get; set; }

        public WorkTask(WorkTaskType type, EmployeeRole requiredRole, string palletId, string description,
            string fromLocation = null, string toLocation = null, PalletData.AreaCategory area = PalletData.AreaCategory.Grocery,
            int priority = DefaultPriority, string orderId = null)
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

        public void Initialize() { }

        public void Shutdown() => _tasks.Clear();

        public WorkTask CreateTask(WorkTaskType type, EmployeeRole requiredRole, string palletId, string description,
            string fromLocation = null, string toLocation = null, PalletData.AreaCategory area = PalletData.AreaCategory.Grocery,
            int priority = WorkTask.DefaultPriority, string orderId = null)
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

            var task = new WorkTask(type, requiredRole, palletId, description, fromLocation, toLocation, area, priority, orderId);
            _tasks.Add(task);
            OnTaskCreated?.Invoke(task);
            Debug.Log($"[WorkQueueSystem] + {description} (role: {requiredRole.DisplayName()}, area: {area})");
            return task;
        }

        public List<WorkTask> GetPendingTasksForRole(EmployeeRole role)
            => _tasks.Where(t => t.RequiredRole == role && t.Status == WorkTaskStatus.Pending).ToList();

        /// <summary>Claims the oldest pending task for a role (FIFO). Marks it Assigned.</summary>
        public bool TryClaimNextTask(EmployeeRole role, string employeeGuid, out WorkTask task)
        {
            task = _tasks.FirstOrDefault(t => t.RequiredRole == role && t.Status == WorkTaskStatus.Pending);
            if (task == null) return false;
            task.Status = WorkTaskStatus.Assigned;
            task.AssignedToEmployeeGuid = employeeGuid;
            task.AssignedAtRealtime = Time.realtimeSinceStartup;
            return true;
        }

        /// <summary>Claims a specific already-known task (e.g. one a caller picked by proximity
        /// rather than FIFO order — see ReceivingTaskDriver's nearest-pallet selection). Returns
        /// false without side effects if it's no longer Pending (already claimed by someone else).</summary>
        public bool TryClaimSpecificTask(WorkTask task, string employeeGuid)
        {
            if (task == null || task.Status != WorkTaskStatus.Pending) return false;
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
                    $"({t.Description}) — claimed by '{t.AssignedToEmployeeGuid}' {now - t.AssignedAtRealtime:F0}s ago with no completion. Reverting to Pending.");
                t.Status = WorkTaskStatus.Pending;
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
