using UnityEngine;
using System.Collections;

/// <summary>
/// Raises a gate arm when any vehicle enters the trigger collider, lowers it after they leave.
/// Attach to the gate collider GameObject. Assign the arm pivot Transform in the Inspector.
///
/// The arm rotates around its LOCAL X axis (default). If your arm model pivots on a
/// different axis, adjust rotationAxis below.
/// </summary>
public class GateArmController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The arm Transform that physically rotates (the pivot, not the whole gate).")]
    [SerializeField] private Transform armPivot;

    [Header("Rotation")]
    [Tooltip("Local-space axis the arm rotates around.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.right;

    [SerializeField] private float closedAngle =   0f;   // arm horizontal (closed)
    [SerializeField] private float openAngle   = -85f;   // arm raised (open)
    [SerializeField] private float speed       = 90f;    // degrees per second

    [Header("Timing")]
    [Tooltip("Seconds after the last vehicle exits before the arm lowers again.")]
    [SerializeField] private float closeDelay = 1.5f;

    private int       _triggerCount  = 0;   // how many vehicles are inside
    private Coroutine _moveCoroutine;

    private void Awake()
    {
        if (armPivot != null)
            armPivot.localRotation = Quaternion.AngleAxis(closedAngle, rotationAxis);
    }

    private void OnTriggerEnter(Collider other)
    {
        _triggerCount++;
        if (_triggerCount == 1)
            SetArm(open: true);
    }

    private void OnTriggerExit(Collider other)
    {
        _triggerCount = Mathf.Max(0, _triggerCount - 1);
        if (_triggerCount == 0)
            StartCoroutine(DelayedClose());
    }

    private IEnumerator DelayedClose()
    {
        yield return new WaitForSeconds(closeDelay);
        if (_triggerCount == 0)
            SetArm(open: false);
    }

    private void SetArm(bool open)
    {
        if (armPivot == null) return;
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(RotateArm(open ? openAngle : closedAngle));
    }

    private IEnumerator RotateArm(float targetAngle)
    {
        Quaternion target = Quaternion.AngleAxis(targetAngle, rotationAxis);

        while (Quaternion.Angle(armPivot.localRotation, target) > 0.2f)
        {
            armPivot.localRotation = Quaternion.RotateTowards(
                armPivot.localRotation, target, speed * Time.deltaTime);
            yield return null;
        }

        armPivot.localRotation = target;
    }
}
