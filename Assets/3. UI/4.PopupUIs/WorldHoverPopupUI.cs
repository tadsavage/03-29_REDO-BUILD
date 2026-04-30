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

    // Delay system
    private float _hoverDelay = 0.35f;   // tweak to taste
    private float _hoverTimer = 0f;
    private bool _isHovering = false;
    private bool _isVisible = false;

    // Data to show after delay
    private string _pendingName;
    private int _pendingCost;
    private int _pendingHourlyCost;

    public void Init(UIDocument doc)
    {
        _root = doc.rootVisualElement;
        _popup = _root.Q<VisualElement>("WorldHoverPopup");
        _title = _root.Q<Label>("HoverTitle");
        _cost = _root.Q<Label>("HoverCost");
        _hourlyCost = _root.Q<Label>("HoverHourlyCost");

        HideImmediate();
    }

    // Called every frame from BuildState
    public void TickHover(bool hovering, string name, int cost, int hourlyCost, Vector3 worldPos, Camera cam)
    {
        if (hovering)
        {
            // Store data for when delay completes
            _pendingName = name;
            _pendingCost = cost;
            _pendingHourlyCost = hourlyCost;

            if (!_isHovering)
            {
                // Just started hovering
                _hoverTimer = 0f;
                _isHovering = true;
            }
            else
            {
                // Continue counting
                _hoverTimer += Time.deltaTime;

                if (!_isVisible && _hoverTimer >= _hoverDelay)
                {
                    Show(_pendingName, _pendingCost, _pendingHourlyCost);
                }
            }

            if (_isVisible)
                SetWorldPosition(worldPos, cam);
        }
        else
        {
            // Reset everything
            _isHovering = false;
            _hoverTimer = 0f;

            if (_isVisible)
                Hide();
        }
    }

    private void Show(string name, int cost, int hourlyCost)
    {
        _title.text = name;
        _cost.text = $"Cost: ${cost:N0}";
        _hourlyCost.text = $"Hourly Cost: ${hourlyCost:N0}";
        _popup.AddToClassList("show");
        _isVisible = true;
    }

    private void Hide()
    {
        _popup.RemoveFromClassList("show");
        _isVisible = false;
    }

    private void HideImmediate()
    {
        _popup.RemoveFromClassList("show");
        _isVisible = false;
        _isHovering = false;
        _hoverTimer = 0f;
    }

    public void SetWorldPosition(Vector3 worldPos, Camera cam)
    {
        if (_popup == null || cam == null)
            return;

        // World → Screen
        Vector3 screenPos3 = cam.WorldToScreenPoint(worldPos);
        Vector2 screenPos = new Vector2(screenPos3.x, screenPos3.y);

        // Screen → Panel
        var panel = _root.panel;
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);

        // Panel → UI Toolkit (top-left)
        float rootHeight = _root.resolvedStyle.height;
        float uiX = panelPos.x;
        float uiY = rootHeight - panelPos.y;

        // Smooth follow
        Vector2 target = new Vector2(uiX, uiY);
        _smoothPos = Vector2.Lerp(_smoothPos, target, Time.deltaTime * 20f);

        // Offset above object
        float offsetX = 15f;
        float offsetY = -50f;

        _popup.style.left = _smoothPos.x + offsetX;
        _popup.style.top = _smoothPos.y + offsetY;
    }
    public void ForceHide()
    {
        _popup.RemoveFromClassList("show");
        _isVisible = false;
        _isHovering = false;
        _hoverTimer = 0f;
    }
}
