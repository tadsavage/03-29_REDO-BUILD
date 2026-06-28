using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Warehouse
{
    /// <summary>
    /// Detects corridors from the placed racks and spawns a glowing chevron <see cref="AislePad"/> at
    /// each end. Self-bootstraps after the scene loads and waits until the racks (which register with
    /// RackLabelDisplay on enable) have finished appearing. Call <see cref="Refresh"/> after the
    /// player builds/removes racks to rebuild the pads.
    /// </summary>
    public class AislePadSpawner : MonoBehaviour
    {
        private static AislePadSpawner _instance;
        private readonly List<GameObject> _pads = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("AislePadSpawner");
            _instance = go.AddComponent<AislePadSpawner>();
            DontDestroyOnLoad(go);
        }

        public static void Refresh()
        {
            if (_instance != null) _instance.Rebuild();
        }

        private void Start() => StartCoroutine(WaitThenSpawn());

        private IEnumerator WaitThenSpawn()
        {
            // Wait until the rack count settles (saves instantiate racks during GameContext.Start).
            int last = -1, stable = 0;
            float timeout = 8f;
            while (stable < 10 && timeout > 0f)
            {
                int n = RackLabelDisplay.AllLabels.Count;
                stable = (n == last && n > 0) ? stable + 1 : 0;
                last = n;
                timeout -= Time.deltaTime;
                yield return null;
            }
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (var p in _pads) if (p != null) Destroy(p);
            _pads.Clear();

            var corridors = CorridorDetector.Detect();
            Debug.Log($"[AislePadSpawner] Detected {corridors.Count} corridors from {RackLabelDisplay.AllLabels.Count} rack sections.");

            foreach (var c in corridors)
            {
                Vector3 aToB = (c.EndB - c.EndA); aToB.y = 0f;
                if (aToB.sqrMagnitude < 0.01f)
                {
                    Debug.Log($"[AislePadSpawner] Skipping corridor - EndA and EndB too close");
                    continue;
                }
                Vector3 dir = aToB.normalized;

                // Single pad at end A (dock), pointing into the aisle toward B.
                Debug.Log($"[AislePadSpawner] Creating pad at EndA={c.EndA} pointing toward EndB={c.EndB}, direction={dir}");
                _pads.Add(AislePad.Create(c, c.EndA, dir, transform).gameObject);
            }

            Debug.Log($"[AislePadSpawner] Spawned {_pads.Count} pads");
        }
    }
}
