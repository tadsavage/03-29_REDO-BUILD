using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

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
    //private float _notMovingTimer = 0f;
    //private float _allowedRestingTime = 2f;

    private bool _isHovering = false;
    private bool _isVisible = false;
    private bool _isFading = false;

    // Pending data
    private string _pendingName;
    private int _pendingCost;
    private int _pendingHourlyCost;

    // ---------------------------------------------------------
    // INITIALIZATION
    // ---------------------------------------------------------
    public void Init(UIDocument doc)
    {
        _root = doc.rootVisualElement;
        _popup = _root.Q<VisualElement>("WorldHoverPopup");
        _title = _root.Q<Label>("HoverTitle");
        _cost = _root.Q<Label>("HoverCost");
        _hourlyCost = _root.Q<Label>("HoverHourlyCost"); 
        
        if (_popup == null)
            Debug.LogError("Popup NOT FOUND in HUD document!");

        HideImmediate();
    }

    public void SetFSM(PlacementStateMachine fsm)
    {
        _fsm = fsm;
    }

    private float _disappearGraceTimer = 0f;
    private float _disappearGraceTime = 0.15f; 

    // ---------------------------------------------------------
    // MAIN UPDATE
    // ---------------------------------------------------------
    public void TickHover(bool hovering, string name, int cost, int hourlyCost, Vector3 worldPos, Camera cam)
    {
        // Popup ONLY allowed in IdleState
        if (_fsm != null && !(_fsm.CurrentState is IdleState))
        {
            HideImmediate();
            return;
        }

        // 2. If object vanished (deleted/moved)
        if (hovering && string.IsNullOrEmpty(name))
        {
            HideImmediate();
            return;
        }

        // 3. Grace period for disappearing
        if (!hovering)
        {
            _disappearGraceTimer += Time.deltaTime;
            if (_disappearGraceTimer < _disappearGraceTime && _isVisible)
            {
                SetWorldPosition(worldPos, cam);
                return;
            }

            _isHovering = false;
            _hoverTimer = 0f;

            if (_isVisible)
                HideSlowlyFadeout();

            return;
        }

        _disappearGraceTimer = 0f;

        // -----------------------------------------------------
        // HOVERING LOGIC
        // -----------------------------------------------------
        bool isNewTarget =
            !_isHovering ||
            name != _pendingName ||
            cost != _pendingCost ||
            hourlyCost != _pendingHourlyCost;

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
        _title.text = name;
        _cost.text = $"Cost: ${cost:N0}";
        _hourlyCost.text = $"Hourly Cost: ${hourlyCost:N0}";

            Vector2 mousePos = Mouse.current.position.ReadValue();
            var layout = _root.panel.visualTree.layout;

            if (layout.width > 0 && layout.height > 0)
            {
                float uiX = mousePos.x * (layout.width / Screen.width);
                float uiY = (Screen.height - mousePos.y) * (layout.height / Screen.height);
                _smoothPos = new Vector2(uiX, uiY);

                // Snap the popup position instantly
                _popup.style.left = _smoothPos.x;
                _popup.style.top = _smoothPos.y;
            }

        _popup.style.opacity = 1f;
        _popup.AddToClassList("show");

        _isVisible = true;
        _isFading = false;
    }

    private void HideSlowlyFadeout()
    {
        if (!_isVisible)
            return;

        _isFading = true;
        _isVisible = false;

        float duration = 0.05f;
        float t = 0f;

        _popup.schedule.Execute(() =>
        {
            if (!_isFading)
                return;

            t += Time.deltaTime / duration;
            float opacity = Mathf.Lerp(1f, 0f, t);
            _popup.style.opacity = opacity;

            if (t >= 1f)
            {
                _popup.style.opacity = 0f;
                _popup.RemoveFromClassList("show");
                _isFading = false;
            }

        }).Every(16).Until(() => t >= 1f || !_isFading);
    }

    public void HideImmediate()
    {
        _popup.RemoveFromClassList("show");
        _popup.style.opacity = 0f;

        _isVisible = false;
        _isHovering = false;
        _hoverTimer = 0f;
        _isFading = false;
    }

    public void SetWorldPosition(Vector3 worldPos, Camera cam)
    {
        if (_popup == null || _root == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        var layout = _root.panel.visualTree.layout;
        if (layout.width <= 0 || layout.height <= 0) return;

        // Manual ratio-based mapping to guarantee direction and scale
        // Screen (0,0) is bottom-left. UITK (0,0) is top-left.
        float uiX = mousePos.x * (layout.width / Screen.width);
        float uiY = (Screen.height - mousePos.y) * (layout.height / Screen.height);

        Vector2 target = new Vector2(uiX, uiY);
        
        // High-responsiveness smoothing
        _smoothPos = Vector2.Lerp(_smoothPos, target, 1.0f - Mathf.Exp(-60f * Time.deltaTime));

        // Zero offsets as requested
        _popup.style.left = _smoothPos.x;
        _popup.style.top = _smoothPos.y;
        }
}
