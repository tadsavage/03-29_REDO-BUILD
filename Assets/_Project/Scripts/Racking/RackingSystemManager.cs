using UnityEngine;

/// <summary>
/// Central manager for the entire racking/aisle system.
/// Owns and coordinates:
/// - RackCollectionDetector
/// - ChevronSpawner
/// - AisleInitializer
/// </summary>
public class RackingSystemManager : MonoBehaviour
{
    [SerializeField] private Material _realRackMaterial;
    [SerializeField] private Sprite _chevronSprite;
    [SerializeField] private Material _chevronMaterial;

    private RackCollectionDetector _collectionDetector;
    private ChevronSpawner _chevronSpawner;
    private AisleInitializer _aisleInitializer;

    private void Awake()
    {
        Debug.Log("RackingSystemManager.Awake() - initializing components");
        // Initialize the racking system components
        _collectionDetector = gameObject.AddComponent<RackCollectionDetector>();
        Debug.Log($"RackCollectionDetector added: {_collectionDetector != null}");
        _chevronSpawner = gameObject.AddComponent<ChevronSpawner>();
        _aisleInitializer = gameObject.AddComponent<AisleInitializer>();

        // Configure chevron spawner with sprite and material
        _chevronSpawner.SetSprite(_chevronSprite);
        _chevronSpawner.SetMaterial(_chevronMaterial);

        // Both detection and chevron placement work in grid-cell space — give them the grid.
        var grid = FindFirstObjectByType<PlacementGrid>();
        if (grid == null)
            Debug.LogError("RackingSystemManager: no PlacementGrid found in scene — rack collection detection will not work.");
        _collectionDetector.SetGrid(grid);
        _chevronSpawner.SetGrid(grid);

        // Configure aisle initializer with material
        _aisleInitializer.SetMaterial(_realRackMaterial);
    }

    public RackCollectionDetector GetCollectionDetector() => _collectionDetector;
    public ChevronSpawner GetChevronSpawner() => _chevronSpawner;
    public AisleInitializer GetAisleInitializer() => _aisleInitializer;
}
