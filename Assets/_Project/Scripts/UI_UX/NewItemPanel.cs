using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Dual-tab panel for slot management:
/// Tab 1 (New Item): Assign pick slots to received items that don't yet have pick slots.
///   - Icon on left, item details in center, case count on right
///   - Details (Pallet Height, Case Weight, Area) at bottom
///   - Large auto-slot button that finds and assigns the first available slot
/// Tab 2 (Slotter): Manage already-assigned items.
///   - Shows all slotted items with their Pick Loc
///   - Free Slot button to unassign and bring to dock
///   - Assign Extra Slot / Auto-Assign Extra Slot / Move Slot buttons
/// Bound to the "8" key.
/// </summary>
public class NewItemPanel : IUIPanel
{
    private static readonly Color ColBg         = new Color(18f / 255f, 26f / 255f, 36f / 255f, 0.97f);
    private static readonly Color ColBorder     = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 1f);
    private static readonly Color ColTitleText  = new Color(0xCF / 255f, 0xE2 / 255f, 0xF0 / 255f, 1f);
    private static readonly Color ColSubtleText = new Color(0x7A / 255f, 0x99 / 255f, 0xB0 / 255f, 1f);
    private static readonly Color ColOrange     = new Color(0xB5 / 255f, 0x74 / 255f, 0x3A / 255f, 1f);
    private static readonly Color ColOrangeEdge = new Color(0x7A / 255f, 0x4C / 255f, 0x22 / 255f, 1f);
    private static readonly Color ColOrangeText = new Color(0xFD / 255f, 0xE8 / 255f, 0xCC / 255f, 1f);
    private static readonly Color ColOrangeHover = new Color(0xC6 / 255f, 0x7F / 255f, 0x42 / 255f, 1f);
    private static readonly Color ColLabelCell  = new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 1f);
    private static readonly Color ColBlueEdge   = new Color(0x2C / 255f, 0x5E / 255f, 0x82 / 255f, 1f);
    private static readonly Color ColBlueHover  = new Color(0x5B / 255f, 0xA6 / 255f, 0xD8 / 255f, 1f);
    private static readonly Color ColRowEven    = new Color(36f / 255f, 48f / 255f, 62f / 255f, 0.65f);
    private static readonly Color ColRowOdd     = new Color(30f / 255f, 40f / 255f, 52f / 255f, 0.65f);
    private static readonly Color ColDisabled   = new Color(0x4A / 255f, 0x5A / 255f, 0x6A / 255f, 0.5f);
    private static readonly Color ColGrayBlueBg = new Color(0x5A / 255f, 0x6E / 255f, 0x80 / 255f, 1f);
    private static readonly Color ColGrayBlueEdge = new Color(0x3C / 255f, 0x4A / 255f, 0x5A / 255f, 1f);
    private static readonly Color ColGrayBlueHover = new Color(0x6D / 255f, 0x84 / 255f, 0x9A / 255f, 1f);
    private static readonly Color ColGrayBlueDisabled = new Color(0x4C / 255f, 0x5E / 255f, 0x70 / 255f, 0.5f);

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

    private static void ApplyFont(VisualElement el, bool bold = false, int size = -1)
    {
        var f = LilitaFont();
        if (f != null) el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
        if (bold) el.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (size > 0) el.style.fontSize = size;
    }

    private ResizableWindow _resizer;   // edge grips + the title-bar resize button
    private Button _scaleButton;

    private readonly VisualElement _overlay;
    private readonly VisualElement _tabContainer;
    private readonly Button _tabNewItem;
    private readonly Button _tabSlotter;
    private readonly VisualElement _newItemTabContent;
    private readonly VisualElement _slotterTabContent;
    private readonly ScrollView _itemsScroll;
    private readonly ScrollView _slottedItemsScroll;
    private readonly DropdownField _aisleDropdown;
    private readonly DropdownField _bayDropdown;
    private readonly DropdownField _positionDropdown;
    private readonly Button _assignButton;
    private readonly Button _goToPickButton;
    private readonly Button _autoSlotButton;
    private readonly Button _assignExtraSlotBtn;
    private readonly Button _autoAssignExtraSlotBtn;
    private readonly Button _moveSlotBtn;

    private bool _visible;
    private bool _onNewItemTab = true;
    private PalletMasterRecord _selectedItem;
    private PalletMasterRecord _selectedSlottedItem;
    private string _selectedAisle;
    private string _selectedBay;
    private string _selectedPosition;

    private List<PalletMasterRecord> _itemsNeedingSlots = new();
    private List<PalletMasterRecord> _slottedItems = new();
    private List<SkuData> _skuChoices = new();

    public NewItemPanel(VisualElement root)
    {
        _overlay = Build(out _tabContainer, out _tabNewItem, out _tabSlotter,
            out _newItemTabContent, out _slotterTabContent,
            out _itemsScroll, out _slottedItemsScroll,
            out _aisleDropdown, out _bayDropdown, out _positionDropdown,
            out _assignButton, out _goToPickButton, out _autoSlotButton,
            out _assignExtraSlotBtn, out _autoAssignExtraSlotBtn, out _moveSlotBtn);
        root.Add(_overlay);
        Hide();
    }

    public bool IsVisible => _visible;

    /// <summary>IUIPanel's view of the same flag — implementing the interface is what puts this panel
    /// in UIKeyBindingManager's registry, which is what makes opening another panel close this one
    /// (and makes Tab close it via CloseAll).</summary>
    public bool IsOpen => _visible;

    public void Toggle() { if (_visible) Hide(); else Show(); }

    /// <summary>Open the panel with a specific SKU already selected for slotting.</summary>
    public void ShowWithSku(string skuId)
    {
        Show();
        // Find and select the item with this SKU
        var itemToSelect = _itemsNeedingSlots.FirstOrDefault(p => p.SkuId == skuId);
        if (itemToSelect != null)
        {
            _selectedItem = itemToSelect;
            RebuildItemsList();
            ResetDropdowns();
        }
    }

    private float _lastRefreshTime = -10f;
    private const float RefreshInterval = 0.5f; // Refresh every 0.5 seconds

    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
        _resizer?.ResetToNormal();
        PanelTitleChrome.SyncScaleGlyph(_scaleButton, _resizer);
        _lastRefreshTime = -10f; // Force immediate refresh
        Rebuild();
    }

    public void Update()
    {
        if (!_visible) return;

        // Periodically refresh to catch new items arriving at the dock
        float now = Time.time;
        if (now - _lastRefreshTime >= RefreshInterval)
        {
            _lastRefreshTime = now;
            if (_onNewItemTab)
            {
                if (RefreshItemsNeedingSlots())
                    RebuildItemsList();
            }
            else
            {
                RefreshSlottedItems();
                RebuildSlottedItemsList();
            }
        }
    }

    public void Hide()
    {
        _visible = false;
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
    }

    private void Rebuild()
    {
        RefreshItemsNeedingSlots();
        RebuildItemsList();
        ResetDropdowns();
    }

    private bool RefreshItemsNeedingSlots()
    {
        string previousSignature = string.Join("|", _itemsNeedingSlots.Select(p => p?.PalletId ?? ""));
        InventoryService inv = null;
        GameCore.Services.ServiceLocator.TryGet(out inv);

        if (inv == null)
        {
            _itemsNeedingSlots.Clear();
            _skuChoices.Clear();
            return previousSignature.Length > 0;
        }

        // Get all unique SKUs that have been received
        var receivedSkus = new HashSet<string>();
        foreach (var pallet in inv.AllPallets.Values)
        {
            if (pallet != null)
                receivedSkus.Add(pallet.SkuId);
        }

        // Find SKUs that don't have any pick slot assigned
        _skuChoices = inv.AllSkus
            .Where(s => receivedSkus.Contains(s.SkuId)
                && !SlotAssignmentService.GetSlotsForSku(s.SkuId).Any())
            .ToList();

        // Get one pallet for each SKU needing slots (for display)
        _itemsNeedingSlots.Clear();
        foreach (var sku in _skuChoices)
        {
            var pallet = inv.AllPallets.Values.FirstOrDefault(p => p != null && p.SkuId == sku.SkuId);
            if (pallet != null)
                _itemsNeedingSlots.Add(pallet);
        }

        string currentSignature = string.Join("|", _itemsNeedingSlots.Select(p => p?.PalletId ?? ""));
        return currentSignature != previousSignature;
    }

    private void RefreshSlottedItems()
    {
        InventoryService inv = null;
        GameCore.Services.ServiceLocator.TryGet(out inv);

        if (inv == null)
        {
            _slottedItems.Clear();
            return;
        }

        // Get all SKUs that have at least one pick slot assigned
        var skusWithSlots = SlotAssignmentService.AllAssignments.Values.Distinct().ToHashSet();

        _slottedItems.Clear();
        foreach (var skuId in skusWithSlots)
        {
            var pallet = inv.AllPallets.Values.FirstOrDefault(p => p != null && p.SkuId == skuId);
            if (pallet != null)
                _slottedItems.Add(pallet);
        }
    }

    private void RebuildItemsList()
    {
        _itemsScroll.Clear();

        if (_itemsNeedingSlots.Count == 0)
        {
            var empty = new Label("No items requiring pick slots.");
            ApplyFont(empty, size: 13);
            empty.style.color = new StyleColor(ColSubtleText);
            _itemsScroll.Add(empty);
            return;
        }

        for (int i = 0; i < _itemsNeedingSlots.Count; i++)
        {
            var pallet = _itemsNeedingSlots[i];
            _itemsScroll.Add(BuildItemButton(pallet, i));
        }
    }

    private void RebuildSlottedItemsList()
    {
        _slottedItemsScroll.Clear();

        if (_slottedItems.Count == 0)
        {
            var empty = new Label("No items assigned to pick slots yet.");
            ApplyFont(empty, size: 13);
            empty.style.color = new StyleColor(ColSubtleText);
            _slottedItemsScroll.Add(empty);
            return;
        }

        for (int i = 0; i < _slottedItems.Count; i++)
        {
            var pallet = _slottedItems[i];
            _slottedItemsScroll.Add(BuildSlottedItemButton(pallet, i));
        }
    }

    private void SwitchTab(bool toNewItem)
    {
        _onNewItemTab = toNewItem;

        if (toNewItem)
        {
            _tabNewItem.style.backgroundColor = new StyleColor(new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 0.9f));
            _tabNewItem.style.color = new StyleColor(Color.white);

            _tabSlotter.style.backgroundColor = new StyleColor(new Color(0x3A / 255f, 0x4E / 255f, 0x62 / 255f, 0.5f));
            _tabSlotter.style.color = new StyleColor(ColSubtleText);

            _newItemTabContent.style.display = DisplayStyle.Flex;
            _slotterTabContent.style.display = DisplayStyle.None;

            RefreshItemsNeedingSlots();
            RebuildItemsList();
        }
        else
        {
            _tabNewItem.style.backgroundColor = new StyleColor(new Color(0x3A / 255f, 0x4E / 255f, 0x62 / 255f, 0.5f));
            _tabNewItem.style.color = new StyleColor(ColSubtleText);

            _tabSlotter.style.backgroundColor = new StyleColor(new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 0.9f));
            _tabSlotter.style.color = new StyleColor(Color.white);

            _newItemTabContent.style.display = DisplayStyle.None;
            _slotterTabContent.style.display = DisplayStyle.Flex;

            RefreshSlottedItems();
            RebuildSlottedItemsList();
        }
    }

    private VisualElement BuildItemButton(PalletMasterRecord pallet, int index)
    {
        var button = new Button();
        button.style.flexDirection = FlexDirection.Column;
        button.style.alignItems = Align.Stretch;
        button.style.paddingTop = 12;
        button.style.paddingBottom = 12;
        button.style.paddingLeft = 16;
        button.style.paddingRight = 16;
        button.style.marginBottom = 8;
        button.style.backgroundColor = new StyleColor(index % 2 == 0 ? ColRowEven : ColRowOdd);
        button.style.borderBottomWidth = 1;
        button.style.borderBottomColor = new StyleColor(ColBorder);
        button.style.minHeight = 100;

        // Get SKU data for this pallet
        var sku = _skuChoices.FirstOrDefault(s => s.SkuId == pallet.SkuId);

        // Top row: Icon | Item Info | Case Count
        var topRow = new VisualElement();
        topRow.style.flexDirection = FlexDirection.Row;
        topRow.style.alignItems = Align.Center;
        topRow.style.justifyContent = Justify.SpaceBetween;
        topRow.style.width = Length.Percent(100);
        topRow.style.marginBottom = 12;

        // Icon (86x86)
        var iconContainer = new VisualElement();
        iconContainer.style.width = 86;
        iconContainer.style.height = 86;
        iconContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.6f));
        iconContainer.style.borderTopLeftRadius = iconContainer.style.borderTopRightRadius =
            iconContainer.style.borderBottomLeftRadius = iconContainer.style.borderBottomRightRadius = 6;
        iconContainer.style.marginRight = 16;
        iconContainer.style.flexShrink = 0;
        iconContainer.style.backgroundImage = new StyleBackground(sku?.Icon);
        topRow.Add(iconContainer);

        // Item info (center)
        var itemInfo = new VisualElement();
        itemInfo.style.flexDirection = FlexDirection.Column;
        itemInfo.style.flexGrow = 1;
        itemInfo.style.justifyContent = Justify.Center;

        var itemLabel = new Label(sku?.ItemDescription ?? pallet.SkuId);
        ApplyFont(itemLabel, bold: true, size: 16);
        itemLabel.style.color = new StyleColor(ColTitleText);
        itemInfo.Add(itemLabel);

        var itemNum = new Label($"Item: {pallet.SkuId}");
        ApplyFont(itemNum, size: 12);
        itemNum.style.color = new StyleColor(ColSubtleText);
        itemInfo.Add(itemNum);

        topRow.Add(itemInfo);

        // Total pallets needing slots for this SKU (right)
        InventoryService inv = null;
        GameCore.Services.ServiceLocator.TryGet(out inv);
        var palletsNeedingSlots = inv?.AllPallets.Values
            .Where(p => p != null && p.SkuId == pallet.SkuId && SlotAssignmentService.GetSlotsForSku(p.SkuId).Count == 0)
            .Count() ?? 1;

        var palletCount = new Label($"pallets: {palletsNeedingSlots}");
        ApplyFont(palletCount, bold: true, size: 13);
        palletCount.style.color = new StyleColor(ColLabelCell);
        palletCount.style.unityTextAlign = TextAnchor.MiddleRight;
        palletCount.style.marginLeft = 16;
        palletCount.style.flexShrink = 0;
        palletCount.style.minWidth = 100;
        topRow.Add(palletCount);

        button.Add(topRow);

        // Pick Slot row (middle)
        var pickSlotRow = new VisualElement();
        pickSlotRow.style.flexDirection = FlexDirection.Row;
        pickSlotRow.style.marginBottom = 8;
        pickSlotRow.style.paddingBottom = 8;
        pickSlotRow.style.borderBottomWidth = 1;
        pickSlotRow.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.3f));

        var pickSlotLabel = new Label("Pick Slot:");
        ApplyFont(pickSlotLabel, size: 12);
        pickSlotLabel.style.color = new StyleColor(ColSubtleText);
        pickSlotLabel.style.minWidth = 80;
        pickSlotRow.Add(pickSlotLabel);

        var assignedSlots = SlotAssignmentService.GetSlotsForSku(pallet.SkuId);
        var pickSlotValue = new Label(assignedSlots.Count > 0 ? string.Join(", ", assignedSlots) : "Unassigned");
        ApplyFont(pickSlotValue, bold: true, size: 12);
        pickSlotValue.style.color = new StyleColor(assignedSlots.Count > 0 ? ColLabelCell : ColSubtleText);
        pickSlotRow.Add(pickSlotValue);

        button.Add(pickSlotRow);

        // Details row at bottom
        var detailsRow = new VisualElement();
        detailsRow.style.flexDirection = FlexDirection.Row;
        detailsRow.style.justifyContent = Justify.SpaceBetween;
        detailsRow.style.width = Length.Percent(100);
        detailsRow.style.paddingTop = 8;
        detailsRow.style.borderTopWidth = 1;
        detailsRow.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.3f));

        var palletHeight = sku != null ? $"{sku.PltHeight:F2}m" : "—";
        var caseWeight = sku?.CaseWeight > 0 ? $"{sku.CaseWeight}lbs" : "—";
        var area = sku?.StorageArea.ToString() ?? "—";

        var detailLabel1 = new Label($"Pallet Height: {palletHeight}");
        ApplyFont(detailLabel1, size: 12);
        detailLabel1.style.color = new StyleColor(ColSubtleText);
        detailsRow.Add(detailLabel1);

        var detailLabel2 = new Label($"Case Weight: {caseWeight}");
        ApplyFont(detailLabel2, size: 12);
        detailLabel2.style.color = new StyleColor(ColSubtleText);
        detailsRow.Add(detailLabel2);

        var detailLabel3 = new Label($"Area: {area}");
        ApplyFont(detailLabel3, size: 12);
        detailLabel3.style.color = new StyleColor(ColSubtleText);
        detailsRow.Add(detailLabel3);

        button.Add(detailsRow);
        // Descendants ignore picking so any click within the row hits this button directly
        // rather than a child Label/VisualElement swallowing it as its own separate target.
        // Query<VisualElement>() includes the button itself (it IS a VisualElement), so the
        // sweep below also set the button's OWN pickingMode to Ignore -- silently excluding
        // the entire row from hit-testing no matter what event type its handler used. Confirmed
        // live via Unity MCP: the constructed row Button had pickingMode=Ignore at runtime.
        // Restore it explicitly after the sweep.
        button.Query<VisualElement>().ForEach(child => child.pickingMode = PickingMode.Ignore);
        button.pickingMode = PickingMode.Position;

        // ClickEvent (not PointerDownEvent) -- it only fires after Unity's own press+release
        // click gesture completes and releases this button's pointer capture. PointerDownEvent
        // fires mid-gesture, and RebuildItemsList() below replaces every row (including this
        // exact button) while the pointer was still captured by it, leaving a dangling capture
        // that silently ate every subsequent click. Matches BuildSlottedItemButton's already-
        // working pattern just below.
        button.RegisterCallback<ClickEvent>(_ =>
        {
            _selectedItem = pallet;
            ResetDropdowns();
            RebuildItemsList();
            UpdateButtonStates();
        });

        // Highlight if selected
        if (_selectedItem == pallet)
        {
            button.style.backgroundColor = new StyleColor(new Color(0x5B / 255f, 0xA6 / 255f, 0xD8 / 255f, 0.4f));
            button.style.borderBottomWidth = 2;
            button.style.borderBottomColor = new StyleColor(ColLabelCell);
        }

        return button;
    }

    private VisualElement BuildSlottedItemButton(PalletMasterRecord pallet, int index)
    {
        var button = new Button();
        button.style.flexDirection = FlexDirection.Column;
        button.style.alignItems = Align.Stretch;
        button.style.paddingTop = 12;
        button.style.paddingBottom = 12;
        button.style.paddingLeft = 16;
        button.style.paddingRight = 16;
        button.style.marginBottom = 8;
        button.style.backgroundColor = new StyleColor(index % 2 == 0 ? ColRowEven : ColRowOdd);
        button.style.borderBottomWidth = 1;
        button.style.borderBottomColor = new StyleColor(ColBorder);
        button.style.minHeight = 120;

        // Get SKU data
        InventoryService inv = null;
        GameCore.Services.ServiceLocator.TryGet(out inv);
        var sku = inv?.AllSkus.FirstOrDefault(s => s.SkuId == pallet.SkuId);

        // Get assigned slots for this SKU
        var assignedSlots = SlotAssignmentService.GetSlotsForSku(pallet.SkuId);

        // Top row: Icon | Item Info | Case Count
        var topRow = new VisualElement();
        topRow.style.flexDirection = FlexDirection.Row;
        topRow.style.alignItems = Align.Center;
        topRow.style.justifyContent = Justify.SpaceBetween;
        topRow.style.width = Length.Percent(100);
        topRow.style.marginBottom = 12;

        // Icon
        var iconContainer = new VisualElement();
        iconContainer.style.width = 86;
        iconContainer.style.height = 86;
        iconContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.6f));
        iconContainer.style.borderTopLeftRadius = iconContainer.style.borderTopRightRadius =
            iconContainer.style.borderBottomLeftRadius = iconContainer.style.borderBottomRightRadius = 6;
        iconContainer.style.marginRight = 16;
        iconContainer.style.flexShrink = 0;
        iconContainer.style.backgroundImage = new StyleBackground(sku?.Icon);
        topRow.Add(iconContainer);

        // Item info
        var itemInfo = new VisualElement();
        itemInfo.style.flexDirection = FlexDirection.Column;
        itemInfo.style.flexGrow = 1;
        itemInfo.style.justifyContent = Justify.Center;

        var itemLabel = new Label(sku?.ItemDescription ?? pallet.SkuId);
        ApplyFont(itemLabel, bold: true, size: 16);
        itemLabel.style.color = new StyleColor(ColTitleText);
        itemInfo.Add(itemLabel);

        var itemNum = new Label($"Item: {pallet.SkuId}");
        ApplyFont(itemNum, size: 12);
        itemNum.style.color = new StyleColor(ColSubtleText);
        itemInfo.Add(itemNum);

        topRow.Add(itemInfo);

        // Total live pallets of this SKU in warehouse
        var totalLivePallets = inv?.AllPallets.Values
            .Where(p => p != null && p.SkuId == pallet.SkuId)
            .Count() ?? 1;

        var palletCount = new Label($"pallets: {totalLivePallets}");
        ApplyFont(palletCount, bold: true, size: 13);
        palletCount.style.color = new StyleColor(ColLabelCell);
        palletCount.style.unityTextAlign = TextAnchor.MiddleRight;
        palletCount.style.marginLeft = 16;
        palletCount.style.flexShrink = 0;
        palletCount.style.minWidth = 100;
        topRow.Add(palletCount);

        button.Add(topRow);

        // Pick Location row
        if (assignedSlots.Count > 0)
        {
            var pickLocRow = new VisualElement();
            pickLocRow.style.flexDirection = FlexDirection.Row;
            pickLocRow.style.marginBottom = 8;
            pickLocRow.style.paddingBottom = 8;
            pickLocRow.style.borderBottomWidth = 1;
            pickLocRow.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.3f));

            var pickLocLabel = new Label("Pick Loc:");
            ApplyFont(pickLocLabel, size: 12);
            pickLocLabel.style.color = new StyleColor(ColSubtleText);
            pickLocLabel.style.minWidth = 80;
            pickLocRow.Add(pickLocLabel);

            var pickLocValue = new Label(string.Join(", ", assignedSlots));
            ApplyFont(pickLocValue, bold: true, size: 13);
            pickLocValue.style.color = new StyleColor(ColLabelCell);
            pickLocRow.Add(pickLocValue);

            button.Add(pickLocRow);

            // Free Slot button (orange)
            var freeSlotBtn = StyleOrangeButton(new Button(() => OnFreeSlotClicked(pallet)) { text = "Free Slot" });
            freeSlotBtn.style.marginBottom = 12;
            freeSlotBtn.style.minHeight = 32;
            button.Add(freeSlotBtn);
        }

        // Details row at bottom
        var detailsRow = new VisualElement();
        detailsRow.style.flexDirection = FlexDirection.Row;
        detailsRow.style.justifyContent = Justify.SpaceBetween;
        detailsRow.style.width = Length.Percent(100);
        detailsRow.style.paddingTop = 8;
        detailsRow.style.borderTopWidth = 1;
        detailsRow.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.3f));

        var palletHeight = sku != null ? $"{sku.PltHeight:F2}m" : "—";
        var caseWeight = sku?.CaseWeight > 0 ? $"{sku.CaseWeight}lbs" : "—";
        var area = sku?.StorageArea.ToString() ?? "—";

        var detailLabel1 = new Label($"Pallet Height: {palletHeight}");
        ApplyFont(detailLabel1, size: 12);
        detailLabel1.style.color = new StyleColor(ColSubtleText);
        detailsRow.Add(detailLabel1);

        var detailLabel2 = new Label($"Case Weight: {caseWeight}");
        ApplyFont(detailLabel2, size: 12);
        detailLabel2.style.color = new StyleColor(ColSubtleText);
        detailsRow.Add(detailLabel2);

        var detailLabel3 = new Label($"Area: {area}");
        ApplyFont(detailLabel3, size: 12);
        detailLabel3.style.color = new StyleColor(ColSubtleText);
        detailsRow.Add(detailLabel3);

        button.Add(detailsRow);

        // Selection handler
        button.RegisterCallback<ClickEvent>(_ =>
        {
            _selectedSlottedItem = pallet;
            RebuildSlottedItemsList();
            UpdateButtonStates();
        });

        // Highlight if selected
        if (_selectedSlottedItem == pallet)
        {
            button.style.backgroundColor = new StyleColor(new Color(0x5B / 255f, 0xA6 / 255f, 0xD8 / 255f, 0.4f));
            button.style.borderBottomWidth = 2;
            button.style.borderBottomColor = new StyleColor(ColLabelCell);
        }

        return button;
    }

    private void OnFreeSlotClicked(PalletMasterRecord pallet)
    {
        var slots = SlotAssignmentService.GetSlotsForSku(pallet.SkuId);
        foreach (var slot in slots)
        {
            SlotAssignmentService.Clear(slot);
        }
        UIToast.Show($"✓ Slots freed: {string.Join(", ", slots)}");
        RefreshSlottedItems();
        RebuildSlottedItemsList();
    }

    private void ResetDropdowns()
    {
        _selectedAisle = null;
        _selectedBay = null;
        _selectedPosition = null;

        RebuildAisleDropdown();
        RebuildBayDropdown();
        RebuildPositionDropdown();
        UpdateButtonStates();
    }

    private void RebuildAisleDropdown()
    {
        _aisleDropdown.choices = new List<string> { "— select aisle —" };
        _aisleDropdown.SetValueWithoutNotify("— select aisle —");
        _aisleDropdown.SetEnabled(_selectedItem != null);

        if (_selectedItem == null) return;

        // Only show aisles that have unassigned pick slots
        var aisles = SlotRegistry.PickSlots
            .Where(s => s.Rack != null && SlotAssignmentService.GetSku(s.Address) == null)
            .Select(s => s.Aisle)
            .Distinct()
            .OrderBy(a => a)
            .ToList();

        if (aisles.Count == 0)
        {
            _aisleDropdown.choices = new List<string> { "— no aisles available —" };
            return;
        }

        var choices = new List<string> { "— select aisle —" };
        choices.AddRange(aisles.Select(a => a.ToString("00")));
        _aisleDropdown.choices = choices;
    }

    private void RebuildBayDropdown()
    {
        _bayDropdown.choices = new List<string> { "— select bay —" };
        _bayDropdown.SetValueWithoutNotify("— select bay —");
        _bayDropdown.SetEnabled(_selectedItem != null && !string.IsNullOrEmpty(_selectedAisle) && _selectedAisle != "— select aisle —");

        if (!_bayDropdown.enabledSelf || _selectedAisle == null) return;

        // Only show bays that have unassigned pick slots in the selected aisle
        var bays = SlotRegistry.PickSlots
            .Where(s => s.Rack != null && s.Aisle.ToString("00") == _selectedAisle
                && SlotAssignmentService.GetSku(s.Address) == null)
            .Select(s => s.Bay)
            .Distinct()
            .OrderBy(b => b)
            .ToList();

        if (bays.Count == 0)
        {
            _bayDropdown.choices = new List<string> { "— no bays available —" };
            return;
        }

        var choices = new List<string> { "— select bay —" };
        choices.AddRange(bays.Select(b => b.ToString("00")));
        _bayDropdown.choices = choices;
    }

    private void RebuildPositionDropdown()
    {
        _positionDropdown.choices = new List<string> { "— select position —" };
        _positionDropdown.SetValueWithoutNotify("— select position —");
        _positionDropdown.SetEnabled(_selectedItem != null && !string.IsNullOrEmpty(_selectedBay) && _selectedBay != "— select bay —");

        if (!_positionDropdown.enabledSelf || _selectedBay == null) return;

        if (!int.TryParse(_selectedAisle, out var aisleNum) || !int.TryParse(_selectedBay, out var bayNum))
            return;

        // Only show positions that have unassigned pick slots in the selected aisle/bay
        var positions = SlotRegistry.PickSlots
            .Where(s => s.Rack != null && s.Aisle == aisleNum && s.Bay == bayNum
                && SlotAssignmentService.GetSku(s.Address) == null)
            .Select(s => s.Position.ToString())
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        if (positions.Count == 0)
        {
            _positionDropdown.choices = new List<string> { "— no positions available —" };
            return;
        }

        var choices = new List<string> { "— select position —" };
        choices.AddRange(positions);
        _positionDropdown.choices = choices;
    }

    private void UpdateButtonStates()
    {
        // New Item tab: need all dropdowns filled
        bool allSelected = _selectedItem != null
            && !string.IsNullOrEmpty(_selectedAisle) && _selectedAisle != "— select aisle —"
            && !string.IsNullOrEmpty(_selectedBay) && _selectedBay != "— select bay —"
            && !string.IsNullOrEmpty(_selectedPosition) && _selectedPosition != "— select position —";

        _assignButton.SetEnabled(allSelected);
        _goToPickButton.SetEnabled(allSelected);
        _autoSlotButton.SetEnabled(_selectedItem != null);

        // Slotter tab: enable action buttons when a slotted item is selected
        bool slottedItemSelected = _selectedSlottedItem != null;
        _assignExtraSlotBtn.SetEnabled(slottedItemSelected);
        _autoAssignExtraSlotBtn.SetEnabled(slottedItemSelected);
        _moveSlotBtn.SetEnabled(slottedItemSelected);
    }

    private void OnGoToPickClicked()
    {
        if (_selectedItem == null || string.IsNullOrEmpty(_selectedPosition)) return;

        if (!int.TryParse(_selectedAisle, out var aisleNum) ||
            !int.TryParse(_selectedBay, out var bayNum) ||
            !int.TryParse(_selectedPosition, out var posNum))
            return;

        var slot = SlotRegistry.PickSlots.FirstOrDefault(s =>
            s.Aisle == aisleNum && s.Bay == bayNum && s.Position == posNum);

        if (slot.Rack == null) return;

        // Position camera at the pick slot for inspection
        PositionCameraAtSlot(slot);
    }

    private void PositionCameraAtSlot(SlotRegistry.Slot slot)
    {
        if (slot.Rack == null) return;

        // Get the slot's position in world space
        var slotWorldPos = slot.Rack.transform.position;

        // Get the aisle forward direction (which way the rack faces for picking)
        var aisleForward = slot.Rack.transform.forward;

        // Enter camera focus view
        PickSlotCameraFocus.EnterFocusView(slotWorldPos, aisleForward);
    }

    private void OnAssignExtraSlotClicked()
    {
        if (_selectedSlottedItem == null) return;

        InventoryService inv = null;
        GameCore.Services.ServiceLocator.TryGet(out inv);
        if (inv == null) return;

        var sku = inv.AllSkus.FirstOrDefault(s => s.SkuId == _selectedSlottedItem.SkuId);
        if (sku == null) return;

        // Find the first available pick slot that's large enough
        var requiredHeight = sku.PltHeight;
        var availableSlot = SlotRegistry.PickSlots
            .Where(s => s.Rack != null && SlotAssignmentService.GetSku(s.Address) == null)
            .FirstOrDefault(s => s.Rack.data.objHeight >= requiredHeight);

        if (availableSlot.Rack == null)
        {
            UIToast.Show($"⚠ No Available Slots of {requiredHeight:F2}m size");
            return;
        }

        // Assign additional slot to this SKU
        SlotAssignmentService.Assign(availableSlot.Address, _selectedSlottedItem.SkuId);
        UIToast.Show($"✓ Extra slot assigned: {availableSlot.Address}");

        // Refresh
        RefreshSlottedItems();
        RebuildSlottedItemsList();
    }

    private void OnAutoAssignExtraSlotClicked()
    {
        if (_selectedSlottedItem == null) return;

        InventoryService inv = null;
        GameCore.Services.ServiceLocator.TryGet(out inv);
        if (inv == null) return;

        var sku = inv.AllSkus.FirstOrDefault(s => s.SkuId == _selectedSlottedItem.SkuId);
        if (sku == null) return;

        // Find the first available pick slot that's large enough
        var requiredHeight = sku.PltHeight;
        var availableSlot = SlotRegistry.PickSlots
            .Where(s => s.Rack != null && SlotAssignmentService.GetSku(s.Address) == null)
            .FirstOrDefault(s => s.Rack.data.objHeight >= requiredHeight);

        if (availableSlot.Rack == null)
        {
            UIToast.Show($"⚠ No Available Slots of {requiredHeight:F2}m size");
            return;
        }

        // Assign additional slot to this SKU
        SlotAssignmentService.Assign(availableSlot.Address, _selectedSlottedItem.SkuId);
        UIToast.Show($"✓ Extra slot auto-assigned: {availableSlot.Address}");

        // Refresh
        RefreshSlottedItems();
        RebuildSlottedItemsList();
    }

    private void OnMoveSlotClicked()
    {
        if (_selectedSlottedItem == null) return;

        // This would move the pallet to a different slot - stub for now
        UIToast.Show("⚠ Move Slot not yet implemented");
    }

    private void OnAutoSlotClicked()
    {
        if (_selectedItem == null) return;

        var sku = _skuChoices.FirstOrDefault(s => s.SkuId == _selectedItem.SkuId);
        if (sku == null) return;

        // Find the first available pick slot that's large enough
        var requiredHeight = sku.PltHeight;
        var availableSlot = SlotRegistry.PickSlots
            .Where(s => s.Rack != null && SlotAssignmentService.GetSku(s.Address) == null)
            .FirstOrDefault(s => s.Rack.data.objHeight >= requiredHeight);

        if (availableSlot.Rack == null)
        {
            UIToast.Show($"⚠ No Available Slots of {requiredHeight:F2}m size");
            return;
        }

        // Auto-assign to this slot
        SlotAssignmentService.Assign(availableSlot.Address, _selectedItem.SkuId);
        UIToast.Show($"✓ Auto-slotted: {availableSlot.Address}");

        // Reset and rebuild
        _selectedItem = null;
        Rebuild();
    }

    private void OnAssignClicked()
    {
        if (_selectedItem == null || string.IsNullOrEmpty(_selectedPosition)) return;

        if (!int.TryParse(_selectedAisle, out var aisleNum) ||
            !int.TryParse(_selectedBay, out var bayNum) ||
            !int.TryParse(_selectedPosition, out var posNum))
            return;

        var slot = SlotRegistry.PickSlots.FirstOrDefault(s =>
            s.Aisle == aisleNum && s.Bay == bayNum && s.Position == posNum);

        if (slot.Rack == null) return;

        // Check if this slot is already assigned
        var existingAssignment = SlotAssignmentService.GetSku(slot.Address);
        if (existingAssignment != null)
        {
            UIToast.Show($"⚠ This slot is already taken!");
            return;
        }

        // Assign this SKU to this pick slot
        SlotAssignmentService.Assign(slot.Address, _selectedItem.SkuId);

        UIToast.Show($"✓ Pick slot assigned: {slot.Address}");

        // Putaway logic kicks in here (future work)

        // Reset and rebuild
        _selectedItem = null;
        Rebuild();
    }

    // ── Shell ────────────────────────────────────────────────────────────────
    private VisualElement Build(out VisualElement tabContainer, out Button tabNewItem, out Button tabSlotter,
        out VisualElement newItemTabContent, out VisualElement slotterTabContent,
        out ScrollView itemsScroll, out ScrollView slottedItemsScroll,
        out DropdownField aisleDropdown, out DropdownField bayDropdown, out DropdownField positionDropdown,
        out Button assignButton, out Button goToPickButton, out Button autoSlotButton,
        out Button assignExtraSlotBtn, out Button autoAssignExtraSlotBtn, out Button moveSlotBtn)
    {
        var overlay = new VisualElement { name = "new-item-overlay" };
        overlay.style.position = Position.Absolute;
        // Stops above the bottom HUD so this scrim can't swallow clicks on the bar or the Build/Play
        // tabs — see the note on workqueue-overlay in WorkQueuePanel.Build.
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0;
        overlay.style.bottom = BuildMenuUI.BottomHudReservedHeight;
        overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
        overlay.style.justifyContent = Justify.FlexStart;
        overlay.style.alignItems = Align.Center;

        var modal = new VisualElement { name = "new-item-modal" };
        modal.style.position = Position.Absolute;
        modal.style.left = 100;
        modal.style.top = 100;
        modal.style.backgroundColor = new StyleColor(ColBg);
        modal.style.borderTopWidth = modal.style.borderBottomWidth =
            modal.style.borderLeftWidth = modal.style.borderRightWidth = 3;
        modal.style.borderTopColor = modal.style.borderBottomColor =
            modal.style.borderLeftColor = modal.style.borderRightColor = new StyleColor(ColBorder);
        modal.style.borderTopLeftRadius = modal.style.borderTopRightRadius =
            modal.style.borderBottomLeftRadius = modal.style.borderBottomRightRadius = 16;
        modal.style.paddingTop = 0; modal.style.paddingBottom = 14;
        modal.style.paddingLeft = 16; modal.style.paddingRight = 16;
        modal.style.height = 760;
        modal.style.minHeight = 700;
        modal.style.maxHeight = StyleKeyword.None;
        modal.style.flexDirection = FlexDirection.Column;

        // Title bar
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.marginBottom = 0;
        titleBar.style.paddingTop = 14;
        titleBar.style.paddingBottom = 0;
        titleBar.style.paddingLeft = 0;
        titleBar.style.paddingRight = 0;

        // Balances the two corner buttons on the right so the centred title stays centred.
        var titleSpacer = new VisualElement();
        titleSpacer.style.width = PanelTitleChrome.ButtonSize * 2f + 6f;
        titleSpacer.style.flexShrink = 0;
        titleBar.Add(titleSpacer);

        var title = new Label("Inventory Slotting");
        ApplyFont(title, bold: true, size: 28);
        title.style.color = new StyleColor(ColTitleText);
        title.style.flexGrow = 1;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(title);

        // Standard corner: resize on the left, close on the right — same chrome as ContractsPanel (6).
        // The resizer is built before the buttons because Adopt needs it to wire the left one.
        _resizer = new ResizableWindow(modal, minW: 760f, minH: 700f, grip: 10f, titleInset: 62f);
        var closeButton = new Button { text = "✕" };
        titleBar.Add(closeButton);
        (_scaleButton, _) = PanelTitleChrome.Adopt(closeButton, _resizer, Hide);
        modal.Add(titleBar);

        new DraggableWindow(modal, titleBar, closeButton);

        // Tab container
        tabContainer = new VisualElement();
        tabContainer.style.flexDirection = FlexDirection.Row;
        tabContainer.style.marginBottom = 12;
        tabContainer.style.paddingBottom = 8;
        tabContainer.style.borderBottomWidth = 2;
        tabContainer.style.borderBottomColor = new StyleColor(ColBorder);
        tabContainer.style.paddingLeft = 0;
        tabContainer.style.paddingRight = 0;

        tabNewItem = new Button(() => SwitchTab(true)) { text = "New Item" };
        ApplyFont(tabNewItem, bold: true, size: 13);
        tabNewItem.style.backgroundColor = new StyleColor(new Color(0x4C / 255f, 0x90 / 255f, 0xC0 / 255f, 0.8f));
        tabNewItem.style.color = new StyleColor(Color.white);
        tabNewItem.style.borderBottomWidth = 0;
        tabNewItem.style.borderTopLeftRadius = tabNewItem.style.borderBottomLeftRadius = 6;
        tabNewItem.style.borderTopRightRadius = tabNewItem.style.borderBottomRightRadius = 0;
        tabNewItem.style.borderTopWidth = tabNewItem.style.borderLeftWidth = 1;
        tabNewItem.style.borderRightWidth = 1;
        tabNewItem.style.borderTopColor = tabNewItem.style.borderLeftColor = tabNewItem.style.borderRightColor = new StyleColor(ColBorder);
        tabNewItem.style.paddingTop = 10; tabNewItem.style.paddingBottom = 10;
        tabNewItem.style.paddingLeft = 24; tabNewItem.style.paddingRight = 24;
        tabNewItem.style.marginRight = 4;
        tabContainer.Add(tabNewItem);

        tabSlotter = new Button(() => SwitchTab(false)) { text = "Slotter" };
        ApplyFont(tabSlotter, bold: true, size: 13);
        tabSlotter.style.backgroundColor = new StyleColor(new Color(0x3A / 255f, 0x4E / 255f, 0x62 / 255f, 0.6f));
        tabSlotter.style.color = new StyleColor(ColSubtleText);
        tabSlotter.style.borderBottomWidth = 0;
        tabSlotter.style.borderTopLeftRadius = 0;
        tabSlotter.style.borderBottomLeftRadius = 0;
        tabSlotter.style.borderTopRightRadius = tabSlotter.style.borderBottomRightRadius = 6;
        tabSlotter.style.borderTopWidth = tabSlotter.style.borderLeftWidth = tabSlotter.style.borderRightWidth = 1;
        tabSlotter.style.borderTopColor = tabSlotter.style.borderLeftColor = tabSlotter.style.borderRightColor = new StyleColor(ColBorder);
        tabSlotter.style.paddingTop = 10; tabSlotter.style.paddingBottom = 10;
        tabSlotter.style.paddingLeft = 24; tabSlotter.style.paddingRight = 24;
        tabContainer.Add(tabSlotter);

        modal.Add(tabContainer);

        // NEW ITEM TAB CONTENT
        newItemTabContent = new VisualElement();
        newItemTabContent.style.display = DisplayStyle.Flex;
        newItemTabContent.style.flexGrow = 1;
        newItemTabContent.style.flexShrink = 1;
        newItemTabContent.style.minHeight = 0;

        itemsScroll = new ScrollView();
        itemsScroll.style.height = 420;
        itemsScroll.style.flexGrow = 0;
        itemsScroll.style.flexShrink = 0;
        itemsScroll.style.marginBottom = 16;
        itemsScroll.style.borderBottomWidth = 1;
        itemsScroll.style.borderBottomColor = new StyleColor(ColBorder);
        itemsScroll.style.paddingBottom = 12;
        newItemTabContent.Add(itemsScroll);

        // Slot Assignment section
        var slotLabel = new Label("Slot Assignment");
        ApplyFont(slotLabel, bold: true, size: 14);
        slotLabel.style.color = new StyleColor(ColTitleText);
        slotLabel.style.marginBottom = 10;
        newItemTabContent.Add(slotLabel);

        // Dropdown row
        var dropdownRow = new VisualElement();
        dropdownRow.style.flexDirection = FlexDirection.Row;
        dropdownRow.style.justifyContent = Justify.SpaceBetween;
        dropdownRow.style.flexShrink = 0;
        dropdownRow.style.minHeight = 72;

        // Aisle
        var aisleContainer = new VisualElement();
        aisleContainer.style.flexGrow = 1;
        aisleContainer.style.marginRight = 12;
        var aisleLabel = new Label("Aisle");
        ApplyFont(aisleLabel, size: 11);
        aisleLabel.style.color = new StyleColor(ColSubtleText);
        aisleLabel.style.marginBottom = 4;
        aisleContainer.Add(aisleLabel);
        aisleDropdown = new DropdownField(new List<string> { "— select aisle —" }, 0);
        ApplyFont(aisleDropdown, size: 12);
        aisleDropdown.style.width = Length.Percent(100);
        aisleDropdown.SetEnabled(false);
        // Registered once here (not inside RebuildAisleDropdown) to avoid stacking a new
        // duplicate callback every time the dropdown choices are rebuilt.
        aisleDropdown.RegisterValueChangedCallback(evt =>
        {
            _selectedAisle = evt.newValue == "— select aisle —" ? null : evt.newValue;
            _selectedBay = null;
            _selectedPosition = null;
            RebuildBayDropdown();
            RebuildPositionDropdown();
            UpdateButtonStates();
        });
        aisleContainer.Add(aisleDropdown);
        dropdownRow.Add(aisleContainer);

        // Bay
        var bayContainer = new VisualElement();
        bayContainer.style.flexGrow = 1;
        bayContainer.style.marginRight = 12;
        var bayLabel = new Label("Bay");
        ApplyFont(bayLabel, size: 11);
        bayLabel.style.color = new StyleColor(ColSubtleText);
        bayLabel.style.marginBottom = 4;
        bayContainer.Add(bayLabel);
        bayDropdown = new DropdownField(new List<string> { "— select bay —" }, 0);
        ApplyFont(bayDropdown, size: 12);
        bayDropdown.style.width = Length.Percent(100);
        bayDropdown.SetEnabled(false);
        bayDropdown.RegisterValueChangedCallback(evt =>
        {
            _selectedBay = evt.newValue == "— select bay —" ? null : evt.newValue;
            _selectedPosition = null;
            RebuildPositionDropdown();
            UpdateButtonStates();
        });
        bayContainer.Add(bayDropdown);
        dropdownRow.Add(bayContainer);

        // Position
        var posContainer = new VisualElement();
        posContainer.style.flexGrow = 1;
        var posLabel = new Label("Position");
        ApplyFont(posLabel, size: 11);
        posLabel.style.color = new StyleColor(ColSubtleText);
        posLabel.style.marginBottom = 4;
        posContainer.Add(posLabel);
        positionDropdown = new DropdownField(new List<string> { "— select position —" }, 0);
        ApplyFont(positionDropdown, size: 12);
        positionDropdown.style.width = Length.Percent(100);
        positionDropdown.SetEnabled(false);
        positionDropdown.RegisterValueChangedCallback(evt =>
        {
            _selectedPosition = evt.newValue == "— select position —" ? null : evt.newValue;
            UpdateButtonStates();
        });
        posContainer.Add(positionDropdown);
        dropdownRow.Add(posContainer);

        newItemTabContent.Add(dropdownRow);

        // Buttons row - three equal width buttons
        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.justifyContent = Justify.SpaceBetween;
        buttonRow.style.marginBottom = 0;
        buttonRow.style.flexShrink = 0;
        buttonRow.style.minHeight = 56;

        assignButton = StyleOrangeButton(new Button(OnAssignClicked) { text = "Assign Pick Slot" });
        assignButton.style.flexGrow = 1;
        assignButton.style.marginRight = 8;
        assignButton.SetEnabled(false);
        assignButton.style.minHeight = 48;
        buttonRow.Add(assignButton);

        autoSlotButton = new Button(OnAutoSlotClicked) { text = "Auto-Slot Selected Item" };
        autoSlotButton.style.flexGrow = 1;
        autoSlotButton.style.marginRight = 8;
        autoSlotButton.SetEnabled(false);
        autoSlotButton.style.minHeight = 48;
        StyleGrayBlueButton(autoSlotButton);
        buttonRow.Add(autoSlotButton);

        goToPickButton = StyleBlueButton(new Button(OnGoToPickClicked) { text = "Go to Pick" });
        goToPickButton.style.flexGrow = 1;
        goToPickButton.SetEnabled(false);
        goToPickButton.style.minHeight = 48;
        buttonRow.Add(goToPickButton);

        newItemTabContent.Add(buttonRow);

        modal.Add(newItemTabContent);

        // SLOTTER TAB CONTENT
        slotterTabContent = new VisualElement();
        slotterTabContent.style.display = DisplayStyle.None;
        slotterTabContent.style.flexDirection = FlexDirection.Column;

        slottedItemsScroll = new ScrollView();
        slottedItemsScroll.style.maxHeight = 550;
        slottedItemsScroll.style.marginBottom = 16;
        slottedItemsScroll.style.borderBottomWidth = 1;
        slottedItemsScroll.style.borderBottomColor = new StyleColor(ColBorder);
        slottedItemsScroll.style.paddingBottom = 12;
        slotterTabContent.Add(slottedItemsScroll);

        // Move destination slot selection (dropdowns)
        var moveToSlotLabel = new Label("Move To Slot");
        ApplyFont(moveToSlotLabel, bold: true, size: 14);
        moveToSlotLabel.style.color = new StyleColor(ColTitleText);
        moveToSlotLabel.style.marginBottom = 10;
        slotterTabContent.Add(moveToSlotLabel);

        // Dropdown row
        var slotterDropdownRow = new VisualElement();
        slotterDropdownRow.style.flexDirection = FlexDirection.Row;
        slotterDropdownRow.style.justifyContent = Justify.SpaceBetween;
        slotterDropdownRow.style.marginBottom = 12;

        // Aisle dropdown for slotter
        var slotterAisleContainer = new VisualElement();
        slotterAisleContainer.style.flexGrow = 1;
        slotterAisleContainer.style.marginRight = 12;
        var slotterAisleLabel = new Label("Aisle");
        ApplyFont(slotterAisleLabel, size: 11);
        slotterAisleLabel.style.color = new StyleColor(ColSubtleText);
        slotterAisleLabel.style.marginBottom = 4;
        slotterAisleContainer.Add(slotterAisleLabel);
        var slotterAisleDropdown = new DropdownField(new List<string> { "— select aisle —" }, 0);
        ApplyFont(slotterAisleDropdown, size: 12);
        slotterAisleDropdown.style.width = Length.Percent(100);
        slotterAisleDropdown.SetEnabled(false);
        slotterAisleContainer.Add(slotterAisleDropdown);
        slotterDropdownRow.Add(slotterAisleContainer);

        // Bay dropdown for slotter
        var slotterBayContainer = new VisualElement();
        slotterBayContainer.style.flexGrow = 1;
        slotterBayContainer.style.marginRight = 12;
        var slotterBayLabel = new Label("Bay");
        ApplyFont(slotterBayLabel, size: 11);
        slotterBayLabel.style.color = new StyleColor(ColSubtleText);
        slotterBayLabel.style.marginBottom = 4;
        slotterBayContainer.Add(slotterBayLabel);
        var slotterBayDropdown = new DropdownField(new List<string> { "— select bay —" }, 0);
        ApplyFont(slotterBayDropdown, size: 12);
        slotterBayDropdown.style.width = Length.Percent(100);
        slotterBayDropdown.SetEnabled(false);
        slotterBayContainer.Add(slotterBayDropdown);
        slotterDropdownRow.Add(slotterBayContainer);

        // Position dropdown for slotter
        var slotterPosContainer = new VisualElement();
        slotterPosContainer.style.flexGrow = 1;
        var slotterPosLabel = new Label("Position");
        ApplyFont(slotterPosLabel, size: 11);
        slotterPosLabel.style.color = new StyleColor(ColSubtleText);
        slotterPosLabel.style.marginBottom = 4;
        slotterPosContainer.Add(slotterPosLabel);
        var slotterPosDropdown = new DropdownField(new List<string> { "— select position —" }, 0);
        ApplyFont(slotterPosDropdown, size: 12);
        slotterPosDropdown.style.width = Length.Percent(100);
        slotterPosDropdown.SetEnabled(false);
        slotterPosContainer.Add(slotterPosDropdown);
        slotterDropdownRow.Add(slotterPosContainer);

        slotterTabContent.Add(slotterDropdownRow);

        // Action buttons for slotter
        var slotterButtonsRow = new VisualElement();
        slotterButtonsRow.style.flexDirection = FlexDirection.Row;
        slotterButtonsRow.style.justifyContent = Justify.SpaceBetween;
        slotterButtonsRow.style.height = 48;

        assignExtraSlotBtn = StyleOrangeButton(new Button(OnAssignExtraSlotClicked) { text = "Assign Extra Slot" });
        assignExtraSlotBtn.style.flexGrow = 1;
        assignExtraSlotBtn.style.marginRight = 8;
        assignExtraSlotBtn.SetEnabled(false);
        assignExtraSlotBtn.style.minHeight = 48;
        slotterButtonsRow.Add(assignExtraSlotBtn);

        autoAssignExtraSlotBtn = StyleGrayBlueButton(new Button(OnAutoAssignExtraSlotClicked) { text = "Auto-Assign Extra Slot" });
        autoAssignExtraSlotBtn.style.flexGrow = 1;
        autoAssignExtraSlotBtn.style.marginRight = 8;
        autoAssignExtraSlotBtn.SetEnabled(false);
        autoAssignExtraSlotBtn.style.minHeight = 48;
        slotterButtonsRow.Add(autoAssignExtraSlotBtn);

        moveSlotBtn = StyleBlueButton(new Button(OnMoveSlotClicked) { text = "Move Slot" });
        moveSlotBtn.style.flexGrow = 1;
        moveSlotBtn.SetEnabled(false);
        moveSlotBtn.style.minHeight = 48;
        slotterButtonsRow.Add(moveSlotBtn);

        slotterTabContent.Add(slotterButtonsRow);

        modal.Add(slotterTabContent);

        overlay.Add(modal);

        return overlay;
    }

    private Button StyleOrangeButton(Button b) => StyleButton(b, ColOrange, ColOrangeEdge, ColOrangeText, ColOrangeHover);
    private Button StyleBlueButton(Button b) => StyleButton(b, ColLabelCell, ColBlueEdge, Color.white, ColBlueHover);
    private Button StyleGrayBlueButton(Button b) => StyleButton(b, ColGrayBlueBg, ColGrayBlueEdge, Color.white, ColGrayBlueHover, ColGrayBlueDisabled);

    private Button StyleButton(Button b, Color bg, Color edge, Color text, Color hover, Color disabledColor = default)
    {
        if (disabledColor == default) disabledColor = new Color(bg.r * 0.7f, bg.g * 0.7f, bg.b * 0.7f, bg.a * 0.7f);
        ApplyFont(b, bold: true, size: 14);
        b.style.backgroundColor = new StyleColor(bg);
        b.style.color = new StyleColor(text);
        b.style.borderBottomWidth = 3;
        b.style.borderBottomColor = new StyleColor(edge);
        b.style.borderTopWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        b.style.paddingTop = 8; b.style.paddingBottom = 8;
        b.style.paddingLeft = 16; b.style.paddingRight = 16;
        b.RegisterCallback<PointerEnterEvent>(_ => {
            if (b.enabledSelf) b.style.backgroundColor = new StyleColor(hover);
        });
        b.RegisterCallback<PointerLeaveEvent>(_ => {
            b.style.backgroundColor = new StyleColor(b.enabledSelf ? bg : disabledColor);
        });
        b.RegisterCallback<ChangeEvent<bool>>(_ => {
            b.style.backgroundColor = new StyleColor(b.enabledSelf ? bg : disabledColor);
        });
        return b;
    }
}
