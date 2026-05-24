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

        transform.position = Vector3.SmoothDamp(transform.position, _targetPos, ref _velocity, _smoothTime);

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
