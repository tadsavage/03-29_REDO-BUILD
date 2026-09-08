using System.Linq;
using UnityEngine;
using GameCore.Events;
using GameCore.Services;

namespace GameCore.Inventory
{
    /// <summary>What the trade thinks of you. Bands, not a raw number, are what the UI shows.</summary>
    public enum ReputationBand
    {
        Unknown,
        Known,
        Respected,
        Preferred,
        Untouchable
    }

    /// <summary>
    /// ONE reputation score, TWO consumers.
    ///
    /// This gates which VENDORS will deal with you (VendorRegistry.Unlocked) and is the same number
    /// intended to drive which CUSTOMERS offer you contracts (designed in the 2026-08-01 session, not
    /// yet wired on that side). Deliberately one score rather than separate inbound/outbound tracks:
    /// it's one number for the player to read, one thing to tune, and it makes both halves of the
    /// game feed the same spine. See PURCHASING_DESIGN.md.
    ///
    /// SLOW TO GAIN, FAST TO LOSE — the same asymmetry already proven on SignedContract's
    /// SatisfactionPercent (-5 a miss against +1 an on-time). A reputation you can rebuild in an
    /// afternoon isn't one the player protects.
    ///
    /// SCOPE, stated honestly: the design lists five inputs. FOUR are wired here because their events
    /// exist — orders shipped, orders fined for being late, orders cancelled, and (as of the Vendor
    /// Partnership rework) a vendor's Partnership Level moving. Driver wait time at the dock and
    /// contaminated product are NOT wired, because neither raises an event yet and authoring a
    /// handler for an event nobody sends is how this codebase grew its dead Overage / Shortage
    /// plumbing. They go in when the mechanic behind them does.
    /// </summary>
    public class ReputationService : IService
    {
        public const int MaxScore = 1000;

        /// <summary>Scalar applied to a Vendor Partnership Level swing before it reaches this global
        /// score. Partnership is per-vendor and is only ONE of several drivers feeding this single
        /// global number — damped so no single vendor relationship can swing it on its own the way a
        /// direct order outcome does.</summary>
        public const float PartnershipContributionWeight = 0.5f;

        // Gains
        public const int PerfectOrderGain = 5;   // on time, 100% fill
        public const int OnTimeShortGain = 2;    // on time, but scratched

        // Losses
        public const int LateOrderPenalty = 10;
        public const int BadFillPenalty = 8;     // shipped under FillFloor
        public const int CancelledOrderPenalty = 25;

        /// <summary>Fill rate below which a shipped order is actively held against you rather than
        /// merely earning less. 90% is a bad day in real distribution, not a catastrophe — which is
        /// why this is a smaller hit than being late.</summary>
        public const float FillFloor = 0.90f;

        private int _score;
        private EventManager _eventManager;

        public int Score => _score;

        public static event System.Action<int, int, string> OnReputationChanged; // (newScore, delta, reason)

        public ReputationBand Band => BandFor(_score);

        public static ReputationBand BandFor(int score) =>
              score >= 850 ? ReputationBand.Untouchable
            : score >= 600 ? ReputationBand.Preferred
            : score >= 300 ? ReputationBand.Respected
            : score >= 100 ? ReputationBand.Known
                           : ReputationBand.Unknown;

        public static string BandLabel(ReputationBand band) => band switch
        {
            ReputationBand.Untouchable => "Untouchable",
            ReputationBand.Preferred => "Preferred",
            ReputationBand.Respected => "Respected",
            ReputationBand.Known => "Known",
            _ => "Unknown"
        };

        /// <summary>Score at which the next band opens, or -1 at the top. Drives the "142 to Known"
        /// readout — a bare score with no target is a number, not a goal.</summary>
        public static int NextBandThreshold(int score) =>
              score < 100 ? 100
            : score < 300 ? 300
            : score < 600 ? 600
            : score < 850 ? 850
                          : -1;

        /// <summary>Red at score 0, yellow at the midpoint, green at MaxScore — a single source of
        /// truth for the read-at-a-glance color used by both the TopBar label and the Reputation
        /// dropdown, so the two never drift apart.</summary>
        public static Color ColorFor(int score)
        {
            float t = Mathf.Clamp01(score / (float)MaxScore);
            Color red = new Color(0.90f, 0.25f, 0.20f);
            Color yellow = new Color(0.95f, 0.80f, 0.20f);
            Color green = new Color(0.35f, 0.85f, 0.45f);
            return t < 0.5f ? Color.Lerp(red, yellow, t / 0.5f) : Color.Lerp(yellow, green, (t - 0.5f) / 0.5f);
        }

        public void Initialize()
        {
            _eventManager = EventManager.Instance;

            OrderService.OnOrderShipped -= HandleOrderShipped;
            OrderService.OnOrderShipped += HandleOrderShipped;
            OrderService.OnOrderFined -= HandleOrderFined;
            OrderService.OnOrderFined += HandleOrderFined;
            OrderService.OnOrderCancelled -= HandleOrderCancelled;
            OrderService.OnOrderCancelled += HandleOrderCancelled;

            VendorEconomyService.OnPartnershipLevelChanged -= HandleVendorPartnershipChanged;
            VendorEconomyService.OnPartnershipLevelChanged += HandleVendorPartnershipChanged;
        }

        public void Shutdown()
        {
            OrderService.OnOrderShipped -= HandleOrderShipped;
            OrderService.OnOrderFined -= HandleOrderFined;
            OrderService.OnOrderCancelled -= HandleOrderCancelled;
            VendorEconomyService.OnPartnershipLevelChanged -= HandleVendorPartnershipChanged;
        }

        public void ClearAll() => _score = 0;

        /// <summary>Applies a change and announces it. Clamped to 0..MaxScore — reputation has a
        /// floor because "worse than nobody will deal with you" isn't a distinction the game can
        /// express, and a runaway negative would make recovery impossible rather than hard.</summary>
        public void Add(int delta, string reason)
        {
            if (delta == 0) return;

            int before = _score;
            _score = Mathf.Clamp(_score + delta, 0, MaxScore);
            if (_score == before) return;

            var bandBefore = BandFor(before);
            var bandAfter = BandFor(_score);

            OnReputationChanged?.Invoke(_score, _score - before, reason);

            // Only crossing a band is worth interrupting the player for. A running commentary of
            // "+5 reputation" on every shipped order would be noise on a number that moves all day.
            if (bandAfter != bandBefore)
            {
                bool up = bandAfter > bandBefore;
                UIToast.Show(up
                    ? $"Reputation: you're now {BandLabel(bandAfter)} in this business. New suppliers " +
                      $"will take your call."
                    : $"Reputation has slipped to {BandLabel(bandAfter)}. Some suppliers have stopped " +
                      $"returning your calls.");
            }
        }

        private void HandleOrderShipped(OrderData order)
        {
            if (order == null) return;

            int needed = order.LineItems?.Sum(li => li.QuantityNeeded) ?? 0;
            int picked = order.TotalUnitsPicked;
            float fill = needed > 0 ? picked / (float)needed : 1f;

            // Lateness is NOT charged here. An order that missed its day was already fined, and
            // HandleOrderFined took the reputation hit at that moment — docking it again on the way
            // out of the door would charge the same failure twice.
            if (order.HasBeenFined)
            {
                if (fill < FillFloor)
                    Add(-BadFillPenalty, $"order {order.OrderId} shipped at {fill:P0} fill");
                return;
            }

            if (fill >= 1f) Add(PerfectOrderGain, $"order {order.OrderId} shipped complete and on time");
            else if (fill >= FillFloor) Add(OnTimeShortGain, $"order {order.OrderId} shipped on time, {fill:P0} fill");
            else Add(-BadFillPenalty, $"order {order.OrderId} shipped at {fill:P0} fill");
        }

        private void HandleOrderFined(OrderData order, int amount)
            => Add(-LateOrderPenalty, $"order {order?.OrderId} was late");

        private void HandleOrderCancelled(OrderData order)
            => Add(-CancelledOrderPenalty, $"order {order?.OrderId} was cancelled");

        /// <summary>Rolls a per-vendor Partnership swing into the global score, damped by
        /// PartnershipContributionWeight — reuses the existing Add() gain/loss/band-crossing pipeline
        /// untouched, so OnReputationChanged and the UIToast band-crossing announcement continue to
        /// work exactly as they do today for order-driven changes.</summary>
        private void HandleVendorPartnershipChanged(string vendorId, int delta, string reason)
            => Add(Mathf.RoundToInt(delta * PartnershipContributionWeight), $"vendor {vendorId} partnership: {reason}");

        // ── Persistence ──────────────────────────────────────────────────────

        public ReputationSnapshot Export() => new ReputationSnapshot { score = _score };

        /// <summary>Restores the score. A save written before reputation existed carries score 0,
        /// which is exactly right — that player has not yet earned anything.</summary>
        public void Import(ReputationSnapshot snap)
            => _score = snap != null ? Mathf.Clamp(snap.score, 0, MaxScore) : 0;
    }

    [System.Serializable]
    public class ReputationSnapshot
    {
        public int score;
    }
}
