using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Green Lilita reward text that grows out of the DISPATCH ORDER button, swells to full size
/// around mid-screen, then floats off the left edge of the viewport — 2 seconds total.
///
/// Built the same way UnhappyCustomerFx is, for the same reason: it has to render above a
/// full-screen modal, so it's added as an absolutely positioned child of that modal's own overlay
/// rather than to the button itself (which would clip it the moment it grew past the button's own
/// bounds).
/// </summary>
public static class DispatchRewardFx
{
    /// <summary>Total run of the text, in ms — per Tad's explicit ask.</summary>
    private const int DurationMs = 2000;

    /// <summary>Fraction of the run spent growing from the button out to mid-screen. The remainder
    /// is spent floating off screen to the left at full size.</summary>
    private const float GrowPhaseFraction = 0.4f;

    private const float StartFontSize = 12f;
    private const float PeakFontSize = 50f;

    /// <summary>How far past the viewport's left edge the text travels before being torn down —
    /// far enough that it's fully clear of the screen, not just touching the edge.</summary>
    private const float OffscreenMarginPx = 300f;

    /// <summary>Fraction into the float-away phase where the fade-out begins.</summary>
    private const float FadeStart = 0.7f;

    private static readonly Color RewardGreen = new Color(0.30f, 0.86f, 0.37f);

    /// <summary>
    /// Plays the reward text over <paramref name="anchor"/> (the DISPATCH ORDER button that was
    /// clicked). <paramref name="root"/> must be an element that spans the full viewport and
    /// outlives the animation — the panel's own full-screen overlay, not the modal box itself.
    /// Degrades to doing nothing on a null root/anchor or an unlaid-out anchor, since this is pure
    /// decoration and must never be able to break the dispatch it's celebrating.
    /// </summary>
    public static void Play(VisualElement root, VisualElement anchor, string text)
    {
        if (root == null || anchor == null || string.IsNullOrEmpty(text)) return;

        var label = new Label(text);
        label.style.position = Position.Absolute;
        label.style.color = new StyleColor(RewardGreen);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.unityTextOutlineWidth = 0.28f;
        label.style.unityTextOutlineColor = new StyleColor(new Color(0f, 0f, 0f, 0.85f));
        label.style.fontSize = StartFontSize;
        // Keeps (left, top) pinned to the label's CENTER regardless of its size, which changes
        // every frame as it grows — same -50%/-50% trick WorldHoverPopupUI's popup uses for its
        // own corner anchor, just centered instead of bottom-right.
        label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
        label.pickingMode = PickingMode.Ignore;
        label.style.opacity = 0f;
        root.Add(label);

        // Geometry one frame late: a just-added element has no layout yet, and the anchor's rect is
        // only meaningful once the panel has laid out this frame — same pattern UnhappyCustomerFx
        // and MoneyFlightFx both use.
        label.schedule.Execute(() =>
        {
            Rect rootR = root.worldBound;
            Rect btnR = anchor.worldBound;
            if (rootR.width < 1f || btnR.width < 1f)
            {
                label.RemoveFromHierarchy();
                return;
            }

            // Root-local: an absolutely positioned child of root is offset from root's content box,
            // not from the panel origin.
            float startX = btnR.center.x - rootR.xMin;
            float startY = btnR.center.y - rootR.yMin;
            float midX = rootR.width * 0.5f;
            float midY = rootR.height * 0.5f;
            float endX = -OffscreenMarginPx;

            label.experimental.animation.Start(0f, 1f, DurationMs, (e, t) =>
            {
                float size, x, y, opacity;

                if (t <= GrowPhaseFraction)
                {
                    // Grows out of the button and travels to mid-screen — ease-out so it bursts
                    // rather than creeping at a constant rate.
                    float localT = t / GrowPhaseFraction;
                    float eased = 1f - Mathf.Pow(1f - localT, 3f);
                    size = Mathf.Lerp(StartFontSize, PeakFontSize, eased);
                    x = Mathf.Lerp(startX, midX, eased);
                    y = Mathf.Lerp(startY, midY, eased);
                    opacity = eased;
                }
                else
                {
                    // Holds full size while floating off to the left, ease-in so it accelerates
                    // away rather than drifting at a constant rate.
                    float localT = (t - GrowPhaseFraction) / (1f - GrowPhaseFraction);
                    float eased = localT * localT;
                    size = PeakFontSize;
                    x = Mathf.Lerp(midX, endX, eased);
                    y = midY;
                    opacity = localT < FadeStart ? 1f : 1f - Mathf.InverseLerp(FadeStart, 1f, localT);
                }

                e.style.fontSize = size;
                e.style.left = x;
                e.style.top = y;
                e.style.opacity = opacity;
            });
        }).ExecuteLater(16);

        label.schedule.Execute(label.RemoveFromHierarchy).ExecuteLater(DurationMs + 32);
    }
}
