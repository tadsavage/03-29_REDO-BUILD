using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Generates 6-digit continuous PO numbers starting at 100000.
    /// Numbers are persisted across saves/loads via PlayerPrefs.
    /// </summary>
    public static class PONumberGenerator
    {
        private const string PREF_KEY = "PO_NextNumber";
        private const int START_NUMBER = 100000;
        private static int _nextNumber = -1;

        /// <summary>
        /// Get the next PO number as a 6-digit string (e.g., "100000", "100001", etc.).
        /// Auto-increments and persists the counter.
        /// </summary>
        public static string GetNextPONumber()
        {
            if (_nextNumber == -1)
            {
                LoadFromPrefs();
            }

            string poNumber = _nextNumber.ToString("D6");
            _nextNumber++;
            SaveToPrefs();
            return poNumber;
        }

        /// <summary>
        /// A RANDOM 6-digit PO number, unique against every number issued this session.
        ///
        /// What the Purchasing panel uses. A sequential counter is what a single warehouse's own ERP
        /// would really produce, but the player is reading these as identifiers — "837194" is a thing
        /// you recognise on a list, where "100004" sitting under "100003" is a row number wearing a
        /// disguise, and telling two of them apart at a glance is exactly what the PO List asks you to
        /// do.
        ///
        /// Dedup is against a session set rather than the persisted counter, since random numbers have
        /// no ordering to persist. A collision inside one session is what actually matters — two live
        /// POs sharing a number would make TrySpawnTruck's duplicate check reject the second one's
        /// truck. Numbers from a loaded save are re-registered by ShipmentService.Import so a restored
        /// PO can't be collided with either.
        /// </summary>
        public static string GetRandomPONumber()
        {
            // 100000..999999 keeps it exactly six digits — no leading zero to be lost by an int round
            // trip, and no seven-digit outlier to break the layout.
            for (int attempt = 0; attempt < 64; attempt++)
            {
                string candidate = Random.Range(100000, 1000000).ToString();
                if (_issued.Add(candidate)) return candidate;
            }

            // 64 straight collisions against a 900k space means something is badly wrong (a stuck RNG,
            // or _issued somehow enormous). Fall back to the sequential counter rather than loop
            // forever or hand back a duplicate.
            Debug.LogWarning("[PONumberGenerator] Could not find a free random PO number — falling back " +
                             "to the sequential counter.");
            string seq = GetNextPONumber();
            _issued.Add(seq);
            return seq;
        }

        /// <summary>Every PO number handed out or restored this session. See GetRandomPONumber.</summary>
        private static readonly System.Collections.Generic.HashSet<string> _issued = new();

        /// <summary>Claims a number that already exists — called by ShipmentService.Import for each
        /// restored PO, so a save's numbers can't be re-issued to a new order after loading.</summary>
        public static void RegisterExisting(string poNumber)
        {
            if (!string.IsNullOrEmpty(poNumber)) _issued.Add(poNumber);
        }

        /// <summary>Load the counter from PlayerPrefs. Called automatically on first use.</summary>
        private static void LoadFromPrefs()
        {
            _nextNumber = PlayerPrefs.GetInt(PREF_KEY, START_NUMBER);
        }

        /// <summary>Save the counter to PlayerPrefs. Called after each PO number generation.</summary>
        private static void SaveToPrefs()
        {
            PlayerPrefs.SetInt(PREF_KEY, _nextNumber);
            PlayerPrefs.Save();
        }

        /// <summary>Reset the counter to the starting number (for testing/development only).</summary>
        public static void ResetCounter()
        {
            _nextNumber = START_NUMBER;
            SaveToPrefs();
            Debug.Log("[PONumberGenerator] Counter reset to 100000");
        }

        /// <summary>Get the current counter value (for debugging/display).</summary>
        public static int GetCurrentCounter()
        {
            if (_nextNumber == -1)
            {
                LoadFromPrefs();
            }
            return _nextNumber;
        }
    }
}
