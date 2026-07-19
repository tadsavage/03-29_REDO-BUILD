using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using GameCore.Inventory;
using GameCore.Services;
using System.Linq;

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

    // OPTIMIZATION: reuse allocation to avoid GC spikes in Tick/Update
    private StyleTranslate _cachedTranslateStyle = new StyleTranslate();

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
            _hoverTimer += Time.deltaTime;
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
            _hoverTimer += Time.deltaTime;
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
            _hoverTimer += Time.deltaTime;
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

        _title.text      = name;
        _cost.text       = $"Cost: ${cost:N0}";
        _hourlyCost.text = $"Hourly: ${hourlyCost:N0}/hr";

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

        // Set title to slot address
        if (_title != null)
            _title.text = $"Location: {location.Address}";

        // Hide icon for locations
        if (_iconImage != null) _iconImage.style.display = DisplayStyle.None;

        // Build location info text
        if (_palletInfoPanel != null)
        {
            _palletInfoPanel.Clear();
            _palletInfoPanel.style.display = DisplayStyle.Flex;

            // Type (Pick/Reserve)
            var typeLbl = new Label($"Type: {location.Type}");
            typeLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            typeLbl.style.fontSize = 12;
            _palletInfoPanel.Add(typeLbl);

            // Height (Level)
            var rackPO = location.GetComponentInParent<PlacedObject>();
            if (rackPO != null && rackPO.rackLevelIndex >= 0)
            {
                var levelLbl = new Label($"Level: {rackPO.rackLevelIndex} ({(location.WorldPosition.y):F2}m)");
                levelLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                levelLbl.style.fontSize = 12;
                _palletInfoPanel.Add(levelLbl);
            }
            else
            {
                var heightLbl = new Label($"Height: {location.WorldPosition.y:F2}m");
                heightLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
                heightLbl.style.fontSize = 12;
                _palletInfoPanel.Add(heightLbl);
            }

            // Status
            var statusLbl = new Label($"Status: {location.Status}");
            statusLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            statusLbl.style.fontSize = 12;
            _palletInfoPanel.Add(statusLbl);

            // Assignment / SKU
            if (!string.IsNullOrEmpty(location.SkuId))
            {
                InventoryService inventory = null;
                ServiceLocator.TryGet<InventoryService>(out inventory);
                var sku = inventory?.GetSkuData(location.SkuId);

                var assignedLbl = new Label($"SKU: {location.SkuId}");
                assignedLbl.style.color = Color.yellow;
                assignedLbl.style.fontSize = 12;
                _palletInfoPanel.Add(assignedLbl);

                if (sku != null)
                {
                    var descLbl = new Label(sku.ItemDescription);
                    descLbl.style.color = Color.white;
                    descLbl.style.fontSize = 11;
                    _palletInfoPanel.Add(descLbl);
                }

                if (location.Quantity > 0)
                {
                    var qtyLbl = new Label($"Stock: {location.Quantity} cases");
                    qtyLbl.style.color = Color.white;
                    qtyLbl.style.fontSize = 11;
                    _palletInfoPanel.Add(qtyLbl);
                }
            }
            else
            {
                var unassignedLbl = new Label("Unassigned / Empty");
                unassignedLbl.style.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                unassignedLbl.style.fontSize = 12;
                _palletInfoPanel.Add(unassignedLbl);
            }
        }

        // Hide building-specific elements
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
        if (_popup == null) return;
        _isVisible  = false;
        _isHovering = false;
        _hoverTimer = 0f;
        _popup.style.display = DisplayStyle.None;
        _popup.RemoveFromClassList("show");
    }

    // ---------------------------------------------------------
    // CURSOR FOLLOWING (+50px right, +50px down)
    // ---------------------------------------------------------
    private void FollowCursor()
    {
        if (_popup == null || _root == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        var layout = _root.layout;
        if (layout.width <= 0 || layout.height <= 0) return;

        float scaleX = layout.width  / Screen.width;
        float scaleY = layout.height / Screen.height;

        float uiX = mousePos.x * scaleX + 15f * scaleX;
        float uiY = (Screen.height - mousePos.y) * scaleY + 20f * scaleY;

        Vector2 target = new Vector2(uiX, uiY);
        _smoothPos = Vector2.Lerp(_smoothPos, target, 1f - Mathf.Exp(-60f * Time.deltaTime));

        _cachedTranslateStyle.value = new Translate(_smoothPos.x, _smoothPos.y, 0);
        _popup.style.translate = _cachedTranslateStyle;
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
            _hoverTimer += Time.deltaTime;
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

    // Legacy entry point kept for any code that still calls it
    public void SetWorldPosition(Vector3 worldPos, Camera cam) => FollowCursor();
}
