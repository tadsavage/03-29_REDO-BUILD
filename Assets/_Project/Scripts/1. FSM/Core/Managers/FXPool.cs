using UnityEngine;
using System.Collections.Generic;

public class FXPool : MonoBehaviour
{
    public static FXPool Instance { get; private set; }

    // Playback speed multiplier for Animator-driven FX (e.g. the sprite-sequence smoke poof).
    // 1f = normal speed. Was 1.25f, slowed ~35% to 0.8125f, another 10% to 0.73125f, sped back up
    // 20% to 0.8775f, then slowed 25% twice more per Tad's asks (0.8775 × 0.75 × 0.75 = 0.49359375).
    private const float AnimatorPlaybackSpeed = 0.49359375f;

    // Normalized start point (0–1) for Animator-driven FX playback. The smoke poof's clip is sampled
    // at 24fps (SmokeEffect2.anim's m_SampleRate) — started at frame 6 (0.25), then frame 15 (0.625),
    // now frame 12 (0.5) per Tad's asks, giving a couple extra frames to smooth the poof in. Skipping
    // ahead makes the effect read as already mid-poof the instant it triggers, without touching the
    // actual trigger timing (which is synchronous with placement).
    private const float AnimatorStartTimeOffset = 12f / 24f;

    // World size of one grid cell, in meters — matches PlacementGrid's own cell size (Tad's figure,
    // taken directly off the Building Data inspector). Drives the footprint-based sizing below.
    private const float CellSize = 1.325f;

    // How much bigger than its footprint a sprite/Animator-driven FX (e.g. the dust poof) reads —
    // e.g. a 1×1 footprint (1.325m) sizes it to 1.325 × 1.2 = 1.59m; a 2×2 (2.65m) to 3.18m.
    private const float FootprintSizeMultiplier = 1.2f;

    // When "Enter Play Mode Options" disables Domain Reload, static fields are NOT
    // cleared between Play sessions. Reset them explicitly so we never carry a stale
    // Instance or disabled-key set into a fresh session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        DisabledKeys.Clear();
    }

    [System.Serializable]
    public class FXEntry
    {
        public string key;
        public GameObject prefab;
        public int preload = 10;
    }

    [SerializeField] private FXEntry[] entries;

    private class FXObject
    {
        public GameObject go;
        public ParticleSystem[] systems;

        // Support for Animator/SpriteRenderer-driven FX (e.g. a sprite-sequence smoke poof)
        // alongside the original ParticleSystem-only FX. When systems is empty and animator
        // is set, lifetime is timed off animatorDuration instead of ParticleSystem.IsAlive().
        public Animator animator;
        public float animatorDuration;
    }

    private readonly Dictionary<string, Queue<FXObject>> _pools =
        new Dictionary<string, Queue<FXObject>>();

    private void Awake()
    {
        Instance = this;

        // Rebuild from scratch. With Scene Reload disabled, leftover pooled children
        // and stale dictionary entries can survive from a previous Play session — clear
        // them so we never hand out a destroyed instance.
        _pools.Clear();
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        foreach (var entry in entries)
        {
            var q = new Queue<FXObject>();
            _pools[entry.key] = q;

            for (int i = 0; i < entry.preload; i++)
                q.Enqueue(CreateInstance(entry.prefab));
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private FXObject CreateInstance(GameObject prefab)
    {
        var go = Instantiate(prefab, transform);
        go.SetActive(false);

        var systems = go.GetComponentsInChildren<ParticleSystem>(true);

        foreach (var ps in systems)
        {
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;

            // Unscaled: FX are presentation, not simulation. Without this they crawl at 1/4x and
            // freeze outright at 0x — and because ReturnWhenDone() waits on ps.IsAlive(), a frozen
            // system never finishes, so the pooled instance is never returned and the pool drains.
            main.useUnscaledTime = true;
        }

        // Assign materials to all child renderers
        var prefabRenderers = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true);
        var instanceRenderers = go.GetComponentsInChildren<ParticleSystemRenderer>(true);

        for (int i = 0; i < instanceRenderers.Length; i++)
        {
            if (instanceRenderers[i].sharedMaterial == null)
                instanceRenderers[i].sharedMaterial = prefabRenderers[i].sharedMaterial;
        }

        var systems2 = go.GetComponentsInChildren<ParticleSystem>(true);

        // 🔑 Critical: prevent auto-destroy on finish
        foreach (var ps in systems2)
        {
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.useUnscaledTime = true;
        }

        // Animator-driven FX (e.g. a sprite-sequence smoke poof) have no ParticleSystem to time
        // against — read the assigned clip's length directly off the controller so ReturnWhenDone
        // can wait for the correct duration instead of returning the instance to the pool before
        // it has even played.
        Animator animator = null;
        float animatorDuration = 0f;
        if (systems.Length == 0)
        {
            animator = go.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                // Force unscaled time in code rather than relying on the prefab's serialized
                // Animator.updateMode: build mode commonly pauses (Time.timeScale = 0), and the
                // default 'Normal' update mode would freeze this FX on its first sampled frame.
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var clips = animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                    animatorDuration = clips[0].length;
            }
        }

        return new FXObject
        {
            go = go,
            systems = systems,
            animator = animator,
            animatorDuration = animatorDuration
        };
    }
    public static readonly System.Collections.Generic.HashSet<string> DisabledKeys =
        new System.Collections.Generic.HashSet<string>();

    /// <summary>Plays a pooled FX at <paramref name="position"/>. <paramref name="footprint"/> is
    /// the (optional) grid footprint, in cells, of whatever object triggered this effect — e.g. a
    /// placed building passing its own <see cref="ObjDataSO.footprint"/>. Left unspecified (the
    /// default (0,0)) is treated as a plain 1×1 cell, which every existing single-point caller
    /// (forklift landings, building destruction) gets automatically — and at 1×1 the centering below
    /// resolves to exactly zero, so none of those callers are shifted. Only applied to sprite/
    /// Animator-driven FX like the dust poof — ParticleSystem-driven FX size themselves and are
    /// left untouched.</summary>
    public void Play(string key, Vector3 position, Vector2Int footprint = default)
    {
        if (DisabledKeys.Contains(key)) return;

        if (!_pools.TryGetValue(key, out var q))
        {
            Debug.LogWarning($"FXPool: No pool for key '{key}'");
            return;
        }

        FXObject fx = null;

        // Pull a live instance, discarding any whose GameObject was destroyed between
        // Play sessions (the Unity == null check catches destroyed objects).
        while (q.Count > 0)
        {
            var candidate = q.Dequeue();
            if (candidate != null && candidate.go != null)
            {
                fx = candidate;
                break;
            }
        }

        if (fx == null)
        {
            var entry = FindEntry(key);
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"FXPool: No usable prefab for key '{key}'");
                return;
            }
            fx = CreateInstance(entry.prefab);
        }

        fx.go.transform.position = position;

        // Sized and centered fresh on every Play() call (not just once, the way a MonoBehaviour's
        // own Start() would for a pooled/reused instance) — footprint.x/y ≤ 0 means "not specified",
        // falling back to a plain 1×1 cell.
        if (fx.systems.Length == 0)
        {
            int footprintX = footprint.x > 0 ? footprint.x : 1;
            int footprintY = footprint.y > 0 ? footprint.y : 1;

            float footprintWorldX = footprintX * CellSize;
            fx.go.transform.localScale = Vector3.one * (footprintWorldX * FootprintSizeMultiplier);

            // Placement anchors an object at the CENTER of its root cell (see DockLedgeSetup.cs),
            // with the footprint extending in +X/+Z from there — so half of the EXTRA cells beyond
            // the first one is the offset to the footprint's true center. At the default 1×1 this
            // is exactly zero, which is what keeps every non-footprint caller unshifted.
            Vector3 pos = fx.go.transform.position;
            pos.x += (footprintX - 1) * CellSize / 2f;
            pos.z += (footprintY - 1) * CellSize / 2f;

            // The centered poof often reads as buried inside the placed object's own geometry
            // (tall props especially). Nudge it from the footprint's center toward the camera, on
            // the horizontal plane, by half the footprint's world size — landing it near the
            // footprint's camera-facing edge/corner instead of dead center, so it renders in front
            // of the geometry rather than inside it.
            if (Camera.main != null)
            {
                Vector3 towardCamera = Camera.main.transform.position - pos;
                towardCamera.y = 0f;
                if (towardCamera.sqrMagnitude > 0.0001f)
                    pos += towardCamera.normalized * (footprintWorldX / 2f);
            }

            fx.go.transform.position = pos;
        }

        fx.go.SetActive(true);

        // Play ALL particle systems
        foreach (var ps in fx.systems)
            ps.Play(true);

        // Animator-driven FX don't restart on their own: re-enabling the GameObject resumes the
        // state machine wherever it was left (frozen on the last frame, since the clip doesn't
        // loop) rather than replaying from the start. Play() back into the same state at
        // normalizedTime 0 forces a clean restart — Rebind() (tried first) tears down and rebuilds
        // the whole playable graph, which proved unreliable and only ever showed a single frame.
        if (fx.animator != null)
        {
            int stateHash = fx.animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            fx.animator.speed = AnimatorPlaybackSpeed;
            fx.animator.Play(stateHash, 0, AnimatorStartTimeOffset);
            fx.animator.Update(0f);
        }

        StartCoroutine(ReturnWhenDone(key, fx));
    }

    private FXEntry FindEntry(string key)
    {
        foreach (var e in entries)
            if (e.key == key)
                return e;

        return null;
    }

    private System.Collections.IEnumerator ReturnWhenDone(string key, FXObject fx)
    {
        if (fx.systems.Length == 0 && fx.animator != null)
        {
            // Animator-driven FX (no ParticleSystem) — wait for its clip's real duration instead
            // of ParticleSystem.IsAlive(), which would immediately report "not alive" and return
            // this instance to the pool (deactivating it) before the animation ever plays.
            // Divide by AnimatorPlaybackSpeed so the wait matches the slowed-down playback rate.
            // Only the clip AFTER AnimatorStartTimeOffset actually plays (we skip ahead to frame
            // 15/24) — waiting on the FULL duration left the instance active, frozen on its last
            // frame, for the leftover time after playback had already finished: a visible "ghost"
            // hang that got worse the slower AnimatorPlaybackSpeed got. Scale by the remaining
            // normalized portion of the clip instead.
            float duration = fx.animatorDuration > 0f ? fx.animatorDuration : 1f;
            float remainingNormalized = 1f - AnimatorStartTimeOffset;
            yield return new WaitForSecondsRealtime(duration * remainingNormalized / AnimatorPlaybackSpeed);
        }
        else
        {
            bool alive = true;
            while (alive)
            {
                alive = false;

                foreach (var ps in fx.systems)
                {
                    if (ps == null || ps.Equals(null))
                        continue;

                    if (ps.IsAlive(true))
                    {
                        alive = true;
                        break;
                    }
                }

                yield return null;
            }
        }

        // The instance may have been destroyed (scene change / pool rebuild) — drop it.
        if (fx.go == null) yield break;

        fx.go.SetActive(false);
        if (_pools.TryGetValue(key, out var q))
            q.Enqueue(fx);
    }
}
