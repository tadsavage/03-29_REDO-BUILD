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
        _label.text = $"Drag Cost: ${cost:N0}";

        _label.style.color = canAfford
            ? new StyleColor(new Color(.9f, 0.5f, 0.5f)) // soft red
            : new StyleColor(Color.red);
    }
    new 

    public void Hide()
    {
        if (_label != null)
            _label.style.display = DisplayStyle.None;
    }
}
