using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Services;

/// <summary>
/// Drives a truck through a simple scripted yard route using direct Transform movement.
/// No NavMeshAgent — trucks follow a fixed set of waypoints.
///
/// Route:
///   Spawn → GateStop (guard inspection)
///   → Xform 1  GateEnterNoTurn        (drive through, NO stop)
///   → Xform 2  _drApproach-DepartPoint (drive straight, NO stop)
///   → BACK IN (2026-11-14, BeginBackIn): drive along the yard past the door, reverse a quarter
///     turn that swings the trailer's rear onto the door's centre line, back straight in.
///   → Docked (unload timer)
///   → pull out forward to Xform 2
///   → Xform 5  GateLeaveNoTurn         (drive through, NO stop)
///   → Exit point → shrink + destroy
///
/// Xform 2 is a SINGLE SHARED waypoint (named child of the guard shack) used by
/// every truck regardless of which door it's assigned. The door (Xform 4) is the
/// assigned DockSlot's DockPosition / DockRotation.
///
/// NO FREE DOOR AT ARRIVAL (2026-09): the truck still queues at the gate and gets
/// inspected exactly as above — a driver who shows up on time isn't turned away
/// sight unseen. After Xform 1 it instead curves to the yard's "DoorWaitPoint"
/// (an optional named child, same convention as GateEnterNoTurn/GateLeaveNoTurn)
/// and parks there, showing the unified 8-hour inbound dwell-clock ladder (TruckDoorWaitBar —
/// see TruckController.UpdateInboundDwellClock). It polls for a freed-up door and, if one appears,
/// drives in exactly like a normal arrival (rejoining the route above at Xform 2). If the whole
/// 8-hour dwell clock elapses with nothing ever offloaded, the driver gives up — the load is lost
/// (no refund) and vendor Partnership takes its biggest hit (−15) — and exits via the normal
/// departure route.
/// </summary>
public class TruckController : MonoBehaviour
{
    public enum TruckState
    {
        Idle,
        Queuing, GuardCheck,   // lining up at / holding the gate
        ToEnterNoTurn,     // → Xform 1
        ToApproach,        // → Xform 2
        ToBackup,          // → the door, dead straight, then flip to face away (see Update())
        WaitingAtBackup,   // RETIRED (old curved-turn-and-reverse maneuver) — kept only so an
        Reversing,         // old save's stored enum ordinal never resolves to the wrong state.
        ReversingToDock,   // Never entered by new gameplay; see RestoreFromSnapshot.
        Docked,
        DepartToApproach,  // door → Xform 2
        ToLeaveNoTurn,     // Xform 2 → Xform 5
        ToExit,            // Xform 5 → Exit point
        Exiting,
        ToDoorWait,        // Xform 1 → the door-wait park spot (no free door at arrival)
        WaitingForDoor,    // parked at the wait spot, polling for a door / counting down
        // SideLot (2026-09): superseded the very next session by the 3-state Entry/Anchor version
        // below once the prefab grew a dedicated SideLotEntry marker — kept inert, not deleted, per
        // this file's own retired-state convention (an old save's stored ordinal never resolves to
        // the wrong state).
        ToSideLot,
        ReversingOutOfSideLot,

        ToSideLotEntry,          // straight drive from wherever to the SideLot's Entry marker
                                 // (further out from the fence than the Anchor) — no curve, no
                                 // dedicated facing step; DriveToward's normal turn-while-moving is
                                 // all it gets, per Tad's "don't worry about rotating for now"
        FacingSideLotAnchor,     // at Entry: rotate in place to face the Anchor — squares the truck
                                 // up perpendicular to the barrier ahead, like backing a real trailer
                                 // into a straight lot, before pulling forward
        ToSideLotAnchor,         // straight drive forward from Entry into the Anchor — already
                                 // facing it correctly courtesy of FacingSideLotAnchor
        ReversingToSideLotEntry, // a door freed up — straight-line reverse from the Anchor back to
                                 // Entry, then BeginBackIn for the claimed door

        // RETIRED 2026-11-14: the TruckNavPoint0-5 / TruckPivot0-1 backing chain, replaced by the three
        // BackIn states below. Kept ONLY so an old save's stored ordinal never resolves to the wrong state;
        // RestoreFromSnapshot docks a truck saved in any of them. Never entered by gameplay.
        DevCheckpoint_GateEnterNoTurn, FacingTruckNavPoint0, ToTruckNavPoint0, FacingTruckNavPoint1,
        DevCheckpoint_FacingNav1, ToTruckNavPoint1, DevCheckpoint_AtNav1, ToTruckNavPoint2, DevCheckpoint_AtNav2,
        FacingTruckNavPoint3, DevCheckpoint_TractorFacingNav3, ToTruckNavPoint3, FacingTruckNavPoint4,
        DevCheckpoint_TractorFacingNav4, ToTruckNavPoint4, StraighteningAtNavPoint4, DevCheckpoint_StraightAtNav4,
        ReversingStraightSlow, DevCheckpoint_ReversedSlow, ReversingToTruckNavPoint5, StraighteningAtNavPoint5,
        DevCheckpoint_AtNavPoint5, ReversingToTruckNavPoint3, DevCheckpoint_AtNavPoint3Reverse,
        ReversingArcAroundPivot1, StraighteningIntoDoor, StraighteningAtNavPoint2,
        TrailerAligningToNavPoint1, TrailerAligningToNavPoint4,

        // Back-in (2026-11-14, from the NavTest graybox). See BeginBackIn.
        BackInToSwingStart,   // forward along the yard, past the door, ending parallel to the wall
        BackInArc,            // reverse a quarter turn: the trailer's rear axle swings onto the door's centre line
        BackInStraight        // back straight in, squaring up, then Docked
    }

    [Header("Back-in (2026-11-14: drive past the door, reverse a quarter turn, back straight in)")]
    [Tooltip("Metres/second while reversing (the quarter turn and the straight leg), and for the SideLot reverses.")]
    [SerializeField] private float slowReverseSpeed = 1.5f;
    [Tooltip("Metres/second while pulling forward from the SideLot's Entry marker into its Anchor.")]
    [SerializeField] private float sideLotPullInSpeed = 3f;
    [Tooltip("Straight reverse (metres, measured at the trailer's rear axle) at the end, square to the door.")]
    [SerializeField] private float backInStraight = 10f;
    [Tooltip("Size of the reversing quarter turn (metres): the rear axle swings from this far along the wall onto the door's centre line.")]
    [SerializeField] private float backInArm = 9f;
    [Tooltip("Cosmetic: tractor angle (degrees) per degree/second the trailer turns while reversing.")]
    [SerializeField] private float backInCabSteer = 2f;
    [Tooltip("The trailer's rear tandem axle in the rig's own unrotated local space. The back-in plans its path for THIS point, so the trailer's rear leads the turn like a real reversing trailer.")]
    [SerializeField] private Vector3 trailerRearAxleLocalOffset = new Vector3(0f, 0.54f, -7.19f);

    // Back-in path (rear axle): quarter turn = quadratic Bezier _biP0 -> _biP1 (corner) -> _biP2 (line-up), then straight to _biAxleTo.
    private Vector3 _biP0, _biP1, _biP2, _biAxle, _biAxleTo;
    private float _biT, _biLen;

    [Header("Driving")]
    // Doubled per Tad's explicit request 2026-09 — backing/approach legs were reading as taking
    // "an hour" in practice. Pure speed constants; the Bézier/steering math is all speed-agnostic.
    // Doubled again 2026-09 per Tad's request ("have it go twice as fast").
    [SerializeField] private float driveSpeed       = 10.0f; // was 5.0 (originally 2.5)
    [SerializeField] private float driveTurnSpeed   = 180f;
    [SerializeField] private float arrivedThreshold = 0.5f;

    [Header("Forward Bézier curve (door-wait / departure curves)")]
    [Tooltip("0 = nearly straight, 1 = wide sweeping curve. 0.45 reads as a natural truck arc.")]
    [SerializeField] private float forwardDriveTension = 0.45f;

    [Header("Cab steering (tractor yaws at the hitch)")]
    [Tooltip("Turn the cab on its Y axis so it leads into curves, like a real tractor pivoting at the fifth wheel. Pure yaw — never touches X/Z.")]
    [SerializeField] private bool  articulateCab   = true;
    [Tooltip("Name of the cab child transform that pivots. Origin sits at the hitch, so it swings correctly.")]
    [SerializeField] private string cabChildName   = "Tractor";
    [Tooltip("Most the cab can crank away from the trailer body, in degrees.")]
    [SerializeField] private float maxCabSteer     = 30f;
    [Tooltip("Maps how fast the body is turning (deg/sec) to cab steer angle while driving forward.")]
    [SerializeField] private float cabSteerGain    = 0.22f;
    [Tooltip("How quickly the cab swings toward its target steer angle, deg/sec.")]
    [SerializeField] private float cabSteerSlew    = 140f;

    [Header("Unload & exit")]
    [SerializeField] private float unloadDuration = 7f;
    [Tooltip("If no dock stocker claims this docked truck within this many seconds, fall back to a bulk receive and depart so the dock doesn't wedge (e.g. no manned DS available).")]
    [SerializeField] private float offloadFallbackTimeout = 22.5f; // was 45 — halved per Tad's request
    [SerializeField] private float exitShrinkTime = 0.6f; // was 1.2 — halved per Tad's request

    [Header("Door wait (no free door at arrival, read-only — injected by TruckYardManager.Init)")]
    [Tooltip("Real seconds between checks for a newly-freed door while waiting. The wait itself no longer has its own timeout — see the unified 8-hour dwell clock, TruckController.UpdateInboundDwellClock.")]
    [SerializeField] private float doorWaitPollSeconds = 2f;

    [Header("Trailer Doors")]
    [SerializeField] private float doorOpenSpeed = 150f;
    [Tooltip("\"Barn door\" swing — real semi trailer doors open fully flat (≈180-185°) against the trailer sides so a docked dock-stocker can reach the product and it's visible from inside the warehouse.")]
    [SerializeField] private float driverDoorOpenAngle = 185f;
    [SerializeField] private float passengerDoorOpenAngle = -185f;

    [Header("Docked Ghosting")]
    [Tooltip("While docked, these renderers switch to the ghost/see-through wall material so you can see the product from inside the warehouse. Assign the trailer mesh + the left & right Savage decals.")]
    [SerializeField] private Renderer[] _ghostWhileDockedRenderers;

    [Header("Cargo (CHUNK 1 test visual)")]
    [Tooltip("Prefab instantiated 12x under Trailer/LorryTrailer/Load to represent loaded pallets — assign ChepStack.prefab (Assets/_Project/Prefabs/Inventory/ChepStack.prefab) or any prefab with a PalletBuilder component.")]
    [SerializeField] private GameObject palletVisualPrefab;
    [SerializeField] private Renderer[] _casesOriginalMaterial; // saved so the original material can be restored once pallets are received by a Receiver. If _casesGhostRenderers is assigned, this array is ignored.
    [Tooltip("Distance from trailer center to each row of 6 (left row at -offset, right row at +offset).")]
    [SerializeField] private float palletLateralOffset = 0.35f;
    [Tooltip("Spacing between the 6 pallets along the trailer's length, centered on the Load anchor.")]
    [SerializeField] private float palletRowSpacing = 0.35f;

    [Header("State (read-only in play)")]
    [SerializeField] private TruckState _state = TruckState.Idle;

    public GameCore.Inventory.ShipmentData AssignedShipment { get; private set; }

    // ── Offload (Chunk 2) handoff ────────────────────────────────────────────────
    private float _dockedTime;
    private bool  _offloadClaimed;
    private bool  _offloadComplete;

    // Tiny cosmetic lateral settle after docking, eased at half slowReverseSpeed (unused by the 2026-11-14 back-in, which ends exact).
    private Vector3 _dockedSlideTarget;
    private bool    _dockedSlideActive;

    /// <summary>Sim-clock minute this INBOUND trailer docked, for the VENDORS tab's "Avg Hours in
    /// Door" stat — real dwell time (docked-to-departed) belongs on the sim clock, same scale as
    /// every other vendor stat, not on real wall-clock seconds like `_dockedTime`'s fallback-timeout
    /// use above. -1 while not docked/not applicable.</summary>
    private long _dockedAtSimMinute = -1;

    /// <summary>Sim-clock minute this trailer (inbound OR outbound) docked, for the Scheduler's "time in door" readout. -1 when unknown (e.g. restored from a save).</summary>
    private long _dockStampSimMinute = -1;

    /// <summary>Game minutes this trailer has been at its door, or -1 if unknown.</summary>
    public int MinutesInDoor
    {
        get
        {
            var c = Ctx;
            if (_dockStampSimMinute < 0 || c == null || c.TimeService == null) return -1;
            return (int)System.Math.Max(0L, c.TimeService.TotalMinutesElapsed - _dockStampSimMinute);
        }
    }

    // ── Outbound loading (D1) handoff — mirrors the offload flags above, but for a
    // truck that arrives EMPTY and gets pallets driven ONTO it instead of off of it. Kept as
    // separate fields (not reused) so TrailerLoadController and TrailerOffloadController can never
    // cross-claim each other's trucks even if a bug ever mixed up which list they scan.
    private bool _isOutbound;
    private bool _loadClaimed;
    private bool _loadComplete;

    /// <summary>Current state of the truck (for persistence and debugging).</summary>
    public TruckState CurrentState => _state;

    /// <summary>True while docked and still waiting for a dock stocker to start offloading it.</summary>
    public bool AwaitingOffload => !_isOutbound && _state == TruckState.Docked && !_offloadClaimed && !_offloadComplete;

    /// <summary>True while an outbound (empty-arriving) truck is docked and still waiting for a dock
    /// stocker to start loading it with staged pallets.</summary>
    public bool AwaitingLoad => _isOutbound && _state == TruckState.Docked && !_loadClaimed && !_loadComplete;

    /// <summary>True once this truck has been spawned for outbound pickup (arrives empty, gets loaded
    /// at the dock) rather than inbound delivery (arrives full, gets offloaded).</summary>
    public bool IsOutbound => _isOutbound;

    /// <summary>Marks this truck as outbound — must be called before it reaches Docked (normally right
    /// after AssignAndGo, by whichever spawn path is used for outbound pickups).</summary>
    public void SetOutbound() => _isOutbound = true;

    /// <summary>Outbound with a specific destination door and (optionally) the dock appointment it's
    /// here for. Unlike an inbound truck — which takes whatever door frees first — an outbound pickup
    /// can only load at the door its order is staged at, so a waiting outbound truck holds out for
    /// exactly this door (see UpdateWaitingForDoor), and gives up only when its appointment is closed
    /// out.</summary>
    public void SetOutbound(int doorNumber, string appointmentId)
    {
        _isOutbound = true;
        _outboundDoorNumber = doorNumber;
        _outboundAppointmentId = appointmentId;
    }

    private int _outboundDoorNumber = -1;
    private string _outboundAppointmentId;

    /// <summary>The player moved this truck's appointment to another door before it docked: wait for that
    /// door instead. (A docked truck is never retargeted - the schedule refuses that move.)</summary>
    public void RetargetOutboundDoor(int doorNumber)
    {
        if (!_isOutbound || _dock != null) return;
        _outboundDoorNumber = doorNumber;
    }

    /// <summary>The door an outbound truck is here to load at (-1 if unknown / inbound).</summary>
    public int OutboundDoorNumber => _outboundDoorNumber;
    /// <summary>DockAppointment.Id this outbound truck was dispatched for, or null.</summary>
    public string OutboundAppointmentId => _outboundAppointmentId;

    /// <summary>True once the truck is on its way out of the yard — no longer counts as the truck
    /// serving its door.</summary>
    public bool IsLeaving => _state == TruckState.DepartToApproach || _state == TruckState.ToLeaveNoTurn ||
                             _state == TruckState.ToExit || _state == TruckState.Exiting || _state == TruckState.Idle;

    /// <summary>True while this is an outbound truck parked (or heading to park) waiting for its door.</summary>
    public bool IsOutboundWaitingForDoor => _isOutbound && _dock == null && !IsLeaving;

    /// <summary>The dock this truck is currently backed into (null unless docked).</summary>
    public DockSlot DockedAt => _state == TruckState.Docked ? _dock : null;

    /// <summary>The Load container holding this truck's cargo pallets as children, or null.</summary>
    public Transform LoadContainer => transform.Find("Trailer/LorryTrailer/Load") ?? FindDeepChild(transform, "Load");

    /// <summary>Snapshot of LoadContainer.childCount taken the instant offload is claimed — before
    /// TrailerOffloadController starts reparenting pallets out one at a time. LoadContainer's own
    /// childCount then tracks what's LEFT on the trailer, so (TotalPalletsAtDock - childCount) is how
    /// many have actually been dropped in the lane so far — used by the hover tooltip's load-progress
    /// bar so it advances the moment a pallet lands, not on some case-count proxy.</summary>
    public int TotalPalletsAtDock { get; private set; }

    /// <summary>Marks this truck as being actively offloaded so no other offloader claims it.</summary>
    public void ClaimForOffload()
    {
        _offloadClaimed = true;
        _metOffloadT = OpsMetrics.Now;
        // Keep the highest count of this visit (Tad, 2026-11-15): an offload that paused ("no room") and was claimed again re-counted
        // from what was LEFT on the trailer, so the bar read 0 off with 1 of 6 already in the lane.
        TotalPalletsAtDock = Mathf.Max(TotalPalletsAtDock, LoadContainer != null ? LoadContainer.childCount : 0);
    }

    /// <summary>Called by the offload controller once every pallet is off — lets the truck depart.</summary>
    public void CompleteOffload()
    {
        if (_metOffloadT >= 0f && !_offloadComplete)
            OpsMetrics.Record(OpsMetrics.Kind.Offload, OpsMetrics.Now - _metOffloadT, TotalPalletsAtDock);
        _metOffloadT = -1f;
        _offloadComplete = true;
    }

    /// <summary>Called by the offload controller when it has to abandon an in-progress offload (e.g.
    /// its dock stocker operator was fired/vacated mid-run) with pallets still on the trailer. Un-claims
    /// the truck so AwaitingOffload goes true again and a future manned dock stocker can pick up and
    /// finish it — without this, the truck would sit docked forever (claimed-but-never-complete is
    /// invisible to both the offload scanner AND the unclaimed-timeout fallback).</summary>
    public void ReleaseOffloadClaim()
    {
        if (!_offloadComplete) _offloadClaimed = false;
    }

    /// <summary>Marks this outbound truck as being actively loaded so no other loader claims it.</summary>
    public void ClaimForLoad() => _loadClaimed = true;

    /// <summary>True while a loader holds the claim on this truck.</summary>
    public bool LoadClaimed => _loadClaimed;

    /// <summary>Releases the "a loader is working me right now" claim, called when a load routine
    /// finishes. This flag is a MUTEX for the duration of one routine, not a permanent latch: the
    /// truck stays docked awaiting close-out and may still have cargo space, so further Load tasks
    /// for its door must be able to run against it.
    ///
    /// Without this the flag stuck on forever — AwaitingLoad went false after the first load and
    /// never came back, so TrailerLoadController's `if (!truck.AwaitingLoad) continue` skipped every
    /// later task at that door. Those tasks sat Available permanently, their orders stayed stuck in
    /// Loading, and Loading rows have no enabled checkbox in the Work Queue panel — so the player
    /// couldn't close them out OR re-release them. Only _loadComplete (set at close-out) should
    /// permanently retire a truck.</summary>
    public void ReleaseLoadClaim() => _loadClaimed = false;

    /// <summary>Called by the load controller once every staged pallet for this door is aboard —
    /// lets the truck depart.</summary>
    public void CompleteLoad() => _loadComplete = true;

    /// <summary>True once the load was closed out or released: a loader must take no more pallets.</summary>
    public bool LoadIsComplete => _loadComplete;

    /// <summary>Resets the docked idle clock (same "reset timer slightly" pattern the inbound
    /// offload fallback already uses when pallets remain but no dock stocker is free yet) so
    /// offloadFallbackTimeout doesn't force this outbound truck to depart empty while it's still
    /// productively waiting — e.g. for its lane to reach the load-start pallet threshold.</summary>
    public void KeepDockAlive() => _dockedTime = Mathf.Min(_dockedTime, offloadFallbackTimeout - 5f);

    /// <summary>Recovery hook for a truck caught holding an invalid/duplicate dock reference (e.g. a
    /// save/restore mismatch that let two trucks end up both pointed at the same door — see the
    /// RestoreFromSnapshot fix above). Deliberately does NOT call _dock.Release() — if another truck
    /// legitimately holds that DockSlot, this truck's own claim was never valid to begin with, and
    /// releasing it would wrongly free the door out from under whoever actually owns it. Just forgets
    /// the reference and re-runs the normal "no free door" decision (SideLot, then the generic wait
    /// point, then give up) from wherever it's currently sitting.</summary>
    public void RerouteAwayFromDock()
    {
        _dock = null;
        BeginDoorWait();
    }

    // ── Persistence read-only state ──────────────────────────────────────────────

    /// <summary>True if this truck is in any departure/exit state and should NOT be saved.</summary>
    public bool IsDeparting =>
        _state == TruckState.DepartToApproach ||
        _state == TruckState.ToLeaveNoTurn   ||
        _state == TruckState.ToExit          ||
        _state == TruckState.Exiting         ||
        // Idle only ever occurs mid-ShrinkAndDestroy (StartExiting/ToExit both set it right before
        // starting that coroutine) — it's always "about to be destroyed," never a resumable state.
        // A save landing in that exact window previously captured a state=Idle/door=-1/PO="" ghost
        // that got re-restored every load thereafter with nothing left to resume.
        _state == TruckState.Idle;

    /// <summary>The dock slot claimed by this truck regardless of current state (set at AssignAndGo time).</summary>
    public DockSlot AssignedDock => _dock;

    /// <summary>The driver's status/message tracker (see TruckDoorWaitBar) — WorldHoverPopupUI's truck
    /// tooltip reads CurrentMessage/CurrentColor/CurrentFillAmount off this rather than duplicating the
    /// driver-comment logic. Null until the first Show* call lazily creates it.</summary>
    public TruckDoorWaitBar DoorWaitBar => _doorWaitBar;

    /// <summary>Seconds the truck has been in the Docked state (used by offload fall-back timer).</summary>
    public float DockedTime => _dockedTime;

    /// <summary>True if a dock stocker has claimed this truck for offloading.</summary>
    public bool OffloadClaimed => _offloadClaimed;

    /// <summary>True if the dock stocker has fully finished offloading the trailer.</summary>
    public bool OffloadComplete => _offloadComplete;

    /// <summary>True if the trailer barn doors are currently open.</summary>
    public bool DoorsOpen => _doorsOpen;

    // ── Persistence movement state (2026-07-15) ──────────────────────────────────
    // Exposed so departing trucks can resume their exact path on load.
    public Vector3 CurrentTarget => _currentTarget;
    public bool    UseBezier     => _useBezier;
    public Vector3 BzP0          => _bzP0;
    public Vector3 BzP1          => _bzP1;
    public Vector3 BzP2          => _bzP2;
    public Vector3 BzP3          => _bzP3;
    public float   BzT           => _bzT;
    public float   BzArcLen      => _bzArcLen;

    [Header("Legacy Dock Positioning (fallback — DockSlot's own waypoint math was stripped 2026-09-05)")]
    [Tooltip("How far out from the wall the truck center sits when fully docked. Was DockSlot.dockOffset.")]
    [SerializeField] private float legacyDockOffset = 3.5f;
    [Tooltip("Metres to stop SHORT of the TruckNavPoint5 marker when backing into a door (positive = further from the wall). Tune if the trailer sits too far in/out.")]
    [SerializeField] private float dockBackInTrim = -0.07f;
    [Tooltip("Straight-out distance from the door into the yard for Xform 2 (_drApproach-DepartPoint). Was DockSlot.approachDepartDepth.")]
    [SerializeField] private float legacyApproachDepartDepth = 9.5f;
    [Tooltip("Xform 2 sideways offset from the door center along the wall. Was DockSlot.approachDepartSide.")]
    [SerializeField] private float legacyApproachDepartSide = 0f;

    // ── Route waypoints (world positions, injected by TruckYardManager) ──────────
private DockSlot        _dock;
    private GuardController  _guard;
    private Gate_Open_Close  _gateArm;
    private Vector3?         _gateStop;          // guard inspection
    private Vector3?         _gateEnterNoTurn;   // Xform 1
    private Vector3?         _gateLeaveNoTurn;   // Xform 5
    private Vector3?         _exitWaypoint;      // Exit point
    private System.Action    _onExited;
    // Xform 2 (_drApproach-DepartPoint) is computed per-door from DockSlot offsets —
    // see ApproachPoint().

    /// <summary>Where a truck parks to wait when every door is occupied at arrival — through the gate,
    /// turn right, big loop, ~40m from the warehouse FACING it (Tad's spec). A single hand-placed scene
    /// Transform in the yard, injected via Init() exactly like the other gate waypoints above; its own
    /// rotation is the truck's final resting facing. Null means "no wait spot configured," in which
    /// case a truck with no free door gives up immediately instead of waiting (see BeginDoorWait).</summary>
    private Transform _doorWaitPoint;

    private float _doorWaitPollTimer;
    private TruckDoorWaitBar _doorWaitBar;

    // ── Unified 8-hour inbound dwell clock (2026-09-10, Tad's ladder spec) ─────────────────────
    // Replaces the old flat "give up after doorWaitMinutes with no door" penalty, which Tad called
    // out as too harsh (2 hours). Runs from the moment the truck clears the gate — see
    // GuardClearedToEnter — independent of door/dock state, and degrades vendor standing gradually
    // instead of an all-or-nothing cutoff. See UpdateInboundDwellClock.
    private const float InboundDeadlineHours = 8f;
    private const float InboundTier1Hours = 2f; // -3, bar turns yellow
    private const float InboundTier2Hours = 4f; // -5, bar turns orange
    private const float InboundTier3Hours = 6f; // -10, bar turns red
    private const int InboundTier1Penalty = 3;
    private const int InboundTier2Penalty = 5;
    private const int InboundTier3Penalty = 10;
    private const int InboundFinalPenalty = 15;
    private long _inboundClockStartSimMinute = -1;
    private int  _inboundPenaltyTierApplied; // 0=none, 1/2/3 = that rung fired, 4 = final (8h) resolved

    // ── Offload "Standard" (Tad's naming, 2026 — first entry in what he's explicit will grow into a
    // much larger task-time-allowance system; a per-task figure like "a dock stocker gets 10 minutes"
    // isn't tracked yet, only this warehouse-level total). Clock is _dockedAtSimMinute (set in
    // OnDocked, i.e. once the driver is actually AT the door — the ~1 hour it takes to get there isn't
    // charged against this) to the sim-minute BeginDeparture runs at (i.e. the moment the last pallet
    // comes off the trailer and CompleteOffload fires) — see ApplyOffloadStandardBonus.
    private const float OffloadStandardBaseMinutes = 60f; // flat 1 hour, Tad's spec
    private const float OffloadStandardMinutesPerPallet = (15f + GameCore.Inventory.OrderPickEstimate.ExtraPalletMinutes) * 1.1f; // 15 min per pallet (Tad, 2026-10-31) + 2, then +10% (2026-11-01)
    private const int OffloadStandardBonusPoints = 5;

    /// <summary>Set true by ApplyOffloadStandardBonus when the Standard was beaten, read (and
    /// cleared) by BeginDeparture right after CloseTrailerDoors() — the world-space reward text has
    /// to wait for that exact "doors closed, pulling out" moment even though the vendor-standing
    /// change/log/driver-note all fire earlier, at CompleteOffload time.</summary>
    private bool _pendingOffloadStandardFx;

    /// <summary>Set while this truck is waiting in a SideLotController's claimed spot instead of the
    /// generic _doorWaitPoint — null the rest of the time. Drives the UpdateWaitingForDoor branch that
    /// reverses back out to the gate instead of curving straight to ApproachPoint(), and the "PARKED ·
    /// Side Lot" status shown in PurchasingPanel/SchedulerPanel via IsInSideLot below.</summary>
    private SideLotController _sideLot;
    /// <summary>The specific parking Slot (one of possibly several on _sideLot) this truck claimed —
    /// see SideLotController.Slot. Null whenever _sideLot is null.</summary>
    private SideLotController.Slot _sideLotSlot;
    public bool IsInSideLot => _sideLot != null;

    /// <summary>World position of the claimed SideLot slot's Anchor — the slot's save identity (see
    /// TruckSnapshot.sideLotAnchorPosition). False when this truck holds no slot.</summary>
    public bool TryGetSideLotAnchorPosition(out Vector3 anchorPosition)
    {
        anchorPosition = default;
        if (_sideLotSlot == null || _sideLotSlot.Anchor == null) return false;
        anchorPosition = _sideLotSlot.Anchor.position;
        return true;
    }

    private float       _groundY;
    private Vector3     _currentTarget;
    private Transform   _driverDoor;
    private Transform   _passDoor;
    private bool        _doorsOpen;
    private bool        _useBezier;

    // Cab steering (articulated tractor)
    private Transform   _cab;
    private Quaternion  _cabRest;
    private float       _cabYaw;
    private float       _prevYaw;
    private bool        _cabInit;

    // Bézier segments (forward smoothing legs — door-wait / departure curves)
    private Vector3 _bzP0, _bzP1, _bzP2, _bzP3;
    private float   _bzT;
    private float   _bzArcLen;

    public TruckState State => _state;

    // ── Gate queue ───────────────────────────────────────────────────────────────
    private bool _isFront;       // this truck holds slot 0 (the gate) and may be inspected
    private bool _clearedGate;   // guard waved it through — it's heading into the yard

    /// <summary>True once the truck has passed the guard and left the gate queue.</summary>
    public bool HasClearedGate => _clearedGate;

    /// <summary>Fired once when the guard clears this truck and it leaves the queue.</summary>
    public event System.Action OnClearedGate;

    // Docked ghosting — the see-through material and each ghosted renderer's original materials,
    // so they can be restored on departure.
    private Material _ghostMaterial;
    private VehicleThrottleAudio _throttleAudio;

    // Ghost material for cargo CASES (GhostCases()) — uses the same GhostLoweredWall as docked trailer walls
    // so unreceived cases have a consistent see-through look.
    private Material _cargoGhostMaterial;
    private readonly Dictionary<Renderer, Material[]> _originalMaterials = new();

    private void Awake()
    {
        _groundY = transform.position.y;
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        _throttleAudio = GetComponent<VehicleThrottleAudio>();

        _ghostMaterial = Resources.Load<Material>("Materials/GhostLoweredWall");
        if (_ghostMaterial == null)
            Debug.LogWarning("[TruckController] GhostLoweredWall material not found at Resources/Materials/GhostLoweredWall — docked trailer won't turn see-through.");

        _cargoGhostMaterial = Resources.Load<Material>("Materials/GhostLoweredWall");
        if (_cargoGhostMaterial == null)
            Debug.LogWarning("[TruckController] GhostLoweredWall material not found at Resources/Materials/GhostLoweredWall — unreceived cargo cases won't ghost correctly.");

        
        _driverDoor = FindDeepChild(transform, "TrailerDoor.Driver");
        if (_driverDoor == null) _driverDoor = FindDeepChild(transform, "TrailerDoor");

        _passDoor = FindDeepChild(transform, "TrailerDoor.Pass");
        if (_passDoor == null) _passDoor = FindDeepChild(transform, "TrailerDoor.001");

        // Door fit (Tad, 2026-11-20): the doors sit slightly taller (Y scale 1.12) and a hair off the trailer's rear edge. They live inside the
        // Truck_SavageDev FBX, so the values are applied here instead of through a prefab override.
        if (_driverDoor != null) { _driverDoor.localPosition = new Vector3(-1.1f, 2.413f, -5.8f); _driverDoor.localScale = new Vector3(1f, 1.12f, 1f); }
        if (_passDoor != null)   { _passDoor.localPosition   = new Vector3(1.25f, 2.371f, -5.801f); _passDoor.localScale   = new Vector3(1f, 1.12f, 1f); }

        _cab = FindDeepChild(transform, cabChildName);
        if (_cab != null) _cabRest = _cab.localRotation;
        else if (articulateCab)
            Debug.LogWarning($"[TruckController] Cab child '{cabChildName}' not found — cab steering disabled.");
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    // ── Setup ───────────────────────────────────────────────────────────────────
    public void Init(Vector3? gateStop, Vector3? gateEnterNoTurn, Vector3? gateLeaveNoTurn,
                     Vector3? exitWaypoint, GuardController guard, System.Action onExited,
                     Transform doorWaitPoint = null, Gate_Open_Close gateArm = null)
    {
        _gateStop        = gateStop;
        _gateEnterNoTurn = gateEnterNoTurn;
        _gateLeaveNoTurn = gateLeaveNoTurn;
        _exitWaypoint    = exitWaypoint;
        _guard           = guard;
        _onExited        = onExited;
        _doorWaitPoint   = doorWaitPoint;
        _gateArm         = gateArm;
    }

    /// <summary>Same entry sequence as AssignAndGo, but for a truck with NO free door at spawn time —
    /// it still queues at the gate and gets inspected like any other arrival (this IS the truck showing
    /// up on time), it just has nothing to back into yet. GuardClearedToEnter/ToEnterNoTurn route it to
    /// the door-wait park spot instead of a door once it's through the gate.</summary>
    public void AssignAndGoWaitForDoor()
    {
        _dock = null;

        Vector3 firstTarget = _gateStop ?? (_doorWaitPoint != null ? _doorWaitPoint.position : transform.position);

        Vector3 dir = firstTarget - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (_gateStop.HasValue)
        {
            _state = TruckState.Queuing;
            _currentTarget = transform.position;
            _useBezier = false;
        }
        else
            GuardClearedToEnter();
    }

    public void AssignAndGo(DockSlot dock)
    {
        if (dock == null || dock.IsOccupied)
        {
            Debug.LogWarning("[TruckController] Dock null or already occupied.");
            return;
        }
        _dock = dock;
        _dock.Claim();
        ApplyOnTimeDoorBonus();

        Vector3 firstTarget = _gateStop ?? ApproachPoint();

        // Snap facing before first Update — no visible first-frame spin.
        Vector3 dir = firstTarget - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (_gateStop.HasValue)
        {
            // Wait in the gate queue. The yard manager assigns our slot via SetQueueSlot().
            _state = TruckState.Queuing;
            _currentTarget = transform.position;
            _useBezier = false;
        }
        else
            GuardClearedToEnter(); // no gate configured — skip straight into the yard
    }

    internal const int PalletSlotCount = 12;
    internal const int PalletsPerRow = 6;

    public void LoadShipment(GameCore.Inventory.ShipmentData shipment)
    {
        AssignedShipment = shipment;
        if (shipment == null) return;

        // Find the load area container. Try the known path first, then fall back to a recursive
        // search for a child literally named "Load" — the container lives inside the nested trailer
        // prefab, so the exact intermediate path ("Trailer/LorryTrailer/…") can drift if those
        // objects are ever renamed. The pallets MUST end up childed under this Load object (it's the
        // anchor the whole cargo layout is positioned relative to, and what the dock-stocker offload
        // will later reparent from).
        var loadParent = transform.Find("Trailer/LorryTrailer/Load") ?? FindDeepChild(transform, "Load");
        if (loadParent == null)
        {
            Debug.LogWarning($"[TruckController] No 'Load' container found on {name} (searched path and recursively). Cannot populate pallets.");
            return;
        }

        if (palletVisualPrefab == null)
        {
            Debug.LogWarning($"[TruckController] Pallet Visual Prefab not assigned on {name} — cannot show cargo. Assign one in the Inspector (e.g. Assets/_Project/Prefabs/Inventory/ChepStack.prefab).");
            return;
        }

        // Clear any pallets from a previous load (in case a truck is ever reused across shipments).
        for (int i = loadParent.childCount - 1; i >= 0; i--)
            Destroy(loadParent.GetChild(i).gameObject);

        if (shipment.LineItems.Count == 0)
        {
            Debug.LogWarning($"[TruckController] PO {shipment.PONumber} has no line items — trailer stays empty.");
            return;
        }

        var inventoryService = GameCore.Services.ServiceLocator.Get<GameCore.Inventory.InventoryService>();

        // Two placement modes. If every line item carries real floor-slot/tier metadata (the
        // RandomDeliveryGenerator path — one line item per physical pallet, tagged with which of
        // the 12 floor positions it sits at and whether it's tier 0 or stacked tier 1), place each
        // pallet exactly there, supporting double-stacking. Otherwise fall back to the legacy fixed
        // 12-slot round-robin cycle (older callers that just hand over undifferentiated line items).
        bool hasSlotMetadata = shipment.LineItems.Count > 0 && shipment.LineItems[0].FloorSlotIndex >= 0;

        int built = 0;
        if (hasSlotMetadata)
        {
            for (int i = 0; i < shipment.LineItems.Count; i++)
            {
                var item = shipment.LineItems[i];

                // SHORT-SHIPPED: ordered and paid for, but the supplier didn't put it on the truck.
                // No pallet is built, so nothing physically arrives, ReceivedQuantity stays 0 and the
                // line reports its full Shortage. The line item deliberately stays on the manifest —
                // see ShipmentLineItem.Dropped for why it's a flag rather than a deletion.
                if (item.Dropped) continue;

                // Already satisfied — a BACKFILL run (ShipmentService.RequestBackfill) re-enters the
                // SAME ShipmentData/PO into the dispatch pipeline with only its short lines reopened,
                // so this truck's manifest can be a mix of already-received and newly-reopened lines.
                // Without this guard every already-delivered pallet on the PO would be built again.
                if (item.ReceivedQuantity >= item.Quantity) continue;

                var sku = inventoryService?.GetSkuData(item.SkuId);
                if (BuildOnePallet(loadParent, sku, item.SkuId, item.FloorSlotIndex, item.PalletTier, i))
                    built++;
            }
        }
        else
        {
            // One pallet per full pallet actually ORDERED (2026-10-17 playtest). This used to fill all 12 slots by
            // cycling the line items, so a 2-pallet PO arrived with 6+ pallets (free stock) and a short-shipped line
            // still turned up. Dropped and already-received lines build nothing, exactly like the metadata path.
            int slot = 0;
            for (int i = 0; i < shipment.LineItems.Count; i++)
            {
                var item = shipment.LineItems[i];
                if (item.Dropped || item.ReceivedQuantity >= item.Quantity) continue;
                var sku = inventoryService?.GetSkuData(item.SkuId);
                int perPallet = sku != null ? Mathf.Max(1, sku.Ti * sku.Hi) : Mathf.Max(1, item.Quantity);
                int pallets = Mathf.Max(1, Mathf.CeilToInt((item.Quantity - item.ReceivedQuantity) / (float)perPallet));
                for (int p = 0; p < pallets && slot < PalletSlotCount * 2; p++, slot++)
                {
                    int floorSlot = slot % PalletSlotCount, tier = slot / PalletSlotCount;
                    if (BuildOnePallet(loadParent, sku, item.SkuId, floorSlot, tier, slot))
                        built++;
                }
            }
        }

        Debug.Log($"[TruckController] PO {shipment.PONumber} loaded. {built} pallet(s) populated with cargo.");
    }

    /// <summary>Instantiates and builds one cargo pallet at the given floor slot/tier. Returns
    /// true if it got real cargo (SkuData + case prefab), false if it was left as an empty base.</summary>
    private bool BuildOnePallet(Transform loadParent, GameCore.Inventory.SkuData sku, string skuId, int floorSlot, int tier, int uniqueIndex)
    {
        var instance = Instantiate(palletVisualPrefab);
        instance.transform.SetParent(loadParent);
        instance.transform.localPosition = SlotLocalPosition(floorSlot, tier, sku);
        instance.transform.localRotation = Quaternion.identity;
        instance.name = $"Pallet_{uniqueIndex:D2}_Slot{floorSlot}_Tier{tier}";

        // palletVisualPrefab is normally a build-menu placeable item (PlacedObject/BuildingData)
        // — as cargo it must not self-register in the world registry at (0,0), same reasoning
        // PalletBuilder.Build() already applies to the individual cases it spawns below.
        //
        // IMPORTANT (2026-07-09, pallet persistence rework): PlacedObject is DISABLED, not
        // destroyed. Disabling still unregisters it from PlacedObjectRegistry via OnDisable (same
        // effect as before) but keeps the component — and critically its `data` field (the
        // ObjDataSO identity, already baked into the prefab) — alive for the whole truck ride.
        // TrailerOffloadController.RegisterAndQueue re-enables it once the pallet has a real grid
        // cell, and PalletPersistenceService reads `data.id` at save time to know which prefab to
        // re-instantiate on load. Destroying it (the old behavior) lost that identity permanently,
        // which is part of why dock pallets could never rebuild correctly after a save/load.
        var po = instance.GetComponent<PlacedObject>();
        if (po != null) po.enabled = false;
        var bd = instance.GetComponent<BuildingData>();
        if (bd != null) Destroy(bd);

        var builder = instance.GetComponentInChildren<PalletBuilder>();
        if (builder == null)
        {
            Debug.LogWarning($"[TruckController] palletVisualPrefab has no PalletBuilder — slot {floorSlot} tier {tier} shows as an empty base.");
            return false;
        }

        if (sku == null)
        {
            Debug.LogWarning($"[TruckController] Unknown SKU: {skuId} — slot {floorSlot} tier {tier} left empty.");
            return false;
        }

        // Some hand-authored SkuData assets never got a case prefab assigned (missing art — see
        // SkuData.Prefab). That used to be treated exactly like "SKU not found at all": the pallet
        // was left completely uninitialized (no PalletData), which made TrailerOffloadController's
        // RegisterAndQueue fall back to a dummy "PHYS" SKU and register 1 unit of dead-air inventory
        // in exchange for the line item's real, already-charged cost — the item vanished on arrival.
        // Now: still build real PalletData/cargo quantity below so the player actually receives what
        // they paid for; only the case-stacking VISUAL is skipped when there's no prefab to stack.
        bool hasCasePrefab = sku.Prefab != null;
        if (!hasCasePrefab)
        {
            Debug.LogWarning($"[TruckController] SKU {skuId} ({sku.ItemDescription}) has no case prefab assigned — slot {floorSlot} tier {tier} will carry real cargo with a placeholder (bare pallet) visual.");
        }
        else
        {
            builder.casePrefab = sku.Prefab;
            builder.linkedSku = sku; // CRITICAL: Link the SKU so PalletBuilder knows its real dimensions

            // Cargo pallets must reflect the SKU's real, PalletOptimizer-verified Ti/Hi (the master
            // record) — not PalletBuilder's own independent auto-layout guess — so the trailer
            // visually shows the same load the inventory/putaway systems believe is there. If a SKU
            // was never run through the optimizer (Ti/Hi still 0), fall back to PalletBuilder's own
            // height-based auto-layout rather than building an empty pallet.
            if (sku.Ti > 0 && sku.Hi > 0)
            {
                builder.useTiHiOverride = true;
                builder.manualTi = sku.Ti;
                builder.manualHi = sku.Hi;
            }
            builder.Build(deductMoney: false);

            // FIX FLOATING CASES: Right after building, reposition cases so they sit on the pallet deck,
            // not floating above it. Same fix applied in TrailerOffloadController.DropPallet() but we
            // apply it here too so cases are positioned correctly from the moment they're built in the trailer.
            var palletLoad = instance.transform.Find("PalletLoad");
            if (palletLoad != null)
            {
                const float palletDeckHeight = 0.165f;
                float minCaseY = float.MaxValue;
                var casesList = new System.Collections.Generic.List<Transform>();

                for (int i = 0; i < palletLoad.childCount; i++)
                {
                    var child = palletLoad.GetChild(i);
                    casesList.Add(child);
                    if (child.localPosition.y < minCaseY)
                        minCaseY = child.localPosition.y;
                }

                // Shift all cases down so the lowest sits at pallet deck height
                if (casesList.Count > 0 && minCaseY != float.MaxValue)
                {
                    float yOffset = minCaseY - palletDeckHeight;
                    foreach (var caseTransform in casesList)
                    {
                        var pos = caseTransform.localPosition;
                        pos.y -= yOffset;
                        caseTransform.localPosition = pos;
                    }
                }

                // FIX CASE ORIENTATION: Zero out the default 90-degree Y rotation on PalletLoad
                // so cases align properly with the pallet direction.
                palletLoad.localRotation = Quaternion.identity;
            }

            // Cargo pallets are unreceived inventory — their CASES (not the pallet base) must read
            // as "ghosted" the moment they're built and stay that way through the dock-stocker
            // offload. PalletBuilder.GhostCases saves each pallet's original case material so
            // ReceiverReceivingWorkflow can restore it once a Receiver actually processes the pallet.
            if (_cargoGhostMaterial != null)
                builder.GhostCases(_cargoGhostMaterial);
        }

        // CRITICAL FIX (2026-07-05): Each cargo pallet needs its own PalletData component with the
        // correct SKU so the hover tooltip shows the right item. Without this, all pallets resolve
        // to the same parent PalletData and show "stuck" on one item (e.g., "soy sauce" forever).
        var palletData = instance.GetComponent<GameCore.Inventory.PalletData>();
        if (palletData == null)
            palletData = instance.AddComponent<GameCore.Inventory.PalletData>();

        // Initialize PalletData with the cargo SKU info. LoadId is left empty — it will be assigned
        // when the pallet is actually received by a Receiver. CaseQuantity estimated from Ti x Hi.
        int estimatedCases = (sku.Ti > 0 && sku.Hi > 0) ? (sku.Ti * sku.Hi) : 0;
        var gameCtx = Ctx;
        int currentDay = gameCtx != null ? gameCtx.TimeService.Day : 0;
        int expirationDay = sku.ShelfLifeDays >= 0 ? currentDay + sku.ShelfLifeDays : -1;
        palletData.Initialize(
            loadId: "",  // Will be assigned during receiving
            itemNumber: skuId,
            caseQuantity: estimatedCases,
            expirationDay: expirationDay,
            area: sku.StorageArea,
            iconSprite: sku.Icon,
            location: Vector2Int.zero  // Will be set when pallet is actually placed in warehouse
        );

        // Add a trigger BoxCollider that encapsulates the entire pallet (pallet base + all cases).
        // Sized to match pallet dimensions: 48" (1.2192m) × 40" (1.016m) × dynamic height.
        // Height = pallet base (0.16m) + stacked cases (caseHeight × Hi) + small top padding.
        var collider = instance.AddComponent<BoxCollider>();
        float palletHeight = 0.16f + (sku.CaseHeight * sku.Hi) + 0.05f;  // +0.05m padding
        collider.size = new Vector3(1.2192f, palletHeight, 1.016f);  // (W, H, L)
        collider.center = new Vector3(0, palletHeight / 2f, 0);  // Center vertically on the pallet
        collider.isTrigger = true;  // Non-physics trigger for raycasts and collision detection

        // Add a kinematic Rigidbody for simple stacking physics.
        // Kinematic means: affected by gravity (pallets rest on each other naturally),
        // but not active physics simulation (no forces applied, cheap to run).
        // Can be switched to dynamic (isKinematic = false) later for full physics interaction.
        var rb = instance.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;  // Kinematic ignores gravity, but pallets will still rest on each other via collider stacking
        rb.constraints = RigidbodyConstraints.FreezeRotation;  // Prevent unwanted rotation

        return true;
    }

    /// <summary>2 rows of 6, equidistant along the trailer's length, centered on the Load anchor —
    /// left row at -palletLateralOffset, right row at +palletLateralOffset. Tier 1 (a pallet
    /// double-stacked on top of tier 0 at the same floor slot) sits at Y = sku.PltHeight, since
    /// tier 0 and tier 1 at a given slot are always the same SKU (RandomDeliveryGenerator never
    /// mixes SKUs within one stack) — tier 0's own total pallet height IS that offset.</summary>
    internal Vector3 SlotLocalPosition(int slotIndex, int tier, GameCore.Inventory.SkuData sku)
    {
        int row = slotIndex / PalletsPerRow;   // 0 = left, 1 = right
        int col = slotIndex % PalletsPerRow;   // 0..5 along the trailer length
        float x = row == 0 ? -palletLateralOffset : palletLateralOffset;
        float z = (col - (PalletsPerRow - 1) / 2f) * palletRowSpacing;

        // CRITICAL FIX: Calculate total loaded pallet height (pallet + cases), not just pallet deck height.
        // Tier 0 sits at Y=0. Tier 1+ is positioned at (tier * total_pallet_height).
        float y = 0f;
        if (tier > 0 && sku != null)
        {
            // Total pallet height = pallet deck (0.16m) + cases stacked on top
            // Cases height = Hi (layers) * (caseHeight + verticalGap) with one less gap
            const float palletDeckHeight = 0.16f;
            const float verticalGapBetweenLayers = 0.025f;

            // Case height comes from the SKU's case prefab dimensions
            float caseHeight = sku.CaseHeight;
            int numLayers = sku.Hi > 0 ? sku.Hi : 1;  // Default to 1 layer if not set

            // Calculate total case stack height: each layer is caseHeight tall, gaps between them
            float casesStackHeight = (numLayers * caseHeight) + ((numLayers - 1) * verticalGapBetweenLayers);

            // Total height = pallet + cases + small buffer to prevent clipping when stacking
            const float stackingBuffer = 0.01f;  // 1cm buffer between stacked pallets
            float totalLoadedHeight = palletDeckHeight + casesStackHeight + stackingBuffer;
            y = tier * totalLoadedHeight;
        }

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Manager-driven gate-queue slot. <paramref name="isFront"/> = this truck holds the
    /// gate (slot 0) and will be inspected once it arrives. Ignored once past the gate.
    /// </summary>
    public void SetQueueSlot(Vector3 slotPos, bool isFront)
    {
        if (_state != TruckState.Queuing) return;
        _currentTarget = slotPos;
        _isFront = isFront;
        _useBezier = false;
    }

    public void ForceDeparture()
    {
        if (_state == TruckState.Docked) BeginDeparture();
    }

    // ── Persistence API ───────────────────────────────────────────────────────────

    /// <summary>Assigns the shipment reference without spawning cargo — used by
    /// TruckPersistenceService to re-link a restored truck to its PO.</summary>
    public void SetShipment(GameCore.Inventory.ShipmentData shipment)
    {
        AssignedShipment = shipment;
    }

    /// <summary>
    /// Captures every pallet currently under the Load container into snapshots, recording
    /// the SKU, slot/tier position, and exact case transforms.  Called by
    /// TruckPersistenceService before the game is saved.
    /// </summary>
    public List<TrailerPalletSnapshot> CaptureTrailerPallets()
    {
        var result = new List<TrailerPalletSnapshot>();
        var loadParent = LoadContainer;
        if (loadParent == null) return result;

        for (int i = 0; i < loadParent.childCount; i++)
        {
            var pallet  = loadParent.GetChild(i);
            var snap    = new TrailerPalletSnapshot();

            ParseSlotAndTierFromName(pallet.name, out snap.floorSlot, out snap.palletTier);

            var pd      = pallet.GetComponent<GameCore.Inventory.PalletData>();
            var builder = pallet.GetComponentInChildren<PalletBuilder>();
            snap.skuId  = (pd != null && !string.IsNullOrEmpty(pd.ItemNumber)) ? pd.ItemNumber
                        : (builder?.linkedSku != null ? builder.linkedSku.SkuId : "");

            // Capture fallback SkuData fields for robust recovery
            if (builder != null && builder.linkedSku != null)
            {
                var sku = builder.linkedSku;
                snap.itemDescription = sku.ItemDescription;
                snap.caseLength = sku.CaseLength;
                snap.caseWidth = sku.CaseWidth;
                snap.caseHeight = sku.CaseHeight;
                snap.caseWeight = sku.CaseWeight;
                snap.buyValue = sku.BuyValue;
                snap.sellValue = sku.SellValue;
                snap.storageArea = (int)sku.StorageArea;
                snap.shelfLifeDays = sku.ShelfLifeDays;
            }

            if (builder != null)
            {
                snap.capturedTi = builder.manualTi;
                snap.capturedHi = builder.manualHi;
            }

            var palletLoad = pallet.Find("PalletLoad");
            if (palletLoad != null)
            {
                for (int j = 0; j < palletLoad.childCount; j++)
                {
                    snap.caseLocalPositions.Add(palletLoad.GetChild(j).localPosition);
                    snap.caseLocalRotations.Add(palletLoad.GetChild(j).localRotation);
                }
            }

            // Outbound cargo: keep the order tag, each case's SKU and the exact pose (see TrailerPalletSnapshot).
            var outbound = pallet.GetComponent<OutboundPalletBuilder>();
            if (outbound != null && !string.IsNullOrEmpty(outbound.OrderId))
            {
                snap.outboundOrderId = outbound.OrderId;
                snap.hasLocalPose = true;
                snap.localPosition = pallet.localPosition;
                snap.localRotation = pallet.localRotation;
                int n = snap.caseLocalPositions.Count;
                for (int j = 0; j < n; j++)
                    snap.caseSkus.Add(outbound.CaseSkus.Count == n ? outbound.CaseSkus[j] : snap.skuId);
            }

            result.Add(snap);
        }
        return result;
    }

    /// <summary>Rebuilds one OUTBOUND pallet that was already in this trailer at save time: exact pose, every case with its
    /// own SKU's prefab, and the order tag at stage Loaded (lifecycle SHIPPED), so the loader and billing see it again.</summary>
    private void RestoreOutboundTrailerPallet(TrailerPalletSnapshot snap, Transform loadParent, int index,
                                              GameCore.Inventory.InventoryService inv)
    {
        var instance = Instantiate(palletVisualPrefab);
        instance.transform.SetParent(loadParent, false);
        instance.transform.localPosition = snap.localPosition;
        instance.transform.localRotation = snap.localRotation;
        instance.name = $"OutboundPallet_{snap.outboundOrderId}_{index + 1}";

        var po = instance.GetComponent<PlacedObject>();
        if (po != null) { po.enabled = false; Destroy(po); }
        var bd = instance.GetComponent<BuildingData>();
        if (bd != null) Destroy(bd);
        var pb = instance.GetComponent<PalletBuilder>();
        if (pb != null) DestroyImmediate(pb);
        var stale = instance.transform.Find("PalletLoad");
        if (stale != null) DestroyImmediate(stale.gameObject);

        var outbound = instance.AddComponent<OutboundPalletBuilder>();
        outbound.OrderId = snap.outboundOrderId;
        int count = Mathf.Min(snap.caseLocalPositions.Count, snap.caseLocalRotations.Count);
        for (int j = 0; j < count; j++)
        {
            string skuId = j < snap.caseSkus.Count && !string.IsNullOrEmpty(snap.caseSkus[j]) ? snap.caseSkus[j] : snap.skuId;
            var sku = inv?.GetSkuData(skuId);
            Vector3 dim = sku != null ? new Vector3(sku.CaseWidth, sku.CaseHeight, sku.CaseLength) : new Vector3(0.4f, 0.3f, 0.3f);
            outbound.RestoreCase(skuId, sku?.Prefab, dim, sku != null ? Mathf.Max(1, sku.Ti) : 1,
                                 sku != null ? Mathf.Max(1, sku.Hi) : 1, snap.caseLocalPositions[j], snap.caseLocalRotations[j]);
        }
        outbound.Stage = OutboundPalletBuilder.PalletStage.Loaded;
        outbound.SetNavObstacleActive(false);
    }

    /// <summary>
    /// Restores this truck from a save snapshot. Must be called AFTER <see cref="Init"/> has
    /// been called (so waypoints are wired up) and after the dock slot has been found.
    /// Sets world transform, claims the dock, rebuilds trailer cargo, applies all docked
    /// visual side-effects, then sets the state machine to the saved state.
    /// </summary>
    public void RestoreFromSnapshot(TruckSnapshot snap, DockSlot dock)
    {
        var restoredState = (TruckState)snap.truckState;
        Debug.Log($"[TruckController.RestoreFromSnapshot] START - state={restoredState}, doorsOpen={snap.doorsOpen}, offloadClaimed={snap.offloadClaimed}, offloadComplete={snap.offloadComplete}");

        // ── SideLot slot (BUG FIX 2026-09-23: "trucks dog-piling in the same lane") ──
        // Slot occupancy lives only in memory on SideLotController, so before this every truck
        // restored into the lot came back owning no slot — every slot read free, and the next truck
        // to need one was handed slot #1 and drove straight into whoever was already parked there.
        // Done before the dock logic below so its BeginDoorWait() fallback reuses the reclaimed slot
        // instead of grabbing (possibly) someone else's.
        bool lotSlotConflict = false;
        if (IsSideLotState(restoredState))
            lotSlotConflict = !TryReclaimSideLotSlot(snap);

        // ── Dock assignment ────────────────────────────────────────────────────────
        if (dock != null && !dock.IsOccupied)
        {
            _dock = dock;
            _dock.Claim();
        }
        else if (snap.assignedDoorNumber > 0 &&
                 restoredState != TruckState.Idle && restoredState != TruckState.Queuing &&
                 restoredState != TruckState.GuardCheck)
        {
            // BUG FIX: the saved door either no longer exists or was already claimed by another
            // truck that restored first (e.g. a save written mid-inconsistency, or two trucks
            // recorded against the same door). Silently leaving _dock null here while resuming a
            // dock-dependent state (ToBackup/Docked/any backing leg) is what produced a truck
            // sitting dead in the yard forever — DockPositionFor(_dock)/ApproachPoint() etc. all
            // NRE on a null dock, throwing every frame in Update() with the state never advancing.
            // Don't resume the saved state at all in that case: re-run the exact same "no free
            // door" decision a live truck makes instead (SideLot, then the generic wait point,
            // then give up) from wherever it was left sitting.
            Debug.LogWarning($"[TruckController.RestoreFromSnapshot] {name}: saved door " +
                $"{snap.assignedDoorNumber} unavailable on restore (state was {restoredState}) — " +
                "routing to BeginDoorWait() instead of resuming a dock-dependent state with no dock.");
            _groundY = snap.worldPosition.y;
            transform.SetPositionAndRotation(snap.worldPosition, snap.worldRotation);
            BeginDoorWait();
            return;
        }

        // ── Transform ─────────────────────────────────────────────────────────────
        _groundY = snap.worldPosition.y;
        transform.SetPositionAndRotation(snap.worldPosition, snap.worldRotation);

        // Its lot slot already belongs to a truck restored ahead of it (only possible with an older
        // save that predates slot persistence) — don't resume parking on top of that truck; route to
        // a genuinely free slot from here. A truck already holding a door is leaving the lot anyway.
        if (lotSlotConflict && _dock == null)
        {
            Debug.LogWarning($"[TruckController.RestoreFromSnapshot] {name}: its SideLot slot is already taken — re-routing to a free slot.");
            BeginDoorWait();
            return;
        }

        // ── Offload flags ──────────────────────────────────────────────────────────
        // Reset docked time to 0 if the truck was previously claimed or if it's currently 
        // docked. This gives the offload controllers time to re-scan and re-claim the 
        // truck after a load, rather than immediately hitting the offloadFallbackTimeout.
        _dockedTime = 0f;
        
        // Never trust a saved "claimed" flag — the dock stocker coroutine that was driving the
        // offload does not survive a save/reload, and any pallet it was carrying got folded back
        // into this snapshot's trailerPallets specifically so the offload can restart cleanly.
        // Leaving this true would wedge the truck forever: TrailerOffloadController only scans for
        // AwaitingOffload trucks (Docked && !_offloadClaimed), and the fallback departure timer is
        // also gated on !_offloadClaimed — so a stuck "claimed" truck never gets un-stuck.
        _offloadClaimed  = false;
        Debug.Log($"[TruckController.RestoreFromSnapshot] Reset offloadClaimed: false (was {snap.offloadClaimed}) and reset dockedTime: 0 (was {snap.dockedTime})");
        
        // Also reset offloadComplete so the truck can resume offloading on load. If offloading was
        // already complete and the truck was waiting to depart, it will transition to Docked and wait
        // for the fallback timeout or a normal departure trigger, which is correct behavior.
        _offloadComplete = false;
        Debug.Log($"[TruckController.RestoreFromSnapshot] Reset offloadComplete: false (was {snap.offloadComplete})");

        // ── Restore movement state (2026-07-15) ──────────────────────────────────
        // Captured from departing or mid-maneuver trucks so they resume exactly 
        // where they left off.
        _currentTarget = snap.currentTarget;
        _useBezier     = snap.useBezier;
        _bzP0          = snap.bzP0;
        _bzP1          = snap.bzP1;
        _bzP2          = snap.bzP2;
        _bzP3          = snap.bzP3;
        _bzT           = snap.bzT;
        _bzArcLen      = snap.bzArcLen;

        // ── Rebuild trailer cargo ──────────────────────────────────────────────────
        if (snap.trailerPallets != null && snap.trailerPallets.Count > 0)
            RestoreTrailerPallets(snap.trailerPallets);

        // ── Per-state path re-initialisation ─────────────────────────────────────
        // Some states need path data that was computed on first entry (Bezier params,
        // straight-reverse start/target). Re-derive it from the restored transform so
        // the Update loop doesn't consume zero-initialised vectors.
        switch (restoredState)
        {
            case TruckState.Queuing:
                // The yard manager will call SetQueueSlot after this returns.
                _currentTarget = snap.worldPosition;
                _useBezier     = false;
                // Force clearedGate to false on restore so it follows the queue logic
                _clearedGate   = false;
                break;

            case TruckState.GuardCheck:
                // Guard state is transient and isn't worth re-entering on restore.
                // Treat it as already cleared — advance straight into the yard.
                GuardClearedToEnter();
                return;   // state already changed by GuardClearedToEnter

            case TruckState.ToEnterNoTurn:
                _currentTarget = _gateEnterNoTurn ?? ApproachPoint();
                _useBezier     = false;
                break;

            case TruckState.ToApproach:
                _currentTarget = ApproachPoint();
                _useBezier     = false;
                break;

            case TruckState.ToBackup:
                // Re-derive the straight-line target toward the door itself.
                _currentTarget = _dock != null ? DockPositionFor(_dock) : snap.worldPosition;
                _useBezier     = false;
                break;

            case TruckState.WaitingAtBackup:
            case TruckState.Reversing:
                // Both retired by the 2026-09 straight-drive-then-flip rewrite — a save frozen
                // mid-old-maneuver just resumes as the new simple "drive to the door" leg instead
                // of re-deriving the retired backup waypoint.
                restoredState  = TruckState.ToBackup;
                _currentTarget = _dock != null ? DockPositionFor(_dock) : snap.worldPosition;
                _useBezier     = false;
                break;

            case TruckState.BackInToSwingStart:
                // Re-plan from where it stands (the path is not persisted).
                if (_dock != null) { BeginBackIn(); return; }
                BeginDoorWait();
                return;

            case TruckState.BackInArc:
            case TruckState.BackInStraight:
                // Saved mid-reverse, right in front of its door: dock it.
                if (_dock != null)
                {
                    transform.SetPositionAndRotation(DockedPosition(), DockRotationFor(_dock));
                    restoredState = TruckState.Docked;
                    ApplyDockedSideEffects();
                    break;
                }
                BeginDoorWait();
                return;

            case TruckState.ReversingToDock:
                // Snap to the dock — the truck was almost there anyway.
                if (_dock != null)
                {
                    transform.SetPositionAndRotation(DockPositionFor(_dock), DockRotationFor(_dock));
                    _groundY = DockPositionFor(_dock).y;
                }
                restoredState = TruckState.Docked;
                ApplyDockedSideEffects();
                break;

            case TruckState.Docked:
                ApplyDockedSideEffects();
                break;

            case TruckState.DepartToApproach:
            case TruckState.ToLeaveNoTurn:
            case TruckState.ToExit:
                // Resumes driving toward the currentTarget (or following the restored Bezier).
                break;

            case TruckState.Exiting:
            case TruckState.Idle:
                // If saved while already exiting/shrinking, just finish the cleanup.
                restoredState = TruckState.Idle;
                StartCoroutine(ShrinkAndDestroy(exitShrinkTime));
                break;

            case TruckState.ToDoorWait:
                // Bezier params were already restored generically above — resumes driving toward the
                // wait spot exactly like DepartToApproach/ToLeaveNoTurn/ToExit do.
                break;

            case TruckState.DevCheckpoint_GateEnterNoTurn: case TruckState.FacingTruckNavPoint0: case TruckState.ToTruckNavPoint0:
            case TruckState.FacingTruckNavPoint1: case TruckState.DevCheckpoint_FacingNav1: case TruckState.ToTruckNavPoint1:
            case TruckState.DevCheckpoint_AtNav1: case TruckState.ToTruckNavPoint2: case TruckState.DevCheckpoint_AtNav2:
            case TruckState.FacingTruckNavPoint3: case TruckState.DevCheckpoint_TractorFacingNav3: case TruckState.ToTruckNavPoint3:
            case TruckState.FacingTruckNavPoint4: case TruckState.DevCheckpoint_TractorFacingNav4: case TruckState.ToTruckNavPoint4:
            case TruckState.StraighteningAtNavPoint4: case TruckState.DevCheckpoint_StraightAtNav4: case TruckState.ReversingStraightSlow:
            case TruckState.DevCheckpoint_ReversedSlow: case TruckState.ReversingToTruckNavPoint5: case TruckState.StraighteningAtNavPoint5:
            case TruckState.DevCheckpoint_AtNavPoint5: case TruckState.ReversingToTruckNavPoint3: case TruckState.DevCheckpoint_AtNavPoint3Reverse:
            case TruckState.ReversingArcAroundPivot1: case TruckState.StraighteningIntoDoor: case TruckState.StraighteningAtNavPoint2:
            case TruckState.TrailerAligningToNavPoint1: case TruckState.TrailerAligningToNavPoint4:
                // Retired backing chain (2026-11-14): a truck saved anywhere in it was on its way into its door — dock it.
                if (_dock != null)
                {
                    transform.SetPositionAndRotation(DockedPosition(), DockRotationFor(_dock));
                    restoredState = TruckState.Docked;
                    ApplyDockedSideEffects();
                    break;
                }
                BeginDoorWait();
                return;

            case TruckState.WaitingForDoor:
                // _inboundClockStartSimMinute is not persisted — UpdateInboundDwellClock lazily
                // re-initializes it to "now" on the next tick, so a restored truck gets a fresh full
                // 8-hour window rather than resuming a stale one. Forgiving default, same reasoning as
                // every other "field didn't exist in this save" fallback in this codebase — costs the
                // player a few extra hours of grace at worst, never a stuck truck.
                transform.position = snap.worldPosition;
                BeginWaitingForDoor();
                return;
        }

        _state = restoredState;
        Debug.Log($"[TruckController.RestoreFromSnapshot] END - state={restoredState} door={snap.assignedDoorNumber} pallets={snap.trailerPallets?.Count ?? 0}");
        Debug.Log($"[TruckController.RestoreFromSnapshot] Final door state: _doorsOpen={_doorsOpen}, AwaitingOffload={AwaitingOffload}");
    }

    private static bool IsSideLotState(TruckState s) =>
        s == TruckState.ToSideLot            || s == TruckState.ToSideLotEntry ||
        s == TruckState.FacingSideLotAnchor  || s == TruckState.ToSideLotAnchor ||
        s == TruckState.WaitingForDoor       || s == TruckState.ReversingToSideLotEntry ||
        s == TruckState.ReversingOutOfSideLot;

    /// <summary>Re-claims the SideLot slot a restored truck held at save time. Returns false only when
    /// the slot exists but another truck already holds it; true when claimed, or when this truck
    /// simply wasn't in the lot (e.g. WaitingForDoor at the generic DoorWaitPoint).</summary>
    private bool TryReclaimSideLotSlot(TruckSnapshot snap)
    {
        SideLotController lot;
        SideLotController.Slot slot;
        if (snap.hasSideLotSlot)
        {
            slot = SideLotController.FindSlotNear(snap.sideLotAnchorPosition, 0.5f, out lot);
        }
        else
        {
            // Older save with no slot recorded: infer it — the leg's target (Entry/Anchor) first,
            // then the truck's own parked position.
            slot = SideLotController.FindSlotNear(snap.currentTarget, 1f, out lot)
                ?? SideLotController.FindSlotNear(snap.worldPosition, 3f, out lot);
        }

        if (slot == null) return true;
        if (slot.IsOccupied && slot.Occupant != this) return false;

        slot.Claim(this);
        _sideLot = lot;
        _sideLotSlot = slot;
        return true;
    }

    // Applies all visual/controller side-effects that OnDocked() normally sets up.
    // Called from RestoreFromSnapshot when restoring any Docked-equivalent state.
    private void ApplyDockedSideEffects()
    {
        OpenTrailerDoors();
        Debug.Log($"[TruckController.ApplyDockedSideEffects] Doors opened: _doorsOpen={_doorsOpen}");
        SetDockedGhost(true);
        _dock?.LightController?.SetOccupied(true);
        _dock?.SetDoorForcedOpen(true);
        _dockedTime = 0f;
    }

    /// <summary>
    /// Rebuilds the trailer's Load container from saved pallet snapshots.
    /// Inlines pallet construction (rather than delegating to BuildOnePallet) so we
    /// hold a direct reference to each new instance and can replace its PalletLoad with
    /// the exact saved case transforms without relying on child-index heuristics.
    /// </summary>
    private void RestoreTrailerPallets(List<TrailerPalletSnapshot> palletSnaps)
    {
        if (palletVisualPrefab == null)
        {
            Debug.LogWarning($"[TruckController] Cannot restore trailer pallets on '{name}' — palletVisualPrefab not assigned.");
            return;
        }

        // FIX: Explicitly find via the known path first to avoid finding stale/duplicate Load containers
        var loadParent = transform.Find("Trailer/LorryTrailer/Load");
        if (loadParent == null)
            loadParent = FindDeepChild(transform, "Load");

        if (loadParent == null)
        {
            Debug.LogWarning($"[TruckController] Cannot restore trailer pallets on '{name}' — Load container not found.");
            return;
        }

        // FIX: Clean up ALL Load containers in the trailer hierarchy to prevent orphaned duplicates.
        // This handles cases where nested prefabs or prefab misalignment left multiple Load objects.
        var allTrailers = transform.Find("Trailer");
        if (allTrailers != null)
        {
            foreach (Transform child in allTrailers)
            {
                if (child != null && child.name == "LorryTrailer")
                {
                    var allLoads = child.GetComponentsInChildren<Transform>();
                    foreach (var load in allLoads)
                    {
                        if (load != null && load.name == "Load" && load != loadParent)
                        {
                            Debug.LogWarning($"[TruckController] Found duplicate Load container — destroying it to prevent empty trailers.");
                            DestroyImmediate(load.gameObject);
                        }
                    }
                }
            }
        }

        // DestroyImmediate here so the children list is empty before we start adding new
        // ones — using Destroy (deferred) could leave old children in the hierarchy during
        // this method's execution, making child-count indexing unreliable.
        for (int i = loadParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(loadParent.GetChild(i).gameObject);

        Debug.Log($"[TruckController] Cleared Load container '{loadParent.name}' on {name}. Preparing to restore {palletSnaps.Count} pallet(s).");

        var inventoryService = GameCore.Services.ServiceLocator.Get<GameCore.Inventory.InventoryService>();
        var gameCtx = Ctx;
        int currentDay = gameCtx != null ? gameCtx.TimeService.Day : 0;

        for (int i = 0; i < palletSnaps.Count; i++)
        {
            var palletSnap = palletSnaps[i];
            if (palletSnap == null) continue;

            if (!string.IsNullOrEmpty(palletSnap.outboundOrderId) && palletSnap.hasLocalPose)
            {
                RestoreOutboundTrailerPallet(palletSnap, loadParent, i, inventoryService);
                continue;
            }

            var sku = inventoryService?.GetSkuData(palletSnap.skuId);

            // ── Resolve Case Prefab ──────────────────────────────────────────
            // If SKU is missing or a "PHYS" dummy, fall back to Resources or the snapshot's embedded fields
            GameObject casePrefab = sku?.Prefab;
            if (casePrefab == null && !string.IsNullOrEmpty(palletSnap.skuId))
            {
                casePrefab = Resources.Load<GameObject>($"Inventory/Prefabs/Cases/Case_{palletSnap.skuId}");
            }

            // ── Instantiate pallet root ──────────────────────────────────────
            var instance = Instantiate(palletVisualPrefab);
            instance.transform.SetParent(loadParent);
            instance.transform.localPosition = SlotLocalPosition(palletSnap.floorSlot, palletSnap.palletTier, sku);
            instance.transform.localRotation = Quaternion.identity;
            instance.name = $"Pallet_{i:D2}_Slot{palletSnap.floorSlot}_Tier{palletSnap.palletTier}";

            // Cargo pallets must not register in the build-menu grid (same as BuildOnePallet).
            var po = instance.GetComponent<PlacedObject>();
            if (po != null) po.enabled = false;
            var bd = instance.GetComponent<BuildingData>();
            if (bd != null) Destroy(bd);

            // ── Rebuild PalletLoad ───────────────────────────────────────────
            var builder = instance.GetComponentInChildren<PalletBuilder>();

            bool hasSavedCases = palletSnap.caseLocalPositions != null &&
                                  palletSnap.caseLocalPositions.Count > 0 &&
                                  casePrefab != null;

            if (hasSavedCases)
            {
                // Remove any pre-existing PalletLoad (from the prefab's baked state or
                // an auto-build triggered by PalletBuilder.Start).
                var existingLoad = instance.transform.Find("PalletLoad");
                if (existingLoad != null) DestroyImmediate(existingLoad.gameObject);

                var loadObj = new GameObject("PalletLoad");
                loadObj.transform.SetParent(instance.transform, false);
                loadObj.transform.localPosition = Vector3.zero;
                loadObj.transform.localRotation = Quaternion.identity;

                int caseCount = Mathf.Min(palletSnap.caseLocalPositions.Count,
                                          palletSnap.caseLocalRotations.Count);
                for (int j = 0; j < caseCount; j++)
                {
                    var caseGO = Instantiate(casePrefab, loadObj.transform);
                    caseGO.transform.localPosition = palletSnap.caseLocalPositions[j];
                    caseGO.transform.localRotation = palletSnap.caseLocalRotations[j];

                    var casePo = caseGO.GetComponent<PlacedObject>();
                    if (casePo != null) { casePo.enabled = false; Destroy(casePo); }
                    var caseBd = caseGO.GetComponent<BuildingData>();
                    if (caseBd != null) Destroy(caseBd);
                }

                if (builder != null)
                {
                    builder.casePrefab = casePrefab;
                    builder.linkedSku  = sku;
                    if (palletSnap.capturedTi > 0 && palletSnap.capturedHi > 0)
                    {
                        builder.useTiHiOverride = true;
                        builder.manualTi = palletSnap.capturedTi;
                        builder.manualHi = palletSnap.capturedHi;
                    }
                }
            }
            else if (builder != null && sku != null)
            {
                // No exact case snapshots — fall back to a fresh Ti/Hi build.
                builder.casePrefab = sku.Prefab;
                builder.linkedSku  = sku;
                if ((palletSnap.capturedTi > 0 && palletSnap.capturedHi > 0) ||
                    (sku.Ti > 0 && sku.Hi > 0))
                {
                    builder.useTiHiOverride = true;
                    builder.manualTi = palletSnap.capturedTi > 0 ? palletSnap.capturedTi : sku.Ti;
                    builder.manualHi = palletSnap.capturedHi > 0 ? palletSnap.capturedHi : sku.Hi;
                }
                builder.Build(deductMoney: false);
            }

            // Ghost all cases — this is cargo on the trailer, not yet received.
            if (_cargoGhostMaterial != null && builder != null)
                builder.GhostCases(_cargoGhostMaterial);

            // ── PalletData component ─────────────────────────────────────────
            var palletData = instance.GetComponent<GameCore.Inventory.PalletData>();
            if (palletData == null)
                palletData = instance.AddComponent<GameCore.Inventory.PalletData>();

            int estimatedCases = (sku != null && sku.Ti > 0 && sku.Hi > 0) ? (sku.Ti * sku.Hi) : 0;
            int expirationDay  = sku != null && sku.ShelfLifeDays >= 0
                                 ? currentDay + sku.ShelfLifeDays : -1;
            palletData.Initialize(
                loadId:        "",
                itemNumber:    palletSnap.skuId,
                caseQuantity:  estimatedCases,
                expirationDay: expirationDay,
                area:          sku?.StorageArea ?? GameCore.Inventory.PalletData.AreaCategory.Grocery,
                iconSprite:    sku?.Icon,
                location:      Vector2Int.zero
            );

            // ── Physics components ───────────────────────────────────────────
            var col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            float palletHeight = sku != null ? 0.16f + (sku.CaseHeight * sku.Hi) + 0.05f : 0.5f;
            col.size   = new Vector3(1.2192f, palletHeight, 1.016f);
            col.center = new Vector3(0f, palletHeight / 2f, 0f);
            col.isTrigger = true;

            var rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        Debug.Log($"[TruckController] Restored {palletSnaps.Count} trailer pallet(s) on '{name}'.");
    }

    // ── Shared name-parsing helper ────────────────────────────────────────────────
    private static void ParseSlotAndTierFromName(string name, out int slot, out int tier)
    {
        slot = 0; tier = 0;
        if (string.IsNullOrEmpty(name)) return;
        var parts = name.Split('_');
        foreach (var p in parts)
        {
            if (p.StartsWith("Slot", System.StringComparison.OrdinalIgnoreCase)
                && int.TryParse(p.Substring(4), out int s)) slot = s;
            if (p.StartsWith("Tier", System.StringComparison.OrdinalIgnoreCase)
                && int.TryParse(p.Substring(4), out int t)) tier = t;
        }
    }

    // ── Route points (computed per-door from DockSlot offsets) ───────────────────
    // Null-guarded like every other _dock read in this file (see BeginDeparture's comment) — an
    // orphaned restore (dock lookup failed) leaves _dock null, and this fell back to the truck's own
    // position instead of throwing every frame in Update() forever.
    private Vector3 ApproachPoint() => _dock != null ? ApproachDepartPointFor(_dock) : transform.position;  // Xform 2

    /// <summary>Resolves a named waypoint under the assigned dock's "BackInSystem" child. Only "TruckNavPoint5" (the docked
    /// spot) is still read. Null if there's no dock assigned or the door doesn't have that child.</summary>
    private Transform FindDockWaypoint(string name)
    {
        if (_dock == null) return null;
        var backIn = _dock.transform.Find("BackInSystem");
        return backIn != null ? backIn.Find(name) : null;
    }

    // Restored 2026-09-05 as local fallbacks after DockSlot's own waypoint math was stripped —
    // same formulas that used to live on DockSlot, just reading legacyDock* constants above
    // instead of per-door serialized fields.
    private Vector3 DockYardForward(DockSlot dock) => dock.FlipYardSide ? -dock.transform.forward : dock.transform.forward;
    private Vector3 DockYardRight(DockSlot dock)   => dock.FlipYardSide ? -dock.transform.right   : dock.transform.right;
    private Vector3 DockPositionFor(DockSlot dock) => dock.transform.position + DockYardForward(dock) * legacyDockOffset;
    private Quaternion DockRotationFor(DockSlot dock) => dock.FlipYardSide
        ? dock.transform.rotation * Quaternion.Euler(0f, 180f, 0f)
        : dock.transform.rotation;
    private Vector3 ApproachDepartPointFor(DockSlot dock) => dock.transform.position
        + DockYardForward(dock) * legacyApproachDepartDepth
        + DockYardRight(dock)   * legacyApproachDepartSide;

    // ── Main loop ────────────────────────────────────────────────────────────────
    // FindAnyObjectByType scans every object in the scene (tens of thousands here): ~6 ms per call. The dwell clocks
    // ran it every frame for every truck in the yard, which was 37 of a 100 ms frame (2026-10-23 profile). Cached; the
    // Unity null check re-finds it after a scene reload.
    private static GameContext _ctxCache;
    private static GameContext Ctx => _ctxCache != null ? _ctxCache : (_ctxCache = FindAnyObjectByType<GameContext>());

    // TEMP-PROFILE (2026-10-23): attribute TruckController.Update's cost; remove once the hot spot is fixed.
    private static readonly Unity.Profiling.ProfilerMarker _mDoors = new("TC.UpdateDoors");
    private static readonly Unity.Profiling.ProfilerMarker _mCab = new("TC.UpdateCabSteering");
    private static readonly Unity.Profiling.ProfilerMarker _mIn = new("TC.InboundDwell");
    private static readonly Unity.Profiling.ProfilerMarker _mOut = new("TC.OutboundDwell");
    private static readonly Unity.Profiling.ProfilerMarker _mSwitch = new("TC.StateSwitch");

    // Paint + logo once the spawner has finished setting the truck up (shipment / outbound appointment known by now).
    private void Start()
    {
        TruckLivery.Apply(this);
        VehicleGrounding.AttachShadowOnly(gameObject);   // soft contact shadow between the tyres and the asphalt
    }

    // Time study: guard shack -> door, only for trucks that never had to wait at the side lot / door-wait spot.
    private float _metGateT = -1f;
    private bool _metWaited;
    private float _metOffloadT = -1f;

    private void TrackGateMetric()
    {
        if (_state == TruckState.GuardCheck && _metGateT < 0f) _metGateT = OpsMetrics.Now;
        // Check-in minute is not saved: a truck restored past the gate counts from the moment it is restored.
        if (CheckInSimMinute < 0 && _state != TruckState.Idle && _state != TruckState.Queuing && _state != TruckState.GuardCheck)
        {
            var c = Ctx;
            if (c != null && c.TimeService != null) CheckInSimMinute = c.TimeService.TotalMinutesElapsed;
        }
        if (_state == TruckState.ToDoorWait || _state == TruckState.WaitingForDoor || IsSideLotState(_state)) _metWaited = true;
    }

    private void Update()
    {
        TrackGateMetric();
        using (_mDoors.Auto()) UpdateDoors();
        using (_mCab.Auto()) UpdateCabSteering();
        using (_mIn.Auto()) UpdateInboundDwellClock();
        using (_mOut.Auto()) UpdateOutboundDwellClock();
        UpdateDockBar();
        using (_mSwitch.Auto()) UpdateStateMachine();
    }

    /// <summary>World bar while docked, once the dwell countdown has stopped (offload claimed / load complete): a live
    /// "Unloading at Door N - 3/12 pallets off" or "Loading at Door N - 5 pallet(s) aboard". Out the gate (Idle) = hidden.</summary>
    private void UpdateDockBar()
    {
        if (_doorWaitBar == null) return;
        if (_state == TruckState.Idle) { if (_doorWaitBar.HasMessage) _doorWaitBar.Hide(); return; }
        if (_state != TruckState.Docked || _dock == null) return;
        var load = LoadContainer;
        int onTrailer = load != null ? load.childCount : 0;
        if (!_isOutbound)
        {
            // A paused offload (no room, re-queued) has pallets off already: keep showing its progress, not the arrival countdown.
            bool started = TotalPalletsAtDock > 0 && onTrailer < TotalPalletsAtDock;
            if (!_offloadClaimed && !_offloadComplete && !started) return;   // the inbound dwell countdown is still showing
            int total = Mathf.Max(TotalPalletsAtDock, onTrailer);
            int off = Mathf.Max(0, total - onTrailer);
            string what = _offloadComplete ? $"Unloaded at Door {_dock.DoorNumber}"
                        : !_offloadClaimed ? $"Unloading paused at Door {_dock.DoorNumber} - {off}/{total} pallets off (waiting for lane space)"
                        : $"Unloading at Door {_dock.DoorNumber} - {off}/{total} pallets off";
            _doorWaitBar.ShowDockProgress(total > 0 ? off / (float)total : 1f, what);
        }
        else
        {
            if (!_loadComplete && !string.IsNullOrEmpty(_outboundAppointmentId)) return;   // the pickup-window countdown is showing
            _doorWaitBar.ShowDockProgress(1f, $"Loaded at Door {_dock.DoorNumber} - {onTrailer} pallet(s) aboard");
        }
    }

    private void UpdateStateMachine()
    {
        switch (_state)
        {
            case TruckState.Queuing:
                // Drive to our queue slot and idle. Only the front truck (slot 0)
                // triggers the guard inspection once it reaches the gate.
                if (DriveToward(_currentTarget) && _isFront) BeginGuardCheck();
                break;

            case TruckState.GuardCheck:
                // Waiting for GuardClearedToEnter() callback from GuardController.
                break;

            case TruckState.ToEnterNoTurn:
                // Xform 1 — drive to GateEnterNoTurn, then back in to the door (BeginBackIn).
                if (DriveToward(_currentTarget))
                {
                    // Truck has now physically driven past the gate arm (Tad's spec) — lower it here
                    // rather than relying on the arm's own trigger-exit tracking, which conflates the
                    // truck's passage with the guard's own idle "Posted" stance sitting inside that
                    // same trigger volume.
                    _gateArm?.LowerArm();

                    // BUG FIX: a truck sent in via AssignAndGoWaitForDoor (no free door at spawn, see
                    // that method) still rolls through this same no-turn gate leg — GuardClearedToEnter
                    // routes EVERY truck through ToEnterNoTurn first, dock-assigned or not — but with
                    // _dock null, it used to dead-end at the gate. Route dock-less trucks to
                    // BeginDoorWait() here instead of ever attempting dock-relative navigation.
                    if (_dock == null)
                    {
                        BeginDoorWait();
                        break;
                    }

                    BeginBackIn();
                }
                break;

            case TruckState.BackInToSwingStart:
                // Forward along the yard, past the door, ending parallel to the wall (Bezier set up by BeginBackIn).
                if (DriveToward(_currentTarget)) BeginBackInArc();
                break;

            case TruckState.BackInArc:
                UpdateBackInArc();
                break;

            case TruckState.BackInStraight:
                UpdateBackInStraight();
                break;

            case TruckState.ToDoorWait:
                // Xform 1 → the door-wait park spot, curving in ("turn right, make a big loop" per
                // Tad's spec — achieved by the same forward-Bézier tension already used for every other
                // curved leg, aimed at whatever point/rotation the scene's wait-spot Transform defines).
                if (DriveToward(_currentTarget))
                {
                    // Snap to the wait spot's own rotation exactly — the Bézier tangent gets close but
                    // "facing the warehouse" needs to be exact, not approximate.
                    if (_doorWaitPoint != null) transform.rotation = _doorWaitPoint.rotation;
                    BeginWaitingForDoor();
                }
                break;

            case TruckState.ToSideLot:
                // Same curve-in/snap idiom as ToDoorWait just above, aimed at the SideLotController's
                // anchor instead — "pulling in from the side that doesn't have the Jersey barrier" is
                // whatever the anchor's own authored rotation already encodes, same as _doorWaitPoint.
                if (DriveToward(_currentTarget))
                {
                    if (_sideLotSlot != null && _sideLotSlot.Anchor != null) transform.rotation = _sideLotSlot.Anchor.rotation;
                    BeginWaitingForDoor();
                }
                break;

            case TruckState.ToSideLotEntry:
                // Straight drive to the Entry marker — no curve, DriveToward's normal turn-while-
                // moving is all the rotation it gets on this leg (per Tad's explicit call, "don't
                // worry about rotating for now"). Arrival hands off to FacingSideLotAnchor instead of
                // pulling straight into the Anchor.
                if (DriveToward(_currentTarget))
                {
                    if (_sideLotSlot != null && _sideLotSlot.Anchor != null)
                    {
                        _currentTarget = _sideLotSlot.Anchor.position;
                        _state = TruckState.FacingSideLotAnchor;
                    }
                    else
                    {
                        // No Anchor somehow (shouldn't happen — BeginDoorWait already required one to
                        // pick this SideLot) — treat the Entry point itself as good enough to wait at.
                        BeginWaitingForDoor();
                    }
                }
                break;

            case TruckState.FacingSideLotAnchor:
            {
                // Rotate in place to square up perpendicular to the barrier ahead — real trailers
                // don't drift-turn into a straight lot, they line up first. Same rotate-in-place idiom
                // as FacingTruckNavPoint0 above.
                Vector3 toAnchor = _currentTarget - transform.position;
                toAnchor.y = 0f;
                if (toAnchor.sqrMagnitude > 0.0001f)
                {
                    Quaternion desired = Quaternion.LookRotation(toAnchor.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, driveTurnSpeed * Time.deltaTime);

                    if (Quaternion.Angle(transform.rotation, desired) <= 0.5f)
                    {
                        transform.rotation = desired;
                        SetTarget(TruckState.ToSideLotAnchor, _currentTarget);
                    }
                }
                else
                {
                    // Same degenerate-vector bug as FacingTruckNavPoint1 — Entry and Anchor happening
                    // to sit at the same XZ would spin the whole body forever. Nothing meaningful to
                    // face — just move on.
                    SetTarget(TruckState.ToSideLotAnchor, _currentTarget);
                }
                break;
            }

            case TruckState.ToSideLotAnchor:
            {
                // Straight pull forward into the Anchor — already squared up by FacingSideLotAnchor,
                // so this is a plain forward translation at sideLotPullInSpeed (slower than driveSpeed,
                // reads as a careful final pull-in), no further rotation needed.
                Vector3 flat = new Vector3(_currentTarget.x, _groundY, _currentTarget.z);
                Vector3 toTarget = flat - transform.position;
                toTarget.y = 0f;

                if (toTarget.magnitude < arrivedThreshold || toTarget.magnitude <= sideLotPullInSpeed * Time.deltaTime ||
                    Vector3.Dot(toTarget, transform.forward) <= 0f)   // same overshoot guard as ToTruckNavPoint1
                {
                    transform.position = flat;
                    if (_sideLotSlot != null && _sideLotSlot.Anchor != null) transform.rotation = _sideLotSlot.Anchor.rotation;
                    BeginWaitingForDoor();
                    break;
                }

                transform.position += transform.forward * sideLotPullInSpeed * Time.deltaTime;
                break;
            }

            case TruckState.WaitingForDoor:
                UpdateWaitingForDoor();
                break;

            case TruckState.ReversingOutOfSideLot:
            {
                // Straight-line reverse back to GateEnterNoTurn — same shape as ReversingToTruckNavPoint3
                // (translate toward the target, root rotation untouched; DriveToward's own rotate-toward-
                // heading in the very next state re-aligns it once it starts driving forward again).
                // Same "who's calling" branch as ReversingToSideLotEntry above: a claimed _dock means a
                // door freed up and this reverse is on its way back IN to grab it; no _dock means this
                // is a give-up timeout and the truck needs to actually leave, not re-enter the gate.
                Vector3 flat = new Vector3(_currentTarget.x, _groundY, _currentTarget.z);
                Vector3 toTarget = flat - transform.position;
                toTarget.y = 0f;

                if (toTarget.magnitude < arrivedThreshold)
                {
                    transform.position = flat;
                    _sideLotSlot?.Release();
                    _sideLotSlot = null;
                    _sideLot = null;

                    if (_dock == null)
                    {
                        BeginDeparture(succeeded: false);
                        break;
                    }

                    if (_gateEnterNoTurn.HasValue)
                        SetTarget(TruckState.ToEnterNoTurn, _gateEnterNoTurn.Value);
                    break;
                }

                Vector3 step = toTarget.normalized * slowReverseSpeed * Time.deltaTime;
                if (step.magnitude > toTarget.magnitude) step = toTarget;
                transform.position += step;
                break;
            }

            case TruckState.ReversingToSideLotEntry:
            {
                // "Use a transform move backup until the tractor is lined up with the entry" — straight-
                // line reverse translation (root rotation untouched) from the Anchor back to Entry, same
                // idiom as ReversingOutOfSideLot/ReversingToTruckNavPoint3 above. On arrival, this state
                // serves two different callers that both back out to the same Entry marker: a door that
                // freed up (UpdateWaitingForDoor claims _dock BEFORE starting the reverse) backs in to
                // that door (BeginBackIn); a give-up timeout (_dock stays null — never
                // claimed one) instead runs the normal give-up departure from here, exactly as if it had
                // just started that departure from a stand-still.
                Vector3 flat = new Vector3(_currentTarget.x, _groundY, _currentTarget.z);
                Vector3 toTarget = flat - transform.position;
                toTarget.y = 0f;

                if (toTarget.magnitude < arrivedThreshold)
                {
                    transform.position = flat;
                    _sideLotSlot?.Release();
                    _sideLotSlot = null;
                    _sideLot = null;

                    if (_dock == null)
                    {
                        BeginDeparture(succeeded: false);
                        break;
                    }

                    BeginBackIn();
                    break;
                }

                Vector3 step = toTarget.normalized * slowReverseSpeed * Time.deltaTime;
                if (step.magnitude > toTarget.magnitude) step = toTarget;
                transform.position += step;
                break;
            }

            case TruckState.ToApproach:
                // Xform 2 (only reached by an old save now) — then back in to the door.
                if (DriveToward(_currentTarget)) BeginBackIn();
                break;

            case TruckState.ToBackup:
                // Old saves only: the dead-straight-then-flip maneuver.
                if (DriveToward(_currentTarget))
                {
                    transform.rotation = DockRotationFor(_dock);
                    _state = TruckState.Docked;
                    OnDocked();
                }
                break;

            // WaitingAtBackup / Reversing / ReversingToDock: retired along with the old curved
            // turn-and-reverse maneuver. Never entered by new gameplay, and RestoreFromSnapshot
            // redirects any old save frozen in one straight into ToBackup instead of resuming here —
            // kept only as TruckState enum members so an old save's stored ordinal never resolves to
            // the wrong state.

            case TruckState.Docked:
                if (_dockedSlideActive)
                {
                    Vector3 toSlideTarget = _dockedSlideTarget - transform.position;
                    toSlideTarget.y = 0f;
                    float dist = toSlideTarget.magnitude;
                    float step = slowReverseSpeed * 0.5f * Time.deltaTime;
                    if (step >= dist)
                    {
                        transform.position = new Vector3(_dockedSlideTarget.x, transform.position.y, _dockedSlideTarget.z);
                        _dockedSlideActive = false;
                    }
                    else
                    {
                        transform.position += toSlideTarget.normalized * step;
                    }
                }

                _dockedTime += Time.deltaTime;
                if (_isOutbound) { UpdateDockedOutbound(); break; }

                if (_offloadComplete)
                {
                    // Dock stocker finished pulling all pallets.
                    BeginDeparture();
                }
                else if (!_offloadClaimed && _dockedTime >= offloadFallbackTimeout)
                {
                    // Fallback departure: only happens if the truck is empty or the player
                    // hasn't assigned anyone to it for a long time.
                    // CRITICAL FIX: If there are still pallets on the trailer, we should
                    // NOT depart automatically just because of a timer (user request:
                    // "trailer should never depart until they're fully unloaded").
                    var container = LoadContainer;
                    if (container == null || container.childCount == 0)
                    {
                        BeginDeparture();
                    }
                    else
                    {
                        // Pallets remain — wait for a dock stocker. Reset timer slightly
                        // to prevent log spam or immediate re-check.
                        _dockedTime = offloadFallbackTimeout - 5f;
                    }
                }
                break;

            case TruckState.DepartToApproach:
                // Pull out forward to Xform 2, then on to Xform 5 without stopping.
                if (DriveToward(_currentTarget))
                {
                    // Truck has now physically cleared the dock stall — safe to free the door for
                    // another truck. Deferred from BeginDeparture/ForceLeaveNow (see their comments)
                    // so the door doesn't read as free, and get claimed by a waiting/newly-spawned
                    // truck, while this truck is still backing/pulling out of that same physical space.
                    if (_dock != null)
                    {
                        _dock.LightController?.SetOccupied(false);
                        _dock.Release();
                        _dock = null;
                    }

                    if (_gateLeaveNoTurn.HasValue)
                    {
                        Vector3 afterGate = _exitWaypoint ?? _gateLeaveNoTurn.Value;
                        SetTargetCurved(TruckState.ToLeaveNoTurn, _gateLeaveNoTurn.Value,
                                        afterGate - _gateLeaveNoTurn.Value);
                    }
                    else
                        StartExiting();
                }
                break;

            case TruckState.ToLeaveNoTurn:
                // Xform 5 — roll through without stopping, on to the exit.
                if (DriveToward(_currentTarget)) StartExiting();
                break;

            case TruckState.ToExit:
                if (DriveToward(_currentTarget))
                {
                    _state = TruckState.Idle;
                    StartCoroutine(ShrinkAndDestroy(exitShrinkTime));
                }
                break;
        }
    }

    // ── Cab steering (articulated tractor) ────────────────────────────────────────
    /// <summary>
    /// Yaw rate for the cab, ignoring instant root-rotation snaps (code that sets transform.rotation
    /// in one frame, e.g. the side-lot squaring-up). A snap used to read as a huge yaw rate and
    /// swung the cab up to maxCabSteer. Anything faster than snapYawRate deg/s counts as a snap:
    /// the rate is 0 that frame and the previous yaw is re-seated.
    /// </summary>
    private const float snapYawRate = 360f;
    private float CabYawRate(float curYaw, float dt)
    {
        float rate = Mathf.DeltaAngle(_prevYaw, curYaw) / dt;
        _prevYaw = curYaw;
        return Mathf.Abs(rate) > snapYawRate ? 0f : rate;
    }

    private void UpdateCabSteering()
    {
        if (!articulateCab || _cab == null) return;

        // The back-in reverse legs steer the cab themselves (SteerCabReversing) — bypass so the two don't fight.
        if (_state == TruckState.BackInArc || _state == TruckState.BackInStraight)
        {
            _prevYaw = transform.eulerAngles.y; // avoid a yaw-rate spike when this state ends
            return;
        }

        float curYaw = transform.eulerAngles.y;

        if (!_cabInit)
        {
            _prevYaw = curYaw;
            _cabInit = true;
            return;
        }

        float dt      = Mathf.Max(Time.deltaTime, 1e-4f);
        float yawRate = CabYawRate(curYaw, dt);

        float steerTarget = Mathf.Clamp(yawRate * cabSteerGain, -maxCabSteer, maxCabSteer);
        _cabYaw           = Mathf.MoveTowards(_cabYaw, steerTarget, cabSteerSlew * dt);

        _cab.localRotation = Quaternion.Euler(0f, _cabYaw, 0f) * _cabRest;
    }

    // ── Forward movement (straight or Bézier) ─────────────────────────────────────
    private bool DriveToward(Vector3 target)
    {
        if (_useBezier)
        {
            _bzT += (driveSpeed / Mathf.Max(_bzArcLen, 0.1f)) * Time.deltaTime;

            if (_bzT >= 1f)
            {
                transform.position = _bzP3;
                _useBezier = false;
                return true;
            }

            Vector3 pos = EvalBezier(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
            pos.y = _groundY;
            transform.position = pos;

            Vector3 tangent = EvalBezierTangent(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 0.001f)
            {
                Quaternion desired = Quaternion.LookRotation(tangent.normalized);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, desired, driveTurnSpeed * 1.5f * Time.deltaTime);
            }
            return false;
        }

        Vector3 flat     = new Vector3(target.x, _groundY, target.z);
        Vector3 toTarget = flat - transform.position;
        toTarget.y = 0f;

        // Also arrived when one frame's step would overshoot the point (fast game speed / frame hitch).
        if (toTarget.magnitude < arrivedThreshold || toTarget.magnitude <= driveSpeed * Time.deltaTime)
        {
            transform.position = flat;
            return true;
        }

        Quaternion desiredLinear = Quaternion.LookRotation(toTarget.normalized);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, desiredLinear, driveTurnSpeed * Time.deltaTime);

        // BUG FIX: confirmed live — a truck circling Door 2 forever instead of ever docking. This
        // pursuit model (rotate a bit toward the target, then always drive forward along the
        // CURRENT heading) has an inherent minimum turning radius of driveSpeed / turnRate. Any
        // caller whose target can end up close AND badly misaligned (e.g. ToBackup's straight-line
        // fallback, reached by a generic-wait truck resuming toward a door from whatever heading it
        // happened to be idling at) can have that target sit inside the achievable turning radius —
        // forward motion then can never close the remaining distance, so the truck just orbits it
        // forever. Holding position while sharply misaligned removes the failure mode entirely: a
        // stationary rotation has no minimum radius, so the heading always converges; only once
        // roughly aligned does it proceed to drive. Well-aligned legs (the overwhelming majority of
        // DriveToward's callers) never hit this branch, so their behavior is unchanged.
        if (Quaternion.Angle(transform.rotation, desiredLinear) > 60f) return false;

        // Drive along the truck's OWN heading, not straight at the target. A straight-line leg
        // whose start heading doesn't already point at its target (e.g. the queue lane feeding
        // into the gate at an angle) used to have the body slide/crab sideways toward the point
        // while it separately spun to face it — two decoupled motions reading as an extra,
        // unnatural move. Moving along transform.forward keeps facing and travel direction in
        // agreement, so the truck arcs through a turn like a vehicle instead.
        transform.position += transform.forward * driveSpeed * Time.deltaTime;

        return false;
    }

    private void SetupBezierForward(Vector3 endPos, Vector3 endForward, float tension)
    {
        _bzP0 = transform.position;
        _bzP3 = new Vector3(endPos.x, _groundY, endPos.z);

        float chord = Vector3.Distance(_bzP0, _bzP3);
        float t = chord * tension;

        _bzP1 = _bzP0 + transform.forward * t;
        _bzP2 = _bzP3 - endForward.normalized * t;

        _bzT = 0f;
        _bzArcLen = chord * 1.4f;
        _useBezier = true;
    }

    private static Vector3 EvalBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u*u*u*p0 + 3f*u*u*t*p1 + 3f*u*t*t*p2 + t*t*t*p3;
    }

    private static Vector3 EvalBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return 3f*u*u*(p1-p0) + 6f*u*t*(p2-p1) + 3f*t*t*(p3-p2);
    }

    // ── Phase transitions ─────────────────────────────────────────────────────────
    private void SetTarget(TruckState next, Vector3 destination)
    {
        _state         = next;
        _currentTarget = destination;
        _useBezier     = false;
    }

    private void SetTargetCurved(TruckState next, Vector3 destination, Vector3 endForward)
    {
        _state         = next;
        _currentTarget = destination;

        endForward.y = 0f;
        if (endForward.sqrMagnitude < 0.0001f)
        {
            _useBezier = false;
            return;
        }
        SetupBezierForward(destination, endForward.normalized, forwardDriveTension);
    }

    /// <summary>Game minute this truck pulled up to position 1 and the guard started checking it in (-1 = not yet). Every arrival
    /// clock (the inbound dwell bar and the PO's 8-hour receive deadline) starts here, never at spawn or at the booked slot
    /// (Tad, 2026-11-01).</summary>
    public long CheckInSimMinute { get; private set; } = -1;

    /// <summary>An inbound truck (with a PO) reached the guard shack / backed into its door. The playtest score's inbound on-time
    /// half listens to these (Tad, 2026-11-28: an inbound PO is on time when it gets a door in time).</summary>
    public static event System.Action<TruckController> InboundCheckedIn, InboundDocked;

    private void BeginGuardCheck()
    {
        _state = TruckState.GuardCheck;
        var ctxCheckIn = Ctx;
        if (CheckInSimMinute < 0 && ctxCheckIn != null && ctxCheckIn.TimeService != null)
            CheckInSimMinute = ctxCheckIn.TimeService.TotalMinutesElapsed;
        if (!_isOutbound && AssignedShipment != null) InboundCheckedIn?.Invoke(this);
        if (!_isOutbound && _inboundClockStartSimMinute < 0 && CheckInSimMinute >= 0)
        {
            _inboundClockStartSimMinute = CheckInSimMinute;
            _inboundPenaltyTierApplied = 0;
        }
        if (_guard != null)
            _guard.BeginInspection(this, GuardClearedToEnter);
        else
            GuardClearedToEnter();
    }

    // ── Back-in (2026-11-14) ─────────────────────────────────────────────────────────────────────
    // Replaces the TruckNavPoint0-5 / TruckPivot0-1 marker chain (13 states). Taken from the NavTest graybox, which Tad liked
    // better: drive along the yard PAST the door, reverse in a quarter turn that swings the trailer's rear onto the door's centre
    // line, then back straight in. The path is planned for the trailer's REAR AXLE (trailerRearAxleLocalOffset), so the rear leads
    // the turn and the cab follows; the root is placed from the axle every frame. Only marker still read: TruckNavPoint5 (the
    // docked spot), falling back to DockPositionFor.

    private Vector3 AxleOffsetFlat => new Vector3(trailerRearAxleLocalOffset.x, 0f, trailerRearAxleLocalOffset.z);
    private Vector3 AxleAt(Vector3 rootPos, Quaternion rot) => rootPos + rot * AxleOffsetFlat;
    private Vector3 RootFromAxle(Vector3 axle, Quaternion rot)
    {
        Vector3 p = axle - rot * AxleOffsetFlat;
        p.y = _groundY;
        return p;
    }

    /// <summary>Where the truck's root sits once backed in: TruckNavPoint5 (stopped dockBackInTrim short), else DockPositionFor.</summary>
    private Vector3 DockedPosition()
    {
        var nav5 = FindDockWaypoint("TruckNavPoint5");
        Vector3 p = nav5 != null ? nav5.position + DockYardForward(_dock) * dockBackInTrim : DockPositionFor(_dock);
        p.y = _groundY;
        return p;
    }

    private static Vector3 QBezier(Vector3 a, Vector3 b, Vector3 c, float t) { float u = 1f - t; return u * u * a + 2f * u * t * b + t * t * c; }
    private static Vector3 QBezierTangent(Vector3 a, Vector3 b, Vector3 c, float t) => 2f * (1f - t) * (b - a) + 2f * t * (c - b);

    /// <summary>Plans the back-in for the assigned door from wherever the truck is and starts the forward leg to the swing point.</summary>
    private void BeginBackIn()
    {
        if (_dock == null) { BeginDoorWait(); return; }
        Vector3 fwd = DockYardForward(_dock); fwd.y = 0f; fwd.Normalize();     // out of the door, into the yard
        Vector3 along = Vector3.Cross(Vector3.up, fwd);                          // along the wall
        Quaternion dockRot = DockRotationFor(_dock);

        Vector3 finalAxle = AxleAt(DockedPosition(), dockRot);
        Vector3 lineUp = finalAxle + fwd * backInStraight;   // the axle is square to the door from here in
        Vector3 corner = lineUp + fwd * backInArm;
        // Come along the wall from the side the truck is on and stop PAST the door: the swing starts on the far side.
        float side = Mathf.Sign(Vector3.Dot(transform.position - corner, along));
        if (side == 0f) side = 1f;
        Vector3 swingAxle = corner - along * side * backInArm;
        Vector3 swingFwd = -along * side;
        Vector3 swingRoot = RootFromAxle(swingAxle, Quaternion.LookRotation(swingFwd));

        _biP0 = swingAxle; _biP1 = corner; _biP2 = lineUp; _biAxleTo = finalAxle;
        _biLen = 0f;
        Vector3 prev = _biP0;
        for (int i = 1; i <= 16; i++) { Vector3 q = QBezier(_biP0, _biP1, _biP2, i / 16f); _biLen += Vector3.Distance(prev, q); prev = q; }
        SetTargetCurved(TruckState.BackInToSwingStart, swingRoot, swingFwd);
    }

    private void BeginBackInArc()
    {
        _throttleAudio?.TriggerBackupAlarm();
        _biT = 0f;
        _biAxle = _biP0;
        _state = TruckState.BackInArc;
    }

    /// <summary>Reverse the quarter turn: the rear axle follows the Bezier, the body faces against its direction of travel.</summary>
    private void UpdateBackInArc()
    {
        _biT = Mathf.Min(1f, _biT + slowReverseSpeed * Time.deltaTime / Mathf.Max(0.1f, _biLen));
        _biAxle = QBezier(_biP0, _biP1, _biP2, _biT);
        Vector3 tan = QBezierTangent(_biP0, _biP1, _biP2, _biT);
        tan.y = 0f;
        if (tan.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(-tan.normalized), 90f * Time.deltaTime);
        transform.position = RootFromAxle(_biAxle, transform.rotation);
        SteerCabReversing();
        if (_biT >= 1f) _state = TruckState.BackInStraight;
    }

    /// <summary>Back straight in, squaring up to the door, then dock.</summary>
    private void UpdateBackInStraight()
    {
        Quaternion dockRot = DockRotationFor(_dock);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, dockRot, 30f * Time.deltaTime);
        _biAxle = Vector3.MoveTowards(_biAxle, _biAxleTo, slowReverseSpeed * Time.deltaTime);
        transform.position = RootFromAxle(_biAxle, transform.rotation);
        SteerCabReversing();
        if ((_biAxle - _biAxleTo).sqrMagnitude < 0.0001f && Quaternion.Angle(transform.rotation, dockRot) < 0.5f)
        {
            transform.SetPositionAndRotation(DockedPosition(), dockRot);
            _cabYaw = 0f;
            if (_cab != null) _cab.localRotation = _cabRest;
            _state = TruckState.Docked;
            OnDocked();
        }
    }

    /// <summary>Cosmetic: while reversing, the tractor angles against the trailer's turn (and straightens as the turn ends).</summary>
    private void SteerCabReversing()
    {
        float curYaw = transform.eulerAngles.y;
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float yawRate = CabYawRate(curYaw, dt);
        if (_cab == null) return;
        float target = Mathf.Clamp(-yawRate * backInCabSteer, -maxCabSteer, maxCabSteer);
        _cabYaw = Mathf.MoveTowards(_cabYaw, target, 40f * dt);
        _cab.localRotation = Quaternion.Euler(0f, _cabYaw, 0f) * _cabRest;
    }

    public void GuardClearedToEnter()
    {
        // Leave the gate queue — the manager advances everyone behind us.
        _clearedGate = true;
        OnClearedGate?.Invoke();
        // The outbound driver's loading clock starts here (see DockScheduleService.DeadlineMinute).
        if (_isOutbound && !string.IsNullOrEmpty(_outboundAppointmentId) && GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService gateSched) && gateSched != null)
            gateSched.NoteGatePass(_outboundAppointmentId);

        // Unified 8-hour inbound dwell clock starts the instant the truck clears the gate — Tad's
        // spec: "every timer bar should run for 8 hours length and should initiate as soon as he
        // proceeds through the gate." See UpdateInboundDwellClock.
        // Now started at check-in (BeginGuardCheck); this only covers a truck that skipped the check (no guard, restored).
        if (!_isOutbound && _inboundClockStartSimMinute < 0)
        {
            var gameCtxClock = Ctx;
            _inboundClockStartSimMinute = gameCtxClock != null && gameCtxClock.TimeService != null
                ? gameCtxClock.TimeService.TotalMinutesElapsed : -1;
            _inboundPenaltyTierApplied = 0;
        }

        // BUG FIX (moved here from OnDocked, 2026-09): "arrived on time" means cleared the gate, not
        // "already backed into a door" — a truck that has to wait for a free door (see
        // AssignAndGoWaitForDoor below) is JUST as much "arrived" as one that docks immediately.
        // Nothing else in the codebase ever set ShipmentStatus.Receiving despite
        // DockScheduleService.JudgeElapsedInboundAppointment explicitly checking for it and letting a
        // truck in that state finish without the "driver never showed" −20 penalty — so a shipment sat
        // InTransit for its entire time in the yard, and a truck that showed up on time but then had to
        // wait for a door (or was simply mid-unload) when its booked block happened to elapse was
        // wrongly charged as a no-show. Setting it HERE is the single fix for both cases at once.
        if (!_isOutbound && AssignedShipment != null)
            AssignedShipment.Status = GameCore.Inventory.ShipmentData.ShipmentStatus.Receiving;

        // Status banner starts the moment the truck clears the gate — green "Heading to Door X" if a
        // door is already assigned (set via AssignAndGo before spawn/gate-clear), red "Heading to Side
        // Lot" otherwise. Inbound only; outbound trucks don't run the door-wait flow at all.
        if (!_isOutbound)
        {
            if (_doorWaitBar == null) _doorWaitBar = gameObject.AddComponent<TruckDoorWaitBar>();
            if (_dock != null) _doorWaitBar.ShowHeadingToDoor(_dock.DoorNumber);
            else _doorWaitBar.ShowHeadingToSideLot();
        }

        // Xform 1: roll through the gate without stopping (straight leg).
        if (_gateEnterNoTurn.HasValue)
            SetTarget(TruckState.ToEnterNoTurn, _gateEnterNoTurn.Value);
        else if (_dock != null)
            BeginBackIn();
        else
            BeginDoorWait();
    }

    /// <summary>Routes a truck with no free door toward somewhere to wait. Tries an unoccupied
    /// SideLotController first (claimed immediately — before the truck physically arrives — so two
    /// trucks clearing the gate close together can't both target the same spot). If every slot is
    /// full (or no lot is built) the driver gives up and leaves, with a red all-caps complaint in the
    /// Systems Log. The generic _doorWaitPoint is no longer used for new arrivals — see below.</summary>
    private void BeginDoorWait()
    {
        // BUG FIX (Tad's spec): used to treat a whole SideLotController as one shared spot
        // (!l.IsOccupied), so only the very first truck ever routed here — every SideLot actually has
        // several independent parking slots (one per Jersey_barrier segment), so this now searches
        // every lot for its own first free Slot instead of asking the lot itself if it's "occupied".
        // Already holding a slot (restore re-claimed it) — keep it rather than taking a second one.
        SideLotController.Slot slot = _sideLotSlot != null && _sideLotSlot.Occupant == this ? _sideLotSlot : null;
        SideLotController lot = slot != null ? _sideLot : null;
        if (slot == null)
        {
            foreach (var candidate in SideLotController.All)
            {
                if (candidate == null) continue;
                slot = candidate.FindFreeSlot();
                if (slot != null) { lot = candidate; break; }
            }
        }

        if (slot != null)
        {
            slot.Claim(this);
            _sideLot = lot;
            _sideLotSlot = slot;

            // Straight drive to the Entry marker first (further out from the fence), not a curve —
            // per Tad's explicit call, no need to worry about rotating on this leg at all.
            // ToSideLotEntry hands off to FacingSideLotAnchor (rotate in place to square up with the
            // barrier) then ToSideLotAnchor (straight pull forward) once it arrives. A SideLot placed
            // before the Entry marker existed falls back to the old curve-straight-to-Anchor behavior.
            if (slot.Entry != null)
                SetTarget(TruckState.ToSideLotEntry, slot.Entry.position);
            else
                SetTargetCurved(TruckState.ToSideLot, slot.Anchor.position, slot.Anchor.forward);
            return;
        }

        // Every side lot slot is taken (or no lot is built): the driver leaves. Tad's call 2026-09-23 —
        // this used to fall back to the single generic _doorWaitPoint, which every overflow truck
        // shared, so trucks 6+ all parked on top of each other there.
        string poNumber = AssignedShipment?.PONumber;
        string supplier = AssignedShipment?.SupplierName;
        // Uppercased BEFORE the link is wrapped around it: upper-casing the finished string would also
        // upper-case the link id (an appointment GUID), which then no longer resolves.
        string who;
        if (_isOutbound)
            who = LogLinks.Appointment(_outboundAppointmentId,
                                       TruckYardManager.OutboundWhoFor(_outboundAppointmentId).ToUpperInvariant());
        else if (!string.IsNullOrEmpty(poNumber))
            who = LogLinks.Po(poNumber, (!string.IsNullOrEmpty(supplier) ? $"{poNumber} | {supplier}" : poNumber).ToUpperInvariant());
        else
            who = (!string.IsNullOrEmpty(supplier) ? supplier : name).ToUpperInvariant();
        SystemsLogWindow.LogWarning("THE DRIVER FOR: " + who +
            " HAS A MESSAGE FOR YOU. PULL YOUR HEAD OUT OF YOUR ASS! THERE'S NO WHERE TO PARK!");

        // (bar stays up until the truck is out the gate - Tad 2026-11-05)
        BeginDeparture(succeeded: false);
    }

    private void BeginWaitingForDoor()
    {
        _state = TruckState.WaitingForDoor;
        _doorWaitPollTimer = 0f;
        // Bar/message is now driven every frame by UpdateInboundDwellClock (the unified 8-hour
        // ladder), not a fixed countdown started here — see that method.

        if (AssignedShipment != null)
        {
            int critical = GameCore.Inventory.CriticalStockCheck.CountCriticalLines(
                AssignedShipment.LineItems.Select(li => li.SkuId));
            SystemsLogWindow.LogGuard(
                $"Another angry driver in the side lot — {LogLinks.Po(AssignedShipment.PONumber)}. " +
                $"One hour to receive {critical} potential scratch(es).");
        }
    }

    /// <summary>Polls for a freed-up door while parked. Claims the first one it finds and drives in
    /// exactly like a normal arrival the moment one appears. No longer times out on its own — a truck
    /// with no door just keeps waiting/polling here until either a door frees up or the unified
    /// 8-hour dwell clock (UpdateInboundDwellClock) forces it to give up.</summary>
    private void UpdateWaitingForDoor()
    {
        _doorWaitPollTimer -= Time.deltaTime;
        if (_doorWaitPollTimer > 0f) return;
        _doorWaitPollTimer = doorWaitPollSeconds;

        DockSlot freeDock;
        if (_isOutbound)
        {
            // Outbound: only its own door will do, and it waits until its appointment is closed out.
            if (OutboundAppointmentGone(out _))
            {
                SystemsLogWindow.LogWarning($"The driver for {OutboundLink()} waited for " +
                                            $"{LogLinks.Door(_outboundDoorNumber, _outboundAppointmentId)} " +
                                            $"through the appointment and another {OutboundGraceMinutes} minutes, then left empty.");
                ForceLeaveNow();
                return;
            }
            freeDock = DockSlot.All.FirstOrDefault(d => d != null && d.DoorNumber == _outboundDoorNumber && !d.IsOccupied);
        }
        else
        {
            freeDock = FindAnyFreeDock();
        }
        if (freeDock == null) return;

        _dock = freeDock;
        _dock.Claim();
        ApplyOnTimeDoorBonus();

        if (_sideLotSlot != null)
        {
            // Reverse straight back out first — can't curve directly to ApproachPoint() from inside the
            // fenced lot the way a generic _doorWaitPoint truck can. Backs up to the Entry marker (per
            // Tad's spec) and backs in to the door from there (BeginBackIn) — see
            // ReversingToSideLotEntry's case body. Falls back to the old all-the-way-through-the-gate
            // retreat for a SideLot placed before the Entry marker existed.
            if (_sideLotSlot.Entry != null)
                SetTarget(TruckState.ReversingToSideLotEntry, _sideLotSlot.Entry.position);
            else if (_gateEnterNoTurn.HasValue)
                SetTarget(TruckState.ReversingOutOfSideLot, _gateEnterNoTurn.Value);
            return;
        }

        BeginBackIn();
    }

    /// <summary>Same random-pick-among-free-doors policy as TruckYardManager.FindFreeDock — kept local
    /// (rather than routing back through the yard manager) since a waiting truck already knows the
    /// full DockSlot registry directly.</summary>
    private DockSlot FindAnyFreeDock()
    {
        // A door an outbound driver is already parked and waiting for is spoken for — otherwise an
        // inbound truck grabs it the moment it frees and the outbound pickup misses its window again.
        var reserved = new HashSet<int>();
        foreach (var t in FindObjectsByType<TruckController>())
            if (t != null && t != this && t.IsOutboundWaitingForDoor) reserved.Add(t.OutboundDoorNumber);

        List<DockSlot> free = null;
        foreach (var d in DockSlot.All)
        {
            if (d == null || d.IsOccupied || reserved.Contains(d.DoorNumber)) continue;
            free ??= new List<DockSlot>();
            free.Add(d);
        }
        return free == null ? null : free[Random.Range(0, free.Count)];
    }

    /// <summary>How long an outbound driver keeps waiting after their booked window ends (the late fee
    /// has already been charged by then) before giving up and leaving empty. The red stage of the
    /// delay bar counts this down.</summary>
    private const int OutboundGraceMinutes = GameCore.Inventory.DockScheduleService.OutboundDriverPatienceMinutes;   // 8 h (Tad, 2026-11-03)

    /// <summary>True when this outbound driver is done waiting: the appointment was pulled off the
    /// grid (Parked) or deleted, or its window ended more than OutboundGraceMinutes ago. A truck with
    /// no appointment on file (the STAGED dispatch trigger) never gives up this way. (Used to give up
    /// the instant the window was swept, which left no red "driver leaves" stage at all.)</summary>
    private bool OutboundAppointmentGone(out string whoFor)
    {
        whoFor = $"Door {_outboundDoorNumber}";
        if (string.IsNullOrEmpty(_outboundAppointmentId)) return false;
        if (!GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService schedule) || schedule == null)
            return false;

        var appt = schedule.FindById(_outboundAppointmentId);
        if (appt != null && !string.IsNullOrEmpty(appt.CustomerName)) whoFor = appt.CustomerName;
        if (appt == null || appt.Parked) return true;
        return TryMinutesToWindowEnd(appt, out int toEnd) && toEnd <= -OutboundGraceMinutes;
    }

    /// <summary>Game minutes from now until the appointment's window ends (negative once it has).</summary>
    private static bool TryMinutesToWindowEnd(GameCore.Inventory.DockAppointment appt, out int minutes)
    {
        minutes = 0;
        var clock = Ctx?.TimeService;
        if (appt == null || clock == null) return false;
        if (!GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService sched) || sched == null) return false;
        int now = clock.Day * 1440 + clock.Hour * 60 + clock.Minute;
        int end = sched.DeadlineMinute(appt);   // the one shared deadline: block, or 15 min per pallet if longer
        minutes = end - now;
        return true;
    }

    /// <summary>
    /// Outbound delay bar (Tad, 2026-09-23). Starts full and green when the pickup window opens and
    /// drains to its end: "X minutes until $fine incurred" (the late fee each unshipped order on the
    /// trailer is charged when the window ends). Past that it goes red and counts down the grace hour:
    /// "X minutes Driver leaves! $X Lost from missed profit" (the net profit those orders would have
    /// made). Before this the card only ever showed the "Delay Timer Bar" placeholder for outbound.
    /// </summary>
    private void UpdateOutboundDwellClock()
    {
        if (!_isOutbound || string.IsNullOrEmpty(_outboundAppointmentId)) return;
        if (_state == TruckState.Queuing || _state == TruckState.GuardCheck || IsLeaving) return;
        if (_loadComplete) return;

        if (!GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService schedule) || schedule == null) return;
        var appt = schedule.FindById(_outboundAppointmentId);
        if (appt == null || !TryMinutesToWindowEnd(appt, out int toEnd)) return;

        int fine = 0, profit = 0;
        if (GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.OrderService orders) && orders != null)
        {
            foreach (var o in orders.ActiveOrders.Where(o => appt.OrderIds.Contains(o.OrderId)))
            {
                if (o.Status == GameCore.Inventory.OrderData.OrderStatus.Shipped ||
                    o.Status == GameCore.Inventory.OrderData.OrderStatus.Cancelled) continue;
                if (!o.HasBeenFined)
                    fine += Mathf.RoundToInt((o.LateFeePercent > 0f ? o.LateFeePercent : 0.25f) * o.TotalRevenue);
                profit += o.ExpectedProfit;
            }
        }

        float windowMinutes = appt.SpanBlocks * GameCore.Inventory.DockScheduleService.BlockMinutes;
        float frac;
        Color color;
        string text;
        if (toEnd > 0)
        {
            frac = Mathf.Clamp01(toEnd / windowMinutes);
            color = frac > 0.5f ? TruckDoorWaitBar.DwellGreenColor
                  : frac > 0.25f ? TruckDoorWaitBar.DwellYellowColor
                  : TruckDoorWaitBar.DwellOrangeColor;
            text = fine > 0 ? $"{toEnd} minutes until ${fine:N0} incurred" : $"{toEnd} minutes left in pickup window";
        }
        else
        {
            int left = Mathf.Max(0, OutboundGraceMinutes + toEnd);
            frac = Mathf.Clamp01(left / (float)OutboundGraceMinutes);
            color = TruckDoorWaitBar.DwellRedColor;
            int aboard = LoadContainer != null ? LoadContainer.childCount : 0;
            // A trailer with freight aboard doesn't leave (see UpdateDockedOutbound) — say what it's
            // actually waiting for instead of counting down to a departure that won't happen.
            text = aboard > 0 && left == 0
                ? $"Late — {aboard} pallet(s) aboard, close out to ship"
                : $"{left} minutes Driver leaves! ${Mathf.Max(0, profit):N0} Lost from missed profit";
        }

        if (_doorWaitBar == null) _doorWaitBar = gameObject.AddComponent<TruckDoorWaitBar>();
        _doorWaitBar.ShowDwellStatus(frac, color, text);
    }

    /// <summary>Scheduler hyperlink naming this outbound truck's customer and order(s).</summary>
    private string OutboundLink() =>
        LogLinks.Appointment(_outboundAppointmentId, TruckYardManager.OutboundWhoFor(_outboundAppointmentId));

    private void OnDocked()
    {
        if (_metGateT >= 0f && !_metWaited) OpsMetrics.Record(OpsMetrics.Kind.GateToDoor, OpsMetrics.Now - _metGateT);
        _metGateT = -1f;
        _dock.LightController?.SetOccupied(true);
        if (_isOutbound && !string.IsNullOrEmpty(_outboundAppointmentId) && GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService dockedSched) && dockedSched != null)
            dockedSched.NoteDocked(_outboundAppointmentId);

        // Docked: the bar stays up (Tad, 2026-11-05) and switches to the live "Unloading / Loading at Door N" line (UpdateDockBar).

        // Reset offload/load handoff — the truck now waits in Docked until a dock stocker offloads
        // or loads it (TrailerOffloadController / TrailerLoadController) or the fallback timeout fires.
        _dockedTime      = 0f;
        _offloadClaimed  = false;
        _offloadComplete = false;
        _loadClaimed     = false;
        _loadComplete    = false;

        { var stampCtx = Ctx; _dockStampSimMinute = stampCtx != null && stampCtx.TimeService != null ? stampCtx.TimeService.TotalMinutesElapsed : -1; }

        // Inbound only — outbound dwell isn't part of the vendor dwell-time stat.
        if (!_isOutbound)
        {
            var gameCtx = Ctx;
            _dockedAtSimMinute = gameCtx != null ? gameCtx.TimeService.TotalMinutesElapsed : -1;
        }

        // Swing the barn doors fully open and ghost the trailer so the dock-stocker can reach the
        // product and it reads from inside the warehouse.
        OpenTrailerDoors();
        SetDockedGhost(true);

        // Hold the warehouse-side rollup door open for the whole docked duration — its own trigger
        // collider can't tell "truck still here" from "dock stocker/receiver just walked out", so it
        // was swinging shut mid-unload. See RollupDoorController.SetForcedOpen.
        _dock.SetDoorForcedOpen(true);

        if (!_isOutbound && AssignedShipment != null) InboundDocked?.Invoke(this);

        TotalPalletsAtDock = 0;   // a new visit: ClaimForOffload counts the load (and keeps the highest count across pauses)
        Debug.Log($"[TruckController] {name} docked at door {_dock.DoorNumber}. Awaiting {(_isOutbound ? "load" : "offload")}.");
    }

    /// <summary>Outbound counterpart of the inbound Docked branch in Update(): waits for
    /// TrailerLoadController to load staged pallets and call CompleteLoad(), or falls back to
    /// departing anyway if unclaimed for too long (same self-preservation the inbound "no DS
    /// available" case has) — unlike inbound, an outbound trailer STARTS empty and fills up, so
    /// "still empty" is never a reason to leave early the way it is for offload.</summary>
    private bool _gaveUpWithCargo;

    /// <summary>A closed-out / cut order must not drive away while a dock stocker still has a pallet on his forks (he chased the truck out
    /// of the property, 2026-11-03). The truck waits for the loader to finish the pallet in hand, but never longer than this (real seconds).</summary>
    private float _departHoldSeconds;
    private const float MaxDepartHoldSeconds = 120f;

    private void UpdateDockedOutbound()
    {
        if (_loadComplete)
        {
            if (_loadClaimed && _departHoldSeconds < MaxDepartHoldSeconds) { _departHoldSeconds += Time.deltaTime; return; }
            BeginDeparture();
        }
        else if (!string.IsNullOrEmpty(_outboundAppointmentId))
        {
            // Booked pickup: the driver doesn't know or care whether the freight is staged — they
            // wait at the door for their whole appointment window (Tad, 2026-09-23). This used to fall
            // through to the 22.5-real-second timeout below, so an on-time driver with nothing staged
            // yet pulled out almost immediately and the block was later swept as a no-show. Once the
            // window closes with loading never started, they leave angry; the missed-appointment
            // penalties come from the normal schedule sweep that closed it.
            // Never with freight aboard: a trailer that has any of the order on it waits for the player's
            // close-out — leaving would drive the loaded pallets away unbilled (seen live 2026-09-23).
            bool cargoAboard = LoadContainer != null && LoadContainer.childCount > 0;
            if (!_loadClaimed && !cargoAboard && OutboundAppointmentGone(out _))
            {
                string door = _dock != null ? LogLinks.Door(_dock.DoorNumber, _outboundAppointmentId) : "the door";
                SystemsLogWindow.LogWarning($"The driver for {OutboundLink()} sat at {door} through their " +
                                            $"appointment and another {OutboundGraceMinutes} minutes with nothing loaded and left empty.");
                BeginDeparture();
            }
            else if (cargoAboard && !_gaveUpWithCargo && OutboundAppointmentGone(out string who) &&
                     GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.OrderService orderSvc) && orderSvc != null)
            {
                // Eight hours past the booked slot with freight aboard: the driver has had enough and goes, taking what is on the
                // trailer (billed); everything not aboard is cut, with the usual release penalties.
                _gaveUpWithCargo = true;
                SystemsLogWindow.LogWarning($"The driver for {OutboundLink()} waited {OutboundGraceMinutes / 60} hours past the slot and left with what was aboard.");
                orderSvc.ReleaseTrailer(this, out _);
            }
        }
        else if (!_loadClaimed && _dockedTime >= offloadFallbackTimeout)
        {
            BeginDeparture();
        }
    }

    private void BeginDeparture() => BeginDeparture(succeeded: true);

    private bool _warnedUnbilledDeparture;

    /// <summary>True if a pallet in this trailer belongs to an order that is still active (not shipped/archived).</summary>
    private bool TryFindUnbilledCargo(out string orderNumber)
    {
        orderNumber = null;
        var load = LoadContainer;
        if (load == null || load.childCount == 0) return false;
        if (!GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.OrderService orders) || orders == null) return false;
        foreach (var p in load.GetComponentsInChildren<OutboundPalletBuilder>(true))
        {
            if (p == null || string.IsNullOrEmpty(p.OrderId)) continue;
            var o = orders.ActiveOrders.FirstOrDefault(x => x.OrderId == p.OrderId);
            if (o == null || o.Status == GameCore.Inventory.OrderData.OrderStatus.Shipped || o.Status == GameCore.Inventory.OrderData.OrderStatus.Cancelled) continue;
            orderNumber = o.OrderNumber ?? o.OrderId;
            return true;
        }
        return false;
    }

    /// <summary><paramref name="succeeded"/> is false exactly once, for the door-wait give-up case
    /// (UpdateWaitingForDoor's timeout / BeginDoorWait's missing-waypoint fallback) — a truck that
    /// never got a door never delivered anything, so it must not be marked Departed (that reads as "PO
    /// fulfilled" everywhere else in the code, e.g. DockScheduleService.IsComplete). Everything else
    /// about leaving the yard — release the dock if one was somehow held, close up, roll out through
    /// the gate — is identical either way, so this stays one method with one branch rather than two
    /// near-duplicate exit routines drifting apart over time.</summary>
    private void BeginDeparture(bool succeeded)
    {
        // NEVER DRIVE AWAY UNBILLED FREIGHT (2026-10-17 playtest): an outbound trailer left with 3 loaded pallets
        // (157 cases) of an order that hadn't shipped - stock gone, nothing billed. Billing only happens at close-out,
        // so while any pallet aboard belongs to a still-active order the truck stays at the door. The warning names
        // the caller so the trigger can be found.
        if (_isOutbound && _state == TruckState.Docked && TryFindUnbilledCargo(out string unbilledOrder))
        {
            if (!_warnedUnbilledDeparture)
            {
                _warnedUnbilledDeparture = true;
                Debug.LogWarning($"[TruckController] {name} was told to depart with unbilled freight of order {unbilledOrder} aboard - " +
                                 $"staying at the door until it is closed out.\n{System.Environment.StackTrace}");
            }
            _loadComplete = false;
            return;
        }
        _warnedUnbilledDeparture = false;

        // Captured before anything below has a chance to reset _dockedAtSimMinute (the VENDORS-tab
        // dwell-hours block a little further down zeroes it back to -1) — ApplyOffloadStandardBonus
        // needs the same "docked to last pallet off" window that block measures, just compared
        // against the Standard's allowance instead of logged as a raw stat.
        long dockedAtSimMinuteForStandard = _dockedAtSimMinute;

        // NOTE: the door itself (_dock.Release() / its dock light) is deliberately NOT freed here —
        // this truck is still physically parked in the stall and is about to spend several seconds
        // slowly pulling out (DepartToApproach). Releasing this early let a waiting/newly-spawned
        // truck claim the "free" door and start backing in while this one was still there, so the two
        // trucks overlapped at the same door. The dock is now released once this truck actually
        // clears the stall — see the DepartToApproach arrival check in Update(). _dock itself stays
        // set (not nulled) until then so ApproachPoint() below still resolves correctly.
        //
        // Release the rollup door's forced-open hold — the truck's own exit through the trigger will
        // close it normally as it drives out.
        _dock?.SetDoorForcedOpen(false);
        // A truck that timed out waiting in the SideLot (give-up path) needs to free its slot too —
        // the normal path already clears _sideLotSlot in ReversingToSideLotEntry/ReversingOutOfSideLot
        // before ever reaching here; this only matters for the rare no-Entry-and-no-gate edge case
        // that calls BeginDeparture directly from BeginDoorWait/UpdateWaitingForDoor.
        _sideLotSlot?.Release();
        _sideLotSlot = null;
        _sideLot = null;

        if (succeeded)
        {
            // Mark shipment as Departed and retire it from the pending list. A departed PO has done its
            // whole job — leaving it in PendingShipments meant the Dev Console's inbound list grew a red
            // "[Departed]" row per truck forever and every one of them was re-serialised into every save.
            // This truck keeps its own AssignedShipment reference, so nothing here loses data it still needs.
            if (AssignedShipment != null)
            {
                AssignedShipment.Status = GameCore.Inventory.ShipmentData.ShipmentStatus.Departed;
                if (GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.ShipmentService shipSvc))
                {
                    // Inbound trailer leaving short: queue the vendor's make-good delivery automatically
                    // rather than waiting on the player to notice and ask for it via the tooltip's
                    // Request Backfill button — per Tad's explicit call. RequestBackfill re-parks this
                    // same PO in the Scheduler's unscheduled pool and is itself what flips Status back to
                    // InTransit, so the PurgeCompleted() call right below leaves it pending, not archived.
                    // The player can still cancel the queued redelivery afterward for a straight credit
                    // instead — see ShipmentService.RequestCredit.
                    if (!_isOutbound && AssignedShipment.HasUnresolvedShortage)
                        shipSvc.RequestBackfill(AssignedShipment);

                    shipSvc.PurgeCompleted();
                }

                // VENDORS tab's "Avg Hours in Door" — inbound only, and only once we actually have a real
                // docked-at stamp (a truck that skipped Docked entirely, if that's ever possible, shouldn't
                // report a bogus 0-hour dwell).
                if (!_isOutbound && _dockedAtSimMinute >= 0 &&
                    !string.IsNullOrEmpty(AssignedShipment.SupplierId) &&
                    GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.VendorPerformanceTracker perf))
                {
                    var gameCtx = Ctx;
                    if (gameCtx != null)
                    {
                        float hours = (gameCtx.TimeService.TotalMinutesElapsed - _dockedAtSimMinute) / 60f;
                        perf.RecordDwellHours(AssignedShipment.SupplierId, Mathf.Max(0f, hours), gameCtx.TimeService.Day);
                    }
                }
                _dockedAtSimMinute = -1;
            }

            // Blue "trailer offloaded, departing" banner with the shipment's actual case tally —
            // inbound only, since "cases received" has no meaning for an outbound (loading) truck.
            // Beating the offload Standard overrides this with the driver's own happy line instead
            // (ApplyOffloadStandardBonus already wrote it into _doorWaitBar when true).
            if (!_isOutbound && AssignedShipment != null)
            {
                if (_doorWaitBar == null) _doorWaitBar = gameObject.AddComponent<TruckDoorWaitBar>();
                bool beatStandard = ApplyOffloadStandardBonus(dockedAtSimMinuteForStandard);
                if (!beatStandard)
                    _doorWaitBar.ShowDeparting(AssignedShipment.TotalReceivedUnits, AssignedShipment.TotalUnits);
            }
        }
        else
        {
            // The unified 8-hour dwell clock (ResolveInboundDeadline) already applied its own
            // penalty, cancelled the shipment, and logged the loss before triggering this departure
            // in the ordinary "couldn't get a door in time" case — skip so it isn't double-charged.
            // Only a genuine "nowhere to wait at all" config failure (BeginDoorWait, before the
            // ladder ever gets a chance to fire) reaches this branch unresolved.
            if (_inboundPenaltyTierApplied < 4)
                ApplyNoWaitSpotPenalty();
        }

        // Solid trailer + closed doors again before it drives off.
        SetDockedGhost(false);
        CloseTrailerDoors();

        // Per Tad's explicit ask: the green reward callout plays "as soon as the doors close and
        // they pull out" — right here, not back in ApplyOffloadStandardBonus (which only fires the
        // vendor-standing change/log/driver-note; the world-space FX waits for this exact moment).
        if (_pendingOffloadStandardFx)
        {
            _pendingOffloadStandardFx = false;
            Vector3 fxOrigin = (_cab != null ? _cab.position : transform.position) + Vector3.up * 2.4f;
            VendorRelationshipFx.Show(fxOrigin, $"Vendor Relationship +{OffloadStandardBonusPoints}!");
        }


        // Pull out forward to Xform 2, easing toward the exit direction. ApproachPoint() falls back to
        // the truck's current position when _dock is null (the give-up case), so this degrades to
        // "curve straight from wherever it's parked toward the gate" with no special-casing needed.
        Vector3 ap   = ApproachPoint();
        Vector3 next = _gateLeaveNoTurn ?? _exitWaypoint ?? ap;
        SetTargetCurved(TruckState.DepartToApproach, ap, next - ap);
    }

    /// <summary>Fallback handling for the rare case a truck clears the gate with no door AND no
    /// configured place to wait at all (see BeginDoorWait) — a level-authoring gap, not normal
    /// gameplay. The unified 8-hour dwell clock (UpdateInboundDwellClock/ResolveInboundDeadline) is
    /// what now judges every ordinary "couldn't get a door in time" case. No vendor standing penalty
    /// here per Tad's explicit ask — this is a level-authoring gap, not something the vendor did.</summary>
    private void ApplyNoWaitSpotPenalty()
    {
        if (AssignedShipment == null) return;

        if (GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService dockSchedule) &&
            dockSchedule != null)
        {
            var appt = dockSchedule.FindForPo(AssignedShipment.PONumber);
            if (appt != null)
                dockSchedule.TryPark(appt.Id, out _);
        }

        UIToast.Show($"No door or wait spot available for PO {AssignedShipment.PONumber} — the driver " +
                     "turned around. Reschedule the appointment.");
        SystemsLogWindow.LogWarning($"{LogLinks.Po(AssignedShipment.PONumber)} turned around immediately — no door " +
                                     "or wait spot was available.");
    }

    /// <summary>Rewards vendor standing when an inbound trailer gets a door (whether immediately at
    /// spawn, via AssignAndGo, or after a real wait, via UpdateWaitingForDoor's success branch) before
    /// the unified 8-hour dwell clock ever dings it. Per Tad's spec: "getting drivers out on time will
    /// always increase our standing."</summary>
    private void ApplyOnTimeDoorBonus()
    {
        if (_isOutbound || AssignedShipment == null || string.IsNullOrEmpty(AssignedShipment.SupplierId))
            return;

        if (GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.VendorEconomyService economy) &&
            economy != null)
        {
            economy.AdjustPartnershipLevel(AssignedShipment.SupplierId, 5,
                $"PO {AssignedShipment.PONumber} got a door before the dwell clock penalized it");
        }

        string gotDoor = _dock != null ? $" — {LogLinks.Door(_dock.DoorNumber, AssignedShipment.PONumber)}" : "";
        SystemsLogWindow.LogGuard($"{LogLinks.Po(AssignedShipment.PONumber)} got a door on time{gotDoor} — vendor standing improved.");
    }

    /// <summary>The warehouse's first "Standard" (Tad's naming) — a task-level time allowance the
    /// player can beat or miss. This one covers the whole offload: 1 hour flat plus
    /// OffloadStandardMinutesPerPallet per pallet, measured from the moment the driver is actually AT
    /// the door (<paramref name="dockedAtSimMinute"/>, OnDocked's stamp — the ~1 hour it takes to get
    /// there isn't part of this clock) to right now, which BeginDeparture only reaches once
    /// CompleteOffload has fired — i.e. the instant the last pallet comes off the trailer.
    /// Returns true when the Standard was beaten, so BeginDeparture knows to skip the plain
    /// departing banner in favor of the driver's happy line, and to fire the world-space reward text
    /// once the doors actually close.</summary>
    private bool ApplyOffloadStandardBonus(long dockedAtSimMinute)
    {
        if (_isOutbound || AssignedShipment == null || string.IsNullOrEmpty(AssignedShipment.SupplierId))
            return false;
        if (dockedAtSimMinute < 0) return false;

        var gameCtx = Ctx;
        if (gameCtx == null) return false;

        float elapsedMinutes = gameCtx.TimeService.TotalMinutesElapsed - dockedAtSimMinute;
        float allowedMinutes = OffloadStandardBaseMinutes +
            OffloadStandardMinutesPerPallet * Mathf.Max(1, TotalPalletsAtDock);
        if (elapsedMinutes > allowedMinutes) return false;

        if (GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.VendorEconomyService economy) &&
            economy != null)
        {
            economy.AdjustPartnershipLevel(AssignedShipment.SupplierId, OffloadStandardBonusPoints,
                $"PO {AssignedShipment.PONumber} — offloaded in {elapsedMinutes:0} of {allowedMinutes:0} " +
                "min (beat the offload Standard)");
        }

        string atDoor = _dock != null ? LogLinks.Door(_dock.DoorNumber, AssignedShipment.PONumber) : "the door";
        SystemsLogWindow.LogSystem($"{LogLinks.Po(AssignedShipment.PONumber)} beat the offload Standard — " +
            $"{elapsedMinutes:0} of {allowedMinutes:0} min at {atDoor}. Vendor standing +{OffloadStandardBonusPoints}!");

        if (_doorWaitBar == null) _doorWaitBar = gameObject.AddComponent<TruckDoorWaitBar>();
        _doorWaitBar.ShowOnTimeDeparture(OffloadStandardBonusPoints);

        _pendingOffloadStandardFx = true;
        return true;
    }

    /// <summary>
    /// Unified 8-hour inbound dwell clock (2026-09-10, Tad's ladder spec). Ticks every frame for every
    /// live inbound truck, independent of _state — queued, waiting for a door, or already docked all
    /// count identically, because the promise being measured is "how long has this driver been sitting
    /// in our yard," not "how long has he had a door." Started at GuardClearedToEnter; lazily
    /// re-initialized here for a truck restored mid-wait (a save doesn't persist the start minute —
    /// same forgiving-default reasoning the old _doorWaitStartSimMinute restore path used: costs a few
    /// extra in-game hours of grace at worst, never a permanently-stuck penalty).
    ///
    /// Ladder: 0-2h green ("I hope this drop goes smooth..."), 2h -3 vendor / yellow ("Still here, at
    /// this clown factory."), 4h -5 vendor / orange ("This ain't worth it..."), 6h -10 vendor / red
    /// (same orange line — Tad didn't give red its own), 8h -15 vendor AND the load is lost outright —
    /// but ONLY if nothing has come off the trailer yet (see ResolveInboundDeadline). A truck already
    /// mid-unload is left alone by this method; DockScheduleService's own 8-hour "no full receipt"
    /// deadline (measured from the appointment, not the gate) still governs that case independently.
    /// </summary>
    /// <summary>When the dwell clock starts: clearing the gate, but never before the PO's booked slot opens. Inbound trucks
    /// are sent a block early so they are at the dock when the slot starts (ShipmentService.DispatchDueShipments); waiting
    /// for their own slot is not the player's fault (2026-10-22).</summary>
    private long InboundClockStart(long now)
    {
        if (AssignedShipment != null &&
            GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService schedule) && schedule != null)
        {
            var appt = schedule.FindForPo(AssignedShipment.PONumber);
            if (appt != null && !appt.Parked)
            {
                long slotStart = (long)(appt.Day - 1) * 24 * 60 +
                                 appt.BlockIndex * GameCore.Inventory.DockScheduleService.BlockMinutes;
                if (slotStart > now) return slotStart;
            }
        }
        return now;
    }

    private void UpdateInboundDwellClock()
    {
        if (_isOutbound || AssignedShipment == null) return;
        if (_state == TruckState.Queuing || _state == TruckState.GuardCheck) return; // not through the gate yet
        if (_state == TruckState.Exiting || _state == TruckState.Idle) return;
        if (_inboundPenaltyTierApplied >= 4) return; // already resolved — nothing left to tick
        // The dwell ladder punishes a trailer left waiting with NOTHING offloaded. Once a dock stocker has claimed
        // it (or finished, or the truck is pulling away) the wait is over — the clock kept running and charged
        // "waited 2 hours with nothing offloaded" right after the same PO earned "beat the offload Standard"
        // (2026-10-17 soak test, PO IG4160).
        if (_offloadClaimed || _offloadComplete || IsLeaving || IsDeparting) return;

        var gameCtx = Ctx;
        if (gameCtx == null || gameCtx.TimeService == null) return;

        if (_inboundClockStartSimMinute < 0)
            _inboundClockStartSimMinute = gameCtx.TimeService.TotalMinutesElapsed;   // restored mid-wait: count from now
        if (CheckInSimMinute < 0) CheckInSimMinute = _inboundClockStartSimMinute;   // not saved: a restored truck is past the gate

        float elapsedHours = (gameCtx.TimeService.TotalMinutesElapsed - _inboundClockStartSimMinute) / 60f;
        float frac = Mathf.Clamp01(1f - elapsedHours / InboundDeadlineHours);

        // Countdown text (Tad, 2026-09-23): how long until the next hit lands and what it costs; in the
        // red, how long until the driver leaves and what's lost. The inbound hits at 2/4/6h are vendor
        // standing, not dollars — there's no dollar fine on this side to quote.
        Color color;
        string quote;
        int MinutesUntil(float hours) => Mathf.Max(0, Mathf.CeilToInt((hours - elapsedHours) * 60f));
        if (elapsedHours < InboundTier1Hours)
        {
            color = TruckDoorWaitBar.DwellGreenColor;
            quote = $"{MinutesUntil(InboundTier1Hours)} minutes until -{InboundTier1Penalty} vendor standing";
        }
        else if (elapsedHours < InboundTier2Hours)
        {
            color = TruckDoorWaitBar.DwellYellowColor;
            quote = $"{MinutesUntil(InboundTier2Hours)} minutes until -{InboundTier2Penalty} vendor standing";
        }
        else if (elapsedHours < InboundTier3Hours)
        {
            color = TruckDoorWaitBar.DwellOrangeColor;
            quote = $"{MinutesUntil(InboundTier3Hours)} minutes until -{InboundTier3Penalty} vendor standing";
        }
        else
        {
            color = TruckDoorWaitBar.DwellRedColor;
            // The load is only lost if nothing has come off yet (see ResolveInboundDeadline).
            bool anyOffloaded = LoadContainer != null && TotalPalletsAtDock > 0 && LoadContainer.childCount < TotalPalletsAtDock;
            quote = anyOffloaded
                ? $"{MinutesUntil(InboundDeadlineHours)} minutes until -{InboundFinalPenalty} vendor standing"
                : $"{MinutesUntil(InboundDeadlineHours)} minutes Driver leaves! ${AssignedShipment.TotalCost:N0} Lost in purchased goods";
        }

        if (_doorWaitBar == null) _doorWaitBar = gameObject.AddComponent<TruckDoorWaitBar>();
        _doorWaitBar.ShowDwellStatus(frac, color, quote);

        // Ladder rungs — sequential ifs (not else-if) so a big elapsed jump (e.g. a time-scale skip)
        // fires every rung it passed through in one tick rather than only the highest.
        if (_inboundPenaltyTierApplied < 1 && elapsedHours >= InboundTier1Hours)
        {
            _inboundPenaltyTierApplied = 1;
            ApplyDwellPenalty(InboundTier1Penalty, InboundTier1Hours);
        }
        if (_inboundPenaltyTierApplied < 2 && elapsedHours >= InboundTier2Hours)
        {
            _inboundPenaltyTierApplied = 2;
            ApplyDwellPenalty(InboundTier2Penalty, InboundTier2Hours);
        }
        if (_inboundPenaltyTierApplied < 3 && elapsedHours >= InboundTier3Hours)
        {
            _inboundPenaltyTierApplied = 3;
            ApplyDwellPenalty(InboundTier3Penalty, InboundTier3Hours);
        }
        if (_inboundPenaltyTierApplied < 4 && elapsedHours >= InboundDeadlineHours)
        {
            _inboundPenaltyTierApplied = 4;
            ResolveInboundDeadline();
        }
    }

    /// <summary>One rung of the dwell-clock ladder — a vendor-relationship hit only, no dollar cost
    /// and no cancellation. Fires once per rung, in order, from UpdateInboundDwellClock.</summary>
    private void ApplyDwellPenalty(int amount, float hourMark)
    {
        if (AssignedShipment == null) return;

        string doorLabel = _dock != null ? $"Door {_dock.DoorNumber}" : "the yard";

        if (GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.VendorEconomyService economy) &&
            economy != null && !string.IsNullOrEmpty(AssignedShipment.SupplierId))
        {
            economy.AdjustPartnershipLevel(AssignedShipment.SupplierId, -amount,
                $"PO {AssignedShipment.PONumber} has waited {hourMark:F0} hours at {doorLabel} with nothing offloaded");
        }

        // Hyperlinked: the PO opens its Scheduler block, the door flies the camera to this truck.
        string doorLink = _dock != null ? LogLinks.Door(_dock.DoorNumber, AssignedShipment.PONumber) : doorLabel;
        SystemsLogWindow.LogWarning($"{LogLinks.Po(AssignedShipment.PONumber)} at {doorLink} has been waiting " +
                                     $"{hourMark:F0} hours — vendor standing took a -{amount} hit.");
    }

    /// <summary>The 8-hour mark: the driver is done waiting. If nothing has come off the trailer yet,
    /// the load is lost outright (no refund — already paid at dispatch) and the vendor relationship
    /// takes the ladder's biggest hit. A truck already mid-unload is left alone — the door/lane it's
    /// occupying is doing real work, and DockScheduleService's own full-receipt deadline (a separate
    /// 8-hour clock measured from the appointment) is what judges that case.</summary>
    private void ResolveInboundDeadline()
    {
        if (AssignedShipment == null) return;

        bool anyOffloaded = LoadContainer != null && TotalPalletsAtDock > 0 &&
                             LoadContainer.childCount < TotalPalletsAtDock;
        if (anyOffloaded) return;

        int lostValue = AssignedShipment.TotalCost;
        string po = AssignedShipment.PONumber;
        string doorLabel = _dock != null ? $"Door {_dock.DoorNumber}" : "the yard";

        if (GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.VendorEconomyService economy) &&
            economy != null && !string.IsNullOrEmpty(AssignedShipment.SupplierId))
        {
            economy.AdjustPartnershipLevel(AssignedShipment.SupplierId, -InboundFinalPenalty,
                $"PO {po} left the yard after 8 hours — nothing was ever offloaded");
        }

        if (GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService dockSchedule) &&
            dockSchedule != null)
        {
            var appt = dockSchedule.FindForPo(po);
            if (appt != null)
                dockSchedule.TryPark(appt.Id, out _);
        }

        AssignedShipment.Status = GameCore.Inventory.ShipmentData.ShipmentStatus.Cancelled;

        if (_doorWaitBar == null) _doorWaitBar = gameObject.AddComponent<TruckDoorWaitBar>();
        _doorWaitBar.ShowDwellStatus(0f, TruckDoorWaitBar.DwellRedColor, "Is Biden running this shitshow?!?");

        UIToast.Show($"PO {po} left the yard — 8 hours passed with nothing offloaded. Lost the load " +
                     $"(${lostValue:N0}, no refund) and took a -{InboundFinalPenalty} vendor hit.");
        string doorLinkLeft = _dock != null ? LogLinks.Door(_dock.DoorNumber, po) : doorLabel;
        SystemsLogWindow.LogWarning($"{LogLinks.Po(po)} at {doorLinkLeft} waited 8 hours and left — sorry boss, " +
                                     $"that's a lost load worth ${lostValue:N0} and a vendor hit of " +
                                     $"-{InboundFinalPenalty}.");

        ForceLeaveNow();
    }

    /// <summary>Abandons whatever the truck is doing and heads for the gate — used only by
    /// ResolveInboundDeadline, which unlike ForceDeparture (Docked-only) must also cover a truck still
    /// queued, parked in the side lot, or waiting for a door. Does NOT release a held dock up front —
    /// same reasoning as BeginDeparture: this truck (if docked) is still physically in the stall and
    /// needs to drive out first, so the door frees itself once DepartToApproach actually arrives.</summary>
    private void ForceLeaveNow()
    {
        if (_state == TruckState.Docked)
        {
            _dockedAtSimMinute = -1;
            SetDockedGhost(false);
            CloseTrailerDoors();
            Vector3 apDocked = ApproachPoint();
            Vector3 nextDocked = _gateLeaveNoTurn ?? _exitWaypoint ?? apDocked;
            SetTargetCurved(TruckState.DepartToApproach, apDocked, nextDocked - apDocked);
            return;
        }

        if (_sideLotSlot != null)
        {
            if (_sideLotSlot.Entry != null)
            {
                SetTarget(TruckState.ReversingToSideLotEntry, _sideLotSlot.Entry.position);
                return;
            }
            if (_gateEnterNoTurn.HasValue)
            {
                SetTarget(TruckState.ReversingOutOfSideLot, _gateEnterNoTurn.Value);
                return;
            }
        }

        SetDockedGhost(false);
        CloseTrailerDoors();
        Vector3 ap = ApproachPoint();
        Vector3 next = _gateLeaveNoTurn ?? _exitWaypoint ?? ap;
        SetTargetCurved(TruckState.DepartToApproach, ap, next - ap);
    }

    /// <summary>
    /// Swap the assigned trailer renderers to the ghost/see-through wall material while docked, and
    /// restore their originals on departure. Handles multi-slot renderers (every submesh slot is
    /// pointed at the ghost material) and is idempotent, so re-calling with the same state is a no-op.
    /// </summary>
    private void SetDockedGhost(bool ghosted)
    {
        if (_ghostWhileDockedRenderers == null) return;

        foreach (var r in _ghostWhileDockedRenderers)
        {
            if (r == null) continue;
            // The vendor/customer logo decals stay solid so you can still see who the trailer belongs to.
            if (r.name.StartsWith("SavageDecal")) continue;

            if (ghosted)
            {
                if (_ghostMaterial == null || _originalMaterials.ContainsKey(r)) continue;
                _originalMaterials[r] = r.sharedMaterials;

                var ghosts = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ghosts.Length; i++) ghosts[i] = _ghostMaterial;
                SplitOutSolidFloor(r);
                r.sharedMaterials = ghosts;
            }
            else if (_originalMaterials.TryGetValue(r, out var original))
            {
                RestoreSolidFloor(r);
                r.sharedMaterials = original;
                _originalMaterials.Remove(r);
            }
        }
    }

    private static readonly Dictionary<Mesh, (Mesh noFloor, Mesh floor)> s_floorSplits = new();
    private readonly Dictionary<Renderer, (Mesh original, GameObject floorGO)> _floorSplit = new();

    /// <summary>Keeps the trailer's floor solid while the rest of the shell is ghosted: the floor's big up-facing
    /// triangles are cut out of the ghosted mesh and drawn by a child renderer using the original material.</summary>
    private void SplitOutSolidFloor(Renderer r)
    {
        if (r.name != "TrailerMesh" || _floorSplit.ContainsKey(r)) return;
        var mf = r.GetComponent<MeshFilter>();
        var src = mf != null ? mf.sharedMesh : null;
        if (src == null || !src.isReadable || src.subMeshCount != 1) return;

        if (!s_floorSplits.TryGetValue(src, out var split) || split.noFloor == null || split.floor == null)
        {
            var v = src.vertices; var n = src.normals; var t = src.triangles;
            var floorTris = new List<int>(); var restTris = new List<int>();
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                var nn = (n[t[i]] + n[t[i + 1]] + n[t[i + 2]]).normalized;
                bool floor = nn.y > 0.95f && Vector3.Cross(b - a, c - a).magnitude * 0.5f > 4f;
                var dst = floor ? floorTris : restTris;
                dst.Add(t[i]); dst.Add(t[i + 1]); dst.Add(t[i + 2]);
            }
            if (floorTris.Count == 0) return;

            Mesh Build(string suffix, List<int> tris)
            {
                var m = Object.Instantiate(src);
                m.name = src.name + suffix;
                m.hideFlags = HideFlags.HideAndDontSave;
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            }
            split = (Build("_NoFloor", restTris), Build("_Floor", floorTris));
            s_floorSplits[src] = split;
        }
        if (split.noFloor == null) return;

        var go = new GameObject("TrailerFloorSolid");
        go.transform.SetParent(r.transform, false);
        go.transform.localPosition = new Vector3(0f, -0.3f, 0f);   // Tad 2026-11-20: the solid floor sits 0.3 m lower
        go.AddComponent<MeshFilter>().sharedMesh = split.floor;
        var fr = go.AddComponent<MeshRenderer>();
        fr.sharedMaterials = r.sharedMaterials;
        fr.shadowCastingMode = r.shadowCastingMode;
        mf.sharedMesh = split.noFloor;
        _floorSplit[r] = (src, go);
    }

    private void RestoreSolidFloor(Renderer r)
    {
        if (!_floorSplit.TryGetValue(r, out var s)) return;
        var mf = r.GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = s.original;
        if (s.floorGO != null) Destroy(s.floorGO);
        _floorSplit.Remove(r);
    }

    private void StartExiting()
    {
        if (_exitWaypoint.HasValue)
            SetTarget(TruckState.ToExit, _exitWaypoint.Value);
        else
        {
            _state = TruckState.Idle;
            StartCoroutine(ShrinkAndDestroy(exitShrinkTime));
        }
    }

    // ── Trailer doors ─────────────────────────────────────────────────────────────
    /// <summary>Passenger-side rear trailer door (the guard faces this during inspection).</summary>
    public Transform PassengerDoor => _passDoor;

    public void OpenTrailerDoors()  {
        Debug.Log($"[TruckController.OpenTrailerDoors] Opening doors");
        _doorsOpen = true;
    }
    public void CloseTrailerDoors() {
        Debug.Log($"[TruckController.CloseTrailerDoors] Closing doors");
        _doorsOpen = false;
    }

    private void UpdateDoors()
    {
        if (_driverDoor == null || _passDoor == null) return;

        float targetDriver = _doorsOpen ? driverDoorOpenAngle : 0f;
        float targetPass   = _doorsOpen ? passengerDoorOpenAngle : 0f;

        Quaternion drRot = Quaternion.Euler(0, targetDriver, 0);
        Quaternion paRot = Quaternion.Euler(0, targetPass, 0);

        _driverDoor.localRotation = Quaternion.RotateTowards(_driverDoor.localRotation, drRot, doorOpenSpeed * Time.deltaTime);
        _passDoor.localRotation   = Quaternion.RotateTowards(_passDoor.localRotation, paRot, doorOpenSpeed * Time.deltaTime);
    }

    // ── Teardown ──────────────────────────────────────────────────────────────────
    private void OnDestroy()
    {
        if (_dock != null)
        {
            _dock.Release();
            _dock = null;
        }
        if (_onExited != null)
        {
            _onExited.Invoke();
            _onExited = null;
        }
    }

    private IEnumerator ShrinkAndDestroy(float duration)
    {
        Vector3 startScale = transform.localScale;
        float   elapsed    = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / duration);
            yield return null;
        }
        _onExited?.Invoke();
        Destroy(gameObject);
    }

    // Visualizes trailerRearAxleLocalOffset in the Scene view as a small X (a cross of two diagonal
    // lines) so it's easy to eyeball against the actual rear tandem axle on the mesh and re-tune the
    // field if this rig's dimensions ever change.
    private void OnDrawGizmosSelected()
    {
        Vector3 axleWorld = transform.position + transform.rotation * trailerRearAxleLocalOffset;
        float s = 0.5f;
        Vector3 right = transform.right * s;
        Vector3 fwd = transform.forward * s;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(axleWorld - right - fwd, axleWorld + right + fwd);
        Gizmos.DrawLine(axleWorld - right + fwd, axleWorld + right - fwd);
    }
}
