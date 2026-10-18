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
    public const float HoursToEmpty = 8f;
    public const float HoursToFull = 2f;
    public const float LowHours = 1f;
    public const float DriverlessSpeedMultiplier = 2f;
    private const string Key = "batt=";
    private const float MoveEpsilon = 0.003f;      // metres per frame that counts as moving
    private const float TurnEpsilon = 0.25f;       // degrees per frame that counts as moving (turning in place)
    private const float MovingHoldSeconds = 0.5f;  // stays "in use" this long after the last movement (no flicker)
    private const float TripDriveSpeed = 2.0f;     // m/s, the scripted last metres into the bay
    private const float TripTurnSpeed = 120f;
    private const float NavLegTimeout = 90f;

    private static readonly List<EquipmentBattery> s_all = new();
    public static IReadOnlyList<EquipmentBattery> All => s_all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { s_all.Clear(); s_warnedNoStation = false; }

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

    public float HoursLeft => Charge01 * HoursToEmpty;
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
            string n = name.ToUpperInvariant();
            if (n.StartsWith("RT")) return "REACH TRUCK";
            if (n.StartsWith("DS")) return "DOCK STOCKER";
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

    private void OnEnable() { if (!s_all.Contains(this)) s_all.Add(this); }
    private void OnDisable()
    {
        s_all.Remove(this);
        if (_reserved != null) { _reserved.Release(Slot); _reserved = null; }
        _trip = null;
    }

    private void Start()
    {
        Slot = GetComponent<MHEOperatorSlot>();
        _po = GetComponent<PlacedObject>();
        _nav = GetComponent<AiNavigation>();
        _agent = GetComponent<NavMeshAgent>();
        _lastPos = transform.position;
        _lastRot = transform.rotation;
        EquipmentBatteryPanels.Ensure();
    }

    private void Update()
    {
        if (!_loaded) LoadCharge();

        // Movement this frame (scaled time: a paused game moves nothing and drains nothing).
        Vector3 d = transform.position - _lastPos; d.y = 0f;
        float turned = Quaternion.Angle(transform.rotation, _lastRot);
        _lastPos = transform.position; _lastRot = transform.rotation;
        if (Time.deltaTime > 0f && (d.magnitude > MoveEpsilon || turned > TurnEpsilon)) _movedAt = Time.time;
        IsMoving = Time.time - _movedAt < MovingHoldSeconds;

        // Game minutes that passed this frame (1 real second = 1 game minute at 1x; Time.deltaTime already carries the speed).
        if (_time == null) ServiceLocator.TryGet(out _time);
        float minutes = Time.deltaTime * (_time != null ? _time.TimeScale : 1f);

        Station = IsMoving ? null : PalletJackParkingSpot.ChargerAt(BodyCentre);

        bool wasDead = IsDead;
        if (IsMoving)
            Charge01 = Mathf.Max(0f, Charge01 - minutes / (HoursToEmpty * 60f));
        else if (Station != null)
            Charge01 = Mathf.Min(1f, Charge01 + minutes / (HoursToFull * 60f));

        bool wasCharging = IsCharging;
        IsCharging = Station != null && !IsFull;
        if (wasCharging && !IsCharging && Station != null && IsFull) AnnounceFull();

        if (!wasDead && IsDead) OnBatteryDied();
        // A flat machine nobody has dealt with (e.g. loaded from a save that way): same treatment.
        if (IsDead && !NeedsCharge) OnBatteryDied();
        // Someone boarded a flat machine anyway (save restore, an older code path): put them back on their feet.
        if (NeedsCharge && Slot != null && !Slot.IsPalletJack && Slot.IsOccupied) EjectOperator();

        UpdateDriverless();
        if (NeedsCharge && _trip == null && !IsBusy && !(Slot != null && Slot.IsOccupied))
        {
            if (_yield != null) { StopCoroutine(_yield); _yield = null; ParkNav(); }
            _trip = StartCoroutine(ChargeTrip());
        }
        // Full and idle on a charger while another machine waits for one: give the bay up. Pallet jacks park in
        // these bays after every order, so without this three jacks would keep a reach truck waiting forever.
        if (!NeedsCharge && _trip == null && _yield == null && Station != null && IsFull && IdleAndFree && AnotherMachineWaiting())
            _yield = StartCoroutine(YieldBay(Station));

        SaveCharge();
        HandleClick();
    }

    // ── Death, driverless delivery, charge requests ──────────────────────────────

    private void OnBatteryDied()
    {
        NeedsCharge = true;
        string msg = $"{KindLower} ran out of battery - it will finish its job and drive itself to a charger.";
        UIToast.Show(msg);   // the toast also writes the Systems Log
        // Pallet jacks: the selector driving it notices (OrderSelectionTaskDriver) and climbs off itself.
        if (Slot != null && !Slot.IsPalletJack) EjectOperator();
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
    public bool CanRequestCharge => Slot != null && !Slot.IsOccupied && !IsBusy && _trip == null && !IsFull;

    public void RequestCharge()
    {
        if (!CanRequestCharge) return;
        NeedsCharge = true;
    }

    // ── Driving itself to a charger ──────────────────────────────────────────────

    private IEnumerator ChargeTrip()
    {
        while (NeedsCharge && this != null)
        {
            // Already standing on a charger: just charge.
            if (Station != null || PalletJackParkingSpot.ChargerAt(BodyCentre) != null)
            {
                while (NeedsCharge && !IsFull) yield return null;
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
            spot.ChargePose(Slot, false, out Vector3 park, out Vector3 fwd);
            park.y = transform.position.y;
            if (beside) park = PickQueueSpot(park, fwd);
            Vector3 approach = park - fwd * 3f;

            bool ok = false;
            yield return NavTo(approach, r => ok = r);
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

            if (beside)
            {
                // Wait beside it until a station frees up, then drive in (next loop pass).
                WaitingForCharger = true;
                while (NeedsCharge && PalletJackParkingSpot.FindNearestFree(BodyCentre, Slot) == null)
                    yield return new WaitForSeconds(2f);
                WaitingForCharger = false;
                _queueSpot = null;
                continue;
            }

            // Plugged in: charge to full (the Update above does the charging and the toast).
            float notThere = 0f;
            while (NeedsCharge && !IsFull)
            {
                notThere = PalletJackParkingSpot.ChargerAt(BodyCentre) == null ? notThere + Time.deltaTime : 0f;
                if (notThere > 3f) break;   // got moved off the charger somehow: go round again
                yield return null;
            }
            if (IsFull) { FinishCharging(); break; }
        }
        if (_reserved != null) { _reserved.Release(Slot); _reserved = null; }
        WaitingForCharger = false;
        _queueSpot = null;
        _trip = null;
    }

    private Coroutine _yield;
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

    private void FinishCharging()
    {
        NeedsCharge = false;
        if (Slot == null || Slot.IsPalletJack) return;   // selectors take jacks per order
        // Call an operator back: the one this machine ejected if they are still free, else the nearest idle
        // operator of this machine's type. (The placement broadcast below only reaches operators hired this
        // session - anyone loaded from a save never hears it, which left the charged truck standing empty.)
        var who = PickOperatorToRecall();
        if (who != null) who.GetComponent<AiNavigation>()?.SeekEquipment(Slot);
        Slot.NotifyPlaced();
    }

    private EmployeeIdentity _ejectedOperator;

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
        foreach (var e in FindObjectsByType<EmployeeIdentity>(FindObjectsSortMode.None))
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
        float t = 0f;
        while (!arrived && t < NavLegTimeout && this != null)
        {
            t += Time.deltaTime;
            if (!_nav.IsSeekingTask && !arrived) break;
            if (abort != null && abort())
            {
                // Someone else took the machine over (a selector claimed the jack, an operator boarded):
                // hand its navigation back untouched rather than parking it under them.
                _nav.CancelSeekPosition();
                _nav.SetTaskBusy(false);
                done(false);
                yield break;
            }
            yield return null;
        }
        ParkNav();
        done(arrived);
    }

    private void ParkNav()
    {
        if (_nav == null) return;
        _nav.CancelSeekPosition();
        _nav.SetTaskBusy(false);
        _nav.GoIdle();   // stops cleanly and turns the vehicle's navigation off (unmanned machines run none)
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
        UIToast.Show($"{KindLower} {where} is fully charged.");   // the toast also writes the Systems Log
    }

    private void LoadCharge()
    {
        _loaded = true;
        if (_po == null) _po = GetComponent<PlacedObject>();
        string cd = _po != null ? _po.customData : null;
        if (!string.IsNullOrEmpty(cd) && cd.StartsWith(Key))
        {
            string body = cd.Substring(Key.Length);
            bool need = body.EndsWith(NeedTag);
            if (need) body = body.Substring(0, body.Length - NeedTag.Length);
            if (float.TryParse(body, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out float c))
                Charge01 = Mathf.Clamp01(c);
            if (need) NeedsCharge = true;   // saved mid-charge / on its way: finish charging before anyone boards
        }
        _savedCharge = Charge01;
        _savedNeed = NeedsCharge;
        if (IsDead) NeedsCharge = true;   // saved flat: still needs its trip to a charger
    }

    private const string NeedTag = ";need";
    private bool _savedNeed;

    /// <summary>Writes the charge (and the needs-charge flag) into customData every 0.2% so a save always has it
    /// (only if customData is ours). "batt=0.4310;need" = charging / on its way to charge.</summary>
    private void SaveCharge()
    {
        if (_po == null) return;
        if (Mathf.Abs(Charge01 - _savedCharge) < 0.002f && _savedNeed == NeedsCharge) return;
        if (!string.IsNullOrEmpty(_po.customData) && !_po.customData.StartsWith(Key)) return;   // someone else's field
        _savedCharge = Charge01;
        _savedNeed = NeedsCharge;
        _po.customData = Key + Charge01.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + (NeedsCharge ? NeedTag : "");
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
        if (!Physics.Raycast(ray, out var hit)) return;
        if (hit.collider.GetComponentInParent<EquipmentBattery>() != this) return;
        EquipmentBatteryPanels.Instance.Toggle(this);
    }

}
