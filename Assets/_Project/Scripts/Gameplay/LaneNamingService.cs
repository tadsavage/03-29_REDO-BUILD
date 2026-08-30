using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using GameCore.Events;

/// <summary>
/// Auto-names shipping lanes as "&lt;doorNumber&gt;&lt;letter&gt;" (e.g. 1A, 1B … 2A, 2B …).
///
/// A shipping lane is one ROW of Flr-ShipLane tiles running out from the dock wall (the tiles of a
/// row share a coordinate along the wall and extend in depth). Each lane:
///   • is OWNED by exactly one door, and that ownership is STICKY — locked into each tile's
///     PlacedObject.customData the first time it's assigned, so a door placed nearby later can never
///     steal already-owned lanes (only unowned/orphaned tiles get a fresh owner). The owning door's
///     number is the leading digit. Deleting a door releases (orphans) its lanes for reassignment.
///   • is lettered A, B, C… among the lanes that share a door, ordered along the wall so that A is
///     on the same side as the highest-numbered door (image-left with the current dock).
///
/// The Flr-ShipLane prefab already carries a child TextMeshPro named "LaneNo" (this is how a lane
/// tile is identified — no hard-coded asset id), and this service just writes the computed name into
/// every lane tile's label. Self-bootstraps after scene load like DockNumberingService, so it works
/// with no scene wiring, and recomputes whenever objects are placed/deleted (plus a slow heartbeat
/// to catch save-loads, which bypass the placement events).
/// </summary>
public class LaneNamingService : MonoBehaviour
{
    /// <summary>Lets load/restore code force an immediate, synchronous re-tag once it knows every
    /// placed object (doors AND lane tiles) is fully instantiated — the 1s heartbeat below exists to
    /// self-heal the same gap probabilistically, but a save-restore knows exactly when it's done and
    /// shouldn't have to wait on a timer for correct labels. See PlacementSystem.BakeAfterDestroyFlush,
    /// which does the same thing for rack labels via AisleInitializer.RefreshAllRackLabelsAfterLoad.</summary>
    public static LaneNamingService Instance => _instance;

    private static LaneNamingService _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[LaneNamingService]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LaneNamingService>();
    }

    // Per spec: within a door, the lane with the LOWEST grid-Y (lowest wall coordinate) is "A",
    // then ascending (B, C, …). Set true only if a differently-oriented dock needs the reverse.
    // static readonly (not const) so flipping this doesn't fold the "if" below into
    // compiler-detected unreachable code — it's a real runtime toggle, just left off by default.
    private static readonly bool LetterFromHighWallCoord = false;

    private bool _subscribed;
    private bool _dirty = true;
    private float _nextHeartbeat;
    private PlacementGrid _grid;

    private void OnEnable() => TrySubscribe();

    private void Update()
    {
        if (!_subscribed) TrySubscribe();

        // Heartbeat: catch tiles restored from a save (they bypass OnObjectPlaced).
        if (Time.unscaledTime >= _nextHeartbeat)
        {
            _nextHeartbeat = Time.unscaledTime + 1f;
            _dirty = true;
        }

        if (_dirty)
        {
            _dirty = false;
            Recompute();
        }
    }

    private void TrySubscribe()
    {
        var em = EventManager.Instance;
        if (em == null) return;
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnChanged);
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnChanged);
        _subscribed = true;
        _dirty = true;
    }

    private void OnChanged(string _, PlacedObject __) => _dirty = true;

    private void OnDestroy()
    {
        var em = EventManager.Instance;
        if (em == null || !_subscribed) return;
        em.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnChanged);
        em.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnChanged);
    }

    // A lane tile is any active placed object with a "LaneNo" TextMeshPro child.
    private static TMP_Text FindLaneLabel(GameObject go)
    {
        var t = go.transform.Find("LaneNo");
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private struct Tile { public PlacedObject po; public TMP_Text label; public Vector3 pos; public Vector2Int cell; public string zoneTag; }

    // Zone tag stored in a lane tile's PlacedObject.customData, alongside (and distinct from) the
    // plain-int door-owner format OwnerNumber reads — a tile carrying this never gets door-claimed
    // by ChooseDoorForNewTile below, since int.TryParse on "ZONE:name" always fails, which correctly
    // routes it through the same unowned-tile path... except that path WOULD normally then hand it a
    // door via nearest-door fallback. See Recompute(): zone tiles are still grouped/lettered under
    // whichever door they resolve to geometrically (reusing that pass's depth-axis/lettering, which
    // needs SOME directional reference), but their published IDENTITY (name, LaneSlot.DoorNumber,
    // LaneGeometry key) is overridden to the zone afterward — geometry and identity are deliberately
    // decoupled so a zone lane doesn't need its own from-scratch axis-detection logic.
    private const string ZoneTagPrefix = "ZONE:";

    private static bool TryReadZoneTag(PlacedObject po, out string zoneName)
    {
        zoneName = null;
        if (po == null || string.IsNullOrEmpty(po.customData) || !po.customData.StartsWith(ZoneTagPrefix)) return false;
        zoneName = po.customData.Substring(ZoneTagPrefix.Length);
        return !string.IsNullOrEmpty(zoneName);
    }

    /// <summary>Tags (or un-tags, passing null/empty) every tile of one lane as belonging to a
    /// standalone Zone instead of its geometrically-nearest door. Called by LaneSetupUI. Forces an
    /// immediate Recompute so the change is visible without waiting on the 1s heartbeat.</summary>
    public static void SetZoneTag(int doorNumber, string lane, string zoneNameOrNull)
    {
        string tag = string.IsNullOrWhiteSpace(zoneNameOrNull) ? null : ZoneTagPrefix + zoneNameOrNull.Trim();
        var cells = new HashSet<Vector2Int>(GetLane(doorNumber, lane).ConvertAll(s => s.Cell));
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || !cells.Contains(new Vector2Int(po.gridX, po.gridY))) continue;
            if (FindLaneLabel(po.gameObject) == null) continue; // only actual lane tiles
            po.customData = tag ?? string.Empty;
        }
        _instance?.Recompute();
    }

    /// <summary>
    /// The addressable "spot" a lane tile represents: door number + lane letter + slot index, where
    /// slot 1 is the tile NEAREST the door (so slot order == truck load order / FIFO). Purely derived
    /// from world geometry every Recompute; the pallet/inventory layer maps a pallet's grid cell to
    /// one of these to answer "what spot is pallet X in?". Name format mirrors racks: e.g. "1A-03".
    /// </summary>
    public struct LaneSlot
    {
        public int DoorNumber;
        public string Lane;    // "A", "B", …
        public int Slot;       // 1-based, counted out from the door
        public Vector2Int Cell;

        /// <summary>Negative DoorNumber = a standalone Zone lane (see ZoneRegistry), so the display
        /// name uses the zone's name instead of the raw pseudo-door id.</summary>
        public string Name => $"{ZoneRegistry.DisplayPrefix(DoorNumber)}{Lane}-{Slot}";
    }

    // ── Lane Geometry ─────────────────────────────────────────────────────────────────────────────
    // How far beyond the last slot the exit staging point is placed (metres).
    private const float ExitPointOffset = 2f;

    /// <summary>
    /// World-space geometry for one lane, computed each <see cref="Recompute"/> pass.
    /// Used by the Reach Truck Operator to navigate to the correct entry side and to drive
    /// forks-first in a straight line along the lane axis.
    /// </summary>
    public struct LaneGeometry
    {
        /// <summary>Staging point 2 m beyond the far end of the lane (exit/open-floor side).
        /// The RTO navigates here via NavMesh, then drives forks-first into the lane.</summary>
        public Vector3 ExitPoint;
        /// <summary>World position of slot 1 (nearest the dock/door).</summary>
        public Vector3 EntryPoint;
        /// <summary>Unit vector pointing FROM the dock wall OUTWARD through the lane (slot 1 → slot N direction).</summary>
        public Vector3 DepthAxis;
        /// <summary>Total number of slots in this lane.</summary>
        public int SlotCount;
    }

    // Lane key ("1C") → geometry, rebuilt each Recompute.
    private static readonly Dictionary<string, LaneGeometry> _laneGeoByKey = new();

    // Cell → world position, rebuilt each Recompute. Lets callers map a LaneSlot.Cell
    // to a world-space grab target without re-scanning PlacedObjectRegistry.
    private static readonly Dictionary<Vector2Int, Vector3> _worldPosByCell = new();

    // cell → slot, rebuilt fresh each Recompute. Static so the plain-C# InventoryService (no scene
    // reference to this hidden singleton) can query addresses directly.
    private static readonly Dictionary<Vector2Int, LaneSlot> _slotByCell = new();

    /// <summary>Lane address for a grid cell, or false if that cell isn't a lane tile.</summary>
    public static bool TryGetSlot(Vector2Int cell, out LaneSlot slot) => _slotByCell.TryGetValue(cell, out slot);

    /// <summary>Address string ("1A-03") for a cell, or null if it isn't a lane tile.</summary>
    public static string AddressAt(Vector2Int cell) => _slotByCell.TryGetValue(cell, out var s) ? s.Name : null;

    /// <summary>All slots in a given lane (door number + letter), ordered slot 1..N out from the door.</summary>
    public static List<LaneSlot> GetLane(int doorNumber, string lane)
        => _slotByCell.Values
            .Where(s => s.DoorNumber == doorNumber && s.Lane == lane)
            .OrderBy(s => s.Slot)
            .ToList();

    /// <summary>Every distinct (door, lane-letter) pair that currently has tiles, so callers can scan
    /// for a lane matching some criteria (e.g. the first Inbound/Both lane with a free slot).</summary>
    public static List<(int door, string lane)> AllLanes()
        => _slotByCell.Values
            .Select(s => (s.DoorNumber, s.Lane))
            .Distinct()
            .OrderBy(x => x.DoorNumber).ThenBy(x => x.Lane)
            .ToList();

    // ── Lane Geometry API ─────────────────────────────────────────────────────────────────────────

    /// <summary>Exit-point geometry for a lane (door + letter). Returns false if the lane has no tiles
    /// or <see cref="Recompute"/> has not yet run for it.</summary>
    public static bool TryGetLaneGeometry(int doorNumber, string lane, out LaneGeometry geo)
        => _laneGeoByKey.TryGetValue(LaneConfigRegistry.Key(doorNumber, lane), out geo);

    /// <summary>World position of a lane tile by its grid cell. Returns false if the cell is not
    /// a known lane tile.</summary>
    public static bool TryGetSlotWorldPos(Vector2Int cell, out Vector3 worldPos)
        => _worldPosByCell.TryGetValue(cell, out worldPos);

    /// <summary>
    /// Parses a lane slot address into its components. Two shapes:
    ///   • Door lane: "{digits}{letters}-{digits}" (e.g. "1C-3") — door number, lane letter, slot.
    ///   • Zone lane: "{zoneName}-{letters}-{digits}" (e.g. "QA-A-3") — a zone has no numeric prefix
    ///     to split on, so it carries an extra dash between its name and lane letter instead. The
    ///     zone name must already be registered (see ZoneRegistry) — an unrecognized name fails
    ///     rather than silently minting a new zone from parsed text.
    /// Returns false if the string doesn't match either format.
    /// </summary>
    public static bool TryParseLaneAddress(string address,
        out int    doorNumber,
        out string laneLetter,
        out int    slotNumber)
    {
        doorNumber = 0;
        laneLetter = null;
        slotNumber = 0;

        if (string.IsNullOrEmpty(address)) return false;

        // Split on the LAST dash for the slot number — a door address has exactly one dash total
        // ("1C-3"), so this is identical to splitting on the first; a zone address has two ("QA-A-3")
        // and this correctly isolates the trailing slot digits either way.
        int lastDash = address.LastIndexOf('-');
        if (lastDash < 0) return false;
        if (!int.TryParse(address.Substring(lastDash + 1), out slotNumber)) return false;

        string prefix = address.Substring(0, lastDash);

        int zoneDash = prefix.LastIndexOf('-');
        if (zoneDash >= 0)
        {
            // Zone shape: "{zoneName}-{letters}".
            string zoneName = prefix.Substring(0, zoneDash);
            laneLetter = prefix.Substring(zoneDash + 1);
            return ZoneRegistry.TryGetPseudoDoor(zoneName, out doorNumber);
        }

        // Door shape: identify the boundary between leading digits (door) and trailing letters (lane).
        int numEnd = 0;
        while (numEnd < prefix.Length && char.IsDigit(prefix[numEnd])) numEnd++;

        if (numEnd == 0) return false; // no digits at all

        if (!int.TryParse(prefix.Substring(0, numEnd), out doorNumber)) return false;
        laneLetter = prefix.Substring(numEnd); // might be empty string, which is fine
        return true;
    }

    // Depth (units out from the dock wall) that a lane may extend and still "belong" to a door. Used
    // only to pick an owner for a brand-new (unowned) lane tile — see ChooseDoorForNewTile.
    private const float MaxLaneDepth = 12f;

    // A lane tile's owning door NUMBER is persisted in its PlacedObject.customData (lane tiles use
    // customData for nothing else) so it survives save/load and, critically, never gets stolen by a
    // door placed nearby later — same rule as door numbers themselves. 0 = not yet owned.
    private static int OwnerNumber(PlacedObject po)
        => (po != null && int.TryParse(po.customData, out int n) && n > 0) ? n : 0;

    private static void SetOwner(PlacedObject po, int number)
    {
        if (po == null) return;
        string s = number.ToString();
        if (po.customData != s) po.customData = s; // only write on change to avoid churn/dirtying
    }

    public void Recompute()
    {
        _slotByCell.Clear();    // rebuilt from scratch below; source of truth is live geometry
        _laneGeoByKey.Clear();
        _worldPosByCell.Clear();

        var doors = DockSlot.All;
        if (doors == null || doors.Count == 0) return;

        // Fast lookup of a door by its (stable) number — how owned tiles find their door back.
        var doorByNumber = new Dictionary<int, DockSlot>();
        foreach (var d in doors)
            if (d != null && d.DoorNumber > 0) doorByNumber[d.DoorNumber] = d;

        // Collect lane tiles (self-identified by their LaneNo label).
        var tiles = new List<Tile>();
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || !po.gameObject.activeInHierarchy) continue;
            var label = FindLaneLabel(po.gameObject);
            if (label != null)
            {
                TryReadZoneTag(po, out string zoneTag);
                tiles.Add(new Tile
                {
                    po = po,
                    label = label,
                    pos = po.transform.position,
                    cell = new Vector2Int(po.gridX, po.gridY),
                    zoneTag = zoneTag
                });
            }
        }
        if (tiles.Count == 0) return;

        if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
        float cell = _grid != null ? _grid.CellSize : 1.33f;

        // 1. Resolve each tile's owner door NUMBER, in strict priority order:
        //    (a) its OWN persisted owner — sticky, never re-stolen by a nearer door (the lock);
        //    (b) ADJACENCY — a fresh tile that physically touches an already-owned lane tile joins
        //        THAT pad. This is what makes extending a dock's lane block grow the existing pad
        //        instead of getting grabbed by whatever door happens to be closest (the door-5 tiles
        //        that got picked up by door 7 bug). Propagates outward so a whole strip of new tiles
        //        laid against a pad all join it;
        //    (c) only a tile touching NO existing lane tile falls back to the nearest door it faces.
        //    Ownership is then persisted so it locks. (Per-door grouping below is also critical when
        //    doors sit on different/opposite walls — two pads can share a wall-coordinate.)
        var ownerByCell = new Dictionary<Vector2Int, int>();
        var unassigned = new List<Tile>();
        foreach (var t in tiles)
        {
            int owner = OwnerNumber(t.po);
            // Zone-tagged tiles never get a persisted door owner (writing one via SetOwner would
            // clobber the tag) — they still need a GEOMETRY-ONLY door reference below (adjacency /
            // nearest-door) purely to borrow that pass's depth-axis/lettering convention, so they go
            // through the same unowned-tile resolution but skip every SetOwner call along the way.
            if (!string.IsNullOrEmpty(t.zoneTag)) { unassigned.Add(t); continue; }
            if (owner > 0 && doorByNumber.ContainsKey(owner)) ownerByCell[t.cell] = owner; // (a)
            else unassigned.Add(t);
        }

        // (b) Grow ownership from owned tiles to touching unowned tiles until a pass changes nothing.
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = unassigned.Count - 1; i >= 0; i--)
            {
                var t = unassigned[i];
                if (!TryGetAdjacentOwner(ownerByCell, t.cell, out int adjOwner)) continue;
                ownerByCell[t.cell] = adjOwner;
                if (string.IsNullOrEmpty(t.zoneTag)) SetOwner(t.po, adjOwner); // don't clobber a zone tag
                unassigned.RemoveAt(i);
                changed = true;
            }
        }

        // (c) Isolated tiles (touching no pad) pick the nearest door they face.
        foreach (var t in unassigned)
        {
            var door = ChooseDoorForNewTile(doors, t.pos, cell);
            if (door == null) continue;
            ownerByCell[t.cell] = door.DoorNumber;
            if (string.IsNullOrEmpty(t.zoneTag)) SetOwner(t.po, door.DoorNumber); // don't clobber a zone tag
        }

        // Group tiles under their resolved owner door.
        var tilesByDoor = new Dictionary<DockSlot, List<Tile>>();
        foreach (var t in tiles)
        {
            if (!ownerByCell.TryGetValue(t.cell, out int ownerNum)) continue;
            if (!doorByNumber.TryGetValue(ownerNum, out var door)) continue;
            if (!tilesByDoor.TryGetValue(door, out var list)) tilesByDoor[door] = list = new List<Tile>();
            list.Add(t);
        }

        // 2. Per door: lanes RUN in the door's facing (depth) direction and sit side-by-side along the
        //    perpendicular WALL axis. So the wall axis is derived from the door's own orientation — not
        //    from where the doors happen to sit relative to each other. Group tiles by the wall-axis
        //    cell (= one lane), then letter along the wall (A = lowest wall coordinate, per spec).
        foreach (var kv in tilesByDoor)
        {
            DockSlot door = kv.Key;
            List<Tile> group = kv.Value;

            Vector3 fwd = door.transform.forward;
            bool wallIsZ = Mathf.Abs(fwd.x) >= Mathf.Abs(fwd.z); // door faces along X → lanes separate along Z
            float Wall(Vector3 p) => wallIsZ ? p.z : p.x;
            // Depth = distance out from the door along its facing; slot 1 = smallest depth (nearest door).
            Vector3 depthAxis = new Vector3(fwd.x, 0f, fwd.z).normalized;
            float Depth(Vector3 p) => Vector3.Dot(p - door.transform.position, depthAxis);

            var lanes = new Dictionary<int, List<Tile>>();
            foreach (var t in group)
            {
                int key = Mathf.RoundToInt(Wall(t.pos) / cell);
                if (!lanes.TryGetValue(key, out var l)) lanes[key] = l = new List<Tile>();
                l.Add(t);
            }

            var keys = lanes.Keys.ToList();
            keys.Sort();                                   // ascending wall coordinate
            if (LetterFromHighWallCoord) keys.Reverse();   // default false → A = lowest wall coord

            for (int i = 0; i < keys.Count; i++)
            {
                string letter = IndexToLetters(i);

                // Order this lane's tiles by depth so slot 1 is nearest the door (== load order), and
                // publish each tile's addressable spot keyed by its grid cell for the inventory layer.
                var laneTiles = lanes[keys[i]];
                laneTiles.Sort((a, b) => Depth(a.pos).CompareTo(Depth(b.pos)));

                // Geometry (depth axis, lettering, entry/exit points) always comes from `door` — the
                // geometrically-nearest one, borrowed purely for its facing/wall-axis convention. But
                // if any tile here is Zone-tagged, this lane's PUBLISHED IDENTITY (name, DoorNumber
                // key, LaneGeometry key) is the zone's, not the door's — see SetZoneTag.
                string zoneTag = laneTiles.Find(t => !string.IsNullOrEmpty(t.zoneTag)).zoneTag;
                int identityDoor = string.IsNullOrEmpty(zoneTag) ? door.DoorNumber : ZoneRegistry.GetOrCreate(zoneTag);
                string laneName = ZoneRegistry.DisplayPrefix(identityDoor) + letter;

                int lastIndex = laneTiles.Count - 1;
                for (int s = 0; s < laneTiles.Count; s++)
                {
                    var t = laneTiles[s];
                    int slot = s + 1;
                    _slotByCell[t.cell] = new LaneSlot
                    {
                        DoorNumber = identityDoor,
                        Lane = letter,
                        Slot = slot,
                        Cell = t.cell
                    };

                    if (t.label == null) continue;

                    // Only the two ENTRY tiles carry a label: slot 1 = Lane In (nearest door), slot N =
                    // Lane Out (far end). They read "<lane>-<slot>" (e.g. 1A-1, 1A-6). Every interior
                    // tile's label is hidden so the lane isn't cluttered with the name on every cell.
                    bool isEntry = (s == 0 || s == lastIndex);
                    if (isEntry)
                    {
                        t.label.enabled = true;
                        t.label.text = $"{laneName}-{slot}";
                    }
                    else
                    {
                        t.label.enabled = false;
                    }
                }

                // ── World positions + exit geometry (for RTO lane entry) ───────────────────────
                foreach (var t in laneTiles)
                    _worldPosByCell[t.cell] = t.pos;

                if (laneTiles.Count > 0)
                {
                    string laneKey = LaneConfigRegistry.Key(identityDoor, letter);
                    // ExitPoint: 2 m past the far-end slot, along the depth direction (away from dock).
                    Vector3 exitPt = laneTiles[lastIndex].pos + depthAxis * ExitPointOffset;
                    exitPt.y = laneTiles[0].pos.y; // keep floor level
                    _laneGeoByKey[laneKey] = new LaneGeometry
                    {
                        ExitPoint  = exitPt,
                        EntryPoint = laneTiles[0].pos,
                        DepthAxis  = depthAxis,
                        SlotCount  = laneTiles.Count
                    };
                }
            }
        }
    }

    private static readonly Vector2Int[] Neighbors4 =
        { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

    // True if any orthogonally-adjacent cell is already owned; returns that owner door number.
    private static bool TryGetAdjacentOwner(Dictionary<Vector2Int, int> ownerByCell, Vector2Int cell, out int owner)
    {
        foreach (var d in Neighbors4)
            if (ownerByCell.TryGetValue(cell + d, out owner)) return true;
        owner = 0;
        return false;
    }

    // Picks the owner for a lane tile that has none yet. A lane extends OUT from the door it serves,
    // so the right owner is the door the tile sits most directly IN FRONT of (smallest lateral offset
    // from the door's centerline), among doors it's actually in front of and within lane reach. Only
    // if the tile is in front of no door do we fall back to plain nearest-by-distance. This is more
    // correct than raw distance (a door off to the side but a hair closer can't claim a lane that
    // clearly runs out from a different door), and it's what makes the first-time assignment land on
    // the intended door before ownership locks it in.
    private static DockSlot ChooseDoorForNewTile(List<DockSlot> doors, Vector3 pos, float cell)
    {
        DockSlot bestFront = null; float bestLateral = float.MaxValue;
        DockSlot bestAny   = null; float bestDist    = float.MaxValue;
        foreach (var d in doors)
        {
            if (d == null) continue;
            Vector3 rel = pos - d.transform.position; rel.y = 0f;
            float dist = rel.sqrMagnitude;
            if (dist < bestDist) { bestDist = dist; bestAny = d; }

            Vector3 fwd = d.transform.forward; fwd.y = 0f;
            Vector3 right = d.transform.right; right.y = 0f;
            fwd.Normalize(); right.Normalize();
            float depth   = Vector3.Dot(rel, fwd);            // + = out into the yard, in front of door
            float lateral = Mathf.Abs(Vector3.Dot(rel, right)); // sideways offset from door centerline
            if (depth >= -cell && depth <= MaxLaneDepth && lateral < bestLateral)
            { bestLateral = lateral; bestFront = d; }
        }
        return bestFront != null ? bestFront : bestAny;
    }

    // 0→A, 1→B … 25→Z, 26→AA … (Excel-style; only ever single letters in practice).
    private static string IndexToLetters(int index)
    {
        string s = "";
        index++;
        while (index > 0)
        {
            int r = (index - 1) % 26;
            s = (char)('A' + r) + s;
            index = (index - 1) / 26;
        }
        return s;
    }
}
