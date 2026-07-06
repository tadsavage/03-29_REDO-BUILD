using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using GameCore.Inventory;
using GameCore.Services;

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
        _isPalletMode = false;

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
        _isPalletMode = true;

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

            // Pick slot assignment
            var pickSlotStr = GetPickSlotAssignment(pallet.CurrentLocation);
            var pickLbl = new Label($"Pick Slot: {pickSlotStr}");
            pickLbl.style.color = new Color(0.8f, 0.9f, 1f, 1f);
            pickLbl.style.fontSize = 12;
            _palletInfoPanel.Add(pickLbl);
        }

        // Hide building-specific elements
        if (_cost != null) _cost.style.display = DisplayStyle.None;
        if (_hourlyCost != null) _hourlyCost.style.display = DisplayStyle.None;

        _popup.style.opacity = 1f;
        _popup.style.display = DisplayStyle.Flex;
        _popup.AddToClassList("show");
        _isVisible = true;
    }

    private string GetPickSlotAssignment(Vector2Int location)
    {
        // Try to get pick slot from SlotRegistry (static)
        var address = LaneNamingService.AddressAt(location) ?? location.ToString();
        if (SlotRegistry.TryGet(address, out var slot))
            return address;
        return "None assigned";
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

    // Legacy entry point kept for any code that still calls it
    public void SetWorldPosition(Vector3 worldPos, Camera cam) => FollowCursor();
}
