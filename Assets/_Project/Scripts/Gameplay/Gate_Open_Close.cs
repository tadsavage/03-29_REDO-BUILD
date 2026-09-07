using UnityEngine;

/// <summary>
/// Attach to Gate_Arm_Base alongside a Trigger BoxCollider.
/// When any NavMesh agent enters the trigger the arm rotates from rot_down
/// to rot_up at arm_speed deg/s. It closes again once all agents have exited.
///
/// Assign the child arm object to 'Arm Pivot' in the Inspector (e.g. Gate_Arm_Animatable).
/// If left empty the script rotates this object itself.
/// </summary>
public class Gate_Open_Close : MonoBehaviour
{
    [Header("Arm Reference")]
    [Tooltip("The child object that physically rotates (e.g. Gate_Arm_Animatable). Leave empty to rotate this object.")]
    [SerializeField] private Transform armPivot;

    [Header("Rotation")]
    [SerializeField] private float rot_down  =  0f;   // closed angle (degrees around local Z)
    [SerializeField] private float rot_up    = 90f;   // open angle (degrees around local Z)
    [SerializeField] private float arm_speed = 80f;   // degrees per second

    private float _current;
    private float _target;
    private int   _agentsInside;

    // Set true by TruckYardManager for the arm gating the inbound gate — Tad's spec: that arm must
    // stay down through the whole guard inspection and only raise on GuardController's own cue
    // (right as the guard gets back to the driver, just before waving them in), not the instant a
    // truck's collider touches the trigger. Entry/exit tracking below is untouched either way, so
    // the existing "close once everyone's out" behavior still applies once this arm is raised.
    private bool _externallyControlled;

    private Transform Pivot => armPivot != null ? armPivot : transform;

    /// <summary>Opt this arm out of the trigger-based auto-raise — something else (GuardController)
    /// is now responsible for calling RaiseArm() at the right moment.</summary>
    public void SetExternallyControlled(bool value) => _externallyControlled = value;

    /// <summary>Raises the arm immediately, regardless of trigger state.</summary>
    public void RaiseArm() => _target = rot_up;

    /// <summary>Lowers the arm immediately, regardless of trigger state. Only meaningful once
    /// _externallyControlled is set — see SetExternallyControlled.</summary>
    public void LowerArm() => _target = rot_down;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _current = rot_down;
        _target  = rot_down;
        ApplyRotation(_current);
    }

    private void Update()
    {
        if (Mathf.Approximately(_current, _target)) return;

        _current = Mathf.MoveTowards(_current, _target, arm_speed * Time.unscaledDeltaTime);
        ApplyRotation(_current);
    }

    // ── Trigger ───────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAgent(other)) return;
        _agentsInside++;
        if (!_externallyControlled)
            _target = rot_up;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsAgent(other)) return;
        _agentsInside = Mathf.Max(0, _agentsInside - 1);
        // Externally-controlled arms rely entirely on RaiseArm()/LowerArm() — auto-close-on-empty
        // was unreliable here since the guard's own idle "Posted" stance sits inside this trigger,
        // so _agentsInside depends on incidental guard movement as much as the truck actually
        // passing through.
        if (_agentsInside == 0 && !_externallyControlled)
            _target = rot_down;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplyRotation(float angle)
    {
        // Rotates around the pivot's local Z axis — adjust the Vector3 axis if your
        // model's pivot orientation requires X or Y instead.
        var e = Pivot.localEulerAngles;
        Pivot.localEulerAngles = new Vector3(e.x, e.y, angle);
    }

    private static bool IsAgent(Collider other)
    {
        return other.GetComponentInParent<AiNavigation>() != null
            || other.GetComponentInParent<RatBehavior>()  != null;
    }
}
