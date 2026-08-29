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
///
/// Deals (the Spot Deals board, The Broker's salvage loads, and the per-vendor DEALS fill bar) no
/// longer live here — they moved to the INBOUND ORDER CREATION tab, which is where loads actually get
/// built and dispatched.
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
    private readonly VendorUiSfxConfig _sfx;
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
                           VendorUiSfxConfig sfx)
    {
        _economy = economy;
        _tracker = tracker;
        _sfx = sfx;

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

        // No in-body "VENDORS" caption — the modal's own title bar already reads "VENDORS" while this
        // tab is active, so a second copy directly under the tab strip was redundant.
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

        // No header label over the icon column — it's a decorative monogram/artwork swatch, not a
        // sortable or otherwise meaningful data column, so a header caption here was just noise.
        _headerRow.Add(MakeFixedHeaderCell("", 32, 6));
        _headerRow.Add(MakeFixedHeaderCell("Vendor", 190, 16));
        _headerRow.Add(MakeSortableHeaderCell("Partnership", ColPartnership, 44 + 190, 8));
        _headerRow.Add(MakeSortableHeaderCell("Travel\nTime", ColTravelTime, 84, 22));
        _headerRow.Add(MakeSortableHeaderCell("Pot Scratch\nItems", ColPotScratch, 104, 22));
        _headerRow.Add(MakeSortableHeaderCell("Best Price\nItems", ColBestPrice, 104, 22));
        _headerRow.Add(MakeSortableHeaderCell("Avg Daily\nSpend", ColAvgSpend, 112, 22));
        _headerRow.Add(MakeSortableHeaderCell("Avg Daily\nPallets", ColAvgPallets, 104, 22));
        _headerRow.Add(MakeSortableHeaderCell("Avg Hours\nin Door", ColAvgDwell, 104, 22));
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
        cell.style.fontSize = 18;
        cell.style.whiteSpace = WhiteSpace.Normal;
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
        cell.style.fontSize = 18;
        cell.style.whiteSpace = WhiteSpace.Normal;
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
}
