using UnityEngine;

/// <summary>
/// Rotates this GameObject to face the main camera around the Y axis only — the same
/// vertical-billboard technique used by EmoteBubble for its floating sprite bubbles.
/// Needed for flat SpriteRenderer-based FX (e.g. a sprite-sequence smoke poof) that would
/// otherwise render edge-on and effectively invisible from an angled/orbiting 3D camera,
/// unlike a ParticleSystem which can billboard itself via its Renderer's Alignment setting.
/// </summary>
public class SpriteBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 toCamera = cam.transform.position - transform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(toCamera.normalized);
    }
}
