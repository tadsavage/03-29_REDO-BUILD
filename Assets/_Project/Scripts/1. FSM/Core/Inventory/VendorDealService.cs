using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Events;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>One live timed discount at one vendor.</summary>
    public class VendorDeal
    {
        public string VendorId;
        public string SkuId;

        /// <summary>1-2. Kept small — a deal is a nudge to buy something specific right now, not a
        /// replacement for a real order.</summary>
        public int Pallets;

        public float DiscountPercent;
        public float TotalSeconds;
        public float RemainingSeconds;

        /// <summary>0 (expired) to 1 (just started). Drives the red fill bar directly.</summary>
        public float Fraction => TotalSeconds > 0f ? Mathf.Clamp01(RemainingSeconds / TotalSeconds) : 0f;
    }

    /// <summary>
    /// Real-time (not sim-day) per-vendor timed deals — a limited-time discount on one random
    /// catalogue item, popped up on the VENDORS tab as a draining red fill bar. Frequency, discount
    /// size and how long the bar stays up before it disappears all scale with the vendor's Partnership
    /// Level: a better-trusted house offers you better, more frequent, but shorter-lived deals.
    ///
    /// TICKED IN REAL, UNSCALED SECONDS — not simulation time, and deliberately immune to
    /// Time.timeScale. Deals are meant to feel like something happening RIGHT NOW while you're looking
    /// at the screen, not a game-day event like MarketService's spot deals or BrokerService's salvage
    /// offers (both day-granularity, rolled on OnDayChanged). Unscaled specifically because
    /// OrdersPauseGate zeroes Time.timeScale for as long as the Purchasing/Contracts panel is open —
    /// exactly when the player is looking at the VENDORS tab — and a deal that freezes the moment you
    /// open the panel to look at it would never be clickable. Ticked from TimeDriver.Update() alongside
    /// SimulationTimeService.Tick — that MonoBehaviour is already the project's one guaranteed
    /// per-frame hook, so this reuses it rather than adding a second scene object that could go
    /// unwired the way a fresh SerializeField reference always can.
    ///
    /// No persistence: a deal that's up when the player saves is simply gone on load, same as
    /// MarketService's SpotDeals are NOT (those persist) but matching BrokerService's posture of
    /// "ephemeral, rerolls are fine" — a discount is not a commitment the game owes the player across
    /// a session boundary.
    /// </summary>
    public class VendorDealService : IService
    {
        /// <summary>Base chance per real second that an idle vendor rolls a new deal, at Partnership
        /// Level -100. Scales up to (Base + Range) at Level +100.</summary>
        private const float BaseChancePerSecond = 0.002f;
        private const float ChanceRangePerSecond = 0.006f;

        private const float MinDiscountPercent = 10f;
        private const float MaxDiscountBonusPercent = 30f; // best partner adds up to +30 on top of Min

        private const float MinDurationSeconds = 30f;
        private const float MaxDurationSeconds = 120f;
        /// <summary>How much a maxed-out partnership shortens the roll — the better the deal, the less
        /// time you have to act on it, per design.</summary>
        private const float DurationShrinkAtMaxLevel = 60f;

        private const int MinPallets = 1;
        private const int MaxPallets = 2;

        private readonly Dictionary<string, VendorDeal> _dealByVendorId = new();
        private VendorEconomyService _vendorEconomy;
        private EventManager _eventManager;

        public void Initialize()
        {
            ServiceLocator.TryGet(out _vendorEconomy);
            _eventManager = EventManager.Instance;
        }

        public void Shutdown() => _dealByVendorId.Clear();

        public void ClearAll() => _dealByVendorId.Clear();

        public VendorDeal GetActiveDeal(string vendorId)
        {
            if (string.IsNullOrEmpty(vendorId)) return null;
            return _dealByVendorId.TryGetValue(vendorId, out var deal) ? deal : null;
        }

        /// <summary>Called every frame from TimeDriver.Update(). Counts every live deal down by
        /// deltaTime and, for every vendor with no live deal, rolls a chance to start one.</summary>
        public void Tick(float deltaTime)
        {
            if (_vendorEconomy == null || deltaTime <= 0f) return;

            var expired = new List<string>();
            foreach (var kv in _dealByVendorId)
            {
                kv.Value.RemainingSeconds -= deltaTime;
                if (kv.Value.RemainingSeconds <= 0f) expired.Add(kv.Key);
            }
            foreach (var vendorId in expired)
            {
                _dealByVendorId.Remove(vendorId);
                _eventManager?.Publish(GameEvents.Vendor.OnDealChanged, vendorId);
            }

            var registry = VendorRegistry.Load();
            if (registry == null) return;

            foreach (var vendor in registry.AllVendors)
            {
                if (vendor == null || _dealByVendorId.ContainsKey(vendor.VendorId)) continue;

                int level = _vendorEconomy.GetState(vendor.VendorId)?.PartnershipLevel ?? 0;
                float normalized = Mathf.InverseLerp(-100f, 100f, level);

                float chance = BaseChancePerSecond + ChanceRangePerSecond * normalized;
                if (Random.value > chance * deltaTime) continue;

                var deal = RollDeal(vendor, normalized);
                if (deal == null) continue;

                _dealByVendorId[vendor.VendorId] = deal;
                _eventManager?.Publish(GameEvents.Vendor.OnDealChanged, vendor.VendorId);
            }
        }

        private VendorDeal RollDeal(VendorData vendor, float normalizedLevel)
        {
            var catalogue = _vendorEconomy.GetAvailableCatalogue(vendor.VendorId);
            var entry = catalogue.Where(e => e?.Sku != null).ToList();
            if (entry.Count == 0) return null;

            var sku = entry[Random.Range(0, entry.Count)].Sku;

            float discount = MinDiscountPercent + MaxDiscountBonusPercent * normalizedLevel;
            // Better partner -> better discount (above) AND shorter window (below) — the two share
            // normalizedLevel as their one input, so they move together by construction.
            float duration = Mathf.Max(MinDurationSeconds, MaxDurationSeconds - DurationShrinkAtMaxLevel * normalizedLevel);

            return new VendorDeal
            {
                VendorId = vendor.VendorId,
                SkuId = sku.SkuId,
                Pallets = Random.Range(MinPallets, MaxPallets + 1),
                DiscountPercent = discount,
                TotalSeconds = duration,
                RemainingSeconds = duration
            };
        }

        /// <summary>"I'LL TAKE IT!" — removes the deal so it can't be double-claimed or keep counting
        /// down behind the modal.</summary>
        public void ClaimDeal(string vendorId)
        {
            if (_dealByVendorId.Remove(vendorId))
                _eventManager?.Publish(GameEvents.Vendor.OnDealChanged, vendorId);
        }

        /// <summary>"Nah — take me back.." — same removal, no reward. Split into its own method from
        /// ClaimDeal despite the identical body: they represent different player intents, and a future
        /// change to one (e.g. logging declined deals for tuning) shouldn't silently apply to the other.</summary>
        public void CancelDeal(string vendorId)
        {
            if (_dealByVendorId.Remove(vendorId))
                _eventManager?.Publish(GameEvents.Vendor.OnDealChanged, vendorId);
        }
    }
}
