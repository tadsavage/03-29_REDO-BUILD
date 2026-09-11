using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using GameCore.Inventory;
using GameCore.Services;
using System.Linq;
using System.Collections.Generic;

public class WorldHoverPopupUI : MonoBehaviour
{
    private VisualElement _root;
    private VisualElement _popup;
    private Label _title;
    private Label _cost;
    private Label _hourlyCost;
    private Image _iconImage; // For pallet icons
    private VisualElement _palletInfoPanel;
    private Vector2 _smoothPos;
    private PlacementStateMachine _fsm;

    // ── Truck hover tooltip ─────────────────────────────────────────────────
    // Built lazily (needs _root, which only exists once _popup is live in a panel) as a sibling of
    // _popup under the same full-screen root, reusing the ".world-hover-popup" USS class for its
    // base absolute-position/background/radius so it follows the cursor exactly like _popup does.
    private VisualElement _truckPopup;
    private VisualElement _truckCommentFill;
    private Label _truckCommentLabel;
    private Label _truckVendorLabel;
    private Label _truckOrderLabel;
    private Label _truckApptLabel;
    private VisualElement _truckDoorBadge;
    private Label _truckDoorLabel;
    private VisualElement _truckLoadFill;
    private Label _truckLoadPercentLabel;
    private VisualElement _truckItemsList;
    private TruckController _pendingTruck;
    private bool _isTruckMode;

    // Order-type colors — deliberately match the Scheduler's own legend (Recurring/Bulk/Inbound PO)
    // so the tooltip's frame "corresponds to the order types on the scheduler" per Tad's spec. Kept
    // as a bright/saturated trio here rather than SchedulerPanel's muted chip-edge colors, since this
    // border has to read as a "thick colored line" at a glance, not blend into a dark chip.
    private static readonly Color TruckRecurringColor = new Color(0x4A / 255f, 0x90 / 255f, 0xD9 / 255f, 1f); // blue
    private static readonly Color TruckBulkColor      = new Color(0x4C / 255f, 0xAF / 255f, 0x50 / 255f, 1f); // green
    private static readonly Color TruckInboundColor   = new Color(0xE6 / 255f, 0x7E / 255f, 0x22 / 255f, 1f); // orange
    // "Make the fill bar the same color as the receiving progress bar" — ReceivingFillBarCanvas's
    // fill sprite is a blue button graphic (button_blue_default_2), so this matches that family.
    private static readonly Color TruckLoadFillColor  = new Color(0x4A / 255f, 0x90 / 255f, 0xD9 / 255f, 1f);

    // Hover timing
    private float _hoverDelay    = 0.1f;
    private float _hoverTimer    = 0f;
    private bool  _isHovering    = false;
    private bool  _isVisible     = false;

    // Pending data (building)
    private string _pendingName;
    private int    _pendingCost;
    private int    _pendingHourlyCost;

    // Pending data (pallet)
    private PalletData _pendingPalletData;
    private bool _isPalletMode;

    // Pending data (location)
    private LocationData _pendingLocationData;
    private bool _isLocationMode;

    // Toggle — can be turned off from Dev Settings
    public bool IsEnabled { get; private set; } = true;

    public void SetEnabled(bool val)
    {
        IsEnabled = val;
        if (!val) HideImmediate();
    }

    // ---------------------------------------------------------
    // INITIALIZATION
    // ---------------------------------------------------------
    public void Init(VisualElement populationTarget)
    {
        if (populationTarget == null)
        {
            Debug.LogError("[WorldHoverPopupUI] Null VisualElement reference!");
            return;
        }

        _popup = populationTarget;
        _root  = _popup.panel?.visualTree;
        _title      = _popup.Q<Label>("HoverTitle");
        _cost       = _popup.Q<Label>("HoverCost");
        _hourlyCost = _popup.Q<Label>("HoverHourlyCost");
        _iconImage  = _popup.Q<Image>("HoverIcon");
        _palletInfoPanel = _popup.Q<VisualElement>("PalletInfoPanel");

        // -100%/-100% translate shifts the box up-left by its OWN final size, so (left, top) becomes
        // its bottom-right corner instead of its top-left -- percent translate resolves against the
        // element's own layout size automatically (no need to ever read resolvedStyle.width/height,
        // which is stale/zero for a frame after Show() and produced wildly wrong positions when tried
        // that way). Set once here since it's a constant; FollowCursor only ever drives left/top.
        _popup.style.translate = new Translate(Length.Percent(-100f), Length.Percent(-100f), 0f);

        _popup.pickingMode = PickingMode.Ignore;
        if (_title      != null) _title.pickingMode      = PickingMode.Ignore;
        if (_cost       != null) _cost.pickingMode       = PickingMode.Ignore;
        if (_hourlyCost != null) _hourlyCost.pickingMode = PickingMode.Ignore;
        if (_iconImage  != null) _iconImage.pickingMode  = PickingMode.Ignore;
        if (_palletInfoPanel != null) _palletInfoPanel.pickingMode = PickingMode.Ignore;

        HideImmediate();
    }

    public void SetFSM(PlacementStateMachine fsm) => _fsm = fsm;

    // ---------------------------------------------------------
    // MAIN UPDATE - Building/Object hover
    // ---------------------------------------------------------
    public void TickHover(bool hovering, string name, int cost, int hourlyCost,
                          Vector3 worldPos, Camera cam)
    {
        if (!IsEnabled) { HideImmediate(); return; }

        // Only show in IdleState
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        { HideImmediate(); return; }

        if (!hovering || string.IsNullOrEmpty(name))
        {
            _isHovering = false;
            _hoverTimer = 0f;
            HideImmediate();
            return;
        }

        bool isNewTarget = !_isHovering || _isPalletMode
            || name != _pendingName
            || cost != _pendingCost
            || hourlyCost != _pendingHourlyCost;

        _pendingName      = name;
        _pendingCost      = cost;
        _pendingHourlyCost = hourlyCost;
        _pendingPalletData = null;
        _pendingLocationData = null;
        _isPalletMode = false;
        _isLocationMode = false;

        if (isNewTarget)
        {
            _isHovering = true;
            _hoverTimer = 0f;
            _popup.style.opacity = 1f;
            ShowBuilding(_pendingName, _pendingCost, _pendingHourlyCost);
        }
        else
        {
            _hoverTimer += Time.unscaledDeltaTime;
            if (!_isVisible && _hoverTimer >= _hoverDelay)
                ShowBuilding(_pendingName, _pendingCost, _pendingHourlyCost);
        }

        if (_isVisible)
            FollowCursor();
    }

    // ---------------------------------------------------------
    // PALLET HOVER
    // ---------------------------------------------------------
    public void TickHoverPallet(bool hovering, PalletData palletData, Vector3 worldPos, Camera cam)
    {
        if (!IsEnabled) { HideImmediate(); return; }

        // Only show in IdleState
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        { HideImmediate(); return; }

        if (!hovering || palletData == null)
        {
            _isHovering = false;
            _hoverTimer = 0f;
            HideImmediate();
            return;
        }

        bool isNewTarget = !_isHovering || !_isPalletMode || palletData != _pendingPalletData;

        _pendingPalletData = palletData;
        _pendingName = null;
        _pendingCost = 0;
        _pendingHourlyCost = 0;
        _pendingLocationData = null;
        _isPalletMode = true;
        _isLocationMode = false;

        if (isNewTarget)
        {
            _isHovering = true;
            _hoverTimer = 0f;
            _popup.style.opacity = 1f;
            ShowPallet(_pendingPalletData);
        }
        else
        {
            _hoverTimer += Time.unscaledDeltaTime;
            if (!_isVisible && _hoverTimer >= _hoverDelay)
                ShowPallet(_pendingPalletData);
        }

        if (_isVisible)
            FollowCursor();
    }

    // ---------------------------------------------------------
    // LOCATION HOVER
    // ---------------------------------------------------------
    public void TickHoverLocation(bool hovering, LocationData locationData, Vector3 worldPos, Camera cam)
    {
        if (!IsEnabled) { HideImmediate(); return; }

        // Only show in IdleState
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        { HideImmediate(); return; }

        if (!hovering || locationData == null)
        {
            _isHovering = false;
            _hoverTimer = 0f;
            HideImmediate();
            return;
        }

        bool isNewTarget = !_isHovering || !_isLocationMode || locationData != _pendingLocationData;

        _pendingLocationData = locationData;
        _pendingPalletData = null;
        _pendingName = null;
        _pendingCost = 0;
        _pendingHourlyCost = 0;
        _isPalletMode = false;
        _isLocationMode = true;

        if (isNewTarget)
        {
            _isHovering = true;
            _hoverTimer = 0f;
            _popup.style.opacity = 1f;
            ShowLocation(_pendingLocationData);
        }
        else
        {
            _hoverTimer += Time.unscaledDeltaTime;
            if (!_isVisible && _hoverTimer >= _hoverDelay)
                ShowLocation(_pendingLocationData);
        }

        if (_isVisible)
            FollowCursor();
    }

    // ---------------------------------------------------------
    // VISUALS - Building
    // ---------------------------------------------------------
private void ShowBuilding(string name, int cost, int hourlyCost)
    {
        if (_popup == null || _title == null || _cost == null || _hourlyCost == null) return;

        _isTruckMode = false;
        HideTruckPopup();

        _title.text      = name;
        _cost.text       = $"Purchase Cost\n${cost:N0}";
        _hourlyCost.text = $"Hourly Cost\n${hourlyCost:N0}/hr";

        // Re-show cost/hourly-cost -- ShowPallet/ShowLocation hide these same Label elements,
        // and without resetting display here they'd stay hidden forever after the first
        // pallet or location hover (only the title would ever show again).
        _cost.style.display = DisplayStyle.Flex;
        _hourlyCost.style.display = DisplayStyle.Flex;

        // Hide pallet-specific elements
        if (_iconImage != null) _iconImage.style.display = DisplayStyle.None;
        if (_palletInfoPanel != null) _palletInfoPanel.style.display = DisplayStyle.None;

        _popup.style.opacity = 1f;
        _popup.style.display = DisplayStyle.Flex;
        _popup.AddToClassList("show");
        _isVisible = true;
    }

    // ---------------------------------------------------------
    // VISUALS - Pallet
    // ---------------------------------------------------------
    private void ShowPallet(PalletData pallet)
    {
        if (_popup == null || pallet == null) return;

        _isTruckMode = false;
        HideTruckPopup();

        // Get SKU data for additional info
        InventoryService inventory = null;
        ServiceLocator.TryGet<InventoryService>(out inventory);
        var sku = inventory?.GetSkuData(pallet.ItemNumber);

        // Set title to item description
        if (_title != null)
            _title.text = sku?.ItemDescription ?? pallet.ItemNumber;

        // Set icon (postage stamp, upper-right corner)
        if (_iconImage != null)
        {
            _iconImage.sprite = pallet.IconSprite;
            _iconImage.style.display = pallet.IconSprite != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Build pallet info text
        if (_palletInfoPanel != null)
        {
            _palletInfoPanel.Clear();
            _palletInfoPanel.style.display = DisplayStyle.Flex;

            // Wholesale cost
            if (sku != null)
            {
                // Item Number
                var itemNumLbl = new Label($"Item #: {sku.ItemNumber}");
                itemNumLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                itemNumLbl.style.fontSize = 12;
                _palletInfoPanel.Add(itemNumLbl);

                // Case Quantity
                var caseQtyLbl = new Label($"Cases: {pallet.CaseQuantity}");
                caseQtyLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                caseQtyLbl.style.fontSize = 12;
                _palletInfoPanel.Add(caseQtyLbl);

                var wholesaleLbl = new Label($"Wholesale: ${sku.BuyValue:N0}");
                wholesaleLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                wholesaleLbl.style.fontSize = 12;
                _palletInfoPanel.Add(wholesaleLbl);

                // Retail cost
                var retailLbl = new Label($"Retail: ${sku.SellValue:N0}");
                retailLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                retailLbl.style.fontSize = 12;
                _palletInfoPanel.Add(retailLbl);

                // Ti x Hi
                var tiHiLbl = new Label($"Ti×Hi: {sku.Ti}×{sku.Hi}");
                tiHiLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                tiHiLbl.style.fontSize = 12;
                _palletInfoPanel.Add(tiHiLbl);

                // Pallet height
                float palletHeight = sku.PltHeight;
                var heightLbl = new Label($"Height: {palletHeight:F2}m");
                heightLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                heightLbl.style.fontSize = 12;
                _palletInfoPanel.Add(heightLbl);
            }

            // Pick slot assignment (check if SKU has a slot assigned globally)
            var pickSlotStr = GetPickSlotForSku(pallet.ItemNumber);
            var pickLbl = new Label($"Pick Slot: {pickSlotStr}");
            pickLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            pickLbl.style.fontSize = 12;
            _palletInfoPanel.Add(pickLbl);

            // Load ID
            var loadIdStr = string.IsNullOrEmpty(pallet.LoadId) ? "N/A" : pallet.LoadId;
            var loadIdLbl = new Label($"Load ID: {loadIdStr}");
            loadIdLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            loadIdLbl.style.fontSize = 12;
            _palletInfoPanel.Add(loadIdLbl);
        }

        // Hide building-specific elements
        if (_cost != null) _cost.style.display = DisplayStyle.None;
        if (_hourlyCost != null) _hourlyCost.style.display = DisplayStyle.None;

        _popup.style.opacity = 1f;
        _popup.style.display = DisplayStyle.Flex;
        _popup.AddToClassList("show");
        _isVisible = true;
    }

    // ---------------------------------------------------------
    // VISUALS - Location
    // ---------------------------------------------------------
private void ShowLocation(LocationData location)
    {
        if (_popup == null || location == null) return;

        _isTruckMode = false;
        HideTruckPopup();

        // Title: the slot address itself, e.g. "01-12-B0".
        if (_title != null)
            _title.text = location.Address;

        if (_iconImage != null) _iconImage.style.display = DisplayStyle.None;

        if (_palletInfoPanel != null)
        {
            _palletInfoPanel.Clear();
            _palletInfoPanel.style.display = DisplayStyle.Flex;

            bool isPick = location.Type == LocationType.Pick;

            var typeLbl = new Label($"Type: {(isPick ? "Pickslot" : "Reserve")}");
            typeLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            typeLbl.style.fontSize = 12;
            _palletInfoPanel.Add(typeLbl);

            int heightInches = Mathf.RoundToInt(location.WorldPosition.y * 39.3701f);
            var heightLbl = new Label($"Height: {heightInches}\"");
            heightLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            heightLbl.style.fontSize = 12;
            _palletInfoPanel.Add(heightLbl);

            if (isPick)
            {
                // Pick slots report assignment state, not the reserve-style physical status --
                // "Assigned" means a SKU has been designated to this pick face (SlotAssignmentService),
                // regardless of whether a pallet currently sits on it.
                bool assigned = SlotAssignmentService.TryGetSku(location.Address, out string sku) && !string.IsNullOrEmpty(sku);

                var statusLbl = new Label($"Status: {(assigned ? "Assigned" : "Available")}");
                statusLbl.style.color = assigned ? Color.yellow : new Color(0.8f, 0.9f, 1f, 1f);
                statusLbl.style.fontSize = 12;
                _palletInfoPanel.Add(statusLbl);

                if (assigned)
                {
                    ServiceLocator.TryGet<InventoryService>(out var inventory);
                    var skuData = inventory?.GetSkuData(sku);
                    string desc = skuData != null ? skuData.ItemDescription : "";
                    var itemLbl = new Label(string.IsNullOrEmpty(desc) ? sku : $"{sku} {desc}");
                    itemLbl.style.color = Color.white;
                    itemLbl.style.fontSize = 12;
                    _palletInfoPanel.Add(itemLbl);
                }
            }
            else
            {
                string statusText;
                switch (location.Status)
                {
                    case LocationStatus.QAHold:   statusText = "On Hold";  break;
                    case LocationStatus.Reserved: statusText = "Reserved"; break;
                    case LocationStatus.Available: statusText = "Available"; break;
                    case LocationStatus.Occupied:  statusText = "Occupied";  break;
                    case LocationStatus.Problem:   statusText = "Problem";   break;
                    default: statusText = location.Status.ToString(); break;
                }

                var statusLbl = new Label($"Status: {statusText}");
                statusLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                statusLbl.style.fontSize = 12;
                _palletInfoPanel.Add(statusLbl);
            }
        }

        if (_cost != null) _cost.style.display = DisplayStyle.None;
        if (_hourlyCost != null) _hourlyCost.style.display = DisplayStyle.None;

        _popup.style.opacity = 1f;
        _popup.style.display = DisplayStyle.Flex;
        _popup.AddToClassList("show");
        _isVisible = true;
    }

    private string GetPickSlotForSku(string skuId)
    {
        // Check if this SKU has a pick slot assigned globally
        var slots = SlotAssignmentService.GetSlotsForSku(skuId);
        if (slots.Any())
        {
            // Show all assigned slots
            return string.Join(", ", slots);
        }
        return "Unassigned";
    }

    public void HideImmediate()
    {
        _isVisible  = false;
        _isHovering = false;
        _hoverTimer = 0f;
        if (_popup != null)
        {
            _popup.style.display = DisplayStyle.None;
            _popup.RemoveFromClassList("show");
        }
        if (_truckPopup != null)
        {
            _truckPopup.style.display = DisplayStyle.None;
            _truckPopup.RemoveFromClassList("show");
        }
    }

    // ---------------------------------------------------------
    // CURSOR FOLLOWING — tooltip's BOTTOM-RIGHT corner sits at (and slightly overlaps) the cursor tip
    // ---------------------------------------------------------
    // The actual "anchor at bottom-right instead of top-left" shift is the constant -100%/-100%
    // translate set once on each popup (see Init / EnsureTruckPopupBuilt) — percent translate resolves
    // against the element's own final layout size automatically. This method only has to move the
    // anchor POINT (left/top) to the cursor, plus a small overlap so the corner pokes past the tip.
    private const float CursorOverlap = 8f;

    private void FollowCursor()
    {
        var target = _isTruckMode ? _truckPopup : _popup;
        if (target == null || _root == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        var layout = _root.layout;
        if (layout.width <= 0 || layout.height <= 0) return;

        float scaleX = layout.width  / Screen.width;
        float scaleY = layout.height / Screen.height;

        float mouseUiX = mousePos.x * scaleX;
        float mouseUiY = (Screen.height - mousePos.y) * scaleY;

        Vector2 target2 = new Vector2(mouseUiX + CursorOverlap * scaleX, mouseUiY + CursorOverlap * scaleY);
        // Unscaled: the popup chases the real mouse cursor, so its smoothing must not slow down
        // with the simulation (and must keep tracking while paused).
        _smoothPos = Vector2.Lerp(_smoothPos, target2, 1f - Mathf.Exp(-60f * Time.unscaledDeltaTime));

        target.style.left = _smoothPos.x;
        target.style.top = _smoothPos.y;
    }

    // ---------------------------------------------------------
    // PALLET BUILDER HOVER (built but not yet received pallets)
    // ---------------------------------------------------------
    /// <summary>Drives the hover tooltip for a pallet built via PalletBuilder that has no
    /// PalletData yet. Resolves the SKU from the builder and shows item number, description,
    /// and case count. Also handles shift+click to open the Pallet Builder UI.</summary>
    public void TickHoverPalletBuilder(bool hovering, PalletBuilder builder, Vector3 worldPos, Camera cam)
    {
        if (!IsEnabled) { HideImmediate(); return; }

        // Only show in IdleState
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        { HideImmediate(); return; }

        if (!hovering || builder == null)
        {
            _isHovering = false;
            _hoverTimer = 0f;
            HideImmediate();
            return;
        }

        // Resolve SKU from the builder (uses DockPalletUtility which checks linkedSku, casePrefab, etc.)
        var sku = GameCore.Labor.DockPalletUtility.GetSkuForPallet(builder.gameObject);
        if (sku == null)
        {
            // Can't determine the item — hide the tooltip
            _isHovering = false;
            _hoverTimer = 0f;
            HideImmediate();
            return;
        }

        // Build a pseudo-data holder so we can reuse ShowPallet's rendering logic
        _pendingPalletData = null;
        _pendingName = null;
        _pendingCost = 0;
        _pendingHourlyCost = 0;
        _isPalletMode = true;

        bool isNewTarget = !_isHovering || !_isPalletMode || sku != _pendingPalletBuilderSku;
        _pendingPalletBuilderSku = sku;

        if (isNewTarget)
        {
            _isHovering = true;
            _hoverTimer = 0f;
            _popup.style.opacity = 1f;
            ShowPalletBuilder(sku, builder.TotalCases, builder);
        }
        else
        {
            _hoverTimer += Time.unscaledDeltaTime;
            if (!_isVisible && _hoverTimer >= _hoverDelay)
                ShowPalletBuilder(sku, builder.TotalCases, builder);
        }

        if (_isVisible)
            FollowCursor();
    }

    private SkuData _pendingPalletBuilderSku;

    /// <summary>Renders the pallet tooltip using SkuData + case count (for built-but-unreceived pallets).</summary>
    private void ShowPalletBuilder(SkuData sku, int caseQty, PalletBuilder builder = null)
    {
        if (_popup == null || sku == null) return;

        _isTruckMode = false;
        HideTruckPopup();

        // Set title to item description
        if (_title != null)
            _title.text = sku.ItemDescription;

        // Set icon
        if (_iconImage != null)
        {
            _iconImage.sprite = sku.Icon;
            _iconImage.style.display = sku.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Build pallet info text
        if (_palletInfoPanel != null)
        {
            _palletInfoPanel.Clear();
            _palletInfoPanel.style.display = DisplayStyle.Flex;

            // Item number
            var itemNumLbl = new Label($"Item #: {sku.ItemNumber}");
            itemNumLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            itemNumLbl.style.fontSize = 12;
            _palletInfoPanel.Add(itemNumLbl);

            // Load ID (if available via MasterLink)
            if (builder != null)
            {
                var link = builder.GetComponent<PalletMasterLink>();
                if (link != null)
                {
                    ServiceLocator.TryGet<InventoryService>(out var inventory);
                    var record = inventory?.GetPallet(link.PalletId);
                    if (record != null && !string.IsNullOrEmpty(record.LoadId))
                    {
                        var loadIdLbl = new Label($"Load ID: {record.LoadId}");
                        loadIdLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                        loadIdLbl.style.fontSize = 12;
                        _palletInfoPanel.Add(loadIdLbl);
                    }
                }
            }

            // Case quantity
            var caseQtyLbl = new Label($"Case Qty: {caseQty}");
            caseQtyLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            caseQtyLbl.style.fontSize = 12;
            _palletInfoPanel.Add(caseQtyLbl);

            // Wholesale cost
            var wholesaleLbl = new Label($"Wholesale: ${sku.BuyValue:N0}");
            wholesaleLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            wholesaleLbl.style.fontSize = 12;
            _palletInfoPanel.Add(wholesaleLbl);

            // Retail cost
            var retailLbl = new Label($"Retail: ${sku.SellValue:N0}");
            retailLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            retailLbl.style.fontSize = 12;
            _palletInfoPanel.Add(retailLbl);

            // Ti x Hi
            var tiHiLbl = new Label($"Ti×Hi: {sku.Ti}×{sku.Hi}");
            tiHiLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            tiHiLbl.style.fontSize = 12;
            _palletInfoPanel.Add(tiHiLbl);
        }

        // Hide building-specific elements
        if (_cost != null) _cost.style.display = DisplayStyle.None;
        if (_hourlyCost != null) _hourlyCost.style.display = DisplayStyle.None;

        _popup.style.opacity = 1f;
        _popup.style.display = DisplayStyle.Flex;
        _popup.AddToClassList("show");
        _isVisible = true;
    }

    // ---------------------------------------------------------
    // TRUCK HOVER (tractor/trailer — vendor/order/appointment/door/items/load progress)
    // ---------------------------------------------------------
    public void TickHoverTruck(bool hovering, TruckController truck, Vector3 worldPos, Camera cam)
    {
        if (!IsEnabled) { HideImmediate(); return; }

        // Only show in IdleState
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        { HideImmediate(); return; }

        if (!hovering || truck == null)
        {
            _isHovering = false;
            _hoverTimer = 0f;
            HideImmediate();
            return;
        }

        bool isNewTarget = !_isHovering || !_isTruckMode || truck != _pendingTruck;

        _pendingTruck = truck;
        _pendingPalletData = null;
        _pendingName = null;
        _pendingCost = 0;
        _pendingHourlyCost = 0;
        _pendingLocationData = null;
        _isPalletMode = false;
        _isLocationMode = false;
        _isTruckMode = true;

        if (isNewTarget)
        {
            _isHovering = true;
            _hoverTimer = 0f;
            ShowTruck(truck);
        }
        else
        {
            _hoverTimer += Time.unscaledDeltaTime;
            if (!_isVisible && _hoverTimer >= _hoverDelay)
                ShowTruck(truck);
        }

        if (_isVisible)
            FollowCursor();
    }

    private void HideTruckPopup()
    {
        if (_truckPopup == null) return;
        _truckPopup.style.display = DisplayStyle.None;
        _truckPopup.RemoveFromClassList("show");
    }

    /// <summary>Builds the truck tooltip's element tree once, lazily (needs _root, which is only
    /// valid once _popup is actually live in a panel — see Init()). Reuses the ".world-hover-popup"
    /// USS class for the same absolute-position/background/radius base _popup already has, so
    /// FollowCursor's translate-based positioning works identically for both.</summary>
    private void EnsureTruckPopupBuilt()
    {
        if (_truckPopup != null) return;
        if (_root == null) _root = _popup?.panel?.visualTree;
        if (_root == null) return;

        _truckPopup = new VisualElement { name = "TruckHoverPopup" };
        _truckPopup.AddToClassList("world-hover-popup");
        // See the matching comment on _popup in Init() — anchors this box's bottom-right corner (not
        // top-left) to whatever left/top FollowCursor drives it to each frame.
        _truckPopup.style.translate = new Translate(Length.Percent(-100f), Length.Percent(-100f), 0f);
        _truckPopup.pickingMode = PickingMode.Ignore;
        // Explicit rather than relying on the inherited USS class value — matches the house
        // semi-transparent dark navy used elsewhere (SystemsLogWindow's floating panel, etc.).
        _truckPopup.style.backgroundColor = new Color(0.078f, 0.110f, 0.173f, 0.94f);
        _truckPopup.style.width = 300;
        _truckPopup.style.paddingLeft = 10;
        _truckPopup.style.paddingRight = 10;
        _truckPopup.style.paddingTop = 8;
        _truckPopup.style.paddingBottom = 8;
        // Thick colored frame — recolored per-order-type in ShowTruck. Overrides the base class's
        // thinner 3px border.
        _truckPopup.style.borderTopWidth = 4;
        _truckPopup.style.borderBottomWidth = 4;
        _truckPopup.style.borderLeftWidth = 4;
        _truckPopup.style.borderRightWidth = 4;
        // Visible (not the base class's default) so the door badge can hang half outside the
        // top-right corner without being clipped.
        _truckPopup.style.overflow = Overflow.Visible;

        // ── Door badge — light blue circle, top-right corner, "Dr.N" (2x size per Tad's ask) ──
        _truckDoorBadge = new VisualElement();
        _truckDoorBadge.pickingMode = PickingMode.Ignore;
        _truckDoorBadge.style.position = Position.Absolute;
        _truckDoorBadge.style.top = -28;
        _truckDoorBadge.style.right = -28;
        _truckDoorBadge.style.width = 68;
        _truckDoorBadge.style.height = 68;
        var fullRadius = new Length(34, LengthUnit.Pixel);
        _truckDoorBadge.style.borderTopLeftRadius = fullRadius;
        _truckDoorBadge.style.borderTopRightRadius = fullRadius;
        _truckDoorBadge.style.borderBottomLeftRadius = fullRadius;
        _truckDoorBadge.style.borderBottomRightRadius = fullRadius;
        _truckDoorBadge.style.backgroundColor = new Color(0x7E / 255f, 0xC8 / 255f, 0xE3 / 255f, 1f);
        _truckDoorBadge.style.borderTopWidth = 4;
        _truckDoorBadge.style.borderBottomWidth = 4;
        _truckDoorBadge.style.borderLeftWidth = 4;
        _truckDoorBadge.style.borderRightWidth = 4;
        var badgeBorderColor = new Color(0.08f, 0.12f, 0.16f, 1f);
        _truckDoorBadge.style.borderTopColor = badgeBorderColor;
        _truckDoorBadge.style.borderBottomColor = badgeBorderColor;
        _truckDoorBadge.style.borderLeftColor = badgeBorderColor;
        _truckDoorBadge.style.borderRightColor = badgeBorderColor;
        _truckDoorBadge.style.alignItems = Align.Center;
        _truckDoorBadge.style.justifyContent = Justify.Center;

        _truckDoorLabel = new Label { text = "" };
        _truckDoorLabel.pickingMode = PickingMode.Ignore;
        _truckDoorLabel.style.fontSize = 22;
        _truckDoorLabel.style.color = badgeBorderColor;
        _truckDoorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _truckDoorBadge.Add(_truckDoorLabel);
        _truckPopup.Add(_truckDoorBadge);

        // ── Comment/status bar — replaces the old always-on TruckDoorWaitBar world banner ──
        var commentBox = new VisualElement();
        commentBox.pickingMode = PickingMode.Ignore;
        commentBox.style.height = 40;
        commentBox.style.marginTop = 4;
        commentBox.style.marginBottom = 8;
        commentBox.style.position = Position.Relative;
        commentBox.style.overflow = Overflow.Hidden;
        // Very dark gray base — this is what's visible whenever there's no active driver message
        // (fill width stays at 0%, see ShowTruck's else-branch below), per Tad's ask. The status fill
        // sits on top of this and covers it once a message/color/progress is actually set.
        commentBox.style.backgroundColor = new Color(0.14f, 0.14f, 0.15f, 0.85f);
        var commentRadius = new Length(4, LengthUnit.Pixel);
        commentBox.style.borderTopLeftRadius = commentRadius;
        commentBox.style.borderTopRightRadius = commentRadius;
        commentBox.style.borderBottomLeftRadius = commentRadius;
        commentBox.style.borderBottomRightRadius = commentRadius;

        _truckCommentFill = new VisualElement();
        _truckCommentFill.pickingMode = PickingMode.Ignore;
        _truckCommentFill.style.position = Position.Absolute;
        _truckCommentFill.style.left = 0;
        _truckCommentFill.style.top = 0;
        _truckCommentFill.style.bottom = 0;
        _truckCommentFill.style.width = new Length(0, LengthUnit.Percent);
        commentBox.Add(_truckCommentFill);

        _truckCommentLabel = new Label { text = "" };
        _truckCommentLabel.pickingMode = PickingMode.Ignore;
        _truckCommentLabel.style.position = Position.Absolute;
        _truckCommentLabel.style.left = 4;
        _truckCommentLabel.style.right = 4;
        _truckCommentLabel.style.top = 2;
        _truckCommentLabel.style.bottom = 2;
        _truckCommentLabel.style.fontSize = 10;
        _truckCommentLabel.style.color = Color.white;
        _truckCommentLabel.style.whiteSpace = WhiteSpace.Normal;
        _truckCommentLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        commentBox.Add(_truckCommentLabel);

        _truckPopup.Add(commentBox);

        // ── Vendor / order # / appointment time ──
        _truckVendorLabel = new Label { text = "" };
        _truckVendorLabel.pickingMode = PickingMode.Ignore;
        _truckVendorLabel.style.fontSize = 14;
        _truckVendorLabel.style.color = new Color(0.9f, 0.95f, 1f, 1f);
        _truckVendorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _truckPopup.Add(_truckVendorLabel);

        _truckOrderLabel = new Label { text = "" };
        _truckOrderLabel.pickingMode = PickingMode.Ignore;
        _truckOrderLabel.style.fontSize = 11;
        _truckOrderLabel.style.color = new Color(0.75f, 0.85f, 0.95f, 1f);
        _truckPopup.Add(_truckOrderLabel);

        _truckApptLabel = new Label { text = "" };
        _truckApptLabel.pickingMode = PickingMode.Ignore;
        _truckApptLabel.style.fontSize = 11;
        _truckApptLabel.style.color = new Color(0.75f, 0.85f, 0.95f, 1f);
        _truckApptLabel.style.marginBottom = 6;
        _truckPopup.Add(_truckApptLabel);

        var divider = new VisualElement();
        divider.pickingMode = PickingMode.Ignore;
        divider.style.height = 1;
        divider.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
        divider.style.marginBottom = 6;
        _truckPopup.Add(divider);

        // ── Truck icon + load/unload progress ──
        var iconRow = new VisualElement();
        iconRow.pickingMode = PickingMode.Ignore;
        iconRow.style.flexDirection = FlexDirection.Row;
        iconRow.style.alignItems = Align.Center;
        iconRow.style.marginBottom = 6;

        var cab = new VisualElement();
        cab.pickingMode = PickingMode.Ignore;
        cab.style.width = 12;
        cab.style.height = 16;
        cab.style.marginRight = 2;
        cab.style.alignSelf = Align.FlexEnd;
        cab.style.backgroundColor = new Color(0.3f, 0.34f, 0.4f, 1f);
        cab.style.borderTopWidth = 1;
        cab.style.borderBottomWidth = 1;
        cab.style.borderLeftWidth = 1;
        cab.style.borderRightWidth = 1;
        var cabBorderColor = new Color(1f, 1f, 1f, 0.25f);
        cab.style.borderTopColor = cabBorderColor;
        cab.style.borderBottomColor = cabBorderColor;
        cab.style.borderLeftColor = cabBorderColor;
        cab.style.borderRightColor = cabBorderColor;

        var body = new VisualElement();
        body.pickingMode = PickingMode.Ignore;
        body.style.flexGrow = 1;
        body.style.height = 22;
        body.style.position = Position.Relative;
        body.style.overflow = Overflow.Hidden;
        body.style.backgroundColor = new Color(0.15f, 0.18f, 0.22f, 1f);
        body.style.borderTopWidth = 1;
        body.style.borderBottomWidth = 1;
        body.style.borderLeftWidth = 1;
        body.style.borderRightWidth = 1;
        body.style.borderTopColor = cabBorderColor;
        body.style.borderBottomColor = cabBorderColor;
        body.style.borderLeftColor = cabBorderColor;
        body.style.borderRightColor = cabBorderColor;

        _truckLoadFill = new VisualElement();
        _truckLoadFill.pickingMode = PickingMode.Ignore;
        _truckLoadFill.style.position = Position.Absolute;
        _truckLoadFill.style.left = 0;
        _truckLoadFill.style.top = 0;
        _truckLoadFill.style.bottom = 0;
        _truckLoadFill.style.width = new Length(0, LengthUnit.Percent);
        _truckLoadFill.style.backgroundColor = TruckLoadFillColor;
        body.Add(_truckLoadFill);

        _truckLoadPercentLabel = new Label { text = "" };
        _truckLoadPercentLabel.pickingMode = PickingMode.Ignore;
        _truckLoadPercentLabel.style.position = Position.Absolute;
        _truckLoadPercentLabel.style.left = 0;
        _truckLoadPercentLabel.style.right = 0;
        _truckLoadPercentLabel.style.top = 0;
        _truckLoadPercentLabel.style.bottom = 0;
        _truckLoadPercentLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _truckLoadPercentLabel.style.fontSize = 10;
        _truckLoadPercentLabel.style.color = Color.white;
        body.Add(_truckLoadPercentLabel);

        iconRow.Add(cab);
        iconRow.Add(body);
        _truckPopup.Add(iconRow);

        // ── Item / pallet-qty list ──
        _truckItemsList = new VisualElement();
        _truckItemsList.pickingMode = PickingMode.Ignore;
        _truckItemsList.style.flexDirection = FlexDirection.Column;
        _truckPopup.Add(_truckItemsList);

        _root.Add(_truckPopup);
    }

    /// <summary>Resolves vendor/order/appointment/door/items/progress for a hovered truck and
    /// renders the tooltip. Inbound trucks read straight off their AssignedShipment (PO). Outbound
    /// trucks have no direct order reference on TruckController, so this looks up the DockAppointment
    /// currently holding their door (DockScheduleService) and pulls orders off that.</summary>
    private void ShowTruck(TruckController truck)
    {
        EnsureTruckPopupBuilt();
        if (_truckPopup == null || truck == null) return;

        if (_popup != null)
        {
            _popup.style.display = DisplayStyle.None;
            _popup.RemoveFromClassList("show");
        }

        ServiceLocator.TryGet<DockScheduleService>(out var dockSchedule);
        ServiceLocator.TryGet<InventoryService>(out var inventory);

        Color borderColor = TruckRecurringColor;
        string vendor = null;
        string orderInfo = null;
        string apptInfo = null;
        int doorNumber = 0;
        float progress = 0f;
        var items = new List<(string desc, int qty, int pallets)>();

        if (!truck.IsOutbound)
        {
            var shipment = truck.AssignedShipment;
            doorNumber = truck.DockedAt != null ? truck.DockedAt.DoorNumber : 0;
            borderColor = TruckInboundColor;

            if (shipment != null)
            {
                vendor = shipment.SupplierName;
                orderInfo = $"PO {shipment.PONumber}";
                var appt = dockSchedule?.FindForPo(shipment.PONumber);
                apptInfo = appt != null ? $"Appt: {appt.TimeLabel}" : null;

                // Physical pallets-off-the-trailer progress, not case-received progress — advances the
                // instant the dock stocker drops a pallet in the lane (TotalPalletsAtDock snapshotted
                // at ClaimForOffload; LoadContainer.childCount then tracks what's left ON the trailer).
                int totalAtDock = truck.TotalPalletsAtDock;
                int remainingOnTrailer = truck.LoadContainer != null ? truck.LoadContainer.childCount : 0;
                int offloaded = Mathf.Max(0, totalAtDock - remainingOnTrailer);
                progress = totalAtDock > 0 ? Mathf.Clamp01(offloaded / (float)totalAtDock) : 0f;

                foreach (var li in shipment.LineItems)
                {
                    if (li == null || li.Dropped) continue;
                    var sku = inventory?.GetSkuData(li.SkuId);
                    string desc = sku != null ? sku.ItemDescription : li.SkuId;
                    int ti = sku != null && sku.Ti > 0 ? sku.Ti : 1;
                    int hi = sku != null && sku.Hi > 0 ? sku.Hi : 1;
                    int pallets = Mathf.Max(1, Mathf.CeilToInt(li.Quantity / (float)(ti * hi)));
                    items.Add((desc, li.Quantity, pallets));
                }
            }
            else
            {
                vendor = "Inbound Trailer";
            }
        }
        else
        {
            var dock = truck.DockedAt;
            doorNumber = dock != null ? dock.DoorNumber : 0;

            DockAppointment appt = null;
            if (dock != null && dockSchedule != null)
            {
                int today = dockSchedule.CurrentDay;
                foreach (var a in dockSchedule.Appointments)
                {
                    if (a.Parked || a.Kind == AppointmentKind.Inbound) continue;
                    if (a.DoorNumber != doorNumber || a.Day != today) continue;
                    appt = a;
                    break;
                }
            }

            if (appt != null)
            {
                vendor = appt.CustomerName;
                apptInfo = $"Appt: {appt.TimeLabel}";
                borderColor = (appt.Kind == AppointmentKind.Bulk || appt.Kind == AppointmentKind.Wholesale)
                    ? TruckBulkColor : TruckRecurringColor;

                ServiceLocator.TryGet<OrderService>(out var orderService);
                int totalPallets = 0;
                var orderNumbers = new List<string>();
                foreach (var orderId in appt.OrderIds)
                {
                    var order = orderService?.ActiveOrders.FirstOrDefault(o => o.OrderId == orderId);
                    if (order == null) continue;
                    if (!string.IsNullOrEmpty(order.OrderNumber)) orderNumbers.Add(order.OrderNumber);

                    foreach (var li in order.LineItems)
                    {
                        var sku = inventory?.GetSkuData(li.SkuId);
                        string desc = sku != null ? sku.ItemDescription : li.SkuId;
                        int ti = sku != null && sku.Ti > 0 ? sku.Ti : 1;
                        int hi = sku != null && sku.Hi > 0 ? sku.Hi : 1;
                        int pallets = Mathf.Max(1, Mathf.CeilToInt(li.QuantityNeeded / (float)(ti * hi)));
                        totalPallets += pallets;

                        int existingIdx = items.FindIndex(it => it.desc == desc);
                        if (existingIdx >= 0)
                        {
                            var e = items[existingIdx];
                            items[existingIdx] = (e.desc, e.qty + li.QuantityNeeded, e.pallets + pallets);
                        }
                        else
                        {
                            items.Add((desc, li.QuantityNeeded, pallets));
                        }
                    }
                }

                orderInfo = orderNumbers.Count == 0 ? null
                    : orderNumbers.Count == 1 ? $"Order {orderNumbers[0]}"
                    : $"Order {orderNumbers[0]} +{orderNumbers.Count - 1} more";

                int loadedPallets = truck.LoadContainer != null ? truck.LoadContainer.childCount : 0;
                progress = totalPallets > 0 ? Mathf.Clamp01(loadedPallets / (float)totalPallets) : 0f;
            }
            else
            {
                vendor = "Outbound Trailer";
            }
        }

        _truckPopup.style.borderTopColor = borderColor;
        _truckPopup.style.borderBottomColor = borderColor;
        _truckPopup.style.borderLeftColor = borderColor;
        _truckPopup.style.borderRightColor = borderColor;

        var waitBar = truck.DoorWaitBar;
        if (waitBar != null && waitBar.HasMessage)
        {
            _truckCommentLabel.text = waitBar.CurrentMessage;
            var c = waitBar.CurrentColor;
            _truckCommentFill.style.backgroundColor = new Color(c.r, c.g, c.b, 0.35f);
            _truckCommentFill.style.width = new Length(Mathf.Clamp01(waitBar.CurrentFillAmount) * 100f, LengthUnit.Percent);
        }
        else
        {
            _truckCommentLabel.text = "—";
            _truckCommentFill.style.width = new Length(0, LengthUnit.Percent);
        }

        _truckVendorLabel.text = string.IsNullOrEmpty(vendor) ? "Unknown" : vendor;

        _truckOrderLabel.text = orderInfo ?? "";
        _truckOrderLabel.style.display = string.IsNullOrEmpty(orderInfo) ? DisplayStyle.None : DisplayStyle.Flex;

        _truckApptLabel.text = apptInfo ?? "";
        _truckApptLabel.style.display = string.IsNullOrEmpty(apptInfo) ? DisplayStyle.None : DisplayStyle.Flex;

        if (doorNumber > 0)
        {
            _truckDoorLabel.text = $"Dr.{doorNumber}";
            _truckDoorBadge.style.display = DisplayStyle.Flex;
        }
        else
        {
            _truckDoorBadge.style.display = DisplayStyle.None;
        }

        _truckLoadFill.style.width = new Length(progress * 100f, LengthUnit.Percent);
        _truckLoadPercentLabel.text = $"{Mathf.RoundToInt(progress * 100f)}%";

        _truckItemsList.Clear();
        if (items.Count == 0)
        {
            var noneLbl = new Label("No items on record");
            noneLbl.pickingMode = PickingMode.Ignore;
            noneLbl.style.fontSize = 10;
            noneLbl.style.color = new Color(0.6f, 0.7f, 0.8f, 1f);
            _truckItemsList.Add(noneLbl);
        }
        else
        {
            foreach (var (desc, qty, pallets) in items)
            {
                var row = new Label($"{desc}  x{qty}  ({pallets} plt)");
                row.pickingMode = PickingMode.Ignore;
                row.style.fontSize = 10;
                row.style.color = new Color(0.85f, 0.9f, 0.95f, 1f);
                row.style.marginBottom = 1;
                _truckItemsList.Add(row);
            }
        }

        _truckPopup.style.opacity = 1f;
        _truckPopup.style.display = DisplayStyle.Flex;
        _truckPopup.AddToClassList("show");
        _isVisible = true;
    }

    // Legacy entry point kept for any code that still calls it
    public void SetWorldPosition(Vector3 worldPos, Camera cam) => FollowCursor();
}
