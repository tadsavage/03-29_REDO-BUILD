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

    // Snapshot of every Animator swapped onto the drive controller, so VacateOperator can put
    // each one back exactly as it was (root "blackboard" Animator + the modular avatar's own).
    private readonly List<Animator> _swappedAnimators = new();
    private readonly List<RuntimeAnimatorController> _savedControllers = new();

    public EmployeeIdentity CurrentOperator { get; private set; }
    public bool IsOccupied => CurrentOperator != null;

    private void Awake()
    {
        _vehicleNav = GetComponent<AiNavigation>();
    }

    private void OnEnable()
    {
        // Broadcast to idle operators that this equipment is now available
        MHEPlacementEvent.BroadcastEquipmentPlaced(this);
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

        _employeeUI.Show(CurrentOperator.Record);
    }

    public void AssignOperator(EmployeeIdentity identity)
    {
        if (identity == null || IsOccupied) return;

        Transform anchor = _operatorAnchor != null ? _operatorAnchor : transform;
        Transform t = identity.transform;
        t.SetParent(anchor, worldPositionStays: false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;

        var operatorAgent = identity.GetComponent<NavMeshAgent>();
        if (operatorAgent != null) operatorAgent.enabled = false;
        var operatorNav = identity.GetComponent<AiNavigation>();
        if (operatorNav != null) operatorNav.enabled = false;

        if (_operatorDriveController != null)
        {
            _swappedAnimators.Clear();
            _savedControllers.Clear();
            foreach (var anim in identity.GetComponentsInChildren<Animator>(true))
            {
                _swappedAnimators.Add(anim);
                _savedControllers.Add(anim.runtimeAnimatorController);
                anim.runtimeAnimatorController = _operatorDriveController;
                anim.Rebind();
            }
        }

        identity.GetComponent<AgentAnimation>()?.SetRidingMHE(true);

        // Hide operator's NoWaypointIndicator while riding (they're not waving, vehicle is driving)
        var operatorIndicator = identity.GetComponent<NoWaypointIndicator>();
        if (operatorIndicator != null) operatorIndicator.gameObject.SetActive(false);

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

        for (int i = 0; i < _swappedAnimators.Count; i++)
        {
            var anim = _swappedAnimators[i];
            if (anim == null) continue;
            anim.runtimeAnimatorController = _savedControllers[i];
            anim.Rebind();
        }
        _swappedAnimators.Clear();
        _savedControllers.Clear();

        identity.GetComponent<AgentAnimation>()?.SetRidingMHE(false);

        // Restore operator's NoWaypointIndicator visibility (they're no longer riding)
        var operatorIndicator = identity.GetComponent<NoWaypointIndicator>();
        if (operatorIndicator != null) operatorIndicator.gameObject.SetActive(true);

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
