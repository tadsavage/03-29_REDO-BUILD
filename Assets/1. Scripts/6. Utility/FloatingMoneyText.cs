using UnityEngine;
using TMPro;

/// <summary>
/// World-space "$" popup à la Mario coins. Red number rises out of an object when
/// money LEAVES capital (a purchase / placement); green number rises when money
/// COMES BACK (a refund / delete). Also supports a persistent label that just
/// hovers (used by the contamination system to show product value lost).
///
/// Spawned entirely from code — no prefab/scene setup required:
///   FloatingMoneyText.Show(worldPos, -cost);   // red  "-$1,200"
///   FloatingMoneyText.Show(worldPos, +refund);  // green "+$1,200"
///   FloatingMoneyText.Show(worldPos, 0);        // green "$0" (e.g. spoiled refund)
///
/// All the look/timing knobs are static so they can be tuned in one place; if we
/// later want them in the Inspector we can promote them to a small settings SO.
/// </summary>
public class FloatingMoneyText : MonoBehaviour
{
    // ── Tunables (shared defaults; tweak here for now) ─────────────────────────
    public static float RiseHeight = 1.2f;   // world units the text floats upward
    public static float Lifetime   = 1.15f;  // seconds before it deletes itself
    public static float FontSize   = 6f;     // TMP world font size
    public static float FadeStart  = 0.55f;  // 0–1 of lifetime where fade begins

    public static Color SpendColor = new Color(0.93f, 0.27f, 0.22f); // red  (money out)
    public static Color GainColor  = new Color(0.30f, 0.86f, 0.37f); // green (money in)

    // ── Instance state ─────────────────────────────────────────────────────────
    private enum Mode { Rising, Persistent }
    private Mode        _mode = Mode.Rising;
    private TMP_Text    _label;
    private Transform   _cam;
    private Vector3     _startPos;
    private float       _age;

    // ── Public API ──────────────────────────────────────────────────────────────

    /// <summary>Spawn a rising/fading money popup. Positive = green gain, negative
    /// = red spend, zero = green "$0".</summary>
    public static FloatingMoneyText Show(Vector3 worldPos, int signedAmount)
    {
        var go = new GameObject("FloatingMoney");
        go.transform.position = worldPos;
        var ft = go.AddComponent<FloatingMoneyText>();
        ft.Init(signedAmount, Mode.Rising, null);
        return ft;
    }

    /// <summary>Spawn a persistent hovering label (no rise/fade). Parented to
    /// <paramref name="parent"/> so it is cleaned up when that object is destroyed.
    /// Returns the label so the caller can destroy it early if needed.</summary>
    public static FloatingMoneyText ShowPersistent(Transform parent, Vector3 worldPos, int signedAmount)
    {
        var go = new GameObject("FloatingMoney_Persistent");
        if (parent != null) go.transform.SetParent(parent, true);
        go.transform.position = worldPos;
        var ft = go.AddComponent<FloatingMoneyText>();
        ft.Init(signedAmount, Mode.Persistent, null);
        return ft;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private void Init(int signedAmount, Mode mode, TMP_FontAsset font)
    {
        _mode     = mode;
        _startPos = transform.position;

        _label = gameObject.AddComponent<TextMeshPro>();
        if (font != null) _label.font = font;
        _label.text      = FormatAmount(signedAmount);
        _label.color     = signedAmount < 0 ? SpendColor : GainColor;
        _label.fontSize  = FontSize;
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontStyle = FontStyles.Bold;
        _label.enableWordWrapping = false;
        _label.outlineWidth = 0.18f;
        _label.outlineColor = new Color32(0, 0, 0, 200);

        _cam = Camera.main != null ? Camera.main.transform : null;
    }

    private static string FormatAmount(int signed)
    {
        if (signed == 0) return "$0";
        string sign = signed < 0 ? "-" : "+";
        return $"{sign}${Mathf.Abs(signed):N0}";
    }

    // ── Update ──────────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        // Re-acquire camera if it changed (scene load) or was missing.
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

        // Billboard: align with the camera so text is always readable (never mirrored).
        if (_cam != null) transform.forward = _cam.forward;

        if (_mode == Mode.Persistent) return;

        _age += Time.deltaTime;
        float t = _age / Lifetime;

        // Ease-out rise.
        float eased = 1f - (1f - t) * (1f - t);
        transform.position = _startPos + Vector3.up * (RiseHeight * eased);

        // Fade in the back half of its life.
        if (t >= FadeStart && _label != null)
        {
            float f = Mathf.InverseLerp(FadeStart, 1f, t);
            Color c = _label.color;
            c.a = 1f - f;
            _label.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
