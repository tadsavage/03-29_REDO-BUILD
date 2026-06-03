using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Shows a softly-bobbing red "?" above an agent when they have no navigation waypoints.
/// Initialized in Start() so ghosts (which never reach Start) never build the pivot child.
/// Call Show()/Hide() from AiNavigation after FindWaypoints() runs.
/// </summary>
[DisallowMultipleComponent]
public class NoWaypointIndicator : MonoBehaviour
{
    [Tooltip("Height above the agent's local origin (should clear the model's head).")]
    [SerializeField] private float headHeight = 2.75f;
    [Tooltip("How many units the '?' bobs up and down each cycle.")]
    [SerializeField] private float bobAmplitude = 0.07f;
    [Tooltip("Bob cycles per second.")]
    [SerializeField] private float bobSpeed = 0.9f;
    [Tooltip("Subtle left/right drift amplitude (x-axis).")]
    [SerializeField] private float driftAmplitude = 0.025f;
    [Tooltip("Font size of the world-space '?' label.")]
    [SerializeField] private float fontSize = 8f;

    [Tooltip("How long the '?' takes to fade out once a path is found.")]
    [SerializeField] private float fadeDuration = 1.5f;

    private Transform _pivot;
    private TMP_Text _label;
    private float _t;
    private Camera _cam;
    private bool _visible;
    private bool _isFading;
    private Coroutine _fadeCoroutine;
    private bool _pendingShow;           // buffered if Show() is called before Start()
    private float _originalOutlineAlpha; // saved so we can restore it after a fade

    // Start() is NOT called on ghost preview objects (they are destroyed before Start runs),
    // so moving creation here ensures ghosts never build the pivot child.
    private void Start()
    {
        _cam = Camera.main;

        var go = new GameObject("NoWaypoint_?");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, headHeight, 0f);
        _pivot = go.transform;

        _label = go.AddComponent<TextMeshPro>();
        _label.text = "?";
        _label.color = Color.red;
        _label.fontSize = fontSize;
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontStyle = FontStyles.Bold;
        _label.outlineWidth = 0.22f;
        _label.outlineColor = new Color32(20, 0, 0, 220);
        _label.enableWordWrapping = false;
        _originalOutlineAlpha = ((Color)_label.outlineColor).a; // ~0.86

        go.SetActive(_pendingShow);
        _visible = _pendingShow;
    }

    /// <summary>Instantly show the indicator. Cancels any in-progress fade.</summary>
    public void Show()
    {
        if (_pivot != null)
        {
            CancelFade();
            if (_visible) return;
            _visible = true;
            _pivot.gameObject.SetActive(true);
            // Restore full opacity in case a previous fade was interrupted
            if (_label != null) { var c = _label.color; c.a = 1f; _label.color = c; }
        }
        else
        {
            _pendingShow = true; // Start() hasn't run yet; activate when it does
        }
    }

    /// <summary>Instantly hide the indicator with no fade.</summary>
    public void Hide()
    {
        _pendingShow = false;
        CancelFade();
        if (!_visible) return;
        _visible = false;
        if (_pivot != null)
            _pivot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Fade the '?' out over fadeDuration seconds once a valid path is found.
    /// No-op if the indicator is already hidden or a fade is already running.
    /// </summary>
    public void FadeOut()
    {
        if (_pivot == null || !_visible || _isFading) return;
        _fadeCoroutine = StartCoroutine(FadeOutRoutine());
    }

    private void CancelFade()
    {
        if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; }
        _isFading = false;
        RestoreFullAlpha();
    }

    private void RestoreFullAlpha()
    {
        if (_label == null) return;
        var c = _label.color;          c.a = 1f;                    _label.color = c;
        var o = (Color)_label.outlineColor; o.a = _originalOutlineAlpha; _label.outlineColor = o;
    }

    private IEnumerator FadeOutRoutine()
    {
        _isFading = true;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            if (_label != null)
            {
                // Fade both face color AND outline — TMP keeps them independent
                var c = _label.color;               c.a = Mathf.Lerp(1f, 0f, t); _label.color = c;
                var o = (Color)_label.outlineColor; o.a = Mathf.Lerp(_originalOutlineAlpha, 0f, t);
                _label.outlineColor = o;
            }
            yield return null;
        }
        _isFading      = false;
        _visible       = false;
        _fadeCoroutine = null;
        if (_pivot != null) _pivot.gameObject.SetActive(false);
        RestoreFullAlpha(); // ready for next Show()
    }

    private void LateUpdate()
    {
        if (_pivot == null || !_visible) return;

        _t += Time.deltaTime;

        float yBob   = Mathf.Sin(_t * bobSpeed * Mathf.PI * 2f) * bobAmplitude;
        float xDrift = Mathf.Sin(_t * bobSpeed * Mathf.PI * 2f * 0.37f) * driftAmplitude;

        _pivot.localPosition = new Vector3(xDrift, headHeight + yBob, 0f);

        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
            _pivot.forward = _cam.transform.forward;
    }
}
