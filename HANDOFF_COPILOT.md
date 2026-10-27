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
