# Player block fix list (2026-11-05)

Collected during Tad's player block (score 60.1, new high). **All items were done the same night** (compiled clean, Play
smoke-tested: top bar populated, 5x/8x buttons, Bank take/pay/pay-off/weekly-payment verified live). NOT yet proven in a
played block - watch for the new log lines: "will ship SHORT", "gave up waiting for a charger", "linked it to the waiting
pallet pick", "More order selectors than pallet jacks", "made no progress ... for 10 game minutes".

Notes on choices made without Tad:
- Bank: the starting capital is the warehouse MORTGAGE on a NEW game (cash stays, debt = same, net worth $0), 104 weeks.
  Existing saves have no mortgage. Weekly payment is amortised, not "25% of revenue". HAL does not take loans.
- Hand picking stays only when the building has NO pallet jack at all (so a jack-less warehouse can't stall).
- Spacing: non-carving personal-space obstacle (0.25 m) on MANNED machines whose own agent is off. Worker radius unchanged.
- Order tiers replace the contract's LineItems/CasesPerLine ranges for recurring orders; 28% second order per delivery day.

## Bugs, highest first

1. **Item Listing hides shortages on Staged orders.** `ItemAvailability.Build` counts Pending only while an order is picking or has a live pick task. A Staged order's unpicked cases drop out, so it shows 0 scratches and no red rows. OG6866 (Barnacle Bill's) shipped 115/140 because of this.
   - Count every unshipped order's unpicked cases (not cancelled) as Pending, whatever its status.
   - Scheduler item table (`SchedulerPanel.BuildItemAvailabilityTable`): the trailer's own unpicked cases turn the row red.
   - `LoadProgressCard`: the "N out of N plts. of case pick staged" line says when staged pallets are missing cases.
   - New message log warning: "order staged short, no stock or inbound PO for X".
2. **Top bar blank (Capital, Day/Time, Rep).** `GraphicsPresetManager.ApplyPreset` runs before `Awake` (from `TopBarSettingsPanel.ApplyGraphicsPreset` via `TopBarUI.Init`). It throws a NullReferenceException on `_urp`/`_profiles` and aborts the top bar's setup. Fix: lazy-init the arrays.
3. **Charger traffic jam / deadlock.**
   - Queue spots sit on the bay line.
   - `NeedsCharge` stays set after charging.
   - A queued helper blocks a bay it has to vacate.
   - Add a watchdog of about 3 game minutes that resets the queue and sends machines above 30% back to work.
4. **Charging rules.**
   - Go only at 20% or less, and only after finishing the current pallet task (putaway, replen, pallet pick or staging).
   - Remove the idle top-ups (below 0.95 and below 0.6) and the coverage rule.
   - At 0%, deliver the pallet first, then go at reduced speed.
   - Charge to full, or to 80% if another machine is waiting.
5. **Stalled picks (Sneaky Pete's OG4856).**
   - No-progress watchdog from 30 to 10 game minutes.
   - Re-release to any free selector.
   - Stage early near door time so in-transit pallets don't miss the trailer.
6. **Orphaned order pallet (OG6862 Raisins).**
   - Before the "standing in lane with no task" sweep files a Putaway, re-link the pallet to an open order that needs it, and move it lane to lane.
   - Pallet picks may source from loose lane pallets.
   - Fix the mid-move "lost (cancelled) while still needed".
7. **Can't click a pallet on a jack.** The jack's trigger wins the raycast. Prefer a carried pallet (or one within about 2 m behind the first hit) and open the pallet pick-progress card.
8. **Spacing between machines and people (0.2-0.25 m).**
   - Non-carving `NavMeshObstacle` on MHE.
   - Larger worker avoidance radius.
   - Clearance check before scripted moves.
   - No parking or set-down on another machine's footprint.
9. **Truck timer bar** stays on from gate in to gate out. Remove `Hide()` in `OnDocked` and the 3-frame auto-hide. Show "Unloading/Loading at Door N" plus progress while docked.
10. **Inbound truck card:** the vendor icon overlaps the header text. Widen the icon frame and the card, and keep the icon size.
11. **Remove the log message** "...will be here at... nothing further to order" (`SchedulerWarningService` line 139).

11b. **Selectors picking by hand: 4 selectors, 3 pallet jacks.** Live check: every jack was running, and Heather Martinez had none, so the fallback put her on foot. The "no free pallet jack" toast fires once per session (`s_warnedNoJack`), so it was never shown again on day 5-6.
   - Re-warn once per game day while a selector is picking by hand, and name the selector.
   - **Decided (Tad): no hand picking.** A selector with no free jack does not take case-pick work; it waits (idle at the jack bays) until a jack is free. Check the HAL bot / soak runs still finish without the fallback.
   - Optional: show "selectors vs jacks" on the hiring board / build menu so the shortage is visible.

## Tuning

12. **Good DoF matches Ultra** (zoneGood 1.0 to 0.6).
13. **Order frequency and size.**
    - About 28% chance of a second order per delivery day.
    - Size tiers:
      - Small (35%): 1-2 pallets, case pick.
      - Medium (45%): about 12 pallets, 9-10 full-pallet picks plus 2-3 case-pick pallets.
      - Large (20%): up to 20 pallets, mostly case pick.

14. **Speed bar gets 5x and 8x buttons** (`TopBarUI` speed buttons, currently 0, 0.25, 0.5, 1, 2, 3). Check that anything frame-dependent copes at 8x (reach-truck grab distance caps, `speed*deltaTime` steps, watchdog timers).

## Feature (after the bugs)

15. **Bank and loans.**
    - $0 start; the warehouse build cost becomes a mortgage, paid weekly.
    - APR by reputation band.
    - Pay Extra, Pay Off and Refinance.
    - Bank window from the Capital panel, styled like contract negotiation. Offers: Starter, Equipment, Expansion and Line of Credit, with locked offers greyed.

## Notes from the block

- OG6861 (Sneaky Pete's) and OG6866 (Barnacle Bill's): no pallets left behind; both trucks left with what was loaded.
- OG7867 (Barnacle Bill's next order) needs Raisins, Salad Dressing, Juice and Cereal, all at 0 on hand.
