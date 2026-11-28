# Playtest block 1098 — days 1–4

## Score: **44.4** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (2 shipped, 1 late) | 10% | 50.0 |
| Fill rate | 10% | 69.7 |
| Throughput (8 plt vs target 36) | 10% | 22.2 |
| Revenue ($26,269) | not scored | 100.0 |
| Net Profit (+6,074) | 10% | 100.0 |
| Cash health (floor $15,000) | 0% | 100.0 |
| Reliability (bugs) | 50% | 21.4 |
| Reputation (0 to 27) | 10% | 95.0 |

Orders shipped **2**, late/fined **1**, pallets shipped **8**. Cash $500,000 → $111,751 (-388,249). Stock at cost $0 → $24,670 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went

Income **$30,519**. Expenses **$418,768** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 25,245 |
| Floor | 4,250 |
| Handling | 1,024 |

| Expense | $ |
|---|---|
| Racking | 115,600 |
| Floor | 74,160 |
| Loss Prevention | 64,031 |
| Walls | 59,950 |
| Vehicles | 35,100 |
| Inventory | 32,080 |
| Grounds | 21,890 |
| Wages | 4,784 |
| Maintenance | 3,630 |
| Barrier | 3,200 |
| Fines | 1,876 |
| Groundskeeping | 1,273 |
| Lease / Mortgage | 1,194 |

## Late orders - state when it came due
- 1 x loaded but the truck had not left

- day 2: OG2157 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,876. Lines 50, 442 cs, released=True.

## Bugs found (29 distinct, 38 incidents)

| Incidents | Bug |
|---|---|
| 4 | WARN — [AiNavigation] Employee_Ann Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] Employee_Ruth Murphy: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 3 | WARN — [AiNavigation] Employee_Ann Peterson: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 2 | WARN — [AiNavigation] Employee_Ruth Murphy: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Ruth Murphy: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Ruth Murphy: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] Employee_Ann Peterson: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Ann Peterson: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 1 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##fd#cfd-a#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Ann Peterson: released Receive task for pallet ##fd#cfd-a#-# back to the queue (employee strayed outside receiving range). |
| 1 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##b#b#-a#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Ann Peterson: released Receive task for pallet ##b#b#-a#-# back to the queue (employee strayed outside receiving range). |
| 2 | WARN — [AiNavigation] Employee_Stephen Stout: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-b#c##af-a#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###-b#c### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##e#a##f### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###-a#c#abeb but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##fd#cfd-a#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-ccdf##f#e## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Ruth Murphy cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##b#b#-a#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###ea#-adbe## but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-b#e#e#-b#fd-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-c##ef##d#a## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ]  |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-c##ef##d#a## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ]  |
| 1 | WARN — [AiNavigation] Employee_Stephen Stout: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-d#c#b#-a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ]  |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-fc#d#a#-a#ca## but the employee is #m away (max #m) — refusing to start receiving. |

## First sightings (with stack traces)

- **WARN** (day 1): To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.
- **WARN** (day 1): [AiNavigation] Employee_Ann Peterson: stuck at (-15.97, 0.03, 16.50) — walking out to (-22.47, 0.02, 5.24) (12.00m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 2be56c67-b0c9-48af-a454-c8ac8d50f041 but the employee is 19.1m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 2f0fe8f7-6538-4487-b5c6-5b6086a2cc34 but the employee is 17.1m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] Employee_Ruth Murphy: stuck at (11.91, 0.05, 44.88) — walking out to (18.41, 1.09, 55.34) (12.04m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Ruth Murphy: task target (-2.94, 2.50, 48.31) is not reachable — the partial path ends 16.85m short of it. Handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 695c0469-0e7a-4796-89f3-0f9353b2dd86 but the employee is 17.8m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [AiNavigation] Employee_Ruth Murphy: task target (-2.94, 2.33, 45.66) is not reachable — the partial path ends 16.33m short of it. Handing the task back.
- **REPEATING** (day 1): x25 and counting: [ReceivingTaskDriver] Employee_Ruth Murphy: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 046b1776-1152-4464-a28c-65497198abeb but the employee is 19.3m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [AiNavigation] Employee_Ann Peterson: task target (-7.76, 1.14, 46.99) is not reachable — the partial path ends 16.34m short of it. Handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet d8463fc0-08fd-4cfd-a817-e8f72db73f73 but the employee is 14.6m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [AiNavigation] Employee_Ann Peterson: task target (-7.76, 1.14, 46.99) is not reachable — the partial path ends 15.88m short of it. Handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 4d3feafc-ccdf-4176-8f6e-0d3ad550241e but the employee is 14.3m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [ReceivingTaskDriver] Employee_Ann Peterson: receive workflow cancelled (out of range) — releasing the task and resuming patrol.
- **REPEATING** (day 1): x25 and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet d8463fc0-08fd-4cfd-a817-e8f72db73f73 but the employee is 14.6m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [ReceivingTaskDriver] Employee_Ann Peterson: released Receive task for pallet d8463fc0-08fd-4cfd-a817-e8f72db73f73 back to the queue (employee strayed outside receiving range).
- **WARN** (day 1): [AiNavigation] Employee_Ruth Murphy cannot reach task position at (-3.86, 1.14, 44.77) (path invalid) — abandoning the walk and handing the task back.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 3d9f6d03-800b-4b13-a029-da883f3e9e8a but the employee is 15.2m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 8da8283a-7792-4ea9-adbe-13d00b832b82 but the employee is 9.6m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [ReceiverReceivingWorkflow] Arrival callback fired for pallet 3d9f6d03-800b-4b13-a029-da883f3e9e8a but the employee is 19.1m away (max 4.5m) — refusing to start receiving.
- **REPEATING** (day 1): x25 and counting: [ReceivingTaskDriver] Employee_Ann Peterson: released Receive task for pallet 3d9f6d03-800b-4b13-a029-da883f3e9e8a back to the queue (employee strayed outside receiving range).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 005d9465-b95e-49e6-b4fd-fc47c717f093 but the employee is 6.9m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [OrderSelectionTaskDriver] Order d5490248-c981-4ef8-9d2a-5aefd35bcff5 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 46/442. Still owed (pick faces with stock): 649320 x9 [02-05-01:0(behind) ] 113489 x9 [01-02-00:0(behind) ] 638412 x9 [02-05-00:0(behind) ] 141256 x9 [01-04-01:0(behind) ] 164923 x9 [01-07-00:0(behind) ] 792146 x4 [02-08-00:0(behind) ] 
- **WARN** (day 2): [AiNavigation] Employee_Stephen Stout: stuck at (5.67, 1.37, 55.29) — walking out to (11.81, 1.12, 55.29) (5.15m to mesh).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order d5490248-c981-4ef8-9d2a-5aefd35bcff5 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 309/442. Still owed (pick faces with stock): 649320 x9 [02-05-01:0(behind) ] 113489 x9 [01-02-00:0(behind) ] 638412 x9 [02-05-00:0(behind) ] 141256 x9 [01-04-01:0(behind) ] 164923 x9 [01-07-00:0(behind) ] 732408 x9 [02-07-00:0(behind) ] 172495 x6 [01-07-01:0(behind) ] 189432 x8 [01-09-00:0(behind) ] 983721 x7 [02-12-00:0(behind) ] 276945 x10 [01-13-00:0(behind) ] 129481 x9 [01-03-01:0(behind) ] 347610 x10 [02-01-00:0(behind) ] 564219 x10 [02-04-00:0(behind) ] 209875 x10 [01-11-01:0(behind) ] 134958 x9 [01-04-00:0(behind) ] 
- **WARN** (day 3): [AiNavigation] Employee_Stephen Stout: task target (7.95, 1.11, 58.59) is not reachable — the partial path ends 4.33m short of it. Handing the task back.
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 50008b63-d33c-4b52-a039-c192aefcc666 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 53/77. Still owed (pick faces with stock): 129481 x3 [01-03-01:0(behind) ] 141256 x3 [01-04-01:0(behind) ] 
- **WARN** (day 3): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 69c50a27-fc8d-41a2-a4ca-94d8a1df9e93 but the employee is 13.9m away (max 4.5m) — refusing to start receiving.

## Bot actions (first 400)
