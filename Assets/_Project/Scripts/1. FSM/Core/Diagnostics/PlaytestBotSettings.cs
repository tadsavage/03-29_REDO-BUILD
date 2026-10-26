using UnityEngine;

namespace GameCore.Diagnostics
{
    /// <summary>
    /// Tweakable knobs for the playtest bot. A plain ScriptableObject so the values persist between Play sessions and
    /// can be edited in the Inspector (Tools > Playtest Bot > Select Settings). NOT an AI: the bot is a fixed rule set
    /// that does what a sensible player does so the simulation keeps running while we hunt bugs.
    /// </summary>
    [CreateAssetMenu(fileName = "PlaytestBotSettings", menuName = "Warehouse/Playtest Bot Settings")]
    public class PlaytestBotSettings : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("Game speed the bot runs at (Time.timeScale).")] public float gameSpeed = 3f;
        [Tooltip("In-game days per scored block.")] public int daysPerBlock = 7;
        [Tooltip("Pause at the end of each block so fixes can be made. Off = roll straight into the next block.")] public bool pauseAtBlockEnd = true;
        [Tooltip("Write a quicksave slot at the end of every block (bot_block_N).")] public bool saveAtBlockEnd = true;
        [Tooltip("How often (in-game minutes) the bot makes decisions.")] public int decisionIntervalMinutes = 30;

        [Header("Starting a company (HAL builds from nothing)")]
        [Tooltip("Which file in Resources/HalLayouts the building is copied from.")] public string layoutName = "Standard Layout";
        public int startDockStockers = 1;
        public int startReachTruckOps = 1;
        public int startReceivers = 1;
        public int startSelectors = 1;
        [Tooltip("The upper rack levels are only built while cash stays above this.")] public int buildReserveCash = 15000;

        [Tooltip("HAL only builds the warehouse and hires the crew: no bot, no records (no scoreboard, no builds.csv, no ledger). For making a new layout.")] public bool buildOnly = false;

        [Header("Customers")]
        [Tooltip("Recurring accounts the bot keeps signed. Orders/day scales with this.")] public int maxRecurringAccounts = 3;
        [Tooltip("Bulk offers the bot accepts per in-game day.")] public int bulkOffersPerDay = 2;
        [Tooltip("Only take a bulk offer when cash is at least this multiple of the minimum balance.")] public float bulkCashMultiple = 2f;
        [Tooltip("Do not accept a bulk order until this many pallets are on hand or on order: HAL must be able to ship what it promises.")] public int minPalletsBeforeBulk = 12;

        [Header("Targets (used for the score)")]
        [Tooltip("Average pallets shipped per day the block is scored against.")] public float targetPalletsPerDay = 12f;
        [Tooltip("Smallest bank balance the bot is happy with. Purchases never take cash below this.")] public int minCashBalance = 15000;

        [Header("Purchasing")]
        [Tooltip("The MOST pallets of one item HAL will hold (on shelf + inbound). How many it actually wants comes from recent demand.")] public int palletsPerActiveSku = 2;
        [Tooltip("HAL releases an order to the floor only this many game hours before its trailer's booked block starts. Releasing earlier fills the staging lanes with pallets that then wait hours for a trailer (block 5).")] public int releaseLeadHours = 3;
        [Tooltip("An outbound trailer is booked only once its product is in the building or on an inbound PO that already has a slot, and never before the work of preparing it can be done (below). Stops trailers sitting at doors with nothing to ship (block 9).")] public int outboundLeadHours = 0;
        [Tooltip("Game hours to stage and load an order made only of whole pallets once the stock is here (straight roll-on / roll-off).")] public float palletTurnHours = 1.5f;
        [Tooltip("Game hours one selector picklist (a trip, up to 2 pallets) takes. Long on purpose in the current build; shorten once the loop is stable.")] public float pickHoursPerPicklist = 2.25f;
        [Tooltip("Extra game hours after picking to stage and load a case-pick order.")] public float loadBufferHours = 1f;
        [Tooltip("Game hours after an inbound PO's slot ends before its product counts as ready to ship (offload, receive, putaway).")] public int inboundBufferHours = 3;
        [Tooltip("Days of recent demand HAL tries to keep on hand for an item that customers are ordering.")] public int stockCoverDays = 2;
        [Tooltip("HAL stops restocking once stock on hand plus on order is worth this much at cost (orders already promised are still bought).")] public int inventoryValueCap = 60000;
        [Tooltip("When cash is plentiful (> minimum x this), keep this many pallets per active sku instead.")] public int bulkUpPalletsPerSku = 6;
        [Tooltip("Cash multiple of the minimum balance that triggers bulking up.")] public float bulkUpCashMultiple = 4f;
        [Tooltip("Max purchase orders in transit at once.")] public int maxPendingPOs = 3;
        [Tooltip("Cover a SKU that orders are owed and nothing is coming with a double-cost rush PO.")] public bool coverRushOrders = true;
        [Tooltip("Max share of spendable cash (above the minimum balance) the bot commits to purchases per in-game day.")] public float maxDailySpendFraction = 0.3f;
        [Tooltip("Max rush POs per in-game day.")] public int maxRushPerDay = 1;
        [Tooltip("Cash the bot may inject if it would otherwise go bankrupt (0 = never; the economy stays honest).")] public int bailoutCash = 0;

        [Header("Bug monitor")]
        [Tooltip("How often (in-game minutes) the integrity check runs.")] public int healthCheckIntervalMinutes = 60;
        [Tooltip("Game seconds a vehicle with a driver may sit still while work waits before it is reported.")] public float stationarySeconds = 240f;
        [Tooltip("Where reports are written, relative to the project root.")] public string reportFolder = "Assets/_Project/Docs/Playtest";

        [Header("Score weights - whole points, meant to total 100 (edited on the Balance tab)")]
        public int wOnTime = 15;
        public int wFill = 10;
        public int wThroughput = 15;
        public int wRevenue = 15;
        public int wProfit = 10;
        public int wCash = 5;
        public int wReliability = 25;
        public int wReputation = 5;

        [Header("Score targets (100% of a component = hitting its target)")]
        public float targetRevenuePerDay = 8000f;
        public float targetProfitPerDay = 1500f;
        public float reputationGainPerBlock = 30f;
        [Tooltip("Higher = bugs hurt the reliability score less.")] public float reliabilityTolerance = 12f;

        [Header("Growth rules (the planner makes ONE change per block)")]
        public bool plannerEnabled = true;
        [Tooltip("Also allow removing people / doors that are not needed.")] public bool plannerMayRemove = true;
        [Tooltip("Hire when a role is at least this busy AND its backlog is at least the number below.")] public int addUtilPercent = 85;
        public int addBacklog = 3;
        [Tooltip("Let someone go when a role is under this busy for two blocks in a row.")] public int removeUtilPercent = 25;
        [Tooltip("Build another dock door when this many trucks waited on average.")] public float addWaitingTrucks = 1.5f;

        /// <summary>Where reports go. Never null or empty even if the settings asset lost the field (it did once, mid-run, and the block report failed to save).</summary>
        public string ReportDir => string.IsNullOrWhiteSpace(reportFolder) ? "Assets/_Project/Docs/Playtest" : reportFolder;

        /// <summary>Absolute report folder: project-relative in the Editor, under the persistent data path in a build (the install folder may be read-only).</summary>
        public string ResolveReportDir()
        {
#if UNITY_EDITOR
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", ReportDir));
#else
            return System.IO.Path.Combine(Application.persistentDataPath, "Playtest");
#endif
        }

        public int TotalWeight => wOnTime + wFill + wThroughput + wRevenue + wProfit + wCash + wReliability + wReputation;

        public void ResetWeightsToBaseline()
        {
            wOnTime = 15; wFill = 10; wThroughput = 15; wRevenue = 15; wProfit = 10; wCash = 5; wReliability = 25; wReputation = 5;
        }
    }
}
