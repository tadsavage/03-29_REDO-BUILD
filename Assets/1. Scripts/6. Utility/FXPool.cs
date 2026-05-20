using UnityEngine;
using System.Collections.Generic;

public class FXPool : MonoBehaviour
{
    public static FXPool Instance { get; private set; }

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

        foreach (var entry in entries)
        {
            var q = new Queue<FXObject>();
            _pools[entry.key] = q;

            for (int i = 0; i < entry.preload; i++)
                q.Enqueue(CreateInstance(entry.prefab));
        }
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
        }

        return new FXObject
        {
            go = go,
            systems = systems
        };
    }
    public void Play(string key, Vector3 position)
    {
        if (!_pools.TryGetValue(key, out var q))
        {
            Debug.LogWarning($"FXPool: No pool for key '{key}'");
            return;
        }

        FXObject fx;

        if (q.Count > 0)
            fx = q.Dequeue();
        else
        {
            Debug.LogWarning($"FXPool: Pool for key '{key}' is empty, instantiating new instance");
            fx = CreateInstance(FindEntry(key).prefab);
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

        fx.go.SetActive(false);
        _pools[key].Enqueue(fx);
    }
}
