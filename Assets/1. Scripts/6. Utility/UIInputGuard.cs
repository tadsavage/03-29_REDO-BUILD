using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public static class UIInputGuard
{
    private static List<UIDocument> _cachedDocs = new List<UIDocument>();
    private static float _lastRefresh;

    private static void RefreshDocs()
    {
        if (Time.realtimeSinceStartup - _lastRefresh < 2.0f && _cachedDocs.Count > 0) return;
        
        _cachedDocs = new List<UIDocument>(Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None));
        _lastRefresh = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// Returns true if any UI Toolkit TextField currently has
    /// keyboard focus.
    /// </summary>
    public static bool IsTextFieldFocused
    {
        get
        {
            RefreshDocs();

            foreach (var doc in _cachedDocs)
            {
                if (doc == null || doc.rootVisualElement == null) continue;
                var focused = doc.rootVisualElement.focusController?.focusedElement;
                if (focused is TextField)
                    return true;
                
                if (focused != null && focused.GetType().Name.Contains("TextInput"))
                    return true;
            }
            return false;
        }
    }
}