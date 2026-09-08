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
            // NO-OP BY DESIGN — kept only so existing QueueRebuild() call sites stay valid.
            //
            // This used to Destroy() the NavMeshObstacle component outright. Two problems:
            //   1. It was pointless. Pallets were configured with carving = false (BuildingData
            //      .ConfigureObstacle), so the obstacle never cut the NavMesh in the first place —
            //      destroying it changed nothing about navigation.
            //   2. It was destructive. The component never came back, so once a pallet had been
            //      picked up it could never block again; the obstacle.enabled = true on the next
            //      drop-off resolved to null and silently did nothing.
            //
            // Pallets now carve (see ConfigureObstacle). Disabling a carving NavMeshObstacle removes
            // its carve immediately and re-enabling restores it — no batching, no rebuild, and
            // certainly no destroying components. So there is nothing left for this queue to do.
            _queue.Clear();
        }
    }
}
