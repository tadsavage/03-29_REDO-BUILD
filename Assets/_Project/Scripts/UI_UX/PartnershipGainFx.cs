using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Large green reward text that grows out of the confirm dialog's YES button, swells to 6x its
/// starting size (per Tad's explicit "grows by 500%"), then falls off the bottom of the screen —
/// 1.5 seconds total, per Tad's explicit ask.
///
/// Built the same way DispatchRewardFx is (and for the same reason): it has to render above a
/// full-screen modal, so it's added as an absolutely positioned child of the panel's overlay rather
/// than to the button itself, which would clip it the moment it grew past the button's own bounds.
/// Must be triggered BEFORE the confirm dialog hides (see PurchasingPanel's _confirmPreHide) — once
/// hidden, the button has no layout left to anchor on.
/// </summary>
public static class PartnershipGainFx
{
    /// <summary>Total run of the text, in ms — per Tad's explicit ask.</summary>
    private const int DurationMs = 1500;

    /// <summary>Fraction of the run spent growing out of the button in place. The remainder is
    /// spent falling toward and off the bottom of the viewport at full size.</summary>
    private const float GrowPhaseFraction = 0.35f;

    private const float StartFontSize = 14f;

    /// <summary>Target size cut in half per Tad's ask (2026-09-21) — was 6x starting size ("grows by
    /// 500%"), now 3x.</summary>
    private const float GrowthMultiplier = 3f;
    private const float PeakFontSize = StartFontSize * GrowthMultiplier;

    /// <summary>How far past the viewport's bottom edge the text travels before being torn down —
    /// far enough that it's fully clear of the screen, not just touching the edge.</summary>
    private const float OffscreenMarginPx = 200f;

    /// <summary>Fraction into the fall phase where the fade-out begins.</summary>
    private const float FadeStart = 0.7f;

    private static readonly Color PartnershipGreen = new Color(0.26f, 0.92f, 0.35f);

    /// <summary>
    /// Plays the reward text over <paramref name="anchor"/> (the confirm dialog's YES button).
    /// <paramref name="root"/> must be an element that spans the full viewport and outlives the
    /// animation — the panel's own full-screen overlay, not the modal box itself, so the text keeps
    /// falling even after the dialog it grew out of has already closed. Degrades to doing nothing on
    /// a null root/anchor or an unlaid-out anchor, since this is pure decoration.
    /// </summary>
    public static void Play(VisualElement root, VisualElement anchor, string text)
    {
        if (root == null || anchor == null || string.IsNullOrEmpty(text)) return;

        Rect rootR = root.worldBound;
        Rect btnR = anchor.worldBound;
        if (rootR.width < 1f || btnR.width < 1f) return;

        var label = new Label(text);
        label.style.position = Position.Absolute;
        label.style.color = new StyleColor(PartnershipGreen);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.unityTextOutlineWidth = 0.3f;
        label.style.unityTextOutlineColor = new StyleColor(new Color(0f, 0f, 0f, 0.85f));
        label.style.fontSize = StartFontSize;
        // Keeps (left, top) pinned to the label's CENTER regardless of its size, which changes
        // every frame as it grows — same -50%/-50% trick DispatchRewardFx/WorldHoverPopupUI use.
        label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
        label.pickingMode = PickingMode.Ignore;
        label.style.opacity = 0f;
        root.Add(label);

        // Root-local: an absolutely positioned child of root is offset from root's content box,
        // not the panel origin. Captured NOW (synchronously) rather than one frame later — unlike
        // DispatchRewardFx's anchor (a page button that stays put), this anchor is the confirm
        // dialog's YES button, which is about to be hidden by the caller the instant this returns.
        float startX = btnR.center.x - rootR.xMin;
        float startY = btnR.center.y - rootR.yMin;
        float endY = rootR.height + OffscreenMarginPx;

        label.experimental.animation.Start(0f, 1f, DurationMs, (e, t) =>
        {
            float size, y, opacity;

            if (t <= GrowPhaseFraction)
            {
                // Grows in place out of the button — ease-out so it bursts rather than creeping.
                float localT = t / GrowPhaseFraction;
                float eased = 1f - Mathf.Pow(1f - localT, 3f);
                size = Mathf.Lerp(StartFontSize, PeakFontSize, eased);
                y = startY;
                opacity = eased;
            }
            else
            {
                // Holds full size while falling toward and off the bottom edge, ease-in so it
                // accelerates away like gravity rather than drifting at a constant rate.
                float localT = (t - GrowPhaseFraction) / (1f - GrowPhaseFraction);
                float eased = localT * localT;
                size = PeakFontSize;
                y = Mathf.Lerp(startY, endY, eased);
                opacity = localT < FadeStart ? 1f : 1f - Mathf.InverseLerp(FadeStart, 1f, localT);
            }

            e.style.fontSize = size;
            e.style.left = startX;
            e.style.top = y;
            e.style.opacity = opacity;
        });

        label.schedule.Execute(label.RemoveFromHierarchy).ExecuteLater(DurationMs + 32);
    }
}
