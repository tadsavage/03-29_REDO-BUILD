using System;
using System.Collections.Generic;
using System.Linq;
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
        public string PalletId { get; }
        public string Description { get; }
        public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Pending;

        public WorkTask(WorkTaskType type, EmployeeRole requiredRole, string palletId, string description)
        {
            TaskId = Guid.NewGuid().ToString();
            Type = type;
            RequiredRole = requiredRole;
            PalletId = palletId;
            Description = description;
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

        public WorkTask CreateTask(WorkTaskType type, EmployeeRole requiredRole, string palletId, string description)
        {
            var task = new WorkTask(type, requiredRole, palletId, description);
            _tasks.Add(task);
            OnTaskCreated?.Invoke(task);
            Debug.Log($"[WorkQueueSystem] + {description} (role: {requiredRole.DisplayName()})");
            return task;
        }

        public List<WorkTask> GetPendingTasksForRole(EmployeeRole role)
            => _tasks.Where(t => t.RequiredRole == role && t.Status == WorkTaskStatus.Pending).ToList();

        /// <summary>Claims the oldest pending task for a role (FIFO). Marks it Assigned.</summary>
        public bool TryClaimNextTask(EmployeeRole role, out WorkTask task)
        {
            task = _tasks.FirstOrDefault(t => t.RequiredRole == role && t.Status == WorkTaskStatus.Pending);
            if (task == null) return false;
            task.Status = WorkTaskStatus.Assigned;
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
    }
}
