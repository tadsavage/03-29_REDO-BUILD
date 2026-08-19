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

        /// <summary>
        /// BROKER-TIER VENDORS ARE EXCLUDED FROM BOTH LISTS BELOW, deliberately.
        ///
        /// Every caller of Unlocked/Locked is asking "which houses can I shop?", and the broker isn't
        /// one — he has no catalogue at all, he sells whole unmanifested trailers through
        /// BrokerService. Left in, he'd render as a supplier chip whose catalogue is empty, and
        /// selecting him would show "0 item(s) available for ordering", which reads as a bug.
        ///
        /// He's still in the registry, and still reached by <see cref="GetById"/> — that's how
        /// BrokerService finds his reputation gate.
        /// </summary>
        private static bool IsShoppable(VendorData v) => v != null && v.Tier != VendorTier.Broker;

        /// <summary>Vendors the player has earned access to, cheapest tier first then by name, so the
        /// list reads as a progression rather than in whatever order the asset happens to hold.</summary>
        public List<VendorData> Unlocked(int reputation)
        {
            var list = new List<VendorData>();
            foreach (var v in vendors)
                if (IsShoppable(v) && reputation >= v.ReputationRequired) list.Add(v);
            list.Sort(CompareForDisplay);
            return list;
        }

        /// <summary>Vendors that exist but are still out of reach. Shown greyed rather than hidden —
        /// a locked door you can see is a goal, a locked door you can't is just a smaller game.</summary>
        public List<VendorData> Locked(int reputation)
        {
            var list = new List<VendorData>();
            foreach (var v in vendors)
                if (IsShoppable(v) && reputation < v.ReputationRequired) list.Add(v);
            list.Sort(CompareForDisplay);
            return list;
        }

        private static int CompareForDisplay(VendorData a, VendorData b)
        {
            int byReq = a.ReputationRequired.CompareTo(b.ReputationRequired);
            if (byReq != 0) return byReq;
            return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
