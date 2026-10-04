using System;
using UnityEngine;

/// <summary>
/// "Dirty Dev": the single switch for NSFW content (Tad, 2026-10-03). Anything TAGGED NSFW - an avatar part flagged in the AOD, or any
/// scene object carrying an <see cref="NsfwObject"/> component - is hidden from the game unless this is ON. Nothing is deleted or
/// unassigned; flipping it back brings everything straight back.
///
///   Dirty Dev OFF (the default, and what a shipped build always starts with) = NSFW content is HIDDEN. Safe for children.
///   Dirty Dev ON                                                            = NSFW content is visible.
///
/// The setting is remembered between sessions (PlayerPrefs) so a developer who turned it on does not have to repeat it, but it defaults to
/// OFF everywhere it has never been set, including every fresh build. The only UI that flips it is the AOD (editor-only tool).
/// </summary>
public static class DirtyDev
{
    private const string PrefKey = "DirtyDev";

    /// <summary>Raised after the setting changes so live content can re-apply itself.</summary>
    public static event Action Changed;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
        set
        {
            if (Enabled == value) return;
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>True if content with this NSFW flag should currently be shown. Untagged content is always visible.</summary>
    public static bool IsVisible(bool nsfw) => !nsfw || Enabled;

    /// <summary>The NSFW tag lives in the Blender mesh name: <c>gender_slot_variant_nsfw</c>, e.g. <c>Female_Head_Gagged_NSFW</c>. The fourth
    /// segment is "nsfw" for tagged content and blank/absent for everything else. The name is the single source of truth (Tad, 2026-10-03):
    /// nothing is stored per part, so renaming the mesh in Blender and re-exporting is the whole workflow.</summary>
    public static bool IsNsfwName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return false;
        var seg = objectName.Split('_');
        for (int i = 3; i < seg.Length; i++)
            if (string.Equals(seg[i], "nsfw", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
