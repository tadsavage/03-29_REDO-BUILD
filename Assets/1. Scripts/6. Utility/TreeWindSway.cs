using UnityEngine;

public class TreeWindSway : MonoBehaviour
{
    [Header("Sway")]
    [Tooltip("Maximum lean angle in degrees.")]
    [SerializeField] float swayAngle = 2.5f;

    [Tooltip("How many full sway cycles per second.")]
    [SerializeField] float swaySpeed = 0.35f;

    [Tooltip("Secondary faster wobble layered on top of the main sway.")]
    [SerializeField] float wobbleAngle = 0.6f;
    [SerializeField] float wobbleSpeed = 1.1f;

    Quaternion _baseRotation;
    float _timeOffset;

    void Awake()
    {
        _baseRotation = transform.localRotation;
        // Random offset so multiple trees in the same scene don't sway in lockstep.
        _timeOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        float t = Time.time + _timeOffset;

        // Main slow sway on Z and a slight drift on X for depth.
        float swayZ = Mathf.Sin(t * swaySpeed * Mathf.PI * 2f) * swayAngle;
        float swayX = Mathf.Sin(t * swaySpeed * Mathf.PI * 2f * 0.7f) * (swayAngle * 0.4f);

        // Lighter, faster wobble layered on top.
        float wobbleZ = Mathf.Sin(t * wobbleSpeed * Mathf.PI * 2f) * wobbleAngle;

        transform.localRotation = _baseRotation *
            Quaternion.Euler(swayX, 0f, swayZ + wobbleZ);
    }
}
