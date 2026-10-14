---
description: Working on anything financial — MoneyService, EconomyService, PayrollService, FinanceCategory/GL_Line routing, hourly costs, wages and overtime, lease, lifetime and spent-today counters, income (Case Pick / Handling / Sales), late fees and fines, refunds and sell-back, difficulty starting capital, and the finance UIs (Capital, Hourly, Spent Today, Time/Shift panels, TopBar money, FloatingMoneyText, MoneyFlightFx). Use whenever the task mentions money, capital, cost, price, wage, payroll, GL line, expense, income, revenue, profit, refund, fee, fine, billing, lease, the TopBar panels, or files MoneyService.cs / EconomyService.cs / PayrollService.cs / FinanceCategory.cs / FinancialBreakdownPanel.cs / CapitalSummaryPanel.cs / SpentTodayPanel.cs / FinanceUIKit.cs.
---

# Financial tracking

Companions: `order-system` (billing, fees, vendor prices), `inventory-system` (spoilage write-offs), `unity:ui-uitk`. The long-form history is the "Economy & Financial Reporting System" section of `CLAUDE.md`.

Files: `Assets/_Project/Scripts/Core/Managers/TimeAndMoney/` (`MoneyService`, `FinanceCategory`, `SimulationTimeService`, `GameContext`), `Assets/_Project/Scripts/1. FSM/1. Services/` (`EconomyService`, `PayrollService`), `.../UI_UX/` (`CapitalSummaryPanel`, `FinancialBreakdownPanel`, `SpentTodayPanel`, `ShiftStatusPanel`, `FinanceUIKit`, `TopBarUI`, `FloatingMoneyText`, `MoneyFlightFx`).

## The model: two parallel cost pipelines + income
1. **Object hourly costs** — `EconomyService`, from `ObjDataSO.hourlyCost`, pooled per **`GL_Line`** (not per object).
2. **Employee wages** — `PayrollService`, from `EmployeeRecord.hourlyWage`, paid once per in-game hour. Never double-charged: `EconomyService.IsWageTracked` excludes `category == "Worker" || "Staff"` (employee bodies carry a `PlacedObject`).
3. **One-time costs** — `PlaceCommand`/`MoveCommand`/`DeleteCommand` via `MoneyService.Deduct(amount, category)`.
4. **Income** — `AddCapital(amount, reason)`; goods → Case Pick, handling fees → Handling, plus Sales/Revenue categories. Fees/fines are expenses.
Money is whole dollars (int). Time: 1 real second = 1 in-game minute (`SimulationTimeService`, `GameEvents.Time.*`).

## MoneyService (plain class via `GameContext`/`ServiceLocator`)
- Balance: `CanAfford`, `AddCapital`, `RemoveCapital` (2-arg records under `reason`; 3-arg `(amount, category, detailKey)` records under the finer key — never both), `Deduct`/`ReverseDeduct` (one-time purchase + its undo, category-tagged), `Refund`, `CalculateRefund`, `SellBackRate` (0.5/0.75/1.0 by difficulty).
- **`RestoreCapital(int)`** sets the balance after a save load without touching any ledger. Never use `SetMoney` for that: it books the delta as a Debug EXPENSE (it once showed 99% of lifetime expenses as one fake load correction).
- Events: `OnMoneyChanged` fires on every change; `ResetDailySpending()` on day change.
- Counters: lifetime `_lifetimeExpenses/_lifetimeIncome/_lifetimeDetail (category → detailKey → amount)`; **two separate "today" counters** — `_spentTodayHourlyOnly` (recurring: fed ONLY by `RecordHourlySpend` from EconomyService and PayrollService) and `_spentTodayByObjectCategory` (one-time purchases, fed ONLY by `Deduct`). A new one-time cost site must go through `Deduct()`; a new recurring site must call `RecordHourlySpend` alongside its real `RemoveCapital`.
- Difficulty (`PlayerPrefs "Difficulty"`, `GameContext.Start`): Clerk $120k/100% sell-back, Supervisor $100k/75%, Manager $80k/50%. Balance numbers are placeholders.

## FinanceCategory = single source of truth for strings and routing
- `IncomeOrder` / `ExpenseOrder` fix row order in every panel.
- `ForGLLine(string)` maps a GL_Line to a top-level bucket. **Any new distinct GL_Line value needs a case here or it silently falls into Maintenance** (bitten twice).
- `ForWageGLLine(EmployeeRole)` → (top category, detail key). Exterminator→Contract Labor, Security→Loss Prevention, Sanitation→Sanitation, TruckDriver→Transportation (out of Wages); the rest tiered Floor / Hourly / Salary wages.
- Known per-asset GL overrides (Doors, Transportation, Dock Stocker / Pallet Jack / Reach Truck → MHE Costs, Sanitation, Contract Labor) are listed in `CLAUDE.md`.
- Known gap: one-time costs still carry the raw `ObjDataSO.category` in LIFETIME totals, so the Hourly tab doesn't show them (Spent Today does).

## EconomyService
- Subscribes to `GameEvents.Build.OnObjectPlaced/OnObjectDeleted`; pools hourly cost per GL_Line; accrues each in-game minute in `OnMinutePassed`.
- **The fractional-carry accumulator (`_fractionalByGLLine`) is load-bearing.** Without it `CeilToInt(hourly/60)` floors every line at $1/min and unrelated categories show identical lifetime totals.
- **`RebuildFromRegistry()` after every save load / scene start / manual rebuild**, alongside `PlacementGrid.RebuildFromRegistry()` (objects restored from saves bypass `PlaceCommand`).
- `ChargeLease()` on `OnDayChanged`: `(Width × Height) / 10` per day — a stand-in.

## PayrollService
Pays every Active, non-`SystemManaged` employee hourly through `ForWageGLLine`, increments `totalWagesPaid`. Overtime = 1.5× when `ShiftSchedule.IsOvertime(shift, hour)` (Day 08–16, Evening 16–24, Night 0–8, Flexible never). Shift windows are fixed constants; `ShiftManagerPanel` templates are UI-only and not wired. `EmployeeStatSystem.TickDay` (fatigue/morale) is not called anywhere in play.

## Where orders touch money
`OrderService.ShipOrder` bills goods and handling fees; `OrderService.OnDayChanged` charges late fees (`OrderData.LateFeePercent`, 0 = legacy 25%); `FineMissedDeadline` / `FineMovedOffRequestedSlot`; `ShipmentService` credits missed freight via `ReverseDeduct`; vendor purchases use market price × vendor multiplier. See `order-system`.

## UI (all implement `ITopBarPanel`, share `FinanceUIKit`)
| TopBar label | Panel | Shows |
|---|---|---|
| Capital | `CapitalSummaryPanel` | Revenue breakdown + ONE rolled-up Total Expenses + Net Profit |
| Hourly | `FinancialBreakdownPanel` | Expenses only, per-category with hover tooltips |
| Spent Today | `SpentTodayPanel` | Total Hourly Expenses + Purchases by category |
| Time | `ShiftStatusPanel` | Hours left in shift + count of employees working overtime |
- `TopBarUI.ToggleExclusive` keeps one open; `PollPanelAutoClose` (100 ms) closes after 1500 ms unhovered. Use **`.StartingIn(ms)`, not `.ExecuteLater`**, to delay a first run.
- Tooltip retract: one `TooltipEntry` list and ONE shared idle timer created once in the constructor. Do not go back to a per-event scheduled item.
- Floating money (`FloatingMoneyText`) and `MoneyFlightFx` (close-out sweep) are feedback: unscaled time. The money sweep is UI Toolkit because world-space text is hidden behind full-screen modals.
- TopBar text with per-frame changes needs fixed width; the Dev HUD card is `flexShrink = 0` with summed widths.

## Gotchas
- **Restart Play after any code change before testing money.** A recompile nulls `EventManager.Instance`/`ServiceLocator`/`GameContext` services; payroll and upkeep stop but the TopBar keeps showing stale numbers, which reads as a data bug.
- A reported "burn rate" problem is usually a ledger artifact; query `GetLifetimeDetail` per category before balancing.
- UI that reads money in a loop must not allocate; `OnMoneyChanged` is frequent.
- Add categories to `FinanceCategory` first, then GL_Line values on assets, then panels — in that order.

## Verification
Check `ServiceLocator.Get<MoneyService>()` totals before/after the action through `Unity_RunCommand`/`execute_code`; confirm the right panel (Spent Today vs Hourly vs Capital) shows the change, with the Debug category at $0 after a load.
