using UnityEngine;

/// <summary>
/// Rotates this GameObject to fully face the main camera in world space — the same billboard
/// technique used by EmoteBubble for its floating sprite bubbles. Needed for flat SpriteRenderer-
/// based FX (e.g. a sprite-sequence smoke poof) that would otherwise render edge-on and effectively
/// invisible from an angled/orbiting 3D camera, unlike a ParticleSystem which can billboard itself
/// via its Renderer's Alignment setting.
///
/// This prefab (SmokeEffect2) is FXPool's shared "dust" poof — pooled and reused for forklift
/// landings (SmoothLanding), building placement (PlacementFinalizer), and building destruction
/// (BuildingDestructionEffect). FXPool.Play() sets this object's world position directly right
/// before activating it each time, and a pooled instance's Start() only ever fires once for its
/// whole lifetime — so this script must NEVER touch position/scale itself, or it corrupts whichever
/// use case happened to trigger that one-time Start() call for every other use case forever after.
/// </summary>
public class SpriteBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Full look at the camera — both X (pitch) and Y (yaw) — so the sprite reads correctly
        // whether the camera is level with it or swooping above/below, not just when orbiting
        // around it at a fixed height. Setting transform.rotation (not localRotation) already
        // makes this a world-space rotation regardless of any parent's own rotation.
        Vector3 toCamera = cam.transform.position - transform.position;
        if (toCamera.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(toCamera.normalized);
    }
}
