using UnityEngine;
using UnityEngine.InputSystem;
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
    /// Returns true if the mouse pointer is currently over any pickable
    /// UI Toolkit element across all active UIDocuments.
    /// Handles what EventSystem.IsPointerOverGameObject() misses for UI Toolkit.
    /// </summary>
    public static bool IsPointerOverUIToolkit()
    {
        if (Mouse.current == null) return false;
        RefreshDocs();
        var screenPos = Mouse.current.position.ReadValue();
        foreach (var doc in _cachedDocs)
        {
            if (doc == null || doc.rootVisualElement?.panel == null) continue;
            var panelPos = RuntimePanelUtils.ScreenToPanel(doc.rootVisualElement.panel, screenPos);
            if (doc.rootVisualElement.panel.Pick(panelPos) != null) return true;
        }
        return false;
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