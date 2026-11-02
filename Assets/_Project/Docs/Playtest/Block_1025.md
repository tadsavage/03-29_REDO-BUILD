# Playtest block 1025 — days 1–8

## Score: **56.3** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (18 shipped, 10 late) | 15% | 41.2 |
| Fill rate | 10% | 94.2 |
| Throughput (165 plt vs target 84) | 15% | 100.0 |
| Revenue ($154,215) | 15% | 100.0 |
| Profit (cash -380,733) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 3.0 |
| Reputation (0 to 131) | 5% | 100.0 |

Orders shipped **18**, late/fined **10**, pallets shipped **165**. Cash $500,000 → $119,267 (-380,733). Stock at cost $0 → $59,720 (not part of profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Late orders - state when it came due
- 1 x loaded but the truck had not left

- day 7: OG7324 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,942. Lines 23, 436 cs, released=True.

## Bugs found (62 distinct, 425 incidents)

| Incidents | Bug |
|---|---|
| 175 | OVERLAP —  A Chep & A Chep @(#, #) |
| 36 | OVERLAP —  A Chep & A Chep @#B# |
| 17 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 14 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 13 | FLOATING —  OutboundPallet_##a#d#bf#-a#-#_restored @#D# bottom=# |
| 13 | OVERLAP —  OutboundPallet_##a#d#bf#-a#-#_restored & OutboundPallet_##a#d#bf#-a#-#_restored @#D# |
| 11 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 11 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 8 | FLOATING —  A Chep @#B# bottom=# |
| 7 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 7 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 6 | FLOATING —  OutboundPallet_##a#c##fa#be#_# @#B# bottom=# |
| 6 | OVERLAP —  OutboundPallet_##a#c##fa#be#_# & OutboundPallet_##a#c##fa#be#_# @#B# |
| 5 | OVERLAP —  A Chep & A Chep @#D# |
| 5 | FLOATING —  A Chep @#D# bottom=# |
| 4 | OVERLAP —  A Chep & A Chep @#A# |
| 4 | FLOATING —  A Chep @#A# bottom=# |
| 4 | FLOATING —  OutboundPallet_##e##fa-b###_# @#A# bottom=# |
| 4 | FLOATING —  OutboundPallet_#-d#db#-b#-#_restored @#A# bottom=# |
| 4 | OVERLAP —  OutboundPallet_#-d#db#-b#-#_restored & OutboundPallet_##a#d#bf#-a#-#_restored @#A# |
| 3 | FLOATING —  OutboundPallet_#-d#db#-b#-#_restored @#E# bottom=# |
| 3 | OVERLAP —  OutboundPallet_##a#d#bf#-a#-#_restored & OutboundPallet_##a#d#bf#-a#-#_restored @#B# |
| 3 | FLOATING —  OutboundPallet_##a#d#bf#-a#-#_restored @#B# bottom=# |
| 7 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Emma Clark: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##e##fa-b###_# @#A# |
| 2 | OVERLAP —  OutboundPallet_##e##fa-b###_# & OutboundPallet_##e##fa-b###_restored @#A# |
| 2 | OVERLAP —  OutboundPallet_##e##fa-b###_# & OutboundPallet_##e##fa-b###_# @#A# |
| 2 | OVERLAP —  OutboundPallet_#-d#db#-b#-#_restored & OutboundPallet_#-d#db#-b#-#_restored @#E# |
| 2 | OVERLAP —  OutboundPallet_#-da##e#e-b#f###_# & OutboundPallet_#-da##e#e-b#f###_# @#A# |
| 2 | FLOATING —  OutboundPallet_#-da##e#e-b#f###_# @#A# bottom=# |
| 4 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #B and it never freed up (held by reach truck RT_Full(Clone) (Nicole Walker, PalletPick) for #s) — giving up and re-queueing. |
| 1 | ERROR — [MHEBoundary] 'PalletJack' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | GHOST-SLOT — RECORD # # x# cell=(#, #) lane= - no object |
| 1 | FLOATING —  OutboundPallet_##e##fa-b###_# @#B# bottom=# |
| 1 | OVERLAP —  OutboundPallet_##e##fa-b###_# & OutboundPallet_##e##fa-b###_# @#B# |
| 1 | FLOATING —  A Chep @#E# bottom=# |
| 1 | OVERLAP —  A Chep & A Chep @#E# |
| 1 | OVERLAP —  A Chep & OutboundPallet_#-d#db#-b#-#_restored @#E# |
| 1 | OVERLAP —  A Chep & Pallet_#_Slot#_Tier# @#A# |
| 3 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | FLOATING —  OutboundPallet_##a#c##fa#be#_# @#D# bottom=# |
| 1 | OVERLAP —  OutboundPallet_##a#c##fa#be#_# & OutboundPallet_##a#c##fa#be#_# @#D# |
| 1 | OVERLAP —  OutboundPallet_##a#c##fa#be#_# & OutboundPallet_##a#c##fa#be#_# @#A# |
| 1 | FLOATING —  OutboundPallet_##a#c##fa#be#_# @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##a##ba##bdf-#_# @#A# |
| 1 | FLOATING —  OutboundPallet_##a##ba##bdf-#_# @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-a#c###e#-#_# @#B# |
| 1 | FLOATING —  OutboundPallet_#-a#c###e#-#_# @#B# bottom=# |
| 1 | FLOATING —  OutboundPallet_#-b##c#a###_# @#A# bottom=# |
| 1 | OVERLAP —  OutboundPallet_#-b##c#a###_# & OutboundPallet_#-b##c#a###_# @#A# |
| 1 | NO-TASK —  lane pallet #A# sku=# load=# |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Emma Clark: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | STALE-ASSIGNED —  OrderSelect Select pallet # of # for Sneaky Pete's Seafood (# cs) #s |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Emma Clark: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 2 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #B and it never freed up (held by reach truck RT_Full(Clone) (Paul Hamilton, PalletPick) for #s) — giving up and re-queueing. |
| 1 | ORPHAN-OUTBOUND —  #B# (order ##c#f#e#c### gone) |
| 1 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #A and it never freed up — giving up and re-queueing. |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jacqueline Sanders' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | WARN — [AiNavigation] Employee_Henry Perez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Carol Morris' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | WARN — [ReachTruckOperator] Replenish: could not reach pallet at ### within #m. |

## First sightings (with stack traces)

- **OVERLAP** (day 7): OVERLAP OutboundPallet_9226e170-da27-4e8e-b5f3-25b6f49157db_1 & OutboundPallet_9226e170-da27-4e8e-b5f3-25b6f49157db_2 @1A-2
- **FLOATING** (day 7): FLOATING OutboundPallet_9226e170-da27-4e8e-b5f3-25b6f49157db_2 @1A-2 bottom=2.19

## Bot actions (first 400)
