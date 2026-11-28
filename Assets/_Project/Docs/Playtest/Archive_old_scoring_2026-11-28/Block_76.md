# Playtest block 76 — days 1–4

## Score: **60.2 (+15.9)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 0 late) | 10% | 100.0 (+50.0) |
| Fill rate | 10% | 70.1 (+0.4) |
| Throughput (2 plt vs target 36) | 10% | 5.6 (-16.7) |
| Revenue ($4,634) | not scored | 19.3 (-80.7) |
| Net Profit (-7,048) | 10% | 0.0 (-100.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 66.7 (+45.3) |
| Reputation (0 to 26) | 10% | 93.3 (-1.7) |

Orders shipped **2**, late/fined **0**, pallets shipped **2**. Cash $130,347 → $109,053 (-21,294). Stock at cost $0 → $14,246 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 9 dock bookings, 2 orders released, 9 POs, 0 rush POs.

## Where the money went

Income **$4,634**. Expenses **$25,928** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 4,422 |
| Handling | 212 |

| Expense | $ |
|---|---|
| Inventory | 15,479 |
| Wages | 4,324 |
| Maintenance | 3,628 |
| Groundskeeping | 1,272 |
| Lease / Mortgage | 1,194 |
| Loss Prevention | 31 |

## Change since last block
- Fixed / no longer seen: **29**
  - WARN|[AiNavigation] Employee_Ann Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Ruth Murphy: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - WARN|[AiNavigation] Employee_Ann Peterson: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - WARN|[AiNavigation] Employee_Ruth Murphy: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [AiNavigation] Employee_Ruth Murphy: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Ruth Murphy: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
  - REPEATING|x# and counting: [AiNavigation] Employee_Ann Peterson: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Ann Peterson: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##fd#cfd-a#-# but the employee is #m away (max #m) — refusing to start receiving.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Ann Peterson: released Receive task for pallet ##fd#cfd-a#-# back to the queue (employee strayed outside receiving range).
  - REPEATING|x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##b#b#-a#-# but the employee is #m away (max #m) — refusing to start receiving.
  - REPEATING|x# and counting: [ReceivingTaskDriver] Employee_Ann Peterson: released Receive task for pallet ##b#b#-a#-# back to the queue (employee strayed outside receiving range).
  - WARN|[AiNavigation] Employee_Stephen Stout: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-b#c##af-a#-# but the employee is #m away (max #m) — refusing to start receiving.
- New this block: **6**
  - WARN|[AiNavigation] Employee_Kathryn Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Mark Smith cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Employee_Mark Smith: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#d##af#-bbfd## but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order #-c#b##dd#-ab### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ] 
  - WARN|[OrderSelectionTaskDriver] Order #-e#f##-b#cc#bb#a#e (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] 

## Bugs found (6 distinct, 6 incidents)

| Incidents | Bug |
|---|---|
| 1 | WARN — [AiNavigation] Employee_Kathryn Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Mark Smith cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Mark Smith: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#d##af#-bbfd## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-c#b##dd#-ab### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ]  |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e#f##-b#cc#bb#a#e (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ]  |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Kathryn Edwards: stuck at (-15.31, 0.04, 18.97) — walking out to (-2.31, 0.04, 18.97) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Smith cannot reach task position at (-6.44, 1.15, 48.31) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [AiNavigation] Employee_Mark Smith: stuck at (7.66, 1.12, 46.99) — walking out to (1.16, 0.04, 35.73) (12.00m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet d695e97a-a5d7-4af9-bbfd-831c5ca44a12 but the employee is 4.7m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order c8dd1c47-c3b9-4dd2-ab02-1e0eb706b2a0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 32/45. Still owed (pick faces with stock): 304759 x2 [01-13-01:0(behind) ] 435689 x11 [02-02-00:0(behind) ] 
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 3117d1a0-e5f4-4349-b2cc-702983bb4a4e (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 29/42. Still owed (pick faces with stock): 189432 x4 [01-09-00:0(behind) ] 517634 x8 [02-03-00:0(behind) ] 615873 x1 [02-04-01:0(behind) ] 

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1233 Anchor Point Foods: 3 plt $2,180
- d1 placed parked Inbound Anchor Point Foods day 1 block 18 door 1
- d1 PO IG1234 Bulk Basin Foods: 1 plt $440
- d1 placed parked Inbound Bulk Basin Foods day 1 block 19 door 2
- d1 PO IG1235 Homestead Market Co.: 1 plt $920
- d1 placed parked Inbound Homestead Market Co. day 1 block 20 door 1
- d1 PO IG1236 Homestead Market Co.: 1 plt $2,080
- d1 placed parked Inbound Homestead Market Co. day 1 block 23 door 1
- d2 released 1 order(s): OG2162 (trailer 7.4 h away; 5.8 h work + 2 h)
- d2 PO IG2237 Blue Ridge Provisions: 3 plt $4,566
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 36 door 1
- d2 PO IG2238 Anchor Point Foods: 1 plt $768
- d2 placed parked Inbound Anchor Point Foods day 2 block 37 door 2
- d3 released 1 order(s): OG3163 (trailer 7.4 h away; 5.4 h work + 2 h)
- d3 PO IG3239 Blue Ridge Provisions: 2 plt $2,076
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 36 door 1
- d3 PO IG3240 Homestead Market Co.: 2 plt $2,337
- d3 placed parked Inbound Homestead Market Co. day 3 block 37 door 2
- d3 PO IG3241 Bulk Basin Foods: 1 plt $112
- d3 placed parked Inbound Bulk Basin Foods day 3 block 38 door 1
