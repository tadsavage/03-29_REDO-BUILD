using UnityEngine;

/// <summary>
/// Soft "contact shadow" blobs on the ground under a character: one under each foot (fades out as the foot lifts
/// mid-stride, so a planted foot reads as touching the floor) plus a faint, wide one under the body.
///
/// Why: a character on a flat dark floor with no sun shadow (night, or the sun below the horizon) has nothing visually
/// anchoring its feet, so it looks like it floats. Real shadows can't fix that (they depend on the sun/lamp angle and
/// get switched off by the graphics presets); a cheap blob always can. One shared material, no textures, ~3 quads per
/// character, works in every graphics preset.
///
/// Added at runtime by EmployeeSpawner after an avatar is attached. Reads the humanoid foot/toe bones, so it works for
/// both the modular avatar and the fixed Polyperfect avatars.
/// </summary>
[DisallowMultipleComponent]
public class FootContactShadow : MonoBehaviour
{
    private const string MaterialResource = "Resource_AvatarSystemAssets/FootContactShadow";

    // Tuning ---------------------------------------------------------------------------------------------------
    private const float GroundLift     = 0.012f;  // metres above the ground reference so the quad never z-fights the floor
    private const float FootAlpha      = 0.95f;   // darkness of a planted foot's shadow
    private const float BodyAlpha      = 0.50f;   // darkness of the wide body shadow
    private const float FadeHeight     = 0.22f;   // foot height (above the planted pose) at which the shadow has fully faded
    private const float FootLength     = 0.62f;
    private const float FootWidth      = 0.42f;
    private const float BodySize       = 1.10f;
    private const float SoleAboveBone  = 0.11f;   // ankle bone height above the ground when the foot is planted
    private const float ToeAboveBone   = 0.02f;   // toe bone height above the ground when the toes are planted

    private static Material _sharedMaterial;
    private static Mesh _quad;
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    private Transform _ground;        // the employee root: its Y is the walkable surface height
    private Transform _lFoot, _lToe, _rFoot, _rToe, _hips;
    private Blob _left, _right, _body;
    private MaterialPropertyBlock _mpb;

    private class Blob
    {
        public Transform t;
        public MeshRenderer r;
    }

    /// <summary>Attach to an avatar root. <paramref name="groundReference"/> is the employee root (its Y = floor height).</summary>
    public static void Attach(GameObject avatarRoot, Transform groundReference)
    {
        if (avatarRoot == null || avatarRoot.GetComponent<FootContactShadow>() != null) return;
        // The portrait photo booth clones avatars far off-map; a shadow there is wasted work and would show in portraits.
        for (var t = avatarRoot.transform; t != null; t = t.parent)
            if (t.name.StartsWith("LiveFeed_") || t.name.Contains("PhotoBooth")) return;

        var fcs = avatarRoot.AddComponent<FootContactShadow>();
        fcs._ground = groundReference != null ? groundReference : avatarRoot.transform;
    }

    private void Start()
    {
        var anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (anim == null || anim.avatar == null || !anim.avatar.isHuman) { enabled = false; return; }

        _lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
        _lToe  = anim.GetBoneTransform(HumanBodyBones.LeftToes);
        _rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        _rToe  = anim.GetBoneTransform(HumanBodyBones.RightToes);
        _hips  = anim.GetBoneTransform(HumanBodyBones.Hips);
        if (_lFoot == null || _rFoot == null) { enabled = false; return; }

        if (_sharedMaterial == null) _sharedMaterial = Resources.Load<Material>(MaterialResource);
        if (_sharedMaterial == null)
        {
            Debug.LogWarning($"[FootContactShadow] Material '{MaterialResource}' not found - contact shadows disabled.");
            enabled = false; return;
        }
        if (_quad == null) _quad = BuildQuad();

        _mpb = new MaterialPropertyBlock();
        _left  = MakeBlob("ContactShadow_L");
        _right = MakeBlob("ContactShadow_R");
        _body  = MakeBlob("ContactShadow_Body");
        _body.t.localScale = new Vector3(BodySize, BodySize, 1f);
    }

    private Blob MakeBlob(string name)
    {
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        // Parented to the employee ROOT (not the avatar) so it stays flat on the ground regardless of bone motion.
        go.transform.SetParent(_ground, worldPositionStays: false);
        go.AddComponent<MeshFilter>().sharedMesh = _quad;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = _sharedMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return new Blob { t = go.transform, r = r };
    }

    private void LateUpdate()
    {
        if (_left == null || _ground == null) return;
        float groundY = _ground.position.y + GroundLift;

        PlaceFoot(_left, _lFoot, _lToe, groundY);
        PlaceFoot(_right, _rFoot, _rToe, groundY);

        if (_hips != null)
        {
            _body.t.position = new Vector3(_hips.position.x, groundY, _hips.position.z);
            _body.t.rotation = Quaternion.Euler(90f, _ground.eulerAngles.y, 0f);
            SetAlpha(_body, BodyAlpha);
        }
    }

    private void PlaceFoot(Blob blob, Transform foot, Transform toe, float groundY)
    {
        Vector3 fp = foot.position;
        Vector3 tp = toe != null ? toe.position : fp;

        // Height of the sole above the floor: lowest of (ankle - rest ankle height, toe - rest toe height).
        float gy = groundY - GroundLift;
        float lift = Mathf.Min(fp.y - SoleAboveBone, toe != null ? tp.y - ToeAboveBone : float.MaxValue) - gy;
        lift = Mathf.Max(0f, lift);
        float k = 1f - Mathf.Clamp01(lift / FadeHeight);          // 1 planted -> 0 fully lifted
        k *= k;                                                   // ease: the shadow drops away quickly once the heel leaves

        Vector3 centre = toe != null ? (fp + tp) * 0.5f : fp;
        blob.t.position = new Vector3(centre.x, groundY, centre.z);

        // Yaw along the foot (ankle -> toe), flattened to the ground plane.
        Vector3 dir = toe != null ? tp - fp : _ground.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-5f) dir = _ground.forward;
        float yaw = Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
        blob.t.rotation = Quaternion.Euler(90f, yaw, 0f);
        blob.t.localScale = new Vector3(FootWidth * Mathf.Lerp(1.15f, 1f, k), FootLength * Mathf.Lerp(1.15f, 1f, k), 1f);   // a touch wider/softer as it lifts

        SetAlpha(blob, FootAlpha * k);
    }

    private void SetAlpha(Blob b, float a)
    {
        b.r.enabled = a > 0.01f;
        if (!b.r.enabled) return;
        b.r.GetPropertyBlock(_mpb);
        _mpb.SetFloat(AlphaId, a);
        b.r.SetPropertyBlock(_mpb);
    }

    private void OnDestroy()
    {
        if (_left != null && _left.t != null) Destroy(_left.t.gameObject);
        if (_right != null && _right.t != null) Destroy(_right.t.gameObject);
        if (_body != null && _body.t != null) Destroy(_body.t.gameObject);
    }

    /// <summary>A 1x1 quad facing +Z (rotated 90 deg about X in LateUpdate so it lies flat, face up) with 0..1 UVs.</summary>
    private static Mesh BuildQuad()
    {
        var m = new Mesh { name = "FootContactShadowQuad", hideFlags = HideFlags.HideAndDontSave };
        m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
