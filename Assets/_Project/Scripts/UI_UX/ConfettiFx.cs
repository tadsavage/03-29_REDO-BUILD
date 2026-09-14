using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A burst of small colored squares that falls from just above an anchor (the Systems Log panel)
/// down off the bottom of the screen, with a little horizontal sway and spin per piece so the whole
/// burst doesn't fall as one rigid block. Pure UI Toolkit — no particle system, no FXPool entry —
/// same "absolutely positioned child of a full-viewport root" trick DispatchRewardFx/UnhappyCustomerFx
/// already use, chosen specifically because it's guaranteed to composite above the rest of the HUD
/// without fighting Unity's world-space-vs-UI-Toolkit render-order rules a Shuriken particle system
/// would run into.
/// </summary>
public static class ConfettiFx
{
    private const int PieceCount = 60;
    private const float PieceMinSize = 8f;
    private const float PieceMaxSize = 14f;
    private const int MinFallMs = 1800;
    private const int MaxFallMs = 3000;
    private const int MaxStaggerMs = 500;
    private const float MaxSwayPx = 70f;
    private const float MaxSpinDegrees = 540f;

    // Sampled from the project's Confetti-A reference art (Assets/_Project/Art/Confetti-A/confetti.png).
    private static readonly Color[] Palette =
    {
        new Color(0.44f, 0.68f, 0.90f), // blue
        new Color(0.58f, 0.80f, 0.49f), // green
        new Color(0.91f, 0.58f, 0.24f), // orange
        new Color(0.90f, 0.42f, 0.45f), // red/pink
        new Color(0.71f, 0.63f, 0.85f), // purple
        new Color(0.95f, 0.76f, 0.20f), // yellow
    };

    /// <summary>
    /// Bursts confetti spanning <paramref name="anchor"/>'s width, starting just above its top edge,
    /// falling down through <paramref name="root"/> (a full-viewport overlay element) to past its
    /// bottom edge. Degrades to doing nothing if either rect isn't laid out yet — pure decoration,
    /// must never be able to break whatever it's celebrating.
    /// </summary>
    public static void Play(VisualElement root, VisualElement anchor)
    {
        if (root == null || anchor == null) return;

        // Geometry one frame late — a just-shown/just-reflowed anchor has no meaningful worldBound
        // until the panel finishes laying out this frame. Same pattern DispatchRewardFx uses.
        root.schedule.Execute(() =>
        {
            Rect rootR = root.worldBound;
            Rect anchorR = anchor.worldBound;
            if (rootR.width < 1f || anchorR.width < 1f) return;

            var rand = new System.Random();
            for (int i = 0; i < PieceCount; i++)
                SpawnPiece(root, rootR, anchorR, rand);
        }).ExecuteLater(16);
    }

    private static void SpawnPiece(VisualElement root, Rect rootR, Rect anchorR, System.Random rand)
    {
        float size = Mathf.Lerp(PieceMinSize, PieceMaxSize, (float)rand.NextDouble());

        var piece = new VisualElement();
        piece.pickingMode = PickingMode.Ignore;
        piece.style.position = Position.Absolute;
        piece.style.width = size;
        piece.style.height = size;
        piece.style.backgroundColor = new StyleColor(Palette[rand.Next(Palette.Length)]);
        // Pivot at the piece's own center so rotation spins in place instead of orbiting its corner.
        piece.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));

        // Root-local coordinates — an absolutely positioned child of root is offset from root's own
        // content box, not the panel origin (same as DispatchRewardFx).
        float startX = (anchorR.xMin - rootR.xMin) + (float)(rand.NextDouble() * anchorR.width);
        float startY = (anchorR.yMin - rootR.yMin) - (float)(rand.NextDouble() * 40f) - size;
        float endY = rootR.height + size + 20f; // fully clear of the bottom edge, not just touching it
        float sway = ((float)rand.NextDouble() * 2f - 1f) * MaxSwayPx;
        float startRotation = (float)(rand.NextDouble() * 360.0);
        float spin = ((float)rand.NextDouble() * 2f - 1f) * MaxSpinDegrees;

        piece.style.left = startX;
        piece.style.top = startY;
        piece.style.rotate = new Rotate(startRotation);
        piece.style.opacity = 1f;
        root.Add(piece);

        int duration = rand.Next(MinFallMs, MaxFallMs + 1);
        int stagger = rand.Next(0, MaxStaggerMs);

        piece.schedule.Execute(() =>
        {
            piece.experimental.animation.Start(0f, 1f, duration, (e, t) =>
            {
                // Ease-in fall (gravity-like acceleration) with a sine sway across the fall for drift,
                // and a fade in the last quarter so pieces vanish instead of popping off mid-fall.
                float fallT = t * t;
                float y = Mathf.Lerp(startY, endY, fallT);
                float x = startX + sway * Mathf.Sin(t * Mathf.PI * 1.5f);
                float rotation = startRotation + spin * t;
                float opacity = t < 0.75f ? 1f : 1f - Mathf.InverseLerp(0.75f, 1f, t);

                e.style.top = y;
                e.style.left = x;
                e.style.rotate = new Rotate(rotation);
                e.style.opacity = opacity;
            });
        }).ExecuteLater(16 + stagger);

        piece.schedule.Execute(piece.RemoveFromHierarchy).ExecuteLater(duration + stagger + 64);
    }
}
