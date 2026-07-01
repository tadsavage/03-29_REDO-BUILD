using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Central manager for the entire racking/aisle system.
/// Owns and coordinates:
/// - RackCollectionDetector
/// - ChevronSpawner
/// - AisleInitializer
/// - RackSetupUI (auto-spawned if not wired in-scene)
/// </summary>
public class RackingSystemManager : MonoBehaviour
{
    [SerializeField] private Material _realRackMaterial;
    [SerializeField] private Sprite _chevronSprite;
    [SerializeField] private Material _chevronMaterial;

    [Header("Rack Setup UI (auto-loaded from Assets in Editor if empty)")]
    [SerializeField] private VisualTreeAsset _rackSetupUxml;
    [SerializeField] private StyleSheet _rackSetupUss;
    [SerializeField] private PanelSettings _rackSetupPanelSettings;

    // Editor-only asset paths used to auto-populate the UI references above so the
    // system can Just Work in a scene that only has a RackingSystemManager GO.
    private const string RSU_UXML_PATH = "Assets/_Project/Scripts/UI_UX/RackSetup/RackSetupUI.uxml";
    private const string RSU_USS_PATH = "Assets/_Project/Scripts/UI_UX/RackSetup/RackSetupUI.uss";

    private RackCollectionDetector _collectionDetector;
    private ChevronSpawner _chevronSpawner;
    private AisleInitializer _aisleInitializer;
    private RackSetupUI _rackSetupUI;

    private void Awake()
    {
        Debug.Log("RackingSystemManager.Awake() - initializing components");
        _collectionDetector = gameObject.AddComponent<RackCollectionDetector>();
        Debug.Log($"RackCollectionDetector added: {_collectionDetector != null}");
        _chevronSpawner = gameObject.AddComponent<ChevronSpawner>();
        _aisleInitializer = gameObject.AddComponent<AisleInitializer>();

        _chevronSpawner.SetSprite(_chevronSprite);
        _chevronSpawner.SetMaterial(_chevronMaterial);

        var grid = FindFirstObjectByType<PlacementGrid>();
        if (grid == null)
            Debug.LogError("RackingSystemManager: no PlacementGrid found in scene — rack collection detection will not work.");
        _collectionDetector.SetGrid(grid);
        _chevronSpawner.SetGrid(grid);

        _aisleInitializer.SetMaterial(_realRackMaterial);

        EnsureRackSetupUI();
    }

    /// <summary>
    /// Guarantees a RackSetupUI GameObject exists in the scene so ChevronController's
    /// double-click OpenSetup has something to find. Skips creation if one is already
    /// present (active or inactive) so a manually-wired scene wins.
    /// </summary>
    private void EnsureRackSetupUI()
    {
        _rackSetupUI = FindFirstObjectByType<RackSetupUI>(FindObjectsInactive.Include);
        if (_rackSetupUI != null) return;

#if UNITY_EDITOR
        if (_rackSetupUxml == null)
            _rackSetupUxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RSU_UXML_PATH);
        if (_rackSetupUss == null)
            _rackSetupUss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(RSU_USS_PATH);
#endif

        if (_rackSetupPanelSettings == null)
        {
            // Reuse any existing UIDocument's PanelSettings so scaling / DPI matches.
            var existingDoc = FindFirstObjectByType<UIDocument>();
            if (existingDoc != null) _rackSetupPanelSettings = existingDoc.panelSettings;
        }

        if (_rackSetupUxml == null || _rackSetupPanelSettings == null)
        {
            Debug.LogError("RackingSystemManager: could not auto-create RackSetupUI — VisualTreeAsset or PanelSettings unavailable. " +
                           "Wire the Rack Setup UXML / PanelSettings fields on RackingSystemManager, or add a RackSetupUI GameObject to the scene.");
            return;
        }

        var go = new GameObject("RackSetupUI (Auto)");
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = _rackSetupPanelSettings;
        doc.visualTreeAsset = _rackSetupUxml;
        doc.sortingOrder = 150; // above TopBar (20), below the tooltip (200)

        if (_rackSetupUss != null)
            doc.rootVisualElement.styleSheets.Add(_rackSetupUss);

        _rackSetupUI = go.AddComponent<RackSetupUI>();
        go.SetActive(false); // modal — hidden until a chevron opens it
        Debug.Log("RackingSystemManager: auto-created RackSetupUI");
    }

    public RackCollectionDetector GetCollectionDetector() => _collectionDetector;
    public ChevronSpawner GetChevronSpawner() => _chevronSpawner;
    public AisleInitializer GetAisleInitializer() => _aisleInitializer;
    public RackSetupUI GetRackSetupUI() => _rackSetupUI;
}
