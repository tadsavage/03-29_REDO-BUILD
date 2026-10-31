# Playtest block 1023 — days 1–8

## Score: **15.3** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (16 shipped, 18 late) | 15% | 18.2 |
| Fill rate | 10% | 46.5 |
| Throughput (8 plt vs target 84) | 15% | 9.5 |
| Revenue ($5,080) | 15% | 9.1 |
| Profit (cash + stock -188,087) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 34.7 |
| Reliability (bugs) | 25% | 3.5 |
| Reputation (0 to 0) | 5% | 50.0 |

Orders shipped **16**, late/fined **18**, pallets shipped **8**. Cash $133,010 → $-136,828 (-269,838). Stock at cost $0 → $81,751; cash + stock -188,087.
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Bugs found (57 distinct, 225 incidents)

| Incidents | Bug |
|---|---|
| 30 | OVERLAP —  Pallet_#_Slot#_Tier# & A Chep @(#, #) |
| 22 | OVERLAP —  A Chep & Pallet_#_Slot#_Tier# @(#, #) |
| 18 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 16 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 10 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 7 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 7 | FLOATING —  A Chep @#B# bottom=# |
| 6 | FLOATING —  Pallet_#_Slot#_Tier# @#D# bottom=# |
| 6 | FLOATING —  Pallet_#_Slot#_Tier# @#E# bottom=# |
| 5 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E# |
| 5 | OVERLAP —  A Chep & A Chep @#D# |
| 4 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 4 | OVERLAP —  A Chep & A Chep @#B# |
| 3 | ERROR — [MHEBoundary] 'DS_FULL(Clone)' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 3 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 3 | OVERLAP —  OutboundPallet_##f##f-bf##b#cfc#_# & OutboundPallet_##f##f-bf##b#cfc#_# @#E# |
| 3 | GHOST-SLOT — RECORD # # x# cell=(#, #) lane= - no object |
| 3 | OVERLAP —  A Chep & A Chep @#C# |
| 3 | FLOATING —  A Chep @#C# bottom=# |
| 3 | OVERLAP —  OutboundPallet_###e#-a#b###_# & OutboundPallet_###e#-a#b###_# @#A# |
| 3 | FLOATING —  A Chep @#E# bottom=# |
| 3 | OVERLAP —  A Chep & A Chep @#E# |
| 2 | FLOATING —  OutboundPallet_##f##f-bf##b#cfc#_# @#E# bottom=# |
| 3 | REPEATING — x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it. |
| 2 | NO-RECORD —  lane pallet #C# Pallet_#_Slot#_Tier# |
| 5 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Madison Shelton: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | REPEATING — x# and counting: [MHEBoundary] 'DS_FULL(Clone)' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 2 | STALE-ASSIGNED —  PalletPick Pallet pick — # cs of # for Sneaky Pete's Seafood #s |
| 1 | EXCEPTION — MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are still trying to access it. |
| 4 | WARN — [AiNavigation] Employee_Tyler Walker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 3 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | TRUCK-STALLED —  Truck→PO_IG# state=DepartToApproach #s at (#, #, #) |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#D# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##f##f-bf##b#cfc#_# @#E# |
| 1 | NO-RECORD —  lane pallet #A# Pallet_#_Slot#_Tier# |
| 1 | GHOST-SLOT — SLOT ##-A# names # - no object |
| 1 | GHOST-SLOT — SLOT ##-B# names # - no object |
| 1 | GHOST-SLOT — SLOT ### names # - no object |
| 1 | ERROR — [ReceiverReceivingWorkflow] No master record for pallet #-f##c-b### |
| 1 | OVERLAP —  OutboundPallet_###b###ae#bae#_restored & OutboundPallet_###b###ae#bae#_restored @#B# |
| 1 | ERROR — [ReceiverReceivingWorkflow] No master record for pallet ###b#-ac### |
| 1 | ERROR — [MHEBoundary] 'RT_Full(Clone)' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 1 | FLOATING —  OutboundPallet_###e#-a#b###_# @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & A Chep @#E# |
| 1 | ERROR — [MHEBoundary] 'DS_FULL(Clone)' keeps driving out of the building (# times in #s, at (#, #, #)). Not putting it back any more; something is steering it outside. |
| 2 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 2 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #C and it never freed up — giving up and re-queueing. |
| 1 | NO-TASK —  lane pallet #A# sku=# load=# |
| 2 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #A and it never freed up — giving up and re-queueing. |
| 1 | REPEATING — x# and counting: [AiNavigation] PltJack_FULL(Clone): no progress toward task target (#, #, #) for #s (stopped Infinitym short, path PathComplete) — treating as arrived. |
| 1 | REPEATING — x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=Infinity pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=False onMesh=True jackBlocked=False elapsed=#s reissues=# |
| 2 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #B and it never freed up — giving up and re-queueing. |
| 1 | WARN — [OrderService] Order OG# closed with a LOADING pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #E and it never freed up — giving up and re-queueing. |
| 1 | WARN — [AiNavigation] Employee_Kimberly Reyes: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **FLOATING** (day 7): FLOATING A Chep @1E-2 bottom=1.30
- **OVERLAP** (day 7): OVERLAP A Chep & A Chep @1E-2
- **ERROR** (day 7): [MHEBoundary] 'DS_FULL(Clone)' keeps driving out of the building (6 times in 10s, at (-2.40, 1.11, 13.27)). Not putting it back any more; something is steering it outside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:85)
  
  ```
- **WARN** (day 7): [AiNavigation] Employee_Kimberly Reyes: stuck at (-3.50, 1.25, 38.42) — walking out to (-9.98, 1.14, 26.69) (12.40m to mesh).

## Bot actions (first 400)
