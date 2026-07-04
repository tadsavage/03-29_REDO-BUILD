using System.Collections.Generic;
using System.Text;
using UnityEngine;
using GameCore.Inventory;
using UnityEngine.UIElements;
using GameCore.Services;

/// <summary>
/// Modal that edits one shipping lane's <see cref="LaneConfig"/> (max stack size, usage, FIFO/LIFO).
/// Opened by double-clicking a lane (see LaneInteractionService). Self-bootstrapping and built
/// entirely in code — no UXML/PanelSettings wiring — following ChevronTooltipUI's pattern, and
/// shown/hidden via the display style (never SetActive) per the RackSetupUI lesson: an inactive
/// UIDocument sharing a PanelSettings can keep rendering.
/// </summary>
public class LaneSetupUI : MonoBehaviour
{
    private static LaneSetupUI _instance;

    private UIDocument _doc;
    private VisualElement _modal;   // full-screen backdrop; display toggles show/hide
    private Label _title;
    private Label _occupancy;
    private IntegerField _maxStack;
    private DropdownField _usage;
    private DropdownField _order;

    private int _door;
    private string _lane;

    // Usage labels shown to the player, mapped to LaneUsage. Player-facing wording per Tad:
    // Receiving/Shipping/Both (LaneUsage is Inbound/Outbound/Both under the hood).
    private static readonly List<string> UsageChoices = new() { "Receiving", "Shipping", "Both" };
    private static readonly List<string> OrderChoices = new() { "FIFO", "LIFO" };

    public bool IsOpen => _modal != null && _modal.style.display == DisplayStyle.Flex;

    public static LaneSetupUI Ensure()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("LaneSetupUI");
        _instance = go.AddComponent<LaneSetupUI>();
        _instance.Build();
        return _instance;
    }

    private void Build()
    {
        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = FindPanelSettings();
        _doc.sortingOrder = 250; // above tooltip (200) / toasts (100)

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
        _modal.pickingMode = PickingMode.Position; // eats clicks so they don't fall through to the world
        _modal.RegisterCallback<MouseDownEvent>(evt => { if (evt.target == _modal) Hide(); }); // click backdrop = cancel
        _modal.style.display = DisplayStyle.None;
        root.Add(_modal);

        var panel = new VisualElement();
        panel.style.minWidth = 340;
        panel.style.paddingLeft = 20; panel.style.paddingRight = 20;
        panel.style.paddingTop = 16; panel.style.paddingBottom = 16;
        panel.style.backgroundColor = new StyleColor(ColBg);
        SetBorder(panel, ColBorderCaramel, 2, 10);
        _modal.Add(panel);

        // Header: staging-lane icon on the upper-left, title left-aligned beside it.
        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 12;
        panel.Add(header);

        var icon = new VisualElement();
        icon.style.width = 64;
        icon.style.height = 64;
        icon.style.flexShrink = 0;
        icon.style.marginRight = 12;
        var tex = LoadStagingSprite();
        if (tex != null)
        {
            icon.style.backgroundImage = new StyleBackground(tex);
            icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            header.Add(icon);
        }

        _title = new Label("LANE SETUP");
        ApplyFont(_title, bold: true, size: 22);
        _title.style.color = new StyleColor(ColTitleOrange);
        _title.style.unityTextAlign = TextAnchor.MiddleLeft;
        _title.style.flexGrow = 1;
        header.Add(_title);

        _occupancy = new Label("");
        ApplyFont(_occupancy, bold: false, size: 12);
        _occupancy.style.color = new StyleColor(ColBlueText);
        _occupancy.style.marginBottom = 12;
        _occupancy.style.whiteSpace = WhiteSpace.Normal;
        _occupancy.style.unityTextAlign = TextAnchor.MiddleLeft;
        panel.Add(_occupancy);

        _maxStack = new IntegerField("Max Stack Size") { value = 2 };
        StyleField(_maxStack, _maxStack.labelElement);
        panel.Add(_maxStack);

        _usage = new DropdownField("Usage", UsageChoices, 2);
        StyleField(_usage, _usage.labelElement);
        panel.Add(_usage);

        _order = new DropdownField("Order", OrderChoices, 0);
        StyleField(_order, _order.labelElement);
        _order.style.marginBottom = 16;
        panel.Add(_order);

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.justifyContent = Justify.SpaceBetween;
        panel.Add(buttons);

        var cancel = new Button(Hide) { text = "Cancel" };
        cancel.style.flexGrow = 1; cancel.style.marginRight = 6;
        StyleButton(cancel, ColBlueFill, ColBlueEdge); // light blue
        buttons.Add(cancel);

        var save = new Button(Save) { text = "Save" };
        save.style.flexGrow = 1; save.style.marginLeft = 6;
        StyleButton(save, ColOrange, ColOrangeEdge);   // orange
        buttons.Add(save);
    }

    /// <summary>Open the modal for a specific lane, pre-filled with its current config.</summary>
    public void OpenFor(int doorNumber, string lane)
    {
        _door = doorNumber;
        _lane = lane;

        var cfg = LaneConfigRegistry.Get(doorNumber, lane);
        _title.text = $"Lane {doorNumber}{lane} Setup";
        _occupancy.text = BuildOccupancyText(doorNumber, lane);
        _maxStack.value = cfg.MaxStackHeight;
        _usage.index = UsageToIndex(cfg.Usage);
        _order.index = cfg.Order == LaneStackOrder.LIFO ? 1 : 0;

        _modal.style.display = DisplayStyle.Flex;
        UIModalGuard.Push(this); // suppress number-key panel hotkeys while typing here
    }

    public void Hide()
    {
        if (_modal != null) _modal.style.display = DisplayStyle.None;
        UIModalGuard.Pop(this);
    }

    private void Save()
    {
        var cfg = new LaneConfig
        {
            MaxStackHeight = Mathf.Max(1, _maxStack.value),
            Usage = IndexToUsage(_usage.index),
            Order = _order.index == 1 ? LaneStackOrder.LIFO : LaneStackOrder.FIFO
        };
        LaneConfigRegistry.Set(_door, _lane, cfg);
        Debug.Log($"[LaneSetupUI] Lane {_door}{_lane} → MaxStack {cfg.MaxStackHeight}, {cfg.Usage}, {cfg.Order}");
        Hide();
    }

    /// <summary>Live read-out of what's currently in the lane (real tracked pallets).</summary>
    private static string BuildOccupancyText(int doorNumber, string lane)
    {
        int slots = LaneNamingService.GetLane(doorNumber, lane).Count;
        if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null)
            return $"{slots} slots";

        var pallets = inv.GetPalletsInLane(doorNumber, lane);
        if (pallets.Count == 0)
            return $"Empty — {slots} slots";

        var sb = new StringBuilder();
        sb.Append($"{pallets.Count} pallet(s) in {slots} slots: ");
        for (int i = 0; i < pallets.Count && i < 8; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(inv.GetPalletAddress(pallets[i].PalletId) ?? "?");
        }
        if (pallets.Count > 8) sb.Append(", …");

        // Show what would ship out next, per this lane's FIFO/LIFO order (demonstrates the wiring).
        var ord = LaneConfigRegistry.Get(doorNumber, lane).Order;
        if (inv.TryGetNextPick(doorNumber, lane, out var next, out _, out _))
            sb.Append($"\nNext out ({ord}): {inv.GetPalletAddress(next.PalletId)}");
        else if (!inv.LaneAllowsPicking(doorNumber, lane))
            sb.Append("\n(Receiving-only — no picking)");
        return sb.ToString();
    }

    // ── mapping helpers (player wording ↔ enum) ──────────────────────────────
    private static int UsageToIndex(LaneUsage u) => u switch
    {
        LaneUsage.Inbound => 0,
        LaneUsage.Outbound => 1,
        _ => 2,
    };

    private static LaneUsage IndexToUsage(int i) => i switch
    {
        0 => LaneUsage.Inbound,
        1 => LaneUsage.Outbound,
        _ => LaneUsage.Both,
    };

    private static PanelSettings FindPanelSettings()
    {
        var existing = FindAnyObjectByType<UIDocument>();
        return existing != null ? existing.panelSettings : null;
    }

    // ── little styling helpers ───────────────────────────────────────────────
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

    private static void StyleField(VisualElement field, Label label)
    {
        ApplyFont(field, bold: false, size: 13);
        field.style.color = new StyleColor(ColBlueText); // input text (inherits to children)
        field.style.marginBottom = 8;

        if (label != null)
        {
            ApplyFont(label, bold: false, size: 13);
            label.style.color = new StyleColor(ColBlueText);
            label.style.minWidth = 130;
        }

        // Give the actual input box a dark fill + blue edge (both IntegerField and DropdownField
        // expose their editable area under the shared BaseField input class).
        var input = field.Q(className: "unity-base-field__input");
        if (input != null)
        {
            input.style.backgroundColor = new StyleColor(ColFieldBg);
            SetBorder(input, ColBlueEdge, 1, 4);
        }
    }

    private static void StyleButton(Button b, Color bg, Color edge)
    {
        ApplyFont(b, bold: true, size: 14);
        b.style.backgroundColor = new StyleColor(bg);
        b.style.color = new StyleColor(ColVanilla);
        b.style.paddingTop = 7; b.style.paddingBottom = 7;
        SetBorder(b, edge, 1, 6);
    }

    // ── Theme (matches ShiftManagerPanel / house style) ──────────────────────
    private static readonly Color ColBg            = new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.98f);
    private static readonly Color ColBorderCaramel = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColTitleOrange   = new Color(0xE8 / 255f, 0x8B / 255f, 0x3D / 255f, 1f);
    private static readonly Color ColBlueText      = new Color(0x9E / 255f, 0xC4 / 255f, 0xDE / 255f, 1f);
    private static readonly Color ColFieldBg       = new Color(30f / 255f, 40f / 255f, 52f / 255f, 1f);
    private static readonly Color ColBlueFill      = new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 1f);
    private static readonly Color ColBlueEdge      = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColOrange        = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge    = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColVanilla       = new Color(0xF5 / 255f, 0xF0 / 255f, 0xE1 / 255f, 1f);

    // Staging-lane header icon. Editor loads by path; a build needs it under a Resources folder
    // (same pattern as RackBlueprint.png) — move/copy it to Resources if it must show in a build.
    private static Texture2D _stagingTex;
    private static Texture2D LoadStagingSprite()
    {
        if (_stagingTex != null) return _stagingTex;
#if UNITY_EDITOR
        _stagingTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Sprites/StagingLaneIdeas.png");
#else
        _stagingTex = Resources.Load<Texture2D>("StagingLaneIdeas");
#endif
        return _stagingTex;
    }

    private static Font _lilita;
    private static Font LilitaFont()
    {
        if (_lilita != null) return _lilita;
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
        if (guids.Length > 0)
            _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
        _lilita = Resources.Load<Font>("LilitaOne-Regular");
#endif
        return _lilita;
    }

    private static void ApplyFont(VisualElement el, bool bold, int size = -1)
    {
        var f = LilitaFont();
        if (f != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
        el.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        if (size > 0) el.style.fontSize = size;
    }
}
