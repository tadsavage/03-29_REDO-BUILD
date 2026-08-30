using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reusable, statically-callable Yes/No confirmation modal — the thing this codebase didn't have:
/// every existing confirm-prompt (ShiftManagerPanel's "Are you sure?") is a private overlay owned by
/// one panel, so no gameplay system (not a UI panel instance) could ever pop one. Built the same way
/// LaneSetupUI is (fully code-built VisualElements, its own UIDocument, PanelSettings borrowed from
/// whatever UIDocument already exists in the scene, shown/hidden via DisplayStyle — never SetActive,
/// per the RackSetupUI lesson that an inactive UIDocument sharing a PanelSettings keeps rendering).
///
/// Self-bootstraps like LaneNamingService/DockNumberingService so any gameplay class (e.g.
/// TrailerOffloadController, driving a background coroutine with no panel of its own) can call
/// ConfirmationModal.Show(...) without first having to "Ensure" a UI it doesn't otherwise touch.
///
/// Only one prompt can be open at a time — Show() while already open logs a warning and replaces the
/// pending one rather than stacking, since a second unrelated prompt popping mid-decision would be
/// confusing and there is no queue.
/// </summary>
public class ConfirmationModal : MonoBehaviour
{
    private static ConfirmationModal _instance;

    private UIDocument _doc;
    private VisualElement _modal;
    private Label _message;
    private Action _onYes;
    private Action _onNo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[ConfirmationModal]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ConfirmationModal>();
        _instance.Build();
    }

    public static bool IsOpen => _instance != null && _instance._modal != null
                                  && _instance._modal.style.display == DisplayStyle.Flex;

    /// <summary>Shows a Yes/No prompt. onNo may be null (Cancel/backdrop-click just closes it).</summary>
    public static void Show(string message, Action onYes, Action onNo = null)
    {
        if (_instance == null) Bootstrap();
        if (_instance == null || _instance._modal == null) return;

        if (IsOpen)
            Debug.LogWarning($"[ConfirmationModal] Show() called while already open — replacing the pending prompt ('{_instance._message.text}') with '{message}'.");

        _instance._onYes = onYes;
        _instance._onNo = onNo;
        _instance._message.text = message;
        _instance._modal.style.display = DisplayStyle.Flex;
    }

    private void Build()
    {
        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = FindPanelSettings();
        _doc.sortingOrder = 900; // below LaneSetupUI's own ad-hoc modals but above ordinary panels/toasts

        var root = _doc.rootVisualElement;
        if (root == null) return;
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;

        _modal = new VisualElement();
        _modal.style.position = Position.Absolute;
        _modal.style.left = 0; _modal.style.top = 0; _modal.style.right = 0; _modal.style.bottom = 0;
        _modal.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.45f));
        _modal.style.alignItems = Align.Center;
        _modal.style.justifyContent = Justify.Center;
        _modal.pickingMode = PickingMode.Position;
        _modal.style.display = DisplayStyle.None;
        root.Add(_modal);

        var panel = new VisualElement();
        panel.style.minWidth = 360;
        panel.style.maxWidth = 480;
        panel.style.paddingLeft = 20; panel.style.paddingRight = 20;
        panel.style.paddingTop = 18; panel.style.paddingBottom = 16;
        panel.style.backgroundColor = new StyleColor(ColBg);
        SetBorder(panel, ColBorderCaramel, 2, 10);
        _modal.Add(panel);

        _message = new Label("");
        _message.style.color = new StyleColor(ColBlueText);
        _message.style.fontSize = 15;
        _message.style.whiteSpace = WhiteSpace.Normal;
        _message.style.unityTextAlign = TextAnchor.MiddleLeft;
        _message.style.marginBottom = 16;
        panel.Add(_message);

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.justifyContent = Justify.SpaceBetween;
        panel.Add(buttons);

        var no = new Button(() => { Hide(); _onNo?.Invoke(); }) { text = "No" };
        no.style.flexGrow = 1; no.style.marginRight = 6;
        StyleButton(no, ColBlueFill, ColBlueEdge);
        buttons.Add(no);

        var yes = new Button(() => { Hide(); _onYes?.Invoke(); }) { text = "Yes" };
        yes.style.flexGrow = 1; yes.style.marginLeft = 6;
        StyleButton(yes, ColOrange, ColOrangeEdge);
        buttons.Add(yes);
    }

    private static void Hide()
    {
        if (_instance == null || _instance._modal == null) return;
        _instance._modal.style.display = DisplayStyle.None;
        _instance._onYes = null;
        _instance._onNo = null;
    }

    private static PanelSettings FindPanelSettings()
    {
        var existing = FindAnyObjectByType<UIDocument>();
        return existing != null ? existing.panelSettings : null;
    }

    private static void SetBorder(VisualElement e, Color c, float w, float r)
    {
        e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
        e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
        var sc = new StyleColor(c);
        e.style.borderTopColor = sc; e.style.borderBottomColor = sc;
        e.style.borderLeftColor = sc; e.style.borderRightColor = sc;
        e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
    }

    private static void StyleButton(Button b, Color bg, Color edge)
    {
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        b.style.fontSize = 14;
        b.style.backgroundColor = new StyleColor(bg);
        b.style.color = new StyleColor(ColVanilla);
        b.style.paddingTop = 7; b.style.paddingBottom = 7;
        SetBorder(b, edge, 1, 6);
    }

    // ── Theme (matches LaneSetupUI / house style) ─────────────────────────────
    private static readonly Color ColBg            = new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.98f);
    private static readonly Color ColBorderCaramel = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColBlueText      = new Color(0x9E / 255f, 0xC4 / 255f, 0xDE / 255f, 1f);
    private static readonly Color ColBlueFill      = new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 1f);
    private static readonly Color ColBlueEdge      = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColOrange        = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge    = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColVanilla       = new Color(0xF5 / 255f, 0xF0 / 255f, 0xE1 / 255f, 1f);
}
