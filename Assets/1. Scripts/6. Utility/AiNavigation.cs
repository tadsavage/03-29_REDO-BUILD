using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Handles agent role configuration and waypoint-based movement.
/// Startup is driven by NavMeshManager.OnNavMeshReady instead of blind polling,
/// so all agents begin moving the moment the bake finishes.
/// </summary>
public class AiNavigation : MonoBehaviour
{
    public enum AgentRole { Worker, Forklift }
    public AgentRole role;

    [Header("Footsteps")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.6f;

    private Transform[] waypoints;
    private NavMeshAgent agent;
    private int currentIndex = 0;
    private bool initialized = false;
    private VehicleThrottleAudio throttleAudio;
    private bool hasHonkedThisArrival = false;
    private bool _traversingLink = false;
    private AgentAnimation _agentAnimation;
    private AudioSource _footstepSource;
    private NoWaypointIndicator _indicator;
    private AmbientMumble _mumble;

    public bool HasWaypoints => waypoints != null && waypoints.Length > 0;

    /// <summary>True while the agent is playing the Climbing animation at a ledge.</summary>
    public bool IsTraversingLedgeUp   { get; private set; }
    /// <summary>True while the agent is playing the JumpingDown animation at a ledge.</summary>
    public bool IsTraversingLedgeDown { get; private set; }

    private void OnEnable()
    {
        NavMeshManager.OnNavMeshReady += OnNavMeshBaked;
    }

    private void OnDisable()
    {
        NavMeshManager.OnNavMeshReady -= OnNavMeshBaked;
    }

    // Called every time the NavMesh finishes a bake (e.g. a waypoint or tile was placed).
    // Refreshes the waypoint list and drives the indicator; if the agent is idle it kicks
    // off navigation immediately rather than waiting for the 60-frame polling cycle.
    private void OnNavMeshBaked()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;

        FindWaypoints();

        // Re-snap every uninitialized agent to the correct surface after each bake.
        // This corrects agents that were placed on a Foundation (target y≈1.06) before
        // the foundation floor NavMesh was baked: they settled on the yard mesh at y=0
        // and need to be lifted to the newly-baked floor surface.
        if (!initialized)
            SnapToCorrectSurface();

        if (!agent.isOnNavMesh) return;

        if (!initialized && waypoints != null && waypoints.Length > 0)
        {
            ApplyAgentCosts();
            currentIndex = Random.Range(0, waypoints.Length);
            if (agent.SetDestination(waypoints[currentIndex].position))
                initialized = true;
        }
        else if (initialized && !agent.hasPath && !agent.pathPending
                 && waypoints != null && waypoints.Length > 0)
        {
            GoToRandomWaypoint();
        }
    }

    /// <summary>
    /// Snaps the agent to the NavMesh surface it should be standing on.
    ///
    /// Uses a two-step search strategy:
    ///   1. Sample from 1.5 m ABOVE the agent's current transform position, radius 1.0 m.
    ///      • Foundation floor at y≈1.06 is only ~0.44 m from the sample point → found first.
    ///      • Yard floor at y=0 is 1.5 m away            → outside the radius, ignored.
    ///      • Adjacent Foundation cells are ~1.40 m away  → outside the radius, ignored.
    ///      This means a Foundation floor directly above takes priority over the yard floor
    ///      without accidentally teleporting agents to a Foundation in a neighbouring cell.
    ///   2. Fall back to a standard SamplePosition from the current transform position
    ///      (covers agents already at the correct height and plain yard-tile agents).
    /// </summary>
    private void SnapToCorrectSurface()
    {
        Vector3 sampleAbove = transform.position + Vector3.up * 1.5f;

        if (NavMesh.SamplePosition(sampleAbove, out NavMeshHit aboveHit, 1.0f, NavMesh.AllAreas)
            && aboveHit.position.y > transform.position.y + 0.3f)
        {
            // An elevated NavMesh surface sits directly above — snap up to it.
            try { agent.Warp(aboveHit.position); } catch { }
        }
        else
        {
            // No elevated surface nearby: snap from current position.
            // Handles agents already at the correct height and plain yard-level agents.
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit flatHit, 3f, NavMesh.AllAreas))
                try { agent.Warp(flatHit.position); } catch { }
        }
    }
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        throttleAudio = GetComponent<VehicleThrottleAudio>();
        _agentAnimation = GetComponent<AgentAnimation>();
        _indicator = GetComponent<NoWaypointIndicator>();
        _mumble    = GetComponent<AmbientMumble>();
        SetupAgentType();

        if (footstepClip != null)
        {
            _footstepSource = gameObject.AddComponent<AudioSource>();
            _footstepSource.clip = footstepClip;
            _footstepSource.loop = true;
            _footstepSource.volume = footstepVolume;
            _footstepSource.spatialBlend = 1f;
            _footstepSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _footstepSource.minDistance = 2f;
            _footstepSource.maxDistance = 20f;
            _footstepSource.playOnAwake = false;
        }

        // Auto-register agent type for door access — won't override a manually set tag
        if (GetComponent<AgentTypeTag>() == null)
        {
            var tag = gameObject.AddComponent<AgentTypeTag>();
            tag.agentType = role == AgentRole.Worker ? AgentType.Human : AgentType.MHE;
        }

        // Ensure we scan for waypoints immediately so the indicator can show up 
        // even if the agent starts off-mesh or is waiting for a bake.
        FindWaypoints();
    }

    private void SetupAgentType()
    {
        if (agent == null) return;

        // Must be false so TraverseLink owns the stair animation.
        // Default true makes Unity teleport the agent through links, often landing off-mesh.
        agent.autoTraverseOffMeshLink = false;

        string targetTypeName = role == AgentRole.Worker ? "Human" : "MHE";
        int count = NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            var settings = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(settings.agentTypeID) == targetTypeName)
            {
                agent.agentTypeID = settings.agentTypeID;
                break;
            }
        }
    }

    private void FindWaypoints()
    {
        Waypoint.WaypointGroup myGroup = RoleToWaypointGroup();

        Waypoint[] all = Object.FindObjectsByType<Waypoint>();

        var matching = new System.Collections.Generic.List<Transform>();
        foreach (var wp in all)
        {
            // Rats roam everywhere — handled in RatBehavior, not here.
            // All other agents only visit waypoints that include their group.
            if (wp.AllowsGroup(myGroup))
                matching.Add(wp.transform);
        }

        waypoints = matching.ToArray();

        // _indicator polls HasWaypoints itself — no callback needed

        //if (waypoints.Length <= 1)
           // Debug.LogWarning($"[AiNavigation] {gameObject.name}: no waypoints for group '{myGroup}'.");
    }

    private Waypoint.WaypointGroup RoleToWaypointGroup()
    {
        // Map AiNavigation role → WaypointGroup flag
        // Security and Boss use their own roles; Workers and Forklifts map directly.
        // Check the AgentTypeTag for more specific types.
        var tag = GetComponent<AgentTypeTag>();
        if (tag != null)
        {
            if (tag.agentType == AgentType.MHE) return Waypoint.WaypointGroup.MHE;
        }

        // Check object name for specific staff types until dedicated AiNavigation roles exist
        string n = gameObject.name.ToLower();
        if (n.Contains("boss"))         return Waypoint.WaypointGroup.Boss;
        if (n.Contains("security"))     return Waypoint.WaypointGroup.Security;
        if (n.Contains("exterminator")) return Waypoint.WaypointGroup.Exterminator;

        return role == AgentRole.Forklift
            ? Waypoint.WaypointGroup.MHE
            : Waypoint.WaypointGroup.Worker;
    }

    private IEnumerator Start()
    {
        if (agent == null) yield break;

        // ── Wait for NavMesh ────────────────────────────────────────────────────
        // Subscribe to NavMeshManager's ready event instead of polling every 0.5s.
        // This means ALL agents snap to the mesh and start moving at the same frame.
        if (!NavMeshManager.IsReady)
        {
            bool navReady = false;
            System.Action onReady = () => navReady = true;
            NavMeshManager.OnNavMeshReady += onReady;

            float timeout = 20f; // hard safety cap
            while (!navReady && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            NavMeshManager.OnNavMeshReady -= onReady;

            if (timeout <= 0f)
                Debug.LogWarning($"[AiNavigation] {gameObject.name}: timed out waiting for NavMesh.");
        }

        // ── Snap to surface ─────────────────────────────────────────────────────
        // Only snap if not already on the NavMesh. OnNavMeshBaked() handles the
        // Foundation-floor height correction for newly placed agents; calling Warp
        // unconditionally here would clear an active path and could leave the agent
        // off-mesh if the snap lands outside the NavMesh boundary.
        if (!agent.isOnNavMesh)
            SnapToCorrectSurface();

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[AiNavigation] {gameObject.name}: still not on NavMesh after bake. Update() recovery will retry.");
            yield break;
        }

        // ── Apply lane costs ────────────────────────────────────────────────────
        ApplyAgentCosts();

        // ── Find waypoints ──────────────────────────────────────────────────────
        FindWaypoints();
        if (waypoints == null || waypoints.Length == 0)
        {
            yield return null; // give one frame for late-spawned waypoints
            FindWaypoints();
        }

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"[AiNavigation] {gameObject.name}: no waypoints found.");
            // _indicator polls HasWaypoints itself — no callback needed
            yield break;
        }

        // ── Go ──────────────────────────────────────────────────────────────────
        currentIndex = Random.Range(0, waypoints.Length);
        if (agent.SetDestination(waypoints[currentIndex].position))
            initialized = true;
    }

    private void ApplyAgentCosts()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (role == AgentRole.Worker)
        {
            // Area 0 (Walkable / Foundation floor) cost is kept close to 1.0 so that
            // walking directly across a Foundation is always cheaper than the stair
            // detour (down stairs + yard + up stairs adds ~2.3 units of overhead).
            // With the old cost of 25.0, a path as short as 3 tiles was routed through
            // the stairs, causing agents to "walk in place" while stuck on off-mesh links.
            agent.SetAreaCost(0, 1.1f);  // Walkable — slight premium over dedicated lanes
            agent.SetAreaCost(3, 80.0f); // MHE lane   — strongly avoid
            agent.SetAreaCost(4, 1.0f);  // Ped lane   — strongly prefer
        }
        else if (role == AgentRole.Forklift)
        {
            agent.SetAreaCost(0, 1.1f);  // Walkable — slight premium over dedicated lanes
            agent.SetAreaCost(3, 1.0f);  // MHE lane   — strongly prefer
            agent.SetAreaCost(4, 80.0f); // Ped lane   — strongly avoid
            agent.stoppingDistance = 1.0f;
        }
        // Note: area costs take effect on the next SetDestination call — no need to reset
        // the current path here, which would cause visible path flicker every recovery tick.
    }

    private void Update()
    {
        // ── Stair / off-mesh link traversal ─────────────────────────────────────

        // Safety: if TraverseLink() was interrupted while _traversingLink=true
        // (e.g. by a rebake event or an exception), updatePosition stays false and
        // the agent "walks in place" forever. Reset flags when the link is gone.
        if (_traversingLink && !agent.isOnOffMeshLink)
        {
            _traversingLink      = false;
            agent.updatePosition = true;
        }

        if (!_traversingLink && agent != null && agent.isOnOffMeshLink)
        {
            StartCoroutine(TraverseLink());
            return;
        }

        if (_traversingLink) return;

        // ── Recovery: re-initialize if Start() gave up ──────────────────────────
        if (!initialized)
        {
            if (Time.frameCount % 60 == 0 && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if (waypoints == null || waypoints.Length == 0) FindWaypoints();
                if (waypoints != null && waypoints.Length > 0)
                {
                    ApplyAgentCosts();
                    if (agent.SetDestination(waypoints[currentIndex].position))
                        initialized = true;
                }
            }
            return;
        }

        // ── Recovery: re-snap if a runtime bake knocked us off the mesh ─────────
        // Use SnapToCorrectSurface() so Foundation-height agents (y≈1.06) are
        // found correctly. The old SamplePosition + "< 1.0f delta" check failed
        // for Foundation floors because the delta is exactly 1.06 (≥ threshold).
        if (!agent.isOnNavMesh)
        {
            if (Time.frameCount % 30 == 0)
            {
                SnapToCorrectSurface();
                if (agent.isOnNavMesh && waypoints != null && waypoints.Length > 0)
                    agent.SetDestination(waypoints[currentIndex].position);
            }
            return;
        }

        // ── Dead-path recovery (animated agents only) ────────────────────────────
        // AgentAnimation.Update() only calls GoToRandomWaypoint() on genuine arrival
        // (PathComplete). If the path is cleared by any other means — a Warp, an
        // interrupted coroutine — the agent has no recovery mechanism.
        // Poll every ~2 s to restart navigation without causing visible jitter.
        if (_agentAnimation != null
            && !agent.hasPath && !agent.pathPending
            && waypoints != null && waypoints.Length > 0)
        {
            if (Time.frameCount % 120 == 0)
                GoToRandomWaypoint();
        }

        // ── Arrival audio ────────────────────────────────────────────────────────
        {
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
            if (arrived && !hasHonkedThisArrival)
            {
                hasHonkedThisArrival = true;
                if (throttleAudio != null) throttleAudio.TriggerArrivalHonk();
                _mumble?.TryMumble();
            }
            else if (!arrived && agent.remainingDistance > agent.stoppingDistance + 0.5f)
            {
                hasHonkedThisArrival = false;
            }
        }

        // ── Footsteps ─────────────────────────────────────────────────────────────
        if (_footstepSource != null)
        {
            bool moving = agent.velocity.sqrMagnitude > 0.01f;
            if (moving && !_footstepSource.isPlaying)
                _footstepSource.Play();
            else if (!moving && _footstepSource.isPlaying)
                _footstepSource.Stop();
        }

        // ── Waypoint progression (for agents without AgentAnimation) ─────────────
        // Only advance on a fully-complete path so a blocked agent at a partial-path
        // endpoint is not mistakenly declared "arrived" and given a new destination.
        if (_agentAnimation == null)
        {
            if (!agent.pathPending
                && agent.remainingDistance <= agent.stoppingDistance + 0.1f
                && agent.pathStatus == NavMeshPathStatus.PathComplete)
                GoToRandomWaypoint();
        }
    }

    // Local-space positions of the bottom and top of the stair walkway on the stairwell prefab
    private static readonly Vector3 StairLocalBottom = new Vector3(0.63f, 0f,    0f);
    private static readonly Vector3 StairLocalTop    = new Vector3(0.63f, 1.06f, 1.34f);

    private IEnumerator TraverseLink()
    {
        _traversingLink = true;
        agent.updatePosition = false;
        agent.updateRotation = false;

        OffMeshLinkData linkData = agent.currentOffMeshLinkData;

        // ── Ledge check (climb up / jump down) ──────────────────────────────────
        LedgeLinkMarker ledge = FindNearestLedgeLink(agent.transform.position);
        if (ledge != null)
        {
            // Determine which link endpoint is the destination (the one we're moving TO)
            bool startIsClose = Vector3.Distance(agent.transform.position, linkData.startPos)
                              < Vector3.Distance(agent.transform.position, linkData.endPos);
            Vector3 from = agent.transform.position;
            Vector3 to   = startIsClose ? linkData.endPos : linkData.startPos;
            bool goingUp = to.y > from.y + 0.1f;

            // Face horizontal direction of travel
            Vector3 hDir = to - from; hDir.y = 0f;
            if (hDir.sqrMagnitude > 0.001f)
                agent.transform.rotation = Quaternion.LookRotation(hDir.normalized);

            float duration = goingUp ? ledge.climbDuration : ledge.jumpDuration;

            if (goingUp) IsTraversingLedgeUp   = true;
            else         IsTraversingLedgeDown  = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (goingUp)
                {
                    // Smooth-step: slow start as the agent grabs the ledge, faster pull-up
                    agent.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                }
                else
                {
                    // Gravity feel: horizontal movement linear, vertical accelerates downward
                    agent.transform.position = new Vector3(
                        Mathf.Lerp(from.x, to.x, t),
                        Mathf.Lerp(from.y, to.y, Mathf.Pow(t, 1.6f)),
                        Mathf.Lerp(from.z, to.z, t));
                }
                yield return null;
            }

            agent.transform.position = to;
            IsTraversingLedgeUp   = false;
            IsTraversingLedgeDown = false;
        }
        else
        {
            // ── Stair traversal (existing logic) ────────────────────────────────
            BuildingData stair = FindNearestStair();

            Vector3 worldBottom, worldTop;
            if (stair != null)
            {
                worldBottom = stair.transform.TransformPoint(StairLocalBottom);
                worldTop    = stair.transform.TransformPoint(StairLocalTop);
            }
            else
            {
                worldBottom = linkData.startPos;
                worldTop    = linkData.endPos;
            }

            bool goingUp = Vector3.Distance(agent.transform.position, worldBottom)
                         < Vector3.Distance(agent.transform.position, worldTop);
            Vector3 from = goingUp ? worldBottom : worldTop;
            Vector3 to   = goingUp ? worldTop    : worldBottom;

            Vector3 dir = to - from; dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                agent.transform.rotation = Quaternion.LookRotation(dir.normalized);

            float dist     = Vector3.Distance(from, to);
            float duration = dist / Mathf.Max(agent.speed, 0.1f);
            float elapsed  = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                agent.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            agent.transform.position = to;
        }

        agent.CompleteOffMeshLink();
        agent.updatePosition = true;
        agent.updateRotation = true;
        _traversingLink = false;
    }

    // Per-instance cache — static would survive Play Mode restarts with stale destroyed refs
    private BuildingData[] _cachedStairs;

    private BuildingData FindNearestStair()
    {
        if (_cachedStairs == null)
        {
            var all = FindObjectsByType<BuildingData>();
            var stairs = new System.Collections.Generic.List<BuildingData>();
            foreach (var bd in all)
                if (bd.Data != null && bd.Data.CanUseStairs) stairs.Add(bd);
            _cachedStairs = stairs.ToArray();
        }

        BuildingData nearest = null;
        float nearestDist = 8f;
        foreach (var bd in _cachedStairs)
        {
            if (bd == null) continue;
            float d = Vector3.Distance(agent.transform.position, bd.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = bd; }
        }
        return nearest;
    }

    // Ledge links are not cached — they can be placed/removed at runtime and are only
    // queried when an agent is already on an off-mesh link (rare, not per-frame).
    private LedgeLinkMarker FindNearestLedgeLink(Vector3 searchPos)
    {
        LedgeLinkMarker nearest = null;
        float nearestDist = 6f; // wide enough to catch links on all 4 sides of a multi-cell foundation

        foreach (var marker in FindObjectsByType<LedgeLinkMarker>(FindObjectsSortMode.None))
        {
            if (marker == null) continue;
            float d = Vector3.Distance(searchPos, marker.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = marker; }
        }
        return nearest;
    }

    public void GoToRandomWaypoint()
    {
        // Always re-scan so newly placed waypoints are picked up immediately.
        FindWaypoints();

        if (waypoints == null || waypoints.Length == 0) return;
        if (!agent.isOnNavMesh) return;

        int nextIndex = currentIndex;
        int safety = 0;
        while (nextIndex == currentIndex && safety < 10 && waypoints.Length > 1)
        {
            nextIndex = Random.Range(0, waypoints.Length);
            safety++;
        }
        currentIndex = nextIndex;

        if (waypoints[currentIndex] == null)
        {
            FindWaypoints();
            if (waypoints == null || waypoints.Length == 0) return;
            currentIndex = Random.Range(0, waypoints.Length);
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh && waypoints[currentIndex] != null)
            agent.SetDestination(waypoints[currentIndex].position);
    }
}
