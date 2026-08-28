using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Events;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// Top-level single-responsibility builder for the whole VENDORS tab content — a single unified
/// panel: one header row and one scrollable body of VendorRow instances (icon/name/status dot plus
/// the full data-grid cells), driven by ExcelHeaderSortController for header-click sorting. Routes
/// the cross-tab "Order from Vendor" event. Constructed once by PurchasingPanel and rebuilt into its
/// content container whenever the Vendors tab is selected.
/// </summary>
public class VendorsTabView
{
    private static readonly Color ColBorder     = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText  = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColOrange     = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColEmptyText  = new Color(0x4D / 255f, 0x65 / 255f, 0x77 / 255f, 1f);

    private const string ColPartnership = "PartnershipLevel";
    private const string ColTravelTime = "TravelTime";
    private const string ColPotScratch = "PotScratchItems";
    private const string ColBestPrice = "BestPriceItems";
    private const string ColAvgSpend = "AvgDailySpend";
    private const string ColAvgPallets = "AvgDailyPallets";
    private const string ColAvgDwell = "AvgHoursInDoor";

    private readonly VendorEconomyService _economy;
    private readonly VendorPerformanceTracker _tracker;
    private readonly VendorDealService _deals;
    private readonly InventoryService _inventory;
    private readonly VendorUiSfxConfig _sfx;
    private readonly System.Action<string, string, int, float> _onDealAccepted;
    private readonly ExcelHeaderSortController _sort = new();

    private VisualElement _root;
    private VisualElement _headerRow;
    private ScrollView _bodyScroll;
    private VisualElement _body;
    private AudioSource _sfxSource;

    private readonly Dictionary<string, VendorRow> _rowsByVendorId = new();
    private string _selectedVendorId;
    private float _lastHScroll = float.NaN;

    public VendorsTabView(VendorEconomyService economy, VendorPerformanceTracker tracker,
                           VendorUiSfxConfig sfx,
                           System.Action<string, string, int, float> onDealAccepted)
    {
        _economy = economy;
        _tracker = tracker;
        _sfx = sfx;
        _onDealAccepted = onDealAccepted;
        ServiceLocator.TryGet(out _deals);
        ServiceLocator.TryGet(out _inventory);

        _sort.RegisterColumn(ColPartnership);
        _sort.RegisterColumn(ColTravelTime);
        _sort.RegisterColumn(ColPotScratch);
        _sort.RegisterColumn(ColBestPrice);
        _sort.RegisterColumn(ColAvgSpend);
        _sort.RegisterColumn(ColAvgPallets);
        _sort.RegisterColumn(ColAvgDwell);
        _sort.OnStateChanged += RebuildRows;

        EventManager.Instance?.Subscribe<string>(GameEvents.Vendor.OnPartnershipLevelChanged, OnPartnershipChanged);
    }

    public VisualElement Build()
    {
        _root = new VisualElement();
        _root.style.flexDirection = FlexDirection.Column;
        _root.style.flexGrow = 1;

        // A dedicated, non-spatial one-shot source for row hover/click SFX — created once here and
        // shared by every VendorRow, per the plan's "shared AudioSource reference" hook.
        var sfxHost = new GameObject("VendorsTabView_SFX");
        _sfxSource = sfxHost.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 0f;

        var title = new Label("VENDORS");
        title.style.color = new StyleColor(ColTitleText);
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 16;
        title.style.marginBottom = 6;
        _root.Add(title);

        // Clipped wrapper + sliding inner row — same split ContractsPanel's Completed/Schedule tabs
        // use (_tabHeader/_completedHeaderRow) for a header that has to stay lined up over a grid wider
        // than the modal: the row itself is exactly as wide as its cells demand, the wrapper clips it to
        // the visible width, and SyncHeaderScroll slides the row left by the body's horizontal scroll
        // offset every frame so the two never drift apart.
        var headerClip = new VisualElement();
        headerClip.style.overflow = Overflow.Hidden;
        headerClip.style.flexShrink = 0;
        _root.Add(headerClip);

        _headerRow = new VisualElement();
        _headerRow.style.flexDirection = FlexDirection.Row;
        _headerRow.style.marginBottom = 4;
        _headerRow.style.borderBottomWidth = 2;
        _headerRow.style.borderBottomColor = new StyleColor(ColBorder);
        _headerRow.style.paddingBottom = 4;
        _headerRow.style.paddingLeft = 10; _headerRow.style.paddingRight = 10;

        _headerRow.Add(MakeFixedHeaderCell("Icon", 28, 6));
        _headerRow.Add(MakeFixedHeaderCell("Vendor", 160, 16));
        _headerRow.Add(MakeSortableHeaderCell("Partnership", ColPartnership, 36 + 108, 16));
        _headerRow.Add(MakeSortableHeaderCell("Travel Time", ColTravelTime, 64, 12));
        _headerRow.Add(MakeSortableHeaderCell("Pot Scratch Items", ColPotScratch, 84, 12));
        _headerRow.Add(MakeSortableHeaderCell("Best Price Items", ColBestPrice, 84, 12));
        _headerRow.Add(MakeSortableHeaderCell("Avg Daily Spend", ColAvgSpend, 90, 12));
        _headerRow.Add(MakeSortableHeaderCell("Avg Daily Pallets", ColAvgPallets, 84, 12));
        _headerRow.Add(MakeSortableHeaderCell("Avg Hours in Door", ColAvgDwell, 84, 12));
        headerClip.Add(_headerRow);

        // VerticalAndHorizontal: six new columns plus the order button and deals bar push a row past
        // the modal's width (same situation ContractsPanel's Completed/Schedule tabs solved this way) —
        // without this the rightmost cells (the buttons players actually need to click) are laid out
        // but visually clipped and unreachable, not merely scrolled off comfortably.
        _bodyScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        _bodyScroll.style.flexGrow = 1;
        _body = new VisualElement();
        _bodyScroll.Add(_body);
        _root.Add(_bodyScroll);
        _bodyScroll.schedule.Execute(SyncHeaderScroll).Every(16);

        var vendors = VendorRegistry.Load()?.AllVendors ?? new List<VendorData>();
        if (vendors.Count > 0) _selectedVendorId = vendors[0].VendorId;

        RebuildRows();

        _root.Add(BuildDealModal());
        // Deliberately separate from Refresh()'s once-per-tab-select cadence — see PollDealBars.
        _root.schedule.Execute(PollDealBars).Every(100);

        return _root;
    }

    private VisualElement MakeFixedHeaderCell(string label, float width, float marginRight)
    {
        var cell = new Label(label);
        cell.style.width = width;
        cell.style.flexShrink = 0;
        cell.style.marginRight = marginRight;
        cell.style.color = new StyleColor(ColTitleText);
        cell.style.unityFontStyleAndWeight = FontStyle.Bold;
        cell.style.fontSize = 13;
        return cell;
    }

    private VisualElement MakeSortableHeaderCell(string label, string columnId, float width, float marginRight)
    {
        var cell = new Label(label);
        cell.style.width = width;
        cell.style.flexShrink = 0;
        cell.style.marginRight = marginRight;
        cell.style.color = new StyleColor(ColTitleText);
        cell.style.unityFontStyleAndWeight = FontStyle.Bold;
        cell.style.fontSize = 13;
        cell.pickingMode = PickingMode.Position;
        cell.RegisterCallback<ClickEvent>(_ => _sort.OnHeaderClicked(columnId));

        void Repaint()
        {
            var dir = _sort.GetDirection(columnId);
            string arrow = dir == ExcelHeaderSortController.SortDirection.Ascending ? " \u25B2"
                          : dir == ExcelHeaderSortController.SortDirection.Descending ? " \u25BC" : "";
            cell.text = label + arrow;
            cell.style.color = new StyleColor(dir == ExcelHeaderSortController.SortDirection.None
                ? ColTitleText : ColOrange);
        }
        Repaint();
        _sort.OnStateChanged += Repaint;

        return cell;
    }

    /// <summary>Re-pulls all vendors' live state and rebuilds row content without rebuilding the
    /// whole layout tree — the entry point for both tab-select and the Partnership-changed event.</summary>
    public void Refresh()
    {
        foreach (var row in _rowsByVendorId.Values) row.Refresh();
    }

    private void RebuildRows()
    {
        if (_body == null) return;

        var vendors = VendorRegistry.Load()?.AllVendors ?? new List<VendorData>();
        vendors = ApplySort(vendors);

        _body.Clear();
        _rowsByVendorId.Clear();

        if (vendors.Count == 0)
        {
            var none = new Label("No vendors available.");
            none.style.color = new StyleColor(ColEmptyText);
            none.style.marginTop = 12;
            _body.Add(none);
            return;
        }

        for (int i = 0; i < vendors.Count; i++)
        {
            var vendor = vendors[i];

            var row = new VendorRow(vendor, _economy, _tracker, _sfxSource, _sfx);
            row.SetStripeIndex(i);
            row.SetSelected(vendor.VendorId == _selectedVendorId);
            row.OnSelected += OnVendorSelected;
            row.OnOrderClicked += () => OnOrderFromVendorClicked(vendor);
            row.OnDealBarClicked += () => OnDealBarClicked(vendor);
            row.RefreshDealBar(_deals?.GetActiveDeal(vendor.VendorId));
            _rowsByVendorId[vendor.VendorId] = row;
            _body.Add(row.Root);
        }
    }

    /// <summary>Mirrors ContractsPanel.SyncCompletedHeader exactly: slides the clipped header row
    /// sideways by whatever the body is scrolled, so the two never drift apart once the grid is wider
    /// than the modal.</summary>
    private void SyncHeaderScroll()
    {
        if (_headerRow == null) return;

        float x = _bodyScroll.scrollOffset.x;
        if (Mathf.Approximately(x, _lastHScroll)) return;
        _lastHScroll = x;
        _headerRow.style.left = -x;
    }

    /// <summary>Drains the red fill bar in real time. Deliberately separate from Refresh() (which
    /// re-pulls every stat and is only called on tab-select/partnership-change) — polling every stat
    /// at this rate would be wasteful when only the bar's width actually needs to move every tick.</summary>
    private void PollDealBars()
    {
        if (_deals == null) return;
        foreach (var kv in _rowsByVendorId)
            kv.Value.RefreshDealBar(_deals.GetActiveDeal(kv.Key));
    }

    private List<VendorData> ApplySort(List<VendorData> vendors)
    {
        string column = _sort.ActiveSortColumn;
        if (column == null) return vendors;

        bool ascending = _sort.GetDirection(column) == ExcelHeaderSortController.SortDirection.Ascending;

        System.Func<VendorData, float> keySelector = column switch
        {
            ColPartnership => v => _economy?.GetState(v.VendorId)?.PartnershipLevel ?? 0,
            ColTravelTime => v => _economy?.GetTravelTimeHours(v.VendorId) ?? 0f,
            ColPotScratch => v => _economy?.GetPotScratchCount(v.VendorId) ?? 0,
            ColBestPrice => v => _economy?.GetBestPriceCount(v.VendorId) ?? 0,
            ColAvgSpend => v => _tracker?.GetAverageDailyRevenue(v.VendorId) ?? 0f,
            ColAvgPallets => v => _tracker?.GetAverageDailyPallets(v.VendorId) ?? 0f,
            ColAvgDwell => v => _tracker?.GetAverageDwellHours(v.VendorId) ?? 0f,
            _ => v => 0f
        };

        return ascending
            ? vendors.OrderBy(keySelector).ToList()
            : vendors.OrderByDescending(keySelector).ToList();
    }

    private void OnVendorSelected(VendorData vendor)
    {
        if (vendor == null) return;
        _selectedVendorId = vendor.VendorId;

        foreach (var kv in _rowsByVendorId) kv.Value.SetSelected(kv.Key == _selectedVendorId);
    }

    /// <summary>Publishes the request and nothing else — VendorsTabView now lives inside the same
    /// PurchasingPanel instance that handles it (it used to live in a separate ContractsPanel, which is
    /// why this used to also hide "the panel that owns this view" after publishing). With both on the
    /// same panel, that Hide() call was undoing the Show()/tab-switch the event handler had just done —
    /// the panel would flash open on Inbound Order Creation and then immediately close.</summary>
    private void OnOrderFromVendorClicked(VendorData vendor)
    {
        if (vendor == null) return;
        EventManager.Instance?.Publish(GameEvents.Vendor.OnOrderFromVendorRequested, vendor.VendorId);
    }

    private void OnPartnershipChanged(string eventId, string vendorId) => Refresh();

    // ── Deal popup modal ─────────────────────────────────────────────────────
    //
    // "I'LL TAKE IT!" hands off to PurchasingPanel via _onDealAccepted (a direct delegate, not the
    // EventManager round-trip OnOrderFromVendorRequested uses — that one predates the Vendors tab
    // living in this same panel and had to cross panels; this one doesn't need to).

    private static readonly Color ColDealCardBg = new Color(20f / 255f, 28f / 255f, 38f / 255f, 1f);
    private static readonly Color ColDealRed = new Color(0xB0 / 255f, 0x2E / 255f, 0x2A / 255f, 1f);
    private static readonly Color ColDealRedEdge = new Color(0x6E / 255f, 0x1C / 255f, 0x19 / 255f, 1f);
    private static readonly Color ColSubtleText = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColMoney = new Color(0x7E / 255f, 0xD6 / 255f, 0x8A / 255f, 1f);

    private VisualElement _dealBlocker;
    private VisualElement _dealCard;
    private Label _dealVendorLabel;
    private Label _dealItemLabel;
    private Label _dealQtyLabel;
    private Label _dealDiscountLabel;
    private VisualElement _dealVendorIcon;
    private VisualElement _dealItemIcon;
    private string _dealPendingVendorId;

    private VisualElement BuildDealModal()
    {
        _dealBlocker = new VisualElement();
        _dealBlocker.style.position = Position.Absolute;
        _dealBlocker.style.left = 0; _dealBlocker.style.right = 0;
        _dealBlocker.style.top = 0; _dealBlocker.style.bottom = 0;
        _dealBlocker.style.alignItems = Align.Center;
        _dealBlocker.style.justifyContent = Justify.Center;
        _dealBlocker.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.6f));
        _dealBlocker.style.display = DisplayStyle.None;
        _dealBlocker.pickingMode = PickingMode.Position;
        _dealBlocker.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

        _dealCard = new VisualElement();
        _dealCard.style.width = 360;
        _dealCard.style.paddingLeft = 22; _dealCard.style.paddingRight = 22;
        _dealCard.style.paddingTop = 20; _dealCard.style.paddingBottom = 20;
        _dealCard.style.backgroundColor = new StyleColor(ColDealCardBg);
        _dealCard.style.borderTopWidth = _dealCard.style.borderBottomWidth =
            _dealCard.style.borderLeftWidth = _dealCard.style.borderRightWidth = 3;
        _dealCard.style.borderTopColor = _dealCard.style.borderBottomColor =
            _dealCard.style.borderLeftColor = _dealCard.style.borderRightColor = new StyleColor(ColDealRedEdge);
        _dealCard.style.borderTopLeftRadius = _dealCard.style.borderTopRightRadius =
            _dealCard.style.borderBottomLeftRadius = _dealCard.style.borderBottomRightRadius = 12;
        // Starting pose for the pop-in — see ShowDealModal for the two-step "overshoot then settle"
        // that fakes a bounce without a tweening library (none exists in this UI codebase).
        _dealCard.style.scale = new Scale(new Vector2(1f, 1f));
        _dealCard.style.opacity = 0f;

        var titleRow = new Label("DEAL!");
        titleRow.style.color = new StyleColor(ColDealRed);
        titleRow.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleRow.style.fontSize = 24;
        titleRow.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleRow.style.marginBottom = 12;
        _dealCard.Add(titleRow);

        var vendorRow = new VisualElement();
        vendorRow.style.flexDirection = FlexDirection.Row;
        vendorRow.style.alignItems = Align.Center;
        vendorRow.style.marginBottom = 10;
        _dealVendorIcon = MakeIconSlot();
        vendorRow.Add(_dealVendorIcon);
        _dealVendorLabel = new Label();
        _dealVendorLabel.style.color = new StyleColor(ColTitleText);
        _dealVendorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _dealVendorLabel.style.fontSize = 16;
        _dealVendorLabel.style.marginLeft = 10;
        vendorRow.Add(_dealVendorLabel);
        _dealCard.Add(vendorRow);

        var itemRow = new VisualElement();
        itemRow.style.flexDirection = FlexDirection.Row;
        itemRow.style.alignItems = Align.Center;
        itemRow.style.marginBottom = 6;
        _dealItemIcon = MakeIconSlot();
        itemRow.Add(_dealItemIcon);
        var itemTextCol = new VisualElement();
        itemTextCol.style.marginLeft = 10;
        _dealItemLabel = new Label();
        _dealItemLabel.style.color = new StyleColor(ColTitleText);
        _dealItemLabel.style.fontSize = 14;
        _dealItemLabel.style.whiteSpace = WhiteSpace.Normal;
        itemTextCol.Add(_dealItemLabel);
        _dealQtyLabel = new Label();
        _dealQtyLabel.style.color = new StyleColor(ColSubtleText);
        _dealQtyLabel.style.fontSize = 13;
        itemTextCol.Add(_dealQtyLabel);
        itemRow.Add(itemTextCol);
        _dealCard.Add(itemRow);

        _dealDiscountLabel = new Label();
        _dealDiscountLabel.style.color = new StyleColor(ColMoney);
        _dealDiscountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _dealDiscountLabel.style.fontSize = 18;
        _dealDiscountLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _dealDiscountLabel.style.marginTop = 8; _dealDiscountLabel.style.marginBottom = 16;
        _dealCard.Add(_dealDiscountLabel);

        var takeIt = new Button(OnDealAcceptClicked) { text = "I'LL TAKE IT!" };
        takeIt.style.height = 40;
        takeIt.style.marginBottom = 8;
        takeIt.style.unityFontStyleAndWeight = FontStyle.Bold;
        takeIt.style.fontSize = 15;
        takeIt.style.color = Color.white;
        takeIt.style.backgroundColor = new StyleColor(ColDealRed);
        takeIt.style.borderTopWidth = takeIt.style.borderBottomWidth =
            takeIt.style.borderLeftWidth = takeIt.style.borderRightWidth = 2;
        takeIt.style.borderTopColor = takeIt.style.borderBottomColor =
            takeIt.style.borderLeftColor = takeIt.style.borderRightColor = new StyleColor(ColDealRedEdge);
        takeIt.style.borderTopLeftRadius = takeIt.style.borderTopRightRadius =
            takeIt.style.borderBottomLeftRadius = takeIt.style.borderBottomRightRadius = 6;
        _dealCard.Add(takeIt);

        var nah = new Button(OnDealDeclineClicked) { text = "Nah - take me back.." };
        nah.style.height = 30;
        nah.style.backgroundColor = new StyleColor(Color.clear);
        nah.style.color = new StyleColor(ColSubtleText);
        nah.style.borderTopWidth = nah.style.borderBottomWidth =
            nah.style.borderLeftWidth = nah.style.borderRightWidth = 0;
        nah.style.unityFontStyleAndWeight = FontStyle.Bold;
        nah.style.fontSize = 12;
        _dealCard.Add(nah);

        _dealBlocker.Add(_dealCard);
        return _dealBlocker;
    }

    private static VisualElement MakeIconSlot()
    {
        var icon = new VisualElement();
        icon.style.width = 40; icon.style.height = 40;
        icon.style.flexShrink = 0;
        icon.style.borderTopLeftRadius = icon.style.borderTopRightRadius =
            icon.style.borderBottomLeftRadius = icon.style.borderBottomRightRadius = 5;
        icon.style.backgroundColor = new StyleColor(new Color(0.25f, 0.3f, 0.36f, 1f));
        return icon;
    }

    private void OnDealBarClicked(VendorData vendor)
    {
        if (vendor == null || _deals == null) return;
        var deal = _deals.GetActiveDeal(vendor.VendorId);
        if (deal == null) return; // expired in the instant between click and handler

        var sku = _inventory?.GetSkuData(deal.SkuId);
        if (sku == null) return;

        _dealPendingVendorId = vendor.VendorId;
        _dealVendorLabel.text = vendor.DisplayName;
        if (vendor.Icon != null) _dealVendorIcon.style.backgroundImage = new StyleBackground(vendor.Icon);
        _dealItemLabel.text = sku.ItemDescription;
        if (sku.Icon != null) _dealItemIcon.style.backgroundImage = new StyleBackground(sku.Icon);
        int cases = deal.Pallets * Mathf.Max(1, sku.Ti * sku.Hi);
        _dealQtyLabel.text = $"{deal.Pallets} pallet(s) - {cases} case(s)";
        _dealDiscountLabel.text = $"-{deal.DiscountPercent:0}% OFF";

        ShowDealModal();
    }

    /// <summary>
    /// Hand-built "pop then settle" — this UI codebase has no scale/bounce tween anywhere to reuse.
    /// Two scheduled steps fake an overshoot: jump straight to a slightly-oversized, fully-opaque pose
    /// (no tween on the way in — that first jump IS the "pop"), then ease back down to resting size a
    /// beat later, which reads as a small bounce without needing real spring/overshoot easing.
    /// </summary>
    private void ShowDealModal()
    {
        _dealBlocker.style.display = DisplayStyle.Flex;
        _dealBlocker.BringToFront();

        _dealCard.style.transitionProperty = new List<StylePropertyName>();
        _dealCard.style.scale = new Scale(new Vector2(1.15f, 1.15f));
        _dealCard.style.opacity = 1f;

        _dealCard.schedule.Execute(() =>
        {
            _dealCard.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale") };
            _dealCard.style.transitionDuration = new List<TimeValue> { new TimeValue(180, TimeUnit.Millisecond) };
            _dealCard.style.scale = new Scale(new Vector2(1f, 1f));
        }).ExecuteLater(16);
    }

    private void HideDealModal()
    {
        _dealBlocker.style.display = DisplayStyle.None;
        _dealPendingVendorId = null;
    }

    private void OnDealAcceptClicked()
    {
        if (string.IsNullOrEmpty(_dealPendingVendorId)) { HideDealModal(); return; }

        var deal = _deals?.GetActiveDeal(_dealPendingVendorId);
        string vendorId = _dealPendingVendorId;
        HideDealModal();
        if (deal == null) return; // expired while the modal was open

        _deals.ClaimDeal(vendorId);
        _onDealAccepted?.Invoke(vendorId, deal.SkuId, deal.Pallets, deal.DiscountPercent);
    }

    private void OnDealDeclineClicked()
    {
        if (!string.IsNullOrEmpty(_dealPendingVendorId)) _deals?.CancelDeal(_dealPendingVendorId);
        HideDealModal();
    }
}
