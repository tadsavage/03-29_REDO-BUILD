using System.Collections;
using UnityEngine;

/// <summary>
/// Continuously spawns low-poly smoke puffs from this transform's position -- built for the
/// Savage Dev branded truck's smokestack (attach to the SmokeAnchor child under Smokestack), but
/// generic enough to drop onto any exhaust/chimney point.
///
/// Reuses the existing SmokeEffect2 sprite-flipbook prefab (the same "poof" FXPool already uses
/// for dust on forklift landings, building placement, and destruction) so the smokestack matches
/// the game's established low-poly smoke look rather than introducing a new art style. Unlike
/// FXPool's pooled "dust" usage, puffs here are plain Instantiate/Destroy -- this runs continuously
/// for the lifetime of the object rather than as a one-shot event, so it intentionally doesn't
/// compete with FXPool's placement-FX pool budget.
/// </summary>
public class SmokestackEmitter : MonoBehaviour
{
    [Header("Puff Prefab")]
    [Tooltip("The smoke puff prefab to spawn (defaults to the shared SmokeEffect2 sprite-flipbook poof).")]
    [SerializeField] private GameObject smokePuffPrefab;

    [Header("Timing")]
    [Tooltip("Minimum/maximum seconds between puffs. Randomized each time so the stack doesn't chug on a mechanical beat.")]
    [SerializeField] private float minInterval = 0.7f;
    [SerializeField] private float maxInterval = 1.3f;

    [Header("Puff Look")]
    [Tooltip("Random uniform scale range applied to each spawned puff, for a bit of natural size variance.")]
    [SerializeField] private float minScale = 0.45f;
    [SerializeField] private float maxScale = 0.7f;
    [Tooltip("Animator playback speed for each puff. Below 1 stretches the poof out so it reads as drifting smoke rather than a quick pop.")]
    [SerializeField] private float animatorSpeed = 0.55f;
    [Tooltip("Tint applied to each puff's SpriteRenderer. The base sprite art reads slightly warm/gray, so this defaults to pure white.")]
    [SerializeField] private Color puffColor = Color.white;

    [Header("Motion")]
    [Tooltip("Upward speed (world units/sec) each puff rises while it plays.")]
    [SerializeField] private float riseSpeed = 0.5f;
    [Tooltip("Max random sideways drift (world units/sec) per puff, for a gentle non-mechanical sway.")]
    [SerializeField] private float sidewaysDrift = 0.12f;
    [Tooltip("Random horizontal offset radius applied to each puff's spawn position.")]
    [SerializeField] private float spawnJitterRadius = 0.05f;

    private void OnEnable()
    {
        StartCoroutine(EmitLoop());
    }

    private IEnumerator EmitLoop()
    {
        // Stagger the first puff so multiple smokestacks in the scene don't all pop in lockstep.
        yield return new WaitForSeconds(Random.Range(0f, maxInterval));

        while (true)
        {
            SpawnPuff();
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }
    }

    private void SpawnPuff()
    {
        if (smokePuffPrefab == null) return;

        Vector2 jitter = Random.insideUnitCircle * spawnJitterRadius;
        Vector3 spawnPos = transform.position + new Vector3(jitter.x, 0f, jitter.y);

        GameObject puff = Instantiate(smokePuffPrefab, spawnPos, Quaternion.identity);
        float scale = Random.Range(minScale, maxScale);
        puff.transform.localScale = Vector3.one * scale;

        var sprite = puff.GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
        {
            // Preserve the sprite's own per-frame alpha (the flipbook fades itself in/out) --
            // only override RGB so the puff reads white instead of the sprite art's default tint.
            Color c = puffColor;
            c.a = sprite.color.a;
            sprite.color = c;
        }

        float duration = 1.5f; // fallback if no Animator/clip is found
        var animator = puff.GetComponent<Animator>();
        if (animator != null)
        {
            animator.speed = animatorSpeed;
            if (animator.runtimeAnimatorController != null)
            {
                var clips = animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                    duration = clips[0].length / Mathf.Max(0.01f, animatorSpeed);
            }
        }

        StartCoroutine(DriftAndDestroy(puff, duration));
    }

    private IEnumerator DriftAndDestroy(GameObject puff, float duration)
    {
        // A gentle, randomized sideways sway so puffs don't all rise in an identical straight
        // line -- reads more like real smoke drifting on a breeze than a mechanical repeat.
        Vector3 sway = new Vector3(
            Random.Range(-sidewaysDrift, sidewaysDrift),
            0f,
            Random.Range(-sidewaysDrift, sidewaysDrift));

        float t = 0f;
        while (puff != null && t < duration)
        {
            puff.transform.position += (Vector3.up * riseSpeed + sway) * Time.deltaTime;
            t += Time.deltaTime;
            yield return null;
        }

        if (puff != null) Destroy(puff);
    }
}
