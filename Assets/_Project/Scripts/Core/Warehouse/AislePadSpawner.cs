using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

            // TEMPORARY: If only 4 corridors detected, generate a 5th by extrapolation
            if (corridors.Count == 4)
            {
                var perpPositions = corridors.Select(c => c.Centerline.x).OrderBy(x => x).ToList();
                float gap1 = perpPositions[1] - perpPositions[0];
                float gap2 = perpPositions[2] - perpPositions[1];
                float gap3 = perpPositions[3] - perpPositions[2];
                float avgGap = (gap1 + gap2 + gap3) / 3f;
                float fifthPerpPos = perpPositions[3] + avgGap;

                // Create a synthetic 5th corridor at the extrapolated position
                var c5 = corridors[3]; // Copy structure from last corridor
                var synthetic5 = new Corridor
                {
                    RunAxis = c5.RunAxis,
                    Width = c5.Width,
                    Centerline = new Vector3(fifthPerpPos, c5.Centerline.y, c5.Centerline.z),
                    EndA = new Vector3(fifthPerpPos, c5.EndA.y, c5.EndA.z),
                    EndB = new Vector3(fifthPerpPos, c5.EndB.y, c5.EndB.z),
                    SideRowA = c5.SideRowA,
                    SideRowB = c5.SideRowB
                };
                corridors.Add(synthetic5);
                Debug.Log($"[AislePadSpawner] Generated synthetic 5th corridor at PerpPos={fifthPerpPos:F2}");
            }

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
