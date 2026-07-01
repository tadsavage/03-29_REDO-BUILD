using UnityEngine;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Handles the complete aisle initialization workflow:
/// 1. Receives setup data from RackSetupUI (aisle number, level designations)
/// 2. Generates location names using LocationNameGenerator
/// 3. Instantiates real rack prefabs (replaces orange preview)
/// 4. Configures label visibility (only aisle-facing side)
/// 5. Deletes chevrons (cleanup)
/// 6. Marks collections as initialized
/// </summary>
public class AisleInitializer : MonoBehaviour
{
    [Header("Rack Configuration")]
    [SerializeField] private Material _realRackMaterial;
    // Path used when _realRackMaterial isn't wired via the RackingSystemManager Inspector.
    // Keeps commit working without an Editor round-trip.
    private const string DEFAULT_LIVE_MATERIAL_PATH = "Assets/_Project/Materials/AA_LowPolyCommon.mat";

    private RackCollectionDetector _collectionDetector;
    private ChevronSpawner _chevronSpawner;
    private ChevronController _selectedChevron;
    private RackSetupUI _setupUI;

    public void SetMaterial(Material material) => _realRackMaterial = material;

    private void Start()
    {
        _collectionDetector = GetComponent<RackCollectionDetector>();
        _chevronSpawner = GetComponent<ChevronSpawner>();

        // Modal starts inactive (only opens on chevron double-click) — include inactive
        // in the search so we still bind OnSubmit at startup.
        _setupUI = FindFirstObjectByType<RackSetupUI>(FindObjectsInactive.Include);
        if (_setupUI != null)
        {
            _setupUI.OnSubmit += HandleSetupSubmit;
        }
    }

    public void SelectChevron(ChevronController chevron)
    {
        _selectedChevron = chevron;
    }

    private void HandleSetupSubmit(RackSetupData setupData)
    {
        if (_selectedChevron == null || _selectedChevron.Collection == null)
        {
            Debug.LogError("No chevron selected for aisle setup!");
            return;
        }

        // Determine which collection(s) to initialize based on chevron
        var collectionsToInitialize = DetermineCollectionsToInitialize(_selectedChevron);

        if (collectionsToInitialize.Count == 0)
        {
            Debug.LogError("No collections to initialize!");
            return;
        }

        // Generate location names for all collections
        var locations = LocationNameGenerator.GenerateAisleLocations(
            setupData.aisleNumber,
            collectionsToInitialize.ToArray(),
            setupData.levelDesignations,
            _selectedChevron.CurrentRotation
        );

        // Commit the racks: un-ghost them in place and label them (no re-instantiation).
        CommitRealRacks(collectionsToInitialize, locations, setupData.levelDesignations);

        // Configure labels (only aisle-facing side)
        ConfigureLabels(collectionsToInitialize, _selectedChevron.CurrentRotation);

        // Mark collections as initialized
        foreach (var collection in collectionsToInitialize)
        {
            collection.Initialize();
        }

        // Claim this aisle number so no other collection can reuse it.
        AisleRegistry.Register(setupData.aisleNumber);

        // Delete all chevrons for this aisle
        _chevronSpawner.DeleteAllChevrons(collectionsToInitialize.ToArray());

        // Reset selection
        _selectedChevron = null;

        Debug.Log($"Aisle {setupData.aisleNumber} initialized with {collectionsToInitialize.Count} collection(s)");
        UIToast.Show($"Aisle {setupData.aisleNumber:D2} has been initialized successfully!");
    }

    private List<RackCollection> DetermineCollectionsToInitialize(ChevronController chevron)
    {
        var collections = new List<RackCollection>();
        var allUninitialized = _collectionDetector.GetUninitializedCollections();

        // Determine which chevron type this is (left, middle, right)
        string chevronType = DetermineChevronType(chevron);

        if (chevronType == "Middle")
        {
            // Both collections in the aisle
            collections.AddRange(allUninitialized);
        }
        else
        {
            // Single-sided aisle
            collections.Add(chevron.Collection);
        }

        return collections;
    }

    private string DetermineChevronType(ChevronController chevron)
    {
        // Determine if this is left, middle, or right chevron
        // Middle chevron is positioned between two collections
        // Left/right are positioned outside single collections

        var allUninitialized = _collectionDetector.GetUninitializedCollections();
        if (allUninitialized.Count < 2)
            return "Single"; // Only one collection exists

        // Check if another uninitialized collection exists
        var other = allUninitialized.Find(c => c != chevron.Collection);
        if (other != null)
            return "Middle"; // Two collections, likely middle chevron

        return "Side"; // One-sided aisle
    }

    private void CommitRealRacks(List<RackCollection> collections, List<LocationNameGenerator.LocationName> locations, string[] levelDesignations)
    {
        int locationIndex = 0;
        var liveMat = ResolveLiveMaterial();

        foreach (var collection in collections)
        {
            foreach (var rackGO in collection.Racks)
            {
                if (rackGO == null) continue;

                // Un-ghost in place: restore the rack's real materials. Keeps the existing
                // PlacedObject / grid registration intact (re-instantiating would lose it).
                var ghost = rackGO.GetComponent<RackGhost>();
                if (ghost != null) ghost.RestoreReal();

                // Swap non-label renderers to the live material (AA_LowPolyCommon by default).
                if (liveMat != null) ApplyLiveMaterial(rackGO, liveMat);

                // Flag the rack as part of a committed aisle — safeguard for future
                // systems (inventory, task assignment) that should skip ghost placeholders.
                var placed = rackGO.GetComponent<PlacedObject>();
                if (placed != null) placed.isRackLive = true;

                // Assign location names to labels on this rack.
                AssignLocationsToLabels(rackGO, locations, locationIndex, levelDesignations);
                locationIndex += 12; // 6 levels × 2 positions per rack
            }
        }
    }

    private Material ResolveLiveMaterial()
    {
        if (_realRackMaterial != null) return _realRackMaterial;
#if UNITY_EDITOR
        _realRackMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DEFAULT_LIVE_MATERIAL_PATH);
#endif
        return _realRackMaterial;
    }

    private static void ApplyLiveMaterial(GameObject rackGO, Material liveMat)
    {
        foreach (var r in rackGO.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r.GetComponent<TMP_Text>() != null) continue; // never touch text meshes

            var array = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < array.Length; i++) array[i] = liveMat;
            r.sharedMaterials = array;
        }
    }

    private void AssignLocationsToLabels(GameObject rackGO, List<LocationNameGenerator.LocationName> locations, int startIndex, string[] levelDesignations)
    {
        // Get all label groups (Front/Rear × Left/Right)
        var labelFrontL = rackGO.transform.Find("LabelFront.L");
        var labelFrontR = rackGO.transform.Find("LabelFront.R");
        var labelRearL = rackGO.transform.Find("LabelRear.L");
        var labelRearR = rackGO.transform.Find("LabelRear.R");

        // Assign location names to each label
        int locIndex = startIndex;

        AssignLocationToLabelGroup(labelFrontL, locations, ref locIndex, levelDesignations);
        AssignLocationToLabelGroup(labelFrontR, locations, ref locIndex, levelDesignations);
        AssignLocationToLabelGroup(labelRearL, locations, ref locIndex, levelDesignations);
        AssignLocationToLabelGroup(labelRearR, locations, ref locIndex, levelDesignations);
    }

    private void AssignLocationToLabelGroup(Transform labelGroup, List<LocationNameGenerator.LocationName> locations, ref int locIndex, string[] levelDesignations)
    {
        if (labelGroup == null) return;

        var tmpLabels = labelGroup.GetComponentsInChildren<TMPro.TextMeshPro>();

        foreach (var label in tmpLabels)
        {
            if (locIndex < locations.Count)
            {
                label.text = locations[locIndex].fullName;
                locIndex++;
            }
        }
    }

    private void ConfigureLabels(List<RackCollection> collections, float chevronRotation)
    {
        // Determine which side faces the aisle based on chevron rotation
        bool labelFrontFacesAisle = chevronRotation < 90f;

        foreach (var collection in collections)
        {
            foreach (var rackGO in collection.Racks)
            {
                ConfigureRackLabels(rackGO, labelFrontFacesAisle);
            }
        }
    }

    private void ConfigureRackLabels(GameObject rackGO, bool frontFacesAisle)
    {
        // Enable/disable label groups based on which side faces the aisle
        var labelFront = rackGO.transform.Find("LabelFront");
        var labelRear = rackGO.transform.Find("LabelRear");

        if (frontFacesAisle)
        {
            // Disable rear labels
            if (labelRear != null)
            {
                labelRear.gameObject.SetActive(false);
                var rearLabels = labelRear.GetComponentsInChildren<TMPro.TextMeshPro>();
                foreach (var label in rearLabels)
                    label.enabled = false;
            }
            // Enable front labels
            if (labelFront != null)
            {
                labelFront.gameObject.SetActive(true);
                var frontLabels = labelFront.GetComponentsInChildren<TMPro.TextMeshPro>();
                foreach (var label in frontLabels)
                    label.enabled = true;
            }
        }
        else
        {
            // Disable front labels
            if (labelFront != null)
            {
                labelFront.gameObject.SetActive(false);
                var frontLabels = labelFront.GetComponentsInChildren<TMPro.TextMeshPro>();
                foreach (var label in frontLabels)
                    label.enabled = false;
            }
            // Enable rear labels
            if (labelRear != null)
            {
                labelRear.gameObject.SetActive(true);
                var rearLabels = labelRear.GetComponentsInChildren<TMPro.TextMeshPro>();
                foreach (var label in rearLabels)
                    label.enabled = true;
            }
        }
    }
}
