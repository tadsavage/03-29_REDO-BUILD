# Playtest block 1024 — days 1–8

## Score: **30.6** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (13 shipped, 12 late) | 15% | 14.3 |
| Fill rate | 10% | 100.0 |
| Throughput (20 plt vs target 84) | 15% | 23.8 |
| Revenue ($14,744) | 15% | 26.3 |
| Profit (cash -406,918) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 3.9 |
| Reputation (0 to 104) | 5% | 100.0 |

Orders shipped **13**, late/fined **12**, pallets shipped **20**. Cash $500,000 → $93,082 (-406,918). Stock at cost $0 → $61,158 (not part of profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Late orders - state when it came due
- 1 x still being picked

- day 7: OG7300 (Sneaky Pete's Seafood) was still being picked (0/480 cs). Fined $2,512. Lines 21, 480 cs, released=True.

## Bugs found (61 distinct, 143 incidents)

| Incidents | Bug |
|---|---|
| 9 | NO-RECORD —  lane pallet #C# A Chep |
| 8 | NO-RECORD —  lane pallet #B# A Chep |
| 7 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 6 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 5 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 5 | OVERLAP —  A Chep & OutboundPallet_###a-a###_restored @#B# |
| 4 | OVERLAP —  A Chep & A Chep @(#, #) |
| 4 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 4 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 3 | NO-RECORD —  lane pallet #A# A Chep |
| 3 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 3 | OVERLAP —  OutboundPallet_#-d#ef#a#a-a###_# & OutboundPallet_#-d#ef#a#a-a###_# @#A# |
| 3 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_###a-a###_# @#B# |
| 3 | FLOATING —  A Chep @#B# bottom=# |
| 2 | ERROR — [MHEBoundary] 'DS_FULL(Clone)' fell below the floor at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 3 | NO-TASK —  lane pallet #A# sku=# load=# |
| 6 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | FLOATING —  OutboundPallet_#-d#ef#a#a-a###_# @#A# bottom=# |
| 2 | FLOATING —  OutboundPallet_##b###ed###_# @#A# bottom=# |
| 2 | OVERLAP —  OutboundPallet_##b###ed###_# & OutboundPallet_##b###ed###_# @#A# |
| 2 | OVERLAP —  OutboundPallet_#-e#f#d#bf##_restored & OutboundPallet_#-e#f#d#bf##_restored @#A# |
| 2 | OVERLAP —  A Chep & A Chep @#A# |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E# |
| 2 | FLOATING —  OutboundPallet_###a-a###_restored @#B# bottom=# |
| 2 | OVERLAP —  OutboundPallet_###a-a###_restored & OutboundPallet_###a-a###_restored @#B# |
| 2 | FLOATING —  OutboundPallet_###a-a###_# @#A# bottom=# |
| 2 | STALE-ASSIGNED —  Putaway Putaway pallet [#] staged at #A# #s |
| 2 | NO-TASK —  lane pallet #C# sku=# load=# |
| 4 | WARN — [AiNavigation] Employee_Carolyn Ramirez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #B and it never freed up — giving up and re-queueing. |
| 4 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it.  —  lane #C: lane #E:  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A: lane #D: lane #C:  |
| 1 | GHOST-SLOT — SLOT ##-A# names # - no object |
| 1 | GHOST-SLOT — SLOT ### names # - no object |
| 1 | GHOST-SLOT — RECORD # # x# cell=(#, #) lane= - no object |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#D# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#D# bottom=# |
| 1 | OVERLAP —  A Chep & A Chep @#D# |
| 1 | OVERLAP —  A Chep & OutboundPallet_###b-a###_# @#C# |
| 1 | OVERLAP —  A Chep & OutboundPallet_#-d#ef#a#a-a###_# @#A# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-ce##b#b#d-#_# @#A# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & A Chep @#A# |
| 1 | ERROR — [ReceiverReceivingWorkflow] No master record for pallet ##e##fe##ba#-# |
| 1 | FLOATING —  OutboundPallet_#-e#f#d#bf##_restored @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & A Chep @#C# |
| 1 | OVERLAP —  A Chep & A Chep @#B# |
| 1 | OVERLAP —  A Chep & OutboundPallet_###a-a###_# @#C# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_###a-a###_# @#A# |
| 1 | FLOATING —  OutboundPallet_###a-a###_# @#C# bottom=# |
| 1 | OVERLAP —  OutboundPallet_###a-a###_# & OutboundPallet_###a-a###_# @#A# |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Ann Miller: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Carolyn Ramirez: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Ann Miller: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 1 | REPEATING — x# and counting: [EquipmentBattery] DS_FULL(Clone): not sliding # m by hand - that is a navigation job. Trying the path again. |
| 2 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #A and it never freed up — giving up and re-queueing. |
| 1 | REPEATING — x# and counting: [OrderService] Settled a floating lane pallet down # m (it had nothing under it). |
| 1 | REPEATING — x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=# pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=False onMesh=True jackBlocked=False elapsed=#s reissues=# |
| 1 | REPEATING — x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=# pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=True onMesh=True jackBlocked=True elapsed=#s reissues=# |
| 1 | WARN — [AiNavigation] Employee_Ann Miller: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Debra Beckett: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

_none_

## Bot actions (first 400)
