using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lightweight gate security guard.
///
/// Sequence:
///   Posted → ExitPost → GateStop (wait) → CheckRear1 → CheckRear2 (wait)
///   → CheckRear1 → GateStop (wave) → fires onCleared → returns to Posted (walk)
/// </summary>
public class GuardController : MonoBehaviour
{
    public enum GuardState
    {
        Posted,
        MovingToExitPost,
        MovingToGateStop,
        WaitingAtGateStop,
        MovingToCheckRear1,
        MovingToCheckRear2,
        TurningToInspectRear,
        WaitingToOpenTrailer,
        InspectingTrailer,
        MovingBackToCheckRear1,
        MovingBackToGateStop,
        WavingIn,
        MovingBackToPosted
    }

    [Header("Movement")]
    [SerializeField] private float walkSpeed        = 3f;
    [SerializeField] private float turnSpeed        = 240f;
    [SerializeField] private float arrivedThreshold = 0.5f;

    [Header("Timing")]
    [SerializeField] private float waitAtGateStop = 3f;
    [SerializeField] private float waitBeforeOpen = 1f;
    [SerializeField] private float waitAtCheckRear = 2f;
    [SerializeField] private float waitWavingIn   = 2.0f;

    [Header("Audio")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.4f;

    [Header("State (read-only)")]
    [SerializeField] private GuardState _state = GuardState.Posted;

    private Transform _posted;
    private Transform _exitPost;
    private Transform _gateStop;
    private Transform _checkRear1;
    private Transform _checkRear2;

    private float           _stateTimer;
    private System.Action   _onCleared;
    private Animator        _animator;
    private NavMeshAgent    _agent;
    private AudioSource     _footstepSource;
    private TruckController _currentTruck;

    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int IsWaving  = Animator.StringToHash("IsWaving");

    public GuardState State => _state;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _agent    = GetComponent<NavMeshAgent>();

        // Setup NavMeshAgent for script-controlled movement
        if (_agent != null)
        {
            _agent.speed = walkSpeed;
            _agent.angularSpeed = turnSpeed;
            _agent.stoppingDistance = arrivedThreshold;
            _agent.acceleration = 12f;
        }

        // Setup audio for footsteps
        if (footstepClip != null)
        {
            _footstepSource = gameObject.AddComponent<AudioSource>();
            _footstepSource.clip = footstepClip;
            _footstepSource.loop = true;
            _footstepSource.volume = footstepVolume;
            _footstepSource.spatialBlend = 1f;
            _footstepSource.playOnAwake = false;
        }
    }

    public void Init(Transform posted, Transform exitPost, Transform gateStop,
                     Transform checkRear1, Transform checkRear2)
    {
        _posted     = posted;
        _exitPost   = exitPost;
        _gateStop   = gateStop;
        _checkRear1 = checkRear1;
        _checkRear2 = checkRear2;

        if (posted != null)
        {
            if (_agent != null) _agent.Warp(posted.position);
            else transform.position = posted.position;
            
            transform.rotation = posted.rotation;
        }
        
        _state = GuardState.Posted;
    }

    public void BeginInspection(TruckController truck, System.Action onCleared)
    {
        if (_state != GuardState.Posted)
        {
            Debug.LogWarning("[GuardController] BeginInspection called while guard is busy.");
            onCleared?.Invoke();
            return;
        }
        _currentTruck = truck;
        _onCleared    = onCleared;

        if (_exitPost != null)
            SetDestination(GuardState.MovingToExitPost, _exitPost.position);
        else
            SetDestination(GuardState.MovingToGateStop, _gateStop.position);
    }

    private void Update()
    {
        UpdateAnimationState();
        UpdateAudio();

        switch (_state)
        {
            case GuardState.MovingToExitPost:
                if (HasArrived())
                    SetDestination(GuardState.MovingToGateStop, _gateStop.position);
                break;

            case GuardState.MovingToGateStop:
                if (HasArrived())
                {
                    _state      = GuardState.WaitingAtGateStop;
                    _stateTimer = waitAtGateStop;
                    StopMovement();
                }
                break;

            case GuardState.WaitingAtGateStop:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                    SetDestination(GuardState.MovingToCheckRear1, _checkRear1.position);
                break;

            case GuardState.MovingToCheckRear1:
                if (HasArrived())
                    SetDestination(GuardState.MovingToCheckRear2, _checkRear2.position);
                break;

            case GuardState.MovingToCheckRear2:
                if (HasArrived())
                {
                    _state      = GuardState.TurningToInspectRear;
                    StopMovement();
                }
                break;

            case GuardState.TurningToInspectRear:
                // Face the passenger-side rear trailer door of the truck being inspected.
                // Falls back to the CheckRear2 anchor's rotation if the door is missing.
                Quaternion targetRot;
                Transform door = _currentTruck != null ? _currentTruck.PassengerDoor : null;
                if (door != null)
                {
                    Vector3 toDoor = door.position - transform.position;
                    toDoor.y = 0f;
                    targetRot = toDoor.sqrMagnitude > 0.01f
                        ? Quaternion.LookRotation(toDoor.normalized)
                        : transform.rotation;
                }
                else
                {
                    targetRot = _checkRear2 != null ? _checkRear2.rotation : transform.rotation;
                }

                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                if (Quaternion.Angle(transform.rotation, targetRot) < 0.5f)
                {
                    _state      = GuardState.WaitingToOpenTrailer;
                    _stateTimer = waitBeforeOpen;
                }
                break;

            case GuardState.WaitingToOpenTrailer:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    if (_currentTruck != null)
                        _currentTruck.OpenTrailerDoors();

                    _state      = GuardState.InspectingTrailer;
                    _stateTimer = waitAtCheckRear;
                }
                break;

            case GuardState.InspectingTrailer:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    if (_currentTruck != null)
                        _currentTruck.CloseTrailerDoors();

                    SetDestination(GuardState.MovingBackToCheckRear1, _checkRear1.position);
                }
                break;

            case GuardState.MovingBackToCheckRear1:
                if (HasArrived())
                    SetDestination(GuardState.MovingBackToGateStop, _gateStop.position);
                break;

            case GuardState.MovingBackToGateStop:
                if (HasArrived())
                {
                    _state      = GuardState.WavingIn;
                    _stateTimer = waitWavingIn;
                    StopMovement();
                    _animator?.SetBool(IsWaving, true);
                }
                break;

            case GuardState.WavingIn:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    _animator?.SetBool(IsWaving, false);
                    _onCleared?.Invoke();
                    _onCleared = null;
                    SetDestination(GuardState.MovingBackToPosted, _posted.position);
                }
                break;

            case GuardState.MovingBackToPosted:
                if (HasArrived())
                {
                    _state = GuardState.Posted;
                    StopMovement();
                    transform.rotation = _posted.rotation;
                }
                break;
        }
    }

    private void SetDestination(GuardState nextState, Vector3 pos)
    {
        _state = nextState;
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.SetDestination(pos);
        }
    }

    private bool HasArrived()
    {
        if (_agent == null || !_agent.isOnNavMesh) return true;
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            if (!_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f)
                return true;
        }
        return false;
    }

    private void StopMovement()
    {
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }
    }

    private void UpdateAnimationState()
    {
        if (_animator == null) return;

        bool moving = _agent != null && _agent.velocity.sqrMagnitude > 0.1f;
        _animator.SetBool(IsWalking, moving);
    }

    private void UpdateAudio()
    {
        if (_footstepSource == null) return;

        bool moving = _agent != null && _agent.velocity.sqrMagnitude > 0.1f && !(_state == GuardState.WavingIn || _state == GuardState.InspectingTrailer || _state == GuardState.WaitingAtGateStop);
        
        if (moving && !_footstepSource.isPlaying)
            _footstepSource.Play();
        else if (!moving && _footstepSource.isPlaying)
            _footstepSource.Stop();
    }
}
