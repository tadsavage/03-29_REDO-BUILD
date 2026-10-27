using GameCore.Services;
using GameCore.Events;
using GameCore.Economy;
using GameCore.Labor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Manages the lifecycle of customer orders: arrival, assignment to employees, and fulfillment.
    /// Works with InventoryService to identify stock locations and allocate units.
    /// </summary>
    public class OrderService : IService
    {
        private readonly List<OrderData> _activeOrders = new();
        private readonly Dictionary<string, List<PickingTask>> _assignedTasks = new();
        
        private InventoryService _inventoryService;
        private SimulationTimeService _timeService;
        private EventManager _eventManager;
        private WorkQueueSystem _workQueue;
        private MoneyService _moneyService;

        // Events
        public static event System.Action<OrderData> OnOrderArrived;
        public static event System.Action<OrderData> OnOrderFulfilled;
        public static event System.Action<OrderData> OnOrderShipped;

        /// <summary>Raised by <see cref="CancelOrders"/> when the player calls an order off.</summary>
        public static event System.Action<OrderData> OnOrderCancelled;

        /// <summary>Raised once per order the day it first goes overdue, with the dollar amount
        /// charged. Exists so the contract that produced the order can count it against that
        /// account's on-time record without OrderService having to know contracts exist.</summary>
        public static event System.Action<OrderData, int> OnOrderFined;

        /// <summary>Orders that still need something from the player or the workforce. A finished
        /// order leaves this list the moment it goes terminal — see Archive.</summary>
        public IReadOnlyList<OrderData> ActiveOrders => _activeOrders;

        /// <summary>Finished orders (Shipped or Cancelled), most recent last. Kept for reporting;
        /// nothing operational should read this.</summary>
        public IReadOnlyList<OrderData> OrderHistory => _orderHistory;

        private readonly List<OrderData> _orderHistory = new();

        /// <summary>
        /// Hard cap on retained finished orders — oldest dropped first once exceeded.
        ///
        /// Bounded by COUNT rather than by age because OrderData records no closed-on day, and adding
        /// one would mean changing OrderSnapshot's schema. Count is the honest thing to bound by here:
        /// the reason this cap exists is save size and scan cost, both of which track the number of
        /// records, not their age.
        /// </summary>
        private const int MaxArchivedOrders = 250;

        /// <summary>Priority a PalletPick task is filed at — well above WorkTask.DefaultPriority (100)
        /// so a Reach Truck Operator clears every outbound pallet pick before touching a Putaway or
        /// Replenish task once the pick is Available. Below ReplenishmentService.ReplenishPriority
        /// (250)? No — deliberately ABOVE it: an empty pick face blocks a customer's trailer, which
        /// outranks keeping a pick face topped up for a picker who isn't there yet.</summary>
        private const int PalletPickPriority = 300;   // 2026-10-20: Putaway 100 / Replenish 200 / PalletPick 300 (player adjusts in the Work Queue)


        public void Initialize()
        {
            _inventoryService = ServiceLocator.Get<InventoryService>();
            _timeService = ServiceLocator.Get<SimulationTimeService>();
            _eventManager = EventManager.Instance;
            _workQueue = ServiceLocator.Get<WorkQueueSystem>();
            _moneyService = ServiceLocator.Get<MoneyService>();

            if (_eventManager != null)
            {
                _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
                _eventManager.Subscribe<GameCore.Events.Payloads.SimulationTimeData>(GameEvents.Time.OnMinutePassed, OnMinuteForLanes);
            }
        }

        public void Shutdown()
        {
            if (_eventManager != null)
            {
                _eventManager.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);
                _eventManager.Unsubscribe<GameCore.Events.Payloads.SimulationTimeData>(GameEvents.Time.OnMinutePassed, OnMinuteForLanes);
            }
            _activeOrders.Clear();
            _assignedTasks.Clear();
        }

        /// <summary>Add a new order to the system, mint its player-facing OrderNumber, and file its
        /// single placeholder WorkTask in the Open status. Per Tad: an accepted order gets its number
        /// immediately, not only once the player commits it to a staging lane — nothing broken out or
        /// reserved yet, but the order is "real" with a number of its own the moment it's accepted.
        /// The order sits in the Work Queue as one line (rendered as e.g. "GroSel" by WorkQueuePanel)
        /// until the player actually releases it. See ReleaseOrdersToLane for what happens then.</summary>
        public void ReceiveOrder(OrderData order)
        {
            if (string.IsNullOrEmpty(order.OrderNumber)) order.OrderNumber = GenerateOrderNumber(order);
            _activeOrders.Add(order);
            OnOrderArrived?.Invoke(order);
            Debug.Log($"[OrderService] New order received: {order.OrderNumber} ({order.OrderId}) from {order.CustomerName} ({order.TotalUnits} units) — awaiting release to a staging lane.");
            FilePlaceholderTask(order);
        }

        /// <summary>
        /// Files the single task that represents a just-arrived, not-yet-released order in the Work
        /// Queue. It carries no SKU, no pallet, and reserves nothing — it exists only so the order has
        /// a row and a phase (Open) before the player decides where it's going. ReleaseOrdersToLane
        /// cancels this the moment the order is actually released, replacing it with the real,
        /// individually-trackable tasks (see FileReleasedOrderTasks).
        /// </summary>
        private void FilePlaceholderTask(OrderData order)
        {
            var task = _workQueue?.CreateTask(
                WorkTaskType.OrderSelect,
                EmployeeRole.OrderSelector,
                palletId: null,
                description: $"Awaiting release to a staging lane — {order.CustomerName}",
                area: DominantArea(order),
                orderId: order.OrderId);
            if (task != null) task.Status = WorkTaskStatus.Open;
        }

        /// <summary>Letter prefix for OrderData.OrderNumber, keyed by AreaCategory. Only Grocery is
        /// reachable today — Perishable/Frozen inventory doesn't exist in the game yet — but the order
        /// number format is written to support all three from day one so turning those areas on later
        /// needs no numbering-scheme change.</summary>
        private static readonly Dictionary<PalletData.AreaCategory, char> OrderNumberPrefixByArea = new()
        {
            { PalletData.AreaCategory.Grocery, 'G' },
            { PalletData.AreaCategory.Perishable, 'P' },
            { PalletData.AreaCategory.Frozen, 'F' }
        };

        /// <summary>Mints this order's player-facing OrderNumber — house format "O{Area}{DueDay}{Seq}",
        /// e.g. "OG5001" for a Grocery order due to ship day 5, the 1st outbound order ever. Called
        /// from ReceiveOrder, at acceptance — an order is "real" with its own number the moment it's
        /// accepted, not only once the player commits it to a staging lane. The day digit is DueDay
        /// (when it's due to ship), not CreatedDayNumber (when it arrived) — those can differ, e.g. an
        /// order accepted today with a lead time lands its number on the later ship day. The sequence
        /// is OrderNumberGenerator's own persisted lifetime counter, not recomputed from existing
        /// orders — see its doc comment for why it deliberately never resets per-day.</summary>
        private string GenerateOrderNumber(OrderData order)
            => OrderNumberGenerator.GetNext('O', DominantOrderNumberPrefix(order), order.DueDay);

        /// <summary>The area most of this order's line items belong to, by case count — not just a
        /// distinct count of areas — so one Perishable line riding along on an otherwise all-Grocery
        /// order doesn't flip the whole order's prefix/area. Falls back to Grocery for a line whose
        /// SKU no longer resolves (deleted since the order was raised) or an order with no line items
        /// yet (the placeholder task filed at arrival, before line-item area even matters).</summary>
        private PalletData.AreaCategory DominantArea(OrderData order)
        {
            if (_inventoryService == null || order.LineItems.Count == 0) return PalletData.AreaCategory.Grocery;

            return order.LineItems
                .GroupBy(li => _inventoryService.GetSkuData(li.SkuId)?.StorageArea ?? PalletData.AreaCategory.Grocery)
                .OrderByDescending(g => g.Sum(li => li.QuantityNeeded))
                .First().Key;
        }

        /// <summary>Letter prefix matching DominantArea, for OrderNumber.</summary>
        private char DominantOrderNumberPrefix(OrderData order)
            => OrderNumberPrefixByArea.TryGetValue(DominantArea(order), out char c) ? c : 'G';


        /// <summary>
        /// Breaks a just-released order into the tasks a Reach Truck / Order Selector can actually
        /// claim: one PalletPick per whole pallet, plus a single OrderSelect covering every loose case
        /// left over across the whole order. Called once, from ReleaseOrdersToLane, at the moment the
        /// player commits the order to a door/lane — everything here is filed straight as Available,
        /// there's no Open step the way there was for the order's placeholder task.
        /// </summary>
        /// <remarks>
        /// 620 cases of a 60-per-pallet SKU is ten Reach Truck trips and one selector walking off
        /// twenty cases — not eleven pallets, and not 620 cases picked by hand.
        ///
        /// ONE OrderSelect for the whole order rather than one per line, matching the non-bulk rule:
        /// a selector works an order continuously, and two selectors on one order would build pallets
        /// against each other.
        ///
        /// A SKU with no committed Ti/Hi can't express a full pallet, so its whole line falls through
        /// to the case picker. That's the honest failure — it keeps the order fillable instead of
        /// filing pallet picks that could never resolve a source pallet.
        ///
        /// Each PalletPick's source reserve pallet is found and locked (Reserve()) RIGHT NOW, at
        /// release — not deferred to whichever Reach Truck eventually claims the task. This is the
        /// order's product actually going on hold: from this point the same pallet can't be handed to
        /// a second order or picked up by replenishment. If a line has run out of stock entirely, the
        /// PalletPick is still filed (unassigned) so the order isn't silently short a task — a Reach
        /// Truck resolves it the old way, at claim time, once stock exists again.
        /// </remarks>
        private void FileReleasedOrderTasks(OrderData order, int doorNumber, string lane)
        {
            bool hasLane = !string.IsNullOrEmpty(lane);
            string toLocation = hasLane ? $"{doorNumber}{lane}" : null; // null = released with no lane yet (stays Open)
            int palletTasks = 0;
            int unreservedPallets = 0;
            int looseCases = 0;
            var looseBySku = new List<KeyValuePair<string, int>>();   // the case-picked share of each line, for the pallet plan

            void AddLoose(string skuId, int qty)
            {
                if (qty <= 0) return;
                int at = looseBySku.FindIndex(k => k.Key == skuId);
                if (at >= 0) looseBySku[at] = new KeyValuePair<string, int>(skuId, looseBySku[at].Value + qty);
                else looseBySku.Add(new KeyValuePair<string, int>(skuId, qty));
            }

            foreach (var li in order.LineItems)
            {
                int fullPallet = FullPalletCases(li.SkuId);
                if (fullPallet <= 0)
                {
                    Debug.LogWarning($"[OrderService] SKU {li.SkuId} on order {order.OrderNumber} has no " +
                                     $"committed Ti/Hi — its {li.QuantityNeeded} case(s) fall back to a case pick.");
                    looseCases += li.QuantityNeeded;
                    AddLoose(li.SkuId, li.QuantityNeeded);
                    continue;
                }

                int pallets = li.QuantityNeeded / fullPallet;
                looseCases += li.QuantityNeeded % fullPallet;
                AddLoose(li.SkuId, li.QuantityNeeded % fullPallet);

                var area = _inventoryService?.GetSkuData(li.SkuId)?.StorageArea ?? PalletData.AreaCategory.Grocery;
                for (int i = 0; i < pallets; i++)
                {
                    string sourcePalletId = null;
                    string sourceAddress = null;
                    if (ReplenishmentService.TryFindStagedPalletAwaitingPutaway(_workQueue, _inventoryService, li.SkuId,
                        out string stagedPalletId, out string stagedFromLocation))
                    {
                        // Claim it straight out of the lane instead of letting it go through putaway
                        // and then a replenish trip back out — cancelling its Putaway task IS the
                        // reservation here: nothing else can grab it for putaway once it's gone, and
                        // this PalletPick's own PalletId (set below) makes ReachTruckOperator's
                        // claim-time gate skip re-checking a source for it entirely.
                        _workQueue?.CancelTasksForPallet(stagedPalletId);
                        sourcePalletId = stagedPalletId;
                        sourceAddress = stagedFromLocation;
                    }
                    else if (ReplenishmentService.TryFindOldestPalletAnywhere(_inventoryService, li.SkuId, out var reserve))
                    {
                        reserve.Reserve(); // locks it on hold for this order — see remarks above.
                        sourcePalletId = reserve.PalletId;
                        sourceAddress = reserve.Address;
                    }
                    else
                    {
                        unreservedPallets++;
                        Debug.LogWarning($"[OrderService] Order {order.OrderNumber}: no reserve or pick-face " +
                                         $"pallet of {li.SkuId} left to fill a pallet pick — filed unassigned; a " +
                                         $"Reach Truck resolves a source once one claims it.");
                    }

                    // No pallet anywhere to fill it: this pallet pick is a SCRATCH run (Tad, 2026-10-20). It is filed
                    // Open - nobody can claim it - and only becomes Available once stock of that SKU exists (PromoteScratches).
                    bool isScratchPick = sourcePalletId == null;
                    var pt = _workQueue?.CreateTask(
                        WorkTaskType.PalletPick,
                        EmployeeRole.ReachTruckOperator,
                        palletId: sourcePalletId,
                        description: $"{(isScratchPick ? WorkTask.ScratchPrefix + ": " : "")}Pallet pick — {fullPallet} cs of {li.SkuId} for {order.CustomerName}",
                        fromLocation: sourceAddress,
                        toLocation: toLocation,
                        area: area,
                        priority: PalletPickPriority,
                        orderId: order.OrderId,
                        skuId: li.SkuId);
                    if (pt != null) pt.Status = hasLane && !isScratchPick ? WorkTaskStatus.Available : WorkTaskStatus.Open;   // no lane yet = HELD (Open): unclaimable, and AssignWaitingLanes / the Work Queue can release it
                    palletTasks++;
                }
            }

            if (looseCases > 0)
            {
                // PALLET PLAN (Tad, 2026-10-26): the loose cases are divided into exact pallets NOW, by the same cube rule the
                // selector's pallet uses, and each pallet is its own task. The number of pallets - and what is on each - is
                // known from release, the Work Queue and the truck card show it 1:1, and a pallet is done when its planned
                // cases are on it (whatever the visual stacking looks like).
                var plan = PlanLoosePallets(looseBySku);
                order.UsesPalletPlan = true;
                for (int i = 0; i < plan.Count; i++)
                {
                    int cases = plan[i].Sum(k => k.Value);
                    var st = _workQueue?.CreateTask(
                        WorkTaskType.OrderSelect,
                        EmployeeRole.OrderSelector,
                        palletId: null,
                        description: $"Select pallet {i + 1} of {plan.Count} for {order.CustomerName} ({cases} cs)",
                        orderId: order.OrderId);
                    if (st == null) continue;
                    st.PlanText = string.Join(",", plan[i].Select(k => $"{k.Key}:{k.Value}"));
                    st.PlanNumber = i + 1;
                    st.PlanTotal = plan.Count;
                    st.Status = hasLane ? WorkTaskStatus.Available : WorkTaskStatus.Open;
                }
            }

            Debug.Log($"[OrderService] Order {order.OrderNumber} released to {toLocation ?? "(no lane yet)"}: filed {palletTasks} " +
                      $"pallet pick(s)" + (looseCases > 0 ? $" and 1 case pick for {looseCases} loose case(s)." : ".") +
                      (unreservedPallets > 0 ? $" {unreservedPallets} filed without a source yet." : ""));
        }

        /// <summary>Cases in one full pallet of this SKU (Ti x Hi), or 0 if it has no committed pallet
        /// maths. The single definition of "a full pallet" on the outbound side.</summary>
        public int FullPalletCases(string skuId)
        {
            var sku = _inventoryService?.GetSkuData(skuId);
            if (sku == null || sku.Ti <= 0 || sku.Hi <= 0) return 0;
            return sku.Ti * sku.Hi;
        }

        /// <summary>True while an order is still just its placeholder task(s) — every live task Open —
        /// i.e. it has not been released to the floor yet. Same test ReleaseOrdersToLane gates on.</summary>
        public bool IsAwaitingRelease(OrderData order)
        {
            if (order == null || order.Status == OrderData.OrderStatus.Cancelled
                              || order.Status == OrderData.OrderStatus.Shipped) return false;
            var live = LiveTasksForOrder(order.OrderId).ToList();
            return !order.ReleasedToFloor && live.Count > 0 && live.All(t => IsReleasablePlaceholder(t));
        }

        /// <summary>A pallet has just been RECEIVED into a staging lane and its Putaway filed. If a released pallet pick for that SKU
        /// is still waiting (not claimed by anyone) - either with no source at all or aimed at a pallet sitting in the racks - point it
        /// at THIS pallet instead and cancel the Putaway: the freight goes straight out the door instead of up into a rack and back
        /// down again (Tad, 2026-10-23: a load of tuna was racked while a dozen tuna pallet picks were pending). The old source, if
        /// any, is un-reserved and stays where it is. Returns true when the pallet was handed to a pick.</summary>
        public bool TryAssignInboundPalletToPendingPick(string palletId)
        {
            if (_workQueue == null || _inventoryService == null || string.IsNullOrEmpty(palletId)) return false;
            var rec = _inventoryService.GetPallet(palletId);
            if (rec == null) return false;
            int full = FullPalletCases(rec.SkuId);
            if (full <= 0 || rec.Quantity != full) return false;   // only whole pallets can satisfy a pallet pick

            var putaway = _workQueue.Tasks.FirstOrDefault(t => t.Type == WorkTaskType.Putaway && t.PalletId == palletId
                                                           && t.Status == WorkTaskStatus.Available);
            if (putaway == null || string.IsNullOrEmpty(putaway.FromLocation)) return false;
            if (PalletLifecycleResolver.IsReservedByTask(palletId, out _)) return false;

            // Unassigned picks first (they have nothing to throw away), then ones aimed at a rack pallet nobody has gone for yet.
            var pick = _workQueue.Tasks
                .Where(t => t.Type == WorkTaskType.PalletPick && t.SkuId == rec.SkuId
                         && (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Open)
                         && string.IsNullOrEmpty(t.AssignedToEmployeeGuid)
                         && !string.IsNullOrEmpty(t.ToLocation)              // has a staging lane to go to
                         && _activeOrders.Any(o => o.OrderId == t.OrderId))
                .OrderBy(t => string.IsNullOrEmpty(t.PalletId) ? 0 : 1)
                .ThenBy(t => t.TaskId)
                .FirstOrDefault();
            if (pick == null) return false;

            string fromLane = putaway.FromLocation;
            // Hand back the rack pallet this pick had put on hold; it simply stays racked.
            if (!string.IsNullOrEmpty(pick.PalletId) && !string.IsNullOrEmpty(pick.FromLocation)
                && LocationRegistry.TryGet(pick.FromLocation, out var oldSource) && oldSource.Status == LocationStatus.Reserved)
                oldSource.Unreserve();

            _workQueue.CancelTasksForPallet(palletId);              // cancels the Putaway (do this BEFORE the pick names the pallet)
            pick.PalletId = palletId;
            pick.AssignFromLocation(fromLane);
            if (pick.Status == WorkTaskStatus.Open) pick.Status = WorkTaskStatus.Available;
            Debug.Log($"[OrderService] Received pallet {palletId} ({rec.SkuId}) goes straight to a waiting pallet pick - no putaway.");
            return true;
        }

        /// <summary>A live task of a never-released order that release may replace. Open is the normal placeholder; Available is
        /// what a PalletPick can be left as when an order was sent back to Open (ReturnOrderToOpen only resets the task the
        /// selector held), and release replaces those too. Anything a worker has already picked up (Assigned) is real work.</summary>
        private static bool IsReleasablePlaceholder(WorkTask t)
            => t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.Available;

        /// <summary>Cancels placeholder tasks being replaced by a release, handing back any rack slot a PalletPick had reserved.</summary>
        private void RetirePlaceholders(IEnumerable<WorkTask> placeholders)
        {
            var list = placeholders.ToList();
            // A claimed lane pallet's Putaway was cancelled when the old pick claimed it; refile it so the new release can claim it again.
            RefilePutawayForClaimedLanePallets(list);
            foreach (var task in list)
            {
                if (task.Type == WorkTaskType.PalletPick && !string.IsNullOrEmpty(task.FromLocation)
                    && LocationRegistry.TryGet(task.FromLocation, out var reserved)
                    && reserved.Status == LocationStatus.Reserved)
                    reserved.Unreserve();
                task.Status = WorkTaskStatus.Cancelled;
            }
        }

        /// <summary>What releasing an order puts on the floor: full-pallet picks for the Reach Trucks, and
        /// the loose cases left over for a selector. Same Ti x Hi split FileReleasedOrderTasks uses.</summary>
        public (int palletPicks, int looseCases) ReleaseBreakdown(OrderData order)
        {
            int pallets = 0, loose = 0;
            if (order == null) return (0, 0);
            foreach (var li in order.LineItems)
            {
                int full = FullPalletCases(li.SkuId);
                if (full <= 0) { loose += li.QuantityNeeded; continue; }
                pallets += li.QuantityNeeded / full;
                loose += li.QuantityNeeded % full;
            }
            return (pallets, loose);
        }

        private void SplitPalletNeed(OrderData order, out int whole, out int loose)
        {
            whole = 0;
            loose = 0;
            if (order == null) return;
            float looseFraction = 0f;
            foreach (var li in order.LineItems)
            {
                int full = FullPalletCases(li.SkuId);
                if (full <= 0) { looseFraction += li.QuantityNeeded / 48f; continue; }
                whole += li.QuantityNeeded / full;
                looseFraction += (li.QuantityNeeded % full) / (float)full;
            }
            loose = looseFraction > 0.0001f ? Mathf.Max(1, Mathf.CeilToInt(looseFraction - 0.0001f)) : 0;
        }

        /// <summary>
        /// How many staging positions (pallet tiers) this order will occupy once picked: one per whole
        /// pallet pick, plus the pallets the loose cases build into (summed as fractions of a full
        /// pallet of their own SKU, rounded up, minimum one if there are any loose cases). An estimate
        /// - a selector's cubing decides the real count - but it is the room the order needs in its lanes.
        /// </summary>
        public int PalletsNeeded(OrderData order)
        {
            SplitPalletNeed(order, out int whole, out int loose);
            return Mathf.Max(1, whole + loose);
        }

        /// <summary>Just the loose-case share of <see cref="PalletsNeeded"/>: the pallets a selector builds.
        /// They always stage in the order's PRIMARY lane, so that much room is held back for them.</summary>
        public int LoosePalletsNeeded(OrderData order)
        {
            SplitPalletNeed(order, out _, out int loose);
            // A selector stages at most SelectorPalletsPerTrip per trip (later trips overflow across the door's
            // lanes). Requiring room for the WHOLE loose share in one lane made any order over one lane's
            // capacity (11 loose pallets vs a 10-tier lane) unreleasable forever - 2026-10-17 playtest.
            return Mathf.Min(loose, SelectorPalletsPerTrip);
        }

        /// <summary>Must match OrderSelectionTaskDriver.MaxPalletsPerOrder (the pallets one selector carries).</summary>
        public const int SelectorPalletsPerTrip = 2;

        /// <summary>Files another OrderSelect trip for an order whose selector filled its pallets before the
        /// order was done. Only when loose cases remain and at least one of them is in a pick slot right now
        /// or can be replenished (stock exists somewhere); otherwise the order ships with what was staged.</summary>
        public bool FileContinuationSelect(OrderData order)
        {
            if (order == null || _workQueue == null) return false;
            if (order.Status == OrderData.OrderStatus.Shipped || order.Status == OrderData.OrderStatus.Cancelled) return false;
            if (order.UsesPalletPlan) return false;   // the plan already files every pallet; a short pick becomes SCRATCHES instead
            if (LiveTasksForOrder(order.OrderId).Any(t => t.Type == WorkTaskType.OrderSelect)) return false;

            int remaining = 0;
            bool stockExists = false;
            foreach (var li in order.LineItems)
            {
                int want = SelectableRemaining(order, li);
                if (want <= 0) continue;
                remaining += want;
                if (!stockExists && (OrderPickPath.ChooseSlot(li.SkuId, want, out _) != null ||
                    (_inventoryService != null && GameCore.Labor.ReplenishmentService.TryFindOldestPalletAnywhere(_inventoryService, li.SkuId, out _))))
                    stockExists = true;
            }
            if (remaining <= 0 || !stockExists) return false;

            var st = _workQueue.CreateTask(
                WorkTaskType.OrderSelect,
                EmployeeRole.OrderSelector,
                palletId: null,
                description: $"Continue selecting {remaining} loose case(s) for {order.CustomerName}",
                area: DominantArea(order),
                orderId: order.OrderId);
            if (st == null) return false;
            st.Status = WorkTaskStatus.Available;
            Debug.Log($"[OrderService] Order {order.OrderNumber}: selector's pallets were full - filed another trip for {remaining} case(s).");
            return true;
        }

        // ── SCRATCHES: the deficit pick list ───────────────────────────────────────────────────────

        /// <summary>The order's live SCRATCHES task, if any.</summary>
        public WorkTask LiveScratchTask(string orderId) => LiveTasksForOrder(orderId).FirstOrDefault(t => t.IsScratch);

        /// <summary>Every live SCRATCHES task of the order: the case-pick deficit list and/or pallet picks nobody had stock for.</summary>
        public List<WorkTask> LiveScratchTasks(string orderId) => LiveTasksForOrder(orderId).Where(t => t.IsScratch).ToList();

        /// <summary>
        /// A selector finished its trip with loose cases still owed (a pick face ran out and nothing else was reachable): the
        /// deficit becomes a SCRATCHES pick list (Tad, 2026-10-18). It is treated like a brand new pick list: the same
        /// OrderSelect work, claimed and picked by a selector, and the order cannot close out / ship while it is live. It is
        /// filed Available when some of the deficit can be picked right now, otherwise Open (waiting for replenishment to
        /// put stock in a pick face - PromoteScratches checks). The player can still cancel the rest of the order.
        /// Returns true when a scratch task exists afterwards.
        /// </summary>
        public bool FileScratchSelect(OrderData order)
        {
            if (order == null || _workQueue == null) return false;
            if (order.Status == OrderData.OrderStatus.Shipped || order.Status == OrderData.OrderStatus.Cancelled) return false;
            if (LiveTasksForOrder(order.OrderId).Any(t => t.Type == WorkTaskType.OrderSelect)) return LiveScratchTask(order.OrderId) != null;

            int deficit = 0;
            foreach (var li in order.LineItems) deficit += SelectableRemaining(order, li);
            if (deficit <= 0) return false;

            var st = _workQueue.CreateTask(
                WorkTaskType.OrderSelect,
                EmployeeRole.OrderSelector,
                palletId: null,
                description: $"{WorkTask.ScratchPrefix}: {deficit} short case(s) for {order.CustomerName}",
                area: DominantArea(order),
                orderId: order.OrderId);
            if (st == null) return false;
            st.Status = ScratchCanBePicked(order) ? WorkTaskStatus.Available : WorkTaskStatus.Open;
            string msg = $"Order {order.OrderNumber}: {deficit} case(s) short - filed a SCRATCHES pick list" +
                         (st.Status == WorkTaskStatus.Open ? " (waiting for stock in a pick slot)." : ".");
            Debug.LogWarning($"[OrderService] {msg}");
            SystemsLogWindow.LogWarning(msg);
            return true;
        }

        /// <summary>True when some line of the order still owed to a case picker has stock in an assigned pick face right now.</summary>
        private bool ScratchCanBePicked(OrderData order)
        {
            foreach (var li in order.LineItems)
            {
                int want = SelectableRemaining(order, li);
                if (want > 0 && OrderPickPath.ChooseSlot(li.SkuId, want, out _) != null) return true;
            }
            return false;
        }

        /// <summary>
        /// A scratch pallet pick with no staging lane yet gets one automatically (Tad, 2026-10-20): the order's own lanes
        /// first (at its door), then the nearest lane with room - the same search a normal release uses. The scratch stays
        /// Open (it waits for stock, not for a lane); this only gives it its destination so it can go out the moment a
        /// pallet exists. With no lane anywhere it is left as is and retried on the next tick.
        /// </summary>
        private void AssignLanesToScratchPicks()
        {
            if (_workQueue == null) return;
            var pending = _workQueue.Tasks.Where(t => t.IsScratch && t.Type == WorkTaskType.PalletPick &&
                                                     t.Status == WorkTaskStatus.Open && string.IsNullOrEmpty(t.ToLocation)).ToList();
            if (pending.Count == 0) return;
            ServiceLocator.TryGet<InventoryService>(out var inv);

            foreach (var t in pending)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == t.OrderId);
                if (order == null || !order.ReleasedToFloor) continue;

                (int door, string lane) chosen = default;
                bool found = false;
                foreach (var l in order.AllStagingLanes())
                    if (FreeRoom(l.door, l.lane) > 0) { chosen = l; found = true; break; }
                if (!found && StagingLaneAssignmentService.TryFindLanesForOrder(this, inv, order.AssignedDoorNumber,
                        order.CustomerId, 1, null, null, out var fresh) && fresh != null && fresh.Count > 0)
                {
                    chosen = fresh[0];
                    found = true;
                }
                if (!found) continue;

                t.AssignToLocation($"{chosen.door}{chosen.lane}");
                if (string.IsNullOrEmpty(order.AssignedLane))
                {
                    order.StagingDoorNumber = chosen.door;
                    order.AssignedLane = chosen.lane;
                    if (order.AssignedDoorNumber <= 0) order.AssignedDoorNumber = chosen.door;
                }
                else if (!(order.EffectiveStagingDoor == chosen.door && order.AssignedLane == chosen.lane))
                    order.AddOverflowLane(chosen.door, chosen.lane);
            }
        }

        /// <summary>Open SCRATCHES lists become Available once replenishment has put stock where a selector can reach it.</summary>
        private void PromoteScratches()
        {
            if (_workQueue == null) return;
            foreach (var t in _workQueue.Tasks.Where(t => t.IsScratch && t.Status == WorkTaskStatus.Open).ToList())
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == t.OrderId);
                if (order == null) { t.Status = WorkTaskStatus.Cancelled; continue; }
                if (t.Type == WorkTaskType.PalletPick)
                {
                    // A pallet-pick scratch waits for ANY pallet of its SKU (reserve, pick face or a staged inbound pallet)
                    // and for a lane to put it in. The reach truck binds the actual source when it claims it.
                    if (string.IsNullOrEmpty(t.ToLocation) || _inventoryService == null) continue;
                    if (ReplenishmentService.TryFindStagedPalletAwaitingPutaway(_workQueue, _inventoryService, t.SkuId, out _, out _)
                        || ReplenishmentService.TryFindOldestPalletAnywhere(_inventoryService, t.SkuId, out _))
                        t.Status = WorkTaskStatus.Available;
                    continue;
                }
                if (ScratchCanBePicked(order)) t.Status = WorkTaskStatus.Available;
            }
        }

        // ── Held work: released tasks that are waiting for lane room ──────────────────────────────

        /// <summary>The PalletPick / OrderSelect tasks of a released order that are still Open: filed, but
        /// not claimable because no staging lane had room for them yet. (A SCRATCHES list is Open for a different
        /// reason - no stock - so it is never "held work" and lane room does not release it.)</summary>
        private IEnumerable<WorkTask> HeldTasks(OrderData order)
        {
            if (order == null || !order.ReleasedToFloor) return Enumerable.Empty<WorkTask>();
            return LiveTasksForOrder(order.OrderId).Where(t => t.Status == WorkTaskStatus.Open && !t.IsScratch &&
                (t.Type == WorkTaskType.PalletPick || t.Type == WorkTaskType.OrderSelect));
        }

        /// <summary>How many of this order's tasks are being held back for lack of lane room.</summary>
        public int HeldWorkCount(OrderData order) => HeldTasks(order).Count();

        /// <summary>Tiers in a lane already spoken for by released work that hasn't landed yet: pallet
        /// picks headed there, and the pallets a selector will build into the order's primary lane.</summary>
        private int CommittedTiers(int door, string lane)
        {
            if (_workQueue == null) return 0;
            string address = $"{door}{lane}";
            int committed = 0;
            foreach (var t in _workQueue.Tasks)
            {
                if (t.Type == WorkTaskType.PalletPick && t.ToLocation == address &&
                    (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Assigned))
                    committed++;
            }
            foreach (var o in _activeOrders)
            {
                if (o == null || o.AssignedLane != lane || o.EffectiveStagingDoor != door) continue;
                if (o.Status == OrderData.OrderStatus.Shipped || o.Status == OrderData.OrderStatus.Cancelled) continue;
                bool selectorPending = LiveTasksForOrder(o.OrderId).Any(t => t.Type == WorkTaskType.OrderSelect &&
                    (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Assigned));
                if (selectorPending) committed += o.UsesPalletPlan
                    ? Mathf.Min(SelectorPalletsPerTrip, LiveTasksForOrder(o.OrderId).Count(t => t.Type == WorkTaskType.OrderSelect && t.HasPlan &&
                        (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Assigned)))
                    : LoosePalletsNeeded(o);
            }
            return committed;
        }

        /// <summary>Pallets a lane can still be given: free tiers minus tiers already promised to work
        /// that hasn't arrived yet.</summary>
        public int FreeRoom(int door, string lane)
        {
            ServiceLocator.TryGet<InventoryService>(out var inv);
            if (inv == null) return 0;
            return Mathf.Max(0, inv.CountFreeStagingSlotsInLane(door, lane) - CommittedTiers(door, lane));
        }

        /// <summary>Every not-yet-finished task belonging to an order, of any type. Bulk orders carry
        /// several (N pallet picks and maybe a case pick), so anything that used to find "the order's
        /// task" with FirstOrDefault has to come through here or it will see only one of them.</summary>
        public IEnumerable<WorkTask> LiveTasksForOrder(string orderId)
            => _workQueue == null || string.IsNullOrEmpty(orderId)
             ? Enumerable.Empty<WorkTask>()
             : _workQueue.Tasks.Where(t => t.OrderId == orderId
                                        && t.Status != WorkTaskStatus.Complete
                                        && t.Status != WorkTaskStatus.Cancelled);

        /// <summary>
        /// Cases on this order that outstanding PalletPick tasks are going to deliver for a SKU.
        ///
        /// This is what stops the case picker from walking off the pallet quantities. An
        /// OrderSelectionTaskDriver looks at OrderLineItem.QuantityRemaining and would happily pick
        /// all 620 cases by hand; subtracting this leaves it only the 20 it's actually there for.
        /// Derived from the live task list rather than stored on the line item, so it can't drift out
        /// of step with the work that actually exists.
        /// </summary>
        public int OutstandingPalletPickCases(string orderId, string skuId)
        {
            int fullPallet = FullPalletCases(skuId);
            if (fullPallet <= 0) return 0;

            int tasks = LiveTasksForOrder(orderId).Count(t => t.Type == WorkTaskType.PalletPick && t.SkuId == skuId);
            return tasks * fullPallet;
        }

        /// <summary>
        /// Divides an order's loose (case-picked) cases into pallets, exactly as a selector's pallet fills: every case adds
        /// 1/(Ti x Hi) of ITS OWN SKU's full pallet and a pallet is full once the sum reaches 1 (the case that crosses the line
        /// still goes on it). Lines are taken in walking order (the serpentine pick sequence) so a pallet is built along the
        /// route. Returns the pallets, each a list of (sku, cases).
        /// </summary>
        public List<List<KeyValuePair<string, int>>> PlanLoosePallets(List<KeyValuePair<string, int>> loose)
        {
            var pallets = new List<List<KeyValuePair<string, int>>>();
            if (loose == null || loose.Count == 0) return pallets;

            var ordered = loose.Select((kv, idx) =>
            {
                var slot = OrderPickPath.ChooseSlot(kv.Key, kv.Value, out _);
                long key = slot != null ? OrderPickPath.SequenceKey(slot.Address) : long.MaxValue;
                return (kv, idx, key);
            }).OrderBy(x => x.key).ThenBy(x => x.idx).ToList();

            float fill = 0f;
            var current = new List<KeyValuePair<string, int>>();
            foreach (var (kv, _, _) in ordered)
            {
                var sku = _inventoryService?.GetSkuData(kv.Key);
                int perPallet = sku != null ? Mathf.Max(1, sku.Ti * sku.Hi) : 48;
                float cost = 1f / perPallet;
                int left = kv.Value, onThisPallet = 0;
                while (left > 0)
                {
                    fill += cost; left--; onThisPallet++;
                    if (fill >= 1f)
                    {
                        current.Add(new KeyValuePair<string, int>(kv.Key, onThisPallet));
                        pallets.Add(current);
                        current = new List<KeyValuePair<string, int>>();
                        fill = 0f; onThisPallet = 0;
                    }
                }
                if (onThisPallet > 0) current.Add(new KeyValuePair<string, int>(kv.Key, onThisPallet));
            }
            if (current.Count > 0) pallets.Add(current);
            return pallets;
        }

        // Cases of each SKU a plan task has already put on its pallet (this session; an interrupted trip re-releases).
        private readonly Dictionary<string, Dictionary<string, int>> _planPicked = new Dictionary<string, Dictionary<string, int>>();

        /// <summary>Cases of this SKU the task's planned pallet still needs. Int.MaxValue when the task has no plan.</summary>
        public int PlanRemaining(WorkTask task, string skuId)
        {
            if (task == null || !task.HasPlan) return int.MaxValue;
            int planned = 0;
            foreach (var kv in task.PlanLines()) if (kv.Key == skuId) planned += kv.Value;
            _planPicked.TryGetValue(task.TaskId, out var picked);
            int got = picked != null && picked.TryGetValue(skuId, out int g) ? g : 0;
            return Mathf.Max(0, planned - got);
        }

        /// <summary>True once every case of the task's planned pallet is on it.</summary>
        public bool PlanComplete(WorkTask task)
        {
            if (task == null || !task.HasPlan) return false;
            foreach (var kv in task.PlanLines())
                if (PlanRemaining(task, kv.Key) > 0) return false;
            return true;
        }

        /// <summary>Tasks other than <paramref name="mine"/> that still owe this order a pallet (unclaimed, held or in someone's hands).</summary>
        public bool OtherLivePlanWork(OrderData order, WorkTask mine)
            => order != null && LiveTasksForOrder(order.OrderId).Any(t => t != mine && !t.IsScratch &&
                (t.Type == WorkTaskType.OrderSelect || t.Type == WorkTaskType.PalletPick));

        /// <summary>How many cases of this line a case picker may still take — what's left after both
        /// what's already picked and what the Reach Trucks still owe.</summary>
        public int SelectableRemaining(OrderData order, OrderLineItem line)
        {
            if (order == null || line == null) return 0;
            // No IsBulk gate any more: every order can carry PalletPick tasks (see FileOrderTasks),
            // so every order has to subtract what the Reach Trucks still owe. Leaving the gate in
            // would let a selector hand-pick cases a pallet pick is already on its way with, and the
            // order would be over-picked by exactly the pallet quantities.
            return Mathf.Max(0, line.QuantityRemaining - OutstandingPalletPickCases(order.OrderId, line.SkuId));
        }

        // ── Several selectors on one order (2026-10-17) ──────────────────────────────────────────────
        // A big order used to be worked by ONE selector at a time (~2.5 game-min per case), so it ran late
        // while other selectors stood idle. Now an order big enough for more than one trip gets helper
        // OrderSelect tasks, and each selector RESERVES the cases it is walking to, so two selectors never
        // head for the same cases. One reservation per task (the current target), counted down per case.

        /// <summary>Most selectors that may work one order at the same time.</summary>
        public const int MaxSelectorsPerOrder = 3;

        private readonly Dictionary<string, (string OrderId, string SkuId, int Qty)> _pickReservations
            = new Dictionary<string, (string OrderId, string SkuId, int Qty)>();

        /// <summary>This task is heading for <paramref name="qty"/> cases of a line (replaces its previous target).</summary>
        public void ReservePick(string taskId, string orderId, string skuId, int qty)
        {
            if (string.IsNullOrEmpty(taskId)) return;
            if (qty <= 0) { _pickReservations.Remove(taskId); return; }
            _pickReservations[taskId] = (orderId, skuId, qty);
        }

        /// <summary>One reserved case landed on the selector's pallet (it now counts in QuantityPicked instead).</summary>
        public void NotePickedCase(string taskId, string skuId = null)
        {
            if (!string.IsNullOrEmpty(taskId) && !string.IsNullOrEmpty(skuId))
            {
                if (!_planPicked.TryGetValue(taskId, out var picked)) _planPicked[taskId] = picked = new Dictionary<string, int>();
                picked[skuId] = (picked.TryGetValue(skuId, out int n) ? n : 0) + 1;
            }
            if (string.IsNullOrEmpty(taskId) || !_pickReservations.TryGetValue(taskId, out var r)) return;
            if (r.Qty <= 1) _pickReservations.Remove(taskId);
            else _pickReservations[taskId] = (r.OrderId, r.SkuId, r.Qty - 1);
        }

        public void ReleasePick(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return;
            _pickReservations.Remove(taskId);
            _planPicked.Remove(taskId);
        }

        /// <summary>Cases of a line other selectors are walking to right now. A reservation whose task is no
        /// longer claimed (finished, cancelled, selector gone) is ignored, so a leak can't hide cases forever.</summary>
        private int ReservedByOthers(string orderId, string skuId, string exceptTaskId)
        {
            if (_pickReservations.Count == 0) return 0;
            int sum = 0;
            foreach (var kv in _pickReservations)
            {
                if (kv.Key == exceptTaskId || kv.Value.OrderId != orderId || kv.Value.SkuId != skuId) continue;
                var t = _workQueue?.Tasks.FirstOrDefault(x => x.TaskId == kv.Key);
                if (t == null || t.Status != WorkTaskStatus.Assigned) continue;
                sum += kv.Value.Qty;
            }
            return sum;
        }

        /// <summary><see cref="SelectableRemaining(OrderData, OrderLineItem)"/> minus what OTHER selectors on this
        /// order are already walking to. What one selector may still go and get.</summary>
        public int SelectableRemaining(OrderData order, OrderLineItem line, string forTaskId)
        {
            if (order == null || line == null) return 0;
            int general = Mathf.Max(0, SelectableRemaining(order, line) - ReservedByOthers(order.OrderId, line.SkuId, forTaskId));
            if (!string.IsNullOrEmpty(forTaskId) && _workQueue != null)
            {
                var task = _workQueue.Tasks.FirstOrDefault(t => t.TaskId == forTaskId);
                if (task != null && task.HasPlan) return Mathf.Min(general, PlanRemaining(task, line.SkuId));   // only what THIS pallet is planned to carry
            }
            return general;
        }

        /// <summary>Selectors currently working this order, other than <paramref name="except"/>.</summary>
        public int OtherSelectorsOnOrder(OrderData order, WorkTask except)
            => order == null ? 0 : LiveTasksForOrder(order.OrderId).Count(t => t.Type == WorkTaskType.OrderSelect &&
                                                                               t != except && t.Status == WorkTaskStatus.Assigned);

        /// <summary>
        /// Called when a selector claims an order's trip. If the loose cases still to pick need more trips than
        /// the selectors already on it can carry, files ONE more OrderSelect task so an idle selector can join.
        /// That helper files the next one when it claims, so help grows one selector at a time up to
        /// <see cref="MaxSelectorsPerOrder"/>. Never stacks unclaimed helpers.
        /// </summary>
        public bool FileHelperSelectIfWorthwhile(OrderData order)
        {
            if (order == null || _workQueue == null || !order.ReleasedToFloor || string.IsNullOrEmpty(order.AssignedLane)) return false;
            if (order.UsesPalletPlan) return false;   // every pallet already has its own task
            if (order.Status == OrderData.OrderStatus.Shipped || order.Status == OrderData.OrderStatus.Cancelled) return false;

            var selects = LiveTasksForOrder(order.OrderId).Where(t => t.Type == WorkTaskType.OrderSelect).ToList();
            if (selects.Any(t => t.Status != WorkTaskStatus.Assigned)) return false;   // a trip is already waiting for someone
            int working = selects.Count;
            if (working >= MaxSelectorsPerOrder) return false;

            // Pallets the remaining loose cases build into, as fractions of each SKU's full pallet.
            float pallets = 0f;
            int cases = 0;
            bool pickable = false;
            foreach (var li in order.LineItems)
            {
                int want = SelectableRemaining(order, li);
                if (want <= 0) continue;
                cases += want;
                int full = FullPalletCases(li.SkuId);
                pallets += full > 0 ? want / (float)full : want / 48f;
                if (!pickable && OrderPickPath.ChooseSlot(li.SkuId, want, out _) != null) pickable = true;
            }
            // Only with something a case picker can actually take from a pick face. Without stock a helper claimed the trip,
            // found nothing, ended it, and the next claim filed another helper: 784 empty trips in the 2026-10-22 soak.
            if (cases <= 0 || !pickable || pallets <= working * SelectorPalletsPerTrip) return false;

            var st = _workQueue.CreateTask(
                WorkTaskType.OrderSelect,
                EmployeeRole.OrderSelector,
                palletId: null,
                description: $"Help select {order.CustomerName} ({cases} loose case(s) left)",
                area: DominantArea(order),
                orderId: order.OrderId);
            if (st == null) return false;
            st.Status = WorkTaskStatus.Available;
            Debug.Log($"[OrderService] Order {order.OrderNumber}: ~{pallets:F1} pallet(s) of loose cases left for {working} selector(s) - filed a helper trip.");
            return true;
        }

        /// <summary>True when nothing is left for a case picker because every remaining case is owed
        /// by a PalletPick. Lets the selector finish its remainder and deliver normally instead of
        /// reporting a short pick, which is what it would otherwise conclude from finding no
        /// pickable location for the pallet quantities.</summary>
        public bool HasLivePalletPicks(OrderData order)
            => order != null && LiveTasksForOrder(order.OrderId).Any(t => t.Type == WorkTaskType.PalletPick);

        public bool RemainingIsAllPalletPick(OrderData order)
        {
            if (order == null) return false;
            if (order.IsFullyPicked) return false;
            return order.LineItems.All(li => SelectableRemaining(order, li) <= 0);
        }

        /// <summary>
        /// Credits one delivered full pallet against a bulk order and advances the order's status.
        ///
        /// Called by the Reach Truck the moment the pallet is physically set down in the staging lane,
        /// which is why it can set Staged: the goods really are where a loader will find them. Whoever
        /// finishes last — the final pallet pick or the case picker's remainder — is what flips the
        /// order to Staged, so neither has to know about the other.
        /// </summary>
        public void NotePalletPicked(string orderId, string skuId, int cases)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null) return;

            var line = order.LineItems.FirstOrDefault(li => li.SkuId == skuId && !li.IsFullyPicked);
            if (line == null)
            {
                Debug.LogWarning($"[OrderService] Pallet pick delivered {cases} cs of {skuId} for order " +
                                 $"{orderId}, but no unfilled line wants that SKU — not credited.");
                return;
            }

            // Clamped: a pallet carrying more than the line still needs must not push QuantityPicked
            // past QuantityNeeded, which would make TotalUnitsRemaining negative and bill the customer
            // for cases they never ordered.
            line.QuantityPicked = Mathf.Min(line.QuantityNeeded, line.QuantityPicked + cases);

            if (order.IsFullyPicked)
            {
                MarkOrderFulfilled(orderId);
                order.Status = OrderData.OrderStatus.Staged;
                Debug.Log($"[OrderService] Bulk order {orderId} ({order.CustomerName}) complete — staged.");
            }
            else if (order.Status == OrderData.OrderStatus.Pending)
            {
                order.Status = OrderData.OrderStatus.PartiallyPicked;
            }
        }

        /// <summary>Releases a batch of still-Open orders — must all belong to the same customer —
        /// to a specific staging lane: stamps AssignedDoorNumber/AssignedLane on each and flips each
        /// order's WorkTask from Open to Available so Order Selectors can start claiming them. Fails
        /// atomically (no partial changes) if any order isn't actually Open, the batch spans more
        /// than one customer, or the lane is already owned by a different customer.</summary>
        /// <summary>Releases a batch to a whole STAGE (a door's set of staging lanes) rather than to
        /// one named lane — what the Work Queue panel now offers as "Stage 1", "Stage 2"… Picks the
        /// first lane of that Stage with room as the starting point; if it later fills, the selector
        /// overflows into the next lane of the same Stage on its own
        /// (InventoryService.TryFindStagingLaneForPallets), so this choice is a starting point, not a
        /// commitment. That overflow is decided per ORDER, never per pallet — one order's pallets all
        /// land in the lane recorded on OrderData.AssignedLane.</summary>
        public bool ReleaseOrdersToStage(List<string> orderIds, int doorNumber)
        {
            ServiceLocator.TryGet<InventoryService>(out var inv);

            // Re-check here, not just in the dropdown that offered this stage: the panel rebuilds on a
            // refresh tick, so a trailer can dock or a putaway can land in the gap between the player
            // seeing the list and clicking Submit. Refusing here is what actually prevents the double
            // assignment; hiding it in the UI is only the first line.
            // An inbound trailer at the door no longer closes it: TryResolveStartLane picks from LanesInStage, which already excludes
            // any lane holding received pallets AND the lanes a docked inbound trailer will fill. It fails only if nothing is left.
            string releasingCustomer = orderIds != null && orderIds.Count > 0 ? _activeOrders.FirstOrDefault(o => o.OrderId == orderIds[0])?.CustomerId : null;
            if (!StagingLaneAssignmentService.TryResolveStartLane(this, inv, doorNumber, out string startLane, releasingCustomer))
            {
                Debug.LogWarning($"[OrderService] Stage {doorNumber} has no pickable staging lane — release refused.");
                return false;
            }
            return ReleaseOrdersToLane(orderIds, doorNumber, startLane);
        }

        /// <summary>One customer's share of a release, and where it goes. <see cref="DoorNumber"/> /
        /// <see cref="Lane"/> are the staging lane (possibly at a door other than the dock door);
        /// <see cref="Waiting"/> means no lane was free, so the work is released but held Open.</summary>
        public class StageRelease
        {
            public string CustomerId;
            public string CustomerName;
            public List<string> OrderIds;
            public int DockDoor;      // door whose trailer carries these orders (appointment door), 0 if unscheduled
            public int DoorNumber;    // staging door
            public string Lane;       // staging lane at DoorNumber; null when Waiting
            public bool Waiting;      // no lane free right now
            public List<(int door, string lane)> Lanes = new List<(int door, string lane)>(); // every lane chosen (DoorNumber/Lane = the first)
        }

        /// <summary>
        /// Works out where each customer in a selection would stage, WITHOUT changing anything — so the
        /// panel can show the plan before the player commits and then commit that same plan (see
        /// ReleaseOrdersToStages). One algorithm for both, so a preview can't drift from the commit.
        ///
        /// Each customer's orders share ONE lane: the first completely empty lane found searching outward
        /// from the dock door the orders' trailer is booked at (see
        /// StagingLaneAssignmentService.TryFindLaneForOrder). Customers in the same batch are planned in
        /// selection order and never given the same lane, nor a door another customer in the batch holds.
        /// If no lane is free the customer is planned as <see cref="StageRelease.Waiting"/> rather than
        /// failing — the release still happens and the lane is assigned when one opens.
        /// </summary>
        public bool TryPlanStageSpread(List<string> orderIds, out List<StageRelease> plan, out string failReason)
        {
            plan = new List<StageRelease>();
            failReason = null;
            if (orderIds == null || orderIds.Count == 0) { failReason = "nothing selected"; return false; }

            ServiceLocator.TryGet<InventoryService>(out var inv);
            ServiceLocator.TryGet<DockScheduleService>(out var schedule);

            // Group by customer (and dock door), preserving the order they were selected in.
            var groups = new List<StageRelease>();
            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (order == null) { failReason = $"order {id} no longer exists"; plan = null; return false; }

                int dock = schedule?.FindForOrder(id)?.DoorNumber ?? 0;
                var group = groups.FirstOrDefault(g => g.CustomerId == order.CustomerId && g.DockDoor == dock);
                if (group == null)
                {
                    group = new StageRelease
                    {
                        CustomerId = order.CustomerId,
                        CustomerName = order.CustomerName,
                        OrderIds = new List<string>(),
                        DockDoor = dock
                    };
                    groups.Add(group);
                }
                group.OrderIds.Add(id);
            }

            var plannedLanes = new HashSet<(int door, string lane)>();
            var plannedDoorOwners = new Dictionary<int, string>();

            foreach (var group in groups)
            {
                int groupPallets = group.OrderIds.Sum(id => PalletsNeeded(_activeOrders.FirstOrDefault(o => o.OrderId == id)));
                if (StagingLaneAssignmentService.TryFindLanesForOrder(this, inv, group.DockDoor, group.CustomerId,
                        groupPallets, plannedLanes, plannedDoorOwners, out var found))
                {
                    group.Lanes = found;
                    group.DoorNumber = found[0].door;
                    group.Lane = found[0].lane;
                    foreach (var (d, l) in found)
                    {
                        plannedLanes.Add((d, l));
                        plannedDoorOwners[d] = group.CustomerId;
                    }
                    if (group.DockDoor <= 0) group.DockDoor = found[0].door; // unscheduled: dock where it stages
                }
                else
                {
                    group.Waiting = true;
                }
                plan.Add(group);
            }
            return true;
        }

        /// <summary>
        /// Releases a selection spanning any number of customers in one action, each to the lane the plan
        /// chose (see TryPlanStageSpread). All planning happens before anything is touched. A customer
        /// with no free lane is released WAITING (tasks Open, warning raised once) rather than refused.
        /// <paramref name="waitingForLane"/> is true if ANY customer in the batch had to wait.
        /// </summary>
        public bool ReleaseOrdersToStages(List<string> orderIds, out string failReason)
            => ReleaseOrdersToStages(orderIds, out failReason, out _, out _);

        public bool ReleaseOrdersToStages(List<string> orderIds, out string failReason, out bool waitingForLane)
            => ReleaseOrdersToStages(orderIds, out failReason, out waitingForLane, out _);

        /// <summary>
        /// Same as above, and also reports <paramref name="capacityNote"/>: non-null when the lanes that
        /// were free could not hold everything, so only part of the work was released and the rest is
        /// held Open until room opens up ("Releasing 20 of 25 pallet picks ... Free up some space!").
        /// </summary>
        public bool ReleaseOrdersToStages(List<string> orderIds, out string failReason, out bool waitingForLane, out string capacityNote)
        {
            waitingForLane = false;
            capacityNote = null;
            if (!TryPlanStageSpread(orderIds, out var plan, out failReason)) return false;

            var notes = new List<string>();
            foreach (var group in plan)
            {
                string note = null;
                bool ok = group.Waiting
                    ? ReleaseOrdersWaitingForLane(group.OrderIds, group.DockDoor)
                    : ReleaseOrdersToLanes(group.OrderIds, group.Lanes, group.DockDoor, out note);
                if (ok)
                {
                    if (group.Waiting)
                    {
                        waitingForLane = true;
                        UIToast.Show("No open lanes - released orders can't be assigned until one opens.");
                        SystemsLogWindow.LogWarning($"{group.CustomerName}: no open lanes; tasks can't be assigned until one opens.");
                    }
                    else if (note != null)
                    {
                        notes.Add(note);
                        UIToast.Show(note);
                        SystemsLogWindow.LogWarning(note);
                    }
                    continue;
                }

                failReason = $"{group.CustomerName} could not be released";
                Debug.LogWarning($"[OrderService] Release aborted partway: {failReason}. " +
                                 $"{plan.IndexOf(group)} of {plan.Count} customer(s) were already released.");
                return false;
            }

            if (notes.Count > 0) capacityNote = string.Join("  ", notes);
            Debug.Log($"[OrderService] Released {orderIds.Count} order(s) across {plan.Count} customer(s): " +
                      string.Join(", ", plan.Select(g => g.Waiting ? $"{g.CustomerName} -> (waiting for lane)"
                                                                    : $"{g.CustomerName} -> {string.Join("+", g.Lanes.Select(l => $"{l.door}{l.lane}"))}")));
            return true;
        }

        /// <summary>
        /// The moment an order stops being a placeholder and becomes real: mints its player-facing
        /// OrderNumber (unless it already has one — see remarks), cancels its placeholder task, and
        /// breaks it into the individually-trackable PalletPick/OrderSelect tasks a Reach Truck or
        /// Order Selector can claim, reserving each pallet's source right now (see
        /// FileReleasedOrderTasks).
        /// </summary>
        /// <remarks>
        /// An order can arrive here already carrying an OrderNumber: ReconcileStagedOrdersAgainstScene
        /// resets a Staged/Loading order whose staged pallets didn't survive a save back to Pending
        /// and unreleased, re-filing a fresh placeholder task — but a number once minted is the
        /// order's identity from then on, so that path deliberately leaves OrderNumber alone. This is
        /// what "release it again" actually re-releases, so the number must not change.
        /// </remarks>
        public bool ReleaseOrdersToLane(List<string> orderIds, int doorNumber, string lane, int dockDoor = 0)
        {
            if (string.IsNullOrEmpty(lane)) return false;
            return ReleaseOrdersToLanes(orderIds, new List<(int door, string lane)> { (doorNumber, lane) }, dockDoor, out _);
        }

        /// <summary>
        /// Releases a batch (one customer) to the floor and spreads its work over the given lanes, as far
        /// as their room goes. Everything the batch owes is filed, but only what FITS is made claimable:
        /// the rest stays Open ("held") until a lane has room (see <see cref="AssignWaitingLanes"/> and
        /// the Work Queue's "Release Orders to Floor"). <paramref name="capacityNote"/> says so when
        /// something had to be held back.
        /// </summary>
        public bool ReleaseOrdersToLanes(List<string> orderIds, IList<(int door, string lane)> lanes, int dockDoor, out string capacityNote)
        {
            capacityNote = null;
            if (_workQueue == null || orderIds == null || orderIds.Count == 0 || lanes == null || lanes.Count == 0) return false;

            // Every order must still have only its placeholder task(s) live and Open - release
            // replaces exactly those with the real, individually-trackable ones (see
            // FileReleasedOrderTasks). An order with anything Available/Assigned/etc. has already
            // been released once; releasing it again would double its work.
            var pairs = new List<(OrderData order, List<WorkTask> placeholders)>();
            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (order == null) return false;

                var placeholders = LiveTasksForOrder(id).ToList();
                // All-or-nothing per order: ReleasedToFloor also rules out an order released with no
                // lane, whose (Open) tasks are real work, not placeholders.
                if (order.ReleasedToFloor) return false;
                if (placeholders.Count == 0 || !placeholders.All(IsReleasablePlaceholder)) return false;
                pairs.Add((order, placeholders));
            }

            string customerId = pairs[0].order.CustomerId;
            if (pairs.Any(p => p.order.CustomerId != customerId)) return false;
            // Exclusivity is per LANE (Tad, 2026-10-25), and the lanes passed in were chosen by the lane planner with that rule (empty lanes
            // first, a lane shared with another customer only as a last resort). A door-wide "no other customer at this door" check here
            // would refuse exactly the lanes the planner is allowed to pick.

            foreach (var (order, placeholders) in pairs)
            {
                RetirePlaceholders(placeholders);

                // AssignedDoorNumber is the DOCK door (whose trailer carries the order); the lanes may be at
                // other doors. AssignedLane/StagingDoorNumber are stamped by AllocateHeldWork when the first
                // task actually lands in a lane.
                order.AssignedDoorNumber = dockDoor > 0 ? dockDoor : lanes[0].door;
                order.StagingDoorNumber = 0;
                order.AssignedLane = null;
                order.OverflowLanes.Clear();
                order.ReleasedToFloor = true;
                if (string.IsNullOrEmpty(order.OrderNumber)) order.OrderNumber = GenerateOrderNumber(order);
                FileReleasedOrderTasks(order, 0, null);
            }

            var orders = pairs.Select(p => p.order).ToList();
            AllocateHeldWork(orders, lanes, out int pickReleased, out int pickHeld, out bool caseHeld);
            capacityNote = BuildCapacityNote(orders, pickReleased, pickHeld, caseHeld);

            Debug.Log($"[OrderService] Released {pairs.Count} order(s) for {customerId} to {string.Join("+", lanes.Select(l => $"{l.door}{l.lane}"))}" +
                      (dockDoor > 0 ? $" (dock door {dockDoor})" : "") + ": " +
                      string.Join(", ", pairs.Select(p => p.order.OrderNumber)) +
                      (capacityNote != null ? $" - {capacityNote}" : ""));
            return true;
        }

        private static string BuildCapacityNote(IList<OrderData> orders, int pickReleased, int pickHeld, bool caseHeld)
        {
            if (pickHeld <= 0 && !caseHeld) return null;
            string who = orders.Count > 0 ? orders[0].CustomerName : "the order";
            if (pickHeld > 0)
                return $"Releasing {pickReleased} of {pickReleased + pickHeld} pallet picks for {who} due to space limitation in lanes." +
                       (caseHeld ? " The case pick is held too." : "") + " Free up some space!";
            return $"{who}'s case pick is held - no lane has room for its pallets. Free up some space!";
        }

        /// <summary>
        /// The core of a release: walks the given lanes and makes as much of each order's HELD work
        /// claimable as the lanes have room for. A pallet pick takes one position (tier) in its lane; the
        /// case pick takes the pallets its loose cases will build into, in the order's PRIMARY lane (the
        /// first lane that receives anything), which is why that much room is set aside there before the
        /// pallet picks fill the rest. Lanes that receive work after the first become overflow lanes.
        /// Returns how many tasks were released; the rest is reported through the out parameters.
        /// </summary>
        public int AllocateHeldWork(IList<OrderData> orders, IList<(int door, string lane)> lanes,
                                    out int pickReleased, out int pickHeld, out bool caseHeld)
        {
            pickReleased = 0;
            pickHeld = 0;
            caseHeld = false;
            int tasksReleased = 0;

            var room = new Dictionary<(int door, string lane), int>();
            foreach (var l in lanes)
                if (!room.ContainsKey(l)) room[l] = FreeRoom(l.door, l.lane);

            foreach (var order in orders)
            {
                if (order == null) continue;
                var held = HeldTasks(order).ToList();
                var picks = held.Where(t => t.Type == WorkTaskType.PalletPick).ToList();
                // PLANNED orders have one case-pick task per pallet, and each needs one lane tier like a pallet pick does.
                var plannedSelects = order.UsesPalletPlan
                    ? held.Where(t => t.Type == WorkTaskType.OrderSelect && t.HasPlan).OrderBy(t => t.PlanNumber).ToList()
                    : new List<WorkTask>();
                var select = order.UsesPalletPlan ? null : held.FirstOrDefault(t => t.Type == WorkTaskType.OrderSelect);
                int loose = LoosePalletsNeeded(order);

                foreach (var l in lanes)
                {
                    if (picks.Count == 0 && select == null && plannedSelects.Count == 0) break;
                    if (!room.TryGetValue(l, out int r) || r <= 0) continue;

                    bool hasPrimary = !string.IsNullOrEmpty(order.AssignedLane);
                    bool isPrimary = hasPrimary && order.EffectiveStagingDoor == l.door && order.AssignedLane == l.lane;
                    bool becomesPrimary = !hasPrimary;

                    // The first lane an order uses is where its case pick stages. If that pick is still
                    // waiting and this lane can't take it, let a roomier lane become the primary instead.
                    if (select != null && becomesPrimary && r < loose) continue;

                    int used = 0;
                    if (select != null && (isPrimary || becomesPrimary) && r >= loose)
                    {
                        select.Status = WorkTaskStatus.Available;
                        select = null;
                        r -= loose;
                        used += loose;
                        tasksReleased++;
                    }

                    // Planned case-pick pallets first (they take longest), one tier each.
                    int takeSel = Mathf.Min(r, plannedSelects.Count);
                    for (int i = 0; i < takeSel; i++) plannedSelects[i].Status = WorkTaskStatus.Available;
                    if (takeSel > 0) plannedSelects.RemoveRange(0, takeSel);
                    r -= takeSel;
                    used += takeSel;
                    tasksReleased += takeSel;

                    int n = Mathf.Min(r, picks.Count);
                    string address = $"{l.door}{l.lane}";
                    for (int i = 0; i < n; i++)
                    {
                        picks[i].AssignToLocation(address);
                        picks[i].Status = WorkTaskStatus.Available;
                    }
                    if (n > 0) picks.RemoveRange(0, n);
                    r -= n;
                    used += n;
                    pickReleased += n;
                    tasksReleased += n;

                    if (used > 0)
                    {
                        if (becomesPrimary)
                        {
                            order.StagingDoorNumber = l.door;
                            order.AssignedLane = l.lane;
                            if (order.AssignedDoorNumber <= 0) order.AssignedDoorNumber = l.door;
                        }
                        else if (!isPrimary)
                        {
                            order.AddOverflowLane(l.door, l.lane);
                        }
                    }
                    room[l] = r;
                }

                pickHeld += picks.Count;
                if (select != null || plannedSelects.Count > 0) caseHeld = true;
            }
            return tasksReleased;
        }

        /// <summary>The player picked a lane for held work in the Work Queue: release as much of it as that
        /// lane has room for. <paramref name="note"/> is set when some work still had to be held back.</summary>
        public int ReleaseHeldWorkToLane(IList<OrderData> orders, int door, string lane, out string note)
        {
            int released = AllocateHeldWork(orders, new List<(int door, string lane)> { (door, lane) },
                                            out int pr, out int ph, out bool ch);
            note = BuildCapacityNote(orders, pr, ph, ch);
            return released;
        }

        /// <summary>
        /// Releases a batch to the floor with NO lane yet: no staging lane was free. The placeholder is
        /// replaced by the real PalletPick/OrderSelect tasks (so the work exists and is visible) but
        /// they stay Open — unclaimable — and the order is marked ReleasedToFloor with AssignedLane
        /// null. <see cref="AssignWaitingLanes"/> finishes the job when a lane opens up.
        /// </summary>
        public bool ReleaseOrdersWaitingForLane(List<string> orderIds, int dockDoor)
        {
            if (_workQueue == null || orderIds == null || orderIds.Count == 0) return false;

            var pairs = new List<(OrderData order, List<WorkTask> placeholders)>();
            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (order == null || order.ReleasedToFloor) return false;
                var placeholders = LiveTasksForOrder(id).ToList();
                if (placeholders.Count == 0 || !placeholders.All(IsReleasablePlaceholder)) return false;
                pairs.Add((order, placeholders));
            }

            foreach (var (order, placeholders) in pairs)
            {
                RetirePlaceholders(placeholders);
                order.AssignedDoorNumber = dockDoor;
                order.StagingDoorNumber = 0;
                order.AssignedLane = null;
                order.ReleasedToFloor = true;
                if (string.IsNullOrEmpty(order.OrderNumber)) order.OrderNumber = GenerateOrderNumber(order);
                FileReleasedOrderTasks(order, 0, null);
            }

            Debug.LogWarning($"[OrderService] Released {pairs.Count} order(s) with NO lane available — tasks stay Open " +
                             $"until a staging lane frees up: {string.Join(", ", pairs.Select(p => p.order.OrderNumber))}");
            return true;
        }

        /// <summary>Gives a released-but-laneless batch its lane and makes its work claimable: sets the
        /// staging door + lane, points every live PalletPick at the lane, and flips Open tasks to
        /// Available. Used by the periodic retry and by the player's manual lane choice.</summary>
        public void AssignLaneToOrders(IEnumerable<OrderData> orders, int stagingDoor, string lane)
        {
            foreach (var order in orders)
            {
                if (order == null) continue;
                if (order.AssignedDoorNumber <= 0) order.AssignedDoorNumber = stagingDoor;
                order.StagingDoorNumber = stagingDoor;
                order.AssignedLane = lane;
                foreach (var t in LiveTasksForOrder(order.OrderId))
                {
                    // Only still-unclaimed work is redirected: a task someone has already Assigned is
                    // mid-flight toward the old lane and keeps its destination.
                    if (t.Status != WorkTaskStatus.Open && t.Status != WorkTaskStatus.Available) continue;
                    if (t.Type == WorkTaskType.PalletPick) t.AssignToLocation($"{stagingDoor}{lane}");
                    if (t.Status == WorkTaskStatus.Open && !t.IsScratch) t.Status = WorkTaskStatus.Available;   // a scratch waits for stock, not a lane
                }
            }
        }

        private int _waitingLaneTick;
        private readonly Dictionary<string, int> _phantomStagedTicks = new();

        /// <summary>A STAGED order with no pallet of its own anywhere (floor, forks, trailer) and no live task can never
        /// load or finish: the Work Queue kept showing "Staged" for a lane with nothing in it (2026-10-18, OG2120). The
        /// load-time reconcile only ran on a save load; run it for such orders at runtime too, after a short grace
        /// (a few ticks) so a pallet in the middle of being swapped between carriers isn't mistaken for missing.</summary>
        private void HealPhantomStagedOrders()
        {
            var havePallets = new HashSet<string>();
            foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>(FindObjectsInactive.Include))
                if (p != null && !string.IsNullOrEmpty(p.OrderId)) havePallets.Add(p.OrderId);

            var phantom = new List<string>();
            var seen = new HashSet<string>();
            foreach (var o in _activeOrders)
            {
                if (o == null || o.Status != OrderData.OrderStatus.Staged) continue;
                seen.Add(o.OrderId);
                if (havePallets.Contains(o.OrderId) || LiveTasksForOrder(o.OrderId).Any()) { _phantomStagedTicks.Remove(o.OrderId); continue; }
                _phantomStagedTicks.TryGetValue(o.OrderId, out int n);
                _phantomStagedTicks[o.OrderId] = ++n;
                if (n >= 3) phantom.Add(o.OrderId);
            }
            foreach (var k in _phantomStagedTicks.Keys.Where(k => !seen.Contains(k)).ToList()) _phantomStagedTicks.Remove(k);

            if (phantom.Count == 0) return;
            foreach (var id in phantom) _phantomStagedTicks.Remove(id);
            ReconcileStagedOrdersAgainstScene(phantom);
        }

        // Every few in-game minutes; AssignWaitingLanes is a no-op (one LINQ pass) when nothing waits.
        private void OnMinuteForLanes(string eventId, GameCore.Events.Payloads.SimulationTimeData _)
        {
            if (++_waitingLaneTick % 3 != 0) return;
            AssignWaitingLanes();
            AssignLanesToScratchPicks();
            PromoteScratches();
            ReclaimOrphanedFreight();
            HealPhantomStagedOrders();
            // Loaders call this when a pass ends, but an order can become closable later (its last pallet pick
            // lands, or turns out to be unfillable) with no loader pass left to notice. Re-check on the tick.
            AutoCloseLoadedOrders();
        }

        /// <summary>
        /// Outbound pallets standing in a staging lane whose ORDER no longer exists (shipped, cancelled or
        /// archived while they were still there) can never be loaded - and used to pull an order-less truck to
        /// the dock. Whole pallets (they still carry their SKU) are registered back into inventory and get a
        /// Putaway, exactly like a pallet cut by Cancel Remaining. Pallets mid-carry, loading or on a trailer are
        /// left alone.
        /// </summary>
        /// <summary>Cancels an order's still-unclaimed PalletPick / OrderSelect tasks, handing back any rack slot a
        /// PalletPick had reserved. Claimed (Assigned) work is left to finish; what it delivers for a gone order
        /// is returned to stock by <see cref="ReclaimOrphanedFreight"/>.</summary>
        private int CancelLeftoverTasks(string orderId)
        {
            int n = 0;
            foreach (var t in LiveTasksForOrder(orderId).ToList())
            {
                if (t.Status == WorkTaskStatus.Assigned) continue;
                if (t.Type != WorkTaskType.PalletPick && t.Type != WorkTaskType.OrderSelect) continue;
                if (t.Type == WorkTaskType.PalletPick && !string.IsNullOrEmpty(t.FromLocation)
                    && LocationRegistry.TryGet(t.FromLocation, out var reserved) && reserved.Status == LocationStatus.Reserved)
                    reserved.Unreserve();
                t.Status = WorkTaskStatus.Cancelled;
                t.AssignedToEmployeeGuid = null;
                n++;
            }
            return n;
        }

        /// <summary>Load tasks are keyed by LANE (FromLocation "1E", no OrderId), so cancelling an order's own tasks
        /// never touches them. Once no active order uses a lane any more, its still-unclaimed Load tasks are
        /// orphans: a dock stocker would claim them and drive off to load nothing (two such tasks were left
        /// behind by a shipped Cracker Barrel order in the 2026-10-17 soak test).</summary>
        private void CancelIdleLoadTasksForUnusedLanes()
        {
            if (_workQueue == null) return;
            foreach (var t in _workQueue.Tasks.Where(t => t.Type == WorkTaskType.Load &&
                                                          (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Open)).ToList())
            {
                string from = t.FromLocation ?? "";
                int i = 0;
                while (i < from.Length && char.IsDigit(from[i])) i++;
                if (i == 0 || i >= from.Length || !int.TryParse(from.Substring(0, i), out int door)) continue;
                string lane = from.Substring(i);
                int dash = lane.IndexOf('-');
                if (dash >= 0) lane = lane.Substring(0, dash);
                if (_activeOrders.Any(o => o.UsesLane(door, lane))) continue;
                t.Status = WorkTaskStatus.Cancelled;
                t.AssignedToEmployeeGuid = null;
                Debug.LogWarning($"[OrderService] Cancelled Load task for lane {door}{lane}: no active order uses that lane any more.");
            }
        }

        private void ReclaimOrphanedFreight()
        {
            if (_inventoryService == null) return;
            CancelIdleLoadTasksForUnusedLanes();
            var activeIds = new HashSet<string>(_activeOrders.Select(o => o.OrderId));

            // Tasks still pointing at an order that's gone (saves made before Archive cancelled them).
            if (_workQueue != null)
                foreach (var orphanId in _workQueue.Tasks.Where(t => !string.IsNullOrEmpty(t.OrderId) && !activeIds.Contains(t.OrderId)
                                                                   && t.Status != WorkTaskStatus.Complete && t.Status != WorkTaskStatus.Cancelled)
                                                         .Select(t => t.OrderId).Distinct().ToList())
                {
                    int n = CancelLeftoverTasks(orphanId);
                    if (n > 0) Debug.LogWarning($"[OrderService] Cancelled {n} leftover task(s) of order {orphanId}, which is no longer active.");
                }
            foreach (var pallet in Object.FindObjectsByType<OutboundPalletBuilder>())
            {
                if (pallet == null || string.IsNullOrEmpty(pallet.OrderId) || activeIds.Contains(pallet.OrderId)) continue;
                if (!pallet.IsOnFloor) continue;   // on forks / in a trailer
                if (pallet.Stage != OutboundPalletBuilder.PalletStage.Staged) continue;

                // A CASE-PICKED pallet left behind (didn't fit / never loaded): its order didn't pay for it (ShipOrder bills
                // shipped pallets only), and the cases go back where they came from - a selector reverse-picks them into
                // their pick slots on a pallet jack (Tad, 2026-10-17).
                if (pallet.CaseCountsBySku.Count > 0)
                {
                    if (string.IsNullOrEmpty(pallet.ReturnId)) FileReturnTask(pallet);
                    continue;
                }

                var data = pallet.GetComponent<PalletData>();
                if (data == null || string.IsNullOrEmpty(data.ItemNumber) || data.CaseQuantity <= 0) continue;
                if (ReturnLanePalletToStock(pallet, data.ItemNumber, data.CaseQuantity))
                    Debug.LogWarning($"[OrderService] Freight for order {pallet.OrderId} outlived the order - {data.CaseQuantity} cs of {data.ItemNumber} returned to stock for putaway.");
            }
        }

        /// <summary>
        /// The heartbeat retry for released orders that still have HELD work (nothing claimable yet, or
        /// only part of it): groups them by customer + dock door, offers the lanes they already use and
        /// then any newly empty lanes, and releases whatever now fits. Quiet when nothing is held or no
        /// room has opened. State is derived from persisted fields (the Open tasks themselves), so a
        /// save taken while holding simply resumes after load.
        /// </summary>
        public void AssignWaitingLanes()
        {
            var holding = _activeOrders.Where(o => o != null && o.ReleasedToFloor &&
                o.Status != OrderData.OrderStatus.Shipped && o.Status != OrderData.OrderStatus.Cancelled &&
                o.Status != OrderData.OrderStatus.Loaded && HeldWorkCount(o) > 0).ToList();
            if (holding.Count == 0) return;

            ServiceLocator.TryGet<InventoryService>(out var inv);
            foreach (var group in holding.GroupBy(o => (o.CustomerId, o.AssignedDoorNumber)))
            {
                var batch = group.ToList();

                int needed = 0;
                foreach (var o in batch)
                {
                    var held = HeldTasks(o).ToList();
                    needed += held.Count(t => t.Type == WorkTaskType.PalletPick);
                    if (o.UsesPalletPlan) needed += held.Count(t => t.Type == WorkTaskType.OrderSelect && t.HasPlan);
                    else if (held.Any(t => t.Type == WorkTaskType.OrderSelect)) needed += LoosePalletsNeeded(o);
                }

                // Lanes the batch already uses first (primary before overflow), then any empty lane.
                var lanes = new List<(int door, string lane)>();
                foreach (var o in batch)
                    foreach (var l in o.AllStagingLanes())
                        if (!lanes.Contains(l)) lanes.Add(l);

                int ownRoom = lanes.Sum(l => FreeRoom(l.door, l.lane));
                if (ownRoom < needed &&
                    StagingLaneAssignmentService.TryFindLanesForOrder(this, inv, group.Key.AssignedDoorNumber,
                        group.Key.CustomerId, needed - ownRoom, null, null, out var fresh))
                {
                    foreach (var l in fresh)
                        if (!lanes.Contains(l)) lanes.Add(l);
                }
                if (lanes.Count == 0) continue;

                int released = AllocateHeldWork(batch, lanes, out int pr, out int ph, out bool ch);
                if (released == 0) continue;

                string numbers = string.Join(", ", batch.Select(o => o.OrderNumber ?? o.OrderId));
                string where = string.Join("+", batch.SelectMany(o => o.AllStagingLanes()).Distinct().Select(l => $"{l.door}{l.lane}"));
                string tail = (ph > 0 || ch) ? $" {ph} pallet pick(s) still waiting for room." : " Everything is released.";
                string msg = $"Room opened in lane {where} - released {released} task(s) for {batch[0].CustomerName}'s order {numbers}.{tail}";
                Debug.Log($"[OrderService] {msg}");
                UIToast.Show(msg);
                SystemsLogWindow.LogSystem(msg);
            }
        }

        /// <summary>
        /// True if this order can still be called off. A distribution centre like this has no such
        /// thing as a backorder: an order the pickers can't fill is either cancelled outright or held
        /// while the stock is hunted down / received in, so "cancellable" means nothing of it is
        /// physically committed yet. That is: still awaiting release (its task is Open), or released
        /// but not yet claimed by a selector (Available) — plus any legacy Backorder record left in a
        /// save from before that status was retired. An order a selector is actively picking, or whose
        /// goods are already staged, loading or loaded, is NOT cancellable — those have pallets in the
        /// world that would be orphaned.
        /// </summary>
        public bool CanCancelOrder(OrderData order)
        {
            if (order == null) return false;
            if (order.Status == OrderData.OrderStatus.Backorder) return true;   // legacy saves only
            if (order.Status != OrderData.OrderStatus.Pending) return false;

            // Every live task has to be uncommitted, not just the first one found: a bulk order with
            // one Reach Truck already carrying a pallet has goods in motion even if its case pick
            // hasn't started.
            return LiveTasksForOrder(order.OrderId)
                .All(t => t.Status == WorkTaskStatus.Open || t.Status == WorkTaskStatus.Available);
        }

        /// <summary>
        /// Cancels every cancellable order in the batch and returns how many were actually cancelled;
        /// any id that isn't cancellable is skipped rather than failing the whole call, so a mixed
        /// selection does the part it can. Each cancellation cancels the order's OrderSelect task (so
        /// no selector can claim work that no longer exists) and clears its door/lane stamp, which
        /// releases the stage the order was holding.
        /// </summary>
        public int CancelOrders(List<string> orderIds)
        {
            if (orderIds == null || orderIds.Count == 0) return 0;

            ServiceLocator.TryGet<DockScheduleService>(out var schedule);

            int cancelled = 0;
            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (!CanCancelOrder(order)) continue;

                // ToList first — cancelling mutates nothing in the queue, but LiveTasksForOrder is a
                // lazy query over it and the status writes below would change what it yields mid-walk.
                var cancelledNow = LiveTasksForOrder(id).ToList();
                var unlifted = cancelledNow.Where(t => t.Status != WorkTaskStatus.Assigned).ToList();   // captured BEFORE the statuses change
                foreach (var task in cancelledNow)
                {
                    // A released-but-unclaimed PalletPick already has its source reserve locked (see
                    // FileReleasedOrderTasks) — cancelling has to hand that back, or the slot stays
                    // Reserved forever with nothing left to ever claim it. A still-Open order (never
                    // released) has no FromLocation yet, so this is a no-op for it.
                    if (task.Type == WorkTaskType.PalletPick && !string.IsNullOrEmpty(task.FromLocation)
                        && LocationRegistry.TryGet(task.FromLocation, out var reservedLocation)
                        && reservedLocation.Status == LocationStatus.Reserved)
                    {
                        reservedLocation.Unreserve();
                    }

                    task.Status = WorkTaskStatus.Cancelled;
                    task.AssignedToEmployeeGuid = null; // release the claim so nothing tries to "resume mine"
                }
                RefilePutawayForClaimedLanePallets(unlifted);

                Debug.Log($"[OrderService] Order {order.OrderNumber ?? order.OrderId} ({order.CustomerName}) cancelled by the player — " +
                          $"{order.TotalUnitsPicked}/{order.TotalUnits} case(s) had been picked; released Stage {order.EffectiveStagingDoor}.");

                order.AssignedDoorNumber = 0;
                order.StagingDoorNumber = 0;
                order.AssignedLane = null;
                order.OverflowLanes.Clear();
                order.Status = OrderData.OrderStatus.Cancelled;
                StampClosedNow(order);
                // Any reserve this order had locked was just handed back above. What's still real is
                // the Schedule tab's appointment for this order: left alone it would squat on a door
                // forever. Detach it, and park the trailer if this was the last order riding on it,
                // freeing the door for a new booking.
                schedule?.DetachCancelledOrder(order.OrderId);
                OnOrderCancelled?.Invoke(order);
                // Safe to mutate _activeOrders here: this loop walks orderIds, not the order list.
                Archive(order);
                cancelled++;
            }

            return cancelled;
        }

        // ── Player cancellation: preview the cost, then do it ───────────────────────────────────────

        /// <summary>What cancelling would cost, computed BEFORE anything changes so the UI can show it and
        /// the player can decide. <see cref="Allowed"/> false means it cannot be done (see WhyNot).</summary>
        public class CancelImpact
        {
            public bool Allowed;
            public string WhyNot;
            public string OrderId;
            public string OrderLabel;
            public string Customer;
            public int ReputationLoss;       // points of reputation lost
            public float PartnershipLoss;    // points of customer satisfaction lost
            public int CasesCancelled;       // partial cancel only: cases that will not ship
            public int TasksCancelled;       // partial cancel only: held tasks removed
            public int PalletsReturned;      // partial cancel only: pallets left on the dock that go back to stock
            public int DaysToDue;
        }

        /// <summary>The scale of a cancellation: the closer to the due date, the worse it is for the
        /// customer, and a bulk (full-pallet) deal hurts more. <paramref name="fraction"/> scales it down
        /// for a partial cancel (the share of the order that will not ship).</summary>
        private void CancelScale(OrderData order, float fraction, out int reputation, out float partnership, out int daysToDue)
        {
            daysToDue = order.DueDay - (_timeService != null ? _timeService.Day : order.DueDay);
            int rep; float sat;
            if (daysToDue >= 2)      { rep = 5;  sat = 3f;  }
            else if (daysToDue == 1) { rep = 10; sat = 6f;  }
            else if (daysToDue == 0) { rep = 20; sat = 10f; }
            else                     { rep = 25; sat = 15f; }   // already overdue
            if (order.IsBulk) { rep = Mathf.RoundToInt(rep * 1.5f); sat *= 2f; }

            reputation = Mathf.Max(1, Mathf.RoundToInt(rep * Mathf.Clamp01(fraction)));
            partnership = sat * Mathf.Clamp01(fraction);
        }

        /// <summary>Cost of cancelling this WHOLE order. Allowed only while nothing of it is committed
        /// (see <see cref="CanCancelOrder"/>).</summary>
        public CancelImpact PreviewCancel(OrderData order)
        {
            var impact = new CancelImpact
            {
                OrderId = order?.OrderId,
                OrderLabel = order != null ? (order.OrderNumber ?? order.OrderId) : "?",
                Customer = order?.CustomerName
            };
            if (order == null) { impact.WhyNot = "the order no longer exists"; return impact; }
            if (!CanCancelOrder(order))
            {
                impact.WhyNot = "work on it has already started - use Cancel Remaining to drop what hasn't been picked, or ship what is done";
                return impact;
            }
            impact.Allowed = true;
            CancelScale(order, 1f, out impact.ReputationLoss, out impact.PartnershipLoss, out impact.DaysToDue);
            return impact;
        }

        /// <summary>Cases still owed by work that is being HELD for lack of lane room: held pallet picks
        /// (one full pallet each) plus, if the case pick is held, the loose cases left after every pallet
        /// pick (held or not) is accounted for.</summary>
        private int CasesOwedByHeldWork(OrderData order, out int heldTasks)
        {
            var held = HeldTasks(order).ToList();
            heldTasks = held.Count;
            int cases = 0;
            foreach (var t in held.Where(t => t.Type == WorkTaskType.PalletPick))
                cases += FullPalletCases(t.SkuId);

            if (held.Any(t => t.Type == WorkTaskType.OrderSelect))
            {
                foreach (var li in order.LineItems)
                    cases += Mathf.Max(0, li.QuantityRemaining - OutstandingPalletPickCases(order.OrderId, li.SkuId));
            }
            return cases;
        }

        /// <summary>Whole pallets of this order standing in its staging lane(s) that can be cut: a real
        /// received pallet a reach truck carried out of the racks (PalletData still on it), so it can be
        /// registered back into inventory and put away. caseBuilt counts the ones a selector built from
        /// case picks (mixed cases, no single SKU) - those can't be returned as a pallet yet.</summary>
        private List<(OutboundPalletBuilder pallet, string sku, int cases)> CuttableLanePallets(OrderData order, out int caseBuilt)
        {
            caseBuilt = 0;
            var result = new List<(OutboundPalletBuilder, string, int)>();
            if (order == null || !order.AllStagingLanes().Any()) return result;
            var grid = Object.FindAnyObjectByType<PlacementGrid>();
            if (grid == null) return result;

            foreach (var pallet in Object.FindObjectsByType<OutboundPalletBuilder>())
            {
                if (pallet == null || pallet.OrderId != order.OrderId || !pallet.IsOnFloor) continue;
                if (!LaneNamingService.TryGetSlot(grid.WorldToCell(pallet.transform.position), out var slot)) continue;
                if (!order.UsesLane(slot.DoorNumber, slot.Lane)) continue;

                var data = pallet.GetComponent<PalletData>();
                if (data != null && !string.IsNullOrEmpty(data.ItemNumber) && data.CaseQuantity > 0)
                    result.Add((pallet, data.ItemNumber, data.CaseQuantity));
                else
                    caseBuilt++;
            }
            return result;
        }

        /// <summary>True while a dock stocker has actually claimed a Load task for one of this order's lanes.</summary>
        private bool LoaderIsWorkingOrder(OrderData order)
        {
            if (_workQueue == null || order == null) return false;
            var lanes = new HashSet<string>(order.AllStagingLanes().Select(l => $"{l.door}{l.lane}"));
            return _workQueue.Tasks.Any(t => t.Type == WorkTaskType.Load && t.Status == WorkTaskStatus.Assigned &&
                                             lanes.Contains(t.FromLocation));
        }

        /// <summary>Puts a pallet that never made it onto the trailer back into stock: registers it in
        /// inventory at the lane slot it stands in, strips its outbound tag, and files a Putaway.</summary>
        /// <summary>Files a Return task for a left-behind case-picked pallet: an Order Selector takes it on a pallet jack
        /// and puts every case back into the pick slot it came from, in the reverse of the order it was picked.</summary>
        /// <summary>Short pallet label shown in RETURNS rows and log lines (first 4 characters of the return id).</summary>
        public static string ReturnLabel(string returnId)
            => string.IsNullOrEmpty(returnId) ? "??" : returnId.Substring(0, Mathf.Min(4, returnId.Length)).ToUpperInvariant();

        private void FileReturnTask(OutboundPalletBuilder pallet)
        {
            if (_workQueue == null || pallet == null) return;
            pallet.ReturnId = System.Guid.NewGuid().ToString();
            string where = "";
            var grid = Object.FindAnyObjectByType<PlacementGrid>();
            if (grid != null && LaneNamingService.TryGetSlot(grid.WorldToCell(pallet.transform.position), out var slot))
                where = $"{slot.DoorNumber}{slot.Lane}";
            var order = _orderHistory.FirstOrDefault(o => o.OrderId == pallet.OrderId) ?? _activeOrders.FirstOrDefault(o => o.OrderId == pallet.OrderId);
            string num = order?.OrderNumber ?? pallet.OrderId;
            var task = _workQueue.CreateTask(
                WorkTaskType.Return,
                EmployeeRole.OrderSelector,
                palletId: pallet.ReturnId,
                description: $"Pallet {ReturnLabel(pallet.ReturnId)}: return {pallet.TotalCases} case(s) from order {num} to their pick slots",
                fromLocation: string.IsNullOrEmpty(where) ? null : where,
                orderId: null);
            if (task != null) task.Status = WorkTaskStatus.Available;
            string msg = $"Order {num}: pallet {ReturnLabel(pallet.ReturnId)} ({pallet.TotalCases} case(s)) was not loaded - it was cancelled off the order and a RETURNS task was filed so a selector puts the cases back in their pick slots.";
            Debug.LogWarning($"[OrderService] {msg}");
            SystemsLogWindow.LogSystem(msg);
        }

        private bool ReturnLanePalletToStock(OutboundPalletBuilder outbound, string skuId, int cases)
        {
            var grid = Object.FindAnyObjectByType<PlacementGrid>();
            if (outbound == null || grid == null || _inventoryService == null) return false;

            var go = outbound.gameObject;
            var cell = grid.WorldToCell(go.transform.position);
            string address = LaneNamingService.TryGetSlot(cell, out var slot)
                ? $"STG{slot.DoorNumber}{slot.Lane}-{slot.Slot}" : null;

            var record = _inventoryService.RegisterPhysicalPallet(cell, skuId, cases);
            if (record == null) return false;
            PalletMasterLink.Attach(go, record.PalletId);
            var pdata = go.GetComponent<PalletData>();
            if (pdata != null) pdata.Initialize(pdata.LoadId, skuId, cases, pdata.ExpirationDay, pdata.Area, pdata.IconSprite, cell);

            outbound.SetNavObstacleActive(true);
            Object.Destroy(outbound);   // no longer outbound freight: the loader and the lane-occupancy scan stop seeing it

            if (_workQueue != null)
            {
                var sku = _inventoryService.GetSkuData(skuId);
                _workQueue.CreateTask(
                    WorkTaskType.Putaway,
                    EmployeeRole.ReachTruckOperator,
                    record.PalletId,
                    $"Putaway pallet [{record.LoadId}] cut from an order, left at {address ?? "the dock"}",
                    fromLocation: address,
                    toLocation: null,
                    area: sku != null ? sku.StorageArea : PalletData.AreaCategory.Grocery);
            }
            return true;
        }

        /// <summary>Cost of dropping the part of an order that is being held for lack of lane room ("Cancel
        /// Remaining"). The order carries on and ships SHORT with what has been released. Not allowed when
        /// nothing is held, or when nothing has been released yet (cancel the whole order instead).</summary>
        public CancelImpact PreviewCancelRemaining(OrderData order)
        {
            var impact = new CancelImpact
            {
                OrderId = order?.OrderId,
                OrderLabel = order != null ? (order.OrderNumber ?? order.OrderId) : "?",
                Customer = order?.CustomerName
            };
            if (order == null) { impact.WhyNot = "the order no longer exists"; return impact; }
            // Two kinds of "remaining": work HELD back for lack of lane room, and finished pallets that
            // never made it onto the trailer (it filled up, or the loader can't take them). Both are cut
            // the same way a real dock cuts an order: ship what's aboard, put the rest back in the racks.
            int heldCases = 0;
            if (order.ReleasedToFloor && HeldWorkCount(order) > 0)
            {
                bool somethingReleased = LiveTasksForOrder(order.OrderId).Any(t => t.Status != WorkTaskStatus.Open)
                                         || order.TotalUnitsPicked > 0;
                if (somethingReleased) heldCases = CasesOwedByHeldWork(order, out impact.TasksCancelled);
            }
            // A SCRATCHES list nobody has started is cut the same way: its short cases are what the customer won't get.
            // Open or Available scratches can be cancelled (an Assigned one is already on its way and will count itself down).
            var scratchTasks = LiveScratchTasks(order.OrderId).Where(t => t.Status != WorkTaskStatus.Assigned).ToList();
            if (scratchTasks.Any(t => t.Type == WorkTaskType.OrderSelect))
                foreach (var li in order.LineItems) heldCases += SelectableRemaining(order, li);
            foreach (var t in scratchTasks.Where(t => t.Type == WorkTaskType.PalletPick))
                heldCases += FullPalletCases(t.SkuId);
            impact.TasksCancelled += scratchTasks.Count;

            var leftOnDock = CuttableLanePallets(order, out int caseBuiltLeft);
            bool loadingOrLoaded = order.Status == OrderData.OrderStatus.Loading || order.Status == OrderData.OrderStatus.Loaded;
            int cutCases = loadingOrLoaded ? leftOnDock.Sum(c => c.cases) : 0;
            impact.PalletsReturned = loadingOrLoaded ? leftOnDock.Count : 0;

            if (heldCases <= 0 && cutCases <= 0)
            {
                impact.WhyNot = caseBuiltLeft > 0 && loadingOrLoaded
                    ? "what is left on the dock was built from case picks - those can't be cut yet, load them"
                    : !order.ReleasedToFloor || HeldWorkCount(order) == 0
                        ? "no part of it is being held back or left on the dock"
                        : "none of it has been released yet - cancel the whole order instead";
                return impact;
            }
            if (cutCases > 0 && LoaderIsWorkingOrder(order))
            {
                impact.WhyNot = "the loader is on it right now - wait for it to finish this pass";
                return impact;
            }

            impact.CasesCancelled = heldCases + cutCases;
            if (impact.CasesCancelled <= 0) { impact.WhyNot = "there is nothing left to cancel"; return impact; }

            float fraction = order.TotalUnits > 0 ? impact.CasesCancelled / (float)order.TotalUnits : 1f;
            CancelScale(order, fraction, out impact.ReputationLoss, out impact.PartnershipLoss, out impact.DaysToDue);
            impact.Allowed = true;
            return impact;
        }

        /// <summary>The wording of a confirmation: what will be cancelled and exactly what it costs.</summary>
        public static string DescribeCancel(IList<CancelImpact> impacts, bool partial)
        {
            var ok = impacts.Where(i => i.Allowed).ToList();
            if (ok.Count == 0) return "Nothing here can be cancelled.";

            int rep = ok.Sum(i => i.ReputationLoss);
            int sat = Mathf.RoundToInt(ok.Sum(i => i.PartnershipLoss));
            var sb = new System.Text.StringBuilder();

            if (partial)
            {
                int cases = ok.Sum(i => i.CasesCancelled);
                int tasks = ok.Sum(i => i.TasksCancelled);
                string who = ok.Count == 1 ? $"{ok[0].Customer}'s order {ok[0].OrderLabel}" : $"{ok.Count} orders";
                sb.Append($"Cancel the remaining {cases} case(s) ({tasks} held task(s)) of {who}?\n");
                int pallets = ok.Sum(i => i.PalletsReturned);
                sb.Append("The order ships SHORT with what is already released or loaded. Held pallets go back to stock");
                sb.Append(pallets > 0 ? $", and {pallets} pallet(s) left on the dock are put away again. The customer is not charged for what is cut.\n"
                                       : ".\n");
            }
            else
            {
                string who = ok.Count == 1 ? $"{ok[0].Customer}'s order {ok[0].OrderLabel}" : $"{ok.Count} orders";
                sb.Append($"Cancel {who}?\nNothing ships and its reserved pallets go back to stock.\n");
            }
            sb.Append($"\nConsequences: -{rep} Reputation / -{sat} Partnership");
            int soonest = ok.Min(i => i.DaysToDue);
            sb.Append(soonest > 1 ? $"\n(Due in {soonest} days - the earlier you cancel, the less it costs.)"
                    : soonest == 1 ? "\n(Due tomorrow.)"
                    : soonest == 0 ? "\n(Due TODAY - this hurts.)"
                    : "\n(Already overdue.)");
            var skipped = impacts.Where(i => !i.Allowed).ToList();
            if (skipped.Count > 0) sb.Append($"\n{skipped.Count} selected order(s) can't be cancelled and are left alone.");
            return sb.ToString();
        }

        /// <summary>Cancels whole orders on the PLAYER's say-so and charges the scaled penalty previewed by
        /// <see cref="PreviewCancel"/>. Orders that cannot be cancelled are skipped. Returns how many were.</summary>
        public int CancelOrdersByPlayer(List<string> orderIds)
        {
            if (orderIds == null) return 0;
            ServiceLocator.TryGet(out OrderArrivalService arrivals);

            var done = new List<string>();
            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                var impact = PreviewCancel(order);
                if (!impact.Allowed) continue;

                // Stamp BEFORE CancelOrders fires OnOrderCancelled, which is where reputation is charged.
                order.CancelReputationCost = impact.ReputationLoss;
                if (CancelOrders(new List<string> { id }) == 0) { order.CancelReputationCost = 0; continue; }
                if (!string.IsNullOrEmpty(order.ContractId))
                    arrivals?.PenalizeSatisfaction(order.ContractId, impact.PartnershipLoss);
                done.Add(id);
            }
            return done.Count;
        }

        /// <summary>When an order claims a pallet that is still sitting in a staging lane, that pallet's Putaway task is
        /// cancelled (see FileOrderTasks). If the order is then cancelled before a truck has lifted the pallet, nothing
        /// would ever put it away, so file a fresh Putaway for it. Tasks a truck already holds (Assigned) are left alone:
        /// the truck is mid-move and finishing it is safer than yanking the pallet out from under it.</summary>
        private void RefilePutawayForClaimedLanePallets(IEnumerable<WorkTask> unliftedPalletPicks)
        {
            if (_workQueue == null || _inventoryService == null) return;
            foreach (var task in unliftedPalletPicks)
            {
                if (task.Type != WorkTaskType.PalletPick || string.IsNullOrEmpty(task.PalletId)) continue;
                if (string.IsNullOrEmpty(task.FromLocation) ||
                    !task.FromLocation.StartsWith("STG", System.StringComparison.OrdinalIgnoreCase)) continue;

                var pallet = _inventoryService.GetPallet(task.PalletId);
                if (pallet == null || pallet.Quantity <= 0) continue;
                if (_workQueue.Tasks.Any(t => t.Type == WorkTaskType.Putaway && t.PalletId == task.PalletId &&
                                              t.Status != WorkTaskStatus.Complete && t.Status != WorkTaskStatus.Cancelled)) continue;

                var sku = _inventoryService.GetSkuData(pallet.SkuId);
                var created = _workQueue.CreateTask(
                    WorkTaskType.Putaway,
                    EmployeeRole.ReachTruckOperator,
                    pallet.PalletId,
                    $"Putaway pallet [{pallet.LoadId}] returned from a cancelled order at {task.FromLocation}",
                    fromLocation: task.FromLocation,
                    toLocation: null,
                    area: sku != null ? sku.StorageArea : PalletData.AreaCategory.Grocery);
                if (created != null)
                    Debug.Log($"[OrderService] Order cancelled - pallet {pallet.PalletId} left in {task.FromLocation} goes back to putaway.");
            }
        }

        /// <summary>Drops the held part of an order ("Cancel Remaining"): releases the pallets those tasks had
        /// reserved, cancels the tasks, records the cases so they aren't penalised a second time at ship
        /// time, and charges the scaled penalty. The order carries on and ships short.</summary>
        public bool CancelRemaining(string orderId, out CancelImpact impact)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            impact = PreviewCancelRemaining(order);
            if (!impact.Allowed) return false;

            var heldNow = HeldTasks(order).ToList();
            foreach (var sc in LiveScratchTasks(order.OrderId))
                if (sc.Status != WorkTaskStatus.Assigned && !heldNow.Contains(sc)) heldNow.Add(sc);
            var unliftedHeld = heldNow.Where(t => t.Status != WorkTaskStatus.Assigned).ToList();   // captured BEFORE the statuses change
            foreach (var task in heldNow)
            {
                if (task.Type == WorkTaskType.PalletPick && !string.IsNullOrEmpty(task.FromLocation)
                    && LocationRegistry.TryGet(task.FromLocation, out var reserved)
                    && reserved.Status == LocationStatus.Reserved)
                    reserved.Unreserve();
                task.Status = WorkTaskStatus.Cancelled;
                task.AssignedToEmployeeGuid = null;
            }
            RefilePutawayForClaimedLanePallets(unliftedHeld);

            // Finished pallets left on the dock (the trailer was full): back into stock via putaway, and
            // taken off what the customer is billed - the order ships with what actually went aboard.
            int returned = 0;
            if (impact.PalletsReturned > 0)
            {
                foreach (var (pallet, sku, cases) in CuttableLanePallets(order, out _))
                {
                    if (!ReturnLanePalletToStock(pallet, sku, cases)) continue;
                    returned++;
                    var line = order.LineItems.FirstOrDefault(li => li.SkuId == sku && li.QuantityPicked > 0);
                    if (line != null) line.QuantityPicked = Mathf.Max(0, line.QuantityPicked - cases);
                }

                // Nothing left to load in those lanes: close out the loader's still-open Load tasks.
                if (_workQueue != null)
                {
                    var lanes = new HashSet<string>(order.AllStagingLanes().Select(l => $"{l.door}{l.lane}"));
                    var grid = Object.FindAnyObjectByType<PlacementGrid>();
                    foreach (var task in _workQueue.Tasks.Where(t => t.Type == WorkTaskType.Load &&
                                 (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Open) &&
                                 lanes.Contains(t.FromLocation)).ToList())
                    {
                        bool laneStillHasFreight = grid != null && Object.FindObjectsByType<OutboundPalletBuilder>()
                            .Any(p => p != null && p.IsOnFloor &&
                                      LaneNamingService.TryGetSlot(grid.WorldToCell(p.transform.position), out var sl) &&
                                      $"{sl.DoorNumber}{sl.Lane}" == task.FromLocation);
                        if (!laneStillHasFreight) task.Status = WorkTaskStatus.Cancelled;
                    }
                }
            }

            order.CancelledCases += impact.CasesCancelled;

            if (ServiceLocator.TryGet(out ReputationService reputation) && reputation != null)
                reputation.Add(-impact.ReputationLoss, $"cancelled the remaining {impact.CasesCancelled} case(s) of order {impact.OrderLabel}");
            if (!string.IsNullOrEmpty(order.ContractId) && ServiceLocator.TryGet(out OrderArrivalService arrivals))
                arrivals?.PenalizeSatisfaction(order.ContractId, impact.PartnershipLoss);

            Debug.Log($"[OrderService] Order {impact.OrderLabel} ({order.CustomerName}): player cancelled the remaining " +
                      $"{impact.CasesCancelled} case(s) ({impact.TasksCancelled} held task(s)); it now ships short. " +
                      $"-{impact.ReputationLoss} reputation, -{impact.PartnershipLoss:F0} partnership." +
                      (returned > 0 ? $" {returned} pallet(s) left on the dock go back to stock." : ""));

            // A loaded order whose leftovers are gone is ready to ship - do it now rather than waiting for
            // a loader pass that will never come.
            if (order.Status == OrderData.OrderStatus.Loaded) AutoCloseLoadedOrders();
            return true;
        }

        /// <summary>Releases a batch of fully-Staged orders — same customer, same staging lane — to
        /// a door for loading: sets each order's status to Loading and files ONE Load WorkTask
        /// covering that whole lane, which TrailerLoadController claims and physically loads onto
        /// the door's trailer. Summons an outbound trailer to the door first if one isn't already
        /// sitting there — the player is assigning to a door, not to a specific truck, so there's no
        /// separate "call a trailer" step. Fails (no changes) if any order isn't Staged, or the batch
        /// spans more than one customer or lane.</summary>
        public bool ReleaseOrdersToLoading(List<string> orderIds, int doorNumber)
        {
            if (_workQueue == null || orderIds == null || orderIds.Count == 0) return false;

            var orders = orderIds.Select(id => _activeOrders.FirstOrDefault(o => o.OrderId == id)).ToList();
            // Same rule as the batch path — a partially picked order loads what it has. See
            // CanStartLoading for why "Staged only" was wrong.
            if (orders.Any(o => !CanStartLoading(o, out _))) return false;

            string customerId = orders[0].CustomerId;
            // Still one customer and one door per release, but NO LONGER one lane: staging overflows
            // across a Stage (1A → 1B → 1C), so a customer's orders can legitimately be spread over
            // several lanes of the same door. Group by lane and file one Load task per lane that
            // actually holds something — a single task pointed at one lane would strand every pallet
            // that overflowed into the others.
            if (orders.Any(o => o.CustomerId != customerId || o.AssignedDoorNumber != doorNumber)) return false;
            if (orders.Any(o => string.IsNullOrEmpty(o.AssignedLane))) return false;

            // doorNumber is the DOCK door (the trailer). The lanes may be at other (staging) doors — keep
            // each lane as (staging door, letter) so the Load task can name where to fetch from.
            var lanes = orders.SelectMany(o => o.AllStagingLanes()).Distinct().ToList();
            // Plus any lane at these doors where these orders' staged pallets PHYSICALLY are. A pallet
            // that landed outside the order's assigned lane (seen live 2026-09-23: an OG4104 pallet in
            // 1D with the order assigned to 1E) was never loaded — and the empty 1E pass reverted the
            // order to Staged, which re-released it, every half second.
            var grid = Object.FindAnyObjectByType<PlacementGrid>();
            if (grid != null)
            {
                var ids = new HashSet<string>(orders.Select(o => o.OrderId));
                foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>())
                {
                    if (p == null || !p.IsOnFloor || !ids.Contains(p.OrderId)) continue;
                    if (LaneNamingService.TryGetSlot(grid.WorldToCell(p.transform.position), out var s) &&
                        lanes.Any(l => l.door == s.DoorNumber) && !lanes.Contains((s.DoorNumber, s.Lane)))
                    {
                        lanes.Add((s.DoorNumber, s.Lane));
                        // Record it on the order too. Without this the order still lists only its primary
                        // lane, so every later check that keys off the order's lanes (the loader's revert,
                        // the empty-lane test, close-out) treats the pallets in this lane as foreign:
                        // found live 2026-10-13, the pass over the empty primary lane reverted the order
                        // to Staged while its pallets sat in 1B/1C, and AutoFileLoadTasks then re-filed
                        // Load tasks every half second.
                        orders.FirstOrDefault(o => o.OrderId == p.OrderId)?.AddOverflowLane(s.DoorNumber, s.Lane);
                    }
                }
            }
            lanes = lanes.OrderBy(l => l.door).ThenBy(l => l.lane).ToList();

            bool truckAtDoor = Object.FindObjectsByType<TruckController>()
                .Any(t => t.IsOutbound && t.DockedAt != null && t.DockedAt.DoorNumber == doorNumber);
            if (!truckAtDoor)
                Object.FindAnyObjectByType<TruckYardManager>()?.SpawnOutboundTruck(doorNumber);

            foreach (var order in orders)
                order.Status = OrderData.OrderStatus.Loading;

            foreach (var (stageDoor, lane) in lanes)
            {
                // One live Load task per lane per dock door. AutoFileLoadTasks runs every half second, so
                // without this an order that bounced back to Staged filed a fresh set each poll (hundreds
                // of identical tasks were found in the queue).
                string fromAddress = $"{stageDoor}{lane}";
                if (_workQueue.Tasks.Any(t => t.Type == WorkTaskType.Load &&
                        (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Assigned) &&
                        t.FromLocation == fromAddress && t.ToLocation == doorNumber.ToString()))
                    continue;

                _workQueue.CreateTask(
                    WorkTaskType.Load,
                    EmployeeRole.DockStockerOperator,
                    palletId: null,
                    description: $"Load {customerId} from {stageDoor}{lane} -> Door {doorNumber}",
                    fromLocation: $"{stageDoor}{lane}",
                    toLocation: doorNumber.ToString());
            }

            Debug.Log($"[OrderService] Released {orders.Count} order(s) for {customerId} to loading at dock door {doorNumber} " +
                      $"(lane(s) {string.Join(", ", lanes.Select(l => $"{l.door}{l.lane}"))}) — {lanes.Count} load task(s) filed.");
            return true;
        }

        /// <summary>
        /// Releases Staged orders spanning several customers to loading in one action. Each customer's
        /// orders go to the door their goods are already staged at — nothing is chosen here, the door
        /// was fixed when they were released to a staging lane.
        ///
        /// A trailer holds ONE customer's freight, so this never puts two customers on one truck: the
        /// batch is split by door, and a door is single-customer by construction (a stage is owned by
        /// one customer while occupied). That invariant is re-checked rather than assumed — if a door
        /// somehow holds two customers, this refuses instead of loading a mixed trailer.
        ///
        /// Validates the whole batch before releasing any of it, so one bad order can't leave half the
        /// selection Loading and half Staged.
        /// </summary>
        /// <summary>
        /// May this order start loading?
        ///
        /// PARTIALLY PICKED COUNTS. This used to demand Staged — "every case accounted for" — which
        /// meant an order the warehouse could only half fill could never begin loading at all: the
        /// pallets sat in the lane, the trailer sat at the door, and the player's only move was to
        /// wait for stock that might never come. Real docks load what they have and short the rest.
        ///
        /// What IS required is that something is actually there to load. An order with nothing picked
        /// has nothing in the lane, and sending a loader to fetch it would just be a wasted trip that
        /// then reports the trailer "Loaded" with an empty deck.
        ///
        /// Once loading begins the shortfall is the player's to resolve — top it up from a later
        /// delivery and load again, or close out short and take the satisfaction hit (see
        /// CloseOutOrders).
        /// </summary>
        public bool CanStartLoading(OrderData order, out string whyNot)
        {
            whyNot = null;
            if (order == null) { whyNot = "no longer exists"; return false; }

            if (order.Status != OrderData.OrderStatus.Staged &&
                order.Status != OrderData.OrderStatus.PartiallyPicked)
            {
                whyNot = $"is {order.Status}, not staged or partially picked";
                return false;
            }

            if (order.TotalUnitsPicked <= 0)
            {
                whyNot = "has nothing staged yet — there is nothing to load";
                return false;
            }

            if (order.AssignedDoorNumber <= 0 || string.IsNullOrEmpty(order.AssignedLane))
            {
                whyNot = "has no staging lane recorded";
                return false;
            }

            return true;
        }

        public bool ReleaseOrdersToLoadingBatch(List<string> orderIds, out string failReason)
        {
            failReason = null;
            if (orderIds == null || orderIds.Count == 0) { failReason = "nothing selected"; return false; }

            var byDoor = new Dictionary<int, List<OrderData>>();
            var doorOrder = new List<int>();

            foreach (var id in orderIds)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (order == null) { failReason = $"order {id} no longer exists"; return false; }
                if (!CanStartLoading(order, out string whyNot))
                {
                    failReason = $"{order.CustomerName}'s order {whyNot}";
                    return false;
                }
                if (order.AssignedDoorNumber <= 0 || string.IsNullOrEmpty(order.AssignedLane))
                {
                    failReason = $"{order.CustomerName}'s order has no staging lane recorded";
                    return false;
                }

                if (!byDoor.TryGetValue(order.AssignedDoorNumber, out var list))
                {
                    list = new List<OrderData>();
                    byDoor[order.AssignedDoorNumber] = list;
                    doorOrder.Add(order.AssignedDoorNumber);
                }
                list.Add(order);
            }

            foreach (int door in doorOrder)
            {
                var customersAtDoor = byDoor[door].Select(o => o.CustomerId).Distinct().ToList();
                if (customersAtDoor.Count <= 1) continue;
                failReason = $"Door {door} has orders from {customersAtDoor.Count} customers — a trailer loads one customer";
                return false;
            }

            foreach (int door in doorOrder)
            {
                var ids = byDoor[door].Select(o => o.OrderId).ToList();
                if (ReleaseOrdersToLoading(ids, door)) continue;

                failReason = $"Door {door} could not be released to loading";
                Debug.LogWarning($"[OrderService] Multi-door loading release aborted partway: {failReason}. " +
                                 $"{doorOrder.IndexOf(door)} of {doorOrder.Count} door(s) were already released.");
                return false;
            }

            Debug.Log($"[OrderService] Released {orderIds.Count} order(s) to loading across {doorOrder.Count} door(s): " +
                      string.Join(", ", doorOrder.Select(d => $"{byDoor[d][0].CustomerName} -> Door {d}")));
            return true;
        }

        /// <summary>
        /// Puts back to Staged any order at this door/lane that a load pass finished WITHOUT loading —
        /// i.e. it's still Loading and wasn't in the set the loader actually put aboard.
        ///
        /// This happens when an order's staged pallets aren't findable in its lane: they were dropped
        /// in an aisle because staging was full, or were otherwise moved. TrailerLoadController scans
        /// the lane for OutboundPalletBuilder objects, finds nothing for that order, and completes —
        /// leaving the order in Loading forever. Loading rows carry no enabled checkbox in the Work
        /// Queue panel, so the player has no way to close them out or re-release them; the order is
        /// simply stranded. Reverting to Staged makes it actionable again and surfaces the real
        /// problem (missing pallets) rather than silently wedging the queue.
        /// </summary>
        public int RevertUnloadedOrdersToStaged(int doorNumber, string lane, ICollection<string> loadedOrderIds)
        {
            int reverted = 0;
            foreach (var order in _activeOrders)
            {
                if (order.Status != OrderData.OrderStatus.Loading) continue;
                if (!order.UsesLane(doorNumber, lane)) continue;
                if (loadedOrderIds != null && loadedOrderIds.Contains(order.OrderId)) continue;

                // This pass only covered ONE of the order's lanes. If another of its lanes still has a
                // Load task waiting, that pass will find the pallets -- reverting now would drop the order
                // to Staged and its remaining passes would treat its own pallets as foreign freight.
                var thisLane = $"{doorNumber}{lane}";
                var otherAddresses = new HashSet<string>(order.AllStagingLanes().Select(l => $"{l.door}{l.lane}"));
                otherAddresses.Remove(thisLane);
                if (_workQueue != null && otherAddresses.Count > 0 && _workQueue.Tasks.Any(t => t.Type == WorkTaskType.Load &&
                        (t.Status == WorkTaskStatus.Available || t.Status == WorkTaskStatus.Assigned) &&
                        otherAddresses.Contains(t.FromLocation)))
                    continue;

                order.Status = OrderData.OrderStatus.Staged;
                reverted++;
                ThrottledLog.Warn("os-revert-" + order.OrderId, $"[OrderService] Order {order.OrderId} ({order.CustomerName}) was released to loading " +
                                 $"from {doorNumber}{lane} but no staged pallet for it could be found there — " +
                                 $"reverted to Staged so it can be re-released. Its pallets are most likely sitting " +
                                 $"somewhere other than the lane.");
            }
            return reverted;
        }

        /// <summary>Marks an order Loaded once every pallet it staged has been physically carried onto
        /// its assigned door's trailer — called by TrailerLoadController once its lane-wide load pass
        /// finishes. Distinct from Shipped: billing and trailer departure now wait for the player's
        /// explicit close-out (see CloseOutOrders) instead of firing the instant the AI finishes
        /// stacking pallets.
        ///
        /// <paramref name="palletsLoaded"/> is ADDED to the order's running PalletsShipped rather
        /// than assigned: an order too big for one trailer is loaded across two passes, and each
        /// pass only knows about the pallets it personally carried aboard.</summary>
        public void MarkOrderLoaded(string orderId, int palletsLoaded = 0)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null) return;
            order.Status = OrderData.OrderStatus.Loaded;
            if (palletsLoaded > 0) order.PalletsShipped += palletsLoaded;
            Debug.Log($"[OrderService] Order {orderId} ({order.CustomerName}) loaded onto its trailer — awaiting close-out.");
        }

        /// <summary>Player-triggered close-out for a batch of fully-Loaded orders: bills and ships
        /// each one (see ShipOrder), then — per door touched — releases that door's outbound trailer
        /// to depart once nothing assigned there is still Loading/Loaded (not just the orders in this
        /// batch; a door only clears once its whole load has been closed out). Fails (no changes) if
        /// any order isn't actually Loaded.
        ///
        /// <paramref name="totalBilled"/> reports what the batch actually earned, summed from what
        /// ShipOrder really credited rather than recomputed by the caller — a partially-picked order
        /// bills only the cases that shipped, so any second calculation of "what this sale was worth"
        /// would eventually disagree with the money that changed hands. The UI uses it to show the
        /// figure it just banked (see WorkQueuePanel / MoneyFlightFx). 0 whenever this returns
        /// false.</summary>
        /// <summary>
        /// May this order be closed out and billed?
        ///
        /// THE ONE HARD RULE IS AN EMPTY LANE. The player is free to close out short — that's the
        /// whole point of being allowed to ship what they have — but not while their own freight is
        /// still standing in the staging lane waiting for a loader. Billing a customer for cases that
        /// are demonstrably still on the warehouse floor isn't a trade-off, it's a bug the player
        /// would be able to farm.
        ///
        /// So: Loaded is always fine (the loader finished its pass and the lane is empty by
        /// construction). Loading is fine too, PROVIDED nothing of this order is left in the lane —
        /// that's the case where the trailer took everything there was and the order never reached
        /// Loaded because there was nothing more to fetch.
        /// </summary>
        /// <summary>Satisfaction lost for a completely unfilled order. Scaled by the shortfall, so a
        /// 95%-filled order is a shrug and a 33%-filled one hurts. Placeholder magnitude, like the
        /// rest of the balance numbers.</summary>
        public const float ShortShipSatisfactionPenalty = 30f;

        /// <summary>
        /// Docks customer satisfaction in proportion to how much of their order didn't turn up.
        ///
        /// Charged at SHIP time rather than at close-out, so it lands the same whether the player
        /// closed out short deliberately or the deadline swept a half-filled order out from under
        /// them — the customer's experience is identical either way, and gating it on the close-out
        /// button would let the second route escape unpunished.
        ///
        /// Silent no-op for a full ship, and for anything with no contract behind it.
        /// </summary>
        private static void PenalizeShortShipment(OrderData order)
        {
            if (order == null || order.TotalUnits <= 0) return;

            // Cases the player cancelled off the order were charged at the time ("Cancel Remaining"), so only
            // the shortfall BEYOND those counts here - otherwise one decision would be penalised twice.
            float owed = order.TotalUnits - order.CancelledCases;
            if (owed <= 0f) return;
            float filled = Mathf.Min(1f, order.TotalUnitsPicked / owed);
            if (filled >= 0.999f) return;                       // shipped complete — nothing owed

            if (!ServiceLocator.TryGet(out OrderArrivalService arrivals) || arrivals == null) return;

            float penalty = ShortShipSatisfactionPenalty * (1f - filled);
            arrivals.PenalizeSatisfaction(order.ContractId, penalty);

            Debug.Log($"[OrderService] {order.CustomerName} shipped {filled:P0} filled " +
                      $"({order.TotalUnitsPicked}/{order.TotalUnits}) — satisfaction −{penalty:F1}.");
        }

        // ── CLOSE ROUTE / RETURNS (Tad, 2026-10-22) ─────────────────────────────────────────────────
        // Closing the route mid-way never silently deletes an order. What is on the trailer ships and is billed; every
        // quantity that is not aboard is cancelled off the order and sent back: case-picked pallets standing in a staging
        // lane become RETURNS tasks (a selector reverse-picks them into their pick slots, ReclaimOrphanedFreight), whole
        // pallets go back through putaway. An order whose selector is still mid-pick goes to RETURNS status: the selector
        // puts everything back in reverse pick order (OrderSelectionTaskDriver.Returns.cs), then the order is purged.

        /// <summary>A selector is out on the floor picking this order right now (the PICKING status).</summary>
        public bool IsPicking(OrderData order)
            => order != null && !order.ReturnsMode &&
               LiveTasksForOrder(order.OrderId).Any(t => t.Type == WorkTaskType.OrderSelect && t.Status == WorkTaskStatus.Assigned);

        public bool CanCloseRoute(OrderData order, out string whyNot)
        {
            whyNot = null;
            if (order == null) { whyNot = "no longer exists"; return false; }
            if (order.ReturnsMode) { whyNot = "is already being put back"; return false; }
            switch (order.Status)
            {
                case OrderData.OrderStatus.Staged:
                case OrderData.OrderStatus.Loading:
                case OrderData.OrderStatus.Loaded:
                    return true;
                case OrderData.OrderStatus.PartiallyPicked:
                case OrderData.OrderStatus.Pending:
                case OrderData.OrderStatus.FullyPicked:
                    if (IsPicking(order) || order.TotalUnitsPicked > 0) return true;
                    whyNot = "has nothing picked yet - cancel it instead";
                    return false;
                default:
                    whyNot = $"is {order.Status}";
                    return false;
            }
        }

        /// <summary>What closing these routes would do, in words, for the confirmation modal.</summary>
        public string DescribeCloseRoute(List<string> orderIds)
        {
            int picking = 0, aboard = 0, inLane = 0;
            foreach (var id in orderIds ?? new List<string>())
            {
                var o = _activeOrders.FirstOrDefault(x => x.OrderId == id);
                if (o == null) continue;
                if (IsPicking(o)) { picking++; continue; }
                foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>())
                {
                    if (p == null || p.OrderId != id) continue;
                    if (p.Lifecycle == PalletLifecycle.Shipped) aboard++; else inLane++;
                }
            }
            var sb = new System.Text.StringBuilder("Close the route?\n");
            if (aboard > 0) sb.Append($"{aboard} pallet(s) already on the trailer ship and are billed.\n");
            if (inLane > 0) sb.Append($"{inLane} pallet(s) not loaded yet are cancelled off the order and sent back (RETURNS).\n");
            if (picking > 0) sb.Append($"{picking} order(s) are being picked right now - the selector puts everything back (RETURNS).\n");
            if (aboard + inLane + picking == 0) sb.Append("Nothing is physically out yet - the order closes.\n");
            return sb.ToString().TrimEnd();
        }

        /// <summary>Closes the route for these orders. See the block comment above.</summary>
        public int CloseRoute(List<string> orderIds)
        {
            if (orderIds == null || orderIds.Count == 0) return 0;
            ServiceLocator.TryGet<DockScheduleService>(out var schedule);
            var billedByDoor = new Dictionary<int, int>();
            int handled = 0;

            foreach (var id in orderIds.ToList())
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (!CanCloseRoute(order, out _)) continue;
                string number = order.OrderNumber ?? order.OrderId;
                int door = order.AssignedDoorNumber;

                // Mid-pick: the selector reverses what they have done. Unclaimed work is called off now.
                if (IsPicking(order))
                {
                    order.ReturnsMode = true;
                    CancelLeftoverTasks(id);
                    SystemsLogWindow.LogWarning($"Order {number}: route closed while it was being picked - the selector is putting everything back (RETURNS).");
                    handled++;
                    continue;
                }

                int aboard = 0;
                foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>())
                    if (p != null && p.OrderId == id && p.Lifecycle == PalletLifecycle.Shipped)
                        foreach (var (_, cases) in CasesOn(p)) aboard += cases;

                CancelLeftoverTasks(id);
                if (aboard > 0)
                {
                    // Everything not on the trailer is cancelled off the order - the customer is neither billed for it nor
                    // charged a short-ship penalty for it (the player chose this).
                    order.CancelledCases = Mathf.Max(order.CancelledCases, order.TotalUnits - aboard);
                    int billed = ShipOrder(id);
                    billedByDoor.TryGetValue(door, out int prev);
                    billedByDoor[door] = prev + billed;
                    string msg = $"Route closed for order {number}: {aboard} case(s) on the trailer shipped (${billed:N0}); the rest was cancelled off the order and is going back.";
                    SystemsLogWindow.LogRevenue(msg);
                    ShippedFanfare.Show(number, billed);
                }
                else
                {
                    ArchiveWithoutPenalty(order, schedule);
                    if (!billedByDoor.ContainsKey(door)) billedByDoor[door] = 0;
                    SystemsLogWindow.LogWarning($"Route closed for order {number}: nothing was on the trailer, so the order was closed. Staged pallets are being returned.");
                }
                handled++;
            }

            ReclaimOrphanedFreight();   // files the RETURNS tasks / putaways for whatever is still standing in a lane
            foreach (var kv in billedByDoor) if (kv.Key > 0) TryReleaseDoorIfClear(kv.Key, kv.Value);
            return handled;
        }

        /// <summary>The selector has put the last case back. Purges the order (no reputation hit - the player chose this) and returns any
        /// pallets earlier trips already staged. Waits for any other selector still returning the same order.</summary>
        public void CompleteReturnedOrder(OrderData order, WorkTask finishedTask)
        {
            if (order == null || !_activeOrders.Contains(order)) return;
            if (OtherSelectorsOnOrder(order, finishedTask) > 0) return;
            ServiceLocator.TryGet<DockScheduleService>(out var schedule);
            string number = order.OrderNumber ?? order.OrderId;
            int door = order.AssignedDoorNumber;
            ArchiveWithoutPenalty(order, schedule);
            ReclaimOrphanedFreight();
            SystemsLogWindow.Log($"Order {number} returned to picks Complete.");
            if (door > 0) TryReleaseDoorIfClear(door, 0);
        }

        private void ArchiveWithoutPenalty(OrderData order, DockScheduleService schedule)
        {
            CancelLeftoverTasks(order.OrderId);
            order.ReturnsMode = false;
            order.NoCancelPenalty = true;
            order.AssignedDoorNumber = 0;
            order.StagingDoorNumber = 0;
            order.AssignedLane = null;
            order.OverflowLanes.Clear();
            order.Status = OrderData.OrderStatus.Cancelled;
            StampClosedNow(order);
            schedule?.DetachCancelledOrder(order.OrderId);
            Archive(order);
        }

        public bool CanCloseOut(OrderData order, out string whyNot)
        {
            whyNot = null;
            if (order == null) { whyNot = "no longer exists"; return false; }

            // A SCRATCHES list (the deficit from a short pick) must be picked first - or the rest of the order cancelled.
            if (LiveScratchTask(order.OrderId) != null)
            {
                whyNot = "SCRATCHES still need picking - pick them, or cancel the rest of the order";
                return false;
            }

            if (AnyPalletInHand(order))
            {
                whyNot = "still has a pallet being moved - it will be closable once that pallet is down";
                return false;
            }

            if (order.Status == OrderData.OrderStatus.Loaded)
            {
                // An order staged across several lanes is marked Loaded after the FIRST lane's pass; the
                // other lanes' Load tasks may still be on their way. Wait for them.
                if (StagedPalletsRemainingInLane(order) > 0 && _workQueue != null)
                {
                    var addresses = new HashSet<string>(order.AllStagingLanes().Select(l => $"{l.door}{l.lane}"));
                    bool loaderComing = _workQueue.Tasks.Any(t => t.Type == WorkTaskType.Load &&
                        t.Status != WorkTaskStatus.Complete && t.Status != WorkTaskStatus.Cancelled &&
                        addresses.Contains(t.FromLocation));
                    if (loaderComing)
                    {
                        whyNot = "still has pallets in another staging lane - the loader hasn't finished it yet";
                        return false;
                    }
                }
                return true;
            }

            if (order.Status != OrderData.OrderStatus.Loading)
            {
                whyNot = $"is {order.Status} — it has to be loading or loaded before it can be closed out";
                return false;
            }

            if (StagedPalletsRemainingInLane(order) > 0)
            {
                whyNot = "still has pallets standing in its staging lane — load them first";
                return false;
            }

            return true;
        }

        /// <summary>
        /// How many staged pallets are still sitting in this order's lane.
        ///
        /// Counted from the world rather than from the order's own numbers, because the order records
        /// cases and the lane holds pallets, and only the physical objects know which is actually
        /// still there. Mirrors TrailerLoadController.FindStagedPalletsInLane, including its rule that
        /// a parented pallet is mid-carry and therefore not standing in the lane.
        /// </summary>
        /// <summary>On forks or inside a trailer. A pallet parented to PlacedObjectsContainer (save-restored)
        /// is standing on the floor, not carried.</summary>
        /// <summary>(SKU, cases) on an outbound pallet: per-SKU counts for a case-picked pallet, or the whole pallet's
        /// SKU and quantity (PalletData) for an adopted full pallet.</summary>
        private static IEnumerable<(string sku, int cases)> CasesOn(OutboundPalletBuilder p)
        {
            if (p.CaseCountsBySku.Count > 0)
            {
                foreach (var kv in p.CaseCountsBySku)
                    if (!string.IsNullOrEmpty(kv.Key) && kv.Value > 0) yield return (kv.Key, kv.Value);
                yield break;
            }
            var pd = p.GetComponent<PalletData>();
            if (pd != null && !string.IsNullOrEmpty(pd.ItemNumber) && pd.CaseQuantity > 0)
                yield return (pd.ItemNumber, pd.CaseQuantity);
        }

        private static bool IsCarriedOrInTrailer(OutboundPalletBuilder pallet)
        {
            var parent = pallet.transform.parent;
            return parent != null && parent.name != "PlacedObjectsContainer";
        }

        public int StagedPalletsRemainingInLane(OrderData order)
        {
            if (order == null || !order.AllStagingLanes().Any())
                return 0;

            var grid = Object.FindAnyObjectByType<PlacementGrid>();
            if (grid == null) return 0;

            int count = 0;
            foreach (var pallet in Object.FindObjectsByType<OutboundPalletBuilder>())
            {
                // Mid-carry / in a trailer. A save-restored pallet sits under PlacedObjectsContainer and IS in
                // the lane: skipping every parented pallet let an order auto-ship (and bill) with 40 cases still
                // standing in its lane (2026-10-17 playtest).
                if (pallet == null || IsCarriedOrInTrailer(pallet)) continue;
                if (!string.IsNullOrEmpty(pallet.OrderId) && pallet.OrderId != order.OrderId) continue;
                var cell = grid.WorldToCell(pallet.transform.position);
                if (!LaneNamingService.TryGetSlot(cell, out var slot)) continue;
                if (!order.UsesLane(slot.DoorNumber, slot.Lane)) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Ships every Loaded order with nothing left standing in its lane(s) -- no player click.
        /// Called by the loaders the moment a load pass is finished and the dock stocker has been
        /// released, so the driver can't pull away while the loader is still on the trailer.
        ///
        /// Both guards matter: <see cref="CanCloseOut"/> holds an order that is Loaded after only its
        /// first lane's pass while another lane's loader is still coming, and the empty-lane check
        /// stops a trailer that filled up from paying out on a half-load (the rest of the order is
        /// still on the floor). Such an order stays Loaded, and the Work Queue's Close Out button
        /// remains as the manual fallback.
        ///
        /// Each shipped order is announced in the Systems Log and with the big on-screen payout
        /// (ShippedFanfare). Returns how many orders were shipped.
        /// </summary>
        /// <summary>
        /// A trailer that can't take another pallet ships with what's aboard (2026-10-17 playtest - a full trailer used
        /// to wait forever for lanes that could never empty). Every order with freight in it stops picking, is closed
        /// out, and is billed for the pallets in the trailer only (ShipOrder bills SHIPPED pallets). Pallets that didn't
        /// fit are archived with the order: ReclaimOrphanedFreight returns whole pallets to putaway and files a Return
        /// for case-picked ones, so a selector puts the cases back in their pick slots.
        /// </summary>
        public int ShipCargoFullTrailer(TruckController truck)
        {
            if (truck == null) return 0;
            var ids = truck.GetComponentsInChildren<OutboundPalletBuilder>(true)
                .Where(p => p != null && !string.IsNullOrEmpty(p.OrderId)).Select(p => p.OrderId).Distinct().ToList();
            int door = truck.DockedAt != null ? truck.DockedAt.DoorNumber : 0;
            int billedTotal = 0;
            foreach (var id in ids)
            {
                var order = _activeOrders.FirstOrDefault(o => o.OrderId == id);
                if (order == null) continue;
                CancelLeftoverTasks(id);
                int billed = ShipOrder(id);
                billedTotal += billed;
                string number = order.OrderNumber ?? id;
                string msg = $"The trailer at door {door} was full - order {number} shipped with what fit (${billed:N0}); the pallets left behind were not billed and go back to stock.";
                Debug.LogWarning($"[OrderService] {msg}");
                SystemsLogWindow.Log(msg);
                ShippedFanfare.Show(number, billed);
            }
            if (door > 0) TryReleaseDoorIfClear(door, billedTotal);
            ReclaimOrphanedFreight();
            return ids.Count;
        }

        private readonly HashSet<string> _readyForCloseNotified = new HashSet<string>();

        /// <summary>True while any pallet of the order is still being carried: on a selector, a pallet jack, a reach truck's or a dock
        /// stocker's forks. It is neither in a lane nor on the trailer yet, so no lane scan sees it - closing out then ships (and
        /// pays for) the order while freight is still moving, and the late pallet is left behind.</summary>
        public bool AnyPalletInHand(OrderData order)
        {
            if (order == null) return false;
            foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>())
                if (p != null && p.OrderId == order.OrderId && !p.IsOnFloor && p.Lifecycle != PalletLifecycle.Shipped) return true;
            return false;
        }

        // ── Ready to close with scratches (Tad, 2026-10-26) ─────────────────────────────────────────
        // Everything the building could load is on the trailer and what is left is SCRATCHED (waiting on stock that is not here).
        // That is technically ready to close out, so the player is asked (pause + zoom + Hold / Release) instead of the driver
        // waiting forever. Release cancels the scratched cases (the customer is not billed for them; the usual remaining-cases
        // penalty applies) and ships; Hold changes nothing.

        public class ReadyToCloseInfo
        {
            public int Door;
            public TruckController Truck;
            public List<OrderData> Orders = new List<OrderData>();
            public int CasesShort;
            public int ReputationLoss;
            public float PartnershipLoss;
            public string Signature;
            public string Customers => string.Join(", ", Orders.Select(o => o.CustomerName).Distinct());
        }

        /// <summary>Raised once per door per state of its scratches. The UI subscribes; nothing happens if nothing does.</summary>
        public static event System.Action<ReadyToCloseInfo> OnReadyToCloseWithScratches;

        private readonly HashSet<string> _scratchPrompted = new HashSet<string>();

        /// <summary>Doors whose docked outbound trailer holds everything that can be loaded while the rest is scratched.</summary>
        public List<ReadyToCloseInfo> FindScratchReadyDoors()
        {
            var result = new List<ReadyToCloseInfo>();
            var trucks = Object.FindObjectsByType<TruckController>();
            foreach (var grp in _activeOrders.Where(o => (o.Status == OrderData.OrderStatus.Loading || o.Status == OrderData.OrderStatus.Loaded)
                                                         && o.AssignedDoorNumber > 0).GroupBy(o => o.AssignedDoorNumber))
            {
                int door = grp.Key;
                var truck = trucks.FirstOrDefault(t => t != null && t.IsOutbound && t.DockedAt != null && t.DockedAt.DoorNumber == door);
                if (truck == null) continue;

                var info = new ReadyToCloseInfo { Door = door, Truck = truck };
                bool blocked = false;
                foreach (var o in grp)
                {
                    var live = LiveTasksForOrder(o.OrderId).ToList();
                    if (live.Any(t => !t.IsScratch)) { blocked = true; break; }                              // real work still going on
                    if (live.Any(t => t.IsScratch && t.Status != WorkTaskStatus.Open)) { blocked = true; break; }   // a scratch is being (or can be) picked
                    if (AnyPalletInHand(o) || StagedPalletsRemainingInLane(o) > 0) { blocked = true; break; }
                    if (!live.Any(t => t.IsScratch)) continue;                                                // nothing scratched: the normal auto-close handles it

                    var impact = PreviewCancelRemaining(o);
                    if (!impact.Allowed) { blocked = true; break; }
                    info.Orders.Add(o);
                    info.CasesShort += impact.CasesCancelled;
                    info.ReputationLoss += impact.ReputationLoss;
                    info.PartnershipLoss += impact.PartnershipLoss;
                }
                if (blocked || info.Orders.Count == 0) continue;
                info.Signature = door + ":" + string.Join(",", info.Orders.Select(o => o.OrderId)) + ":" + info.CasesShort;
                result.Add(info);
            }
            return result;
        }

        private void CheckScratchPrompts()
        {
            foreach (var info in FindScratchReadyDoors())
            {
                if (!_scratchPrompted.Add(info.Signature)) continue;
                // An unattended HAL block cannot answer a prompt (and must not freeze the game): it releases.
                var bot = GameCore.Diagnostics.PlaytestBot.Instance;
                if (bot != null && bot.Running && !bot.ObserveOnly) { ReleaseScratchedTrailer(info.Door); continue; }
                OnReadyToCloseWithScratches?.Invoke(info);
            }
        }

        /// <summary>The player chose Release: cancel what is scratched on every order riding this door's trailer, then close them out
        /// so the driver leaves. The customer is billed only for what is aboard.</summary>
        public bool ReleaseScratchedTrailer(int door)
        {
            var info = FindScratchReadyDoors().FirstOrDefault(i => i.Door == door);
            if (info == null) return false;

            var ids = new List<string>();
            var numbers = new List<string>();
            foreach (var o in info.Orders)
                if (CancelRemaining(o.OrderId, out _)) { ids.Add(o.OrderId); numbers.Add(o.OrderNumber ?? o.OrderId); }
            if (ids.Count == 0) return false;

            if (!CloseOutOrders(ids, out int billed)) return false;
            string label = string.Join(", ", numbers);
            SystemsLogWindow.LogRevenue($"Door {door}'s trailer was released - order {label} shipped short, ${billed:N0} received. The scratched cases were cancelled and not billed.");
            ShippedFanfare.Show(label, billed);
            return true;
        }

        public int AutoCloseLoadedOrders()
        {
            CheckScratchPrompts();
            var ready = _activeOrders
                .Where(o => o.Status == OrderData.OrderStatus.Loaded
                            && CanCloseOut(o, out _)
                            && !AnyPalletInHand(o)
                            && StagedPalletsRemainingInLane(o) == 0
                            // Nothing still being picked or delivered: a pass can finish while reach
                            // trucks are still carrying this order's pallets to the lane.
                            && !LiveTasksForOrder(o.OrderId).Any(t => t.Type == WorkTaskType.PalletPick || t.Type == WorkTaskType.OrderSelect))
                .ToList();

            int shipped = 0;
            foreach (var order in ready)
            {
                string number = order.OrderNumber ?? order.OrderId;
                int door = order.AssignedDoorNumber;

                // An order the player cut short (Cancel Remaining / scratches) waits for the player's green light:
                // everything loadable is on the truck, the driver stands by until Close Out is pressed. Buzz once.
                if (order.CancelledCases > 0)
                {
                    if (_readyForCloseNotified.Add(order.OrderId))
                    {
                        string msg = $"All of the quantities for order {number} have been filled. You can close the order out in Door {door}.";
                        SystemsLogWindow.LogWarning(msg);
                        UIToast.Show(msg, 4f);
                        AudioManager.Play("NegotiationFail");
                        AudioManager.Play("NegotiationFail");
                    }
                    continue;
                }

                if (!CloseOutOrders(new List<string> { order.OrderId }, out int billed)) continue;
                shipped++;

                // Shipped orders are archived, so what's left at this door is whatever still rides
                // the same trailer. Nothing => the driver was released to leave.
                bool driverDeparting = !_activeOrders.Any(o => o.AssignedDoorNumber == door &&
                    (o.Status == OrderData.OrderStatus.Loading || o.Status == OrderData.OrderStatus.Loaded));

                SystemsLogWindow.LogRevenue(driverDeparting
                    ? $"Order {number} has finished loading and the driver has departed — shipped, ${billed:N0} received."
                    : $"Order {number} has finished loading — shipped, ${billed:N0} received. Driver is waiting on the rest of the load.");
                ShippedFanfare.Show(number, billed);
            }
            return shipped;
        }

        public bool CloseOutOrders(List<string> orderIds, out int totalBilled)
        {
            totalBilled = 0;
            if (orderIds == null || orderIds.Count == 0) return false;

            var orders = orderIds.Select(id => _activeOrders.FirstOrDefault(o => o.OrderId == id)).ToList();
            if (orders.Any(o => !CanCloseOut(o, out _))) return false;

            var revenueByDoor = new Dictionary<int, int>();
            foreach (var order in orders)
            {
                int doorNumber = order.AssignedDoorNumber;
                int revenue = ShipOrder(order.OrderId);
                revenueByDoor.TryGetValue(doorNumber, out int existing);
                revenueByDoor[doorNumber] = existing + revenue;
                totalBilled += revenue;
            }

            foreach (var kvp in revenueByDoor)
                TryReleaseDoorIfClear(kvp.Key, kvp.Value);

            Debug.Log($"[OrderService] Closed out {orders.Count} order(s) — billed ${totalBilled:N0}.");
            return true;
        }

        /// <summary>Shows the closed-out batch's total sale value hovering over its door (same
        /// "Mario-coin" FloatingMoneyText effect used elsewhere), then — if nothing assigned to this
        /// door is still Loading/Loaded — releases the outbound trailer docked there so it can
        /// depart. The other half of the manual close-out flow (see CloseOutOrders).</summary>
        private void TryReleaseDoorIfClear(int doorNumber, int revenueJustBilled)
        {
            var truck = Object.FindObjectsByType<TruckController>()
                .FirstOrDefault(t => t.IsOutbound && t.DockedAt != null && t.DockedAt.DoorNumber == doorNumber);

            if (revenueJustBilled > 0 && truck != null)
                FloatingMoneyText.Show(truck.DockedAt.transform.position + Vector3.up * 2.5f, revenueJustBilled);

            bool stillPending = _activeOrders.Any(o => o.AssignedDoorNumber == doorNumber &&
                (o.Status == OrderData.OrderStatus.Loading || o.Status == OrderData.OrderStatus.Loaded));
            if (stillPending || truck == null) return;

            truck.CompleteLoad();
            Debug.Log($"[OrderService] Door {doorNumber}'s trailer cleared to depart — every order closed out.");
        }

        /// <summary>Finds the next pending order that needs picking.</summary>
        public OrderData GetNextPendingOrder()
        {
            return _activeOrders.FirstOrDefault(o => o.Status == OrderData.OrderStatus.Pending || o.Status == OrderData.OrderStatus.PartiallyPicked);
        }

        /// <summary>
        /// Generates a list of picking tasks for an order based on current inventory.
        /// </summary>
        public List<PickingTask> GeneratePickingTasks(OrderData order)
        {
            var tasks = new List<PickingTask>();
            
            foreach (var lineItem in order.LineItems.Where(li => !li.IsFullyPicked))
            {
                int remaining = lineItem.QuantityRemaining;
                var pallets = _inventoryService.GetPalletsBySku(lineItem.SkuId);

                foreach (var pallet in pallets)
                {
                    if (remaining <= 0) break;

                    int toPick = Mathf.Min(remaining, pallet.Quantity);
                    tasks.Add(new PickingTask(order.OrderId, pallet.PalletId, pallet.CurrentLocation, toPick));
                    remaining -= toPick;
                }
            }

            return tasks;
        }

        public void MarkOrderFulfilled(string orderId)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order != null)
            {
                order.Status = OrderData.OrderStatus.FullyPicked;
                OnOrderFulfilled?.Invoke(order);
                Debug.Log($"[OrderService] Order {orderId} fully picked.");
            }
        }

        /// <summary>Bills the customer for whatever actually shipped (SellingPrice x QuantityPicked
        /// per line item — an order that hit B3's cubing cap partway through only bills for the
        /// units that made it onto a pallet, never the full originally-requested amount) and marks
        /// the order Shipped. Called from CloseOutOrders once the player manually closes out a
        /// Loaded order. Safe to call more than once for the same order (a no-op, returning 0, after
        /// the first) so a bug upstream can't double-charge the customer. Returns the amount billed.</summary>
        public int ShipOrder(string orderId)
        {
            var order = _activeOrders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null)
            {
                // Shipping archives the order, so a repeat call finds it in history, not here. That's
                // the documented no-op path now — only warn if the id is genuinely unknown.
                if (_orderHistory.Any(o => o.OrderId == orderId)) return 0;

                Debug.LogWarning($"[OrderService] ShipOrder: no active order found for {orderId}.");
                return 0;
            }
            if (order.Status == OrderData.OrderStatus.Shipped) return 0;

            // Per-pallet stage: everything of this order that rode the trailer out is now Shipped. The
            // order is archived below, so these pallets leave the Work Queue with it. A pallet still standing
            // in the lane did NOT ship: it is not billed (its cases come off QuantityPicked) and stays Staged so
            // ReclaimOrphanedFreight returns it to stock once the order is archived.
            // BILLING = SHIPPED PALLETS ONLY (Tad, 2026-10-17). A pallet of this order that is not in the trailer (still
            // staged, on the loader's forks, riding a selector) did not ship: its cases come off QuantityPicked so they are
            // not billed, and it stays tagged so ReclaimOrphanedFreight returns it to stock once the order is archived.
            foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>())
            {
                if (p == null || p.OrderId != orderId) continue;
                if (p.Lifecycle == PalletLifecycle.Shipped)
                {
                    p.Stage = OutboundPalletBuilder.PalletStage.Shipped;
                    continue;
                }
                int unbilledTotal = 0;
                foreach (var (sku, cases) in CasesOn(p))
                {
                    var line = order.LineItems.FirstOrDefault(li => li.SkuId == sku && li.QuantityPicked > 0);
                    if (line == null) continue;
                    int unbilled = Mathf.Min(line.QuantityPicked, cases);
                    line.QuantityPicked -= unbilled;
                    unbilledTotal += unbilled;
                }
                if (unbilledTotal > 0)
                    Debug.LogWarning($"[OrderService] Order {order.OrderNumber} closed with a {PalletLifecycleResolver.Label(p.Lifecycle)} pallet " +
                                     $"({unbilledTotal} cs) that never made the trailer - not billed, returning it to stock.");
            }

            int revenue = order.LineItems.Sum(li => li.QuantityPicked * li.SellingPrice);

            // SAME-DAY RUSH BONUS. An order the customer wanted out the door on its own arrival day
            // pays double when it actually makes it — that premium is what makes a rush worth taking,
            // since the deadline leaves no slack at all. Deliberately keyed on the CURRENT day rather
            // than on HasBeenFined: the fine sweep runs at the day roll, so a rush closed out at
            // 23:59 has earned the bonus and a rush closed out one minute later has not, regardless
            // of whether the fine has been charged yet. A rush that slips bills at the ordinary rate
            // and still takes the ordinary late fee — the downside is not doubled, only the upside.
            bool sameDayBonus = _timeService != null && order.QualifiesForSameDayBonus(_timeService.Day);
            if (sameDayBonus)
                revenue = Mathf.RoundToInt(revenue * OrderData.SameDayRushRevenueMultiplier);

            _moneyService?.AddCapital(revenue, FinanceCategory.CasePick);

            // Negotiated handling income (ContractTerms): a fee per case picked, a fee per pallet that went out
            // (staging + loading), and the short-notice fee when the customer broke the notice rule. Booked as
            // Handling, separate from the goods themselves. All zero on a legacy order, so nothing changes there.
            int handling = Mathf.RoundToInt(order.TotalUnitsPicked * order.PickFeePerCase)
                         + Mathf.Max(0, order.PalletsShipped) * order.PalletFee
                         + order.ShortNoticeFee;
            if (handling > 0)
            {
                _moneyService?.AddCapital(handling, FinanceCategory.Handling);
                revenue += handling;
            }

            order.Status = OrderData.OrderStatus.Shipped;
            StampClosedNow(order);
            PenalizeShortShipment(order);
            OnOrderShipped?.Invoke(order);
            // Terminal now — out of the working list before CloseOutOrders asks the door whether
            // anything there is still Loading/Loaded, so a just-shipped order can't hold its own
            // trailer at the dock.
            Archive(order);
            Debug.Log($"[OrderService] Order {orderId} ({order.CustomerName}) shipped — billed ${revenue} for " +
                      $"{order.TotalUnitsPicked} unit(s){(sameDayBonus ? " (SAME-DAY RUSH — revenue doubled)" : "")}.");
            return revenue;
        }

        /// <summary>Fallback late fee for an order that carries no rate of its own — a Dev Console
        /// order, or one restored from a save written before OrderData.LateFeePercent existed.
        ///
        /// Contract-generated orders now carry their OWN rate, stamped at creation from
        /// ContractData.LateFeePercent. Until that landed, every contract advertised a bespoke rate
        /// on its card and then got charged this flat 25% regardless — the terms were decoration.</summary>
        private const float LateFeePercentClerk = 0.25f;

        private void OnDayChanged(string eventId, int newDay)
        {
            // Fine every order that just went overdue and hasn't already been charged — a one-time
            // hit the day it first crosses its due date, not a recurring daily charge. Fires
            // regardless of Status: an order still sitting unreleased in the Work Queue panel is
            // just as late as one that's Staged or Loading. Shipped and Cancelled orders are excluded
            // structurally now, not incidentally — they've been archived out of _activeOrders, so a
            // delivered order can never be fined for going overdue after the fact.
            // ToList() because OnOrderFined listeners are free to touch order state; the enumeration
            // itself is over _activeOrders and nothing here archives, but snapshotting keeps a future
            // listener from invalidating the iterator.
            //
            // THIS IS THE BULK DEADLINE, and the backstop for everything else. A bulk order's deadline
            // is a whole DAY, so midnight is exactly the right moment to judge it. A RECURRING order's
            // deadline is the end of its booked two-hour block and has almost always been judged hours
            // earlier by DockScheduleService.SweepElapsedAppointments — which sets the same
            // HasBeenFined flag, so it's already excluded here and one miss can't be billed twice.
            // A recurring order with no appointment at all still falls through to this sweep, which is
            // the correct floor: it can't be measured against a block it never had.
            foreach (var order in _activeOrders.Where(o => o.IsOverdue(newDay) && !o.HasBeenFined).ToList())
                ChargeLateFee(order, $"is OVERDUE (due day {order.DueDay})");
        }

        /// <summary>
        /// Charges an order's one-time late fee at its OWN contracted rate and announces it.
        ///
        /// One method for both deadline types on purpose. A bulk order misses a day, a recurring order
        /// misses a two-hour block — different triggers, but the same consequence and the same
        /// bookkeeping, and HasBeenFined being the single guard across both is what makes double-
        /// billing structurally impossible rather than something two call sites have to agree about.
        ///
        /// Fires OnOrderFined, which OrderArrivalService.HandleOrderFined rolls into the owning
        /// contract's OrdersLate / LateFeesPaid — so a missed block counts against an account's
        /// on-time rate exactly like a missed day does.
        ///
        /// Returns true only if this call is what charged it, so a caller can pair the money penalty
        /// with a satisfaction penalty without needing to re-derive whether the fine actually landed.
        /// </summary>
        private bool ChargeLateFee(OrderData order, string why)
        {
            if (order == null || order.HasBeenFined) return false;

            float rate = order.LateFeePercent > 0f ? order.LateFeePercent : LateFeePercentClerk;
            int fine = Mathf.RoundToInt(rate * order.TotalRevenue);
            _moneyService?.RemoveCapital(fine, FinanceCategory.Fines);
            order.HasBeenFined = true;
            order.LastFineAmount = fine;
            order.LastFineWhy = why;
            OnOrderFined?.Invoke(order, fine);

            Debug.LogWarning($"[OrderService] Order {order.OrderId} ({order.CustomerName}) {why} — " +
                             $"fined ${fine} ({rate:P0} of ${order.TotalRevenue} order value).");
            return true;
        }

        /// <summary>
        /// The freight was still here when its booked dock block ran out. Charges the same one-time
        /// late fee a day-overdue order takes — see ChargeLateFee — and reports whether this call is
        /// what charged it, so DockScheduleService.SweepElapsedAppointments can dock customer
        /// satisfaction in step with the money and never twice for one miss.
        ///
        /// This is what makes "your deadline is the end of your slot" a real rule rather than a label
        /// on a card. It replaced FineLateLoad, which charged a separate flat 25% under its own
        /// HasBeenLateLoadFined flag and only ever fired for trailers that had already STARTED
        /// loading: a trailer nobody touched all window went entirely unpunished, and the one that was
        /// half-loaded got billed at a rate its contract never advertised. (OrderData.
        /// HasBeenLateLoadFined is kept and still round-trips through saves — dropping the field would
        /// break older save files for a flag nothing reads any more.)
        /// </summary>
        public bool FineMissedDeadline(OrderData order, int blockEndHour)
            => ChargeLateFee(order, $"missed its {blockEndHour:00}:00 dock slot");

        /// <summary>
        /// The player moved this order's trailer off the hour its customer asked for. Charges the same
        /// one-time fee as any other broken promise about when the freight moves, and reports whether
        /// this call is what charged it so the caller can pair it with a satisfaction hit.
        ///
        /// Shares HasBeenFined with the two deadline sweeps ON PURPOSE, rather than getting a flag of
        /// its own. An order rebooked into a worse slot and then also late for it has broken one
        /// promise, not two, and the player has already been shown the price once and accepted it —
        /// billing again when the slot they chose runs out would be charging twice for a single
        /// decision. First fine wins; the rest is already-paid-for consequence.
        /// </summary>
        public bool FineMovedOffRequestedSlot(OrderData order, int blockStartHour)
            => ChargeLateFee(order, $"was moved off its requested slot to {blockStartHour:00}:00");

        /// <summary>
        /// Moves a finished order out of the working list and into history.
        ///
        /// Called the instant an order goes terminal (Shipped or Cancelled) rather than swept up
        /// later, so a closed-out order leaves the Work Queue immediately. Without this, terminal
        /// orders accumulated in _activeOrders forever — 67 in one observed session, nearly all
        /// finished — and every one of them was re-scanned by WorkQueuePanel.BuildLiveSignature four
        /// times a second and re-serialised into every save. Harmless at a few hand-made orders per
        /// session; compounding without limit once orders arrive on a schedule.
        ///
        /// Safe to call from a loop over any collection EXCEPT _activeOrders itself.
        /// </summary>
        /// <summary>Records the in-game moment an order went terminal, for the Work Queue's Completed
        /// tab. Written once, at the transition itself, rather than derived later — nothing else in
        /// the save records when a shipment left, and a figure recomputed from anything downstream
        /// would just be a guess.
        ///
        /// Leaves the stamp at -1 if the clock isn't available, which the tab renders as "—". A
        /// missing timestamp is honest; day 0 would be a lie that also sorts to the bottom.</summary>
        private void StampClosedNow(OrderData order)
        {
            if (order == null || _timeService == null) return;
            order.ClosedDayNumber = _timeService.Day;
            order.ClosedMinuteOfDay = _timeService.Hour * 60 + _timeService.Minute;
        }

        private void Archive(OrderData order)
        {
            if (order == null) return;

            // A short-shipped order can leave pallet picks nobody will ever need; left Available, a reach truck
            // pulls those pallets out of the racks into a lane for an order that no longer exists.
            CancelLeftoverTasks(order.OrderId);
            _activeOrders.Remove(order);
            CancelIdleLoadTasksForUnusedLanes();
            _orderHistory.Add(order);

            // Oldest-first trim: history is append-ordered, so index 0 is the longest-finished.
            int excess = _orderHistory.Count - MaxArchivedOrders;
            if (excess > 0) _orderHistory.RemoveRange(0, excess);
        }

        /// <summary>Flatten orders for saving — active first, then history, in one list. Both go into
        /// the same OrderSnapshot list ON PURPOSE: Status already distinguishes them, so Import can
        /// sort them back out, and the save schema needs no new field. That also means an existing
        /// save full of un-retired terminal orders migrates itself on the next load.
        ///
        /// _assignedTasks is intentionally not persisted — nothing in the codebase writes to it yet
        /// (reserved for future picking-task assignment tracking), so there's nothing real to
        /// snapshot.</summary>
        public List<OrderSnapshot> Export()
        {
            var list = new List<OrderSnapshot>();
            foreach (var o in _activeOrders.Concat(_orderHistory))
            {
                var snap = new OrderSnapshot
                {
                    orderId = o.OrderId,
                    orderNumber = o.OrderNumber,
                    customerId = o.CustomerId,
                    customerName = o.CustomerName,
                    deliveryAddress = o.DeliveryAddress,
                    createdDayNumber = o.CreatedDayNumber,
                    dueDay = o.DueDay,
                    createdTimeMinute = o.CreatedTimeMinute,
                    status = (int)o.Status,
                    paymentMethod = (int)o.PaymentMethod,
                    assignedDoorNumber = o.AssignedDoorNumber,
                    stagingDoorNumber = o.StagingDoorNumber,
                    releasedToFloor = o.ReleasedToFloor,
                    returnsMode = o.ReturnsMode,
                    cancelledCases = o.CancelledCases,
                    assignedLane = o.AssignedLane,
                    overflowLanes = o.OverflowLanes.Select(l => new StagingLaneRef { door = l.door, lane = l.lane }).ToList(),
                    hasBeenFined = o.HasBeenFined,
                    hasBeenLateLoadFined = o.HasBeenLateLoadFined,
                    contractId = o.ContractId,
                    lateFeePercent = o.LateFeePercent,
                    // isWholesale deliberately left false/unwritten — that field is retired, folded
                    // into isBulk. Only Import still reads it, for saves written before the merge.
                    isBulk = o.IsBulk,
                    closedDayNumber = o.ClosedDayNumber,
                    closedMinuteOfDay = o.ClosedMinuteOfDay,
                    palletsShipped = o.PalletsShipped,
                    pickFeePerCase = o.PickFeePerCase,
                    palletFee = o.PalletFee,
                    shortNoticeFee = o.ShortNoticeFee,
                    usesPalletPlan = o.UsesPalletPlan
                };
                foreach (var li in o.LineItems)
                {
                    snap.lineItems.Add(new OrderLineItemSnapshot
                    {
                        skuId = li.SkuId,
                        quantityNeeded = li.QuantityNeeded,
                        quantityPicked = li.QuantityPicked,
                        unitCost = li.UnitCost,
                        sellingPrice = li.SellingPrice
                    });
                }
                list.Add(snap);
            }
            return list;
        }

        /// <summary>Restores orders from a save file, sorting each into the working list or history
        /// by its saved Status. Deliberately does NOT fire OnOrderArrived — that event exists to
        /// notify UI/employees of a genuinely NEW order, and a restored order isn't new.
        ///
        /// This split is also the migration path for saves written before archiving existed: their
        /// terminal orders were all stored as "active", and land in history on the next load without
        /// anything having to rewrite the file.</summary>
        public void Import(List<OrderSnapshot> entries)
        {
            _activeOrders.Clear();
            _orderHistory.Clear();
            _assignedTasks.Clear();
            if (entries == null) return;

            foreach (var snap in entries)
            {
                if (snap == null) continue;

                var order = new OrderData(snap.orderId, snap.customerId, snap.customerName, snap.deliveryAddress, snap.createdDayNumber, snap.dueDay, snap.createdTimeMinute, (OrderData.OrderStatus)snap.status, (OrderData.OrderPaymentMethod)snap.paymentMethod)
                {
                    AssignedDoorNumber = snap.assignedDoorNumber,
                    StagingDoorNumber = snap.stagingDoorNumber,
                    ReturnsMode = snap.returnsMode,
                    ReleasedToFloor = snap.releasedToFloor || snap.assignedDoorNumber != 0, // old saves: released iff a door was stamped
                    AssignedLane = snap.assignedLane,
                    HasBeenFined = snap.hasBeenFined,
                    HasBeenLateLoadFined = snap.hasBeenLateLoadFined,
                    ContractId = snap.contractId,
                    // 0 means the field wasn't in the file — keep the old flat rate rather than
                    // silently making a legacy order free to be late.
                    LateFeePercent = snap.lateFeePercent > 0f ? snap.lateFeePercent : LateFeePercentClerk,
                    // A save written before Wholesale merged into Bulk carries isWholesale=true and
                    // isBulk=false — fold it forward so that order keeps getting full-pallet
                    // fulfilment instead of silently becoming an ordinary case-pick order on load.
                    IsBulk = snap.isBulk || snap.isWholesale,
                    // Day numbers start at 1, so 0 can only mean "this save predates the field".
                    ClosedDayNumber = snap.closedDayNumber > 0 ? snap.closedDayNumber : -1,
                    // The DAY decides whether the pair was recorded, never the minute's own value —
                    // 0 is a legitimate minute (midnight), so it can't stand in for "unset" here.
                    ClosedMinuteOfDay = snap.closedDayNumber > 0 ? snap.closedMinuteOfDay : -1,
                    PalletsShipped = snap.palletsShipped,
                    PickFeePerCase = snap.pickFeePerCase,
                    PalletFee = snap.palletFee,
                    ShortNoticeFee = snap.shortNoticeFee,
                    UsesPalletPlan = snap.usesPalletPlan
                };
                order.CancelledCases = snap.cancelledCases;
                if (snap.overflowLanes != null)
                    foreach (var l in snap.overflowLanes)
                        if (l != null) order.AddOverflowLane(l.door, l.lane);
                foreach (var liSnap in snap.lineItems)
                {
                    var li = new OrderLineItem(liSnap.skuId, liSnap.quantityNeeded, liSnap.unitCost, liSnap.sellingPrice)
                    {
                        QuantityPicked = liSnap.quantityPicked
                    };
                    order.LineItems.Add(li);
                }
                // Backfill for a save written before OrderNumber existed — but only for an order that
                // had already been released (AssignedDoorNumber != 0). An order still awaiting release
                // legitimately has no OrderNumber under the current model (see ReleaseOrdersToLane);
                // minting one here would let it collide with, or get skipped by, the real one release
                // mints later. A released legacy order is backfilled the same way a new one is minted,
                // off however many active+history orders already carry a number for this day/area,
                // which by construction can never collide because Import processes the file in the
                // same order Export wrote it.
                order.OrderNumber = !string.IsNullOrEmpty(snap.orderNumber)
                    ? snap.orderNumber
                    : (order.AssignedDoorNumber != 0 ? GenerateOrderNumber(order) : null);
                bool terminal = order.Status == OrderData.OrderStatus.Shipped
                             || order.Status == OrderData.OrderStatus.Cancelled;
                if (terminal) _orderHistory.Add(order);
                else _activeOrders.Add(order);
            }

            // Trim once, after the whole file is in, rather than per-add: a legacy save can carry far
            // more than the cap and the oldest are the ones to drop.
            int excess = _orderHistory.Count - MaxArchivedOrders;
            if (excess > 0) _orderHistory.RemoveRange(0, excess);

            if (_activeOrders.Count > 0 || _orderHistory.Count > 0)
                Debug.Log($"[OrderService] Restored {_activeOrders.Count} active order(s) " +
                          $"and {_orderHistory.Count} finished order(s) in history.");
        }

        /// <summary>
        /// Reconciles restored orders against the outbound pallets that actually exist in the scene,
        /// and hands any phantom back to picking. Call once at the END of a load, after pallets and
        /// work tasks are restored.
        ///
        /// An order's Status/AssignedDoorNumber/AssignedLane persist, but the physical staged pallets
        /// do NOT — there is no OutboundPalletSnapshot, and OutboundPalletBuilder instances are plain
        /// world objects the save never captures. A save taken while orders sat staged therefore
        /// reloads into a world where the Work Queue reports a lane holding freight that isn't there:
        /// the lane reads Staged, the dock is empty, and releasing it to loading files a Load task the
        /// loader can never satisfy — leaving a truck parked at the door indefinitely, since
        /// TruckController.KeepDockAlive() suppresses the empty-trailer timeout while a task is
        /// outstanding.
        ///
        /// Any order claiming its goods are in a lane (Staged or Loading) with no matching pallet is
        /// reset to Pending and unreleased, its QuantityPicked cleared, and a fresh OrderSelect task
        /// filed in Open so the player can release it again. The cases picked before the save are
        /// genuinely gone — that rack decrement DID persist — so re-picking from current stock is the
        /// honest recovery, not a duplicate.
        ///
        /// Loaded orders are reported but NOT reset: their pallets were inside a trailer that also
        /// didn't persist, but they represent finished work awaiting billing, and voiding that revenue
        /// isn't a decision to make silently on a load.
        /// </summary>
        public int ReconcileStagedOrdersAgainstScene(ICollection<string> onlyOrderIds = null)
        {
            var livePalletOrderIds = new HashSet<string>();
            foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>())
                if (p != null && !string.IsNullOrEmpty(p.OrderId))
                    livePalletOrderIds.Add(p.OrderId);

            int reset = 0;
            var strandedLoaded = new List<string>();
            var vacatedLanes = new HashSet<string>();

            foreach (var order in _activeOrders)
            {
                if (order == null) continue;
                if (onlyOrderIds != null && !onlyOrderIds.Contains(order.OrderId)) continue;
                if (livePalletOrderIds.Contains(order.OrderId)) continue;

                if (order.Status == OrderData.OrderStatus.Loaded)
                {
                    strandedLoaded.Add($"{order.OrderId} ({order.CustomerName})");
                    continue;
                }
                if (order.Status != OrderData.OrderStatus.Staged &&
                    order.Status != OrderData.OrderStatus.Loading)
                    continue;

                Debug.LogWarning($"[OrderService] Order {order.OrderId} ({order.CustomerName}) restored as " +
                                 $"{order.Status} at {order.EffectiveStagingDoor}{order.AssignedLane}, but none of its " +
                                 $"staged pallets exist in the scene (staged pallets aren't persisted). Returning it to " +
                                 $"picking — {order.TotalUnitsPicked} previously picked case(s) are gone and must be " +
                                 $"re-picked from current stock.");

                if (order.Status == OrderData.OrderStatus.Loading)
                    foreach (var vl in order.AllStagingLanes()) vacatedLanes.Add($"{vl.door}{vl.lane}");

                order.Status = OrderData.OrderStatus.Pending;
                order.AssignedDoorNumber = 0;
                order.StagingDoorNumber = 0;
                order.ReleasedToFloor = false;
                order.AssignedLane = null;
                order.OverflowLanes.Clear();
                foreach (var li in order.LineItems)
                    li.QuantityPicked = 0;

                // Reuse surviving tasks if the work-queue snapshot restored any, rather than filing a
                // second set for the same order. If the order had already been staged its tasks were
                // Complete and won't have been exported at all — file fresh ones, Open, exactly as
                // ReceiveOrder does (minus OnOrderArrived: this order isn't new).
                var surviving = LiveTasksForOrder(order.OrderId).ToList();
                if (surviving.Count > 0)
                {
                    foreach (var t in surviving)
                    {
                        t.Status = WorkTaskStatus.Open;
                        t.AssignedToEmployeeGuid = null;
                        // A PalletPick's source is re-resolved on the next claim; a stale PalletId from
                        // before the reload would point at a pallet this order no longer holds.
                        if (t.Type == WorkTaskType.PalletPick) t.PalletId = null;
                    }
                }
                else
                {
                    // QuantityPicked was just zeroed above, so this hands the order the same
                    // placeholder task ReceiveOrder would file for a brand-new order — see
                    // FilePlaceholderTask. The player has to release it again from scratch, which is
                    // also exactly what re-derives the pallet/remainder split against current stock.
                    FilePlaceholderTask(order);
                }

                reset++;
            }

            // A Load task is keyed by LANE, not by order, and one task can cover several orders' pallets
            // — so it may only be cancelled once no order is still Loading out of that lane. Left
            // behind, it would send a loader and a commandeered dock stocker to an empty lane.
            foreach (var laneAddress in vacatedLanes)
            {
                bool stillLoading = _activeOrders.Any(
                    o => o != null && o.Status == OrderData.OrderStatus.Loading &&
                         o.AllStagingLanes().Any(l => $"{l.door}{l.lane}" == laneAddress));
                if (stillLoading) continue;

                var deadLoads = _workQueue?.Tasks.Where(
                    t => t.Type == WorkTaskType.Load && t.FromLocation == laneAddress
                      && t.Status != WorkTaskStatus.Complete && t.Status != WorkTaskStatus.Cancelled).ToList();
                if (deadLoads == null) continue;

                foreach (var dead in deadLoads)
                {
                    dead.Status = WorkTaskStatus.Cancelled;
                    Debug.LogWarning($"[OrderService] Cancelled Load task for {laneAddress} — every order it " +
                                     $"covered was a phantom, so there is nothing in that lane to load.");
                }
            }

            if (reset > 0)
                Debug.LogWarning($"[OrderService] Returned {reset} phantom staged order(s) to picking after load.");
            if (strandedLoaded.Count > 0)
                Debug.LogWarning($"[OrderService] {strandedLoaded.Count} order(s) restored as Loaded with no pallets in " +
                                 $"the scene — their trailer didn't persist either, so they can't be closed out as-is: " +
                                 $"{string.Join(", ", strandedLoaded)}");
            return reset;
        }
    }

    /// <summary>
    /// Represents a single physical step in fulfilling an order: go here, take this, from that pallet.
    /// </summary>
    public class PickingTask
    {
        public string OrderId { get; private set; }
        public string PalletId { get; private set; }
        public Vector2Int Location { get; private set; }
        public int Quantity { get; private set; }
        public bool IsCompleted { get; set; }

        public PickingTask(string orderId, string palletId, Vector2Int location, int quantity)
        {
            OrderId = orderId;
            PalletId = palletId;
            Location = location;
            Quantity = quantity;
        }
    }
}
