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
///   → drive straight to the door (DockPositionFor(_dock)), then snap-rotate to face
///     away from it (DockRotationFor(_dock)) — an instant flip, not a reverse curve.
///     (These were DockSlot.DockPosition/DockRotation until DockSlot's own waypoint math was
///     stripped 2026-09-05; the formulas now live here as legacyDock* fallbacks.)
///     Deliberately simple per Tad's call after the curved pull-in/reverse maneuver
///     kept misreading as jerky no matter how it was tuned: driving straight in and
///     flipping to "backed in" looks like a jump cut, but it's simple and reliable.
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
/// and parks there, showing a world-space countdown (TruckDoorWaitBar). It polls
/// for a freed-up door and, if one appears, drives in exactly like a normal
/// arrival (rejoining the route above at Xform 2). If doorWaitMinutes (60 by
/// default, in SIM minutes) elapses with nothing freeing up, it gives up — still
/// takes the late penalty (appointment parked back to the pool + vendor
/// Partnership −20) — and exits via the normal departure route.
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
                                 // Entry (transform.position translation only, no rotation change,
                                 // same idiom as ReversingToTruckNavPoint3), then hands off to
                                 // FacingTruckNavPoint0 with the assigned dock's own TruckNavPoint0
                                 // as target — skips re-entering through the gate entirely

        // Backing-maneuver rebuild (2026-09), built one leg at a time. DevCheckpoint_GateEnterNoTurn
        // was step 1's dead-end (stop at GateEnterNoTurn); step 2 moved the dead-end further out to
        // DevCheckpoint_FacingNav1 (drive to TruckNavPoint0, turn to face TruckNavPoint1). The old
        // enum member is kept (unused) rather than renumbered — cheap insurance, no real cost.
        DevCheckpoint_GateEnterNoTurn,
        FacingTruckNavPoint0,   // rotating in place at GateEnterNoTurn to face TruckNavPoint0
        ToTruckNavPoint0,       // GateEnterNoTurn → TruckNavPoint0 (facing it already, no turning needed)
        FacingTruckNavPoint1,   // rotating in place at TruckNavPoint0 to face TruckNavPoint1
        DevCheckpoint_FacingNav1, // retired dead-end — step 3 moved it on to ToTruckNavPoint1/DevCheckpoint_AtNav1
        ToTruckNavPoint1,       // TruckNavPoint0 → TruckNavPoint1 (facing it already, no turning needed)
        DevCheckpoint_AtNav1,   // retired dead-end — step 5 moved it on to ToTruckNavPoint2/etc
        ToTruckNavPoint2,       // TruckNavPoint1 → TruckNavPoint2, arcing around TruckPivot0
        DevCheckpoint_AtNav2,   // retired dead-end — step 7 moved it on to FacingTruckNavPoint3/etc
        FacingTruckNavPoint3,   // at TruckNavPoint2: slowly swivel ONLY the tractor mesh (cosmetic —
                                // root/trailer heading is untouched) to visually face TruckNavPoint3
        DevCheckpoint_TractorFacingNav3, // retired dead-end — step 8 moved it on to ToTruckNavPoint3/etc
        ToTruckNavPoint3,       // TruckNavPoint2 → TruckNavPoint3 (root rebased onto the tractor's
                                // already-swiveled facing first, so there's no pop when driving starts)
        FacingTruckNavPoint4,   // at TruckNavPoint3: slowly swivel ONLY the tractor mesh to face TruckNavPoint4
        DevCheckpoint_TractorFacingNav4, // retired dead-end — step 9 moved it on to ToTruckNavPoint4/etc
        ToTruckNavPoint4,       // TruckNavPoint3 → TruckNavPoint4 (root rebased onto the tractor's
                                // already-swiveled facing first, so there's no pop when driving starts)
        StraighteningAtNavPoint4, // at TruckNavPoint4: tractor ramps toward reverseArcCabAngle (+20°,
                                  // 2026-09-05 — was "back to 0°"/undo-jackknife until Tad asked for
                                  // one continuous climb to +20° across the whole TruckNavPoint3→
                                  // TruckNavPoint5 stretch instead); body/root orientation is left alone
        DevCheckpoint_StraightAtNav4,
        // Step 10 (reverse straight into the shipping door, computed target) was attempted and
        // rejected — see git history / conversation for the discarded ReversingIntoDoor/
        // DevCheckpoint_AtDoor states and ReverseToward helper.
        // Step 11: a simpler fixed-distance version — translate the whole body backward along its
        // own local reverse (-transform.forward) for a fixed distance, no target/root-rotation
        // involved, while the tractor mesh swivels to a fixed +slowReverseCabAngle local offset
        // (cosmetic only).
        ReversingStraightSlow, // retired dead-end — step 13 replaced the auto-continue out of
                               // StraighteningAtNavPoint4 with ReversingToTruckNavPoint3 below
        DevCheckpoint_ReversedSlow,
        // Step 12: continue the same backward translation (still holding the tractor at the fixed
        // cosmetic offset, no interruption) on to TruckNavPoint5, then square everything up: tractor
        // eases back to 0° local, and the whole body ALSO rotates to face the nearest of world
        // +Z/-Z exactly, finishing squared up before the checkpoint pause.
        // (Also no longer entered by normal flow as of step 13 — kept inert, not deleted, since it
        // wasn't rejected, just superseded at this branch point.)
        ReversingToTruckNavPoint5,
        StraighteningAtNavPoint5,
        DevCheckpoint_AtNavPoint5,
        // Step 13: StraighteningAtNavPoint4 now leads here instead — a target-based straight-line
        // reverse (translation only, no root rotation) from TruckNavPoint4 back to TruckNavPoint3,
        // while the tractor mesh gradually eases toward +reverseToNav3CabAngle (RotateTowards via
        // cabSwivelSpeed — genuinely gradual over the whole leg, unlike the earlier holds-a-fixed-
        // offset-immediately legs) so it reads as a slow correction rather than a snap.
        ReversingToTruckNavPoint3,
        DevCheckpoint_AtNavPoint3Reverse,

        // Step 14 (2026-09-05, Tad's spec): replaces the dead-simple ToApproach→ToBackup
        // straight-in-then-flip with a real backing pivot, reusing the door's existing
        // TruckPivot1/TruckNavPoint5 BackInSystem markers (previously only used by the retired
        // NavPoint0-4 rebuild chain above). ToApproach now hands off into these two states instead
        // of ToBackup; ToBackup itself is untouched and still serves as the fallback if a door is
        // ever missing these markers.
        ReversingArcAroundPivot1, // backing around TruckPivot1 at a constant radius toward
                                   // TruckNavPoint5, tractor swiveling out to reverseArcCabAngle
        StraighteningIntoDoor,    // final straight-line backward leg onto the dock offset; tractor
                                   // eases to 0° and the body eases to DockRotationFor(_dock)
                                   // (facing away from the door) — finishes by handing off to
                                   // Docked/OnDocked exactly like ToBackup always has.

        // Step 15 (2026-09-05, Tad's spec): the tractor now holds a cosmetic countersteer angle
        // (nav1ApproachCabAngle) from just before TruckNavPoint1 through the entire TruckNavPoint1→
        // TruckNavPoint2 arc (previously that whole stretch left the cab to the generic reactive
        // auto-steer). This state is the "slowly ease back to 0°" tail once TruckNavPoint2 is
        // reached, before FacingTruckNavPoint3 takes over cab control for its own purpose (looking
        // at TruckNavPoint3) — same "drive leg, then a dedicated ease-the-cab state" shape as
        // StraighteningAtNavPoint4/StraighteningAtNavPoint5 elsewhere in this chain.
        StraighteningAtNavPoint2,

        // Step 16 (2026-09-06, Tad's spec): FacingTruckNavPoint1 used to finish by instantly
        // snapping the root's rotation to match the cab's now-fixed heading — confirmed-live as a
        // jarring 1-frame teleport of the whole trailer. This state replaces that snap: the
        // tractor's world heading is locked (it does NOT rotate again), the rig is towed forward
        // at the regular driveSpeed along that fixed heading, and the root/trailer gradually
        // RotateTowards-catches up to it — the cab's local rotation is recomputed every frame so its WORLD heading stays
        // pinned even as the root rotates underneath it. Hands off to ToTruckNavPoint1 once the
        // trailer's rotation is close enough to match.
        TrailerAligningToNavPoint1
    }

    [Header("Backing maneuver (2026-09 rebuild)")]
    [Tooltip("Radius (meters) the truck holds from TruckPivot0 while arcing from TruckNavPoint1 to TruckNavPoint2.")]
    [SerializeField] private float arcRadius = 9.25f;
    [Tooltip("Degrees/second the tractor mesh (cosmetic only — not the whole truck) swivels to face TruckNavPoint3. Deliberately much slower than driveTurnSpeed so it reads as a slow, intentional look-and-line-up rather than a snap.")]
    [SerializeField] private float cabSwivelSpeed = 20f;
    [Tooltip("Meters the whole body translates straight backward (along its own -transform.forward) during ReversingStraightSlow.")]
    [SerializeField] private float slowReverseDistance = 2f;
    [Tooltip("Meters/second the body moves during ReversingStraightSlow — deliberately much slower than driveSpeed so it reads as a slow, careful backing move.")]
    [SerializeField] private float slowReverseSpeed = 1.5f;
    [Tooltip("Meters/second while pulling forward from the SideLot's Entry marker into its Anchor (ToSideLotAnchor) — slower than driveSpeed so the final rotate-and-line-up reads as a careful pull-in, not a normal drive.")]
    [SerializeField] private float sideLotPullInSpeed = 3f;
    [Tooltip("Degrees the tractor mesh (cosmetic only) is turned to, locally, relative to the trailer, while ReversingStraightSlow/ReversingToTruckNavPoint5 are underway.")]
    [SerializeField] private float slowReverseCabAngle = 10f;
    [Tooltip("Degrees the tractor mesh (cosmetic only) holds while driving from TruckNavPoint2 to TruckNavPoint3 — negative reads as countersteering into the upcoming line-up.")]
    [SerializeField] private float nav3ApproachCabAngle = -10f;
    [Tooltip("Meters of remaining distance to TruckNavPoint3 at which the tractor starts easing back to 0° instead of holding nav3ApproachCabAngle.")]
    [SerializeField] private float nav3StraightenDistance = 4f;
    [Tooltip("Degrees the tractor mesh (cosmetic only) gradually eases toward, locally, while reversing straight from TruckNavPoint4 back to TruckNavPoint3.")]
    [SerializeField] private float reverseToNav3CabAngle = 10f;

    [Tooltip("Degrees the tractor mesh (cosmetic only) holds from just before TruckNavPoint1 through the whole TruckNavPoint1→TruckNavPoint2 arc (Tad's spec: -12.5°).")]
    [SerializeField] private float nav1ApproachCabAngle = -12.5f;
    [Tooltip("Meters of remaining distance to TruckNavPoint1 at which the tractor starts easing toward nav1ApproachCabAngle instead of holding rest.")]
    [SerializeField] private float nav1CabEngageDistance = 4f;

    [Tooltip("Degrees/second the trailer (root) rotates to catch up with the tractor's fixed heading during TrailerAligningToNavPoint1. Slower than driveTurnSpeed so it reads as being towed into alignment, not snapping.")]
    [SerializeField] private float trailerCatchUpTurnSpeed = 60f;

    [Tooltip("Degrees the tractor mesh (cosmetic only) cranks out to, locally, while backing around TruckPivot1 toward TruckNavPoint5 (Tad's spec: +20°).")]
    [SerializeField] private float reverseArcCabAngle = 20f;

    [Tooltip("Meters/second the effective arc radius is allowed to close toward arcRadius at the start of ReversingArcAroundPivot1 (the truck's actual entry distance from TruckPivot1 doesn't match arcRadius — TruckNavPoint4 isn't on that circle). Fast enough to close a 2-3m gap in under a second without an instant snap.")]
    [SerializeField] private float radiusCorrectionSpeed = 4f;

    // Distance already covered this ReversingStraightSlow leg — reset by BeginReverseStraightSlow.
    private float _slowReverseTraveled;

    // Actual truck→TruckPivot1 distance at the moment ReversingArcAroundPivot1 begins — NOT the same
    // as arcRadius (which is tuned to TruckPivot1→TruckNavPoint5's distance). Eases toward arcRadius
    // over the arc (radiusCorrectionSpeed) rather than snapping to it — see
    // UpdateReversingArcAroundPivot1 for why holding the raw entry radius for the whole arc was
    // confirmed-live wrong (the truck ran 2-5m off the intended circle for its entire length).
    private float _arcRadiusActive;

    // Accumulated counterclockwise sweep for ReversingArcAroundPivot1 and the single angle (around
    // TruckPivot1, AngleAroundPivot convention) that position/heading are both derived from each
    // frame — see UpdateReversingArcAroundPivot1's doc comment for why this is angle-parametric
    // rather than tangent-step-then-reproject.
    private float _arcSweptSoFar;
    private float _arcCurrentAngle;

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
    [Tooltip("How many in-game minutes a truck waits for a door to free up before giving up and leaving (still taking the late penalty). Standard 60 per Tad's spec.")]
    [SerializeField] private float doorWaitMinutes = 60f;
    [Tooltip("Real seconds between checks for a newly-freed door while waiting.")]
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

    /// <summary>Sim-clock minute this INBOUND trailer docked, for the VENDORS tab's "Avg Hours in
    /// Door" stat — real dwell time (docked-to-departed) belongs on the sim clock, same scale as
    /// every other vendor stat, not on real wall-clock seconds like `_dockedTime`'s fallback-timeout
    /// use above. -1 while not docked/not applicable.</summary>
    private long _dockedAtSimMinute = -1;

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

    /// <summary>The dock this truck is currently backed into (null unless docked).</summary>
    public DockSlot DockedAt => _state == TruckState.Docked ? _dock : null;

    /// <summary>The Load container holding this truck's cargo pallets as children, or null.</summary>
    public Transform LoadContainer => transform.Find("Trailer/LorryTrailer/Load") ?? FindDeepChild(transform, "Load");

    /// <summary>Marks this truck as being actively offloaded so no other offloader claims it.</summary>
    public void ClaimForOffload() => _offloadClaimed = true;

    /// <summary>Called by the offload controller once every pallet is off — lets the truck depart.</summary>
    public void CompleteOffload() => _offloadComplete = true;

    /// <summary>Marks this outbound truck as being actively loaded so no other loader claims it.</summary>
    public void ClaimForLoad() => _loadClaimed = true;

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

    private long  _doorWaitStartSimMinute = -1;
    private float _doorWaitPollTimer;
    private TruckDoorWaitBar _doorWaitBar;

    /// <summary>Set while this truck is waiting in a SideLotController's claimed spot instead of the
    /// generic _doorWaitPoint — null the rest of the time. Drives the UpdateWaitingForDoor branch that
    /// reverses back out to the gate instead of curving straight to ApproachPoint(), and the "PARKED ·
    /// Side Lot" status shown in PurchasingPanel/SchedulerPanel via IsInSideLot below.</summary>
    private SideLotController _sideLot;
    /// <summary>The specific parking Slot (one of possibly several on _sideLot) this truck claimed —
    /// see SideLotController.Slot. Null whenever _sideLot is null.</summary>
    private SideLotController.Slot _sideLotSlot;
    public bool IsInSideLot => _sideLot != null;

    // World heading the tractor locked onto when it finished its cosmetic swivel in
    // FacingTruckNavPoint1 — held fixed for the whole of TrailerAligningToNavPoint1 while the
    // trailer (root) rotates to catch up to it. See that state's enum comment.
    private Quaternion  _lockedTractorHeadingNav1;

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

    // Ghost material for cargo CASES (GhostCases()) — uses the same GhostLoweredWall as docked trailer walls
    // so unreceived cases have a consistent see-through look.
    private Material _cargoGhostMaterial;
    private readonly Dictionary<Renderer, Material[]> _originalMaterials = new();

    private void Awake()
    {
        _groundY = transform.position.y;
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

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

                var sku = inventoryService?.GetSkuData(item.SkuId);
                if (BuildOnePallet(loadParent, sku, item.SkuId, item.FloorSlotIndex, item.PalletTier, i))
                    built++;
            }
        }
        else
        {
            for (int i = 0; i < PalletSlotCount; i++)
            {
                var item = shipment.LineItems[i % shipment.LineItems.Count];
                var sku = inventoryService?.GetSkuData(item.SkuId);
                if (BuildOnePallet(loadParent, sku, item.SkuId, i, 0, i))
                    built++;
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
        var gameCtx = FindAnyObjectByType<GameContext>();
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

            result.Add(snap);
        }
        return result;
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

            case TruckState.ReversingArcAroundPivot1:
                // _arcEntryAngle/_arcTargetSweptDegrees/_arcCurrentAngle/_arcRadiusActive are transient (not persisted)
                // — re-derive them from the restored transform via the same Begin helper normal
                // gameplay uses, rather than resuming with stale/zero values (which would read the
                // sweep as already complete and pop straight to StraighteningIntoDoor).
                if (_dock != null)
                {
                    BeginReversingArcAroundPivot1(); // sets _state itself (arc or ToBackup fallback)
                    return;
                }
                restoredState  = TruckState.ToBackup;
                _currentTarget = snap.worldPosition;
                _useBezier     = false;
                break;

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

            case TruckState.WaitingForDoor:
                // _doorWaitStartSimMinute is not persisted (a save mid-wait is a rare enough edge case
                // not to warrant its own snapshot field this pass) — restart a fresh full wait window
                // rather than resuming a stale one that would silently never time out (elapsed would
                // read 0 forever). Forgiving default, same reasoning as every other "field didn't exist
                // in this save" fallback in this codebase — costs the player a few extra minutes of
                // waiting at worst, never a stuck truck.
                transform.position = snap.worldPosition;
                BeginWaitingForDoor();
                return;
        }

        _state = restoredState;
        Debug.Log($"[TruckController.RestoreFromSnapshot] END - state={restoredState} door={snap.assignedDoorNumber} pallets={snap.trailerPallets?.Count ?? 0}");
        Debug.Log($"[TruckController.RestoreFromSnapshot] Final door state: _doorsOpen={_doorsOpen}, AwaitingOffload={AwaitingOffload}");
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
        var gameCtx = FindAnyObjectByType<GameContext>();
        int currentDay = gameCtx != null ? gameCtx.TimeService.Day : 0;

        for (int i = 0; i < palletSnaps.Count; i++)
        {
            var palletSnap = palletSnaps[i];
            if (palletSnap == null) continue;

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

    /// <summary>Resolves a named waypoint under the assigned dock's "BackInSystem" child (e.g.
    /// "TruckNavPoint0"/"TruckNavPoint1") — the backing-maneuver rebuild's per-door waypoints.
    /// Null if there's no dock assigned or the door doesn't have that child.</summary>
    private Transform FindDockWaypoint(string name)
    {
        if (_dock == null) return null;
        var backIn = _dock.transform.Find("BackInSystem");
        return backIn != null ? backIn.Find(name) : null;
    }

    private float _arcEntryAngle;
    private float _arcTargetSweptDegrees;

    /// <summary>Call once, right when entering ToTruckNavPoint2 — captures the angle swept needed to
    /// reach navPoint2 around TruckPivot0. A raw XZ-distance-to-target check (as used everywhere else
    /// in this file) can be skipped clean over on a circle: confirmed live, the truck swept straight
    /// past TruckNavPoint2 without ever landing inside arrivedThreshold and kept circling the full
    /// loop. Comparing swept angle instead is step-size independent — the exit fires the first frame
    /// the required sweep is reached or exceeded, no matter how big that frame's step was.</summary>
    private void BeginArcToTruckNavPoint2(Vector3 navPoint2Pos)
    {
        var pivot0 = FindDockWaypoint("TruckPivot0");
        if (pivot0 == null) return;

        _arcEntryAngle = AngleAroundPivot(transform.position, pivot0.position);
        float targetAngle = AngleAroundPivot(navPoint2Pos, pivot0.position);
        _arcTargetSweptDegrees = Mathf.Max(1f, Mathf.Abs(Mathf.DeltaAngle(_arcEntryAngle, targetAngle)));
    }

    private static float AngleAroundPivot(Vector3 worldPos, Vector3 pivotPos)
    {
        Vector3 offset = worldPos - pivotPos;
        return Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
    }

    /// <summary>TruckNavPoint1 → TruckNavPoint2, curved: keeps the truck at a constant radius from
    /// TruckPivot0 (an arc, not a straight line) instead of just driving straight there. Position is
    /// re-snapped onto the exact circle every frame (not just nudged toward it), so the radius really
    /// is constant, not merely close. Sweep direction is picked each frame as whichever of the two
    /// tangents best matches the truck's CURRENT forward — since forward itself is turned toward that
    /// same tangent every frame, the choice stays consistent turn to turn without needing a stored
    /// direction flag. On arrival, hard-snaps to world yaw 0 exactly, per spec ("a perfect 0 degrees"),
    /// rather than trusting wherever the discrete per-frame stepping happened to land.</summary>
    private void UpdateArcToTruckNavPoint2()
    {
        var pivot0 = FindDockWaypoint("TruckPivot0");
        if (pivot0 == null)
        {
            // No pivot to arc around — fall back to a straight drive rather than getting stuck.
            if (DriveToward(_currentTarget)) _state = TruckState.FacingTruckNavPoint3;
            return;
        }

        Vector3 toPivot = pivot0.position - transform.position;
        toPivot.y = 0f;
        float currentRadius = toPivot.magnitude;
        if (currentRadius < 0.01f) { _state = TruckState.FacingTruckNavPoint3; return; }
        Vector3 radialDir = toPivot / currentRadius; // truck → pivot

        Vector3 tangentA = Vector3.Cross(Vector3.up, radialDir);
        Vector3 tangent = Vector3.Dot(transform.forward, tangentA) >= 0f ? tangentA : -tangentA;

        Vector3 movedPos = transform.position + tangent * (driveSpeed * Time.deltaTime);
        Vector3 movedToPivot = pivot0.position - movedPos;
        movedToPivot.y = 0f;
        if (movedToPivot.sqrMagnitude > 0.0001f)
        {
            Vector3 onCircle = pivot0.position - movedToPivot.normalized * arcRadius;
            onCircle.y = _groundY;
            transform.position = onCircle;
        }

        if (tangent.sqrMagnitude > 0.0001f)
        {
            Quaternion desired = Quaternion.LookRotation(tangent.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, driveTurnSpeed * Time.deltaTime);
        }

        // Tractor mesh (cosmetic only): hold nav1ApproachCabAngle through the whole arc — it should
        // already be there from the tail end of ToTruckNavPoint1, this just keeps it pinned (and
        // covers the case where the leg was too short to fully ease in).
        if (_cab != null)
        {
            Quaternion desiredCabLocal = Quaternion.Euler(0f, nav1ApproachCabAngle, 0f) * _cabRest;
            _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, desiredCabLocal, cabSwivelSpeed * Time.deltaTime);
        }

        float swept = Mathf.Abs(Mathf.DeltaAngle(_arcEntryAngle, AngleAroundPivot(transform.position, pivot0.position)));
        if (swept >= _arcTargetSweptDegrees)
        {
            // Position snaps to the exact waypoint (harmless — a few mm of positional correction
            // isn't visible), but rotation is deliberately left exactly as the arc's own tangent-
            // following last set it. A hard snap to a fixed rotation here was a real, confirmed-live
            // bug: it yanked the whole truck's heading discontinuously the instant the arc finished,
            // reading as a jerk back toward the shipping door right before FacingTruckNavPoint3's
            // tractor-only swivel took over. Leaving rotation untouched makes the handoff seamless —
            // the tractor swivel continues smoothly from whatever heading the arc actually ended on.
            transform.position = new Vector3(_currentTarget.x, _groundY, _currentTarget.z);
            // Hands off to StraighteningAtNavPoint2 (eases the cab from nav1ApproachCabAngle back to
            // 0°) instead of straight to FacingTruckNavPoint3 — that state immediately drives the cab
            // toward TruckNavPoint3 instead, which would fight/skip the "slowly rotate to 0°" step.
            _state = TruckState.StraighteningAtNavPoint2;
        }
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
    private void Update()
    {
        UpdateDoors();
        UpdateCabSteering();

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
                // Xform 1 — drive to GateEnterNoTurn, then turn in place to face TruckNavPoint0
                // before driving to it (backing-maneuver rebuild, step 4). REVERTED 2026-09-05: I
                // (wrongly) short-circuited this straight to ToApproach, thinking the NavPoint0-4
                // chain below was abandoned dead code — it's actually Tad's working, verified
                // step-by-step build. Restored to the real live route; see StraighteningAtNavPoint4
                // below for where the new Pivot1/NavPoint5 maneuver now picks up instead.
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
                    // _dock null, FindDockWaypoint below always returns null too, so this used to fall
                    // through to the "no TruckNavPoint0" dead-end and the truck sat stuck at the gate
                    // forever instead of parking at the door-wait spot. Route dock-less trucks to
                    // BeginDoorWait() here instead of ever attempting dock-relative navigation.
                    if (_dock == null)
                    {
                        BeginDoorWait();
                        break;
                    }

                    var nav0 = FindDockWaypoint("TruckNavPoint0");
                    if (nav0 != null)
                    {
                        _currentTarget = nav0.position;
                        _state = TruckState.FacingTruckNavPoint0;
                    }
                    else
                    {
                        Debug.LogWarning("[TruckController] Dock has no TruckNavPoint0 (BackInSystem child) — stopping at the gate instead.");
                        _state = TruckState.DevCheckpoint_GateEnterNoTurn;
                    }
                }
                break;

            case TruckState.DevCheckpoint_GateEnterNoTurn:
                // Parked on purpose — see the enum comment. Nothing advances out of this automatically.
                break;

            case TruckState.FacingTruckNavPoint0:
            {
                Vector3 toNav0 = _currentTarget - transform.position;
                toNav0.y = 0f;
                if (toNav0.sqrMagnitude > 0.0001f)
                {
                    Quaternion desired = Quaternion.LookRotation(toNav0.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, driveTurnSpeed * Time.deltaTime);

                    if (Quaternion.Angle(transform.rotation, desired) <= 0.5f)
                    {
                        transform.rotation = desired;
                        SetTarget(TruckState.ToTruckNavPoint0, _currentTarget);
                    }
                }
                else
                {
                    // Same degenerate-vector bug as FacingTruckNavPoint1 (see its fix) — here it spins
                    // the WHOLE BODY instead of just the cab, which reads as the truck "doing laps" in
                    // place. Root already sits at (or effectively at) TruckNavPoint0's XZ position —
                    // nothing meaningful to face — so just move on instead of feeding LookRotation a
                    // near-zero vector.
                    SetTarget(TruckState.ToTruckNavPoint0, _currentTarget);
                }
                break;
            }

            case TruckState.ToTruckNavPoint0:
                if (DriveToward(_currentTarget))
                {
                    _state = TruckState.FacingTruckNavPoint1;
                }
                break;

            case TruckState.FacingTruckNavPoint1:
            {
                // Cosmetic ONLY (Tad's spec) — the TRACTOR turns to face TruckNavPoint1, root parked
                // at TruckNavPoint0 for the duration. Once it's done, ToTruckNavPoint1 snaps the ROOT
                // to this same heading and drives a pure STRAIGHT LINE — no gradual turn-while-moving
                // (that arced the whole truck out wide instead of a clean straight shot, which is what
                // read as "weird maneuvering"/endlessly re-attempting the backing leg — Tad's explicit
                // call to remove it).
                var nav1 = FindDockWaypoint("TruckNavPoint1");
                if (nav1 == null)
                {
                    Debug.LogWarning("[TruckController] Dock has no TruckNavPoint1 (BackInSystem child) — stopping here instead.");
                    _state = TruckState.DevCheckpoint_FacingNav1;
                    break;
                }

                if (_cab == null)
                {
                    SetTarget(TruckState.ToTruckNavPoint1, nav1.position);
                    break;
                }

                Vector3 toNav1 = nav1.position - transform.position;
                toNav1.y = 0f;
                if (toNav1.sqrMagnitude > 0.0001f)
                {
                    Quaternion desiredWorld = Quaternion.LookRotation(toNav1.normalized);
                    Quaternion desiredLocalFull = Quaternion.Inverse(transform.rotation) * desiredWorld;
                    // Tad: the full swing read as too dramatic — only turn halfway toward facing
                    // TruckNavPoint1, not all the way. Slerp from rest (not from wherever the cab
                    // currently sits) so "half" always means half of the full rest→target angle,
                    // regardless of which frame this happens to be evaluated on.
                    Quaternion desiredLocal = Quaternion.Slerp(_cabRest, desiredLocalFull, 0.5f);
                    _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, desiredLocal, cabSwivelSpeed * Time.deltaTime);

                    if (Quaternion.Angle(_cab.localRotation, desiredLocal) <= 0.5f)
                    {
                        _cab.localRotation = desiredLocal;
                        // BUG FIX: used to snap transform.rotation to desiredWorld here — confirmed
                        // live as a jarring 1-frame teleport of the whole trailer. The tractor's
                        // heading is done rotating and must not move again, but the trailer/root
                        // needs to swing into alignment gradually while being towed forward, not
                        // snap. TrailerAligningToNavPoint1 owns that transition.
                        _lockedTractorHeadingNav1 = desiredWorld;
                        _currentTarget = nav1.position;
                        _state = TruckState.TrailerAligningToNavPoint1;
                    }
                }
                else
                {
                    // BUG FIX: root already sits at (or effectively at) TruckNavPoint1's XZ position —
                    // confirmed live as the cause of a truck "circling repeatedly" forever in this
                    // state. There's no meaningful direction to compute a facing rotation from here,
                    // and feeding a near-zero-length vector into LookRotation is numerically unstable
                    // (tiny per-frame float noise flips the result wildly, reading as the tractor
                    // spinning in place instead of settling). Nothing to face — just move on.
                    SetTarget(TruckState.ToTruckNavPoint1, nav1.position);
                }
                break;
            }

            case TruckState.DevCheckpoint_FacingNav1:
                // Retired dead-end — see enum comment. Nothing advances out of this automatically.
                break;

            case TruckState.TrailerAligningToNavPoint1:
            {
                // The tractor's world heading (_lockedTractorHeadingNav1) is fixed — it does NOT
                // rotate again in this state, per Tad's spec ("its DONE ROTATING, keep the rotation
                // fixed... while the trailer rotates to match up with it"). The trailer (this
                // transform, the actual root) is what's still catching up: towed forward at the
                // regular driveSpeed along that fixed heading while its own rotation gradually
                // swings into line with it — the visual read is the trailer swinging into alignment behind a tractor
                // that's already pointed the right way, not an instant snap.
                //
                // BUG FIX: this state used to only hand off once the trailer's ROTATION caught up
                // to the locked heading, with no distance check of its own. Confirmed live — on a
                // short nav0→nav1 leg, moving forward the whole rotation-catch-up duration overshot
                // TruckNavPoint1 before the rotation ever finished; ToTruckNavPoint1 then inherited
                // a target that was already behind the truck and, having no way to reverse course,
                // drove on forever off the map. Distance-to-target is now checked exactly like
                // ToTruckNavPoint1 checks it, and wins over the rotation-catch-up check if the rig
                // reaches the target first.
                Vector3 flat = new Vector3(_currentTarget.x, _groundY, _currentTarget.z);
                Vector3 toTarget = flat - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude < arrivedThreshold)
                {
                    transform.position = flat;
                    transform.rotation = _lockedTractorHeadingNav1;
                    if (_cab != null) _cab.localRotation = _cabRest;
                    SetTarget(TruckState.ToTruckNavPoint1, _currentTarget);
                    break;
                }

                transform.position += (_lockedTractorHeadingNav1 * Vector3.forward) * driveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _lockedTractorHeadingNav1, trailerCatchUpTurnSpeed * Time.deltaTime);

                // Cab's LOCAL rotation is recomputed every frame from the root's current (still
                // catching-up) rotation so the cab's WORLD rotation stays pinned to
                // _lockedTractorHeadingNav1 the whole time — the tractor visually doesn't move at
                // all while the trailer swings underneath it.
                if (_cab != null)
                    _cab.localRotation = Quaternion.Inverse(transform.rotation) * _lockedTractorHeadingNav1;

                if (Quaternion.Angle(transform.rotation, _lockedTractorHeadingNav1) <= 1f)
                {
                    transform.rotation = _lockedTractorHeadingNav1;
                    if (_cab != null) _cab.localRotation = _cabRest;
                    SetTarget(TruckState.ToTruckNavPoint1, _currentTarget);
                }
                break;
            }

            case TruckState.ToTruckNavPoint1:
            {
                // Pure straight-line translation — root was already snapped to face TruckNavPoint1 in
                // FacingTruckNavPoint1 above, so no rotation happens here at all (see that state's
                // comment for why: DriveToward's gradual turn-while-moving swung the whole truck out
                // in a wide arc for this leg instead of a clean straight shot).
                Vector3 flat = new Vector3(_currentTarget.x, _groundY, _currentTarget.z);
                Vector3 toTarget = flat - transform.position;
                toTarget.y = 0f;
                bool arrived = toTarget.magnitude < arrivedThreshold;

                if (arrived)
                    transform.position = flat;
                else
                    transform.position += transform.forward * driveSpeed * Time.deltaTime;

                // Tractor mesh (cosmetic only): holds rest until within nav1CabEngageDistance of
                // TruckNavPoint1, then eases toward nav1ApproachCabAngle so it's already
                // countersteering by the time the Nav1→Nav2 arc begins (Tad's spec) — same "hold,
                // then ease near arrival" shape as nav3ApproachCabAngle's leg, just inverted
                // (rest→angle instead of angle→rest).
                if (_cab != null)
                {
                    float distRemaining = Vector3.Distance(
                        new Vector3(transform.position.x, 0f, transform.position.z),
                        new Vector3(flat.x, 0f, flat.z));
                    Quaternion desiredCabLocal = distRemaining <= nav1CabEngageDistance
                        ? Quaternion.Euler(0f, nav1ApproachCabAngle, 0f) * _cabRest
                        : _cabRest;
                    _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, desiredCabLocal, cabSwivelSpeed * Time.deltaTime);
                }

                if (arrived)
                {
                    var nav2 = FindDockWaypoint("TruckNavPoint2");
                    if (nav2 != null)
                    {
                        SetTarget(TruckState.ToTruckNavPoint2, nav2.position);
                        BeginArcToTruckNavPoint2(nav2.position);
                    }
                    else
                    {
                        Debug.LogWarning("[TruckController] Dock has no TruckNavPoint2 (BackInSystem child) — stopping here instead.");
                        _state = TruckState.DevCheckpoint_AtNav1;
                    }
                }
                break;
            }

            case TruckState.DevCheckpoint_AtNav1:
                // Retired dead-end — see enum comment. Nothing advances out of this automatically.
                break;

            case TruckState.ToTruckNavPoint2:
                UpdateArcToTruckNavPoint2();
                break;

            case TruckState.StraighteningAtNavPoint2:
            {
                // Cosmetic only, root untouched — eases the tractor from nav1ApproachCabAngle back
                // to rest "slowly" (Tad's spec) before FacingTruckNavPoint3 takes over cab control
                // for its own purpose (looking at TruckNavPoint3).
                if (_cab == null)
                {
                    _state = TruckState.FacingTruckNavPoint3;
                    break;
                }

                _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, _cabRest, cabSwivelSpeed * Time.deltaTime);

                if (Quaternion.Angle(_cab.localRotation, _cabRest) <= 0.5f)
                {
                    _cab.localRotation = _cabRest;
                    _state = TruckState.FacingTruckNavPoint3;
                }
                break;
            }

            case TruckState.FacingTruckNavPoint3:
            {
                // Cosmetic ONLY — rotates the Tractor child mesh, never transform.rotation (the
                // root, and therefore the Trailer, don't move). See UpdateCabSteering for why this
                // state has to fully bypass that system rather than fight it over _cab.localRotation.
                if (_cab == null)
                {
                    BeginDriveToTruckNavPoint3();
                    break;
                }

                var nav3 = FindDockWaypoint("TruckNavPoint3");
                if (nav3 == null)
                {
                    Debug.LogWarning("[TruckController] Dock has no TruckNavPoint3 (BackInSystem child) — stopping here instead.");
                    _state = TruckState.DevCheckpoint_TractorFacingNav3;
                    break;
                }

                Vector3 toNav3 = nav3.position - transform.position;
                toNav3.y = 0f;
                if (toNav3.sqrMagnitude > 0.0001f)
                {
                    Quaternion desiredWorld = Quaternion.LookRotation(toNav3.normalized);
                    Quaternion desiredLocal = Quaternion.Inverse(transform.rotation) * desiredWorld;
                    _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, desiredLocal, cabSwivelSpeed * Time.deltaTime);

                    if (Quaternion.Angle(_cab.localRotation, desiredLocal) <= 0.5f)
                    {
                        _cab.localRotation = desiredLocal;
                        BeginDriveToTruckNavPoint3();
                    }
                }
                else
                {
                    // Same degenerate-vector bug as FacingTruckNavPoint1 — root already sits at (or
                    // effectively at) TruckNavPoint3's XZ, nothing meaningful to face. Move on instead
                    // of feeding LookRotation a near-zero vector.
                    BeginDriveToTruckNavPoint3();
                }
                break;
            }

            case TruckState.DevCheckpoint_AtNav2:
            case TruckState.DevCheckpoint_TractorFacingNav3:
                // Retired dead-ends — see enum comments. Nothing advances out of these automatically.
                break;

            case TruckState.ToTruckNavPoint3:
            {
                bool arrived = DriveToward(_currentTarget);

                // Tractor mesh only (cosmetic) — holds nav3ApproachCabAngle for most of the leg,
                // then eases back to 0° once within nav3StraightenDistance of TruckNavPoint3 so it's
                // square again by arrival. Root/trailer steering (DriveToward, above) is untouched.
                if (_cab != null)
                {
                    Vector3 flat = new Vector3(_currentTarget.x, _groundY, _currentTarget.z);
                    float distRemaining = Vector3.Distance(
                        new Vector3(transform.position.x, 0f, transform.position.z),
                        new Vector3(flat.x, 0f, flat.z));
                    Quaternion desiredCabLocal = distRemaining <= nav3StraightenDistance
                        ? _cabRest
                        : Quaternion.Euler(0f, nav3ApproachCabAngle, 0f) * _cabRest;
                    _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, desiredCabLocal, cabSwivelSpeed * Time.deltaTime);
                }

                if (arrived)
                {
                    if (_cab != null) _cab.localRotation = _cabRest;
                    BeginFacingTruckNavPoint4();
                }
                break;
            }

            case TruckState.FacingTruckNavPoint4:
            {
                if (_cab == null)
                {
                    BeginDriveToTruckNavPoint4();
                    break;
                }

                var nav4 = FindDockWaypoint("TruckNavPoint4");
                if (nav4 == null)
                {
                    Debug.LogWarning("[TruckController] Dock has no TruckNavPoint4 (BackInSystem child) — stopping here instead.");
                    _state = TruckState.DevCheckpoint_TractorFacingNav4;
                    break;
                }

                Vector3 toNav4 = nav4.position - transform.position;
                toNav4.y = 0f;
                if (toNav4.sqrMagnitude > 0.0001f)
                {
                    Quaternion desiredWorld = Quaternion.LookRotation(toNav4.normalized);
                    Quaternion desiredLocal = Quaternion.Inverse(transform.rotation) * desiredWorld;
                    _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, desiredLocal, cabSwivelSpeed * Time.deltaTime);

                    if (Quaternion.Angle(_cab.localRotation, desiredLocal) <= 0.5f)
                    {
                        _cab.localRotation = desiredLocal;
                        BeginDriveToTruckNavPoint4();
                    }
                }
                else
                {
                    // Same degenerate-vector bug as FacingTruckNavPoint1 — root already sits at (or
                    // effectively at) TruckNavPoint4's XZ, nothing meaningful to face. Move on instead
                    // of feeding LookRotation a near-zero vector.
                    BeginDriveToTruckNavPoint4();
                }
                break;
            }

            case TruckState.DevCheckpoint_TractorFacingNav4:
                // Retired dead-end — see enum comment. Nothing advances out of this automatically.
                break;

            case TruckState.ToTruckNavPoint4:
                if (DriveToward(_currentTarget))
                {
                    _state = TruckState.StraighteningAtNavPoint4;
                }
                break;

            case TruckState.StraighteningAtNavPoint4:
            {
                if (_cab == null)
                {
                    BeginReversingArcAroundPivot1();
                    break;
                }

                // Root/body orientation is left untouched here (deliberately no whole-body
                // reorientation onto the world X axis, per Tad's request — the body just stays as-is).
                // Tractor: NOT eased back to rest anymore (2026-09-05, Tad's spec) — ramps straight on
                // toward reverseArcCabAngle (+20°) instead, at the same cabSwivelSpeed
                // ReversingArcAroundPivot1 itself uses, so the whole TruckNavPoint3→TruckNavPoint5
                // stretch reads as one continuous climb to +20° rather than dropping back to 0° here
                // and ramping up again once the arc starts.
                Quaternion desiredCabLocal = Quaternion.Euler(0f, reverseArcCabAngle, 0f) * _cabRest;
                _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, desiredCabLocal, cabSwivelSpeed * Time.deltaTime);

                if (Quaternion.Angle(_cab.localRotation, desiredCabLocal) <= 0.5f)
                {
                    _cab.localRotation = desiredCabLocal;
                    // Step 14 (2026-09-05, Tad's spec): this used to call BeginReverseToTruckNavPoint3
                    // (step 13's straight-line reverse, dead-ending at DevCheckpoint_AtNavPoint3Reverse).
                    // Replaced with the new arc-around-TruckPivot1-to-TruckNavPoint5 maneuver, which
                    // continues on into StraighteningIntoDoor and actually finishes the dock — the first
                    // step in this whole chain to do so instead of pausing at a checkpoint.
                    BeginReversingArcAroundPivot1();
                }
                break;
            }

            case TruckState.DevCheckpoint_StraightAtNav4:
                // Retired dead-end — see enum comment. Nothing advances out of this automatically.
                break;

            case TruckState.ReversingStraightSlow:
            {
                // Whole body translates straight backward along its own local reverse — no rotation
                // change to the root at all, so this can never fight/steer regardless of whatever
                // heading was left over from the earlier legs (unlike the rejected target-seeking
                // ReversingIntoDoor attempt, this one has no target to converge on — it's a fixed
                // relative distance, so the direction is always well-defined).
                float step = Mathf.Min(slowReverseSpeed * Time.deltaTime, slowReverseDistance - _slowReverseTraveled);
                if (step > 0f)
                {
                    transform.position += -transform.forward * step;
                    _slowReverseTraveled += step;
                }

                bool cabAligned = true;
                if (_cab != null)
                {
                    Quaternion desiredCabLocal = Quaternion.Euler(0f, slowReverseCabAngle, 0f) * _cabRest;
                    _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, desiredCabLocal, cabSwivelSpeed * Time.deltaTime);
                    cabAligned = Quaternion.Angle(_cab.localRotation, desiredCabLocal) <= 0.5f;
                }

                if (_slowReverseTraveled >= slowReverseDistance - 0.001f && cabAligned)
                {
                    BeginReverseToTruckNavPoint5();
                }
                break;
            }

            case TruckState.DevCheckpoint_ReversedSlow:
                // Retired dead-end — see enum comment. Nothing advances out of this automatically.
                break;

            case TruckState.ReversingToTruckNavPoint5:
            {
                // Continue the exact same backward translation, no rotation change to the root at
                // all — seamless continuation of ReversingStraightSlow, just now aimed at a real
                // waypoint instead of a fixed distance. Tractor is pinned (not eased toward, just
                // held) at the same fixed offset the whole leg — nothing about the cosmetic look
                // should change until we actually arrive.
                if (_cab != null)
                    _cab.localRotation = Quaternion.Euler(0f, slowReverseCabAngle, 0f) * _cabRest;

                Vector3 flat = new Vector3(_currentTarget.x, _groundY, _currentTarget.z);
                Vector3 toTarget = flat - transform.position;
                toTarget.y = 0f;

                if (toTarget.magnitude < arrivedThreshold)
                {
                    transform.position = flat;
                    _state = TruckState.StraighteningAtNavPoint5;
                    break;
                }

                // Straight-line translation toward the target (not along ±transform.forward) — same
                // fix as the rejected ReversingIntoDoor attempt: guarantees arrival regardless of
                // whether the frozen heading happens to point exactly at TruckNavPoint5.
                Vector3 step = toTarget.normalized * slowReverseSpeed * Time.deltaTime;
                if (step.magnitude > toTarget.magnitude) step = toTarget;
                transform.position += step;
                break;
            }

            case TruckState.StraighteningAtNavPoint5:
            {
                // Whole body rotates to face the nearest of world +Z/-Z exactly (whichever is
                // closer to the current heading, so it doesn't spin an unnecessary ~180°) WHILE the
                // tractor simultaneously eases back to 0° local — both must finish before pausing.
                Vector3 zAxisTarget = Vector3.Dot(transform.forward, Vector3.forward) >= 0f ? Vector3.forward : Vector3.back;
                Quaternion desiredRoot = Quaternion.LookRotation(zAxisTarget, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRoot, driveTurnSpeed * Time.deltaTime);
                bool rootAligned = Quaternion.Angle(transform.rotation, desiredRoot) <= 0.5f;

                bool cabAligned = true;
                if (_cab != null)
                {
                    _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, _cabRest, cabSwivelSpeed * Time.deltaTime);
                    cabAligned = Quaternion.Angle(_cab.localRotation, _cabRest) <= 0.5f;
                }

                if (rootAligned && cabAligned)
                {
                    transform.rotation = desiredRoot;
                    if (_cab != null) _cab.localRotation = _cabRest;
                    _state = TruckState.DevCheckpoint_AtNavPoint5;
                }
                break;
            }

            case TruckState.DevCheckpoint_AtNavPoint5:
                // Retired dead-end — see enum comment (no longer reached by normal flow). Nothing
                // advances out of this automatically.
                break;

            case TruckState.ReversingToTruckNavPoint3:
            {
                // Straight-line translation toward the target, root rotation untouched — same
                // approach as ReversingToTruckNavPoint5, guarantees arrival regardless of whatever
                // heading is left over from the earlier legs.
                Vector3 flat = new Vector3(_currentTarget.x, _groundY, _currentTarget.z);
                Vector3 toTarget = flat - transform.position;
                toTarget.y = 0f;

                // Tractor mesh gradually eases toward reverseToNav3CabAngle over the whole leg
                // (genuinely gradual, via cabSwivelSpeed — not held/snapped immediately).
                if (_cab != null)
                {
                    Quaternion desiredCabLocal = Quaternion.Euler(0f, reverseToNav3CabAngle, 0f) * _cabRest;
                    _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, desiredCabLocal, cabSwivelSpeed * Time.deltaTime);
                }

                if (toTarget.magnitude < arrivedThreshold)
                {
                    transform.position = flat;
                    _state = TruckState.DevCheckpoint_AtNavPoint3Reverse;
                    break;
                }

                Vector3 step = toTarget.normalized * slowReverseSpeed * Time.deltaTime;
                if (step.magnitude > toTarget.magnitude) step = toTarget;
                transform.position += step;
                break;
            }

            case TruckState.DevCheckpoint_AtNavPoint3Reverse:
                // Parked on purpose — see the enum comment. Nothing advances out of this automatically.
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

                if (toTarget.magnitude < arrivedThreshold)
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
                // freed up (UpdateWaitingForDoor claims _dock BEFORE starting the reverse) drives
                // straight onto that door's TruckNavPoint0; a give-up timeout (_dock stays null — never
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

                    var nav0 = FindDockWaypoint("TruckNavPoint0");
                    if (nav0 != null)
                    {
                        _currentTarget = nav0.position;
                        _state = TruckState.FacingTruckNavPoint0;
                    }
                    else if (_gateEnterNoTurn.HasValue)
                    {
                        // Fallback: this door has no TruckNavPoint0 marker — re-enter through the gate
                        // like the old ReversingOutOfSideLot path did, rather than getting stuck.
                        SetTarget(TruckState.ToEnterNoTurn, _gateEnterNoTurn.Value);
                    }
                    break;
                }

                Vector3 step = toTarget.normalized * slowReverseSpeed * Time.deltaTime;
                if (step.magnitude > toTarget.magnitude) step = toTarget;
                transform.position += step;
                break;
            }

            case TruckState.ToApproach:
                // Xform 2 — straight, no stop, then straight on to the door itself. REVERTED
                // 2026-09-05 back to its original behavior — this leg is no longer where the new
                // maneuver hooks in (see StraighteningAtNavPoint4); ToBackup below is now purely
                // the fallback path (see BeginReversingArcAroundPivot1).
                if (DriveToward(_currentTarget))
                    SetTarget(TruckState.ToBackup, DockPositionFor(_dock));
                break;

            case TruckState.ToBackup:
                // Fallback only (see BeginReversingArcAroundPivot1) — a door missing its
                // TruckPivot1/TruckNavPoint5 markers still gets the old dead-straight-then-flip
                // maneuver instead of getting stuck.
                if (DriveToward(_currentTarget))
                {
                    transform.rotation = DockRotationFor(_dock);
                    _state = TruckState.Docked;
                    OnDocked();
                }
                break;

            case TruckState.ReversingArcAroundPivot1:
                UpdateReversingArcAroundPivot1();
                break;

            case TruckState.StraighteningIntoDoor:
                UpdateStraighteningIntoDoor();
                break;

            // WaitingAtBackup / Reversing / ReversingToDock: retired along with the old curved
            // turn-and-reverse maneuver. Never entered by new gameplay, and RestoreFromSnapshot
            // redirects any old save frozen in one straight into ToBackup instead of resuming here —
            // kept only as TruckState enum members so an old save's stored ordinal never resolves to
            // the wrong state.

            case TruckState.Docked:
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
    private void UpdateCabSteering()
    {
        if (!articulateCab || _cab == null) return;

        // FacingTruckNavPoint3 drives _cab.localRotation directly (a deliberate slow swivel toward
        // TruckNavPoint3, not a reaction to body movement) — fully bypass so the two don't fight
        // over the same transform.
        // DevCheckpoint_TractorFacingNav3 is included here too — without it, the very next frame
        // after FacingTruckNavPoint3 finishes falls through to the generic logic below, which reads
        // a ~zero body yaw-rate (parked) and slews _cabYaw back toward 0, silently undoing the
        // deliberate swivel the moment time resumes. Confirmed live: tractor read back to exactly
        // its rest rotation instead of holding the swiveled pose.
        // StraighteningAtNavPoint4 also drives _cab.localRotation directly (easing the tractor back
        // to rest). ReversingStraightSlow/ReversingToTruckNavPoint5 hold it at the fixed
        // +slowReverseCabAngle offset; StraighteningAtNavPoint5 eases it back to rest. Same
        // reasoning either way — bypass fully so the generic yaw-rate-reactive logic below can't
        // fight a directly-driven rotation.
        // ReversingArcAroundPivot1 (arcs to reverseArcCabAngle) and StraighteningIntoDoor (eases
        // back to rest) drive _cab.localRotation directly too — same reasoning as every other
        // entry in this list. FacingTruckNavPoint1 (forced-CCW swivel to face TruckNavPoint1, root
        // parked), ToTruckNavPoint1 (eases toward nav1ApproachCabAngle near arrival), ToTruckNavPoint2
        // (holds nav1ApproachCabAngle through the arc), and StraighteningAtNavPoint2 (eases back to
        // rest) likewise.
        if (_state == TruckState.FacingTruckNavPoint3 || _state == TruckState.DevCheckpoint_TractorFacingNav3 ||
            _state == TruckState.FacingTruckNavPoint1 || _state == TruckState.TrailerAligningToNavPoint1 ||
            _state == TruckState.ToTruckNavPoint1 || _state == TruckState.ToTruckNavPoint2 ||
            _state == TruckState.StraighteningAtNavPoint2 ||
            _state == TruckState.ToTruckNavPoint3 ||
            _state == TruckState.FacingTruckNavPoint4 || _state == TruckState.DevCheckpoint_TractorFacingNav4 ||
            _state == TruckState.StraighteningAtNavPoint4 || _state == TruckState.DevCheckpoint_StraightAtNav4 ||
            _state == TruckState.ReversingStraightSlow || _state == TruckState.DevCheckpoint_ReversedSlow ||
            _state == TruckState.ReversingToTruckNavPoint5 || _state == TruckState.StraighteningAtNavPoint5 ||
            _state == TruckState.DevCheckpoint_AtNavPoint5 ||
            _state == TruckState.ReversingToTruckNavPoint3 || _state == TruckState.DevCheckpoint_AtNavPoint3Reverse ||
            _state == TruckState.ReversingArcAroundPivot1 || _state == TruckState.StraighteningIntoDoor)
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
        float yawRate = Mathf.DeltaAngle(_prevYaw, curYaw) / dt;
        _prevYaw      = curYaw;

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

        if (toTarget.magnitude < arrivedThreshold)
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

    private void BeginGuardCheck()
    {
        _state = TruckState.GuardCheck;
        if (_guard != null)
            _guard.BeginInspection(this, GuardClearedToEnter);
        else
            GuardClearedToEnter();
    }

    /// <summary>Hands control from the tractor's cosmetic swivel offset back to the root, with zero
    /// visual change, then starts driving to TruckNavPoint3. The tractor's WORLD orientation right
    /// now (root rotation combined with its swiveled local offset) is exactly what should keep
    /// showing — so the root is snapped to that same world rotation and the tractor's local offset
    /// is reset to rest, which cancels out to an identical visual result the next frame. Without
    /// this, starting to drive would let the generic cosmetic system (UpdateCabSteering) immediately
    /// recompute the tractor's local offset from scratch based on the root's own turning, visibly
    /// popping away from the "look toward Nav3" pose it had just settled into.</summary>
    private void BeginDriveToTruckNavPoint3()
    {
        if (_cab != null)
        {
            transform.rotation = _cab.rotation;
            _cab.localRotation = _cabRest;
        }

        var nav3 = FindDockWaypoint("TruckNavPoint3");
        SetTarget(TruckState.ToTruckNavPoint3, nav3 != null ? nav3.position : transform.position);
    }

    private void BeginFacingTruckNavPoint4()
    {
        _state = TruckState.FacingTruckNavPoint4;
    }

    /// <summary>Same rebase pattern as BeginDriveToTruckNavPoint3 — the tractor just finished a
    /// deliberate cosmetic swivel toward Nav4, so before driving starts, hand authority for that
    /// world facing back to the root (root snaps to the tractor's world rotation, tractor's local
    /// offset resets to rest) so nothing pops when the generic drive/steer logic takes over.</summary>
    private void BeginDriveToTruckNavPoint4()
    {
        if (_cab != null)
        {
            transform.rotation = _cab.rotation;
            _cab.localRotation = _cabRest;
        }

        var nav4 = FindDockWaypoint("TruckNavPoint4");
        SetTarget(TruckState.ToTruckNavPoint4, nav4 != null ? nav4.position : transform.position);
    }

    private void BeginReverseStraightSlow()
    {
        _slowReverseTraveled = 0f;
        _state = TruckState.ReversingStraightSlow;
    }

    private void BeginReverseToTruckNavPoint5()
    {
        var nav5 = FindDockWaypoint("TruckNavPoint5");
        SetTarget(TruckState.ReversingToTruckNavPoint5, nav5 != null ? nav5.position : transform.position);
    }

    private void BeginReverseToTruckNavPoint3()
    {
        var nav3 = FindDockWaypoint("TruckNavPoint3");
        SetTarget(TruckState.ReversingToTruckNavPoint3, nav3 != null ? nav3.position : transform.position);
    }

    /// <summary>Starts the 2026-09-05 reversing-pivot maneuver: from wherever ToApproach ends, the
    /// truck backs into the dock by arcing around the door's TruckPivot1 marker until it reaches
    /// TruckNavPoint5, tractor swiveling out to reverseArcCabAngle for the duration. Falls back to
    /// the old dead-straight ToBackup+flip if this door's BackInSystem child is missing either
    /// marker, same defensive pattern as every other FindDockWaypoint call in this file.</summary>
    private void BeginReversingArcAroundPivot1()
    {
        var pivot1 = FindDockWaypoint("TruckPivot1");
        var nav5   = FindDockWaypoint("TruckNavPoint5");
        if (pivot1 == null || nav5 == null)
        {
            Debug.LogWarning("[TruckController] Dock missing TruckPivot1/TruckNavPoint5 (BackInSystem child) — falling back to the straight backup maneuver.");
            SetTarget(TruckState.ToBackup, DockPositionFor(_dock));
            return;
        }

        // Radius starts at the truck's actual measured distance from TruckPivot1 (TruckNavPoint4
        // sits ~11.5m out; TruckPivot1→TruckNavPoint5 is exactly arcRadius, 9.25m — these two legs
        // were never on the same circle) and eases toward the authored arcRadius over the arc (see
        // Update) rather than snapping to it, so there's no instant position pop; it converges to
        // and then HOLDS exactly arcRadius for the rest of the maneuver, per Tad's spec.
        Vector3 flatTruck  = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatPivot1 = new Vector3(pivot1.position.x, 0f, pivot1.position.z);
        _arcRadiusActive = Vector3.Distance(flatTruck, flatPivot1);
        if (_arcRadiusActive < 0.01f) _arcRadiusActive = arcRadius; // degenerate — truck is on the pivot

        _arcEntryAngle = AngleAroundPivot(transform.position, pivot1.position);
        float targetAngle = AngleAroundPivot(nav5.position, pivot1.position);

        // ALWAYS counterclockwise (Tad's spec — this is not a "pick the shorter side" arc). Sweep
        // target = the counterclockwise (decreasing-angle) distance from entry to TruckNavPoint5,
        // which can legitimately exceed 180° depending on where the truck enters relative to the
        // pivot.
        _arcTargetSweptDegrees = Mathf.Repeat(_arcEntryAngle - targetAngle, 360f);
        if (_arcTargetSweptDegrees < 1f) _arcTargetSweptDegrees = 360f; // degenerate "already at target"
        _arcSweptSoFar = 0f;
        _arcCurrentAngle = _arcEntryAngle;

        _currentTarget = nav5.position;
        _useBezier = false;
        _state = TruckState.ReversingArcAroundPivot1;
    }

    /// <summary>Backs the truck around TruckPivot1 toward TruckNavPoint5. Angle-parametric by
    /// design (position and heading are both computed DIRECTLY from a single accumulated angle each
    /// frame), not tangent-step-then-reproject — two confirmed-live bugs drove this rewrite:
    /// (1) using the tangent-step/reproject-onto-circle technique with the truck's own entry radius
    /// held the whole arc 2-5m off the authored 9.25 circle for its entire length (TruckNavPoint4
    /// isn't on that circle, so "hold whatever radius you entered at" is simply the wrong radius);
    /// (2) driving transform.rotation via Quaternion.RotateTowards toward a per-frame tangent target
    /// let it take whichever rotational direction was LOCALLY shorter to catch up, which could be
    /// briefly CLOCKWISE even though the arc's own sweep is always counterclockwise — visible live as
    /// the trailer "swinging out the wrong way before it starts backing up."
    /// Fix: _arcRadiusActive eases toward the authored arcRadius (MoveTowards, not a snap) so the
    /// truck spends the arc's opening moment closing a small radial gap and then holds exactly
    /// arcRadius for the rest of it; position and heading are both derived straight from
    /// _arcCurrentAngle (which only ever decreases — counterclockwise, unconditionally) with no
    /// independent "current vs desired" catch-up state for either, so neither can ever take a
    /// wrong-direction detour.</summary>
    private void UpdateReversingArcAroundPivot1()
    {
        var pivot1 = FindDockWaypoint("TruckPivot1");
        if (pivot1 == null)
        {
            // Marker vanished mid-maneuver (shouldn't happen — a dock doesn't move) — bail to the
            // straight fallback rather than divide-by-zero on a missing pivot.
            SetTarget(TruckState.ToBackup, DockPositionFor(_dock));
            return;
        }

        _arcRadiusActive = Mathf.MoveTowards(_arcRadiusActive, arcRadius, radiusCorrectionSpeed * Time.deltaTime);

        // Advance the parametric angle by however many degrees this frame's linear speed covers at
        // the CURRENT radius (arc length / radius, in degrees). Always counterclockwise (decreasing).
        float angularSpeedDeg = (slowReverseSpeed / Mathf.Max(_arcRadiusActive, 0.01f)) * Mathf.Rad2Deg;
        float remaining = _arcTargetSweptDegrees - _arcSweptSoFar;
        float stepDeg = Mathf.Min(angularSpeedDeg * Time.deltaTime, remaining);
        _arcCurrentAngle -= stepDeg;
        _arcSweptSoFar += stepDeg;

        // Position: computed directly from angle+radius every frame — exact by construction (matches
        // AngleAroundPivot's atan2(x,z) convention: offset = radius*(sinθ, 0, cosθ)), never drifts,
        // never needs reprojecting.
        float rad = _arcCurrentAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * _arcRadiusActive;
        Vector3 newPos = pivot1.position + offset;
        newPos.y = _groundY;
        transform.position = newPos;

        // Heading: the velocity direction for a DECREASING θ (counterclockwise) is (-cosθ, 0, sinθ)
        // — set directly, no RotateTowards lag, so it's always exactly locked to the current point on
        // the circle (physically correct for rigid circular motion, and structurally impossible to
        // swing the wrong way since there's no separate "current heading" to reconcile against a
        // moving target). The root faces AWAY from the direction of travel (it's reversing) — the
        // same orientation it needs at final dock, held from the very first frame.
        Vector3 travelDir = new Vector3(-Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        transform.rotation = Quaternion.LookRotation(-travelDir);

        if (_cab != null)
        {
            // Sweep is always CCW now, so the cab always cranks the same way — no sign needed.
            Quaternion desiredCabLocal = Quaternion.Euler(0f, reverseArcCabAngle, 0f) * _cabRest;
            _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, desiredCabLocal, cabSwivelSpeed * Time.deltaTime);
        }

        if (_arcSweptSoFar >= _arcTargetSweptDegrees)
        {
            // No position snap here — _arcRadiusActive has already eased to exactly arcRadius well
            // before the sweep completes (radiusCorrectionSpeed closes a 2-3m gap in under a second,
            // the arc itself runs several seconds), so the truck is already sitting on the same
            // circle TruckNavPoint5 sits on. StraighteningIntoDoor absorbs whatever tiny remainder is
            // left with its existing smooth translation.
            BeginStraighteningIntoDoor();
        }
    }

    private void BeginStraighteningIntoDoor()
    {
        _state = TruckState.StraighteningIntoDoor;
    }

    /// <summary>Final leg of the reversing-pivot maneuver: straight-line backward translation onto
    /// the door's dock offset while the body eases from the arc's tangent heading to the door's
    /// real "backed in" rotation (DockRotationFor — facing away from the door, per Tad's spec) and
    /// the tractor eases back to 0° local. Both finish together right as the truck reaches the
    /// door, so there's no separate snap/flip like the old dead-simple ToBackup. Once close, hands
    /// off to Docked/OnDocked exactly like ToBackup always has — opens the trailer doors, ghosts
    /// the trailer, flags the dock occupied, and lets every other system (offload/load controllers,
    /// DockScheduleService, etc.) see the truck via _dock/AwaitingOffload the normal way.</summary>
    private void UpdateStraighteningIntoDoor()
    {
        // TruckNavPoint5 IS the correct final docked position (Tad: its 9.25 offset is exactly where
        // the truck needs to sit to be "perfectly in the door") — NOT DockPositionFor's legacyDockOffset
        // (3.5), which was the old dead-simple maneuver's resting spot and would drive the truck too
        // far past where it should actually stop for this maneuver.
        var nav5 = FindDockWaypoint("TruckNavPoint5");
        Vector3 target = nav5 != null ? nav5.position : DockPositionFor(_dock);
        // XZ only from the marker — Y always comes from _groundY (the height the truck has tracked
        // for its entire route), never the marker's own authored Y. Confirmed live: TruckNavPoint5's
        // Y isn't guaranteed to sit exactly on the ground plane, and snapping straight to its raw
        // position made docked trucks visibly float/sink depending on that marker's placement.
        target.y = _groundY;
        Quaternion desiredRoot = DockRotationFor(_dock);

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        bool positionArrived = toTarget.magnitude <= arrivedThreshold;
        bool rotationArrived = Quaternion.Angle(transform.rotation, desiredRoot) <= 0.5f;

        if (!positionArrived)
        {
            Vector3 step = toTarget.normalized * slowReverseSpeed * Time.deltaTime;
            if (step.magnitude > toTarget.magnitude) step = toTarget;
            transform.position += step;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRoot, driveTurnSpeed * Time.deltaTime);

        if (_cab != null)
            _cab.localRotation = Quaternion.RotateTowards(_cab.localRotation, _cabRest, cabSwivelSpeed * Time.deltaTime);

        if (positionArrived && rotationArrived)
        {
            transform.position = target;
            transform.rotation = desiredRoot;
            if (_cab != null) _cab.localRotation = _cabRest;
            _state = TruckState.Docked;
            OnDocked();
        }
    }

    public void GuardClearedToEnter()
    {
        // Leave the gate queue — the manager advances everyone behind us.
        _clearedGate = true;
        OnClearedGate?.Invoke();

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
            SetTarget(TruckState.ToApproach, ApproachPoint());
        else
            BeginDoorWait();
    }

    /// <summary>Routes a truck with no free door toward somewhere to wait. Tries an unoccupied
    /// SideLotController first (claimed immediately — before the truck physically arrives — so two
    /// trucks clearing the gate close together can't both target the same spot), then falls back to
    /// the generic _doorWaitPoint, then to the OLD behavior — give up immediately — if neither is
    /// available, rather than the truck sitting frozen mid-yard with nowhere to go. A second truck
    /// needing to wait while the lot is already occupied deliberately falls all the way through to
    /// give-up rather than queueing for the lot — no gate-side queueing exists for it yet.</summary>
    private void BeginDoorWait()
    {
        // BUG FIX (Tad's spec): used to treat a whole SideLotController as one shared spot
        // (!l.IsOccupied), so only the very first truck ever routed here — every SideLot actually has
        // several independent parking slots (one per Jersey_barrier segment), so this now searches
        // every lot for its own first free Slot instead of asking the lot itself if it's "occupied".
        SideLotController.Slot slot = null;
        SideLotController lot = null;
        foreach (var candidate in SideLotController.All)
        {
            if (candidate == null) continue;
            slot = candidate.FindFreeSlot();
            if (slot != null) { lot = candidate; break; }
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

        if (_doorWaitPoint == null)
        {
            Debug.LogWarning("[TruckController] No door-wait park spot configured for this yard — " +
                             "truck can't wait for a door, giving up immediately instead.");
            _doorWaitBar?.Hide();
            BeginDeparture(succeeded: false);
            return;
        }

        SetTargetCurved(TruckState.ToDoorWait, _doorWaitPoint.position, _doorWaitPoint.forward);
    }

    private void BeginWaitingForDoor()
    {
        _state = TruckState.WaitingForDoor;
        var gameCtx = FindAnyObjectByType<GameContext>();
        _doorWaitStartSimMinute = gameCtx != null && gameCtx.TimeService != null ? gameCtx.TimeService.TotalMinutesElapsed : -1;
        _doorWaitPollTimer = 0f;
        if (_doorWaitBar == null) _doorWaitBar = gameObject.AddComponent<TruckDoorWaitBar>();
        _doorWaitBar.ShowWaitingForDoor(doorWaitMinutes, doorWaitMinutes);

        if (AssignedShipment != null)
        {
            int critical = GameCore.Inventory.CriticalStockCheck.CountCriticalLines(
                AssignedShipment.LineItems.Select(li => li.SkuId));
            SystemsLogWindow.LogGuard(
                $"Another angry driver in the side lot — order number {AssignedShipment.PONumber}. " +
                $"One hour to receive {critical} critical item(s).");
        }
    }

    /// <summary>Ticks the door-wait countdown (in SIM minutes, same clock the dock schedule's 2-hour
    /// blocks run on — not real seconds, since "60 minutes" is a game-time promise) and polls for a
    /// freed-up door. Claims the first one it finds and drives in exactly like a normal arrival the
    /// moment one appears; gives up (still taking the late penalty) once the window runs out.</summary>
    private void UpdateWaitingForDoor()
    {
        var gameCtx = FindAnyObjectByType<GameContext>();
        if (gameCtx == null || gameCtx.TimeService == null) return;

        float elapsedMinutes = _doorWaitStartSimMinute >= 0
            ? gameCtx.TimeService.TotalMinutesElapsed - _doorWaitStartSimMinute
            : 0f;
        float remaining = Mathf.Max(0f, doorWaitMinutes - elapsedMinutes);
        _doorWaitBar?.ShowWaitingForDoor(remaining, doorWaitMinutes);

        if (remaining <= 0f)
        {
            _doorWaitBar?.Hide();

            // BUG FIX (Tad's spec): a truck that gave up while parked in the SideLot used to call
            // BeginDeparture directly from right where it sat — inside the fenced lot — which then
            // curved it straight toward the gate through the barrier instead of actually driving out.
            // It needs to back straight out to the Entry marker first (identical maneuver to the
            // "a door freed up" case below), THEN run the normal give-up departure. _dock stays null
            // here (never claimed one) — that's exactly what ReversingToSideLotEntry's arrival check
            // uses to tell "backing out to grab a door" apart from "backing out to give up and leave".
            if (_sideLotSlot != null)
            {
                if (_sideLotSlot.Entry != null)
                    SetTarget(TruckState.ReversingToSideLotEntry, _sideLotSlot.Entry.position);
                else if (_gateEnterNoTurn.HasValue)
                    SetTarget(TruckState.ReversingOutOfSideLot, _gateEnterNoTurn.Value);
                else
                    BeginDeparture(succeeded: false);
                return;
            }

            BeginDeparture(succeeded: false);
            return;
        }

        _doorWaitPollTimer -= Time.deltaTime;
        if (_doorWaitPollTimer > 0f) return;
        _doorWaitPollTimer = doorWaitPollSeconds;

        var freeDock = FindAnyFreeDock();
        if (freeDock == null) return;

        _dock = freeDock;
        _dock.Claim();
        _doorWaitBar?.ShowHeadingToDoor(_dock.DoorNumber);

        if (_sideLotSlot != null)
        {
            // Reverse straight back out first — can't curve directly to ApproachPoint() from inside the
            // fenced lot the way a generic _doorWaitPoint truck can. Backs up to the Entry marker (per
            // Tad's spec) and rejoins the backing pipeline at TruckNavPoint0 from there — see
            // ReversingToSideLotEntry's case body. Falls back to the old all-the-way-through-the-gate
            // retreat for a SideLot placed before the Entry marker existed.
            if (_sideLotSlot.Entry != null)
                SetTarget(TruckState.ReversingToSideLotEntry, _sideLotSlot.Entry.position);
            else if (_gateEnterNoTurn.HasValue)
                SetTarget(TruckState.ReversingOutOfSideLot, _gateEnterNoTurn.Value);
            return;
        }

        Vector3 ap = ApproachPoint();
        Vector3 curveDir = ap - transform.position;
        SetTargetCurved(TruckState.ToApproach, ap, curveDir);
    }

    /// <summary>Same random-pick-among-free-doors policy as TruckYardManager.FindFreeDock — kept local
    /// (rather than routing back through the yard manager) since a waiting truck already knows the
    /// full DockSlot registry directly.</summary>
    private DockSlot FindAnyFreeDock()
    {
        List<DockSlot> free = null;
        foreach (var d in DockSlot.All)
        {
            if (d == null || d.IsOccupied) continue;
            free ??= new List<DockSlot>();
            free.Add(d);
        }
        return free == null ? null : free[Random.Range(0, free.Count)];
    }

    private void OnDocked()
    {
        _dock.LightController?.SetOccupied(true);

        // Docked and being offloaded/loaded now — the "Heading to Door X" banner has done its job.
        _doorWaitBar?.Hide();

        // Reset offload/load handoff — the truck now waits in Docked until a dock stocker offloads
        // or loads it (TrailerOffloadController / TrailerLoadController) or the fallback timeout fires.
        _dockedTime      = 0f;
        _offloadClaimed  = false;
        _offloadComplete = false;
        _loadClaimed     = false;
        _loadComplete    = false;

        // Inbound only — outbound dwell isn't part of the vendor dwell-time stat.
        if (!_isOutbound)
        {
            var gameCtx = FindAnyObjectByType<GameContext>();
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

        Debug.Log($"[TruckController] {name} docked at door {_dock.DoorNumber}. Awaiting {(_isOutbound ? "load" : "offload")}.");
    }

    /// <summary>Outbound counterpart of the inbound Docked branch in Update(): waits for
    /// TrailerLoadController to load staged pallets and call CompleteLoad(), or falls back to
    /// departing anyway if unclaimed for too long (same self-preservation the inbound "no DS
    /// available" case has) — unlike inbound, an outbound trailer STARTS empty and fills up, so
    /// "still empty" is never a reason to leave early the way it is for offload.</summary>
    private void UpdateDockedOutbound()
    {
        if (_loadComplete)
        {
            BeginDeparture();
        }
        else if (!_loadClaimed && _dockedTime >= offloadFallbackTimeout)
        {
            BeginDeparture();
        }
    }

    private void BeginDeparture() => BeginDeparture(succeeded: true);

    /// <summary><paramref name="succeeded"/> is false exactly once, for the door-wait give-up case
    /// (UpdateWaitingForDoor's timeout / BeginDoorWait's missing-waypoint fallback) — a truck that
    /// never got a door never delivered anything, so it must not be marked Departed (that reads as "PO
    /// fulfilled" everywhere else in the code, e.g. DockScheduleService.IsComplete). Everything else
    /// about leaving the yard — release the dock if one was somehow held, close up, roll out through
    /// the gate — is identical either way, so this stays one method with one branch rather than two
    /// near-duplicate exit routines drifting apart over time.</summary>
    private void BeginDeparture(bool succeeded)
    {
        // Defensive null-guard: a truck should always have a claimed dock by the time it departs,
        // but an orphaned restore (dock lookup failed) previously left this null and threw here
        // every frame forever (the truck never actually left). Null-conditional so a bad restore
        // degrades to "truck leaves without releasing a dock" instead of an infinite NRE loop. Also
        // covers the door-wait-timeout case cleanly: _dock is null there too (never claimed one).
        _dock?.LightController?.SetOccupied(false);
        // Release the rollup door's forced-open hold — the truck's own exit through the trigger will
        // close it normally as it drives out.
        _dock?.SetDoorForcedOpen(false);
        _dock?.Release();
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
                    shipSvc.PurgeCompleted();

                // VENDORS tab's "Avg Hours in Door" — inbound only, and only once we actually have a real
                // docked-at stamp (a truck that skipped Docked entirely, if that's ever possible, shouldn't
                // report a bogus 0-hour dwell).
                if (!_isOutbound && _dockedAtSimMinute >= 0 &&
                    !string.IsNullOrEmpty(AssignedShipment.SupplierId) &&
                    GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.VendorPerformanceTracker perf))
                {
                    var gameCtx = FindAnyObjectByType<GameContext>();
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
            if (!_isOutbound && AssignedShipment != null)
            {
                if (_doorWaitBar == null) _doorWaitBar = gameObject.AddComponent<TruckDoorWaitBar>();
                _doorWaitBar.ShowDeparting(AssignedShipment.TotalReceivedUnits, AssignedShipment.TotalUnits);
            }
        }
        else
        {
            ApplyGaveUpWaitingForDoorPenalty();
        }

        // Solid trailer + closed doors again before it drives off.
        SetDockedGhost(false);
        CloseTrailerDoors();

        // Pull out forward to Xform 2, easing toward the exit direction. ApproachPoint() falls back to
        // the truck's current position when _dock is null (the give-up case), so this degrades to
        // "curve straight from wherever it's parked toward the gate" with no special-casing needed.
        Vector3 ap   = ApproachPoint();
        Vector3 next = _gateLeaveNoTurn ?? _exitWaypoint ?? ap;
        SetTargetCurved(TruckState.DepartToApproach, ap, next - ap);
    }

    /// <summary>
    /// The consequence for a driver who waited the full doorWaitMinutes with no door ever freeing up:
    /// per Tad's spec, the appointment is handed back to the pool (the player must actively reschedule
    /// it) and the vendor relationship still takes the same flat hit HandleNoAvailableDoor used to apply
    /// immediately — the only thing that changed is WHEN it lands (after a real 60-minute wait instead
    /// of instantly), not whether it happens. Deliberately does NOT touch AssignedShipment.Status — it
    /// stays whatever it was (InTransit/Receiving), so DispatchDueShipments can re-attempt this same PO
    /// once the player books it a new appointment, exactly like the old immediate-turnaround path did.
    /// </summary>
    private void ApplyGaveUpWaitingForDoorPenalty()
    {
        if (AssignedShipment == null) return;

        if (GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService dockSchedule) &&
            dockSchedule != null)
        {
            var appt = dockSchedule.FindForPo(AssignedShipment.PONumber);
            if (appt != null)
            {
                dockSchedule.TryPark(appt.Id, out string failReason);
                if (failReason != null)
                    Debug.LogWarning($"[TruckController] Couldn't park PO {AssignedShipment.PONumber}'s " +
                                     $"appointment after giving up on a door: {failReason}");
            }
        }

        if (GameCore.Services.ServiceLocator.TryGet(out GameCore.Inventory.VendorEconomyService economy) &&
            economy != null && !string.IsNullOrEmpty(AssignedShipment.SupplierId))
        {
            economy.AdjustPartnershipLevel(AssignedShipment.SupplierId, -20,
                $"No door freed up for PO {AssignedShipment.PONumber} within {doorWaitMinutes:F0} minutes — driver gave up and left");
        }

        UIToast.Show($"No door freed up for PO {AssignedShipment.PONumber} in time — the driver has " +
                     "left. Reschedule the appointment.");
        SystemsLogWindow.LogGuard($"Order# {AssignedShipment.PONumber} left the yard due to delays — you BLEW IT!");
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

            if (ghosted)
            {
                if (_ghostMaterial == null || _originalMaterials.ContainsKey(r)) continue;
                _originalMaterials[r] = r.sharedMaterials;

                var ghosts = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ghosts.Length; i++) ghosts[i] = _ghostMaterial;
                r.sharedMaterials = ghosts;
            }
            else if (_originalMaterials.TryGetValue(r, out var original))
            {
                r.sharedMaterials = original;
                _originalMaterials.Remove(r);
            }
        }
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
}
