using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class WorldHoverPopupUI : MonoBehaviour
{
    private VisualElement _root;
    private VisualElement _popup;
    private Label _title;
    private Label _cost;
    private Label _hourlyCost;
    private Vector2 _smoothPos;
    private PlacementStateMachine _fsm;

    // Hover timing
    private float _hoverDelay    = 0.1f;
    private float _hoverTimer    = 0f;
    private bool  _isHovering    = false;
    private bool  _isVisible     = false;

    // Pending data
    private string _pendingName;
    private int    _pendingCost;
    private int    _pendingHourlyCost;

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

        _popup.pickingMode = PickingMode.Ignore;
        if (_title      != null) _title.pickingMode      = PickingMode.Ignore;
        if (_cost       != null) _cost.pickingMode       = PickingMode.Ignore;
        if (_hourlyCost != null) _hourlyCost.pickingMode = PickingMode.Ignore;

        HideImmediate();
    }

    public void SetFSM(PlacementStateMachine fsm) => _fsm = fsm;

    // ---------------------------------------------------------
    // MAIN UPDATE
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

        bool isNewTarget = !_isHovering
            || name != _pendingName
            || cost != _pendingCost
            || hourlyCost != _pendingHourlyCost;

        _pendingName      = name;
        _pendingCost      = cost;
        _pendingHourlyCost = hourlyCost;

        if (isNewTarget)
        {
            _isHovering = true;
            _hoverTimer = 0f;
            _popup.style.opacity = 1f;
            Show(_pendingName, _pendingCost, _pendingHourlyCost);
        }
        else
        {
            _hoverTimer += Time.deltaTime;
            if (!_isVisible && _hoverTimer >= _hoverDelay)
                Show(_pendingName, _pendingCost, _pendingHourlyCost);
        }

        if (_isVisible)
            FollowCursor();
    }

    // ---------------------------------------------------------
    // VISUALS
    // ---------------------------------------------------------
    private void Show(string name, int cost, int hourlyCost)
    {
        if (_popup == null || _title == null || _cost == null || _hourlyCost == null) return;

        _title.text      = name;
        _cost.text       = $"Cost: ${cost:N0}";
        _hourlyCost.text = $"Hourly: ${hourlyCost:N0}/hr";

        _popup.style.opacity = 1f;
        _popup.style.display = DisplayStyle.Flex;
        _popup.AddToClassList("show");
        _isVisible = true;
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
