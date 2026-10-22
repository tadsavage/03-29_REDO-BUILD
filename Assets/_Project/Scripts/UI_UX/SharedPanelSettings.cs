using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Finds the PanelSettings the game's UI documents share, for code-built UIs that borrow it instead
/// of having their own. Skips documents whose PanelSettings is null.
///
/// That skip matters in a BUILD: the game starts in MainMenu, and self-bootstrapping UIs created
/// there (ConfirmationModal) can end up with null PanelSettings. A plain
/// FindAnyObjectByType&lt;UIDocument&gt;().panelSettings could return that null document, so the
/// borrowing UI never got a panel and never drew. The Editor hid this because Play starts in Main.
/// </summary>
public static class SharedPanelSettings
{
    public static PanelSettings Find()
    {
        foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
            if (doc != null && doc.panelSettings != null) return doc.panelSettings;
        return null;
    }
}
