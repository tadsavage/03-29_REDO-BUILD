using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Recurring = a standing account that sends work every day until cancelled — mixed case-pick
    /// orders. OneOffWholesale = a single large drop, signed once and delivered once: a trailer's
    /// worth of FULL PALLETS, never a loose case.
    /// </summary>
    public enum ContractKind
    {
        Recurring,
        OneOffWholesale
    }

    /// <summary>
    /// The commercial terms under which one customer sends work: how much, how often, how soon it's
    /// due, what it pays, what it costs you to be late.
    ///
    /// Deliberately a SEPARATE asset from CustomerData rather than fields bolted onto it. CustomerData
    /// is identity (who they are, what they sell, their icon) and all 26 assets already exist; terms
    /// are a commercial offer, and the same customer should be able to appear as a small starter
    /// account early and a punishing high-volume one later. Keeping them apart also means the existing
    /// roster needs no re-authoring.
    ///
    /// The point of contracts over a random order spawner: the player COMMITS first and the world
    /// responds — the same shape as the inbound side, where a purchase order the player creates
    /// produces a truck that shows up later. Demand the player chose is a decision they can be wrong
    /// about; demand that merely happens to them is noise.
    /// </summary>
    [CreateAssetMenu(fileName = "Contract_", menuName = "Warehouse/Contract")]
    public class ContractData : ScriptableObject
    {
        [Header("Who")]
        [SerializeField] private CustomerData _customer;
        [SerializeField, TextArea(2, 4)] private string _pitch;

        [Header("Kind")]
        [SerializeField] private ContractKind _kind = ContractKind.Recurring;

        [Header("Wholesale (OneOffWholesale only)")]
        [Tooltip("How many pallets the deal is worth. Each becomes ONE line item sized to exactly a " +
                 "full pallet of that SKU (Ti x Hi) — wholesale is full pallets only, never a part " +
                 "case. A trailer holds 12.")]
        [SerializeField, Min(1)] private int _palletCount = 12;

        [Header("Recurring volume — rolled fresh each arrival day")]
        [SerializeField, Min(1)] private int _ordersPerDayMin = 1;
        [SerializeField, Min(1)] private int _ordersPerDayMax = 3;
        [SerializeField, Min(1)] private int _lineItemsMin = 3;
        [SerializeField, Min(1)] private int _lineItemsMax = 5;
        [SerializeField, Min(1)] private int _casesPerLineMin = 5;
        [SerializeField, Min(1)] private int _casesPerLineMax = 10;

        [Header("Timing")]
        [Tooltip("Recurring orders land at this hour, dated for delivery LeadTimeDays later. A cutoff " +
                 "late in the working day is what creates the 'is the board clear before cutoff' " +
                 "rhythm — orders arriving at midnight give the player no deadline to feel. A " +
                 "wholesale deal ignores this and drops the moment it's signed.")]
        [SerializeField, Range(0, 23)] private int _cutoffHour = 17;
        [SerializeField, Min(1)] private int _leadTimeDays = 2;

        [Header("Money")]
        [Tooltip("Scales each line's SellValue. Above 1 = a premium customer worth the trouble; " +
                 "below 1 = high volume at thin margin. The knob that makes contracts differ.")]
        [SerializeField, Min(0.1f)] private float _payRateMultiplier = 1f;
        [Tooltip("Share of order revenue charged as a late fee. Supersedes E1's flat 25% constant, " +
                 "which exists only because there was nothing per-customer to read it from.")]
        [SerializeField, Range(0f, 1f)] private float _lateFeePercent = 0.25f;

        public CustomerData Customer => _customer;
        public string Pitch => _pitch;
        public ContractKind Kind => _kind;
        public int PalletCount => _palletCount;
        public int OrdersPerDayMin => _ordersPerDayMin;
        public int OrdersPerDayMax => _ordersPerDayMax;
        public int LineItemsMin => _lineItemsMin;
        public int LineItemsMax => _lineItemsMax;
        public int CasesPerLineMin => _casesPerLineMin;
        public int CasesPerLineMax => _casesPerLineMax;
        public int CutoffHour => _cutoffHour;
        public int LeadTimeDays => _leadTimeDays;
        public float PayRateMultiplier => _payRateMultiplier;
        public float LateFeePercent => _lateFeePercent;

        public bool IsWholesale => _kind == ContractKind.OneOffWholesale;

        /// <summary>Stable identity for save data. Asset name, not company name — one customer can
        /// have several contracts, so the customer id doesn't identify a contract.</summary>
        public string ContractId => name;

        /// <summary>Display name for the offer card.</summary>
        public string Title => _kind == ContractKind.OneOffWholesale
            ? $"Wholesale — {_palletCount} pallet{(_palletCount == 1 ? "" : "s")}"
            : "Standing Account";

        /// <summary>Rough cases/day a recurring account commits you to, for comparing offers.
        /// Midpoint of every band multiplied out — an estimate, not a promise. Meaningless for
        /// wholesale, which is a single drop rather than a daily rate.</summary>
        public int EstimatedCasesPerDay => _kind == ContractKind.OneOffWholesale
            ? 0
            : Mathf.RoundToInt(((_ordersPerDayMin + _ordersPerDayMax) / 2f)
                             * ((_lineItemsMin + _lineItemsMax) / 2f)
                             * ((_casesPerLineMin + _casesPerLineMax) / 2f));
    }

    /// <summary>
    /// A contract the player has actually signed, and the bookkeeping that keeps arrivals honest
    /// across a save/load.
    ///
    /// LastGeneratedDay is the whole reason this type exists rather than a bare list of ContractData:
    /// without persisting which day a contract last produced orders, a save/load either re-runs a
    /// day's arrivals (duplicate orders) or skips one (a silent gap in demand). Same class of bug as
    /// the phantom staged orders — durable bookkeeping paired with state that wasn't saved.
    ///
    /// A wholesale deal sets Active false the moment it delivers, which is what stops it re-firing:
    /// it is a one-off, and "already delivered" is exactly what needs to survive a reload.
    /// </summary>
    [System.Serializable]
    public class SignedContract
    {
        public string ContractId;
        public int SignedOnDay;
        public int LastGeneratedDay = -1; // -1 = has never generated
        public bool Active = true;
    }

    /// <summary>Save shape for a SignedContract. Mirrors OrderSnapshot's flat-fields style.</summary>
    [System.Serializable]
    public class ContractSnapshot
    {
        public string contractId;
        public int signedOnDay;
        public int lastGeneratedDay;
        public bool active;
    }
}
