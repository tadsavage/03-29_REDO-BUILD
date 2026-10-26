using System.Collections;
using System.Collections.Generic;
using GameCore.Actors;
using GameCore.Economy;
using GameCore.Labor;
using GameCore.Services;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Battery on every piece of MHE: pallet jacks, reach trucks, dock stockers (Tad, 2026-10-17).
///
///   • A full battery lasts <see cref="HoursToEmpty"/> game hours of USE. It only drains while the machine is
///     moving; parked or standing still it holds its charge.
///   • Parked on an MHE charging station (MHE_Station) and not moving, it charges from empty to full in
///     <see cref="HoursToFull"/> game hours.
///   • Within <see cref="LowHours"/> of running out, a red lightning bolt floats over it (EquipmentBatteryPanels).
///   • Click the machine to show its battery panel; a machine on a charger shows its charging panel by itself.
///
/// WHEN THE BATTERY DIES (Tad, 2026-10-17): the operator is ejected and goes back to patrolling. The machine
/// carries on by itself, at DOUBLE speed, until whatever is on its forks is delivered (staging lane, rack,
/// replenishment - whatever its job was), then drives itself to the nearest free charging station and plugs in.
/// With no free station it parks beside the nearest one and moves in when one frees up. Operators won't board
/// it again until it is full (<see cref="MHEOperatorSlot.AcceptsOperator"/>). The player can also right-click a
/// free machine and pick "Charge Battery" (<see cref="RequestCharge"/>) to send it to charge early.
///
/// Charge is saved in the machine's PlacedObject.customData ("batt=0.734"). Attached by MHEOperatorSlot.Start to
/// every real placed machine (not the build-menu hover ghost).
/// </summary>
[DisallowMultipleComponent]
public class EquipmentBattery : MonoBehaviour
{
    /// <summary>Each machine gets its own battery size, 8 to 12 game hours of use, so they don't all reach the chargers together
    /// (Tad, 2026-10-18). Saved with the machine ("cap=").</summary>
    public const float MinCapacityHours = 8f;
    public const float MaxCapacityHours = 12f;
    public const float HoursToEmpty = 10f;   // average; the real figure per machine is CapacityHours
    public const float HoursToFull = 1f;     // empty to full on a charger
    public const float LowHours = 2f;          // red bolt + log warning at this many hours left (Tad, 2026-10-18)
    public const float DriverlessSpeedMultiplier = 2f;
    private const string Key = "batt=";
    private const float MoveEpsilon = 0.003f;      // metres per frame that counts as moving
    private const float TurnEpsilon = 0.25f;       // degrees per frame that counts as moving (turning in place)
    private const float MovingHoldSeconds = 0.5f;  // stays "in use" this long after the last movement (no flicker)
    private const float TripDriveSpeed = 2.0f;     // m/s, the scripted last metres into the bay
    private const float TripTurnSpeed = 120f;
    private const float NavLegTimeout = 90f;

    private static readonly List<EquipmentBattery> s_all = new();
    private static int s_nextLinkId = 1;

    /// <summary>Stable-for-the-session id used by the Systems Log hyperlink (LogLinks "equip:").</summary>
    public int LinkId { get; private set; }
    public static EquipmentBattery FindByLinkId(int id)
    {
        foreach (var b in s_all) if (b != null && b.LinkId == id) return b;
        return null;
    }
    public static IReadOnlyList<EquipmentBattery> All => s_all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { s_all.Clear(); s_warnedNoStation = false; s_nextLinkId = 1; s_helpers.Clear(); }

    public float Charge01 { get; private set; } = 1f;
    public bool IsMoving { get; private set; }
    public bool IsCharging { get; private set; }
    public PalletJackParkingSpot Station { get; private set; }
    public MHEOperatorSlot Slot { get; private set; }

    /// <summary>Flat battery or sent to charge: heading to / waiting at / sitting on a charger until full.</summary>
    public bool NeedsCharge { get; private set; }

    /// <summary>Finishing the job on its forks with nobody aboard (after the battery died). Runs at double speed.</summary>
    public bool DriverlessRun { get; private set; }

    /// <summary>On its way to (or waiting beside) a charging station.</summary>
    public bool OnChargeTrip => _trip != null;

    /// <summary>Every station was taken: parked beside one, waiting for a bay to free up.</summary>
    public bool WaitingForCharger { get; private set; }

    /// <summary>This machine's full battery, in game hours of use (8-12).</summary>
    public float CapacityHours { get; private set; } = HoursToEmpty;

    public float HoursLeft => Charge01 * CapacityHours;
    public float HoursToFullCharge => (1f - Charge01) * HoursToFull;
    public bool IsDead => Charge01 <= 0.0001f;
    public bool IsLow => HoursLeft <= LowHours && !IsCharging;
    public bool IsFull => Charge01 >= 0.9999f;

    /// <summary>Speed factor the driving code applies: double while finishing a job driverless.</summary>
    public float SpeedMultiplier => DriverlessRun ? DriverlessSpeedMultiplier : 1f;

    /// <summary>Speed factor for the machine at <paramref name="t"/> (1 if it has no battery).</summary>
    public static float SpeedMultiplierFor(Component c)
    {
        var b = c != null ? c.GetComponentInParent<EquipmentBattery>() : null;
        return b != null ? b.SpeedMultiplier : 1f;
    }

    /// <summary>Middle of the machine's body (a pallet jack's forks trail behind its operator anchor).</summary>
    public Vector3 BodyCentre => Slot != null && Slot.IsPalletJack ? PalletJackParkingSpot.JackBodyCentre(transform) : transform.position;

    /// <summary>Doing a job right now: a reach-truck task, a dock-stocker offload/load, or a selector's pallet-jack run.</summary>
    public bool IsBusy
    {
        get
        {
            if (Slot == null) return false;
            if (Slot.IsPalletJack) return OrderSelectionTaskDriver.IsJackClaimed(Slot);
            var rt = GetComponent<ReachTruckOperator>();
            if (rt != null && rt.IsBusy) return true;
            return DockEquipmentCommandeerRegistry.IsCommandeered(Slot);
        }
    }
    /// <summary>"PALLET JACK", "REACH TRUCK" or "DOCK STOCKER".</summary>
    public string KindName
    {
        get
        {
            if (Slot != null && Slot.IsPalletJack) return "PALLET JACK";
            // Name-based on purpose-built prefabs ("RT_Full", "DS_FULL") AND the plain names a placed machine now carries
            // ("ReachTruck", "Dockstocker"). Matching only the old prefixes classified every placed machine as "EQUIPMENT", whose helper
            // role is OrderSelector - so the real drivers counted as the WRONG rider and were stepped off every 8 seconds, killing every
            // job (the 2026-10-24 block 1: 0 shipped, hundreds of dismounts, "NO NAVMESH PATH" on every leg).
            string n = name.ToUpperInvariant();
            var po = _po != null ? _po : GetComponent<PlacedObject>();
            string dn = po != null && po.data != null && po.data.objName != null ? po.data.objName.ToUpperInvariant() : "";
            if (GetComponent<ReachTruckOperator>() != null || n.StartsWith("RT") || n.Contains("REACH") || dn.Contains("REACH")) return "REACH TRUCK";
            if (n.StartsWith("DS") || n.Contains("DOCK") || dn.Contains("DOCK")) return "DOCK STOCKER";
            return "EQUIPMENT";
        }
    }

    private string KindLower => KindName.Substring(0, 1) + KindName.Substring(1).ToLowerInvariant();

    private PlacedObject _po;
    private SimulationTimeService _time;
    private AiNavigation _nav;
    private NavMeshAgent _agent;
    private float _baseAgentSpeed = -1f;
    private Vector3 _lastPos;
    private Quaternion _lastRot;
    private float _movedAt = -100f;
    private float _savedCharge = -1f;
    private bool _loaded;
    private Camera _cam;
    private Coroutine _trip;
    private PalletJackParkingSpot _reserved;
    private static bool s_warnedNoStation;

    public static EquipmentBattery Attach(GameObject go)
    {
        if (go == null) return null;
        var b = go.GetComponent<EquipmentBattery>();
        return b != null ? b : go.AddComponent<EquipmentBattery>();
    }

    private void OnEnable() { if (!s_all.Contains(this)) s_all.Add(this); if (LinkId == 0) LinkId = s_nextLinkId++; }
    private void OnDisable()
    {
        s_all.Remove(this);
        if (_reserved != null) { _reserved.Release(Slot); _reserved = null; }
        _trip = null;
        _vacate = null;
        VacatingBay = false;
    }

    private void Start()
    {
        Slot = GetComponent<MHEOperatorSlot>();
        _po = GetComponent<PlacedObject>();
        _nav = GetComponent<AiNavigation>();
        _agent = GetComponent<NavMeshAgent>();
        _lastPos = transform.position;
        _lastRot = transform.rotation;
        // Raising / lowering the forks (and a reach truck's mast) is work too: it drains the battery.
        var parts = new List<Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t != transform && (t.name == "Forks" || t.name == "Mast")) parts.Add(t);
        _lift = parts.ToArray();
        _liftLast = new Vector3[_lift.Length];
        for (int i = 0; i < _lift.Length; i++) _liftLast[i] = _lift[i].localPosition;
        EquipmentBatteryPanels.Ensure();
    }

    private Transform[] _lift;
    private float _strayTime;
    private Coroutine _idle;
    private Coroutine _lap;
    private float _idleMinutes;
    private float _lapAfter = 8f;

    /// <summary>The driver is bored and patrolling for a few minutes ("I'm bored... doing a lap").</summary>
    public bool IsDoingLap { get; private set; }

    private IEnumerator LapRoutine()
    {
        var op = Slot != null ? Slot.CurrentOperator : null;
        if (op == null || _nav == null) { _lap = null; yield break; }
        IsDoingLap = true;
        _nav.SetTaskBusy(false);
        _nav.GoActive(op);   // the vehicle's own patrol along the waypoints
        float duration = Random.Range(2f, 5f);          // game minutes
        float t = 0f;
        while (this != null && t < duration && !IsBusy && !WantsStation && Slot != null && Slot.IsOccupied)
        {
            if (_time == null) ServiceLocator.TryGet(out _time);
            t += Time.deltaTime * (_time != null ? _time.TimeScale : 1f);
            yield return null;
        }
        IsDoingLap = false;
        if (this != null && !IsBusy && !WantsStation) ParkNav();
        _idlePos = null;                                // choose a fresh spot (or a charger) next
        _lapAfter = Random.Range(5f, 15f);
        _lap = null;
    }
    private float _idleWait;
    private bool _idleCharge;
    private Vector3? _idlePos;
    private string _idleName;
    private const float IdleGraceSeconds = 1.5f;

    /// <summary>A job just ended: start looking for somewhere to wait almost at once instead of wandering.</summary>
    public void NoteJobFinished() { _idleWait = Mathf.Max(_idleWait, IdleGraceSeconds - 0.4f); }

    /// <summary>With a driver and no work: charge at a free station if it isn't full, then park out of the way.</summary>
    private IEnumerator IdleRoutine()
    {
        // 1. A free charger: plug in while there is nothing to do.
        if (Charge01 < 0.95f && !IsBusy && PalletJackParkingSpot.FindNearestFree(BodyCentre, Slot) != null)
        {
            _idleCharge = true;
            _wantPark = true;
            _trip = StartCoroutine(ChargeTrip());
            while (this != null && _trip != null) yield return null;
            _idleCharge = false;
            _wantPark = false;
        }

        // 2. Still nothing to do: somewhere out of the way - plain floor, not an aisle, a lane or a doorway.
        // A full machine sitting on a charger with another one waiting for it leaves at once.
        float wait = 0f;
        while (this != null && wait < 3f && !IsBusy)
        {
            if (PalletJackParkingSpot.ChargerAt(BodyCentre) != null && IsFull && AnotherMachineWaiting()) break;
            wait += Time.deltaTime; yield return null;
        }

        if (this != null && !IsBusy && Slot != null && Slot.IsOccupied && !WantsStation)
        {
            bool alreadyParked = _idlePos.HasValue && Flat(BodyCentre - _idlePos.Value).magnitude < 1.5f && PalletJackParkingSpot.ChargerAt(BodyCentre) == null;
            // Never leave a full machine on a charger: try an out-of-the-way spot, then a station's left / right / front or an empty
            // lane, then ANY clear floor - and keep trying if a drive fails.
            bool placed = alreadyParked;
            for (int attempt = 0; attempt < 5 && !placed && !IsBusy && this != null; attempt++)
            {
                MheParking.Spot spot;
                bool found;
                if (attempt < 2) found = MheParking.TryFindIdleSpot(this, out spot);
                else if (attempt == 2)
                {
                    var bay = PalletJackParkingSpot.ChargerAt(BodyCentre);
                    found = bay != null ? MheParking.TryFindSpot(this, bay, out spot) : MheParking.TryFindIdleSpot(this, out spot, true);
                }
                else found = MheParking.TryFindIdleSpot(this, out spot, true);
                if (!found) continue;

                bool ok = false;
                yield return NavTo(spot.Pos, r => ok = r, () => IsBusy);
                if (ok && !IsBusy)
                {
                    Vector3 toSpot = spot.Pos - transform.position; toSpot.y = 0f;
                    if (toSpot.magnitude > 0.05f) { yield return Face(toSpot); yield return MoveStraight(spot.Pos); }
                    yield return Face(spot.Forward);
                    _idlePos = BodyCentre;
                    _idleName = spot.Name;
                    _idleMinutes = 0f;
                    _lapAfter = Random.Range(5f, 15f);   // game minutes parked before the next lap
                    placed = true;
                }
            }
            // A job may have started while we were driving here (the truck claims work the moment it is free): then its controller owns
            // the navigation and switching it off now would kill the leg it is on ("NO NAVMESH PATH ... agentOnMesh=False").
            if (!IsBusy) ParkNav();   // wait here, aboard, with navigation off (no patrol)
        }
        _idle = null;
    }
    private bool _wantPark;
    private EmployeeIdentity _designatedHelper;

    /// <summary>Flat battery, sent to charge, or being taken to a station to park.</summary>
    private bool WantsStation => NeedsCharge || _wantPark;

    public bool IsDesignatedHelper(EmployeeIdentity e) => e != null && e == _designatedHelper;
    private Vector3[] _liftLast;
    private bool _warnedLow;

    private void Update()
    {
        if (!_loaded) LoadCharge();

        // Movement this frame (scaled time: a paused game moves nothing and drains nothing).
        Vector3 d = transform.position - _lastPos; d.y = 0f;
        float turned = Quaternion.Angle(transform.rotation, _lastRot);
        _lastPos = transform.position; _lastRot = transform.rotation;
        bool liftMoved = false;
        if (_lift != null)
            for (int i = 0; i < _lift.Length; i++)
            {
                if (_lift[i] == null) continue;
                Vector3 lp = _lift[i].localPosition;
                if ((lp - _liftLast[i]).sqrMagnitude > 1e-7f) liftMoved = true;
                _liftLast[i] = lp;
            }
        if (Time.deltaTime > 0f && (d.magnitude > MoveEpsilon || turned > TurnEpsilon || liftMoved)) _movedAt = Time.time;
        IsMoving = Time.time - _movedAt < MovingHoldSeconds;

        // Game minutes that passed this frame (1 real second = 1 game minute at 1x; Time.deltaTime already carries the speed).
        if (_time == null) ServiceLocator.TryGet(out _time);
        float minutes = Time.deltaTime * (_time != null ? _time.TimeScale : 1f);

        Station = IsMoving ? null : PalletJackParkingSpot.ChargerAt(BodyCentre);

        bool wasDead = IsDead;
        if (IsMoving)
            Charge01 = Mathf.Max(0f, Charge01 - minutes / (CapacityHours * 60f));
        else if (Station != null)
            Charge01 = Mathf.Min(1f, Charge01 + minutes / (HoursToFull * 60f));

        bool wasCharging = IsCharging;
        IsCharging = Station != null && !IsFull;
        if (wasCharging && !IsCharging && Station != null && IsFull) AnnounceFull();

        // Heads-up at LowHours left: Systems Log line with a link that focuses the machine.
        if (!IsCharging && !NeedsCharge && !IsDead && HoursLeft <= LowHours)
        {
            if (!_warnedLow) { _warnedLow = true; AnnounceLow(); }
        }
        else if (IsCharging || HoursLeft > LowHours + 0.3f) _warnedLow = false;

        if (!wasDead && IsDead) OnBatteryDied();
        // A flat machine nobody has dealt with (e.g. loaded from a save that way): same treatment.
        if (IsDead && !NeedsCharge) OnBatteryDied();

        UpdateDriverless();
        // NOTHING MOVES UNLESS AN EMPLOYEE IS DRIVING IT (Tad, 2026-10-18). A machine that has to go to a charger is either
        // driven there by whoever is aboard (once any job in hand is delivered), or an idle employee is sent to get it.
        bool occupied = Slot != null && Slot.IsOccupied;
        if (WantsStation && _trip == null && _vacate == null && !IsBusy)
        {
            if (occupied) _trip = StartCoroutine(ChargeTrip());
            else StartHelperTask(true);
        }
        // Charged and still on the charger WHILE another machine waits for a bay: an idle employee pulls it off. Otherwise a
        // charged machine just stays put until someone needs it (Tad, 2026-10-18).
        else if (!NeedsCharge && _trip == null && _vacate == null && Station != null && IsFull && IdleAndFree && AnotherMachineWaiting())
            StartHelperTask(false);
        // A pallet jack standing unmanned anywhere but a charger (e.g. loaded from a save that way) is taken to one.
        else if (!WantsStation && _trip == null && _vacate == null && Station == null && !IsMoving && IdleAndFree
                 && Slot != null && Slot.IsPalletJack && PalletJackParkingSpot.FindNearestFree(BodyCentre, Slot) != null)
        {
            _wantPark = true;
            StartHelperTask(true);
            if (_vacate == null) _wantPark = false;   // nobody free right now: ask again later
        }

        // A reach truck / dock stocker with a driver and NO work does not patrol (that drains the battery): it plugs in at a free
        // charger, and if there is still nothing to do it parks somewhere out of the way (Tad, 2026-10-18).
        bool operated = Slot != null && Slot.IsOccupied && !Slot.IsPalletJack;

        // A job was claimed while an IDLE routine (park at a spot / plug in while waiting) was still driving the truck. Those
        // routines move the transform directly (Face/MoveStraight), so they fought the putaway: the truck started its grab
        // off the lane and was dragged metres sideways mid-grab, missing every pallet ("Stephen is stuck on the dock").
        // Idle work must stop the instant real work arrives.
        if (_idle != null && IsBusy)
        {
            StopCoroutine(_idle); _idle = null;
            if (_idleCharge && _trip != null) { StopCoroutine(_trip); _trip = null; }
            _idleCharge = false; _wantPark = false; WaitingForCharger = false; _inNavTo = false;
            if (_reserved != null) { _reserved.Release(Slot); _reserved = null; }
            _idleWait = 0f;
        }

        // Wrong person aboard (e.g. an order selector who only came to pull the truck off a charger and was never sent
        // away): a reach truck / dock stocker does no putaways with them. After a grace period, step them off and call
        // the right operator (Tad, 2026-10-18: "RT1 is operated by an order selector").
        // Pallet jacks count too (2026-10-24): a RECEIVER was left aboard a jack after a charge errand. The jack patrols by itself with
        // a driver aboard, so it carried her out of receiving range again and again, her Receive task was released each time, and the
        // whole inbound side stalled (19 receive tasks waiting, none claimed). Only an order selector belongs on a jack.
        bool riderAboard = Slot != null && Slot.IsOccupied;
        if (riderAboard && _trip == null && _vacate == null && !_helperTrip && !_switchTrip && !NeedsCharge && !(Slot.IsPalletJack && IsBusy))
        {
            var rider = Slot.CurrentOperator;
            bool wrong = rider != null && rider.Record != null && rider.Record.role != HelperRole;
            _wrongRiderTime = wrong ? _wrongRiderTime + Time.deltaTime : 0f;
            if (wrong && _wrongRiderTime > 8f)
            {
                _wrongRiderTime = 0f;
                _ejectedOperator = null;
                DismountOperator();   // not the right role: they go back to patrolling
                if (!Slot.IsPalletJack)   // a jack needs no one called: selectors claim it themselves when they have a pick
                {
                    var right = PickOperatorToRecall();
                    if (right != null) right.GetComponent<AiNavigation>()?.SeekEquipment(Slot);
                }
            }
        }
        else _wrongRiderTime = 0f;
        if (operated && !IsBusy && !WantsStation && _trip == null && _vacate == null && _idle == null && _lap == null)
        {
            bool parkedAtSpot = _idlePos.HasValue && Flat(BodyCentre - _idlePos.Value).magnitude < 1.5f && PalletJackParkingSpot.ChargerAt(BodyCentre) == null;
            bool wantsCharge = Charge01 < 0.6f && PalletJackParkingSpot.FindNearestFree(BodyCentre, Slot) != null;
            if (parkedAtSpot && !wantsCharge)
            {
                // Parked out of the way with still nothing to do: now and then take a lap, 2-5 minutes, then park again.
                _idleWait = 0f;
                _idleMinutes += minutes;
                if (_idleMinutes >= _lapAfter) { _idleMinutes = 0f; _lap = StartCoroutine(LapRoutine()); }
            }
            else
            {
                _idleWait += Time.deltaTime;
                if (_idleWait > IdleGraceSeconds) { _idleWait = 0f; _idle = StartCoroutine(IdleRoutine()); }
            }
        }
        else if (!operated || IsBusy) { _idleWait = 0f; _idleMinutes = 0f; }

        // During a charge trip the vehicle's navigation is on ONLY inside NavTo; anywhere else (Face / MoveStraight / waiting) it must be
        // off, or its own patrol drags the machine around while the trip stands still.
        if (_trip != null && !_inNavTo && !IsBusy && _nav != null && _nav.enabled && Slot != null && Slot.IsOccupied)
        {
            _tripNavStray += Time.deltaTime;
            if (_tripNavStray > 0.6f) { _tripNavStray = 0f; ParkNav(); }
        }
        else _tripNavStray = 0f;

        // Safety net: an unmanned machine runs NO navigation. Anything that leaves a vehicle's agent live with nobody
        // aboard and no job (a boarding that was cut short, an ejected rider) would have it driving itself around.
        if (_nav != null && _nav.enabled && Slot != null && !Slot.IsOccupied && !IsBusy && _trip == null && _vacate == null && _yield == null)
        {
            _strayTime += Time.deltaTime;
            if (_strayTime > 0.6f) { _strayTime = 0f; ParkNav(); }
        }
        else _strayTime = 0f;

        SaveCharge();
        HandleClick();
    }

    // ── Death, driverless delivery, charge requests ──────────────────────────────

    /// <summary>"Reach truck (Aaron Miller)" / "Dock stocker": what the log lines call this machine.</summary>
    public string Label
    {
        get
        {
            string who = Slot != null && Slot.CurrentOperator != null && Slot.CurrentOperator.Record != null
                ? Slot.CurrentOperator.Record.employeeName : null;
            return string.IsNullOrEmpty(who) ? KindLower : $"{KindLower} ({who})";
        }
    }

    // Battery chatter (low / flat / fully charged) is kept OUT of the Systems Log on purpose (Tad, 2026-10-26): the bolt icon over
    // the machine and the battery card already say it, and the log was drowning in it.
    private void AnnounceLow() { }

    private void OnBatteryDied()
    {
        NeedsCharge = true;
        // Pallet jacks: the selector driving it notices (OrderSelectionTaskDriver) and climbs off itself.
        // Reach trucks / dock stockers: the operator stays aboard and drives it there (see ChargeTrip).
    }

    /// <summary>"Switch Machines" (employee info Actions): the driver leaves this machine at a charger and goes to another
    /// unoccupied machine of their kind. Refused (with a speech bubble) when there is none.</summary>
    public bool CanSwitchMachines => Slot != null && Slot.IsOccupied && !NeedsCharge && _trip == null && _vacate == null;

    /// <summary>"Change Battery" (employee info Actions): the driver takes their own machine to a charger, STAYS aboard while it
    /// charges, and carries on when it is full. Available any time they are on a machine that isn't already going to charge.</summary>
    public bool CanChangeBattery => Slot != null && Slot.IsOccupied && !NeedsCharge && _trip == null && _vacate == null && !IsFull;

    /// <summary>True when this machine should go and charge NOW, between jobs: its battery is low (<see cref="LowHours"/>), a
    /// charger is free, and at least one other operated machine of the same kind still has charge to carry on. Staggers
    /// charging so a whole fleet doesn't run flat together and leave the queue unworked. The last healthy machine keeps
    /// working until it must stop.</summary>
    private const float CoverHours = 0.75f;   // the machine covering for us must have at least this much battery left

    public bool ShouldChargeBetweenJobs()
    {
        if (!CanChangeBattery || IsBusy || NeedsCharge || HoursLeft > LowHours) return false;
        if (PalletJackParkingSpot.FindNearestFree(BodyCentre, Slot) == null) return false;   // every charger is taken
        foreach (var other in All)
        {
            if (other == null || other == this || other.KindLower != KindLower) continue;
            if (other.Slot == null || !other.Slot.IsOccupied) continue;
            if (other.NeedsCharge || other.IsCharging || other.HoursLeft <= CoverHours) continue;
            return true;   // someone else of this kind can keep working for a while yet
        }
        return false;
    }

    public void RequestChangeBattery()
    {
        if (!CanChangeBattery) return;
        NeedsCharge = true;
        // (no log line: charging chatter was overkill - Tad, 2026-10-24)
    }

    private bool _switchTrip;
    private bool _helperTrip;
    private System.Action _onHelperDone;

    public bool RequestSwitchMachines()
    {
        if (!CanSwitchMachines) return false;
        var data = GetComponent<PlacedObject>() != null ? GetComponent<PlacedObject>().data : null;
        var next = data != null ? MHESlotFinder.FindUnoccupied(data) : null;
        if (next == null || next == Slot)
        {
            WorldSpeechBubbles.Show(transform, "There's no available equipment");
            return false;
        }
        _switchTrip = true;
        NeedsCharge = true;   // drives to a charger, parks, and the driver steps off to the other machine
        return true;
    }

    private void DismountIfNotStaying()
    {
        if (Slot == null) return;
        if (_switchTrip) { DismountOperator(); return; }
        // A reach truck / dock stocker operator stays aboard while it charges - including one sent to fetch an empty machine.
        // Only someone who isn't that machine's kind of operator (a fetcher from another role) steps off.
        var rider = Slot.CurrentOperator;
        if (_helperTrip && rider != null && rider.Record != null && rider.Record.role != HelperRole) DismountOperator();
    }

    public static bool IsHelper(EmployeeIdentity e) => e != null && s_helpers.Contains(e);

    /// <summary>The Work Queue's ChargeMHE job: <paramref name="who"/> takes this unmanned machine to a charging station.</summary>
    public void BeginChargeTask(EmployeeIdentity who, System.Action onDone)
    {
        if (who == null || _vacate != null) { onDone?.Invoke(); return; }
        _onHelperDone = onDone;
        _wantPark = true;
        _vacate = StartCoroutine(VacateTrip(who, true));
    }

    /// <summary>Where this machine is right now, like an inventory location: "STA-A", "STA-A-L/R/F", "LANE 1B" or "".</summary>
    public string ParkedLocation
    {
        get
        {
            if (Time.time >= _locAt)
            {
                _locAt = Time.time + 0.5f;
                _loc = MheParking.NameAt(this);
                if (string.IsNullOrEmpty(_loc) && _idlePos.HasValue && !string.IsNullOrEmpty(_idleName) && Flat(BodyCentre - _idlePos.Value).magnitude < 1.5f) _loc = _idleName;
            }
            return _loc;
        }
    }
    private string _loc = "";
    private float _locAt;

    public int AgentAreaMask => _agent != null ? _agent.areaMask : NavMesh.AllAreas;

    /// <summary>Half-size of the machine's footprint box (x = half width, y = half height, z = half length), for the parking checks.</summary>
    public Vector3 BodyHalfExtents
    {
        get
        {
            if (Slot != null && Slot.IsPalletJack) return new Vector3(0.65f, 0.8f, 1.95f);
            return KindName == "DOCK STOCKER" ? new Vector3(1.0f, 1.1f, 1.9f) : new Vector3(1.0f, 1.1f, 1.7f);
        }
    }

    /// <summary>After parking on a charger the driver steps off and goes to find another machine of the same kind; with none
    /// free they patrol and a log warning (linked to them) says so.</summary>
    private void DismountOperator()
    {
        if (Slot == null || !Slot.IsOccupied) return;
        var op = Slot.EjectRider();
        if (op == null) return;
        ParkNav();   // EjectRider leaves the vehicle's own navigation as it was: an unmanned machine must run none
        _ejectedOperator = op;
        var nav = op.GetComponent<AiNavigation>();
        if (Slot.IsPalletJack) { nav?.Patrol(); return; }   // a selector doesn't keep a jack: step off, back to work / patrol
        // Someone who was only fetching the machine (not an operator of this kind) just goes back to what they were doing.
        if (op.Record == null || op.Record.role != HelperRole) { nav?.Patrol(); return; }
        var data = GetComponent<PlacedObject>() != null ? GetComponent<PlacedObject>().data : null;
        var next = data != null ? MHESlotFinder.FindUnoccupied(data) : null;
        if (next != null && next != Slot) { nav?.SeekEquipment(next); return; }
        nav?.Patrol();
        string who = op.Record != null ? op.Record.employeeName : "The driver";
        string guid = op.Record != null ? op.Record.employeeGuid : null;
        SystemsLogWindow.LogWarning($"No {KindLower} available: {(string.IsNullOrEmpty(guid) ? who : LogLinks.Employee(guid, who))} is patrolling " +
                                    "until one is free (or send them home / terminate them).");
    }

    private void EjectOperator()
    {
        if (Slot == null || !Slot.IsOccupied) return;
        var op = Slot.EjectRider();
        if (op != null) { _ejectedOperator = op; op.GetComponent<AiNavigation>()?.Patrol(); }
        if (IsBusy) DriverlessRun = true;
    }

    /// <summary>Called by a selector's pallet-jack run when it climbs off a flat jack mid-job.</summary>
    public void BeginDriverlessRun() { if (NeedsCharge) DriverlessRun = true; }

    /// <summary>Double speed on the vehicle's own NavMeshAgent while finishing a job driverless; back to normal after.</summary>
    private void UpdateDriverless()
    {
        if (DriverlessRun && !IsBusy) DriverlessRun = false;
        if (_agent == null) return;
        if (_baseAgentSpeed < 0f) _baseAgentSpeed = _agent.speed;
        float want = _baseAgentSpeed * SpeedMultiplier;
        if (!Mathf.Approximately(_agent.speed, want)) _agent.speed = want;
    }

    /// <summary>Right-click "Charge Battery": only for a free machine (nobody aboard, no job in hand).</summary>
    public bool CanRequestCharge => Slot != null && !Slot.IsOccupied && !IsBusy && _trip == null && _vacate == null && !WantsStation && !IsFull;

    // ── Driving itself to a charger ──────────────────────────────────────────────

    private IEnumerator ChargeTrip()
    {
        while (WantsStation && this != null)
        {
            // Already standing on a charger: just charge.
            if (Station != null || PalletJackParkingSpot.ChargerAt(BodyCentre) != null)
            {
                DismountIfNotStaying();
                while (WantsStation && !IsFull) { if (ClaimedForWork()) break; yield return null; }
                FinishCharging();
                break;
            }

            var spot = PalletJackParkingSpot.FindNearestFree(BodyCentre, Slot);
            bool beside = false;
            if (spot == null)
            {
                spot = PalletJackParkingSpot.FindNearest(BodyCentre);
                beside = true;
            }
            if (spot == null)
            {
                if (!s_warnedNoStation)
                {
                    s_warnedNoStation = true;
                    UIToast.Show("Equipment needs charging but there are no MHE charging stations. Place one (Vehicles).");
                }
                yield return new WaitForSeconds(5f);
                continue;
            }

            if (!beside) { spot.Reserve(Slot); _reserved = spot; }
            Debug.Log($"[EquipmentBattery] {name}#{LinkId} ChargeTrip: heading for a station (beside={beside}) charge={Charge01:F2}");
            spot.ChargePose(Slot, false, out Vector3 park, out Vector3 fwd);
            park.y = transform.position.y;
            if (beside) park = PickQueueSpot(park, fwd);
            Vector3 approach = park - fwd * 3f;

            bool ok = false;
            yield return NavTo(approach, r => ok = r);
            Debug.Log($"[EquipmentBattery] {name}#{LinkId} ChargeTrip: NavTo approach ok={ok} at {transform.position}");
            if (!ok)
            {
                // Couldn't path there: try the bay itself directly (short hops fail on the mesh more than long ones).
                Debug.LogWarning($"[EquipmentBattery] {name}: no path to the charger approach - trying again shortly.");
                if (_reserved != null) { _reserved.Release(Slot); _reserved = null; }
                yield return new WaitForSeconds(3f);
                continue;
            }
            yield return Face(fwd);
            yield return MoveStraight(park);
            Debug.Log($"[EquipmentBattery] {name}#{LinkId} ChargeTrip: reached the bay at {transform.position}");
            DismountIfNotStaying();   // parked: a driver on a battery change STAYS aboard; helpers / Switch Machines step off

            if (beside)
            {
                // Wait beside it until a station frees up, then drive in (next loop pass).
                WaitingForCharger = true;
                while (WantsStation && PalletJackParkingSpot.FindNearestFree(BodyCentre, Slot) == null)
                    yield return new WaitForSeconds(2f);
                WaitingForCharger = false;
                _queueSpot = null;
                continue;
            }

            // Plugged in: charge to full (the Update above does the charging and the toast).
            float notThere = 0f;
            while (WantsStation && !IsFull)
            {
                if (ClaimedForWork()) break;
                notThere = PalletJackParkingSpot.ChargerAt(BodyCentre) == null ? notThere + Time.deltaTime : 0f;
                if (notThere > 3f) break;   // got moved off the charger somehow: go round again
                yield return null;
            }
            if (IsFull) { FinishCharging(); break; }
        }
        if (_reserved != null) { _reserved.Release(Slot); _reserved = null; }
        WaitingForCharger = false;
        _queueSpot = null;
        _switchTrip = false;   // a trip that ended without FinishCharging (job claimed) must not leave the flag set for the next charge
        _trip = null;
    }

    private Coroutine _yield;

    // ── Vacating a charged machine off its station ───────────────────────────────
    private Coroutine _vacate;
    private float _nextVacateCheck;
    private static readonly HashSet<EmployeeIdentity> s_helpers = new();
    private const float SideStep = 2.6f;          // from the bay's centre line to the spot beside it
    private const float VacateBoardTimeout = 90f;

    /// <summary>A free employee is on their way to / driving this charged machine off its station. Holds back new jobs for it.</summary>
    public bool VacatingBay { get; private set; }

    private EmployeeRole HelperRole => KindName == "REACH TRUCK" ? EmployeeRole.ReachTruckOperator
                                     : KindName == "DOCK STOCKER" ? EmployeeRole.DockStockerOperator
                                     : EmployeeRole.OrderSelector;

    private static bool HasAssignedTask(EmployeeIdentity e)
    {
        if (e == null || e.Record == null || string.IsNullOrEmpty(e.Record.employeeGuid)) return false;
        if (!ServiceLocator.TryGet(out WorkQueueSystem queue) || queue == null) return false;
        foreach (var t in queue.GetAllTasks())
            if (t != null && t.Type != WorkTaskType.ChargeMHE && t.Status == WorkTaskStatus.Assigned && t.AssignedToEmployeeGuid == e.Record.employeeGuid) return true;
        return false;
    }

    private EmployeeIdentity FindVacateHelper()
    {
        ServiceLocator.TryGet(out WorkQueueSystem queue);
        var role = HelperRole;
        EmployeeIdentity best = null;
        float bestD = float.MaxValue;
        foreach (var e in FindObjectsByType<EmployeeIdentity>())
        {
            if (e == null || !e.isActiveAndEnabled || e.Record == null) continue;
            // Prefer someone whose job is this kind of machine; with none free, any idle employee on foot will do (not the
            // guard, the boss, drivers...). Otherwise stations stay full of charged machines while others wait at 0%.
            bool matches = e.Record.role == role;
            if (!matches && (e.Record.role == EmployeeRole.Boss || e.Record.role == EmployeeRole.Security || e.Record.role == EmployeeRole.TruckDriver
                             || e.Record.role == EmployeeRole.Exterminator)) continue;
            if (e.Record.status != EmploymentStatus.Active || e.AssignedSlot != null || s_helpers.Contains(e)) continue;
            if (!e.name.StartsWith("Employee_")) continue;   // not the portrait-booth clone
            var en = e.GetComponent<AiNavigation>();
            if (en != null && en.IsSeekingEquipment) continue;   // already on their way to a machine
            if (queue != null && !string.IsNullOrEmpty(e.Record.employeeGuid))
            {
                bool busy = false;
                foreach (var t in queue.GetAllTasks())
                    if (t != null && t.Status == WorkTaskStatus.Assigned && t.AssignedToEmployeeGuid == e.Record.employeeGuid) { busy = true; break; }
                if (busy) continue;
            }
            float d = Vector3.Distance(e.transform.position, transform.position) + (matches ? 0f : 1000f);   // same-role first
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    /// <summary>Beside the bay (left of it first), parked parallel to it, on a spot no other machine or charger occupies.</summary>
    private Vector3 PickSideSpot(Vector3 bayPark)
    {
        Vector3 side = Vector3.Cross(Vector3.up, transform.forward).normalized;
        Vector3[] cands = { bayPark - side * SideStep, bayPark + side * SideStep };
        foreach (var c in cands)
        {
            bool free = PalletJackParkingSpot.ChargerAt(c) == null;
            foreach (var o in s_all)
                if (free && o != null && o != this && Vector3.Distance(Flat(o.BodyCentre), Flat(c)) < 2.2f) free = false;
            if (free) return c;
        }
        return cands[0];
    }

    /// <summary>Throttled: pick a free employee for this machine (to take it to a charger when <paramref name="charge"/>, else to pull it
    /// off its station) and send them. No one free -> nothing moves; it is tried again a few seconds later.</summary>
    private void StartHelperTask(bool charge)
    {
        if (Time.time < _nextVacateCheck) return;
        _nextVacateCheck = Time.time + 3f;
        var helper = FindVacateHelper();
        if (helper != null) _vacate = StartCoroutine(VacateTrip(helper, charge));
    }

    private IEnumerator VacateTrip(EmployeeIdentity helper, bool charge)
    {
        VacatingBay = true;
        _helperTrip = true;
        _designatedHelper = helper;
        s_helpers.Add(helper);
        // The bay it is standing on NOW: once the helper boards, the vehicle's navigation wakes up and starts to drift.
        var bay = Station != null ? Station : PalletJackParkingSpot.ChargerAt(BodyCentre);
        var helperNav = helper.GetComponent<AiNavigation>();
        if (helperNav != null && helperNav.IsSeekingEquipment)
        {
            // SeekEquipment ignores a second request: they are already heading for another machine.
            VacatingBay = false; _helperTrip = false; _designatedHelper = null; s_helpers.Remove(helper); _vacate = null;
            var early = _onHelperDone; _onHelperDone = null; early?.Invoke();
            yield break;
        }
        helperNav?.SeekEquipment(Slot);

        float t = 0f;
        while (this != null && helper != null && t < VacateBoardTimeout && Slot != null
               && Slot.CurrentOperator != helper && !Slot.IsOccupied)
        {
            t += Time.deltaTime;
            // They were given work (or climbed on another machine) on the way: let someone else do this.
            if (helper.AssignedSlot != null || HasAssignedTask(helper)) break;
            yield return null;
        }

        bool boarded = this != null && helper != null && Slot != null && Slot.CurrentOperator == helper;
        if (boarded)
        {
            // Boarding switched the vehicle's own patrol on (GoActive): stop it at once, this employee drives it from here.
            if (_agent != null && _agent.isActiveAndEnabled) { _agent.isStopped = true; _agent.ResetPath(); }
            if (_nav != null) { _nav.CancelSeekPosition(); _nav.SetTaskBusy(true); }

            if (charge)
            {
                _trip = StartCoroutine(ChargeTrip());   // drives to a station, parks, then the driver steps off (DismountOperator)
                while (this != null && _trip != null) yield return null;
            }
            else
            {
                if (bay != null)
                {
                    PalletJackParkingSpot.ReleaseJack(Slot);
                    // Left of the station, else right, else in front - and only if nothing physical is there; else the
                    // nearest empty staging lane. Never on top of, or inside, anything.
                    if (MheParking.TryFindSpot(this, bay, out var spot))
                    {
                        bool ok = false;
                        yield return NavTo(spot.Pos, r => ok = r);
                        if (ok && Slot != null && Slot.CurrentOperator == helper)
                        {
                            // The agent stops ~1 m short of its target: finish the last stretch by hand so the machine ends up
                            // exactly on the spot (parallel to the station, not half on its pad), then square it up.
                            Vector3 toSpot = spot.Pos - transform.position; toSpot.y = 0f;
                            if (toSpot.magnitude > 0.05f) { yield return Face(toSpot); yield return MoveStraight(spot.Pos); }
                            yield return Face(spot.Forward);
                        }
                    }
                    else
                    {
                        SystemsLogWindow.LogWarning($"{LogLinks.Equipment(LinkId, Label)} is charged but has nowhere clear to be parked " +
                                                    "(both sides, the front and every staging lane are taken).");
                    }
                }

                if (Slot != null && Slot.CurrentOperator == helper)
                {
                    if (_nav != null)
                    {
                        _nav.SetTaskBusy(false);   // NavTo parked the vehicle's navigation; the operator keeps the machine (no patrol: IdleRoutine decides)
                    }
                }
            }
        }
        else if (helper != null && helperNav != null)
        {
            // Never got aboard (someone else took it, they were given work, or it timed out). Only walk them back to their
            // patrol when they are actually free - never yank someone off a job or a machine.
            if (helper.AssignedSlot == null && !HasAssignedTask(helper)) helperNav.Patrol();
            if (!NeedsCharge) _wantPark = false;
        }

        VacatingBay = false;
        _helperTrip = false;
        _designatedHelper = null;
        if (helper != null) s_helpers.Remove(helper);
        _vacate = null;
        var done = _onHelperDone; _onHelperDone = null;
        done?.Invoke();
    }
    private bool IdleAndFree => Slot != null && !Slot.IsOccupied && !IsBusy;

    private bool AnotherMachineWaiting()
    {
        foreach (var o in s_all) if (o != null && o != this && o.WaitingForCharger) return true;
        return false;
    }

    /// <summary>Pulls a full, idle machine out of its charging bay to a spot in front of and between the bays (off the
    /// waiting line, which runs straight out from the bay's open end), so a waiting machine can drive in.</summary>
    private IEnumerator YieldBay(PalletJackParkingSpot spot)
    {
        spot.ChargePose(Slot, false, out Vector3 park, out Vector3 fwd);
        Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
        Vector3 target = park - fwd * QueueFirstGap + side * 2.5f;
        target.y = transform.position.y;
        PalletJackParkingSpot.ReleaseJack(Slot);
        bool ok = false;
        yield return NavTo(target, r => ok = r, () => !IdleAndFree || NeedsCharge);
        if (ok && IdleAndFree) yield return Face(fwd);
        _yield = null;
    }

    private Vector3? _queueSpot;
    private const float QueueFirstGap = 4.5f;    // from the bay's charge spot back past its open end
    private const float QueueSpacing = 3.6f;     // one machine length between waiting machines

    /// <summary>Every station taken: wait in a line at the station's OPEN end (behind its bay, never across the
    /// neighbouring bays, which sit only ~5 m apart), each waiting machine on its own spot.</summary>
    private Vector3 PickQueueSpot(Vector3 bayPark, Vector3 fwd)
    {
        for (int k = 0; k < 12; k++)
        {
            Vector3 cand = bayPark - fwd * (QueueFirstGap + k * QueueSpacing);
            bool taken = false;
            foreach (var o in s_all)
            {
                if (o == null || o == this) continue;
                if (o._queueSpot.HasValue && Vector3.Distance(Flat(o._queueSpot.Value), Flat(cand)) < 1.5f) { taken = true; break; }
                if (!o.OnChargeTrip && Vector3.Distance(Flat(o.BodyCentre), Flat(cand)) < 1.5f) { taken = true; break; }
            }
            if (!taken) { _queueSpot = cand; return cand; }
        }
        _queueSpot = bayPark - fwd * QueueFirstGap;
        return _queueSpot.Value;
    }

    /// <summary>Someone took the machine for a job while it only wanted to park (not a flat battery): stop wanting a station.</summary>
    private bool ClaimedForWork()
    {
        if (NeedsCharge) return false;
        if (_idleCharge)
        {
            // Plugged in only because there was nothing to do: leave the moment a job shows up (the driver stays aboard).
            if (!IsBusy) return false;
            _wantPark = false;
            return true;
        }
        if (!IsBusy && !(Slot != null && Slot.IsOccupied)) return false;
        _wantPark = false;
        return true;
    }

    private void FinishCharging()
    {
        NeedsCharge = false;
        _wantPark = false;
        _switchTrip = false;
        // The driver who stayed aboard while it charged just carries on (Tad, 2026-10-18).
        if (Slot != null && Slot.IsOccupied)
        {
            // Stay parked on the charger with navigation off: no patrol. Jobs reach them through the task controllers, and
            // IdleRoutine moves them off the charger if there is still nothing to do.
            if (_nav != null) _nav.SetTaskBusy(false);
            return;
        }
        if (Slot == null || Slot.IsPalletJack) return;   // selectors take jacks per order
        // Call an operator back: the one this machine ejected if they are still free, else the nearest idle
        // operator of this machine's type. (The placement broadcast below only reaches operators hired this
        // session - anyone loaded from a save never hears it, which left the charged truck standing empty.)
        var who = PickOperatorToRecall();
        if (who != null) who.GetComponent<AiNavigation>()?.SeekEquipment(Slot);
        Slot.NotifyPlaced();
    }

    private EmployeeIdentity _ejectedOperator;
    private float _wrongRiderTime;

    private EmployeeIdentity PickOperatorToRecall()
    {
        EmployeeRole role = KindName == "REACH TRUCK" ? EmployeeRole.ReachTruckOperator
                          : KindName == "DOCK STOCKER" ? EmployeeRole.DockStockerOperator
                          : (EmployeeRole)(-1);
        if ((int)role < 0) return null;
        bool Free(EmployeeIdentity e) => e != null && e.isActiveAndEnabled && e.Record != null && e.Record.role == role &&
                                         e.Record.status == EmploymentStatus.Active && e.AssignedSlot == null &&
                                         e.name.StartsWith("Employee_");   // not the portrait-booth clone
        if (Free(_ejectedOperator)) return _ejectedOperator;
        EmployeeIdentity best = null;
        float bestD = float.MaxValue;
        foreach (var e in FindObjectsByType<EmployeeIdentity>())
        {
            if (!Free(e)) continue;
            float d = Vector3.Distance(e.transform.position, transform.position);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    /// <summary>Drives the vehicle under its own NavMeshAgent to near <paramref name="target"/>, then parks its navigation again.</summary>
    private IEnumerator NavTo(Vector3 target, System.Action<bool> done, System.Func<bool> abort = null)
    {
        if (_nav == null || _agent == null) { done(false); yield break; }
        if (Vector3.Distance(Flat(transform.position), Flat(target)) < 1.5f) { done(true); yield break; }

        _nav.enabled = true;
        _agent.enabled = true;
        float reg = 0f;
        while (!_agent.isOnNavMesh && reg < 1f) { _agent.Warp(transform.position); reg += Time.deltaTime; yield return null; }
        if (!_agent.isOnNavMesh) { ParkNav(); done(false); yield break; }
        _agent.Warp(transform.position);
        _nav.SetParked(false);
        _nav.SetTaskBusy(true);
        _nav.CancelSeekPosition();

        Vector3 drive = target;
        if (NavMesh.SamplePosition(target, out var hit, 4f, _agent.areaMask)) drive = hit.position;
        bool arrived = false;
        _nav.SeekPosition(drive, () => arrived = true);
        _inNavTo = true;
        float t = 0f, lastFix = 0f, noProgress = 0f;
        float best = Flat(transform.position - drive).magnitude;
        while (!arrived && t < NavLegTimeout && this != null)
        {
            t += Time.deltaTime;
            if (!_nav.IsSeekingTask && !arrived) break;

            // Backstops (an AiNavigation seek can silently lose its destination to a patrol waypoint, which left trucks at 0%
            // wandering for minutes): keep the agent pointed at the target, count arrival by distance, give up if it isn't getting closer.
            float dist = Flat(transform.position - drive).magnitude;
            if (dist < 1.3f) { arrived = true; break; }
            if (dist < best - 0.5f) { best = dist; noProgress = 0f; } else noProgress += Time.deltaTime;
            if (noProgress > 12f) break;
            if (t - lastFix > 1f)
            {
                lastFix = t;
                _nav.SetTaskBusy(true);
                if (_agent.isActiveAndEnabled && _agent.isOnNavMesh && Flat(_agent.destination - drive).magnitude > 1.5f)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(drive);
                }
            }
            if (abort != null && abort())
            {
                // Someone else took the machine over (a selector claimed the jack, an operator boarded):
                // hand its navigation back untouched rather than parking it under them.
                _nav.CancelSeekPosition();
                _nav.SetTaskBusy(false);
                _inNavTo = false;
                done(false);
                yield break;
            }
            yield return null;
        }
        _inNavTo = false;
        if (!IsBusy) ParkNav();   // same reason: never switch navigation off under a job that has just started
        done(arrived);
    }

    private bool _inNavTo;
    private float _tripNavStray;

    private void ParkNav()
    {
        if (_nav == null) return;
        _nav.CancelSeekPosition();
        _nav.SetTaskBusy(false);
        // Unmanned machines run no navigation at all. With a driver aboard the driver is remembered, so the task controllers
        // (which just re-enable the component for their NavMesh legs) wake it correctly instead of finding it "parked".
        if (Slot != null && Slot.IsOccupied) _nav.GoIdleKeepOperator();
        else _nav.GoIdle();
    }

    private IEnumerator Face(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) yield break;
        Quaternion goal = Quaternion.LookRotation(dir.normalized);
        while (this != null && Quaternion.Angle(transform.rotation, goal) > 0.5f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, TripTurnSpeed * Time.deltaTime);
            yield return null;
        }
        if (this != null) transform.rotation = goal;
    }

    private IEnumerator MoveStraight(Vector3 target)
    {
        target.y = transform.position.y;
        while (this != null && Vector3.Distance(Flat(transform.position), Flat(target)) > 0.02f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, TripDriveSpeed * Time.deltaTime);
            yield return null;
        }
        if (this != null) transform.position = target;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // ── Notifications, persistence, clicks ───────────────────────────────────────

    /// <summary>Log line + toast when a machine finishes charging (Tad: the toast may go later; if it does, call
    /// SystemsLogWindow.LogSystem here instead so the log line stays).</summary>
    private void AnnounceFull()
    {
        string letter = Station != null ? Station.GetComponent<MHEStationLetter>()?.Letter : null;
        string where = string.IsNullOrEmpty(letter) ? "on its charger" : $"at Station {letter}";
        // no toast / log line: every toast is also written to the Systems Log, and "fully charged" is not worth a log entry
    }

    private void LoadCharge()
    {
        _loaded = true;
        if (_po == null) _po = GetComponent<PlacedObject>();
        string cd = _po != null ? _po.customData : null;
        bool haveCap = false;
        if (!string.IsNullOrEmpty(cd) && cd.StartsWith(Key))
        {
            // "batt=0.4310;cap=10.20;need" - segments in any order after the first.
            foreach (var seg in cd.Split(';'))
            {
                if (seg.StartsWith(Key))
                {
                    if (float.TryParse(seg.Substring(Key.Length), System.Globalization.NumberStyles.Float,
                                       System.Globalization.CultureInfo.InvariantCulture, out float c))
                        Charge01 = Mathf.Clamp01(c);
                }
                else if (seg.StartsWith(CapKey))
                {
                    if (float.TryParse(seg.Substring(CapKey.Length), System.Globalization.NumberStyles.Float,
                                       System.Globalization.CultureInfo.InvariantCulture, out float cap) && cap >= 1f)
                    { CapacityHours = cap; haveCap = true; }
                }
                else if (seg == NeedTag.TrimStart(';')) NeedsCharge = true;   // saved mid-charge / on its way: finish charging before anyone boards
            }
        }
        // A new machine (or one saved before batteries had a size): roll its own.
        if (!haveCap) CapacityHours = Mathf.Round(Random.Range(MinCapacityHours, MaxCapacityHours) * 10f) / 10f;
        _savedCharge = Charge01;
        _savedNeed = NeedsCharge;
        if (IsDead) NeedsCharge = true;   // saved flat: still needs its trip to a charger
        WriteCustomData();
    }

    private const string NeedTag = ";need";
    private const string CapKey = "cap=";
    private bool _savedNeed;

    private void WriteCustomData()
    {
        if (_po == null) return;
        if (!string.IsNullOrEmpty(_po.customData) && !_po.customData.StartsWith(Key)) return;   // someone else's field
        _po.customData = Key + Charge01.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)
                       + ";" + CapKey + CapacityHours.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                       + (NeedsCharge ? NeedTag : "");
    }

    /// <summary>Writes the charge (and the needs-charge flag) into customData every 0.2% so a save always has it
    /// (only if customData is ours). "batt=0.4310;cap=10.2;need" = charge, battery size, charging / on its way to charge.</summary>
    private void SaveCharge()
    {
        if (_po == null) return;
        if (Mathf.Abs(Charge01 - _savedCharge) < 0.002f && _savedNeed == NeedsCharge) return;
        _savedCharge = Charge01;
        _savedNeed = NeedsCharge;
        WriteCustomData();
    }

    /// <summary>Plain left-click on the machine toggles its battery panel (shift-click is the follow camera's).</summary>
    private void HandleClick()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EmployeeHighlighter.ShiftHeld) return;
        if ((EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) || UIInputGuard.IsPointerOverUIToolkit()) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!WorldClickPick.Raycast(ray, out var hit)) return;
        if (hit.collider.GetComponentInParent<EquipmentBattery>() != this) return;
        EquipmentBatteryPanels.Instance.Toggle(this);
    }

}
