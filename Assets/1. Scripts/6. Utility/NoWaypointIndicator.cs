using UnityEngine;
using TMPro;
using UnityEngine.AI;

/// <summary>
/// Displays a red "?" above the agent's head whenever it has no meaningful destination:
///   • No waypoints exist for its group.
///   • Only one waypoint exists and the agent is already there (nowhere further to go).
///   • Every waypoint is unreachable — e.g. a sealed room with no door produces a
///     PathPartial or PathInvalid result, so the agent is effectively stuck.
///
/// A short grace timer prevents the ? from flickering during the normal
/// between-waypoint moment when the agent briefly has no active path.
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
    [SerializeField] private float yRotateDeg = 30f;   // peak wobble in degrees each side
    [SerializeField] private float yRotateSpeed = 0.7f; // oscillations per second

    [Header("Fade")]
    [SerializeField] private float fadeInDuration  = 0.25f;
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Header("Stuck Detection")]
    [Tooltip("Seconds without a meaningful destination before the ? appears. "  +
             "Keeps it from flickering during the brief gap between waypoints.")]
    [SerializeField] private float stuckGraceSeconds = 2f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private AiNavigation _aiNav;
    private NavMeshAgent _agent;
    private Transform    _pivot;   // child that bobs + rotates
    private TextMeshPro  _tmp;

    private float _alpha;
    private float _hoverPhase;    // randomised so agents don't all bob in sync
    private float _stuckTimer;    // seconds agent has had no meaningful destination

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
        go.SetActive(false); // start hidden
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        // Accumulate time without a meaningful destination; reset the moment one exists.
        if (HasMeaningfulDestination())
            _stuckTimer = 0f;
        else
            _stuckTimer += Time.deltaTime;

        // Only show after the grace window to avoid flickering during normal waypoint hops.
        bool wantsVisible = _stuckTimer >= stuckGraceSeconds;

        float targetAlpha = wantsVisible ? 1f : 0f;
        float fadeSpeed   = wantsVisible
            ? 1f / Mathf.Max(fadeInDuration,  0.001f)
            : 1f / Mathf.Max(fadeOutDuration, 0.001f);

        _alpha = Mathf.MoveTowards(_alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        // Toggle the GameObject so it costs nothing when fully invisible.
        if (_alpha <= 0f)
        {
            if (_pivot.gameObject.activeSelf) _pivot.gameObject.SetActive(false);
            return;
        }

        if (!_pivot.gameObject.activeSelf) _pivot.gameObject.SetActive(true);

        // Apply alpha.
        Color c = _tmp.color;
        c.a        = _alpha;
        _tmp.color = c;

        // ── Hover (bob up and down in local space) ────────────────────────────
        float hover = Mathf.Sin(Time.time * hoverSpeed + _hoverPhase) * hoverAmplitude;
        _pivot.localPosition = new Vector3(0f, heightAboveHead + hover, 0f);

        // ── Billboard + subtle Y wobble ───────────────────────────────────────
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
    /// True when the agent genuinely has somewhere to go right now.
    ///
    /// Returns false when:
    ///   - there are no waypoints for this agent's group
    ///   - the path is PathPartial or PathInvalid (destination is unreachable)
    ///   - there is no path and nothing is pending (SetDestination was never called
    ///     or the agent is stuck with nowhere new to go)
    ///   - the agent has arrived at its only waypoint and remaining distance is 0
    ///     (it has nowhere further to travel)
    /// </summary>
    private bool HasMeaningfulDestination()
    {
        // No waypoints registered for this agent's group at all.
        if (!_aiNav.HasWaypoints) return false;

        // Agent is off-mesh or disabled — can't evaluate, give benefit of the doubt.
        if (!_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return true;

        // Path is still being calculated — assume it will succeed.
        if (_agent.pathPending) return true;

        // Path exists, is fully reachable, and the agent still has real ground to cover.
        // A PathComplete path with near-zero remaining distance means the agent has
        // arrived (or is at its only waypoint) — treat that as "no destination".
        if (_agent.hasPath
            && _agent.path.status == NavMeshPathStatus.PathComplete
            && _agent.remainingDistance > _agent.stoppingDistance + 0.2f)
            return true;

        // All other states: no path, partial path, invalid path, or arrived with
        // no further meaningful waypoint to queue up.
        return false;
    }
}
