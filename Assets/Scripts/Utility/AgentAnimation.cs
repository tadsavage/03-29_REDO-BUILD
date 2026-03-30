using UnityEngine;
using UnityEngine.AI;

public class AgentAnimation : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;

    public float turnThreshold;
    private Vector3 lastForward;

    bool isMoving = false;
    bool turningLeft = false;
    bool turningRight = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        lastForward = transform.forward;
    }
    void Update()
    {
        Vector3 currentForward = transform.forward;

        // Signed angle between last frame and this frame
        float angle = Vector3.SignedAngle(lastForward, currentForward, Vector3.up);
        
        if (Mathf.Abs(angle) > turnThreshold)
        {
            turningLeft = angle < -turnThreshold;
            turningRight = angle > turnThreshold;
            agent.velocity = Vector3.zero;
        }
        else if(agent.velocity.sqrMagnitude > 0.1f)
        {
            turningLeft = false;
            turningRight = false;
            isMoving = true;
        }
        else
            isMoving = false;


        animator.SetBool("IsTurningLeft", turningLeft);
        animator.SetBool("IsTurningRight", turningRight);
        animator.SetBool("IsWalking", isMoving);

        lastForward = currentForward;
       
    }
}


