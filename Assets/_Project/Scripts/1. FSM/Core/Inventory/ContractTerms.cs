using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// The commercial terms of ONE signed recurring account - the six numbers the player negotiates
    /// (Tad, 2026-10-11). Copied onto every order the account generates, the same way LateFeePercent
    /// always was, so an order is billed on the terms that were in force when it arrived.
    ///
    /// Plain fields (not properties) so JsonUtility can save it inside ContractSnapshot.
    /// </summary>
    [Serializable]
    public class ContractTerms
    {
        /// <summary>Goods are billed at cost x (1 + this). Cost = the SKU's BuyValue.</summary>
        public float MarkupPercent = 0.20f;

        /// <summary>Dollars per case the selectors pick.</summary>
        public float PickFeePerCase = 0.75f;

        /// <summary>Dollars per pallet that goes out - covers staging AND loading.</summary>
        public int PalletFee = 25;

        /// <summary>How many days ahead the customer must tell us what they want. Also how far ahead the
        /// player gets to SEE each order (orders are raised this many days before they ship).</summary>
        public int AdvanceNoticeDays = 2;

        /// <summary>Flat fee the customer owes when an order turns up with less notice than required.</summary>
        public int ShortNoticeFee = 150;

        /// <summary>Share of an order's revenue we pay when its trailer is late.</summary>
        public float LateFeePercent = 0.15f;

        public ContractTerms Clone() => (ContractTerms)MemberwiseClone();

        public override string ToString() =>
            $"markup {MarkupPercent:P0}, pick ${PickFeePerCase:0.00}/case, pallet ${PalletFee}, notice {AdvanceNoticeDays}d, " +
            $"short-notice ${ShortNoticeFee}, late {LateFeePercent:P0}";
    }

    public enum TermId { Markup, PickFee, PalletFee, Notice, ShortNoticeFee, LateFee }

    /// <summary>
    /// One negotiable row. Opening = the customer's own first offer (best for THEM), Limit = the most
    /// the player can possibly ask for (best for US), Standard = the market-rate baseline. All three
    /// sit on one axis so "how hard are we pushing" is simply (value - Opening) / (Limit - Opening),
    /// whichever direction is good for us (the late fee runs downhill - lower is better for the
    /// player - and the same formula still works).
    /// </summary>
    public class TermSpec
    {
        public TermId Id;
        public string Label;
        public string Hint;
        public float Opening, Standard, Limit, Step;
        public Func<ContractTerms, float> Get;
        public Action<ContractTerms, float> Set;
        public Func<float, string> Format;

        /// <summary>0 = at the customer's opening offer, 1 = at the player's maximum ask.</summary>
        public float Push(float value) => Mathf.Approximately(Limit, Opening) ? 0f : (value - Opening) / (Limit - Opening);

        public float Snap(float value)
        {
            float lo = Mathf.Min(Opening, Limit), hi = Mathf.Max(Opening, Limit);
            value = Mathf.Clamp(value, lo, hi);
            return Mathf.Round(value / Step) * Step;
        }
    }

    public static class TermCatalog
    {
        public static readonly IReadOnlyList<TermSpec> All = new List<TermSpec>
        {
            new TermSpec { Id = TermId.Markup, Label = "Goods markup", Hint = "Added on top of what the goods cost us.",
                Opening = 0.08f, Standard = 0.20f, Limit = 0.44f, Step = 0.01f,
                Get = t => t.MarkupPercent, Set = (t, v) => t.MarkupPercent = v, Format = v => $"{Mathf.RoundToInt(v * 100f)}%" },
            new TermSpec { Id = TermId.PickFee, Label = "Pick fee / case", Hint = "Paid per case our selectors pick.",
                Opening = 0.25f, Standard = 0.75f, Limit = 1.75f, Step = 0.05f,
                Get = t => t.PickFeePerCase, Set = (t, v) => t.PickFeePerCase = v, Format = v => $"${v:0.00}" },
            new TermSpec { Id = TermId.PalletFee, Label = "Pallet handling", Hint = "Per pallet out the door - staging and loading.",
                Opening = 10f, Standard = 25f, Limit = 55f, Step = 1f,
                Get = t => t.PalletFee, Set = (t, v) => t.PalletFee = Mathf.RoundToInt(v), Format = v => $"${Mathf.RoundToInt(v)}" },
            new TermSpec { Id = TermId.Notice, Label = "Advance notice", Hint = "Days ahead they must place each order.",
                Opening = 1f, Standard = 2f, Limit = 4f, Step = 1f,
                Get = t => t.AdvanceNoticeDays, Set = (t, v) => t.AdvanceNoticeDays = Mathf.RoundToInt(v),
                Format = v => Mathf.RoundToInt(v) == 1 ? "1 day" : $"{Mathf.RoundToInt(v)} days" },
            new TermSpec { Id = TermId.ShortNoticeFee, Label = "Short-notice fee", Hint = "They pay this when an order breaks the notice rule.",
                Opening = 25f, Standard = 150f, Limit = 400f, Step = 5f,
                Get = t => t.ShortNoticeFee, Set = (t, v) => t.ShortNoticeFee = Mathf.RoundToInt(v), Format = v => $"${Mathf.RoundToInt(v)}" },
            // Runs DOWNHILL: the customer opens at 20% and the player pushes it toward 5%.
            new TermSpec { Id = TermId.LateFee, Label = "Late fee", Hint = "Share of the order we forfeit if the trailer is late.",
                Opening = 0.20f, Standard = 0.15f, Limit = 0.05f, Step = 0.01f,
                Get = t => t.LateFeePercent, Set = (t, v) => t.LateFeePercent = v, Format = v => $"{Mathf.RoundToInt(v * 100f)}%" },
        };

        public static TermSpec Spec(TermId id) { foreach (var s in All) if (s.Id == id) return s; return null; }

        /// <summary>The customer's own opening offer - every term at its worst-for-us end.</summary>
        public static ContractTerms OpeningOffer()
        {
            var t = new ContractTerms();
            foreach (var s in All) s.Set(t, s.Snap(s.Opening));
            return t;
        }

        /// <summary>Market-rate terms - the "Standard" column, and what a plain sign without
        /// negotiating gets.</summary>
        public static ContractTerms StandardTerms()
        {
            var t = new ContractTerms();
            foreach (var s in All) s.Set(t, s.Snap(s.Standard));
            return t;
        }
    }
}
