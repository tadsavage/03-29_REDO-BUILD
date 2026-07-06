using UnityEngine;

/// <summary>
/// Single authority for manually (re)assigning an employee's current job behavior, driven by
/// the Actions dropdown (EmployeeInfoUI). Mirrors EmployeeTerminationService's role as a
/// static, no-Inspector-wiring shared service rather than a MonoBehaviour singleton.
/// </summary>
public static class EmployeeAssignmentService
{
    /// <summary>Fired after an assignment is successfully applied — UI can use this to refresh
    /// without polling. No subscribers yet; reserved for roster/info-card live-refresh.</summary>
    public static event System.Action<EmployeeIdentity, EmployeeAssignment> OnAssignmentChanged;

    private static EmployeeSpawner _cachedSpawner;
    private static bool _spawnerSearched;

    public static void Assign(EmployeeIdentity identity, EmployeeAssignment assignment)
    {
        if (identity == null || identity.Record == null) return;

        // Leaving ReceiveInbound for anything else must strip the RF gun/clipboard and stop the
        // work-queue polling loop — done centrally here so every other branch below (including a
        // failed DriveReach/DriveDockstalker attempt) doesn't have to remember it individually.
        if (assignment != EmployeeAssignment.ReceiveInbound)
        {
            ReceivingEquipmentService.Unequip(identity);
            RemoveReceivingDriver(identity);
        }

        switch (assignment)
        {
            case EmployeeAssignment.Patrol:
                AssignPatrol(identity);
                break;
            case EmployeeAssignment.DriveReach:
                AssignDrive(identity, assignment, GetEquipmentData(reach: true));
                break;
            case EmployeeAssignment.DriveDockstalker:
                AssignDrive(identity, assignment, GetEquipmentData(reach: false));
                break;
            case EmployeeAssignment.OrderSelection:
                // No order/picking system exists yet — record the intent and stop there.
                SetAssignment(identity, assignment);
                break;
            case EmployeeAssignment.ReceiveInbound:
                AssignReceiving(identity);
                break;
        }
    }

    private static void AssignPatrol(EmployeeIdentity identity)
    {
        // Boarded operators must vacate before resuming their own patrol.
        identity.AssignedSlot?.VacateOperator();

        identity.GetComponent<AiNavigation>()?.Patrol();

        SetAssignment(identity, EmployeeAssignment.Patrol);
    }

    private static void AssignReceiving(EmployeeIdentity identity)
    {
        // Boarded operators must vacate before receiving.
        identity.AssignedSlot?.VacateOperator();

        // Equip RF gun and clipboard
        ReceivingEquipmentService.Equip(identity);

        // Drives the actual claim-task/walk-to-pallet/receive loop; stays patrolling between tasks.
        if (identity.GetComponent<GameCore.Actors.ReceivingTaskDriver>() == null)
            identity.gameObject.AddComponent<GameCore.Actors.ReceivingTaskDriver>();

        identity.GetComponent<AiNavigation>()?.Patrol();

        SetAssignment(identity, EmployeeAssignment.ReceiveInbound);
    }

    private static void RemoveReceivingDriver(EmployeeIdentity identity)
    {
        var driver = identity.GetComponent<GameCore.Actors.ReceivingTaskDriver>();
        if (driver != null) Object.Destroy(driver);

        var workflow = identity.GetComponent<GameCore.Labor.ReceiverReceivingWorkflow>();
        if (workflow != null) Object.Destroy(workflow);
    }

    private static void AssignDrive(EmployeeIdentity identity, EmployeeAssignment assignment, ObjDataSO targetData)
    {
        var slot = MHESlotFinder.FindUnoccupied(targetData);
        if (slot == null)
        {
            UIToast.Show("Equipment not available for this role", 2f);
            return;
        }

        // Already riding a different vehicle — vacate it first.
        identity.AssignedSlot?.VacateOperator();

        var nav = identity.GetComponent<AiNavigation>();
        if (nav == null) return;

        nav.SeekEquipment(slot);
        SetAssignment(identity, assignment);
    }

    private static void SetAssignment(EmployeeIdentity identity, EmployeeAssignment assignment)
    {
        identity.Record.currentAssignment = assignment;
        OnAssignmentChanged?.Invoke(identity, assignment);
    }

    private static ObjDataSO GetEquipmentData(bool reach)
    {
        // Cache spawner on first access to avoid expensive FindAnyObjectByType calls
        if (!_spawnerSearched)
        {
            _cachedSpawner = Object.FindAnyObjectByType<EmployeeSpawner>();
            _spawnerSearched = true;
        }

        if (_cachedSpawner == null) return null;
        return reach ? _cachedSpawner.ReachTruckData : _cachedSpawner.DockStockerData;
    }
}
