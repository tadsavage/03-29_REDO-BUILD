# Handoff to GitHub Copilot (written 2026-10-27 by Claude)

Read `CLAUDE.md` (repo root) first; it is the detailed project log. This file is the short "where we are / what to do next".

## Project
Unity 6 (URP) warehouse-sim. Roadmap step 1 of 7 is **close the core loop** (order, receive, putaway, pick, ship, get paid) before the tutorial.
Roadmap detail: `.claude/skills/roadmap/SKILL.md`. System skills (work-queue, order-system, inventory-system, finance-system, warehouse-strategy...) are in `.claude/skills/`.
Do not build ideas from `FUTURE_IDEAS.md` unasked.

## Step 1 exit criteria
Three 7-day HAL blocks in a row, same layout, no code changes between them, each with: on-time >= 80%, zero deadlocked doors/lanes, zero `REPEATING` bugs,
positive profit, no manual intervention.

## HAL 9000 (the playtest bot) - how it works
- Code: `Assets/_Project/Scripts/1. FSM/Core/Diagnostics/PlaytestBot.cs` (+ HalPlanner/HalBuilder/HalMetrics/HalActions). Settings asset: `Resources/PlaytestBotSettings.asset` (gameSpeed 5).
- Start a block: Dev Console (key 1) > HAL 9000 tab. A block = 7 game days. Reports land in `Assets/_Project/Docs/Playtest/` (Block_NN.md, BugLog_Block_NN.md, Scoreboard.md).
- **Rules:** never edit scripts while Play mode is running (a recompile wipes ServiceLocator/EventManager; the sim silently dies). Change ONE thing per block so scores compare.
  Never delete the user's own saves. Compile gate: wait until `Library/ScriptAssemblies/*.dll` is newer than the edited .cs, then check `Logs/Editor.log` for `error CS`.
  Unity only recompiles when its window has focus.
- Scoreboard so far: B11 51.9, B12 51.7, B13 44.8, B14 39.3 (collapsed: both doors taken by case-pick trailers, one selector, late release).

## Done in the last session (committed as "Block 15")
1. `OrderService.ReleaseFullyShippedTrucks`: a docked outbound trailer whose cargo is all Shipped leaves even if its orders carry a different door number (block 14: order OG2443 had dock door 1 but the truck sat at door 2 forever).
2. `OrderSelectionTaskDriver.PlacePalletsAtStagingSlot`: when the lane fills mid-drop, try another lane (TryFindTripLane) instead of dropping a pallet where the loader will not look.
3. `OrderService.FindScratchReadyDoors`: counts only the orders on the docked truck's own appointment.
4. `OrderService.SettleFloatingOutboundPallets` (3-game-minute tick): lowers outbound pallets hanging in the air.
5. `DockScheduleService.FitsOneTrailer`: leaves room for a part-built pallet per order when orders share a trailer. Routine "slot elapsed" warning downgraded to a log line.
6. `EquipmentBattery`: an idle selector sitting on an unclaimed pallet jack is stepped off after 45 s; new `StandingStillOnPurpose` flag.
7. `PlaytestBot`: door-reserve rule in `BookDock` (case-pick trailers may not take every door; exception only if all stock is already in hand and there is time),
   release lead = own pick time + buffer, role-aware VEHICLE-STATIONARY detector (only counts Available tasks the vehicle's role can take; skips machines standing still on purpose).
   **This starts a NEW scoreboard series: block 15 is not directly comparable to blocks 1-14.**
8. Outbound truck cab paints darker (`Actors/TruckLivery.cs`). Ultra depth of field switched from Bokeh to Gaussian far-field-only (`UI_UX/FreeLookCamera.cs`) so rack labels stay readable.
**None of items 1-8 has been play-tested.** Verify compile (check `Logs/Editor.log` for `error CS`), then run HAL block 15 and read `Block_15.md` / `BugLog_Block_15.md`.

## Known facts worth knowing
- Reach trucks / dock stockers with a driver and no work are SUPPOSED to stop: charge, park at an out-of-the-way spot, take an occasional "bored lap" (`EquipmentBattery.IdleRoutine`). That is not a bug.
- A task cancelled because an order took the lane pallet directly is expected (cancelled Putaway rows).
- Remaining block 12-14 problems, by priority: close-out leaves pallets behind on shared trailers (partly mitigated, root cause unconfirmed - watch a live load),
  orphaned outbound pallets in lanes after an order is archived, repeated NavMesh-island strandings (maybe the idle-parking spot picker choosing cells in isolated pockets:
  reject cells with no reachable patrol waypoint), received lane pallets with no task (NO-TASK), a Receiver stuck-flag watchdog firing repeatedly.
- Door "% Complete Loading" divides by the full order, so an order with cancelled cases can never show 100%.
- Not built (decided not worth it for HAL): immediate selector hire, an add-a-door action. HAL is a tool for finding game bugs, not a goal in itself.
- Tad wants the Ultra depth of field to look cleaner; if Gaussian still looks bad, tune `gaussianStart`/`gaussianEnd` in `FreeLookCamera.cs`.

## Warehouse-strategy rules (Tad) - see `.claude/skills/warehouse-strategy/SKILL.md`
Never fill every door with outbound case-pick trailers; keep one door free for inbound unless all product is already in hand with time to pick it. Release work by its pick time.

## Session 2026-10-27 (later) - metrics, truck bars, Inbound filters, glow (all compiled, none play-tested)
Full detail is the last addendum in `CLAUDE.md` ("Addendum 2026-10-27 (later)"). Short version:
1. `OpsMetrics` time-study recorder (gate-to-door, offload, offload lap, receiving incl. gaps, load cycle/total, pallet pick, case pick; standard = avg x 1.15). **Reworked 2026-10-28 (see below): samples are now split by who was playing.**
2. Truck modal: pallet-pick (orange) and case-pick (yellow) bars + per-item tags.
3. Inbound Order Creation: filter bar pinned above the scroll, margin ordering button, multi-select item checkboxes.
4. Glow: `TruckHighlighter` rescans for missed renderers; `OrderFreightHighlighter` rebuilds stale pallet boxes.
Next: play-test these, then smoking area + ashtray, then turn the METRICS standards into scoring goals once they settle. Still step 1 of the roadmap (close the core loop).

## Session 2026-10-28 - metrics split into Bot / Player / Blended (compiled, NOT play-tested)
Tad's ask: the METRICS tab becomes **BOT METRICS** (recorded while HAL plays), a new **PLAYER METRICS** tab (recorded while a human plays; HAL only watching counts as human), and a
**METRICS BLENDED** tab (bot + player together = the labor standards). The data must live in its own folder, survive saves, and become the long-term reference for game balance,
difficulty levels, achievements, goals and labor standards.
- `Assets/_Project/Scripts/1. FSM/Core/Diagnostics/OpsMetrics.cs` rewritten: `Source { Bot, Player }`, `CurrentSource` (Bot only while `PlaytestBot.Instance.Running && !ObserveOnly`),
  `Get(kind, source)`, `GetBlended(kind)`, `Clear(source)`, public `Save()`.
- Storage: `Application.persistentDataPath/Metrics/bot_metrics.json` and `player_metrics.json` (NOT in the save files). In the editor each save is also mirrored to
  `<project>/Metrics/*.json` (outside Assets, so no reimport churn, git-trackable so the data travels between machines; a fresh machine seeds its persistent copy from the mirror).
  Each sample carries minutes, units, real UTC ticks and the difficulty (`PlayerPrefs "Difficulty"`) so it can be sliced later. Last 300 per kind per source. Saved every 10 samples and on quit.
- The old single `ops_metrics.json` is migrated once into the Bot file (those samples came from HAL runs).
- `UI_UX/HalSummaryWindow.cs`: tabs are now SUMMARY / ITERATIONS / ALL CHANGES / BOT METRICS (3) / PLAYER METRICS (4) / METRICS BLENDED (5); `RenderMetrics(mode)`; Blended rows also show the bot/player sample split;
  Bot and Player tabs have their own clear button, Blended has none. Tab labels are 12 pt with tighter padding so six tabs fit.
- To verify: compile clean, run one HAL block and one human session, check both JSON files appear in `persistentDataPath/Metrics` and in `<project>/Metrics`, and that the three tabs fill.
- Open idea, not built: turn the blended standards into scoring goals (partnership, reputation, Reliability) once they settle; smoking area + ashtray for idle workers.

## Status (Tad: "we are almost at 100%")
Everything above is committed except the 2026-10-28 metrics split (uncommitted at the time of writing). Nothing from "Block 15" onward has been play-tested. First thing to do: open Unity, let it compile
(check `Logs/Editor.log` for `error CS`), start HAL block 15, and read `Block_15.md` / `BugLog_Block_15.md`. Change ONE thing per block.
