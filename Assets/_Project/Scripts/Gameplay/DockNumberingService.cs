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
        if (_instance != null) return;   // Unity's overloaded == treats a destroyed instance as null
        var go = new GameObject("[DockNumberingService]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DockNumberingService>();
    }

    private bool _subscribed;

    private void OnEnable() => TrySubscribe();

    // EventManager may not exist yet at AfterSceneLoad; keep trying until it does.
    private void Update()
    {
        if (!_subscribed) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var em = EventManager.Instance;
        if (em == null) return;

        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnObjectChanged);
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnObjectChanged);
        _subscribed = true;

        DockSlot.AssignDoorNumbers();   // initial pass for anything already present
    }

    private void OnObjectChanged(string eventId, PlacedObject placedObj) => DockSlot.AssignDoorNumbers();

    private void OnDestroy()
    {
        var em = EventManager.Instance;
        if (em == null || !_subscribed) return;

        em.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnObjectChanged);
        em.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnObjectChanged);
    }
}
