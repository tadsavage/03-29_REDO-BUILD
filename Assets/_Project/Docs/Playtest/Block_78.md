# Playtest block 78 — days 1–4

## Score: **60.0 (+39.8)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 2 late) | 10% | 0.0 (0.0) |
| Fill rate | 10% | 0.0 (0.0) |
| Throughput (0 plt vs target 36) | 10% | 0.0 (0.0) |
| Revenue ($0) | not scored | 0.0 (0.0) |
| Net Profit (-14,379) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 0% | 100.0 (0.0) |
| Reliability (bugs) | 50% | 100.0 (+79.6) |
| Reputation (0 to 55) | 10% | 100.0 (0.0) |

Orders shipped **0**, late/fined **2**, pallets shipped **0**. Cash $130,347 → $72,001 (-58,346). Stock at cost $0 → $43,967 (offsets stock bought in Net Profit).
Bot did: 1 contracts signed, 14 dock bookings, 4 orders released, 14 POs, 0 rush POs.

## Where the money went

Income **$0**. Expenses **$58,346** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|

| Expense | $ |
|---|---|
| Inventory | 44,857 |
| Wages | 4,623 |
| Maintenance | 3,628 |
| Fines | 2,741 |
| Groundskeeping | 1,272 |
| Lease / Mortgage | 1,194 |
| Loss Prevention | 31 |

## Late orders - state when it came due
- 2 x still being picked

- day 2: OG2172 (Sneaky Pete's Seafood) was still being picked (0/482 cs). Fined $2,152. Lines 50, 482 cs, released=True.
- day 3: OG3173 (Sneaky Pete's Seafood) was still being picked (0/111 cs). Fined $589. Lines 13, 111 cs, released=True.

## Change since last block
- Fixed / no longer seen: **27**
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
- New this block: **0**

## Bugs found (0 distinct, 0 incidents)

| Incidents | Bug |
|---|---|

## First sightings (with stack traces)

_none_

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1256 Coastal Cargo Exchange: 5 plt $6,596
- d1 placed parked Inbound Coastal Cargo Exchange day 1 block 18 door 1
- d1 PO IG1257 Fairweather Trading Co.: 9 plt $6,478
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 19 door 2
- d1 PO IG1258 Anchor Point Foods: 9 plt $5,836
- d1 placed parked Inbound Anchor Point Foods day 1 block 22 door 1
- d1 PO IG1259 Homestead Market Co.: 4 plt $3,826
- d1 placed parked Inbound Homestead Market Co. day 1 block 25 door 2
- d1 PO IG1260 Bulk Basin Foods: 7 plt $3,729
- d1 placed parked Inbound Bulk Basin Foods day 1 block 28 door 1
- d1 PO IG1261 Backdoor Bill's Overstock: 3 plt $2,912
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 28 door 2
- d2 released 1 order(s): OG2172 (trailer 13.9 h away; 12.3 h work + 2 h)
- d2 PO IG2262 Bulk Basin Foods: 3 plt $440
- d2 placed parked Inbound Bulk Basin Foods day 2 block 36 door 2
- d2 PO IG2263 Anchor Point Foods: 2 plt $888
- d2 placed parked Inbound Anchor Point Foods day 2 block 38 door 2
- d2 released 1 order(s): OG3173 (trailer 18.9 h away; 16.9 h work + 2 h)
- d3 released 1 order(s): OG4174 (trailer 26.9 h away; 24.9 h work + 2 h)
- d3 released 1 order(s): OG4175 (trailer 26.4 h away; 30.6 h work + 2 h)
- d3 PO IG3264 Blue Ridge Provisions: 3 plt $3,960
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 36 door 1
- d3 PO IG3265 Blue Ridge Provisions: 5 plt $7,194
- d3 placed parked Inbound Blue Ridge Provisions day 3 block 37 door 2
- d3 PO IG3266 Anchor Point Foods: 2 plt $1,488
- d3 placed parked Inbound Anchor Point Foods day 3 block 38 door 1
- d3 PO IG3267 Bulk Basin Foods: 2 plt $512
- d3 placed parked Inbound Bulk Basin Foods day 3 block 40 door 1
- d3 PO IG3268 Homestead Market Co.: 1 plt $486
- d3 placed parked Inbound Homestead Market Co. day 3 block 41 door 2
- d3 PO IG3269 Homestead Market Co.: 1 plt $512
- d3 placed parked Inbound Homestead Market Co. day 3 block 45 door 1
