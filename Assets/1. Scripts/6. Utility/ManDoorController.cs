using UnityEngine;
using System.Collections;

public class ManDoorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform doorTransform;

    [Header("Settings")]
    [SerializeField] private float startRotY = 0f;
    [SerializeField] private float endRotY = -90f;
    [SerializeField] private float speed = 150f; // Degrees per second

    private Coroutine _moveCoroutine;

    private void Awake()
    {
        // Automatically find the Door child if not assigned
        if (doorTransform == null)
        {
            doorTransform = transform.Find("DoorFrame/Door");
        }

        // Initialize to closed position
        if (doorTransform != null)
        {
            doorTransform.localRotation = Quaternion.Euler(0, startRotY, 0);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Starts opening the door
        StopMoving();
        _moveCoroutine = StartCoroutine(RotateDoor(endRotY));
    }

    private void OnTriggerExit(Collider other)
    {
        // Starts closing the door
        StopMoving();
        _moveCoroutine = StartCoroutine(RotateDoor(startRotY));
    }

    private void StopMoving()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
    }

    private IEnumerator RotateDoor(float targetY)
    {
        if (doorTransform == null) yield break;

        Quaternion targetRotation = Quaternion.Euler(0, targetY, 0);

        // Use RotateTowards for a constant, smooth swing speed
        while (Quaternion.Angle(doorTransform.localRotation, targetRotation) > 0.1f)
        {
            doorTransform.localRotation = Quaternion.RotateTowards(
                doorTransform.localRotation,
                targetRotation,
                speed * Time.deltaTime
            );
            yield return null;
        }
        doorTransform.localRotation = targetRotation;
    }
}