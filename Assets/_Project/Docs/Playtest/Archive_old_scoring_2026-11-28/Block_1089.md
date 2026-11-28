# Playtest block 1089 — days 1–8

## Score: **41.5** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (12 shipped, 6 late) | 10% | 50.0 |
| Fill rate | 10% | 97.2 |
| Throughput (81 plt vs target 84) | 10% | 96.4 |
| Revenue ($81,860) | not scored | 100.0 |
| Net Profit (-9,682) | 10% | 3.9 |
| Cash health (floor $15,000) | 0% | 100.0 |
| Reliability (bugs) | 50% | 13.5 |
| Reputation (0 to 96) | 10% | 100.0 |

Orders shipped **12**, late/fined **6**, pallets shipped **81**. Cash $500,000 → $114,619 (-385,381). Stock at cost $0 → $60,141 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went

Income **$158,510**. Expenses **$538,891** (stock bought shows as Inventory). Loan principal repaid **$5,000** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 79,288 |
| Racking | 61,200 |
| Refund | 7,650 |
| Floor | 5,100 |
| Handling | 2,572 |
| Flavor | 2,250 |
| Vehicles | 450 |

| Expense | $ |
|---|---|
| Inventory | 95,811 |
| Racking | 81,600 |
| General | 77,970 |
| Floor | 74,440 |
| Loss Prevention | 64,079 |
| Vehicles | 63,650 |
| Grounds | 20,240 |
| Walls | 17,550 |
| Wages | 10,890 |
| Lease / Mortgage | 10,244 |
| Transportation | 6,517 |
| Fines | 6,045 |
| Loan Principal | 5,000 |
| Maintenance | 3,662 |
| Flavor | 3,250 |
| Groundskeeping | 2,943 |

## Late orders - state when it came due
- 3 x loaded but the truck had not left
- 1 x still loading
- 1 x still being picked
- 1 x never released

- day 2: OG2505 (Sneaky Pete's Seafood) was still loading when it came due. Fined $62. Lines 16, 16 cs, released=True.
- day 3: OG4511 (Puff & Stuff Pastries) was loaded but the truck had not left. Fined $828. Lines 3, 202 cs, released=True.
- day 4: OG3512 (Puff & Stuff Pastries) was loaded but the truck had not left. Fined $2,466. Lines 2, 590 cs, released=True.
- day 4: OG6514 (The Daily Grind Tofu) was loaded but the truck had not left. Fined $1,164. Lines 3, 229 cs, released=True.
- day 4: OG4510 (Sneaky Pete's Seafood) was still being picked (341/341 cs). Fined $1,466. Lines 50, 341 cs, released=True.
- day 5: OG5513 (Sneaky Pete's Seafood) was never released to the floor. Fined $59. Lines 16, 16 cs, released=False.

## Bugs found (29 distinct, 84 incidents)

| Incidents | Bug |
|---|---|
| 15 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 10 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 7 | WARN — [AiNavigation] Employee_Angela Miller: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Brandon Cartwright: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [OrderSelectionTaskDriver] Order ##a###fe#ee#b# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 6 | WARN — [AiNavigation] Employee_Virginia Wright: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 4 | WARN — [AiNavigation] Employee_Joyce Brown: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#D# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#D# |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #B line-up' (#m short) - giving up on this stop. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Order OG# pallet # of #: every pick face is empty - handing the task back until replenishment stocks them. |
| 2 | WARN — [AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 2 | WARN — [AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | ORPHAN-OUTBOUND —  #A# (order ##aac##a#-# gone) |
| 2 | WARN — [AiNavigation] Employee_Alexander Davis: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c#b#cc#-aa### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##e##f#cc### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-c#a##c#-b#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##bc#c#### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-a#d##ff##-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Employee_Joyce Brown cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Angela Miller cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [OrderService] Order OG# closed with a LOADING pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] ReachTruck: stuck at (9.87, 1.08, 35.64) — walking out to (2.99, 1.16, 46.88) (12.18m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Brandon Cartwright: stuck at (-3.01, 1.16, 42.52) — walking out to (-14.27, 1.09, 36.02) (12.00m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 2664f74f-1c1b-4cc3-aa67-89c1b3a40915 but the employee is 4.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [OrderSelectionTaskDriver] Order da8bf81e-38a9-4364-89fe-04658ee41b56 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 5/16.
- **WARN** (day 1): [AiNavigation] ReachTruck cannot reach task position at (4.59, 1.16, 48.99) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] Employee_Virginia Wright: stuck at (6.68, 1.16, 42.06) — walking out to (-6.48, 1.16, 42.06) (12.17m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 92028903-75e6-475f-9cc8-5d8c4f60b10d but the employee is 4.8m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet a41c0875-c2a5-45c0-b657-f361679b9c5b but the employee is 6.1m away (max 4.5m) — refusing to start receiving.
- **FLOATING** (day 1): FLOATING Pallet_03_Slot1_Tier1 @2A-4 bottom=2.16  <<HISTORY: d1 18:09 registered at (46, 85) 24x903561 by InventoryService.RegisterPhysicalPallet | d1 18:09 Receive task created (->STG2A-4) by ReceivingService.CreateReceiveTaskForPallet | d1 18:20 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>
- **OVERLAP** (day 1): OVERLAP Pallet_03_Slot1_Tier1 & Pallet_00_Slot0_Tier0 @2A-4  <<HISTORY: d1 18:09 registered at (46, 85) 24x903561 by InventoryService.RegisterPhysicalPallet | d1 18:09 Receive task created (->STG2A-4) by ReceivingService.CreateReceiveTaskForPallet | d1 18:20 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask>>  <<HISTORY: d1 17:52 Receive task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 18:19 Putaway task created (STG2A-4->) by ReceiverReceivingWorkflow.CreatePutawayTask | d1 18:19 Receive task Assigned->Cancelled by WorkQueueSystem.CancelTasksForPallet | d1 18:19 Putaway task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet | d1 18:19 PalletPick task Open->Available by OrderService.TryAssignInboundPalletToPendingPick | d1 18:19 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask>>
- **FLOATING** (day 1): FLOATING Pallet_05_Slot2_Tier1 @2C-2 bottom=2.16  <<HISTORY: d1 18:56 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d1 19:09 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 19:15 moved (46, 84)->(-1, -1) by InventoryService.MovePallet | d1 19:22 adopted as outbound freight of order 3d3044ec-3cc2-4f96-af67-e3d45bc316c7 (24 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 19:22 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:22 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 1): OVERLAP Pallet_05_Slot2_Tier1 & Pallet_00_Slot0_Tier0 @2C-2  <<HISTORY: d1 18:56 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d1 19:09 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 19:15 moved (46, 84)->(-1, -1) by InventoryService.MovePallet | d1 19:22 adopted as outbound freight of order 3d3044ec-3cc2-4f96-af67-e3d45bc316c7 (24 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 19:22 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:22 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d1 19:05 moved (46, 85)->(-1, -1) by InventoryService.MovePallet | d1 19:06 PalletPick task Assigned->Available by ReachTruckOperator.ReleaseTaskWithBackoff | d1 19:15 adopted as outbound freight of order 3d3044ec-3cc2-4f96-af67-e3d45bc316c7 (24 cs) by OutboundPalletBuilder.AdoptFullPallet | d1 19:15 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:15 record destroyed by InventoryService.DestroyPallet | d1 19:15 PalletPick task Available->Cancelled by WorkQueueSystem.CancelTasksForPallet>>
- **WARN** (day 1): [AiNavigation] Dockstocker: stuck at (-38.90, 1.11, 46.79) — walking out to (-37.51, 1.16, 46.79) (12.32m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_Angela Miller: stuck at (-11.27, 1.13, 55.38) — walking out to (-11.27, 1.16, 42.38) (12.00m to mesh).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order b041d3de-76bc-43c1-9538-0fa5285c2737 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 28/29.
- **REPEATING** (day 2): x25 and counting: [OrderSelectionTaskDriver] Order OG4511 pallet 1 of 1: every pick face is empty - handing the task back until replenishment stocks them.
- **WARN** (day 3): [AiNavigation] Dockstocker cannot reach task position at (-45.76, 1.11, 46.99) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 3): [AiNavigation] Employee_Joyce Brown: stuck at (-10.72, 1.14, 54.50) — walking out to (-4.22, 1.16, 43.24) (12.00m to mesh).
- **WARN** (day 3): [AiNavigation] PalletJack: stuck at (3.67, 1.11, 50.98) — walking out to (4.24, 1.16, 52.37) (12.00m to mesh).
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 204831c7-a0d2-4ff6-9692-d84fc8bf0750 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 77/341.
- **WARN** (day 4): [AiNavigation] Employee_Joyce Brown cannot reach task position at (-32.75, 1.11, 41.21) (path invalid) — abandoning the walk and handing the task back.
- **FLOATING** (day 4): FLOATING Pallet_01_Slot0_Tier1 @3D-1 bottom=2.16  <<HISTORY: d4 06:05 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d4 06:32 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 06:39 moved (40, 85)->(-1, -1) by InventoryService.MovePallet | d4 06:53 adopted as outbound freight of order a5d58d12-0aac-4088-98a6-cfc442619a46 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 06:53 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 06:53 record destroyed by InventoryService.DestroyPallet>>
- **OVERLAP** (day 4): OVERLAP Pallet_01_Slot0_Tier1 & Pallet_00_Slot0_Tier0 @3D-1  <<HISTORY: d4 06:05 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d4 06:32 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 06:39 moved (40, 85)->(-1, -1) by InventoryService.MovePallet | d4 06:53 adopted as outbound freight of order a5d58d12-0aac-4088-98a6-cfc442619a46 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 06:53 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 06:53 record destroyed by InventoryService.DestroyPallet>>  <<HISTORY: d4 06:17 Receive task Cancelled->Complete by WorkQueueSystem.CompleteTask | d4 06:18 PalletPick task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d4 06:27 moved (40, 85)->(-1, -1) by InventoryService.MovePallet | d4 06:41 adopted as outbound freight of order a5d58d12-0aac-4088-98a6-cfc442619a46 (32 cs) by OutboundPalletBuilder.AdoptFullPallet | d4 06:41 PalletPick task Assigned->Complete by WorkQueueSystem.CompleteTask | d4 06:41 record destroyed by InventoryService.DestroyPallet>>
- **WARN** (day 4): [AiNavigation] Employee_Angela Miller cannot reach task position at (-4.69, 1.09, 43.01) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 4): [OrderService] Order OG6514 closed with a STAGED pallet (4 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 3A-1 (order a5d58d12-0aac-4088-98a6-cfc442619a46 gone)
- **WARN** (day 5): [AiNavigation] Employee_Alexander Davis: stuck at (-11.24, 1.14, 53.61) — walking out to (-4.74, 1.16, 42.35) (12.00m to mesh).
- **WARN** (day 6): [OrderService] Order OG5513 closed with a LOADING pallet (1 cs) that never made the trailer - not billed, returning it to stock.
- **ERROR** (day 6): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B line-up' (18.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__159:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1548)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```

## Bot actions (first 400)
