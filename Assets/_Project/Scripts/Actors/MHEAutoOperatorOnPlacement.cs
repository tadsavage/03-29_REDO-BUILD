using UnityEngine;

/// <summary>
/// Lives on PltJack_FULL alongside MHEOperatorSlot. It USED to spawn a random free employee to drive a
/// freshly placed pallet jack around the patrol loop. Since 2026-10-16 pallet jacks belong to ORDER
/// SELECTORS: a placed jack sits parked until a selector claims it for an order (see
/// OrderSelectionTaskDriver.PalletJack.cs). Kept as a no-op so the prefab reference stays valid.
/// </summary>
[RequireComponent(typeof(MHEOperatorSlot))]
public class MHEAutoOperatorOnPlacement : MonoBehaviour
{
    private void Start() => enabled = false;
}
