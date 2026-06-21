using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

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
    private Label _modeLabel;
    private VisualElement _modeButton;
    private DraggableWindow _dragger;
    private float _smoothedFps = 60f;

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _doc.sortingOrder = 100;
        var root = _doc.rootVisualElement;
        if (root == null) return;

        root.pickingMode = PickingMode.Ignore;
        root.Clear();
        BuildUI(root);
<<<<<<< HEAD:Assets/_Project/Scripts/UI_UX/BottomHud/DevHudWindow.cs
        RestoreWindowPos();
        TrySubscribeSave();
    }

    private void OnDisable()
    {
        if (_subscribed && SaveManager.Instance != null)
            SaveManager.Instance.OnSaveCompleted -= OnGameSaved;
        _subscribed = false;
=======
>>>>>>> parent of eb142747 (stuff):Assets/3. UI/3. BottomHud/DevHudWindow.cs
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
        body.Add(_modeButton);

        _panel.Add(titleBar);
        _panel.Add(body);
        root.Add(_panel);

        _dragger = new DraggableWindow(_panel, titleBar, close);
    }

    private void Update()
    {
<<<<<<< HEAD:Assets/_Project/Scripts/UI_UX/BottomHud/DevHudWindow.cs
        TrySubscribeSave();

        // Smoothed FPS
=======
        // Smoothed FPS (unscaled so pause/fast-forward don't skew it).
>>>>>>> parent of eb142747 (stuff):Assets/3. UI/3. BottomHud/DevHudWindow.cs
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

<<<<<<< HEAD:Assets/_Project/Scripts/UI_UX/BottomHud/DevHudWindow.cs
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

=======
>>>>>>> parent of eb142747 (stuff):Assets/3. UI/3. BottomHud/DevHudWindow.cs
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