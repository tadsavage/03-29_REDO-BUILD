using UnityEngine;
using System.Linq;

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

    [Header("Dev UI")]
    [SerializeField] private bool showDevOverlay = false;

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
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public int ActiveTrucks => _activeTrucks;

    public void ResetYard()
    {
        _activeTrucks = 0;
        foreach (var dock in DockSlot.All)
        {
            dock.Release();
        }
        
        var trucks = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None);
        foreach (var t in trucks)
        {
            Destroy(t.gameObject);
        }
        
        Debug.Log("[TruckYardManager] Yard reset: all docks released, all trucks removed.");
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
        Vector3? enterNoTurn  = _gateEnterNoTurn != null ? (Vector3?)_gateEnterNoTurn.position : null;
        Vector3? leaveNoTurn  = _gateLeaveNoTurn != null ? (Vector3?)_gateLeaveNoTurn.position : null;
        Vector3? exitPos      = _exitPoint       != null ? (Vector3?)_exitPoint.position       : null;

        ctrl.Init(gatePos, enterNoTurn, leaveNoTurn, exitPos, _guard, OnTruckExited);
        ctrl.AssignAndGo(dock);

        _activeTrucks++;
        Debug.Log($"[TruckYardManager] Truck dispatched to door {dock.DoorNumber}. Active: {_activeTrucks}");
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
        Debug.Log("[TruckYardManager] Guard spawned at Posted.");
    }

    private void AssignDoorNumbers()
    {
        var sorted = DockSlot.All
            .OrderBy(d => d.transform.position.x)
            .ThenBy(d  => d.transform.position.z)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
            sorted[i].DoorNumber = i + 1;

        Debug.Log($"[TruckYardManager] Assigned door numbers to {sorted.Count} dock(s).");
    }

    private void OnTruckExited()
    {
        _activeTrucks = Mathf.Max(0, _activeTrucks - 1);
    }

    private DockSlot FindFreeDock()
    {
        var free = DockSlot.All.Where(d => !d.IsOccupied).ToList();
        return free.Count == 0 ? null : free[Random.Range(0, free.Count)];
    }

    // ── Dev overlay ───────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!showDevOverlay) return;

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        var lblStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal   = { textColor = Color.white },
        };

        int  freeDocks = DockSlot.All.Count(d => !d.IsOccupied);
        bool canSpawn  = freeDocks > 0 && _spawnPoint != null;

        GUI.color = canSpawn ? Color.white : new Color(1f, 0.4f, 0.4f);
        if (GUI.Button(new Rect(12, 52, 190, 40), "TRUCK ENTERS", btnStyle) && canSpawn)
            SpawnNextTruck();

        GUI.color = new Color(1f, 0.6f, 0.6f);
        if (GUI.Button(new Rect(210, 52, 120, 40), "RESET YARD", btnStyle))
            ResetYard();

        GUI.color = Color.white;
        GUI.Label(
            new Rect(12, 96, 350, 22),
            $"Doors: {freeDocks}/{DockSlot.All.Count} free  |  Trucks: {_activeTrucks}",
            lblStyle);

        if (_spawnPoint == null)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(12, 118, 300, 20), "ERROR: SpawnPoint not found!", lblStyle);
        }
    }
}
