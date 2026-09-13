using UnityEngine;
using System.Collections.Generic;

public class FXPool : MonoBehaviour
{
    public static FXPool Instance { get; private set; }

    // Playback speed multiplier for Animator-driven FX (e.g. the sprite-sequence smoke poof).
    // 1f = normal speed.
    private const float AnimatorPlaybackSpeed = 1.25f;

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

    public void Play(string key, Vector3 position)
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
            fx.animator.Play(stateHash, 0, 0f);
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
            float duration = fx.animatorDuration > 0f ? fx.animatorDuration : 1f;
            yield return new WaitForSecondsRealtime(duration / AnimatorPlaybackSpeed);
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
