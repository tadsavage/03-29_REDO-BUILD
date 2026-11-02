# Contracts, scheduler, purchasing/vendors, reputation (Jul-Aug 2026)

> Archived from CLAUDE.md on 2026-11-02 (verbatim). Historical detail — read only when working in this area. The lean CLAUDE.md holds the rules that always apply.

## Session 2026-07-29 — Contracts (demand origin), order archiving, and a cluster of UI-wiring bugs

### Contract system — where demand comes from in a real build

Replaces the Dev Console's "Create Test Order" button as the player-facing source of orders (that
button stays as a debug override and should NOT be removed).

- **`ContractData`** (`Core/Inventory/`) — SO holding one customer's commercial terms. Deliberately a
  SEPARATE asset from `CustomerData`: that is identity (name/description/icon, 26 assets already
  authored), this is an offer, and the same customer should be able to appear as a small starter
  account early and a punishing one later.
  - `ContractKind.Recurring` — a standing account; rolls mixed case-pick orders daily at `CutoffHour`.
  - `ContractKind.OneOffWholesale` — signed once, delivers once. **Full pallets only**: every line item
    is exactly `Ti x Hi` cases of one SKU, never a partial layer or loose case (Tad's explicit rule).
  - `PayRateMultiplier` scales `SellValue` and IS honoured. **`LateFeePercent` is displayed on cards
    but NOT enforced** — `OrderService.OnDayChanged` still charges the flat `LateFeePercentClerk`
    (25%). Known loose end.
- **`ContractRegistry`** — `Resources`-loaded by name (`ContractRegistry.Load()`) so it resolves in a
  BUILT PLAYER; `AssetDatabase` lookups are editor-only. Asset lives at
  `Assets/_Project/Resources/ContractRegistry.asset`.
- **`OrderArrivalService`** (`IService`, registered LAST in `GameContext` since it resolves the others
  out of the locator) — subscribes to `OnHourChanged`, not `OnDayChanged`: orders landing at a cutoff
  hour create the daily rhythm; midnight arrivals give the player no deadline to feel. `OnDayChanged`
  remains right for the E1 fine sweep.
  - `SignedContract.LastGeneratedDay` is persisted via a new `SaveData.contracts`. Without it a
    save/load either re-runs a day's arrivals or silently skips one.
  - **`Active` means "taken", nothing else.** A delivered wholesale deal stays Active forever; what
    stops it re-firing is the `IsWholesale` skip in `OnHourChanged`. Clearing `Active` (the original
    mistake) made a delivered one-off read as never-signed to `IsSigned`, so the card kept its Sign
    button and every click produced another full trailer.
- **`ContractsPanel`** (key 6) — card list using `CustomerData.Icon` directly (all 26 already
  assigned). Draggable by its title bar; centres on first open only. Matches the house palette
  (navy #141C26, blue border #5C9BC4, orange #B5743A, Lilita One).
  - Card values are written `+$X` — a leading tilde for "approximately" reads as a MINUS sign at this
    font size and made every contract look like a cost.

**KNOWN GAP:** wholesale orders have full-pallet quantities but fulfilment still runs through the
case-pick selector, which walks a 12-pallet trailer off one case at a time. The mechanic that moves
whole pallets from reserve to the staging lane does not exist. This is the main thing standing between
wholesale and it feeling like wholesale.

### Terminal-order archiving (prerequisite for automatic arrivals)

`OrderService` used to keep Shipped/Cancelled orders in `_activeOrders` forever — 67 observed in one
session, nearly all terminal. That list feeds `WorkQueuePanel.BuildLiveSignature` (a string
concatenation over every active order, **every 250ms**), the stage-ownership gates, and
`TryPlanStageSpread`, and it is serialised into every save.

- `Archive(order)` moves an order to `_orderHistory` **the instant it goes terminal** — from
  `ShipOrder` (before `TryReleaseDoorIfClear` asks whether the door still has work, so a just-shipped
  order cannot hold its own trailer) and from `CancelOrders` (safe: that loop walks `orderIds`, not
  `_activeOrders`).
- Bounded by COUNT (`MaxArchivedOrders = 250`), not age — `OrderData` records no closed-on day, and
  save size tracks record count anyway.
- `Export` writes active + history into the SAME `OrderSnapshot` list; `Import` sorts them back by
  Status. No schema change, and **old saves migrate themselves** on next load.

### Pallet rotation — the 180 degree flip, and the fix that made it worse

Symptom: pallets visibly spun 180 degrees on the Y axis when picked up.

**Wrong fix (reverted):** yawing `ForkCarryLocalEuler` to 180. The carry pose applies to EVERY pallet,
so it just moved the flip onto all the pallets that were previously correct.

**Root cause:** lane `DepthAxis` is +Z for every lane and trailers dock at negative Z, so a pallet
**in a trailer** and a pallet **in a lane** genuinely sit 180 degrees apart in world space. Any
ABSOLUTE carry pose is right for exactly one source and wrong for the other.

**Real fix:** `NearestFacing(want, currentForward)` in both `TrailerLoadController` and
`TrailerOffloadController` — seats the pallet square to the carrier but picks whichever of the two
180-degree variants is already closer. A pallet's footprint is symmetric under a 180 yaw, so both park
identically; only the transition was ever visible. Applied at all four transitions (pickup and
set-down, both controllers). Facing must be captured BEFORE parenting.

### Bottom HUD

- **Keybind legend** — orange strip (#B5743A @ 0.15 alpha) above the bar, built in `BuildMenuUI`,
  anchored at `BottomBarHeight` (122 = bar's 120px + 2px border). Strip and every child are
  `PickingMode.Ignore`.
- **Dev HUD docked into the bar.** `BuildMenuUI.Instance.BottomBar` (not `.Root` — parenting to the
  document root still left it absolutely positioned and floating). Drag and close button removed; the
  graphics-preset button stays.
- **PERFORMANCE RULE:** anything with per-frame-changing text inside the bottom bar MUST have a
  **fixed width**. The FPS label at auto width resized every frame and forced a full re-layout of the
  bar (ten category buttons + utility row): **frame rate went 34 -> 2**. Fixed widths restored it to 41.
- **Match sizes by MEASURING, not by copying USS numbers.** `.buildmenu-category-button` is `104px` in
  the sheet but resolves to 112-113 depending on panel scaling. `MatchCategoryButtonHeight()` reads a
  live button's `resolvedStyle.height` after layout.

### Toast always-on-top

`UIToast` lives on the **TopBar** GameObject and its `Awake` is what sets that document's
`sortingOrder` to 999999. But every full-screen panel is built at runtime into **that same document's
root**, so they are siblings of the toast and z-order is SIBLING ORDER — `sortingOrder` never applied.
`Show()` now calls `BringToFront()`, per-show because a panel created later would otherwise overtake
it again.

### Other fixes this session

- **Lane straddle (was stranding pallets + billing for them):** an order's pallets could land in two
  different lanes while `OrderData.AssignedLane` records only one; the loader scans one lane, so the
  other pallet was never loaded, yet `ShipOrder` billed the full picked quantity. New
  `InventoryService.CountFreeStagingSlotsInLane` / `TryFindStagingLaneForPallets` pick ONE lane with
  room for the whole order, resolved once in `FinishOrder`; per-pallet placement is locked to
  `AssignedLane` and cannot overflow.
- **Phantom staged orders after load:** staged pallets are NOT persisted while order status/door/lane
  ARE. `OrderService.ReconcileStagedOrdersAgainstScene()` (called at the END of
  `PlacementSystem.ApplySaveData`, after pallets AND trucks restore) returns Staged/Loading orders
  with no matching pallet to Pending, refiles an Open OrderSelect task, and cancels lane-keyed Load
  tasks nothing needs. `Loaded` orders are reported only — voiding earned revenue is not the guard's
  call. **Real persistence for staged pallets and in-flight trucks still does not exist.**
- **Multi-customer release:** `TryPlanStageSpread`/`ReleaseOrdersToStages` give each customer its own
  STAGE (a stage cannot be shared — staging overflows A->B->C within it);
  `ReleaseOrdersToLoadingBatch` splits by door, one trailer each.
- **Work Queue From/To columns:** Open rows show first/last pick face via new `OrderPickPath`, which
  `OrderSelectionTaskDriver.TryFindBestPickLocation` now also calls so preview and reality cannot
  diverge. Released rows show staging lane -> `Door N`.
- **`MoneyFlightFx`** — UI-Toolkit money sweep on close-out. `FloatingMoneyText` is world-space and
  therefore invisible behind a full-screen modal, which is why close-out felt like it banked nothing.

### Tooling gotchas (cost real time — read before debugging)

- **`Unity_ValidateScript` is a SYNTAX check only.** It returned clean for a file whose
  `SimulationTimeService` reference was unresolvable. "Validates clean" does NOT mean compiles.
  **Real compile gate:** run a trivial `Unity_RunCommand` naming the types just edited — it builds
  against the real game assemblies.
- **`Unity_ReadConsole`'s type filter is broken** — `Types: ["Error","Warning"]` returns 0 entries
  regardless, and it returns OLDEST-first. Use `FilterText` (e.g. `"error CS"`).
- **`SimulationTimeService` is in `GameCore.Economy`**, not `GameCore.Services`.
- **`Unity_RunCommand` blocks `System.Reflection`** — use `AssetDatabase.FindAssets("t:Type")` and
  `SerializedObject` to reach private serialized fields.
- **Verify the thing, not the value you set.** Several fixes this session were reported as done while
  broken because the code was checked and the behaviour was not — registration that never ran, a panel
  parented to the wrong element, a height that did not match. Read the runtime log or measure against
  the real target.

### Still open after this session

1. **End-to-end run never completed** — the stuck-selector backstop (`AiNavigation` no-progress) and
   the loader `DriveInToGrab` travel-cap fix from 2026-07-28 remain UNPROVEN.
2. Full-pallet fulfilment mechanic (the big one).
3. Wholesale offers should REFRESH rather than being permanently spent once delivered (agreed, not
   built).
4. ~~`ContractData.LateFeePercent` not enforced by the fine sweep.~~ **FIXED 2026-08-01** — see below.
5. `ShipOrder` bills `QuantityPicked` with no check that pallets actually made it onto a truck — this
   is what let the lane-straddle bug stay silent.
6. Nothing guards against multiple outbound trucks per door.
7. Dev HUD card sits to the RIGHT of the utility buttons; may want moving into the gap before them.
8. Only 3 `ContractData` assets exist (Starter / HighVolume / WholesaleTrailer). A fuller spread
   across the 26 customers still needs authoring.

---

## Session 2026-08-01 — Contracts panel becomes three tabs; DockScheduleService (appointment book)

**Live-verified in Play mode against Tad's real save** (5 outbound doors, 3 contracts, 2 running
accounts + 1 delivered wholesale). Compiles clean; every claim below was measured, not inferred.
Only the legend reposition (last edit) is unproven — a one-line move of an already-rendered element.

### `ContractsPanel` — Offers / Accounts / Schedule (key `6`)

Retitled **OUTBOUND CONTRACTS**. Customer icons (`CustomerData.Icon`) on all three tabs: 72px on
Offers cards, 44px on Accounts rows, **18px immediately left of the name** on Schedule chips.

- **Offers** — `AvailableOffers` ONLY. Previously the tab listed everything and greyed out what was
  taken, so a delivered wholesale deal squatted there permanently at 45% opacity. An offer you can act
  on and an account you're being judged on are different objects.
- **Accounts** — three stat tiles (committed cases/day, orders delivered, on-time %) over one row per
  running account, then a `COMPLETED DEALS` group for spent wholesale. Per-row: day held, next drop
  time, shipped/late counts, fees paid, earned-to-date, and the only **Cancel** button in the game
  (`OrderArrivalService.Cancel` existed and nothing called it). Cancelling stops FUTURE arrivals only
  — orders already on the board keep their due dates and still fine you.
- **Schedule** — 12 two-hour block rows × N door columns, day pager (−2/+7 around today), legend,
  live capacity readout. Past blocks dim. Full blocks flag `FULL` in red.

**Interaction is select-then-place, not drag.** Click a chip to pick it up, click any open slot to
drop it, click the chip again to abandon. Pointer capture inside a `ScrollView` fights the scroller,
and two-click also works when source and target blocks aren't both on screen.

**Chip layout gotcha (found by screenshotting, not by reading):** name + door in ONE label meant a
long company name pushed `· D3` off the end under `overflow: Hidden`. Name now flexes and clips; the
door number is a separate `flexShrink = 0` element. The door is the one fact on the chip you can't
infer from anything else.

**The footer bug from the screenshot.** `Signed.Count(s => s.Active)` counted a delivered wholesale as
a running account (reported 3 when 2 were). `Active` means "TAKEN", nothing more — a spent one-off
stays Active forever, which is what keeps the Sign button off its card. New
`OrderArrivalService.RunningAccounts` / `DeliveredWholesale` split it properly. Measured live: 2 and 1.

### `DockScheduleService` (new `IService`, `Core/Inventory/`)

**A block reserves a TRAILER AT A DOOR, not paperwork.** That distinction is the whole design. A
contract's `CutoffHour` still governs when orders ARRIVE on the Work Queue; this decides when the
truck to carry them shows up. Door count only constrains anything if the thing being counted
physically occupies a door.

- `BlockHours = 2`, `BlocksPerDay = 12`. `DockAppointment` = day + block + door + kind + customer +
  contract + order ids.
- **Capacity = outbound-capable doors, not all doors.** A door counts only if ≥1 of its shipping lanes
  isn't `LaneUsage.Inbound` (default `Both`, so every door with lanes counts until told otherwise).
  Counting bare doors would let the player "fix" congestion by buying a door they can't stage into.
  Recomputed per call — lanes are placed/deleted through the build FSM at any moment.
- **Inbound POs consume the same capacity.** `ShipmentService.TrySpawnTruck` now calls
  `BookInboundNow`. A dock door doesn't know which way a trailer faces; without this the player books
  every door for outbound at 08:00 and a PO arrives with nowhere to go.
- **Auto-placement on `OrderService.OnOrderArrived`** — first free block from now, never past the
  order's due day (an appointment after the deadline looks handled while guaranteeing the fine).
  Orders sharing a customer AND contract share one trailer; a different contract gets its own, so a
  wholesale drop can't ride on the case-pick trailer.
- Persisted via `SaveData.dockAppointments`; old saves load with an empty schedule and refill.

**Measured live:** 5 bookings filled a block and were assigned doors 1–5 distinctly; the 6th was
refused with `"08:00–10:00 is full — all 5 door(s) are booked."`; a move into a full block failed and
left the original in place (no data loss); 5 real orders auto-placed into exactly 3 trailers.

### Late fees are real now

`OrderData.LateFeePercent` is stamped from `ContractData.LateFeePercent` at order creation and
`OrderService.OnDayChanged` charges THAT instead of the flat `LateFeePercentClerk` 25%. Copied rather
than looked up at fine time so the fine reflects the terms accepted when the order arrived, and so
`OrderService` needs no dependency on the contract system. **0 in a save = unset → falls back to 25%**,
so legacy orders behave exactly as before.

New `OrderData` fields: `ContractId`, `LateFeePercent`, `IsWholesale` (all round-tripped in
`OrderSnapshot`). New `OrderService.OnOrderFined(OrderData, int)` event.

### Per-contract performance is accumulated, not recomputed

`SignedContract` gained `OrdersDelivered` / `OrdersLate` / `RevenueEarned` / `LateFeesPaid`, fed by
`OrderArrivalService` subscribing to `OnOrderShipped` / `OnOrderFined`. **Accumulated deliberately:**
`OrderService.OrderHistory` is capped at 250 and trims oldest-first, so a recomputed "earned to date"
would silently FALL over a long game as early orders aged out. Attribution matches on `ContractId`
only, never falling back to `CustomerId` — one customer may hold several contracts and a wrong
attribution is worse than none. `RevenueEarned` uses billed `QuantityPicked`, matching `ShipOrder`.

### Tab polish + dev-button move (same day, second pass)

- **Tabs redrawn as folder tabs.** First pass drew inactive tabs as bare transparent text and they
  read as a subtitle — nothing suggested there was anything behind them. Now every tab has a card
  face, outline and rounded top corners; the active one is orange, **taller (38 vs 32), and sits 2px
  lower with NO bottom border** so it breaks through the divider and joins the content below. That
  break is what sells it — colour alone just looks like a highlighted word. Counts moved into their
  own dark pill (`Schedule 4/60` was scanning as a four-word title). Tab bar is `Align.FlexEnd` so
  tabs sit ON the divider regardless of individual height.
- **"Offers" tab renamed "Customers"** (enum `Tab.Customers`, `BuildCustomers`). Contracts will
  eventually appear over time rather than sit in a fixed list, so it's a customer board you watch,
  not a catalogue you shop.
- **Dev order triggers moved** out of the Tools window's OUTBOUND SIMULATOR section (deleted from
  `ToolsWindow.uxml`, `Wire` calls removed) onto a small muted **DEV** cluster at the right of the
  Contracts tab row. `CreateTestOrder()` / `SpawnOutboundTruckDebug()` are now **public** on
  `ToolsWindowController` and called through `ToolsWindowController.Instance`; they stay on that
  component because the `CustomerRegistry` they need is a serialized Inspector field, which a
  code-built panel has no way to supply. The cluster hides itself when `Instance` is null.
  **Do NOT remove the Dev Console's other buttons** — only the two outbound ones moved.

### Dev HUD sizing — the real cause was flex-shrink, not the button

Reported as "the FPS element still looks wrong." Measured: the card asked for 310px and **resolved to
262** — the bottom bar is a flex row and was squeezing it, while the fixed-width labels inside refused
to shrink, so the preset button spilled **39px past the card's right edge**. It looked like a button
sizing bug and was a container bug.

- `_panel.style.flexShrink = 0` (the fix), card width 300, and the inner widths are now named
  constants that must sum within it: `CardPadding(12) + FpsLabelWidth(100) + FpsLabelGap(12) +
  StackWidth(160) + CardPadding(12) = 296`. Change one, check the sum.
- FPS font **34 → 29** (~15% down). Cell label `marginBottom` 6 → 10 to lift it clear. Preset button
  is now a fixed 160×40 with `whiteSpace = Normal` so "Mode: Toaster" wraps instead of overrunning.
- Verified after the fix: card 300px wide, button fits with 13px spare, 12px gap to the utility row,
  28px clear of the bar's right edge — nothing else on the bar moved.

### Third pass (same day) — PO purge, toast z-order, TEST CUSTOMER

**Finished POs are retired now.** `ShipmentService.PendingShipments` never dropped anything, so every
departed truck left a red `[Departed]` row in the Dev Console's inbound list forever and every one was
re-serialised into every save — the same leak `OrderService.Archive` fixed for orders. New
`ShipmentService.PurgeCompleted()` removes Departed/Received/Cancelled, called from
`TruckController.BeginDeparture` (immediate — the row vanishes as the truck pulls out), from
`OnDayChanged` (backstop for trucks destroyed mid-route, which never reach BeginDeparture), and at the
end of `Import()` (**this is what clears an existing save's backlog on first load**). Safe because a
truck holds a direct `AssignedShipment` reference, not a list lookup. Verified: 0 finished POs left in
the list after loading Tad's save.

**Toast z-order — the previous fix was raising the wrong element.** `ToastLabel` is NESTED inside a
wrapper in the UXML, so `_toast.BringToFront()` only reordered it against its siblings *inside that
wrapper*; the wrapper itself stayed where it was in the root's child list, still under the panels. New
`RaiseAboveEverything()` walks the whole ancestor chain calling `BringToFront()` at each level.
Measured after the fix: common ancestor `TopBar-container` (21 children), toast branch at index 20 vs
modal branch at 18 — and confirmed visually with a toast drawn over the open Contracts modal.
Deliberately NOT reparenting the label to the root: the wrapper may carry USS that positions it.

**`TEST CUSTOMER` replaces `TEST ORDER`, and it now adds an OFFER, not orders.** Tad's call. It puts
one new signable customer on the Customers tab — the debug stand-in for reputation-driven arrival.
- `ContractData.CreateRuntime(...)` — new static factory that builds a ContractData in memory instead
  of from an authored asset. **This is the API the reputation system will use**; the dev button is
  just its first caller. `contractId` becomes `.name`, because `ContractId => name`.
- `OrderArrivalService.AddOffer(contract)` — appends to the catalog, refusing a duplicate ContractId
  rather than shadowing (two entries under one id would make `GetContract` depend on list order).
- `ToolsWindowController.CreateTestCustomerOffer()` — picks a customer with no offer already on the
  board, rolls varied terms (1-in-4 wholesale), returns the company name. Lives there because the
  `CustomerRegistry` is a serialized field on that component.
- **CAVEAT, documented in code:** a runtime contract isn't in the `ContractRegistry`, so it does NOT
  survive save/load. Signing one and reloading leaves a `SignedContract` whose id resolves to nothing;
  `OnHourChanged` already warns and skips. Fine for a debug trigger — the real feature must persist
  generated offers alongside `SaveData.contracts`.
- The DEV cluster moved out of the tab row into the **Customers tab content** (top-right, beside the
  intro text). It was on screen while reading the Schedule, where it means nothing.

Verified live: two presses produced `The Cracker Barrel Caravan` and `Puff & Stuff Pastries` with
genuinely different terms (x1.44 pay / 34% fee / 98 cases/day vs x1.38 / 23% / 182), both immediately
signable, tab badge going 0 → 2.

**Dev HUD, second pass:** FPS 29 → **26**, and the Cell label is now `MiddleCenter` inside its
`StackWidth` box so it sits centred over the preset button instead of jammed left.

### Fourth pass — Fill Rate shorts popup + Schedule horizontal scroll

**`OrderShortsPopup`** (`UI_UX/OrderShortsPopup.cs`) — click the Work Queue's **Fill Rate** cell to see
what was CUT. "16 / 23" says an order shipped short but not WHAT was short, and that's the
operationally useful part: one SKU 8 cases light is a different problem from eight lines 1 case light.
Lists only lines with a shortfall (a fully-picked line isn't "cut" and listing it buries the two that
were), sorted worst-first, with a footer totalling lines short / cases cut / revenue not billed.

- Floats over the Work Queue rather than replacing it — it's an inspector for a row you're still
  reading. Draggable by its title bar, red ✕ top-right.
- The Fill Rate cell is underlined by hover-colour and carries a tooltip; an unmarked clickable cell
  in a table of dead ones is a hidden feature. `evt.StopPropagation()` so the click can't reach the
  row checkbox.
- Built lazily on first click (`_shortsPopup ??= new ...`) — most sessions never open it.

**Closing it needed a new concept: AUXILIARY panels.** `UIKeyBindingManager._uiPanels` is keyed by
hotkey number and all nine slots are taken, but Tab (→ `CloseAll`) only iterates that registry. New
`RegisterAuxiliary` / `UnregisterAuxiliary` / `CloseAuxiliaries()` / `AnyAuxiliaryOpen` handle
hotkey-less sub-popups: `CloseAll` now closes them first, and `TopBarUI`'s escape chain calls
`CloseAuxiliaries()` at the TOP — **its bool return is what stops the same Escape falling through and
opening the pause menu behind the popup.** Any future sub-popup should register the same way.

**Schedule tab scrolls horizontally with a stationary frame.** Two changes:
1. New `_tabHeader` element in the modal, **between the tab bar and the ScrollView**. The Schedule
   tab's day switcher and colour legend go there, so they don't slide away when the grid scrolls.
   Other tabs leave it empty and it collapses. Measured: scrolling right 200px moved the header 0px.
2. `_content.mode` flips to `VerticalAndHorizontal` on the Schedule tab only, AND the grid's cells
   became **fixed widths with flexShrink 0** (`TimeColWidth 92 / SlotWidth 168 / FullFlagWidth 44`).
   **This second half is the load-bearing one:** slots were `flexGrow 1 / flexBasis 0`, so they shared
   whatever width existed and squeezed to nothing rather than overflowing — the horizontal scrollbar
   could never appear no matter how many doors there were. Verified at 5 doors: row 1013px vs 978px
   viewport, h-scroller displayed.

Verified live end-to-end: popup listed exactly the two short lines out of three (Canned Tomato 12 cut,
Bread 8 cut, Water fully picked and correctly omitted), footer `2 line(s) short · 20 case(s) cut ·
$220 not billed` matching by hand; Tab and Escape both closed it; a 429-case order scrolled within the
popup's own list.

### Contract arrival by REPUTATION — designed 2026-08-01, NOT built

Tad's direction for how contracts should appear once the loop is real. Recording it here so the
Customers tab gets built toward it rather than away from it.

- Offers **appear over time, semi-randomly**, rather than all being present from turn one. The
  Customers tab becomes something you check, and a good offer is a moment.
- **Frequency scales with the business's reputation AND game difficulty.** A well-run DC attracts more
  and better customers; a bad one dries up. Difficulty scales the curve.
- Reputation inputs Tad named, in his order: **late orders · cancelled orders · drivers left waiting
  to be offloaded at inbound · mispicked orders · damaged cases.** The last two don't exist yet.
- Everything the first three need is already recorded: `SignedContract.OrdersLate` /
  `OrdersDelivered` (per-account and summable), `OrderService.OnOrderCancelled`, and inbound wait is
  derivable from `TruckController`'s dock lifecycle (`AwaitingOffload` → `CompleteOffload`).
- The natural shape is a `ReputationService : IService` holding a rolling score, persisted like
  `SignedContract`'s stats are, driving how often `OrderArrivalService.AddOffer` fires and how good
  the rolled terms are. **Not started — but both halves of the plumbing now exist**
  (`ContractData.CreateRuntime` + `AddOffer`), and `ToolsWindowController.CreateTestCustomerOffer`
  is a working reference implementation of the roll.
- **Persistence is the one real gap.** Generated offers vanish on reload (see the caveat above). Needs
  a `List<ContractSnapshot>`-style store of generated offers in `SaveData` before this ships.

### Still open after this session

1. **Nothing makes a truck actually show up for its appointment.** The book is real, persisted, and
   capacity-enforced, but `TruckYardManager` doesn't read it — outbound trailers still arrive however
   they did before. That wiring is the next step and is where the schedule starts to bite.
2. Full-pallet fulfilment (unchanged, still the big one) — `OrderData.IsWholesale` is now the flag it
   will key off.
3. Wholesale offers should REFRESH rather than being permanently spent (unchanged). Delivered deals at
   least no longer clutter Offers — they live under Accounts → Completed deals.
4. Accounts aggregates per CONTRACT. One customer holding two contracts shows as two rows. Correct,
   but if that ever looks wrong it's the place to group.
5. `ScheduleDaysBack = 2` matches `DockScheduleService.KeepPastDays`; changing one needs the other.
6. Reputation-gated contract arrival (designed above, not built) — the thing that makes the Customers
   tab worth opening more than once.
7. Only 3 `ContractData` assets, so the Customers tab currently shows its empty state on Tad's save.
   Reputation-driven arrival will need a much deeper catalogue across the 26 authored customers.

---

## Purchasing & Vendor System — Phases 1, 2, 4 BUILT 2026-08-18; 3 partial, 5 designed

> **See `PURCHASING_DESIGN.md` in the repo root for the full document.** This is a pointer, not a
> summary — the design lives there so it's editable as one piece.

The inbound half of the loop, and currently its weakest link. Today: one hardcoded supplier
(`"PLAYER_SUPPLIER"` / `"Wholesale Supply"`, `PurchasingPanel.cs:968`), one never-moving price per
SKU, and a truck that always carries exactly what was ordered. Tad: *"a two dimensional pick items
from a list like you're at a restaurant."*

**The agreed direction: vendor tiers gated by a Reputation score.**

Four load-bearing decisions, so they don't get re-litigated:

1. **ONE Reputation score, two consumers.** The vendor reputation is the SAME number as the
   customer-side reputation designed in the 2026-08-01 session above — it gates which customers offer
   you contracts AND which vendors answer your calls. One `ReputationService`, one number the player
   reads, both halves of the game feeding the same spine. Do not build two.
2. **Salvage, not contraband.** The Buccaneer-style "rare vendor with illegal goods" is reskinned to
   unmanifested salvage / close-out loads bought sight-unseen. Same thrill, no
   inspection/seizure/fines second failure system, and it's authentically warehouse. It's also the
   natural entry vector for the deferred rat system (ship a contaminated pallet = −40 rep).
3. **Tier 3 launches AMBIENT.** Caviar/seafood/rare beef are all cold chain, which needs
   Perishable/Frozen storage + refrigerated rooms + power upkeep. Tier 3 ships with high-value
   ambient goods (truffle oil, saffron, aged spirits, single-origin coffee); cold chain is its own
   later milestone that reuses the tier structure.
4. **The Anti-Obsolescence Rule.** Tiers differ on a **risk/velocity** axis, never strictly-better.
   Staples = thin margin, constant demand, forgiving. Specialty = fat margin, lumpy demand,
   slot-hungry, punishing to scratch. Otherwise ~25 of the 31 SKUs become dead content by hour three.

**Cut:** the Oblivion-style persuasion/barter minigame (Tad self-rejected; the fantasy here is
operational mastery, not social skill).

**Two findings from the design review worth knowing before touching this code:**

- **Receiving variance is plumbed but structurally impossible.** `ShipmentLineItem.Overage`/`.Shortage`
  are computed and logged, but `UpdateReceivedQuantity` is called from exactly ONE place
  (`ShipmentReceivingCoordinator.cs:96`) with the quantity of the pallet that physically arrived —
  and the trailer is built from the PO. Received always equals ordered. The gun is built and unloaded.
- **Current SKU margins run BACKWARDS from the intended tiering.** Water 5→12 is a 140% markup and
  Salt 10→18 is 80%, while Maple Syrup 55→85 and Honey 42→65 are both 55%. The cheap staples are
  currently the fattest margins. A repricing pass is required — and it's where the tiers actually get
  their character, not cleanup.

**Build order:** (1) ~~make purchasing a bet~~ **DONE**; (2) `VendorData` + `VendorRegistry` mirroring
`ContractData`/`ContractRegistry`, plus the repricing/tier-assignment pass; (3) `ReputationService`;
(4) the Broker (salvage loads, `PalletStatus.QAHold` write-offs); (5+) cold chain, rats.

### Phase 1 — what's actually in the code now (live-verified 2026-08-18)

- **`MarketService`** (`Core/Inventory/MarketService.cs`) — new `IService`. Per-SKU daily price on a
  MEAN-REVERTING random walk clamped to 0.70x-1.35x of authored `BuyValue`, 7 days of history for the
  sparkline, and the 3-a-day expiring spot-deal board. **Mean reversion is load-bearing** — a pure
  random walk wanders to the clamp and the sparkline becomes a flat line against a wall.
- **Registered in `GameContext` AFTER `inventoryService.LoadSkuDatabase(...)`, not with the other
  services.** It seeds every price from the SKU database on `Initialize`, and an empty database at
  that moment means a market with no prices and no deals for the entire session.
- **`ShipmentLineItem.Dropped` + `ShipmentService.ApplySupplierVariance`** — 25% of PLAYER POs arrive
  short by 1-3 pallets. Rolled ONCE at dispatch (`_varianceApplied` guards a re-spawn), before
  `TruckController.LoadShipment` reads the manifest, so the physical trailer, the PO list and the
  receiving records agree from the first frame. Never more than half a load, never a single-pallet
  PO, never non-player freight. **The player is CREDITED for what didn't arrive** — being charged for
  freight that never came reads as the game stealing from you; the interesting loss is the missing
  stock and the fill rate it costs.
- **`Dropped` is a FLAG, not a deletion.** `Quantity` is what was ORDERED; removing the line would
  erase the evidence anything was missing. Left in place, `ReceivedQuantity` stays 0 and `Shortage`
  finally reports something real — the first time that field has ever been non-zero.
- **Every price on the panel goes through one `UnitPrice(sku)` helper** — item card, line cost, order
  total, and the PO's actual `ShipmentLineItem`s — so the number quoted and the number charged cannot
  diverge. Falls back to `BuyValue` if the market isn't running (usable, not free).
- **`WAREHOUSE:` line** under the trailer meter reads `LocationStatusRegistry` (what the reach truck
  obeys), NOT `LocationData` — see the split-brain note earlier in this file.
- **UI gotcha:** the item card's price row MUST be `flexWrap = Wrap` with `flexShrink = 0` children.
  The card body is what's left after a 116px icon and a 150px cost plate — under 250px at two
  columns — so cost + trend chip + sparkline do not fit on one line. Without wrapping the chip
  renders as "NORM" and the sparkline is clipped away entirely.

**Bug found in verification, worth remembering:** `EnsureDealsForToday` originally re-read the clock
instead of using the day carried by `OnDayChanged`. Those two can disagree, and when they did it
expired the board against the NEW day then refused to rebuild it against the OLD one — the spot-deal
board would have emptied at the first midnight and never come back. Anything reacting to
`OnDayChanged` should trust the event's day, not re-read `SimulationTimeService.Day`.

**Already in the code and unused, relevant here:** `PalletData.AreaCategory { Grocery, Perishable,
Frozen }`, `PalletData.PalletStatus.QAHold`, `SignedContract.SatisfactionPercent` (the per-counterparty
standing precedent to mirror for vendors), `ContractData.CreateRuntime` + `OrderArrivalService.AddOffer`
(the runtime-counterparty-generation pattern).


### Phase 2 — vendors, reputation, repricing (live-verified 2026-08-18)

- **`VendorData` / `VendorRegistry`** (`Core/Inventory/`) mirror `ContractData`/`ContractRegistry`.
  Registry asset at `Assets/_Project/Resources/VendorRegistry.asset`, vendors under
  `Assets/_Project/ScriptableObjects/Vendors/`. **Loaded from Resources BY NAME** so it resolves in a
  built player.
- **Seven vendors on a LADDER, not at the band boundaries:** BulkBasin 0, Cornerstone 0, Halloran 60,
  Fairweather 100, Meridian 160, VesselVine 250, Ambrose 340. Deliberate — unlocking in three lumps
  at 100/300/600 would make the roster feel static between bands.
- **Only THREE differentiating fields, all consumed:** `PriceMultiplier`, `ShortShipmentChance`
  (drives `ShipmentService.ShortShipChanceFor`, replacing the flat 25%), `MinimumOrderCases`.
  **On-time % and net-30 were drafted and CUT** — no late-delivery or trade-credit mechanic exists,
  and unconsumed fields are how Overage/Shortage sat dead for months. Add them WITH their mechanic.
- **`ReputationService`** was pulled forward from Phase 3 out of necessity: gating with no score
  source means 13 buyable SKUs forever. Score 0-1000, bands Unknown/Known/Respected/Preferred/
  Untouchable at 0/100/300/600/850, persisted via `SaveData.reputation`. **Three of five designed
  inputs wired** (OnOrderShipped / OnOrderFined / OnOrderCancelled). Dock wait time and contamination
  are NOT wired — no events exist for them yet.
- **Reputation toasts only on BAND CHANGE**, not per event — "+5 reputation" on every shipped order is
  noise on a number that moves all day.
- **REPRICED all 31 SKUs** (0 unmapped). T1 30-40%, T2 50-56%, T3 100-117%. Catalogue splits 13/14/4.
  This is what makes the tiers mean anything — before it, Water was a 140% markup and Maple Syrup 55%.
- **One PO is one vendor.** `OnSelectVendor` clears the basket on switch. `SelectedVendor()`
  re-anchors to the first unlocked house whenever the selection stops being valid, so a reputation
  drop or an edited asset can't leave the panel pointing at a vendor that won't serve you.
- **Spot deals are NOT vendor-gated on purpose** — separate channel, and an occasional early taste of
  a Tier 3 item is a feature.
- **UI gotchas, both cost a screenshot to find:** the vendor bar and its row need `flexShrink = 0` or
  the wrapped second row draws straight over the trailer meter below it. And chip name/terms labels
  must be `WhiteSpace.Normal` — at 148px wide, `NoWrap` clipped "Fairweather Trading Co." to
  "Fairweather Tradi" and, worse, hid the "min 240" clause that REFUSES the PO.
- **Locked-chip label says the NUMBER, not the band.** Naming the band a threshold sits in produced
  "Needs 250 rep (Known)" for a player who was already Known.


### Phase 4 — The Broker (salvage loads), live-verified 2026-08-18

`BrokerService` + `Vendor_Broker` asset, gated at **600 reputation**. 35%/day chance of one
unmanifested trailer, expires after 2 days.

- **CONTENTS ARE ROLLED AT OFFER TIME AND PERSISTED** (`SaveData.broker` carries them). Not at
  reveal. The trailer really does contain something specific before the player decides, so a reload
  cannot reroll a bad load — a gamble you can save-scum isn't one.
- Mix measured over 3,897 pallets: 59.4/20.1/15.2/5.4 vs targets 60/20/15/5
  (ordinary/short-dated/damaged/jackpot).
- **Short-dated stamps a real 4-day `ShelfLifeDays`** and rides the EXISTING receive path into a real
  expiration day + the spoilage system. No new plumbing — that pipeline finally has a user.
- **Damaged sets `Dropped = true`** so no pallet is built and none reaches inventory (refused at the
  door), but the line item STAYS on the manifest carrying `Salvage = Damaged`. That's what lets the
  PO list say "3 damaged, written off" instead of just showing a smaller trailer.
- **Jackpot pool = SKUs no unlocked vendor carries**, so it's by construction something you can't buy.
- **`ShipmentData.IsSalvage` hides the manifest** in the PO list until `AnyReceived` is true. This
  concealment IS the product.
- **The Broker is filtered out of `VendorRegistry.Unlocked`/`Locked`** — he has no catalogue, and as a
  supplier chip he'd read "0 item(s) available for ordering", which looks like a bug. Still reachable
  via `GetById`, which is how BrokerService reads his reputation gate.
- `ApplySupplierVariance` **skips salvage** — the load is already the gamble; short-shipping it on top
  would charge twice for the same uncertainty and be indistinguishable from the junk you knowingly bought.

**BUG FOUND BY MEASURING, worth remembering as a pattern.** The asking price originally excluded
damaged pallets, so it could never exceed usable value — **300 of 300 sampled loads were profitable**
and the gamble had literally no downside. Pricing on the FULL manifest (junk included) is the whole
risk. Now 40-92% of full manifest value: **88% profitable / 12% losses / +42% avg margin / worst
−46%** over 800 loads. Lesson: a risk mechanic is worth simulating a few hundred times before
believing it works — it read correctly and was inert.

**Known limitation:** damaged product is refused at the door rather than physically arriving and
needing disposal (the more interesting version, but it needs a disposal mechanic).
`PalletMasterRecord` has no status field, so `PalletStatus.QAHold` remains unused — that's where it
belongs when disposal lands.


---

## Session 2026-08-18 (late) — three loop-blocking bugs, all found by measuring

Went looking for "what else is there to do" and found the outbound half of the loop dead and the
financial reporting lying. None of these were visible from reading the code; all three were found by
querying the running game.

### 1. A freshly-hired Receiver or Order Selector never started working

`EmployeeSpawner` gave an automatic assignment to the MHE roles only (`TryBoardExistingMHE` for
ReachTruck / DockStocker). **Every other role spawned on Patrol with no task driver
attached** — so a hired Order Selector walked around forever while OrderSelect tasks piled up, and the
ONLY way to make them work was to find them in the Roster and pick their assignment out of a dropdown
by hand. Nothing said so.

Measured live: an order sat `Pending` behind an Available OrderSelect task with **zero
`OrderSelectionTaskDriver` components anywhere in the scene**. The one Receiver who did work had been
hand-assigned at some point and had it persisted via `record.currentAssignment`.

**Fix:** fresh non-MHE hires now get `record.role.RoleSpecificAssignment()` applied at spawn — the
same map the Roster and Info card already use, so a hire starts in exactly the state that dropdown
would have produced. Verified: hiring an OrderSelector now yields `assignment=OrderSelection` with the
driver attached.

### 2. Nothing ever said "you have no one who can do this job"

A queue full of Available tasks and nobody employed who can take them is silent and fatal.
`WorkQueueSystem.WarnAboutUnstaffedWork` (on the existing hour tick) now toasts once per role per day
when Available tasks require a role **no active employee holds**.

**Keyed on ROLE-NOT-HIRED, not on task age** — deliberately. A task waiting because everyone is busy
is a queue working correctly; a task waiting because the role doesn't exist in the building is a dead
end, and only the second is worth interrupting for.

**A TASK'S RequiredRole IS NOT ALWAYS THE ONLY ROLE THAT CAN DO IT — this warning got that wrong on
its first pass (HISTORY - the `Loader` role was removed 2026-10-04: Load tasks are now filed as `DockStockerOperator`, `CanServe` is exact-match, role ints are pinned so nothing shifted).** `OrderService` filed Load tasks as `EmployeeRole.Loader`, but
`TrailerLoadController` (line ~194) accepts a **DockStockerOperator** too: they drive the same
equipment, and `RoleSpecificAssignment` maps both to `DriveDockstalker`. Comparing roles exactly meant
the warning nagged "you haven't hired a Loader" every day at a player whose dock stocker could load
the trailer perfectly well — a false alarm on a warning whose entire value is that it only fires when
something is genuinely impossible.

`WorkQueueSystem.CanServe(employeeRole, requiredRole)` now encodes the substitution, mirroring
TrailerLoadController's own operator check as the authority. **If any controller ever learns to accept
a substitute role, add it there too.** Every other consumer is an exact match (ReachTruckOperator for
Putaway/Replenish/PalletPick, DockStockerOperator for offload, Receiver, OrderSelector) — verified by
reading each consumer's role check, not assumed.

### 3. ⭐ `SetMoney` on save load was booking the whole balance delta as a "Debug" EXPENSE

**This one poisoned every financial panel in the game.** `PlacementSystem.ApplySaveData` called
`moneyService.SetMoney(save.money)`, and `SetMoney` records the delta as a real transaction. The game
boots at the difficulty's starting capital ($120k Clerk) and then loads a save holding less — so
**every single load charged the gap to lifetime expenses.**

Measured on a real save: **$26,731 of $26,879 lifetime expenses — 99.4% of everything the player had
apparently ever spent — was one load correction.** It also inflated `ExpensesThisHour`,
`ExpensesThisWeek` ($38,746) and everything built on them. The genuine operating costs underneath were
Wages $124, Groundskeeping $11, Maintenance $10, MHE $2.

**There was never a balance problem.** The 30:1 "burn" was an artifact. New
`MoneyService.RestoreCapital(int)` assigns the balance and fires `OnMoneyChanged` **without touching
any ledger or counter**; `ApplySaveData` uses it. Verified after a real load: Debug portion **$0**,
total lifetime expenses **$237**, all of it real.

**Lesson worth keeping: restoring a balance is not a transaction.** Any future save-restore of a
running total needs the same treatment.

### Also: reputation now gates CUSTOMERS as well as vendors

The second half of "one reputation, two consumers". `ContractData._reputationRequired` (0 in every
asset authored before this, which is correct — those are starter accounts),
`OrderArrivalService.AvailableOffers` filtered by it, and a new `ReputationLockedOffers` rendered as a
greyed card on the Customers tab: *"WON'T DEAL WITH YOU YET — Needs 300 reputation (Respected) — you
have 170. 130 to go."* Same reasoning as locked vendors: a door you can see is a goal.

`CurrentReputation` is resolved LAZILY, not cached at Initialize — `OrderArrivalService` is
constructed before `ReputationService` in `GameContext`, and a field captured at init would be null
forever, silently reading as reputation 0 and locking every gated customer out of the game permanently.

Authored ladder: `Contract_Starter` 0, `Contract_HighVolume` 250, `Contract_WholesaleTrailer` 400.
Runtime BULK offers stay at 0 — walk-in business doesn't check your references.

---


