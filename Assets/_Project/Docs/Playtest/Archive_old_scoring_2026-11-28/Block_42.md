# Playtest block 42 — days 1–3

## Score: **42.2 (+15.3)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 0 late) | 10% | 100.0 (+100.0) |
| Fill rate | 10% | 100.0 (+4.2) |
| Throughput (15 plt vs target 24) | 10% | 62.5 (-26.4) |
| Revenue ($38,142) | not scored | 100.0 (0.0) |
| Net Profit (-37,290) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 17.5 (+6.0) |
| Reputation (19 to 32) | 10% | 71.7 (+45.0) |

Orders shipped **4**, late/fined **0**, pallets shipped **15**. Cash $127,764 → $157,068 (+29,304). Stock at cost $200,106 → $133,512 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 9 dock bookings, 0 orders released, 6 POs, 0 rush POs.

## Where the money went

Income **$38,142**. Expenses **$8,838** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 37,230 |
| Handling | 912 |

| Expense | $ |
|---|---|
| Wages | 2,835 |
| Inventory | 2,800 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Change since last block
- Fixed / no longer seen: **20**
  - ERROR|"IsStopped" can only be called on an active agent that has been placed on a NavMesh.
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: own/Staged/y#] 
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'bay ## (loop in)' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'bay ## (aisle entry) (route)' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'bay ## (aisle entry)' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'bay ## (route)' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'bay ##' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up (route)' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Larry Morgan': no route for 'staging lane #C line-up' from (#, #, #) to (#, #, #) (NoPath, # expanded) - the jack will not drive it.
  - ERROR|[GridNav] 'Employee_Mark Peterson': 'staging lane #D line-up' was interrupted # times - giving the leg up.
  - REPEATING|x# and counting: [PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.
  - REPEATING|x# and counting: [AiNavigation] DS_FULL(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - WARN|[AiNavigation] Employee_Douglas Garcia cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##fc#c#-b#c-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Larry Morgan': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
- New this block: **10**
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (loop in)' ((#, #, #)) after #s.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #A line-up' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop.
  - ERROR|[MHEBoundary] 'PltJack_FULL(Clone)' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside.
  - REPEATING|x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#).
  - WARN|[OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead.
  - WARN|[OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#.

## Bugs found (24 distinct, 62 incidents)

| Incidents | Bug |
|---|---|
| 14 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 7 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'bay ## (loop in)' ((#, #, #)) after #s. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (loop in)' (#m short) - giving up on this stop. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #A line-up' (#m short) - giving up on this stop. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ## (aisle entry)' (#m short) - giving up on this stop. |
| 1 | ERROR — [MHEBoundary] 'PltJack_FULL(Clone)' drove OUT of the building at (#, #, #) (cell (#, #)). Putting it back on its last inside spot (#, #, #). Forklifts, dock stockers and pallet jacks must stay inside. |
| 2 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | REPEATING — x# and counting: [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ## (aisle entry)' - re-issuing the drive (#/#). |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##ba##c#f-a#fb-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay ##' - driving straight to the stop instead. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] DS_FULL(Clone): stuck at (-19.33, 1.11, 56.33) — walking out to (-20.72, 1.16, 56.33) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-33.86, 1.11, 46.91) — walking out to (-32.47, 1.19, 47.48) (12.26m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (1.86, 1.12, 59.25) — walking out to (1.86, 1.17, 46.25) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (0.64, 1.12, 59.00) — walking out to (0.64, 1.20, 46.00) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (-11.72, 1.15, 52.60) — walking out to (-24.72, 1.12, 53.21) (12.01m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-13.71, 1.16, 51.32) — walking out to (-2.45, 1.22, 44.82) (12.00m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 177/248.
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-6.92, 1.14, 55.17) — walking out to (-0.42, 1.22, 43.91) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] PltJack_FULL(Clone): stuck at (-8.96, 1.11, 57.97) — walking out to (-8.96, 1.16, 59.00) (12.43m to mesh).
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack did not reach 'bay 02-05 (loop in)' ((-17.59, 1.23, 42.34)) after 0.1s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__211:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:2151)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **REPEATING** (day 2): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-05 (loop in)' (13.0m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 3A line-up' (15.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 2371db5b-4ba1-4c6f-a5fb-dbdcbd441229 (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at 44/59.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): the straight way out of (-40.35, 1.11, 46.12) runs through something solid - taking a clear way out to (-40.35, 1.17, 44.73) instead.
- **WARN** (day 2): [AiNavigation] RT_Full(Clone): stuck in aisle 02 - walking out forward along the right lane to (-24.48, 1.11, 50.19).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order b72defae-0fda-441b-9c33-e78561011633 (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at 46/48.
- **REPEATING** (day 2): x25 and counting: [AiNavigation] RT_Full(Clone): stuck at (-9.85, 1.11, 53.32) — walking out to (-9.28, 1.19, 54.70) (12.70m to mesh).
- **VEHICLE-STATIONARY** (day 2): PltJack_FULL(Clone) has a driver, hasn't moved in 359s while 6 task(s) it could take wait
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] Pallet jack stopped 5.9m short of 'bay 02-01 (aisle entry)' - re-issuing the drive (6/6).
- **ERROR** (day 2): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 02-01 (aisle entry)' (5.9m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__202:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1751)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [OrderSelectionTaskDriver] 'Employee_Mark Peterson': could not reach the low end of the aisle for 'bay 02-01' - driving straight to the stop instead.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order a73ffce7-e0a7-4563-8ce5-522a69ed33da (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at 48/55.
- **ERROR** (day 2): [MHEBoundary] 'PltJack_FULL(Clone)' drove OUT of the building at (-48.71, 1.11, 47.66) (cell (13, 86)). Putting it back on its last inside spot (-48.28, 1.11, 47.66). Forklifts, dock stockers and pallet jacks must stay inside.
  ```
  UnityEngine.Debug:LogError (object)
  MHEBoundary:Update () (at Assets/_Project/Scripts/Actors/MHEBoundary.cs:91)
  
  ```

## Bot actions (first 400)
- d1 signed bulk BULK_Honest Harvest Rice_D1_0
- d1 PO IG1954 Bulk Basin Foods: 4 plt $480
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 2
- d1 PO IG1955 Cornerstone Staples: 4 plt $480
- d1 placed parked Inbound Cornerstone Staples day 1 block 44 door 1
- d2 PO IG2956 Iron Gate Staples: 4 plt $480
- d2 placed parked Inbound Iron Gate Staples day 2 block 14 door 1
- d2 PO IG2957 Bulk Basin Foods: 4 plt $512
- d2 placed parked Inbound Bulk Basin Foods day 2 block 32 door 2
- d2 PO IG2958 Bulk Basin Foods: 1 plt $128
- d2 placed parked Inbound Bulk Basin Foods day 2 block 36 door 2
- d2 PO IG2959 Riverside Housewares: 1 plt $720
- d2 placed parked Inbound Riverside Housewares day 2 block 37 door 2
