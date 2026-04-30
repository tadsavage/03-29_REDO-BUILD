using UnityEngine;
using UnityEngine.UIElements;

public class PreviewCostUI : MonoBehaviour
{
    private Label _label;
    private UIDocument _doc;

    // Smooth UI position
    private Vector2 _smoothPos;

    public void Init(UIDocument doc)
    {
        _doc = doc;
        _label = doc.rootVisualElement.Q<Label>("PreviewCostLabel");
        Hide();
    }

    public void ShowCost(int cost, bool canAfford)
    {
        if (_label == null)
            return;

        _label.style.display = DisplayStyle.Flex;
        _label.text = $"Drag Cost: ${cost:N0}";

        _label.style.color = canAfford
            ? new StyleColor(new Color(.9f, 0.5f, 0.5f))
            : new StyleColor(Color.red);
    }

    public void SetScreenPosition(Vector3 worldPos, Camera cam)
    {
        if (_label == null || _doc == null || cam == null)
            return;

        // World → Screen
        Vector3 screenPos3 = cam.WorldToScreenPoint(worldPos);
        Vector2 screenPos = new Vector2(screenPos3.x, screenPos3.y);

        // Screen → Panel (bottom-left origin)
        var panel = _doc.rootVisualElement.panel;
        if (panel == null)
            return;

        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);

        // Panel → UI Toolkit (top-left origin)
        float rootHeight = _doc.rootVisualElement.resolvedStyle.height;
        float uiX = panelPos.x;
        float uiY = rootHeight - panelPos.y;

        // Smooth follow
        Vector2 target = new Vector2(uiX, uiY);
        _smoothPos = Vector2.Lerp(_smoothPos, target, Time.deltaTime * 20f);

        // Offset (tweak to taste)
        float offsetX = -40f;
        float offsetY = -40f;

        _label.style.left = _smoothPos.x + offsetX;
        _label.style.top = _smoothPos.y + offsetY;
    }

    public void Hide()
    {
        if (_label != null)
            _label.style.display = DisplayStyle.None;
    }
}
