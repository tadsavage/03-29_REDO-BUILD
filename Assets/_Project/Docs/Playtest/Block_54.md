# Playtest block 54 — days 1–3

## Score: **43.4 (+10.8)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (4 shipped, 0 late) | 10% | 100.0 (+33.3) |
| Fill rate | 10% | 100.0 (0.0) |
| Throughput (12 plt vs target 24) | 10% | 50.0 (0.0) |
| Revenue ($37,962) | not scored | 100.0 (0.0) |
| Net Profit (-47,540) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 24.4 (+8.3) |
| Reputation (19 to 26) | 10% | 61.7 (+33.3) |

Orders shipped **4**, late/fined **0**, pallets shipped **12**. Cash $127,764 → $148,508 (+20,744). Stock at cost $200,106 → $131,822 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 7 dock bookings, 0 orders released, 4 POs, 0 rush POs.

## Where the money went

Income **$37,962**. Expenses **$17,218** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 37,230 |
| Handling | 732 |

| Expense | $ |
|---|---|
| Inventory | 11,180 |
| Wages | 2,835 |
| Maintenance | 1,618 |
| Lease / Mortgage | 784 |
| Groundskeeping | 781 |
| Loss Prevention | 20 |

## Change since last block
- Fixed / no longer seen: **7**
  - ERROR|[TrailerOffload] 'DS_FULL(Clone)' cross-door offload → #B: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMesh=False, pathStatus=n/a. Refusing to straight-line through obstacles — aborting this leg.
  - ERROR|[GridNav] 'Employee_Shirley Edwards': 'staging lane #A line-up' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Mark Peterson': 'staging lane #A line-up' was interrupted # times - giving the leg up.
  - ERROR|[GridNav] 'Employee_Larry Morgan': 'staging lane #A line-up' was interrupted # times - giving the leg up.
  - REPEATING|x# and counting: [AiNavigation] RT_Full(Clone): the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - WARN|[OrderSelectionTaskDriver] Order #-be##-af### (The Big Cheese Inc.) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##fda#b#c#-# (Chubby Chipmunk Cookies) has no reachable pick location for its remaining line item(s) — staging short at #/#.
- New this block: **6**
  - OVERLAP| A Chep & A Chep @(#, #)
  - WARN|[AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Employee_Kelly Harmon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##bce#f#-ac#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[AiNavigation] Employee_Mary Young: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.

## Bugs found (17 distinct, 47 incidents)

| Incidents | Bug |
|---|---|
| 12 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 7 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] RT_Full(Clone): stuck in aisle # - walking out forward along the right lane to (#, #, #). |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | OVERLAP —  A Chep & A Chep @(#, #) |
| 2 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | REPEATING — x# and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload. |
| 2 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [AiNavigation] Employee_Kelly Harmon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e#a###ce### (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##bce#f#-ac#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Mary Young: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Kelly Harmon: stuck at (5.92, 1.16, 49.86) — walking out to (-5.33, 1.21, 43.36) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck in aisle 01 - walking out forward along the right lane to (-37.12, 1.11, 42.34).
- **WARN** (day 1): [AiNavigation] RT_Full(Clone): stuck at (-25.60, 1.11, 43.21) — walking out to (-37.12, 1.11, 42.34) (11.55m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (-11.58, 1.12, 58.65) — walking out to (-11.58, 1.21, 45.65) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (-12.34, 1.13, 57.70) — walking out to (-23.58, 1.12, 50.90) (12.14m to mesh).
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 108/248.
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-16.73, 1.14, 47.95) — walking out to (-16.73, 0.04, 34.46) (12.49m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (-9.38, 1.14, 55.40) — walking out to (-2.88, 1.22, 44.15) (12.00m to mesh).
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 25 task(s) it could take wait
- **REPEATING** (day 2): x25 and counting: [TrailerOffload] No reachable Inbound/Both staging-lane slot anywhere in the building — returning the pallet to the trailer and pausing the offload.
- **WARN** (day 2): [AiNavigation] Employee_Larry Morgan: stuck at (-38.52, 1.12, 43.12) — walking out to (-32.02, 1.12, 54.38) (12.00m to mesh).
- **WARN** (day 2): [AiNavigation] PltJack_FULL(Clone): stuck at (-38.02, 1.11, 43.06) — walking out to (-38.59, 1.17, 41.68) (12.00m to mesh).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order a73ffce7-e0a7-4563-8ce5-522a69ed33da (Crunch-Time Cereals) has no reachable pick location for its remaining line item(s) — staging short at 42/55.
- **WARN** (day 2): [AiNavigation] RT_Full(Clone) cannot reach task position at (-45.76, 1.18, 40.36) (path invalid) — abandoning the walk and handing the task back.
- **OVERLAP** (day 2): OVERLAP A Chep & A Chep @(22, 84)  <<HISTORY: d2 17:02 Replenish task created (01-14-B0->02-03-00) by ReplenishmentService.CreateReplenishTask | d2 17:03 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 17:08 moved (23, 83)->(-1, -1) by InventoryService.MovePallet | d2 17:42 moved (-1, -1)->(23, 84) by InventoryService.MovePallet | d2 17:42 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask | d2 17:43 Replenish task Complete->Available by ReachTruckOperator.ReleaseTaskWithBackoff>>  <<HISTORY: d1 08:10 restored from save at (24, 84) 32x659083 by InventoryService.RegisterPalletDirect | d2 17:46 Replenish task created (02-05-B0->02-03-00) by ReplenishmentService.CreateReplenishTask | d2 17:46 Replenish task Available->Assigned by WorkQueueSystem.TryClaimSpecificTask | d2 18:02 moved (24, 84)->(-1, -1) by InventoryService.MovePallet | d2 18:09 moved (-1, -1)->(23, 84) by InventoryService.MovePallet | d2 18:09 Replenish task Assigned->Complete by WorkQueueSystem.CompleteTask>>
- **WARN** (day 2): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 0ac24a01-2bce-46f6-ac80-bb050e0a5701 but the employee is 25.8m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 2): [AiNavigation] Employee_Mary Young: task target (-13.06, 1.20, 45.66) is not reachable — the partial path ends 10.11m short of it. Handing the task back.

## Bot actions (first 400)
- d1 signed bulk BULK_Grizzly Ridge Jerky_D1_0
- d1 PO IG1030 Bulk Basin Foods: 5 plt $2,600
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 3
- d1 placed parked Inbound Halloran Household day 1 block 25 door 1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 26 door 1
- d1 placed parked Inbound Bulk Basin Foods day 1 block 26 door 2
- d1 PO IG1031 Cornerstone Staples: 5 plt $2,600
- d1 placed parked Inbound Cornerstone Staples day 1 block 44 door 1
- d2 PO IG2032 Iron Gate Staples: 5 plt $2,860
- d2 placed parked Inbound Iron Gate Staples day 2 block 14 door 1
- d2 PO IG2033 Anchor Point Foods: 5 plt $3,120
- d2 placed parked Inbound Anchor Point Foods day 2 block 32 door 2
