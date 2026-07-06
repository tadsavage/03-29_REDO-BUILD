using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Raises an alert emote above the agent when it has nowhere to go (0–1 waypoints)
/// or is physically stuck (hasn't moved in stuckSeconds despite having a valid path).
///
/// This component only does the DETECTION + timing; the visual is the shared
/// <see cref="EmoteBubble"/>, driven via its priority channel so the alert always
/// wins over the random idle emotes.
///
/// Sequence:
///   Condition true → graceBeforeShow (2.5 s) → bubble shows emote_exclamations (fades in)
///   Condition clears → bubble clears (fades out)
/// </summary>
[RequireComponent(typeof(AiNavigation))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EmoteBubble))]
public class NoWaypointIndicator : MonoBehaviour
{
    [Header("Alert emote")]
    [Tooltip("Bubble shown when the agent is stuck / has no path. Loaded from Resources/Emotes by name if no sprite is assigned.")]
    [SerializeField] private string alertEmoteName = "emote_exclamations";
    [Tooltip("Optional explicit sprite override. If set, used instead of looking up alertEmoteName.")]
    [SerializeField] private Sprite alertEmoteOverride;

    [Header("Timing")]
    [Tooltip("Seconds the condition must be true before the alert appears.")]
    [SerializeField] private float graceBeforeShow = 2.5f;

    [Header("Stuck Detection")]
    [Tooltip("Agent must go this many seconds without meaningful movement (and have enough waypoints) before being counted as stuck.")]
    [SerializeField] private float stuckSeconds = 5f;
    [Tooltip("Minimum distance moved per 0.5 s check to NOT be considered stuck.")]
    [SerializeField] private float stuckMoveThreshold = 0.1f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private AiNavigation _aiNav;
    private NavMeshAgent _agent;
    private EmoteBubble  _bubble;
    private Sprite       _alertSprite;

    private bool  _alertActive;       // currently requesting the alert (post-grace)

    private float   _conditionTimer;  // time current condition has been continuously true
    private float   _stuckTimer;      // accumulated time without movement
    private float   _posCheckTimer;   // sub-timer for 0.5 s position samples
    private Vector3 _lastCheckedPos;

    /// <summary>True while the alert is up — polled by AgentAnimation to drive the wave.</summary>
    public bool IsShowingIndicator => _alertActive;

    /// <summary>Immediately clears any active alert. MUST be called before disabling this
    /// component (e.g. MHEOperatorSlot.AssignOperator/AiNavigation.GoActive/GoIdle) — Update()
    /// is what normally clears the bubble when the condition goes away, and a disabled
    /// component's Update never runs, so a bubble that was up the instant before disabling
    /// would otherwise stay frozen on screen forever.</summary>
    public void ForceClear()
    {
        if (_alertActive)
        {
            _bubble?.ClearPriority();
            _alertActive = false;
        }
        _conditionTimer = 0f;
        _stuckTimer     = 0f;
        _posCheckTimer  = 0f;
    }

    /// <summary>
    /// Repoints waypoint-availability checks at an external AiNavigation (the MHE this
    /// employee is riding) instead of this employee's own — RequireComponent guarantees
    /// the local one always exists, so a null-check fallback can't tell "use the local
    /// one" apart from "no override set"; this is the explicit override path instead.
    /// Pass null to revert to this employee's own AiNavigation.
    /// </summary>
    public void UseExternalNavSource(AiNavigation source)
    {
        _aiNav = source != null ? source : GetComponent<AiNavigation>();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _aiNav  = GetComponent<AiNavigation>();
        _agent  = GetComponent<NavMeshAgent>();
        _bubble = GetComponent<EmoteBubble>();
        if (_bubble == null) _bubble = gameObject.AddComponent<EmoteBubble>();

        _alertSprite = alertEmoteOverride != null
            ? alertEmoteOverride
            : EmoteLibrary.Get(alertEmoteName);

        if (_alertSprite == null)
            Debug.LogWarning($"[NoWaypointIndicator] Alert emote '{alertEmoteName}' not found in Resources/Emotes.");

        _lastCheckedPos = transform.position;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        // An operator walking toward MHE equipment to board it has a real destination — just
        // not one that came from the Worker waypoint patrol — so it must not count as "nowhere
        // to go" and trigger the wave/stuck alert for the entire approach.
        bool noWaypoints = !_aiNav.HasEnoughWaypoints && !_aiNav.IsSeekingEquipment && !_aiNav.IsSeekingTask;

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
            _stuckTimer     = 0f;
            _posCheckTimer  = 0f;
            _lastCheckedPos = transform.position;
        }

        // ── Condition ─────────────────────────────────────────────────────────
        bool conditionActive = noWaypoints || (_stuckTimer >= stuckSeconds);

        if (conditionActive)
            _conditionTimer += Time.deltaTime;
        else
            _conditionTimer = 0f;

        // ── Drive the shared bubble's priority channel ─────────────────────────
        bool wantsAlert = _conditionTimer >= graceBeforeShow;

        if (wantsAlert && !_alertActive)
        {
            _bubble.SetPriority(_alertSprite);
            _alertActive = true;
        }
        else if (!wantsAlert && _alertActive)
        {
            _bubble.ClearPriority();
            _alertActive = false;
        }
    }
}
