using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Personal space around a machine (Tad, 2026-11-05: "keep a little bit of distance - a fifth to a quarter of a meter";
/// workers and equipment were walking and driving through each other).
///
/// A NON-carving NavMeshObstacle on a child object, sized to the machine's footprint plus <see cref="Margin"/>, so walking
/// workers' agents steer round it through local avoidance. Non-carving on purpose: carving would cut the NavMesh out from
/// under the operator and the machine itself, and re-carve every frame it moves.
///
/// Only live while the machine is manned and its OWN NavMeshAgent is off (scripted moves, parked, being loaded): an agent must never have to
/// avoid its own obstacle, and SetDriveMode remains the only thing that flips the vehicle agent - this just follows it.
/// Added at runtime by EquipmentBattery.Start (every RT / DS / pallet jack carries one).
/// </summary>
[DisallowMultipleComponent]
public class MheAvoidanceObstacle : MonoBehaviour
{
    public const float Margin = 0.25f;

    private NavMeshAgent _agent;
    private MHEOperatorSlot _slot;
    private NavMeshObstacle _obstacle;
    private float _nextResize;

    public static void Ensure(GameObject machine)
    {
        if (machine != null && machine.GetComponent<MheAvoidanceObstacle>() == null) machine.AddComponent<MheAvoidanceObstacle>();
    }

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _slot = GetComponent<MHEOperatorSlot>();
        var child = new GameObject("PersonalSpace");
        child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;
        _obstacle = child.AddComponent<NavMeshObstacle>();
        _obstacle.shape = NavMeshObstacleShape.Box;
        _obstacle.carving = false;
        _obstacle.enabled = false;
        Resize();
    }

    /// <summary>Footprint from the machine's renderers (in its own local space), padded by the margin on every side.</summary>
    private void Resize()
    {
        if (_obstacle == null) return;
        var rends = GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds local = default;
        var inv = transform.worldToLocalMatrix;
        foreach (var r in rends)
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
            // Skip anything riding the forks or the driver: only the machine's own body sets its footprint.
            if (r.GetComponentInParent<OutboundPalletBuilder>() != null || r.GetComponentInParent<GameCore.Inventory.PalletData>() != null) continue;
            if (r.GetComponentInParent<EmployeeIdentity>() != null) continue;
            var b = r.bounds;
            Vector3 c = inv.MultiplyPoint3x4(b.center);
            Vector3 e = Abs(inv.MultiplyVector(b.extents));
            var lb = new Bounds(c, e * 2f);
            if (!any) { local = lb; any = true; } else local.Encapsulate(lb);
        }
        if (!any) { local = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(1.2f, 2f, 2.2f)); }
        Vector3 size = local.size;
        size.x = Mathf.Clamp(size.x, 0.6f, 4f) + Margin * 2f;
        size.z = Mathf.Clamp(size.z, 0.6f, 6f) + Margin * 2f;
        size.y = Mathf.Clamp(size.y, 0.5f, 4f);
        _obstacle.center = new Vector3(local.center.x, size.y * 0.5f, local.center.z);
        _obstacle.size = size;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private void LateUpdate()
    {
        if (_obstacle == null) return;
        // Off while nobody is aboard: an operator walking up to board (its agent heads for a mount point right beside the
        // machine) must not be pushed away from it by the machine's own personal space.
        bool want = (_agent == null || !_agent.enabled) && _slot != null && _slot.IsOccupied;
        if (_obstacle.enabled != want) _obstacle.enabled = want;
        if (Time.unscaledTime >= _nextResize) { _nextResize = Time.unscaledTime + 30f; Resize(); }
    }
}
