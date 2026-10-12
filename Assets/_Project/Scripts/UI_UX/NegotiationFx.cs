using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Full-screen result flourish for a negotiation: confetti + a big green "YOU GOT THE DEAL!" that swells to two
/// thirds of the screen and shrinks away, or a red "NO DEAL" that drops from the ceiling, bounces on the floor of the
/// frame and sinks off the bottom. Lives on the panel's PARENT so it survives the negotiation window closing.
/// Runs on unscaled time (the game is paused behind the window).</summary>
public static class NegotiationFx
{
    static readonly Color[] ConfettiColors =
    {
        new Color(0.30f, 0.85f, 0.40f), new Color(0.98f, 0.80f, 0.20f), new Color(0.36f, 0.61f, 0.77f),
        new Color(0.91f, 0.45f, 0.20f), new Color(0.90f, 0.35f, 0.65f), Color.white
    };

    class Piece { public VisualElement El; public float X, Y, Vx, Vy, Rot, Spin, Phase; }

    public static void Play(VisualElement root, bool win)
    {
        if (root == null) return;
        var layer = new VisualElement { name = win ? "NegotiationWinFx" : "NegotiationFailFx", pickingMode = PickingMode.Ignore };
        layer.style.position = Position.Absolute;
        layer.style.left = 0; layer.style.top = 0; layer.style.right = 0; layer.style.bottom = 0;
        layer.style.overflow = Overflow.Hidden;
        root.Add(layer);
        layer.BringToFront();
        UIToast.KeepOnTop();

        if (win) PlayWin(layer); else PlayFail(layer);
    }

    static Label MakeLabel(string text, Color color, float fontSize)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.style.position = Position.Absolute;
        l.style.fontSize = fontSize;
        l.style.color = color;
        l.style.whiteSpace = WhiteSpace.NoWrap;
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
        l.style.unityTextOutlineWidth = Mathf.Max(2f, fontSize * 0.06f);
        l.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.85f);
        var f = NegotiationPanel.FxFont();
        if (f != null) l.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(f));
        return l;
    }

    static void ScreenSize(VisualElement layer, out float sw, out float sh)
    {
        var tree = layer.panel != null ? layer.panel.visualTree : null;
        sw = Mathf.Max(800f, tree != null ? tree.resolvedStyle.width : Screen.width);
        sh = Mathf.Max(500f, tree != null ? tree.resolvedStyle.height : Screen.height);
    }

    // ── Win ──────────────────────────────────────────────────────────────

    static void PlayWin(VisualElement layer)
    {
        AudioManager.Play("Fanfare");
        ScreenSize(layer, out float sw, out float sh);

        // Text sized so the finished word spans ~2/3 of the screen width.
        var label = MakeLabel("YOU GOT THE DEAL!", new Color(0.30f, 0.92f, 0.38f), sw * 0.66f / 8.2f);
        label.style.left = 0; label.style.right = 0; label.style.top = 0; label.style.bottom = 0;
        label.style.scale = new Scale(Vector3.zero);
        layer.Add(label);

        var pieces = new List<Piece>();
        float start = Time.unscaledTime;
        float lastSpawn = start;
        const float SpawnFor = 1.6f, Grow = 0.55f, Hold = 1.3f, Shrink = 0.5f;
        float total = Grow + Hold + Shrink;
        float last = start;

        IVisualElementScheduledItem item = null;
        item = layer.schedule.Execute(() =>
        {
            float now = Time.unscaledTime, t = now - start, dt = Mathf.Min(0.05f, now - last);
            last = now;

            // Text: overshoot-in, hold, ease-out shrink.
            float s;
            if (t < Grow) { float u = t / Grow; s = 1f + 2.70158f * Mathf.Pow(u - 1f, 3) + 1.70158f * Mathf.Pow(u - 1f, 2); }
            else if (t < Grow + Hold) s = 1f;
            else { float u = Mathf.Clamp01((t - Grow - Hold) / Shrink); s = 1f - u * u; }
            label.style.scale = new Scale(new Vector3(s, s, 1f));

            // Confetti: a burst from the ceiling, spawned over SpawnFor seconds.
            if (t < SpawnFor)
            {
                int n = Mathf.CeilToInt((now - lastSpawn) * 110f);
                lastSpawn = now;
                for (int i = 0; i < n; i++)
                {
                    var el = new VisualElement { pickingMode = PickingMode.Ignore };
                    float w = Random.Range(8f, 16f);
                    el.style.position = Position.Absolute;
                    el.style.width = w; el.style.height = w * Random.Range(0.4f, 0.9f);
                    el.style.backgroundColor = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
                    layer.Insert(0, el);
                    pieces.Add(new Piece
                    {
                        El = el, X = Random.Range(0f, sw), Y = Random.Range(-60f, -10f),
                        Vx = Random.Range(-90f, 90f), Vy = Random.Range(80f, 260f),
                        Rot = Random.Range(0f, 360f), Spin = Random.Range(-420f, 420f), Phase = Random.Range(0f, 6.28f)
                    });
                }
            }
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i];
                p.Vy = Mathf.Min(p.Vy + 420f * dt, 340f);
                p.X += (p.Vx + Mathf.Sin(now * 3f + p.Phase) * 60f) * dt;
                p.Y += p.Vy * dt;
                p.Rot += p.Spin * dt;
                p.El.style.left = p.X; p.El.style.top = p.Y;
                p.El.style.rotate = new Rotate(new Angle(p.Rot, AngleUnit.Degree));
                if (p.Y > sh + 30f) { p.El.RemoveFromHierarchy(); pieces.RemoveAt(i); }
            }

            if ((t > total && pieces.Count == 0) || t > total + 6f) { item.Pause(); layer.RemoveFromHierarchy(); }
        }).Every(16);
    }

    // ── Fail ─────────────────────────────────────────────────────────────

    static void PlayFail(VisualElement layer)
    {
        AudioManager.Play("NegotiationFail");
        ScreenSize(layer, out float sw, out float sh);

        float fontSize = sw * 0.5f / 4.6f;
        var label = MakeLabel("NO DEAL", new Color(0.95f, 0.22f, 0.20f), fontSize);
        float boxH = fontSize * 1.4f;
        label.style.left = 0; label.style.right = 0; label.style.height = boxH;
        float y = -boxH, vy = 0f;
        label.style.top = y;
        layer.Add(label);

        float last = Time.unscaledTime, start = last;
        int bounces = 0;
        float floor = sh - boxH - 8f;        // the text rests on the bottom edge of the frame
        bool sinking = false;

        IVisualElementScheduledItem item = null;
        item = layer.schedule.Execute(() =>
        {
            float now = Time.unscaledTime, dt = Mathf.Min(0.05f, now - last);
            last = now;
            vy += 2400f * dt;
            y += vy * dt;
            if (!sinking && y >= floor)
            {
                y = floor;
                bounces++;
                if (bounces >= 4) sinking = true;                     // enough bouncing: let it fall through the floor
                else { vy = -vy * 0.55f; AudioManager.Play("DiceTap"); }
            }
            label.style.top = y;
            if (y > sh + 20f || now - start > 8f) { item.Pause(); layer.RemoveFromHierarchy(); }
        }).Every(16);
    }
}
