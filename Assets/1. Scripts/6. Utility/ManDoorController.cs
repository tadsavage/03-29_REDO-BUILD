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
    private AgentType _allowedAgents = AgentType.Human | AgentType.Rat;

    private void Awake()
    {
        if (doorTransform == null)
            doorTransform = transform.Find("DoorFrame/Door");

        if (doorTransform != null)
            doorTransform.localRotation = Quaternion.Euler(0, startRotY, 0);
    }

    private void Start()
    {
        var bd = GetComponentInParent<BuildingData>();
        if (bd != null && bd.Data != null)
            _allowedAgents = bd.Data.allowedAgents;
    }

    private bool IsAllowed(Collider other)
    {
        var tag = other.GetComponentInParent<AgentTypeTag>();
        return tag != null && (_allowedAgents & tag.agentType) != 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAllowed(other)) return;
        StopMoving();
        _moveCoroutine = StartCoroutine(RotateDoor(endRotY));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsAllowed(other)) return;
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