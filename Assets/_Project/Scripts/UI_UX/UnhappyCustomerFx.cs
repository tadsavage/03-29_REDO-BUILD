using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The angry-customer reaction that plays on the Schedule grid whenever the player does something a
/// customer won't like: a scowling face swelling out of the exact time slot the trailer landed in and
/// drifting upward, trailed a second later by the fine in red if one was charged.
///
/// WHY IT EXISTS. Off-slot moves already cost satisfaction and money, but the only evidence was a
/// toast in the corner — a line of text that says nothing about WHICH slot caused it, and that the
/// player is usually not looking at because their eyes are on the grid cell they just clicked. The
/// reaction happens where the decision happened.
///
/// WHY UI TOOLKIT AND NOT FloatingMoneyText. That one is world-space, so it renders behind any
/// full-screen modal — the exact reason MoneyFlightFx had to exist for order close-out. The Schedule
/// tab IS a full-screen modal, so a world-space popup here would be invisible. This is built the same
/// way MoneyFlightFx is, for the same reason, and borrows its red and its outline so a fine reads as
/// the same kind of event wherever it appears.
///
/// The animation is deliberately driven by ONE experimental.animation per element over a normalised t
/// rather than chained tweens: experimental.animation has no completion callback, so chaining means
/// guessing timings that then drift (MoneyFlightFx documents the same trap). The fine's one-second
/// delay is a single scheduled start, not a chain.
/// </summary>
public static class UnhappyCustomerFx
{
    // ── Choreography ─────────────────────────────────────────────────────────
    // Tad's spec, verbatim, in one place: 2.5s total, 10% → 150%, floating slowly up, with the fine
    // starting a second behind so it trails rather than competes.

    /// <summary>Total run of the emoji, in ms.</summary>
    private const int EmojiDurationMs = 2500;

    /// <summary>How far behind the emoji the fine starts. It then runs its own full duration, so the
    /// two are never on screen at the same size at the same moment.</summary>
    private const int FineDelayMs = 1000;

    private const float StartScale = 0.10f;
    private const float EndScale   = 1.50f;

    /// <summary>Natural (100%) size of the face in px. Chosen against the grid's slot height so the
    /// face at full swell reads clearly without covering the neighbouring blocks.</summary>
    private const float EmojiSize = 46f;

    /// <summary>Pixels the emoji drifts up over its life. "Slowly" is this over 2.5s — a shorter rise
    /// than the money sweep because the point is to hang over the slot that caused it, not to travel.</summary>
    private const float EmojiRiseY = 54f;

    /// <summary>The fine rises further: it starts lower (below the face) and has to clear it.</summary>
    private const float FineRiseY = 76f;

    /// <summary>Offsets that keep the fine trailing the face rather than sitting under it — down and
    /// to the right, so both stay separately readable the whole way up.</summary>
    private const float FineOffsetX = 26f;
    private const float FineOffsetY = 22f;

    private const int   FineFontSize = 22;

    /// <summary>Fraction of each element's life spent at full opacity before it starts fading.</summary>
    private const float FadeStart = 0.55f;

    /// <summary>Same red FloatingMoneyText uses for money leaving capital, so a fine looks like a fine
    /// whether it surfaces in the world or on a panel.</summary>
    private static readonly Color SpendRed = new Color(0.93f, 0.27f, 0.22f);

    private const string EmojiResourcePath = "Emotes/emote_faceAngry";

    /// <summary>Cached because this can fire several times in a row while the player shuffles doors,
    /// and Resources.Load hits the asset database every call. Null-checked rather than a bool flag so a
    /// failed first load is retried instead of poisoning the cache for the session.</summary>
    private static Texture2D _emojiTexture;

    private static Texture2D EmojiTexture
    {
        get
        {
            if (_emojiTexture != null) return _emojiTexture;
            // Texture2D, not Sprite: UI Toolkit's backgroundImage takes either, but the sprite import
            // for these Kenney PNGs isn't guaranteed and a null sprite would silently draw nothing.
            _emojiTexture = Resources.Load<Texture2D>(EmojiResourcePath);
            if (_emojiTexture == null)
                Debug.LogWarning($"[UnhappyCustomerFx] No texture at Resources/{EmojiResourcePath} — " +
                                 $"the unhappy-customer reaction will be skipped.");
            return _emojiTexture;
        }
    }

    /// <summary>
    /// Plays the reaction over <paramref name="anchor"/> — the grid slot the trailer landed in.
    ///
    /// <paramref name="root"/> must be an element that spans the panel and outlives the animation;
    /// the effect is added there as an absolutely positioned child rather than to the slot itself,
    /// because the slot lives inside a ScrollView that clips its children — a face growing to 150%
    /// out of a grid cell would be sliced off at the cell boundary.
    ///
    /// <paramref name="fineAmount"/> of 0 plays the face alone. That case is real and not an edge:
    /// an appointment with no orders on it yet takes the satisfaction hit without a fine, and the
    /// customer is no less annoyed for it.
    ///
    /// Every failure degrades to doing nothing — a missing texture, an unlaid-out anchor, or a null
    /// root. This is feedback about a penalty, and it must never be able to break the move that
    /// caused it.
    /// </summary>
    public static void Play(VisualElement root, VisualElement anchor, int fineAmount = 0)
    {
        if (root == null || anchor == null) return;
        var texture = EmojiTexture;
        if (texture == null) return;

        var emoji = new VisualElement();
        emoji.style.position = Position.Absolute;
        emoji.style.width = EmojiSize;
        emoji.style.height = EmojiSize;
        emoji.style.backgroundImage = new StyleBackground(texture);
        // Never eat a click: the player is mid-shuffle and the grid underneath has to stay live.
        emoji.pickingMode = PickingMode.Ignore;
        emoji.style.opacity = 0f; // hidden until geometry resolves, so it can't flash at 0,0
        root.Add(emoji);

        Label fine = null;
        if (fineAmount != 0)
        {
            fine = new Label($"-${Mathf.Abs(fineAmount):N0}");
            fine.style.position = Position.Absolute;
            fine.style.fontSize = FineFontSize;
            fine.style.color = new StyleColor(SpendRed);
            fine.style.unityFontStyleAndWeight = FontStyle.Bold;
            // Same outline as MoneyFlightFx — the grid behind this is a mix of dark empty slots and
            // bright chips, and unoutlined red on a red-edged chip disappears.
            fine.style.unityTextOutlineWidth = 0.28f;
            fine.style.unityTextOutlineColor = new StyleColor(new Color(0f, 0f, 0f, 0.85f));
            fine.pickingMode = PickingMode.Ignore;
            fine.style.opacity = 0f;
            root.Add(fine);
        }

        // Geometry one frame late, exactly as MoneyFlightFx does it: a just-added element has no
        // layout, and the anchor's rect is only meaningful once the panel has laid out this frame.
        emoji.schedule.Execute(() =>
        {
            Rect rootR = root.worldBound;
            Rect slotR = anchor.worldBound;
            if (rootR.width < 1f || slotR.width < 1f)
            {
                // No layout to anchor to — clean up rather than animate from a garbage origin.
                emoji.RemoveFromHierarchy();
                fine?.RemoveFromHierarchy();
                return;
            }

            // Root-local: an absolutely positioned child of root is offset from root's content box,
            // not from the panel origin.
            float centreX = slotR.center.x - rootR.xMin;
            float centreY = slotR.center.y - rootR.yMin;

            PlayEmoji(emoji, centreX, centreY);
            if (fine != null) PlayFine(fine, centreX, centreY);
        }).ExecuteLater(16);
    }

    /// <summary>The face: swells 10% → 150% while drifting up out of the slot, holding full opacity
    /// through the first half so the growth is what's read, then fading as it clears the cell.</summary>
    private static void PlayEmoji(VisualElement emoji, float centreX, float centreY)
    {
        emoji.experimental.animation.Start(0f, 1f, EmojiDurationMs, (e, t) =>
        {
            // Ease-out on both scale and rise: it bursts out of the slot and settles, rather than
            // creeping at a constant rate, which at 2.5s would read as a stuck element.
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float scale = Mathf.Lerp(StartScale, EndScale, eased);

            // left/top are the element's TOP-LEFT, and transform.scale scales about the CENTRE — so
            // the box is kept centred on the slot at its natural size and allowed to grow around that
            // point. Offsetting by the scaled size instead would drift it off the slot as it grew.
            e.style.left = centreX - EmojiSize * 0.5f;
            e.style.top = centreY - EmojiSize * 0.5f - EmojiRiseY * eased;
            e.transform.scale = new Vector3(scale, scale, 1f);
            e.style.opacity = Fade(t);
        });

        emoji.schedule.Execute(emoji.RemoveFromHierarchy).ExecuteLater(EmojiDurationMs + 16);
    }

    /// <summary>The fine, one second behind. Same rise and fade shape so the two read as one event,
    /// but offset down-right and started late so it trails the face instead of racing it.</summary>
    private static void PlayFine(Label fine, float centreX, float centreY)
    {
        fine.schedule.Execute(() =>
        {
            fine.experimental.animation.Start(0f, 1f, EmojiDurationMs, (e, t) =>
            {
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                e.style.left = centreX + FineOffsetX;
                e.style.top = centreY + FineOffsetY - FineRiseY * eased;
                e.style.opacity = Fade(t);
            });
        }).ExecuteLater(FineDelayMs);

        fine.schedule.Execute(fine.RemoveFromHierarchy).ExecuteLater(FineDelayMs + EmojiDurationMs + 16);
    }

    /// <summary>Snap to full opacity, hold, then fade out over the tail. Shared so the face and the
    /// number can't end up on different fade curves and look like two unrelated popups.</summary>
    private static float Fade(float t)
        => t < FadeStart ? 1f : 1f - Mathf.InverseLerp(FadeStart, 1f, t);
}
