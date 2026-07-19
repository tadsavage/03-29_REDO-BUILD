using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // NavMeshObstacle

namespace GameCore.Services
{
    /// <summary>
    /// Batches NavMesh carving updates and rebuilds once per second instead of per-obstacle.
    /// When a NavMeshObstacle is disabled (e.g., pallet picked up), add it to the queue.
    /// Rebuilds fire automatically on a heartbeat, keeping navigation responsive without
    /// the cost of a rebuild per pallet.
    /// </summary>
    public class NavMeshRebuildQueue : MonoBehaviour
    {
        private static NavMeshRebuildQueue _instance;

        private List<NavMeshObstacle> _queue = new();
        private float _rebuildInterval = 1f; // seconds
        private float _nextRebuild = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[NavMeshRebuildQueue]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NavMeshRebuildQueue>();
        }

        /// <summary>Queue an obstacle for NavMesh carving rebuild. Safe to call multiple times
        /// per obstacle (list dedupes on rebuild). Call this when you disable a NavMeshObstacle.</summary>
        public static void QueueRebuild(NavMeshObstacle obstacle)
        {
            if (_instance != null && obstacle != null)
                _instance._queue.Add(obstacle);
        }

        private void Update()
        {
            if (_queue.Count == 0) return;
            if (Time.unscaledTime < _nextRebuild) return;

            Rebuild();
            _nextRebuild = Time.unscaledTime + _rebuildInterval;
        }

        private void Rebuild()
        {
            if (_queue.Count == 0) return;

            // Dedupe: remove nulls and duplicates
            var deduped = new HashSet<NavMeshObstacle>();
            foreach (var obs in _queue)
            {
                if (obs != null) deduped.Add(obs);
            }
            _queue.Clear();

            if (deduped.Count == 0) return;

            // Destroy carving by destroying the components themselves (not the GameObject —
            // the pallet might still have other components). Destroying a NavMeshObstacle
            // automatically clears its carving from the NavMesh, letting the RTO navigate
            // smoothly. The GameObject stays, but NavMesh won't see an obstacle there anymore.
            foreach (var obs in deduped)
            {
                if (obs != null && obs.gameObject.activeInHierarchy)
                    Destroy(obs);
            }

            Debug.Log($"[NavMeshRebuildQueue] Cleared NavMesh carving for {deduped.Count} obstacles.");
        }
    }
}
