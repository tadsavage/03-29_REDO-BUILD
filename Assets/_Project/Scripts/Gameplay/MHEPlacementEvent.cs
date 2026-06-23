using System;
using UnityEngine;

/// <summary>
/// Static event system for MHE (Material Handling Equipment) placement notifications.
/// When a Reach Truck, Dock Stocker, or other equipment is placed in the world,
/// this broadcasts so idle operators can detect and seek it.
/// </summary>
public static class MHEPlacementEvent
{
    /// <summary>Fired when MHE equipment is placed/enabled in the world.</summary>
    public static event Action<MHEOperatorSlot> OnMHEEquipmentPlaced;

    /// <summary>Call this when equipment is placed (from MHEOperatorSlot.OnEnable).</summary>
    public static void BroadcastEquipmentPlaced(MHEOperatorSlot slot)
    {
        OnMHEEquipmentPlaced?.Invoke(slot);
    }
}
