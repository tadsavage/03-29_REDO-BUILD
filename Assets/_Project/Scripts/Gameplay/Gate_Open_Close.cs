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

    private Transform Pivot => armPivot != null ? armPivot : transform;

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

        _current = Mathf.MoveTowards(_current, _target, arm_speed * Time.deltaTime);
        ApplyRotation(_current);
    }

    // ── Trigger ───────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAgent(other)) return;
        _agentsInside++;
        _target = rot_up;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsAgent(other)) return;
        _agentsInside = Mathf.Max(0, _agentsInside - 1);
        if (_agentsInside == 0)
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
