using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GameCore.Labor
{
    /// <summary>Generates unique 10-digit "license plate" Load IDs for received pallets.</summary>
    public static class LoadIDGenerator
    {
        private static readonly HashSet<string> _issued = new();

        public static string Generate()
        {
            string id;
            do
            {
                var sb = new StringBuilder(10);
                for (int i = 0; i < 10; i++) sb.Append(Random.Range(0, 10));
                id = sb.ToString();
            } while (!_issued.Add(id));

            return id;
        }

        /// <summary>Registers an already-issued Load ID (e.g. one restored from a save file) so
        /// future Generate() calls can't reissue it. Call this for every pallet Load ID restored
        /// on load — without it, _issued only tracks what THIS session generated, so a restored ID
        /// is invisible to the dedup check and could theoretically collide with a freshly-minted one.</summary>
        public static void Seed(string id)
        {
            if (!string.IsNullOrEmpty(id)) _issued.Add(id);
        }
    }
}
