using UnityEngine;
using System;

/// <summary>
/// Event fired when a rack is placed during build mode.
/// Allows collection detection and other systems to react to rack placement.
/// </summary>
public static class RackPlacedEvent
{
    public static event Action<GameObject> OnRackPlaced;

    public static void Fire(GameObject rackGO)
    {
        Debug.Log($"RackPlacedEvent.Fire() - firing event for {rackGO.name}, subscribers: {OnRackPlaced?.GetInvocationList().Length ?? 0}");
        OnRackPlaced?.Invoke(rackGO);
    }
}
