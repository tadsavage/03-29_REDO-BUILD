using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Places chevrons around each uninitialized rack collection — but only on the OPEN sides.
/// A side blocked by an adjacent parallel collection (back-to-back racking) gets no chevrons,
/// so two adjacent rows each get 2 chevrons on their outer face rather than 8 crammed between them.
///
/// Each open side gets 2 chevrons (one at each end of the run). The two chevrons on a side are a
/// team (flip together, green together). Everything is recomputed whenever ANY collection changes,
/// because placing/removing a neighbour can block or open a side.
///
/// COMBINED CHEVRONS (two-sided aisle): when two uninitialized collections face each other across a
/// clear corridor (parallel, within <see cref="MAX_AISLE_GAP"/> cells, nothing racking between,
/// overlapping run range), their two facing chevron pairs are replaced by ONE pair of chevrons
/// centered in the corridor. Double-clicking a combined chevron sets up BOTH rows as one two-sided
/// aisle. Only the "primary" collection of the pair hosts the combined chevrons.
/// </summary>
public class ChevronSpawner : MonoBehaviour
{
    [Header("Chevron Configuration")]
    [SerializeField] private Sprite _chevronSprite;
    [SerializeField] private Material _chevronMaterial;
    [SerializeField] private float _chevronY = 1.15f;

    public void SetSprite(Sprite sprite) => _chevronSprite = sprite;
    public void SetMaterial(Material material) => _chevronMaterial = material;
    public void SetHeight(float height) => _chevronY = height;
    public void SetGrid(PlacementGrid grid) => _grid = grid;

    private RackCollectionDetector _detector;
    private PlacementGrid _grid;
    private Material _selectedMaterial; // green highlight, built lazily from _chevronMaterial

    // Slot keys — two sides ("neg" = perpMin-1, "pos" = perpMax+1), each with a Start and End chevron.
    private const string NegStart = "Neg_Start", NegEnd = "Neg_End", PosStart = "Pos_Start", PosEnd = "Pos_End";
    // Combined corridor chevrons (hosted by the primary collection of a facing pair).
    private const string CombStart = "Comb_Start", CombEnd = "Comb_End";

    // Two uninitialized rows are treated as a single two-sided aisle when the empty corridor between
    // them is this many cells or fewer.
    private const int MAX_AISLE_GAP = 5;

    private readonly Dictionary<RackCollection, Dictionary<string, GameObject>> _chevrons = new();
    private readonly Dictionary<RackCollection, ChevronGroup> _groups = new();
    // Separate selection group for a collection's combined (corridor) chevrons.
    private readonly Dictionary<RackCollection, ChevronGroup> _combinedGroups = new();

    private void Start()
    {
        _detector = GetComponent<RackCollectionDetector>();
        if (_detector != null)
        {
            _detector.OnCollectionCreated += _ => RefreshAll();
            _detector.OnCollectionAdded += _ => RefreshAll();
            _detector.OnCollectionMerged += HandleMerged;
            _detector.OnCollectionRemoved += HandleRemoved;
        }
    }

    private void HandleMerged(RackCollection survivor, RackCollection absorbed)
    {
        DeleteChevrons(absorbed);
        RefreshAll();
    }

    private void HandleRemoved(RackCollection collection)
    {
        DeleteChevrons(collection);
        RefreshAll(); // a neighbour may have just re-opened
    }

    /// <summary>Refresh every uninitialized collection — a change to one can block/open another's side,
    /// or form/dissolve a two-sided-aisle pairing.</summary>
    private void RefreshAll()
    {
        if (_detector == null) return;
        if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
        if (_grid == null) return;

        var collections = _detector.GetUninitializedCollections();

        // 1. Extent (bbox / run axis) for each uninitialized collection.
        var extents = new List<AisleExtent>();
        foreach (var c in collections)
        {
            var e = ComputeExtent(c);
            if (e != null) extents.Add(e);
        }

        // 2. Greedily pair collections that face each other across a clear corridor. Each collection
        //    joins at most one pair.
        var pairs = new Dictionary<RackCollection, FacingPair>();
        for (int i = 0; i < extents.Count; i++)
        {
            if (pairs.ContainsKey(extents[i].collection)) continue;
            for (int j = i + 1; j < extents.Count; j++)
            {
                if (pairs.ContainsKey(extents[j].collection)) continue;
                if (TryMakeFacingPair(extents[i], extents[j], out var p1, out var p2))
                {
                    pairs[extents[i].collection] = p1;
                    pairs[extents[j].collection] = p2;
                    break;
                }
            }
        }

        // 3. Refresh each collection: suppress the shared corridor side, primary hosts the combined pair.
        foreach (var e in extents)
        {
            pairs.TryGetValue(e.collection, out var pair);
            RefreshChevrons(e, pair);
        }

        // Newly created/repositioned chevron colliders otherwise aren't registered with PhysX until
        // the next FixedUpdate — which never comes while Time.timeScale is 0 (game paused). Without
        // this, a chevron spawned while paused is invisible to ChevronController's Physics.Raycast
        // until the player unpauses for at least one tick, which is exactly the "chevrons are
        // unresponsive while paused" symptom Tad hit placing racks with the speed control at 0.
        Physics.SyncTransforms();
    }

    /// <summary>Bounding box + run orientation of a collection in grid-cell space.</summary>
    private AisleExtent ComputeExtent(RackCollection collection)
    {
        if (collection == null || collection.Racks.Count == 0) return null;

        var cells = new HashSet<Vector2Int>();
        foreach (var rack in collection.Racks)
            if (rack != null)
                foreach (var c in RackGridUtil.GetCells(rack, _grid))
                    cells.Add(c);
        if (cells.Count == 0) return null;

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var c in cells)
        {
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.y > maxY) maxY = c.y;
        }

        bool runAlongY = (maxY - minY) >= (maxX - minX);
        return new AisleExtent
        {
            collection = collection,
            runAlongY = runAlongY,
            alongMin = runAlongY ? minY : minX,
            alongMax = runAlongY ? maxY : maxX,
            perpMin = runAlongY ? minX : minY,
            perpMax = runAlongY ? maxX : maxY,
        };
    }

    /// <summary>
    /// True if two uninitialized collections face each other across a clear corridor and should share
    /// one combined chevron pair. Outputs each collection's pairing (which side is shared, who hosts).
    /// </summary>
    private bool TryMakeFacingPair(AisleExtent e1, AisleExtent e2, out FacingPair p1, out FacingPair p2)
    {
        p1 = null; p2 = null;
        if (e1.runAlongY != e2.runAlongY) return false; // must be parallel rows

        // Order by perpendicular so 'low' sits below 'high' along the perp axis.
        AisleExtent low = e1, high = e2;
        if (e1.perpMin > e2.perpMin) { low = e2; high = e1; }
        if (high.perpMin <= low.perpMax) return false;   // overlapping perp ⇒ not two separate rows

        int gap = high.perpMin - low.perpMax - 1;         // empty cells between the two rows
        if (gap < 1 || gap > MAX_AISLE_GAP) return false; // gap 0 = back-to-back; >MAX = not one aisle

        // They must share some run length (a real corridor between them).
        int ovMin = Mathf.Max(low.alongMin, high.alongMin);
        int ovMax = Mathf.Min(low.alongMax, high.alongMax);
        if (ovMin > ovMax) return false;

        // Nothing racking parked in the corridor between them.
        if (CorridorBlocked(low, high, ovMin, ovMax)) return false;

        Quaternion facing = FacingDown(low.runAlongY);
        Vector3 start = CorridorMid(low.runAlongY, ovMin, low.perpMax, high.perpMin);
        Vector3 end = CorridorMid(low.runAlongY, ovMax, low.perpMax, high.perpMin);

        // Deterministic primary (lower instance id) hosts the two combined chevrons.
        // GetInstanceID() is obsolete in favor of GetEntityId(), but that returns an EntityId struct
        // whose ordering/comparison semantics aren't confirmed equivalent here — swapping it blind
        // risks silently changing which collection becomes primary. Suppressed, not replaced.
#pragma warning disable CS0618
        bool lowIsPrimary = low.collection.GetInstanceID() <= high.collection.GetInstanceID();
#pragma warning restore CS0618
        RackCollection primary = lowIsPrimary ? low.collection : high.collection;
        RackCollection secondary = lowIsPrimary ? high.collection : low.collection;

        var pairLow = new FacingPair
        {
            sharedSide = "pos",                 // low's pos side faces high
            isPrimary = (primary == low.collection),
            secondary = secondary,
            combinedStart = start, combinedEnd = end, facing = facing,
        };
        var pairHigh = new FacingPair
        {
            sharedSide = "neg",                 // high's neg side faces low
            isPrimary = (primary == high.collection),
            secondary = secondary,
            combinedStart = start, combinedEnd = end, facing = facing,
        };

        if (low == e1) { p1 = pairLow; p2 = pairHigh; }
        else { p1 = pairHigh; p2 = pairLow; }
        return true;
    }

    /// <summary>True if any racking sits in the corridor cells strictly between the two rows.</summary>
    private bool CorridorBlocked(AisleExtent low, AisleExtent high, int ovMin, int ovMax)
    {
        for (int perp = low.perpMax + 1; perp <= high.perpMin - 1; perp++)
            for (int along = ovMin; along <= ovMax; along++)
            {
                var objs = _grid.GetObjectsInCell(MakeCell(low.runAlongY, along, perp));
                if (objs == null) continue;
                foreach (var e in objs)
                    if (e.data != null && e.data.category == "Racking") return true;
            }
        return false;
    }

    /// <summary>World point centered in the corridor at a given along-run coordinate.</summary>
    private Vector3 CorridorMid(bool runAlongY, int along, int lowPerpMax, int highPerpMin)
    {
        Vector3 a = _grid.GetCellCenter(MakeCell(runAlongY, along, lowPerpMax + 1));
        Vector3 b = _grid.GetCellCenter(MakeCell(runAlongY, along, highPerpMin - 1));
        Vector3 mid = (a + b) * 0.5f;
        mid.y = _chevronY;
        return mid;
    }

    /// <summary>Flat resting orientation that points a chevron down the run.</summary>
    private Quaternion FacingDown(bool runAlongY)
    {
        Vector3 runWorldDir = runAlongY ? Vector3.forward : Vector3.right;
        // Flat arrow lies along +Z after Euler(90,0,0), so yaw from +Z to the run.
        float yaw = Vector3.SignedAngle(Vector3.forward, runWorldDir, Vector3.up);
        return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
    }

    private void RefreshChevrons(AisleExtent ext, FacingPair pair)
    {
        var collection = ext.collection;
        bool runAlongY = ext.runAlongY;
        int alongMin = ext.alongMin, alongMax = ext.alongMax;
        int negPerp = ext.perpMin - 1; // one side of the run
        int posPerp = ext.perpMax + 1; // the other side

        // A side is open only if nothing (no other rack row) sits along it.
        bool negOpen = !IsSideBlocked(negPerp, alongMin, alongMax, runAlongY);
        bool posOpen = !IsSideBlocked(posPerp, alongMin, alongMax, runAlongY);

        // The side that forms the shared corridor of a facing pair is replaced by the combined
        // chevrons — suppress its individual pair.
        if (pair != null)
        {
            if (pair.sharedSide == "neg") negOpen = false;
            else if (pair.sharedSide == "pos") posOpen = false;
        }

        Quaternion facing = FacingDown(runAlongY);

        if (!_chevrons.TryGetValue(collection, out var slots))
        {
            slots = new Dictionary<string, GameObject>();
            _chevrons[collection] = slots;
        }
        if (!_groups.TryGetValue(collection, out var group))
        {
            group = new ChevronGroup();
            _groups[collection] = group;
        }

        // (slotKey, cell, open)
        ApplySlot(collection, slots, NegStart, MakeCell(runAlongY, alongMin, negPerp), negOpen, facing, "neg");
        ApplySlot(collection, slots, NegEnd,   MakeCell(runAlongY, alongMax, negPerp), negOpen, facing, "neg");
        ApplySlot(collection, slots, PosStart, MakeCell(runAlongY, alongMin, posPerp), posOpen, facing, "pos");
        ApplySlot(collection, slots, PosEnd,   MakeCell(runAlongY, alongMax, posPerp), posOpen, facing, "pos");

        // Rebuild side-teams from whatever chevrons currently exist, then re-apply green.
        var negTeam = ControllersFor(slots, NegStart, NegEnd);
        var posTeam = ControllersFor(slots, PosStart, PosEnd);

        var selectedMat = GetSelectedMaterial();
        foreach (var c in negTeam) { c.SetSideTeam(negTeam); c.SetGroup(group); c.SetSideKey("neg"); c.SetSecondaryCollection(null); c.SetSelectionMaterials(_chevronMaterial, selectedMat); }
        foreach (var c in posTeam) { c.SetSideTeam(posTeam); c.SetGroup(group); c.SetSideKey("pos"); c.SetSecondaryCollection(null); c.SetSelectionMaterials(_chevronMaterial, selectedMat); }

        group.SetSide("neg", negTeam);
        group.SetSide("pos", posTeam);
        group.ApplyColors();

        RefreshCombined(collection, slots, pair);
    }

    /// <summary>
    /// Hosts (or tears down) the two combined corridor chevrons for a collection. Only the PRIMARY of a
    /// facing pair hosts them; everyone else clears any stale combined chevrons they might still hold
    /// (e.g. after the partner was deleted/initialized and the pairing dissolved).
    /// </summary>
    private void RefreshCombined(RackCollection collection, Dictionary<string, GameObject> slots, FacingPair pair)
    {
        bool host = pair != null && pair.isPrimary;
        if (!host)
        {
            RemoveSlot(slots, CombStart);
            RemoveSlot(slots, CombEnd);
            _combinedGroups.Remove(collection);
            return;
        }

        if (!_combinedGroups.TryGetValue(collection, out var cGroup))
        {
            cGroup = new ChevronGroup();
            _combinedGroups[collection] = cGroup;
        }

        EnsureCombined(collection, slots, CombStart, pair.combinedStart, pair.facing);
        EnsureCombined(collection, slots, CombEnd,   pair.combinedEnd,   pair.facing);

        var team = ControllersFor(slots, CombStart, CombEnd);
        var selectedMat = GetSelectedMaterial();
        foreach (var c in team)
        {
            c.SetSideTeam(team);
            c.SetGroup(cGroup);
            c.SetSideKey("combined");
            c.SetSecondaryCollection(pair.secondary); // so double-click inits BOTH rows
            c.SetSelectionMaterials(_chevronMaterial, selectedMat);
        }
        cGroup.SetSide("combined", team);
        cGroup.ApplyColors();
    }

    private void EnsureCombined(RackCollection collection, Dictionary<string, GameObject> slots,
        string key, Vector3 worldPos, Quaternion facing)
    {
        if (!slots.TryGetValue(key, out var go) || go == null)
        {
            go = CreateChevron(collection, key);
            slots[key] = go;
        }
        go.transform.position = worldPos;
        var controller = go.GetComponent<ChevronController>();
        if (controller != null) controller.SetBaseFacing(facing);
    }

    private void RemoveSlot(Dictionary<string, GameObject> slots, string key)
    {
        if (slots.TryGetValue(key, out var go))
        {
            if (go != null) Destroy(go);
            slots.Remove(key);
        }
    }

    private void ApplySlot(RackCollection collection, Dictionary<string, GameObject> slots,
        string key, Vector2Int cell, bool open, Quaternion facing, string sideKey)
    {
        if (open)
        {
            if (!slots.TryGetValue(key, out var go) || go == null)
            {
                go = CreateChevron(collection, key);
                slots[key] = go;
            }

            Vector3 world = _grid.GetCellCenter(cell);
            world.y = _chevronY;
            go.transform.position = world;

            var controller = go.GetComponent<ChevronController>();
            if (controller != null) controller.SetBaseFacing(facing);
        }
        else
        {
            if (slots.TryGetValue(key, out var go))
            {
                if (go != null) Destroy(go);
                slots.Remove(key);
            }
        }
    }

    private List<ChevronController> ControllersFor(Dictionary<string, GameObject> slots, string a, string b)
    {
        var list = new List<ChevronController>(2);
        if (slots.TryGetValue(a, out var ga) && ga != null)
        {
            var c = ga.GetComponent<ChevronController>();
            if (c != null) list.Add(c);
        }
        if (slots.TryGetValue(b, out var gb) && gb != null)
        {
            var c = gb.GetComponent<ChevronController>();
            if (c != null) list.Add(c);
        }
        return list;
    }

    /// <summary>True if any rack sits along the given side line (so it's not open for travel).</summary>
    private bool IsSideBlocked(int perp, int alongMin, int alongMax, bool runAlongY)
    {
        for (int a = alongMin; a <= alongMax; a++)
        {
            var objs = _grid.GetObjectsInCell(MakeCell(runAlongY, a, perp));
            if (objs == null) continue;
            foreach (var e in objs)
                if (e.data != null && e.data.category == "Racking")
                    return true;
        }
        return false;
    }

    private static Vector2Int MakeCell(bool runAlongY, int along, int perp)
    {
        return runAlongY ? new Vector2Int(perp, along) : new Vector2Int(along, perp);
    }

    private GameObject CreateChevron(RackCollection collection, string slot)
    {
        var chevronGO = new GameObject($"Chevron_{slot}_{collection.name}");

        chevronGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        chevronGO.transform.localScale = Vector3.one * 0.7f; // 30% smaller
        // Parent under the collection so chevrons are destroyed with it (no orphans).
        chevronGO.transform.parent = collection.transform;

        var spriteRenderer = chevronGO.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = _chevronSprite;
        spriteRenderer.material = _chevronMaterial;

        var collider = chevronGO.AddComponent<BoxCollider>();
        collider.center = Vector3.zero;
        collider.size = new Vector3(2f, 2f, 0.5f);
        collider.isTrigger = false;

        var controller = chevronGO.AddComponent<ChevronController>();
        controller.Initialize(collection);

        return chevronGO;
    }

    private Material GetSelectedMaterial()
    {
        if (_selectedMaterial == null && _chevronMaterial != null)
        {
            _selectedMaterial = new Material(_chevronMaterial);
            Color green = new Color(0.15f, 1f, 0.2f, 1f);

            if (_selectedMaterial.HasProperty("_Color")) _selectedMaterial.SetColor("_Color", green);
            if (_selectedMaterial.HasProperty("_BaseColor")) _selectedMaterial.SetColor("_BaseColor", green);
            if (_selectedMaterial.HasProperty("_EmissionColor")) _selectedMaterial.SetColor("_EmissionColor", green * 2f);
            _selectedMaterial.color = green;
        }
        return _selectedMaterial;
    }

    public void DeleteChevrons(RackCollection collection)
    {
        if (_chevrons.TryGetValue(collection, out var slots))
        {
            foreach (var kv in slots)
                if (kv.Value != null) Destroy(kv.Value);
            _chevrons.Remove(collection);
        }
        _groups.Remove(collection);
        _combinedGroups.Remove(collection);
    }

    public void DeleteAllChevrons(params RackCollection[] collections)
    {
        foreach (var collection in collections)
            DeleteChevrons(collection);
    }

    /// <summary>Bounding box + run orientation of an uninitialized collection, in grid-cell space.</summary>
    private class AisleExtent
    {
        public RackCollection collection;
        public bool runAlongY;
        public int alongMin, alongMax, perpMin, perpMax;
    }

    /// <summary>One collection's role in a facing (two-sided-aisle) pairing.</summary>
    private class FacingPair
    {
        public string sharedSide;          // "neg"/"pos": which of THIS collection's sides is the corridor
        public bool isPrimary;             // this collection hosts the two combined chevrons
        public RackCollection secondary;   // the OTHER row (stamped onto the combined chevrons)
        public Vector3 combinedStart, combinedEnd;
        public Quaternion facing;
    }
}
