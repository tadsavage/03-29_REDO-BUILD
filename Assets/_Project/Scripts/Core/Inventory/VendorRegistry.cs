using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Every vendor in the game, in one Inspector asset — same shape and spirit as ContractRegistry
    /// and CustomerRegistry. Linear scans, not a cached dictionary: a hand-authored list is not a hot
    /// path, and a cache here is a staleness bug waiting to happen the first time the list is edited.
    ///
    /// Loaded from Resources BY NAME so it resolves in a built player, not just the editor —
    /// AssetDatabase lookups are editor-only. The asset lives at
    /// Assets/_Project/Resources/VendorRegistry.asset.
    ///
    /// REWORKED for the Partnership Level economy: every vendor is active from game start. There is
    /// no more Reputation-gated Unlocked()/Locked() split — see VendorEconomyService for the
    /// per-vendor Partnership Level that now drives cost, fill rate, damaged-goods rate and rarity
    /// gating instead.
    /// </summary>
    [CreateAssetMenu(fileName = "VendorRegistry", menuName = "Warehouse/Vendor Registry")]
    public class VendorRegistry : ScriptableObject
    {
        public const string ResourcePath = "VendorRegistry";

        public List<VendorData> vendors = new();

        private static VendorRegistry _cached;

        /// <summary>The project's registry, or null with one warning if the asset is missing.</summary>
        public static VendorRegistry Load()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<VendorRegistry>(ResourcePath);
            if (_cached == null)
                Debug.LogWarning($"[VendorRegistry] No '{ResourcePath}' asset under a Resources folder — " +
                                 $"purchasing will fall back to the open market. Create one via " +
                                 $"Warehouse/Vendor Registry.");
            return _cached;
        }

        public VendorData GetById(string vendorId)
        {
            if (string.IsNullOrEmpty(vendorId)) return null;
            foreach (var v in vendors)
                if (v != null && v.VendorId == vendorId) return v;
            return null;
        }

        /// <summary>The full roster, active from game start — every vendor deals with you the moment
        /// the game does, sorted by name so the list reads consistently rather than in whatever order
        /// the asset happens to hold.</summary>
        public List<VendorData> AllVendors
        {
            get
            {
                var list = new List<VendorData>();
                foreach (var v in vendors)
                    if (v != null) list.Add(v);
                list.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase));
                return list;
            }
        }
    }
}
