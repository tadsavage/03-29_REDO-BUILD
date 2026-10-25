using System.Collections.Generic;
using System.Linq;
using GameCore.Economy;
using GameCore.Services;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Samples the warehouse once per in-game hour (pallets on hand + inventory value at cost) so the
    /// Warehouse Capacity report can draw history. Self-bootstraps like SlotRegistry/LaneNamingService (hidden
    /// DontDestroyOnLoad object, no scene wiring) and POLLS the clock instead of subscribing to an event, so it
    /// can never be left subscribed to a dead EventManager. In-memory only: the history starts fresh each session.
    /// </summary>
    public class InventoryHistory : MonoBehaviour
    {
        public struct Sample
        {
            public float Hours;     // absolute game hours: Day * 24 + Hour
            public int Pallets;     // product pallets on hand
            public float Value;     // inventory value at cost
        }

        private const int MaxSamples = 24 * 21;

        private static InventoryHistory _instance;
        private static readonly List<Sample> _samples = new();
        private int _lastHourKey = int.MinValue;

        public static IReadOnlyList<Sample> Samples => _samples;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // HideInHierarchy + DontDestroyOnLoad (not HideAndDontSave): no domain reload between Play sessions, so sweep leftovers.
            foreach (var stale in Resources.FindObjectsOfTypeAll<InventoryHistory>())
                if (stale != null && stale != _instance) DestroyImmediate(stale.gameObject);
            if (_instance != null) return;
            _samples.Clear();
            var go = new GameObject("[InventoryHistory]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<InventoryHistory>();
        }

        /// <summary>Product pallets on hand and their value at cost, the same way the report totals them.</summary>
        public static void Measure(InventoryService inv, out int pallets, out float value)
        {
            pallets = 0; value = 0f;
            if (inv == null) return;
            foreach (var p in inv.GetAllPallets())
            {
                var sku = inv.GetSkuData(p.SkuId);
                if (sku == null) continue;          // empty carriers etc. are not product
                pallets++;
                value += p.Quantity * sku.BuyValue;
            }
        }

        /// <summary>Game time as absolute hours including the minutes (for a smoothly scrolling chart).</summary>
        public static bool TryGetNowHours(out float hours)
        {
            hours = 0f;
            if (!ServiceLocator.TryGet<SimulationTimeService>(out var t) || t == null) return false;
            hours = t.Day * 24f + t.Hour + t.Minute / 60f;
            return true;
        }

        private void Update()
        {
            if (!ServiceLocator.TryGet<SimulationTimeService>(out var time) || time == null) return;
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return;

            int key = time.Day * 24 + time.Hour;
            if (key == _lastHourKey) return;

            // Time went backwards (a different save was loaded): drop everything from "the future".
            if (_lastHourKey != int.MinValue && key < _lastHourKey)
                _samples.RemoveAll(s => s.Hours > key + 0.01f);
            _lastHourKey = key;

            Measure(inv, out int pallets, out float value);
            _samples.Add(new Sample { Hours = key, Pallets = pallets, Value = value });
            if (_samples.Count > MaxSamples) _samples.RemoveRange(0, _samples.Count - MaxSamples);
        }

        /// <summary>Average inventory value for each of the last <paramref name="days"/> days that have samples, oldest first.</summary>
        public static List<(int day, float avgValue)> AverageValueByDay(int days)
        {
            var result = new List<(int, float)>();
            foreach (var g in _samples.GroupBy(s => Mathf.FloorToInt(s.Hours / 24f)).OrderBy(g => g.Key))
                result.Add((g.Key, (float)g.Average(s => s.Value)));
            if (result.Count > days) result.RemoveRange(0, result.Count - days);
            return result;
        }
    }
}
