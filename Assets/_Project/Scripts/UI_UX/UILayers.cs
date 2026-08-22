/// <summary>
/// UIDocument sortingOrder values for the layers that have to agree with each other.
///
/// Every runtime UI here shares one PanelSettings, so documents are drawn in sortingOrder order and
/// these numbers are the only thing deciding what covers what. They were previously scattered as
/// literals across the panels that own them (50 / 95 / 99 / 120 / 999999), which is how the top bar
/// ended up permanently above three windows that were supposed to cover it.
///
/// Bottom to top:
///   … build menu (120) and the other incidental panels keep their own local values …
///   Hud (999999)          — the top bar, plus every panel built into the HUD document (keys 5–9).
///   WindowAboveHud        — windows that own a separate document and must still cover the top bar.
///   UIToast.ToastSortingOrder — the toast, alone on top.
///
/// The gap between the tiers is deliberate: it leaves room to slot something in without renumbering.
/// </summary>
public static class UILayers
{
    /// <summary>The HUD document — top bar and the panels built into it.</summary>
    public const float Hud = 999999f;

    /// <summary>
    /// Windows with their own UIDocument that must draw over BOTH bars, matching the panels built
    /// into the HUD document. Anything using this still sits below the toast.
    /// </summary>
    public const float WindowAboveHud = 1000000f;
}
