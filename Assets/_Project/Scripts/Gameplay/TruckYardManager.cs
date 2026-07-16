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

    // ── Persistence API ───────────────────────────────────────────────────────

    /// <summary>The truck prefab used to spawn trucks. Exposed for TruckPersistenceService.</summary>
    public GameObject TruckPrefab => truckPrefab;

    /// <summary>Gate-stop waypoint position (Null if guard shack absent).</summary>
    public Vector3? GateStopPosition => _gateStop != null ? (Vector3?)_gateStop.position : null;

    /// <summary>GateEnterNoTurn waypoint position.</summary>
    public Vector3? GateEnterNoTurnPosition => _gateEnterNoTurn != null ? (Vector3?)_gateEnterNoTurn.position : null;

    /// <summary>GateLeaveNoTurn waypoint position.</summary>
    public Vector3? GateLeaveNoTurnPosition => _gateLeaveNoTurn != null ? (Vector3?)_gateLeaveNoTurn.position : null;

    /// <summary>Exit-point waypoint position.</summary>
    public Vector3? ExitWaypointPosition => _exitPoint != null ? (Vector3?)_exitPoint.position : null;

    /// <summary>The guard controller at the gate. May be null if no guard is present.</summary>
    public GuardController Guard => _guard;

    /// <summary>
    /// Wires a restored truck into this yard manager — injects waypoints and callbacks,
    /// subscribes to the gate-clear event, increments the active-truck counter, and
    /// optionally queues the truck in the gate queue (for trucks saved mid-queue).
    /// Call this for every truck restored by TruckPersistenceService before calling
    /// <see cref="FinalizeGateQueue"/> once all trucks have been registered.
    /// </summary>
    /// <param name="ctrl">The freshly-restored truck.</param>
    /// <param name="addToGateQueue">True if the truck was in Queuing/GuardCheck state at
    /// save time and should be added back to the physical gate queue.</param>
    public void RegisterRestoredTruck(TruckController ctrl, bool addToGateQueue)
    {
        ctrl.Init(
            GateStopPosition,
            GateEnterNoTurnPosition,
            GateLeaveNoTurnPosition,
            ExitWaypointPosition,
            _guard,
            OnTruckExited
        );

        ctrl.OnClearedGate += () => OnTruckClearedGate(ctrl);
        _activeTrucks++;

        if (addToGateQueue)
            _gateQueue.Add(ctrl);
    }

    /// <summary>
    /// Re-lays the gate queue after all restored trucks have been registered via
    /// <see cref="RegisterRestoredTruck"/>. Assigns correct slot positions to all queued trucks.
    /// </summary>
    public void FinalizeGateQueue() => LayoutQueue();

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
        
        // Find EVERY truck in the scene, even those that lost their controller or identity
        var trucks = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None);
        foreach (var t in trucks)
        {
            if (t != null && t.gameObject != null) DestroyImmediate(t.gameObject);
        }

        // Cleanup any orphaned "(Clone)" trucks or named instances that might be missing the script
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allGOs)
        {
            if (go == null) continue;
            if (go.name.Contains("Truck_SavageDev") || go.name.Contains("Truck→Door") || go.name.Contains("Truck→PO_"))
            {
                DestroyImmediate(go);
            }
        }
    }

    public void SpawnNextTruck()
    {
        SpawnNextTruck(null);
    }

    public void SpawnNextTruck(GameCore.Inventory.ShipmentData shipment)
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
        go.name = shipment != null ? $"Truck→PO_{shipment.PONumber}" : $"Truck→Door{dock.DoorNumber}";

        var ctrl = go.GetComponent<TruckController>() ?? go.AddComponent<TruckController>();

        Vector3? gatePos       = _gateStop        != null ? (Vector3?)_gateStop.position        : null;
        Vector3? enterNoTurn   = _gateEnterNoTurn != null ? (Vector3?)_gateEnterNoTurn.position : null;
        Vector3? leaveNoTurn   = _gateLeaveNoTurn != null ? (Vector3?)_gateLeaveNoTurn.position : null;
        Vector3? exitPos       = _exitPoint       != null ? (Vector3?)_exitPoint.position       : null;

        ctrl.Init(gatePos, enterNoTurn, leaveNoTurn, exitPos, _guard, OnTruckExited);
        ctrl.OnClearedGate += () => OnTruckClearedGate(ctrl);
        
        if (shipment != null)
        {
            ctrl.LoadShipment(shipment);
        }
        
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

    // Numbering now lives in DockSlot.AssignDoorNumbers so it works with or without a guard shack
    // (see DockNumberingService) and never renumbers existing doors. Kept here so the guard shack's
    // own Start/placement hooks still drive an assignment pass; all paths are idempotent.
    private void AssignDoorNumbers() => DockSlot.AssignDoorNumbers();

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
