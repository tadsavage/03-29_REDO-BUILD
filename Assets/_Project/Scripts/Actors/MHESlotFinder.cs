using UnityEngine;

/// <summary>
/// Shared "find an unoccupied MHE matching this equipment type" scan, used by both
/// EmployeeSpawner (auto-board on hire/save-restore) and EmployeeAssignmentService
/// (manual Drive Reach/Drive Dockstalker assignment) so the lookup logic lives in one place.
/// </summary>
public static class MHESlotFinder
{
    public static MHEOperatorSlot FindUnoccupied(ObjDataSO targetData)
    {
        if (targetData == null) return null;

        foreach (var slot in Object.FindObjectsByType<MHEOperatorSlot>())
        {
            if (slot.IsOccupied) continue;
            var vehicleObj = slot.GetComponent<PlacedObject>();
            if (vehicleObj == null || vehicleObj.data != targetData) continue;
            return slot;
        }

        return null;
    }
}
