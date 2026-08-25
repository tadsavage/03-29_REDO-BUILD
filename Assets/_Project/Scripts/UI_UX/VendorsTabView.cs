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
/// the cross-tab "Order from Vendor" event. Constructed once by ContractsPanel and rebuilt into its
/// content container whenever the Vendors tab is selected.
/// </summary>
public class VendorsTabView
{
    private static readonly Color ColBorder     = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText  = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColOrange     = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColEmptyText  = new Color(0x4D / 255f, 0x65 / 255f, 0x77 / 255f, 1f);

    private const string ColPartnership = "PartnershipLevel";
    private const string ColFillRate = "FillRate";
    private const string ColAvgRevenue = "AvgDailyRevenue";
    private const string ColItemsAvailable = "ItemsAvailable";

    private readonly VendorEconomyService _economy;
    private readonly VendorPerformanceTracker _tracker;
    private readonly VendorUiSfxConfig _sfx;
    private readonly System.Action _hideOwner;
    private readonly ExcelHeaderSortController _sort = new();

    private VisualElement _root;
    private VisualElement _headerRow;
    private ScrollView _bodyScroll;
    private VisualElement _body;
    private AudioSource _sfxSource;

    private readonly Dictionary<string, VendorRow> _rowsByVendorId = new();
    private string _selectedVendorId;

    public VendorsTabView(VendorEconomyService economy, VendorPerformanceTracker tracker,
                           VendorUiSfxConfig sfx, System.Action hideOwner)
    {
        _economy = economy;
        _tracker = tracker;
        _sfx = sfx;
        _hideOwner = hideOwner;

        _sort.RegisterColumn(ColPartnership);
        _sort.RegisterColumn(ColFillRate);
        _sort.RegisterColumn(ColAvgRevenue);
        _sort.RegisterColumn(ColItemsAvailable);
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

        _headerRow = new VisualElement();
        _headerRow.style.flexDirection = FlexDirection.Row;
        _headerRow.style.marginBottom = 4;
        _headerRow.style.borderBottomWidth = 2;
        _headerRow.style.borderBottomColor = new StyleColor(ColBorder);
        _headerRow.style.paddingBottom = 4;
        _headerRow.style.paddingLeft = 10; _headerRow.style.paddingRight = 10;

        _headerRow.Add(MakeFixedHeaderCell("Icon", 32, 8));
        _headerRow.Add(MakeFixedHeaderCell("Vendor", 220, 24));
        _headerRow.Add(MakeSortableHeaderCell("Partnership", ColPartnership, 48 + 170, 24));
        _headerRow.Add(MakeSortableHeaderCell("Fill Rate", ColFillRate, 110, 24));
        _headerRow.Add(MakeSortableHeaderCell("Avg Daily Revenue", ColAvgRevenue, 150, 24));
        _headerRow.Add(MakeSortableHeaderCell("Items Available", ColItemsAvailable, 110, 24));
        _root.Add(_headerRow);

        _bodyScroll = new ScrollView(ScrollViewMode.Vertical);
        _bodyScroll.style.flexGrow = 1;
        _body = new VisualElement();
        _bodyScroll.Add(_body);
        _root.Add(_bodyScroll);

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
            _rowsByVendorId[vendor.VendorId] = row;
            _body.Add(row.Root);
        }
    }

    private List<VendorData> ApplySort(List<VendorData> vendors)
    {
        string column = _sort.ActiveSortColumn;
        if (column == null) return vendors;

        bool ascending = _sort.GetDirection(column) == ExcelHeaderSortController.SortDirection.Ascending;

        System.Func<VendorData, float> keySelector = column switch
        {
            ColPartnership => v => _economy?.GetState(v.VendorId)?.PartnershipLevel ?? 0,
            ColFillRate => v => _economy?.GetFillRate(v.VendorId) ?? 0f,
            ColAvgRevenue => v => _tracker?.GetAverageDailyRevenue(v.VendorId) ?? 0f,
            ColItemsAvailable => v => _economy?.GetItemsAvailableCount(v.VendorId) ?? 0,
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

    private void OnOrderFromVendorClicked(VendorData vendor)
    {
        if (vendor == null) return;
        EventManager.Instance?.Publish(GameEvents.Vendor.OnOrderFromVendorRequested, vendor.VendorId);
        _hideOwner?.Invoke();
    }

    private void OnPartnershipChanged(string eventId, string vendorId) => Refresh();
}
