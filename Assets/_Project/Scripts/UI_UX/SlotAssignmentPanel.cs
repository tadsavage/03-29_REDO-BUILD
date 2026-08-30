using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// "Slotting" panel — lets the player manually assign SKUs to rack Pick slots (a SKU may have
/// several). Bound to the "6" key (see TopBarUI), same programmatic-UIToolkit shape as
/// ShiftManagerPanel (built in the constructor, added into the shared HUD root, toggled via
/// .style.display, dragged via DraggableWindow).
///
/// SCOPE NOTE: this is the data/UI layer only — it feeds SlotRegistry (what slots exist) and
/// SlotAssignmentService (who's assigned where). Actual Putaway task consumption is separate,
/// later work. Assignments here are in-memory only for now (SlotAssignmentService is not
/// persisted to save files yet), same known limitation as ShiftManagerPanel's schedules.
/// </summary>
public class SlotAssignmentPanel
{
    private static readonly Color ColBg         = new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.92f);
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

    private readonly VisualElement _overlay;
    private readonly VisualElement _byLocationTab;
    private readonly VisualElement _bySkuTab;
    private readonly ScrollView _locationScroll;
    private readonly DropdownField _skuDropdown;
    private readonly VisualElement _skuSlotsList;
    private readonly Label _skuEmptyLabel;
    private readonly Button _availablePickslotsButton;

    private PickSlotOverlayController _overlayController;
    private bool _visible;
    private List<SkuData> _skuChoices = new();
    private readonly Dictionary<int, VisualElement> _aisleHeaders = new();
    private Button _scaleBtn;
    private ResizableWindow _resizeWindow;

    public SlotAssignmentPanel(VisualElement root)
    {
        _overlay = Build(out _byLocationTab, out _bySkuTab, out _locationScroll,
            out _skuDropdown, out _skuSlotsList, out _skuEmptyLabel, out _availablePickslotsButton);
        root.Add(_overlay);
        Hide();
    }

    public bool IsVisible => _visible;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
        RebuildLocationView();
        RebuildSkuDropdown();
        RebuildSkuView();
    }

    /// <summary>Opens on the By Location tab, scrolled to the given aisle — used by Shift+Click on
    /// a rack (PlacementStateMachine) so the player lands directly on that rack's slots instead of
    /// scrolling through the whole list. Symmetric with Ctrl+Click opening Pallet Builder scoped to
    /// the clicked pallet.</summary>
    public void ShowForAisle(int aisle)
    {
        Show();
        SelectTab(location: true);
        if (_aisleHeaders.TryGetValue(aisle, out var header))
            _locationScroll.schedule.Execute(() => _locationScroll.ScrollTo(header)).ExecuteLater(16);
    }

    public void Hide()
    {
        _visible = false;
        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
        CancelOverlay();
    }

    public void Dispose()
    {
        CancelOverlay();
        if (_overlay.parent != null) _overlay.RemoveFromHierarchy();
    }

    private void CancelOverlay()
    {
        if (_overlayController != null)
        {
            _overlayController.End();
            _overlayController = null;
        }
    }

    // ── Shell ────────────────────────────────────────────────────────────────
    private VisualElement Build(out VisualElement byLocationTab, out VisualElement bySkuTab,
        out ScrollView locationScroll, out DropdownField skuDropdown, out VisualElement skuSlotsList,
        out Label skuEmptyLabel, out Button availablePickslotsButton)
    {
        var overlay = new VisualElement { name = "slot-assign-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
        overlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
        overlay.style.justifyContent = Justify.FlexStart;
        overlay.style.alignItems = Align.Center;

        var modal = new VisualElement { name = "slot-assign-modal" };
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
        modal.style.paddingTop = 14; modal.style.paddingBottom = 14;
        modal.style.paddingLeft = 16; modal.style.paddingRight = 16;
        modal.style.minWidth = 560;
        modal.style.maxHeight = 640;

        // Title bar
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.marginBottom = 10;

        var titleSpacer = new VisualElement();
        titleSpacer.style.width = 28;
        titleBar.Add(titleSpacer);

        var title = new Label("Slotting");
        ApplyFont(title, bold: true, size: 26);
        title.style.color = new StyleColor(ColTitleText);
        title.style.flexGrow = 1;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(title);

        // Scale button (resize window)
        _scaleBtn = new Button { text = string.Empty };
        RuntimeTooltip.Attach(_scaleBtn, "Resize window (normal / large / fill screen)");
        _scaleBtn.style.width = 28; _scaleBtn.style.height = 28;
        _scaleBtn.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
        _scaleBtn.style.color = new StyleColor(ColSubtleText);
        _scaleBtn.style.marginRight = 6;
        ResizableWindow.AddStackedSquaresGlyph(_scaleBtn, 28f, ColTitleText, isFilled: false);
        _scaleBtn.RegisterCallback<PointerEnterEvent>(_ =>
            _scaleBtn.style.backgroundColor = new StyleColor(new Color(0.35f, 0.55f, 0.95f, 0.35f)));
        _scaleBtn.RegisterCallback<PointerLeaveEvent>(_ =>
            _scaleBtn.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f)));
        titleBar.Add(_scaleBtn);

        var closeButton = new Button(Hide) { text = "✕" };
        closeButton.style.width = 28; closeButton.style.height = 28;
        closeButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
        closeButton.style.color = new StyleColor(ColSubtleText);
        closeButton.RegisterCallback<PointerEnterEvent>(_ =>
        {
            closeButton.style.backgroundColor = new StyleColor(new Color(0.8f, 0.3f, 0.2f, 1f));
            closeButton.style.color = new StyleColor(Color.white);
        });
        closeButton.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            closeButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.06f));
            closeButton.style.color = new StyleColor(ColSubtleText);
        });
        titleBar.Add(closeButton);
        modal.Add(titleBar);

        new DraggableWindow(modal, titleBar, closeButton);
        _resizeWindow = new ResizableWindow(modal, minW: 560f, minH: 300f, grip: 8f, titleInset: 40f);
        _scaleBtn.clicked += () =>
        {
            _resizeWindow.CycleScale();
            _resizeWindow.UpdateScaleButtonIcon(_scaleBtn, 28f, ColTitleText);
        };

        // Tab row
        var tabRow = new VisualElement();
        tabRow.style.flexDirection = FlexDirection.Row;
        tabRow.style.marginBottom = 10;

        var byLocationBtn = StyleBlueButton(new Button(() => SelectTab(true)) { text = "By Location" });
        byLocationBtn.style.marginRight = 8;
        var bySkuBtn = StyleBlueButton(new Button(() => SelectTab(false)) { text = "By SKU" });
        tabRow.Add(byLocationBtn);
        tabRow.Add(bySkuBtn);
        modal.Add(tabRow);

        // By Location tab content
        byLocationTab = new VisualElement();
        byLocationTab.style.flexGrow = 1;

        locationScroll = new ScrollView();
        locationScroll.style.maxHeight = 460;
        byLocationTab.Add(locationScroll);
        modal.Add(byLocationTab);

        // By SKU tab content
        bySkuTab = new VisualElement();
        bySkuTab.style.display = DisplayStyle.None;

        skuDropdown = new DropdownField(new List<string> { "— no SKUs loaded —" }, 0);
        ApplyFont(skuDropdown, size: 15);
        skuDropdown.RegisterValueChangedCallback(_ => RebuildSkuView());
        bySkuTab.Add(skuDropdown);

        skuSlotsList = new VisualElement();
        skuSlotsList.style.marginTop = 8;
        bySkuTab.Add(skuSlotsList);

        skuEmptyLabel = new Label("No pick slots assigned to this SKU yet.");
        ApplyFont(skuEmptyLabel, size: 13);
        skuEmptyLabel.style.color = new StyleColor(ColSubtleText);
        skuEmptyLabel.style.marginTop = 4;
        bySkuTab.Add(skuEmptyLabel);

        availablePickslotsButton = StyleOrangeButton(new Button(OnAvailablePickslotsClicked) { text = "Available Pickslots" });
        availablePickslotsButton.style.marginTop = 12;
        bySkuTab.Add(availablePickslotsButton);

        modal.Add(bySkuTab);

        overlay.Add(modal);

        _tabByLocationBtn = byLocationBtn;
        _tabBySkuBtn = bySkuBtn;

        return overlay;
    }

    private Button _tabByLocationBtn;
    private Button _tabBySkuBtn;

    private void SelectTab(bool location)
    {
        CancelOverlay();
        _byLocationTab.style.display = location ? DisplayStyle.Flex : DisplayStyle.None;
        _bySkuTab.style.display = location ? DisplayStyle.None : DisplayStyle.Flex;
        if (location) RebuildLocationView(); else RebuildSkuView();
    }

    private Button StyleOrangeButton(Button b) => StyleButton(b, ColOrange, ColOrangeEdge, ColOrangeText, ColOrangeHover);
    private Button StyleBlueButton(Button b) => StyleButton(b, ColLabelCell, ColBlueEdge, Color.white, ColBlueHover);

    private Button StyleButton(Button b, Color bg, Color edge, Color text, Color hover)
    {
        ApplyFont(b, bold: true, size: 14);
        b.style.backgroundColor = new StyleColor(bg);
        b.style.color = new StyleColor(text);
        b.style.borderBottomWidth = 3;
        b.style.borderBottomColor = new StyleColor(edge);
        b.style.borderTopWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
        b.style.borderTopLeftRadius = b.style.borderTopRightRadius =
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 8;
        b.style.paddingTop = 5; b.style.paddingBottom = 5;
        b.style.paddingLeft = 12; b.style.paddingRight = 12;
        b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = new StyleColor(hover));
        b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(bg));
        return b;
    }

    // ── By Location ──────────────────────────────────────────────────────────
    private void RebuildLocationView()
    {
        PruneOrphanedAssignments();

        _locationScroll.Clear();
        _aisleHeaders.Clear();

        var byAisle = SlotRegistry.PickSlots
            .OrderBy(s => s.Aisle).ThenBy(s => s.Bay).ThenBy(s => s.Position)
            .GroupBy(s => s.Aisle);

        int rowIndex = 0;
        foreach (var aisleGroup in byAisle.OrderBy(g => g.Key))
        {
            var header = new Label($"Aisle {aisleGroup.Key:00}");
            ApplyFont(header, bold: true, size: 15);
            header.style.color = new StyleColor(ColOrangeText);
            header.style.backgroundColor = new StyleColor(ColOrange);
            header.style.paddingLeft = 6; header.style.paddingTop = 3; header.style.paddingBottom = 3;
            header.style.marginTop = 6;
            _locationScroll.Add(header);
            _aisleHeaders[aisleGroup.Key] = header;

            foreach (var slot in aisleGroup)
            {
                _locationScroll.Add(BuildLocationRow(slot, rowIndex));
                rowIndex++;
            }
        }

        if (rowIndex == 0)
        {
            var empty = new Label("No committed racks found yet — build an aisle to see Pick slots here.");
            ApplyFont(empty, size: 13);
            empty.style.color = new StyleColor(ColSubtleText);
            _locationScroll.Add(empty);
        }
    }

    private VisualElement BuildLocationRow(SlotRegistry.Slot slot, int rowIndex)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingTop = 3; row.style.paddingBottom = 3; row.style.paddingLeft = 6;
        row.style.backgroundColor = new StyleColor(rowIndex % 2 == 0 ? ColRowEven : ColRowOdd);

        var addrLabel = new Label(slot.Address);
        ApplyFont(addrLabel, bold: true, size: 13);
        addrLabel.style.width = 110;
        addrLabel.style.color = new StyleColor(ColTitleText);
        row.Add(addrLabel);

        string assignedSkuId = SlotAssignmentService.GetSku(slot.Address);
        var skuLabel = new Label(SkuDisplayName(assignedSkuId));
        ApplyFont(skuLabel, size: 13);
        skuLabel.style.width = 180;
        skuLabel.style.color = new StyleColor(assignedSkuId == null ? ColSubtleText : ColTitleText);
        row.Add(skuLabel);

        var choices = new List<string> { "— none —" };
        choices.AddRange(_skuChoices.Select(s => s.ItemDescription));
        int selectedIndex = assignedSkuId == null ? 0
            : Math.Max(0, _skuChoices.FindIndex(s => s.SkuId == assignedSkuId) + 1);

        var dropdown = new DropdownField(choices, selectedIndex);
        ApplyFont(dropdown, size: 12);
        dropdown.style.width = 180;
        dropdown.RegisterValueChangedCallback(evt =>
        {
            int idx = choices.IndexOf(evt.newValue);
            if (idx <= 0) SlotAssignmentService.Clear(slot.Address);
            else SlotAssignmentService.Assign(slot.Address, _skuChoices[idx - 1].SkuId);
            RebuildLocationView();
        });
        row.Add(dropdown);

        var clearButton = new Button(() => { SlotAssignmentService.Clear(slot.Address); RebuildLocationView(); }) { text = "Clear" };
        ApplyFont(clearButton, size: 11);
        clearButton.style.marginLeft = 6;
        clearButton.SetEnabled(assignedSkuId != null);
        row.Add(clearButton);

        return row;
    }

    // ── By SKU ───────────────────────────────────────────────────────────────
    private void RebuildSkuDropdown()
    {
        InventoryService inv = null;
        GameCore.Services.ServiceLocator.TryGet(out inv);
        _skuChoices = inv != null ? inv.AllSkus.Where(s => s != null).OrderBy(s => s.ItemDescription).ToList() : new List<SkuData>();

        var choices = _skuChoices.Count > 0 ? _skuChoices.Select(s => s.ItemDescription).ToList() : new List<string> { "— no SKUs loaded —" };
        int prevIndex = choices.IndexOf(_skuDropdown.value);
        _skuDropdown.choices = choices;
        _skuDropdown.SetValueWithoutNotify(choices[Mathf.Max(0, prevIndex)]);
    }

    private SkuData SelectedSku()
    {
        int idx = _skuChoices.FindIndex(s => s.ItemDescription == _skuDropdown.value);
        return idx >= 0 ? _skuChoices[idx] : null;
    }

    private void RebuildSkuView()
    {
        PruneOrphanedAssignments();
        _skuSlotsList.Clear();

        var sku = SelectedSku();
        _availablePickslotsButton.SetEnabled(sku != null);
        if (sku == null)
        {
            _skuEmptyLabel.style.display = DisplayStyle.Flex;
            return;
        }

        var addresses = SlotAssignmentService.GetSlotsForSku(sku.SkuId).OrderBy(a => a).ToList();
        _skuEmptyLabel.style.display = addresses.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

        for (int i = 0; i < addresses.Count; i++)
        {
            string address = addresses[i];
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 3; row.style.paddingBottom = 3; row.style.paddingLeft = 6;
            row.style.backgroundColor = new StyleColor(i % 2 == 0 ? ColRowEven : ColRowOdd);

            var addrLabel = new Label(address);
            ApplyFont(addrLabel, bold: true, size: 13);
            addrLabel.style.width = 140;
            addrLabel.style.color = new StyleColor(ColTitleText);
            row.Add(addrLabel);

            var unassignButton = new Button(() => { SlotAssignmentService.Clear(address); RebuildSkuView(); }) { text = "Unassign" };
            ApplyFont(unassignButton, size: 11);
            row.Add(unassignButton);

            _skuSlotsList.Add(row);
        }
    }

    private void OnAvailablePickslotsClicked()
    {
        var sku = SelectedSku();
        if (sku == null) return;

        CancelOverlay();

        var go = new GameObject("[PickSlotOverlayController]");
        _overlayController = go.AddComponent<PickSlotOverlayController>();
        _overlayController.Begin(sku, address =>
        {
            SlotAssignmentService.Assign(address, sku.SkuId);
            _overlayController = null;
            RebuildSkuView();
            if (_byLocationTab.style.display == DisplayStyle.Flex) RebuildLocationView();
        });
    }

    // Drops any assignment whose address SlotRegistry no longer reports (its rack was deleted),
    // so the "By SKU" view never shows a phantom slot that doesn't exist anymore.
    private void PruneOrphanedAssignments()
    {
        var stale = SlotAssignmentService.AllAssignments.Keys
            .Where(addr => !SlotRegistry.TryGet(addr, out _))
            .ToList();
        foreach (var addr in stale) SlotAssignmentService.Clear(addr);
    }

    private string SkuDisplayName(string skuId)
    {
        if (skuId == null) return "— unassigned —";
        var sku = _skuChoices.FirstOrDefault(s => s.SkuId == skuId);
        return sku != null ? sku.ItemDescription : skuId;
    }
}
