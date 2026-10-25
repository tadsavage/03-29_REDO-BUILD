using System.Collections.Generic;
using System.Linq;
using GameCore.Labor;
using GameCore.Services;
using UnityEngine;

/// <summary>
/// "Charge Battery" (right-click an unoccupied machine) files a <see cref="WorkTaskType.ChargeMHE"/> task on the Work Queue with
/// role ALL (Tad, 2026-10-18). The first employee who is patrolling or looking for work takes it, walks to the machine, drives it
/// to a charging station and parks it (EquipmentBattery.VacateTrip). If nobody is free, the NEAREST employee who is busy on foot
/// says "I guess I can do it...", finishes the task they are on, and then goes for the machine.
///
/// Self-bootstrapping hidden singleton like the other dock services. Charge tasks are transient: they are not saved.
/// </summary>
public class ChargeMheService : MonoBehaviour
{
    private const float PollSeconds = 1f;
    private const string IdPrefix = "MHE:";

    private static ChargeMheService _instance;
    private float _next;
    private readonly Dictionary<string, EmployeeIdentity> _pending = new();   // taskId -> employee who will go after their current task
    private readonly HashSet<string> _inProgress = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        foreach (var stale in Resources.FindObjectsOfTypeAll<ChargeMheService>())
            if (stale != null && stale != _instance) DestroyImmediate(stale.gameObject);
        if (_instance != null) return;
        var go = new GameObject("[ChargeMheService]") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ChargeMheService>();
    }

    private static string KeyFor(EquipmentBattery b) => IdPrefix + b.LinkId;

    private static bool ExcludedRole(EmployeeRole r)
        => r == EmployeeRole.Boss || r == EmployeeRole.Security || r == EmployeeRole.TruckDriver || r == EmployeeRole.Exterminator;

    /// <summary>Files the task for this machine. False (with a toast) when one is already open or the machine isn't eligible.</summary>
    public static bool Request(EquipmentBattery b)
    {
        if (b == null) return false;
        if (!ServiceLocator.TryGet(out WorkQueueSystem queue) || queue == null) return false;
        if (!b.CanRequestCharge) { UIToast.Show("That machine can't be sent to charge right now."); return false; }

        string key = KeyFor(b);
        if (queue.GetAllTasks().Any(t => t != null && t.Type == WorkTaskType.ChargeMHE && t.PalletId == key &&
                                         (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Assigned)))
        {
            UIToast.Show("A charge job for that machine is already on the Work Queue.");
            return false;
        }

        var task = queue.CreateTask(WorkTaskType.ChargeMHE, EmployeeRole.Boss, key, $"Charge {b.KindName.ToLowerInvariant()}");
        if (task == null) return false;
        task.AnyRole = true;
        return true;
    }

    private void Update()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + PollSeconds;
        if (!ServiceLocator.TryGet(out WorkQueueSystem queue) || queue == null) return;

        foreach (var task in queue.GetAllTasks().Where(t => t != null && t.Type == WorkTaskType.ChargeMHE).ToList())
        {
            var battery = ParseBattery(task);

            // A claimed job: keep its claim fresh (the queue's stale sweep would hand it back out after 90 s).
            if (task.Status == WorkTaskStatus.Assigned)
            {
                task.AssignedAtRealtime = Time.realtimeSinceStartup;
                if (!_inProgress.Contains(task.TaskId)) queue.CompleteTask(task.TaskId);   // left over (e.g. the trip ended)
                continue;
            }
            if (task.Status != WorkTaskStatus.Available) continue;

            if (battery == null || !battery.CanRequestCharge) { queue.CompleteTask(task.TaskId); _pending.Remove(task.TaskId); continue; }

            // Someone already said they'd do it: go once they are free.
            if (_pending.TryGetValue(task.TaskId, out var promised))
            {
                if (promised == null || promised.Record == null || !promised.isActiveAndEnabled) { _pending.Remove(task.TaskId); }
                else if (!HasAssignedTask(queue, promised) && promised.AssignedSlot == null) { Begin(queue, task, battery, promised); continue; }
                else continue;
            }

            var idle = FindIdle(queue, battery.transform.position);
            if (idle != null) { Begin(queue, task, battery, idle); continue; }

            var busy = FindNearestBusy(queue, battery.transform.position);
            if (busy != null)
            {
                _pending[task.TaskId] = busy;
                WorldSpeechBubbles.Show(busy.transform, "I guess I can do it...", 4f);
            }
        }
    }

    private void Begin(WorkQueueSystem queue, WorkTask task, EquipmentBattery battery, EmployeeIdentity who)
    {
        if (who.Record == null || !queue.TryClaimSpecificTask(task, who.Record.employeeGuid)) return;
        _pending.Remove(task.TaskId);
        _inProgress.Add(task.TaskId);
        string id = task.TaskId;
        battery.BeginChargeTask(who, () =>
        {
            _inProgress.Remove(id);
            if (ServiceLocator.TryGet(out WorkQueueSystem q) && q != null) q.CompleteTask(id);
        });
    }

    private static EquipmentBattery ParseBattery(WorkTask t)
    {
        if (t == null || string.IsNullOrEmpty(t.PalletId) || !t.PalletId.StartsWith(IdPrefix)) return null;
        return int.TryParse(t.PalletId.Substring(IdPrefix.Length), out int id) ? EquipmentBattery.FindByLinkId(id) : null;
    }

    private static bool HasAssignedTask(WorkQueueSystem queue, EmployeeIdentity e)
    {
        string guid = e.Record != null ? e.Record.employeeGuid : null;
        if (string.IsNullOrEmpty(guid)) return false;
        foreach (var t in queue.GetAllTasks())
            if (t != null && t.Type != WorkTaskType.ChargeMHE && t.Status == WorkTaskStatus.Assigned && t.AssignedToEmployeeGuid == guid) return true;
        return false;
    }

    private static bool Eligible(EmployeeIdentity e)
        => e != null && e.isActiveAndEnabled && e.Record != null && e.Record.IsAvailableForWork && e.name.StartsWith("Employee_")
           && !ExcludedRole(e.Record.role) && e.AssignedSlot == null && !EquipmentBattery.IsHelper(e)
           && !(e.GetComponent<AiNavigation>() is AiNavigation n && n.IsSeekingEquipment);

    /// <summary>Patrolling or waiting for work: on foot, no task of their own.</summary>
    private static EmployeeIdentity FindIdle(WorkQueueSystem queue, Vector3 near)
    {
        EmployeeIdentity best = null; float bestD = float.MaxValue;
        foreach (var e in EmployeeRegistry.Instance != null ? EmployeeRegistry.Instance.All : Enumerable.Empty<EmployeeIdentity>())
        {
            if (!Eligible(e) || HasAssignedTask(queue, e)) continue;
            float d = Vector3.Distance(e.transform.position, near);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    /// <summary>Nobody is free: the nearest employee on foot who is in the middle of a task.</summary>
    private EmployeeIdentity FindNearestBusy(WorkQueueSystem queue, Vector3 near)
    {
        var taken = new HashSet<EmployeeIdentity>(_pending.Values.Where(v => v != null));
        EmployeeIdentity best = null; float bestD = float.MaxValue;
        foreach (var e in EmployeeRegistry.Instance != null ? EmployeeRegistry.Instance.All : Enumerable.Empty<EmployeeIdentity>())
        {
            if (!Eligible(e) || taken.Contains(e) || !HasAssignedTask(queue, e)) continue;
            float d = Vector3.Distance(e.transform.position, near);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }
}
