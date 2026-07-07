using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snapshot of all pending work tasks.
/// Tasks survive save/load so employees can resume their work.
/// </summary>
[System.Serializable]
public class WorkQueuePersistenceData
{
    public List<WorkTaskSnapshot> tasks = new();

    [System.Serializable]
    public class WorkTaskSnapshot
    {
        public string taskId = "";  // GUID from WorkTask.TaskId
        public string type = "";  // "Receive", "Putaway", "Replenish", "OrderSelect", "Load"
        public string requiredRole = "";  // "DockStockerOperator", "ReachTruckOperator", etc.
        public string palletId = "";  // Load ID if applicable
        public string status = "";  // "Pending", "Assigned", "Complete"
        public int assignedToEmployeeGuid = -1;  // employee GUID if assigned, -1 if unassigned
    }
}
