# Handoff notes (2026-11-03) - ADIOS

## Where we are
Roadmap step 1 (close the core loop). Decision today: bot scores mixed bot mistakes with game bugs, so the gate to start step 2 (tutorial) is now ONE HUMAN-PLAYED GAME WEEK on `three doors aisle`, no manual rescues, then triage its bug log.

## Set up for the human run
- `PlaytestBotSettings.asset`: `buildOnly: 1`, `layoutName: three doors aisle`. INITIATE HAL 9000 on the main menu builds the warehouse + crew, pauses on "You're up boss!!!", no bot acts.
- Check a `BugLog_Block_10xx.md` appears (observe-only PLAYER block, started by PlayerScoreHud). If it does not, the recorder did not start: look at that first.
- Small Layout.json and 1 door.json are EMPTY (no small tutorial layout exists yet). Building one in-game and saving it from the HAL 9000 tab is the better tutorial base.
- Do not edit scripts during Play. Stop Play first to clear leftover PlaytestBot objects.

## Agreed daily goals (tutorial-hard)
On-time 90%, late fines $0, pallets 12, cases 500, revenue $8,500, net profit $1,500, inbound trailers 3, outbound trailers 2, same-day orders 1, fill rate 90%, reputation +8, cash above $15,000. (Dollar figures use rough $700-900/pallet, ~40 cases/pallet; unmeasured margin.)

## Code changed this session (compiled clean, NOT play-verified beyond block 36)
- MheParking/EquipmentBattery: idle/parking spots must be reachable for the agent type (path-invalid 21 -> 0 in block 35, but 12 again in block 36).
- Hot replenishment (priority 900) + HOT LIST scratch run for shorted cases, picked before any other selector work (WorkQueueSystem, ReplenishmentService, OrderService, WorkQueuePanel label).
- Outbound trucks wait until loaded or released; driver leaves by himself after 8 h (DockScheduleService.OutboundDriverPatienceMinutes, TruckController).
- `[ShipDecision]` diagnostic log at ship time.

## Block 36 result (23.4) - why
Fresh game, $0 stock. Sneaky Pete's orders had no stock, got released, bounced to Open, bot re-released 634 times; 2 orders shipped (8/221 cases). Hot list never triggered (nothing to short-pick). Likely cause: bot did not buy those SKUs (maybe rep-gated vendors) - unconfirmed.

## Known open bugs (real, not bot)
Vehicles sent to unreachable targets near x -44 and x 3-4 / z 44-48; stranded NavMesh islands; one truck stalled in DepartToApproach; unstockable-SKU orders bounce to Open with no clear player response; staged pallets not persisted across save/load; full-pallet wholesale fulfilment missing.
