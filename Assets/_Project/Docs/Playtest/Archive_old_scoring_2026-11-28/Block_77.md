# Playtest block 77 — days 1–4

## Score: **20.2 (-40.0)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 2 late) | 10% | 0.0 (-100.0) |
| Fill rate | 10% | 0.0 (-70.1) |
| Throughput (0 plt vs target 36) | 10% | 0.0 (-5.6) |
| Revenue ($0) | not scored | 0.0 (-19.3) |
| Net Profit (-18,632) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 20.4 (-46.3) |
| Reputation (0 to 53) | 10% | 100.0 (+6.7) |

Orders shipped **0**, late/fined **2**, pallets shipped **0**. Cash $130,345 → $60,583 (-69,762). Stock at cost $0 → $51,130 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 19 dock bookings, 4 orders released, 14 POs, 0 rush POs.

## Where the money went

Income **$0**. Expenses **$69,762** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|

| Expense | $ |
|---|---|
| Inventory | 57,198 |
| Wages | 4,232 |
| Maintenance | 3,627 |
| Fines | 2,209 |
| Groundskeeping | 1,271 |
| Lease / Mortgage | 1,194 |
| Loss Prevention | 31 |

## Late orders - state when it came due
- 2 x still being picked

- day 2: OG2167 (Sneaky Pete's Seafood) was still being picked (0/52 cs). Fined $178. Lines 8, 52 cs, released=True.
- day 3: OG3168 (Sneaky Pete's Seafood) was still being picked (0/470 cs). Fined $2,031. Lines 50, 470 cs, released=True.

## Change since last block
- Fixed / no longer seen: **6**
  - WARN|[AiNavigation] Employee_Kathryn Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Mark Smith cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Employee_Mark Smith: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#d##af#-bbfd## but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order #-c#b##dd#-ab### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ] 
  - WARN|[OrderSelectionTaskDriver] Order #-e#f##-b#cc#bb#a#e (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] 
- New this block: **27**
  - WARN|[AiNavigation] Employee_Joshua Ramirez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Joshua Ramirez: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - WARN|[AiNavigation] Employee_Jonathan Scott: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - WARN|[AiNavigation] Employee_Jonathan Scott: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [AiNavigation] Employee_Jonathan Scott: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived.
  - REPEATING|x# and counting: [AiNavigation] Employee_Joshua Ramirez: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##af##ac-b#-# but the employee is #m away (max #m) — refusing to start receiving.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Jonathan Scott: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Jonathan Scott: released Receive task for pallet ##af##ac-b#-# back to the queue (employee strayed outside receiving range).
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Joshua Ramirez: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f#e##ba#-bfd#-# but the employee is #m away (max #m) — refusing to start receiving.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Joshua Ramirez: released Receive task for pallet #-f#e##ba#-bfd#-# back to the queue (employee strayed outside receiving range).
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-fcce#c#c#ec#-# but the employee is #m away (max #m) — refusing to start receiving.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Joshua Ramirez: released Receive task for pallet #-fcce#c#c#ec#-# back to the queue (employee strayed outside receiving range).

## Bugs found (27 distinct, 37 incidents)

| Incidents | Bug |
|---|---|
| 5 | WARN — [AiNavigation] Employee_Joshua Ramirez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [AiNavigation] Employee_Joshua Ramirez: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 3 | WARN — [AiNavigation] Employee_Jonathan Scott: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 2 | WARN — [AiNavigation] Employee_Jonathan Scott: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Jonathan Scott: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Joshua Ramirez: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##af##ac-b#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Jonathan Scott: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Jonathan Scott: released Receive task for pallet ##af##ac-b#-# back to the queue (employee strayed outside receiving range). |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Joshua Ramirez: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 1 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f#e##ba#-bfd#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Joshua Ramirez: released Receive task for pallet #-f#e##ba#-bfd#-# back to the queue (employee strayed outside receiving range). |
| 1 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-fcce#c#c#ec#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Joshua Ramirez: released Receive task for pallet #-fcce#c#c#ec#-# back to the queue (employee strayed outside receiving range). |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Joshua Ramirez: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ef##bf#f##f#b# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##af##ac-b#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Jonathan Scott: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f#e##ba#-bfd#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Joshua Ramirez cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-fcce#c#c#ec#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Joshua Ramirez: task target (#, #, #) only partially reachable — stopped #m short. Treating as arrived. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##dc##e#a-b#f### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###e#c### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-b#cc#a#-b### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###b#e## but the employee is #m away (max #m) — refusing to start receiving. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Jonathan Scott: stuck at (-17.00, 0.03, 17.81) — walking out to (-10.50, 0.05, 6.55) (12.00m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet dc227f94-1ef0-43bf-9f36-225637f0b134 but the employee is 36.7m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 54f40373-5af7-43ac-b521-fa8720f19e3e but the employee is 23.4m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] Employee_Jonathan Scott: task target (7.20, 1.14, 47.55) only partially reachable — stopped 3.50m short. Treating as arrived.
- **WARN** (day 1): [AiNavigation] Employee_Jonathan Scott: task target (4.16, 1.19, 48.31) is not reachable — the partial path ends 6.73m short of it. Handing the task back.
- **REPEATING** (day 1): x25 and counting: [AiNavigation] Employee_Jonathan Scott: task target (7.40, 1.16, 48.14) only partially reachable — stopped 3.43m short. Treating as arrived.
- **WARN** (day 1): [AiNavigation] Employee_Joshua Ramirez: stuck at (7.66, 1.16, 45.66) — walking out to (11.68, 0.02, 48.00) (3.65m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Joshua Ramirez: task target (-6.44, 1.14, 48.31) is not reachable — the partial path ends 17.25m short of it. Handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet e6dceaf7-f6e1-4ba0-bfd7-b5faf3711008 but the employee is 15.5m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [AiNavigation] Employee_Joshua Ramirez: task target (-3.19, 1.14, 48.31) is not reachable — the partial path ends 14.00m short of it. Handing the task back.
- **REPEATING** (day 1): x25 and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet 54f40373-5af7-43ac-b521-fa8720f19e3e but the employee is 4.9m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [ReceivingTaskDriver] Employee_Jonathan Scott: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
- **REPEATING** (day 1): x25 and counting: [ReceivingTaskDriver] Employee_Jonathan Scott: released Receive task for pallet 54f40373-5af7-43ac-b521-fa8720f19e3e back to the queue (employee strayed outside receiving range).
- **REPEATING** (day 1): x25 and counting: [ReceivingTaskDriver] Employee_Joshua Ramirez: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
- **REPEATING** (day 1): x25 and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet e6dceaf7-f6e1-4ba0-bfd7-b5faf3711008 but the employee is 15.5m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [ReceivingTaskDriver] Employee_Joshua Ramirez: released Receive task for pallet e6dceaf7-f6e1-4ba0-bfd7-b5faf3711008 back to the queue (employee strayed outside receiving range).
- **WARN** (day 1): [AiNavigation] Employee_Joshua Ramirez cannot reach task position at (-2.94, 1.14, 48.31) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet cda22540-fcce-4c6c-8ec1-f47973b2831c but the employee is 6.2m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] Employee_Joshua Ramirez: task target (7.22, 1.13, 45.08) only partially reachable — stopped 3.85m short. Treating as arrived.
- **REPEATING** (day 1): x25 and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet cda22540-fcce-4c6c-8ec1-f47973b2831c but the employee is 5.4m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [ReceivingTaskDriver] Employee_Joshua Ramirez: released Receive task for pallet cda22540-fcce-4c6c-8ec1-f47973b2831c back to the queue (employee strayed outside receiving range).
- **REPEATING** (day 1): x25 and counting: [AiNavigation] Employee_Joshua Ramirez: task target (7.66, 2.19, 44.34) only partially reachable — stopped 3.81m short. Treating as arrived.
- **OVERLAP** (day 1): OVERLAP Pallet_00_Slot0_Tier0 & Pallet_04_Slot2_Tier0 @(33, 89)  <<HISTORY: d1 14:55 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 18:05 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 18:14 moved (46, 84)->(-1, -1) by InventoryService.MovePallet | d1 18:43 moved (-1, -1)->(34, 89) by InventoryService.MovePallet | d1 18:43 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 18:45 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>  <<HISTORY: d1 15:10 Receive task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 18:46 Putaway task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d1 18:57 moved (46, 83)->(-1, -1) by InventoryService.MovePallet | d1 19:24 moved (-1, -1)->(34, 89) by InventoryService.MovePallet | d1 19:24 Putaway task Assigned->Complete by WorkQueueSystem.CompleteTask | d1 19:26 Putaway task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 8b8de58d-4dc6-4e9a-b5f6-5010fa12b8a1 but the employee is 5.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 1a6ad5af-4300-4e3c-8375-337c8976c522 but the employee is 11.2m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 4ed41a7a-b0cc-46a1-b592-2bdbeb467ba8 but the employee is 9.8m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet b91fb520-7697-490b-918e-0bfc7842bf0b but the employee is 9.9m away (max 4.5m) — refusing to start receiving.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1242 Bulk Basin Foods: 7 plt $3,246
- d1 placed parked Inbound Bulk Basin Foods day 1 block 18 door 1
- d1 PO IG1243 Anchor Point Foods: 3 plt $1,868
- d1 placed parked Inbound Anchor Point Foods day 1 block 19 door 2
- d1 PO IG1244 Backdoor Bill's Overstock: 3 plt $2,968
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 21 door 2
- d1 PO IG1245 Coastal Cargo Exchange: 5 plt $6,308
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 23 door 1
- d1 PO IG1246 Blue Ridge Provisions: 5 plt $6,414
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 23 door 2
- d1 PO IG1247 Anchor Point Foods: 6 plt $3,800
- d1 placed parked Inbound Anchor Point Foods day 1 block 27 door 1
- d1 PO IG1248 Homestead Market Co.: 4 plt $3,564
- d1 placed parked Inbound Homestead Market Co. day 1 block 27 door 2
- d1 PO IG1249 Homestead Market Co.: 2 plt $1,152
- d1 placed parked Inbound Homestead Market Co. day 1 block 30 door 2
- d2 released 1 order(s): OG2167 (trailer 7.9 h away; 6.2 h work + 2 h)
- d2 PO IG2250 Fairweather Trading Co.: 9 plt $6,965
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 36 door 1
- d2 PO IG2251 Ambrose Fine Foods: 5 plt $12,984
- d2 placed parked Inbound Ambrose Fine Foods day 2 block 37 door 2
- d2 PO IG2252 Anchor Point Foods: 3 plt $2,021
- d2 placed parked Inbound Anchor Point Foods day 2 block 41 door 2
- d2 booked Sneaky Pete's Seafood day 3 block 16 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 49 + 6.2h of work)
- d2 PO IG2253 Bulk Basin Foods: 3 plt $968
- d2 placed parked Inbound Bulk Basin Foods day 2 block 42 door 1
- d2 booked Sneaky Pete's Seafood day 3 block 16 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 49 + 6.2h of work)
- d2 PO IG2254 Homestead Market Co.: 3 plt $3,136
- d2 placed parked Inbound Homestead Market Co. day 2 block 43 door 2
- d2 PO IG2255 Backdoor Bill's Overstock: 2 plt $1,804
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 44 door 1
- d2 released 1 order(s): OG3168 (trailer 18.9 h away; 16.9 h work + 2 h)
- d2 booked Sneaky Pete's Seafood day 3 block 30 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 46 + 16.5h of work)
- d2 booked Sneaky Pete's Seafood day 3 block 32 door 2 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 47 + 16.5h of work)
- d3 booked Sneaky Pete's Seafood day 4 block 20 door 1 (case-pick, 1 picklist(s) / 4 selector(s), 0 pallet pick(s) / 2 reach truck(s): stock ready hour 65 + 16.5h of work)
- d3 released 1 order(s): OG4169 (trailer 20.9 h away; 19.0 h work + 2 h)
- d3 released 1 order(s): OG4170 (trailer 20.4 h away; 23.8 h work + 2 h)
