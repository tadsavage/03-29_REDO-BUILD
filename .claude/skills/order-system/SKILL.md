---
description: Working on the order system, inbound AND outbound — customer contracts and negotiation, order arrival, OrderService lifecycle (release, stage, load, close-out, ship, fines), dock appointments/scheduler, purchase orders and shipments, vendors and market pricing, reputation, outbound trucks, staging-lane auto-assign. Use whenever the task mentions orders, contracts, customers, negotiation, wholesale, purchase orders/POs, shipments, vendors, the Broker, appointments, the Scheduler (key 0), Purchasing (key 9), Contracts (key 8), reputation, late fees, close-out, billing, or files OrderService.cs / OrderArrivalService.cs / OrderData.cs / DockScheduleService.cs / ShipmentService.cs / ContractData.cs / NegotiationService.cs / StagingLaneAssignmentService.cs.
---

# Order system (inbound + outbound)

Companion skills: `work-queue` (the task list orders feed), `inventory-system` (pallets, slots, putaway, lanes), `lane_setup` (door/lane naming), `unity:ui-uitk` (general UI rules). Sources of truth are the code and the dated sections of `CLAUDE.md`; this file is the map.

Code lives in `Assets/_Project/Scripts/1. FSM/Core/Inventory/` (services, data) and `.../Core/Labor/` (trucks, dock controllers). Every service is an `IService` fetched with `ServiceLocator.TryGet<T>()`, registered in `GameContext.Awake()`. `OrderArrivalService` is registered LAST (it resolves the others from the locator). `MarketService` is registered AFTER `LoadSkuDatabase`.

## UI hotkeys (from `TopBarUI`, number row)
5 New Item/Slotter, 6 Shift Manager, 7 Work Queue, 8 Contracts (`ContractsPanel`), 9 Purchasing (`PurchasingPanel`), 0 Scheduler (`SchedulerPanel`). The CLAUDE.md keybinding list is partly stale — `TopBarUI.RegisterUI` calls are the truth. Slot Assignment has no key; click a rack. New panel = implement `IUIPanel` + `RegisterUI` + route through `ToggleUI`, or Tab/exclusivity silently ignores it.

## OUTBOUND: customer → order → shipped

### Where demand comes from
- `CustomerData` = identity (26 assets). `ContractData` = an offer's commercial terms, a separate SO. `ContractKind.Recurring` (mixed case-pick orders daily) or `OneOffWholesale` (full pallets only, every line exactly Ti x Hi of one SKU). `ContractRegistry` is loaded from `Resources` by name (works in a built player).
- `ContractData.CreateRuntime(...)` + `OrderArrivalService.AddOffer(contract)` generate offers in code (the Dev cluster on the Customers tab, and the future reputation system, use these). Generated offers persist via `ExportGeneratedOffers/ImportGeneratedOffers`.
- Offers are gated by reputation (`_reputationRequired`); locked ones render greyed on the Customers tab. `CurrentReputation` is resolved lazily — never cache it at Initialize.
- `SignedContract` (persisted in `SaveData.contracts`): `Active` means **taken**, nothing else (a delivered wholesale stays Active forever; `IsWholesale` skip stops it re-firing). Use `RunningAccounts` / `DeliveredWholesale` to split. Per-contract stats (`OrdersDelivered/Late`, `RevenueEarned`, `LateFeesPaid`) are ACCUMULATED, never recomputed from the capped `OrderHistory`.

### Negotiation (recurring offers only; bulk stays one-click)
- `ContractTerms` (on `SignedContract.Terms`, null = legacy): `MarkupPercent`, `PickFeePerCase`, `PalletFee`, `AdvanceNoticeDays`, `ShortNoticeFee`, `LateFeePercent`. `TermCatalog` holds Opening/Standard/Limit/Step per row.
- `NegotiationSession` weighs the terms into one score against a concession budget (`NegotiationService.BudgetFor`: reputation, partnership, difficulty, per-customer mood). Each Send Offer rolls a d20 (`Submit(ask, roll)`); nat 1 / total ≤3 loses the round, nat 20 / ≥26 accepts and sweetens. Sessions live in `OrderArrivalService._negotiations` so closing the window does not reset patience. Walk-away cooldown persists inside `ContractSnapshot`.
- UI: `NegotiationPanel` + `NegotiatorStage`/`NegotiatorGestures` (avatar window), `NegotiationFx`. Tuning knobs: `TermCatalog`, `BudgetFor`, dice thresholds in `NegotiationSession`.

### Arrival timing
- `OrderArrivalService` subscribes to `OnHourChanged` (not OnDayChanged). `GenerateNoticeWindow`: the order for delivery day D is raised `NoticeDays` before D at the contract cutoff hour; at signing the whole window is raised at once. Created today, **due = deliveryDay**. ~12% of delivery days are short notice (fee stamped on the order). `MaintainRecurringSchedule` pre-books trailers through the notice window.
- Legacy accounts (no Terms) use `max(1, LeadTimeDays)` as notice and the old flat pricing.

### Order lifecycle (`OrderService`, `OrderData`)
`OrderStatus { Pending, PartiallyPicked, FullyPicked, Staged, Loading, Loaded, Shipped, Cancelled, Backorder }`
1. `ReceiveOrder` → raises `OnOrderArrived`; files OrderSelect (and PalletPick) tasks. Tasks start **Open** (unreleased).
2. **Release** (`ReleaseOrdersToStages` / `TryPlanStageSpread` / `ReleaseOrdersToLane`): each customer gets its own STAGE (lane). `StagingLaneAssignmentService.TryFindLaneForOrder` auto-picks the first completely empty lane searching outward from the dock door (own lanes, then nearest doors). A lane must be completely empty AND, ideally, hold the whole order; if no single lane does, `TryFindLanesForOrder` takes several empty lanes nearest-first. A lane holds positions x `MaxStackHeight` pallets (2 per position by default); `FreeRoom(door, lane)` = free tiers minus tiers already promised to released work (`CommittedTiers`). `OrderService.AllocateHeldWork` then makes as much work claimable as the lanes fit: pallet picks go 1 tier each to a lane (`ToLocation`), the case pick needs `LoosePalletsNeeded` tiers in the order's PRIMARY lane (set aside first), later lanes become `OrderData.OverflowLanes`. What doesn't fit stays Open ("held"), and the player is told: "Releasing 20 of 25 pallet picks ... due to space limitation in lanes. Free up some space!" `AssignWaitingLanes` (every 3 in-game minutes) releases held work as room opens (own lanes first, then new empty lanes); the Work Queue also lets the player pick a lane ("Release Orders to Floor", `ReleaseHeldWorkToLane`). An order can now span lanes (even at different doors): use `OrderData.AllStagingLanes()` / `UsesLane(door, lane)` instead of reading `AssignedLane` alone; loading files one Load task per lane; `CanCloseOut` waits for the other lanes' loaders. Release is ONLY from the Scheduler button, never from the Work Queue.
3. **Two doors per order:** `AssignedDoorNumber` = DOCK door (trailer appointment; loader/close-out key off it). `StagingDoorNumber` (`EffectiveStagingDoor`) = door whose lane holds the pallets. Anything pairing a door with `AssignedLane` must read `EffectiveStagingDoor`. `ReleasedToFloor` separates "released, waiting for lane" from an unreleased placeholder.
4. Pick → `FinishOrder` resolves ONE lane with room for the whole order (`TryFindStagingLaneForPallets`) so an order can't straddle lanes. Pallet picks: `NotePalletPicked`, `OutstandingPalletPickCases`.
5. **Loading** (`ReleaseOrdersToLoading[Batch]`, `CanStartLoading`): one trailer per door. Load task FromLocation = staging lane (any door), ToLocation = dock door; `TrailerLoadController` handles cross-door loads. `MarkOrderLoaded`, `RevertUnloadedOrdersToStaged`.
6. **Close-out** (`CanCloseOut`, `CloseOutOrders`) → `ShipOrder`: bills goods to Case Pick income and handling (cases x PickFee + pallets x PalletFee + ShortNoticeFee) to Handling income; fires `OnOrderShipped`; **archives the order immediately** (`Archive`, history capped at `MaxArchivedOrders` 250, same snapshot list as active orders). Archive BEFORE `TryReleaseDoorIfClear` asks if the door still has work.
7. Fines: `OnDayChanged` sweep charges `OrderData.LateFeePercent` (stamped from the contract at creation; 0 = legacy flat 25%); `FineMissedDeadline`, `FineMovedOffRequestedSlot`. `OnOrderFined` feeds contract stats and reputation.
- `OrderPickPath` / `OrderSelectionTaskDriver.TryFindBestPickLocation` share one path so the Work Queue From/To preview matches reality.
- **Selector on a pallet jack (2026-10-16, `OrderSelectionTaskDriver.PalletJack.cs`, partial class):** with a free placed `PltJack_FULL` (`MHEOperatorSlot.IsPalletJack`, one selector per jack via a static claim table) the selector walks to it, `AssignOperator` (DriveStyle 2 handlebar pose), an empty CHEP sits on the forks (`SeatPalletOnJack`, local Z -2.00 tip / -0.95), drives (jack's own NavMeshAgent for open floor, scripted finish) to park `ParkOffset` 2.1 m out from each pick face, `StepOff`, scripted walks slot ⇄ pallet carrying a real case model with the `CarryCase` animator bool, stock/QuantityPicked/AddCase change when the case lands, `StepOn`. Staging: drive to `LaneGeometry.ExitPoint`+1 m, face `DepthAxis` (away from the door), take the lane-entry lock, reverse until the tip pallet is over the slot (one cell short if it is stacking), lower, `PlacePalletsAtStagingSlot`, drive out, `VacateOperator`, jack stays parked at the lane mouth. No free jack, no stock, or a failed jack leg → the old on-foot path (pallets ride the selector) so an order never stalls. `MHEAutoOperatorOnPlacement` is now a no-op (jacks no longer spawn a random driver). Pallet-jack riders are NOT saved as boarded (`PlacementSystem` skips `IsPalletJack`). The Carry layer (arms-only mask, authored `Carry_Case` muscle clip) comes from `Tools/Add Carry Layer To Staff Controllers`. Any in-progress OrderSelect claim now refreshes `AssignedAtRealtime` every frame so the 90 s stale sweep can't steal a long pick. Prefer the jack prefab's own transforms: `MountLeft`/`MountRight` (step off/on through these, never across the machine), `LaneLineUp` (rear point placed `LaneEntryMargin` outside the lane before reversing). Pick faces hide one case model per pick, top layer first; an emptied pick-face pallet is removed so replenishment can drop a new one.
- Cancel: `CanCancelOrder`, `CancelOrders` (walks `orderIds`, archives). Cancelled orders hit reputation (`OnOrderCancelled`).
- Save/load: `Export/Import` (active + history in one `OrderSnapshot` list, sorted back by Status on import). Import does NOT fire `OnOrderArrived`. `ReconcileStagedOrdersAgainstScene()` runs at the END of `PlacementSystem.ApplySaveData` — staged pallets and in-flight trucks are NOT persisted, so Staged/Loading orders with no matching pallet go back to Pending. Loaded orders are only reported.

### Dock appointments (`DockScheduleService`, Scheduler key 0)
- A block (`BlockHours` 2, 12/day) reserves a **trailer at a door**. Capacity = outbound-capable doors (a door counts only if ≥1 lane isn't `LaneUsage.Inbound`). Inbound POs consume the same capacity (`BookInboundNow`, `ParkInboundForPo`).
- `TryBook`, `TryAutoPlace` (never past the order's due day), `TryMoveToDoor` with `EvaluateMove` → `MoveVerdict { Free, Costly, Blocked }` (tints slots green/yellow/red while a trailer is held). Orders sharing customer AND contract share a trailer; a different contract gets its own. `SweepElapsedAppointments` fines unfinished elapsed blocks. Persisted in `SaveData.dockAppointments`.
- Penalty rules: recurring orders are pinned to their own day (can move later that day); bulk can go anywhere; moving off the requested hour costs a one-time fine + satisfaction + reputation (2/block, 10/day, cap 30). `OutboundTruckDispatchService` / `TruckYardManager` physically spawn trucks; **a Guard Shack must exist for trucks to arrive**.
- UI gotchas in `SchedulerPanel`: chip layout (name flexes, door number `flexShrink 0`), RELEASE ORDERS button + `ReleaseTerminalFx`, PENALIZED stamp, `ScheduleDaysBack = 2` must match `KeepPastDays`.

## INBOUND: vendor → PO → truck → receiving

- `PurchasingPanel` (key 9) builds a PO from the selected vendor's catalogue. **One PO = one vendor** (switching clears the basket). Every price goes through `UnitPrice(sku)` (market price x vendor `PriceMultiplier`) so quoted == charged.
- `VendorData`/`VendorRegistry` (Resources, by name). Differentiators actually consumed: `PriceMultiplier`, `ShortShipmentChance`, `MinimumOrderCases`. Do not add on-time%/net-30 fields without the mechanic. Seven vendors on a reputation ladder; Broker excluded from `Unlocked/Locked` lists.
- `MarketService`: per-SKU daily mean-reverting price (0.70x–1.35x of `BuyValue`), 7-day history, 3 spot deals/day (not vendor-gated). React to `OnDayChanged` using the event's day, not a fresh clock read.
- `ShipmentService`: `CreatePlayerPurchaseOrder`, `CancelPurchaseOrder`, `PurgeCompleted` (called from `TruckController.BeginDeparture`, `OnDayChanged` and end of `Import`). `ApplySupplierVariance` rolls short shipments ONCE at dispatch (`ShipmentLineItem.Dropped` is a FLAG, `Quantity` stays the ordered amount; player is credited). Salvage loads (`ShipmentData.IsSalvage`) hide the manifest until received and skip variance.
- `BrokerService` (600 rep): contents rolled at OFFER time and persisted (no save-scum). Damaged = `Dropped` + `Salvage = Damaged` line.
- Dock: `TrailerOffloadController` (dock stocker, trailer → inbound lane), `ReceiverReceivingWorkflow`/`ReceivingTaskDriver` (Receiver clipboard/RF gun), `ShipmentReceivingCoordinator`, `ReceivingService` (fallback bulk path). `TruckController.AwaitingOffload` → `CompleteOffload`. If no manned DS, a 45s fallback bulk-receives so the dock can't wedge.
- No Guard Shack: `ShipmentService` warns once per in-game day (toast) instead of erroring hourly.

## Reputation (one number, two consumers)
`ReputationService`: score 0–1000, bands Unknown/Known/Respected/Preferred/Untouchable at 0/100/300/600/850, saved in `SaveData.reputation`. Inputs wired: shipped, fined, cancelled, off-slot moves. NOT wired: dock wait time, contamination. Toast only on BAND change. It gates BOTH vendors and customers; never build a second score. Partnership/satisfaction is per counterparty (`SignedContract.SatisfactionPercent`, `PartnershipTier`).

## Money rules
- One-time costs go through `MoneyService.Deduct(amount, category)`; recurring through `RecordHourlySpend`. Restoring a saved balance uses `RestoreCapital`, never `SetMoney` (it books the delta as an expense).
- Refunds for undelivered freight use `ReverseDeduct`, not income.
- Route new GL lines through `FinanceCategory.ForGLLine`.

## Gotchas that already cost time
- `Active` ≠ running. `Dropped` ≠ deleted. Archive terminal orders immediately or `WorkQueuePanel`'s 250ms signature and the stage-ownership gates slow down.
- A signed one-off wholesale re-fired because `Active` was cleared; do not clear it.
- Wholesale full-pallet fulfilment through the case-pick selector is still the main missing mechanic (`OrderData.IsWholesale` is the flag).
- Runtime contracts not in `ContractRegistry` don't survive reload unless persisted via `GeneratedOfferSnapshot`.
- Late fee: stamp onto the order at creation, don't look up the contract at fine time.
- After editing any service, stop Play first (domain reload nulls `EventManager`/`ServiceLocator`; statics persist because Domain Reload is off).

## Verification
Drive the real services with `Unity_RunCommand` / `execute_code` (e.g. `ServiceLocator.Get<OrderService>()`), then confirm the result in the panel. Simulate any risk/price mechanic a few hundred times before trusting it (the Broker was inert until measured).

## Player cancellation (2026-10-13)
Always preview, then confirm, then do: `PreviewCancel(order)` / `PreviewCancelRemaining(order)` return a `CancelImpact` (allowed, reputation lost, partnership lost, cases) and `OrderService.DescribeCancel` words it for `ConfirmationModal`. Scale (`CancelScale`): by days to the due date (2+: -5 rep/-3 partnership, tomorrow: -10/-6, today: -20/-10, overdue: -25/-15), x1.5 rep and x2 partnership for bulk, scaled by the cancelled share for a partial cancel.
- **Whole order** (`CancelOrdersByPlayer`): only while nothing is claimed (`CanCancelOrder`). Stamps `OrderData.CancelReputationCost` so `ReputationService` charges the scaled amount instead of its flat 25, and charges partnership via `PenalizeSatisfaction`. `CancelOrders` itself stays penalty-free (system sweeps and the Contracts panel's own cancel use it).
- **Cancel Remaining** (`CancelRemaining`): drops HELD work only (needs some work already released), unreserves its pallets, adds the cases to `OrderData.CancelledCases` (persisted) so the short-ship penalty and the reputation fill check at ship time skip them (no double charge); the order then ships short.
- UI: Scheduler tile has CANCEL next to RELEASE ORDERS (unreleased orders only); Work Queue has Cancel Selected and Cancel Remaining, both behind a confirmation that lists the cost.
