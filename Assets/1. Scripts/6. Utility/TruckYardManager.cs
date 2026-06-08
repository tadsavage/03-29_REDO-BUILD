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

    [Header("Dev UI")]
    [SerializeField] private bool showDevOverlay = true;

    private Transform _spawnPoint;
    private Transform _gateStop;
    private Transform _gateEnterNoTurn;
    private Transform _gateLeaveNoTurn;
    private Transform _exitPoint;
    private int       _activeTrucks;

    private void Awake()
    {
        _spawnPoint      = transform.Find("SpawnPoint");
        _gateStop        = transform.Find("GateStop");
        _gateEnterNoTurn = transform.Find("GateEnterNoTurn");
        _gateLeaveNoTurn = transform.Find("GateLeaveNoTurn");
        _exitPoint       = transform.Find("ExitPoint");

        if (_spawnPoint == null) Debug.LogWarning("[TruckYardManager] 'SpawnPoint' child not found on guard shack.");
        if (_gateStop   == null) Debug.LogWarning("[TruckYardManager] 'GateStop' child not found on guard shack.");
        if (_exitPoint  == null) Debug.LogWarning("[TruckYardManager] 'ExitPoint' child not found on guard shack.");
    }

    private void Start()
    {
        AssignDoorNumbers();
    }

    // ── Public API ────────────────────────────────────────────────────────────

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

        ctrl.Init(gatePos, enterNoTurn, leaveNoTurn, exitPos, OnTruckExited);
        ctrl.AssignAndGo(dock);

        _activeTrucks++;
        Debug.Log($"[TruckYardManager] Truck dispatched to door {dock.DoorNumber}. Active: {_activeTrucks}");
    }

    // ── Private ───────────────────────────────────────────────────────────────

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

        GUI.color = Color.white;
        GUI.Label(
            new Rect(12, 96, 220, 22),
            $"Doors: {freeDocks}/{DockSlot.All.Count} free  |  Trucks: {_activeTrucks}",
            lblStyle);
    }
}
