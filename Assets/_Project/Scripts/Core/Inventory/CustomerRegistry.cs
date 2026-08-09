using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>List of all CustomerData assets, same shape as ObjDataRegistry — a single Inspector
    /// asset the order generator pulls a random customer from. Linear scan, not a cached
    /// dictionary — this is a ~25-entry hand-authored roster, not a hot path, and a cache here
    /// is just a staleness bug waiting to happen if the list is ever edited after first load.</summary>
    [CreateAssetMenu(fileName = "CustomerRegistry", menuName = "Warehouse/Customer Registry")]
    public class CustomerRegistry : ScriptableObject
    {
        public const string ResourcePath = "CustomerRegistry";

        public List<CustomerData> customers = new();

        private static CustomerRegistry _cached;

        /// <summary>
        /// The project's roster, for callers with no Inspector to assign one from — the daily Bulk
        /// offer roll in OrderArrivalService, which is a plain service.
        ///
        /// Mirrors ContractRegistry.Load, but with a fallback ContractRegistry doesn't need: its asset
        /// lives at _Project/Resources/ContractRegistry.asset, whereas the CustomerRegistry asset sits
        /// in _Project/ScriptableObjects/ where Resources.Load cannot see it. FindObjectsOfTypeAll
        /// finds it anyway in the EDITOR, so this works today without relocating a hand-authored asset
        /// out from under whatever references it.
        ///
        /// THAT FALLBACK WILL NOT SAVE A BUILT PLAYER. An asset outside Resources with no scene
        /// reference isn't included in the build at all, so bulk offers would silently stop appearing.
        /// Move (or duplicate) the asset to Assets/_Project/Resources/CustomerRegistry.asset before
        /// shipping — the warning below says so at runtime.
        /// </summary>
        public static CustomerRegistry Load()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<CustomerRegistry>(ResourcePath);
            if (_cached != null) return _cached;

            var all = Resources.FindObjectsOfTypeAll<CustomerRegistry>();
            if (all.Length > 0)
            {
                _cached = all[0];
                Debug.LogWarning($"[CustomerRegistry] Found '{_cached.name}' outside Resources. This works in " +
                                 $"the editor ONLY — move it to Assets/_Project/Resources/{ResourcePath}.asset " +
                                 $"or a build will ship without a customer roster.");
                return _cached;
            }

            Debug.LogWarning($"[CustomerRegistry] No '{ResourcePath}' asset found — no bulk offers can be " +
                             $"generated. Create one via Warehouse/Customer Registry.");
            return null;
        }

        public CustomerData GetById(string customerId)
        {
            foreach (var c in customers)
                if (c != null && c.CustomerId == customerId)
                    return c;
            return null;
        }

        public CustomerData GetRandom()
        {
            if (customers == null || customers.Count == 0) return null;
            return customers[Random.Range(0, customers.Count)];
        }
    }
}
