# Playtest block 35 — days 1–8

## Score: **46.0 (0.0)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (6 shipped, 1 late) | 15% | 83.3 (-16.7) |
| Fill rate | 10% | 43.8 (-17.0) |
| Throughput (26 plt vs target 84) | 15% | 31.0 (+6.0) |
| Revenue ($11,166) | 15% | 19.9 (-6.5) |
| Profit (cash -33,192) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 45.9 (+17.2) |
| Reputation (0 to 31) | 5% | 100.0 (0.0) |

Orders shipped **6**, late/fined **1**, pallets shipped **26**. Cash $221,225 → $188,033 (-33,192). Stock at cost $0 → $19,240 (not part of profit).
Bot did: 0 contracts signed, 21 dock bookings, 6 orders released, 21 POs, 0 rush POs.

## Late orders - state when it came due
- 1 x loaded but the truck had not left

- day 7: OG7128 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $811. Lines 13, 193 cs, released=True.

## Change since last block
- Fixed / no longer seen: **17**
  - WARN|[AiNavigation] Dockstocker cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] ReachTruck cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back.
  - WARN|[AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order ###baa-bfb#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[ReceiverReceivingWorkflow] Arrival callback fired for pallet ##c##cd#f#-# but the employee is #m away (max #m) — refusing to start receiving.
  - WARN|[OrderSelectionTaskDriver] Order #-b##a#-ac### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
  - WARN|[OrderSelectionTaskDriver] Order ###e-b#d### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
  - WARN|[AiNavigation] PalletJack: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[ReachTruckOperator] Replenish: could not reach pallet at ##-A# within #m.
  - WARN|[OrderSelectionTaskDriver] Order #-bbfb#ec#f#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##a###e#e# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
  - WARN|[OrderSelectionTaskDriver] Order ##a###e#e# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##c#b#d#-a#-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s).
- New this block: **7**
  - WARN|[OrderSelectionTaskDriver] Order ###d##-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##aaa##-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ##a##-ad#b-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - TRUCK-STALLED| Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #)
  - WARN|[AiNavigation] Employee_Rachel Lopez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - WARN|[OrderSelectionTaskDriver] Order ###dc#-b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.
  - WARN|[OrderSelectionTaskDriver] Order ###f#-b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#.

## Bugs found (7 distinct, 14 incidents)

| Incidents | Bug |
|---|---|
| 4 | WARN — [OrderSelectionTaskDriver] Order ###d##-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##aaa##-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##a##-ad#b-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 1 | WARN — [AiNavigation] Employee_Rachel Lopez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###dc#-b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ###f#-b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Rachel Lopez: stuck at (-19.26, 1.13, 41.69) — walking out to (-8.00, 1.21, 35.19) (12.00m to mesh).
- **WARN** (day 2): [OrderSelectionTaskDriver] Order 0ded9fb8-3556-49d8-9787-f927ca7d79f0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 62/337.
- **WARN** (day 4): [OrderSelectionTaskDriver] Order b7f0b750-3234-4dc6-b002-3beb30b8d794 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 45/93.
- **WARN** (day 5): [OrderSelectionTaskDriver] Order c7916be1-0aaa-4119-9002-a802c2a0d5d0 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 42/135.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 51bed305-7415-4f24-b325-0cb406546f25 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 41/160.
- **WARN** (day 7): [OrderSelectionTaskDriver] Order 2cd9e603-59a7-4377-ad8b-a15bd6cf4d99 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 66/193.
- **TRUCK-STALLED** (day 7): TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 120s at (4.6, 0.0, 18.7)

## Bot actions (first 400)
- d1 PO IG1144 Bulk Basin Foods: 10 plt $3,056
- d1 placed parked Inbound Bulk Basin Foods day 1 block 18 door 1
- d1 PO IG1145 Anchor Point Foods: 3 plt $1,504
- d1 placed parked Inbound Anchor Point Foods day 1 block 19 door 2
- d1 PO IG1146 Blue Ridge Provisions: 2 plt $4,234
- d1 placed parked Inbound Blue Ridge Provisions day 1 block 20 door 3
- d1 PO IG1147 Anchor Point Foods: 3 plt $1,692
- d1 placed parked Inbound Anchor Point Foods day 1 block 22 door 2
- d1 PO IG1148 Homestead Market Co.: 2 plt $1,800
- d1 placed parked Inbound Homestead Market Co. day 1 block 24 door 2
- d1 PO IG1149 Homestead Market Co.: 1 plt $640
- d1 placed parked Inbound Homestead Market Co. day 1 block 26 door 1
- d1 PO IG1150 Backdoor Bill's Overstock: 3 plt $864
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 27 door 1
- d2 released 1 order(s)
- d2 released 1 order(s)
- d2 PO IG2151 Anchor Point Foods: 2 plt $1,140
- d2 placed parked Inbound Anchor Point Foods day 2 block 36 door 2
- d2 PO IG2152 Bulk Basin Foods: 1 plt $128
- d2 placed parked Inbound Bulk Basin Foods day 2 block 37 door 3
- d3 PO IG3153 Bulk Basin Foods: 2 plt $656
- d3 placed parked Inbound Bulk Basin Foods day 3 block 36 door 2
- d3 PO IG3154 Backdoor Bill's Overstock: 2 plt $2,360
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 37 door 3
- d3 PO IG3155 Homestead Market Co.: 1 plt $1,596
- d3 placed parked Inbound Homestead Market Co. day 3 block 38 door 2
- d4 released 1 order(s)
- d4 PO IG4156 Anchor Point Foods: 1 plt $608
- d4 placed parked Inbound Anchor Point Foods day 4 block 14 door 1
- d4 PO IG4157 Anchor Point Foods: 1 plt $672
- d4 placed parked Inbound Anchor Point Foods day 4 block 36 door 2
- d4 PO IG4158 Bulk Basin Foods: 2 plt $624
- d4 placed parked Inbound Bulk Basin Foods day 4 block 37 door 1
- d4 PO IG4159 Homestead Market Co.: 1 plt $1,760
- d4 placed parked Inbound Homestead Market Co. day 4 block 38 door 2
- d5 released 1 order(s)
- d6 PO IG6160 Homestead Market Co.: 1 plt $448
- d6 placed parked Inbound Homestead Market Co. day 6 block 2 door 1
- d6 PO IG6161 Bulk Basin Foods: 2 plt $1,123
- d6 placed parked Inbound Bulk Basin Foods day 6 block 3 door 1
- d6 PO IG6162 Backdoor Bill's Overstock: 1 plt $272
- d6 placed parked Inbound Backdoor Bill's Overstock day 6 block 4 door 2
- d6 released 1 order(s)
- d6 PO IG6163 Homestead Market Co.: 1 plt $540
- d6 placed parked Inbound Homestead Market Co. day 6 block 36 door 2
- d7 released 1 order(s)
- d7 PO IG7164 Backdoor Bill's Overstock: 1 plt $256
- d7 placed parked Inbound Backdoor Bill's Overstock day 7 block 36 door 2
