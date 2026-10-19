using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// Lives on an MHE root (RT_Full, DS_FULL, PltJack_FULL) alongside AiNavigation.
/// Boards/vacates an employee as this vehicle's visual operator. The VEHICLE drives the
/// MHE waypoint loop (AiNavigation, unchanged); the operator just rides along, playing a
/// drive-loop animation, and waves with the "?" emote if the vehicle gets stuck — reusing
/// the existing wave/indicator wiring repointed at the vehicle's own AiNavigation.
/// </summary>
[RequireComponent(typeof(AiNavigation))]
public class MHEOperatorSlot : MonoBehaviour
{
    [Tooltip("Where the operator is parented and positioned while riding. For RT_Full this is " +
             "the existing (now-disabled) WorkerNew transform, reused as a pure position/rotation " +
             "reference. Falls back to this vehicle's own transform if left unassigned.")]
    [SerializeField] private Transform _operatorAnchor;

    [Tooltip("Animator controller swapped onto the operator while riding (e.g. DockStocker.controller " +
             "or PalletJack.controller). Left null for Reach Truck — no drive clip exists for it yet, " +
             "so its operator keeps its normal worker controller and just idles in place.")]
    [SerializeField] private RuntimeAnimatorController _operatorDriveController;

    private AiNavigation _vehicleNav;
    private Camera _mainCamera;
    private EmployeeInfoUI _employeeUI;

    public EmployeeIdentity CurrentOperator { get; private set; }
    public bool IsOccupied => CurrentOperator != null;

    private void Awake()
    {
        _vehicleNav = GetComponent<AiNavigation>();
    }

    /// <summary>
    /// Call after a REAL placement (PlacementFinalizer/PlacementSystem, post-Initialize) to
    /// notify idle operators this equipment is available. Deliberately NOT wired to
    /// OnEnable/Start: those fire synchronously during Instantiate, including for
    /// PreviewController's build-menu hover ghost — which is a full clone of this prefab
    /// (component-stripping can't run until AFTER Instantiate returns, so OnEnable can't be
    /// guarded against it) and was firing this broadcast at the ghost's instantiate-time
    /// position (often (0,0,0)) every time a player just hovered a Reach Truck/Dock Stocker
    /// in the build menu, sending real operators off to seek a never-placed ghost forever.
    /// </summary>
    public void NotifyPlaced()
    {
        MHEPlacementEvent.BroadcastEquipmentPlaced(this);
    }
    private void Start()
    {
        // Only a REAL placed vehicle still has its AiNavigation by now (the build-menu hover ghost is stripped of
        // it right after Instantiate), so only real vehicles get floor-snapping and a contact shadow.
        if (GetComponent<AiNavigation>() != null)
        {
            VehicleGrounding.Attach(gameObject);
            EquipmentBattery.Attach(gameObject);   // 8 h of use, 2 h on a charger (Tad, 2026-10-17)
        }
    }


    // The vehicle's own (large, parent-optimized) BoxCollider sits in front of the much smaller
    // operator collider in any raycast, so EmployeeClickHandler on the operator itself almost
    // never wins the hit test while they're riding. Forward clicks on the vehicle's collider to
    // whoever is currently riding instead — mirrors EmployeeClickHandler's own raycast pattern.
    private void Update()
    {
        if (CurrentOperator == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        if ((UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            || UIInputGuard.IsPointerOverUIToolkit())
        {
            return;
        }

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit) || hit.collider.gameObject != gameObject) return;

        if (_employeeUI == null) _employeeUI = FindAnyObjectByType<EmployeeInfoUI>();
        if (_employeeUI == null || CurrentOperator.Record == null) return;

        // Same rule as clicking a person on foot: a plain click while their banner or card is up puts both away.
        if (!EmployeeHighlighter.ShiftHeld)
        {
            bool bannerUp = EmployeeWorldBanners.Instance.IsShown(CurrentOperator);
            bool cardUp = _employeeUI.IsVisible && _employeeUI.CurrentIdentity == CurrentOperator;
            if (bannerUp || cardUp)
            {
                EmployeeWorldBanners.Instance.Hide(CurrentOperator);
                _employeeUI.Hide();
                AudioManager.Play("UIClick");
                return;
            }
        }
        _employeeUI.Show(CurrentOperator.Record);
        AudioManager.Play("UIClick");
        // Shift-click on the machine: outline the WHOLE machine (the rider is parented under it) and follow it, after Show drops any older focus.
        if (EmployeeHighlighter.ShiftHeld) EmployeeHighlighter.Instance.FocusAndHighlight(CurrentOperator);
        // Floating banner over the operator (same rule as clicking a person on foot).
        EmployeeWorldBanners.Instance.Show(CurrentOperator);
    }

    // Riders use the SAME controllers as everyone else (MaleStaff / FemaleStaff); the drive pose is just
    // the "DriveStyle" int parameter (0 = on foot, 1 = sit-drive, 2 = pallet jack). The old
    // _operatorDriveController field is kept only as a marker for which style this vehicle uses.
    private int DriveStyle()
    {
        if (_operatorDriveController == null) return 1;
        return _operatorDriveController.name.IndexOf("Pallet", System.StringComparison.OrdinalIgnoreCase) >= 0 ? 2 : 1;
    }

    /// <summary>True for a pallet jack (walk-behind / stand-on). Order selectors borrow these per order.</summary>
    public bool IsPalletJack => DriveStyle() == 2;

    /// <summary>Where the rider stands while driving.</summary>
    public Transform OperatorAnchor => _operatorAnchor != null ? _operatorAnchor : transform;

    /// <summary>Order-selector use: the rider hops off to pick a case but KEEPS the machine (still
    /// CurrentOperator, own navigation stays off, vehicle stays parked). The caller walks them around by
    /// script and calls <see cref="StepOn"/> to get back on.</summary>
    public void StepOff()
    {
        if (CurrentOperator == null) return;
        SetDriveStyle(CurrentOperator, 0);
        CurrentOperator.transform.SetParent(null, worldPositionStays: true);
    }

    /// <summary>Back onto the machine after <see cref="StepOff"/>.</summary>
    public void StepOn()
    {
        if (CurrentOperator == null) return;
        Transform t = CurrentOperator.transform;
        t.SetParent(OperatorAnchor, worldPositionStays: false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;
        SetDriveStyle(CurrentOperator, DriveStyle());
    }

    private static void SetDriveStyle(EmployeeIdentity identity, int style)
    {
        foreach (var anim in identity.GetComponentsInChildren<Animator>(true))
        {
            if (anim == null || anim.runtimeAnimatorController == null) continue;
            foreach (var p in anim.parameters)
                if (p.name == "DriveStyle" && p.type == AnimatorControllerParameterType.Int) { anim.SetInteger("DriveStyle", style); break; }
        }
    }

    /// <summary>False while the battery is flat or the machine is on its way to / sitting on a charger
    /// (EquipmentBattery.NeedsCharge). Operators don't climb aboard until it is full again.</summary>
    /// <summary>The battery is flat or the machine is going to charge (EquipmentBattery.NeedsCharge): an operator aboard
    /// takes it to the charger instead of working.</summary>
    public bool BatteryFlat
    {
        get { var b = GetComponent<EquipmentBattery>(); return b != null && (b.NeedsCharge || b.VacatingBay); }
    }

    /// <summary>Like <see cref="AcceptsOperator"/>, but also true for the employee EquipmentBattery sent to take THIS machine
    /// to (or off) a charger: a machine that needs charging is boarded only by its designated helper.</summary>
    public bool AcceptsOperatorFrom(EmployeeIdentity who)
    {
        if (AcceptsOperator) return true;
        var b = GetComponent<EquipmentBattery>();
        return b != null && b.IsDesignatedHelper(who);
    }

    public bool AcceptsOperator
    {
        get
        {
            var b = GetComponent<EquipmentBattery>();
            return b == null || !b.NeedsCharge;
        }
    }

    /// <summary>
    /// Battery died: the rider climbs off and goes back to patrolling, but the MACHINE keeps running (Tad,
    /// 2026-10-17: it finishes delivering what is on its forks, then drives itself to a charger). Same as
    /// <see cref="VacateOperator"/> for the person, WITHOUT parking the vehicle's own navigation - GoIdle there
    /// would freeze a reach truck or pallet jack mid-delivery.
    /// </summary>
    public EmployeeIdentity EjectRider()
    {
        EmployeeIdentity identity = CurrentOperator;
        if (identity == null) return null;

        SetDriveStyle(identity, 0);
        identity.GetComponent<AgentAnimation>()?.SetRidingMHE(false);
        var operatorIndicator = identity.GetComponent<NoWaypointIndicator>();
        if (operatorIndicator != null) operatorIndicator.enabled = true;
        var operatorBubble = identity.GetComponent<EmoteBubble>();
        if (operatorBubble != null) operatorBubble.enabled = true;

        identity.transform.SetParent(null, worldPositionStays: true);
        // Step down beside the machine rather than inside it.
        identity.transform.position += transform.right * 1.2f;

        var operatorAgent = identity.GetComponent<NavMeshAgent>();
        var operatorNav = identity.GetComponent<AiNavigation>();
        if (operatorNav != null) operatorNav.enabled = true;
        if (operatorAgent != null) { operatorAgent.enabled = true; if (operatorAgent.isOnNavMesh) operatorAgent.Warp(identity.transform.position); }

        identity.AssignedSlot = null;
        CurrentOperator = null;
        return identity;
    }

    public void AssignOperator(EmployeeIdentity identity)
    {
        if (identity == null || IsOccupied) return;
        if (!AcceptsOperatorFrom(identity)) return;   // flat battery / charging: see EquipmentBattery

        Transform anchor = _operatorAnchor != null ? _operatorAnchor : transform;
        Transform t = identity.transform;
        t.SetParent(anchor, worldPositionStays: false);
        t.localPosition = Vector3.zero;
        // Inherit the anchor's own rotation exactly — per-vehicle facing/position is tuned
        // directly on each prefab's operator anchor transform (RT_Full / DS_FULL), not here.
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;

        var operatorAgent = identity.GetComponent<NavMeshAgent>();
        if (operatorAgent != null) operatorAgent.enabled = false;
        var operatorNav = identity.GetComponent<AiNavigation>();
        if (operatorNav != null) operatorNav.enabled = false;

        identity.GetComponent<AgentAnimation>()?.SetRidingMHE(true);
        SetDriveStyle(identity, DriveStyle());

        // Hide operator's NoWaypointIndicator while riding (they're not waving, vehicle is driving).
        // NoWaypointIndicator RequireComponents AiNavigation/NavMeshAgent, both of which live on
        // this SAME root GameObject as EmployeeIdentity — so .gameObject.SetActive(false) here
        // disabled the operator's ENTIRE GameObject (mesh, Animator, everything), not just the
        // indicator. Disable the component instead.
        var operatorIndicator = identity.GetComponent<NoWaypointIndicator>();
        if (operatorIndicator != null) { operatorIndicator.ForceClear(); operatorIndicator.enabled = false; }

        // Only the VEHICLE's bubble is active while occupied (enabled in AiNavigation.GoActive,
        // called below) — the rider's own bubble must stay off so the two parented objects are
        // never both showing one at once.
        var operatorBubble = identity.GetComponent<EmoteBubble>();
        if (operatorBubble != null) { operatorBubble.ForceHide(); operatorBubble.enabled = false; }

        identity.AssignedSlot = this;
        CurrentOperator = identity;

        _vehicleNav.GoActive(identity);
    }

    /// <summary>Returns the departing operator so the caller (termination flow) can hand them
    /// off to the walk-out sequence. Returns null if the slot was already empty.</summary>
    public EmployeeIdentity VacateOperator()
    {
        EmployeeIdentity identity = CurrentOperator;
        if (identity == null) return null;

        SetDriveStyle(identity, 0);
        identity.GetComponent<AgentAnimation>()?.SetRidingMHE(false);

        // Restore operator's NoWaypointIndicator (they're no longer riding)
        var operatorIndicator = identity.GetComponent<NoWaypointIndicator>();
        if (operatorIndicator != null) operatorIndicator.enabled = true;

        // Restore operator's own bubble (the vehicle's gets disabled in AiNavigation.GoIdle,
        // called below)
        var operatorBubble = identity.GetComponent<EmoteBubble>();
        if (operatorBubble != null) operatorBubble.enabled = true;

        var operatorNav = identity.GetComponent<AiNavigation>();
        if (operatorNav != null) operatorNav.enabled = true;
        var operatorAgent = identity.GetComponent<NavMeshAgent>();
        if (operatorAgent != null) operatorAgent.enabled = true;

        identity.transform.SetParent(null, worldPositionStays: true);
        identity.AssignedSlot = null;
        CurrentOperator = null;

        _vehicleNav.GoIdle();

        return identity;
    }
}
