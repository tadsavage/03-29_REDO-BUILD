# Playtest block 1090 — days 1–8

## Score: **47.9** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (21 shipped, 12 late) | 10% | 42.9 |
| Fill rate | 10% | 86.7 |
| Throughput (96 plt vs target 84) | 10% | 100.0 |
| Revenue ($202,109) | not scored | 100.0 |
| Net Profit (+14,523) | 10% | 100.0 |
| Cash health (floor $15,000) | 0% | 100.0 |
| Reliability (bugs) | 50% | 9.9 |
| Reputation (0 to 34) | 10% | 100.0 |

Orders shipped **21**, late/fined **12**, pallets shipped **96**. Cash $500,000 → $84,996 (-415,004). Stock at cost $0 → $103,468 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went
*A save load zeroed the money ledger during this block, so this covers only the time since the load.*

Income **$159,866**. Expenses **$131,693** (stock bought shows as Inventory). Loan principal repaid **$5,000** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 156,669 |
| Handling | 3,197 |

| Expense | $ |
|---|---|
| Inventory | 78,714 |
| Fines | 25,358 |
| Lease / Mortgage | 9,068 |
| Transportation | 6,570 |
| Wages | 6,368 |
| Loan Principal | 5,000 |
| Maintenance | 3,783 |
| Groundskeeping | 1,786 |
| Loss Prevention | 46 |

## Late orders - state when it came due
- 4 x loaded but the truck had not left
- 2 x still being picked
- 1 x staged but no trailer loaded it
- 1 x still loading

- day 4: OG5529 (Honest Harvest Rice) was staged but no trailer loaded it (door 3). Fined $8,522. Lines 3, 1042 cs, released=True.
- day 4: OG4525 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $2,330. Lines 20, 529 cs, released=True.
- day 4: OG4526 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,489. Lines 50, 345 cs, released=True.
- day 5: OG4530 (Crunch-Time Cereals) was loaded but the truck had not left. Fined $6,096. Lines 3, 936 cs, released=True.
- day 5: OG4532 (Blocky Bakeshop) was still being picked (55/165 cs). Fined $1,072. Lines 1, 165 cs, released=True.
- day 5: OG5537 (Twisted Twine Pasta) was still loading when it came due. Fined $761. Lines 1, 145 cs, released=True.
- day 6: OG7538 (Odd-Shape Orchards) was loaded but the truck had not left. Fined $1,296. Lines 2, 306 cs, released=True.
- day 6: OG6535 (Barnacle Bill's Bait & Bite) was still being picked (14/14 cs). Fined $68. Lines 14, 14 cs, released=True.

## Bugs found (46 distinct, 109 incidents)

| Incidents | Bug |
|---|---|
| 18 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 17 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Steven Johnson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [OrderSelectionTaskDriver] Order #-a##de-aad#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #A line-up' (#m short) - giving up on this stop. |
| 5 | WARN — [AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 3 | WARN — [AiNavigation] Employee_David Cross: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] Employee_Matthew Davis: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 3 | WARN — [AiNavigation] Employee_Scott Rivers cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 3 | WARN — [AiNavigation] Employee_Scott Rivers: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #B line-up' (#m short) - giving up on this stop. |
| 1 | GHOST-RECORD —  # sku=# lane=#D |
| 1 | GHOST-SLOT — RECORD # # x# cell=(#, #) lane=#D - no object |
| 1 | GHOST-SLOT — RECORD # # x# cell=(#, #) lane= - no object |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'return pallet pickup' (#m short) - giving up on this stop. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #D line-up' (#m short) - giving up on this stop. |
| 1 | ERROR — "GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 2 | WARN — [AiNavigation] Employee_Christine Shelton: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##a##aae#f### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Order OG# pallet # of #: every pick face is empty - handing the task back until replenishment stocks them. |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##aec#ed-a### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 2 | WARN — [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | NO-TASK —  lane pallet #E# sku=# load=# |
| 1 | REPEATING — x# and counting: [JackStall] 'bay ##' at (#, #, #) (target (#, #, #)) remaining=# pathStatus=PathComplete hasPath=True pathPending=False desiredVel=(#, #, #) stopped=True onMesh=True jackBlocked=True elapsed=#s reissues=# avoidPrio=# vel=# [solid PlacementController/YardGroundPlane]  |
| 1 | ORPHAN-OUTBOUND —  #A# (order #-a##de-aad#-# gone) |
| 1 | ORPHAN-OUTBOUND —  #A# (order ##aec#ed-a### gone) |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#). |
| 1 | ORPHAN-OUTBOUND —  #B# (order #-adbc#d#c-b#a#-# gone) |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'staging lane #A line-up' - re-issuing the drive (#/#). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-dae##cf-bc#c-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Matthew Davis cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Matthew Davis: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-c##ac#-a#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_David Cross': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Christine Shelton': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##bf##bfb-a#be-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-f#c##e#-a#b### (Mama Tomato Sauces) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e##a#b#e#b-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-f#f##b#f## (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##ec#f##c### (Bramble & Bite Berry Co.) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **GHOST-RECORD** (day 4): GHOST-RECORD 3832201f sku=105672 lane=2D
- **GHOST-SLOT** (day 4): RECORD 3832201f 105672 x80 cell=(49, 84) lane=2D - no object
- **GHOST-SLOT** (day 4): RECORD b3cbf35f 105672 x80 cell=(-1, -1) lane= - no object
- **WARN** (day 4): [AiNavigation] Employee_Matthew Davis cannot reach task position at (1.04, 1.20, 45.66) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 4): [AiNavigation] Employee_Matthew Davis: task target (1.26, 1.20, 45.63) is not reachable — the partial path ends 11.19m short of it. Handing the task back.
- **WARN** (day 4): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 92089dbd-c913-4ac1-a244-d35972eacc1c but the employee is 12.7m away (max 4.5m) — refusing to start receiving.
- **ERROR** (day 4): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-07 (aisle entry)' (5.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__159:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1565)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 4): [OrderSelectionTaskDriver] 'Employee_David Cross': could not reach the low end of the aisle for 'bay 01-07' - driving straight to the stop instead.
- **ERROR** (day 4): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 2A line-up' (34.8m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__159:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1565)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 5): [OrderService] Order OG4526 closed with a IN TRANSIT pallet (36 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 5): ORPHAN-OUTBOUND 2A-1 (order 4d434ebb-a601-45de-aad4-f17d0a9aa3af gone)
- **ORPHAN-OUTBOUND** (day 5): ORPHAN-OUTBOUND 1A-1 (order c870b4c5-9aec-49ed-a987-43e9b5af2d73 gone)
- **REPEATING** (day 5): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 9.6m short of 'bay 01-01 (aisle entry)' - re-issuing the drive (2/6).
- **WARN** (day 5): [OrderSelectionTaskDriver] 'Employee_Christine Shelton': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **ERROR** (day 5): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'return pallet pickup' (34.0m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__159:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1565)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 877b44ac-8bf0-4bfb-a4be-c91320dea1b1 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 14/16.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 34bbc766-f1c5-42e1-a7b9-2a3276862914 (Mama Tomato Sauces) has no reachable pick location for its remaining line item(s) — staging short at 566/573.
- **ERROR** (day 6): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 2D line-up' (6.4m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__159:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1565)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 563d34cb-e358-4a2b-8e6b-f41075d8ce44 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 14/15.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order ced5e573-f52f-4367-8b0f-8ce7896545e2 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 13/14.
- **ORPHAN-OUTBOUND** (day 6): ORPHAN-OUTBOUND 2B-1 (order 0b77dc1b-adbc-4d4c-b7a1-c6287d4d51ec gone)
- **WARN** (day 6): [AiNavigation] PltJack_FULL(Clone): stuck at (-40.35, 1.11, 46.59) — walking out to (-39.78, 1.17, 47.98) (12.43m to mesh).
- **ERROR** (day 6): "GetRemainingDistance" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_remainingDistance ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__166:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1809)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 6): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__166:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1809)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 6): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 20.4m short of 'staging lane 3A line-up' - re-issuing the drive (5/6).
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 5b94e8a1-06ec-4f75-99c4-61b3be51d70b (Bramble & Bite Berry Co.) has no reachable pick location for its remaining line item(s) — staging short at 71/475.

## Bot actions (first 400)
