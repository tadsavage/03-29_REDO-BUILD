using UnityEngine;

/// <summary>
/// Place this on the same GameObject as a NavMeshLink to mark it as a climb/jump
/// ledge rather than a stairwell. AiNavigation will play the Climbing or JumpingDown
/// animation instead of the stair walk when an agent traverses the link.
///
/// Setup:
///   1. At each dock edge, create a child GameObject under the platform or scene root.
///   2. Add a NavMeshLink component: connect the floor-level point to the dock-level point,
///      set bidirectional = true, and choose the Human agent type.
///   3. Add this LedgeLinkMarker component to the same GameObject.
///   4. Rebake the NavMesh — agents will automatically climb/jump at this edge.
/// </summary>
public class LedgeLinkMarker : MonoBehaviour
{
    [Tooltip("How long the Climbing animation plays. Match this to your Climbing clip length.")]
    public float climbDuration = 1.2f;

    [Tooltip("How long the JumpingDown animation plays. Match this to your JumpingDown clip length.")]
    public float jumpDuration = 0.8f;
}
