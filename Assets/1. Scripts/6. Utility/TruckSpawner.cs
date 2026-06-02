using UnityEngine;

/// <summary>
/// Dev tool — spawns a truck and sends it to a dock on demand.
///
/// Setup:
///   1. Add to any persistent GameObject in the scene.
///   2. Assign Truck Prefab, Spawn Point, and Target Dock in the Inspector.
///   3. Hit the on-screen "TRUCK ENTERS" button in Play Mode, or call SpawnTruck() from any UI event.
///
/// The Spawn Point transform should sit at the entry road, facing INTO the yard
/// (the truck will be instantiated here facing that direction).
/// </summary>
public class TruckSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject truckPrefab;

    [Tooltip("Entry point on the road. Truck spawns here facing the yard.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("The DockSlot the truck should pull into. Drag the component here.")]
    [SerializeField] private DockSlot targetDock;

    [Header("Dev UI")]
    [SerializeField] private bool showDevButton = true;

    // ── Public API ────────────────────────────────────────────────────────────

    public void SpawnTruck()
    {
        if (!Validate()) return;

        if (targetDock.IsOccupied)
        {
            Debug.LogWarning("[TruckSpawner] Target dock is occupied — truck not spawned.");
            return;
        }

        var go         = Instantiate(truckPrefab, spawnPoint.position, spawnPoint.rotation);
        go.name        = "Truck(Entering)";
        var controller = go.GetComponent<TruckController>()
                      ?? go.AddComponent<TruckController>();

        controller.AssignAndGo(targetDock);
        Debug.Log($"[TruckSpawner] Truck spawned → {targetDock.name}");
    }

    // ── Dev overlay ───────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!showDevButton) return;

        var style = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        bool dockFree = targetDock != null && !targetDock.IsOccupied;
        GUI.color = dockFree ? Color.white : new Color(1f, 0.4f, 0.4f);

        if (GUI.Button(new Rect(12, 12, 170, 40), "TRUCK ENTERS", style))
            SpawnTruck();

        GUI.color = Color.white;

        if (!dockFree)
        {
            GUI.Label(new Rect(12, 54, 170, 20),
                targetDock == null ? "No dock assigned" : "Dock occupied",
                new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(1f, 0.5f, 0.5f) } });
        }
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private bool Validate()
    {
        if (truckPrefab == null) { Debug.LogError("[TruckSpawner] Truck Prefab not assigned."); return false; }
        if (spawnPoint  == null) { Debug.LogError("[TruckSpawner] Spawn Point not assigned.");  return false; }
        if (targetDock  == null) { Debug.LogError("[TruckSpawner] Target Dock not assigned.");  return false; }
        return true;
    }
}
