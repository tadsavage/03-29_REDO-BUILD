using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Add this to Foundation1 (or any raised-dock foundation prefab).
/// On Awake it creates/refreshes 4 NavMeshLinks (N/S/E/W) so humanoid agents
/// can climb up onto the dock and jump down automatically — no manual link setup needed.
///
/// Remove any manually-placed LedgeLink child GameObjects before using this.
/// </summary>
public class DockLedgeSetup : MonoBehaviour
{
    [Header("Dock Geometry")]
    [Tooltip("Height of the dock surface above the surrounding floor. Match this to the Foundation's objHeight.")]
    [SerializeField] private float dockHeight = 1.06f;

    [Tooltip("Half the grid cell width (CellSize / 2 = 1.33 / 2 ≈ 0.665). "  +
             "This positions each link at the foundation edge.")]
    [SerializeField] private float halfCellSize = 0.665f;

    [Tooltip("How far the floor endpoint is pushed outside the foundation edge so it "  +
             "lands on the adjacent floor tile rather than under the slab.")]
    [SerializeField] private float floorNudge = 0.4f;

    [Tooltip("Width of each NavMeshLink — should cover at least one grid cell.")]
    [SerializeField] private float linkWidth = 1.2f;

    [Header("Animation")]
    [Tooltip("Seconds the Climbing animation plays. Match your Climbing clip length.")]
    [SerializeField] private float climbDuration = 1.2f;

    [Tooltip("Seconds the JumpingDown animation plays. Match your JumpingDown clip length.")]
    [SerializeField] private float jumpDuration  = 0.8f;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildLinks();
    }

    // ── Link construction ────────────────────────────────────────────────────

    private static readonly (string name, Vector3 edgeDir)[] Sides =
    {
        ("LedgeLink_N", Vector3.forward),
        ("LedgeLink_S", Vector3.back),
        ("LedgeLink_E", Vector3.right),
        ("LedgeLink_W", Vector3.left),
    };

    private void BuildLinks()
    {
        int humanTypeID = GetHumanAgentTypeID();

        foreach (var (name, dir) in Sides)
        {
            // Find or create the child
            Transform child = transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(transform, false);
            }

            // Position at the dock-SURFACE edge: lateral offset to the edge + up to the top face.
            // Without the dockHeight offset the child sits at the foundation BASE (Y=0),
            // making startPoint at ground level and endPoint below ground — nothing connects.
            child.localPosition = dir * halfCellSize + Vector3.up * dockHeight;
            child.localRotation = Quaternion.LookRotation(dir);

            // NavMeshLink — start at dock level, end at floor level just outside the slab
            var link = child.GetComponent<NavMeshLink>();
            if (link == null) link = child.gameObject.AddComponent<NavMeshLink>();

            link.startPoint    = Vector3.zero;                             // dock surface
            link.endPoint      = new Vector3(0f, -dockHeight, floorNudge); // floor, outside slab
            link.width         = linkWidth;
            link.bidirectional = true;
            link.agentTypeID   = humanTypeID;
            link.costModifier  = -1f; // -1 = use area cost

            // LedgeLinkMarker so TraverseLink uses climb/jump animations
            var marker = child.GetComponent<LedgeLinkMarker>();
            if (marker == null) marker = child.gameObject.AddComponent<LedgeLinkMarker>();

            marker.climbDuration = climbDuration;
            marker.jumpDuration  = jumpDuration;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int GetHumanAgentTypeID()
    {
        int count = NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            var s = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == "Human")
                return s.agentTypeID;
        }
        return 0; // fallback to default
    }
}
