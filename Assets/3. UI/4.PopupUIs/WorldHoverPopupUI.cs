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
    private float _hoverDelay = 0.1f;
    private float _hoverTimer = 0f;
    private bool _isHovering = false;
    private bool _isVisible = false;
    private bool _isFading = false;

    // Pending data
    private string _pendingName;
    private int _pendingCost;
    private int _pendingHourlyCost;

    private float _disappearGraceTimer = 0f;
    private float _disappearGraceTime = 0.15f;

    // OPTIMIZATION: Reuse a single style allocation to prevent Garbage Collection allocation spikes in Tick/Update loops
    private StyleTranslate _cachedTranslateStyle = new StyleTranslate();

    // ---------------------------------------------------------
    // INITIALIZATION
    // ---------------------------------------------------------
    public void Init(VisualElement populationTarget)
    {
        if (populationTarget == null)
        {
            Debug.LogError("[WorldHoverPopupUI] Passed a null VisualElement reference target!");
            return;
        }

        _popup = populationTarget;
        _root = _popup.panel?.visualTree;
        _title = _popup.Q<Label>("HoverTitle");
        _cost = _popup.Q<Label>("HoverCost");
        _hourlyCost = _popup.Q<Label>("HoverHourlyCost");

        // Ensure the popup itself and its children don't block raycasts/picking
        _popup.pickingMode = PickingMode.Ignore;
        if (_title != null) _title.pickingMode = PickingMode.Ignore;
        if (_cost != null) _cost.pickingMode = PickingMode.Ignore;
        if (_hourlyCost != null) _hourlyCost.pickingMode = PickingMode.Ignore;

        HideImmediate();
    }

    public void SetFSM(PlacementStateMachine fsm)
    {
        _fsm = fsm;
    }

    // ---------------------------------------------------------
    // MAIN UPDATE
    // ---------------------------------------------------------
    public void TickHover(bool hovering, string name, int cost, int hourlyCost, Vector3 worldPos, Camera cam)
    {
        // 1. Popup ONLY allowed in IdleState
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        {
            HideImmediate();
            return;
        }

        // 2. Handle non-hovering state: Show placeholder instead of hiding to maintain layout
        if (!hovering || string.IsNullOrEmpty(name))
        {
            _isHovering = false;
            _hoverTimer = 0f;
            ShowEmpty();
            return;
        }

        _disappearGraceTimer = 0f;

        // -----------------------------------------------------
        // HOVERING LOGIC
        // -----------------------------------------------------
        bool isNewTarget = !_isHovering || name != _pendingName || cost != _pendingCost || hourlyCost != _pendingHourlyCost;
        _pendingName = name;
        _pendingCost = cost;
        _pendingHourlyCost = hourlyCost;

        if (isNewTarget)
        {
            _isHovering = true;
            _hoverTimer = 0f;
            if (_isFading)
            {
                _isFading = false;
                _popup.style.opacity = 1f;
            }
            Show(_pendingName, _pendingCost, _pendingHourlyCost);
        }
        else
        {
            _hoverTimer += Time.deltaTime;
            // If not visible, show after delay. If visible, STAY visible.
            if (!_isVisible && _hoverTimer >= _hoverDelay)
                Show(_pendingName, _pendingCost, _pendingHourlyCost);
        }

        if (_isVisible)
            SetWorldPosition(worldPos, cam);
    }

    // ---------------------------------------------------------
    // VISUALS
    // ---------------------------------------------------------
    private void Show(string name, int cost, int hourlyCost)
    {
        if (_popup == null || _title == null || _cost == null || _hourlyCost == null) return;

        _title.text = name;
        _cost.text = $"Cost: ${cost:N0}";
        _hourlyCost.text = $"Hourly: ${hourlyCost:N0}/hr";

        _popup.style.opacity = 1f;
        _popup.style.display = DisplayStyle.Flex;
        _popup.AddToClassList("show");
        _isVisible = true;
        _isFading = false;
    }

    private void ShowEmpty()
    {
        if (_popup == null || _title == null || _cost == null || _hourlyCost == null) return;

        _title.text = "No Data to Show";
        _cost.text = "Cost: --";
        _hourlyCost.text = "Hourly: --";

        _popup.style.opacity = 1f;
        _popup.style.display = DisplayStyle.Flex;
        _isVisible = true;
        _isFading = false;
    }

    private void HideSlowlyFadeout()
    {
        if (!_isVisible) return;
        _isVisible = false;
        
        HideImmediate();
    }

    public void HideImmediate()
    {
        if (_popup == null) return;

        _isVisible = false;
        _isHovering = false;
        _hoverTimer = 0f;
        _isFading = false;
        
        _popup.style.display = DisplayStyle.None;
        _popup.RemoveFromClassList("show");
    }

    private void ResetToPlaceholder()
    {
        if (_popup == null || _title == null || _cost == null || _hourlyCost == null) return;

        _title.text = "Inspect Warehouse Item";
        _cost.text = "Cost: --";
        _hourlyCost.text = "Hourly: --";
        
        _popup.style.opacity = 1f;
        _popup.style.display = DisplayStyle.None;
        _popup.RemoveFromClassList("show");
    }

    public void SetWorldPosition(Vector3 worldPos, Camera cam)
    {
        if (_popup == null || _root == null) return;

        // If the popup is stationed in the BottomBar (BuildMenuUI style), don't move it
        if (_popup.ClassListContains("buildmenu-stationed-popup"))
        {
            _popup.style.translate = StyleKeyword.Initial;
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
var layout = _root.layout;
        if (layout.width <= 0 || layout.height <= 0) return;

        float uiX = mousePos.x * (layout.width / Screen.width);
        float uiY = (Screen.height - mousePos.y) * (layout.height / Screen.height);
        Vector2 target = new Vector2(uiX, uiY);

        // Responsive smoothing
        _smoothPos = Vector2.Lerp(_smoothPos, target, 1.0f - Mathf.Exp(-60f * Time.deltaTime));

        // OPTIMIZATION: Write parameters cleanly using reused caching references
        _cachedTranslateStyle.value = new Translate(_smoothPos.x, _smoothPos.y, 0);
        _popup.style.translate = _cachedTranslateStyle;
    }
}
