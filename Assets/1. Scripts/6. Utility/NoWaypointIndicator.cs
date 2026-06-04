using UnityEngine;
using TMPro;
using UnityEngine.AI;

/// <summary>
/// Displays a red "?" above the agent's head when it has no waypoints to travel to.
/// Fades out over 1.5 s once the agent has a destination and is moving.
/// Reads directly from AiNavigation.HasWaypoints and NavMeshAgent — no callbacks needed.
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

    // ── Runtime ───────────────────────────────────────────────────────────────
    private AiNavigation  _aiNav;
    private NavMeshAgent  _agent;
    private Transform     _pivot;   // child that bobs + rotates
    private TextMeshPro   _tmp;

    private float _alpha;
    private float _hoverPhase;   // randomised per agent so they don't all bob in sync

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _aiNav = GetComponent<AiNavigation>();
        _agent = GetComponent<NavMeshAgent>();

        BuildIndicator();

        // Stagger the hover and wobble so a crowd of agents looks natural
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

        // Disable shadows on the question mark mesh so it doesn't cast weird blobs
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
        // Show whenever there are no waypoints OR the agent has no active path.
        // We intentionally use HasWaypoints as the primary gate so the indicator
        // does NOT flicker during the normal between-waypoint gap.
        bool agentMoving = _agent.isActiveAndEnabled
                        && (_agent.hasPath || _agent.pathPending)
                        && _agent.velocity.sqrMagnitude > 0.01f;

        bool wantsVisible = !_aiNav.HasWaypoints || (_aiNav.HasWaypoints && !agentMoving && !_agent.hasPath && !_agent.pathPending);

        // Fade alpha toward target
        float targetAlpha = wantsVisible ? 1f : 0f;
        float fadeSpeed   = wantsVisible
            ? 1f / Mathf.Max(fadeInDuration,  0.001f)
            : 1f / Mathf.Max(fadeOutDuration, 0.001f);

        _alpha = Mathf.MoveTowards(_alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        // Toggle the GameObject so it costs nothing when fully invisible
        if (_alpha <= 0f)
        {
            if (_pivot.gameObject.activeSelf) _pivot.gameObject.SetActive(false);
            return;
        }

        if (!_pivot.gameObject.activeSelf) _pivot.gameObject.SetActive(true);

        // Apply alpha
        Color c = _tmp.color;
        c.a        = _alpha;
        _tmp.color = c;

        // ── Hover (bob up and down in local space) ────────────────────────────
        float hover = Mathf.Sin(Time.time * hoverSpeed + _hoverPhase) * hoverAmplitude;
        _pivot.localPosition = new Vector3(0f, heightAboveHead + hover, 0f);

        // ── Billboard + subtle Y wobble ───────────────────────────────────────
        // Always face the main camera so the text is readable, then layer a
        // gentle Y oscillation on top for the "slowly rotating" feel.
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
