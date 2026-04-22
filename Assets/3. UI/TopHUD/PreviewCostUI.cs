using UnityEngine;
using UnityEngine.UIElements;

public class PreviewCostUI : MonoBehaviour
{
    private Label _label;

    public void Init(UIDocument doc)
    {
        _label = doc.rootVisualElement.Q<Label>("PreviewCostLabel");
        Hide();
    }

    public void ShowCost(int cost, bool canAfford)
    {
        if (_label == null)
            return;

        _label.style.display = DisplayStyle.Flex;
        _label.text = $"${cost}";

        _label.style.color = canAfford
            ? new StyleColor(Color.white)
            : new StyleColor(new Color(1f, 0.25f, 0.25f)); // soft red
    }

    public void Hide()
    {
        if (_label != null)
            _label.style.display = DisplayStyle.None;
    }
}
