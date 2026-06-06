using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

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
        _label.text = $"Build Cost: ${cost:N0}";

        // Off-White (#F5F6F9) when affordable; Pop-Orange (#FF7F11) when not
        _label.style.color = canAfford
            ? new StyleColor(new Color(0.961f, 0.965f, 0.976f))
            : new StyleColor(new Color(1f, 0.498f, 0.067f));
    }

    public void SetScreenPosition(Vector3 worldPos, Camera cam)
    {
        if (_label == null || _doc == null || cam == null)
            return;

        // Use the mouse position directly for following if desired, 
        // or world space if pinned to the ghost. The user wants it to follow the cursor.
        Vector2 mousePos = Mouse.current.position.ReadValue();

        var layout = _doc.rootVisualElement.panel.visualTree.layout;
        if (layout.width <= 0 || layout.height <= 0) return;

        // Manual ratio-based mapping
        float uiX = mousePos.x * (layout.width / Screen.width);
        float uiY = (Screen.height - mousePos.y) * (layout.height / Screen.height);

        Vector2 target = new Vector2(uiX, uiY);
        _smoothPos = Vector2.Lerp(_smoothPos, target, 1.0f - Mathf.Exp(-60f * Time.deltaTime));

        // Use translate to avoid layout passes
        _label.style.translate = new Translate(_smoothPos.x, _smoothPos.y, 0);
        }

    public void Hide()
    {
        if (_label != null)
            _label.style.display = DisplayStyle.None;
    }
}
