using UnityEngine;

/// <summary>
/// A single expression bubble that floats above an agent's head: a camera-facing sprite
/// that gently bobs up/down and sways on its Y axis, fading in and out as emotes come and go.
///
/// Two channels feed it:
///   • Priority  — set/cleared by <see cref="NoWaypointIndicator"/> (the "I'm stuck" alert).
///                 Always wins and stays up until cleared.
///   • Transient — fired by <see cref="AgentEmoteController"/> (the random idle emotes).
///                 Shows for a fixed duration, then fades.
///
/// This is the shared display primitive — other scripts decide WHAT to show and WHEN;
/// this just renders it nicely. One per agent (auto-added via RequireComponent).
/// </summary>
[DisallowMultipleComponent]
public class EmoteBubble : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Height above the agent pivot, in metres. ~2.5 sits just above the head.")]
    [SerializeField] private float heightAboveHead = 2.5f;
    [Tooltip("World scale of the bubble sprite.")]
    [SerializeField] private float bubbleScale = 1.2f;

    [Header("Bob (vertical)")]
    [Tooltip("Vertical bob amplitude in world units.")]
    [SerializeField] private float bobAmplitude = 0.12f;
    [Tooltip("Vertical bob speed in cycles per second.")]
    [SerializeField] private float bobSpeed = 1.1f;

    [Header("Sway (Y rotation)")]
    [Tooltip("Maximum left/right sway around the Y axis, in degrees.")]
    [SerializeField] private float swayDegrees = 16f;
    [Tooltip("Sway speed in cycles per second.")]
    [SerializeField] private float swaySpeed = 0.6f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration  = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private Transform      _pivot;
    private SpriteRenderer _sr;
    private float          _phase;
    private float          _alpha;

    private Sprite _priority;       // persistent alert (stuck indicator)
    private Sprite _transient;      // timed random emote
    private float  _transientTimer;
    private Sprite _displayed;      // currently-assigned sprite (held through fade-out)

    /// <summary>True while the bubble is at least faintly visible.</summary>
    public bool IsVisible => _alpha > 0.05f;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Show a sprite and keep it up until <see cref="ClearPriority"/>. Overrides random emotes.</summary>
    public void SetPriority(Sprite sprite) => _priority = sprite;

    /// <summary>Clear the persistent alert sprite.</summary>
    public void ClearPriority() => _priority = null;

    /// <summary>Flash an emote for <paramref name="duration"/> seconds. Ignored while a priority emote is up.</summary>
    public void Play(Sprite sprite, float duration)
    {
        if (sprite == null) return;
        _transient      = sprite;
        _transientTimer = Mathf.Max(duration, 0.1f);
    }

    /// <summary>Immediately hides the bubble and clears all pending emotes. MUST be called
    /// before disabling this component — Update() (which normally fades it out) won't run
    /// once disabled, so a bubble visible the instant before would otherwise freeze on
    /// screen forever (e.g. an MHE vehicle's bubble when its operator vacates, or an
    /// operator's own bubble the moment they board).</summary>
    public void ForceHide()
    {
        _priority       = null;
        _transient      = null;
        _transientTimer = 0f;
        _displayed      = null;
        _alpha          = 0f;
        if (_pivot != null && _pivot.gameObject.activeSelf) _pivot.gameObject.SetActive(false);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _phase = Random.Range(0f, Mathf.PI * 2f);
        BuildBubble();
    }

    private void BuildBubble()
    {
        var go = new GameObject("_EmoteBubble");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, heightAboveHead, 0f);
        go.transform.localScale    = Vector3.one * bubbleScale;

        _sr = go.AddComponent<SpriteRenderer>();
        _sr.color                = new Color(1f, 1f, 1f, 0f);
        _sr.receiveShadows       = false;
        _sr.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;

        _pivot = go.transform;
        go.SetActive(false);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_transientTimer > 0f) _transientTimer -= Time.deltaTime;

        // Priority always wins; otherwise the transient shows while its timer runs.
        Sprite effective = _priority != null
            ? _priority
            : (_transientTimer > 0f ? _transient : null);

        float target = effective != null ? 1f : 0f;
        float dur    = effective != null ? fadeInDuration : fadeOutDuration;
        _alpha = Mathf.MoveTowards(_alpha, target, (1f / Mathf.Max(dur, 0.001f)) * Time.deltaTime);

        if (effective != null) _displayed = effective;

        // Hidden — disable the child and bail.
        if (_alpha <= 0f)
        {
            if (_pivot.gameObject.activeSelf) _pivot.gameObject.SetActive(false);
            return;
        }

        if (!_pivot.gameObject.activeSelf) _pivot.gameObject.SetActive(true);
        if (_displayed != null && _sr.sprite != _displayed) _sr.sprite = _displayed;

        Color c = _sr.color;
        c.a       = _alpha;
        _sr.color = c;

        // ── Bob ────────────────────────────────────────────────────────────────
        float bob = Mathf.Sin(Time.time * bobSpeed + _phase) * bobAmplitude;
        _pivot.localPosition = new Vector3(0f, heightAboveHead + bob, 0f);

        // ── Billboard + Y sway ───────────────────────────────────────────────────
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCamera = cam.transform.position - _pivot.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.01f)
            {
                Quaternion billboard = Quaternion.LookRotation(toCamera.normalized);
                float sway = Mathf.Sin(Time.time * swaySpeed * Mathf.PI * 2f + _phase) * swayDegrees;
                _pivot.rotation = billboard * Quaternion.Euler(0f, sway, 0f);
            }
        }
    }
}
