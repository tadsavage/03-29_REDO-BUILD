using UnityEngine;
using System.Collections;

public class RollupDoorController : MonoBehaviour
{
    [SerializeField] private Transform doorPanel;
    [SerializeField] private float startY = 0f;
    [SerializeField] private float endY = 3.8f;
    [SerializeField] private float speed = 2f;

    private Vector3 _initialLocalPos;
    private Coroutine _moveCoroutine;

    private void Awake()
    {
        if (doorPanel == null)
        {
            doorPanel = transform.Find("DoorPanel");
        }

        if (doorPanel != null)
        {
            _initialLocalPos = doorPanel.localPosition;
            // Ensure it starts at startY
            doorPanel.localPosition = new Vector3(_initialLocalPos.x, startY, _initialLocalPos.z);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Triggers when an object enters the box collider
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveDoor(endY));
    }

    private void OnTriggerExit(Collider other)
    {
        // Triggers when the object leaves the box collider
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveDoor(startY));
    }

    private IEnumerator MoveDoor(float targetY)
    {
        if (doorPanel == null) yield break;

        Vector3 targetPos = new Vector3(_initialLocalPos.x, targetY, _initialLocalPos.z);
        
        while (Vector3.Distance(doorPanel.localPosition, targetPos) > 0.001f)
        {
            doorPanel.localPosition = Vector3.MoveTowards(doorPanel.localPosition, targetPos, speed * Time.deltaTime);
            yield return null;
        }
        doorPanel.localPosition = targetPos;
    }
}
