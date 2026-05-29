using UnityEngine;
using UnityEngine.UIElements;
using Unity.AppUI.UI;
using Button = Unity.AppUI.UI.Button;

public class BasicAppUIHandler : MonoBehaviour
{
    private void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        var root = uiDocument.rootVisualElement;
        
        // App UI ActionButtons can be found by name
        var exampleButton = root.Q<ActionButton>("exampleButton");
        if (exampleButton != null)
        {
            exampleButton.clicked += () => Debug.Log("App UI ActionButton Clicked!");
        }

        var settingsButton = root.Q<ActionButton>("settingsButton");
        if (settingsButton != null)
        {
            settingsButton.clicked += () => Debug.Log("Settings Button Clicked!");
        }
    }
}
