using UnityEngine.UIElements;

public static class UIInputGuard
{
    /// <summary>
    /// Returns true if any UI Toolkit TextField currently has
    /// keyboard focus — meaning the user is typing into a text field.
    /// </summary>
    public static bool IsTextFieldFocused
    {
        get
        {
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                UnityEngine.FindObjectsSortMode.None);
            foreach (var doc in docs)
            {
                if (doc.rootVisualElement == null) continue;
                var focused = doc.rootVisualElement.focusController?.focusedElement;
                if (focused is TextField )
                    return true;
                // TextField's inner input element
                if (focused != null &&
                    focused.GetType().Name.Contains("TextInput"))
                    return true;
            }
            return false;
        }
    }
}
