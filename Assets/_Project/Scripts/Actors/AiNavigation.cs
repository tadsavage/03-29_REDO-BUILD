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
    // Forklift-role vehicles (ReachTruck, DockStocker, PalletJack) start parked until an
    // MHEOperatorSlot assigns a rider. Push-based — SetParked is the only way this flips,
    // no per-frame re-derivation (slot occupancy is owned by MHEOperatorSlot, not guessed
    // from GetComponentInChildren<EmployeeIdentity>, since the operator now rides as a
    // sibling under an anchor, not necessarily a child captured at Awake time).
    private bool _parked;

    // Equipment seeking (operator walks toward placed equipment)
    private MHEOperatorSlot _targetEquipment;
    private bool _seekingEquipment;
    private EmployeeIdentity _operatorIdentity;

    // Generic position seeking (e.g. ReceivingTaskDriver walking to a pallet) — same shape as
    // equipment seeking above, but for an arbitrary world position + arrival callback instead of
    // an MHEOperatorSlot to board.
    private Vector3 _taskTargetPosition;
    private System.Action _onTaskArrived;
    private bool _seekingTask;

    // Set true by the task driver once SeekPosition's arrival callback hands the agent off to a
    // stationary task (e.g. ReceiverReceivingWorkflow's 5-second fill bar). Without this, the
    // generic waypoint-progression fallback below (for agents with no AgentAnimation) sees
    // remainingDistance sitting at ~0 the instant _seekingTask is cancelled on arrival and
    // immediately calls GoToRandomWaypoint() the same frame — the agent walks to the task
    // position and then walks away before the task even starts.
    private bool _taskBusy;

    // Auto-rebake: fires when agent has no waypoints (? visible) or is stuck for too long.
    private float   _noWaypointRebakeTimer;
    private float   _stuckRebakeTimer;
    private Vector3 _stuckRefPos;
    private int     _consecutiveStuckCount;
    private static float s_lastGlobalRebake = float.MinValue;
    private const  float k_RebakeTrigger  = 3.5f;
    private const  float k_RebakeCooldown = 25f;

    // ── Unstick (walk-out) ───────────────────────────────────────────────────────────────────────
    // Last-resort escape for an agent that is genuinely walled in — the classic case being a pallet
    // dropped beside a standing receiver whose NavMeshObstacle carves the floor out from under them,
    // leaving no navmesh within reach and therefore no path anywhere. Rather than teleport (which
    // reads as a glitch) the agent physically WALKS to the nearest valid navmesh and a little past
    // it, animation playing, then rejoins normal navigation.
    private bool  _unsticking;
    private float _offMeshSeconds;   // how long we've been off-mesh with nothing to snap to
    private float _blockedSeconds;   // how long we've been on-mesh but unable to make progress

    /// <summary>Seconds of being genuinely stuck before the walk-out kicks in. Long enough that
    /// ordinary congestion (two agents shuffling past each other) resolves on its own first.</summary>
    private const float StuckEscalateSeconds = 3f;
    /// <summary>Radii tried in order when hunting for navmesh to snap back onto (off-mesh case).</summary>
    private static readonly float[] k_RescueRadii = { 2f, 6f, 12f, 20f };
    /// <summary>Ring radii probed when looking for a point OUTSIDE the pocket (walled-in case).</summary>
    private static readonly float[] k_EscapeRadii = { 3f, 5f, 8f, 12f };
    /// <summary>An escape target closer than this is assumed to still be inside the trap.</summary>
    private const float MinEscapeDistance = 2.5f;
    /// <summary>Extra distance walked PAST the recovered navmesh point, so the agent clears the
    /// pocket it was trapped in instead of stopping right on its lip and re-trapping.</summary>
    private const float UnstickOvershoot = 1f;
    private const float UnstickWalkSpeed = 1.6f;
    private const float UnstickTimeout   = 8f;

    /// <summary>How long an agent must sit stationary at the end of a PARTIAL path before we accept
    /// it as "arrived". A grace period rather than an instant call, so an agent still threading its
    /// way along a partial route isn't cut short the moment it slows down.</summary>
    private const float PartialSeekGraceSeconds = 1.5f;
    private float _partialSeekSeconds;
public bool HasWaypoints       => waypoints != null && waypoints.Length > 0;
    /// <summary>True when there are at least 2 waypoints — enough for a return trip.</summary>
    public bool HasEnoughWaypoints => waypoints != null && waypoints.Length >= 2;
    /// <summary>True while a Worker-role operator is walking toward MHE equipment to board it —
    /// they have a real destination even though it didn't come from the Worker waypoint patrol,
    /// so NoWaypointIndicator must not treat this as "nowhere to go".</summary>
    public bool IsSeekingEquipment => _seekingEquipment;
    /// <summary>True while the agent is walking toward a generic task position (SeekPosition) — same
    /// reasoning as IsSeekingEquipment: it has a real destination outside the normal waypoint patrol.</summary>
    public bool IsSeekingTask => _seekingTask;

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
        if (agent == null || !agent.isActiveAndEnabled || _parked) return;

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
                 && !_taskBusy && !_seekingTask && !_seekingEquipment
                 && waypoints != null && waypoints.Length > 0)
        {
            // _taskBusy/_seekingTask guard: a Receiver standing still mid-task (walking to a pallet
            // or actively receiving one for its full ~10s duration) has no path and no pending path —
            // exactly what this branch used to treat as "idle, go patrol." Every NavMesh rebake in the
            // ENTIRE warehouse fires this callback on every agent, so a receiver got silently yanked
            // off to a random waypoint mid-receive the moment anything else nearby triggered a rebake,
            // while the receiving animation/fill bar (which don't know navigation moved) kept running
            // as if nothing happened — the "walks away while still receiving" bug.
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
        if (agent != null)
        {
            agent.updatePosition = false;
            // Random priority helps resolve "head-on" deadlocks in narrow corridors or
            // stairwells — the prioritized agent will claim the path while the other yields.
            agent.avoidancePriority = Random.Range(0, 100);
        }

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

        if (role == AgentRole.Forklift)
        {
            _parked = true;
            // Unoccupied MHE has no business running navigation at all: no NavMeshAgent, no
            // AiNavigation Update/OnEnable (which would otherwise subscribe to NavMesh rebakes
            // for a vehicle nobody's driving). GoActive() (MHEOperatorSlot.AssignOperator) turns
            // both back on the moment an operator boards; GoIdle() (VacateOperator) turns them
            // back off.
            if (agent != null) agent.enabled = false;

            // Unoccupied MHE shows no bubble at all — only the boarded operator's vehicle gets
            // one, never both. GoActive()/GoIdle() toggle this for the rest of the vehicle's life.
            var bubble = GetComponent<EmoteBubble>();
            if (bubble != null) bubble.enabled = false;

            enabled = false;
        }
    }

    /// <summary>
    /// Parks (stops, no destination changes) or un-parks (resumes waypoint loop) a
    /// Forklift-role agent. Called by MHEOperatorSlot when an operator boards/leaves.
    /// agent.isStopped is never reset anywhere else for Forklift-role agents (they have
    /// no AgentAnimation), so un-parking must explicitly clear it and kick a fresh
    /// destination rather than relying on Update() to notice.
    /// </summary>
    public void SetParked(bool parked)
    {
        _parked = parked;
        if (agent == null) return;
        if (parked)
        {
            if (agent.isActiveAndEnabled) agent.isStopped = true;
        }
        else
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                GoToRandomWaypoint();
            }
        }
    }

    /// <summary>Transitions equipment to active state with an operator aboard. MHE owns its own
    /// navigation only while occupied — turns its NavMeshAgent + this script back on (Awake()
    /// turns both off for an unoccupied vehicle) and un-parks.</summary>
    public void GoActive(EmployeeIdentity operatorIdentity)
    {
        _operatorIdentity = operatorIdentity;
        _parked = false;
        enabled = true;
        if (agent != null) agent.enabled = true;

        // The VEHICLE's own bubble is the only one active while occupied — MHEOperatorSlot.
        // AssignOperator disables the rider's own bubble at the same time, so the two parented
        // objects are never both showing a bubble at once.
        var bubble = GetComponent<EmoteBubble>();
        if (bubble != null) bubble.enabled = true;

        // MHE never plays its own stuck/no-waypoint emote — operators driving it should show
        // no navigation-indicator behavior at all. ForceClear() FIRST: Update() is what
        // normally fades the bubble out, and that never runs once disabled — without this a
        // bubble that happened to be up the instant before boarding stays frozen forever.
        var mheIndicator = GetComponent<NoWaypointIndicator>();
        if (mheIndicator != null) { mheIndicator.ForceClear(); mheIndicator.enabled = false; }

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            GoToRandomWaypoint();
        }
    }

    /// <summary>Transitions equipment to idle state (no operator). Stop cleanly first, THEN turn
    /// the NavMeshAgent + this script off — unoccupied MHE runs no navigation at all.</summary>
    public void GoIdle()
    {
        _operatorIdentity = null;

        var mheIndicator = GetComponent<NoWaypointIndicator>();
        if (mheIndicator != null) { mheIndicator.ForceClear(); mheIndicator.enabled = false; }

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // Unoccupied vehicle shows no bubble at all — ForceHide first so a bubble visible the
        // instant before vacating doesn't freeze on screen (Update, which fades it out, stops
        // running the moment this component is disabled below).
        var bubble = GetComponent<EmoteBubble>();
        if (bubble != null) { bubble.ForceHide(); bubble.enabled = false; }

        _parked = true;
        if (agent != null) agent.enabled = false;
        enabled = false;
    }

    /// <summary>Operator seeks this equipment and walks toward it.</summary>
    public void SeekEquipment(MHEOperatorSlot slot)
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        if (role == AgentRole.Forklift) return;  // Only workers seek equipment
        if (_seekingEquipment) return;  // Already seeking

        _targetEquipment = slot;
        // AiNavigation and EmployeeIdentity are sibling components on the same worker
        // GameObject. Update()'s arrival check calls _targetEquipment.AssignOperator(
        // _operatorIdentity) — without this, _operatorIdentity stays null (it's normally
        // only set by GoActive(), which runs on the VEHICLE's AiNavigation, not the
        // worker's), so AssignOperator's null guard silently no-ops and the operator
        // arrives, "boards", and just stands there forever.
        _operatorIdentity = GetComponent<EmployeeIdentity>();
        _seekingEquipment = true;
        // Update()'s "Recovery: re-initialize if Start() gave up" block returns BEFORE reaching
        // off-mesh-link traversal / CheckDockLedge whenever !initialized — and initialized is
        // otherwise only ever set by the patrol-waypoint bootstrap, which an equipment-seeking
        // operator never goes through (no Worker waypoints needed for this destination). Without
        // this, an operator seeking equipment up on a dock would never even attempt the climb —
        // they'd just stand at the yard edge forever, isOnOffMeshLink=true and going nowhere.
        initialized = true;
        SetDestinationSnapped(slot.transform.position);
    }

    /// <summary>Cancel equipment seeking (e.g., if target became occupied).</summary>
    private void CancelEquipmentSeeking()
    {
        _seekingEquipment = false;
        _targetEquipment = null;
    }

    /// <summary>
    /// Walk to an arbitrary world position and invoke a callback on arrival. Used by
    /// ReceivingTaskDriver to send a dynamically-assigned Receiver to a pallet — deliberately
    /// generic (no MHEOperatorSlot) since the destination isn't equipment to board. Movement stays
    /// owned by THIS agent (not a second NavMeshAgent driver) to avoid fighting over the shared
    /// updatePosition=false transform sync — see the comment on that field in Awake().
    /// </summary>
    public void SeekPosition(Vector3 position, System.Action onArrived)
    {
        if (agent == null || !agent.isActiveAndEnabled) { onArrived?.Invoke(); return; }
        if (_seekingTask) return; // already seeking a task position

        _taskTargetPosition = position;
        _onTaskArrived = onArrived;
        _seekingTask = true;
        // Same reasoning as SeekEquipment: an employee walking to a receive task never went through
        // the Worker-waypoint patrol bootstrap, so without this the off-mesh-link/dock-climb logic
        // (which gates on `initialized`) would never even try to path them up onto the dock.
        initialized = true;
        SetDestinationSnapped(position);
    }

    /// <summary>Cancel an in-progress SeekPosition (e.g. assignment changed mid-walk).</summary>
    public void CancelSeekPosition()
    {
        _seekingTask = false;
        _onTaskArrived = null;
    }

    /// <summary>Marks the agent as busy with a stationary task (e.g. receiving a pallet) so the
    /// generic waypoint-progression fallback doesn't send it off to a random waypoint while it's
    /// supposed to be standing still. Callers must clear this when the task ends.</summary>
    public void SetTaskBusy(bool busy) => _taskBusy = busy;

    /// <summary>True while a task driver (e.g. ReceivingTaskDriver) has claimed this agent for a
    /// stationary task. AgentAnimation's own arrival handler (WaitAtWaypointRoutine) checks this
    /// too — it has an independent "arrived → wander after idleDelay" loop that doesn't go through
    /// this class's Update() at all, so it needed the same guard separately.</summary>
    public bool IsTaskBusy => _taskBusy;

    /// <summary>Cancels any in-progress equipment seeking and resumes the normal waypoint
    /// patrol loop. Used by EmployeeAssignmentService when a Worker's assignment changes
    /// to Patrol — does NOT vacate an MHE slot the operator may be riding; the caller is
    /// responsible for that (it runs on the rider's own AiNavigation, which is disabled
    /// while riding, so this only matters for an operator who hasn't boarded yet).</summary>
    public void Patrol()
    {
        if (_seekingEquipment) CancelEquipmentSeeking();
        if (_seekingTask) CancelSeekPosition();
        GoToRandomWaypoint();
    }

    /// <summary>Find and seek the closest unoccupied equipment matching the previous target's type.</summary>
    private void FindAndSeekNextAvailableEquipment(MHEOperatorSlot previousTarget)
    {
        if (_operatorIdentity == null || previousTarget == null) return;

        // We don't know the operator's exact role, but we know the equipment type they were seeking
        // Get the data from the previously-occupied target to match equipment type
        var previousVehicleData = previousTarget.GetComponent<PlacedObject>()?.data;
        if (previousVehicleData == null) return;

        // Find all MHE slots and pick the closest unoccupied one matching the previous equipment type
        var allSlots = FindObjectsByType<MHEOperatorSlot>();
        MHEOperatorSlot closest = null;
        float closestDist = float.MaxValue;

        foreach (var slot in allSlots)
        {
            if (slot.IsOccupied) continue;
            if (slot == previousTarget) continue;  // Skip the one that just became occupied

            var vehicleObj = slot.GetComponent<PlacedObject>();
            if (vehicleObj == null) continue;

            // Check if this equipment matches the type they were just seeking
            if (vehicleObj.data != previousVehicleData) continue;

            float dist = Vector3.Distance(transform.position, slot.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = slot;
            }
        }

        if (closest != null)
        {
            SeekEquipment(closest);
        }
    }

    // Warps the agent onto the best nearby walkable surface, searching upward first.
    //
    // Both call sites' own comments already assumed this "always snaps, prefers the higher
    // surface" behavior — but two bugs meant neither ever actually happened:
    //   1. The early-return `if (agent.isOnNavMesh) return;` skipped the search entirely
    //      whenever the agent already read as on SOME navmesh — and a freshly spawned/
    //      save-loaded agent reads isOnNavMesh=true the instant its NavMeshAgent activates,
    //      snapped by Unity to whatever's nearest, usually the y=0 ground plane directly under
    //      an elevated foundation. The intended upward search never ran, so they stayed
    //      visually clipped into the floor — resolved only once real movement gave Unity's own
    //      pathfinding a reason to re-place them, matching "fine once they start moving, stuck
    //      in the ground while idle."
    //   2. The offset order checked the agent's CURRENT height FIRST (0f before 1.0f/-1.0f),
    //      so even without bug #1 it would lock onto the ground-level match before ever trying
    //      the elevated one above it.
    private void SnapToNavMeshSurface()
    {
        if (agent == null) return;

        // Only fall through to the NEGATIVE (below current height) offsets when the agent isn't
        // already validly on a mesh. Every dock/floor surface has a ground-level (y≈0) NavMesh
        // directly beneath it (for rats etc.), so once an agent is already correctly standing on
        // the elevated surface, a routine rebake that transiently fails to re-sample its exact
        // CURRENT height (offset 0f — e.g. right at a polygon edge) used to fall through to -0.5f/
        // -1.0f, find that ground-level mesh, and Warp a perfectly-fine agent straight down through
        // the floor. This is what caused a Receiver — the one agent that stands motionless through
        // many rebake cycles in a row — to visibly sink to y=0 mid-animation. An agent that isn't
        // on a mesh yet (freshly spawned/save-loaded, or genuinely knocked off) still gets the full
        // range so it can find ANY nearby surface as a recovery fallback.
        bool alreadyOnMesh = agent.isOnNavMesh;
        float[] yOffsets = alreadyOnMesh
            ? new[] { 1.0f, 0.5f, 0f }
            : new[] { 1.0f, 0.5f, 0f, -0.5f, -1.0f };

        foreach (float offset in yOffsets)
        {
            Vector3 sample = new Vector3(transform.position.x, transform.position.y + offset, transform.position.z);
            // Restrict sampling to this agent's own allowed areas — NavMesh.AllAreas would also match
            // the ground-level (y~0) layer baked under every dock/floor for non-human agents ("rats"),
            // letting a human/MHE agent snap down onto that layer during an edge-case rebake.
            if (NavMesh.SamplePosition(sample, out NavMeshHit hit, 0.5f, agent.areaMask))
            {
                try { agent.Warp(hit.position); } catch { }
                return;
            }
        }
    }

    /// <summary>
    /// Finds the nearest point THIS agent could legitimately stand on, widening the search until
    /// something is found. Filters by the agent's own type + areaMask so a Human never gets rescued
    /// onto MHE-only mesh (or vice versa) — NavMesh.AllAreas with the default agent type, which the
    /// old recovery used, could hand back a point this agent can't actually occupy.
    /// </summary>
    private bool TryFindNavMeshNear(Vector3 origin, out Vector3 point, out float distance)
    {
        point = origin; distance = 0f;
        if (agent == null) return false;

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        foreach (float r in k_RescueRadii)
        {
            if (NavMesh.SamplePosition(origin, out NavMeshHit hit, r, filter))
            {
                point = hit.position;
                distance = Vector3.Distance(origin, hit.position);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Finds somewhere to walk to that is genuinely OUT of the pocket the agent is trapped in.
    ///
    /// Needed because a walled-in agent is usually standing ON navmesh — the pocket is navmesh, it's
    /// just an isolated island with no route off it. A plain SamplePosition therefore returns the
    /// agent's own position (measured live: "walking out to &lt;same spot&gt; (0.00m to mesh)") and the
    /// walk-out moves nobody. So instead: probe outward in a ring of directions for a point that is
    /// both a real distance away AND currently UNREACHABLE by path — unreachable proves it is on the
    /// far side of whatever is blocking us, which is exactly where we want to end up.
    /// </summary>
    private bool TryFindEscapePoint(Vector3 origin, out Vector3 point)
    {
        point = origin;
        if (agent == null) return false;

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        var path = new NavMeshPath();
        const int Directions = 12;
        Vector3 best = origin; float bestScore = -1f;

        foreach (float radius in k_EscapeRadii)
        {
            for (int i = 0; i < Directions; i++)
            {
                float ang = (360f / Directions) * i * Mathf.Deg2Rad;
                Vector3 probe = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;

                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 2f, filter)) continue;

                float d = Vector3.Distance(origin, hit.position);
                if (d < MinEscapeDistance) continue;   // too close — still inside the pocket

                // Unreachable by path == on the other side of the blockage. That's the winner.
                bool reachable = agent.isOnNavMesh
                                 && NavMesh.CalculatePath(origin, hit.position, filter, path)
                                 && path.status == NavMeshPathStatus.PathComplete;
                if (!reachable) { point = hit.position; return true; }

                // Otherwise keep the furthest reachable point as a fallback — walking there still
                // gets us clear of a tight spot even if the mesh isn't actually severed.
                if (d > bestScore) { bestScore = d; best = hit.position; }
            }
        }

        if (bestScore > 0f) { point = best; return true; }
        return false;
    }

    /// <summary>
    /// Walks the agent out of a spot it cannot path from. Takes the transform over for the duration
    /// (agent disabled so it can't fight us), moves in a straight line to the nearest valid navmesh
    /// plus <see cref="UnstickOvershoot"/> beyond it, then warps back onto the mesh and resumes.
    ///
    /// Deliberately a straight walk rather than a teleport: the walk animation keeps playing (the
    /// Animator is driven by velocity elsewhere, and we move the transform steadily), so it reads as
    /// the worker stepping clear rather than snapping across the room. The move is short and local,
    /// so walking through a carved pocket's edge is acceptable — that pocket is precisely what has
    /// no navmesh to path around anyway.
    /// </summary>
    private IEnumerator UnstickWalkOut()
    {
        if (_unsticking) yield break;
        _unsticking = true;
        _offMeshSeconds = 0f;
        _blockedSeconds = 0f;

        Vector3 from = transform.position;

        // Prefer a point genuinely OUTSIDE the pocket (handles the walled-in case, where the agent is
        // standing on navmesh that happens to be an isolated island). Fall back to the plain
        // nearest-navmesh snap, which is the right answer when we're simply off-mesh.
        bool haveTarget = TryFindEscapePoint(from, out Vector3 target);
        if (!haveTarget)
            haveTarget = TryFindNavMeshNear(from, out target, out _);

        if (!haveTarget)
        {
            Debug.LogError($"[AiNavigation] {name}: STUCK at {from} and no escape point found within " +
                           $"{k_RescueRadii[k_RescueRadii.Length - 1]}m — cannot walk out.");
            _unsticking = false;
            yield break;
        }

        float dist = Vector3.Distance(from, target);

        // Step a little past the recovered point so we clear the pocket rather than stopping on its lip.
        Vector3 dir = target - from; dir.y = 0f;
        Vector3 finalTarget = dir.sqrMagnitude > 0.01f
            ? target + dir.normalized * UnstickOvershoot
            : target;
        if (NavMesh.SamplePosition(finalTarget, out NavMeshHit overshootHit, 2f,
                new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }))
            finalTarget = overshootHit.position;

        Debug.LogWarning($"[AiNavigation] {name}: stuck at {from} — walking out to {finalTarget} ({dist:F2}m to mesh).");

        bool agentWas = agent.enabled;
        agent.enabled = false;   // we own the transform for the duration

        float elapsed = 0f;
        while (Vector3.Distance(transform.position, finalTarget) > 0.1f && elapsed < UnstickTimeout)
        {
            elapsed += Time.deltaTime;
            Vector3 next = Vector3.MoveTowards(transform.position, finalTarget, UnstickWalkSpeed * Time.deltaTime);
            transform.position = next;
            if (_rb != null) _rb.MovePosition(next);

            Vector3 face = finalTarget - transform.position; face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(face.normalized), 180f * Time.deltaTime);
            yield return null;
        }

        agent.enabled = agentWas;
        if (agent.isActiveAndEnabled)
        {
            agent.Warp(transform.position);
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                // Resume whatever the agent was doing. A task-seeking agent keeps its destination;
                // otherwise fall back to the patrol loop.
                if (_seekingTask) SetDestinationSnapped(_taskTargetPosition);
                else if (_seekingEquipment && _targetEquipment != null) SetDestinationSnapped(_targetEquipment.transform.position);
                else GoToRandomWaypoint();
            }
        }

        Debug.Log($"[AiNavigation] {name}: walk-out complete at {transform.position} (onMesh={agent.isOnNavMesh}).");
        _unsticking = false;
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
        // ── Parked forklift: no driver, no movement ──────────────────────────────
        // Stop the agent and kill the animation rather than let an empty vehicle
        // keep wandering the warehouse on its own. Flipped via SetParked().
        if (_parked)
        {
            if (agent != null && agent.isActiveAndEnabled) agent.isStopped = true;
            if (_animator != null) _animator.enabled = false;
            return;
        }
        if (_animator != null && !_animator.enabled) _animator.enabled = true;

        // ── Equipment seeking (operator walks toward placed equipment) ──────────────
        if (_seekingEquipment && _targetEquipment != null)
        {
            // Check if target equipment became occupied
            if (_targetEquipment.IsOccupied)
            {
                // Target is now occupied — find the next available equipment of the same type.
                // Capture the target BEFORE cancelling: CancelEquipmentSeeking() nulls
                // _targetEquipment, and FindAndSeekNextAvailableEquipment needs the OLD
                // target to know what equipment type to match.
                var previousTarget = _targetEquipment;
                CancelEquipmentSeeking();
                FindAndSeekNextAvailableEquipment(previousTarget);
            }
            // Check if we've reached the equipment (very generous distance tolerance)
            else if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathComplete
                     && agent.remainingDistance <= 2.0f)  // Increased from 0.5f to account for equipment position offset
            {
                // Reached equipment — board it. Capture the target BEFORE cancelling:
                // CancelEquipmentSeeking() nulls _targetEquipment, and the old code dereferenced
                // it AFTER nulling it — a guaranteed NullReferenceException on every arrival,
                // which is why operators would walk up to equipment and then just stand there.
                var target = _targetEquipment;
                CancelEquipmentSeeking();
                target.AssignOperator(_operatorIdentity);
            }
            // Check if path is invalid (can't reach equipment)
            else if (agent.pathStatus == NavMeshPathStatus.PathInvalid && !agent.pathPending)
            {
                Debug.LogWarning($"[AiNavigation] {name} cannot reach equipment at {_targetEquipment.transform.position} (path invalid) — seeking next available");
                var previousTarget = _targetEquipment;
                CancelEquipmentSeeking();
                FindAndSeekNextAvailableEquipment(previousTarget);
            }
        }

        // ── Task seeking (employee walks toward a generic task position, e.g. a pallet) ──────────
        if (_seekingTask)
        {
            if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathComplete
                && agent.remainingDistance <= 1.0f)
            {
                var callback = _onTaskArrived;
                CancelSeekPosition();
                callback?.Invoke();
            }
            else if (agent.pathStatus == NavMeshPathStatus.PathInvalid && !agent.pathPending)
            {
                Debug.LogWarning($"[AiNavigation] {name} cannot reach task position at {_taskTargetPosition} (path invalid) — abandoning task seek.");
                CancelSeekPosition();
            }
            else if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                // PathPartial satisfied NEITHER branch above, so a seek to a target the agent can
                // only PARTIALLY reach hung forever: never "arrived" (not PathComplete) and never
                // cancelled (not PathInvalid). The caller's arrival callback therefore never fired
                // and its own in-progress flag never cleared — that is how a Receiver ended up
                // standing motionless inside a rack indefinitely (observed live: PathPartial with
                // remainingDistance 0.14m).
                //
                // The agent has genuinely walked as far as the mesh allows, so once it stops moving
                // at the end of that partial path, treat it as arrived. Being a metre or two short
                // of an unreachable target is the best outcome available, and it lets the task
                // proceed instead of deadlocking the whole receiver.
                bool atEndOfReachable = agent.remainingDistance <= agent.stoppingDistance + 0.5f;
                bool stoppedMoving    = agent.velocity.sqrMagnitude < 0.02f;

                if (atEndOfReachable && stoppedMoving)
                {
                    _partialSeekSeconds += Time.deltaTime;
                    if (_partialSeekSeconds >= PartialSeekGraceSeconds)
                    {
                        Debug.LogWarning($"[AiNavigation] {name}: task target {_taskTargetPosition} only " +
                            $"partially reachable — stopped {agent.remainingDistance:F2}m short. Treating as arrived.");
                        _partialSeekSeconds = 0f;
                        var callback = _onTaskArrived;
                        CancelSeekPosition();
                        callback?.Invoke();
                    }
                }
                else
                {
                    _partialSeekSeconds = 0f;   // still making progress along the partial path
                }
            }
            else
            {
                _partialSeekSeconds = 0f;
            }
        }

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
            if (_unsticking) return; // the walk-out coroutine owns the transform right now

            if (Time.frameCount % 30 == 0)
            {
                // Progressive, agent-type-aware search. The old version sampled a fixed 2m with
                // NavMesh.AllAreas and then REFUSED to warp unless the hit was within 1m vertically —
                // so an agent that ended up further out, or that dropped to ground level while its
                // navmesh sat ~1.15m above (seen live on a reach truck at y=0.02), could never
                // recover and stayed frozen forever with no fallback at all.
                if (TryFindNavMeshNear(transform.position, out Vector3 rescue, out float dist))
                {
                    agent.Warp(rescue);
                    _offMeshSeconds = 0f;
                    // Same taskBusy/seeking guard as OnNavMeshBaked() — re-snapping onto the mesh
                    // is always safe, but overwriting a Receiver's destination mid-task is not.
                    if (!_taskBusy && !_seekingTask && !_seekingEquipment
                        && waypoints != null && waypoints.Length > 0)
                        agent.SetDestination(waypoints[currentIndex].position);
                }
                else
                {
                    // Nothing reachable to snap to — this is the genuinely walled-in case (e.g. a
                    // pallet's NavMeshObstacle carved the floor out from under a standing worker).
                    // Escalate to physically walking out (fix #2).
                    _offMeshSeconds += 30f * Time.deltaTime;
                    if (_offMeshSeconds >= StuckEscalateSeconds)
                        StartCoroutine(UnstickWalkOut());
                }
            }
            return;
        }
        else
        {
            _offMeshSeconds = 0f;
        }

        // ── Recovery: re-snap if we're ON the mesh but rendering at the wrong HEIGHT ──
        // The block above only fires when isOnNavMesh is false. An agent can be perfectly
        // on the mesh and still be drawn at the wrong height (see ReconcileSurfaceY), which
        // that check cannot see.
        ReconcileSurfaceY();

        // ── Recovery: ON the mesh, correct height, but STRANDED on an island ──
        CheckStrandedOnNavMesh();

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
            // isOnNavMesh is re-checked HERE, not just at the top of Update: UpdateRebakeTrigger()
            // immediately above can rebuild the surface and drop this agent off the mesh mid-frame,
            // and remainingDistance throws ("GetRemainingDistance can only be called on an active
            // agent that has been placed on a NavMesh") the moment that happens.
            if (agent.isOnNavMesh
                && !_taskBusy
                && !agent.pathPending
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
        // Same for the unstick walk-out — it owns the transform, and copying nextPosition over the
        // top of it would drag the agent straight back into the pocket it is escaping.
        if (_unsticking) return;

        Vector3 np = agent.nextPosition;
        transform.position = np;
        if (_rb != null)
            _rb.MovePosition(np);
        // Rotation is owned by AgentAnimation.LateUpdate() — do not set _rb.rotation
        // here, as it fires before AgentAnimation's LateUpdate and would overwrite it.
    }

    // ── Stranded-on-NavMesh recovery ─────────────────────────────────────────
    // An agent can be perfectly ON a NavMesh and still be completely stuck, because the mesh it is
    // standing on is a DISCONNECTED ISLAND. Seen live on a dock stocker that finished a load run at
    // the dock edge: the restore Warp snapped it onto the yard mesh below instead of the dock, and it
    // sat at ground level with isOnNavMesh=true, pathStatus=Partial, and a one-corner path pointing at
    // its own feet. Every existing recovery missed it — the rescue block above only fires on
    // !isOnNavMesh, ReconcileSurfaceY sees no height drift (it really is on that surface), and
    // CheckDockLedge needs a LedgeLinkMarker within 2.5m, which open yard doesn't have.
    //
    // The test deliberately is NOT "idle and pathless" — an agent waiting at a patrol waypoint looks
    // exactly like that and would be constantly false-positived. It's "can this agent reach ANY of its
    // waypoints?". A normally idle agent still can; a stranded one can't reach a single one. Checked on
    // a slow timer and bailing at the first reachable waypoint, so the usual cost is one CalculatePath.
    private const float StrandedCheckPeriod  = 2.5f; // seconds between reachability probes
    private const float StrandedRescueSeconds = 7.5f; // must be unreachable this long before we teleport
    private float _strandedCheckTimer;
    private float _strandedSeconds;
    private NavMeshPath _strandedPath;

    private void CheckStrandedOnNavMesh()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        // Something else owns this agent's movement right now — don't second-guess it.
        if (_traversingLink || _unsticking || agent.isOnOffMeshLink) return;
        if (_taskBusy || _seekingTask || _seekingEquipment) return;
        if (waypoints == null || waypoints.Length == 0) return;

        _strandedCheckTimer += Time.deltaTime;
        if (_strandedCheckTimer < StrandedCheckPeriod) return;
        _strandedCheckTimer = 0f;

        if (_strandedPath == null) _strandedPath = new NavMeshPath();

        for (int i = 0; i < waypoints.Length; i++)
        {
            var wp = waypoints[i];
            if (wp == null) continue;
            if (agent.CalculatePath(wp.position, _strandedPath) &&
                _strandedPath.status == NavMeshPathStatus.PathComplete)
            {
                _strandedSeconds = 0f; // at least one waypoint is reachable — healthy
                return;
            }
        }

        _strandedSeconds += StrandedCheckPeriod;
        if (_strandedSeconds < StrandedRescueSeconds) return;
        _strandedSeconds = 0f;

        // Nothing at all is reachable from here. Teleport to the nearest waypoint's surface — the one
        // move guaranteed to put the agent back on the connected mesh, and the reason this exists at
        // all: with nothing left to do, a dock stocker should be back out on patrol, not parked in the
        // yard forever.
        Transform nearest = null;
        float bestSqr = float.MaxValue;
        foreach (var wp in waypoints)
        {
            if (wp == null) continue;
            float d = (wp.position - transform.position).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; nearest = wp; }
        }
        if (nearest == null) return;

        if (!NavMesh.SamplePosition(nearest.position, out NavMeshHit hit, 4f, agent.areaMask))
        {
            Debug.LogWarning($"[AiNavigation] {name}: stranded on an isolated NavMesh island at {transform.position}, " +
                             $"and the nearest waypoint has no NavMesh within 4m either — cannot self-rescue.");
            return;
        }

        Debug.LogWarning($"[AiNavigation] {name}: stranded on an isolated NavMesh island at {transform.position} " +
                         $"(no waypoint reachable for {StrandedRescueSeconds:F0}s) — warping to {hit.position} to rejoin patrol.");
        agent.Warp(hit.position);
        transform.position = hit.position;
        agent.isStopped = false;
        GoToRandomWaypoint();
    }

    // ── Surface-height reconciliation ────────────────────────────────────────
    // Because updatePosition=false, LateUpdate renders agent.nextPosition VERBATIM — there is
    // no ground projection anywhere in the pipeline. And NavMeshAgent.Warp() re-maps the agent
    // to the nearest polygon but does NOT lift its position onto that polygon's surface, so
    // every Warp(transform.position) recovery call faithfully preserves a bad height instead of
    // fixing it. The result is an agent that is genuinely ON the dock island — full PathComplete
    // routes, corners at dock height — while being drawn ~1.1m lower, sunk to the waist in the
    // deck. Seen live on two Receivers walking dock waypoints at y=0.005 with their own path
    // corners at y=1.129.
    //
    // Nothing else catches this: the isOnNavMesh recovery above never fires (the agent IS on the
    // mesh), and CheckDockLedge's climb never fires either, because it gates on a PARTIAL path
    // and this agent's path completes perfectly. So the state is invisible to every existing
    // recovery and persists until the agent is destroyed.
    //
    // path.corners[0] is the agent's own position projected onto the polygon it is standing on,
    // so its Y is exactly the surface height the agent should be rendered at. Comparing against
    // it needs no raycast and no NavMesh.SamplePosition (which would defeat the purpose — it
    // returns the NEAREST surface, i.e. the ground the agent has already sunk to, and would
    // report everything as fine). Correct via the nextPosition setter rather than Warp: it moves
    // the simulated position without re-mapping or discarding the current path.
    private const float SurfaceYTolerance   = 0.30f; // allow normal ramp/step slop before correcting
    private const float SurfaceYCheckPeriod = 0.5f;  // seconds between checks (agent.path allocates)
    private float _surfaceYCheckTimer;

    private void ReconcileSurfaceY()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        // Every one of these owns the transform / height itself right now.
        if (_traversingLink || _unsticking || agent.isOnOffMeshLink) return;
        if (IsTraversingLedgeUp || IsTraversingLedgeDown) return;
        if (!agent.hasPath || agent.pathPending) return;

        _surfaceYCheckTimer += Time.deltaTime;
        if (_surfaceYCheckTimer < SurfaceYCheckPeriod) return;
        _surfaceYCheckTimer = 0f;

        var corners = agent.path.corners;
        if (corners.Length == 0) return;

        float surfaceY = corners[0].y;
        Vector3 np     = agent.nextPosition;
        float   drift  = np.y - surfaceY;
        if (Mathf.Abs(drift) <= SurfaceYTolerance) return;

        Debug.LogWarning($"[AiNavigation] {name}: rendering {drift:F2}m off its NavMesh surface " +
                         $"(pos Y {np.y:F2} vs surface {surfaceY:F2}) — re-snapping to the surface.");
        agent.nextPosition = new Vector3(np.x, surfaceY, np.z);
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

        // Use the CURRENT TARGET's true height, not agent.destination.y — the latter is mapped
        // onto the nearest NavMesh and can read ground even when targeting the dock. The target
        // is either the MHE equipment an operator is walking to board (no patrol waypoint at
        // all while seeking — this case was previously skipped entirely, leaving operators
        // stuck waving at the dock edge instead of climbing up) or the normal patrol waypoint.
        Vector3? targetPos = _seekingEquipment && _targetEquipment != null
            ? _targetEquipment.transform.position
            : _seekingTask
                ? _taskTargetPosition
                : ((waypoints != null && currentIndex >= 0 && currentIndex < waypoints.Length)
                    ? waypoints[currentIndex].position
                    : (Vector3?)null);
        if (targetPos == null) { _ledgeCheckTimer = 0f; return; }

        // Target is on a different height level than the agent → needs a ledge.
        float posY = agent.transform.position.y;
        float tgtY = targetPos.Value.y;
        bool goingUp   = posY < 0.3f && tgtY > 0.5f;
        bool goingDown = posY > 0.5f && tgtY < 0.3f;
        if (!goingUp && !goingDown) { _ledgeCheckTimer = 0f; return; }

        // Only when the agent can't get there directly (partial path) and has reached
        // the end of what it CAN walk (i.e. it's sitting at the dock edge), essentially stopped.
        // Same guard as the waypoint-progression block: a rebake can drop the agent off the mesh
        // between the top of Update() and here, and remainingDistance throws when that happens.
        // Off the mesh there is no meaningful ledge decision to make anyway — the isOnNavMesh
        // recovery in Update() owns that case.
        if (!agent.isOnNavMesh) { _ledgeCheckTimer = 0f; return; }

        bool blocked  = agent.pathStatus != NavMeshPathStatus.PathComplete;
        bool atEnd    = agent.remainingDistance <= agent.stoppingDistance + 0.75f;
        bool stopped  = agent.velocity.sqrMagnitude < 0.06f;
        if (!blocked || !(atEnd || stopped)) { _ledgeCheckTimer = 0f; return; }

        // Find the nearest dock-edge marker (the climb pivot). Wider radius than before
        // because the eroded ground navmesh stops short of the foundation edge.
        if (_cachedLedges == null)
            _cachedLedges = FindObjectsByType<LedgeLinkMarker>();
        LedgeLinkMarker nearest = null;
        float nearestXZ = 2.5f;
        float ax = agent.transform.position.x;
        float az = agent.transform.position.z;
        foreach (var m in _cachedLedges)
        {
            if (m == null) continue;
            if (!m.gameObject.name.StartsWith("LedgeLink_")) continue;
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

        // Floor endpoint for a jump-down. A fixed 0.5 m step can land in the eroded gap
        // between the dock and ground NavMesh islands; the end-of-traversal Warp then
        // snaps the agent onto the real ground edge, which shows up as a lateral teleport
        // the instant the animation ends. Instead, step outward from the dock edge and
        // sample the GROUND NavMesh (y < 0.5) so the jump-down lands exactly where the
        // agent will stand — leaving nothing for Warp to correct.
        //
        // If the search finds NOTHING there is no landing spot, and we must NOT fall back to a
        // made-up point. The old fallback seeded floorPt at a hardcoded y = 0f and used it
        // unconditionally: the agent got lerped down to a height never validated against any
        // NavMesh and then Warped, which leaves it standing at ground level while still mapped to
        // the DOCK island. That state is unrecoverable by every check in this file — see
        // ReconcileSurfaceY — so refuse the jump instead and let the agent keep walking the dock.
        Vector3 floorPt   = Vector3.zero;
        bool    foundFloor = false;
        for (float d = 0.5f; d <= 3.0f; d += 0.25f)
        {
            Vector3 probe = new Vector3(dockPt.x + fwd.x * d, 0f, dockPt.z + fwd.z * d);
            if (NavMesh.SamplePosition(probe, out NavMeshHit groundHit, 0.6f, NavMesh.AllAreas)
                && groundHit.position.y < 0.5f)
            {
                floorPt = groundHit.position;
                foundFloor = true;
                break;
            }
        }
        if (!goingUp && !foundFloor)
        {
            Debug.LogWarning($"[AiNavigation] {name}: jump-down refused at ledge {dockPt} — no ground " +
                             $"NavMesh within 3m to land on. Staying on the dock.");
            _ledgeCheckTimer = 0f;
            return;
        }

        _pendingFrom         = agent.transform.position;
        _pendingTo           = goingUp ? dockPt : floorPt;
        _hasPendingPositions = true;

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
        // path that was PathPartial before the climb will complete. Prefer the equipment-seek
        // target over the patrol waypoint — an operator seeking MHE equipment has no patrol
        // waypoints at all, so without this they'd climb the ledge and then just stop.
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            if (_seekingEquipment && _targetEquipment != null)
                SetDestinationSnapped(_targetEquipment.transform.position);
            else if (_seekingTask)
                SetDestinationSnapped(_taskTargetPosition);
            else if (waypoints != null && waypoints.Length > 0 && waypoints[currentIndex] != null)
                SetDestinationSnapped(waypoints[currentIndex].position);
        }
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

        foreach (var marker in FindObjectsByType<LedgeLinkMarker>())
        {
            if (marker == null) continue;
            if (!marker.gameObject.name.StartsWith("LedgeLink_")) continue;
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
                    _stuckRebakeTimer = 0f;
                    _stuckRefPos = transform.position;
                    _consecutiveStuckCount++;

                    // Smarter replanning: abandon the current destination and pick a new one
                    // before trying a heavy global NavMesh rebake. This resolves deadlocks
                    // between agents by forcing one of them to turn around.
                    if (HasEnoughWaypoints)
                    {
                        Debug.Log($"[AiNavigation] {name}: stuck for {k_RebakeTrigger}s, abandoning destination.");
                        GoToRandomWaypoint();
                    }

                    // Only rebake as a last resort if they get stuck multiple times in a row
                    // (which suggests a broken NavMesh, not just congestion).
                    if (_consecutiveStuckCount >= 2)
                    {
                        TryForceRebake("persistently stuck");
                        _consecutiveStuckCount = 0;
                    }
                }

                // ── Walled-in escalation ──────────────────────────────────────────────────
                // Distinct from the congestion case above. A rebake and a new destination both
                // assume a route EXISTS and the agent is merely being jostled. When a pallet's
                // NavMeshObstacle carves the floor around a standing worker, no route exists at
                // all — the agent has a path it can never advance along, so it would sit here
                // re-picking waypoints and re-baking forever. If it still hasn't moved after
                // StuckEscalateSeconds, physically walk it out.
                _blockedSeconds += Time.deltaTime;
                if (_blockedSeconds >= StuckEscalateSeconds && !_unsticking)
                {
                    _blockedSeconds = 0f;
                    StartCoroutine(UnstickWalkOut());
                }
            }
            else
            {
                _stuckRebakeTimer = 0f;
                _stuckRefPos = transform.position;
                _consecutiveStuckCount = 0;
                _blockedSeconds = 0f;   // real progress — reset the walled-in timer
            }
        }
else
        {
            _stuckRebakeTimer = 0f;
            _stuckRefPos = transform.position;
            _blockedSeconds = 0f;
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
