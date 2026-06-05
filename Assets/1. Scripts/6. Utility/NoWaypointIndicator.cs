using UnityEngine;
using TMPro;
using UnityEngine.AI;

/// <summary>
/// Displays a floating symbol above the agent's head in two distinct situations:
///
///   "?" — No meaningful destination:
///         No waypoints exist for this agent's group, only one waypoint exists and
///         the agent is already there, or every waypoint produces a PathPartial /
///         PathInvalid result.
///
///   "!" — Physically blocked:
///         The agent HAS a fully reachable destination (PathComplete) but its
///         velocity has been zero for longer than physicallyBlockedGrace seconds —
///         a wall, door, equipment, or congestion is in the way.
///
/// A short grace timer prevents flicker during normal between-waypoint hops.
/// AgentAnimation reads IsShowingIndicator to trigger the waving animation.
/// </summary>
[RequireComponent(typeof(AiNavigation))]
[RequireComponent(typeof(NavMeshAgent))]
public class NoWaypointIndicator : MonoBehaviour
{
    [Header("Position")]
    [SerializeField] private float heightAboveHead = 2.4f;

    [Header("Appearance")]
    [SerializeField] private Color markColor = new Color(1f, 0.10f, 0.08f, 1f);
    [SerializeField] private float fontSize = 6f;

    [Header("Hover")]
    [SerializeField] private float hoverAmplitude = 0.18f;
    [SerializeField] private float hoverSpeed     = 1.1f;

    [Header("Rotation")]
    [SerializeField] private float yRotateDeg = 30f;
    [SerializeField] private float yRotateSpeed = 0.7f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration  = 0.25f;
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Header("Stuck Detection")]
    [Tooltip("Seconds without a meaningful destination before the indicator appears. " +
             "Prevents flicker during the brief gap between waypoints.")]
    [SerializeField] private float stuckGraceSeconds = 2f;

    [Tooltip("Seconds with a complete NavMesh path but zero velocity before the " +
             "physically-blocked (!) indicator appears.")]
    [SerializeField] private float physicallyBlockedGrace = 3f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private AiNavigation _aiNav;
    private NavMeshAgent _agent;
    private Transform    _pivot;
    private TextMeshPro  _tmp;

    private float _alpha;
    private float _hoverPhase;
    private float _stuckTimer;         // seconds without a meaningful destination
    private float _velocityStuckTimer; // seconds at zero velocity with a complete path

    /// <summary>
    /// True while the indicator is fading in or fully visible.
    /// AgentAnimation uses this to trigger the waving clip.
    /// </summary>
    public bool IsShowingIndicator => _alpha > 0.05f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _aiNav = GetComponent<AiNavigation>();
        _agent = GetComponent<NavMeshAgent>();

        BuildIndicator();

        _hoverPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void BuildIndicator()
    {
        var go = new GameObject("_NoWaypointIndicator");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, heightAboveHead, 0f);

        _tmp = go.AddComponent<TextMeshPro>();
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
        // ── Physically-blocked detection ──────────────────────────────────────
        // An agent that has a fully-complete NavMesh path but is not moving is
        // blocked by a physical obstacle the NavMesh doesn't know about.
        bool hasCompletePath = _agent.isActiveAndEnabled && _agent.isOnNavMesh
            && !_agent.pathPending && _agent.hasPath
            && _agent.path.status == NavMeshPathStatus.PathComplete
            && _agent.remainingDistance > _agent.stoppingDistance + 0.3f;

        // Off-mesh link traversal produces zero velocity legitimately — don't count it.
        if (hasCompletePath && !_agent.isOnOffMeshLink && _agent.velocity.sqrMagnitude < 0.01f)
            _velocityStuckTimer += Time.deltaTime;
        else
            _velocityStuckTimer = 0f;

        bool physicallyBlocked = _velocityStuckTimer >= physicallyBlockedGrace;

        // ── No-destination detection ──────────────────────────────────────────
        if (HasMeaningfulDestination())
            _stuckTimer = 0f;
        else
            _stuckTimer += Time.deltaTime;

        bool noDestination = _stuckTimer >= stuckGraceSeconds;

        // ── Decide symbol and visibility ──────────────────────────────────────
        bool wantsVisible = noDestination || physicallyBlocked;

        // "!" = has waypoints but is blocked; "?" = no waypoints at all.
        if (wantsVisible)
        {
            bool showExclamation = physicallyBlocked
                || (noDestination && _aiNav.HasWaypoints);
            _tmp.text = showExclamation ? "!" : "?";
        }

        float targetAlpha = wantsVisible ? 1f : 0f;
        float fadeSpeed   = wantsVisible
            ? 1f / Mathf.Max(fadeInDuration,  0.001f)
            : 1f / Mathf.Max(fadeOutDuration, 0.001f);

        _alpha = Mathf.MoveTowards(_alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        if (_alpha <= 0f)
        {
            if (_pivot.gameObject.activeSelf) _pivot.gameObject.SetActive(false);
            return;
        }

        if (!_pivot.gameObject.activeSelf) _pivot.gameObject.SetActive(true);

        Color c = _tmp.color;
        c.a        = _alpha;
        _tmp.color = c;

        // ── Hover ─────────────────────────────────────────────────────────────
        float hover = Mathf.Sin(Time.time * hoverSpeed + _hoverPhase) * hoverAmplitude;
        _pivot.localPosition = new Vector3(0f, heightAboveHead + hover, 0f);

        // ── Billboard + Y wobble ──────────────────────────────────────────────
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

    /// <summary>
    /// Returns true when the agent genuinely has somewhere meaningful to go.
    /// False for: no waypoints, PathPartial/PathInvalid, no path pending, or arrived
    /// at the only waypoint with nowhere further to travel.
    /// </summary>
    private bool HasMeaningfulDestination()
    {
        if (!_aiNav.HasWaypoints) return false;
        if (!_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return true;
        if (_agent.pathPending) return true;

        if (_agent.hasPath
            && _agent.path.status == NavMeshPathStatus.PathComplete
            && _agent.remainingDistance > _agent.stoppingDistance + 0.2f)
            return true;

        return false;
    }
}
