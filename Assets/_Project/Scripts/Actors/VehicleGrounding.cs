using UnityEngine;
using GameCore.Inventory;

/// <summary>
/// Keeps a piece of material-handling equipment (reach truck, dock stocker, pallet jack) visibly ON the floor.
///
/// Two problems, one component:
///  1. FLOATING. The NavMesh surface the vehicles drive on sits 7-13 cm above the real floor (measured live:
///     floor 1.110, vehicle pivots 1.18-1.24, and the chassis bottom is at the pivot), so every vehicle hovered.
///     AiNavigation writes the agent's NavMesh height into the transform every frame, so this runs AFTER it and
///     drops the vehicle onto the floor collider underneath.
///  2. NOTHING ANCHORING IT. Like the characters, a vehicle on a dark floor with no sun shadow has nothing that
///     says it is touching the ground, so it also gets a soft contact-shadow blob, drawn with the same material
///     as FootContactShadow (a wide faint one and a tighter darker core), sized from the vehicle's own meshes.
///
/// Added by MHEOperatorSlot once the vehicle is a real, placed one (the build-menu ghost loses its AiNavigation
/// before Start, so it never gets this).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public class VehicleGrounding : MonoBehaviour
{
    private const string MaterialResource = "Resource_AvatarSystemAssets/FootContactShadow";

    // Tuning ---------------------------------------------------------------------------------------------------
    private const float RayStartAbove = 0.6f;   // cast from this far above the pivot
    private const float RayLength     = 1.6f;
    private const float MaxSnap       = 0.35f;  // never move further than this in one go (guards against a stray collider)
    private const float RestAboveFloor = 0.004f;// pivot height above the floor surface
    private const float ShadowLift    = 0.012f;
    private const float WideAlpha     = 0.55f;
    private const float CoreAlpha     = 0.55f;
    private const float WideGrow      = 1.30f;  // wide blob = footprint * this
    private const float CoreShrink    = 0.72f;  // core blob = footprint * this

    private static Material _material;
    private static Mesh _quad;
    private static int _groundMask;
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    private Transform _wide, _core;
    private MeshRenderer _wideR, _coreR;
    private MaterialPropertyBlock _mpb;
    private Vector3 _footCentreLocal;
    private Vector2 _footSize = new Vector2(1.2f, 2.0f);   // X (width), Z (length) of the footprint
    private bool _footReady;

    public static void Attach(GameObject vehicle)
    {
        if (vehicle == null || vehicle.GetComponent<VehicleGrounding>() != null) return;
        for (var t = vehicle.transform; t != null; t = t.parent)
            if (t.name.StartsWith("LiveFeed_") || t.name.Contains("PhotoBooth")) return;
        vehicle.AddComponent<VehicleGrounding>();
    }

    private void Start()
    {
        if (_groundMask == 0)
        {
            int ground = LayerMask.NameToLayer("Ground");
            _groundMask = ground >= 0 ? 1 << ground : 1 << 3;
        }
        if (_material == null) _material = Resources.Load<Material>(MaterialResource);
        if (_quad == null) _quad = BuildQuad();
        _mpb = new MaterialPropertyBlock();
        if (_material != null)
        {
            _wide = MakeBlob("VehicleShadow_Wide", out _wideR);
            _core = MakeBlob("VehicleShadow_Core", out _coreR);
        }
        StartCoroutine(MeasureFootprintSoon());
    }

    // One frame later, once every child has settled into its pose.
    private System.Collections.IEnumerator MeasureFootprintSoon()
    {
        yield return null;
        MeasureFootprint();
    }

    private Transform MakeBlob(string name, out MeshRenderer r)
    {
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, worldPositionStays: false);
        go.AddComponent<MeshFilter>().sharedMesh = _quad;
        r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = _material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go.transform;
    }

    /// <summary>The floor footprint, in the vehicle's own space: the union of its solid meshes' footprints
    /// (the operator, loaded pallets and our own blobs are left out).</summary>
    private void MeasureFootprint()
    {
        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;

        foreach (var r in GetComponentsInChildren<MeshRenderer>(false))
        {
            if (r == _wideR || r == _coreR) continue;
            if (r.GetComponentInParent<EmployeeIdentity>() != null) continue;          // the rider
            if (r.GetComponentInParent<PalletMasterLink>() != null) continue;           // a carried pallet
            if (r.GetComponentInParent<OutboundPalletBuilder>() != null) continue;
            if (r is TMPro.TextMeshPro || r.GetType().Name.Contains("Line")) continue;

            Bounds lb = r.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!any) { min = max = new Vector2(p.x, p.z); any = true; }
                else { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
            }
        }

        if (any)
        {
            _footCentreLocal = new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);
            _footSize = new Vector2(Mathf.Max(0.6f, max.x - min.x), Mathf.Max(0.8f, max.y - min.y));
        }
        _footReady = true;
    }

    private void LateUpdate()
    {
        SnapToFloor();
        UpdateShadow();
    }

    private void SnapToFloor()
    {
        Vector3 p = transform.position;
        if (!Physics.Raycast(p + Vector3.up * RayStartAbove, Vector3.down, out var hit, RayLength, _groundMask, QueryTriggerInteraction.Ignore))
            return;

        float target = hit.point.y + RestAboveFloor;
        float delta = target - p.y;
        if (Mathf.Abs(delta) > MaxSnap || Mathf.Abs(delta) < 0.0005f) return;

        // Set it outright: AiNavigation rewrites the NavMesh height every frame, so any smoothing here would only
        // ever close one small step of the gap per frame and the vehicle would keep hovering.
        p.y = target;
        transform.position = p;
    }

    private void UpdateShadow()
    {
        if (_wide == null || !_footReady) return;

        Vector3 centre = transform.TransformPoint(_footCentreLocal);
        float floorY = transform.position.y - RestAboveFloor + ShadowLift;   // the floor under the pivot
        Quaternion rot = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);

        PlaceBlob(_wide, _wideR, centre, floorY, rot, _footSize * WideGrow, WideAlpha);
        PlaceBlob(_core, _coreR, centre, floorY, rot, _footSize * CoreShrink, CoreAlpha);
    }

    private void PlaceBlob(Transform t, MeshRenderer r, Vector3 centre, float floorY, Quaternion rot, Vector2 size, float alpha)
    {
        t.position = new Vector3(centre.x, floorY, centre.z);
        t.rotation = rot;
        t.localScale = new Vector3(size.x, size.y, 1f);   // X = width, Y = length (the quad is rotated flat)
        r.GetPropertyBlock(_mpb);
        _mpb.SetFloat(AlphaId, alpha);
        r.SetPropertyBlock(_mpb);
    }

    private void OnDestroy()
    {
        if (_wide != null) Destroy(_wide.gameObject);
        if (_core != null) Destroy(_core.gameObject);
    }

    private static Mesh BuildQuad()
    {
        var m = new Mesh { name = "VehicleContactShadowQuad", hideFlags = HideFlags.HideAndDontSave };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
