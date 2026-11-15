# Playtest block 1091 — days 1–3

## Score: **59.0** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 0 late) | 10% | 100.0 |
| Fill rate | 10% | 94.9 |
| Throughput (28 plt vs target 24) | 10% | 100.0 |
| Revenue ($41,510) | not scored | 100.0 |
| Net Profit (+14,472) | 10% | 100.0 |
| Cash health (floor $15,000) | 0% | 100.0 |
| Reliability (bugs) | 50% | 18.9 |
| Reputation (0 to 128) | 10% | 100.0 |

Orders shipped **2**, late/fined **0**, pallets shipped **28**. Cash $500,000 → $133,722 (-366,278). Stock at cost $0 → $54,249 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went
*A save load zeroed the money ledger during this block, so this covers only the time since the load.*

Income **$41,510**. Expenses **$18,013** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 40,478 |
| Handling | 1,032 |

| Expense | $ |
|---|---|
| Inventory | 11,990 |
| Wages | 2,835 |
| Maintenance | 1,608 |
| Lease / Mortgage | 784 |
| Groundskeeping | 776 |
| Loss Prevention | 20 |

## Bugs found (25 distinct, 53 incidents)

| Incidents | Bug |
|---|---|
| 8 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | NO-RECORD —  lane pallet #B# A Chep |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #B wait' (#m short) - giving up on this stop. |
| 3 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ERROR — "IsStopped" can only be called on an active agent that has been placed on a NavMesh. |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##ecc#-a#a-# (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 2 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | WARN — [AiNavigation] Employee_Charles Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c#e#d#-b#f#a#e# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [InventoryService] Lane #B lock was held for over #s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over. |
| 1 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##ecc#-a#a-# (Rooted Robotics) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [InventoryService] Lane #A lock was held for over #s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [InventoryService] Lane #A lock was held for over #s by reach truck RT_Full(Clone) (Amanda Dixon, PalletPick) - treating it as leaked and taking it over. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (6.69, 1.16, 49.00) — walking out to (6.69, 1.09, 36.00) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (4.56, 1.11, 53.16) — walking out to (5.95, 1.20, 53.74) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (1.63, 1.16, 58.25) — walking out to (8.13, 1.23, 46.99) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (1.62, 1.16, 58.25) — walking out to (8.12, 1.23, 46.99) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-4.09, 1.14, 58.31) — walking out to (-4.09, 1.23, 45.31) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (1.62, 1.16, 57.70) — walking out to (8.12, 1.23, 46.44) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 20/248.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet fe91fdea-1c1e-4d21-b83f-625283a397e2 but the employee is 4.8m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] RT_Full(Clone) cannot reach task position at (-21.91, 1.22, 41.34) (path invalid) — abandoning the walk and handing the task back.
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B wait' (3.8m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-1.79, 1.11, 47.83) — walking out to (-1.79, 1.25, 46.44) (12.18m to mesh).
- **ERROR** (day 1): "IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  ```
  UnityEngine.AI.NavMeshAgent:get_isStopped ()
  GameCore.Actors.ReachTruckOperator/<SeekViaNavMesh>d__141:MoveNext () (at Assets/_Project/Scripts/Actors/ReachTruckOperator.cs:2761)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 1): [OrderSelectionTaskDriver] Order eb1433f2-2ecc-4529-a46a-a256833be6b7 (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at 463/728.
- **WARN** (day 1): [InventoryService] Lane 3B lock was held for over 120s by pallet jack PltJack_FULL(Clone) (WorkerNew, staging) - treating it as leaked and taking it over.
- **WARN** (day 2): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-40.81, 1.11, 42.34).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order eb1433f2-2ecc-4529-a46a-a256833be6b7 (Rooted Robotics) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 3.
- **WARN** (day 2): [OrderService] Order OG4790 closed with a STAGED pallet (23 cs) that never made the trailer - not billed, returning it to stock.
- **WARN** (day 2): [InventoryService] Lane 1A lock was held for over 120s by reach truck RT_Full(Clone) (Justin Waller, PalletPick) - treating it as leaked and taking it over.
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-05 (aisle entry)' (10.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 01-05' - driving straight to the stop instead.
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay 01-01' - driving straight to the stop instead.
- **WARN** (day 2): [InventoryService] Lane 1A lock was held for over 120s by reach truck RT_Full(Clone) (Amanda Dixon, PalletPick) - treating it as leaked and taking it over.
- **VEHICLE-STATIONARY** (day 2): RT_Full(Clone) has a driver, hasn't moved in 360s while 14 task(s) it could take wait

## Bot actions (first 400)
