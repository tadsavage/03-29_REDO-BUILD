using UnityEngine;
using TMPro;
using UnityEngine.AI;

/// <summary>
/// Displays a red "?" above the agent when it has nowhere to go (0–1 waypoints)
/// or is physically stuck (hasn't moved in stuckSeconds despite having a valid path).
///
/// Sequence:
///   Condition true → graceBeforeShow (2.5 s) → fade IN (2 s) → fully visible
///   → AgentAnimation starts wave after 1.5 s more (waveDelay = 3.5 s in AgentAnimation)
///   Condition clears → fade OUT (2.5 s) → hidden
/// </summary>
[RequireComponent(typeof(AiNavigation))]
[RequireComponent(typeof(NavMeshAgent))]
public class NoWaypointIndicator : MonoBehaviour
{
    [Header("Position")]
    [Tooltip("Height above the agent's pivot to place the indicator. Adjust based on agent height and pivot point.")]
    [SerializeField] private float heightAboveHead = 2.4f;

    [Header("Appearance")]
    [SerializeField] private Color markColor = new Color(1f, 0.10f, 0.08f, 1f);
    [SerializeField] private float fontSize = 6f;

    [Header("Hover")]
    [Tooltip("Vertical bobbing amplitude in world units.")]
    [SerializeField] private float hoverAmplitude = 0.18f;
    [Tooltip("Vertical bobbing speed in cycles per second.")]
    [SerializeField] private float hoverSpeed     = 1.1f;

    [Header("Rotation Wobble")]
    [Tooltip("Maximum rotation angle around the Y axis in degrees.")]
    [SerializeField] private float yRotateDeg   = 30f;
    [Tooltip("Rotation speed around the Y axis in cycles per second.")]
    [SerializeField] private float yRotateSpeed = 0.7f;

    [Header("Timing")]
    [Tooltip("Seconds the condition must be true before the ? starts to appear.")]
    [SerializeField] private float graceBeforeShow = 2.5f;
    [Tooltip("Seconds to fade the ? in once grace has expired.")]
    [SerializeField] private float fadeInDuration  = 2.0f;
    [Tooltip("Seconds to fade the ? out once the condition clears.")]
    [SerializeField] private float fadeOutDuration = 2.5f;

    [Header("Stuck Detection")]
    [Tooltip("Agent must go this many seconds without meaningful movement (and have enough waypoints) before being counted as stuck.")]
    [SerializeField] private float stuckSeconds = 5f;
    [Tooltip("Minimum distance moved per 0.5 s check to NOT be considered stuck.")]
    [SerializeField] private float stuckMoveThreshold = 0.1f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private AiNavigation _aiNav;
    private NavMeshAgent _agent;
    private Transform    _pivot;
    private TextMeshPro  _tmp;

    private float _alpha;
    private float _hoverPhase;

    private float   _conditionTimer;   // time current condition has been continuously true
    private float   _stuckTimer;       // accumulated time without movement
    private float   _posCheckTimer;    // sub-timer for 0.5 s position samples
    private Vector3 _lastCheckedPos;

    /// <summary>True while the "?" is fading in or fully visible — polled by AgentAnimation.</summary>
    public bool IsShowingIndicator => _alpha > 0.05f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _aiNav = GetComponent<AiNavigation>();
        _agent = GetComponent<NavMeshAgent>();

        BuildIndicator();

        _hoverPhase     = Random.Range(0f, Mathf.PI * 2f);
        _lastCheckedPos = transform.position;
    }

    private void BuildIndicator()
    {
        var go = new GameObject("_NoWaypointIndicator");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, heightAboveHead, 0f);

        _tmp           = go.AddComponent<TextMeshPro>();
        _tmp.text      = "?";
        _tmp.fontSize  = fontSize;
        _tmp.fontStyle = FontStyles.Bold;
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.color     = new Color(markColor.r, markColor.g, markColor.b, 0f);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.receiveShadows    = false;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        _pivot = go.transform;
        go.SetActive(false);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        bool noWaypoints = !_aiNav.HasEnoughWaypoints;

        // ── Stuck detection ───────────────────────────────────────────────────
        // Only run when the agent HAS enough waypoints — an agent with 0–1 waypoints
        // is expected to stand still, so zero velocity isn't "stuck".
        if (!noWaypoints && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
        {
            _posCheckTimer += Time.deltaTime;
            if (_posCheckTimer >= 0.5f)
            {
                float moved = Vector3.Distance(transform.position, _lastCheckedPos);
                if (moved < stuckMoveThreshold)
                    _stuckTimer += _posCheckTimer;
                else
                    _stuckTimer = 0f;

                _lastCheckedPos = transform.position;
                _posCheckTimer  = 0f;
            }
        }
        else
        {
            // Reset so a freshly-added waypoint doesn't immediately re-trigger stuck.
            _stuckTimer    = 0f;
            _posCheckTimer = 0f;
            _lastCheckedPos = transform.position;
        }

        // ── Condition ─────────────────────────────────────────────────────────
        bool conditionActive = noWaypoints || (_stuckTimer >= stuckSeconds);

        if (conditionActive)
            _conditionTimer += Time.deltaTime;
        else
            _conditionTimer = 0f;

        // ── Fade target ───────────────────────────────────────────────────────
        bool wantsVisible = _conditionTimer >= graceBeforeShow;

        float targetAlpha = wantsVisible ? 1f : 0f;
        float fadeSpeed   = wantsVisible
            ? 1f / Mathf.Max(fadeInDuration,  0.001f)
            : 1f / Mathf.Max(fadeOutDuration, 0.001f);

        _alpha = Mathf.MoveTowards(_alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        // ── Visibility toggle ─────────────────────────────────────────────────
        if (_alpha <= 0f)
        {
            if (_pivot.gameObject.activeSelf)
                _pivot.gameObject.SetActive(false);
            return;
        }

        if (!_pivot.gameObject.activeSelf)
            _pivot.gameObject.SetActive(true);

        // ── Apply alpha ────────────────────────────────────────────────────────
        Color c = _tmp.color;
        c.a        = _alpha;
        _tmp.color = c;

        // ── Hover (bob up/down) ────────────────────────────────────────────────
        float hover = Mathf.Sin(Time.time * hoverSpeed + _hoverPhase) * hoverAmplitude;
        _pivot.localPosition = new Vector3(0f, heightAboveHead + hover, 0f);

        // ── Billboard + subtle Y wobble ────────────────────────────────────────
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCamera = cam.transform.position - _pivot.position;
            toCamera.y = 0f;

            if (toCamera.sqrMagnitude > 0.01f)
            {
                Quaternion billboardRot = Quaternion.LookRotation(-toCamera.normalized);
                float wobble = Mathf.Sin(Time.time * yRotateSpeed * Mathf.PI * 2f + _hoverPhase) * yRotateDeg;
                _pivot.rotation = billboardRot * Quaternion.Euler(0f, wobble, 0f);
            }
        }
    }
}
