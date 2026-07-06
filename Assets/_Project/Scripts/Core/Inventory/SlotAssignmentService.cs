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
    ///
    /// In-memory only for now — not persisted to save files. Lost on domain reload / app restart,
    /// same known limitation as ShiftManagerPanel's schedules. A future Export()/Import() pair (same
    /// shape as LaneConfigRegistry's) is the natural follow-up once a save slot exists for it.
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
    }
}
