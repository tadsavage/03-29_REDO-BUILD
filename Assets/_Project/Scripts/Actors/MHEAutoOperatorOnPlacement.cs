using UnityEngine;

/// <summary>
/// Lives on PltJack_FULL alongside MHEOperatorSlot. Reach Trucks and Dock Stockers only come into
/// being via hiring (EmployeeSpawner.AssignToMHE), but Pallet Jacks are placed directly from the
/// build bar — so placing one must auto-staff it with a random employee immediately rather than
/// waiting for a hire.
/// </summary>
[RequireComponent(typeof(MHEOperatorSlot))]
public class MHEAutoOperatorOnPlacement : MonoBehaviour
{
    private void Start()
    {
        var slot = GetComponent<MHEOperatorSlot>();
        if (slot.IsOccupied) return;

        var spawner = FindAnyObjectByType<EmployeeSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("[MHEAutoOperatorOnPlacement] No EmployeeSpawner in scene — pallet jack stays unmanned.");
            return;
        }

        var record = EmployeeGenerator.Generate(EmployeeGender.Random, "WHSE");
        var identity = spawner.SpawnEmployee(record);
        if (identity == null) return;

        slot.AssignOperator(identity);
    }
}
