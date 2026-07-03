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
///   • figures out its OWN closest door → that door's number is the leading digit, and
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
    private const bool LetterFromHighWallCoord = false;

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

    private struct Tile { public TMP_Text label; public Vector3 pos; }

    public void Recompute()
    {
        var doors = DockSlot.All;
        if (doors == null || doors.Count == 0) return;

        // Collect lane tiles (self-identified by their LaneNo label).
        var tiles = new List<Tile>();
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || !po.gameObject.activeInHierarchy) continue;
            var label = FindLaneLabel(po.gameObject);
            if (label != null) tiles.Add(new Tile { label = label, pos = po.transform.position });
        }
        if (tiles.Count == 0) return;

        if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
        float cell = _grid != null ? _grid.CellSize : 1.33f;

        // 1. Each lane tile belongs to its nearest door. Group tiles PER DOOR first — critical when
        //    doors sit on different/opposite walls (e.g. x=36 vs x=67): two pads can occupy the same
        //    wall-coordinate, so a global grouping would wrongly merge them into one lane.
        var tilesByDoor = new Dictionary<DockSlot, List<Tile>>();
        foreach (var t in tiles)
        {
            var door = NearestDoor(doors, t.pos);
            if (door == null) continue;
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
                string name = door.DoorNumber.ToString() + IndexToLetters(i);
                foreach (var t in lanes[keys[i]])
                    if (t.label != null) t.label.text = name;
            }
        }
    }

    private static DockSlot NearestDoor(List<DockSlot> doors, Vector3 pos)
    {
        DockSlot best = null;
        float bestSqr = float.MaxValue;
        foreach (var d in doors)
        {
            if (d == null) continue;
            Vector3 dp = d.transform.position;
            float sqr = (dp.x - pos.x) * (dp.x - pos.x) + (dp.z - pos.z) * (dp.z - pos.z);
            if (sqr < bestSqr) { bestSqr = sqr; best = d; }
        }
        return best;
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
