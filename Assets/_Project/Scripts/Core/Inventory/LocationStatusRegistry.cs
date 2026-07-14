using System.Collections.Generic;

namespace GameCore.Inventory
{
    /// <summary>
    /// Runtime store of per-slot <see cref="LocationStatus"/> values for every reserve and pick
    /// slot in the racking system. Untracked slots default to <see cref="LocationStatus.Available"/>.
    ///
    /// Keyed by slot address (e.g. "01-02-A0"), the same format <see cref="SlotRegistry"/> uses.
    /// In-memory only for now; <see cref="Export"/> and <see cref="Import"/> are the save/load hooks.
    /// </summary>
    public static class LocationStatusRegistry
    {
        private static readonly Dictionary<string, LocationStatus> _statusByAddress = new();

        // ============ QUERIES ============

        /// <summary>Returns the status of a slot. Defaults to <see cref="LocationStatus.Available"/>
        /// for any address not yet tracked.</summary>
        public static LocationStatus Get(string address)
            => _statusByAddress.TryGetValue(address, out var s) ? s : LocationStatus.Available;

        /// <summary>True if the slot is explicitly or implicitly <see cref="LocationStatus.Available"/>.</summary>
        public static bool IsAvailable(string address)
            => Get(address) == LocationStatus.Available;

        // ============ MUTATIONS ============

        /// <summary>Explicitly set a slot's status.</summary>
        public static void Set(string address, LocationStatus status)
            => _statusByAddress[address] = status;

        /// <summary>Lock a slot for an in-progress putaway task.</summary>
        public static void Reserve(string address)
            => Set(address, LocationStatus.Reserved);

        /// <summary>Mark a slot as occupied once a putaway completes and the pallet is released.</summary>
        public static void MarkOccupied(string address)
            => Set(address, LocationStatus.Occupied);

        /// <summary>Return a slot to Available (e.g. task cancelled, or pallet removed from slot).</summary>
        public static void Release(string address)
        {
            if (_statusByAddress.ContainsKey(address))
                _statusByAddress.Remove(address); // untracked == Available
        }

        /// <summary>Clear all tracked statuses (use before restoring from save).</summary>
        public static void ClearAll() => _statusByAddress.Clear();

        // ============ PERSISTENCE ============

        /// <summary>Flatten all non-default statuses for saving.</summary>
        public static List<LocationStatusEntry> Export()
        {
            var list = new List<LocationStatusEntry>();
            foreach (var kv in _statusByAddress)
                list.Add(new LocationStatusEntry { address = kv.Key, status = (int)kv.Value });
            return list;
        }

        /// <summary>Restore saved statuses (replaces current state).</summary>
        public static void Import(List<LocationStatusEntry> entries)
        {
            _statusByAddress.Clear();
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.address)) continue;
                _statusByAddress[e.address] = (LocationStatus)e.status;
            }
        }
    }

    /// <summary>Serializable form of one slot status entry, for JSON save/load.</summary>
    [System.Serializable]
    public class LocationStatusEntry
    {
        public string address;
        public int status; // cast to/from LocationStatus
    }
}
