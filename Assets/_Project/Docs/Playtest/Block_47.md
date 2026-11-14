# Playtest block 47 — days 1–3

## Score: **30.9 (-11.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 1 late) | 10% | 66.7 (-33.3) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (10 plt vs target 24) | 10% | 41.7 (-16.7) |
| Revenue ($27,882) | not scored | 100.0 (0.0) |
| Net Profit (-56,633) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 14.8 (-8.8) |
| Reputation (19 to 5) | 10% | 26.7 (-25.0) |

Orders shipped **2**, late/fined **1**, pallets shipped **10**. Cash $127,764 → $138,348 (+10,584). Stock at cost $200,106 → $132,889 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 9 dock bookings, 1 orders released, 5 POs, 0 rush POs.

## Where the money went

Income **$27,882**. Expenses **$17,298** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 27,090 |
| Handling | 792 |

| Expense | $ |
|---|---|
| Inventory | 10,280 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Fines | 980 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x loaded but the truck had not left

- day 2: OG4718 (Neon Nectar Juice Co.) was loaded but the truck had not left. Fined $980. Lines 1, 280 cs, released=True.

## Change since last block
- Fixed / no longer seen: **3**
  - WARN|[OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
- New this block: **15**
  - ERROR|[ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #A: [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: own/Staged/y#] 
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (aisle entry)' ((#, #, #)) after #s.
  - REPEATING|x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#).
  - WARN|[OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - WARN|[OrderSelectionTaskDriver] Order ##c##bd-ae#e## (Mama Tomato Sauces) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##ba##c#f-a#fb-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##e##ab#f## (Neon Nectar Juice Co.) has no reachable pick location for its remaining line item(s) — staging short at #/#.

## Bugs found (25 distinct, 69 incidents)

| Incidents | Bug |
|---|---|
| 13 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#df] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 4 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A: [#: own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #B: [#: own/Staged/y#]  |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #C: [#: own/Staged/y#]  |
| 3 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop. |
| 3 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (aisle entry)' ((#, #, #)) after #s. |
| 2 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 2 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#). |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##c##bd-ae#e## (Mama Tomato Sauces) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##ba##c#f-a#fb-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##e##ab#f## (Neon Nectar Juice Co.) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-1.76, 1.11, 52.10) — walking out to (-1.19, 1.22, 50.72) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-6.36, 1.12, 58.59) — walking out to (-12.86, 1.17, 47.34) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-3.47, 1.12, 59.12) — walking out to (-3.47, 1.24, 46.12) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-38.47, 1.11, 42.34).
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (-3.21, 1.11, 57.57) — walking out to (-3.21, 1.16, 59.00) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-33.80, 1.12, 46.54) — walking out to (-40.28, 0.04, 34.58) (12.60m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-32.32, 1.12, 49.73) — walking out to (-21.07, 1.12, 42.95) (12.14m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-37.06, 1.12, 47.66) — walking out to (-37.06, 0.04, 34.46) (12.20m to mesh).
- **REPEATING** (day 1): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-08 (aisle entry)' (8.4m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 01-08' - driving straight to the stop instead.
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-08' (10.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] DS_FULL(Clone): stuck at (3.26, 1.11, 52.39) — walking out to (-3.24, 1.25, 41.13) (12.00m to mesh).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order e0e75bca-1c45-42bd-ae3e-8518cfeb877a (Mama Tomato Sauces) has no reachable pick location for its remaining line item(s) — staging short at 54/57.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 2371db5b-4ba1-4c6f-a5fb-dbdcbd441229 (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at 34/59.
- **ERROR** (day 2): [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane 2A: NO NAVMESH PATH. anchor=(-4.69, 1.14, 48.99) → driveTarget=(-4.69, 1.24, 48.99), from=(-32.73, 1.17, 40.25), elapsed=31.3s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=32.7, reissues=0, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [0x002df] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1247 < ] 392.4s ago. Refusing to straight-line through obstacles — aborting this leg.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2868)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] Order b72defae-0fda-441b-9c33-e78561011633 (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 40/48.
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack did not reach 'bay 01-02 (aisle entry)' ((-16.26, 1.24, 42.34)) after 8.8s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__211:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2151)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 21.3m short of 'bay 01-01 (aisle entry)' - re-issuing the drive (2/6).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order e80b74d2-49e2-45ab-952f-7503ec4d9e76 (Neon Nectar Juice Co.) has no reachable pick location for its remaining line item(s) — staging short at 11/45.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-5.38, 1.11, 58.20) runs through something solid - taking a clear way out to (-5.38, 1.16, 59.00) instead.
- **DOOR-DEADLOCK** (day 2): OG3672 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2A: [1: own/Staged/y1.2] 
- **DOOR-DEADLOCK** (day 2): OG3675 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2B: [1: own/Staged/y1.2] 
- **DOOR-DEADLOCK** (day 2): OG3678 is Staged but door 2's truck has waited 90 min with no one loading it. | lane 2C: [1: own/Staged/y1.2] 

## Bot actions (first 400)
- d1 signed bulk BULK_Neon Nectar Juice Co._D1_0
- d1 PO IG1980 Bulk Basin Foods: 5 plt $2,400
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 3
- d1 booked Neon Nectar Juice Co. day 2 block 38 door 2 (pallet-only, 7 pallet pick(s) / 2 reach truck(s): stock ready hour 18 + 24.9h of work)
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 2
- d1 PO IG1981 Cornerstone Staples: 5 plt $2,400
- d1 placed parked Inbound Cornerstone Staples day 1 block 44 door 1
- d2 released 1 order(s): OG4718 (trailer 18.3 h away; 16.5 h work + 2 h)
- d2 PO IG2982 Riverside Housewares: 5 plt $2,400
- d2 placed parked Inbound Riverside Housewares day 2 block 14 door 1
- d2 PO IG2983 Riverside Housewares: 1 plt $480
- d2 placed parked Inbound Riverside Housewares day 2 block 29 door 1
- d2 PO IG2984 Anchor Point Foods: 5 plt $2,600
- d2 placed parked Inbound Anchor Point Foods day 2 block 32 door 2
