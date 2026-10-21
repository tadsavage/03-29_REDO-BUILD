using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Floating "+$120" / "-3" pop-ups for the TopBar (capital, hour/today expenses, reputation). A change
/// spawns a green (good) or red (bad) number on the element: it swells to 1.5x, shrinks back, then drifts
/// down and fades out.
///
/// Cost model: ONE small pool of Labels parented to the document root (picking ignored), ONE scheduled
/// ticker that is paused whenever nothing is animating, and changes to the same element are coalesced for
/// <see cref="CoalesceSeconds"/> so a burst (e.g. a per-minute upkeep tick landing on three labels at once)
/// reads as one number instead of spawning a label per event. Runs on unscaled time, so it still plays
/// while the game is paused or sped up (see CLAUDE.md "UI/FX must run on unscaled time").
/// </summary>
public class TopBarDeltaFx
{
    private const float CoalesceSeconds = 0.35f;
    private const float SwellSeconds    = 0.15f;   // 1.0 -> 1.5
    private const float ShrinkSeconds   = 0.15f;   // 1.5 -> 1.0
    private const float FadeSeconds     = 0.65f;   // drift + fade out
    private const float DriftPixels     = 22f;
    private const float PeakScale       = 1.5f;
    private const int   FontSize        = 26;
    private const float LabelWidth      = 140f;
    private const float LabelHeight     = 36f;

    private static readonly Color Good = new Color(0.30f, 0.95f, 0.45f);
    private static readonly Color Bad  = new Color(1.00f, 0.32f, 0.30f);

    private class Pop
    {
        public Label Label;
        public float Age;
        public float BaseTop;
        public bool Active;
    }

    private class Pending
    {
        public float Delta;
        public bool Money;          // format as $ vs plain number
        public float Cooldown;      // seconds until it may flush
    }

    private readonly VisualElement _root;
    private readonly List<Pop> _pool = new List<Pop>();
    private readonly Dictionary<VisualElement, Pending> _pending = new Dictionary<VisualElement, Pending>();
    private readonly List<VisualElement> _flushScratch = new List<VisualElement>();
    private readonly IVisualElementScheduledItem _ticker;
    private float _lastTime;
    private bool _running;

    public TopBarDeltaFx(VisualElement root)
    {
        _root = root;
        _ticker = root.schedule.Execute(Tick).Every(16);
        _ticker.Pause();
    }

    /// <summary>Report a change on <paramref name="anchor"/>. Positive = shown green, negative = red.
    /// Use <paramref name="money"/> true for dollar amounts, false for plain numbers (reputation).</summary>
    public void Notify(VisualElement anchor, float delta, bool money)
    {
        // Only gains are shown (green). Red/negative pop-ups were clutter - costs trickle out every minute.
        if (anchor == null || delta <= 0f) return;

        if (!_pending.TryGetValue(anchor, out var p))
        {
            p = new Pending { Money = money };
            _pending[anchor] = p;
        }
        p.Delta += delta;
        p.Money = money;
        Start();
    }

    private void Start()
    {
        if (_running) return;
        _running = true;
        _lastTime = Time.unscaledTime;
        _ticker.Resume();
    }

    private void Tick()
    {
        float now = Time.unscaledTime;
        float dt = Mathf.Min(now - _lastTime, 0.1f);
        _lastTime = now;

        // Flush coalesced deltas whose cooldown has elapsed.
        if (_pending.Count > 0)
        {
            _flushScratch.Clear();
            foreach (var kv in _pending)
            {
                kv.Value.Cooldown -= dt;
                if (kv.Value.Cooldown <= 0f) _flushScratch.Add(kv.Key);
            }
            for (int i = 0; i < _flushScratch.Count; i++)
            {
                var anchor = _flushScratch[i];
                var p = _pending[anchor];
                if (!Mathf.Approximately(p.Delta, 0f)) Spawn(anchor, p.Delta, p.Money);
                p.Delta = 0f;
                p.Cooldown = CoalesceSeconds;
            }
        }

        bool anyActive = false;
        for (int i = 0; i < _pool.Count; i++)
        {
            var pop = _pool[i];
            if (!pop.Active) continue;
            pop.Age += dt;
            Animate(pop);
            anyActive = true;
        }

        // Nothing animating and nothing waiting to flush -> stop ticking entirely.
        bool anyPendingDelta = false;
        foreach (var kv in _pending)
            if (!Mathf.Approximately(kv.Value.Delta, 0f)) { anyPendingDelta = true; break; }

        if (!anyActive && !anyPendingDelta)
        {
            _running = false;
            _ticker.Pause();
        }
    }

    private void Spawn(VisualElement anchor, float delta, bool money)
    {
        Rect wb = anchor.worldBound;
        if (float.IsNaN(wb.x) || wb.width < 1f) return;   // not laid out yet (or hidden)

        var pop = GetFree();
        Vector2 local = _root.WorldToLocal(new Vector2(wb.center.x, wb.yMax));

        bool good = delta > 0f;
        string sign = good ? "+" : "-";
        float mag = Mathf.Abs(delta);
        pop.Label.text = money ? $"{sign}${mag:N0}" : $"{sign}{Mathf.RoundToInt(mag)}";
        pop.Label.style.color = good ? Good : Bad;

        // Borrow the anchor's font so the number matches the bar's look.
        pop.Label.style.unityFontDefinition = anchor.resolvedStyle.unityFontDefinition;

        pop.Label.style.left = local.x - LabelWidth * 0.5f;
        pop.BaseTop = local.y - LabelHeight * 0.55f;
        pop.Label.style.top = pop.BaseTop;
        pop.Label.style.opacity = 1f;
        pop.Label.style.scale = new Scale(Vector3.one);
        pop.Label.style.display = DisplayStyle.Flex;
        pop.Label.BringToFront();

        pop.Age = 0f;
        pop.Active = true;
    }

    private void Animate(Pop pop)
    {
        float t = pop.Age;
        float scale;
        float opacity = 1f;
        float drift = 0f;

        if (t < SwellSeconds)
        {
            float k = EaseOut(t / SwellSeconds);
            scale = Mathf.Lerp(1f, PeakScale, k);
        }
        else if (t < SwellSeconds + ShrinkSeconds)
        {
            float k = (t - SwellSeconds) / ShrinkSeconds;
            scale = Mathf.Lerp(PeakScale, 1f, k);
        }
        else
        {
            scale = 1f;
            float k = (t - SwellSeconds - ShrinkSeconds) / FadeSeconds;
            if (k >= 1f)
            {
                pop.Active = false;
                pop.Label.style.display = DisplayStyle.None;
                return;
            }
            opacity = 1f - k;
            drift = DriftPixels * EaseOut(k);
        }

        pop.Label.style.scale = new Scale(new Vector3(scale, scale, 1f));
        pop.Label.style.opacity = opacity;
        pop.Label.style.top = pop.BaseTop + drift;
    }

    private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

    private Pop GetFree()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].Active) return _pool[i];

        var label = new Label();
        label.pickingMode = PickingMode.Ignore;
        label.style.position = Position.Absolute;
        label.style.width = LabelWidth;
        label.style.height = LabelHeight;
        label.style.fontSize = FontSize;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.display = DisplayStyle.None;
        _root.Add(label);

        var pop = new Pop { Label = label };
        _pool.Add(pop);
        return pop;
    }
}
