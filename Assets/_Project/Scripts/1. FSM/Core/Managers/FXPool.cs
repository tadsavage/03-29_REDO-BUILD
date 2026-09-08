using UnityEngine;
using System.Collections.Generic;

public class FXPool : MonoBehaviour
{
    public static FXPool Instance { get; private set; }

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

        return new FXObject
        {
            go = go,
            systems = systems
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

        // The instance may have been destroyed (scene change / pool rebuild) — drop it.
        if (fx.go == null) yield break;

        fx.go.SetActive(false);
        if (_pools.TryGetValue(key, out var q))
            q.Enqueue(fx);
    }
}
