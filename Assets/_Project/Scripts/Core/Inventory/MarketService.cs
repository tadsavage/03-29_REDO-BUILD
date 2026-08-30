using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Economy;
using GameCore.Events;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>
    /// THE MARKET — what a case costs today, and what's on offer today.
    ///
    /// Phase 1 of the purchasing redesign (see PURCHASING_DESIGN.md). Before this, every SKU had one
    /// price that never moved and every PO arrived exactly as ordered, which made buying arithmetic
    /// rather than a decision. This service supplies the two things that turn it into a bet:
    ///
    ///   PRICES THAT MOVE   a daily per-SKU price that drifts around the SKU's authored BuyValue,
    ///                      with seven days of history behind it so the panel can draw a sparkline.
    ///                      Buying low becomes a real skill instead of a rounding difference.
    ///
    ///   SPOT DEALS         three offers a day, discounted, expiring tonight. Not a shop — a DRAFT.
    ///                      Taking one means the capital isn't there for the other two.
    ///
    /// Deliberately does NOT create purchase orders. It publishes prices and offers; PurchasingPanel
    /// owns the basket, the trailer plan and the actual PO, because that's where the capacity planner
    /// and the line-item construction already live and two places building POs would drift apart.
    ///
    /// Registered in GameContext AFTER InventoryService (it reads the SKU database on Initialize).
    /// </summary>
    public class MarketService : IService
    {
        // ── Tunables ─────────────────────────────────────────────────────────

        /// <summary>Days of price history kept per SKU — the width of the sparkline.</summary>
        public const int HistoryDays = 7;

        /// <summary>Maximum single-day random move, as a fraction of the current price. At 0.08 a
        /// $20 case swings up to ±$1.60 overnight: visible on the sparkline, not chaotic.</summary>
        private const float DailyVolatility = 0.08f;

        /// <summary>
        /// How hard each day's price is pulled back toward the SKU's authored BuyValue.
        ///
        /// LOAD-BEARING — do not set to 0. A pure random walk wanders off and never comes back, so
        /// after a few in-game weeks half the catalogue sits at the clamp and the sparkline is a flat
        /// line against a wall. Mean reversion is what keeps BuyValue meaningful as the "normal"
        /// price and keeps every day's chart worth reading.
        /// </summary>
        private const float MeanReversion = 0.25f;

        /// <summary>Hard floor/ceiling as a multiple of authored BuyValue. Bounds the bet: a player
        /// can't be ruined by a price spike, and can't trivially win off one crash.</summary>
        private const float MinPriceFactor = 0.70f;
        private const float MaxPriceFactor = 1.35f;

        /// <summary>How many spot deals are on the board at once.</summary>
        public const int SpotDealsPerDay = 3;

        private const int DealPalletsMin = 2;
        private const int DealPalletsMax = 6;
        private const float DealDiscountMin = 0.15f;
        private const float DealDiscountMax = 0.45f;

        // ── State ────────────────────────────────────────────────────────────

        private readonly Dictionary<string, int> _priceBySku = new();
        private readonly Dictionary<string, List<int>> _historyBySku = new();
        private readonly List<SpotDeal> _deals = new();

        private EventManager _eventManager;
        private InventoryService _inventory;
        private SimulationTimeService _clock;

        /// <summary>Day the current board of spot deals was generated for. Guards against a double
        /// roll (two OnDayChanged for one day would otherwise replace the board mid-day and yank an
        /// offer out from under the player as they were reaching for it).</summary>
        private int _dealsGeneratedForDay = -1;

        public IReadOnlyList<SpotDeal> Deals => _deals;

        /// <summary>Offers still on the board: not claimed, not expired.</summary>
        public IEnumerable<SpotDeal> LiveDeals =>
            _deals.Where(d => !d.Claimed && d.ExpiresAfterDay >= CurrentDay);

        // ── Lifecycle ────────────────────────────────────────────────────────

        public void Initialize()
        {
            _eventManager = EventManager.Instance;
            ServiceLocator.TryGet(out _inventory);
            ServiceLocator.TryGet(out _clock);

            SeedPricesIfEmpty();
            EnsureDealsForToday();

            if (_eventManager != null)
                _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
        }

        public void Shutdown()
        {
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            _priceBySku.Clear();
            _historyBySku.Clear();
            _deals.Clear();
            _dealsGeneratedForDay = -1;
        }

        public void ClearAll()
        {
            _priceBySku.Clear();
            _historyBySku.Clear();
            _deals.Clear();
            _dealsGeneratedForDay = -1;
        }

        private int CurrentDay => _clock?.Day ?? 1;

        // ── Prices ───────────────────────────────────────────────────────────

        /// <summary>Today's price per case. Falls back to the SKU's authored BuyValue for anything the
        /// market hasn't seen — a SKU asset added mid-session still costs something sane.</summary>
        public int CurrentPrice(SkuData sku)
        {
            if (sku == null) return 0;
            if (_priceBySku.TryGetValue(sku.SkuId, out int p)) return p;
            int seeded = Mathf.Max(1, Mathf.RoundToInt(sku.BuyValue));
            _priceBySku[sku.SkuId] = seeded;
            return seeded;
        }

        public int CurrentPrice(string skuId)
        {
            if (string.IsNullOrEmpty(skuId)) return 0;
            if (_priceBySku.TryGetValue(skuId, out int p)) return p;
            var sku = _inventory?.GetSkuData(skuId);
            return sku != null ? CurrentPrice(sku) : 0;
        }

        /// <summary>Oldest-first price history, up to <see cref="HistoryDays"/> entries, the last of
        /// which is today. Never null — an unknown SKU returns an empty list.</summary>
        public IReadOnlyList<int> History(string skuId) =>
            _historyBySku.TryGetValue(skuId, out var h) ? h : System.Array.Empty<int>();

        /// <summary>Today's move against yesterday, as a percentage. 0 when there's no yesterday to
        /// compare against, so a fresh save reads "flat" rather than inventing a spike.</summary>
        public float DeltaPercent(string skuId)
        {
            var h = History(skuId);
            if (h.Count < 2) return 0f;
            int prev = h[h.Count - 2];
            if (prev <= 0) return 0f;
            return (h[h.Count - 1] - prev) / (float)prev * 100f;
        }

        /// <summary>Today's price against the SKU's authored normal. Negative = a genuine bargain
        /// rather than merely cheaper than yesterday, which is the number worth buying on.</summary>
        public float VsNormalPercent(SkuData sku)
        {
            if (sku == null || sku.BuyValue <= 0f) return 0f;
            return (CurrentPrice(sku) - sku.BuyValue) / sku.BuyValue * 100f;
        }

        /// <summary>
        /// Gives every known SKU a starting price and a plausible run-up to it.
        ///
        /// The back-history is GENERATED, not recorded — on a fresh save there are no previous days.
        /// It's seeded anyway because the alternative is a flat, meaningless sparkline for the first
        /// in-game week, which is exactly the stretch where the player is learning to read the chart.
        /// The world is assumed to have had prices before the player showed up.
        /// </summary>
        private void SeedPricesIfEmpty()
        {
            if (_inventory == null) return;

            foreach (var sku in _inventory.AllSkus)
            {
                if (sku == null || _priceBySku.ContainsKey(sku.SkuId)) continue;

                float basePrice = Mathf.Max(1f, sku.BuyValue);
                var history = new List<int>();

                // Walk forward from a point near normal so the last entry — today — is the price the
                // player is quoted, and the six before it show how it got there.
                float p = basePrice * Random.Range(0.92f, 1.08f);
                for (int i = 0; i < HistoryDays; i++)
                {
                    p = Step(p, basePrice);
                    history.Add(Mathf.Max(1, Mathf.RoundToInt(p)));
                }

                _historyBySku[sku.SkuId] = history;
                _priceBySku[sku.SkuId] = history[history.Count - 1];
            }
        }

        /// <summary>One day's price move: mean-reverting random walk, clamped to the band.</summary>
        private static float Step(float current, float basePrice)
        {
            float pull = (basePrice - current) / basePrice * MeanReversion;
            float noise = Random.Range(-DailyVolatility, DailyVolatility);
            float next = current * (1f + pull + noise);
            return Mathf.Clamp(next, basePrice * MinPriceFactor, basePrice * MaxPriceFactor);
        }

        private void OnDayChanged(string eventId, int day)
        {
            RollPrices();
            ExpireDeals(day);

            // THE EVENT'S DAY, not the clock's.
            //
            // These can disagree — OnDayChanged fires with the new day, and whether the clock's own
            // Day property has been updated by that point is an ordering detail this service must not
            // depend on. When it read the clock instead, the sequence was: expire yesterday's board
            // against the NEW day (correct), then refuse to build a new one because _dealsGeneratedForDay
            // still matched the OLD day the clock was reporting. Net effect: the offer board emptied on
            // the first midnight and never came back. Caught in test, not in play.
            EnsureDealsForToday(day);
        }

        private void RollPrices()
        {
            if (_inventory == null) return;

            foreach (var sku in _inventory.AllSkus)
            {
                if (sku == null) continue;

                float basePrice = Mathf.Max(1f, sku.BuyValue);
                float current = CurrentPrice(sku);
                int next = Mathf.Max(1, Mathf.RoundToInt(Step(current, basePrice)));

                _priceBySku[sku.SkuId] = next;

                if (!_historyBySku.TryGetValue(sku.SkuId, out var history))
                    _historyBySku[sku.SkuId] = history = new List<int>();

                history.Add(next);
                while (history.Count > HistoryDays) history.RemoveAt(0);
            }
        }

        // ── Spot deals ───────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the board if today hasn't got one yet.
        ///
        /// Called from Initialize as well as the day roll so a freshly-started game — or a save loaded
        /// mid-day — has offers immediately. Waiting for the next midnight would mean the feature is
        /// invisible for the whole first session, which is the session that has to sell it.
        /// </summary>
        private void EnsureDealsForToday() => EnsureDealsForToday(CurrentDay);

        private void EnsureDealsForToday(int today)
        {
            if (_dealsGeneratedForDay == today) return;
            if (_inventory == null) return;

            var eligible = _inventory.AllSkus.Where(s => s != null && s.Ti > 0 && s.Hi > 0).ToList();
            if (eligible.Count == 0) return;

            ExpireDeals(today);

            // Only top up to the target — a save loaded twice in one day shouldn't double the board.
            int live = _deals.Count(d => !d.Claimed && d.ExpiresAfterDay >= today);
            for (int i = live; i < SpotDealsPerDay; i++)
                _deals.Add(RollDeal(eligible, today));

            _dealsGeneratedForDay = today;
        }

        private SpotDeal RollDeal(List<SkuData> eligible, int today)
        {
            // Prefer a SKU that isn't already on the board. A board showing the same item three times
            // is one offer wearing three hats, and the whole point is that taking one costs you the
            // others.
            var onBoard = new HashSet<string>(
                _deals.Where(d => !d.Claimed && d.ExpiresAfterDay >= today).Select(d => d.SkuId));
            var pool = eligible.Where(s => !onBoard.Contains(s.SkuId)).ToList();
            if (pool.Count == 0) pool = eligible;

            var sku = pool[Random.Range(0, pool.Count)];
            int pallets = Random.Range(DealPalletsMin, DealPalletsMax + 1);
            float discount = Random.Range(DealDiscountMin, DealDiscountMax);
            int listPrice = CurrentPrice(sku);
            int dealPrice = Mathf.Max(1, Mathf.RoundToInt(listPrice * (1f - discount)));
            // A Spot Deal reads as an offer FROM somebody, same as every other card on this tab —
            // picked at random rather than tied to the SKU's cheapest/current seller, since the whole
            // point of a spot deal is that it's a one-off, not "this vendor's regular price."
            string vendorId = RandomVendorId();

            return new SpotDeal
            {
                Id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                SkuId = sku.SkuId,
                VendorId = vendorId,
                Pallets = pallets,
                CasesPerPallet = Mathf.Max(1, sku.Ti * sku.Hi),
                UnitPrice = dealPrice,
                ListPriceWhenOffered = listPrice,
                ExpiresAfterDay = today,      // gone at midnight — that's the pressure
                Claimed = false
            };
        }

        /// <summary>Picks one vendor at random to attribute a Spot Deal to. Also used by Import to
        /// backfill VendorId on deals from a save written before this field existed — null/empty
        /// rather than a missing vendor reads as more broken than just picking one.</summary>
        private static string RandomVendorId()
        {
            var vendors = VendorRegistry.Load()?.AllVendors;
            return vendors != null && vendors.Count > 0
                ? vendors[Random.Range(0, vendors.Count)].VendorId
                : null;
        }

        private void ExpireDeals(int today)
        {
            _deals.RemoveAll(d => d.Claimed || d.ExpiresAfterDay < today);
        }

        public SpotDeal FindDeal(string dealId) =>
            _deals.FirstOrDefault(d => d != null && d.Id == dealId);

        /// <summary>
        /// Marks an offer taken. Returns false if it's already gone — the caller must check, because
        /// the panel can be sitting open across a midnight roll with a stale card still on screen.
        /// </summary>
        public bool ClaimDeal(string dealId)
        {
            var deal = FindDeal(dealId);
            if (deal == null || deal.Claimed || deal.ExpiresAfterDay < CurrentDay) return false;
            deal.Claimed = true;
            return true;
        }

        // ── Persistence ──────────────────────────────────────────────────────

        public MarketSnapshot Export()
        {
            var snap = new MarketSnapshot { dealsGeneratedForDay = _dealsGeneratedForDay };

            foreach (var kv in _priceBySku)
            {
                snap.prices.Add(new SkuPriceSnapshot
                {
                    skuId = kv.Key,
                    price = kv.Value,
                    history = History(kv.Key).ToList()
                });
            }

            foreach (var d in _deals)
            {
                snap.deals.Add(new SpotDealSnapshot
                {
                    id = d.Id,
                    vendorId = d.VendorId,
                    skuId = d.SkuId,
                    pallets = d.Pallets,
                    casesPerPallet = d.CasesPerPallet,
                    unitPrice = d.UnitPrice,
                    listPriceWhenOffered = d.ListPriceWhenOffered,
                    expiresAfterDay = d.ExpiresAfterDay,
                    claimed = d.Claimed
                });
            }

            return snap;
        }

        /// <summary>
        /// Restores prices and offers. A save written before this system existed has an empty
        /// snapshot, which lands here as "no prices" — SeedPricesIfEmpty then fills them from the SKU
        /// assets on the next Initialize, so old saves come up with a fresh market rather than an
        /// empty catalogue. That's the migration; there's nothing to convert.
        /// </summary>
        public void Import(MarketSnapshot snap)
        {
            ClearAll();
            if (snap == null) return;

            foreach (var p in snap.prices)
            {
                if (string.IsNullOrEmpty(p.skuId)) continue;
                _priceBySku[p.skuId] = Mathf.Max(1, p.price);
                _historyBySku[p.skuId] = p.history != null ? new List<int>(p.history) : new List<int>();
            }

            foreach (var d in snap.deals)
            {
                if (string.IsNullOrEmpty(d.id)) continue;
                _deals.Add(new SpotDeal
                {
                    Id = d.id,
                    SkuId = d.skuId,
                    VendorId = !string.IsNullOrEmpty(d.vendorId) ? d.vendorId : RandomVendorId(),
                    Pallets = d.pallets,
                    CasesPerPallet = d.casesPerPallet,
                    UnitPrice = d.unitPrice,
                    ListPriceWhenOffered = d.listPriceWhenOffered,
                    ExpiresAfterDay = d.expiresAfterDay,
                    Claimed = d.claimed
                });
            }

            _dealsGeneratedForDay = snap.dealsGeneratedForDay;

            // A save can be loaded on a later day than it was written; top the board back up rather
            // than leaving the player staring at three expired cards.
            SeedPricesIfEmpty();
            EnsureDealsForToday();
        }
    }

    /// <summary>One discounted, expiring offer. Whole pallets only — a spot deal is a pallet of
    /// freight somebody needs to move, not a shopping list.</summary>
    public class SpotDeal
    {
        public string Id;
        public string SkuId;
        public string VendorId;
        public int Pallets;
        public int CasesPerPallet;

        /// <summary>Discounted price per case.</summary>
        public int UnitPrice;

        /// <summary>What the same case cost on the open market when this was offered — the reference
        /// the discount is measured against. Frozen at offer time on purpose: the market moves, and a
        /// "40% off" that quietly became "12% off" by the time the player looked would be a lie.</summary>
        public int ListPriceWhenOffered;

        public int ExpiresAfterDay;
        public bool Claimed;

        public int TotalCases => Pallets * CasesPerPallet;
        public int TotalCost => TotalCases * UnitPrice;
        public int SavingsVsList => TotalCases * (ListPriceWhenOffered - UnitPrice);

        public int DiscountPercent => ListPriceWhenOffered <= 0
            ? 0
            : Mathf.RoundToInt((ListPriceWhenOffered - UnitPrice) / (float)ListPriceWhenOffered * 100f);
    }

    [System.Serializable]
    public class MarketSnapshot
    {
        public int dealsGeneratedForDay = -1;
        public List<SkuPriceSnapshot> prices = new();
        public List<SpotDealSnapshot> deals = new();
    }

    [System.Serializable]
    public class SkuPriceSnapshot
    {
        public string skuId;
        public int price;
        public List<int> history = new();
    }

    [System.Serializable]
    public class SpotDealSnapshot
    {
        public string id;
        public string skuId;
        public string vendorId;
        public int pallets;
        public int casesPerPallet;
        public int unitPrice;
        public int listPriceWhenOffered;
        public int expiresAfterDay;
        public bool claimed;
    }
}
