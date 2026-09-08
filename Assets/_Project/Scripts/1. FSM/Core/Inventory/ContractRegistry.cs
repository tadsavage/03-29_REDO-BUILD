using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Every contract offer in the game, in one Inspector asset — same shape and spirit as
    /// CustomerRegistry. Linear scans, not a cached dictionary: this is a hand-authored list, not a
    /// hot path, and a cache here is a staleness bug waiting to happen if the list is edited.
    ///
    /// Loaded from Resources by name so it resolves in a BUILT PLAYER, not just the editor —
    /// AssetDatabase lookups are editor-only. Put the asset at
    /// Assets/_Project/Resources/ContractRegistry.asset.
    /// </summary>
    [CreateAssetMenu(fileName = "ContractRegistry", menuName = "Warehouse/Contract Registry")]
    public class ContractRegistry : ScriptableObject
    {
        public const string ResourcePath = "ContractRegistry";

        public List<ContractData> contracts = new();

        private static ContractRegistry _cached;

        /// <summary>The project's registry, or null with one warning if the asset is missing.</summary>
        public static ContractRegistry Load()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<ContractRegistry>(ResourcePath);
            if (_cached == null)
                Debug.LogWarning($"[ContractRegistry] No '{ResourcePath}' asset under a Resources folder — " +
                                 $"no contracts will be offered. Create one via Warehouse/Contract Registry.");
            return _cached;
        }

        public ContractData GetById(string contractId)
        {
            foreach (var c in contracts)
                if (c != null && c.ContractId == contractId)
                    return c;
            return null;
        }
    }
}
