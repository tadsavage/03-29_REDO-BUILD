using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Drag-in / drag-out behaviour shared by every widget that can live in the bottom widget tray
/// (BuildMenuUI.WidgetTray): the dev HUD (FPS/graphics), the systems log, the mini dock schedule and the
/// player score box.
///
/// <para>The tray has three dock zones - LEFT, CENTER and RIGHT (BuildMenuUI.TrayZone). Left-press and
/// drag a docked card a few pixels and it lifts out of its slot (a blue ghost holds the slot); carry it
/// anywhere over the tray and the ghost follows into whichever third of the tray the card is over, at
/// the position among that zone's cards; let go to dock it there. Let go away from the tray and it stays
/// floating where it was dropped. A card keeps its docked look while it is still over the tray and only
/// switches to its floating look once it is carried clear of it.</para>
///
/// <para>Docked/floating, the zone and the floating position persist in PlayerPrefs under the card's
/// key. Each widget supplies its own look for the two states through <c>applyLayout(docked)</c>.</para>
///
/// <para>Pickability: RaycastController.Start() sets every UIDocument root's direct children to
/// PickingMode.Ignore once at scene start, which silently made a floating card undraggable (the reason
/// the FPS card "wouldn't move at all", 2026-11-08). The card re-asserts its picking mode every 250 ms.</para>
/// </summary>
public sealed class DockableHudCard
{
    public const int ZoneLeft = 0, ZoneCenter = 1, ZoneRight = 2;

    /// <summary>Pixels a docked card has to be dragged before it lifts out of its slot, so a plain
    /// click (or double-click) on a docked card never disturbs it.</summary>
    private const float DetachThreshold = 6f;

    private readonly VisualElement _card;
    private readonly Func<VisualElement> _floatRoot;
    private readonly Action<bool> _applyLayout;
    private readonly string _prefKey;
    private readonly int _defaultZone;
    private readonly bool _defaultDocked;
    private readonly Vector2 _defaultFloatPos;
    private readonly bool _pickableWhenFloating;
    private readonly DraggableWindow _dragger;
    private VisualElement _ghost;
    private bool _lookDocked;

    public bool Docked { get; private set; }
    public int Zone { get; private set; }
    public VisualElement Card => _card;
    public DraggableWindow Dragger => _dragger;

    /// <summary>True once the player has dropped this card somewhere off the tray at least once.</summary>
    public bool HasSavedFloatPosition => PlayerPrefs.HasKey(_prefKey + "_X");

    /// <summary>Fired after the card docks (true) or settles floating (false) at the end of a drag.</summary>
    public event Action<bool> DockChanged;

    /// <param name="card">The widget's root element (moved between the tray and <paramref name="floatRoot"/>).</param>
    /// <param name="handle">Where a press starts a drag (presses on descendants bubble up to it).</param>
    /// <param name="floatRoot">The full-screen element a floating card is parented to.</param>
    /// <param name="applyLayout">Applies the widget's own docked (true) / floating (false) look. Position,
    /// left/top and the parent are this class's job and are set after it runs.</param>
    /// <param name="pickableWhenFloating">False for a card that should be click-through while floating
    /// (only its pickable children - e.g. a title bar - start a drag then).</param>
    public DockableHudCard(VisualElement card, VisualElement handle, Func<VisualElement> floatRoot, string prefKey,
                           int defaultZone, bool defaultDocked, Vector2 defaultFloatPos, Action<bool> applyLayout,
                           bool pickableWhenFloating, params VisualElement[] excluded)
    {
        _card = card;
        _floatRoot = floatRoot;
        _prefKey = prefKey;
        _defaultZone = defaultZone;
        _defaultDocked = defaultDocked;
        _defaultFloatPos = defaultFloatPos;
        _applyLayout = applyLayout ?? (_ => { });
        _pickableWhenFloating = pickableWhenFloating;
        Zone = defaultZone;

        _dragger = new DraggableWindow(card, handle, null, excluded);
        _dragger.OnDragStart += OnDragStart;
        _dragger.OnDragMove += OnDragMove;
        _dragger.OnDragEnd += OnDragEnd;

        card.schedule.Execute(KeepPickable).Every(250);
    }

    private static VisualElement Tray => BuildMenuUI.Instance != null ? BuildMenuUI.Instance.WidgetTray : null;
    private static VisualElement ZoneElement(int zone) => BuildMenuUI.Instance != null ? BuildMenuUI.Instance.TrayZone(zone) : null;

    // ── Placement ────────────────────────────────────────────────────

    /// <summary>Puts the card where the player left it last session (or its default). Needs the tray to
    /// exist to dock; returns false (and floats it) when it doesn't, so the caller can retry.</summary>
    public bool Restore()
    {
        bool floating = PlayerPrefs.HasKey(_prefKey + "_Floating")
            ? PlayerPrefs.GetInt(_prefKey + "_Floating") == 1
            : !_defaultDocked;
        Zone = Mathf.Clamp(PlayerPrefs.GetInt(_prefKey + "_Zone", _defaultZone), ZoneLeft, ZoneRight);

        if (!floating)
        {
            var zone = ZoneElement(Zone);
            if (zone == null) { FloatAt(SavedOrDefaultPos(), true); return false; }
            DockInto(zone, -1);
            return true;
        }

        FloatAt(SavedOrDefaultPos(), true);
        _card.schedule.Execute(ClampOnScreen).ExecuteLater(60);
        return true;
    }

    /// <summary>Re-docks a docked card whose tray was rebuilt (scene reload) or that isn't in it yet.</summary>
    public void EnsureInTray()
    {
        if (!Docked) return;
        var zone = ZoneElement(Zone);
        if (zone == null || _card.parent == zone) return;
        DockInto(zone, -1);
    }

    /// <summary>Docks the card into <paramref name="zone"/> (one of the tray's zones), at the end when
    /// <paramref name="index"/> is negative.</summary>
    public void DockInto(VisualElement zone, int index)
    {
        if (zone == null) return;
        _card.RemoveFromHierarchy();
        if (index < 0 || index > zone.childCount) zone.Add(_card);
        else zone.Insert(index, _card);

        Docked = true;
        _lookDocked = true;
        Zone = ZoneIndexOf(zone);
        _applyLayout(true);
        _card.style.position = Position.Relative;
        _card.style.left = StyleKeyword.Null;
        _card.style.top = StyleKeyword.Null;
        _card.style.right = StyleKeyword.Null;
        _card.style.bottom = StyleKeyword.Null;
        KeepPickable();
    }

    private void FloatAt(Vector2 localPos, bool applyFloatingLook)
    {
        var root = _floatRoot?.Invoke();
        if (root == null) return;
        _card.RemoveFromHierarchy();
        root.Add(_card);
        Docked = false;
        if (applyFloatingLook)
        {
            _applyLayout(false);
            _lookDocked = false;
        }
        _card.style.position = Position.Absolute;
        _card.style.left = localPos.x;
        _card.style.top = localPos.y;
        _card.style.right = StyleKeyword.Auto;
        _card.style.bottom = StyleKeyword.Auto;
        _card.style.marginLeft = 0;
        _card.style.marginRight = 0;
        _card.style.marginTop = 0;
        _card.style.marginBottom = 0;
        KeepPickable();
    }

    private Vector2 SavedOrDefaultPos() => HasSavedFloatPosition
        ? new Vector2(PlayerPrefs.GetFloat(_prefKey + "_X"), PlayerPrefs.GetFloat(_prefKey + "_Y"))
        : _defaultFloatPos;

    private void KeepPickable()
    {
        var want = Docked || _pickableWhenFloating || _dragger.IsDragging ? PickingMode.Position : PickingMode.Ignore;
        if (_card.pickingMode != want) _card.pickingMode = want;
    }

    private void ClampOnScreen()
    {
        var root = _floatRoot?.Invoke();
        if (Docked || root == null) return;
        float w = root.resolvedStyle.width, h = root.resolvedStyle.height;
        float cw = _card.resolvedStyle.width, ch = _card.resolvedStyle.height;
        if (float.IsNaN(w) || float.IsNaN(h) || float.IsNaN(cw) || float.IsNaN(ch) || w < 1f) return;
        // Never let a floating card sit under the tray either.
        float maxBottom = h - (Tray != null ? BuildMenuUI.BottomBarHeight : 0f);
        _card.style.left = Mathf.Clamp(_card.style.left.value.value, 0f, Mathf.Max(0f, w - cw));
        _card.style.top = Mathf.Clamp(_card.style.top.value.value, 0f, Mathf.Max(0f, maxBottom - ch));
    }

    // ── Drag ─────────────────────────────────────────────────────────

    private void OnDragStart()
    {
        // A docked card first moves as a relative offset inside its slot (nothing around it reflows);
        // OnDragMove lifts it out once it has really moved.
        if (Docked) _dragger.PositionMode = Position.Relative;
        KeepPickable();
    }

    private void OnDragMove()
    {
        if (Docked)
        {
            float dx = _card.style.left.keyword == StyleKeyword.Undefined ? _card.style.left.value.value : 0f;
            float dy = _card.style.top.keyword == StyleKeyword.Undefined ? _card.style.top.value.value : 0f;
            if (Mathf.Abs(dx) < DetachThreshold && Mathf.Abs(dy) < DetachThreshold) return;

            var slotZone = _card.parent;
            int slotIndex = slotZone != null ? slotZone.IndexOf(_card) : -1;
            _dragger.RebaseDuringDrag(() =>
            {
                var root = _floatRoot?.Invoke();
                if (root == null) return;
                Vector2 local = _card.worldBound.position - root.worldBound.position;
                var size = _card.worldBound.size;
                FloatAt(local, applyFloatingLook: false);   // keep the docked look while over the tray
                if (slotZone != null) ShowGhostAt(slotZone, slotIndex, size);
            }, Position.Absolute);
            return;
        }

        var tray = Tray;
        if (tray == null || !tray.worldBound.Overlaps(_card.worldBound))
        {
            RemoveGhost();
            if (_lookDocked)
            {
                _applyLayout(false);
                _lookDocked = false;
            }
            return;
        }

        var zone = ZoneElement(ZoneAt(tray, _card.worldBound.center.x));
        if (zone != null) ShowGhostAt(zone, InsertIndex(zone), _card.worldBound.size);
    }

    private void OnDragEnd()
    {
        if (Docked)
        {
            // Never moved far enough to lift out: snap back into the slot.
            _card.style.position = Position.Relative;
            _card.style.left = StyleKeyword.Null;
            _card.style.top = StyleKeyword.Null;
            _card.style.right = StyleKeyword.Null;
            _card.style.bottom = StyleKeyword.Null;
            KeepPickable();
            return;
        }

        var zone = _ghost != null ? _ghost.parent : null;
        int index = zone != null ? zone.IndexOf(_ghost) : -1;
        RemoveGhost();

        if (zone != null)
        {
            DockInto(zone, index);
            Save();
            DockChanged?.Invoke(true);
            return;
        }

        if (_lookDocked)
        {
            _applyLayout(false);
            _lookDocked = false;
        }
        _card.schedule.Execute(() => { ClampOnScreen(); Save(); }).ExecuteLater(30);
        KeepPickable();
        DockChanged?.Invoke(false);
    }

    private void Save()
    {
        PlayerPrefs.SetInt(_prefKey + "_Floating", Docked ? 0 : 1);
        PlayerPrefs.SetInt(_prefKey + "_Zone", Zone);
        if (!Docked && _card.style.left.keyword == StyleKeyword.Undefined && _card.style.top.keyword == StyleKeyword.Undefined)
        {
            PlayerPrefs.SetFloat(_prefKey + "_X", _card.style.left.value.value);
            PlayerPrefs.SetFloat(_prefKey + "_Y", _card.style.top.value.value);
        }
        PlayerPrefs.Save();
    }

    // ── Zones and the ghost ──────────────────────────────────────────

    /// <summary>Which third of the tray a screen x falls in.</summary>
    private static int ZoneAt(VisualElement tray, float x)
    {
        var b = tray.worldBound;
        if (b.width < 1f) return ZoneLeft;
        float t = (x - b.xMin) / b.width;
        return t < 1f / 3f ? ZoneLeft : (t < 2f / 3f ? ZoneCenter : ZoneRight);
    }

    private static int ZoneIndexOf(VisualElement zone)
    {
        for (int z = ZoneLeft; z <= ZoneRight; z++)
            if (ZoneElement(z) == zone) return z;
        return ZoneLeft;
    }

    private int InsertIndex(VisualElement zone)
    {
        float cx = _card.worldBound.center.x;
        int index = 0;
        foreach (var child in zone.Children())
        {
            if (child == _ghost || child == _card) continue;
            if (child.resolvedStyle.display == DisplayStyle.None) { index++; continue; }
            if (cx > child.worldBound.center.x) index++;
            else break;
        }
        return index;
    }

    private void ShowGhostAt(VisualElement zone, int index, Vector2 size)
    {
        if (_ghost == null)
        {
            _ghost = new VisualElement { name = "DockGhost", pickingMode = PickingMode.Ignore };
            _ghost.style.flexShrink = 0;
            _ghost.style.alignSelf = Align.Center;
            _ghost.style.marginLeft = 8;
            _ghost.style.marginRight = 8;
            _ghost.style.backgroundColor = new Color(0.35f, 0.55f, 0.85f, 0.18f);
            var c = new Color(0.45f, 0.65f, 0.95f, 0.9f);
            _ghost.style.borderTopColor = _ghost.style.borderBottomColor = _ghost.style.borderLeftColor = _ghost.style.borderRightColor = c;
            _ghost.style.borderTopWidth = _ghost.style.borderBottomWidth = _ghost.style.borderLeftWidth = _ghost.style.borderRightWidth = 2;
            _ghost.style.borderTopLeftRadius = _ghost.style.borderTopRightRadius = _ghost.style.borderBottomLeftRadius = _ghost.style.borderBottomRightRadius = 8;
        }
        _ghost.style.width = size.x;
        _ghost.style.height = Mathf.Min(size.y, BuildMenuUI.BottomBarHeight - 8f);

        // The ghost's own presence shifts indices by one once it's in this zone; compare without it.
        int clamped = Mathf.Clamp(index, 0, zone.childCount - (_ghost.parent == zone ? 1 : 0));
        if (_ghost.parent == zone && zone.IndexOf(_ghost) == clamped) return;
        _ghost.RemoveFromHierarchy();
        zone.Insert(Mathf.Clamp(clamped, 0, zone.childCount), _ghost);
    }

    private void RemoveGhost()
    {
        if (_ghost != null && _ghost.parent != null) _ghost.RemoveFromHierarchy();
    }
}
