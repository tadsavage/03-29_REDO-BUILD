using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Recurring = a standing account that sends work every day until cancelled — mixed case-pick
    /// orders. Bulk = a single order a customer places off the cuff, priced off cost of goods, in
    /// full-pallet quantities with whatever part case is left over — the board rolls 1–3 of these
    /// every day and they expire unaccepted.
    ///
    /// RETIRED: OneOffWholesale described the same real-world thing as Bulk (a one-off full-pallet
    /// drop) under a second name with fulfilment that never actually worked (it still case-picked
    /// one at a time despite the full-pallet shape). Merged into Bulk — ContractData.IsBulk is true
    /// for both ordinals, so an old asset or save still resolves correctly. Kept in the enum rather
    /// than deleted because it's persisted by ordinal (see NOTE below) — removing it would silently
    /// reinterpret any authored asset or save still carrying ordinal 1 as a different kind.
    ///
    /// NOTE: append new values at the END. Authored ContractData assets serialize this by ordinal.
    /// </summary>
    public enum ContractKind
    {
        Recurring,
        OneOffWholesale, // retired — treated identically to Bulk, see IsBulk
        Bulk
    }

    /// <summary>
    /// How often a contract sends work once it's been taken.
    ///
    /// Separate from <see cref="ContractKind"/> because the two answer different questions — kind is
    /// WHAT arrives (case picks, a trailer of pallets, a bulk drop), frequency is HOW OFTEN. A
    /// standing account is the only one where the player has a real choice between them today;
    /// OneTime exists so a bulk order and a wholesale drop can say "never again" in the same
    /// vocabulary rather than each being special-cased at the arrival tick.
    ///
    /// NOTE: append new values at the END — persisted by ordinal.
    /// </summary>
    public enum OrderFrequency
    {
        Daily,
        Weekly,
        OneTime
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
        [Tooltip("How often this contract sends work once taken. A standing account is Daily or " +
                 "Weekly; bulk is OneTime and never re-fires.")]
        [SerializeField] private OrderFrequency _frequency = OrderFrequency.Daily;

        [Header("Bulk (Bulk only)")]
        [Tooltip("How many SKUs the customer asks for. Each line is a whole number of pallets plus " +
                 "whatever part case is left over — 620 cases of a 60/pallet SKU is 10 pallets and " +
                 "20 loose cases, not 11 pallets.")]
        [SerializeField, Min(1)] private int _bulkLinesMin = 1;
        [SerializeField, Min(1)] private int _bulkLinesMax = 3;
        [SerializeField, Min(1)] private int _bulkPalletsPerLineMin = 1;
        [SerializeField, Min(1)] private int _bulkPalletsPerLineMax = 10;

        [Header("Legacy Wholesale field (retired OneOffWholesale kind only)")]
        [Tooltip("Unused by anything current — kept only so an authored OneOffWholesale asset (now " +
                 "treated as Bulk) still deserializes its old value without warnings.")]
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
        public OrderFrequency Frequency => _frequency;
        public int BulkLinesMin => _bulkLinesMin;
        public int BulkLinesMax => _bulkLinesMax;
        public int BulkPalletsPerLineMin => _bulkPalletsPerLineMin;
        public int BulkPalletsPerLineMax => _bulkPalletsPerLineMax;
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

        /// <summary>True for a bulk order — a customer's off-the-cuff drop, priced off cost of
        /// goods and fulfilled in FULL PALLETS by PalletPick tasks rather than case by case. True
        /// for both the current Bulk ordinal and the retired OneOffWholesale one, which described
        /// the same thing and is now folded into it (see ContractKind's doc comment).</summary>
        public bool IsBulk => _kind == ContractKind.Bulk || _kind == ContractKind.OneOffWholesale;

        /// <summary>True for a contract that delivers once and never fires again. Reads off Frequency
        /// rather than off Kind so the arrival tick has ONE question to ask — before this, "does it
        /// re-fire?" was answered by an IsWholesale special case that bulk would have had to
        /// duplicate.</summary>
        public bool IsOneTime => _frequency == OrderFrequency.OneTime;

        /// <summary>Stable identity for save data. Asset name, not company name — one customer can
        /// have several contracts, so the customer id doesn't identify a contract.</summary>
        public string ContractId => name;

        /// <summary>Display name for the offer card. Keyed on IsBulk, not _kind, so a retired
        /// OneOffWholesale asset reads as "Bulk Order" too rather than keeping its old label.</summary>
        public string Title => IsBulk ? "Bulk Order" : "Recurring Order";

        /// <summary>What the New Contracts board calls this type. Keyed on IsBulk for the same
        /// reason as Title.</summary>
        public string KindLabel => IsBulk ? "BULK ORDER" : "RECURRING ORDER";

        public string FrequencyLabel => _frequency switch
        {
            OrderFrequency.Weekly => "Weekly",
            OrderFrequency.OneTime => "One-Time",
            _ => "Daily"
        };

        /// <summary>
        /// Builds a ContractData in memory rather than from an authored asset.
        ///
        /// Exists because contract offers are meant to APPEAR over time — semi-randomly, at a rate set
        /// by the business's reputation and the difficulty — rather than being a fixed shelf of hand-
        /// authored assets. The reputation system will need exactly this; the Customers tab's DEV
        /// button is its first caller.
        ///
        /// PERSISTENCE: a runtime contract is not in the ContractRegistry, so it can't be recovered
        /// from an asset on load. OrderArrivalService now exports every generated offer field-by-field
        /// and rebuilds them through this method on Import — see GeneratedOfferSnapshot there. That
        /// matters far more than it did when only the Dev Console made these: the daily Bulk board is
        /// entirely runtime contracts, and losing them on load would lose the offers AND orphan any
        /// signed-but-unshipped bulk order whose ContractId no longer resolves.
        /// </summary>
        public static ContractData CreateRuntime(
            string contractId, CustomerData customer, string pitch, ContractKind kind,
            int palletCount,
            int ordersPerDayMin, int ordersPerDayMax,
            int lineItemsMin, int lineItemsMax,
            int casesPerLineMin, int casesPerLineMax,
            int cutoffHour, int leadTimeDays,
            float payRateMultiplier, float lateFeePercent,
            OrderFrequency frequency = OrderFrequency.Daily,
            int bulkLinesMin = 1, int bulkLinesMax = 3,
            int bulkPalletsPerLineMin = 1, int bulkPalletsPerLineMax = 10)
        {
            var c = CreateInstance<ContractData>();
            // ContractId reads straight off name, so this IS the identity, not a display nicety.
            c.name = contractId;
            c._customer = customer;
            c._pitch = pitch;
            c._kind = kind;
            c._palletCount = Mathf.Max(1, palletCount);
            c._ordersPerDayMin = Mathf.Max(1, ordersPerDayMin);
            c._ordersPerDayMax = Mathf.Max(c._ordersPerDayMin, ordersPerDayMax);
            c._lineItemsMin = Mathf.Max(1, lineItemsMin);
            c._lineItemsMax = Mathf.Max(c._lineItemsMin, lineItemsMax);
            c._casesPerLineMin = Mathf.Max(1, casesPerLineMin);
            c._casesPerLineMax = Mathf.Max(c._casesPerLineMin, casesPerLineMax);
            c._cutoffHour = Mathf.Clamp(cutoffHour, 0, 23);
            c._leadTimeDays = Mathf.Max(1, leadTimeDays);
            c._payRateMultiplier = Mathf.Max(0.1f, payRateMultiplier);
            c._lateFeePercent = Mathf.Clamp01(lateFeePercent);
            c._frequency = frequency;
            c._bulkLinesMin = Mathf.Max(1, bulkLinesMin);
            c._bulkLinesMax = Mathf.Max(c._bulkLinesMin, bulkLinesMax);
            c._bulkPalletsPerLineMin = Mathf.Max(1, bulkPalletsPerLineMin);
            c._bulkPalletsPerLineMax = Mathf.Max(c._bulkPalletsPerLineMin, bulkPalletsPerLineMax);
            return c;
        }

        /// <summary>Rough cases/day a recurring account commits you to, for comparing offers.
        /// Midpoint of every band multiplied out — an estimate, not a promise. Meaningless for
        /// wholesale and bulk, both of which are a single drop rather than a daily rate. Keyed on
        /// Kind rather than on IsOneTime because authored wholesale assets predate Frequency and
        /// still carry its Daily default.</summary>
        public int EstimatedCasesPerDay => _kind != ContractKind.Recurring
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
    /// Active means "this contract is TAKEN" and nothing more. A delivered one-off (bulk, or the
    /// retired wholesale kind folded into it) stays Active forever — what stops it re-firing is the
    /// IsBulk skip in OrderArrivalService's hour tick, not this flag. Clearing it would make a
    /// delivered one-off read as never-signed to IsSigned, putting the Sign button back on the card.
    /// </summary>
    [System.Serializable]
    public class SignedContract
    {
        public string ContractId;
        public int SignedOnDay;
        public int LastGeneratedDay = -1; // -1 = has never generated
        public bool Active = true;

        // ── Loss ─────────────────────────────────────────────────────────────
        //
        // A contract the player lost by never booking a door for its freight (see
        // OrderArrivalService's day-roll sweep). Distinct from a player CANCELLATION, which also
        // clears Active: a cancelled account can be re-signed the moment it reappears, a lost one
        // is barred for ContractLossCooldownDays.
        //
        // TWO fields rather than a single "lost on day N, -1 for never". LostOnDay alone can't tell
        // "never lost" from "lost on day 0" once it's been through a save — JsonUtility writes the
        // default 0 for a field the old save didn't have, and day 0 is a real day.

        /// <summary>True once this contract was lost to a missed pickup.</summary>
        public bool Lost;

        /// <summary>Day the loss happened. Only meaningful while Lost is true.</summary>
        public int LostOnDay;

        // ── Running performance, accumulated as orders finish ────────────────
        //
        // Accumulated onto the contract rather than recomputed from OrderService on demand, because
        // OrderService.OrderHistory is capped at MaxArchivedOrders (250) and trims oldest-first —
        // a long game would silently see "earned to date" start falling as early orders aged out.
        // These are monotonic and persisted, so they mean what they say for the life of the save.

        /// <summary>Orders from this contract that reached Shipped.</summary>
        public int OrdersDelivered;

        /// <summary>Orders from this contract that were fined for going overdue. Counted at fine
        /// time, so an order that goes late and then ships counts in BOTH this and OrdersDelivered —
        /// which is correct: it was delivered, and it was late.</summary>
        public int OrdersLate;

        /// <summary>Gross billed for this contract's shipped orders (what ShipOrder credited).</summary>
        public long RevenueEarned;

        /// <summary>Late fees charged against this contract's orders.</summary>
        public long LateFeesPaid;

        /// <summary>0–100. Docked a small flat amount (OrderArrivalService.PenalizeSatisfaction) each
        /// time a trailer for this contract lands away from the customer's own requested hour — during
        /// auto-book or a manual move alike (see DockScheduleService.MissedRequestedSlot). Starts at a
        /// perfect 100; nothing else currently reads this value except the Accounts tab's status line.</summary>
        public float SatisfactionPercent = 100f;

        /// <summary>Share of delivered orders that were never fined, 0–1. Returns 1 before anything
        /// has shipped — a brand-new account reads as perfect rather than as 0%, which would look
        /// like a failing customer the moment you signed it.</summary>
        public float OnTimeRate => OrdersDelivered <= 0
            ? 1f
            : Mathf.Clamp01((OrdersDelivered - OrdersLate) / (float)OrdersDelivered);
    }

    /// <summary>Save shape for a SignedContract. Mirrors OrderSnapshot's flat-fields style.
    /// New stat fields default to 0 in a save written before they existed, which is the correct
    /// starting value — history simply begins from that load.</summary>
    [System.Serializable]
    public class ContractSnapshot
    {
        public string contractId;
        public int signedOnDay;
        public int lastGeneratedDay;
        public bool active;
        public int ordersDelivered;
        public int ordersLate;
        public long revenueEarned;
        public long lateFeesPaid;
        /// <summary>-1 in a save written before satisfaction existed — Import treats negative as "not
        /// recorded" and defaults to a perfect 100, unlike ordersLate/lateFeesPaid a real 0 IS a
        /// legitimate value here (fully dissatisfied), so 0 can't double as the "unset" sentinel.</summary>
        public float satisfactionPercent = -1f;
        /// <summary>False in a save written before contract loss existed, which is correct — nothing
        /// in that save was ever lost.</summary>
        public bool lost;
        public int lostOnDay;
    }

    /// <summary>
    /// Save shape for a contract that was GENERATED at runtime rather than authored as an asset —
    /// today, every Bulk offer on the New Contracts board.
    ///
    /// Exists because ContractRegistry can only resolve authored assets. Without this the daily bulk
    /// board would empty itself on every load, and any bulk order already signed and part-picked
    /// would reload pointing at a ContractId that resolves to nothing — which
    /// OrderArrivalService.OnHourChanged handles by warning and skipping, i.e. the order would sit
    /// there uncredited forever.
    ///
    /// Deliberately flat and complete rather than a seed + regeneration: re-rolling the offers from a
    /// stored seed would hand the player a different board than the one they saved looking at.
    /// </summary>
    [System.Serializable]
    public class GeneratedOfferSnapshot
    {
        public string contractId;
        public string customerId;
        public string pitch;
        public int kind;
        public int frequency;
        public int palletCount;
        public int ordersPerDayMin;
        public int ordersPerDayMax;
        public int lineItemsMin;
        public int lineItemsMax;
        public int casesPerLineMin;
        public int casesPerLineMax;
        public int bulkLinesMin;
        public int bulkLinesMax;
        public int bulkPalletsPerLineMin;
        public int bulkPalletsPerLineMax;
        public int cutoffHour;
        public int leadTimeDays;
        public float payRateMultiplier;
        public float lateFeePercent;
        /// <summary>Day this offer was rolled onto the board. The daily roll clears offers older than
        /// today, so without this a restored offer would be immortal.</summary>
        public int createdOnDay;
    }
}
