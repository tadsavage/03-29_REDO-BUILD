using UnityEngine;

/// <summary>
/// Marker for a PlacedObject that must self-register in PlacedObjectRegistry even though it sits
/// under another PlacedObject in the Transform hierarchy (e.g. a Foundation's own default floor-tile
/// children) — an explicit opt-IN, so PlacedObject.OnEnable's existing nested-parent guard keeps its
/// current behavior for everything else (pallet cases, carried/WIP pallets, etc., all of which
/// already strip their own PlacedObject on spawn rather than depending on that guard) unchanged.
/// </summary>
public class RegisterDespiteNestedParent : MonoBehaviour
{
}
