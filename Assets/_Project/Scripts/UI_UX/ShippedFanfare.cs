using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The "you got paid" moment: big green "$X revenue received for order Y!" text with a confetti
/// shower from the top of the screen. Played when an order ships (OrderService.AutoCloseLoadedOrders).
///
/// Lives on a UIDocument of its OWN, sorted above the toast layer (UIToast.ToastSortingOrder), so no
/// panel, modal or toast can cover it -- the whole point is that a payout can't go unnoticed. Several
/// orders shipping together queue up and play one after another (each shorter when others are
/// waiting) rather than stacking on top of each other.
///
/// The caller also writes the payout to the Systems Log, so nothing is lost if this layer can't be
/// built; in that case it degrades to a plain toast.
/// </summary>
public static class ShippedFanfare
{
    private const float LayerSortingOrder = UIToast.ToastSortingOrder + 100f;
    private const int SoloDurationMs = 4800;
    private const int QueuedDurationMs = 2800;
    private const int FontSizePx = 64;

    private static readonly Color Green = new Color(0.36f, 0.90f, 0.42f, 1f);

    private static readonly Queue<(string order, int amount)> _queue = new();
    private static bool _playing;
    private static VisualElement _root;
    private static VisualElement _confettiStrip;
    private static Font _lilita;

    /// <summary>Domain reload is off in this project, so statics outlive Play sessions.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _queue.Clear();
        _playing = false;
        _root = null;
        _confettiStrip = null;
    }

    public static void Show(string orderNumber, int amount)
    {
        _queue.Enqueue((orderNumber, amount));
        if (!_playing) PlayNext();
    }

    /// <summary>A one-off headline with the same pop-in and confetti, outside the payout queue (used when the player beats HAL 9000).</summary>
    public static void ShowBanner(string text, Color color, int durationMs = 6500)
    {
        if (!EnsureLayer()) { UIToast.Show(text, 5f); return; }

        var label = new Label(text);
        label.pickingMode = PickingMode.Ignore;
        label.style.position = Position.Absolute;
        label.style.left = 0; label.style.right = 0;
        label.style.top = Length.Percent(12);
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.fontSize = 84;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = new StyleColor(color);
        label.style.unityTextOutlineWidth = 0.4f;
        label.style.unityTextOutlineColor = new StyleColor(new Color(0.1f, 0.06f, 0f, 0.95f));
        label.style.opacity = 0f;
        ApplyFont(label);
        _root.Add(label);
        label.BringToFront();

        label.experimental.animation.Start(0f, 1f, durationMs, (e, t) =>
        {
            float pop = t < 0.1f ? Mathf.Sin(t / 0.1f * Mathf.PI * 0.5f) : 1f;
            float overshoot = t < 0.1f ? 1f + 0.3f * Mathf.Sin(t / 0.1f * Mathf.PI) : 1f;
            float s = Mathf.Lerp(0.4f, 1f, pop) * overshoot;
            e.transform.scale = new Vector3(s, s, 1f);
            e.style.opacity = t < 0.85f ? Mathf.Min(1f, t * 10f) : 1f - Mathf.InverseLerp(0.85f, 1f, t);
        });

        // Two showers a couple of seconds apart: this is the big moment, not a payout.
        ConfettiFx.Play(_root, _confettiStrip);
        label.schedule.Execute(() => ConfettiFx.Play(_root, _confettiStrip)).ExecuteLater(2200);
        label.schedule.Execute(() => label.RemoveFromHierarchy()).ExecuteLater(durationMs + 40);
    }

    /// <summary>The fanfare layer's root, so other full-screen UI (the results modal) can share its panel and stay above everything.</summary>
    public static VisualElement LayerRoot() { return EnsureLayer() ? _root : null; }

    private static bool EnsureLayer()
    {
        if (_root != null && _root.panel != null) return true;

        var toast = Object.FindAnyObjectByType<UIToast>();
        var source = toast != null ? toast.GetComponent<UIDocument>() : null;
        if (source == null || source.panelSettings == null) return false;

        // Created inactive so panelSettings is assigned before the first enable (see UIToast).
        var go = new GameObject("[FanfareLayer]");
        go.SetActive(false);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = source.panelSettings;
        doc.sortingOrder = LayerSortingOrder;
        go.SetActive(true);

        var root = doc.rootVisualElement;
        if (root == null) return false;
        root.pickingMode = PickingMode.Ignore;

        // Confetti anchor: a full-width sliver at the very top edge, so the shower spans the screen.
        var strip = new VisualElement { pickingMode = PickingMode.Ignore };
        strip.style.position = Position.Absolute;
        strip.style.left = 0; strip.style.right = 0; strip.style.top = 0; strip.style.height = 2;
        root.Add(strip);

        _root = root;
        _confettiStrip = strip;
        return true;
    }

    private static void PlayNext()
    {
        if (_queue.Count == 0) { _playing = false; return; }
        _playing = true;

        var (order, amount) = _queue.Dequeue();
        bool othersWaiting = _queue.Count > 0;

        if (!EnsureLayer())
        {
            UIToast.Show($"${amount:N0} revenue received for order {order}!", 4f);
            _playing = false;
            if (_queue.Count > 0) PlayNext();
            return;
        }

        int duration = othersWaiting ? QueuedDurationMs : SoloDurationMs;

        var label = new Label($"${amount:N0} revenue received\nfor order {order}!");
        label.pickingMode = PickingMode.Ignore;
        label.style.position = Position.Absolute;
        label.style.left = 0; label.style.right = 0;
        label.style.top = Length.Percent(22);
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.fontSize = FontSizePx;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = new StyleColor(Green);
        label.style.unityTextOutlineWidth = 0.35f;
        label.style.unityTextOutlineColor = new StyleColor(new Color(0f, 0.12f, 0f, 0.95f));
        label.style.opacity = 0f;
        ApplyFont(label);
        _root.Add(label);
        label.BringToFront();

        // Pop in (overshoot), hold, fade out the last stretch.
        label.experimental.animation.Start(0f, 1f, duration, (e, t) =>
        {
            float pop = t < 0.12f ? Mathf.Sin(t / 0.12f * Mathf.PI * 0.5f) : 1f;
            float overshoot = t < 0.12f ? 1f + 0.25f * Mathf.Sin(t / 0.12f * Mathf.PI) : 1f;
            float s = Mathf.Lerp(0.5f, 1f, pop) * overshoot;
            e.transform.scale = new Vector3(s, s, 1f);
            e.style.opacity = t < 0.8f ? Mathf.Min(1f, t * 10f) : 1f - Mathf.InverseLerp(0.8f, 1f, t);
        });

        ConfettiFx.Play(_root, _confettiStrip);

        label.schedule.Execute(() =>
        {
            label.RemoveFromHierarchy();
            PlayNext();
        }).ExecuteLater(duration + 40);
    }

    private static void ApplyFont(Label label)
    {
        if (_lilita == null)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("LilitaOne-Regular t:Font");
            if (guids.Length > 0)
                _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            _lilita = Resources.Load<Font>("Resource_Fonts/LilitaOne-Regular");
#endif
        }
        if (_lilita != null)
            label.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_lilita));
    }
}
