using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A "+$1,234" label that sweeps across a panel and then flies into the Capital readout, pulsing it
/// on arrival — the UI-Toolkit counterpart to FloatingMoneyText, which only works in world space and
/// so is invisible while a full-screen modal is open (the case that motivated this: closing out
/// orders in the Work Queue billed real money with no on-screen feedback at all).
///
/// One animation drives a piecewise path rather than two chained ones: experimental.animation has no
/// completion callback, so chaining means guessing at timings that then drift. Phase boundaries are
/// read off the normalised t instead, and a single scheduled callback at the end handles cleanup.
/// </summary>
public static class MoneyFlightFx
{
    // Tuning — the whole choreography is these five numbers.
    private const int   TotalDurationMs = 4600;
    private const float SweepFraction   = 0.55f; // share of the run spent crossing the modal
    private const float SweepRiseY      = 26f;   // gentle arc height during the sweep, in px
    private const int   PulseDurationMs = 320;
    private const float PulseScale      = 1.18f;

    private static readonly Color MoneyGreen = new Color(0.36f, 0.86f, 0.42f, 1f);

    /// <summary>
    /// Sweeps <paramref name="amount"/> across <paramref name="sweepOver"/> (usually the modal), then
    /// flies it to <paramref name="target"/> (usually the Capital pill) and pulses that target.
    ///
    /// Everything is optional-safe: a null root no-ops, and a null/unresolved target degrades to the
    /// sweep alone fading out where it finished, so a renamed or missing Capital label can never break
    /// close-out. <paramref name="styleHook"/> lets the caller apply its own font so the label matches
    /// the panel it appears over.
    /// </summary>
    public static void Play(VisualElement root, VisualElement sweepOver, VisualElement target,
                            int amount, Action<Label> styleHook = null)
    {
        if (root == null || amount == 0) return;

        var label = new Label(amount > 0 ? $"+${amount:N0}" : $"-${Mathf.Abs(amount):N0}");
        label.style.position = Position.Absolute;
        label.style.fontSize = 34;
        label.style.color = new StyleColor(amount > 0 ? MoneyGreen : Color.red);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        // Outline so the number stays legible over both the bright modal and the dim world behind it.
        label.style.unityTextOutlineWidth = 0.28f;
        label.style.unityTextOutlineColor = new StyleColor(new Color(0f, 0f, 0f, 0.85f));
        label.pickingMode = PickingMode.Ignore; // never eat clicks meant for the panel underneath
        styleHook?.Invoke(label);
        root.Add(label);

        // Geometry is resolved one frame late: a just-added element has no layout yet, and the
        // modal/target rects are only meaningful once the panel has laid out this frame.
        label.schedule.Execute(() =>
        {
            Rect rootR = root.worldBound;
            Rect sweepR = sweepOver != null && sweepOver.worldBound.width > 1f
                ? sweepOver.worldBound
                : rootR;

            // Work in root-local space: an absolutely positioned child of root is offset from root's
            // content box, not from the panel origin.
            float startX = sweepR.xMin - rootR.xMin + 40f;
            float endX = sweepR.xMax - rootR.xMin - 160f;
            float midY = sweepR.center.y - rootR.yMin;

            bool haveTarget = target != null && target.worldBound.width > 1f;
            float targetX = haveTarget ? target.worldBound.center.x - rootR.xMin : endX;
            float targetY = haveTarget ? target.worldBound.center.y - rootR.yMin : midY;

            label.experimental.animation.Start(0f, 1f, TotalDurationMs, (e, t) =>
            {
                float x, y, alpha, scale;

                if (t <= SweepFraction)
                {
                    // Phase 1 — cross the modal on a shallow arc, easing out so it decelerates into
                    // the hand-off rather than snapping direction.
                    float st = t / SweepFraction;
                    float eased = 1f - Mathf.Pow(1f - st, 3f);
                    x = Mathf.Lerp(startX, endX, eased);
                    y = midY - Mathf.Sin(eased * Mathf.PI) * SweepRiseY;
                    alpha = Mathf.Min(1f, st * 4f); // quick fade-in, then hold
                    scale = Mathf.Lerp(0.7f, 1f, Mathf.Min(1f, st * 3f));
                }
                else
                {
                    // Phase 2 — accelerate up into the Capital pill, shrinking as it goes so it reads
                    // as being absorbed rather than just sliding off.
                    float ft = (t - SweepFraction) / (1f - SweepFraction);
                    float eased = ft * ft;
                    x = Mathf.Lerp(endX, targetX, eased);
                    y = Mathf.Lerp(midY, targetY, eased);
                    alpha = 1f - Mathf.Pow(ft, 2.5f);
                    scale = Mathf.Lerp(1f, 0.45f, eased);
                }

                e.style.left = x;
                e.style.top = y;
                e.style.opacity = alpha;
                e.transform.scale = new Vector3(scale, scale, 1f);
            });

            // No completion callback on experimental.animation — one scheduled tick at the end both
            // removes the label and fires the arrival pulse, so the two can't drift apart.
            label.schedule.Execute(() =>
            {
                label.RemoveFromHierarchy();
                if (haveTarget) Pulse(target);
            }).ExecuteLater(TotalDurationMs);
        }).ExecuteLater(16);
    }

    /// <summary>Brief scale-up on the element the money landed in, so the number changing reads as
    /// caused by the arrival. Restores the original scale rather than assuming it was identity.</summary>
    private static void Pulse(VisualElement target)
    {
        Vector3 baseScale = target.transform.scale;
        target.experimental.animation.Start(0f, 1f, PulseDurationMs, (e, t) =>
        {
            float bump = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
            float s = Mathf.Lerp(1f, PulseScale, bump);
            e.transform.scale = new Vector3(baseScale.x * s, baseScale.y * s, 1f);
        });
        target.schedule.Execute(() => target.transform.scale = baseScale).ExecuteLater(PulseDurationMs + 16);
    }
}
