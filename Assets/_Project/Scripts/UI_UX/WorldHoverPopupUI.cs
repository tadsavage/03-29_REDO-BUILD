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
    private BuildMenuUI _buildMenuUI;

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
        public VisualElement VendorIconFrame;
        public VisualElement VendorIconBorder;
        public Label VendorLabel;
        public Label OrderLabel;
        public Label ApptLabel;
        public VisualElement DoorBadge;
        public Label DoorLabel;
        public VisualElement LoadFill;
        public Label LoadPercentLabel;
        public Label TotalPalletsLabel;
        public VisualElement ItemsList;
    }

    private TruckPopupUI _hoverTruckUI;
    private TruckPopupUI _pinnedTruckUI;

    // Cached Lilita One font asset for badge/label text that needs the house display font.
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
    private TruckController _pendingTruck;
    private bool _isTruckMode;
    // Guards SweepOrphanTruckPopups so it only runs once per script-recompile lifetime, not once per
    // popup build (resets to false on the next domain reload along with everything else — see there).
    private bool _sweptOrphanTruckPopups;

    // The one truck currently pinned open by a click (null when none is). Distinct from
    // _pendingTruck, which only tracks what the cursor is hovering right now.
    private TruckController _pinnedTruck;
    // True once the player has manually dragged the pinned popup — PositionPinnedPopupAtBottomLeft
    // then leaves it alone instead of re-anchoring it to the fixed corner every frame. Reset on each
    // fresh pin.
    private bool _pinnedPopupManuallyPositioned;

    // Order-type colors — deliberately match the Scheduler's own legend (Recurring/Bulk/Inbound PO)
    // so the tooltip's frame "corresponds to the order types on the scheduler" per Tad's spec. Kept
    // as a bright/saturated trio here rather than SchedulerPanel's muted chip-edge colors, since this
    // border has to read as a "thick colored line" at a glance, not blend into a dark chip.
    private static readonly Color TruckRecurringColor = TruckOrderColors.Recurring; // blue
    private static readonly Color TruckBulkColor      = TruckOrderColors.Bulk;      // green
    private static readonly Color TruckInboundColor   = TruckOrderColors.Inbound;   // orange
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
    public void SetBuildMenuUI(BuildMenuUI buildMenuUI) => _buildMenuUI = buildMenuUI;

    /// <summary>Tad's spec: the item stats tooltip (name/cost/hourly cost) is only wanted while
    /// idle-browsing under the Build tab — not in Play mode, and not while actively
    /// placing/deleting/moving something even if the Build tab is up (IdleState already covers that
    /// half). Reports has no world view to hover in, so it's excluded along with Play by simply
    /// requiring HudMode.Build specifically rather than "not Play".</summary>
    private bool BuildTabActive => _buildMenuUI == null || _buildMenuUI.CurrentMode == BuildMenuUI.HudMode.Build;

    /// <summary>Per Tad's follow-up ask: on the OUTBOUND tab (HudMode.Play — that's its on-screen
    /// label, see TabPlay in BuildMenu.uxml) he only wants Items (pallets) and Trailers (trucks) to
    /// hover — not locations/racks/generic buildings, which stay Build-tab-only. Pass
    /// <paramref name="allowOutboundTab"/> true from exactly those two call sites.</summary>
    private bool HoverAllowedForMode(bool allowOutboundTab) =>
        BuildTabActive || (allowOutboundTab && _buildMenuUI != null && _buildMenuUI.CurrentMode == BuildMenuUI.HudMode.Play);

    // ---------------------------------------------------------
    // MAIN UPDATE - Building/Object hover
    // ---------------------------------------------------------
    public void TickHover(bool hovering, string name, int cost, int hourlyCost,
                          Vector3 worldPos, Camera cam, bool allowOutboundTab = false)
    {
        if (!IsEnabled) { HideImmediate(); return; }

        // Only show while idle-browsing under the Build tab — not in Play mode, and not mid
        // placement/delete/move even under Build (IdleState check covers that half). Trucks are the
        // one exception (see TickHoverTruckSummary) — Tad wants Trailers hoverable on the Outbound
        // tab too, so that one call site passes allowOutboundTab: true.
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        { HideImmediate(); return; }
        if (!HoverAllowedForMode(allowOutboundTab)) { HideImmediate(); return; }

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

        // Only show while idle-browsing under the Build tab — not in Play mode, and not mid
        // placement/delete/move even under Build (IdleState check covers that half). Pallets ("Items")
        // are one of the two things Tad wants hoverable on the Outbound tab too (see HoverAllowedForMode).
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        { HideImmediate(); return; }
        if (!HoverAllowedForMode(allowOutboundTab: true)) { HideImmediate(); return; }

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

        // Only show while idle-browsing under the Build tab — not in Play mode, and not mid
        // placement/delete/move even under Build (IdleState check covers that half).
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        { HideImmediate(); return; }
        if (!BuildTabActive) { HideImmediate(); return; }

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

        // Only show while idle-browsing under the Build tab — not in Play mode, and not mid
        // placement/delete/move even under Build (IdleState check covers that half). Pallet builders
        // ("Items") are one of the two things Tad wants hoverable on the Outbound tab too.
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        { HideImmediate(); return; }
        if (!HoverAllowedForMode(allowOutboundTab: true)) { HideImmediate(); return; }

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

    /// <summary>Same pallet-info hover card <see cref="TickHoverPalletBuilder"/> shows for live
    /// warehouse pallets, but for a UI-only caller (ItemCreatorPanel's render-texture pallet preview)
    /// that has no FSM/build-tab/world-raycast context of its own — none of TickHoverPalletBuilder's
    /// gating (build-tab active, idle FSM state, resolving a SKU via DockPalletUtility) applies here,
    /// since the caller already knows exactly which SkuData to show and exactly when the pointer
    /// enters/leaves (a UI pointer event, not a per-frame world-hover Tick).</summary>
    // Reparent bookkeeping for ShowForUiPreview — see the comment inside it for why this is needed
    // at all instead of just BringToFront().
    private VisualElement _uiPreviewOriginalParent;
    private int _uiPreviewOriginalIndex = -1;
    private VisualElement _uiPreviewOriginalRoot;
    private bool _uiPreviewReparented;

    public void ShowForUiPreview(SkuData sku, int caseQty, VisualElement hostRoot = null)
    {
        if (!IsEnabled || sku == null) { HideImmediate(); return; }
        _isTruckMode = false;
        HideTruckPopup();
        _isPalletMode = true;
        _pendingPalletBuilderSku = sku;
        _isHovering = true;

        // _popup actually lives inside BuildMenuUI's own UIDocument (sortingOrder 120, fixed — see
        // BuildMenuUI.Awake) — permanently BELOW the Hud document (999999) that ItemCreatorPanel and
        // every other TopBarUI panel is built into (see UIBootStrapper.InitializeHud's comment).
        // BringToFront only ever settles sibling order WITHIN one document; across two documents the
        // lower sortingOrder always loses no matter how many times you raise it, so the popup was
        // invisibly stuck behind the Item Creator window regardless of z-order tricks. The actual fix
        // is to temporarily move the popup INTO the caller's own document for as long as the preview
        // is up, then move it back — everywhere else in this class it stays right where Init() put it.
        if (hostRoot != null && _popup != null && _popup.parent != hostRoot)
        {
            if (!_uiPreviewReparented)
            {
                _uiPreviewOriginalParent = _popup.parent;
                _uiPreviewOriginalIndex = _uiPreviewOriginalParent?.IndexOf(_popup) ?? -1;
                _uiPreviewOriginalRoot = _root;
            }
            hostRoot.Add(_popup);
            _root = hostRoot;
            _uiPreviewReparented = true;
        }

        ShowPalletBuilder(sku, caseQty);
        FollowCursor();
        RaisePopupAboveEverything();
    }

    /// <summary>Same technique as RuntimeTooltip.RaiseAboveEverything / Toast.RaiseAboveEverything:
    /// sibling order inside one UIDocument is decided by whoever last called BringToFront, and that
    /// has to be walked up the whole ancestor chain, not just the popup's immediate parent.</summary>
    private void RaisePopupAboveEverything()
    {
        VisualElement e = _popup;
        while (e != null)
        {
            e.BringToFront();
            e = e.parent;
        }
    }

    /// <summary>Companion to <see cref="ShowForUiPreview"/> — also clears the hover-tracking state so
    /// a later real-world pallet hover doesn't think it's still looking at this preview's SKU, and
    /// moves the popup back to its normal home document if it was reparented to show the preview.</summary>
    public void HideUiPreview()
    {
        _pendingPalletBuilderSku = null;
        HideImmediate();

        if (_uiPreviewReparented && _popup != null && _uiPreviewOriginalParent != null)
        {
            if (_uiPreviewOriginalIndex >= 0 && _uiPreviewOriginalIndex <= _uiPreviewOriginalParent.childCount)
                _uiPreviewOriginalParent.Insert(_uiPreviewOriginalIndex, _popup);
            else
                _uiPreviewOriginalParent.Add(_popup);
            _root = _uiPreviewOriginalRoot;
            _uiPreviewReparented = false;
        }
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
    // TRUCK HOVER (regular mouse-over) — same Name/Purchase Cost/Hourly Cost card used for
    // ShippingDoor etc. The detailed vendor/order/appointment card only ever shows for the one
    // truck currently pinned (see ToggleTruckPin) — clicked, not hovered — per Tad's ask.
    // ---------------------------------------------------------
    public void TickHoverTruckSummary(bool hovering, TruckController truck, Vector3 worldPos, Camera cam)
    {
        // The pinned truck's own detailed popup already covers this exact truck's info — a second,
        // overlapping generic card for the same truck would just be redundant.
        if (!hovering || truck == null || truck == _pinnedTruck)
        {
            TickHover(false, null, 0, 0, Vector3.zero, null);
            return;
        }

        TickHover(true, ResolveTruckSummaryName(truck), 0, 0, worldPos, cam, allowOutboundTab: true);
    }

    /// <summary>Best identifying label for a truck's regular hover card — the vendor it's carrying
    /// a shipment for when known, otherwise a generic fallback.</summary>
    private static string ResolveTruckSummaryName(TruckController truck)
    {
        var shipment = truck.AssignedShipment;
        if (shipment != null && !string.IsNullOrEmpty(shipment.SupplierName)) return shipment.SupplierName;
        return truck.IsOutbound ? "Outbound Trailer" : "Truck";
    }

    // ---------------------------------------------------------
    // TRUCK PIN — click a truck to outline it (TruckHighlighter) and pin its tooltip open,
    // anchored above it in world space, until it's clicked again (left OR right click).
    // ---------------------------------------------------------
    private void Update()
    {
        if (_pinnedTruck != null)
        {
            // Any bottom-bar panel (Scheduler, Purchasing, New Item, Work Queue, Contracts, etc.)
            // taking over the screen closes the pin outright — same routine as clicking the truck a
            // second time (UnpinTruck) — rather than leaving it fighting the new panel for z-order.
            // Driven by panel state (AnyPanelOpen) instead of hooking every place a panel can be
            // opened from, since several of them (WorkQueuePanel.OpenScheduler and its siblings on
            // ContractsPanel/PurchasingPanel) call Show() directly instead of routing through
            // UIKeyBindingManager.ToggleUI, so there's no single call site to intercept.
            if (UIKeyBindingManager.Instance != null && UIKeyBindingManager.Instance.AnyPanelOpen)
            {
                UnpinTruck();
                return;
            }

            RenderTruck(_pinnedTruckUI, _pinnedTruck);
            PositionPinnedPopupAtBottomLeft();
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

        // A fresh pin always starts fresh — only dragging it by hand (or the initial placement
        // below) should mark it manually positioned; a brand-new pin shouldn't inherit wherever the
        // popup happened to be left after the LAST truck was dragged around.
        _pinnedPopupManuallyPositioned = false;

        _pinnedTruck = truck;
        RenderTruck(_pinnedTruckUI, truck);
        ShowTruckPopupElement(_pinnedTruckUI.Popup);
        // Fixed near the lower-left corner of the viewport (per Tad's reference mockup) rather than
        // anchored to the truck's world position or centered on the cursor — a world anchor point (a
        // fixed height above the truck's pivot) can land outside the viewport entirely when the
        // camera is zoomed in close on a large truck, even though the truck itself is fully visible,
        // sending the popup flying off-screen. A fixed screen corner is always on-screen and always
        // in the same familiar spot. _pinnedPopupManuallyPositioned stays false here (unlike the old
        // mouse-center placement) so Update() keeps re-anchoring it to that corner every frame — see
        // PositionPinnedPopupAtBottomLeft's comment for why a one-time placement isn't enough — until
        // the player drags it by hand.
        PositionPinnedPopupAtBottomLeft();
        TruckHighlighter.Instance.Highlight(truck);
        AudioManager.Play("UIClick");
    }

    /// <summary>Lets the player grab a pinned truck popup anywhere on it and drag it to a fixed
    /// spot on screen. Once dragged, PositionPinnedPopupAtBottomLeft stops re-anchoring it to the
    /// corner for the rest of this pin — re-pinning (a fresh ToggleTruckPin) resets that. Only wired
    /// onto _pinnedTruckUI, never _hoverTruckUI: the transient hover popup stays click-through so it
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

            // Same clamp FollowCursor/PositionPinnedPopupAtBottomLeft use — left/top are the visual
            // right/bottom edge (see the -100%/-100% translate comment above), so keeping them
            // inside [box size, viewport size] keeps the WHOLE box on screen, not just this one edge.
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

    /// <summary>Fixed screen-space margins for the pinned popup's default position (see
    /// PositionPinnedPopupAtBottomLeft) — 30px in from the left edge of the viewport, 50px above the
    /// top of BuildMenuUI's bottom HUD bar, per Tad's reference mockup.</summary>
    private const float PinnedPopupLeftMargin = 30f;
    private const float PinnedPopupBottomMargin = 50f;

    /// <summary>Keeps the pinned popup sitting in a fixed spot near the lower-left corner of the
    /// viewport — its LEFT edge 30px in from the viewport's left edge, its BOTTOM edge 50px above
    /// the top of BuildMenuUI's bottom HUD bar — instead of anchoring to the truck's world position
    /// or the cursor. Re-run every frame from Update() (like the old world-anchor logic it replaced)
    /// rather than computed once at pin time: RenderTruck can change the popup's own width/height
    /// frame to frame (e.g. its item list growing), and since the box is translated -100%/-100% (see
    /// the comment on _popup in Init()), a stale left/top computed for an OLDER, smaller size would
    /// leave the box's edges anywhere but where they're supposed to be once it resizes. Recomputing
    /// from the CURRENT resolvedStyle size every frame keeps both edges pinned correctly regardless.
    /// Stops entirely once the player drags the popup by hand (_pinnedPopupManuallyPositioned) —
    /// re-pinning (a fresh ToggleTruckPin) resets that.</summary>
    private void PositionPinnedPopupAtBottomLeft()
    {
        if (_pinnedTruckUI == null || _root == null) return;
        if (_pinnedPopupManuallyPositioned) return;

        var layout = _root.layout;
        if (layout.width <= 0 || layout.height <= 0) return;

        var popup = _pinnedTruckUI.Popup;
        float boxWidth = popup.resolvedStyle.width;
        float boxHeight = popup.resolvedStyle.height;
        if (boxWidth <= 0 || boxHeight <= 0) return; // not laid out yet — retried next frame

        // The popup's translate is -100%/-100% (its bottom-right corner sits at left/top), so the
        // box's LEFT edge only lands at PinnedPopupLeftMargin once left is pushed out by the box's
        // own current width, and its BOTTOM edge only lands PinnedPopupBottomMargin above the bar
        // once top is pulled up by that margin plus the bar's own reserved height.
        float left = PinnedPopupLeftMargin + boxWidth;
        float top = layout.height - BuildMenuUI.BottomHudReservedHeight - PinnedPopupBottomMargin;

        // Keep it fully on screen even if the popup somehow ends up larger than the viewport.
        left = Mathf.Clamp(left, boxWidth, layout.width);
        top = Mathf.Clamp(top, boxHeight, layout.height);

        popup.style.left = left;
        popup.style.top = top;
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
        // top-left) to whatever left/top FollowCursor (or PositionPinnedPopupAtBottomLeft) drives it to.
        popup.style.translate = new Translate(Length.Percent(-100f), Length.Percent(-100f), 0f);
        // Position (not Ignore) — this instance is only ever built for the PINNED truck (the
        // transient hover-follow instance was retired, see TickHoverTruckSummary), and a pinned
        // card should block clicks to whatever's underneath it, scoped to exactly its own
        // rectangle — nothing else on screen. Also lets EnablePinnedPopupDragging's pointer
        // callbacks below actually receive events.
        popup.pickingMode = PickingMode.Position;
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
        // Position, not Ignore — the badge hangs partly outside the main popup box (overflow
        // visible), so without this that overhanging sliver would be click-through even though
        // it's visually part of the truck UI.
        doorBadge.pickingMode = PickingMode.Position;
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
        // Door number uses the house display font (Lilita One) per Tad's ask.
        var lilita = LilitaFont();
        if (lilita != null) doorLabel.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(lilita));
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
        // Background spans the full popup width (runs underneath the door badge circle on
        // purpose) — only the text inside it is inset, see commentLabel.style.right below.
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
        // Inset further than the box itself so the text stops before the door badge circle,
        // while the commentBox background (above) keeps running underneath it — per Tad's ask.
        commentLabel.style.right = 44;
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

        // ── Vendor icon frame — a fixed square footprint (the border lives here, so it stays put
        // regardless of how the icon graphic inside is scaled) — big, top-right, sized/positioned
        // where Tad's mockup drew it (below the door badge, roughly spanning the PO/Appt lines)
        // rather than a small inline icon next to the name.
        var vendorIconFrame = new VisualElement();
        ui.VendorIconFrame = vendorIconFrame;
        vendorIconFrame.pickingMode = PickingMode.Ignore;
        vendorIconFrame.style.position = Position.Absolute;
        // Bounding box measured directly off a live capture: top matches the PO number row's top
        // edge, width equals height so the border is a tight square around the icon. Shifted 8px
        // left of the popup's inner (padded) edge per Tad's ask, so it sits close to the popup's
        // right border without touching it. Overflow visible so the icon graphic inside (scaled
        // taller, see below) can bleed past this frame's edges without growing the frame/border
        // itself.
        vendorIconFrame.style.top = 106;
        vendorIconFrame.style.height = 69;
        vendorIconFrame.style.width = 69;
        vendorIconFrame.style.right = 12;
        vendorIconFrame.style.overflow = Overflow.Visible;
        vendorIconFrame.style.display = DisplayStyle.None;
        popup.Add(vendorIconFrame);

        var vendorIcon = new Image();
        ui.VendorIcon = vendorIcon;
        vendorIcon.pickingMode = PickingMode.Ignore;
        vendorIcon.style.width = new Length(100, LengthUnit.Percent);
        vendorIcon.style.height = new Length(100, LengthUnit.Percent);
        // Icon graphic scaled up per Tad's ask — Y already 30% taller, both axes now +10% more
        // on top of that (1.0->1.1 X, 1.3->1.43 Y), then a further +5% on both axes on top of that
        // (1.1->1.155 X, 1.43->1.5015 Y) per follow-up ask, then narrowed back down 7% on both axes
        // (1.155->1.07415 X, 1.5015->1.396395 Y) per a later follow-up ask. Frame position/size and
        // the border below are untouched by this — the border is a separate element painted after
        // this icon (see vendorIconBorder) so it always renders on top and the icon, however far it
        // bleeds past the frame's edges, can never visually cover it.
        vendorIcon.style.scale = new Scale(new Vector3(1.07415f, 1.396395f, 1f));
        vendorIconFrame.Add(vendorIcon);

        // Border painted as its OWN element, added AFTER the icon so it always renders on top of
        // it. The icon above sits at 100% of this same box but is scaled up and the frame has
        // Overflow.Visible, so without this separate top-layer element the icon graphic would paint
        // over and hide the border beneath it.
        var vendorIconBorder = new VisualElement();
        ui.VendorIconBorder = vendorIconBorder;
        vendorIconBorder.pickingMode = PickingMode.Ignore;
        vendorIconBorder.style.position = Position.Absolute;
        vendorIconBorder.style.top = 0;
        vendorIconBorder.style.left = 0;
        vendorIconBorder.style.width = new Length(100, LengthUnit.Percent);
        vendorIconBorder.style.height = new Length(100, LengthUnit.Percent);
        // Thin frame matching the popup's own border width/color (color set per order-type in
        // RenderTruck, alongside ui.Popup's border) per Tad's ask.
        vendorIconBorder.style.borderTopWidth = 4;
        vendorIconBorder.style.borderBottomWidth = 4;
        vendorIconBorder.style.borderLeftWidth = 4;
        vendorIconBorder.style.borderRightWidth = 4;
        vendorIconBorder.style.borderTopLeftRadius = 6;
        vendorIconBorder.style.borderTopRightRadius = 6;
        vendorIconBorder.style.borderBottomLeftRadius = 6;
        vendorIconBorder.style.borderBottomRightRadius = 6;
        vendorIconFrame.Add(vendorIconBorder);

        // Font sizes bumped 50% (11 -> 17) per Tad's ask.
        var orderLabel = new Label { text = "" };
        ui.OrderLabel = orderLabel;
        orderLabel.pickingMode = PickingMode.Ignore;
        orderLabel.style.fontSize = 27; // 21 -> 27, +30% per Tad's ask
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
        body.style.height = 33; // 22 -> 33, +50% on the Y axis per Tad's ask
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
        loadPercentLabel.style.fontSize = 21; // 16 -> 21, +30% more per Tad's ask
        loadPercentLabel.style.color = Color.white;
        body.Add(loadPercentLabel);

        iconRow.Add(body);
        popup.Add(iconRow);

        // ── Total pallet count — heads the manifest, right under the progress bar ──
        var totalPalletsLabel = new Label { text = "" };
        ui.TotalPalletsLabel = totalPalletsLabel;
        totalPalletsLabel.pickingMode = PickingMode.Ignore;
        totalPalletsLabel.style.fontSize = 16;
        totalPalletsLabel.style.color = new Color(0.85f, 0.9f, 0.95f, 1f);
        totalPalletsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        totalPalletsLabel.style.marginBottom = 4;
        popup.Add(totalPalletsLabel);

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
        int totalPalletCount = 0;
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
                // Stays 0 until an offloader actually claims the truck, which is fine for the progress
                // BAR (there's genuinely nothing offloaded yet) but wrong for the TOTAL PALLETS count
                // below — that has to read the full manifest the moment the popup opens, not wait for
                // docking/claiming, so it's computed separately from the same LineItems the item list
                // underneath is built from (see the loop below).
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

                // Read off the manifest built just above, not TotalPalletsAtDock (which stays 0 until
                // an offloader actually claims this truck) — the player expects this count the moment
                // the popup opens, well before the truck has even backed into a door.
                totalPalletCount = items.Sum(it => it.pallets);
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
                totalPalletCount = totalPallets;
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

        // Vendor icon's border (painted on top of the icon, see construction) always matches the
        // popup's own border color, per Tad's ask.
        ui.VendorIconBorder.style.borderTopColor = borderColor;
        ui.VendorIconBorder.style.borderBottomColor = borderColor;
        ui.VendorIconBorder.style.borderLeftColor = borderColor;
        ui.VendorIconBorder.style.borderRightColor = borderColor;

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
            ui.VendorIconFrame.style.display = DisplayStyle.Flex;
        }
        else
        {
            ui.VendorIconFrame.style.display = DisplayStyle.None;
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

        ui.TotalPalletsLabel.text = $"TOTAL PALLETS: {totalPalletCount}";

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
