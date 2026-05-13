using UnityEngine;
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
    private float _notMovingTimer = 0f;
    private float _allowedRestingTime = 2f;

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

        // 3. If not hovering → fade out
        if (!hovering)
        {
            _isHovering = false;
            _hoverTimer = 0f;
            _notMovingTimer = 0f;

            if (_isVisible)
                HideSlowlyFadeout();

            return;
        }

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
            // Reset timers
            _isHovering = true;
            _hoverTimer = 0f;
            _notMovingTimer = 0f;

            // Cancel fade if needed
            if (_isFading)
            {
                _isFading = false;
                _popup.style.opacity = 1f;
            }

            Show(_pendingName, _pendingCost, _pendingHourlyCost);
        }
        else
        {
            // Same target → track hover time
            _hoverTimer += Time.deltaTime;
            _notMovingTimer += Time.deltaTime;

            // Fade out after resting too long
            if (_isVisible && !_isFading && _notMovingTimer > _allowedRestingTime)
                HideSlowlyFadeout();

            // Show after delay
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

        _popup.style.opacity = 1f;
        _popup.AddToClassList("show");

        _isVisible = true;
        _isFading = false;
        _notMovingTimer = 0f;
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

        }).Every(16).Until(() => t >= 1f);
    }

    public void HideImmediate()
    {
        _popup.RemoveFromClassList("show");
        _popup.style.opacity = 0f;

        _isVisible = false;
        _isHovering = false;
        _hoverTimer = 0f;
        _notMovingTimer = 0f;
        _isFading = false;
    }
    public void SetWorldPosition(Vector3 worldPos, Camera cam)
    {
        if (_popup == null || cam == null)
            return;

        Vector3 screenPos3 = cam.WorldToScreenPoint(worldPos);
        Vector2 screenPos = new Vector2(screenPos3.x, screenPos3.y);

        var panel = _root.panel;
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);

        float uiX = panelPos.x;
        float uiY = panel.visualTree.layout.height - panelPos.y;

        Vector2 target = new Vector2(uiX, uiY);
        _smoothPos = Vector2.Lerp(_smoothPos, target, Time.deltaTime * 20f);

        float offsetX = 15f;
        float offsetY = -50f;

        _popup.style.left = _smoothPos.x + offsetX;
        _popup.style.top = _smoothPos.y + offsetY;
    }
}
