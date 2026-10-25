---
name: warehouse-strategy
description: The player's real planning logic for running the warehouse (what a good operator decides, and why), and how it is translated into the HAL 9000 playtest bot's buying, booking and release decisions. Use whenever the task is about HAL's decision-making, "smarter" buying/booking/release timing, order planning, how long things take on the floor, or when Tad explains how he plays. Read this BEFORE changing PlaytestBot.cs decisions (Purchase, BookDock, ReleaseOrders, SignContracts), and ADD to it whenever Tad explains another rule of play.
---

# Warehouse strategy (the operator's playbook)

This skill is **Tad's own way of playing, written down**, so the bot (HAL 9000) can make decisions with strategy instead of one-line triggers
("buy if X", "release after 12 h"). Tad runs this warehouse for a living; his rules beat anything inferred from the code. When he explains a
rule, record it here in his terms first, then say how it maps to code. Keep the "why" - the why is what lets us extend it later.

**Principle (Tad, 2026-10-25):** *You never want to be in a position where your doors are occupied and the product you need to ship is not
in house yet.* Plan backwards from the appointment: what has to be true, by when, for this trailer to leave full.

## Design philosophy (Tad, 2026-10-25) - READ BEFORE ANY BALANCE OR PROCESS ADVICE

**This is a GAME with challenges, not an autonomous simulator to watch from an armchair.** Keeping up the dock is the PLAYER's responsibility:
in a real warehouse you "blow up the dock" when you cannot keep up. If the player cannot keep up they buy more doors, hire more people, or - if they
cannot afford that - sign fewer contracts. So do NOT make the game protect the player from over-committing (no auto-reserving lanes for a whole
order, no auto-throttling of contracts). Fix BUGS and unfair/hidden failures; keep the pressure and the consequences. When reviewing balance or
process changes, ask "does this keep the player's decisions meaningful?", not "does this make the warehouse run smoothly by itself?".

**Staging lanes (rules, 2026-10-25):** exclusivity is per LANE: one customer per lane, but one DOOR may hold several customers (1A one customer, 1B
another). Search order for a stager: the door they want first, then the adjacent doors left and right, outward, for an empty lane. If there is none,
wait until space opens ANYWHERE, even if that means commingling customers in one lane (last resort only). The loader digs pallets out. Code:
`StagingLaneAssignmentService` (`EnumerateEmptyLanes` = per-lane, `EnumerateSharedLanes` = last resort, used after all empty lanes). The old door-wide
"one customer per door" rule capped a 2-door layout at 2 customers staging at once and, with the Load-task bug, left orders Open for days.

**Inbound hold (Tad, 2026-10-25):** while an inbound trailer is docked at a door, outbound stops considering ONLY the lanes that trailer needs, not the whole
door. Example: a 5-lane door with a 2-pallet trailer holds back 1 lane. Computed like the dock stocker picks lanes (letter order, skipping lanes
that don't take inbound or that outbound already holds, taking free room until the trailer's remaining pallets are covered):
`StagingLaneAssignmentService.LanesReservedForInbound`. **`AutoReserveLanesForInbound` is the difficulty hook**: true = automatic (default);
false = nothing is held back and the player must keep inbound and outbound apart themselves (a harder mode Tad may add).

## 1. First question for any offer: full-pallet or case-pick?

- **Full-pallet pick** ("straight roll-on, straight roll-off" when it is a drop-off): every line is a whole number of pallets. Far less
  planning. A pallet pick is done by the reach truck, not the selectors.
- **Case pick**: at least one line leaves loose cases. These become selector picklists. Much slower and needs real planning.
- **Mixed** (case pick + pallet pick): the pallet picks are another role and almost always finish long before the selectors do, so the
  pallet part adds **nothing** to the timeline. The case part decides it.

## 2. Full-pallet orders: the "4 or 5 hour block" rule

- If there is an open window of **about 4-5 hours** before the appointment, it is a no-brainer: just order the product and let it come in.
- A full-pallet turn takes **under 5 hours including receiving the product that covers it**.
- If the stock is already on hand it is shorter: only staging + loading remain.

## 3. Case-pick orders: estimate the picking, then work backwards

1. Work out **when everything is in** (on hand, or inbound with a slot + receiving).
2. Count the **picklists** (selector trips; each carries up to 2 pallets). In the Work Queue these are the "GroSel" picklists.
3. Each picklist takes about **2 - 2.5 game hours**. Selectors work in parallel, so
   `rounds = ceil(picklists / selectors)` and `pick time = rounds x ~2.25 h`.
   Example (Tad): several picklists and 2 selectors can take **6-7 game hours**. He notes this is **too long even by game standards** and
   should be tuned down **later, once the core loop is bug-free** (it is a setting, see below).
4. The appointment must be at least: *stock ready + pick time + load*.
5. **Scratch runs:** if not everything is in yet, you end up running out to pick at the last minute. With **rush orders turned off** that
   is not an option, so do not book the trailer until the stock is in or has a slot.

## 4. Order of operations (how a good operator sequences it)

1. Offer arrives -> classify (section 1).
2. Cover the stock: buy for the order (soonest due first, only when the whole order's shortfall is affordable).
3. Book the **inbound** PO slots first (earliest free).
4. Book the **outbound** trailer for stock-ready + work time, never earlier.
5. **Release the order to the floor about 3 hours before the appointment** (not earlier: it fills lanes with pallets that then wait).

## How this is wired in code

All in `Assets/_Project/Scripts/1. FSM/Core/Diagnostics/` (PlaytestBot.cs, PlaytestBotSettings.cs). Settings live in
`Resources/PlaytestBotSettings.asset` (since 2026-10-25 the asset really is linked to the class; before that every edit was ignored).

| Rule | Code | Setting |
|---|---|---|
| Buy to cover whole orders, soonest due first | `PlaytestBot.PlanOrderCoverage` | `coverRushOrders` (off), `inventoryValueCap`, `maxDailySpendFraction` |
| Inbound booked before outbound | `BookDock` step 1 (parked inbound placed first) | - |
| Outbound only when stock is here or booked inbound | `BuildStockTimeline`, `TryStockReadyHour` | - |
| Pallet-only vs case-pick turn time | `TurnHoursFor` | `palletTurnHours` (1.5), `pickHoursPerPicklist` (2.25), `loadBufferHours` (1) |
| Wait for inbound to be received before it counts | `BuildStockTimeline` | `inboundBufferHours` (3) |
| Optional extra minimum lead | `BookDock` | `outboundLeadHours` (0; Tad: the 24 h rule "isn't necessary") |
| Release ~3 h before the appointment | `ReleaseOrders` | `releaseLeadHours` (3) |
| Do not sign bulk without stock covered | `SignContracts`, `CoveredPallets` | `minPalletsBeforeBulk`, `bulkCashMultiple` |

## Status / history

- 2026-10-25: first version written from Tad's explanation. **Compiled? Not yet - written while block 9 was running; it takes effect from the
  next Play after block 9 ends.** Block 9 ran WITHOUT this (empty trailers sat at two doors with nothing to ship).
- Early results to look for in block 10: bookings logged as `(pallet-only: ...)` or `(case-pick, N picklist(s) / M selector(s): ...)`, no outbound
  trailer waiting on a door for stock, fewer late fines.

## Bug vs. bad play, and DO NOT CHANGE THE SCORING (Tad, 2026-10-25)

A **bug** is the game doing something wrong. **Bad play** (wrong stock bought, a trailer with nothing to ship, an idle worker) is not a bug - but
**never change how the score or the bug counts are computed**: Tad wants the block-to-block trend to stay comparable. When reading a block
report, separate the two *in the analysis*, not in the code: e.g. `VEHICLE-STATIONARY` counts any claimable-by-anyone task, so most of those flags
are idle vehicles, and "no reserve or pick-face pallet ... left" / "found no Available Putaway ... tasks" are consequences of the bot's buying.
(A role-aware stationary check and a "play issues" list were tried on 2026-10-25 and REVERTED for this reason. If scoring ever must change, version
it: start a new scoreboard series, do not edit history.)

## Player mode: Tad plays, HAL scores (written 2026-10-25, compiled after block 10)

`PlaytestBot.StartBlock(observeOnly: true)` = HAL only WATCHES. `Decide()` is skipped (no contracts, bookings, releases, buying, boarding), the planner
changes nothing, and the game speed is the player's. Score, bug monitor and report work exactly as for HAL. Player blocks are numbered **1001+**
(reports `Block_1001.md`, `BugLog_Block_1001.md`) and go in `player_scoreboard.json`, NEVER into `scoreboard.json` (that is HAL's trend).
- Start: load your save, Dev Console (key 1) > HAL 9000 tab > **I'LL PLAY (SCORE ME)**. Day count starts from that moment; a block is `daysPerBlock` (7) days.
- UI (`UI_UX/PlayerScoreHud.cs`): big live score top-centre under the Day box (gold while beating HAL's best block), and at the end a results modal
  (HAL's best per category vs yours) + `ShippedFanfare.ShowBanner` if you beat HAL's best overall. CONTINUE closes it and resumes at 1x.
- The bottom-bar HAL score modal is hidden during a player block.

## Not done yet / ideas to push next (Tad wants this thinking to keep going)

- The estimate uses the **number of selectors hired**, not their availability (they also do returns, other orders). A queue-aware estimate
  (picklists already waiting in the Work Queue ahead of this order) would be more honest.
- **Reach-truck capacity** for big pallet-only orders (many pallet picks) is not modelled - one truck takes time per pallet.
- **Dock capacity**: the same doors take inbound and outbound; plan them together, not just inbound-first.
- Use **vendor lead time** and reputation (some vendors short-ship) when deciding how early to book inbound.
- Decide when to **decline** an offer (cannot be covered in time, or margin too thin) instead of accepting and fining.
- Pick time per picklist should come from **measured** data (the Work Queue's actual pick durations) instead of a constant.
- Wholesale/bulk full-pallet orders: the mechanic that moves whole pallets reserve -> staging (see order-system skill KNOWN GAP).
- Tune `pickHoursPerPicklist` down (and check selector speed) after the core loop is bug-free - Tad flagged 6-7 h as too long.
