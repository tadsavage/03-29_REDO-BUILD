# Bug cleanup plan - after player block 1029 (2026-11-04)

Score 61.4 (UI showed 62.9 live). 21 shipped, 6 late, 184 pallets, fill 97.8%. Reliability 3.8/100 is driven by the bug monitor counting
duplicates and load artifacts, not by 79 separate faults.

## Real, worth fixing (in order)
1. **Stale duplicate PalletPick holds a finished order / truck.** OG8851 was fully loaded (472/472, 12 shipped) but a second Available
   PalletPick for an already-moved pallet (c10db6b9, STG2C-4 after STG2C-5 completed) kept "Still to pick: 1" and blocked close-out.
   Fix: close-out ignores/cancels live PalletPick tasks once every case is picked; find where the duplicate is filed (lane re-source retry).
2. **Planned pallet tasks burned on empty pick faces (OG3814).** With every pick face at 0 and no replenish task filed, each selector
   completed its planned-pallet task with nothing picked; the order could never recover and was cut to 0. Fix: a plan task that finds nothing
   goes back to Available (not Complete); do not release planned work while its pick faces are empty and replenishment has not landed;
   check pick slots stuck "Occupied" with no pallet (phantom occupancy) stopping ReplenishmentService.
3. **Duplicate pallet registration after a load** (RegisterPhysicalPallet + RegisterPalletDirect on the same cell -> the "A Chep & A Chep"
   OVERLAPs and FLOATING at 12:54 on day 5, the reload). Most of the 1,899 OVERLAP / 922 FLOATING lines are this.
4. **"closed with a STAGED / IN TRANSIT pallet that never made the trailer"** (3). Needs a look together with item 1.
5. **Two MissingReferenceExceptions** (NavMeshAgent / Transform destroyed) - add null guards.
6. **Edge-of-floor and in-rack NavMesh targets unreachable** ("cannot reach task position", "stuck ... walking out"). Already tracked in CLAUDE.md.

## Probably play issues, not bugs
- "no reachable pick location ... staging short at x/y" when stock really is out (fill-rate cuts). Becomes real only in the empty-pick-face case in item 2.
- `REPEATING x25 PLAYER BLOCK DONE` - the monitor counting its own end-of-block message.
- "Arrival callback ... refusing to start receiving" - the guard doing its job.
- ORPHAN-OUTBOUND / GHOST-SLOT / NO-RECORD - mostly left behind by the F5/F9 tests run this session.

## Suggested next steps
- Tell the bug monitor to ignore its own block-end line and dedupe load-time overlaps (so Reliability reflects real faults).
- Do items 1 and 2 first (both stalled real orders), then 3.
- Re-run a block with no code changes between to see what remains.
