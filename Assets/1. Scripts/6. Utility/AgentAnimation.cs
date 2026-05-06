using UnityEngine;
using UnityEngine.AI;

public class AgentAnimation : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;

    [Header("Turning Settings")]
    [SerializeField] private float turnThreshold = 45f;     // degrees per second
    [SerializeField] private float turnSlowdown = 0.5f;     // speed multiplier while turning

    private Vector3 lastForward;
    private float baseSpeed;

    private bool isTurningLeft;
    private bool isTurningRight;
    private bool isMoving;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        lastForward = transform.forward;
        baseSpeed = agent.speed;
    }

    void Update()
    {
        Vector3 currentForward = transform.forward;

        // Degrees per second
        float turnRate = Vector3.SignedAngle(lastForward, currentForward, Vector3.up) / Time.deltaTime;

        // Turning logic
        bool isTurning = Mathf.Abs(turnRate) > turnThreshold;

        if (isTurning)
        {
            isTurningLeft = turnRate < 0f;
            isTurningRight = turnRate > 0f;

            // Smooth slowdown instead of snapping to zero
            agent.speed = baseSpeed * turnSlowdown;
        }
        else
        {
            isTurningLeft = false;
            isTurningRight = false;

            // Restore full speed
            agent.speed = baseSpeed;
        }

        // Movement logic
        isMoving = agent.velocity.sqrMagnitude > 0.1f;

        // Animator parameters
        animator.SetBool("IsTurningLeft", isTurningLeft);
        animator.SetBool("IsTurningRight", isTurningRight);
        animator.SetBool("IsWalking", isMoving);

        lastForward = currentForward;
    }
}
