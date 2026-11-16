# Playtest block 1091 — days 1–8

## Score: **45.4** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (15 shipped, 10 late) | 10% | 47.4 |
| Fill rate | 10% | 87.8 |
| Throughput (173 plt vs target 84) | 10% | 100.0 |
| Revenue ($373,873) | not scored | 100.0 |
| Net Profit (+135,447) | 10% | 100.0 |
| Cash health (floor $15,000) | 0% | 100.0 |
| Reliability (bugs) | 50% | 3.8 |
| Reputation (0 to 164) | 10% | 100.0 |

Orders shipped **15**, late/fined **10**, pallets shipped **173**. Cash $500,000 → $235,260 (-264,740). Stock at cost $0 → $73,686 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went
*A save load zeroed the money ledger during this block, so this covers only the time since the load.*

Income **$313,588**. Expenses **$186,640** (stock bought shows as Inventory). Loan principal repaid **$5,000** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 305,859 |
| Handling | 7,729 |

| Expense | $ |
|---|---|
| Inventory | 127,960 |
| Fines | 18,355 |
| Lease / Mortgage | 9,530 |
| Transportation | 9,186 |
| Wages | 7,560 |
| Maintenance | 5,047 |
| Loan Principal | 5,000 |
| Floor | 4,760 |
| Groundskeeping | 2,182 |
| General | 1,540 |
| Pallet Lease and Repair | 462 |
| Loss Prevention | 58 |

## Late orders - state when it came due
- 4 x loaded but the truck had not left
- 2 x still being picked
- 2 x never released

- day 4: OG4854 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,327. Lines 30, 337 cs, released=True.
- day 5: OG5864 (Caffeine Fiend Coffee) was loaded but the truck had not left. Fined $7,486. Lines 3, 1139 cs, released=True.
- day 5: OG5861 (Barnacle Bill's Bait & Bite) was loaded but the truck had not left. Fined $1,792. Lines 22, 383 cs, released=True.
- day 6: OG6862 (Barnacle Bill's Bait & Bite) was still being picked (0/94 cs). Fined $504. Lines 13, 94 cs, released=True.
- day 6: OG6863 (Barnacle Bill's Bait & Bite) was still being picked (0/455 cs). Fined $1,972. Lines 50, 455 cs, released=True.
- day 6: OG6866 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $2,984. Lines 22, 558 cs, released=True.
- day 7: OG7867 (Barnacle Bill's Bait & Bite) was never released to the floor. Fined $356. Lines 12, 93 cs, released=False.
- day 7: OG7868 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,934. Lines 20, 521 cs, released=False.

## Bugs found (92 distinct, 342 incidents)

| Incidents | Bug |
|---|---|
| 43 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 41 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 15 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in) (line up)' (#m short) - giving up on this stop. |
| 12 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop. |
| 12 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry) (line up)' (#m short) - giving up on this stop. |
| 10 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry) (line up) (route) (line up)' (#m short) - giving up on this stop. |
| 21 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'chep stack (empty pallets)' ((#, #, #)) after #s. |
| 13 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 12 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 12 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 11 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 11 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 9 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'pallet jack parking' (#m short) - giving up on this stop. |
| 2 | GHOST-SLOT — RECORD # # x# cell=(#, #) lane= - no object |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop. |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'chep stack (empty pallets)' (#m short) - giving up on this stop. |
| 5 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [OrderSelectionTaskDriver] 'Employee_Shirley Edwards': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 2 | ORPHAN-OUTBOUND —  #B# (order ##d##fd#b## gone) |
| 4 | WARN — [OrderSelectionTaskDriver] Order ##e#c##d#-# (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | GHOST-SLOT — SLOT ##-B# names # - no object |
| 1 | GHOST-SLOT — SLOT ##-A# names # - no object |
| 1 | GHOST-SLOT — SLOT ### names # - no object |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##ffb#e#-bd#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | OVERLAP —  A Chep & OutboundPallet_##ffb#e#-bd#-#_restored @#A# |
| 1 | FLOATING —  OutboundPallet_##ffb#e#-bd#-#_restored @#A# bottom=# |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry) (route)' (#m short) - giving up on this stop. |
| 1 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 1 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' dig: → source lane #D: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago] |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' pallet pick: → lane #A: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at GameCore.Actors.ReachTruckOperator.SetDriveMode (GameCore.Actors.ReachTruckOperator+DriveMode mode, System.String phase) [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at GameCore.Actors.ReachTruckOperator+<TryPassJack>d__#.MoveNext () [#x#e] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\ReachTruckOperator.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 3 | WARN — [InventoryService] Lane #A lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (aisle entry)' ((#, #, #)) after #s. |
| 1 | OVERLAP —  StackPlts & StackPlts @(#, #) |
| 3 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 3 | WARN — [AiNavigation] RT_Full(Clone): task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 3 | WARN — [OrderSelectionTaskDriver] Order #-f#d#### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | TRUCK-STALLED —  Truck→PO_IG# state=DepartToApproach #s at (#, #, #) |
| 1 | ERROR — "SetDestination" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack: no path to (#, #, #) for 'bay ## (loop in) (line up)'. |
| 3 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (reach) (loop in) (line up)' (#m short) - giving up on this stop. |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Douglas Garcia: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 1 | NO-TASK —  lane pallet #C# sku=# load=# |
| 2 | WARN — [InventoryService] Lane #B lock was held for over #s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#). |
| 2 | WARN — [OrderSelectionTaskDriver] Order #-da#c##b#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ### anchor (via RT gate # low end): path lost mid-leg (likely a NavMesh rebake) — re-issuing destination (attempt #/#). |
| 2 | WARN — [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 2 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach bay ## after # tries with nothing picked - leaving the jack and handing the task back. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (loop in) (line up)' - re-issuing the drive (#/#). |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor (via RT gate # low end): path lost mid-leg (likely a NavMesh rebake) — re-issuing destination (attempt #/#). |
| 2 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | REPEATING — x# and counting: "SetDestination" can only be called on an active agent that has been placed on a NavMesh. |
| 1 | REPEATING — x# and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway. |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (#, #, #) - the next move would violate the # m vehicle clearance from RT_Full(Clone). |
| 1 | ORPHAN-OUTBOUND —  #A# (order #-f#d#### gone) |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##d#a##d#a-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (#, #, #) - the next move would violate the # m vehicle clearance from PltJack_FULL(Clone). |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry) (line up)' - re-issuing the drive (#/#). |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ##' - re-issuing the drive (#/#). |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry) (line up) (route) (line up)' - re-issuing the drive (#/#). |
| 1 | REPEATING — x# and counting: [LoadedHold] Order OG# (Sneaky Pete's Seafood) loaded d# #:#, not shipping (still, # min so far): still has a pallet being moved - it will be closable once that pallet is down, deadline d# #:#. |
| 1 | WARN — [AiNavigation] Employee_Charles Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ea###b### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#a#cce-a### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-cb###fcf## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f###c### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##b##-bce#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##a##### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##add#-abdd#c#a but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f###d### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-aea##a-b### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##ea##aa#-a#bc## (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [InventoryService] Lane #A lock was held for over #s by reach truck RT_Full(Clone) (Amanda Dixon, PalletPick) - treating it as leaked and taking it over. |
| 1 | WARN — [AiNavigation] RT_Full(Clone): task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-e#ff#-a#c#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##f#f#e#d### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##d##fd#b## (Bramble & Bite Berry Co.) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [InventoryService] Lane #B lock was held for over #s by reach truck RT_Full(Clone) (Amanda Dixon, PalletPick) - treating it as leaked and taking it over. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-b###b### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Douglas Garcia cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [InventoryService] Lane #A lock was held for over #s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-dbb##f#e#e#-# (Caffeine Fiend Coffee) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #D and it never freed up (held by reach truck RT_Full(Clone) (Amanda Dixon, PalletPick) for #s) — giving up and re-queueing. |
| 1 | WARN — [ReachTruckOperator] 'RT_Full(Clone)' waited #s for lane #B and it never freed up (held by reach truck RT_Full(Clone) (Justin Waller, Putaway) for #s) — giving up and re-queueing. |
| 1 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |

## First sightings (with stack traces)

- **WARN** (day 3): [OrderSelectionTaskDriver] Order edc76943-da5c-4596-85b4-dfcdc6bb5d44 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 298/420.
- **WARN** (day 3): [AiNavigation] RT_Full(Clone): task target (-21.25, 1.19, 39.71) only partially reachable — stopped 0.34m short. Treating as arrived.
- **ERROR** (day 3): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'pallet jack parking' (7.0m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__260:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 3): x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 02-13-01 anchor (via RT gate 01 low end): path lost mid-leg (likely a NavMesh rebake) — re-issuing destination (attempt 5/10).
- **ERROR** (day 3): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-08 (loop in) (line up)' (22.4m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__260:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 3): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'chep stack (empty pallets)' (13.7m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__260:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 3): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 0b7ad705-e7ff-4767-a7c7-d4d43f662de0 but the employee is 6.1m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 3): [ReceiverReceivingWorkflow] Arrival callback fired for pallet e29aab5f-9f6f-4e1d-8225-63e29a3b86bd but the employee is 9.4m away (max 4.5m) — refusing to start receiving.
- **ERROR** (day 3): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-04 (aisle entry) (line up)' (19.3m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__260:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 3): [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 01-04' - driving straight to the stop instead.
- **WARN** (day 3): [AiNavigation] DS_FULL(Clone): stuck at (5.71, 1.11, 40.85) — walking out to (6.28, 1.25, 42.23) (12.00m to mesh).
- **WARN** (day 4): [OrderSelectionTaskDriver] Order 9c8e7160-73d6-43fd-843b-458fb3ab8949 (Bramble & Bite Berry Co.) has no reachable pick location for its remaining line item(s) — staging short at 574/588.
- **WARN** (day 4): [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach bay 01-07 after 5 tries with nothing picked - leaving the jack and handing the task back.
- **WARN** (day 4): [OrderService] Order OG6858 closed with a STAGED pallet (14 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 2B-2 (order 9c8e7160-73d6-43fd-843b-458fb3ab8949 gone)
- **REPEATING** (day 4): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 6.3m short of 'bay 01-07 (loop in) (line up)' - re-issuing the drive (5/6).
- **REPEATING** (day 4): x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location 01-13-A0 anchor (via RT gate 01 low end): path lost mid-leg (likely a NavMesh rebake) — re-issuing destination (attempt 5/10).
- **WARN** (day 4): [AiNavigation] RT_Full(Clone): task target (-21.25, 1.20, 39.71) is not reachable — the partial path ends 8.22m short of it. Handing the task back.
- **WARN** (day 4): [OrderSelectionTaskDriver] Order 5ca642e6-f75d-4554-9521-047cfddd65c4 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 224/337.
- **WARN** (day 4): [AiNavigation] RT_Full(Clone) cannot reach task position at (-3.36, 1.24, 48.99) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 4): [InventoryService] Lane 2B lock was held for over 120s by reach truck RT_Full(Clone) (Amanda Dixon, PalletPick) - treating it as leaked and taking it over.
- **TRUCK-STALLED** (day 4): TRUCK-STALLED Truck→PO_IG4176 state=DepartToApproach 90s at (-11.3, 0.0, 27.9)
- **WARN** (day 4): [ReceiverReceivingWorkflow] Arrival callback fired for pallet d67ba1f4-b442-4192-8b56-61e36d9f5a46 but the employee is 22.2m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 4): [OrderSelectionTaskDriver] Order 01e007a2-6e0c-4409-98d8-f6d5494248f7 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 166/383.
- **WARN** (day 4): [AiNavigation] Employee_Douglas Garcia cannot reach task position at (5.49, 1.20, 47.67) (path invalid) — abandoning the walk and handing the task back.
- **ERROR** (day 4): [OrderSelectionTaskDriver] Pallet jack did not reach 'chep stack (empty pallets)' ((-18.64, 1.20, 57.76)) after 0.2s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__271:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2313)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 4): "SetDestination" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:SetDestination (UnityEngine.Vector3)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__271:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2181)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 4): x25 and counting: "SetDestination" can only be called on an active agent that has been placed on a NavMesh.
- **ERROR** (day 4): [OrderSelectionTaskDriver] Pallet jack: no path to (-14.76, 1.24, 42.34) for 'bay 02-04 (loop in) (line up)'.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__271:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2186)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 5): x25 and counting: [TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.
- **REPEATING** (day 5): x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (-11.99, 1.11, 57.62) - the next move would violate the 1 m vehicle clearance from RT_Full(Clone).
- **ORPHAN-OUTBOUND** (day 5): ORPHAN-OUTBOUND 2A-4 (order 5ca642e6-f75d-4554-9521-047cfddd65c4 gone)
- **WARN** (day 5): [InventoryService] Lane 3A lock was held for over 120s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 5a44db12-dbb5-4f1e-8e68-b9d519c41933 (Caffeine Fiend Coffee) has no reachable pick location for its remaining line item(s) — staging short at 792/1139.
- **WARN** (day 5): [ReachTruckOperator] 'RT_Full(Clone)' waited 45s for lane 1D and it never freed up (held by reach truck RT_Full(Clone) (Amanda Dixon, PalletPick) for 92s) — giving up and re-queueing.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order 151acc85-737d-47a7-8d0a-acdba0afa07a (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 269/430.
- **WARN** (day 5): [ReachTruckOperator] 'RT_Full(Clone)' waited 45s for lane 2B and it never freed up (held by reach truck RT_Full(Clone) (Justin Waller, Putaway) for 57s) — giving up and re-queueing.
- **REPEATING** (day 6): x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' waiting at (-35.16, 1.11, 49.16) - the next move would violate the 1 m vehicle clearance from PltJack_FULL(Clone).
- **WARN** (day 6): [OrderService] Order OG5864 closed with a IN TRANSIT pallet (52 cs) that never made the trailer - not billed, returning it to stock.
- **WARN** (day 6): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-32.77, 1.11, 42.34).
- **ERROR** (day 6): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__149:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 6): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-02 (aisle entry) (line up) (route) (line up)' (15.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__260:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 6): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-07 (reach) (loop in) (line up)' (24.0m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__260:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1832)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 6): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 20.7m short of 'bay 02-02 (aisle entry) (line up)' - re-issuing the drive (2/6).
- **REPEATING** (day 6): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 13.8m short of 'bay 02-02' - re-issuing the drive (1/6).
- **REPEATING** (day 6): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 15.7m short of 'bay 02-02 (aisle entry) (line up) (route) (line up)' - re-issuing the drive (1/6).
- **REPEATING** (day 7): x25 and counting: [LoadedHold] Order OG6866 (Sneaky Pete's Seafood) loaded d6 16:46, not shipping (still, 781 min so far): still has a pallet being moved - it will be closable once that pallet is down, deadline d6 22:00.

## Bot actions (first 400)
