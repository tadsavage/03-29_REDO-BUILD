using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// One physical pallet placed inside a trailer: what's on it, and where it rides.
    ///
    /// This is the unit the trailer actually carries. An order line of 500 cases is not a thing that
    /// can be loaded — it's some number of these.
    /// </summary>
    public struct PlannedPallet
    {
        public string SkuId;
        /// <summary>Cases on THIS pallet. The last pallet of a line carries the remainder.</summary>
        public int Cases;
        /// <summary>Loaded height in metres, from SkuData.PltHeight — always the SKU's FULL pallet
        /// height, even for a part-filled pallet (see TrailerCapacity's class comment).</summary>
        public float Height;
        public bool IsTall;
        /// <summary>0–11. 0–5 is the left row, 6–11 the right — matches TruckController.SlotLocalPosition.</summary>
        public int FloorSlot;
        /// <summary>0 = on the deck, 1 = stacked on top of the tier-0 pallet in the same slot.</summary>
        public int Tier;
    }

    /// <summary>How a basket of cases packs into one trailer.</summary>
    public struct TrailerLoadPlan
    {
        public List<PlannedPallet> Pallets;
        /// <summary>Floor positions consumed, 0–12. The authoritative capacity measure.</summary>
        public int FloorSlotsUsed;
        /// <summary>Capacity units consumed out of MaxUnits (24): a short pallet is 1, a tall one 2.
        /// What the fill bar shows.</summary>
        public int UnitsUsed;
        public int TallCount;
        public int ShortCount;
        public bool OverCapacity => FloorSlotsUsed > TrailerCapacity.FloorSlots;
        public float Fill01 => Mathf.Clamp01(UnitsUsed / (float)TrailerCapacity.MaxUnits);
    }

    /// <summary>
    /// Works out how much of a trailer an inbound order fills, and where each pallet rides.
    ///
    /// THE RULE, in Tad's terms: the trailer has 12 floor positions (6 down each side) and 1.9m of
    /// safe interior height. A pallet at or under 0.95m can have another short pallet stacked on it,
    /// so it costs 1/24 of the load. A pallet over 0.95m rides alone in its footprint and costs
    /// 1/12 — a whole floor position. Stacks are two high, never three, and may MIX SKUs freely.
    ///
    /// HEIGHT IS ALWAYS THE SKU'S FULL PALLET HEIGHT (Tad's call), even when the line only part-fills
    /// its last pallet. The alternative — measuring the actual layers on a part pallet — is more
    /// physically honest but makes the same item stackable or not depending on how many cases you
    /// happened to order, which is unpredictable while you're building an order. One SKU, one answer.
    ///
    /// CAPACITY IS CHECKED IN FLOOR POSITIONS, NOT UNITS. A lone short pallet still occupies a whole
    /// floor position until a second short joins it, so units alone would let an order look like it
    /// fits when it physically doesn't. The two agree at the boundary: with U = 2*tall + short and
    /// F = tall + ceil(short/2), U = 24 forces short to be even, so F is exactly 12. The unit count
    /// is for the fill BAR (it moves in 1/24ths as Tad specced); the floor count is the gate.
    /// </summary>
    public static class TrailerCapacity
    {
        /// <summary>Usable interior height, metres. Two stacked pallets must both fit under this.</summary>
        public const float SafeInteriorHeight = 1.90f;

        /// <summary>At or below this a pallet can be stacked; above it, it rides alone. Exactly half
        /// the safe height, so two of the tallest stackable pallets still clear the roof.</summary>
        public const float StackableHeight = SafeInteriorHeight / 2f;   // 0.95

        /// <summary>Floor positions: 6 down each side.</summary>
        public const int FloorSlots = 12;

        /// <summary>Capacity units — every floor position double-stacked with short pallets.</summary>
        public const int MaxUnits = FloorSlots * 2;                     // 24

        /// <summary>Is this SKU's pallet too tall to have anything stacked on it?</summary>
        public static bool IsTall(SkuData sku) => sku != null && sku.PltHeight > StackableHeight;

        /// <summary>Cases that fit on one pallet of this SKU. Guards against unauthored Ti/Hi, which
        /// would otherwise divide by zero and report an infinite pallet count.</summary>
        public static int CasesPerPallet(SkuData sku)
            => sku != null && sku.Ti > 0 && sku.Hi > 0 ? sku.Ti * sku.Hi : 1;

        /// <summary>How many physical pallets a line of this many cases needs. A part pallet still
        /// takes a whole one — you can't ship a third of a pallet.</summary>
        public static int PalletsFor(SkuData sku, int cases)
            => cases <= 0 ? 0 : Mathf.CeilToInt(cases / (float)CasesPerPallet(sku));

        /// <summary>
        /// Plans a whole basket into a trailer: expands each line into pallets, sorts them into tall
        /// and short, and assigns every one a floor slot and tier.
        ///
        /// TALL PALLETS ARE PLACED FIRST, each taking its own floor position, then shorts fill the
        /// remaining positions two-high. Doing it the other way round can strand a tall pallet with
        /// only half-occupied positions left — space that exists but that nothing tall can use.
        ///
        /// Returns a plan even when it doesn't fit: OverCapacity says so and the surplus pallets get
        /// FloorSlot = -1. The caller decides whether to refuse — the UI wants to SHOW an over-full
        /// load, not be unable to describe one.
        /// </summary>
        public static TrailerLoadPlan Plan(IEnumerable<(SkuData sku, int cases)> lines)
        {
            var tall = new List<PlannedPallet>();
            var shorts = new List<PlannedPallet>();

            if (lines != null)
            {
                foreach (var (sku, cases) in lines)
                {
                    if (sku == null || cases <= 0) continue;

                    int perPallet = CasesPerPallet(sku);
                    int count = PalletsFor(sku, cases);
                    bool isTall = IsTall(sku);
                    int remaining = cases;

                    for (int i = 0; i < count; i++)
                    {
                        int onThis = Mathf.Min(perPallet, remaining);
                        remaining -= onThis;

                        var pallet = new PlannedPallet
                        {
                            SkuId = sku.SkuId,
                            Cases = onThis,
                            Height = sku.PltHeight,
                            IsTall = isTall,
                            FloorSlot = -1,
                            Tier = 0
                        };
                        (isTall ? tall : shorts).Add(pallet);
                    }
                }
            }

            var placed = new List<PlannedPallet>(tall.Count + shorts.Count);
            int slot = 0;

            foreach (var p in tall)
            {
                var t = p;
                t.FloorSlot = slot < FloorSlots ? slot : -1;   // -1 = didn't fit
                t.Tier = 0;
                placed.Add(t);
                slot++;
            }

            // Shorts pair up: two per floor position, tier 0 then tier 1. Mixed SKUs are fine.
            for (int i = 0; i < shorts.Count; i++)
            {
                var s = shorts[i];
                int pairSlot = slot + i / 2;
                s.FloorSlot = pairSlot < FloorSlots ? pairSlot : -1;
                s.Tier = i % 2;
                placed.Add(s);
            }

            int shortSlots = Mathf.CeilToInt(shorts.Count / 2f);

            return new TrailerLoadPlan
            {
                Pallets = placed,
                TallCount = tall.Count,
                ShortCount = shorts.Count,
                FloorSlotsUsed = tall.Count + shortSlots,
                UnitsUsed = tall.Count * 2 + shorts.Count
            };
        }

        /// <summary>Convenience for the UI: would adding <paramref name="extraCases"/> of a SKU to an
        /// existing basket push it past a trailer? Plans the hypothetical basket rather than trying to
        /// reason about deltas — a short pallet can be free (pairing with an odd one) or cost a whole
        /// floor position, and only a full re-plan knows which.</summary>
        public static bool WouldOverflow(IEnumerable<(SkuData sku, int cases)> basket,
                                         SkuData sku, int newCasesForThatSku,
                                         out TrailerLoadPlan planned)
        {
            var hypothetical = new List<(SkuData, int)>();
            bool replaced = false;
            if (basket != null)
            {
                foreach (var (s, c) in basket)
                {
                    if (sku != null && s == sku) { hypothetical.Add((s, newCasesForThatSku)); replaced = true; }
                    else hypothetical.Add((s, c));
                }
            }
            if (!replaced && sku != null && newCasesForThatSku > 0)
                hypothetical.Add((sku, newCasesForThatSku));

            planned = Plan(hypothetical);
            return planned.OverCapacity;
        }
    }
}
