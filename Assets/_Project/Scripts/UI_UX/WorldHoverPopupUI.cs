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
    //
    // Bundled into one class (rather than a dozen separate _truckXxx fields) because there are now
    // TWO independent instances of this popup: _hoverTruckUI (the transient one that follows the
    // cursor while hovering, built/updated by TickHoverTruck) and _pinnedTruckUI (opened by clicking
    // a truck — stays open, anchored above that truck in world space, until clicked again). Keeping
    // one field set per instance means "hover truck A while truck B is pinned" shows both at once
    // instead of one popup fighting over which truck it's currently displaying.
    private class TruckPopupUI
    {
        public VisualElement Popup;
        public VisualElement CommentFill;
        public Label CommentLabel;
        public Image VendorIcon;
        public Label VendorLabel;
        public Label OrderLabel;
        public Label ApptLabel;
        public VisualElement DoorBadge;
        public Label DoorLabel;
        public VisualElement LoadFill;
        public Label LoadPercentLabel;
        public VisualElement ItemsList;
    }

    private TruckPopupUI _hoverTruckUI;
    private TruckPopupUI _pinnedTruckUI;
    private TruckController _pendingTruck;
    private bool _isTruckMode;
    // Guards SweepOrphanTruckPopups so it only runs once per script-recompile lifetime, not once per
    // popup build (resets to false on the next domain reload along with everything else — see there).
    private bool _sweptOrphanTruckPopups;

    // The one truck currently pinned open by a click (null when none is). Distinct from
    // _pendingTruck, which only tracks what the cursor is hovering right now.
    private TruckController _pinnedTruck;
    // World-space height above a truck's pivot the pinned popup anchors to — clears the cab roof.
    private const float PinnedPopupWorldHeight = 3f;
    // True once the player has manually dragged the pinned popup — PositionPinnedPopup then leaves
    // it alone instead of re-anchoring it above the truck every frame. Reset on each fresh pin.
    private bool _pinnedPopupManuallyPositioned;

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
        // NOT _pinnedTruckUI — a pinned truck's popup is independent of ordinary hover and only
        // closes via ToggleTruckPin (clicking the truck again), not whatever caused THIS hide.
        if (_hoverTruckUI != null)
        {
            _hoverTruckUI.Popup.style.display = DisplayStyle.None;
            _hoverTruckUI.Popup.RemoveFromClassList("show");
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
        var target = _isTruckMode ? _hoverTruckUI?.Popup : _popup;
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

        // Clamp so the popup can never slide off the viewport. left/top are the box's PRE-translate
        // top-left; the -100%/-100% translate (set once on each popup, see Init/EnsureTruckPopupBuilt)
        // then shifts it so left/top visually land on the box's bottom-right corner. That means left
        // IS the visual right edge and top IS the visual bottom edge — clamping them into
        // [box size, viewport size] keeps the opposite (left/top) edge from going negative while
        // keeping the near (right/bottom) edge from running past the viewport.
        float boxWidth = target.resolvedStyle.width;
        float boxHeight = target.resolvedStyle.height;
        float clampedX = boxWidth > 0 ? Mathf.Clamp(_smoothPos.x, boxWidth, layout.width) : _smoothPos.x;
        float clampedY = boxHeight > 0 ? Mathf.Clamp(_smoothPos.y, boxHeight, layout.height) : _smoothPos.y;

        target.style.left = clampedX;
        target.style.top = clampedY;
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

        // The pinned popup already shows this exact truck, persistently — a second, overlapping
        // cursor-following copy of the same info would just be visual noise, so skip it.
        if (truck == _pinnedTruck)
        {
            _isHovering = false;
            _hoverTimer = 0f;
            HideTruckPopup();
            _isVisible = false;
            return;
        }

        if (_popup != null)
        {
            _popup.style.display = DisplayStyle.None;
            _popup.RemoveFromClassList("show");
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

        _hoverTruckUI ??= BuildTruckPopupUI();

        if (_hoverTruckUI == null) return;

        if (isNewTarget)
        {
            _isHovering = true;
            _hoverTimer = 0f;
            RenderTruck(_hoverTruckUI, truck);
            ShowTruckPopupElement(_hoverTruckUI.Popup);
            _isVisible = true;
        }
        else
        {
            _hoverTimer += Time.unscaledDeltaTime;
            if (!_isVisible && _hoverTimer >= _hoverDelay)
            {
                RenderTruck(_hoverTruckUI, truck);
                ShowTruckPopupElement(_hoverTruckUI.Popup);
                _isVisible = true;
            }
            else if (_isVisible)
                // Keep refreshing every frame while already showing — otherwise the load %, item
                // critical counts, etc. only update when the mouse leaves and re-enters the truck.
                RenderTruck(_hoverTruckUI, truck);
        }

        if (_isVisible)
            FollowCursor();
    }

    // ---------------------------------------------------------
    // TRUCK PIN — click a truck to outline it (TruckHighlighter) and pin its tooltip open,
    // anchored above it in world space, until it's clicked again (left OR right click).
    // ---------------------------------------------------------
    private void Update()
    {
        if (_pinnedTruck != null)
        {
            RenderTruck(_pinnedTruckUI, _pinnedTruck);
            PositionPinnedPopup();
        }
        else if (_pinnedTruckUI != null && _pinnedTruckUI.Popup.style.display == DisplayStyle.Flex)
        {
            // The pinned truck was destroyed/despawned (drove off and got cleaned up) while still
            // pinned — Unity's overloaded == null already caught that above; tear down the leftover
            // popup/outline instead of leaving a stale card on screen forever.
            UnpinTruck();
        }
    }

    /// <summary>Click handler for a truck: pins it open (outline + persistent tooltip) if nothing
    /// else is pinned to it yet, or unpins it if it's the currently-pinned truck. Only one truck can
    /// be pinned at a time — pinning a new one drops whatever was pinned before.</summary>
    public void ToggleTruckPin(TruckController truck)
    {
        if (truck == null) return;

        if (_pinnedTruck == truck)
        {
            UnpinTruck();
            return;
        }

        if (_pinnedTruck != null) UnpinTruck();

        if (_root == null) _root = _popup?.panel?.visualTree;
        if (_pinnedTruckUI == null)
        {
            _pinnedTruckUI = BuildTruckPopupUI();
            if (_pinnedTruckUI == null) return;
            EnablePinnedPopupDragging(_pinnedTruckUI.Popup);
        }

        // A fresh pin always starts anchored to the truck — only dragging it by hand should stop
        // PositionPinnedPopup from following the truck; a brand-new pin shouldn't inherit wherever
        // the popup happened to be left after the LAST truck was dragged around.
        _pinnedPopupManuallyPositioned = false;

        _pinnedTruck = truck;
        RenderTruck(_pinnedTruckUI, truck);
        ShowTruckPopupElement(_pinnedTruckUI.Popup);
        PositionPinnedPopup();
        TruckHighlighter.Instance.Highlight(truck);
        AudioManager.Play("UIClick");
    }

    /// <summary>Lets the player grab a pinned truck popup anywhere on it and drag it to a fixed
    /// spot on screen. Once dragged, PositionPinnedPopup stops re-anchoring it above the truck for
    /// the rest of this pin — re-pinning (a fresh ToggleTruckPin) resets that. Only wired onto
    /// _pinnedTruckUI, never _hoverTruckUI: the transient hover popup stays click-through so it
    /// never blocks clicking the truck underneath it.</summary>
    private void EnablePinnedPopupDragging(VisualElement popup)
    {
        popup.pickingMode = PickingMode.Position;

        bool dragging = false;
        Vector2 pointerStart = Vector2.zero;
        Vector2 elementStart = Vector2.zero;

        popup.RegisterCallback<PointerDownEvent>(evt =>
        {
            dragging = true;
            pointerStart = evt.position;
            elementStart = new Vector2(popup.resolvedStyle.left, popup.resolvedStyle.top);
            popup.CapturePointer(evt.pointerId);
            _pinnedPopupManuallyPositioned = true;
            // Suppress the ordinary world-hover tooltip (building name/cost, pallet, location) while
            // dragging — SetEnabled(false) hides it via the same path Dev Settings uses, but leaves
            // THIS pinned popup alone (HideImmediate never touches _pinnedTruckUI, see its comment).
            SetEnabled(false);
            evt.StopPropagation();
        });
        popup.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging) return;
            Vector2 delta = (Vector2)evt.position - pointerStart;
            float newLeft = elementStart.x + delta.x;
            float newTop = elementStart.y + delta.y;

            // Same clamp FollowCursor/PositionPinnedPopup use — left/top are the visual right/bottom
            // edge (see the -100%/-100% translate comment above), so keeping them inside
            // [box size, viewport size] keeps the WHOLE box on screen, not just this one edge.
            if (_root != null)
            {
                var layout = _root.layout;
                if (layout.width > 0 && layout.height > 0)
                {
                    float boxWidth = popup.resolvedStyle.width;
                    float boxHeight = popup.resolvedStyle.height;
                    if (boxWidth > 0) newLeft = Mathf.Clamp(newLeft, boxWidth, layout.width);
                    if (boxHeight > 0) newTop = Mathf.Clamp(newTop, boxHeight, layout.height);
                }
            }

            popup.style.left = newLeft;
            popup.style.top = newTop;
            evt.StopPropagation();
        });
        popup.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging) return;
            dragging = false;
            SetEnabled(true);
            popup.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        });
    }

    private void UnpinTruck()
    {
        if (_pinnedTruckUI != null)
        {
            _pinnedTruckUI.Popup.style.display = DisplayStyle.None;
            _pinnedTruckUI.Popup.RemoveFromClassList("show");
        }
        if (TruckHighlighter.HasInstance) TruckHighlighter.Instance.Clear();
        _pinnedTruck = null;
        _pinnedPopupManuallyPositioned = false;
    }

    /// <summary>Anchors the pinned popup's bottom-right corner (the same -100%/-100%-translated
    /// corner FollowCursor drives) above the pinned truck's current world position instead of the
    /// cursor — moves with the truck as it drives, and gets the same viewport clamp FollowCursor
    /// uses so it can't slide off-screen either.</summary>
    private void PositionPinnedPopup()
    {
        if (_pinnedTruck == null || _pinnedTruckUI == null || _root == null) return;
        if (_pinnedPopupManuallyPositioned) return;

        var cam = Camera.main;
        if (cam == null) return;

        var layout = _root.layout;
        if (layout.width <= 0 || layout.height <= 0) return;

        Vector3 anchorWorld = _pinnedTruck.transform.position + Vector3.up * PinnedPopupWorldHeight;
        Vector3 sp = cam.WorldToScreenPoint(anchorWorld);
        if (sp.z < 0)
        {
            // Truck is behind the camera — nothing sane to anchor to this frame, just hide it
            // rather than pin it to a nonsense position; it reappears once the truck's back in view.
            _pinnedTruckUI.Popup.style.display = DisplayStyle.None;
            return;
        }
        _pinnedTruckUI.Popup.style.display = DisplayStyle.Flex;

        float scaleX = layout.width / Screen.width;
        float scaleY = layout.height / Screen.height;
        float uiX = sp.x * scaleX;
        float uiY = (Screen.height - sp.y) * scaleY;

        float boxWidth = _pinnedTruckUI.Popup.resolvedStyle.width;
        float boxHeight = _pinnedTruckUI.Popup.resolvedStyle.height;
        float clampedX = boxWidth > 0 ? Mathf.Clamp(uiX, boxWidth, layout.width) : uiX;
        float clampedY = boxHeight > 0 ? Mathf.Clamp(uiY, boxHeight, layout.height) : uiY;

        _pinnedTruckUI.Popup.style.left = clampedX;
        _pinnedTruckUI.Popup.style.top = clampedY;
    }

    private void HideTruckPopup()
    {
        // Only the transient hover popup — a pinned truck's popup outlives whatever else is being
        // hovered and only closes via ToggleTruckPin.
        if (_hoverTruckUI == null) return;
        _hoverTruckUI.Popup.style.display = DisplayStyle.None;
        _hoverTruckUI.Popup.RemoveFromClassList("show");
    }

    /// <summary>Removes any "TruckHoverPopup" element already sitting under _root that this
    /// component's own _hoverTruckUI/_pinnedTruckUI don't know about. Runs once per script-recompile
    /// lifetime (see _sweptOrphanTruckPopups) — this component's C# fields reset to null across a
    /// mid-Play recompile, but the panel/root itself is NOT torn down, so a popup built before that
    /// reload is still sitting there, invisible to the new fields, looking to Tad like a permanent
    /// duplicate truck tooltip nothing can close.</summary>
    private void SweepOrphanTruckPopups()
    {
        if (_sweptOrphanTruckPopups || _root == null) return;
        _sweptOrphanTruckPopups = true;

        var stray = new List<VisualElement>();
        foreach (var child in _root.Children())
            if (child.name == "TruckHoverPopup") stray.Add(child);
        foreach (var el in stray) el.RemoveFromHierarchy();
    }

    /// <summary>Builds one truck tooltip's element tree — called once each for _hoverTruckUI and
    /// _pinnedTruckUI (needs _root, which is only valid once _popup is actually live in a panel —
    /// see Init()). Reuses the ".world-hover-popup" USS class for the same absolute-position/
    /// background/radius base _popup already has, so FollowCursor's translate-based positioning
    /// works identically for both.</summary>
    private TruckPopupUI BuildTruckPopupUI()
    {
        if (_root == null) _root = _popup?.panel?.visualTree;
        if (_root == null) return null;

        // Defensive one-time cleanup: a mid-Play script recompile can leave a PREVIOUS instance's
        // truck popup element attached to this same panel while this component's own _hoverTruckUI/
        // _pinnedTruckUI references reset to null (plain C# fields don't survive domain reload) — the
        // result is an orphaned popup nothing points to anymore, frozen in place, that Tad sees as a
        // duplicate truck tooltip. Sweep it out before building a fresh one.
        SweepOrphanTruckPopups();

        var ui = new TruckPopupUI();

        var popup = new VisualElement { name = "TruckHoverPopup" };
        ui.Popup = popup;
        popup.AddToClassList("world-hover-popup");
        // See the matching comment on _popup in Init() — anchors this box's bottom-right corner (not
        // top-left) to whatever left/top FollowCursor (or PositionPinnedPopup) drives it to.
        popup.style.translate = new Translate(Length.Percent(-100f), Length.Percent(-100f), 0f);
        popup.pickingMode = PickingMode.Ignore;
        // Explicit rather than relying on the inherited USS class value — matches the house
        // semi-transparent dark navy used elsewhere (SystemsLogWindow's floating panel, etc.).
        popup.style.backgroundColor = new Color(0.078f, 0.110f, 0.173f, 0.94f);
        popup.style.width = 300;
        popup.style.paddingLeft = 10;
        popup.style.paddingRight = 10;
        popup.style.paddingTop = 8;
        popup.style.paddingBottom = 8;
        // Thick colored frame — recolored per-order-type in RenderTruck. Overrides the base class's
        // thinner 3px border.
        popup.style.borderTopWidth = 4;
        popup.style.borderBottomWidth = 4;
        popup.style.borderLeftWidth = 4;
        popup.style.borderRightWidth = 4;
        // Visible (not the base class's default) so the door badge can hang half outside the
        // top-right corner without being clipped.
        popup.style.overflow = Overflow.Visible;

        // ── Door badge — light blue circle, "Dr.N" (2x size per Tad's ask). Top-right corner of the
        // popup, hanging half outside it — per Tad's follow-up, moved back off the fill bar and up to
        // this corner.
        var doorBadge = new VisualElement();
        ui.DoorBadge = doorBadge;
        doorBadge.pickingMode = PickingMode.Ignore;
        doorBadge.style.position = Position.Absolute;
        doorBadge.style.top = -28;
        doorBadge.style.right = -28;
        doorBadge.style.width = 68;
        doorBadge.style.height = 68;
        var fullRadius = new Length(34, LengthUnit.Pixel);
        doorBadge.style.borderTopLeftRadius = fullRadius;
        doorBadge.style.borderTopRightRadius = fullRadius;
        doorBadge.style.borderBottomLeftRadius = fullRadius;
        doorBadge.style.borderBottomRightRadius = fullRadius;
        doorBadge.style.backgroundColor = new Color(0x7E / 255f, 0xC8 / 255f, 0xE3 / 255f, 1f);
        doorBadge.style.borderTopWidth = 4;
        doorBadge.style.borderBottomWidth = 4;
        doorBadge.style.borderLeftWidth = 4;
        doorBadge.style.borderRightWidth = 4;
        var badgeBorderColor = new Color(0.08f, 0.12f, 0.16f, 1f);
        doorBadge.style.borderTopColor = badgeBorderColor;
        doorBadge.style.borderBottomColor = badgeBorderColor;
        doorBadge.style.borderLeftColor = badgeBorderColor;
        doorBadge.style.borderRightColor = badgeBorderColor;
        doorBadge.style.alignItems = Align.Center;
        doorBadge.style.justifyContent = Justify.Center;

        var doorLabel = new Label { text = "" };
        ui.DoorLabel = doorLabel;
        doorLabel.pickingMode = PickingMode.Ignore;
        doorLabel.style.fontSize = 22;
        doorLabel.style.color = badgeBorderColor;
        doorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        doorBadge.Add(doorLabel);
        // NOT added to popup here — added last, at the bottom of this method, so it paints on
        // top of every other row it overhangs (the comment bar, item list, etc.) instead of sitting
        // behind whichever one happens to be added after it.

        // ── Comment/status bar — replaces the old always-on TruckDoorWaitBar world banner ──
        var commentBox = new VisualElement();
        commentBox.pickingMode = PickingMode.Ignore;
        commentBox.style.height = 40;
        commentBox.style.marginTop = 4;
        commentBox.style.marginBottom = 8;
        commentBox.style.position = Position.Relative;
        commentBox.style.overflow = Overflow.Hidden;
        // Medium-dark gray base — this is what's visible whenever there's no active driver message
        // (fill width stays at 0%, see RenderTruck's else-branch below), per Tad's ask (the lighter
        // gray this used to be made the white text hard to read). The status fill sits on top of
        // this and covers it once a message/color/progress is actually set.
        commentBox.style.backgroundColor = new Color(0.32f, 0.34f, 0.37f, 0.95f);
        var commentRadius = new Length(4, LengthUnit.Pixel);
        commentBox.style.borderTopLeftRadius = commentRadius;
        commentBox.style.borderTopRightRadius = commentRadius;
        commentBox.style.borderBottomLeftRadius = commentRadius;
        commentBox.style.borderBottomRightRadius = commentRadius;

        var commentFill = new VisualElement();
        ui.CommentFill = commentFill;
        commentFill.pickingMode = PickingMode.Ignore;
        commentFill.style.position = Position.Absolute;
        commentFill.style.left = 0;
        commentFill.style.top = 0;
        commentFill.style.bottom = 0;
        commentFill.style.width = new Length(0, LengthUnit.Percent);
        commentBox.Add(commentFill);

        var commentLabel = new Label { text = "" };
        ui.CommentLabel = commentLabel;
        commentLabel.pickingMode = PickingMode.Ignore;
        commentLabel.style.position = Position.Absolute;
        commentLabel.style.left = 4;
        commentLabel.style.right = 4;
        commentLabel.style.top = 2;
        commentLabel.style.bottom = 2;
        commentLabel.style.fontSize = 16; // bumped to 16px per Tad's ask
        commentLabel.style.color = Color.white;
        commentLabel.style.whiteSpace = WhiteSpace.Normal;
        commentLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        commentBox.Add(commentLabel);

        popup.Add(commentBox);

        // ── Vendor name / order # / appointment time ──
        var vendorLabel = new Label { text = "" };
        ui.VendorLabel = vendorLabel;
        vendorLabel.pickingMode = PickingMode.Ignore;
        vendorLabel.style.fontSize = 18; // 14 -> 18, +25% per Tad's ask
        vendorLabel.style.color = new Color(0.9f, 0.95f, 1f, 1f);
        vendorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        popup.Add(vendorLabel);

        // ── Vendor icon — big, top-right, sized/positioned where Tad's mockup drew it (below the
        // door badge, roughly spanning the PO/Appt lines) rather than a small inline icon next to
        // the name.
        var vendorIcon = new Image();
        ui.VendorIcon = vendorIcon;
        vendorIcon.pickingMode = PickingMode.Ignore;
        vendorIcon.style.position = Position.Absolute;
        vendorIcon.style.top = 76; // pushed further down per Tad's follow-up, over the PO/Appt lines
        vendorIcon.style.right = 0;
        vendorIcon.style.width = 95;  // stretched larger (was 70x70) per Tad's follow-up
        vendorIcon.style.height = 100;
        vendorIcon.style.borderTopLeftRadius = 6;
        vendorIcon.style.borderTopRightRadius = 6;
        vendorIcon.style.borderBottomLeftRadius = 6;
        vendorIcon.style.borderBottomRightRadius = 6;
        vendorIcon.style.display = DisplayStyle.None;
        popup.Add(vendorIcon);

        // Font sizes bumped 50% (11 -> 17) per Tad's ask.
        var orderLabel = new Label { text = "" };
        ui.OrderLabel = orderLabel;
        orderLabel.pickingMode = PickingMode.Ignore;
        orderLabel.style.fontSize = 21; // 17 -> 21, +25% per Tad's ask
        orderLabel.style.color = new Color(0.75f, 0.85f, 0.95f, 1f);
        popup.Add(orderLabel);

        var apptLabel = new Label { text = "" };
        ui.ApptLabel = apptLabel;
        apptLabel.pickingMode = PickingMode.Ignore;
        apptLabel.style.fontSize = 21; // 17 -> 21, +25% per Tad's ask
        apptLabel.style.color = new Color(0.75f, 0.85f, 0.95f, 1f);
        apptLabel.style.marginBottom = 6;
        popup.Add(apptLabel);

        var divider = new VisualElement();
        divider.pickingMode = PickingMode.Ignore;
        divider.style.height = 1;
        divider.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
        divider.style.marginBottom = 6;
        popup.Add(divider);

        // ── Truck icon + load/unload progress ──
        // The little unused cab square that used to sit left of the bar is gone; the bar (body) now
        // spans the row's full width.
        var iconRow = new VisualElement();
        iconRow.pickingMode = PickingMode.Ignore;
        iconRow.style.flexDirection = FlexDirection.Row;
        iconRow.style.alignItems = Align.Center;
        iconRow.style.marginBottom = 6;

        var cabBorderColor = new Color(1f, 1f, 1f, 0.25f);
        var body = new VisualElement();
        body.pickingMode = PickingMode.Ignore;
        body.style.flexGrow = 1;
        body.style.height = 22;
        body.style.position = Position.Relative;
        body.style.overflow = Overflow.Hidden;
        body.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f); // darker gray per Tad's ask
        body.style.borderTopWidth = 1;
        body.style.borderBottomWidth = 1;
        body.style.borderLeftWidth = 1;
        body.style.borderRightWidth = 1;
        body.style.borderTopColor = cabBorderColor;
        body.style.borderBottomColor = cabBorderColor;
        body.style.borderLeftColor = cabBorderColor;
        body.style.borderRightColor = cabBorderColor;

        var loadFill = new VisualElement();
        ui.LoadFill = loadFill;
        loadFill.pickingMode = PickingMode.Ignore;
        loadFill.style.position = Position.Absolute;
        loadFill.style.left = 0;
        loadFill.style.top = 0;
        loadFill.style.bottom = 0;
        loadFill.style.width = new Length(0, LengthUnit.Percent);
        loadFill.style.backgroundColor = TruckLoadFillColor;
        body.Add(loadFill);

        var loadPercentLabel = new Label { text = "" };
        ui.LoadPercentLabel = loadPercentLabel;
        loadPercentLabel.pickingMode = PickingMode.Ignore;
        loadPercentLabel.style.position = Position.Absolute;
        loadPercentLabel.style.left = 0;
        loadPercentLabel.style.right = 0;
        loadPercentLabel.style.top = 0;
        loadPercentLabel.style.bottom = 0;
        loadPercentLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        loadPercentLabel.style.fontSize = 16; // 13 -> 16, +25% per Tad's follow-up ask
        loadPercentLabel.style.color = Color.white;
        body.Add(loadPercentLabel);

        iconRow.Add(body);
        popup.Add(iconRow);

        // ── Item / pallet-qty list ──
        var itemsList = new VisualElement();
        ui.ItemsList = itemsList;
        itemsList.pickingMode = PickingMode.Ignore;
        itemsList.style.flexDirection = FlexDirection.Column;
        popup.Add(itemsList);

        // Added last so it paints in front of every row above it hangs over — see the comment where
        // it was constructed, near the top of this method.
        popup.Add(doorBadge);

        _root.Add(popup);
        return ui;
    }

    /// <summary>Resolves vendor/order/appointment/door/items/progress for a truck and renders it
    /// into the given popup instance (either _hoverTruckUI or _pinnedTruckUI — see TruckPopupUI).
    /// Inbound trucks read straight off their AssignedShipment (PO). Outbound trucks have no direct
    /// order reference on TruckController, so this looks up the DockAppointment currently holding
    /// their door (DockScheduleService) and pulls orders off that.</summary>
    private void RenderTruck(TruckPopupUI ui, TruckController truck)
    {
        if (ui == null || truck == null) return;

        ServiceLocator.TryGet<DockScheduleService>(out var dockSchedule);
        ServiceLocator.TryGet<InventoryService>(out var inventory);
        ServiceLocator.TryGet<OrderService>(out var orderServiceForCritical);

        Color borderColor = TruckRecurringColor;
        string vendor = null;
        string vendorId = null;
        string orderInfo = null;
        string apptInfo = null;
        int doorNumber = 0;
        float progress = 0f;
        var items = new List<(string desc, int qty, int pallets, int criticalCases)>();

        if (!truck.IsOutbound)
        {
            var shipment = truck.AssignedShipment;
            doorNumber = truck.DockedAt != null ? truck.DockedAt.DoorNumber : 0;
            borderColor = TruckInboundColor;

            if (shipment != null)
            {
                vendor = shipment.SupplierName;
                vendorId = shipment.SupplierId;
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

                // How many cases of each line are urgently needed once they land — on-hand stock
                // that can't cover the combined demand across every active order for that SKU.
                // Mirrors CriticalStockCheck's own definition, but capped to this line's own
                // quantity since a case can only be "critical" up to how much of it is arriving.
                var neededCache = new Dictionary<string, int>();
                int NeededFor(string skuId)
                {
                    if (neededCache.TryGetValue(skuId, out var cached)) return cached;
                    int needed = orderServiceForCritical?.ActiveOrders
                        .SelectMany(o => o.LineItems)
                        .Where(oli => oli.SkuId == skuId)
                        .Sum(oli => oli.QuantityNeeded) ?? 0;
                    neededCache[skuId] = needed;
                    return needed;
                }

                foreach (var li in shipment.LineItems)
                {
                    if (li == null || li.Dropped) continue;
                    var sku = inventory?.GetSkuData(li.SkuId);
                    string desc = sku != null ? sku.ItemDescription : li.SkuId;
                    int ti = sku != null && sku.Ti > 0 ? sku.Ti : 1;
                    int hi = sku != null && sku.Hi > 0 ? sku.Hi : 1;
                    int pallets = Mathf.Max(1, Mathf.CeilToInt(li.Quantity / (float)(ti * hi)));
                    int onHand = inventory?.GetTotalUnitsBySku(li.SkuId) ?? 0;
                    int criticalCases = Mathf.Clamp(NeededFor(li.SkuId) - onHand, 0, li.Quantity);
                    items.Add((desc, li.Quantity, pallets, criticalCases));
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
                            items[existingIdx] = (e.desc, e.qty + li.QuantityNeeded, e.pallets + pallets, 0);
                        }
                        else
                        {
                            items.Add((desc, li.QuantityNeeded, pallets, 0));
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

        ui.Popup.style.borderTopColor = borderColor;
        ui.Popup.style.borderBottomColor = borderColor;
        ui.Popup.style.borderLeftColor = borderColor;
        ui.Popup.style.borderRightColor = borderColor;

        var waitBar = truck.DoorWaitBar;
        if (waitBar != null && waitBar.HasMessage)
        {
            ui.CommentLabel.text = waitBar.CurrentMessage;
            ui.CommentLabel.style.color = Color.white;
            var c = waitBar.CurrentColor;
            ui.CommentFill.style.backgroundColor = new Color(c.r, c.g, c.b, 0.35f);
            ui.CommentFill.style.width = new Length(Mathf.Clamp01(waitBar.CurrentFillAmount) * 100f, LengthUnit.Percent);
        }
        else
        {
            // Labelled placeholder instead of a lone "—" — this bar is about to carry live driver
            // comments, and a bare dash reads as broken UI while a truck just sits waiting for a door.
            ui.CommentLabel.text = "Delay Timer Bar";
            ui.CommentLabel.style.color = Color.white;
            ui.CommentFill.style.width = new Length(0, LengthUnit.Percent);
        }

        ui.VendorLabel.text = string.IsNullOrEmpty(vendor) ? "Unknown" : vendor;

        var vendorData = !string.IsNullOrEmpty(vendorId) ? VendorRegistry.Load()?.GetById(vendorId) : null;
        if (vendorData != null && vendorData.Icon != null)
        {
            ui.VendorIcon.sprite = vendorData.Icon;
            ui.VendorIcon.style.display = DisplayStyle.Flex;
        }
        else
        {
            ui.VendorIcon.style.display = DisplayStyle.None;
        }

        ui.OrderLabel.text = orderInfo ?? "";
        ui.OrderLabel.style.display = string.IsNullOrEmpty(orderInfo) ? DisplayStyle.None : DisplayStyle.Flex;

        ui.ApptLabel.text = apptInfo ?? "";
        ui.ApptLabel.style.display = string.IsNullOrEmpty(apptInfo) ? DisplayStyle.None : DisplayStyle.Flex;

        if (doorNumber > 0)
        {
            ui.DoorLabel.text = $"Dr.{doorNumber}";
            ui.DoorBadge.style.display = DisplayStyle.Flex;
        }
        else
        {
            ui.DoorBadge.style.display = DisplayStyle.None;
        }

        ui.LoadFill.style.width = new Length(progress * 100f, LengthUnit.Percent);
        ui.LoadPercentLabel.text = $"{Mathf.RoundToInt(progress * 100f)}%";

        ui.ItemsList.Clear();
        if (items.Count == 0)
        {
            var noneLbl = new Label("No items on record");
            noneLbl.pickingMode = PickingMode.Ignore;
            noneLbl.style.fontSize = 10;
            noneLbl.style.color = new Color(0.6f, 0.7f, 0.8f, 1f);
            ui.ItemsList.Add(noneLbl);
        }
        else
        {
            foreach (var (desc, qty, pallets, criticalCases) in items)
            {
                string text = $"{desc}  x{qty}  ({pallets} plt)";
                if (criticalCases > 0) text += $"  — {criticalCases} critical";
                var row = new Label(text);
                row.pickingMode = PickingMode.Ignore;
                row.style.fontSize = 16; // 13 -> 16, +25% per Tad's ask
                row.style.color = criticalCases > 0
                    ? new Color(1f, 0.55f, 0.4f, 1f)
                    : new Color(0.85f, 0.9f, 0.95f, 1f);
                row.style.marginBottom = 2;
                ui.ItemsList.Add(row);
            }
        }
    }

    /// <summary>Flips a truck popup (hover or pinned) to visible. Split out of RenderTruck because
    /// RenderTruck also runs every frame for the PINNED popup's live refresh (see Update()), and that
    /// refresh must never touch the shared _isVisible flag the ordinary hover system gates on.</summary>
    private static void ShowTruckPopupElement(VisualElement popup)
    {
        popup.style.opacity = 1f;
        popup.style.display = DisplayStyle.Flex;
        popup.AddToClassList("show");
    }

    // Legacy entry point kept for any code that still calls it
    public void SetWorldPosition(Vector3 worldPos, Camera cam) => FollowCursor();
}
