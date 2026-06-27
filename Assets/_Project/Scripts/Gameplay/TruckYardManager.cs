using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using GameCore.Events;

/// <summary>
/// Lives on the guard shack prefab. Manages the yard: assigns door numbers at startup,
/// picks free docks, spawns trucks, tracks active count.
///
/// The guard shack prefab must have three named child GameObjects:
///   "SpawnPoint" — where trucks appear on the inbound road
///   "GateStop"   — where the truck idles for the guard inspection
///   "ExitPoint"  — off-screen departure point where the truck despawns
///
/// truckPrefab is the only Inspector assignment needed; everything else is derived
/// from the guard shack's own transform and named children.
/// </summary>
public class TruckYardManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject truckPrefab;
    [SerializeField] private GameObject guardPrefab;

    [Header("Gate queue")]
    [Tooltip("Spacing between trucks queued behind the gate (≈ trailer length + ~2m gap).")]
    [SerializeField] private float queueSpacing = 16f;

    private readonly List<TruckController> _gateQueue = new();

    private Transform       _spawnPoint;
    private Transform       _gateStop;
    private Transform       _gateEnterNoTurn;
    private Transform       _gateLeaveNoTurn;
    private Transform       _exitPoint;
    private Transform       _guardAnchors;
    private GuardController _guard;
    private int             _activeTrucks;

    private void Awake()
    {
        // Search recursively for the anchors to be robust against hierarchy changes
        _spawnPoint      = FindDeepChild("SpawnPoint");
        _gateStop        = FindDeepChild("GateStop");
        _gateEnterNoTurn = FindDeepChild("GateEnterNoTurn");
        _gateLeaveNoTurn = FindDeepChild("GateLeaveNoTurn");
        _exitPoint       = FindDeepChild("ExitPoint");
        _guardAnchors    = FindDeepChild("GuardAnchors");

        if (_spawnPoint == null) Debug.LogWarning("[TruckYardManager] 'SpawnPoint' child not found in hierarchy.");
        if (_gateStop   == null) Debug.LogWarning("[TruckYardManager] 'GateStop' child not found in hierarchy.");
        if (_exitPoint  == null) Debug.LogWarning("[TruckYardManager] 'ExitPoint' child not found in hierarchy.");
    }

    private Transform FindDeepChild(string childName)
    {
        // Check direct first for performance
        var direct = transform.Find(childName);
        if (direct != null) return direct;
        
        // Check under TruckAnchors
        var truck = transform.Find("TruckAnchors");
        if (truck != null)
        {
            var found = truck.Find(childName);
            if (found != null) return found;
        }
        
        // Check under GuardAnchors
        var guard = transform.Find("GuardAnchors");
        if (guard != null)
        {
            var found = guard.Find(childName);
            if (found != null) return found;
        }

        return null;
    }

    private void Start()
{
        AssignDoorNumbers();
        SpawnGuard();

        // Subscribe to object placement/deletion to re-assign door numbers dynamically
        var eventManager = GameCore.Events.EventManager.Instance;
        if (eventManager != null)
        {
            eventManager.Subscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnObjectPlacedOrDeleted);
            eventManager.Subscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnObjectPlacedOrDeleted);
        }
    }

    private void OnDestroy()
    {
        var eventManager = GameCore.Events.EventManager.Instance;
        if (eventManager != null)
        {
            eventManager.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnObjectPlacedOrDeleted);
            eventManager.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnObjectPlacedOrDeleted);
        }
    }

    private void OnObjectPlacedOrDeleted(string eventId, PlacedObject placedObj)
    {
        // Re-assign door numbers whenever any object is placed/deleted
        // (specifically for shipping doors, but harmless to run always)
        AssignDoorNumbers();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public int ActiveTrucks => _activeTrucks;

    /// <summary>Guard-shack stop position (where trucks pause for inspection). Null if missing.</summary>
    public Vector3? GuardShackStop => _gateStop != null ? (Vector3?)_gateStop.position : null;

    /// <summary>Yard exit position (where trucks leave the scene). Null if missing.</summary>
    public Vector3? YardExit => _exitPoint != null ? (Vector3?)_exitPoint.position : null;

    /// <summary>GuardAnchors/ExitPost — where a fired employee stops to wave. Null if missing.</summary>
    public Vector3? GuardExitPost => Pos(DeepFind(transform, "ExitPost"));

    /// <summary>GS_Main — the guard-shack body a fired employee faces while waving. Null if missing.</summary>
    public Vector3? GuardShackMain => Pos(DeepFind(transform, "GS_Main"));

    private static Vector3? Pos(Transform t) => t != null ? (Vector3?)t.position : null;

    private static Transform DeepFind(Transform parent, string childName)
    {
        foreach (Transform c in parent)
        {
            if (c.name == childName) return c;
            var r = DeepFind(c, childName);
            if (r != null) return r;
        }
        return null;
    }

    public void ResetYard()
    {
        _activeTrucks = 0;
        _gateQueue.Clear();
        foreach (var dock in DockSlot.All)
        {
            dock.Release();
        }
        
        var trucks = Object.FindObjectsByType<TruckController>();
        foreach (var t in trucks)
        {
            Destroy(t.gameObject);
        }
        
    }

    public void SpawnNextTruck()
{
        if (truckPrefab == null) { Debug.LogError("[TruckYardManager] Truck Prefab not assigned."); return; }
        if (_spawnPoint == null) { Debug.LogError("[TruckYardManager] SpawnPoint child missing from guard shack."); return; }

        var dock = FindFreeDock();
        if (dock == null)
        {
            Debug.LogWarning("[TruckYardManager] No free docks — truck not spawned.");
            return;
        }

        var go  = Instantiate(truckPrefab, _spawnPoint.position, _spawnPoint.rotation);
        go.name = $"Truck→Door{dock.DoorNumber}";

        var ctrl = go.GetComponent<TruckController>() ?? go.AddComponent<TruckController>();

        Vector3? gatePos       = _gateStop        != null ? (Vector3?)_gateStop.position        : null;
        Vector3? enterNoTurn   = _gateEnterNoTurn != null ? (Vector3?)_gateEnterNoTurn.position : null;
        Vector3? leaveNoTurn   = _gateLeaveNoTurn != null ? (Vector3?)_gateLeaveNoTurn.position : null;
        Vector3? exitPos       = _exitPoint       != null ? (Vector3?)_exitPoint.position       : null;

        ctrl.Init(gatePos, enterNoTurn, leaveNoTurn, exitPos, _guard, OnTruckExited);
        ctrl.OnClearedGate += () => OnTruckClearedGate(ctrl);
        ctrl.AssignAndGo(dock);

        if (_gateStop != null)
        {
            _gateQueue.Add(ctrl);
            LayoutQueue();   // assigns this truck its slot and re-lays the line
        }

        _activeTrucks++;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void SpawnGuard()
    {
        if (guardPrefab == null)
        {
            Debug.LogWarning("[TruckYardManager] Guard Prefab not assigned — gate guard disabled.");
            return;
        }
        if (_guardAnchors == null)
        {
            Debug.LogWarning("[TruckYardManager] 'GuardAnchors' child not found on guard shack — gate guard disabled.");
            return;
        }

        var posted     = _guardAnchors.Find("Posted");
        var exitPost   = _guardAnchors.Find("ExitPost");
        var gateStop   = _guardAnchors.Find("GateStop");
        var checkRear1 = _guardAnchors.Find("CheckRear1");
        var checkRear2 = _guardAnchors.Find("CheckRear2");

        if (posted == null)
        {
            Debug.LogWarning("[TruckYardManager] 'GuardAnchors/Posted' not found — guard not spawned.");
            return;
        }

        var go = Instantiate(guardPrefab, posted.position, posted.rotation);
        go.name = "Guard";
        _guard  = go.GetComponent<GuardController>() ?? go.AddComponent<GuardController>();
        _guard.Init(posted, exitPost, gateStop, checkRear1, checkRear2);
    }

    private void AssignDoorNumbers()
    {
        var sorted = DockSlot.All
            .OrderBy(d => d.transform.position.x)
            .ThenBy(d  => d.transform.position.z)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
            sorted[i].DoorNumber = i + 1;
    }

    private void OnTruckExited()
    {
        _activeTrucks = Mathf.Max(0, _activeTrucks - 1);
    }

    // ── Gate queue ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Positions every queued truck behind the gate: slot 0 sits on GateStop, each
    /// truck behind it offset by queueSpacing along the inbound road. Slot 0 is the
    /// only one flagged "front" (it gets inspected on arrival).
    /// </summary>
    private void LayoutQueue()
    {
        if (_gateStop == null) return;

        Vector3 gate   = _gateStop.position;
        Vector3 behind = _spawnPoint != null ? (_spawnPoint.position - gate) : -_gateStop.forward;
        behind.y = 0f;
        if (behind.sqrMagnitude < 0.0001f) behind = -_gateStop.forward;
        behind.Normalize();

        for (int i = 0; i < _gateQueue.Count; i++)
        {
            var t = _gateQueue[i];
            if (t == null) continue;
            Vector3 slot = gate + behind * (i * queueSpacing);
            t.SetQueueSlot(slot, i == 0);
        }
    }

    /// <summary>Front truck cleared the guard → drop it and slide everyone forward.</summary>
    private void OnTruckClearedGate(TruckController ctrl)
    {
        _gateQueue.Remove(ctrl);
        LayoutQueue();
    }

    private DockSlot FindFreeDock()
    {
        var free = DockSlot.All.Where(d => !d.IsOccupied).ToList();
        return free.Count == 0 ? null : free[Random.Range(0, free.Count)];
    }
}
