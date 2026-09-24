using UnityEngine;
using GameCore.Events;

/// <summary>
/// Keeps ShippingDoor numbers in sync whenever objects are placed or deleted, independent
/// of whether a GuardShack / TruckYardManager exists in the scene.
///
/// Historically door numbering was driven solely by <see cref="TruckYardManager"/>, which
/// only exists on the guard-shack prefab — so with no guard shack placed, every door stayed
/// on its prefab default ("99"). This service bootstraps itself after scene load and renumbers
/// via the build events, so doors number themselves even before a guard shack is ever placed.
///
/// It complements <see cref="DockSlot.AssignDoorNumbers"/> (which also runs on dock register/
/// unregister): the events fire AFTER a ghost-placed door reaches its final position, which
/// is when the position-based sort is actually correct.
/// </summary>
public class DockNumberingService : MonoBehaviour
{
    private static DockNumberingService _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // HideInHierarchy + DontDestroyOnLoad, NOT HideAndDontSave: with Enter Play Mode Options (no domain
        // reload) a HideAndDontSave object survives exiting Play, so every session/recompile left another
        // copy running (6 found live 2026-09-23). This one is destroyed on Play exit; sweep any leftovers.
        foreach (var stale in Resources.FindObjectsOfTypeAll<DockNumberingService>())
            if (stale != null && stale != _instance) DestroyImmediate(stale.gameObject);
        if (_instance != null) return;
        var go = new GameObject("[DockNumberingService]") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DockNumberingService>();
    }

    private bool _subscribed;
    private EventManager _subscribedTo;

    private void OnEnable() => TrySubscribe();

    // EventManager may not exist yet at AfterSceneLoad; keep trying until it does. Also re-subscribe
    // if EventManager.Instance has been replaced since (new Play session / scene load).
    private void Update()
    {
        if (!_subscribed || EventManager.Instance != _subscribedTo) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var em = EventManager.Instance;
        if (em == null) { _subscribed = false; return; }

        _subscribedTo?.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnObjectChanged);
        _subscribedTo?.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnObjectChanged);
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnObjectChanged);
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnObjectChanged);
        _subscribedTo = em;
        _subscribed = true;

        DockSlot.AssignDoorNumbers();   // initial pass for anything already present
    }

    private void OnObjectChanged(string eventId, PlacedObject placedObj) => DockSlot.AssignDoorNumbers();

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_subscribedTo == null || !_subscribed) return;

        _subscribedTo.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnObjectChanged);
        _subscribedTo.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnObjectChanged);
    }
}
