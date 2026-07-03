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

    private ChevronSpawner _chevronSpawner;
    private ChevronController _selectedChevron;
    private RackSetupUI _setupUI;
    private PlacementGrid _grid;

    public void SetMaterial(Material material) => _realRackMaterial = material;

    private void Start()
    {
        _chevronSpawner = GetComponent<ChevronSpawner>();

        // Modal starts inactive (only opens on chevron double-click) — include inactive
        // in the search so we still bind OnSubmit at startup.
        _setupUI = FindAnyObjectByType<RackSetupUI>(FindObjectsInactive.Include);
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
        if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
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

        // Inherit the aisle-facing world direction from the rack below (stored at its own commit),
        // so this doesn't depend on the below rack's live label states. Fall back to reading them
        // for legacy racks committed before the field existed.
        Vector3 aisleFacing = RackFacingOf(belowPO, below);
        newPO.rackAisleFacing = aisleFacing;

        // Level char honors the aisle's Pick/Reserve scheme (registered at setup): picks numeric,
        // reserves lettered bottom-up. Post-init stacks look it up by aisle number.
        string levelChar = LocationNameGenerator.LevelChar(level, AisleRegistry.GetDesignations(aisle));
        newPO.rackLevelChar = levelChar;
        // Inherit the run/travel direction from the rack below so the position columns stay
        // consistent all the way up and this survives save/load.
        newPO.rackTravelDir = belowPO.rackTravelDir;
        AssignStackedRackLabels(rackGO, below, aisle, bay, levelChar);

        // Show the same face as the rack below, matched by WORLD side — robust to the stacked
        // rack's rotation and to prefab label-naming variants (e.g. the yellow rack's groups).
        ConfigureFaces(rackGO, aisleFacing);

        Debug.Log($"Stacked rack committed: aisle {aisle:D2} bay {bay:D2} level {levelChar} — appended to existing aisle, no chevrons/UI.");
        return true;
    }

    /// <summary>
    /// If <paramref name="rackGO"/> is a fresh ground rack placed FACING an already-finalized aisle
    /// (within 5 cells, that aisle's labeled side pointing back at it, nothing racking in between),
    /// it becomes the OTHER side of that aisle: same aisle number, opposite bay parity, paired
    /// directly across from the facing bay, committed LIVE immediately — no ghost, chevron, or setup
    /// UI. Upper levels then stack normally (TryCommitStackedRack) once this ground rack is live.
    /// The exception to the unique-aisle rule the user asked for. Returns true if handled.
    /// </summary>
    public bool TryCommitSecondSide(GameObject rackGO)
    {
        if (rackGO == null) return false;
        if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
        if (_grid == null) return false;

        var newPO = rackGO.GetComponent<PlacedObject>();
        if (newPO == null || newPO.data == null || newPO.data.category != "Racking") return false;

        var facing = FindFacingFinalizedRack(rackGO);
        if (facing == null) return false;

        var facingPO = facing.GetComponent<PlacedObject>();
        if (facingPO == null || facingPO.rackAisle < 0) return false;

        int aisle = facingPO.rackAisle;
        // Opposite parity, paired directly across: even facing → odd new (facing-1); odd → even (facing+1).
        int newBay = (facingPO.rackBay % 2 == 0) ? facingPO.rackBay - 1 : facingPO.rackBay + 1;
        if (newBay < 1) newBay = facingPO.rackBay + 1;

        // Don't duplicate a bay that already exists on this aisle (guards against adding a THIRD row).
        if (AisleGroundBayExists(aisle, newBay)) return false;

        newPO.rackAisle = aisle;
        newPO.rackBay = newBay;
        newPO.rackLevelIndex = 0;
        newPO.isRackLive = true;

        var ghost = rackGO.GetComponent<RackGhost>();
        if (ghost != null) ghost.RestoreReal();
        var liveMat = ResolveLiveMaterial();
        if (liveMat != null) ApplyLiveMaterial(rackGO, liveMat);

        // Position the new side by the aisle's TRAVEL direction (same rule the ground uses), so
        // pos 0 is at the start end on BOTH sides — they line up across the aisle. (Can't inherit
        // by nearest-XZ here: the facing row faces the opposite way, so its columns don't overlap.)
        string levelChar = LocationNameGenerator.LevelChar(0, AisleRegistry.GetDesignations(aisle));
        Vector3 travelDir = TravelDirFromRack(facing);
        newPO.rackLevelChar = levelChar;
        newPO.rackTravelDir = travelDir.sqrMagnitude > 0.0001f ? travelDir.normalized : travelDir;
        SetRackLabels(rackGO, aisle, newBay, levelChar, TravelPositionResolver(rackGO, travelDir));

        // Aisle-facing side = toward the facing rack.
        Vector3 aisleDir = facing.transform.position - rackGO.transform.position; aisleDir.y = 0f;
        newPO.rackAisleFacing = aisleDir.sqrMagnitude > 0.0001f ? aisleDir.normalized : aisleDir;
        ConfigureFaces(rackGO, aisleDir);

        Debug.Log($"Second-side rack committed: aisle {aisle:D2} bay {newBay:D2} (paired across from bay {facingPO.rackBay:D2}), live, no chevron/UI.");
        return true;
    }

    /// <summary>
    /// Nearest live rack belonging to a finalized aisle that sits as the OPPOSITE side of an aisle
    /// from <paramref name="rackGO"/>: within 5 cells, roughly directly across (aligned on the run
    /// axis), its labeled side facing back toward this rack, and nothing racking in between.
    /// </summary>
    private GameObject FindFacingFinalizedRack(GameObject rackGO)
    {
        float maxDist = 5f * _grid.CellSize;
        Vector3 p = rackGO.transform.position;
        Vector3 runAxis = rackGO.transform.right;       // local X = run
        Vector3 lateralAxis = rackGO.transform.forward; // local Z = aisle-facing

        GameObject best = null; float bestDist = float.MaxValue;

        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null) continue;
            var go = po.gameObject;
            if (go == rackGO) continue;
            if (!po.isRackLive || po.rackAisle < 0) continue;
            if (po.data == null || po.data.category != "Racking") continue;

            Vector3 d = go.transform.position - p; d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.1f || dist > maxDist) continue;

            float along = Mathf.Abs(Vector3.Dot(d, runAxis));
            float lateral = Mathf.Abs(Vector3.Dot(d, lateralAxis));
            if (lateral < 0.5f) continue;                  // must be off to the side (across an aisle)
            if (along > 0.75f * _grid.CellSize) continue;  // must be ~directly across, not down the run

            // The existing rack's labeled (aisle) side must point back toward the new rack.
            if (Vector3.Dot(BelowAisleDir(go), -d) <= 0f) continue;

            if (IsRackingBetween(p, go.transform.position, rackGO, go)) continue;

            if (dist < bestDist) { bestDist = dist; best = go; }
        }
        return best;
    }

    /// <summary>True if any racking object occupies a grid cell strictly between a and b.</summary>
    private bool IsRackingBetween(Vector3 a, Vector3 b, GameObject exclude1, GameObject exclude2)
    {
        Vector2Int ca = _grid.WorldToCell(a);
        Vector2Int cb = _grid.WorldToCell(b);
        int steps = Mathf.Max(Mathf.Abs(cb.x - ca.x), Mathf.Abs(cb.y - ca.y));
        for (int s = 1; s < steps; s++)
        {
            float t = (float)s / steps;
            var c = new Vector2Int(
                Mathf.RoundToInt(Mathf.Lerp(ca.x, cb.x, t)),
                Mathf.RoundToInt(Mathf.Lerp(ca.y, cb.y, t)));
            var objs = _grid.GetObjectsInCell(c);
            if (objs == null) continue;
            foreach (var e in objs)
            {
                if (e.instance == null || e.data == null) continue;
                if (e.instance == exclude1 || e.instance == exclude2) continue;
                if (e.data.category == "Racking") return true;
            }
        }
        return false;
    }

    /// <summary>Travel direction of an already-labeled rack: from its position-0 label to its
    /// position-1 label (falls back to the rack's run axis).</summary>
    private Vector3 TravelDirFromRack(GameObject rack)
    {
        Vector3 p0 = Vector3.zero, p1 = Vector3.zero; bool has0 = false, has1 = false;
        foreach (var tmp in rack.GetComponentsInChildren<TMPro.TextMeshPro>(false)) // active only
        {
            if (string.IsNullOrEmpty(tmp.text)) continue;
            char last = tmp.text[tmp.text.Length - 1];
            if (last == '0' && !has0) { p0 = tmp.transform.position; has0 = true; }
            else if (last == '1' && !has1) { p1 = tmp.transform.position; has1 = true; }
        }
        if (has0 && has1)
        {
            Vector3 d = p1 - p0; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }
        Vector3 r = rack.transform.right; r.y = 0f;
        return r.sqrMagnitude > 0.0001f ? r.normalized : Vector3.right;
    }

    private bool AisleGroundBayExists(int aisle, int bay)
    {
        foreach (var po in PlacedObjectRegistry.All)
            if (po != null && po.isRackLive && po.rackAisle == aisle && po.rackBay == bay && po.rackLevelIndex == 0)
                return true;
        return false;
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
    private void AssignStackedRackLabels(GameObject rackGO, GameObject below, int aisle, int bay, string levelChar)
    {
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

        // Register aisle + its level designations BEFORE committing, so the ground/stacked commits
        // can look up the Pick/Reserve scheme to compute level chars (and so later side/stack
        // additions to this aisle use the same scheme).
        AisleRegistry.Register(setupData.aisleNumber, setupData.levelDesignations);

        // Un-ghost, number, and label the whole aisle. Only GROUND racks (the lowest in each
        // vertical column) become bays; racks stacked above them become higher LEVELS of the
        // same bay — so a pre-built multi-level structure names correctly at one-shot init.
        CommitAndLabelAisle(collectionsToInitialize, setupData.aisleNumber, _selectedChevron);

        // Mark collections as initialized
        foreach (var collection in collectionsToInitialize)
        {
            collection.Initialize();
        }

        // Delete all chevrons for this aisle
        _chevronSpawner.DeleteAllChevrons(collectionsToInitialize.ToArray());

        // Reset selection
        _selectedChevron = null;

        Debug.Log($"Aisle {setupData.aisleNumber} initialized with {collectionsToInitialize.Count} collection(s)");
        UIToast.Show($"Aisle {setupData.aisleNumber:D2} has been initialized successfully!");
    }

    private List<RackCollection> DetermineCollectionsToInitialize(ChevronController chevron)
    {
        // A COMBINED corridor chevron carries a secondary collection — it sets up BOTH facing rows as
        // one two-sided aisle. A normal side chevron sets up only its own collection. (The old
        // "any-two-uninitialized ⇒ init all" heuristic is gone: the spawner now spawns the combined
        // chevron explicitly, so which collections pair is decided there, not guessed here.)
        var collections = new List<RackCollection> { chevron.Collection };
        if (chevron.SecondaryCollection != null && chevron.SecondaryCollection != chevron.Collection)
            collections.Add(chevron.SecondaryCollection);
        return collections;
    }

    /// <summary>
    /// Commits and labels every rack in the aisle. GROUND racks (the lowest rack in each vertical
    /// column) are numbered as bays; racks stacked above them are committed as higher LEVELS of the
    /// same bay via TryCommitStackedRack. This is what lets a fully pre-built multi-level structure
    /// name correctly when it's all initialized in one shot — not just when levels are added later.
    /// </summary>
    private void CommitAndLabelAisle(List<RackCollection> collections, int aisleNumber, ChevronController chevron)
    {
        if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();

        float chevronRotation = chevron != null ? chevron.CurrentRotation : 0f;
        bool firstCollectionEven = chevronRotation < 90f; // mirrors LocationNameGenerator's side rule
        Vector3 chevronPos = chevron != null ? chevron.transform.position : Vector3.zero;
        var liveMat = ResolveLiveMaterial();

        for (int colIndex = 0; colIndex < collections.Count; colIndex++)
        {
            var collection = collections[colIndex];
            bool isEvenSide = (colIndex == 0) ? firstCollectionEven : !firstCollectionEven;

            var groundRacks = GetGroundRacks(collection);

            // Travel direction is the CHEVRON'S arrow (the golden rule the player set) — NOT the
            // order the racks were dragged/placed. Order the ground racks along it so bay 1 is at the
            // chevron's start end and the position columns count up in the travel direction. (Both
            // sides of a two-sided aisle use the same chevron direction, so bays pair across.)
            Vector3 travelDir = ChevronTravelDir(chevron, groundRacks);
            SortAlong(groundRacks, travelDir);

            var bayNumbers = LocationNameGenerator.GenerateBayNumbers(groundRacks.Count, isEvenSide);

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

    /// <summary>
    /// The aisle's travel direction = the selected chevron's arrow. The RUN AXIS comes from the
    /// rack geometry (unambiguous — the line the racks form), and the SIGN comes from the chevron's
    /// arrow (transform.up points down the run in the travel direction, flipped 180° by the player's
    /// right-click). This is the golden rule: bay 1 sits at the chevron's start end regardless of
    /// which rack was placed first. Falls back to raw geometry only when there's no chevron.
    /// </summary>
    private Vector3 ChevronTravelDir(ChevronController chevron, List<GameObject> groundRacks)
    {
        Vector3 runAxis = ComputeTravelDir(groundRacks); // along the run; sign = placement order
        if (chevron != null)
        {
            Vector3 arrow = chevron.transform.up; arrow.y = 0f; // chevron arrow = its local +Y
            float d = Vector3.Dot(arrow, runAxis);
            if (Mathf.Abs(d) > 0.01f)
                return d >= 0f ? runAxis : -runAxis; // keep the run axis, flip its sign to the arrow
        }
        return runAxis;
    }

    /// <summary>Orders racks by their projection along <paramref name="dir"/> (earliest first).</summary>
    private static void SortAlong(List<GameObject> racks, Vector3 dir)
    {
        racks.Sort((a, b) =>
            Vector3.Dot(a.transform.position, dir).CompareTo(Vector3.Dot(b.transform.position, dir)));
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

        // Ground is level 0 — its char honors the aisle's Pick/Reserve scheme (Reserve at level 0 = "A").
        string levelChar = LocationNameGenerator.LevelChar(0, AisleRegistry.GetDesignations(aisle));

        var placed = rackGO.GetComponent<PlacedObject>();
        if (placed != null)
        {
            placed.isRackLive = true;
            placed.rackAisle = aisle;
            placed.rackBay = bay;
            placed.rackLevelIndex = 0;
            placed.rackLevelChar = levelChar;
            placed.rackTravelDir = travelDir.sqrMagnitude > 0.0001f ? travelDir.normalized : travelDir;
        }

        SetRackLabels(rackGO, aisle, bay, levelChar, TravelPositionResolver(rackGO, travelDir));

        Vector3 aisleFacing = GroundAisleDir(rackGO, travelDir, chevron, chevronPos);
        if (placed != null) placed.rackAisleFacing = aisleFacing.sqrMagnitude > 0.0001f ? aisleFacing.normalized : aisleFacing;
        ConfigureFaces(rackGO, aisleFacing);
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

    /// <summary>
    /// Enables the labels on the aisle-facing face and ALWAYS disables the opposite face — enable
    /// the rear labels ⇒ disable the front labels, and vice versa. A rack has exactly two label
    /// faces: front (+local Z / transform.forward) and rear (−forward); the L/R position columns are
    /// spread along local X. We classify each label by the sign of its offset along the rack's OWN
    /// forward axis (unambiguous front vs rear, pivot-proof and prefab-agnostic), then pick which
    /// face fronts the aisle with a single dot(forward, aisleDir). This is deliberately NOT a
    /// per-label projection of the full offset onto aisleDir: the two columns are ~1.38 units apart
    /// in X, so any run-axis component in aisleDir (e.g. the "across from" second-side case) could
    /// flip an individual label and leave a back-face label showing.
    /// </summary>
    private void ConfigureFaces(GameObject rackGO, Vector3 aisleDir)
    {
        Transform rt = rackGO.transform;
        Vector3 fwd = rt.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
        aisleDir.y = 0f;

        // Which physical face points toward the aisle: front (+forward) or rear (−forward).
        bool frontFacesAisle = Vector3.Dot(fwd, aisleDir) >= 0f;

        Vector3 center = rt.position;
        foreach (var tmp in rackGO.GetComponentsInChildren<TMPro.TextMeshPro>(true))
        {
            Vector3 d = tmp.transform.position - center; d.y = 0f;
            bool isFront = Vector3.Dot(d, fwd) >= 0f;      // the face this label sits on
            // Toggle the whole label GROUP object (e.g. LabelRear.L), not just its inner TMP_Label
            // child — disabling the group turns off the label and its nested text together, which
            // is what actually hides the unused face.
            LabelGroupUnderRoot(tmp.transform, rt).gameObject.SetActive(isFront == frontFacesAisle);
        }
    }

    /// <summary>The label-group object for a TMP: the ancestor that is a direct child of the rack
    /// root (falls back to the TMP's own object if it's already a direct child). Disabling this hides
    /// the group and its nested text in one shot.</summary>
    private static Transform LabelGroupUnderRoot(Transform label, Transform root)
    {
        Transform t = label;
        while (t.parent != null && t.parent != root) t = t.parent;
        return t;
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

    /// <summary>The committed aisle-facing direction of a rack: the stored value if present,
    /// else re-derived from its live labels (legacy racks committed before the field existed).</summary>
    private Vector3 RackFacingOf(PlacedObject po, GameObject go)
    {
        if (po != null && po.rackAisleFacing.sqrMagnitude > 0.0001f) return po.rackAisleFacing;
        return BelowAisleDir(go);
    }

    /// <summary>
    /// Re-hides the away face of a committed rack using its STORED aisle-facing direction. Call this
    /// after a rack is moved/rotated so the correct face stays hidden (a rotate would otherwise leave
    /// the previously-hidden face pointing at the aisle). No-op for racks that aren't live aisle racks
    /// or have no stored facing.
    /// </summary>
    public void ReapplyRackFaces(GameObject rackGO)
    {
        if (rackGO == null) return;
        var po = rackGO.GetComponent<PlacedObject>();
        if (po == null || !po.isRackLive) return;
        if (po.rackAisleFacing.sqrMagnitude < 0.0001f) return;
        ConfigureFaces(rackGO, po.rackAisleFacing);
    }

    /// <summary>
    /// Redraws rack labels after loading a save. Racks come back from SpawnFromSave with their
    /// structured fields restored (aisle/bay/level/facing/travel, via RackSaveCodec) but the TMP
    /// text still at the prefab default. This rewrites each live rack's labels and face visibility
    /// deterministically from those stored fields — no chevron or neighbour lookups required.
    /// </summary>
    public void RefreshAllRackLabelsAfterLoad()
    {
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || !po.isRackLive) continue;
            if (po.data == null || po.data.category != "Racking") continue;
            if (po.rackAisle < 0 || po.rackBay < 0 || po.rackLevelIndex < 0) continue;

            var go = po.gameObject;

            // Level char was persisted (per-aisle Pick/Reserve designations are runtime-only and
            // lost on load); fall back to the designation scheme only if it wasn't stored.
            string levelChar = !string.IsNullOrEmpty(po.rackLevelChar)
                ? po.rackLevelChar
                : LocationNameGenerator.LevelChar(po.rackLevelIndex, AisleRegistry.GetDesignations(po.rackAisle));

            // Travel direction was persisted so the position columns land the same way they did at
            // commit; fall back to the rack's run axis if missing (legacy saves).
            Vector3 travel = po.rackTravelDir;
            if (travel.sqrMagnitude < 0.0001f)
            {
                travel = go.transform.right; travel.y = 0f;
                if (travel.sqrMagnitude < 0.0001f) travel = Vector3.right;
            }

            SetRackLabels(go, po.rackAisle, po.rackBay, levelChar, TravelPositionResolver(go, travel.normalized));

            if (po.rackAisleFacing.sqrMagnitude > 0.0001f)
                ConfigureFaces(go, po.rackAisleFacing);
        }
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

    // ============================================================================================
    // IN-LINE EXTENSION of an already-finalized aisle (continuing its row of bays)
    // --------------------------------------------------------------------------------------------
    // A fresh ground rack placed IN LINE with (same row, same run axis as) a finalized aisle — past
    // the last bay, or dragged beyond the collection — is a CONTINUATION of that aisle, not a new
    // one. It commits live immediately (no ghost / chevron / setup UI), exactly like a rack stacked
    // on top. Two numbering cases:
    //   • Appended at the high-bay end  → just take the next bay up (ascending).
    //   • Prepended at the low-bay end   → bays can't go below 01/02, so RENUMBER the WHOLE aisle
    //     from the new start. If the aisle is two-sided, both sides renumber together (by physical
    //     position along travel) so the two rows stay paired and the pick path isn't scrambled.
    // ============================================================================================

    public bool TryCommitExtension(GameObject rackGO)
    {
        if (rackGO == null) return false;
        if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
        if (_grid == null) return false;

        var newPO = rackGO.GetComponent<PlacedObject>();
        if (newPO == null || newPO.data == null || newPO.data.category != "Racking") return false;

        var anchor = FindExtendableAisleRack(rackGO);
        if (anchor == null) return false;
        var anchorPO = anchor.GetComponent<PlacedObject>();
        if (anchorPO == null || anchorPO.rackAisle < 0) return false;

        int aisle = anchorPO.rackAisle;
        Vector3 facing = RackFacing(anchorPO);          // same row → same aisle-facing side
        Vector3 travelDir = TravelDirFromRack(anchor);  // aisle's bay-ascending direction

        // Commit live in place — same treatment as a stacked rack.
        var ghost = rackGO.GetComponent<RackGhost>();
        if (ghost != null) ghost.RestoreReal();
        var liveMat = ResolveLiveMaterial();
        if (liveMat != null) ApplyLiveMaterial(rackGO, liveMat);

        newPO.isRackLive = true;
        newPO.rackAisle = aisle;
        newPO.rackLevelIndex = 0;
        newPO.rackAisleFacing = facing.sqrMagnitude > 0.0001f ? facing.normalized : facing;

        if (IsPrependEnd(rackGO, aisle, newPO.rackAisleFacing, travelDir))
        {
            // Low-bay end: can't number below 01/02 — renumber the whole aisle from the new start.
            newPO.rackBay = 0; // placeholder; RenumberAisle assigns real bays for every rack
            RenumberAisle(aisle);
            Debug.Log($"Extension rack PREPENDED to aisle {aisle:D2} — whole aisle renumbered.");
        }
        else
        {
            // High-bay end: just continue ascending on this side (parity preserved by +2).
            int side = SideKey(newPO.rackAisleFacing);
            int maxBay = MaxBayOnSide(aisle, side, rackGO);
            int newBay = maxBay >= 1 ? maxBay + 2 : (anchorPO.rackBay % 2 == 0 ? 2 : 1);
            newPO.rackBay = newBay;
            string levelChar = LocationNameGenerator.LevelChar(0, AisleRegistry.GetDesignations(aisle));
            newPO.rackLevelChar = levelChar;
            newPO.rackTravelDir = travelDir.sqrMagnitude > 0.0001f ? travelDir.normalized : travelDir;
            SetRackLabels(rackGO, aisle, newBay, levelChar, TravelPositionResolver(rackGO, travelDir));
            ConfigureFaces(rackGO, newPO.rackAisleFacing);
            Debug.Log($"Extension rack APPENDED to aisle {aisle:D2} as bay {newBay:D2}.");
        }
        return true;
    }

    /// <summary>Nearest finalized GROUND rack that <paramref name="rackGO"/> continues IN LINE: same
    /// run axis, same row line (near-zero lateral offset), within 5 cells along the run, and nothing
    /// racking in between. That's what marks a continuation vs. a separate/parallel aisle.</summary>
    private GameObject FindExtendableAisleRack(GameObject rackGO)
    {
        float cell = _grid.CellSize;
        Vector3 p = rackGO.transform.position;
        Vector3 runAxis = rackGO.transform.right; runAxis.y = 0f;
        runAxis = runAxis.sqrMagnitude > 0.0001f ? runAxis.normalized : Vector3.right;
        Vector3 lat = rackGO.transform.forward; lat.y = 0f;
        lat = lat.sqrMagnitude > 0.0001f ? lat.normalized : Vector3.forward;

        GameObject best = null; float bestAlong = float.MaxValue;
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || !po.isRackLive || po.rackAisle < 0) continue;
            if (po.data == null || po.data.category != "Racking") continue;
            if (po.rackLevelIndex != 0) continue;
            var go = po.gameObject;
            if (go == rackGO) continue;
            if (!RackGridUtil.SameAxis(rackGO, go)) continue;

            Vector3 d = go.transform.position - p; d.y = 0f;
            float lateral = Vector3.Dot(d, lat);
            if (Mathf.Abs(lateral) > 0.6f * cell) continue;      // must share the row line, not be across
            float aalong = Mathf.Abs(Vector3.Dot(d, runAxis));
            if (aalong < 0.5f * cell || aalong > 5f * cell) continue;
            if (IsRackingBetween(p, go.transform.position, rackGO, go)) continue;
            if (aalong < bestAlong) { bestAlong = aalong; best = go; }
        }
        return best;
    }

    /// <summary>True if the new rack sits BEFORE the aisle's current lowest bay on its own side
    /// (travel-projection less than the existing minimum) — i.e. a prepend that needs renumbering.</summary>
    private bool IsPrependEnd(GameObject rackGO, int aisle, Vector3 facing, Vector3 travelDir)
    {
        int side = SideKey(facing);
        float newProj = Vector3.Dot(rackGO.transform.position, travelDir);
        float minProj = float.MaxValue;
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || !po.isRackLive || po.rackAisle != aisle) continue;
            if (po.data == null || po.data.category != "Racking" || po.rackLevelIndex != 0) continue;
            if (po.gameObject == rackGO) continue;
            if (SideKey(RackFacing(po)) != side) continue;
            float pr = Vector3.Dot(po.transform.position, travelDir);
            if (pr < minProj) minProj = pr;
        }
        if (minProj == float.MaxValue) return false;
        return newProj < minProj - 0.01f;
    }

    /// <summary>
    /// Wipes and regenerates every location name in an aisle. Bays are assigned by PHYSICAL POSITION
    /// along the aisle's travel direction (slot k → odd side 2k+1, even side 2k+2), with a single
    /// origin/pitch shared by both sides, so racks directly across from each other always get a
    /// paired bay number (N / N±1) and the pick path stays intact. Every level of every column is
    /// relabelled bottom-up. Used when a rack is prepended below bay 01/02.
    /// </summary>
    private void RenumberAisle(int aisle)
    {
        var racks = GetAisleRacks(aisle);
        var ground = new List<PlacedObject>();
        foreach (var po in racks) if (po.rackLevelIndex == 0) ground.Add(po);
        if (ground.Count == 0) return;

        var designations = AisleRegistry.GetDesignations(aisle);
        Vector3 travelDir = AisleTravelDir(ground);

        // Shared origin (aisle start) + bay pitch across BOTH sides.
        float minProj = float.MaxValue;
        foreach (var g in ground)
        {
            float pr = Vector3.Dot(g.transform.position, travelDir);
            if (pr < minProj) minProj = pr;
        }
        float pitch = BayPitch(ground, travelDir);

        // Each side keeps its parity (the side that was even stays even), read from a rack that still
        // has a real bay (skip the just-added placeholder with bay 0).
        var sideParity = new Dictionary<int, int>(); // sideKey -> bay%2 (0 even, 1 odd)
        foreach (var g in ground)
        {
            int sk = SideKey(RackFacing(g));
            if (!sideParity.ContainsKey(sk) && g.rackBay >= 1)
                sideParity[sk] = g.rackBay % 2;
        }

        foreach (var g in ground)
        {
            int sk = SideKey(RackFacing(g));
            int parity = sideParity.TryGetValue(sk, out var pv) ? pv : 1; // 0 even, 1 odd
            int slot = Mathf.RoundToInt((Vector3.Dot(g.transform.position, travelDir) - minProj) / pitch);
            int bay = 2 * slot + (parity == 0 ? 2 : 1);
            RelabelColumn(g, racks, aisle, bay, travelDir, designations);
        }
    }

    /// <summary>Relabels a whole vertical column: ground gets the new bay + travel-ordered positions,
    /// then each stacked level above (same cell) inherits the bay and its position from below.</summary>
    private void RelabelColumn(PlacedObject groundPO, List<PlacedObject> allRacks, int aisle, int bay, Vector3 travelDir, string[] designations)
    {
        var groundGO = groundPO.gameObject;
        groundPO.rackBay = bay;
        string lc0 = LocationNameGenerator.LevelChar(0, designations);
        groundPO.rackLevelChar = lc0;
        groundPO.rackTravelDir = travelDir.sqrMagnitude > 0.0001f ? travelDir.normalized : travelDir;
        SetRackLabels(groundGO, aisle, bay, lc0, TravelPositionResolver(groundGO, travelDir));
        ConfigureFaces(groundGO, RackFacing(groundPO));

        Vector2Int cell = _grid.WorldToCell(groundGO.transform.position);
        var stack = new List<PlacedObject>();
        foreach (var po in allRacks)
        {
            if (po == groundPO || po.rackLevelIndex <= 0) continue;
            if (_grid.WorldToCell(po.transform.position) != cell) continue;
            stack.Add(po);
        }
        stack.Sort((a, b) => a.rackLevelIndex.CompareTo(b.rackLevelIndex));

        GameObject below = groundGO;
        foreach (var s in stack)
        {
            s.rackBay = bay;
            string lc = LocationNameGenerator.LevelChar(s.rackLevelIndex, designations);
            s.rackLevelChar = lc;
            s.rackTravelDir = groundPO.rackTravelDir;
            AssignStackedRackLabels(s.gameObject, below, aisle, bay, lc);
            ConfigureFaces(s.gameObject, RackFacing(s));
            below = s.gameObject;
        }
    }

    /// <summary>Bay-ascending travel direction for the aisle, from an already-labelled rack.</summary>
    private Vector3 AisleTravelDir(List<PlacedObject> ground)
    {
        foreach (var g in ground)
        {
            if (g.rackBay < 1) continue;
            Vector3 t = TravelDirFromRack(g.gameObject);
            if (t.sqrMagnitude > 0.0001f) return t;
        }
        Vector3 r = ground[0].gameObject.transform.right; r.y = 0f;
        return r.sqrMagnitude > 0.0001f ? r.normalized : Vector3.right;
    }

    /// <summary>
    /// Physical spacing between adjacent bays along travel = the rack's footprint span along its run
    /// axis (in cells) × cell size. Derived from the footprint, NOT from inter-rack gaps: two rows
    /// of a 2-sided aisle share a travel-projection when directly across, and any slight misalignment
    /// between them would otherwise collapse a gap-based pitch and explode the slot numbers.
    /// </summary>
    private float BayPitch(List<PlacedObject> ground, Vector3 travelDir)
    {
        foreach (var g in ground)
        {
            if (g.data == null) continue;
            int alongCells = AlongRunCells(g);
            if (alongCells > 0) return alongCells * _grid.CellSize;
        }
        return _grid.CellSize * 2f; // full-bay default
    }

    /// <summary>How many grid cells a rack spans along its own run axis (footprint width).</summary>
    private int AlongRunCells(PlacedObject po)
    {
        if (po == null || po.data == null) return 0;
        float rotDeg = RackGridUtil.RotationStep(po.gameObject) * 90f;
        var offsets = po.data.GetFootprintOffsets(-rotDeg);
        if (offsets == null || offsets.Length == 0) return Mathf.Max(1, po.data.footprint.x);

        // Run axis in grid space is the rack's local X → footprint extent along cell X for a
        // 0/180° rack, along cell Y for a 90/270° rack. Use whichever matches the run orientation.
        int step = RackGridUtil.RotationStep(po.gameObject);
        bool runAlongCellX = (step % 2) == 0;
        int min = int.MaxValue, max = int.MinValue;
        foreach (var o in offsets)
        {
            int v = runAlongCellX ? o.x : o.y;
            if (v < min) min = v;
            if (v > max) max = v;
        }
        return (max - min) + 1;
    }

    private List<PlacedObject> GetAisleRacks(int aisle)
    {
        var list = new List<PlacedObject>();
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || !po.isRackLive || po.rackAisle != aisle) continue;
            if (po.data == null || po.data.category != "Racking") continue;
            list.Add(po);
        }
        return list;
    }

    private int MaxBayOnSide(int aisle, int sideKey, GameObject exclude)
    {
        int max = -1;
        foreach (var po in GetAisleRacks(aisle))
        {
            if (po.rackLevelIndex != 0 || po.gameObject == exclude) continue;
            if (SideKey(RackFacing(po)) != sideKey) continue;
            if (po.rackBay > max) max = po.rackBay;
        }
        return max;
    }

    /// <summary>A quantized key for which SIDE of an aisle a rack is on, from its aisle-facing
    /// direction. Opposite-facing rows get distinct keys.</summary>
    private int SideKey(Vector3 facing)
    {
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f) return 0;
        facing.Normalize();
        return Mathf.RoundToInt(facing.x) * 10 + Mathf.RoundToInt(facing.z);
    }

    private Vector3 RackFacing(PlacedObject po)
    {
        if (po == null) return Vector3.forward;
        if (po.rackAisleFacing.sqrMagnitude > 0.0001f) return po.rackAisleFacing;
        return BelowAisleDir(po.gameObject);
    }

}
