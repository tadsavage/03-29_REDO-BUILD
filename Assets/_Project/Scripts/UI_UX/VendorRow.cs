using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Inventory;

/// <summary>
/// Single unified row of the VENDORS tab: icon, name + colour-coded status dot, Partnership Level,
/// Travel Time, Pot Scratch Items, Best Price Items, Avg Daily Spend, Avg Daily Pallets, Avg Hours in
/// Door, the orange "Order from Vendor" button, and the red DEALS fill bar — all in one horizontal
/// VisualElement. Replaces the former VendorPartnershipListRow + VendorDataGridRow two-column split so
/// the whole tab reads as one scrollable panel.
/// </summary>
public class VendorRow
{
    private static readonly Color ColCardEven = new Color(36f / 255f, 48f / 255f, 62f / 255f, 0.65f);
    private static readonly Color ColCardOdd  = new Color(30f / 255f, 40f / 255f, 52f / 255f, 0.65f);
    private static readonly Color ColBgSelect = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 0.30f);
    private static readonly Color ColEdgeSel  = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColSubtle   = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColTitle    = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColMoney    = new Color(0x7E / 255f, 0xD6 / 255f, 0x8A / 255f, 1f);

    private static readonly Color ColOrange      = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge  = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColOrangeHover = new Color(0xC6 / 255f, 0x7F / 255f, 0x42 / 255f, 1f);
    private static readonly Color ColOrangePress = new Color(0x94 / 255f, 0x5E / 255f, 0x2E / 255f, 1f);
    private static readonly Color ColDisabled    = new Color(0x3A / 255f, 0x3A / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeText  = new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f);
    private static readonly Color ColMonogramText = new Color(0xF2 / 255f, 0xF6 / 255f, 0xF9 / 255f, 1f);

    private static readonly Color ColDealRed     = new Color(0xB0 / 255f, 0x2E / 255f, 0x2A / 255f, 1f);
    private static readonly Color ColDealRedEdge = new Color(0x6E / 255f, 0x1C / 255f, 0x19 / 255f, 1f);
    private static readonly Color ColDealTrack   = new Color(0x22 / 255f, 0x2A / 255f, 0x33 / 255f, 1f);

    // A small fixed palette of muted tones, deterministically chosen per vendor via a hash of its
    // VendorId, so vendors without artwork are still visually distinguishable from one another.
    private static readonly Color[] MonogramPalette =
    {
        new Color(0x4A / 255f, 0x6D / 255f, 0x7C / 255f, 1f),
        new Color(0x6B / 255f, 0x51 / 255f, 0x7A / 255f, 1f),
        new Color(0x3E / 255f, 0x7A / 255f, 0x5E / 255f, 1f),
        new Color(0x8A / 255f, 0x5A / 255f, 0x3E / 255f, 1f),
        new Color(0x5A / 255f, 0x5E / 255f, 0x8A / 255f, 1f),
        new Color(0x7A / 255f, 0x4E / 255f, 0x5A / 255f, 1f),
        new Color(0x4E / 255f, 0x7A / 255f, 0x7A / 255f, 1f),
        new Color(0x7A / 255f, 0x72 / 255f, 0x3E / 255f, 1f),
    };

    private readonly VendorData _vendor;
    private readonly VendorEconomyService _economy;
    private readonly VendorPerformanceTracker _tracker;
    private readonly AudioSource _sfxSource;
    private readonly VendorUiSfxConfig _sfx;

    private readonly VisualElement _root;
    private readonly VisualElement _statusDot;
    private readonly Label _nameLabel;
    private readonly Label _partnershipNumberLabel;
    private readonly Label _partnershipTierLabel;
    private readonly Label _travelTimeLabel;
    private readonly Label _potScratchLabel;
    private readonly Label _bestPriceLabel;
    private readonly Label _spendLabel;
    private readonly Label _palletsLabel;
    private readonly Label _dwellLabel;
    private readonly Button _orderButton;

    private readonly VisualElement _dealBarRoot;
    private readonly VisualElement _dealBarFill;
    private readonly Label _dealBarLabel;

    private bool _selected;
    private int _stripeIndex;

    public VisualElement Root => _root;
    public event System.Action<VendorData> OnSelected;
    public event System.Action OnOrderClicked;

    /// <summary>Fired when the red deal bar is clicked while a deal is live. VendorsTabView owns the
    /// deal popup/modal — this row only reports the gesture, same division of labor as OnOrderClicked.</summary>
    public event System.Action OnDealBarClicked;

    /// <summary>Played on hover (RowHoverClip) and on a successful order click (OrderClickSfx).</summary>
    public AudioClip HoverSfx { get; set; }
    public AudioClip OrderClickSfx { get; set; }

    public VendorRow(VendorData vendor, VendorEconomyService economy, VendorPerformanceTracker tracker,
                      AudioSource sfxSource = null, VendorUiSfxConfig sfx = null)
    {
        _vendor = vendor;
        _economy = economy;
        _tracker = tracker;
        _sfxSource = sfxSource;
        _sfx = sfx;
        if (sfx != null)
        {
            HoverSfx = sfx.RowHoverClip;
            OrderClickSfx = sfx.OrderButtonClickClip;
        }

        _root = new VisualElement();
        _root.style.flexDirection = FlexDirection.Row;
        _root.style.alignItems = Align.Center;
        _root.style.paddingTop = 6; _root.style.paddingBottom = 6;
        _root.style.paddingLeft = 10; _root.style.paddingRight = 10;
        _root.style.marginBottom = 4;
        _root.style.borderTopLeftRadius = _root.style.borderTopRightRadius =
            _root.style.borderBottomLeftRadius = _root.style.borderBottomRightRadius = 6;

        // Icon cell.
        var icon = new VisualElement();
        icon.style.width = 28; icon.style.height = 28;
        icon.style.flexShrink = 0;
        icon.style.marginRight = 6;
        icon.style.alignItems = Align.Center;
        icon.style.justifyContent = Justify.Center;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius =
            icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 5;
        BuildIcon(icon, vendor);
        _root.Add(icon);

        // Name + status-dot cell — fixed width so it lines up under a "Vendor" header.
        var nameCell = new VisualElement();
        nameCell.style.width = 160;
        nameCell.style.flexShrink = 0;
        nameCell.style.flexDirection = FlexDirection.Row;
        nameCell.style.alignItems = Align.Center;
        nameCell.style.marginRight = 16;

        _nameLabel = new Label(vendor != null ? vendor.DisplayName : "Unknown Vendor");
        _nameLabel.style.color = new StyleColor(ColTitle);
        _nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _nameLabel.style.fontSize = 13;
        _nameLabel.style.flexGrow = 1;
        _nameLabel.style.whiteSpace = WhiteSpace.Normal;
        _nameLabel.style.marginRight = 6;
        nameCell.Add(_nameLabel);

        _statusDot = new VisualElement();
        _statusDot.style.width = 10; _statusDot.style.height = 10;
        _statusDot.style.flexShrink = 0;
        _statusDot.style.borderTopLeftRadius = _statusDot.style.borderTopRightRadius =
            _statusDot.style.borderBottomLeftRadius = _statusDot.style.borderBottomRightRadius = 5;
        nameCell.Add(_statusDot);
        _root.Add(nameCell);

        // Partnership cell — split into a fixed-width signed number and a clipped/ellipsized tier
        // label so long tier names can never overflow into the Fill Rate cell.
        var partnershipCell = new VisualElement();
        partnershipCell.style.flexDirection = FlexDirection.Row;
        partnershipCell.style.alignItems = Align.Center;
        partnershipCell.style.marginRight = 16;

        _partnershipNumberLabel = new Label();
        _partnershipNumberLabel.style.width = 36;
        _partnershipNumberLabel.style.flexShrink = 0;
        _partnershipNumberLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _partnershipNumberLabel.style.fontSize = 13;
        partnershipCell.Add(_partnershipNumberLabel);

        _partnershipTierLabel = new Label();
        _partnershipTierLabel.style.minWidth = 108;
        _partnershipTierLabel.style.flexShrink = 0;
        _partnershipTierLabel.style.fontSize = 13;
        _partnershipTierLabel.style.overflow = Overflow.Hidden;
        _partnershipTierLabel.style.textOverflow = TextOverflow.Ellipsis;
        _partnershipTierLabel.style.whiteSpace = WhiteSpace.NoWrap;
        partnershipCell.Add(_partnershipTierLabel);
        _root.Add(partnershipCell);

        _travelTimeLabel = AddCell(64);
        _potScratchLabel = AddCell(84);
        _bestPriceLabel  = AddCell(84);
        _spendLabel      = AddCell(90);
        _palletsLabel    = AddCell(84);
        _dwellLabel      = AddCell(84);

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        _root.Add(spacer);

        // DEALS bar sits LEFT of Order From Vendor, per Tad's explicit request — the reverse of the
        // reference mock's [ORDER] [DEALS] reading order.
        _dealBarRoot = BuildDealBar(out _dealBarFill, out _dealBarLabel);
        _dealBarRoot.style.marginRight = 8;
        _root.Add(_dealBarRoot);

        _orderButton = new Button(HandleOrderClicked) { text = "ORDER FROM VENDOR" };
        StyleOrderButton(_orderButton);
        _root.Add(_orderButton);

        _root.pickingMode = PickingMode.Position;
        _root.RegisterCallback<MouseEnterEvent>(_ =>
        {
            PlaySfx(HoverSfx);
            if (!_selected) _root.style.backgroundColor = new StyleColor(_stripeIndex % 2 == 0 ? ColCardOdd : ColCardEven);
        });
        _root.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            if (!_selected) _root.style.backgroundColor = new StyleColor(_stripeIndex % 2 == 0 ? ColCardEven : ColCardOdd);
        });
        _root.RegisterCallback<ClickEvent>(_ => OnSelected?.Invoke(_vendor));

        Refresh();
    }

    private static void BuildIcon(VisualElement icon, VendorData vendor)
    {
        if (vendor?.Icon != null)
        {
            icon.style.backgroundImage = new StyleBackground(vendor.Icon);
            return;
        }

        // No vendor artwork exists in the project yet — use a deterministic colour-hash + first
        // letter monogram badge instead of a flat colour square, so missing art reads as an
        // intentional placeholder. Real art will be picked up automatically once assigned.
        string vendorId = vendor != null ? vendor.VendorId ?? string.Empty : string.Empty;
        int hash = 0;
        unchecked
        {
            foreach (char c in vendorId) hash = hash * 31 + c;
        }
        int paletteIndex = Mathf.Abs(hash) % MonogramPalette.Length;
        icon.style.backgroundColor = new StyleColor(MonogramPalette[paletteIndex]);

        string displayName = vendor != null ? vendor.DisplayName : null;
        char letter = !string.IsNullOrEmpty(displayName) ? char.ToUpperInvariant(displayName[0]) : '?';

        var monogram = new Label(letter.ToString());
        monogram.style.color = new StyleColor(ColMonogramText);
        monogram.style.unityFontStyleAndWeight = FontStyle.Bold;
        monogram.style.fontSize = 16;
        monogram.style.unityTextAlign = TextAnchor.MiddleCenter;
        icon.Add(monogram);
    }

    private Label AddCell(float width, bool bold = false, float marginRight = 12f)
    {
        var label = new Label();
        label.style.width = width;
        label.style.flexShrink = 0;
        label.style.marginRight = marginRight;
        label.style.color = new StyleColor(ColTitle);
        label.style.fontSize = 14;
        if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
        _root.Add(label);
        return label;
    }

    /// <summary>Pulls every live figure from the owning services and paints this row. Alternating
    /// row shading is set from the caller's index via <see cref="SetStripeIndex"/> so the grid reads
    /// as a spreadsheet, matching ContractsPanel's card convention.</summary>
    public void Refresh()
    {
        if (_vendor == null) return;

        var state = _economy?.GetState(_vendor.VendorId);
        int level = state?.PartnershipLevel ?? 0;

        _statusDot.style.backgroundColor = new StyleColor(PartnershipColorUtility.GetColor(level));

        _partnershipNumberLabel.text = $"{level:+0;-0;0}";
        _partnershipNumberLabel.style.color = new StyleColor(PartnershipColorUtility.GetColor(level));

        _partnershipTierLabel.text = PartnershipColorUtility.GetStatusText(level);
        _partnershipTierLabel.style.color = new StyleColor(PartnershipColorUtility.GetColor(level));

        float travelHours = _economy?.GetTravelTimeHours(_vendor.VendorId) ?? 0f;
        _travelTimeLabel.text = $"{travelHours:0.0} hrs";
        _travelTimeLabel.style.color = new StyleColor(ColSubtle);

        int potScratch = _economy?.GetPotScratchCount(_vendor.VendorId) ?? 0;
        _potScratchLabel.text = potScratch.ToString();
        _potScratchLabel.style.color = new StyleColor(potScratch > 0 ? ColDealRed : ColSubtle);

        int bestPrice = _economy?.GetBestPriceCount(_vendor.VendorId) ?? 0;
        _bestPriceLabel.text = bestPrice.ToString();
        _bestPriceLabel.style.color = new StyleColor(ColSubtle);

        float avgSpend = _tracker?.GetAverageDailyRevenue(_vendor.VendorId) ?? 0f;
        _spendLabel.text = Money(avgSpend);
        _spendLabel.style.color = new StyleColor(ColMoney);

        float avgPallets = _tracker?.GetAverageDailyPallets(_vendor.VendorId) ?? 0f;
        _palletsLabel.text = avgPallets.ToString("0.#");
        _palletsLabel.style.color = new StyleColor(ColSubtle);

        float avgDwell = _tracker?.GetAverageDwellHours(_vendor.VendorId) ?? 0f;
        _dwellLabel.text = $"{avgDwell:0.#} hrs";
        _dwellLabel.style.color = new StyleColor(ColSubtle);

        int itemsAvailable = _economy?.GetItemsAvailableCount(_vendor.VendorId) ?? 0;
        bool disabled = itemsAvailable <= 0;
        _orderButton.SetEnabled(!disabled);
        _orderButton.style.opacity = disabled ? 0.5f : 1f;
    }

    /// <summary>Polled independently of Refresh() (see VendorsTabView's fast timer) so the fill bar
    /// drains smoothly in real time without re-pulling every other stat every 100ms.</summary>
    public void RefreshDealBar(VendorDeal deal)
    {
        _dealBarRoot.style.display = deal != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (deal == null) return;

        _dealBarFill.style.width = new Length(Mathf.Clamp01(deal.Fraction) * 100f, LengthUnit.Percent);
        _dealBarLabel.text = $"DEAL! -{deal.DiscountPercent:0}%";
    }

    private VisualElement BuildDealBar(out VisualElement fill, out Label label)
    {
        var root = new VisualElement();
        root.style.width = 120;
        root.style.height = 34;
        root.style.flexShrink = 0;
        root.style.display = DisplayStyle.None;
        root.style.backgroundColor = new StyleColor(ColDealTrack);
        root.style.borderTopLeftRadius = root.style.borderTopRightRadius =
            root.style.borderBottomLeftRadius = root.style.borderBottomRightRadius = 6;
        root.style.borderTopWidth = root.style.borderBottomWidth =
            root.style.borderLeftWidth = root.style.borderRightWidth = 2;
        root.style.borderTopColor = root.style.borderBottomColor =
            root.style.borderLeftColor = root.style.borderRightColor = new StyleColor(ColDealRedEdge);
        root.style.overflow = Overflow.Hidden;
        root.pickingMode = PickingMode.Position;

        var fillEl = new VisualElement();
        fillEl.style.position = Position.Absolute;
        fillEl.style.left = 0; fillEl.style.top = 0; fillEl.style.bottom = 0;
        fillEl.style.width = new Length(100f, LengthUnit.Percent);
        fillEl.style.backgroundColor = new StyleColor(ColDealRed);
        root.Add(fillEl);
        fill = fillEl;

        var labelEl = MakeCenteredLabel("DEAL!");
        root.Add(labelEl);
        label = labelEl;

        root.RegisterCallback<ClickEvent>(evt =>
        {
            OnDealBarClicked?.Invoke();
            evt.StopPropagation();
        });

        return root;
    }

    private static Label MakeCenteredLabel(string text)
    {
        var label = new Label(text);
        label.style.position = Position.Absolute;
        label.style.left = 0; label.style.right = 0; label.style.top = 0; label.style.bottom = 0;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.color = new StyleColor(ColMonogramText);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 12;
        return label;
    }

    public void SetStripeIndex(int index)
    {
        _stripeIndex = index;
        if (!_selected)
            _root.style.backgroundColor = new StyleColor(index % 2 == 0 ? ColCardEven : ColCardOdd);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        _root.style.backgroundColor = new StyleColor(selected
            ? ColBgSelect
            : (_stripeIndex % 2 == 0 ? ColCardEven : ColCardOdd));
    }

    private void HandleOrderClicked()
    {
        if (_vendor == null || !_orderButton.enabledSelf) return;
        PlaySfx(OrderClickSfx);
        OnOrderClicked?.Invoke();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, _sfx != null ? _sfx.Volume : 1f);
    }

    private static string Money(float amount)
        => Mathf.Abs(amount - Mathf.Round(amount)) < 0.005f ? $"${amount:N0}" : $"${amount:N2}";

    /// <summary>Duplicated locally rather than shared with ContractsPanel/PurchasingPanel's private
    /// StyleSquareButton-style helpers — those are private to their own classes.</summary>
    private static void StyleOrderButton(Button b)
    {
        b.style.height = 34;
        b.style.paddingLeft = 14; b.style.paddingRight = 14;
        b.style.unityFontStyleAndWeight = FontStyle.Bold;
        b.style.fontSize = 13;
        b.style.color = new StyleColor(ColOrangeText);
        b.style.backgroundColor = new StyleColor(ColOrange);
        b.style.borderTopWidth = b.style.borderBottomWidth =
            b.style.borderLeftWidth = b.style.borderRightWidth = 2;
        b.style.borderTopColor = b.style.borderBottomColor =
            b.style.borderLeftColor = b.style.borderRightColor = new StyleColor(ColOrangeEdge);
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6;

        var normalBg = new StyleColor(ColOrange);
        var hoverBg = new StyleColor(ColOrangeHover);
        var pressBg = new StyleColor(ColOrangePress);
        var disabledBg = new StyleColor(ColDisabled);

        b.RegisterCallback<MouseEnterEvent>(_ => { if (b.enabledSelf) b.style.backgroundColor = hoverBg; });
        b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = b.enabledSelf ? normalBg : disabledBg);
        b.RegisterCallback<MouseDownEvent>(_ => { if (b.enabledSelf) b.style.backgroundColor = pressBg; });
        b.RegisterCallback<MouseUpEvent>(_ => b.style.backgroundColor = b.enabledSelf ? hoverBg : disabledBg);
    }
}
