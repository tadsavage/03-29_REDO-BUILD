using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using SaveLoadSystem;

/// <summary>
/// Tiny draggable play-testing readout: current FPS on top, active graphics preset
/// (Ultra / Good / Toaster) below. Built entirely in code (no UXML/USS), on its own
/// UIDocument sharing the HUD's PanelSettings. Drag it anywhere by the title bar; the
/// red X hides it; F8 toggles it back. Playtest-only — not meant for production.
///
/// Position is saved to PlayerPrefs every time the game saves OR quicksaves, AND also
/// immediately when you finish dragging so you never lose a drag you did without saving.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class DevHudWindow : MonoBehaviour
{
    [Tooltip("Key that toggles the window on/off.")]
    [SerializeField] private Key toggleKey = Key.F8;
    [Tooltip("Seconds of smoothing for the FPS readout (0 = raw).")]
    [SerializeField] private float fpsSmoothing = 0.4f;

    private UIDocument _doc;
    private VisualElement _panel;
    private Label _fpsLabel;
    private Label _cellLabel;
    private Label _modeLabel;
    private Label _modeCaptionLabel;
    private VisualElement _modeButton;
    private DraggableWindow _dragger;
    private VisualElement _titleBar;
    private Label _titleLabel;
    private VisualElement _headerDivider;
    private VisualElement _body;
    private Button _closeButton;
    private bool _docked;

    /// <summary>Docked-only wrapper holding Cell (top) + FPS (bottom) stacked and left-aligned, so the
    /// Mode button can claim the rest of the row. Built lazily on first dock; torn down (children
    /// pulled back into <see cref="_body"/> directly) whenever the card goes floating again.</summary>
    private VisualElement _leftInfoColumn;
    private float _smoothedFps = 60f;
    private int _lastCellX = -1, _lastCellY = -1;

    /// <summary>This HUD's own document root — the reparent target whenever the card is dragged back
    /// out of the bar into a floating window. Set once in OnEnable; stable for the component's life.</summary>
    private VisualElement _docRoot;

    /// <summary>Live insertion-preview element shown in the bar while dragging hovers over it — a
    /// same-size placeholder so the bar's real buttons visibly reflow around where the card would
    /// land, without moving the real card until the drop is actually confirmed.</summary>
    private VisualElement _dockGhost;

    private bool _subscribed;
    private const string PrefKeyX = "DevHudWindow_X";
    private const string PrefKeyY = "DevHudWindow_Y";

    // Small always-present tab shown ONLY while the panel itself is hidden (X button, or F8) — same
    // pattern as SystemsLogWindow's own reopen tab, stacked directly above it (bottom:162 vs the log's
    // bottom:130) so closing this one leaves a way back in rather than depending on remembering F8.
    private VisualElement _reopenTab;

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _doc.sortingOrder = 100;
        var ownRoot = _doc.rootVisualElement;
        if (ownRoot == null) return;

        ownRoot.pickingMode = PickingMode.Ignore;
        ownRoot.Clear();
        _docRoot = ownRoot;

        // Dock INTO the bar, not merely into its document. Parenting to the document root still left
        // this absolutely positioned at left:16/top:90 — a floating window that happened to share a
        // document, which is not what "part of the bottom bar" means. Adding to the bar itself puts it
        // in the bar's row layout (space-between, so it lands between the category and utility rows)
        // and it inherits the bar's position, layering and lifetime for free.
        //
        // ActiveBar, not BuildBar: an element has one parent, so docking to the build bar meant the
        // FPS readout and the graphics preset simply disappeared the moment the player switched to
        // Play. OnActiveBarChanged below moves it across with the mode.
        BuildMenuUI.OnActiveBarChanged -= FollowActiveBar;
        BuildMenuUI.OnActiveBarChanged += FollowActiveBar;

        var bar = BuildMenuUI.Instance != null ? BuildMenuUI.Instance.ActiveBar : null;
        _docked = bar != null;
        BuildUI(bar ?? ownRoot);

        // Only a free-floating window has a position worth remembering; a docked one is placed by the
        // bar's layout and must not be moved by a stale pref.
        if (!_docked) RestoreWindowPos();

        // If the bar wasn't up yet, retry once — otherwise a script-order accident silently leaves the
        // dev HUD floating in its own document forever.
        if (!_docked)
        {
            ownRoot.schedule.Execute(() =>
            {
                var late = BuildMenuUI.Instance != null ? BuildMenuUI.Instance.ActiveBar : null;
                if (late == null || _panel == null || _panel.parent == late) return;
                _panel.RemoveFromHierarchy();
                late.Add(_panel);
                _docked = true;
                ApplyDockedLayout();
            }).ExecuteLater(250);
        }
        TrySubscribeSave();
    }

    /// <summary>Re-parents this HUD into whichever bar just became visible. A no-op when it's already
    /// there, so the mode-switch event costs nothing on the bar it's already docked to. Also a no-op
    /// while the player has deliberately pulled the card out into a floating window (<see cref="_docked"/>
    /// false) — a Build/Play mode switch shouldn't yank it back into a bar it was just removed from.</summary>
    private void FollowActiveBar(VisualElement bar)
    {
        if (!_docked || bar == null || _panel == null || _panel.parent == bar) return;
        _panel.RemoveFromHierarchy();
        bar.Add(_panel);
        ApplyDockedLayout();
    }

    private void OnDisable()
    {
        BuildMenuUI.OnActiveBarChanged -= FollowActiveBar;

        if (_subscribed && SaveManager.Instance != null)
            SaveManager.Instance.OnSaveCompleted -= OnGameSaved;
        _subscribed = false;
    }

    private void BuildUI(VisualElement root)
    {
        // ── Window panel (DOM only here — ApplyFloatingLayout/ApplyDockedLayout own the look) ──
        _panel = new VisualElement();
        // A default resting spot for the very first floating build, before RestoreWindowPos (if any)
        // or a later drag overrides it. Left untouched by both layout methods so re-applying either
        // one never stomps wherever the card actually is.
        _panel.style.left = 16;
        _panel.style.top = 90;
        // Click-through by design: only the title bar (drag handle), close button, and Mode button
        // (click-to-cycle preset) should ever intercept the pointer. Everything else — this panel's own
        // background, FPS/Cell — is Ignore so FreeLookCamera's right-drag/middle-drag orbit still works
        // with the cursor resting over the card while it's floating in the middle of the viewport;
        // UIInputGuard blocks camera input on ANY pickable UI Toolkit element under the cursor, and
        // without this a floating readout with no interactive purpose beyond its title bar was eating
        // camera control for its entire rectangle.
        _panel.pickingMode = PickingMode.Ignore;

        // ── Title bar ─────────────────────────────────────────────────
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.justifyContent = Justify.SpaceBetween;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.paddingLeft = 8;
        titleBar.style.paddingRight = 4;
        titleBar.style.paddingTop = 3;
        titleBar.style.paddingBottom = 3;
        // Background/radius are NOT set here — they flip between an opaque strip (floating window,
        // needs to read as its own titled panel) and fully transparent (docked, needs to read as part
        // of the bar) in ApplyFloatingLayout/ApplyDockedLayout below.

        var title = new Label("DEV");
        title.style.color = new Color(0.6f, 0.7f, 0.85f, 1f);
        title.style.fontSize = 10;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;

        var close = new Button(Hide) { text = "X" };
        close.style.fontSize = 10;
        close.style.width = 16;
        close.style.height = 16;
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

        titleBar.Add(title);
        titleBar.Add(close);

        // ── Body ──────────────────────────────────────────────────────
        var body = new VisualElement();
        body.pickingMode = PickingMode.Ignore;

        _fpsLabel = new Label("-- FPS");
        _fpsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _fpsLabel.style.color = Color.white;
        _fpsLabel.pickingMode = PickingMode.Ignore;

        _cellLabel = new Label("Cell: (--, --)");
        _cellLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
        _cellLabel.style.color = new Color(0.72f, 0.80f, 0.92f, 1f);
        _cellLabel.pickingMode = PickingMode.Ignore;

        _modeButton = new VisualElement();
        _modeButton.style.backgroundColor = new Color(0.16f, 0.21f, 0.31f, 1f);
        _modeButton.style.flexDirection = FlexDirection.Column;
        _modeButton.style.alignItems = Align.Center;
        _modeButton.style.justifyContent = Justify.Center;
        SetRadius(_modeButton, 3f);
        SetBorder(_modeButton, new Color(0.40f, 0.52f, 0.72f, 1f), 1f);

        // Docked-only caption row ("Graphics Profile:") stacked above the value inside the SAME box —
        // per Tad's mockup. Not used at all in the floating window, which keeps the single-line
        // "Mode: Ultra" label it always had.
        _modeCaptionLabel = new Label("Graphics Profile:");
        _modeCaptionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _modeCaptionLabel.style.color = new Color(0.60f, 0.70f, 0.85f, 1f);
        _modeCaptionLabel.style.display = DisplayStyle.None; // shown only while docked

        _modeLabel = new Label("Mode: --");
        _modeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _modeLabel.style.color = new Color(0.72f, 0.80f, 0.92f, 1f);

        _modeButton.Add(_modeCaptionLabel);
        _modeButton.Add(_modeLabel);
        _modeButton.RegisterCallback<ClickEvent>(_ => CyclePreset());

        body.Add(_fpsLabel);
        body.Add(_cellLabel);
        body.Add(_modeButton);

        // Docked-only thin separator under the title bar, matching Tad's mockup — hidden while floating.
        var headerDivider = new VisualElement();
        headerDivider.style.height = 1;
        headerDivider.style.backgroundColor = new Color(1f, 1f, 1f, 0.14f);
        headerDivider.style.display = DisplayStyle.None;
        headerDivider.pickingMode = PickingMode.Ignore;

        _panel.Add(titleBar);
        _panel.Add(headerDivider);
        _panel.Add(body);
        root.Add(_panel);

        _titleBar = titleBar;
        _titleLabel = title;
        _headerDivider = headerDivider;
        _body = body;
        _closeButton = close;

        // Drag now works in BOTH states: a floating card drags freely as before, and dragging a
        // DOCKED card's title bar detaches it first (OnHudDragStart, via DraggableWindow's new
        // OnDragStart hook) so the same drag then carries it as a floating window — that's what lets
        // the player pull it back OUT of the bar. OnDragMove previews where a drop would dock it;
        // OnDragEnd commits that dock or leaves the card floating wherever it was released.
        _dragger = new DraggableWindow(_panel, titleBar, close);
        _dragger.OnDragStart += OnHudDragStart;
        _dragger.OnDragMove += OnHudDragMove;
        _dragger.OnDragEnd += OnHudDragEnd;

        if (_docked) ApplyDockedLayout();
        else ApplyFloatingLayout();

        BuildReopenTab();
        SyncReopenTabVisibility();
    }

    /// <summary>Small always-present tab, separate from _panel entirely, so closing this HUD (X or F8)
    /// still leaves something on screen to bring it back with — mirrors SystemsLogWindow's own reopen
    /// tab exactly, stacked just above it. Lives on _docRoot directly rather than inside the bar, so it
    /// works the same whether the panel is currently docked or floating.</summary>
    private void BuildReopenTab()
    {
        if (_reopenTab != null || _docRoot == null) return;

        _reopenTab = new Button(ToggleVisibility) { text = "▲ FPS" };
        // Explicit, not just the Button default — RaycastController.Start() does a ONE-TIME sweep at
        // scene start that force-sets pickingMode=Ignore on every UIDocument root AND its direct
        // children (so world-click raycasts aren't blocked by an empty full-screen overlay). This HUD
        // builds its UI in OnEnable(), which runs before that sweep, so this tab — a direct child of
        // _docRoot — got caught by it and was silently unclickable. SystemsLogWindow's own reopen tab
        // escaped the same sweep purely because it bootstraps later (RuntimeInitializeOnLoadMethod
        // AfterSceneLoad, after Start() has already run) — asserting this explicitly here removes the
        // dependency on that timing coincidence.
        _reopenTab.pickingMode = PickingMode.Position;
        _reopenTab.style.position = Position.Absolute;
        _reopenTab.style.right = 16;
        _reopenTab.style.bottom = 162; // stacked just above SystemsLogWindow's reopen tab at bottom:130
        _reopenTab.style.paddingLeft = 10;
        _reopenTab.style.paddingRight = 10;
        _reopenTab.style.paddingTop = 4;
        _reopenTab.style.paddingBottom = 4;
        _reopenTab.style.fontSize = 12;
        _reopenTab.style.unityFontStyleAndWeight = FontStyle.Bold;
        _reopenTab.style.color = Color.white;
        _reopenTab.style.backgroundColor = new Color(0.078f, 0.110f, 0.173f, 0.92f);
        SetBorder(_reopenTab, new Color(0.35f, 0.55f, 0.85f, 0.9f), 1f);
        SetRadius(_reopenTab, 4f);
        _reopenTab.RegisterCallback<PointerEnterEvent>(_ =>
            _reopenTab.style.backgroundColor = new Color(0.12f, 0.18f, 0.28f, 0.95f));
        _reopenTab.RegisterCallback<PointerLeaveEvent>(_ =>
            _reopenTab.style.backgroundColor = new Color(0.078f, 0.110f, 0.173f, 0.92f));

        _docRoot.Add(_reopenTab);
    }

    /// <summary>Shown exactly when the panel itself is hidden — never both at once. Reads the inline
    /// style we just set (not resolvedStyle) so this is correct the instant it's called, without
    /// waiting on a layout pass.</summary>
    private void SyncReopenTabVisibility()
    {
        if (_reopenTab == null || _panel == null) return;
        bool panelHidden = _panel.style.display == DisplayStyle.None;
        _reopenTab.style.display = panelHidden ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>The floating window's look — restored whenever the card leaves the bar, whether at
    /// first build or after being dragged back out. Deliberately never touches left/top: those are
    /// either the DOM-time default, a restored PlayerPrefs position, or a drag's drop position —
    /// re-applying this method must not jump the card back to a stale spot.</summary>
    private void ApplyFloatingLayout()
    {
        if (_panel == null) return;

        _panel.style.position = Position.Absolute;
        _panel.style.right = StyleKeyword.Auto;
        _panel.style.bottom = StyleKeyword.Auto;
        _panel.style.width = 130;
        _panel.style.height = StyleKeyword.Auto;
        _panel.style.flexShrink = StyleKeyword.Null;
        _panel.style.marginLeft = 0;
        _panel.style.marginRight = 0;
        _panel.style.backgroundColor = new Color(0.078f, 0.110f, 0.173f, 0.92f);
        SetRadius(_panel, 6f);
        SetBorder(_panel, new Color(0.22f, 0.30f, 0.45f, 1f), 1f);

        if (_titleBar != null)
        {
            _titleBar.style.display = DisplayStyle.Flex;
            _titleBar.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 1f);
            _titleBar.style.borderTopLeftRadius = 6;
            _titleBar.style.borderTopRightRadius = 6;
            _titleBar.style.paddingLeft = 8;
            _titleBar.style.paddingRight = 4;
            _titleBar.style.paddingTop = 3;
            _titleBar.style.paddingBottom = 3;
        }
        if (_titleLabel != null)
        {
            _titleLabel.text = "DEV";
            _titleLabel.style.fontSize = 10;
        }
        if (_closeButton != null)
        {
            _closeButton.style.display = DisplayStyle.Flex;
            _closeButton.style.width = 16;
            _closeButton.style.height = 16;
            _closeButton.style.fontSize = 10;
        }

        var body = _body;
        if (body != null)
        {
            body.style.flexGrow = StyleKeyword.Null;
            body.style.flexDirection = FlexDirection.Column;
            body.style.alignItems = Align.Stretch;
            body.style.justifyContent = Justify.FlexStart;
            body.style.paddingLeft = 10;
            body.style.paddingRight = 10;
            body.style.paddingTop = 6;
            body.style.paddingBottom = 8;
            body.style.height = StyleKeyword.Auto;

            // Un-nest FPS/Cell from the docked left-info column (if the card was just docked) back
            // into body directly, and restore the floating DOM order: FPS, then Cell, then Mode.
            if (_leftInfoColumn != null)
            {
                _fpsLabel?.RemoveFromHierarchy();
                _cellLabel?.RemoveFromHierarchy();
                _leftInfoColumn.RemoveFromHierarchy();
            }
            if (_fpsLabel != null) { _fpsLabel.RemoveFromHierarchy(); body.Add(_fpsLabel); }
            if (_cellLabel != null) { _cellLabel.RemoveFromHierarchy(); body.Add(_cellLabel); }
            if (_modeButton != null) { _modeButton.RemoveFromHierarchy(); body.Add(_modeButton); }
        }

        if (_fpsLabel != null)
        {
            _fpsLabel.style.fontSize = 20;
            _fpsLabel.style.marginTop = 0;
            _fpsLabel.style.marginRight = 0;
            _fpsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _fpsLabel.style.width = StyleKeyword.Auto;
            _fpsLabel.style.flexShrink = StyleKeyword.Null;
        }

        if (_cellLabel != null)
        {
            _cellLabel.style.fontSize = 11;
            _cellLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            _cellLabel.style.marginTop = 4;
            _cellLabel.style.marginBottom = 0;
            _cellLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _cellLabel.style.width = StyleKeyword.Auto;
        }

        if (_modeButton != null)
        {
            _modeButton.style.marginTop = 5;
            _modeButton.style.marginLeft = 0;
            _modeButton.style.width = StyleKeyword.Auto;
            _modeButton.style.flexGrow = StyleKeyword.Null;
            _modeButton.style.alignSelf = StyleKeyword.Null;
            _modeButton.style.height = StyleKeyword.Auto;
            _modeButton.style.minHeight = StyleKeyword.Null;
            _modeButton.style.flexShrink = StyleKeyword.Null;
            _modeButton.style.minWidth = StyleKeyword.Null;
            _modeButton.style.justifyContent = Justify.Center;
            _modeButton.style.paddingTop = 2;
            _modeButton.style.paddingBottom = 2;
            _modeButton.style.paddingLeft = 6;
            _modeButton.style.paddingRight = 6;
        }
        // Caption ("Graphics Profile:") is a docked-only addition — floating keeps its original
        // single-line "Mode: Ultra" label and never shows the caption row at all.
        if (_modeCaptionLabel != null) _modeCaptionLabel.style.display = DisplayStyle.None;
        if (_modeLabel != null)
        {
            _modeLabel.style.fontSize = 11;
            _modeLabel.style.marginTop = 0;
            _modeLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _modeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        }
        if (_headerDivider != null) _headerDivider.style.display = DisplayStyle.None;
    }

    // ── Drag-to-dock / drag-to-undock ────────────────────────────────

    /// <summary>Fires at the very start of every drag. Deliberately does NOT detach a docked card
    /// immediately — see the class-level note on <see cref="DraggableWindow.PositionMode"/>. Reparenting
    /// on pointer-down (the old behavior) yanked the card out of the bar's flex flow before the user had
    /// dragged anywhere, so the neighboring category buttons visibly reflowed the instant you clicked
    /// it, not when you actually dragged it out. Instead: stay parented in the bar, and switch to
    /// Position.Relative so the card keeps reserving its normal slot (nothing around it moves) while
    /// still sliding freely under the pointer. OnHudDragEnd below decides, once the drag is over, whether
    /// it was actually carried clear of the bar — only then does the real detach (and the resulting
    /// reflow) happen.</summary>
    private void OnHudDragStart()
    {
        if (_docked) _dragger.PositionMode = Position.Relative;
    }

    /// <summary>Removes the card from whatever bar row it's parented to and re-floats it at its current
    /// on-screen spot (which — since this is called AFTER a relative-offset drag — already reflects
    /// wherever the drag left it, not the original docked position), so the transition from "sliding
    /// in-flow inside the bar" to "floating window" is visually seamless. Called two ways: directly at
    /// drag-end (pointer already released, no capture concerns), or via <see cref="DraggableWindow.
    /// RebaseDuringDrag"/> mid-drag from <see cref="OnHudDragMove"/> — that wrapper handles keeping the
    /// pointer capture alive and re-baselining the drag's tracking around whatever this method does, so
    /// this method itself doesn't need to know or care which case it's in.</summary>
    private void DetachFromDock()
    {
        if (_panel == null || _docRoot == null) return;

        Vector2 worldPos = _panel.worldBound.position;
        _panel.RemoveFromHierarchy();
        _docRoot.Add(_panel);
        _docked = false;
        ApplyFloatingLayout();

        Vector2 parentOrigin = _docRoot.worldBound.position;
        _panel.style.position = Position.Absolute;
        _panel.style.left = worldPos.x - parentOrigin.x;
        _panel.style.top = worldPos.y - parentOrigin.y;
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

    /// <summary>Every pointer move during a drag.
    /// <para>Still docked: rather than waiting for the drop (the previous behavior), detach the moment
    /// the card is dragged clear of the bar — Tad's ask, so the category buttons reflow live while
    /// dragging instead of only on release. "Clear" is defined the way he described it: the card's
    /// BOTTOM edge has risen above the bar's TOP edge (a pure vertical check — it doesn't matter whether
    /// it's still horizontally over the bar). <see cref="DraggableWindow.RebaseDuringDrag"/> does the
    /// actual mid-drag reparent without ending the gesture (see its own doc comment for why a plain
    /// reparent can't be used directly — it silently drops pointer capture).</para>
    /// <para>Once genuinely floating (whether that just happened above, or the card was already
    /// floating from an earlier drag): restores the original ghost-preview behavior — hovering the
    /// active bar shows a same-sized placeholder at the nearest insertion point so the bar's real
    /// buttons visibly slide out of the way, previewing where a drop would dock it.</para></summary>
    private void OnHudDragMove()
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

    /// <summary>Pointer-up: commits the drop.
    /// <list type="bullet">
    /// <item>Still docked — meaning <see cref="OnHudDragMove"/>'s clear-the-bar check never fired during
    /// this drag (a click, or a jiggle that never actually cleared the bar's top edge): if by some other
    /// measure the pointer nonetheless ended up clear of the bar's bounds, detach as a backstop; otherwise
    /// just clear the temporary relative offset and re-apply the plain docked look — no DOM change, no
    /// reflow, nothing to undo. The NORMAL detach path is no longer here — see OnHudDragMove — this is
    /// only reached when that path didn't already run.</item>
    /// <item>Already floating (the common case now, since detach usually already happened mid-drag):
    /// unchanged from before — docks at the ghost's index if one is showing, otherwise stays floating
    /// wherever it was released.</item>
    /// </list>
    /// </summary>
    private void OnHudDragEnd()
    {
        if (_docked)
        {
            var dockedBar = _panel.parent;
            bool clearedTheBar = dockedBar == null || !dockedBar.worldBound.Overlaps(_panel.worldBound);
            if (clearedTheBar) DetachFromDock();
            else ApplyDockedLayout(); // snap back — resets the relative left/top offset to Auto
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
        else
        {
            SaveWindowPos();
        }
    }

    /// <summary>Nearest insertion index among the bar's current children, by comparing the dragged
    /// card's horizontal center against each sibling's — the same rule any sortable-list drag uses, so
    /// it naturally produces "before everything" / "in a gap" / "after everything" without hardcoding
    /// zones, and still works if the bar ever gains more top-level groups later.</summary>
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
        if (_dockGhost.parent == bar && bar.IndexOf(_dockGhost) == clamped) return; // already there
        _dockGhost.RemoveFromHierarchy();
        bar.Insert(Mathf.Clamp(clamped, 0, bar.childCount), _dockGhost);
    }

    private void RemoveGhost()
    {
        if (_dockGhost != null && _dockGhost.parent != null)
            _dockGhost.RemoveFromHierarchy();
    }

    /// <summary>
    /// Turns the floating window into a bar-resident widget. The bar (.buildmenu-bottom-bar) is a
    /// FIXED 120px tall, single-line, align-items:center row — the earlier vertical-stack layout (title
    /// bar + FPS + Cell + Mode each on their own line) totalled well over 120px, so align-items:center
    /// spread that overflow equally above AND below the bar, leaving the card visibly poking out both
    /// edges. Fix: everything below the title bar runs in ONE horizontal row (a left-aligned Cell/FPS
    /// stack, then Mode), and the panel gets an explicit fixed height comfortably inside the bar, so it
    /// can only grow wider, never taller. The frame keeps a solid, opaque face (background + border) —
    /// Tad tried the fully-transparent look and preferred the card read as its own distinct panel.
    /// </summary>
    private const float DockedCardWidth = 240f;
    private const float DockedCardHeight = 100f;
    private const float CardPadding = 10f;

    private void ApplyDockedLayout()
    {
        if (_panel == null) return;

        _panel.style.position = Position.Relative;
        _panel.style.left = StyleKeyword.Auto;
        _panel.style.top = StyleKeyword.Auto;
        _panel.style.width = DockedCardWidth;
        // Fixed, not Auto: this is what stops the card from ever growing taller than the bar again,
        // regardless of what content ends up inside it.
        _panel.style.height = DockedCardHeight;
        _panel.style.flexShrink = 0;
        _panel.style.flexDirection = FlexDirection.Column;
        _panel.style.alignItems = Align.Stretch;
        _panel.style.paddingLeft = 0;
        _panel.style.paddingRight = 0;
        // Small side margins to match the ~4-8px breathing room the category buttons get from their
        // own 4px margin; small top/bottom margins so the card sits centered with a little clearance
        // top and bottom rather than touching the bar's edges exactly.
        _panel.style.marginLeft = 8;
        _panel.style.marginRight = 8;
        _panel.style.marginTop = 4;
        _panel.style.marginBottom = 4;
        // Same solid card face the floating window uses (Tad changed his mind on the transparent look).
        _panel.style.backgroundColor = new Color(34f / 255f, 44f / 255f, 56f / 255f, 0.92f);
        SetBorder(_panel, new Color(1f, 1f, 1f, 0.14f), 1f);
        SetRadius(_panel, 8f);

        // DEV / ✕ live in the title strip — ✕ pinned to the top-right corner, ~25% bigger than the
        // floating window's close button, and the label reads as a drag hint rather than just a name
        // (this whole strip IS the drag handle — see OnHudDragStart).
        if (_titleBar != null)
        {
            _titleBar.style.display = DisplayStyle.Flex;
            _titleBar.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 1f);
            _titleBar.style.borderTopLeftRadius = 8;
            _titleBar.style.borderTopRightRadius = 8;
            _titleBar.style.paddingLeft = 6;
            _titleBar.style.paddingRight = 3;
            _titleBar.style.paddingTop = 3;
            _titleBar.style.paddingBottom = 3;
        }
        if (_titleLabel != null)
        {
            _titleLabel.text = "DEV — Click to drag me";
            _titleLabel.style.fontSize = 9;
        }
        if (_closeButton != null)
        {
            _closeButton.style.display = DisplayStyle.Flex;
            // ~25% bigger than the floating window's 16x16.
            _closeButton.style.width = 20;
            _closeButton.style.height = 20;
            _closeButton.style.fontSize = 12;
        }
        if (_headerDivider != null) _headerDivider.style.display = DisplayStyle.Flex;

        // Body runs as ONE horizontal row: a left-aligned Cell/FPS stack, then the Mode button
        // claiming the rest of the width — filling whatever height is left under the title bar within
        // the panel's fixed height, which is what keeps the whole card short instead of tall.
        // FlexStart (not Center) so the info column sits right up under the title bar rather than
        // vertically centered in the leftover space — the Mode button gets its own alignSelf below so
        // it isn't dragged up to the top along with it.
        var body = _body;
        if (body != null)
        {
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Row;
            body.style.alignItems = Align.FlexStart;
            body.style.justifyContent = Justify.FlexStart;
            body.style.paddingLeft = CardPadding;
            body.style.paddingRight = CardPadding;
            body.style.paddingTop = 2;
            body.style.paddingBottom = 0;
            body.style.height = StyleKeyword.Auto;
        }

        // Nest Cell (top) + FPS (bottom) into a left-aligned info column — Cell above FPS per Tad's
        // ask — so the Mode button is free to claim the rest of the row's width. Reordered
        // UNCONDITIONALLY (not "only if not already parented there") — a conditional skip here is what
        // caused the very first live test of this layout to render Mode BEFORE the info column: Mode's
        // parent was already body from BuildUI's initial construction, so its "move" was skipped and it
        // kept its original (first) sibling position while the info column got appended after it. This
        // method only runs on dock/undock, never per-frame, so the unconditional reparenting costs
        // nothing that matters.
        if (_cellLabel != null && _fpsLabel != null && _modeButton != null && body != null)
        {
            if (_leftInfoColumn == null)
            {
                _leftInfoColumn = new VisualElement { pickingMode = PickingMode.Ignore };
            }
            // FlexStart, not Center — Cell (the top item) sits right underneath the title bar with
            // minimal gap, per Tad's ask to move it up.
            _leftInfoColumn.style.flexDirection = FlexDirection.Column;
            _leftInfoColumn.style.alignItems = Align.FlexStart;
            _leftInfoColumn.style.justifyContent = Justify.FlexStart;
            _leftInfoColumn.style.flexShrink = 0;
            _leftInfoColumn.style.marginRight = 14;

            _leftInfoColumn.RemoveFromHierarchy();
            body.Add(_leftInfoColumn);
            _cellLabel.RemoveFromHierarchy();
            _leftInfoColumn.Add(_cellLabel);
            _fpsLabel.RemoveFromHierarchy();
            _leftInfoColumn.Add(_fpsLabel);
            _modeButton.RemoveFromHierarchy();
            body.Add(_modeButton);
        }

        // FPS's per-frame color-by-threshold is untouched (driven in Update(), Tad asked not to touch
        // it). Cell sits right under the title bar (no top margin) and right against FPS below it (no
        // bottom margin) — FPS in turn is bigger/bolder than Cell now that it has the room, per Tad's
        // ask. Cell keeps its own color — deliberately not touched below.
        if (_cellLabel != null)
        {
            _cellLabel.style.fontSize = 13;
            _cellLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _cellLabel.style.marginTop = 0;
            _cellLabel.style.marginBottom = 0;
            _cellLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _cellLabel.style.width = StyleKeyword.Auto;
            _cellLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _cellLabel.style.flexShrink = 0;
        }

        if (_fpsLabel != null)
        {
            _fpsLabel.style.fontSize = 22;
            _fpsLabel.style.marginRight = 0;
            // Right up against Cell above it, no gap.
            _fpsLabel.style.marginTop = 0;
            _fpsLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _fpsLabel.style.width = StyleKeyword.Auto;
            _fpsLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _fpsLabel.style.flexShrink = 0;
        }

        // Fills the rest of the row's width (flexGrow) — but with flexShrink 0 and no minWidth cap, the
        // PREVIOUS version could demand more space than the card actually had (a fixed 60px height plus
        // "Mode: Toaster" at 18px font) and visibly overflow past the card's own border/corner. Fixed
        // three ways: flexShrink 1 + minWidth 0 (lets the box actually shrink to fit instead of forcing
        // overflow), a shorter fixed height that reliably fits the row's real available height, and — the
        // main fix — splitting the caption out of the value line (below) so the box no longer needs to
        // fit "Mode: Toaster" on one wide line at a large font. alignSelf Center keeps it vertically
        // centered in the body even though the body itself is FlexStart-aligned (so the info column can
        // hug the title bar without dragging Mode up to the top with it).
        if (_modeButton != null)
        {
            _modeButton.style.marginTop = 0;
            _modeButton.style.marginLeft = 0;
            _modeButton.style.alignSelf = Align.Center;
            _modeButton.style.width = StyleKeyword.Auto;
            _modeButton.style.minWidth = 0;
            _modeButton.style.flexGrow = 1;
            _modeButton.style.height = 52;
            _modeButton.style.minHeight = StyleKeyword.Null;
            _modeButton.style.flexShrink = 1;
            _modeButton.style.justifyContent = Justify.Center;
            _modeButton.style.paddingTop = 3;
            _modeButton.style.paddingBottom = 3;
            _modeButton.style.paddingLeft = 8;
            _modeButton.style.paddingRight = 8;
        }
        // Caption row ("Graphics Profile:") stacked above the value, both inside the same box — per
        // Tad's mockup. ~50% of the value label's old single-line size.
        if (_modeCaptionLabel != null)
        {
            _modeCaptionLabel.style.display = DisplayStyle.Flex;
            _modeCaptionLabel.style.fontSize = 9;
            _modeCaptionLabel.style.marginBottom = 2;
            _modeCaptionLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _modeCaptionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        }
        if (_modeLabel != null)
        {
            _modeLabel.style.fontSize = 18;
            _modeLabel.style.marginTop = 0;
            _modeLabel.style.whiteSpace = WhiteSpace.NoWrap;
            _modeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        }
    }

    private void Update()
    {
        TrySubscribeSave();

        // Self-heal against RaycastController.Start()'s one-time sweep, which force-sets
        // pickingMode=Ignore on every UIDocument root's DIRECT CHILDREN and runs AFTER this window's
        // OnEnable() builds _reopenTab — so setting pickingMode=Position at construction (see
        // BuildReopenTab) gets silently clobbered back to Ignore moments later regardless, making the
        // tab look right but not actually be clickable. Confirmed live: explicit assignment at build
        // time did not survive. Reasserting here every frame is a one-enum-comparison cost and can't
        // be beaten by ANY one-time sweep running at any point in the boot order, present or future —
        // same self-heal shape as BuildMenuUI.IsPointerOverBuildMenu's own fix earlier this session.
        if (_reopenTab != null && _reopenTab.pickingMode != PickingMode.Position)
            _reopenTab.pickingMode = PickingMode.Position;

        // Smoothed FPS
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
        {
            float inst = 1f / dt;
            float t = fpsSmoothing > 0f ? Mathf.Clamp01(dt / fpsSmoothing) : 1f;
            _smoothedFps = Mathf.Lerp(_smoothedFps, inst, t);
        }

        if (_fpsLabel != null)
        {
            int fps = Mathf.RoundToInt(_smoothedFps);
            _fpsLabel.text = $"{fps} FPS";
            _fpsLabel.style.color =
                fps >= 60 ? new Color(0.45f, 0.90f, 0.50f) :
                fps >= 30 ? new Color(0.95f, 0.85f, 0.35f) :
                            new Color(0.95f, 0.40f, 0.40f);
        }

        if (_modeLabel != null)
        {
            string mode = GraphicsPresetManager.Instance != null
                ? GraphicsPresetManager.Instance.CurrentPreset.ToString()
                : "—";
            // Docked: the caption row above already reads "Graphics Profile:", so the value line is
            // just the preset name. Floating: no caption row exists, so it stays a single "Mode: X" line.
            _modeLabel.text = _docked ? mode : $"Mode: {mode}";
        }

        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            ToggleVisibility();
    }

    private void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
        SyncReopenTabVisibility();
    }

    private void ToggleVisibility()
    {
        if (_panel == null) return;
        bool visible = _panel.resolvedStyle.display != DisplayStyle.None;
        _panel.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        SyncReopenTabVisibility();
    }

    private void CyclePreset()
    {
        var mgr = GraphicsPresetManager.Instance;
        if (mgr == null) return;
        var next = mgr.CurrentPreset switch
        {
            GraphicsPresetManager.Preset.Toaster => GraphicsPresetManager.Preset.Good,
            GraphicsPresetManager.Preset.Good => GraphicsPresetManager.Preset.Ultra,
            _ => GraphicsPresetManager.Preset.Toaster,
        };
        mgr.ApplyPreset(next);
    }

    public void SetCell(int x, int y)
    {
        if (_cellLabel != null && (_lastCellX != x || _lastCellY != y))
        {
            _lastCellX = x;
            _lastCellY = y;
            _cellLabel.text = $"Cell: ({x}, {y})";
        }
    }

    // ── Position persistence ──────────────────────────────────────────

    private void TrySubscribeSave()
    {
        if (_subscribed || SaveManager.Instance == null) return;
        SaveManager.Instance.OnSaveCompleted += OnGameSaved;
        _subscribed = true;
    }

    private void OnGameSaved(int _) => SaveWindowPos();

    private void SaveWindowPos()
    {
        if (_panel == null) return;
        var rs = _panel.resolvedStyle;
        // resolvedStyle.left/top are 0 when the panel hasn't been laid out yet; guard
        // against writing zeros over a valid saved position.
        if (rs.left == 0f && rs.top == 0f) return;
        PlayerPrefs.SetFloat(PrefKeyX, rs.left);
        PlayerPrefs.SetFloat(PrefKeyY, rs.top);
        PlayerPrefs.Save();
    }

    private void RestoreWindowPos()
    {
        if (_panel == null || !PlayerPrefs.HasKey(PrefKeyX)) return;
        _panel.style.position = Position.Absolute;
        _panel.style.left = PlayerPrefs.GetFloat(PrefKeyX);
        _panel.style.top = PlayerPrefs.GetFloat(PrefKeyY);
        _panel.style.right = StyleKeyword.Auto;
        _panel.style.bottom = StyleKeyword.Auto;
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