using UnityEngine;
using GameCore.Inventory;

/// <summary>
/// Tiny UI-layer helper converting a PartnershipTierProfile's hex colour into a UnityEngine.Color,
/// and formatting the semantic status text shown next to the numeric Partnership Level. Kept
/// separate from PartnershipTierUtility (Core/Inventory) because Color/ColorUtility are UI-adjacent
/// concerns the backend economy math has no reason to depend on.
/// </summary>
public static class PartnershipColorUtility
{
    public static Color GetColor(int partnershipLevel)
    {
        var profile = PartnershipTierUtility.GetProfile(partnershipLevel);
        return ColorUtility.TryParseHtmlString(profile.HexColor, out var c) ? c : Color.white;
    }

    public static string GetStatusText(int partnershipLevel)
        => PartnershipTierUtility.GetProfile(partnershipLevel).DisplayLabel;
}
