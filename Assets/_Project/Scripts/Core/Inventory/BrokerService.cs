using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Economy;
using GameCore.Events;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>What a salvage pallet turns out to be once it's off the truck.</summary>
    public enum SalvageCondition
    {
        Ordinary,    // perfectly good stock
        ShortDated,  // real value, but only if you can move it fast
        Damaged,     // refused at the door — you paid for it, you don't get it
        Jackpot      // specialty product you cannot buy through any other channel
    }

    /// <summary>
    /// THE BROKER — salvage and close-out loads, bought sight-unseen.
    ///
    /// Phase 4 of the purchasing redesign (see PURCHASING_DESIGN.md). This is the Buccaneer slot: the
    /// rare vendor with the enormous margin and the real risk. Tad's original framing was contraband;
    /// it's salvage instead, because literal illegality needs inspections, seizure and fines — a whole
    /// second failure system — while an unmanifested distressed trailer delivers the same thrill using
    /// machinery the game already has.
    ///
    /// THE MECHANIC IS THE MANIFEST. The offer says "MIXED GROCERY — approx 18 plt — AS IS" and a flat
    /// price. You find out what you actually bought by breaking it down on the dock, which is the
    /// first time in this game the Receiver has had a job that could surprise anyone.
    ///
    /// CONTENTS ARE ROLLED AT OFFER TIME, not at reveal, and persisted with the offer. That ordering
    /// matters: the trailer genuinely contains something specific before the player decides, so
    /// reloading a save can't reroll a bad load into a good one. A gamble you can save-scum isn't one.
    ///
    /// The risk is OPERATIONAL, not legal — the load eats dock time and rack slots whether or not any
    /// of it was worth having.
    /// </summary>
    public class BrokerService : IService
    {
        /// <summary>The vendor asset that fronts these loads. Its ReputationRequired is the gate.</summary>
        public const string BrokerVendorId = "Vendor_Broker";

        /// <summary>Chance per day that the Broker has anything at all. Deliberately not every day —
        /// "appears irregularly" is the character, and an offer that's always there is a shop.</summary>
        private const float OfferChancePerDay = 0.35f;

        /// <summary>Only ever one on the board. Two competing salvage trailers turns a gut call into
        /// a comparison exercise, which is precisely the wrong texture for this vendor.</summary>
        private const int MaxLiveOffers = 1;

        private const int MinPallets = 8;
        private const int MaxPallets = 18;

        /// <summary>How long an offer sits before it's gone. Two days, not one — a salvage trailer is
        /// a bigger commitment than a spot deal and deserves a night to think about it.</summary>
        private const int OfferLifetimeDays = 2;

        // Contents mix. Must sum to 1.
        private const float ChanceOrdinary = 0.60f;
        private const float ChanceShortDated = 0.20f;
        private const float ChanceDamaged = 0.15f;
        // remainder is Jackpot (0.05)

        /// <summary>Shelf life stamped on a short-dated pallet. Short enough to be a genuine race,
        /// long enough that a well-run building can actually win it.</summary>
        private const int ShortDatedShelfLifeDays = 4;

        /// <summary>
        /// Flat price as a fraction of the FULL manifest value, damaged pallets included.
        ///
        /// Tuned against the mix: 15% of pallets are worthless, so the usable fraction averages ~0.74
        /// of manifest value. A price drawn uniformly from 0.40-0.80 therefore lands above usable
        /// value about 15% of the time — a real, uncommon, genuinely painful loss. Narrower than this
        /// and every load is a free win (measured: at 0.45-0.75 against *usable* value, 300 of 300
        /// loads were profitable); wider and the broker is a coin flip nobody sane would touch.
        /// </summary>
        private const float PriceFloorFactor = 0.40f;
        private const float PriceCeilFactor = 0.92f;

        private readonly List<SalvageOffer> _offers = new();
        private EventManager _eventManager;
        private InventoryService _inventory;
        private SimulationTimeService _clock;
        private ReputationService _reputation;

        public IReadOnlyList<SalvageOffer> Offers => _offers;

        public IEnumerable<SalvageOffer> LiveOffers =>
            _offers.Where(o => !o.Claimed && o.ExpiresAfterDay >= CurrentDay);

        private int CurrentDay => _clock?.Day ?? 1;

        public void Initialize()
        {
            _eventManager = EventManager.Instance;
            ServiceLocator.TryGet(out _inventory);
            ServiceLocator.TryGet(out _clock);
            ServiceLocator.TryGet(out _reputation);

            if (_eventManager != null)
                _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
        }

        public void Shutdown()
        {
            _eventManager?.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
            _offers.Clear();
        }

        public void ClearAll() => _offers.Clear();

        /// <summary>True once the player has earned the Broker's attention. Reads the vendor asset
        /// rather than a constant so the gate is tuned where every other vendor's gate is tuned.</summary>
        public bool BrokerUnlocked
        {
            get
            {
                var vendor = VendorRegistry.Load()?.GetById(BrokerVendorId);
                if (vendor == null) return false;
                return (_reputation?.Score ?? 0) >= vendor.ReputationRequired;
            }
        }

        private void OnDayChanged(string eventId, int day)
        {
            _offers.RemoveAll(o => o.Claimed || o.ExpiresAfterDay < day);

            if (!BrokerUnlocked) return;
            if (_offers.Count(o => !o.Claimed) >= MaxLiveOffers) return;
            if (Random.value > OfferChancePerDay) return;

            var offer = RollOffer(day);
            if (offer != null)
            {
                _offers.Add(offer);
                UIToast.Show($"The broker has a load: {offer.ManifestLine} — {offer.PriceLabel}, as is. " +
                             $"Purchasing, if you want it.");
            }
        }

        /// <summary>
        /// Builds a whole trailer of real, specific pallets — then hides them behind a vague manifest.
        /// </summary>
        private SalvageOffer RollOffer(int today)
        {
            if (_inventory == null) return null;

            var eligible = _inventory.AllSkus.Where(s => s != null && s.Ti > 0 && s.Hi > 0).ToList();
            if (eligible.Count == 0) return null;

            // Jackpot pool = whatever the player can't simply go and buy: the SKUs no unlocked vendor
            // carries. Falls back to the priciest items if the roster somehow covers everything, so a
            // jackpot is always meaningfully better than the rest of the load.
            var jackpotPool = BuildJackpotPool(eligible);

            int pallets = Random.Range(MinPallets, MaxPallets + 1);
            var offer = new SalvageOffer
            {
                Id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                ExpiresAfterDay = today + OfferLifetimeDays,
                DeclaredPallets = pallets
            };

            int marketValue = 0;
            for (int i = 0; i < pallets; i++)
            {
                var condition = RollCondition();
                var pool = condition == SalvageCondition.Jackpot && jackpotPool.Count > 0 ? jackpotPool : eligible;
                var sku = pool[Random.Range(0, pool.Count)];
                int cases = Mathf.Max(1, sku.Ti * sku.Hi);

                offer.Contents.Add(new SalvageItem
                {
                    SkuId = sku.SkuId,
                    Cases = cases,
                    Condition = condition
                });

                // PRICED ON THE FULL MANIFEST, JUNK INCLUDED. You are buying eighteen pallets at a
                // discount without knowing how many are rubbish, and paying for the rubbish is
                // exactly the risk you're taking.
                //
                // This originally excluded damaged pallets, on the reasoning that charging for
                // worthless product was unfair. Measured over 300 loads, that made 300 of them
                // profitable — the price could never exceed the usable value, so there was no way to
                // lose and therefore no gamble at all. Every card was a free win wearing a warning
                // label.
                marketValue += cases * Mathf.RoundToInt(sku.BuyValue);
            }

            // QUANTIZED to a whole number of dollars per case at roll time, so the price on the card is
            // exactly what CreatePlayerPurchaseOrder ends up billing. Line items only carry an integer
            // per-case cost, so an unquantized flat price would be quoted at $4,000 and charged at
            // $3,978 — small, but it's the kind of discrepancy that reads as the game miscounting.
            int asking = Mathf.Max(1, Mathf.RoundToInt(marketValue * Random.Range(PriceFloorFactor, PriceCeilFactor)));
            int totalCases = Mathf.Max(1, offer.Contents.Sum(c => c.Cases));
            offer.Price = Mathf.Max(1, asking / totalCases) * totalCases;
            return offer;
        }

        private List<SkuData> BuildJackpotPool(List<SkuData> eligible)
        {
            var registry = VendorRegistry.Load();
            int rep = _reputation?.Score ?? 0;

            if (registry != null)
            {
                var buyable = new HashSet<string>();
                foreach (var v in registry.Unlocked(rep))
                    foreach (var s in v.Catalogue)
                        if (s != null) buyable.Add(s.SkuId);

                var unreachable = eligible.Where(s => !buyable.Contains(s.SkuId)).ToList();
                if (unreachable.Count > 0) return unreachable;
            }

            // Everything is already reachable — fall back to the top of the catalogue by value.
            return eligible.OrderByDescending(s => s.BuyValue).Take(Mathf.Max(1, eligible.Count / 5)).ToList();
        }

        private static SalvageCondition RollCondition()
        {
            float r = Random.value;
            if (r < ChanceOrdinary) return SalvageCondition.Ordinary;
            if (r < ChanceOrdinary + ChanceShortDated) return SalvageCondition.ShortDated;
            if (r < ChanceOrdinary + ChanceShortDated + ChanceDamaged) return SalvageCondition.Damaged;
            return SalvageCondition.Jackpot;
        }

        public SalvageOffer FindOffer(string id) => _offers.FirstOrDefault(o => o != null && o.Id == id);

        /// <summary>Marks an offer taken. False if it's already gone — the panel can sit open across a
        /// midnight roll with a dead card still on screen.</summary>
        public bool ClaimOffer(string id)
        {
            var offer = FindOffer(id);
            if (offer == null || offer.Claimed || offer.ExpiresAfterDay < CurrentDay) return false;
            offer.Claimed = true;
            return true;
        }

        /// <summary>
        /// Turns an offer's hidden contents into real shipment line items.
        ///
        /// DAMAGED PALLETS ARE MARKED Dropped, so no pallet is ever built for them on the trailer and
        /// none reaches inventory — they're refused at the door. The line item stays on the manifest
        /// carrying its condition, which is what lets the PO list report "3 damaged, written off"
        /// instead of silently handing back a smaller trailer than the one that was sold.
        ///
        /// Short-dated pallets carry a real, short ShelfLifeDays, which flows through the existing
        /// receive path into a genuine expiration day and the spoilage system already watching for it.
        /// No new plumbing — that pipeline has been waiting for something to actually use it.
        /// </summary>
        public List<ShipmentLineItem> BuildLineItems(SalvageOffer offer)
        {
            var items = new List<ShipmentLineItem>();
            if (offer == null || _inventory == null) return items;

            // THE FLAT PRICE IS THE PRICE. ShipmentData.TotalCost is the sum of quantity x unit cost,
            // and that's what CreatePlayerPurchaseOrder bills — so the trailer's flat price has to be
            // spread back across its cases or the player would be charged the contents' market value
            // instead of what they agreed to. Spread over ALL cases including the damaged ones,
            // because you did buy the whole trailer, junk included.
            int totalCases = Mathf.Max(1, offer.Contents.Sum(c => c.Cases));
            int perCase = Mathf.Max(1, offer.Price / totalCases);

            // Pallets are placed onto real trailer floor slots, tier 0 only. Salvage is stacked by
            // whoever loaded it, not by our capacity planner, and one-per-floor-slot is the honest
            // shape for a trailer somebody else packed.
            int slot = 0;
            foreach (var entry in offer.Contents)
            {
                var sku = _inventory.GetSkuData(entry.SkuId);
                if (sku == null) continue;

                int shelfLife = entry.Condition == SalvageCondition.ShortDated
                    ? ShortDatedShelfLifeDays
                    : sku.ShelfLifeDays;

                items.Add(new ShipmentLineItem(entry.SkuId, entry.Cases, perCase, shelfLife)
                {
                    FloorSlotIndex = Mathf.Min(slot, TrailerCapacity.FloorSlots - 1),
                    PalletTier = 0,
                    Dropped = entry.Condition == SalvageCondition.Damaged,
                    Salvage = entry.Condition
                });
                slot++;
            }
            return items;
        }

        // ── Persistence ──────────────────────────────────────────────────────

        public BrokerSnapshot Export()
        {
            var snap = new BrokerSnapshot();
            foreach (var o in _offers)
            {
                var os = new SalvageOfferSnapshot
                {
                    id = o.Id,
                    price = o.Price,
                    declaredPallets = o.DeclaredPallets,
                    expiresAfterDay = o.ExpiresAfterDay,
                    claimed = o.Claimed
                };
                foreach (var c in o.Contents)
                    os.contents.Add(new SalvageItemSnapshot
                    {
                        skuId = c.SkuId,
                        cases = c.Cases,
                        condition = (int)c.Condition
                    });
                snap.offers.Add(os);
            }
            return snap;
        }

        public void Import(BrokerSnapshot snap)
        {
            _offers.Clear();
            if (snap == null) return;

            foreach (var os in snap.offers)
            {
                if (string.IsNullOrEmpty(os.id)) continue;
                var offer = new SalvageOffer
                {
                    Id = os.id,
                    Price = os.price,
                    DeclaredPallets = os.declaredPallets,
                    ExpiresAfterDay = os.expiresAfterDay,
                    Claimed = os.claimed
                };
                foreach (var cs in os.contents)
                    offer.Contents.Add(new SalvageItem
                    {
                        SkuId = cs.skuId,
                        Cases = cs.cases,
                        Condition = (SalvageCondition)cs.condition
                    });
                _offers.Add(offer);
            }
        }
    }

    /// <summary>One unmanifested trailer. <see cref="Contents"/> is the truth; everything the player
    /// sees before they buy is deliberately vaguer than it.</summary>
    public class SalvageOffer
    {
        public string Id;
        public int Price;

        /// <summary>What the manifest CLAIMS. Equal to the real count today — the lie in a salvage
        /// manifest is about what's in the boxes, not how many there are.</summary>
        public int DeclaredPallets;

        public int ExpiresAfterDay;
        public bool Claimed;
        public List<SalvageItem> Contents = new();

        public string ManifestLine => $"MIXED GROCERY · approx {DeclaredPallets} plt · AS IS";
        public string PriceLabel => $"${Price:N0}";

        public int DamagedCount => Contents.Count(c => c.Condition == SalvageCondition.Damaged);
        public int ShortDatedCount => Contents.Count(c => c.Condition == SalvageCondition.ShortDated);
        public int JackpotCount => Contents.Count(c => c.Condition == SalvageCondition.Jackpot);
    }

    public class SalvageItem
    {
        public string SkuId;
        public int Cases;
        public SalvageCondition Condition;
    }

    [System.Serializable]
    public class BrokerSnapshot
    {
        public List<SalvageOfferSnapshot> offers = new();
    }

    [System.Serializable]
    public class SalvageOfferSnapshot
    {
        public string id;
        public int price;
        public int declaredPallets;
        public int expiresAfterDay;
        public bool claimed;
        public List<SalvageItemSnapshot> contents = new();
    }

    [System.Serializable]
    public class SalvageItemSnapshot
    {
        public string skuId;
        public int cases;
        public int condition;
    }
}
