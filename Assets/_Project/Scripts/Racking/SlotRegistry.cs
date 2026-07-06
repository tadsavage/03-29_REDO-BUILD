using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using GameCore.Events;

/// <summary>
/// Scans the scene for live, committed racks and exposes every addressable Pick/Reserve slot
/// ("01-02-00") for the Slotting UI (SlotAssignmentPanel) to assign SKUs against, and for future
/// Putaway logic to route pallets through. Self-bootstraps like LaneNamingService/DockNumberingService
/// — no scene wiring — and recomputes on placement/deletion events plus a 1s heartbeat (to catch
/// racks restored from a save, which bypass those events).
///
/// A live rack GameObject has exactly 2 ACTIVE TextMeshPro children (the aisle-facing label pair —
/// AisleInitializer disables the far face's label group entirely). Each label's own text IS the
/// slot's address; the position digit (0/1) is never stored anywhere else, so it's read straight off
/// that text rather than re-derived from geometry.
/// </summary>
public class SlotRegistry : MonoBehaviour
{
    private static SlotRegistry _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[SlotRegistry]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SlotRegistry>();
    }

    public struct Slot
    {
        public string Address;       // exact label text, e.g. "01-02-00"
        public int Aisle;
        public int Bay;
        public string LevelChar;     // "0","1","A","B"...
        public int Position;         // 0 or 1
        public bool IsPick;          // true if LevelChar is numeric
        public PlacedObject Rack;    // back-reference — .data.objHeight, .transform.position, etc.
        public TextMeshPro Label;    // the actual label component, for recoloring / click targeting
    }

    private static readonly Dictionary<string, Slot> _slotsByAddress = new();

    public static IEnumerable<Slot> AllSlots => _slotsByAddress.Values;
    public static IEnumerable<Slot> PickSlots => _slotsByAddress.Values.Where(s => s.IsPick);
    public static IEnumerable<Slot> ReserveSlots => _slotsByAddress.Values.Where(s => !s.IsPick);

    public static bool TryGet(string address, out Slot slot) => _slotsByAddress.TryGetValue(address, out slot);

    /// <summary>Nearest Reserve slot to a given Pick slot, by XZ distance between rack ROOT positions
    /// (not label position, which is offset outward per-face and would bias the comparison). No
    /// occupancy filtering yet (pallet-to-rack occupancy tracking doesn't exist until Putaway is
    /// built) — this only tells you "physically closest," for future Putaway logic to combine with
    /// whatever occupancy check it adds.</summary>
    public static bool TryGetNearestReserveSlot(string pickAddress, out Slot reserveSlot)
    {
        reserveSlot = default;
        if (!TryGet(pickAddress, out var pick) || pick.Rack == null) return false;

        float bestSqr = float.PositiveInfinity;
        bool found = false;
        Vector3 origin = pick.Rack.transform.position;

        foreach (var candidate in ReserveSlots)
        {
            if (candidate.Rack == null) continue;
            Vector3 d = candidate.Rack.transform.position - origin;
            float sqr = d.x * d.x + d.z * d.z;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                reserveSlot = candidate;
                found = true;
            }
        }

        return found;
    }

    private bool _subscribed;
    private bool _dirty = true;
    private float _nextHeartbeat;

    private void OnEnable() => TrySubscribe();

    private void Update()
    {
        if (!_subscribed) TrySubscribe();

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

    public void Recompute()
    {
        _slotsByAddress.Clear(); // source of truth is live geometry — full rebuild every pass

        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || !po.isRackLive) continue;
            if (po.data == null || po.data.category != "Racking") continue;

            var labels = po.GetComponentsInChildren<TextMeshPro>(false);
            if (labels == null || labels.Length != 2)
            {
                if (labels != null && labels.Length != 0)
                    Debug.LogWarning($"[SlotRegistry] Rack '{po.name}' has {labels.Length} active labels, expected 2 — skipping.");
                continue;
            }

            foreach (var label in labels)
            {
                if (!TryParseAddress(label.text, out var slot)) continue;
                slot.Rack = po;
                slot.Label = label;
                _slotsByAddress[slot.Address] = slot;
            }
        }
    }

    // Address format is "AA-BB-Lp" — aisle, bay, then one level-char + one position digit run
    // together as the third segment (e.g. "00", "A1").
    private static bool TryParseAddress(string text, out Slot slot)
    {
        slot = default;
        if (string.IsNullOrEmpty(text)) return false;

        var parts = text.Split('-');
        if (parts.Length != 3 || parts[2].Length != 2) return false;

        if (!int.TryParse(parts[0], out int aisle)) return false;
        if (!int.TryParse(parts[1], out int bay)) return false;

        string levelChar = parts[2].Substring(0, 1);
        if (!int.TryParse(parts[2].Substring(1, 1), out int position)) return false;

        slot.Address = text;
        slot.Aisle = aisle;
        slot.Bay = bay;
        slot.LevelChar = levelChar;
        slot.Position = position;
        slot.IsPick = char.IsDigit(levelChar[0]);
        return true;
    }
}
