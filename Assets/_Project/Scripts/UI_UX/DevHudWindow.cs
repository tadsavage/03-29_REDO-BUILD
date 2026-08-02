using System.Linq;          // Children().FirstOrDefault() in MatchCategoryButtonHeight
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
    private VisualElement _modeButton;
    private DraggableWindow _dragger;
    private VisualElement _titleBar;
    private Button _closeButton;
    private bool _docked;
    private float _smoothedFps = 60f;
    private int _lastCellX = -1, _lastCellY = -1;

    private bool _subscribed;
    private const string PrefKeyX = "DevHudWindow_X";
    private const string PrefKeyY = "DevHudWindow_Y";

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _doc.sortingOrder = 100;
        var ownRoot = _doc.rootVisualElement;
        if (ownRoot == null) return;

        ownRoot.pickingMode = PickingMode.Ignore;
        ownRoot.Clear();

        // Dock INTO the bar, not merely into its document. Parenting to the document root still left
        // this absolutely positioned at left:16/top:90 — a floating window that happened to share a
        // document, which is not what "part of the bottom bar" means. Adding to the bar itself puts it
        // in the bar's row layout (space-between, so it lands between the category and utility rows)
        // and it inherits the bar's position, layering and lifetime for free.
        var bar = BuildMenuUI.Instance != null ? BuildMenuUI.Instance.BuildBar : null;
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
                var late = BuildMenuUI.Instance != null ? BuildMenuUI.Instance.BuildBar : null;
                if (late == null || _panel == null || _panel.parent == late) return;
                _panel.RemoveFromHierarchy();
                late.Add(_panel);
                _docked = true;
                ApplyDockedLayout();
            }).ExecuteLater(250);
        }
        TrySubscribeSave();
    }

    private void OnDisable()
    {
        if (_subscribed && SaveManager.Instance != null)
            SaveManager.Instance.OnSaveCompleted -= OnGameSaved;
        _subscribed = false;
    }

    private void BuildUI(VisualElement root)
    {
        // ── Window panel ──────────────────────────────────────────────
        _panel = new VisualElement();
        _panel.style.position = Position.Absolute;
        _panel.style.left = 16;
        _panel.style.top = 90;
        _panel.style.width = 130;
        _panel.style.backgroundColor = new Color(0.078f, 0.110f, 0.173f, 0.92f);
        SetRadius(_panel, 6f);
        SetBorder(_panel, new Color(0.22f, 0.30f, 0.45f, 1f), 1f);

        // ── Title bar ─────────────────────────────────────────────────
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.justifyContent = Justify.SpaceBetween;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.paddingLeft = 8;
        titleBar.style.paddingRight = 4;
        titleBar.style.paddingTop = 3;
        titleBar.style.paddingBottom = 3;
        titleBar.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 1f);
        titleBar.style.borderTopLeftRadius = 6;
        titleBar.style.borderTopRightRadius = 6;

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
        body.style.paddingLeft = 10;
        body.style.paddingRight = 10;
        body.style.paddingTop = 6;
        body.style.paddingBottom = 8;

        _fpsLabel = new Label("-- FPS");
        _fpsLabel.style.fontSize = 20;
        _fpsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _fpsLabel.style.color = Color.white;
        _fpsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        _cellLabel = new Label("Cell: (--, --)");
        _cellLabel.style.fontSize = 11;
        _cellLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
        _cellLabel.style.color = new Color(0.72f, 0.80f, 0.92f, 1f);
        _cellLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _cellLabel.style.marginTop = 4;

        _modeButton = new VisualElement();
        _modeButton.style.marginTop = 5;
        _modeButton.style.paddingTop = 2;
        _modeButton.style.paddingBottom = 2;
        _modeButton.style.paddingLeft = 6;
        _modeButton.style.paddingRight = 6;
        _modeButton.style.backgroundColor = new Color(0.16f, 0.21f, 0.31f, 1f);
        SetRadius(_modeButton, 3f);
        SetBorder(_modeButton, new Color(0.40f, 0.52f, 0.72f, 1f), 1f);

        _modeLabel = new Label("Mode: --");
        _modeLabel.style.fontSize = 11;
        _modeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _modeLabel.style.color = new Color(0.72f, 0.80f, 0.92f, 1f);
        _modeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        _modeButton.Add(_modeLabel);
        _modeButton.RegisterCallback<ClickEvent>(_ => CyclePreset());

        body.Add(_fpsLabel);
        body.Add(_cellLabel);
        body.Add(_modeButton);

        _panel.Add(titleBar);
        _panel.Add(body);
        root.Add(_panel);

        if (_docked)
        {
            // Docked: the bar owns placement, so drop the floating-window chrome entirely. No drag
            // (there is nowhere to drag it to), and no ✕ — closing a widget that's part of the bar
            // would leave a hole in the bar rather than dismissing a window.
            _titleBar = titleBar;
            _closeButton = close;
            ApplyDockedLayout();
            return;
        }

        _dragger = new DraggableWindow(_panel, titleBar, close);
        _dragger.OnDragEnd += SaveWindowPos;
    }

    /// <summary>
    /// Turns the floating window into a bar-resident widget: in-flow instead of absolute, laid out in
    /// a row so it fits the bar's 120px height, and stripped of the drag/close affordances that only
    /// make sense for a window. The graphics-preset button is deliberately kept — it's the one
    /// interactive part worth having on the bar.
    /// </summary>
    /// <summary>Fallback height, from .buildmenu-category-button in buildmenuNEW.uss. Only used until
    /// MatchCategoryButtonHeight can measure a real button — the USS value doesn't survive panel
    /// scaling (104px in the sheet resolved to 113.2 at this resolution), so copying the live height
    /// is the only way to actually match.</summary>
    private const float BarCardHeight = 104f;

    /// <summary>Wide enough for "999 FPS" plus the cell/preset stack without the text ever changing
    /// the card's size. See the note on style.width in ApplyDockedLayout.
    ///
    /// Must be >= paddingLeft + FpsLabelWidth + FpsLabelGap + StackWidth + paddingRight, and the card
    /// must have flexShrink = 0 to actually get it — see ApplyDockedLayout.</summary>
    private const float DockedCardWidth = 300f;

    /// <summary>Width of the Cell label and the preset button beneath it. They share one width so the
    /// stack has a straight left AND right edge.</summary>
    private const float StackWidth = 160f;
    private const float FpsLabelWidth = 100f;
    private const float FpsLabelGap = 12f;
    private const float CardPadding = 12f;

    private void ApplyDockedLayout()
    {
        if (_panel == null) return;

        _panel.style.position = Position.Relative;
        _panel.style.left = StyleKeyword.Auto;
        _panel.style.top = StyleKeyword.Auto;
        // FIXED width, not auto. The FPS text changes every frame, and an auto-width card inside the
        // bar's flex row makes that a per-frame re-layout of the whole bar — ten category buttons and
        // the utility row — which tanked the frame rate the moment this docked. A fixed width means a
        // text change repaints one label and nothing reflows.
        _panel.style.width = DockedCardWidth;
        // flexShrink 0 or the width above is a suggestion, not a rule. The bar is a full flex row and
        // was squeezing this card from 310 down to 262 to fit everything else — while the fixed-width
        // labels INSIDE it refused to shrink, so the preset button spilled 39px out of the right-hand
        // edge. That looked like a button-sizing bug and wasn't one.
        _panel.style.flexShrink = 0;
        _panel.style.height = BarCardHeight;
        _panel.style.flexDirection = FlexDirection.Row;
        _panel.style.alignItems = Align.Center;
        _panel.style.paddingLeft = CardPadding;
        _panel.style.paddingRight = CardPadding;
        _panel.style.marginLeft = 12;
        _panel.style.marginRight = 12;
        // Same card face the bar's own buttons use, so it reads as part of the set rather than a
        // window that happens to be parked there.
        _panel.style.backgroundColor = new Color(34f / 255f, 44f / 255f, 56f / 255f, 0.55f);
        SetBorder(_panel, new Color(1f, 1f, 1f, 0.08f), 1f);
        SetRadius(_panel, 8f);

        if (_titleBar != null) _titleBar.style.display = DisplayStyle.None;
        if (_closeButton != null) _closeButton.style.display = DisplayStyle.None;

        var body = _fpsLabel?.parent;
        if (body != null)
        {
            body.style.flexDirection = FlexDirection.Row;
            body.style.alignItems = Align.Center;
            body.style.paddingTop = 0;
            body.style.paddingBottom = 0;
            body.style.paddingLeft = 0;
            body.style.paddingRight = 0;
            body.style.height = Length.Percent(100);
        }

        // FPS is the headline number and carries the height on its own; the cell readout and preset
        // button stack beside it so the card fills 104px vertically instead of floating one thin row
        // in the middle of it.
        if (_fpsLabel != null)
        {
            _fpsLabel.style.fontSize = 26; // 34 -> 29 -> 26, two passes of "still too loud"
            _fpsLabel.style.marginRight = FpsLabelGap;
            _fpsLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            // Fixed too: "9 FPS" and "144 FPS" must occupy the same box, or the stack beside it
            // shuffles sideways every time the number changes width.
            _fpsLabel.style.width = FpsLabelWidth;
            _fpsLabel.style.flexShrink = 0;
        }

        if (_cellLabel != null && _modeButton != null && body != null)
        {
            var stack = _cellLabel.parent == body && _modeButton.parent == body
                ? new VisualElement()
                : _cellLabel.parent as VisualElement;

            if (stack != null && stack != _cellLabel.parent)
            {
                stack.style.flexDirection = FlexDirection.Column;
                stack.style.alignItems = Align.FlexStart;
                stack.style.justifyContent = Justify.Center;
                stack.style.width = StackWidth;
                stack.style.flexShrink = 0;
                _cellLabel.RemoveFromHierarchy();
                _modeButton.RemoveFromHierarchy();
                stack.Add(_cellLabel);
                stack.Add(_modeButton);
                body.Add(stack);
            }

            _cellLabel.style.fontSize = 16;
            _cellLabel.style.marginTop = 0;
            _cellLabel.style.marginBottom = 10; // lifts Cell clear of the taller preset button below
            // Centred, not left-aligned: the label and the preset button share StackWidth, so centring
            // the text inside it parks "Cell: (28, 61)" directly over the button rather than jammed
            // against its left edge.
            _cellLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _cellLabel.style.width = StackWidth; // fixed for the same reason as the FPS label

            // Taller, fixed-width, and its label WRAPS. "Mode: Toaster" on one line at 16pt overran
            // the card's 310px and spilled out the right-hand side; a content-hugging button can't
            // be clipped back in, so the button is sized and the text is allowed to break instead.
            _modeButton.style.marginTop = 0;
            _modeButton.style.width = StackWidth;   // matches the cell label above it
            _modeButton.style.height = 40;
            _modeButton.style.flexShrink = 0;
            _modeButton.style.justifyContent = Justify.Center;
            _modeButton.style.paddingTop = 2;
            _modeButton.style.paddingBottom = 2;
            _modeButton.style.paddingLeft = 8;
            _modeButton.style.paddingRight = 8;
            if (_modeLabel != null)
            {
                _modeLabel.style.fontSize = 15;
                _modeLabel.style.whiteSpace = WhiteSpace.Normal;
                _modeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
        }

        MatchCategoryButtonHeight();
    }

    /// <summary>
    /// Copies the live height of a real category button onto the card.
    ///
    /// Hardcoding the USS value (104) doesn't match: panel scaling turns it into 113.2 at this
    /// resolution, and any other resolution gives a different number again. Measuring the rendered
    /// button is the only thing that stays correct — and it has to run after layout, hence the
    /// scheduled callback.
    /// </summary>
    private void MatchCategoryButtonHeight()
    {
        if (_panel == null) return;

        _panel.schedule.Execute(() =>
        {
            var bar = BuildMenuUI.Instance != null ? BuildMenuUI.Instance.BuildBar : null;
            var catRow = bar?.Q<VisualElement>("CategoryRow");
            var button = catRow?.Children().FirstOrDefault();
            if (button == null) return;

            float h = button.resolvedStyle.height;
            if (h > 1f && Mathf.Abs(h - _panel.resolvedStyle.height) > 0.5f)
                _panel.style.height = h;
        }).ExecuteLater(200);
    }

    private void Update()
    {
        TrySubscribeSave();

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
            _modeLabel.text = $"Mode: {mode}";
        }

        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            ToggleVisibility();
    }

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