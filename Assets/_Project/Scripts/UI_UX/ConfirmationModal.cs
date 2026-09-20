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

    private const string DontShowAgainPrefPrefix = "ConfirmationModal.DontShowAgain.";

    private UIDocument _doc;
    private VisualElement _modal;
    private Label _message;
    private Toggle _dontShowAgainToggle;
    private string _dontShowAgainKey;
    private Action _onYes;
    private Action _onNo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        // A script recompile during Play mode wipes this class's static _instance field (a domain
        // reload resets ALL managed state), but the GameObject/UIDocument it pointed to is a real
        // native Unity object — DontDestroyOnLoad + a domain reload does NOT destroy it. Without this
        // cleanup, every single recompile in a play session left yet another orphaned, still-enabled
        // UIDocument behind (confirmed: 14 stacked up in one long session, several still carrying a
        // stale sortingOrder from before an earlier fix), and which one actually won the draw order
        // for a given Show() call became unpredictable — matching "works the first time, then it's
        // behind everything." AfterSceneLoad re-runs this method on every such reload, so sweeping up
        // any pre-existing instance here guarantees exactly one ever exists.
        foreach (var orphan in Resources.FindObjectsOfTypeAll<ConfirmationModal>())
            if (orphan != null) Destroy(orphan.gameObject);

        var go = new GameObject("[ConfirmationModal]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ConfirmationModal>();
        _instance.Build();
    }

    public static bool IsOpen => _instance != null && _instance._modal != null
                                  && _instance._modal.style.display == DisplayStyle.Flex;

    /// <summary>Shows a Yes/No prompt. onNo may be null (Cancel/backdrop-click just closes it).
    ///
    /// Pass <paramref name="dontShowAgainKey"/> to add a "Do not show this again" checkbox — checking
    /// it before hitting Yes persists the skip (PlayerPrefs, survives across sessions) under that key,
    /// and every future Show() call using the same key fires onYes immediately without ever displaying
    /// the prompt. Two callers must never share a key unless they genuinely want one "don't ask me
    /// again" to suppress both.</summary>
    public static void Show(string message, Action onYes, Action onNo = null, string dontShowAgainKey = null)
    {
        if (dontShowAgainKey != null && PlayerPrefs.GetInt(DontShowAgainPrefPrefix + dontShowAgainKey, 0) == 1)
        {
            onYes?.Invoke();
            return;
        }

        if (_instance == null) Bootstrap();
        if (_instance == null || _instance._modal == null) { onYes?.Invoke(); return; }

        if (IsOpen)
            Debug.LogWarning($"[ConfirmationModal] Show() called while already open — replacing the pending prompt ('{_instance._message.text}') with '{message}'.");

        _instance._onYes = onYes;
        _instance._onNo = onNo;
        _instance._message.text = message;
        _instance._dontShowAgainKey = dontShowAgainKey;
        _instance._dontShowAgainToggle.SetValueWithoutNotify(false);
        _instance._dontShowAgainToggle.style.display = dontShowAgainKey != null ? DisplayStyle.Flex : DisplayStyle.None;
        _instance._modal.style.display = DisplayStyle.Flex;
    }

    private void Build()
    {
        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = FindPanelSettings();
        // Every panel keyed into the HUD document (ItemCreatorPanel among them) renders on THAT
        // document at UILayers.Hud (999999) — a flat 900 here put this modal underneath literally
        // every one of them, invisible behind whatever panel opened it. Sits above WindowAboveHud
        // (1000000, the tier for windows with their own document) so it covers those too, but still
        // below Toast.ToastSortingOrder (1000100) so a toast notification stays visible on top of it.
        _doc.sortingOrder = UILayers.WindowAboveHud + 10f;

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
        panel.style.minWidth = 420;
        panel.style.maxWidth = 620; // widened alongside the 2x font bump below so the larger text has room
        panel.style.paddingLeft = 20; panel.style.paddingRight = 20;
        panel.style.paddingTop = 18; panel.style.paddingBottom = 16;
        panel.style.backgroundColor = new StyleColor(ColBg);
        SetBorder(panel, ColBorderCaramel, 2, 10);
        _modal.Add(panel);

        _message = new Label("");
        _message.style.color = new StyleColor(ColBlueText);
        _message.style.fontSize = 30; // 2x the previous 15
        _message.style.whiteSpace = WhiteSpace.Normal;
        _message.style.unityTextAlign = TextAnchor.MiddleLeft;
        _message.style.marginBottom = 16;
        panel.Add(_message);

        _dontShowAgainToggle = new Toggle("Do not show this again") { value = false };
        _dontShowAgainToggle.style.color = new StyleColor(ColBlueText);
        _dontShowAgainToggle.style.fontSize = 26; // 2x the previous unset (~13px) default
        _dontShowAgainToggle.style.marginBottom = 14;
        _dontShowAgainToggle.style.display = DisplayStyle.None;
        var dontShowAgainLabel = _dontShowAgainToggle.Q<Label>();
        if (dontShowAgainLabel != null) dontShowAgainLabel.style.color = new StyleColor(ColBlueText);
        panel.Add(_dontShowAgainToggle);

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.justifyContent = Justify.SpaceBetween;
        panel.Add(buttons);

        // Callback captured into a local BEFORE Hide() — Hide() nulls out _onNo/_onYes so the modal
        // can't fire a stale callback the next time it opens for something else, but reading the field
        // again straight after that (the old code's `Hide(); _onNo?.Invoke();`) meant it always read
        // back null and the callback silently never ran at all.
        var no = new Button(() =>
        {
            var callback = _onNo;
            Hide();
            callback?.Invoke();
        }) { text = "No" };
        no.style.flexGrow = 1; no.style.marginRight = 6;
        StyleButton(no, ColBlueFill, ColBlueEdge);
        buttons.Add(no);

        var yes = new Button(() =>
        {
            var callback = _onYes;
            bool suppress = _dontShowAgainToggle.style.display == DisplayStyle.Flex && _dontShowAgainToggle.value;
            string key = _dontShowAgainKey;
            Hide();
            if (suppress && key != null)
            {
                PlayerPrefs.SetInt(DontShowAgainPrefPrefix + key, 1);
                PlayerPrefs.Save();
            }
            callback?.Invoke();
        }) { text = "Yes" };
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
        b.style.fontSize = 28; // 2x the previous 14
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
