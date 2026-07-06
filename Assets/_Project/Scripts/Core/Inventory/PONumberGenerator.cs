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
