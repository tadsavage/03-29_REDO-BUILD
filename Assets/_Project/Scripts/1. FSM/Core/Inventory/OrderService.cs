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
        private const int PalletPickPriority = 100;   // 2026-10-12: all task types start equal


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

            foreach (var li in order.LineItems)
            {
                int fullPallet = FullPalletCases(li.SkuId);
                if (fullPallet <= 0)
                {
                    Debug.LogWarning($"[OrderService] SKU {li.SkuId} on order {order.OrderNumber} has no " +
                                     $"committed Ti/Hi — its {li.QuantityNeeded} case(s) fall back to a case pick.");
                    looseCases += li.QuantityNeeded;
                    continue;
                }

                int pallets = li.QuantityNeeded / fullPallet;
                looseCases += li.QuantityNeeded % fullPallet;

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

                    var pt = _workQueue?.CreateTask(
                        WorkTaskType.PalletPick,
                        EmployeeRole.ReachTruckOperator,
                        palletId: sourcePalletId,
                        description: $"Pallet pick — {fullPallet} cs of {li.SkuId} for {order.CustomerName}",
                        fromLocation: sourceAddress,
                        toLocation: toLocation,
                        area: area,
                        priority: PalletPickPriority,
                        orderId: order.OrderId,
                        skuId: li.SkuId);
                    if (pt != null) pt.Status = hasLane ? WorkTaskStatus.Available : WorkTaskStatus.Open;   // no lane yet = HELD (Open): unclaimable, and AssignWaitingLanes / the Work Queue can release it
                    palletTasks++;
                }
            }

            if (looseCases > 0)
            {
                var st = _workQueue?.CreateTask(
                    WorkTaskType.OrderSelect,
                    EmployeeRole.OrderSelector,
                    palletId: null,
                    description: $"Select {looseCases} loose case(s) for {order.CustomerName}",
                    orderId: order.OrderId);
                if (st != null) st.Status = hasLane ? WorkTaskStatus.Available : WorkTaskStatus.Open;
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
            return loose;
        }

        // ── Held work: released tasks that are waiting for lane room ──────────────────────────────

        /// <summary>The PalletPick / OrderSelect tasks of a released order that are still Open: filed, but
        /// not claimable because no staging lane had room for them yet.</summary>
        private IEnumerable<WorkTask> HeldTasks(OrderData order)
        {
            if (order == null || !order.ReleasedToFloor) return Enumerable.Empty<WorkTask>();
            return LiveTasksForOrder(order.OrderId).Where(t => t.Status == WorkTaskStatus.Open &&
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
                if (selectorPending) committed += LoosePalletsNeeded(o);
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
            if (StagingLaneAssignmentService.HasInboundTruckDocked(doorNumber))
            {
                Debug.LogWarning($"[OrderService] Stage {doorNumber} has an inbound trailer docked — release refused.");
                return false;
            }
            // Lane-level inbound stock needs no separate check here: TryResolveStartLane picks from
            // LanesInStage, which already excludes any lane holding received pallets.
            if (!StagingLaneAssignmentService.TryResolveStartLane(this, inv, doorNumber, out string startLane))
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
            // Availability is reckoned per STAGE (the whole door): no other customer may use a door
            // this customer is staging at.
            foreach (var l in lanes)
                if (!StagingLaneAssignmentService.IsStageAvailableFor(this, l.door, customerId)) return false;

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
                var select = held.FirstOrDefault(t => t.Type == WorkTaskType.OrderSelect);
                int loose = LoosePalletsNeeded(order);

                foreach (var l in lanes)
                {
                    if (picks.Count == 0 && select == null) break;
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
                if (select != null) caseHeld = true;
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
                    if (t.Status == WorkTaskStatus.Open) t.Status = WorkTaskStatus.Available;
                }
            }
        }

        private int _waitingLaneTick;

        // Every few in-game minutes; AssignWaitingLanes is a no-op (one LINQ pass) when nothing waits.
        private void OnMinuteForLanes(string eventId, GameCore.Events.Payloads.SimulationTimeData _)
        {
            if (++_waitingLaneTick % 3 != 0) return;
            AssignWaitingLanes();
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
                    if (held.Any(t => t.Type == WorkTaskType.OrderSelect)) needed += LoosePalletsNeeded(o);
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
            if (!order.ReleasedToFloor || HeldWorkCount(order) == 0)
            {
                impact.WhyNot = "no part of it is being held back";
                return impact;
            }
            bool somethingReleased = LiveTasksForOrder(order.OrderId).Any(t => t.Status != WorkTaskStatus.Open)
                                     || order.TotalUnitsPicked > 0;
            if (!somethingReleased)
            {
                impact.WhyNot = "none of it has been released yet - cancel the whole order instead";
                return impact;
            }

            impact.CasesCancelled = CasesOwedByHeldWork(order, out impact.TasksCancelled);
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
                sb.Append("The order ships SHORT with what is already released. Held pallets go back to stock.\n");
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

            order.CancelledCases += impact.CasesCancelled;

            if (ServiceLocator.TryGet(out ReputationService reputation) && reputation != null)
                reputation.Add(-impact.ReputationLoss, $"cancelled the remaining {impact.CasesCancelled} case(s) of order {impact.OrderLabel}");
            if (!string.IsNullOrEmpty(order.ContractId) && ServiceLocator.TryGet(out OrderArrivalService arrivals))
                arrivals?.PenalizeSatisfaction(order.ContractId, impact.PartnershipLoss);

            Debug.Log($"[OrderService] Order {impact.OrderLabel} ({order.CustomerName}): player cancelled the remaining " +
                      $"{impact.CasesCancelled} case(s) ({impact.TasksCancelled} held task(s)); it now ships short. " +
                      $"-{impact.ReputationLoss} reputation, -{impact.PartnershipLoss:F0} partnership.");
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
                foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>(FindObjectsSortMode.None))
                {
                    if (p == null || p.transform.parent != null || !ids.Contains(p.OrderId)) continue;
                    if (LaneNamingService.TryGetSlot(grid.WorldToCell(p.transform.position), out var s) &&
                        lanes.Any(l => l.door == s.DoorNumber) && !lanes.Contains((s.DoorNumber, s.Lane)))
                        lanes.Add((s.DoorNumber, s.Lane));
                }
            }
            lanes = lanes.OrderBy(l => l.door).ThenBy(l => l.lane).ToList();

            bool truckAtDoor = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None)
                .Any(t => t.IsOutbound && t.DockedAt != null && t.DockedAt.DoorNumber == doorNumber);
            if (!truckAtDoor)
                Object.FindAnyObjectByType<TruckYardManager>()?.SpawnOutboundTruck(doorNumber);

            foreach (var order in orders)
                order.Status = OrderData.OrderStatus.Loading;

            foreach (var (stageDoor, lane) in lanes)
            {
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

                order.Status = OrderData.OrderStatus.Staged;
                reverted++;
                Debug.LogWarning($"[OrderService] Order {order.OrderId} ({order.CustomerName}) was released to loading " +
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

        public bool CanCloseOut(OrderData order, out string whyNot)
        {
            whyNot = null;
            if (order == null) { whyNot = "no longer exists"; return false; }

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
        public int StagedPalletsRemainingInLane(OrderData order)
        {
            if (order == null || !order.AllStagingLanes().Any())
                return 0;

            var grid = Object.FindFirstObjectByType<PlacementGrid>();
            if (grid == null) return 0;

            int count = 0;
            foreach (var pallet in Object.FindObjectsByType<OutboundPalletBuilder>(FindObjectsSortMode.None))
            {
                if (pallet == null || pallet.transform.parent != null) continue;   // mid-carry
                var cell = grid.WorldToCell(pallet.transform.position);
                if (!LaneNamingService.TryGetSlot(cell, out var slot)) continue;
                if (!order.UsesLane(slot.DoorNumber, slot.Lane)) continue;
                count++;
            }
            return count;
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
            var truck = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None)
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

            _activeOrders.Remove(order);
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
                    shortNoticeFee = o.ShortNoticeFee
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
                    ShortNoticeFee = snap.shortNoticeFee
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
        public int ReconcileStagedOrdersAgainstScene()
        {
            var livePalletOrderIds = new HashSet<string>();
            foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>(FindObjectsSortMode.None))
                if (p != null && !string.IsNullOrEmpty(p.OrderId))
                    livePalletOrderIds.Add(p.OrderId);

            int reset = 0;
            var strandedLoaded = new List<string>();
            var vacatedLanes = new HashSet<string>();

            foreach (var order in _activeOrders)
            {
                if (order == null) continue;
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
