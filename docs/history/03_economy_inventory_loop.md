# Economy, panel exclusivity, inventory milestone 1, core gameplay loop chunks (Jun-Jul 2026)

> Archived from CLAUDE.md on 2026-11-02 (verbatim). Historical detail — read only when working in this area. The lean CLAUDE.md holds the rules that always apply.

## Economy & Financial Reporting System

Built out 2026-06-25, actively being extended. This is the active focus area for the foreseeable future — keep this section current as it evolves rather than letting it drift like the Wave 4/5 docs did.

**⚠️ Testing gotcha, confirmed 2026-06-25: restart Play Mode after any code change before re-testing this system.** Recompiling scripts mid-Play-session triggers a Unity domain reload, which silently nulls `EventManager.Instance`, `ServiceLocator`'s entire registry, and `GameContext.MoneyService`/`TimeService` (plain C# objects don't survive domain reload the way the `GameContext` GameObject itself does). The result: `SimulationTimeService` stops ticking, `EconomyService`'s hourly accrual stops, `PayrollService` stops paying wages — but the TopBar UI keeps showing whatever numbers were last rendered, so it LOOKS like the game is still running when it's actually frozen. This reads exactly like a UI/data-wiring bug (and cost real debugging time before the actual cause was found via `Unity_RunCommand`: `EventManager.Instance == null` + `ServiceLocator.TryGet<MoneyService>()` failing while `GameContext` itself is still findable in-scene). There's no automatic recovery — stop and re-enter Play Mode to get a clean `GameContext.Awake()`.

### Core data flow

Two **separate, parallel** cost pipelines feed `MoneyService`, both ultimately routed through `FinanceCategory`:

1. **Object hourly costs** (`EconomyService`) — driven by `ObjDataSO.hourlyCost` for placed buildings/vehicles/decor. Tracked per `GL_Line`, NOT per individual object.
2. **Employee wages** (`PayrollService`) — driven by `EmployeeRecord.hourlyWage`, paid once per in-game hour. Completely separate from (1) — see the "Worker/Staff exclusion" gotcha below.

**`ObjDataSO.GL_Line`** — a plain string field, hand-set per asset (Inspector-editable, no runtime default-computation). Defaults were populated by a one-time batch (`Unity_RunCommand`, not a menu item) mapping `category` → `GL_Line`:

| category | default GL_Line | top-level bucket |
|---|---|---|
| Barrier/Barriers | Barriers | Maintenance |
| Flavor | Flavor | Maintenance |
| Floor | Floor | Maintenance |
| Grounds | Groundskeeping | Groundskeeping |
| Inventory | Pallet Lease and Repair | Pallet Lease and Repair |
| Loss Prevention | Loss Prevention | Loss Prevention |
| Racking | Racking | Maintenance |
| Staff | Wages | Wages |
| Vehicles | (per-vehicle, see below) | MHE Costs |
| Walls | Walls | Maintenance |
| Waypoints | Waypoints | Maintenance |
| Worker | Worker | Maintenance |

**Per-asset overrides on top of the table above** (set directly on the asset, not derivable from category):
- `ManDoor`, `RollupDoor`, `ShippingDoor`, `Wall-Win-Entrance` → GL_Line `"Doors"` (still Maintenance)
- `Truck`, `TruckSavageDev`, `TruckDriver` → GL_Line `"Transportation"` (own top-level bucket, pairs the vehicle + its driver's wage)
- `DS`/`PJ`/`RT` (Dockstocker/PalletJack/ReachTruck) → GL_Line `"Dock Stocker"`/`"Pallet Jack"`/`"Reach Truck"` respectively — each vehicle type gets its **own** GL_Line string so the MHE Costs tooltip can break them out individually, all three routed to the same `MHECosts` top bucket via `FinanceCategory.ForGLLine`
- `Dumpster`, `Garbage Can`, `Trash Can` → GL_Line `"Sanitation"` (own top-level bucket, pairs with the Sanitation employee tier)
- `Exterminator` (the Staff body model) → GL_Line `"Contract Labor"` (pairs with the Exterminator employee's wage)

**`FinanceCategory.cs`** (`Assets/_Project/Scripts/Core/Managers/TimeAndMoney/FinanceCategory.cs`) is the single source of truth for category strings and routing:
- `IncomeOrder`/`ExpenseOrder` — fixed arrays that drive row order in every breakdown panel.
- `ForGLLine(string glLine)` — maps a GL_Line value to its top-level bucket. **Whenever you give an object/category a new distinct GL_Line value, you must add a case here too**, or it silently falls through to the `Maintenance` default (this has bitten us twice already — Sanitation and Contract Labor GL_Line values were missing cases and got miscategorized).
- `ForWageGLLine(EmployeeRole role)` — maps an employee role to `(topCategory, detailKey)`. Exterminator→Contract Labor, Security→Loss Prevention, Sanitation→Sanitation (own line), TruckDriver→Transportation — these four are pulled OUT of Wages entirely. Everything else stays under Wages, tiered into `FloorWages`/`HourlyWages`/`SalaryWages` (OrderSelector/Loader/DockStockerOperator/ReachTruckOperator/Receiver = Floor; InventoryControl/Admin = Hourly; HR/Boss/Supervisor = Salary).

### EconomyService (`Assets/_Project/Scripts/1. FSM/1. Services/EconomyService.cs`)

- Subscribes to `GameEvents.Build.OnObjectPlaced/OnObjectDeleted` and pools hourly cost **per GL_Line** in `_hourlyByGLLine` (NOT a single flat total — that was the old, dormant-until-2026-06-25 design).
- **Fractional-carry accumulator (`_fractionalByGLLine`) is load-bearing, do not remove.** Without it, `Mathf.CeilToInt(hourlyAmount/60f)` rounds every GL_Line up to a **$1/minute floor** regardless of its real rate — several cheap categories (a few dollars/hour each) all hit the same floor and accumulate *identical* lifetime totals, which looks exactly like a data bug but is a rounding bug.
- **`IsWageTracked()` excludes `category == "Worker" || "Staff"`.** Employee prefabs carry a `PlacedObject` component too (for the hover-popup), so they self-register with `PlacedObjectRegistry` just like a placed building even though they never go through `PlaceCommand`. Without this exclusion, every hired employee's body double-charges its legacy `ObjDataSO.hourlyCost` on top of `PayrollService`'s own wage — confirmed empirically (the math matched to the dollar) before this was understood.
- **`RebuildFromRegistry()` must be called after every save load / scene start / manual rebuild** — call it everywhere `PlacementGrid.RebuildFromRegistry()` is called (currently: `GameContext.SyncAndBake`, `PlacementSystem`'s two load call sites, `ToolsWindowController`'s rebuild button and Clear-All). Objects restored from a save or placed by hand in the Editor scene are instantiated directly, bypassing `PlaceCommand` — `OnObjectPlaced` never fires for them, so without an explicit rescan they're invisible to hourly-cost tracking even though they're sitting right there costing money.
- **`OnMinutePassed` also calls `moneyService.RecordHourlySpend(wholeDollars)`** for every real per-GL_Line deduction — bookkeeping only (the dollar was already removed by `RemoveCapital` on the line above), tags it as a *recurring* hourly cost for the Spent Today panel. `PayrollService` does the same for wages. See MoneyService below.
- **Known gap, still not fixed:** one-time placement costs (`PlaceCommand`'s `_money.Deduct(_data.cost, _data.category)`) are tagged with the raw `ObjDataSO.category` string directly in `_lifetimeExpenses`/`_lifetimeDetail` — completely bypassing `FinanceCategory`/`GL_Line` for the **lifetime** totals. The Hourly tab's per-category breakdown still won't show one-time purchase costs (category strings like `"Grounds"`/`"Walls"` don't match any `FinanceCategory.ExpenseOrder` entry). This WAS fixed for the **Spent Today** panel specifically (see `MoneyService.Deduct()` below) but not for the lifetime/Hourly-tab view — would need the same `FinanceCategory`/`GL_Line` routing `Deduct()` callers don't currently use.

### PayrollService (`Assets/_Project/Scripts/1. FSM/1. Services/PayrollService.cs`)

Pays every `Active`, non-`SystemManaged` employee their `EmployeeRecord.hourlyWage` once per in-game hour, routed through `FinanceCategory.ForWageGLLine`. Increments `EmployeeRecord.totalWagesPaid`.

**Overtime rule (added 2026-06-25):** if `ShiftSchedule.IsOvertime(record.shift, currentHour)` is true, that hour is paid at **1.5×** the base rate. `Flexible`-shift employees have no fixed window and are never overtime.

### ShiftSchedule (`Assets/_Project/Scripts/Actors/EmployeeSystem/ShiftSchedule.cs`)

Single source of truth for shift windows, shared by `PayrollService` (overtime pay) and `ShiftStatusPanel` (the Time dropdown). Fixed windows, not yet configurable:
- Day: 08:00–16:00
- Evening: 16:00–00:00
- Night: 00:00–08:00
- Flexible: always "in shift," never overtime

This is a reasonable first-pass interpretation, not a confirmed design spec — revisit if the windows or overtime definition need to change.

### MoneyService (`Assets/_Project/Scripts/Core/Managers/TimeAndMoney/MoneyService.cs`)

Lifetime tracking (`_lifetimeExpenses`/`_lifetimeIncome` top-level totals, `_lifetimeDetail` = `category → detailKey → amount` powering the Hourly tab's tooltips) all flow through `RemoveCapital`'s two overloads sharing a private `ApplyRemoval()` core: the 2-arg version records under `reason`; the 3-arg version (`amount, category, detailKey`) records under the finer `detailKey` instead of `category`, to avoid double-recording the same dollar under two granularities.

**Two SEPARATE "today" counters power the Spent Today panel** — these used to be one conflated number and that was a real, user-reported bug (2026-06-25/26): buying 56 foundations showed as "$25,200 Total Hourly Expenses" with an empty Purchases list, because the old single `SpentToday`/`_spentTodayDetail` lumped one-time purchases in with recurring hourly costs, and the by-category list was only ever fed by the hourly loop. Fixed by splitting into:
- **`_spentTodayHourlyOnly`** — RECURRING costs only: `EconomyService.OnMinutePassed` (object upkeep) and `PayrollService` (wages) both call `RecordHourlySpend(amount)` as bookkeeping alongside their real `RemoveCapital` call. Powers Spent Today's "Total Hourly Expenses" line.
- **`_spentTodayByObjectCategory`** — ONE-TIME purchase costs only, keyed by raw `ObjDataSO.category`. Populated exclusively by **`Deduct(int amount, string category)`** — every `PlaceCommand`/`MoveCommand`/`DeleteCommand` one-time cost adjustment goes through this method, and it always carries a real category string, so it's the correct/only hook point. Powers Spent Today's "Purchases" section + "Total Purchases" row.

Both reset in `ResetDailySpending()` alongside the legacy `_spentToday` int (which still exists and still means "everything," just isn't displayed as a single number anywhere anymore). If you add a NEW one-time-cost call site, route it through `Deduct()` (not raw `RemoveCapital`) so it's correctly counted as a purchase, not an hourly cost.

**Lease/Mortgage** — `EconomyService.ChargeLease()`, fired on `OnDayChanged`: `(PlacementGrid.Width × Height) / 10` dollars, once per in-game **day** (not hourly, unlike everything else — rent is conventionally billed per period). Explicitly a quick stand-in per the user's request ("may not be a permanent feature") — easy to rip out or replace with a real property/lease system later.

### The four TopBar reporting panels

All implement `ITopBarPanel` (`IsVisible`/`Show()`/`Hide()`/`Root` — `Root` exposes the panel's own VisualElement) and share visual building blocks from `FinanceUIKit.cs`. `TopBarUI.ToggleExclusive()` ensures only one is ever open at a time, and `TopBarUI.PollPanelAutoClose()` (checked every 100ms) auto-closes whichever panel is open once it's been unhovered — neither its trigger label nor its own `Root` — for 1500ms. Hovering either the label or the panel resets that idle clock.

| TopBar label | Panel class | Shows |
|---|---|---|
| Capital | `CapitalSummaryPanel` | Revenue breakdown (green header / light-yellow text) + **one** rolled-up Total Expenses line + Net Profit. No per-category expense drill-down here. |
| Hourly | `FinancialBreakdownPanel` | **Expenses only** (red header / white text) — full per-category breakdown with hover-to-expand tooltips (Wages tiers, Maintenance by area, MHE Costs by vehicle, etc). The Income section and Net Profit row were removed entirely — Revenue/Net Profit live on Capital instead. |
| Spent Today | `SpentTodayPanel` | "Total Hourly Expenses" — recurring costs only, one number, not broken out. Below it, a "Purchases" section: one-time costs broken out by raw `ObjDataSO.category`, with a "Total Purchases" row. See MoneyService above for why these are two genuinely separate counters now. |
| Time | `ShiftStatusPanel` | Hours left in whichever shift window currently contains the in-game time, + live count of `Active` employees currently working outside their own shift (overtime). |

**Tooltip retract mechanism (FinancialBreakdownPanel's per-category hover tooltips, NOT the same thing as the panel auto-close above)** — rewritten 2026-06-25 after two patch attempts both failed under sustained use. The original design created a fresh `IVisualElementScheduledItem` per mouse-enter/leave event and called `Pause()` on the previous one each time; this degraded after repeated hover cycles into tooltips getting permanently stuck open. **Do not reintroduce that pattern.** Current design: a `TooltipEntry` (`Row`, `Tooltip`, `Category`, `Hovered` bool) per expandable row, tracked in a `List<TooltipEntry>`, with **one** shared idle-timer (`_idleMs`) polled by **one** scheduled item created once in the constructor (`_panel.schedule.Execute(PollIdle).Every(100)`) that runs for the panel's entire lifetime, closing after 1500ms unhovered. No per-interaction timer creation/cancellation.

**UI Toolkit scheduling gotcha, learned the hard way (twice):** `IVisualElementScheduledItem.ExecuteLater(ms)` reschedules an item that has **already fired once** — it does NOT delay the first execution. To delay a first run, use `.StartingIn(ms)` instead. Both the tooltip retract above AND the panel auto-close (`TopBarUI.PollPanelAutoClose`) initially used `ExecuteLater` and silently didn't work — the panels stayed open until manually re-clicked, which read as a missing feature rather than a one-word API mistake.

### Overtime actions — "Ask to Work OT" / "Send Home" (added 2026-06-25)

Two new options in the Employee Info card's Actions dropdown (`EmployeeInfoUI`), handled by a new static `EmployeeOvertimeService`:
- **Ask to Work OT** — applies an instant morale penalty (5 points normally, 12 if morale is already below 40 — "low morale takes a bigger hit"), and marks today as an overtime day on the employee's `EmployeeWorkSchedule` (`SetOvertime`). Does NOT gate whether overtime pay/fatigue actually happens — `PayrollService` already pays 1.5x automatically by hour regardless of consent (see ShiftSchedule above); this button is the morale-cost flavor action of formally asking, not a gameplay gate.
- **Send Home** — flat 10-point morale penalty (`// TODO: scale by difficulty` — explicitly a placeholder per Tad's note, not implemented yet). Does nothing else right now (no pay/status change) — kept deliberately minimal to match the literal ask.

**`EmployeeStatSystem.cs`'s daily fatigue/morale tick (`TickDay`/`TickDayForAll`) is NOT wired to run automatically anywhere** — confirmed by grep, it's only ever called from its own Editor debug context menu ("Tick Day for All Employees"). Its overtime fatigue gain is now `_fatiguePerWorkDay * _overtimeFatigueMultiplier` (multiplier defaults to 2 — an exact "doubled" relationship, replacing an old unrelated flat constant), and `GetCurrentDayOfWeek()` is now `public static` (still uses real-world `DateTime.Now`, not in-game day — a known placeholder) so `EmployeeOvertimeService` can share it instead of duplicating the bug. Net effect: the "doubled fatigue during overtime" rule is implemented correctly but has **zero visible effect in actual play** until this daily tick gets wired to a real day-change event — flagged to Tad, not silently fixed (wiring it up is a separate decision: should it fire on `GameEvents.Time.OnDayChanged`, and should `GetCurrentDayOfWeek` be fixed to use `SimulationTimeService.Day` at the same time, since they're related bugs).

### ShiftManagerPanel — first draft (2026-06-25/26, UI ONLY, not wired to gameplay; visual pass + polish 2026-06-26)

`Assets/_Project/Scripts/UI_UX/ShiftManagerPanel.cs`, bound to the **5** key (built/owned by `TopBarUI` like the four panels above, but NOT one of them — it's a full-screen modal, not a TopBar dropdown, so it's outside `ToggleExclusive`/`PollPanelAutoClose`). Lets the player define named shifts (free-text name, e.g. "Day Shift", "Clean Shift", "Night Shift") with a Start/End time dropdown per day of week (Sun=0..Sat=6 — **note this differs from `EmployeeWorkSchedule`'s Mon=0..Sun=6 convention**, unreconciled since this isn't wired to that system yet), in 30-minute increments 00:00–23:30, or "Closed". Constructor now takes `ITimeService` (`new ShiftManagerPanel(root, _timeService)` in `TopBarUI`) so the header row can show absolute day numbers (see below).

**Cell state model:** each Start/End cell is one of `NotSet` (blank, never touched — red background, .65 alpha), `Closed`, or a real time. A day where **both** Start and End are still `NotSet` silently resolves to `Closed` on Save & Close / a row's own "Enter Info" (the "simplify the process" default) — no error. A day left **half-filled** (one side set, the other blank) blocks submission. Setting Start to a real time **auto-populates End as +8.5 hours later** (a standard no-OT shift, clamped to 23:30 — no midnight wraparound modeled), which the player can still override manually. A Start-after-End on a day where both sides are real times, or any cell still `NotSet`, renders both that day's Start and End boxes in red (.66 alpha) and blocks submission with a toast.

**Per-row actions:** Name field placeholder reads "Shift Name (req.)"; typed name also mirrors into that row's "Start:"/"End:" labels. **Enter Info** (blue, matches the Start:/End: label color) validates/commits just that one row — "Shift Name is Required" / "Shift Name in Use" / "Please correct any errors before submitting the Schedule" toasts as appropriate. **Remove** (orange, far right, flush with the grid's right edge) now opens a small "Confirmation" / "Are you sure?" Yes/No sub-modal before deleting — no more instant delete. **Save & Close** (fire-hydrant red `#C1272D`) re-validates everything. The **✕** button is a separate **cancel/discard** path — "Confirmation" / "Exit? Changes may be lost." — Yes closes immediately with no validation (throws away unsaved edits), No cancels; it no longer silently behaves like Save & Close.

**Visuals (2026-06-26 pass):** restyled to match the Employee Roster / Hiring Board navy-and-blue aesthetic with Lilita One throughout (loaded via `AssetDatabase.FindAssets` in editor / `Resources.Load` in builds, same pattern as `LoadingScreenManager`); day headers show the full day name plus an absolute "Day NN" line underneath (computed from `ITimeService.Day` offset by each column's distance from "today"); title is centered; panel anchors **top-center** (not vertically centered) specifically so it can grow downward as shifts are added without running out of screen room — modal/shiftsContainer/each shift block all set `flexShrink = 0` and there's deliberately no `maxHeight`/scroll cap.

**Column-width gotchas worth knowing if you touch this file again:** the header banner row, the name/Enter-Info/Remove row, and each Start/End row are all pinned to the *same explicit* width (`LabelColWidth + DayColWidth*7 + SeparatorWidth*6`) because a plain `VisualElement` row otherwise defaults to stretching to fill its parent's cross-axis width, which silently let rows drift out of alignment with each other. Separately, `DropdownField` has its own content-driven min-width (popup arrow + longest time string) that can win over an explicit `width` and quietly grow a "fixed" column — fixed by wrapping each dropdown in a fixed-width `cellWrapper` (`overflow: Hidden`) with the dropdown's own `minWidth = 0`; the per-cell border was also moved off the dropdown onto a decorative `position: Absolute` overlay with zero layout weight so a border can't reintroduce the same drift. A related one: a column-flow `VisualElement`'s child does **not** auto-stretch along the vertical/main axis by default (only the horizontal/cross axis does) — the dropdown was leaving a gap at the bottom of its wrapper whenever the row stretched the wrapper taller than the dropdown's own natural height; fixed with `dropdown.style.flexGrow = 1`. That last fix compiled clean but has not yet been visually re-confirmed in-editor.

**Explicitly scoped as UI-only per Tad:** nothing here drives actual employee arrival/departure/overtime/attendance — `EmployeeRecord.shift`/`ShiftSchedule.cs`/`PayrollService` are completely unchanged and still what actually runs. Schedules built here are also **not persisted** — in-memory only, reset every Play session. A "Remove" button per shift and a "+ Add Shift" button were added as minimal necessary usability (not explicitly requested, but the UI is unusable without them).

**Planned follow-up, not built yet:** a second "side UI" for assigning individual employees to one of these named shifts (the Shift Manager only defines shift *templates* — Sun-Sat windows under a name — it doesn't touch any specific employee). When this and the gameplay wiring above both land, this whole system would replace `ShiftSchedule.cs`'s fixed Day/Evening/Night/Flexible windows with the player-defined ones.

**Confirmed, not changed:** Employee Roster (`EmployeeRosterUI.cs`) dragging was checked during this pass — it's already wired via the same shared `DraggableWindow` helper used here and by the Hiring Board (`DraggableWindow.cs`'s own doc comment specifically cites the roster's stretched top+bottom anchoring as the motivating case it was built to handle). No code change was needed there.


#### Panel exclusivity — how it actually works, and how it silently didn't

Opening any panel closes the currently open one. That is `UIKeyBindingManager.ToggleUI`, and it only
works for panels that are **registered** — which requires implementing `IUIPanel` AND a
`RegisterUI(n, panel)` call. Two long-standing bugs both traced to this:

1. **Panels 5, 7, 8, 9 were never registered.** `UIKeyBindingManager.Instance` was assigned only in
   `Awake`, but `UIBootstrapper.Awake` → `TopBarUI.Init` runs *earlier*, so every registration there
   hit a null Instance and was skipped without a word. `Instance` is now a **self-creating property**
   (adopts an existing scene instance first). Symptom was subtle: those panels still opened via their
   own key handling, but `CloseAll()` — i.e. **Tab** — ignored them.
2. **Keys 5–8 bypassed the manager entirely**, calling `panel.Toggle()` directly in `TopBarUI.Update`,
   so nothing ever closed anything. All now route through `ToggleUI`.

**Shift Manager exception:** it can hold unsaved edits, so it gets a veto. `ShiftManagerPanel.
RequestClose(onClosed)` closes silently when clean, and when dirty raises the existing
`ShowConfirmation` dialog; the requested panel opens inside the callback so it can't appear on top of
an unanswered prompt.

**Adding a new panel?** Implement `IUIPanel` (`Show`/`Hide`/`IsOpen`), call `RegisterUI`, and route its
key through `ToggleUI`. Skipping any of the three fails quietly.

### Inventory System — Milestone 1 (CORE COMPLETE — 2026-06-27)

**✅ COMPLETED: Core Data Structures**
- `PalletData.cs` — Pallet entity (SKU, qty, location, age, expiration); expiration detection
- `SkuData.cs` — Master product data (SO): pricing, shelf-life, Ti/Hi/CaseWeight, stacking rules, demand
- `SkuImporter.cs` — CSV-based importer (no COM deps); reads Days Supply + ItemSetup; cross-references by SKU; auto-generates pricing/shelf-life/stacking rules
- `InventoryService.cs` — Pallet lifecycle: create, move, pick, destroy; location indexing; daily spoilage checks; event publishing
- `ShipmentData.cs` + `ShipmentLineItem.cs` — Inbound tracking (supplier, ETA, line items, costs)
- `OrderData.cs` + `OrderLineItem.cs` — Order tracking (fulfillment, SLA, revenue, profit calc)
- `TestDataGenerator.cs` — Runtime test shipment/order generation from SKU database

**✅ COMPLETED: Excel Integration**
- **99 SKU assets** created from ItemFilesForForkIT.xlsx (Days Supply + ItemSetup sheets)
- Cross-reference validated: matched all Days Supply items with ItemSetup dimension data
- All assets populate Ti, Hi, CaseWeight, pricing, shelf-life categories
- Assets saved to: `Assets/_Project/Data/Inventory/SKUs/SKU_*.asset`

**✅ COMPLETED: Service Integration**
- InventoryService registered in GameContext.Awake(), initialized at startup
- Subscribes to OnDayChanged for daily spoilage detection
- Event system: OnPalletReceived, OnPalletMoved, OnPalletPartialPicked, OnPalletDestroyed, OnSpoilageDetected
- SkuDatabase loaded at runtime via `inventoryService.LoadSkuDatabase(skus)`

**⚠️ KNOWN ISSUE**
- Shadow atlas set to 16384×16384 to accommodate 240+ shadow maps; source unclear (no lights visible in Main.unity)
- Need to investigate on next session if warning persists

**📚 Documentation**
- `GAMEPLAY_LOOP_DESIGN.md` — 6-phase loop with mechanics, roles, 6-milestone roadmap, success metrics
- `MILESTONE_1_TECHNICAL_DESIGN.md` — Architecture, API, UI/UX specs, testing strategy, acceptance criteria
- `EXCEL_DATA_IMPORT_GUIDE.md` — Step-by-step workflow for CSV export and import

---

## Core Gameplay Loop (Active Development 2026-07-03)

**COMPLETE PALLET/CASE LIFECYCLE:** Vendor PO → Inbound → Putaway → Replenishment → Order Selection → Shipping → Invoicing

**See:** `GAMEPLAY_LOOP_SPECIFICATION.md` in memory for full design. This is the definitive spec Tad authored.

### Dummy Data (MVP Testing)

**SKU:** 035-12345 (Dummy Item)
- Description: Generic Cases
- Ti/Hi: 1/1 (one case per pallet)
- Weight: 50 lbs/case
- Cost: TBD / case
- Sell Price: Cost + margin

**Trailer:**
- Capacity: 12 pallets
- Layout: 2 front + 10 middle (side-by-side) + 2 rear (equidistant)
- No physics on pallets (visual only in trailer)

**Chep Pallet Prefab:**
- Visual representation (loaded cases)
- 12 instances per inbound trailer

### The 6 Chunks (Implementation Order)

**CHUNK 1: INBOUND PROCESS — data/event backbone BUILT 2026-07-03, not yet compile-verified (Unity Editor wasn't connected via MCP this session — open the Editor and check for `error CS` before relying on this).**

**What's built:**
- `WorkQueueSystem` (`Assets/_Project/Scripts/Core/Labor/WorkQueueSystem.cs`) — the critical-path task queue every chunk depends on. `WorkTask` (Id, Type, RequiredRole, PalletId, Description, Status: Pending/Assigned/Complete). `CreateTask()`, `GetPendingTasksForRole()`, `TryClaimNextTask()` (FIFO claim), `CompleteTask()`. Registered as an `IService` in `GameContext.Awake()` alongside the other services. `WorkTaskType` enum has stubs for all 6 chunks (Receive/Putaway/Replenish/OrderSelect/Load) even though only Putaway is produced today.
- `LoadIDGenerator` (`Assets/_Project/Scripts/Core/Labor/LoadIDGenerator.cs`) — static utility, generates unique 10-digit numeric Load IDs (dedup via a `HashSet` of issued IDs).
- `PalletData.LoadId` — new settable string property (null for pallets created before Load IDs existed, e.g. hand-placed in the Editor via `PalletInventoryTracker`).
- `InventoryService.ReceivePalletWithLoadId(skuId, quantity, shelfLifeDays)` — new method, parallels the existing `ReceiveShipment`/`RegisterPhysicalPallet` but assigns a Load ID and drops the pallet at receiving staging (0,0).
- `ReceivingService` (`Assets/_Project/Scripts/Core/Labor/ReceivingService.cs`) — static bridge: `ProcessReceiving(ShipmentData)` walks a shipment's line items, calls `ReceivePalletWithLoadId` for each (one pallet per line item, matching `TruckController.LoadShipment`'s existing distribution), then creates a `Putaway` WorkTask per pallet targeted at `EmployeeRole.ReachTruckOperator`. Marks the shipment `Received`. Guards against double-processing via `shipment.Status != InTransit`.
- **Wired into `TruckController`:** when a docked truck's existing `unloadDuration` timer (7s, already there — the visual pallets from `LoadShipment` were already spawned on dock) expires, `TruckController.Update()`'s `Docked` case now calls `ReceivingService.ProcessReceiving(AssignedShipment)` right before `BeginDeparture()`. This is the full inbound loop: PO (`ShipmentService.CreatePurchaseOrder`, already existed) → truck spawns/docks (already existed) → **NEW:** pallets get Load IDs + inventory records + Putaway tasks queued, right as the truck starts to leave.

**Deliberately NOT built this pass (prototype-first, per Tad's "small chunks" approach):**
- No Dock Stocker offload animation or Receiver clipboard/RF-gun animation — the truck's pre-existing unload timer stands in for physical offload. Animations are explicitly Tad's domain to drive later ("deeply involved" per his spec, especially for Order Selection in Chunk 4).
- No employee actually consumes a WorkTask yet — `WorkQueueSystem` only tracks task existence/status. Chunk 2 (Putaway) is what makes a Reach Truck Operator actually claim and act on these tasks.
- Guard-assigns-PO-to-trailer step is implicit (PO creates and spawns its own truck via `ShipmentService`/`TruckYardManager`) rather than a separate guard interaction.

**Next up: CHUNK 2 (Putaway)** — `PutawayLogic` (Pick vs Reserve based on order demand), `PickSlot`/`ReserveSlot` components on rack cells, and the first real employee-claims-a-WorkTask flow (Reach Truck Operator consumes the Putaway tasks this chunk now produces).

**CHUNK 2 (Offload / dock-stocker) — BUILT 2026-07-03, scripted-choreography first pass, NOT yet compile- or play-verified.** Moves all 12 pallets off a docked trailer into staging lanes with a manned dock stocker.
- **`TrailerOffloadController`** (`Assets/_Project/Scripts/Core/Labor/TrailerOffloadController.cs`) — self-bootstrapping service (hidden `DontDestroyOnLoad` object, like the other dock services). Polls (0.5s) for a `TruckController.AwaitingOffload` truck **plus** an idle, manned dock stocker (an `MHEOperatorSlot` whose `CurrentOperator.Record.role == DockStockerOperator`). On a match it `ClaimForOffload()`s the truck, **commandeers the DS** (disables its patrol `AiNavigation` + `NavMeshAgent` so the controller drives the transform directly — same scripted style as `TruckController`'s yard route; the operator rides along as a DS child), then per pallet: line up behind it → drive forks under → lift the `Forks` child by `ForkLiftHeight` (reparent pallet to forks) → reverse out onto the dock → 180° spin → drive forks-first to the next open Inbound/Both lane slot → lower → drop → `RegisterPhysicalPallet` + assign Load ID + file a `Putaway` `WorkTask`. When all 12 are done it restores the DS (re-enable agent/nav, warp, resume patrol) and `CompleteOffload()`s the truck so it departs.
- **Movement is scripted transform choreography** (chosen over NavMesh — Tad's call — for frame-precise fork alignment and to avoid fighting the patrol AI that owns the agent). Trade-off: no obstacle avoidance on the dock→lane hop.
- **All geometry is TUNABLE constants at the top of the file** (`DriveSpeed`, `TurnSpeed`, `ForkLiftHeight=0.5`, `ForkEngageDistance`, `LineUpBackDistance`, `BackOutExtra`, `LaneStackStep`, `InvertTrailerAxis`, `ForkChildName="Forks"`). Because the controller is a hidden bootstrapped object there's no Inspector — **these are code constants tuned over chat and recompiled**; the maneuver's directions/distances are educated guesses and WILL need iteration in-game (esp. `InvertTrailerAxis` — flip it first if the DS drives away from the trailer). Promote to a scene component with `[SerializeField]`s if live slider tuning becomes worth it.
- **`TruckController` dock lifecycle reworked:** a docked truck no longer departs on the old fixed `unloadDuration` timer. It now sets `AwaitingOffload` and waits in `Docked` until `CompleteOffload()` is called, OR until `offloadFallbackTimeout` (45s) elapses **unclaimed** — the fallback path does the old bulk `ReceivingService.ProcessReceiving` + departs so a missing/unmanned DS can't wedge the dock. New public API on `TruckController`: `AwaitingOffload`, `DockedAt`, `LoadContainer`, `ClaimForOffload()`, `CompleteOffload()`.
- **Receiving/putaway-task creation moved to per-pallet-on-drop** (was bulk-on-dock-timer in Chunk 1). Each staged pallet gets its own inventory record + Load ID + Putaway task as it lands in the lane. The bulk `ReceivingService.ProcessReceiving` now only runs on the no-DS fallback path.
- **`LaneNamingService.AllLanes()`** added — returns every distinct `(door, lane-letter)` pair, so the controller can scan for the first Inbound/Both lane (`InventoryService.LaneAcceptsPutaway`) with a free slot (`TryGetNextFreeSlot`). Reminder: lanes default to `LaneUsage.Both` until a setup UI says otherwise, so any placed `Flr-ShipLane` tiles already accept inbound putaway.
- **Prerequisites to actually see it run:** (1) a dock stocker placed on the dock with a hired **DockStockerOperator** aboard (the existing hire/auto-board flow boards them — the controller needs the slot OCCUPIED, so if the operator is still walking over it just waits); (2) at least one row of `Flr-ShipLane` tiles placed as the staging lane; (3) the `palletVisualPrefab`/`Ghost While Docked Renderers` Inspector assignments from Chunk 1 done so there are pallets to move. Spawn a truck with the InboundTest panel and watch.

**Manual test tool — `InboundTestPanel`** (`Assets/_Project/Scripts/UI_UX/InboundTestPanel.cs`, added 2026-07-03): self-bootstrapping DEBUG-only OnGUI overlay (top-left, y=50 to clear the TopBar, always on in Play mode) with a button that calls `ShipmentService.CreatePurchaseOrder` with a 12-line-item test PO (SKU `035-12345`) — same hotkey pattern as the P key. Lets you watch the whole inbound loop live: truck spawns → gate queue → guard inspection (trailer doors open via existing `GuardController` behavior) → drives to dock → sits `Docked` with its pre-built cargo → `ReceivingService` fires as the timer ends → departs. The panel also lists live `ShipmentService.PendingShipments` (status flips `InTransit`→`Received`) and `WorkQueueSystem.Tasks` (the Putaway tasks created per pallet) so you can confirm Load IDs/receiving worked without reading the console. Remove or gate behind a build flag before shipping — not intended as a real gameplay UI.

**Visible cargo — built 2026-07-03, needs ONE manual Inspector step before it'll show anything.** Previously the trailer's `Trailer/LorryTrailer/Load` container had **zero `PalletBuilder` children** — `TruckController.LoadShipment()` always hit its "no PalletBuilder components found" branch and silently returned, so no cargo ever rendered regardless of SKU data. Also nothing anywhere ever called `InventoryService.LoadSkuDatabase()`, so `GetSkuData()` always returned null even for real SKUs. Fixed both:
- Created `Assets/_Project/Resources/Inventory/SKUs/SKU_035-12345.asset` (a real `SkuData` asset — Ti/Hi 1/1, non-perishable, `_casePrefab` pointing at the existing `Cs_Reg_Brn.prefab`). Placed under `Resources/` specifically so it's loadable at runtime (the other 99 Excel-imported SKUs live outside `Resources` and still aren't loaded — separate follow-up if they need real case visuals too).
- `GameContext.Awake()` now calls `inventoryService.LoadSkuDatabase(Resources.LoadAll<SkuData>("Inventory/SKUs"))` right after `inventoryService.Initialize()` — this call never existed anywhere before.
- `TruckController.LoadShipment()` rewritten: no longer searches for pre-existing `PalletBuilder`s. It now **procedurally instantiates a new `[SerializeField] palletVisualPrefab`** 12 times under `Load`, in 2 rows of 6 (`SlotLocalPosition()` — row 0 = left at `-palletLateralOffset`, row 1 = right at `+palletLateralOffset`, columns spaced by `palletRowSpacing` centered on the Load anchor), cycling through the shipment's line items so a 12x-same-SKU test shipment fills every slot. Each instance gets its `PlacedObject`/`BuildingData` stripped (same reasoning `PalletBuilder.Build()` already applies to the cases it spawns — otherwise 12 phantom (0,0) registry entries per truck) before resolving `SkuData.CasePrefab` and calling `PalletBuilder.Build(deductMoney: false)`.
- **`driverDoorOpenAngle`/`passengerDoorOpenAngle`** (new tunable fields, default ±170°) replace the old hardcoded ±65° swing — a proper "barn door" opening (doors rest flat against the trailer side) instead of just ajar, per Tad's ask.

**⚠️ `palletVisualPrefab` is a new field, currently unassigned — deliberately left for a manual step rather than hand-edited via YAML.** `ChepStack.prefab` (the intended value) is itself a multi-layer nested prefab variant chain; hand-deriving its correct root `fileID` for a raw YAML reference was judged too risky to guess blind without Editor confirmation. **To finish this: open `Truck_SavageDev.prefab`, select the root TruckController component, and drag `Assets/_Project/Prefabs/Inventory/ChepStack.prefab` into the new "Pallet Visual Prefab" field.** Until that's set, `LoadShipment()` logs a clear warning and leaves the trailer empty rather than failing silently. `palletLateralOffset`/`palletRowSpacing` are now **both 0.35** (tightened 2026-07-03 from 0.75/1.6 — the old spread clipped through the trailer edges); `LoadShipment` also now finds the `Load` container via the `Trailer/LorryTrailer/Load` path OR a recursive `FindDeepChild(transform,"Load")` fallback (the container is inside the nested trailer FBX so the exact path can drift), and every pallet is childed under that `Load` object.

**Docked finishing touches (2026-07-03) — barn doors open while docked + trailer ghosts:**
- Door open angle raised to **±185°** (`driverDoorOpenAngle`/`passengerDoorOpenAngle`, was ±170) — full flat "barn door" swing so a docked dock-stocker can reach the cargo and it's visible from inside the warehouse.
- `OnDocked()` now calls `OpenTrailerDoors()` + `SetDockedGhost(true)`; `BeginDeparture()` calls `SetDockedGhost(false)` + `CloseTrailerDoors()`. (Previously the doors only opened during the guard's gate inspection, then closed before docking — so a docked trailer was sealed.)
- **`SetDockedGhost`** swaps a set of assigned renderers to `Resources/Materials/GhostLoweredWall.mat` (the same see-through material lowered walls use) while docked, restoring each renderer's original `sharedMaterials` on departure. Handles multi-slot renderers and is idempotent. Original materials cached per-renderer in `_originalMaterials`.
- **⚠️ Second manual Inspector step (same reasoning as `palletVisualPrefab`): the new `_ghostWhileDockedRenderers` (Renderer[]) field is unassigned.** The trailer mesh + left/right Savage decal objects live inside the binary `Truck_SavageDev.fbx`, so their exact child names/`fileID`s aren't extractable from YAML to wire blind. **Assign it: open `Truck_SavageDev.prefab`, and drag the trailer mesh renderer + the two Savage decal renderers into the TruckController's "Ghost While Docked Renderers" list.** Until assigned, docking still opens the doors but the trailer stays solid (no ghost).

**Slotting (Pick Slot Assignment) — BUILT 2026-07-04, compile-verified via Unity MCP reflection check (console tool itself was broken this session — see MCP bridge notes below), not yet play-tested.** Prerequisite data/UI layer for Chunk 2 (Putaway) — deliberately built BEFORE any Putaway consumption logic per Tad: "I want the putaway logic to be based around the location of the pick slot and then get the reserves of that item as close to it as possible." Assignment is manual (a UI panel, not auto-assigned on receipt); a SKU may be assigned to multiple pick slots.

- **`SlotRegistry`** (`Assets/_Project/Scripts/Racking/SlotRegistry.cs`) — self-bootstrapping scan registry, same shape as `LaneNamingService`/`DockNumberingService` (hidden `DontDestroyOnLoad`, recomputes on `OnObjectPlaced`/`OnObjectDeleted` + 1s heartbeat). Every **live** rack has exactly 2 active `TextMeshPro` labels (the aisle-facing pair — the far face's group is disabled); the label TEXT itself ("01-02-00") is parsed directly for aisle/bay/level-char/position rather than re-derived from geometry, since that text is the only place the position digit (0/1) is ever stored. Pick vs Reserve = numeric vs alphabetic level-char (matches `LocationNameGenerator`'s own convention). Exposes `PickSlots`/`ReserveSlots`/`TryGet(address)`/`TryGetNearestReserveSlot(pickAddress)` (XZ distance between rack root positions — a stub for Chunk 2 to consume, nothing calls it yet).
- **`SlotAssignmentService`** (`Assets/_Project/Scripts/Core/Inventory/SlotAssignmentService.cs`) — plain static `Dictionary<address, skuId>`, same in-memory-only shape as `LaneConfigRegistry`. Deliberately has no dependency on `SlotRegistry` — the UI is responsible for only assigning live addresses and pruning assignments whose rack has since been deleted (`SlotAssignmentPanel.PruneOrphanedAssignments`, run on every rebuild).
- **`SlotAssignmentPanel`** (`Assets/_Project/Scripts/UI_UX/SlotAssignmentPanel.cs`) — bound to the **`6`** key (TopBarUI, same `Keyboard.current.digitNKey` + `UIModalGuard` pattern as Shift Manager's `5`). Programmatic UI Toolkit panel (ShiftManagerPanel's pattern, not RackSetupUI's uxml pattern — avoids that pattern's three documented timing footguns; this panel is a data-list-and-dropdown utility, not bespoke art). Two tabs: **By Location** (every Pick slot grouped by aisle, dropdown to assign/reassign, Clear button) and **By SKU** (pick a SKU, see/unassign all its slots, plus the **"Available Pickslots"** button below).
- **`PickSlotOverlayController`** (`Assets/_Project/Scripts/Racking/PickSlotOverlayController.cs`) — spawned on demand by "Available Pickslots" (not a persistent singleton). Recolors every live Pick slot's in-world TMP label: **red** = already assigned to some SKU (regardless of physical pallet occupancy — occupancy tracking doesn't exist until Putaway is built), **yellow** = unassigned but the candidate SKU's full pallet is too tall for that level, **green** = unassigned and fits. A small `BoxCollider` (trigger) + marker component is added to each label just for the overlay's duration (same click-detection shape as `ChevronController`: `Camera.main` + `Mouse.current` + `Physics.Raycast`); clicking a label commits the assignment immediately, no separate confirm step.
- **Height-fit math**: `SkuData.RequiredPalletHeight() = PalletBuilder.GetPrefabDimensions(CasePrefab).y * HiCount + 0.16f` (0.16m = pallet height, Tad's constant), compared against `ObjDataSO.objHeight` (already existed, already authored per rack asset — no new data needed). **`HiCount` = layers per pallet**, confirmed by reading `PalletBuilder.Build()`'s actual usage (`layers = manualHi`) — the inline comments on `SkuData.HiCount`/`SkuImporter`'s `Hi` field ("units per pallet layer") are backwards relative to how the code actually uses it; worth a comment fix sometime, not a behavior bug. `PalletBuilder.GetPrefabDimensions` was promoted from `private` to `public static` to enable reuse (it was already stateless).
- **Known limitation, not silently solved:** in-memory only, same as `ShiftManagerPanel`'s schedules — `SlotAssignmentService` is lost on domain reload/app restart. A future `Export()`/`Import()` pair (same shape as `LaneConfigRegistry`'s) is the natural follow-up once there's a save slot for it. `GameContext.Awake()` calls `SlotAssignmentService.ClearAll()` defensively in case a same-process reload path ever exists.
- **Not yet play-tested in Editor** (only compile-verified) — still needs: confirming the label recoloring actually reads clearly (the in-world "label" may be a TMP text mesh plus a separate background plate renderer — this only recolors the TMP `color`, not a plate, if there is one), confirming the `BoxCollider` click-target size feels right, and a real assign/unassign/reopen pass with committed racks in-scene.

**CHUNK 2: PUTAWAY PROCESS**
- Work queue: "Put pallet [Load ID] away"
- Reach Truck Operator drives pallet jack
- Determines Pick Slot vs Reserve based on order demand — **now backed by `SlotRegistry`/`SlotAssignmentService` above** instead of needing new data
- Sets down pallet, updates location
- Inventory system updated

#### 2026-07-18 Session — Reach-truck putaway "stairwell" livelock fixed; capacity issue still open

**IN PROGRESS — code changes committed/pushed on branch `PreRack_2ndAttempt`, NOT yet play-verified.** Continue here on the other machine.

**Symptom Tad reported:** reach truck picking up from a staging lane would, at ~the 4th/5th slot in, drive deeper and deeper and "nudge the pallet up like a stairwell" until it floated in space. I mis-diagnosed it TWICE as an approach/aim bug (rewrote the fork-insert drive) before pulling `Editor.log` telemetry, which showed the truth:

**Actual root cause = a no-rack-space LIVELOCK, not the drive.** Log sequence: 8 pallets put away fine, then `[PutawayLogic] Limbo fallback: no Available reserve slots in the entire building` (warehouse full; those SKUs also had *no pick slot assigned*, so all flooded reserve). Then the RTO **physically picked up** the pallet and only AFTER it was on the forks did `PutawayLogic.AssignPutawayDestination` return null → the abort did `SetParent(null, worldPositionStays:true)`, dropping the pallet at the lifted fork height (+~0.08m). Re-queue → re-pick → +0.08m per loop = the stairwell climb. Two tasks for the same exit slot also churned against each other.

**Fixes applied to `ReachTruckOperator.cs`:**
1. **Resolve + reserve the rack destination BEFORE the physical pickup** (moved `AssignPutawayDestination` ahead of the drive/lift). No destination → the pallet is never touched: park it in the lane, block it, bail.
2. **`_blockedUntil` dict + `NoDestinationBackoff` (15s)** — a pallet that found no destination isn't re-claimed immediately (checked in BOTH claim paths in `TryClaimAndStart`: the `alreadyAssigned` resume and the pending loop). Kills the livelock; retries every 15s in case space frees.
3. **Every post-pickup abort restores the pallet's captured original pose** (`originalPalletPos/Rot`) and calls `CancelPutaway(toAddress)` to release the reserved slot → a pallet is never left floating.
4. **`task.AssignToLocation(toAddress)` is stamped only AFTER the pallet is seated on the forks** — preserves the save/load contract (`MHEOperatorPersistenceService` routes `ResumeDeliverToRack` vs `ResumeTask` off `task.ToLocation` ⇔ a carried-pallet snapshot; stamping it pre-pickup would misroute a save taken in that window).
- Kept an earlier **closed-loop acquire** rewrite (homing to a staging point 0.6m in front → square up → short capped `InsertToGrab` with retry). It WORKS (the 8 successes went through it) and is more robust than the old open-loop `DriveForksFirst`+`DriveToGrab`, which are now **dead code** in this file. Not the bug, but fine to keep.

**STILL OPEN — the real blocker to actually finish Chunk 2:** running out of reserve slots after only **8** pallets is suspiciously low for the rack setup in Tad's screenshots. Next step: determine whether `SlotRegistry.ReserveSlots` is under-registering, `LocationStatusRegistry` is marking most slots unavailable, or pick slots simply aren't assigned (Slot Assignment panel = key **`6`**). Confirm via `Editor.log` after a run.

**Bridge/verification note (important, wasted time this session):** the Unity MCP bridge here (CodeMaestro executor) supports `manage_editor` / `manage_scene` / `find_gameobjects` / resources, but **`read_console`, `manage_console`, `execute_code`, `validate_script`, `refresh_unity` all fail** ("Unknown/unsupported command" or "No Unity Editor instances found"). To verify compiles/runtime: read `%LOCALAPPDATA%/Unity/Editor/Editor.log` from disk (grep `error CS`, `[ReachTruckOperator]`, `[PutawayLogic]`, `[TrailerOffload]`), and compare `Library/ScriptAssemblies/Assembly-CSharp.dll` mtime vs the source `.cs` mtime to confirm a build actually happened.

**Also changed `TrailerOffloadController.cs` (dock stocker) same session, also unverified in-play:** (a) trailer grab order is now depth-then-Y-descending so it takes the **top** of a stack first (was leaving top pallets hanging); (b) lane-fill `TryFindLaneTarget` rewritten to **walk in from the entry and stop at the first occupied slot** (stack the frontier if it has room, else the slot before it) so the DS can't route *through* occupied lane positions.

#### 2026-07-25 Session — Root cause of "capacity issue" found: LocationData (Inspector) and LocationStatusRegistry (real gatekeeper) are two separate stores that only sync one-way

**Answers the "running out of reserve slots after only 8 pallets" question left open 2026-07-18 above — it was never really a capacity/SlotRegistry problem.** `SlotRegistry`/`LocationRegistry` were registering slots fine. The actual bug: `PutawayLogic` only ever checks `LocationStatusRegistry` (a static `Dictionary<string,LocationStatus>`, not visible anywhere in the Inspector, but IS the thing saved/loaded via `PlacementSystem.cs`'s `save.locationStatuses`). `LocationData` (the MonoBehaviour on each rack slot child — what you actually see in the Inspector) is a fresh, **non-persisted** object every session that defaults to `Available`. `LocationData.Reserve()/Occupy()/Release()` correctly mirror into the registry, but `PutawayLogic.AssignPutawayDestination()`/`CompletePutaway()` and `ReplenishmentService.CreateReplenishTask()` (which locks both a pick and reserve slot the instant a task is *created*, before any truck claims it) write **directly** into the registry, bypassing `LocationData` entirely. Net effect: the registry (what the reach truck actually obeys) and the Inspector (what you look at) can silently disagree, and the Inspector always loses the argument.

**Live-diagnosed via `Unity_execute_code` reflection against Tad's actual running scene (186 total rack slots: 146 reserve, 40 pick):**
- **2 slots permanently stuck `Reserved`** (`01-02-D0`, `01-08-C0`) with zero matching WorkTask anywhere in the 66 live tasks — orphaned. Root cause: a `Reserved` lock written into the registry survives a save (`PlacementSystem.cs` exports it) and gets re-imported on the next load, but if the owning task didn't survive (didn't stamp `ToLocation` before the save, or its coroutine died to a deleted vehicle/fired employee/domain reload), nothing is ever left to call `CompletePutaway`/`CancelPutaway` on it again — permanently "full" to `PutawayLogic` forever.
- **50 of 186 slots (27%!) showed `LocationData.Status = Available` with a blank `PalletId` while the registry said `Occupied`.** This is almost certainly `ReachTruckOperator.DeliverPalletToRack` calling `locationTr.GetComponent<LocationData>()` on whatever Transform `FindLocationTransform` resolved — and `FindLocationTransform` has documented fallback branches ("last resort: bare direct child, no anchor" / global `GameObject.Find`) that return a Transform **without** a `LocationData` component for racks whose prefab doesn't match the expected label structure (see the `Rack-FullYellow48` mirrored-label-group note earlier in this doc). When that happens, the `if (locationData != null)` guard silently no-ops the Inspector-side `Occupy()` while `_putawayLogic.CompletePutaway()` right below it unconditionally marks the registry Occupied anyway. Net result: a real, correctly-placed pallet's slot can permanently read "Available" in the Inspector.
- **Reserve availability at time of check: 0 of 146 available** (144 Occupied + 2 orphaned Reserved) — console showed `PutawayLogic` repeatedly hitting limbo fallback ("No available reserve found ... using limbo fallback") for multiple SKUs. Whether those 144 "Occupied" reserves are all genuinely full (real pallets — meaning the reach truck's behavior was correct all along and this was purely a display bug) or include some phantom/stale entries (registry says Occupied, no physical pallet backs it) was **not fully confirmed** — Unity exited Play Mode mid-session when the fix's script edits triggered a domain reload (per [[domain-reload-kills-live-services]]), ending the live diagnostic before that specific check could run. Re-verify next session: re-enter Play, reload the save, and spot-check a few of the addresses above (e.g. `01-02-A0`/`01-02-A1`/`01-02-B0`) against whether a pallet is actually physically sitting there.

**Fixes applied (compiled clean, verified via `read_console`):**
1. **`LocationStatusRegistry.ReleaseUnclaimedReservations(ICollection<string> claimedAddresses)`** (new method) — releases any `Reserved` address not referenced as a `FromLocation`/`ToLocation` by a non-`Complete` WorkTask. Wired into `PlacementSystem.cs` right after `LocationStatusRegistry.Import(save.locationStatuses)` on load, using the just-restored `workQueueSystem.Tasks`. Cleans up orphaned Reserved locks every time a save is loaded, going forward.
2. **`LocationData.SyncStatusDisplay(LocationStatus status)`** (new method, sets `_status` directly, does NOT write back to the registry) — called from `LocationRegistry.Recompute()` (already running on its existing ~1s heartbeat) for every live address, pulling the true value from `LocationStatusRegistry.Get(address)`. This is a blanket fix: it keeps the Inspector honest relative to the registry regardless of which upstream code path set the registry value, so this exact class of "Inspector says Available, real gatekeeper disagrees" confusion can't recur even from some other future write path.

**Still open / needs next-session confirmation:** whether any of the 144 "Occupied" reserves are phantom (no real backing pallet) rather than genuinely full. If Tad finds any address that shows "Available, no pallet physically there" even AFTER these fixes and a fresh reload, that's a third, distinct bug (bad occupancy data with no orphan-reservation or display-staleness explanation) and needs its own investigation — likely starting with `LocationRegistry.ReconcilePhysicalOccupancy()`'s 0.25m physical-proximity tolerance possibly being too tight for some rack/pallet placements.

**CHUNK 3: REPLENISHMENT PROCESS**
- Pick slot monitored for occupancy
- Threshold: <2 cases remaining
- Work queue: "Replenish [location]"
- Reach Truck Operator fills from reserve
- Pick slot stays stocked

**CHUNK 4: ORDER SELECTION PROCESS**
- Customer order arrives (e.g., 12 pallets)
- Empty outbound trailer backed into assigned door
- Order assigned to door/trailer/staging lane
- Work queue: "Order [ID] ready for selection"
- Order Selector picks cases (hand animation)
- Max 2 pallets per selector jack
- Cases placed on jack, staged to outbound lane
- Inventory updated (staging lane location)

**CHUNK 5: SHIPPING PROCESS**
- Work queue: "Load order [ID]"
- Loader animation (walking + placing)
- Pallets moved from staging to trailer
- Trailer status: "Ready to depart"
- Driver departs (visual: trailer exits yard)
- Order status: "Shipped"
- Triggers invoicing

**CHUNK 6: INVOICING PROCESS**
- Customer charged: Cost of Goods + $1/case handling fee
- Revenue credited to MoneyService + FinanceCategory (Sales)
- Order marked "Invoiced"
- Daily summary updated

### Work Queue System (Cross-Cutting)

All actions driven by work queue entries:
- Signals employees via events
- Tracks task progress
- Auto-closes when complete
- Prioritization: TBD (FIFO initially)

### Key Components (TBD During Architecture Review)

- `POSystem` — Purchase order creation/tracking
- `TrailerArrivalEvent` — PO match, signals dock
- `ReceiverAnimationService` — Clipboard + RF gun
- `LoadIDGenerator` — 10-digit pallet IDs
- `InventoryEntity` — Pallet in system (SKU, qty, load ID, location)
- `WorkQueueSystem` — Task queuing + assignment
- `PickSlot` — Component on rack cells; replenishment triggers
- `ReserveSlot` — Component on reserve racks
- `OutboundTrailerAssignment` — Trailer ↔ Door ↔ Staging Lane
- `OrderSelectionTask` — Order picking progress
- `OrderSelectorAnimation` — Hand-picking + jack load
- `OutboundStagingLane` — Per-door staging zone
- `LoaderTask` + `LoaderAnimation` — Loading animation
- `InvoicingService` — Calculate charges, credit revenue

---


