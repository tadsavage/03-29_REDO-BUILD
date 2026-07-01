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

    private readonly Dictionary<RackCollection, Dictionary<string, GameObject>> _chevrons = new();
    private readonly Dictionary<RackCollection, ChevronGroup> _groups = new();

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

    /// <summary>Refresh every uninitialized collection — a change to one can block/open another's side.</summary>
    private void RefreshAll()
    {
        if (_detector == null) return;
        foreach (var collection in _detector.GetUninitializedCollections())
            RefreshChevrons(collection);
    }

    private void RefreshChevrons(RackCollection collection)
    {
        if (_grid == null) _grid = FindFirstObjectByType<PlacementGrid>();
        if (collection == null || _grid == null || collection.Racks.Count == 0) return;

        // Union of every cell the collection occupies.
        var cells = new HashSet<Vector2Int>();
        foreach (var rack in collection.Racks)
            if (rack != null)
                foreach (var c in RackGridUtil.GetCells(rack, _grid))
                    cells.Add(c);
        if (cells.Count == 0) return;

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var c in cells)
        {
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.y > maxY) maxY = c.y;
        }

        bool runAlongY = (maxY - minY) >= (maxX - minX);
        Vector3 runWorldDir = runAlongY ? Vector3.forward : Vector3.right;

        int alongMin = runAlongY ? minY : minX;
        int alongMax = runAlongY ? maxY : maxX;
        int perpMin = runAlongY ? minX : minY;
        int perpMax = runAlongY ? maxX : maxY;

        int negPerp = perpMin - 1; // one side of the run
        int posPerp = perpMax + 1; // the other side

        // A side is open only if nothing (no other rack row) sits along it.
        bool negOpen = !IsSideBlocked(negPerp, alongMin, alongMax, runAlongY);
        bool posOpen = !IsSideBlocked(posPerp, alongMin, alongMax, runAlongY);

        // Point all chevrons down the run (flat arrow lies along +Z, so yaw from +Z to the run).
        float yaw = Vector3.SignedAngle(Vector3.forward, runWorldDir, Vector3.up);
        Quaternion facing = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

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
        foreach (var c in negTeam) { c.SetSideTeam(negTeam); c.SetGroup(group); c.SetSideKey("neg"); c.SetSelectionMaterials(_chevronMaterial, selectedMat); }
        foreach (var c in posTeam) { c.SetSideTeam(posTeam); c.SetGroup(group); c.SetSideKey("pos"); c.SetSelectionMaterials(_chevronMaterial, selectedMat); }

        group.SetSide("neg", negTeam);
        group.SetSide("pos", posTeam);
        group.ApplyColors();
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
    }

    public void DeleteAllChevrons(params RackCollection[] collections)
    {
        foreach (var collection in collections)
            DeleteChevrons(collection);
    }
}
