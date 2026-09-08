using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Scrolling "Systems Log" — a persistent, dockable readout of everything that would otherwise have
/// only flashed by in a toast. Newest entry at the TOP; older entries scroll down as new ones arrive.
/// No minimize/maximize buttons, just a close X. Built directly on <see cref="DraggableWindow"/> and
/// modeled on <see cref="DevHudWindow"/>'s drag-to-dock/drag-to-undock mechanics — Tad's explicit ask
/// ("use that as a framework because its perfect") — so it can be dragged out of the bottom bar into a
/// floating window and dragged back in exactly the same way.
///
/// Self-bootstrapping (like the other always-on hidden services in this project — DockNumberingService,
/// LaneNamingService, etc.) rather than requiring a manual scene GameObject: it spawns its own hidden
/// DontDestroyOnLoad object at scene load and borrows PanelSettings from <see cref="DevHudWindow"/>'s
/// UIDocument once that's up (same "grab an existing document's settings" trick <see cref="UIToast"/>
/// uses for its own top layer).
///
/// Toggle key: F7 (F8 is already the Dev HUD).
///
/// <para><b>Click-through by design.</b> Everything except the title bar (drag handle) and close button
/// is <see cref="PickingMode.Ignore"/> — the panel background, the scroll content, every log line. That
/// is what lets FreeLookCamera's right-drag/middle-drag orbit still work with the pointer resting over
/// the log body while it's floating in the middle of the viewport; only UIInputGuard's blanket "any
/// pickable UI Toolkit element under the cursor" check would otherwise block it (see FreeLookCamera.
/// IsPointerOverUI). Because the scroll content is Ignore, it can no longer receive UI Toolkit's own
/// WheelEvent either, so scrolling is driven manually in <see cref="Update"/> by polling raw
/// Mouse.current.scroll and testing the cursor against the panel's own worldBound — the same
/// "mouse-over-a-rect" test FreeLookCamera itself uses, not UI Toolkit's picking system.</para>
/// </summary>
public class SystemsLogWindow : MonoBehaviour
{
    private const int MaxEntries = 200;
    private const float DockedCardWidth = 480f;
    private const float DockedCardHeight = 100f;
    private const float FloatingWidth = 680f;
    private const float FloatingHeight = 260f;
    private const float CardPadding = 12f;
    // 35% smaller than the title's 22/18 — the timestamp+message text was reading too heavy against
    // the "LOG" header at full size.
    private const float FloatingEntryFontSize = 21.45f; // was 14.3 -- bumped 50% per Tad's explicit call
    private const float DockedEntryFontSize = 17.55f; // was 11.7 -- bumped 50% per Tad's explicit call
    private const float WheelScrollSpeed = 28f;

    // Matches DevHudWindow's own palette — Cell label color for message text, title color for the
    // header — so the two windows read as one family. Timestamps are yellow, same yellow FPS uses for
    // its "getting rough" mid-range reading, since they're effectively the toast's own timestamp.
    // Timestamp color never varies by category — only the message text does (see LogSystem/LogGuard).
    private static readonly Color MessageColor   = new Color(0.72f, 0.80f, 0.92f, 1f);
    private static readonly Color TitleColor     = new Color(0.6f, 0.7f, 0.85f, 1f);
    private static readonly Color TimestampColor = new Color(0.95f, 0.85f, 0.35f, 1f);
    private static readonly Color SystemColor    = new Color(0.55f, 0.90f, 0.55f, 1f); // light green
    private static readonly Color GuardColor     = new Color(0.35f, 0.60f, 0.95f, 1f); // dark blue
    private static readonly Color WarningColor   = new Color(0.75f, 0.05f, 0.05f, 1f); // blood red

    private static SystemsLogWindow _instance;
    private static readonly List<(string time, string message, Color color)> _pending = new List<(string, string, Color)>();

    private UIDocument _doc;
    private VisualElement _docRoot;
    private VisualElement _panel;
    private VisualElement _titleBar;
    private Label _titleLabel;
    private Label _titleSubLabel;
    private VisualElement _headerDivider;
    private Button _closeButton;
    private ScrollView _scroll;
    private DraggableWindow _dragger;
    private ResizableWindow _resizeWindow;
    private VisualElement _dockGhost;
    private bool _docked;
    private bool _built;

    private const string DockedTitle = "LOG";
    private const string FloatingSubtitle = "Drag to move, adj. size on left and bottom by dragging";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[SystemsLogWindow]");
        DontDestroyOnLoad(go);
        go.AddComponent<SystemsLogWindow>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    private void OnDestroy()
    {
        BuildMenuUI.OnActiveBarChanged -= FollowActiveBar;
        if (_instance == this) _instance = null;
    }

    /// <summary>Appends one line to the log, newest-first. Safe to call before the UI exists — buffers
    /// into <see cref="_pending"/> and flushes on first build. This is the hook <see cref="UIToast"/>
    /// calls so every toast also lands here. Hours:minutes only — seconds aren't useful here and just
    /// add noise.</summary>
    public static void Log(string message) => Log(message, MessageColor);

    /// <summary>System text — welcome/status messages (e.g. the Message of the Day). Light green.</summary>
    public static void LogSystem(string message) => Log(message, SystemColor);

    /// <summary>Guard shack announcements — inbound PO/door assignments, side lot, driver departures.
    /// Dark blue.</summary>
    public static void LogGuard(string message) => Log(message, GuardColor);

    /// <summary>Scheduler shortage warnings — order-readiness checks that found a real problem. Blood red.</summary>
    public static void LogWarning(string message) => Log(message, WarningColor);

    /// <summary>Core entry point — every other Log* overload funnels through this one with its own
    /// message color. Timestamp color never changes; only the message text does.</summary>
    public static void Log(string message, Color color)
    {
        if (string.IsNullOrEmpty(message)) return;
        string time = System.DateTime.Now.ToString("HH:mm");

        if (_instance != null && _instance._built)
        {
            _instance.AddEntry(time, message, color);
        }
        else
        {
            _pending.Add((time, message, color));
            if (_pending.Count > MaxEntries) _pending.RemoveAt(0);
        }
    }

    private void Update()
    {
        if (!_built) { TryBuild(); return; }

        if (Keyboard.current != null && Keyboard.current[Key.F7].wasPressedThisFrame)
            ToggleVisibility();

        PollManualWheelScroll();
    }

    // ── Build / bootstrap ────────────────────────────────────────────

    private void TryBuild()
    {
        // Borrow PanelSettings from the Dev HUD's own document — it's guaranteed present in every
        // session and already wired to the right panel, so there's nothing to configure by hand.
        var source = FindAnyObjectByType<DevHudWindow>();
        var sourceDoc = source != null ? source.GetComponent<UIDocument>() : null;
        if (sourceDoc == null || sourceDoc.panelSettings == null) return;

        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = sourceDoc.panelSettings;
        _doc.sortingOrder = 100;

        _docRoot = _doc.rootVisualElement;
        if (_docRoot == null) return;
        _docRoot.pickingMode = PickingMode.Ignore;

        BuildMenuUI.OnActiveBarChanged -= FollowActiveBar;
        BuildMenuUI.OnActiveBarChanged += FollowActiveBar;

        var bar = BuildMenuUI.Instance != null ? BuildMenuUI.Instance.ActiveBar : null;
        _docked = bar != null;
        BuildUI(bar ?? _docRoot);

        _built = true;

        // Flush anything logged before the UI existed, oldest first — AddEntry inserts at index 0 each
        // time, so iterating oldest→newest leaves the newest entry on top, same as if it had always
        // been live.
        foreach (var entry in _pending) AddEntry(entry.time, entry.message, entry.color);
        _pending.Clear();
    }

    /// <summary>Re-parents into whichever bar just became active — mirrors DevHudWindow.FollowActiveBar.
    /// No-op while the window has been dragged out into a floating state on purpose.</summary>
    private void FollowActiveBar(VisualElement bar)
    {
        if (!_docked || bar == null || _panel == null || _panel.parent == bar) return;
        _panel.RemoveFromHierarchy();
        bar.Add(_panel);
        ApplyDockedLayout();
    }

    private void BuildUI(VisualElement root)
    {
        _panel = new VisualElement();
        _panel.style.right = 16;
        _panel.style.top = 90;
        // The only things this panel should ever intercept are the title bar (drag handle) and the
        // close button — everything else (background, scroll content, log lines) is Ignore so camera
        // orbit/pan can reach right through it. See the class doc comment.
        _panel.pickingMode = PickingMode.Ignore;

        // ── Title bar (no min/max — just the close X) ───────────────
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.justifyContent = Justify.Center;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.paddingLeft = 10;
        titleBar.style.paddingRight = 6;
        titleBar.style.paddingTop = 5;
        titleBar.style.paddingBottom = 5;

        // "LOG" + the italic drag/resize hint are two separate labels wrapped in a row so each can have
        // its own size/weight/style — a single Label can't mix a bold headline with a half-size italic
        // tail. flexWrap lets the hint spill onto its own line without the row growing past the panel.
        // Centered on the title bar as a whole: the close button is pulled OUT of the flex flow
        // (Position.Absolute, pinned to the top-right corner) specifically so it doesn't skew this
        // row's centering the way it would if both shared the row via SpaceBetween.
        var titleTextRow = new VisualElement();
        titleTextRow.style.flexDirection = FlexDirection.Row;
        titleTextRow.style.flexWrap = Wrap.Wrap;
        titleTextRow.style.justifyContent = Justify.Center;
        titleTextRow.style.alignItems = Align.FlexEnd;
        titleTextRow.style.flexShrink = 1;

        var title = new Label(DockedTitle);
        title.style.color = TitleColor;
        title.style.fontSize = 22;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.whiteSpace = WhiteSpace.NoWrap;
        title.style.marginRight = 6;

        // Floating-only hint — hidden while docked (see ApplyDockedLayout). Half the size of "LOG" and
        // italic, per Tad's ask.
        var titleSub = new Label(FloatingSubtitle);
        titleSub.style.color = TitleColor;
        titleSub.style.fontSize = 11;
        titleSub.style.unityFontStyleAndWeight = FontStyle.Italic;
        titleSub.style.whiteSpace = WhiteSpace.Normal;
        titleSub.style.flexShrink = 1;
        titleSub.style.display = DisplayStyle.None;

        titleTextRow.Add(title);
        titleTextRow.Add(titleSub);

        // Absolute, not a flex sibling of titleTextRow — pinned to the corner so it can't pull the
        // title/subtitle off-center the way sharing the row via SpaceBetween used to.
        var close = new Button(Hide) { text = "X" };
        close.style.position = Position.Absolute;
        close.style.right = 6;
        close.style.top = 5;
        close.style.fontSize = 16;
        close.style.width = 30;
        close.style.height = 30;
        close.style.paddingLeft = 0;
        close.style.paddingRight = 0;
        close.style.paddingTop = 0;
        close.style.paddingBottom = 0;
        close.style.marginLeft = 0;
        close.style.marginRight = 0;
        close.style.marginTop = 0;
        close.style.marginBottom = 0;
        close.style.backgroundColor = new Color(0.60f, 0.18f, 0.18f, 1f);
        close.style.color = Color.white;
        SetRadius(close, 3f);

        titleBar.Add(titleTextRow);
        titleBar.Add(close);

        // Thin separator between the header and the message body — same treatment as the Dev HUD's own
        // header divider (1px, faint white so it reads as light gray against the dark panel).
        var headerDivider = new VisualElement();
        headerDivider.style.height = 1;
        headerDivider.style.backgroundColor = new Color(1f, 1f, 1f, 0.14f);
        headerDivider.pickingMode = PickingMode.Ignore;

        // ── Body: a hidden-scrollbar ScrollView, newest entry inserted at index 0 ───
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.style.flexGrow = 1;
        scroll.contentContainer.style.paddingLeft = CardPadding;
        scroll.contentContainer.style.paddingRight = CardPadding;
        scroll.contentContainer.style.paddingTop = 6;
        scroll.contentContainer.style.paddingBottom = 6;
        // Ignore, not the default — see the class doc comment. Wheel scrolling is driven manually via
        // PollManualWheelScroll instead of UI Toolkit's own WheelEvent, specifically so this doesn't
        // have to stay pickable for scrolling to work.
        scroll.pickingMode = PickingMode.Ignore;
        scroll.contentContainer.pickingMode = PickingMode.Ignore;
        scroll.contentViewport.pickingMode = PickingMode.Ignore;

        _panel.Add(titleBar);
        _panel.Add(headerDivider);
        _panel.Add(scroll);
        root.Add(_panel);

        _titleBar = titleBar;
        _titleLabel = title;
        _titleSubLabel = titleSub;
        _headerDivider = headerDivider;
        _closeButton = close;
        _scroll = scroll;

        _dragger = new DraggableWindow(_panel, titleBar, close);
        _dragger.OnDragStart += OnDragStart;
        _dragger.OnDragMove += OnDragMove;
        _dragger.OnDragEnd += OnDragEnd;

        // Left + Bottom only — Top/Right are already spoken for (title bar drag, close button, and the
        // panel's own fixed right/width when docked). Only meaningful while floating; disabled while
        // docked (see ApplyDockedLayout) since the docked card's size is owned by the bar's layout.
        _resizeWindow = new ResizableWindow(_panel, minW: 420f, minH: 150f, grip: 10f, titleInset: 48f,
            edges: ResizableWindow.Edges.Left | ResizableWindow.Edges.Bottom);

        if (_docked) ApplyDockedLayout();
        else ApplyFloatingLayout();
    }

    // ── Layouts ───────────────────────────────────────────────────────

    private void ApplyFloatingLayout()
    {
        if (_panel == null) return;

        _panel.style.position = Position.Absolute;
        _panel.style.bottom = StyleKeyword.Auto;
        _panel.style.width = FloatingWidth;
        _panel.style.height = FloatingHeight;
        _panel.style.flexShrink = StyleKeyword.Null;
        _panel.style.flexDirection = FlexDirection.Column;
        _panel.style.marginLeft = 0;
        _panel.style.marginRight = 0;
        _panel.style.backgroundColor = new Color(0.078f, 0.110f, 0.173f, 0.94f);
        SetRadius(_panel, 6f);
        SetBorder(_panel, new Color(0.22f, 0.30f, 0.45f, 1f), 1f);

        if (_titleBar != null)
        {
            _titleBar.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 1f);
            _titleBar.style.borderTopLeftRadius = 6;
            _titleBar.style.borderTopRightRadius = 6;
            _titleBar.style.flexShrink = 0;
        }
        if (_titleLabel != null) _titleLabel.style.fontSize = 22;
        if (_titleSubLabel != null) _titleSubLabel.style.display = DisplayStyle.Flex;
        if (_closeButton != null)
        {
            _closeButton.style.width = 30;
            _closeButton.style.height = 30;
            _closeButton.style.fontSize = 16;
        }

        _resizeWindow?.SetInteractable(true);

        RestyleEntries(fontSize: FloatingEntryFontSize);
    }

    /// <summary>Docked look — same fixed compact card size the Dev HUD uses, so it sits comfortably in
    /// the bar's 120px row instead of forcing it taller. Still fully scrollable inside that smaller
    /// area; the useful full-size reading view is the floating state.</summary>
    private void ApplyDockedLayout()
    {
        if (_panel == null) return;

        _panel.style.position = Position.Relative;
        _panel.style.left = StyleKeyword.Auto;
        _panel.style.top = StyleKeyword.Auto;
        _panel.style.right = StyleKeyword.Auto;
        _panel.style.width = DockedCardWidth;
        _panel.style.height = DockedCardHeight;
        _panel.style.flexShrink = 0;
        _panel.style.flexDirection = FlexDirection.Column;
        _panel.style.marginLeft = 8;
        _panel.style.marginRight = 8;
        _panel.style.marginTop = 4;
        _panel.style.marginBottom = 4;
        _panel.style.backgroundColor = new Color(34f / 255f, 44f / 255f, 56f / 255f, 0.92f);
        SetBorder(_panel, new Color(1f, 1f, 1f, 0.14f), 1f);
        SetRadius(_panel, 8f);

        if (_titleBar != null)
        {
            _titleBar.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 1f);
            _titleBar.style.borderTopLeftRadius = 8;
            _titleBar.style.borderTopRightRadius = 8;
            _titleBar.style.paddingLeft = 8;
            _titleBar.style.paddingRight = 4;
            _titleBar.style.flexShrink = 0;
        }
        if (_titleLabel != null) _titleLabel.style.fontSize = 18;
        if (_titleSubLabel != null) _titleSubLabel.style.display = DisplayStyle.None;
        if (_closeButton != null)
        {
            _closeButton.style.width = 24;
            _closeButton.style.height = 24;
            _closeButton.style.fontSize = 13;
        }

        // Resizing only makes sense while floating — the docked card's size is owned by ApplyDockedLayout
        // itself (fixed, reasserted on every dock), so dragging an edge here would fight that or tear the
        // panel out of the bar's flex flow via ResizableWindow's own Position.Absolute pin.
        _resizeWindow?.SetInteractable(false);

        RestyleEntries(fontSize: DockedEntryFontSize);
    }

    private void RestyleEntries(float fontSize)
    {
        if (_scroll == null) return;
        foreach (var row in _scroll.contentContainer.Children())
        {
            foreach (var child in row.Children())
            {
                if (child is Label l) l.style.fontSize = fontSize;
            }
        }
    }

    // ── Entries ───────────────────────────────────────────────────────

    private void AddEntry(string time, string message, Color messageColor)
    {
        if (_scroll == null) return;

        float fontSize = _docked ? DockedEntryFontSize : FloatingEntryFontSize;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.FlexStart;
        row.style.marginBottom = 2;
        row.pickingMode = PickingMode.Ignore;

        var timeLabel = new Label(time);
        timeLabel.style.color = TimestampColor;
        timeLabel.style.fontSize = fontSize;
        timeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        timeLabel.style.marginRight = 10;
        timeLabel.style.whiteSpace = WhiteSpace.NoWrap;
        timeLabel.style.flexShrink = 0;
        timeLabel.pickingMode = PickingMode.Ignore;

        var msgLabel = new Label(message);
        msgLabel.style.color = messageColor;
        msgLabel.style.fontSize = fontSize;
        msgLabel.style.whiteSpace = WhiteSpace.Normal;
        msgLabel.style.flexShrink = 1;
        msgLabel.style.flexGrow = 1;
        msgLabel.pickingMode = PickingMode.Ignore;

        row.Add(timeLabel);
        row.Add(msgLabel);

        // Only snap the view back to the newest entry if the reader was already at (or near) the top —
        // preserves scroll position for anyone deliberately reading back through older entries.
        bool wasNearTop = _scroll.scrollOffset.y <= 4f;

        _scroll.contentContainer.Insert(0, row);
        while (_scroll.contentContainer.childCount > MaxEntries)
            _scroll.contentContainer.RemoveAt(_scroll.contentContainer.childCount - 1);

        if (wasNearTop) _scroll.scrollOffset = Vector2.zero;
    }

    // ── Manual wheel scroll ──────────────────────────────────────────
    // Bypasses UI Toolkit's own WheelEvent entirely — the scroll content is PickingMode.Ignore (see
    // class doc comment), so it would never receive one. Polls the raw Input System scroll value and
    // gates it on the same kind of "is the cursor over this rect" test FreeLookCamera itself uses,
    // rather than UI Toolkit picking.

    private void PollManualWheelScroll()
    {
        if (_scroll == null || _panel == null || Mouse.current == null) return;
        if (_panel.resolvedStyle.display == DisplayStyle.None) return;

        var panel = _panel.panel;
        if (panel == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
        if (!_panel.worldBound.Contains(panelPos)) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        float contentHeight = _scroll.contentContainer.resolvedStyle.height;
        float viewportHeight = _scroll.contentViewport.resolvedStyle.height;
        float maxY = Mathf.Max(0f, contentHeight - viewportHeight);

        // Scrolling down (negative raw value) moves toward older entries further down the list, i.e.
        // increases the offset — matches the physical wheel direction of any ordinary scroll view.
        float newY = Mathf.Clamp(_scroll.scrollOffset.y - scroll * WheelScrollSpeed, 0f, maxY);
        _scroll.scrollOffset = new Vector2(_scroll.scrollOffset.x, newY);
    }

    // ── Drag-to-dock / drag-to-undock (mirrors DevHudWindow) ────────────

    private void OnDragStart()
    {
        if (_docked) _dragger.PositionMode = Position.Relative;
    }

    private void DetachFromDock()
    {
        if (_panel == null || _docRoot == null) return;

        Vector2 worldPos = _panel.worldBound.position;
        _panel.RemoveFromHierarchy();
        _docRoot.Add(_panel);
        _docked = false;
        ApplyFloatingLayout();

        Vector2 parentOrigin = _docRoot.worldBound.position;
        float left = worldPos.x - parentOrigin.x;
        float top = worldPos.y - parentOrigin.y;

        // The floating window is much taller than the docked card it just left, which sits flush
        // against the bottom of the screen — anchoring from the docked card's unchanged top-left corner
        // would grow the extra height straight off the bottom edge. Clamp against the document's own
        // bounds so it always lands fully on-screen regardless of which edge of the bar it was docked
        // near.
        float rootW = _docRoot.resolvedStyle.width;
        float rootH = _docRoot.resolvedStyle.height;
        if (rootW > 0f) left = Mathf.Clamp(left, 0f, Mathf.Max(0f, rootW - FloatingWidth));
        if (rootH > 0f) top = Mathf.Clamp(top, 0f, Mathf.Max(0f, rootH - FloatingHeight));

        _panel.style.position = Position.Absolute;
        _panel.style.left = left;
        _panel.style.top = top;
        _panel.style.right = StyleKeyword.Auto;
        _panel.style.bottom = StyleKeyword.Auto;

        // This detach happens WHILE the pointer is captured on this panel (mid-drag) — capture
        // suspends normal hover tracking on the bar for its duration, so the bar never fires its own
        // PointerLeaveEvent as the drag carries the cursor away from it. Left alone, that leaves
        // BuildMenuUI.IsPointerOverBuildMenu stuck true forever, which blocks camera orbit/pan/zoom
        // everywhere, no matter where the cursor actually is. Force a geometric reconciliation.
        if (Mouse.current != null && BuildMenuUI.Instance != null)
            BuildMenuUI.Instance.SyncPointerOverBuildMenu(Mouse.current.position.ReadValue());
    }

    private void OnDragMove()
    {
        if (_docked)
        {
            var dockedBar = _panel.parent;
            if (dockedBar != null && _panel.worldBound.yMax <= dockedBar.worldBound.yMin)
                _dragger.RebaseDuringDrag(DetachFromDock, Position.Absolute);
            return;
        }

        var bar = BuildMenuUI.Instance != null ? BuildMenuUI.Instance.ActiveBar : null;
        if (bar == null || _panel == null || !bar.worldBound.Overlaps(_panel.worldBound))
        {
            RemoveGhost();
            return;
        }
        ShowGhostAt(bar, ComputeInsertIndex(bar));
    }

    private void OnDragEnd()
    {
        if (_docked)
        {
            var dockedBar = _panel.parent;
            bool clearedTheBar = dockedBar == null || !dockedBar.worldBound.Overlaps(_panel.worldBound);
            if (clearedTheBar) DetachFromDock();
            else ApplyDockedLayout();
            return;
        }

        var bar = BuildMenuUI.Instance != null ? BuildMenuUI.Instance.ActiveBar : null;
        bool dropOnBar = bar != null && _dockGhost != null && _dockGhost.parent == bar;
        int index = dropOnBar ? bar.IndexOf(_dockGhost) : -1;
        RemoveGhost();

        if (dropOnBar)
        {
            _panel.RemoveFromHierarchy();
            bar.Insert(Mathf.Clamp(index, 0, bar.childCount), _panel);
            _docked = true;
            ApplyDockedLayout();
        }
    }

    private int ComputeInsertIndex(VisualElement bar)
    {
        float panelCenterX = _panel.worldBound.center.x;
        int index = 0;
        foreach (var child in bar.Children())
        {
            if (child == _dockGhost) continue;
            if (panelCenterX > child.worldBound.center.x) index++;
            else break;
        }
        return index;
    }

    private void ShowGhostAt(VisualElement bar, int index)
    {
        if (_dockGhost == null)
        {
            _dockGhost = new VisualElement();
            _dockGhost.style.width = DockedCardWidth;
            _dockGhost.style.height = DockedCardHeight;
            _dockGhost.style.marginLeft = 12;
            _dockGhost.style.marginRight = 12;
            _dockGhost.style.backgroundColor = new Color(0.35f, 0.55f, 0.85f, 0.18f);
            SetBorder(_dockGhost, new Color(0.45f, 0.65f, 0.95f, 0.9f), 2f);
            SetRadius(_dockGhost, 8f);
            _dockGhost.pickingMode = PickingMode.Ignore;
        }

        int clamped = Mathf.Clamp(index, 0, bar.childCount);
        if (_dockGhost.parent == bar && bar.IndexOf(_dockGhost) == clamped) return;
        _dockGhost.RemoveFromHierarchy();
        bar.Insert(Mathf.Clamp(clamped, 0, bar.childCount), _dockGhost);
    }

    private void RemoveGhost()
    {
        if (_dockGhost != null && _dockGhost.parent != null)
            _dockGhost.RemoveFromHierarchy();
    }

    // ── Visibility ────────────────────────────────────────────────────

    private void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    private void ToggleVisibility()
    {
        if (_panel == null) return;
        bool visible = _panel.resolvedStyle.display != DisplayStyle.None;
        _panel.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void SetRadius(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r;
        e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r;
        e.style.borderBottomRightRadius = r;
    }

    private static void SetBorder(VisualElement e, Color c, float w)
    {
        e.style.borderTopColor = c;
        e.style.borderBottomColor = c;
        e.style.borderLeftColor = c;
        e.style.borderRightColor = c;
        e.style.borderTopWidth = w;
        e.style.borderBottomWidth = w;
        e.style.borderLeftWidth = w;
        e.style.borderRightWidth = w;
    }
}
