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
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] private float footstepPitch = 1f;

    [Header("Footstep Rolloff (Custom)")]
    [SerializeField, Range(0f, 1f)] private float volumeAt5m = 1f;
    [SerializeField, Range(0f, 1f)] private float volumeAt10m = 0.75f;
    [SerializeField, Range(0f, 1f)] private float volumeAt15m = 0.5f;
    [SerializeField, Range(0f, 1f)] private float volumeAt20m = 0.25f;
    [SerializeField, Range(0f, 1f)] private float volumeAt30m = 0.1f;

    private Transform[] waypoints;
    private NavMeshAgent agent;
    private int currentIndex = 0;
    private bool initialized = false;
    private VehicleThrottleAudio throttleAudio;
    private bool hasHonkedThisArrival = false;
    private bool    _traversingLink = false;
    private Vector3 _pendingFrom;
    private Vector3 _pendingTo;
    private bool    _hasPendingPositions;
    // Proximity-based ledge detection state (CheckDockLedge)
    private float             _ledgeCheckTimer = 0f;
    private LedgeLinkMarker[] _cachedLedges;
    private AgentAnimation _agentAnimation;
    private AudioSource _footstepSource;
    private NoWaypointIndicator _indicator;
    private AmbientMumble _mumble;
    private Animator _animator;
    private Rigidbody _rb;

    // Temporary diagnostic state for the walk-in-place check in Update().
    private Vector3 _diagLastPos;
    private float _diagTimer;

    // Auto-rebake: fires when agent has no waypoints (? visible) or is stuck for too long.
    private float   _noWaypointRebakeTimer;
    private float   _stuckRebakeTimer;
    private Vector3 _stuckRefPos;
    private static float s_lastGlobalRebake = float.MinValue;
    private const  float k_RebakeTrigger  = 3.5f;
    private const  float k_RebakeCooldown = 25f;
    public bool HasWaypoints       => waypoints != null && waypoints.Length > 0;
    /// <summary>True when there are at least 2 waypoints — enough for a return trip.</summary>
    public bool HasEnoughWaypoints => waypoints != null && waypoints.Length >= 2;

    /// <summary>True while the agent is playing the Climbing animation at a ledge.</summary>
    public bool IsTraversingLedgeUp   { get; private set; }
    /// <summary>True while the agent is playing the JumpingDown animation at a ledge.</summary>
    public bool IsTraversingLedgeDown { get; private set; }
    /// <summary>True during any off-mesh link traversal (ledge or stair).</summary>
    public bool IsTraversingLink      => _traversingLink;

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

        _cachedLedges = null;  // foundations may have been placed since last bake
        FindWaypoints();

        // Skip snap while mid-climb — a warp mid-animation would teleport the agent.
        if (_traversingLink) return;

        // Always snap — the agent may be on the y=0 ground plane instead of the
        // y=1.06 foundation surface when placed before the bake completes.
        // SnapToNavMeshSurface searches upward first, so it prefers the higher surface.
        SnapToNavMeshSurface();

        if (!agent.isOnNavMesh) return;

        if (!initialized && waypoints != null && waypoints.Length > 0)
        {
            ApplyAgentCosts();
            currentIndex = Random.Range(0, waypoints.Length);
            if (SetDestinationSnapped(waypoints[currentIndex].position))
                initialized = true;
        }
        else if (initialized && !agent.hasPath && !agent.pathPending
                 && waypoints != null && waypoints.Length > 0)
        {
            GoToRandomWaypoint();
        }
    }
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        throttleAudio = GetComponent<VehicleThrottleAudio>();
        _agentAnimation = GetComponent<AgentAnimation>();
        _indicator = GetComponent<NoWaypointIndicator>();
        _mumble    = GetComponent<AmbientMumble>();
        _animator  = GetComponent<Animator>();
        _rb        = GetComponent<Rigidbody>();
        SetupAgentType();

        // The navigation design REQUIRES a kinematic Rigidbody. The agent simulates with
        // updatePosition=false and we drive the transform + Rigidbody manually (LateUpdate
        // and TraverseLink both call _rb.MovePosition). A DYNAMIC Rigidbody falls under
        // gravity and gets ejected by collision depenetration the moment a climb pushes it
        // through the foundation collider — that is the "teleporting all over the place"
        // bug. A freshly-added Rigidbody defaults to dynamic, so force the correct state
        // here rather than trusting every agent prefab to be configured by hand.
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.useGravity  = false;
        }

        // updatePosition is DELIBERATELY false. The agent shares its GameObject with a
        // kinematic Rigidbody (required so gates/doors get OnTrigger callbacks). With
        // updatePosition=true the agent reads the transform back every frame and resets
        // its internal simulation to it; the Rigidbody's physics transform-sync reverts
        // the transform to a stale pose first, so the agent resets to its start position
        // every frame — full velocity, zero progress ("walks in place", stuck at spawn Y).
        // With updatePosition=false the agent simulates freely; we copy agent.nextPosition
        // onto the transform AND the Rigidbody in LateUpdate (below), so physics can never
        // freeze pathfinding and the agent always renders on the correct surface.
        if (agent != null) agent.updatePosition = false;

        if (footstepClip != null)
        {
            _footstepSource = gameObject.AddComponent<AudioSource>();
            _footstepSource.clip = footstepClip;
            _footstepSource.loop = true;
            _footstepSource.volume = footstepVolume;
            _footstepSource.pitch = footstepPitch;
            _footstepSource.spatialBlend = 1f;
            _footstepSource.rolloffMode = AudioRolloffMode.Custom;
            _footstepSource.minDistance = 1f;
            _footstepSource.maxDistance = 30f;
            _footstepSource.playOnAwake = false;
            ApplyFootstepRolloffCurve();
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

    // Warps an off-NavMesh agent to the nearest walkable surface.
    // Only fires when the agent is NOT already on the NavMesh — avoids blinking
    // floor-level agents up to the dock surface during auto-rebakes.
    private void SnapToNavMeshSurface()
    {
        if (agent == null) return;
        if (agent.isOnNavMesh) return;  // already grounded — don't disturb
        float[] yOffsets = { 0f, 0.5f, -0.5f, 1.0f, -1.0f };
        foreach (float offset in yOffsets)
        {
            Vector3 sample = new Vector3(transform.position.x, transform.position.y + offset, transform.position.z);
            if (NavMesh.SamplePosition(sample, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
            {
                try { agent.Warp(hit.position); } catch { }
                return;
            }
        }
    }

    private void SetupAgentType()
    {
        if (agent == null) return;

        // FALSE — we handle off-mesh link traversal manually in TraverseLink() so
        // the climb/jump animations play. updatePosition stays permanently false
        // (see Awake) and we sync agent.nextPosition at the end of each traversal
        // instead of setting updatePosition=true, which was the old crash/freeze cause.
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
        // Always snap — even if the agent is already "on" the NavMesh, it may be on
        // the wrong surface (ground plane at Y=0 instead of floor tiles at Y=1.06).
        // Search upward first so floor-tile NavMesh is preferred over the ground plane.
        SnapToNavMeshSurface();

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
        if (SetDestinationSnapped(waypoints[currentIndex].position))
            initialized = true;
    }

    private void ApplyAgentCosts()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (role == AgentRole.Worker)
        {
            agent.SetAreaCost(0, 25.0f); // Walkable (generic) — expensive
            agent.SetAreaCost(3, 80.0f); // MHE lane   — strongly avoid
            agent.SetAreaCost(4, 1.0f);  // Ped lane   — strongly prefer
        }
        else if (role == AgentRole.Forklift)
        {
            agent.SetAreaCost(0, 25.0f); // Walkable (generic) — expensive
            agent.SetAreaCost(3, 1.0f);  // MHE lane   — strongly prefer
            agent.SetAreaCost(4, 80.0f); // Ped lane   — strongly avoid
            agent.stoppingDistance = 1.0f;
        }
        // Note: area costs take effect on the next SetDestination call — no need to reset
        // the current path here, which would cause visible path flicker every recovery tick.
    }

    private void Update()
    {
        // SAFETY: agent.updatePosition stays false for the entire lifetime (set in Awake).
        // LateUpdate drives transform + Rigidbody from agent.nextPosition.
        // Never set updatePosition=true — it re-introduces the Rigidbody reset fight.

        // ROOT MOTION FIX: an Animator with applyRootMotion=true on the same object as
        // a NavMeshAgent overrides the agent's position via OnAnimatorMove every frame.
        // With an in-place walk clip that pins the transform → "walks in place" while
        // agent.velocity stays high. The agent drives locomotion here, not animation,
        // so root motion must stay OFF. (Climb traversal is disabled, so nothing needs it.)
        if (_animator != null && _animator.applyRootMotion)
        {
            _animator.applyRootMotion = false;
            Debug.LogWarning($"[NavDiag] {name}: Animator.applyRootMotion was TRUE — forced OFF. " +
                             $"This was the walk-in-place cause.");
        }

        // ── DIAGNOSTIC (temporary): catch walk-in-place if it ever recurs ────────
        _diagTimer += Time.deltaTime;
        if (_diagTimer >= 1f)
        {
            _diagTimer = 0f;
            float moved = (transform.position - _diagLastPos).magnitude;
            if (agent != null && agent.isOnNavMesh && agent.velocity.magnitude > 0.2f && moved < 0.05f)
            _diagLastPos = transform.position;
        }

        // ── Recovery: re-initialize if Start() gave up ──────────────────────────
        if (!initialized)
        {
            if (Time.frameCount % 60 == 0 && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if (waypoints == null || waypoints.Length == 0) FindWaypoints();
                if (waypoints != null && waypoints.Length > 0)
                {
                    ApplyAgentCosts();
                    if (SetDestinationSnapped(waypoints[currentIndex].position))
                        initialized = true;
                }
            }
            return;
        }

        // ── Periodic state diagnostic ────────────────────────────────────────────
        if (Time.frameCount % 180 == 0 && agent.isOnNavMesh)
        {
            int wpCount = waypoints != null ? waypoints.Length : 0;
            int wpHigh  = 0;
            if (waypoints != null)
                foreach (var w in waypoints) if (w != null && w.position.y > 0.5f) wpHigh++;
            float curY = (waypoints != null && currentIndex < wpCount && waypoints[currentIndex] != null)
                       ? waypoints[currentIndex].position.y : -99f;
            Debug.Log($"[AiDiag] {name}: status={agent.pathStatus} destY={agent.destination.y:F2} " +
                      $"posY={transform.position.y:F2} | wpCount={wpCount} wpHigh={wpHigh} curIdx={currentIndex} curWpY={curY:F2}");
        }

        // ── Off-mesh link traversal (climb up / jump down) ──────────────────────
        // Primary: isOnOffMeshLink (keep in case it ever fires).
        // Fallback: CheckDockLedge — proximity + destination height check. This is
        // the reliable path: it doesn't depend on isOnOffMeshLink or path.corners,
        // both of which are unreliable with NavMeshLink in Unity 6 when the dock and
        // floor NavMesh surfaces form disconnected islands.
        if (!_traversingLink)
        {
            if (agent.isOnOffMeshLink)
            {
                _hasPendingPositions = false;
                Debug.Log($"[Climb] {name}: isOnOffMeshLink=true at {agent.transform.position:F2}");
                StartCoroutine(TraverseLink());
            }
            else
            {
                CheckDockLedge();
            }
        }

        // ── Recovery: re-snap if a runtime bake knocked us off the mesh ─────────
        // GUARD: when the agent is on an off-mesh link, Unity sets isOnNavMesh=false.
        // Without this check the recovery block fires every ~30 frames and Warps the
        // agent off the ledge link mid-climb, breaking traversal entirely.
        if (!agent.isOnNavMesh)
        {
            if (_traversingLink || agent.isOnOffMeshLink) return;

            if (Time.frameCount % 30 == 0)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                {
                    if (Mathf.Abs(hit.position.y - transform.position.y) < 1.0f)
                    {
                        agent.Warp(hit.position);
                        if (waypoints != null && waypoints.Length > 0)
                            agent.SetDestination(waypoints[currentIndex].position);
                    }
                }
            }
            return;
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
            _footstepSource.pitch = footstepPitch;
            bool moving = agent.velocity.sqrMagnitude > 0.01f;
            if (moving && !_footstepSource.isPlaying)
                _footstepSource.Play();
            else if (!moving && _footstepSource.isPlaying)
                _footstepSource.Stop();
        }

        // ── Auto-rebake trigger ───────────────────────────────────────────────────
        // Never rebake while traversing a ledge — it would disrupt the manual climb/jump.
        if (!_traversingLink) UpdateRebakeTrigger();

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

    // ── Drive transform + Rigidbody from the agent simulation ────────────────
    // agent.updatePosition is false (see Awake), so the NavMeshAgent simulates its
    // path internally without being corrupted by the shared kinematic Rigidbody. Here
    // we copy the agent's authoritative nextPosition onto the transform AND the
    // Rigidbody every frame. Writing the Rigidbody too keeps physics in sync so it can
    // never revert the transform to a stale pose (the cause of the agent being stuck at
    // spawn Y ≈ 0 and "walking in place" instead of climbing onto the foundation).
    private void LateUpdate()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        // While a manual link traversal coroutine is running it drives the transform
        // itself (currently disabled, but guard anyway).
        if (_traversingLink) return;

        Vector3 np = agent.nextPosition;
        transform.position = np;
        if (_rb != null)
            _rb.MovePosition(np);
        // Rotation is owned by AgentAnimation.LateUpdate() — do not set _rb.rotation
        // here, as it fires before AgentAnimation's LateUpdate and would overwrite it.
    }

    // Local-space positions of the bottom and top of the stair walkway on the stairwell prefab
    private static readonly Vector3 StairLocalBottom = new Vector3(0.63f, 0f,    0f);
    private static readonly Vector3 StairLocalTop    = new Vector3(0.63f, 1.06f, 1.34f);

    // Partial-path-driven ledge traversal trigger.
    //
    // The dock top and the ground are separate NavMesh islands (the NavMeshLinks do
    // not reliably bridge them — confirmed via Tools/Diagnose Dock Links). So when the
    // agent targets a waypoint on the OTHER island, its path comes back PathPartial and
    // it walks to the closest reachable point — the dock edge. We detect that situation
    // (height-mismatched destination + stuck near a dock-edge marker) and perform the
    // climb/jump manually, then warp onto the destination island and re-path.
    private void CheckDockLedge()
    {
        if (agent.pathPending) { _ledgeCheckTimer = 0f; return; }

        // Use the CURRENT WAYPOINT's true height, not agent.destination.y — the latter is
        // mapped onto the nearest NavMesh and can read ground even when targeting the dock.
        Transform wp = (waypoints != null && currentIndex >= 0 && currentIndex < waypoints.Length)
                     ? waypoints[currentIndex] : null;
        if (wp == null) { _ledgeCheckTimer = 0f; return; }

        // Target is on a different height level than the agent → needs a ledge.
        float posY = agent.transform.position.y;
        float tgtY = wp.position.y;
        bool goingUp   = posY < 0.3f && tgtY > 0.5f;
        bool goingDown = posY > 0.5f && tgtY < 0.3f;
        if (!goingUp && !goingDown) { _ledgeCheckTimer = 0f; return; }

        // Only when the agent can't get there directly (partial path) and has reached
        // the end of what it CAN walk (i.e. it's sitting at the dock edge), essentially stopped.
        bool blocked  = agent.pathStatus != NavMeshPathStatus.PathComplete;
        bool atEnd    = agent.remainingDistance <= agent.stoppingDistance + 0.75f;
        bool stopped  = agent.velocity.sqrMagnitude < 0.06f;
        if (!blocked || !(atEnd || stopped)) { _ledgeCheckTimer = 0f; return; }

        // Find the nearest dock-edge marker (the climb pivot). Wider radius than before
        // because the eroded ground navmesh stops short of the foundation edge.
        if (_cachedLedges == null)
            _cachedLedges = FindObjectsByType<LedgeLinkMarker>(FindObjectsInactive.Exclude);
        LedgeLinkMarker nearest = null;
        float nearestXZ = 2.5f;
        float ax = agent.transform.position.x;
        float az = agent.transform.position.z;
        foreach (var m in _cachedLedges)
        {
            if (m == null) continue;
            float dx = ax - m.transform.position.x;
            float dz = az - m.transform.position.z;
            float xz = Mathf.Sqrt(dx * dx + dz * dz);
            if (xz < nearestXZ) { nearestXZ = xz; nearest = m; }
        }
        if (nearest == null) { _ledgeCheckTimer = 0f; return; }

        // Require a brief sustained stop so we don't fire mid-stride.
        _ledgeCheckTimer += Time.deltaTime;
        if (_ledgeCheckTimer < 0.4f) return;
        _ledgeCheckTimer = 0f;

        // Dock endpoint = the marker (sits on the dock NavMesh, Y≈1.11).
        // Floor endpoint = pushed outward from the marker onto the ground NavMesh (Y≈0).
        Vector3 fwd = nearest.transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) { fwd = agent.transform.position - nearest.transform.position; fwd.y = 0f; }
        fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;

        Vector3 dockPt  = nearest.transform.position;
        Vector3 floorPt = new Vector3(dockPt.x + fwd.x * 0.5f, 0f, dockPt.z + fwd.z * 0.5f);

        _pendingFrom         = agent.transform.position;
        _pendingTo           = goingUp ? dockPt : floorPt;
        _hasPendingPositions = true;

        Debug.Log($"[Climb] {name}: DockLedge goingUp={goingUp} marker={nearest.name} from={_pendingFrom:F2} to={_pendingTo:F2} tgtY={tgtY:F2}");
        StartCoroutine(TraverseLink());
    }

    private IEnumerator TraverseLink()
    {
        _traversingLink = true;
        // LateUpdate skips when _traversingLink=true so this coroutine fully owns the transform.

        OffMeshLinkData linkData = agent.currentOffMeshLinkData;
        LedgeLinkMarker ledge    = FindNearestLedgeLink(agent.transform.position);

        // ── Stop the agent so nextPosition doesn't drift during manual movement ──
        // Must apply to BOTH the isOnOffMeshLink and _hasPendingPositions paths.
        bool manualStop = false;
        if (agent.isActiveAndEnabled && !agent.isStopped)
        {
            agent.isStopped = true;
            manualStop = true;
        }

        // ── Resolve from/to positions ─────────────────────────────────────────
        // Path-corner fallback sets _pendingFrom/_pendingTo before starting the coroutine.
        // isOnOffMeshLink path uses currentOffMeshLinkData.
        Vector3 from, to;
        if (_hasPendingPositions)
        {
            from = _pendingFrom;
            to   = _pendingTo;
            _hasPendingPositions = false;
        }
        else
        {
            bool startIsClose = Vector3.Distance(agent.transform.position, linkData.startPos)
                              < Vector3.Distance(agent.transform.position, linkData.endPos);
            from = agent.transform.position;
            to   = startIsClose ? linkData.endPos : linkData.startPos;
        }

        Debug.Log($"[Climb] {name}: TraverseLink ledge={ledge?.name ?? "NONE"} from={from:F2} to={to:F2}");

        if (ledge != null)
        {
            // ── Ledge (climb up / jump down) ──────────────────────────────────
            bool goingUp = to.y > from.y + 0.1f;

            Vector3 hDir = to - from; hDir.y = 0f;
            if (hDir.sqrMagnitude > 0.001f)
                agent.transform.rotation = Quaternion.LookRotation(hDir.normalized);

            float duration = goingUp ? ledge.climbDuration : ledge.jumpDuration;

            if (goingUp) IsTraversingLedgeUp  = true;
            else         IsTraversingLedgeDown = true;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 pos = goingUp
                    ? Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t))
                    : new Vector3(
                        Mathf.Lerp(from.x, to.x, t),
                        Mathf.Lerp(from.y, to.y, Mathf.Pow(t, 1.6f)),
                        Mathf.Lerp(from.z, to.z, t));

                agent.transform.position = pos;
                agent.nextPosition        = pos;
                if (_rb != null) _rb.MovePosition(pos);

                yield return null;
            }

            agent.transform.position = to;
            IsTraversingLedgeUp  = false;
            IsTraversingLedgeDown = false;
        }
        else
        {
            // ── Stair traversal ───────────────────────────────────────────────
            BuildingData stair = FindNearestStair();

            Vector3 worldBottom, worldTop;
            if (stair != null)
            {
                worldBottom = stair.transform.TransformPoint(StairLocalBottom);
                worldTop    = stair.transform.TransformPoint(StairLocalTop);
            }
            else
            {
                worldBottom = linkData.valid ? linkData.startPos : from;
                worldTop    = linkData.valid ? linkData.endPos   : to;
            }

            bool goingUp    = Vector3.Distance(agent.transform.position, worldBottom)
                            < Vector3.Distance(agent.transform.position, worldTop);
            Vector3 stairFrom = goingUp ? worldBottom : worldTop;
            Vector3 stairTo   = goingUp ? worldTop    : worldBottom;

            Vector3 dir = stairTo - stairFrom; dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                agent.transform.rotation = Quaternion.LookRotation(dir.normalized);

            float dist     = Vector3.Distance(stairFrom, stairTo);
            float duration = dist / Mathf.Max(agent.speed, 0.1f);
            float elapsed  = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Vector3 pos = Vector3.Lerp(stairFrom, stairTo, Mathf.Clamp01(elapsed / duration));
                agent.transform.position = pos;
                agent.nextPosition        = pos;
                if (_rb != null) _rb.MovePosition(pos);
                yield return null;
            }

            agent.transform.position = stairTo;
        }

        if (agent.isOnOffMeshLink)
            agent.CompleteOffMeshLink();

        // Land cleanly ON the destination NavMesh island. Warp snaps the agent's internal
        // simulation to the nearest NavMesh at the final position (the dock surface when
        // climbing up, the ground when jumping down) so the re-path below can succeed.
        if (agent.isActiveAndEnabled)
            agent.Warp(agent.transform.position);

        if (manualStop && agent.isActiveAndEnabled) agent.isStopped = false;
        _traversingLink = false;

        // Re-path from the new surface — the agent is now ON the destination island, so the
        // path that was PathPartial before the climb will complete.
        if (agent.isActiveAndEnabled && agent.isOnNavMesh
            && waypoints != null && waypoints.Length > 0 && waypoints[currentIndex] != null)
            SetDestinationSnapped(waypoints[currentIndex].position);
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

        foreach (var marker in FindObjectsByType<LedgeLinkMarker>(FindObjectsInactive.Exclude))
        {
            if (marker == null) continue;
            float d = Vector3.Distance(searchPos, marker.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = marker; }
        }
        return nearest;
    }

    private void ApplyFootstepRolloffCurve()
    {
        if (_footstepSource == null) return;

        // x-axis: normalized distance (0 = source, 1 = maxDistance of 30 m).
        // Stays at full volume up to 10 m, then fades through 20 m, reaching
        // volumeAt30m at max distance. Unity returns 0 beyond maxDistance.
        var curve = new AnimationCurve(
            new Keyframe(0f,        1f),
            new Keyframe(5f / 30f, volumeAt5m),
            new Keyframe(10f / 30f, volumeAt10m),
            new Keyframe(15f / 30f, volumeAt15m),
            new Keyframe(20f / 30f, volumeAt20m),
            new Keyframe(1f,        volumeAt30m)
        );
        _footstepSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
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
            SetDestinationSnapped(waypoints[currentIndex].position);
    }

    // Snaps the target onto the NavMesh with a GENEROUS radius before pathing.
    // NavMeshAgent.SetDestination uses a tight internal sample tolerance, so a waypoint
    // sitting slightly over the dock edge gets mapped DOWN to the ground below it — the
    // agent then thinks its goal is on the ground and never tries to climb. Pre-sampling
    // with radius 2 lands the destination solidly on the correct surface (dock at Y≈1.11),
    // which also makes the ground→dock path correctly Partial so the agent waits at the
    // dock edge instead of false-arriving on the ground.
    private bool SetDestinationSnapped(Vector3 target)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2.0f, agent.areaMask))
            return agent.SetDestination(hit.position);
        return agent.SetDestination(target);
    }

    // ── Auto-rebake trigger ───────────────────────────────────────────────────

    private void UpdateRebakeTrigger()
    {
        // No-waypoint check: question mark is showing over agent's head
        if (_indicator != null && _indicator.IsShowingIndicator)
        {
            _noWaypointRebakeTimer += Time.deltaTime;
            if (_noWaypointRebakeTimer >= k_RebakeTrigger)
            {
                TryForceRebake("no waypoints");
                _noWaypointRebakeTimer = 0f;
            }
        }
        else
        {
            _noWaypointRebakeTimer = 0f;
        }

        // Stuck check: agent wants to move but hasn't actually moved
        if (initialized && agent.isOnNavMesh && agent.hasPath && !agent.isStopped
            && agent.desiredVelocity.sqrMagnitude > 0.04f)
        {
            float moved = Vector3.Distance(transform.position, _stuckRefPos);
            if (moved < 0.15f)
            {
                _stuckRebakeTimer += Time.deltaTime;
                if (_stuckRebakeTimer >= k_RebakeTrigger)
                {
                    TryForceRebake("stuck");
                    _stuckRebakeTimer = 0f;
                    _stuckRefPos = transform.position;
                }
            }
            else
            {
                _stuckRebakeTimer = 0f;
                _stuckRefPos = transform.position;
            }
        }
        else
        {
            _stuckRebakeTimer = 0f;
            _stuckRefPos = transform.position;
        }
    }

    private void TryForceRebake(string reason)
    {
        if (Time.time - s_lastGlobalRebake < k_RebakeCooldown) return;
        if (NavMeshManager.Instance == null) return;
        s_lastGlobalRebake = Time.time;
        Debug.Log($"[AiNavigation] {name}: forcing NavMesh rebake ({reason})");
        NavMeshManager.Instance.BakeImmediate();
    }
}
