# Playtest block 38 — days 1–8

## Score: **34.0 (-9.4)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (7 shipped, 0 late) | 10% | 100.0 (0.0) |
| Fill rate | 10% | 59.6 (+5.9) |
| Throughput (13 plt vs target 84) | 10% | 15.5 (+2.4) |
| Revenue ($12,349) | not scored | 22.1 (+9.8) |
| Net Profit (-27,862) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 15.6 (-21.2) |
| Reputation (0 to 22) | 10% | 86.7 (-13.3) |

Orders shipped **7**, late/fined **0**, pallets shipped **13**. Cash $184,442 → $137,075 (-47,367). Stock at cost $0 → $19,505 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 18 dock bookings, 9 orders released, 18 POs, 0 rush POs.

## Where the money went

Income **$12,349**. Expenses **$54,716** (stock bought shows as Inventory). Loan principal repaid **$5,000** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 11,130 |
| Handling | 1,219 |

| Expense | $ |
|---|---|
| Inventory | 24,318 |
| Wages | 10,725 |
| Lease / Mortgage | 10,076 |
| Maintenance | 6,579 |
| Loan Principal | 5,000 |
| Groundskeeping | 2,939 |
| Loss Prevention | 79 |

## Change since last block
- Fixed / no longer seen: **17**
  - WARN|[AiNavigation] Employee_Daniel Wilson: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back.
  - WARN|[AiNavigation] Employee_Daniel Wilson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-f#fe#a##c#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ae##d#f### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order ###d-a#b#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ###cff-b#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#a##-ac#e## but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order ###e##db## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order #-fd##ae#-bac#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###a#dca## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a###c#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##f#f#afd-a#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ###ca#e### but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ad#ceb#afa## but the employee is #m away (max #m) — refusing to start receiving.
- New this block: **23**
  - WARN|[AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Judith Martin: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_James Williams: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Jonathan Allen: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[AiNavigation] Employee_Victoria Lewis: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - ERROR|[OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop.
  - ERROR|[OrderSelectionTaskDriver] Pallet jack did not reach 'bay ##' ((#, #, #)) after #s.
  - WARN|[AiNavigation] Employee_Janet Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead.
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: no progress toward task target (#, #, #) for #s (stopped #m short, path PathComplete) — handing the task back.
  - REPEATING|x# and counting: [AiNavigation] Dockstocker: no progress toward task target (#, #, #) for #s (stopped #m short, path PathComplete) — handing the task back.

## Bugs found (24 distinct, 107 incidents)

| Incidents | Bug |
|---|---|
| 25 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 21 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 10 | WARN — [AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] Employee_Judith Martin: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 8 | WARN — [AiNavigation] Employee_James Williams: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 7 | WARN — [AiNavigation] Employee_Jonathan Allen: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 6 | WARN — [AiNavigation] Employee_Victoria Lewis: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop. |
| 2 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'bay ##' ((#, #, #)) after #s. |
| 4 | WARN — [AiNavigation] Employee_Janet Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: the straight way out of (#, #, #) runs through something solid - taking a clear way out to (#, #, #) instead. |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: no progress toward task target (#, #, #) for #s (stopped #m short, path PathComplete) — handing the task back. |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: no progress toward task target (#, #, #) for #s (stopped #m short, path PathComplete) — handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-fbcd#dd##f-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-b##b#d-a#f## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##afa#c#b#b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##c##de-adb#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##afa#c#b#b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##e##c##aac# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###df#-bb#a## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-dfe##-bf### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] ReachTruck: stuck at (1.41, 1.11, 52.27) — walking out to (0.83, 1.25, 51.67) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Dockstocker: stuck at (-3.08, 1.11, 49.26) — walking out to (-16.08, 1.16, 49.26) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Victoria Lewis: stuck at (-30.59, 1.12, 46.45) — walking out to (-37.09, 1.08, 35.19) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Jonathan Allen: stuck at (-40.13, 1.12, 42.96) — walking out to (-28.87, 1.09, 36.33) (12.06m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Judith Martin: stuck at (-42.74, 1.12, 50.55) — walking out to (-42.74, 1.09, 37.55) (12.00m to mesh).
- **WARN** (day 2): [AiNavigation] Employee_James Williams: stuck at (-44.76, 1.11, 43.05) — walking out to (-33.51, 1.09, 36.32) (12.11m to mesh).
- **WARN** (day 2): [AiNavigation] PalletJack: stuck at (2.00, 1.11, 49.50) — walking out to (12.67, 1.33, 43.00) (12.00m to mesh).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order d1f7d993-fbcd-4dd8-957f-de03598d8f56 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 27/48 case(s).
- **REPEATING** (day 3): x25 and counting: [AiNavigation] Dockstocker: the straight way out of (-25.49, 1.11, 56.13) runs through something solid - taking a clear way out to (-26.55, 1.16, 56.33) instead.
- **WARN** (day 3): [AiNavigation] Employee_Janet Peterson: stuck at (-18.09, 1.12, 41.42) — walking out to (-6.84, 1.19, 47.92) (12.00m to mesh).
- **WARN** (day 3): [OrderSelectionTaskDriver] Order 1970bbf7-b584-4b8d-a58f-6ca72b2c320d (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 15/24.
- **WARN** (day 4): [OrderSelectionTaskDriver] Order bef107c1-1afa-4c5b-8b79-508f65acf9b9 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 29/94.
- **ERROR** (day 4): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-02' (4.8m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__159:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1548)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 4): [OrderSelectionTaskDriver] Order e1116618-64c0-47de-adb2-b5e983bec66e (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 40/80.
- **WARN** (day 4): [OrderSelectionTaskDriver] Order bef107c1-1afa-4c5b-8b79-508f65acf9b9 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 63/94 case(s).
- **WARN** (day 4): [AiNavigation] ReachTruck cannot reach task position at (7.33, 1.16, 50.96) (path invalid) — abandoning the walk and handing the task back.
- **REPEATING** (day 4): x25 and counting: [AiNavigation] Dockstocker: stuck at (6.43, 1.11, 50.17) — walking out to (12.67, 1.33, 38.63) (12.22m to mesh).
- **REPEATING** (day 5): x25 and counting: [AiNavigation] ReachTruck: the straight way out of (-14.31, 1.11, 49.63) runs through something solid - taking a clear way out to (-14.89, 1.16, 48.24) instead.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order d05894ec-514e-4593-9c15-29199aac6728 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 29/44.
- **ERROR** (day 6): [OrderSelectionTaskDriver] Pallet jack did not reach 'bay 01-02' ((-19.91, 1.15, 39.81)) after 7.3s.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<JackNavLeg>d__166:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1860)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 8e9ff3d6-7365-4df9-bb9a-5f0175c0bdc1 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 9/17 case(s).
- **REPEATING** (day 6): x25 and counting: [AiNavigation] ReachTruck: no progress toward task target (-43.11, 1.16, 56.26) for 10s (stopped 51.47m short, path PathComplete) — handing the task back.
- **REPEATING** (day 6): x25 and counting: [AiNavigation] Dockstocker: no progress toward task target (-44.44, 1.16, 50.96) for 10s (stopped 34.63m short, path PathComplete) — handing the task back.
- **WARN** (day 7): [OrderSelectionTaskDriver] Order abfec322-dfe1-4893-bf82-689a42afbe13 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 10/17.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1794 Anchor Point Foods: 5 plt $2,816
- d1 placed parked Inbound Anchor Point Foods day 1 block 18 door 1
- d1 PO IG1795 Blue Ridge Provisions: 2 plt $3,242
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 19 door 2
- d1 PO IG1796 Homestead Market Co.: 2 plt $1,728
- d1 placed parked Inbound Homestead Market Co. day 1 block 20 door 3
- d1 PO IG1797 Homestead Market Co.: 1 plt $1,760
- d1 placed parked Inbound Homestead Market Co. day 1 block 22 door 1
- d1 PO IG1798 Bulk Basin Foods: 2 plt $1,104
- d1 placed parked Inbound Bulk Basin Foods day 1 block 24 door 1
- d2 released 1 order(s)
- d2 PO IG2799 Blue Ridge Provisions: 2 plt $2,560
- d2 placed parked Inbound Blue Ridge Provisions day 2 block 36 door 1
- d2 PO IG2800 Homestead Market Co.: 1 plt $576
- d2 placed parked Inbound Homestead Market Co. day 2 block 37 door 2
- d2 PO IG2801 Bulk Basin Foods: 2 plt $328
- d2 placed parked Inbound Bulk Basin Foods day 2 block 38 door 1
- d2 PO IG2802 Backdoor Bill's Overstock: 1 plt $320
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 40 door 1
- d2 PO IG2803 Anchor Point Foods: 1 plt $572
- d2 placed parked Inbound Anchor Point Foods day 2 block 42 door 1
- d3 released 1 order(s)
- d3 PO IG3804 Homestead Market Co.: 1 plt $1,672
- d3 placed parked Inbound Homestead Market Co. day 3 block 36 door 1
- d4 released 2 order(s)
- d4 PO IG4805 Homestead Market Co.: 1 plt $486
- d4 placed parked Inbound Homestead Market Co. day 4 block 36 door 1
- d4 PO IG4806 Anchor Point Foods: 1 plt $768
- d4 placed parked Inbound Anchor Point Foods day 4 block 37 door 1
- d4 PO IG4807 Bulk Basin Foods: 1 plt $544
- d4 placed parked Inbound Bulk Basin Foods day 4 block 38 door 1
- d5 released 1 order(s)
- d6 PO IG6808 Homestead Market Co.: 1 plt $576
- d6 placed parked Inbound Homestead Market Co. day 6 block 2 door 1
- d6 PO IG6809 Bulk Basin Foods: 1 plt $595
- d6 placed parked Inbound Bulk Basin Foods day 6 block 3 door 1
- d6 released 1 order(s)
- d6 PO IG6810 Blue Ridge Provisions: 3 plt $2,583
- d6 placed parked Inbound Blue Ridge Provisions day 6 block 36 door 1
- d6 PO IG6811 Bulk Basin Foods: 3 plt $2,088
- d6 placed parked Inbound Bulk Basin Foods day 6 block 37 door 2
- d7 released 1 order(s)
- d7 released 1 order(s)
- d7 released 1 order(s)
