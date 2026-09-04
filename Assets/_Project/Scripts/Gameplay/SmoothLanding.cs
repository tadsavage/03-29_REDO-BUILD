using UnityEngine;
using UnityEngine.AI;

public class SmoothLanding : MonoBehaviour
{
    private Vector3 _targetPos;
    private float _smoothTime;
    private Vector3 _velocity;
    private bool _isLanding;
    private NavMeshAgent _agent;

    public bool IsLanding => _isLanding;

    public void Initialize(Vector3 startPos, Vector3 targetPos, float smoothTime)
    {
        transform.position = startPos;
        _targetPos = targetPos;
        _smoothTime = smoothTime;
        _isLanding = true;
        _agent = GetComponent<NavMeshAgent>();

        // Disable agent to prevent pathfinding conflicts during landing
        if (_agent != null) _agent.enabled = false;
    }

    private void Update()
    {
        if (!_isLanding) return;

        // Unscaled: this is feedback about a placement action, not part of the simulation — with
        // the default scaled Time.deltaTime, pausing (Time.timeScale = 0, common while carefully
        // placing objects) freezes the SmoothDamp forever, stranding the object at its lifted
        // start height (finalPos + OffsetMovePreview) instead of ever reaching _targetPos. This
        // was confirmed live: walls/doors moved while paused settled at 1.61 instead of 1.11 —
        // exactly finalY + OffsetMovePreview's default 0.5.
        transform.position = Vector3.SmoothDamp(transform.position, _targetPos, ref _velocity, _smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        if (Vector3.SqrMagnitude(transform.position - _targetPos) < 0.0001f)
        {
            transform.position = _targetPos;
            _isLanding = false;

            if (_agent != null)
            {
                _agent.enabled = true;
                if (_agent.isActiveAndEnabled && _agent.isOnNavMesh)
                {
                    _agent.Warp(_targetPos);
                }
            }

            if (FXPool.Instance != null)
            {
                FXPool.Instance.Play("dust", _targetPos);
            }

            Destroy(this);
        }
    }
}
