using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// "New Item" panel — assign pick slots to items that have been received on the dock
/// but don't yet have pick slots assigned. Bound to the "8" key.
/// Works through a cascading Aisle → Bay → Position dropdown system. When a slot is selected,
/// the camera moves to show it and can be assigned.
/// </summary>
public class NewItemPanel
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
    private static readonly Color ColDisabled   = new Color(0x4A / 255f, 0x5A / 255f, 0x6A / 255f, 0.5f);

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
    private readonly ScrollView _itemsScroll;
    private readonly DropdownField _aisleDropdown;
    private readonly DropdownField _bayDropdown;
    private readonly DropdownField _positionDropdown;
    private readonly Button _assignButton;
    private readonly Button _goToPickButton;

    private bool _visible;
    private PalletMasterRecord _selectedItem;
    private string _selectedAisle;
    private string _selectedBay;
    private string _selectedPosition;

    private List<PalletMasterRecord> _itemsNeedingSlots = new();
    private List<SkuData> _skuChoices = new();

    public NewItemPanel(VisualElement root)
    {
        _overlay = Build(out _itemsScroll, out _aisleDropdown, out _bayDropdown,
            out _positionDropdown, out _assignButton, out _goToPickButton);
        root.Add(_overlay);
        Hide();
    }

    public bool IsVisible => _visible;
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
            RefreshItemsNeedingSlots();
            RebuildItemsList();
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

    private void RefreshItemsNeedingSlots()
    {
        InventoryService inv = null;
        GameCore.Services.ServiceLocator.TryGet(out inv);

        if (inv == null)
        {
            _itemsNeedingSlots.Clear();
            _skuChoices.Clear();
            return;
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

    private VisualElement BuildItemButton(PalletMasterRecord pallet, int index)
    {
        var button = new Button();
        button.style.flexDirection = FlexDirection.Column;
        button.style.alignItems = Align.FlexStart;
        button.style.paddingTop = 8;
        button.style.paddingBottom = 8;
        button.style.paddingLeft = 12;
        button.style.paddingRight = 12;
        button.style.marginBottom = 4;
        button.style.backgroundColor = new StyleColor(index % 2 == 0 ? ColRowEven : ColRowOdd);
        button.style.borderBottomWidth = 1;
        button.style.borderBottomColor = new StyleColor(ColBorder);

        // Get SKU data for this pallet
        var sku = _skuChoices.FirstOrDefault(s => s.SkuId == pallet.SkuId);

        // Item number and description
        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.justifyContent = Justify.SpaceBetween;
        headerRow.style.width = Length.Percent(100);
        headerRow.style.marginBottom = 6;

        var itemInfo = new VisualElement();
        itemInfo.style.flexDirection = FlexDirection.Column;
        itemInfo.style.flexGrow = 1;

        var itemLabel = new Label(sku?.ItemDescription ?? pallet.SkuId);
        ApplyFont(itemLabel, bold: true, size: 13);
        itemLabel.style.color = new StyleColor(ColTitleText);
        itemInfo.Add(itemLabel);

        var itemNum = new Label($"Item: {pallet.SkuId}");
        ApplyFont(itemNum, size: 11);
        itemNum.style.color = new StyleColor(ColSubtleText);
        itemInfo.Add(itemNum);

        headerRow.Add(itemInfo);

        var palletCount = new Label($"{pallet.Quantity} cases");
        ApplyFont(palletCount, size: 11);
        palletCount.style.color = new StyleColor(ColSubtleText);
        headerRow.Add(palletCount);

        button.Add(headerRow);

        // Details row
        var detailsRow = new VisualElement();
        detailsRow.style.flexDirection = FlexDirection.Row;
        detailsRow.style.justifyContent = Justify.SpaceBetween;
        detailsRow.style.width = Length.Percent(100);
        detailsRow.style.marginBottom = 6;

        var palletHeight = sku != null ? $"{sku.PltHeight:F2}m" : "—";
        var caseWeight = sku?.CaseWeight > 0 ? $"{sku.CaseWeight}lbs" : "—";
        var area = sku?.StorageArea.ToString() ?? "—";

        var detailLabel1 = new Label($"Pallet Height: {palletHeight}");
        ApplyFont(detailLabel1, size: 11);
        detailLabel1.style.color = new StyleColor(ColSubtleText);
        detailsRow.Add(detailLabel1);

        var detailLabel2 = new Label($"Case Weight: {caseWeight}");
        ApplyFont(detailLabel2, size: 11);
        detailLabel2.style.color = new StyleColor(ColSubtleText);
        detailsRow.Add(detailLabel2);

        var detailLabel3 = new Label($"Area: {area}");
        ApplyFont(detailLabel3, size: 11);
        detailLabel3.style.color = new StyleColor(ColSubtleText);
        detailsRow.Add(detailLabel3);

        button.Add(detailsRow);

        // Selection handler
        button.RegisterCallback<ClickEvent>(_ =>
        {
            _selectedItem = pallet;
            ResetDropdowns();
            RebuildItemsList();  // Refresh visual state
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

        _aisleDropdown.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue == "— select aisle —")
            {
                _selectedAisle = null;
            }
            else
            {
                _selectedAisle = evt.newValue;
            }
            _selectedBay = null;
            _selectedPosition = null;
            RebuildBayDropdown();
            RebuildPositionDropdown();
            UpdateButtonStates();
        });
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

        _bayDropdown.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue == "— select bay —")
            {
                _selectedBay = null;
            }
            else
            {
                _selectedBay = evt.newValue;
            }
            _selectedPosition = null;
            RebuildPositionDropdown();
            UpdateButtonStates();
        });
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

        _positionDropdown.RegisterValueChangedCallback(evt =>
        {
            _selectedPosition = evt.newValue == "— select position —" ? null : evt.newValue;
            UpdateButtonStates();
        });
    }

    private void UpdateButtonStates()
    {
        bool allSelected = _selectedItem != null
            && !string.IsNullOrEmpty(_selectedAisle) && _selectedAisle != "— select aisle —"
            && !string.IsNullOrEmpty(_selectedBay) && _selectedBay != "— select bay —"
            && !string.IsNullOrEmpty(_selectedPosition) && _selectedPosition != "— select position —";

        _assignButton.SetEnabled(allSelected);
        _goToPickButton.SetEnabled(allSelected);
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

        // Find the rack and position camera
        PositionCameraAtSlot(slot);
    }

    private void PositionCameraAtSlot(SlotRegistry.Slot slot)
    {
        var camera = Camera.main;
        if (camera == null) return;

        // Find the rack in the scene
        var racks = UnityEngine.Object.FindObjectsByType<PlacedObject>()
            .Where(p => p.data != null && p.data.category == "Racking")
            .ToList();

        PlacedObject targetRack = null;
        foreach (var rack in racks)
        {
            var label = rack.GetComponentInChildren<TMPro.TextMeshPro>();
            if (label != null && label.text == slot.Address)
            {
                targetRack = rack;
                break;
            }
        }

        if (targetRack == null) return;

        var targetPos = targetRack.transform.position;
        targetPos.y += 2f;  // Offset above the rack

        // Determine camera facing direction based on rack forward
        var facing = targetRack.transform.forward;
        var cameraDir = -facing;  // Face opposite direction

        camera.transform.position = targetPos;
        camera.transform.LookAt(targetRack.transform.position + Vector3.up * 1.5f);
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
    private VisualElement Build(out ScrollView itemsScroll, out DropdownField aisleDropdown,
        out DropdownField bayDropdown, out DropdownField positionDropdown,
        out Button assignButton, out Button goToPickButton)
    {
        var overlay = new VisualElement { name = "new-item-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0; overlay.style.top = 0; overlay.style.right = 0; overlay.style.bottom = 0;
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
        modal.style.paddingTop = 14; modal.style.paddingBottom = 14;
        modal.style.paddingLeft = 16; modal.style.paddingRight = 16;
        modal.style.minWidth = 680;
        modal.style.maxHeight = 800;

        // Title bar
        var titleBar = new VisualElement();
        titleBar.style.flexDirection = FlexDirection.Row;
        titleBar.style.alignItems = Align.Center;
        titleBar.style.marginBottom = 14;

        var titleSpacer = new VisualElement();
        titleSpacer.style.width = 28;
        titleBar.Add(titleSpacer);

        var title = new Label("New Item");
        ApplyFont(title, bold: true, size: 28);
        title.style.color = new StyleColor(ColTitleText);
        title.style.flexGrow = 1;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleBar.Add(title);

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

        // Current items section
        var currentLabel = new Label("Items requiring a pick slot:");
        ApplyFont(currentLabel, bold: true, size: 14);
        currentLabel.style.color = new StyleColor(ColOrangeText);
        currentLabel.style.marginBottom = 8;
        modal.Add(currentLabel);

        itemsScroll = new ScrollView();
        itemsScroll.style.maxHeight = 280;
        itemsScroll.style.marginBottom = 16;
        itemsScroll.style.borderBottomWidth = 1;
        itemsScroll.style.borderBottomColor = new StyleColor(ColBorder);
        itemsScroll.style.paddingBottom = 12;
        modal.Add(itemsScroll);

        // Slot Assignment section
        var slotLabel = new Label("Slot Assignment");
        ApplyFont(slotLabel, bold: true, size: 14);
        slotLabel.style.color = new StyleColor(ColTitleText);
        slotLabel.style.marginBottom = 10;
        modal.Add(slotLabel);

        // Dropdown row
        var dropdownRow = new VisualElement();
        dropdownRow.style.flexDirection = FlexDirection.Row;
        dropdownRow.style.justifyContent = Justify.SpaceBetween;
        dropdownRow.style.marginBottom = 12;

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
        posContainer.Add(positionDropdown);
        dropdownRow.Add(posContainer);

        modal.Add(dropdownRow);

        // Buttons row
        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.justifyContent = Justify.SpaceEvenly;

        assignButton = StyleOrangeButton(new Button(OnAssignClicked) { text = "Assign Pick Slot" });
        assignButton.style.flexGrow = 1;
        assignButton.style.marginRight = 12;
        assignButton.SetEnabled(false);
        buttonRow.Add(assignButton);

        goToPickButton = StyleBlueButton(new Button(OnGoToPickClicked) { text = "Go to Pick" });
        goToPickButton.style.flexGrow = 1;
        goToPickButton.SetEnabled(false);
        buttonRow.Add(goToPickButton);

        modal.Add(buttonRow);

        overlay.Add(modal);

        return overlay;
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
        b.style.paddingTop = 8; b.style.paddingBottom = 8;
        b.style.paddingLeft = 16; b.style.paddingRight = 16;
        b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = new StyleColor(hover));
        b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = new StyleColor(bg));
        return b;
    }
}
