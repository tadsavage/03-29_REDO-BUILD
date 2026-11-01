using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Diagnostics;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>
    /// How long the floor needs to pick an order (Tad, 2026-10-31): the work is counted, then divided among the people who can do it.
    ///   • Whole-pallet lines are PALLET PICKS, done by reach truck operators: pallets / reach trucks, rounded up, times the minutes one takes.
    ///   • Loose cases are CASE PICKS, done by selectors on a pallet jack. Time = cases x (minutes per case: walk to the slot, take it,
    ///     carry it to the pallet, set it down) + one jack move per pick stop + one staging run per trip (up to
    ///     <see cref="OrderService.SelectorPalletsPerTrip"/> pallets). The selectors share that work.
    /// The two roles work side by side, so the order is picked when the slower of them is done. The bot uses this to decide when to
    /// release an order, and the Scheduler tile and tooltip show it to the player.
    ///
    /// Every minute figure is a game minute and comes from what this warehouse has measured (OpsMetrics) once there are a few samples; the
    /// defaults below are only used before that.
    /// </summary>
    public static class OrderPickEstimate
    {
        public const float DefaultPalletPickMinutes = 22f;    // reach truck: claim, pick up, drop at the lane, drive back (measured ~20-23, block 19-20)
        public const float DefaultCaseMinutes = 1.3f;         // one case, whole round trip (measured ~1.28 on foot, 5 trips)
        public const float DefaultBayMoveMinutes = 2.0f;      // jack from one pick stop to the next (guess until measured)
        public const float DefaultStagingMinutes = 8f;        // drive to the lane, back in, set down, leave (guess until measured)
        public const float DefaultLoadMinutes = 15f;          // dock stocker: one pallet from the lane onto the trailer (measured ~14-15)
        const int MinSamples = 3;

        public struct Result
        {
            public int palletPicks;        // whole-pallet picks still to do
            public int looseCases;         // loose cases still to pick
            public int pickStops;          // pick faces to visit (one per line with loose cases)
            public int looseTrips;         // selector trips still to do
            public int reachTrucks;        // reach truck operators on the payroll
            public int selectors;
            public float palletMinutes, caseMinutes;   // minutes per pallet pick / per case used
            public float bayMoveMinutes, stagingMinutes;
            public int pallets;            // pallets the order puts on its trailer (pallet picks + the pallets the loose cases build)
            public int dockStockers;
            public float loadMinutes;      // minutes to load one pallet
            public float loadHours;        // wall-clock hours to load them all (shared by the dock stockers)
            public float palletHours;      // wall-clock hours for the pallet picks
            public float caseHours;        // wall-clock hours for the case picks
            public float Hours => Mathf.Max(palletHours, caseHours);
            public bool HasWork => palletPicks > 0 || looseCases > 0;
            public bool NoPeople;          // work exists but nobody who can do it is employed
        }

        static float Standard(OpsMetrics.Kind k, float fallback)
        {
            var s = OpsMetrics.GetBlended(k);
            return s.n >= MinSamples && s.standard > 0.05f ? s.standard : fallback;
        }

        public static int Headcount(EmployeeRole role) => HalMetrics.Headcount(role);

        /// <summary>The estimate for the cases an order STILL needs (picked cases are not counted again).</summary>
        public static Result For(OrderData order)
            => order == null ? default : For(new[] { order });

        /// <summary>The estimate for several orders picked together (one trailer).</summary>
        public static Result For(IEnumerable<OrderData> orders)
        {
            var r = new Result();
            if (orders == null || !ServiceLocator.TryGet<OrderService>(out var svc) || svc == null) return r;

            float loosePallets = 0f;
            foreach (var o in orders)
            {
                if (o == null) continue;
                foreach (var li in o.LineItems)
                {
                    if (li == null) continue;
                    int need = li.QuantityRemaining;
                    if (need <= 0) continue;
                    int full = svc.FullPalletCases(li.SkuId);
                    int loose = need;
                    if (full > 0)
                    {
                        r.palletPicks += need / full;
                        loose = need % full;
                    }
                    if (loose > 0)
                    {
                        r.looseCases += loose;
                        r.pickStops++;
                        loosePallets += full > 0 ? loose / (float)full : loose / 48f;
                    }
                }
            }
            if (r.looseCases > 0)
                r.looseTrips = Mathf.Max(1, Mathf.CeilToInt(Mathf.CeilToInt(loosePallets - 0.0001f) / (float)OrderService.SelectorPalletsPerTrip));

            r.pallets = r.palletPicks + Mathf.CeilToInt(loosePallets - 0.0001f);
            r.dockStockers = Headcount(EmployeeRole.DockStockerOperator);
            r.reachTrucks = Headcount(EmployeeRole.ReachTruckOperator);
            r.selectors = Headcount(EmployeeRole.OrderSelector);
            r.NoPeople = (r.palletPicks > 0 && r.reachTrucks == 0) || (r.looseCases > 0 && r.selectors == 0);

            r.palletMinutes = Standard(OpsMetrics.Kind.PalletPick, DefaultPalletPickMinutes);
            r.caseMinutes = Standard(OpsMetrics.Kind.CaseCycle, DefaultCaseMinutes);
            r.bayMoveMinutes = Standard(OpsMetrics.Kind.JackBayMove, DefaultBayMoveMinutes);
            r.stagingMinutes = Standard(OpsMetrics.Kind.JackStaging, DefaultStagingMinutes);

            r.loadMinutes = Standard(OpsMetrics.Kind.LoadCycle, DefaultLoadMinutes);
            if (r.pallets > 0) r.loadHours = r.pallets * r.loadMinutes / Mathf.Max(1, r.dockStockers) / 60f;
            int rt = Mathf.Max(1, r.reachTrucks);
            if (r.palletPicks > 0) r.palletHours = Mathf.CeilToInt(r.palletPicks / (float)rt) * r.palletMinutes / 60f;
            if (r.looseCases > 0)
            {
                float totalMinutes = r.looseCases * r.caseMinutes + r.pickStops * r.bayMoveMinutes + r.looseTrips * r.stagingMinutes;
                int working = Mathf.Max(1, Mathf.Min(r.selectors, r.looseTrips));   // one trip cannot be split between two selectors
                r.caseHours = totalMinutes / working / 60f;
            }
            return r;
        }

        /// <summary>"3h 15m" style text for a number of game hours.</summary>
        public static string Format(float hours)
        {
            int mins = Mathf.Max(0, Mathf.RoundToInt(hours * 60f));
            int h = mins / 60, m = mins % 60;
            return h > 0 ? (m > 0 ? $"{h}h {m:00}m" : $"{h}h") : $"{m}m";
        }

        /// <summary>One line for a tooltip: what has to be picked and how long it should take.</summary>
        public static string Describe(Result r)
        {
            if (!r.HasWork) return "Nothing left to pick";
            var parts = new List<string>();
            if (r.palletPicks > 0)
                parts.Add($"{r.palletPicks} pallet pick(s) x {r.palletMinutes:0} min over {Mathf.Max(1, r.reachTrucks)} reach truck(s) = {Format(r.palletHours)}");
            if (r.looseCases > 0)
                parts.Add($"{r.looseCases} loose case(s) x {r.caseMinutes:0.0} min + {r.pickStops} stop(s) x {r.bayMoveMinutes:0.0} min + {r.looseTrips} staging run(s) x {r.stagingMinutes:0} min, " +
                          $"shared by {Mathf.Max(1, Mathf.Min(r.selectors, r.looseTrips))} selector(s) = {Format(r.caseHours)}");
            string s = string.Join("; ", parts) + $" -> about {Format(r.Hours)} to pick, then about {Format(r.loadHours)} to load {r.pallets} pallet(s) with {Mathf.Max(1, r.dockStockers)} dock stocker(s)";
            if (r.NoPeople) s += " (nobody hired for part of this work)";
            return s;
        }
    }
}
