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

    // Levels below this index are pickable (numeric level char "0","1"); levels at/above it
    // are reserves ("A","B","C"...). Must match LocationNameGenerator.ConvertLevelToChar.
    private const int PICK_LEVELS = 2;

    private RackCollectionDetector _collectionDetector;
    private ChevronSpawner _chevronSpawner;
    private ChevronController _selectedChevron;
    private RackSetupUI _setupUI;
    private PlacementGrid _grid;

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

    /// <summary>
    /// If <paramref name="rackGO"/> was placed directly on top of an already-live rack, this
    /// is a VERTICAL extension of an existing aisle — not a new aisle. Commit it immediately:
    /// no chevrons, no setup UI, no orange ghost. It inherits the aisle + bay of the rack below
    /// and takes the next level up. Upper levels are reserves (picks are only reachable at the
    /// bottom). Returns true if it was handled as a stack, so the caller can skip collection
    /// detection entirely.
    /// </summary>
    public bool TryCommitStackedRack(GameObject rackGO)
    {
        if (rackGO == null) return false;
        if (_grid == null) _grid = FindFirstObjectByType<PlacementGrid>();
        if (_grid == null) return false;

        var below = FindLiveRackBelow(rackGO);
        if (below == null) return false;

        var belowPO = below.GetComponent<PlacedObject>();
        var newPO = rackGO.GetComponent<PlacedObject>();
        if (belowPO == null || newPO == null) return false;

        int aisle = belowPO.rackAisle;
        int bay = belowPO.rackBay;
        int level = belowPO.rackLevelIndex + 1;

        newPO.rackAisle = aisle;
        newPO.rackBay = bay;
        newPO.rackLevelIndex = level;
        newPO.isRackLive = true;

        // Live right away. Un-ghost if it was placed as a planning ghost pre-init, then match
        // the committed racks below (material + labels).
        var stackGhost = rackGO.GetComponent<RackGhost>();
        if (stackGhost != null) stackGhost.RestoreReal();
        var liveMat = ResolveLiveMaterial();
        if (liveMat != null) ApplyLiveMaterial(rackGO, liveMat);

        // Levels 0-1 are pickable and use the numeric level char ("0","1"); levels 2+ are
        // reserves ("A","B","C"...). This mirrors LocationNameGenerator.ConvertLevelToChar,
        // whose Reserve branch assumes level >= 2 — passing "Reserve" for level 1 produced
        // '@' (the char just before 'A'). That was the stacked-level-1 naming bug.
        string designation = level < PICK_LEVELS ? "Pick" : "Reserve";
        AssignStackedRackLabels(rackGO, below, aisle, bay, level, designation);

        // Show the same face as the rack below, matched by WORLD side — robust to the stacked
        // rack's rotation and to prefab label-naming variants (e.g. the yellow rack's groups).
        ConfigureFaces(rackGO, BelowAisleDir(below));

        Debug.Log($"Stacked rack committed: aisle {aisle:D2} bay {bay:D2} level {level} (Reserve) — appended to existing aisle, no chevrons/UI.");
        return true;
    }

    /// <summary>
    /// The topmost live racking object in the newly-placed rack's root cell. RackPlacedEvent
    /// fires before the new rack is added to the grid stack, but we still skip the rack itself
    /// defensively (drag-place adds to the grid before firing).
    /// </summary>
    private GameObject FindLiveRackBelow(GameObject rackGO)
    {
        Vector2Int cell = _grid.WorldToCell(rackGO.transform.position);
        var objects = _grid.GetObjectsInCell(cell);
        if (objects == null) return null;

        float myY = rackGO.transform.position.y;
        GameObject below = null;  float belowY = float.NegativeInfinity;  // highest rack strictly under myY
        GameObject topAny = null; float topAnyY = float.NegativeInfinity;  // highest live rack overall

        foreach (var entry in objects)
        {
            if (entry.instance == null || entry.data == null) continue;
            if (entry.instance == rackGO) continue;
            if (entry.data.category != "Racking") continue;
            var po = entry.instance.GetComponent<PlacedObject>();
            if (po == null || !po.isRackLive) continue;

            float oy = entry.instance.transform.position.y;
            if (oy > topAnyY) { topAnyY = oy; topAny = entry.instance; }
            if (oy < myY - 0.05f && oy > belowY) { belowY = oy; below = entry.instance; }
        }

        // Prefer the rack directly under this one's height (init: everything already positioned);
        // fall back to the topmost live rack when the new rack's Y isn't finalized yet (post-init
        // placement fires RackPlacedEvent before the stack Y is applied).
        return below != null ? below : topAny;
    }

    /// <summary>
    /// Writes a single level's location names onto a rack's four label groups: the left column
    /// (position 0) on both faces, the right column (position 1) on both faces. ConfigureRackLabels
    /// then hides whichever face doesn't front the aisle.
    /// </summary>
    /// <summary>
    /// Labels a stacked rack. The LEVEL comes from levelIndex, but the POSITION (column) is a
    /// vertical property: each label inherits the position digit of whichever label on the rack
    /// BELOW sits physically beneath it (nearest in world X/Z). That keeps a column's position
    /// consistent all the way up and follows the chevron direction the ground level established —
    /// even if the stacked rack was placed at a different rotation than the rack below it (which
    /// is what made an earlier version read positions in reverse, e.g. …-11 then …-10).
    /// </summary>
    private void AssignStackedRackLabels(GameObject rackGO, GameObject below, int aisle, int bay, int levelIndex, string designation)
    {
        string levelChar = LocationNameGenerator.GetLocationName(aisle, bay, levelIndex, designation, 0).level;
        SetRackLabels(rackGO, aisle, bay, levelChar, worldPos => InheritPositionFromBelow(worldPos, below));
    }

    /// <summary>Position digit of the below rack's label nearest (in world X/Z) to worldPos.</summary>
    private int InheritPositionFromBelow(Vector3 worldPos, GameObject below)
    {
        int bestPos = 0;
        float bestDist = float.MaxValue;
        var target = new Vector2(worldPos.x, worldPos.z);

        foreach (var tmp in below.GetComponentsInChildren<TMPro.TextMeshPro>())
        {
            string txt = tmp.text;
            if (string.IsNullOrEmpty(txt)) continue;
            char last = txt[txt.Length - 1];
            if (last < '0' || last > '9') continue;

            Vector3 bp = tmp.transform.position;
            float dist = (new Vector2(bp.x, bp.z) - target).sqrMagnitude;
            if (dist < bestDist) { bestDist = dist; bestPos = last - '0'; }
        }
        return bestPos;
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

        // Un-ghost, number, and label the whole aisle. Only GROUND racks (the lowest in each
        // vertical column) become bays; racks stacked above them become higher LEVELS of the
        // same bay — so a pre-built multi-level structure names correctly at one-shot init.
        CommitAndLabelAisle(collectionsToInitialize, setupData.aisleNumber, _selectedChevron);

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

    /// <summary>
    /// Commits and labels every rack in the aisle. GROUND racks (the lowest rack in each vertical
    /// column) are numbered as bays; racks stacked above them are committed as higher LEVELS of the
    /// same bay via TryCommitStackedRack. This is what lets a fully pre-built multi-level structure
    /// name correctly when it's all initialized in one shot — not just when levels are added later.
    /// </summary>
    private void CommitAndLabelAisle(List<RackCollection> collections, int aisleNumber, ChevronController chevron)
    {
        if (_grid == null) _grid = FindFirstObjectByType<PlacementGrid>();

        float chevronRotation = chevron != null ? chevron.CurrentRotation : 0f;
        bool firstCollectionEven = chevronRotation < 90f; // mirrors LocationNameGenerator's side rule
        Vector3 chevronPos = chevron != null ? chevron.transform.position : Vector3.zero;
        var liveMat = ResolveLiveMaterial();

        for (int colIndex = 0; colIndex < collections.Count; colIndex++)
        {
            var collection = collections[colIndex];
            bool isEvenSide = (colIndex == 0) ? firstCollectionEven : !firstCollectionEven;

            var groundRacks = GetGroundRacks(collection);
            var bayNumbers = LocationNameGenerator.GenerateBayNumbers(groundRacks.Count, isEvenSide);
            Vector3 travelDir = ComputeTravelDir(groundRacks);

            // Ground level (0): number as bays; position order follows the travel direction.
            for (int i = 0; i < groundRacks.Count; i++)
            {
                int bay = i < bayNumbers.Count ? bayNumbers[i] : (isEvenSide ? 2 + i * 2 : 1 + i * 2);
                CommitGroundRack(groundRacks[i], aisleNumber, bay, travelDir, chevron, chevronPos, liveMat);
            }

            // Stacked racks: commit bottom-up so each finds a committed rack directly below it.
            foreach (var upper in GetUpperRacksAscending(collection))
                TryCommitStackedRack(upper);
        }
    }

    /// <summary>The lowest rack in each vertical column, in collection order (≈ travel order,
    /// which drives the bay numbering).</summary>
    private List<GameObject> GetGroundRacks(RackCollection collection)
    {
        var result = new List<GameObject>();
        foreach (var rack in collection.Racks)
            if (rack != null && IsGroundRack(rack, collection))
                result.Add(rack);
        return result;
    }

    private List<GameObject> GetUpperRacksAscending(RackCollection collection)
    {
        var upper = new List<GameObject>();
        foreach (var rack in collection.Racks)
            if (rack != null && !IsGroundRack(rack, collection))
                upper.Add(rack);
        upper.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        return upper;
    }

    /// <summary>True when no other rack in the collection shares this rack's cell at a lower height.</summary>
    private bool IsGroundRack(GameObject rack, RackCollection collection)
    {
        if (_grid == null) return true;
        Vector2Int cell = _grid.WorldToCell(rack.transform.position);
        float y = rack.transform.position.y;
        foreach (var other in collection.Racks)
        {
            if (other == null || other == rack) continue;
            if (_grid.WorldToCell(other.transform.position) != cell) continue;
            if (other.transform.position.y < y - 0.05f) return false;
        }
        return true;
    }

    /// <summary>Travel direction down the run, derived from the ordered ground racks (bay N → N+1).</summary>
    private Vector3 ComputeTravelDir(List<GameObject> groundRacks)
    {
        if (groundRacks.Count >= 2)
        {
            Vector3 d = groundRacks[1].transform.position - groundRacks[0].transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }
        if (groundRacks.Count >= 1)
        {
            Vector3 r = groundRacks[0].transform.right; // run axis (local X) fallback for a single bay
            r.y = 0f;
            if (r.sqrMagnitude > 0.0001f) return r.normalized;
        }
        return Vector3.right;
    }

    private void CommitGroundRack(GameObject rackGO, int aisle, int bay, Vector3 travelDir,
                                  ChevronController chevron, Vector3 chevronPos, Material liveMat)
    {
        var ghost = rackGO.GetComponent<RackGhost>();
        if (ghost != null) ghost.RestoreReal();
        if (liveMat != null) ApplyLiveMaterial(rackGO, liveMat);

        var placed = rackGO.GetComponent<PlacedObject>();
        if (placed != null)
        {
            placed.isRackLive = true;
            placed.rackAisle = aisle;
            placed.rackBay = bay;
            placed.rackLevelIndex = 0;
        }

        string levelChar = LocationNameGenerator.GetLocationName(aisle, bay, 0, "Pick", 0).level;
        SetRackLabels(rackGO, aisle, bay, levelChar, TravelPositionResolver(rackGO, travelDir));
        ConfigureFaces(rackGO, GroundAisleDir(rackGO, travelDir, chevron, chevronPos));
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

    // ---- Geometry-based labeling (prefab-agnostic: Orange, Yellow, Half, etc.) ----
    // We deliberately do NOT key off label-group NAMES. The yellow rack prefab names its groups
    // differently ("LabelFront.L.002") and even mirrors them, so name lookups silently failed and
    // left its default "A-01-01" text. Everything below keys off world geometry instead.

    /// <summary>Sets every label to AA-BB-L{pos}, where pos = positionOf(that label's world pos).</summary>
    private void SetRackLabels(GameObject rackGO, int aisle, int bay, string levelChar, System.Func<Vector3, int> positionOf)
    {
        foreach (var tmp in rackGO.GetComponentsInChildren<TMPro.TextMeshPro>(true))
        {
            int pos = positionOf(tmp.transform.position);
            tmp.text = $"{aisle:D2}-{bay:D2}-{levelChar}{pos}";
        }
    }

    /// <summary>Ground resolver: the column earlier along travelDir is position 0.</summary>
    private System.Func<Vector3, int> TravelPositionResolver(GameObject rackGO, Vector3 travelDir)
    {
        float minP = float.MaxValue, maxP = float.MinValue;
        foreach (var tmp in rackGO.GetComponentsInChildren<TMPro.TextMeshPro>(true))
        {
            float p = Vector3.Dot(tmp.transform.position, travelDir);
            if (p < minP) minP = p;
            if (p > maxP) maxP = p;
        }
        float mid = (minP + maxP) * 0.5f;
        return worldPos => Vector3.Dot(worldPos, travelDir) <= mid ? 0 : 1;
    }

    /// <summary>Enables only the labels on the aisle-facing side (offset·aisleDir > 0); hides the rest.</summary>
    private void ConfigureFaces(GameObject rackGO, Vector3 aisleDir)
    {
        Vector3 center = rackGO.transform.position;
        foreach (var tmp in rackGO.GetComponentsInChildren<TMPro.TextMeshPro>(true))
        {
            Vector3 d = tmp.transform.position - center; d.y = 0f;
            tmp.gameObject.SetActive(Vector3.Dot(d, aisleDir) > 0f);
        }
    }

    /// <summary>Horizontal direction toward the aisle for a ground rack: the lateral (perpendicular
    /// to the run) part of the direction to the selected chevron.</summary>
    private Vector3 GroundAisleDir(GameObject rackGO, Vector3 travelDir, ChevronController chevron, Vector3 chevronPos)
    {
        if (chevron != null)
        {
            Vector3 toChev = chevronPos - rackGO.transform.position; toChev.y = 0f;
            Vector3 lateral = toChev - Vector3.Project(toChev, travelDir);
            if (lateral.sqrMagnitude > 0.0001f) return lateral.normalized;
        }
        Vector3 f = rackGO.transform.forward; f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    /// <summary>Aisle-facing face normal of the rack below, from where its still-active labels sit.</summary>
    private Vector3 BelowAisleDir(GameObject below)
    {
        Vector3 fwd = below.transform.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;

        float sum = 0f;
        foreach (var tmp in below.GetComponentsInChildren<TMPro.TextMeshPro>(false)) // active only
            sum += Vector3.Dot(tmp.transform.position - below.transform.position, fwd);

        return sum >= 0f ? fwd : -fwd;
    }

}
