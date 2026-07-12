using System.Collections.Generic;
using System.Linq;

namespace GameCore.Inventory
{
    /// <summary>
    /// Player-assigned mapping of pick-slot address ("01-02-00") -> SkuId, driven by the Slotting UI
    /// (SlotAssignmentPanel). A pick slot holds exactly one SKU; a SKU may be assigned to many pick
    /// slots. Pure string map — deliberately has no dependency on SlotRegistry (which enumerates what
    /// slots physically EXIST); the UI is responsible for only assigning addresses SlotRegistry
    /// currently reports, and for clearing an assignment once SlotRegistry stops reporting that
    /// address (rack deleted).
    /// </summary>
    public static class SlotAssignmentService
    {
        private static readonly Dictionary<string, string> _skuByAddress = new();

        public static bool TryGetSku(string address, out string skuId) => _skuByAddress.TryGetValue(address, out skuId);

        public static string GetSku(string address) => _skuByAddress.TryGetValue(address, out var skuId) ? skuId : null;

        public static bool IsAssigned(string address) => _skuByAddress.ContainsKey(address);

        public static void Assign(string address, string skuId) => _skuByAddress[address] = skuId;

        public static void Clear(string address) => _skuByAddress.Remove(address);

        public static void ClearAll() => _skuByAddress.Clear();

        /// <summary>All pick-slot addresses currently assigned to a SKU.</summary>
        public static List<string> GetSlotsForSku(string skuId)
            => _skuByAddress.Where(kv => kv.Value == skuId).Select(kv => kv.Key).ToList();

        public static IReadOnlyDictionary<string, string> AllAssignments => _skuByAddress;

        /// <summary>Flatten all assignments for saving.</summary>
        public static List<SlotAssignmentEntry> Export()
        {
            var list = new List<SlotAssignmentEntry>();
            foreach (var kv in _skuByAddress)
                list.Add(new SlotAssignmentEntry { address = kv.Key, skuId = kv.Value });
            return list;
        }

        /// <summary>Restore saved assignments (replaces current state).</summary>
        public static void Import(List<SlotAssignmentEntry> entries)
        {
            _skuByAddress.Clear();
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.address) || string.IsNullOrEmpty(e.skuId)) continue;
                _skuByAddress[e.address] = e.skuId;
            }
        }
    }

    /// <summary>Serializable form of one slot assignment, for JSON save/load.</summary>
    [System.Serializable]
    public class SlotAssignmentEntry
    {
        public string address;
        public string skuId;
    }
}
