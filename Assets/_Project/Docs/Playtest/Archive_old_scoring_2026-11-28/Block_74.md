# Playtest block 74 — days 1–4

## Score: **28.2 (-18.0)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 2 late) | 10% | 0.0 (-50.0) |
| Fill rate | 10% | 0.0 (-100.0) |
| Throughput (0 plt vs target 36) | 10% | 0.0 (-29.2) |
| Revenue ($0) | not scored | 0.0 (-100.0) |
| Net Profit (-15,486) | 10% | 0.0 (-34.4) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 36.4 (+6.8) |
| Reputation (0 to 46) | 10% | 100.0 (0.0) |

Orders shipped **0**, late/fined **2**, pallets shipped **0**. Cash $130,347 → $76,229 (-54,118). Stock at cost $0 → $38,632 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 11 dock bookings, 2 orders released, 11 POs, 0 rush POs.

## Where the money went

Income **$0**. Expenses **$54,118** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|

| Expense | $ |
|---|---|
| Inventory | 41,003 |
| Wages | 4,531 |
| Maintenance | 3,628 |
| Fines | 2,459 |
| Groundskeeping | 1,272 |
| Lease / Mortgage | 1,194 |
| Loss Prevention | 31 |

## Late orders - state when it came due
- 2 x still being picked

- day 2: OG2150 (Sneaky Pete's Seafood) was still being picked (0/81 cs). Fined $352. Lines 25, 81 cs, released=True.
- day 3: OG3151 (Sneaky Pete's Seafood) was still being picked (77/517 cs). Fined $2,107. Lines 22, 517 cs, released=True.

## Change since last block
- Fixed / no longer seen: **8**
  - VEHICLE-STATIONARY|RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - VEHICLE-STATIONARY|PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait
  - WARN|[AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order ##ba##c#f-a#fb-# (Pony Express Oats) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **12**
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - ERROR|[TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E#
  - WARN|[AiNavigation] Employee_Paul Dixon: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - REPEATING|x# and counting: [MheTravel] 'ReachTruck' battery trip: did not arrive (status PathComplete) from (#, #, #) to (#, #, #); start on mesh: True, a static query says PathComplete, agent.CalculatePath says True/PathComplete, agent pos (#, #, #), destination (#, #, #), agent on mesh: True enabled: True.
  - REPEATING|x# and counting: [AiNavigation] Employee_Paul Dixon: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ###b-a### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[AiNavigation] Employee_Paul Dixon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##e##d-a#de## but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ###a##d### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##e##f#### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##b###e## but the employee is #m away (max #m) — refusing to start receiving.

## Bugs found (12 distinct, 13 incidents)

| Incidents | Bug |
|---|---|
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 1 | ERROR — [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E# |
| 2 | WARN — [AiNavigation] Employee_Paul Dixon: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | REPEATING — x# and counting: [MheTravel] 'ReachTruck' battery trip: did not arrive (status PathComplete) from (#, #, #) to (#, #, #); start on mesh: True, a static query says PathComplete, agent.CalculatePath says True/PathComplete, agent pos (#, #, #), destination (#, #, #), agent on mesh: True enabled: True. |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Paul Dixon: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###b-a### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Paul Dixon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##e##d-a#de## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###a##d### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##e##f#### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##b###e## but the employee is #m away (max #m) — refusing to start receiving. |

## First sightings (with stack traces)

- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 454633da-3965-458b-a372-686afbc4bd71 but the employee is 26.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [AiNavigation] Employee_Paul Dixon: stuck at (8.42, 1.31, 42.99) — walking out to (4.75, 1.06, 36.50) (7.46m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_Paul Dixon: task target (5.09, 1.14, 47.10) is not reachable — the partial path ends 9.88m short of it. Handing the task back.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet d64f9f4f-18e0-470d-a4de-28e5a1cc0cfb but the employee is 11.1m away (max 4.5m) — refusing to start receiving.
- **OVERLAP** (day 2): OVERLAP Pallet_00_Slot0_Tier0 & Pallet_00_Slot0_Tier0 @(61, 93)  <<HISTORY: d2 20:08 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 20:08 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 20:20 moved (53, 86)->(-1, -1) by InventoryService.MovePallet | d2 20:47 moved (-1, -1)->(61, 93) by InventoryService.MovePallet | d2 20:47 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 20:49 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>  <<HISTORY: d1 19:56 Putaway task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 22:37 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 22:46 moved (54, 86)->(-1, -1) by InventoryService.MovePallet | d1 23:13 moved (-1, -1)->(61, 93) by InventoryService.MovePallet | d1 23:13 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 23:17 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>
- **REPEATING** (day 3): x25 and counting: [MheTravel] 'ReachTruck' battery trip: did not arrive (status PathComplete) from (-11.84, 1.11, 52.50) to (-13.96, 1.16, 52.29); start on mesh: True, a static query says PathComplete, agent.CalculatePath says True/PathComplete, agent pos (-11.84, 1.16, 52.50), destination (-13.96, 1.16, 52.29), agent on mesh: True enabled: True.
- **ERROR** (day 3): [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<PlaceCarriedInTrailer>d__97:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:981)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 3): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 91825e62-8032-42a3-93d4-2bc801620b6f but the employee is 6.8m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 3): [ReceiverReceivingWorkflow] Arrival callback fired for pallet c670eb38-8e70-4f09-8625-140adacf2f5f but the employee is 8.2m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 3): x25 and counting: [AiNavigation] Employee_Paul Dixon: task target (3.48, 1.16, 46.83) is not reachable — the partial path ends 7.32m short of it. Handing the task back.
- **WARN** (day 3): [ReceiverReceivingWorkflow] Arrival callback fired for pallet c4275df1-4b85-4443-963e-83e1ca62f2e3 but the employee is 7.6m away (max 4.5m) — refusing to start receiving.
- **OVERLAP** (day 3): OVERLAP Pallet_05_Slot4_Tier0 & Pallet_00_Slot0_Tier0 @2E-1  <<HISTORY: d3 19:45 Receive task created (->STG2E-1) by ReceivingService.CreateReceiveTaskForPallet | d3 19:45 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 19:50 Putaway task created (STG2E-1->) by ReceiverReceivingWorkflow.CreatePutawayTask | d3 19:50 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d3 21:29 moved (54, 82)->(-1, -1) by InventoryService.MovePallet | d3 21:47 moved (-1, -1)->(54, 82) by InventoryService.MovePallet>>  <<HISTORY: d3 21:33 registered at (54, 82) 8x193587 by InventoryService.RegisterPhysicalPallet | d3 21:33 Receive task created (->STG2E-1) by ReceivingService.CreateReceiveTaskForPallet | d3 21:33 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d3 21:37 Putaway task created (STG2E-1->) by ReceiverReceivingWorkflow.CreatePutawayTask | d3 21:37 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask>>

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1204 Bulk Basin Foods: 6 plt $3,468
- d1 placed parked Inbound Bulk Basin Foods day 1 block 18 door 1
- d1 PO IG1205 Fairweather Trading Co.: 7 plt $5,945
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 19 door 2
- d1 PO IG1206 Homestead Market Co.: 5 plt $4,880
- d1 placed parked Inbound Homestead Market Co. day 1 block 22 door 1
- d1 PO IG1207 Homestead Market Co.: 2 plt $3,680
- d1 placed parked Inbound Homestead Market Co. day 1 block 24 door 2
- d1 PO IG1208 Backdoor Bill's Overstock: 1 plt $1,140
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 26 door 1
- d1 PO IG1209 Anchor Point Foods: 3 plt $1,712
- d1 placed parked Inbound Anchor Point Foods day 1 block 26 door 2
- d1 PO IG1210 Anchor Point Foods: 7 plt $4,838
- d1 placed parked Inbound Anchor Point Foods day 1 block 27 door 1
- d2 released 1 order(s): OG2150 (trailer 8.9 h away; 7.0 h work + 2 h)
- d2 PO IG2211 Backdoor Bill's Overstock: 2 plt $1,688
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 36 door 1
- d2 PO IG2212 Anchor Point Foods: 2 plt $1,240
- d2 placed parked Inbound Anchor Point Foods day 2 block 37 door 2
- d3 released 1 order(s): OG3151 (trailer 14.4 h away; 12.5 h work + 2 h)
- d3 PO IG3213 Fairweather Trading Co.: 8 plt $7,922
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 41 door 2
- d3 PO IG3215 Anchor Point Foods: 1 plt $640
- d3 placed parked Inbound Bulk Basin Foods day 3 block 43 door 1
