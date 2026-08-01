using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// The Outbound Order Manager's wholesale side: every contract offer in the game as a browsable card
/// list, with the customer's own icon, its terms, and a Sign button.
///
/// Signing is where demand enters the game in a real build — the Dev Console's "Create Test Order"
/// button stays as a debug override, but this is the player-facing route. A recurring account starts
/// sending orders at its cutoff hour each day; a wholesale deal drops its whole trailer's worth the
/// moment it's signed and is then spent.
///
/// Palette and font are taken from WorkQueuePanel deliberately, so the two read as one family. Each
/// panel in this project declares its own colour constants rather than sharing a theme class — that's
/// the existing convention here, not an oversight.
/// </summary>
public class ContractsPanel : IUIPanel
{
    private static readonly Color ColBg          = new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.92f);
    private static readonly Color ColBorder      = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText   = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColSubtleText  = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColOrange      = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge  = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColOrangeText  = new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f);
    private static readonly Color ColOrangeHover = new Color(0xC6 / 255f, 0x7F / 255f, 0x42 / 255f, 1f);
    private static readonly Color ColCardEven    = new Color(36f / 255f, 48f / 255f, 62f / 255f, 0.65f);
    private static readonly Color ColCardOdd     = new Color(30f / 255f, 40f / 255f, 52f / 255f, 0.65f);
    private static readonly Color ColMoney       = new Color(0x7E / 255f, 0xD6 / 255f, 0x8A / 255f, 1f);
    private static readonly Color ColWholesale   = new Color(0xF2 / 255f, 0xC2 / 255f, 0x5A / 255f, 1f);
    private static readonly Color ColBlueEdge    = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);

    private const float ModalWidth  = 1040f;
    private const float ModalHeight = 680f;
    private const float IconSize    = 72f;

    private readonly VisualElement _overlay;
    private readonly VisualElement _modal;
    private readonly ScrollView _cardScroll;
    private readonly Label _footerMessage;
    private bool _visible;

    // Drag state. The modal is absolutely positioned so left/top can be written directly; the offset
    // is captured at pointer-down so the window doesn't jump to centre itself under the cursor.
    private bool _dragging;
    private Vector2 _dragOffset;
    private bool _placed; // false until the first Show centres it

    private static Font _lilita;

    public ContractsPanel(VisualElement root)
    {
        _overlay = Build(out _modal, out _cardScroll, out _footerMessage);
        root.Add(_overlay);
        Hide();
    }

    public bool IsVisible => _visible;
    public bool IsOpen => _visible;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
        RebuildCards();
        CentreOnce();
    }

    /// <summary>Centres the modal the FIRST time it's shown and never again — reopening should return
    /// it to wherever the player dragged it, not yank it back to the middle.</summary>
    private void CentreOnce()
    {
        if (_placed) return;
        _overlay.schedule.Execute(() =>
        {
            if (_placed) return;
            Rect r = _overlay.worldBound;
            if (r.width < 1f) return; // no layout yet — try again next Show
            _modal.style.left = Mathf.Max(0f, (r.width - ModalWidth) * 0.5f);
            _modal.style.top = Mathf.Max(0f, (r.height - ModalHeight) * 0.5f);
            _placed = true;
        }).ExecuteLater(16);
    }

    public void Hide()
    {
        _visible = false;
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
    }

    public void Dispose()
    {
        if (_overlay.parent != null) _overlay.RemoveFromHierarchy();
    }

    // ── Shell ────────────────────────────────────────────────────────────────

    private VisualElement Build(out VisualElement modalOut, out ScrollView cardScroll, out Label footerMessage)
    {
        var overlay = new VisualElement { name = "contracts-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
        overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));

        var modal = new VisualElement { name = "contracts-modal" };
        // Absolute rather than centred by the overlay's flex: a dragged window needs left/top it can
        // own, and flex centring would fight every frame with whatever the drag writes.
        modal.style.position = Position.Absolute;
        modal.style.width = ModalWidth;
        modal.style.height = ModalHeight;
        modal.style.backgroundColor = new StyleColor(ColBg);
        modal.style.borderTopWidth = modal.style.borderBottomWidth =
            modal.style.borderLeftWidth = modal.style.borderRightWidth = 3;
        modal.style.borderTopColor = modal.style.borderBottomColor =
            modal.style.borderLeftColor = modal.style.borderRightColor = new StyleColor(ColBorder);
        modal.style.borderTopLeftRadius = modal.style.borderTopRightRadius =
            modal.style.borderBottomLeftRadius = modal.style.borderBottomRightRadius = 16;
        modal.style.paddingTop = 14; modal.style.paddingBottom = 14;
        modal.style.paddingLeft = 16; modal.style.paddingRight = 16;

        // Title bar
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.height = 52;
        titleBar.style.marginBottom = 8;

        var title = new Label("WHOLESALE CONTRACTS");
        ApplyFont(title, bold: true, size: 28);
        title.style.color = new StyleColor(ColTitleText);
        title.style.flexGrow = 1;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(title);

        var close = new Button(Hide) { text = "✕" };
        StyleSquareButton(close);
        titleBar.Add(close);
        modal.Add(titleBar);

        // The title bar is the drag handle. Registered on the BAR, not the modal, so dragging can't
        // start from a card or swallow a click meant for a Sign button.
        titleBar.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            if (evt.target is Button) return; // let ✕ do its job
            _dragging = true;
            _dragOffset = (Vector2)evt.position - new Vector2(modal.worldBound.x, modal.worldBound.y);
            titleBar.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });
        titleBar.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!_dragging) return;
            Rect bounds = _overlay.worldBound;
            Vector2 target = (Vector2)evt.position - _dragOffset - new Vector2(bounds.x, bounds.y);
            // Keep at least a strip of the title bar on screen so it can always be grabbed back.
            float maxX = Mathf.Max(0f, bounds.width - 120f);
            float maxY = Mathf.Max(0f, bounds.height - 60f);
            modal.style.left = Mathf.Clamp(target.x, -(ModalWidth - 120f), maxX);
            modal.style.top = Mathf.Clamp(target.y, 0f, maxY);
            _placed = true;
            evt.StopPropagation();
        });
        titleBar.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!_dragging) return;
            _dragging = false;
            titleBar.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        });

        var sub = new Label("Sign an account to bring work in. Standing accounts send orders every day; " +
                            "wholesale deals drop a full trailer once.  ·  Drag this bar to move the window.");
        ApplyFont(sub, size: 15);
        sub.style.color = new StyleColor(ColSubtleText);
        sub.style.marginBottom = 10;
        sub.style.whiteSpace = WhiteSpace.Normal;
        modal.Add(sub);

        cardScroll = new ScrollView(ScrollViewMode.Vertical);
        cardScroll.style.flexGrow = 1;
        modal.Add(cardScroll);

        footerMessage = new Label();
        ApplyFont(footerMessage, size: 15);
        footerMessage.style.color = new StyleColor(ColSubtleText);
        footerMessage.style.marginTop = 8;
        footerMessage.style.whiteSpace = WhiteSpace.Normal;
        modal.Add(footerMessage);

        overlay.Add(modal);
        modalOut = modal;
        return overlay;
    }

    // ── Cards ────────────────────────────────────────────────────────────────

    private void RebuildCards()
    {
        _cardScroll.Clear();

        if (!ServiceLocator.TryGet<OrderArrivalService>(out var arrivals) || arrivals == null)
        {
            _footerMessage.text = "Order arrival service isn't running — contracts can't be signed. " +
                                  "(OrderArrivalService is not registered in GameContext yet.)";
            return;
        }

        var offers = arrivals.Catalog.ToList();
        if (offers.Count == 0)
        {
            _footerMessage.text = "No contract offers authored yet. Create ContractData assets and list them " +
                                  "on a ContractRegistry asset under a Resources folder.";
            return;
        }

        ServiceLocator.TryGet<InventoryService>(out var inv);

        int row = 0;
        foreach (var contract in offers)
        {
            bool taken = arrivals.IsSigned(contract.ContractId);
            _cardScroll.Add(BuildCard(contract, arrivals, inv, taken, row++));
        }

        int active = arrivals.Signed.Count(s => s.Active);
        _footerMessage.text = $"{offers.Count} offer(s) · {active} standing account(s) currently running.";
    }

    private VisualElement BuildCard(ContractData contract, OrderArrivalService arrivals,
                                    InventoryService inv, bool taken, int rowIndex)
    {
        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Row;
        card.style.alignItems = Align.Center;
        card.style.paddingTop = 10; card.style.paddingBottom = 10;
        card.style.paddingLeft = 12; card.style.paddingRight = 12;
        card.style.marginBottom = 6;
        card.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColCardEven : ColCardOdd);
        card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 10;
        card.style.borderLeftWidth = 3;
        card.style.borderLeftColor = new StyleColor(contract.IsWholesale ? ColWholesale : ColBorder);
        if (taken) card.style.opacity = 0.45f;

        // Customer icon — straight off CustomerData, which already has every sprite assigned.
        var icon = new VisualElement();
        icon.style.width = IconSize;
        icon.style.height = IconSize;
        icon.style.marginRight = 14;
        icon.style.flexShrink = 0;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius =
            icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 8;
        var sprite = contract.Customer != null ? contract.Customer.Icon : null;
        if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
        else icon.style.backgroundColor = new StyleColor(ColBlueEdge);
        card.Add(icon);

        // Name + pitch + terms
        var body = new VisualElement();
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;

        var name = new Label(contract.Customer != null ? contract.Customer.CompanyName : "(no customer assigned)");
        ApplyFont(name, bold: true, size: 19);
        name.style.color = new StyleColor(ColTitleText);
        body.Add(name);

        var kind = new Label(contract.Title);
        ApplyFont(kind, bold: true, size: 14);
        kind.style.color = new StyleColor(contract.IsWholesale ? ColWholesale : ColSubtleText);
        body.Add(kind);

        if (!string.IsNullOrWhiteSpace(contract.Pitch))
        {
            var pitch = new Label(contract.Pitch);
            ApplyFont(pitch, size: 14);
            pitch.style.color = new StyleColor(ColSubtleText);
            pitch.style.whiteSpace = WhiteSpace.Normal;
            pitch.style.marginTop = 2;
            body.Add(pitch);
        }

        var terms = new Label(TermsLine(contract));
        ApplyFont(terms, size: 14);
        terms.style.color = new StyleColor(ColSubtleText);
        terms.style.marginTop = 4;
        terms.style.whiteSpace = WhiteSpace.Normal;
        body.Add(terms);
        card.Add(body);

        // Value + action
        var right = new VisualElement();
        right.style.width = 210;
        right.style.flexShrink = 0;
        right.style.alignItems = Align.FlexEnd;

        var value = new Label(EstimatedValueText(contract, inv));
        ApplyFont(value, bold: true, size: 21);
        value.style.color = new StyleColor(ColMoney);
        right.Add(value);

        var valueCaption = new Label(contract.IsWholesale ? "est. one-off revenue" : "est. revenue per day");
        ApplyFont(valueCaption, size: 12);
        valueCaption.style.color = new StyleColor(ColSubtleText);
        valueCaption.style.marginBottom = 6;
        right.Add(valueCaption);

        if (taken)
        {
            var signedLabel = new Label(contract.IsWholesale ? "DELIVERED" : "SIGNED");
            ApplyFont(signedLabel, bold: true, size: 15);
            signedLabel.style.color = new StyleColor(ColSubtleText);
            right.Add(signedLabel);
        }
        else
        {
            var sign = new Button(() => OnSign(contract)) { text = "SIGN CONTRACT" };
            StyleOrangeButton(sign);
            right.Add(sign);
        }

        card.Add(right);
        return card;
    }

    private static string TermsLine(ContractData c)
    {
        if (c.IsWholesale)
            return $"{c.PalletCount} full pallets · due in {c.LeadTimeDays} day(s) · " +
                   $"late fee {c.LateFeePercent:P0} · full-pallet quantities only";

        return $"{c.OrdersPerDayMin}–{c.OrdersPerDayMax} orders/day · " +
               $"~{c.EstimatedCasesPerDay} cases/day · cutoff {c.CutoffHour:00}:00 · " +
               $"due in {c.LeadTimeDays} day(s) · late fee {c.LateFeePercent:P0}";
    }

    /// <summary>
    /// Rough money the offer represents, for comparing cards. Always money COMING IN, so it's written
    /// with a leading "+".
    ///
    /// It used to lead with "~" for "approximately", which at this size read as a minus sign and made
    /// every contract look like a cost. The estimate caveat lives in the caption underneath instead,
    /// where it can't be mistaken for arithmetic.
    /// </summary>
    private static string EstimatedValueText(ContractData c, InventoryService inv)
    {
        if (inv == null) return $"x{c.PayRateMultiplier:0.00}";

        var sellable = inv.AllSkus.Where(s => s != null && s.SellValue > 0f).ToList();
        if (sellable.Count == 0) return $"x{c.PayRateMultiplier:0.00}";

        if (c.IsWholesale)
        {
            var palletCapable = sellable.Where(s => s.Ti > 0 && s.Hi > 0).ToList();
            if (palletCapable.Count == 0) return $"x{c.PayRateMultiplier:0.00}";
            float avgPalletValue = palletCapable.Average(s => s.Ti * s.Hi * s.SellValue);
            return $"+${Mathf.RoundToInt(avgPalletValue * c.PalletCount * c.PayRateMultiplier):N0}";
        }

        float avgCase = sellable.Average(s => s.SellValue);
        return $"+${Mathf.RoundToInt(avgCase * c.EstimatedCasesPerDay * c.PayRateMultiplier):N0}";
    }

    private void OnSign(ContractData contract)
    {
        if (!ServiceLocator.TryGet<OrderArrivalService>(out var arrivals) || arrivals == null) return;

        if (!arrivals.Sign(contract.ContractId))
        {
            UIToast.Show("Couldn't sign that contract — it may already be taken.");
            RebuildCards();
            return;
        }

        string who = contract.Customer != null ? contract.Customer.CompanyName : contract.ContractId;
        UIToast.Show(contract.IsWholesale
            ? $"{who}: {contract.PalletCount} pallets inbound — check the Work Queue."
            : $"{who} signed — orders start arriving at {contract.CutoffHour:00}:00.");

        RebuildCards();
    }

    // ── Styling helpers (mirrors WorkQueuePanel's) ───────────────────────────

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

    private static void ApplyFont(VisualElement el, bool bold = false, int size = -1)
    {
        var f = LilitaFont();
        if (f != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
        if (bold) el.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (size > 0) el.style.fontSize = size;
    }

    private static void StyleOrangeButton(Button b)
    {
        ApplyFont(b, bold: true, size: 15);
        b.style.backgroundColor = new StyleColor(ColOrange);
        b.style.color = new StyleColor(ColOrangeText);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(ColOrangeEdge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
        b.style.paddingLeft = 12; b.style.paddingRight = 12;
        b.style.height = 30;
        b.style.marginLeft = 0; b.style.marginRight = 0;
        b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = new StyleColor(ColOrangeHover));
        b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(ColOrange));
    }

    private static void StyleSquareButton(Button b)
    {
        ApplyFont(b, bold: true, size: 15);
        b.style.width = 32; b.style.height = 32;
        b.style.backgroundColor = new StyleColor(new Color(0.16f, 0.22f, 0.29f, 1f));
        b.style.color = new StyleColor(ColTitleText);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(ColBlueEdge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;
    }
}
