using GameCore.Labor;
using GameCore.Services;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles saving and loading of work queue state.
/// Tasks survive save/load so employees can resume their work.
/// </summary>
public static class WorkQueuePersistenceService
{
    /// <summary>
    /// Snapshot the current work queue state.
    /// </summary>
    public static WorkQueuePersistenceData Snapshot()
    {
        var data = new WorkQueuePersistenceData();
        var workQueue = ServiceLocator.Get<WorkQueueSystem>();

        if (workQueue == null)
        {
            Debug.LogWarning("[WorkQueuePersistenceService] WorkQueueSystem not found.");
            return data;
        }

        // Snapshot all tasks
        var allTasks = workQueue.GetAllTasks();
        foreach (var task in allTasks)
        {
            if (task == null) continue;

            data.tasks.Add(new WorkQueuePersistenceData.WorkTaskSnapshot
            {
                taskId = task.TaskId,
                type = task.Type.ToString(),
                requiredRole = task.RequiredRole.ToString(),
                palletId = task.PalletId ?? "",
                status = task.Status.ToString(),
                assignedToEmployeeGuid = task.AssignedToEmployeeGuid ?? -1
            });
        }

        return data;
    }

    /// <summary>
    /// Restore work queue state from a saved snapshot.
    /// </summary>
    public static void Restore(WorkQueuePersistenceData data)
    {
        if (data == null || data.tasks == null)
        {
            Debug.LogWarning("[WorkQueuePersistenceService] No work queue data to restore.");
            return;
        }

        var workQueue = ServiceLocator.Get<WorkQueueSystem>();
        if (workQueue == null)
        {
            Debug.LogError("[WorkQueuePersistenceService] WorkQueueSystem not found during restore.");
            return;
        }

        // Clear existing tasks
        workQueue.ClearAllTasks();

        // Recreate each saved task
        foreach (var snap in data.tasks)
        {
            var task = new WorkTask(
                System.Enum.Parse<WorkTaskType>(snap.type),
                System.Enum.Parse<EmployeeRole>(snap.requiredRole),
                string.IsNullOrEmpty(snap.palletId) ? null : snap.palletId,
                snap.type  // use type as description placeholder
            )
            {
                Status = System.Enum.Parse<WorkTaskStatus>(snap.status),
                AssignedToEmployeeGuid = snap.assignedToEmployeeGuid >= 0 ? (int?)snap.assignedToEmployeeGuid : null
            };

            // Re-register the task with the work queue
            workQueue.RegisterRestoredTask(task);
        }

        Debug.Log($"[WorkQueuePersistenceService] Restored {data.tasks.Count} work tasks.");
    }
}
