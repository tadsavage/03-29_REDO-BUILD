# Playtest block 30 — days 1–8

## Score: **52.4 (+15.6)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (6 shipped, 0 late) | 15% | 100.0 (+100.0) |
| Fill rate | 10% | 68.2 (-23.8) |
| Throughput (6 plt vs target 84) | 15% | 7.1 (-46.4) |
| Revenue ($1,722) | 15% | 3.1 (-60.5) |
| Profit (cash -43,350) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 100.0 (0.0) |
| Reliability (bugs) | 25% | 80.0 (+70.1) |
| Reputation (0 to 18) | 5% | 80.0 (+30.0) |

Orders shipped **6**, late/fined **0**, pallets shipped **6**. Cash $205,368 → $162,018 (-43,350). Stock at cost $0 → $22,334 (not part of profit).
Bot did: 1 contracts signed, 13 dock bookings, 6 orders released, 13 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **30**
  - ERROR|[ReachTruckOperator] 'ReachTruck' pallet pick: → lane #D: destination did not take — no path and none pending for driveTarget=(#, #, #) (anchor=(#, #, #), from=(#, #, #), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  - ERROR|[TrailerLoad] 'Dockstocker' cross-door load: to lane #C: destination did not take (no path) for (#, #, #) from (#, #, #).
  - ERROR|[TrailerLoad] Could not reach lane #C — leaving OutboundPallet_#-f#e##b#-bd###_# staged.
  - ERROR|[ReachTruckOperator] 'ReachTruck' deliver: → location ### anchor: destination did not take — no path and none pending for driveTarget=(#, #, #) (anchor=(#, #, #), from=(#, #, #), onMesh=True). Aborting leg immediately instead of waiting out the timeout.
  - VEHICLE-STATIONARY|Dockstocker has a driver, hasn't moved in #s while # task(s) it could take wait
  - FLOATING| OutboundPallet_###bda#f#-#_# @#A# bottom=#
  - OVERLAP| OutboundPallet_###bda#f#-#_# & OutboundPallet_###bda#f#-#_# @#A#
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #D: [#: own/Staged/y#, own/Staged/y#] lane #A: [#: own/Staged/y#, own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #D: [#: own/Staged/y#, own/Staged/y#] lane #A: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #B: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #D: [#: own/Staged/y#, own/Staged/y#] lane #A: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #C: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #E: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] 
  - DOOR-DEADLOCK|OG# is Staged but door #'s truck has waited # min with no one loading it. | lane #D: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] lane #E: [#: own/Staged/y#, own/Staged/y#] lane #B: [#: OTHER ###bda#f#-#/Staged/y#, OTHER ###bda#f#-#/Staged/y#] [#: OTHER ###bda#f#-#/Staged/y#, OTHER ###bda#f#-#/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#] 
  - ERROR|[OrderSelectionTaskDriver] Pallet jack: no path to (#, #, #) for 'pallet jack parking'.
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
- New this block: **1**
  - REPEATING|x# and counting: Parameter 'IsWaving' does not exist.

## Bugs found (2 distinct, 2 incidents)

| Incidents | Bug |
|---|---|
| 1 | REPEATING — x# and counting: Parameter 'IsWaving' does not exist. |
| 1 | WARN — [AiNavigation] Dockstocker: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Dockstocker: stuck at (-3.60, 1.11, 40.66) — walking out to (-15.92, 1.16, 47.12) (12.91m to mesh).
- **REPEATING** (day 7): x25 and counting: Parameter 'IsWaving' does not exist.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1537 Anchor Point Foods: 6 plt $4,240
- d1 placed parked Inbound Anchor Point Foods day 1 block 18 door 1
- d1 PO IG1538 Fairweather Trading Co.: 7 plt $5,875
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 19 door 2
- d1 PO IG1539 Homestead Market Co.: 1 plt $1,596
- d1 placed parked Inbound Homestead Market Co. day 1 block 20 door 3
- d1 PO IG1540 Homestead Market Co.: 1 plt $1,920
- d1 placed parked Inbound Homestead Market Co. day 1 block 22 door 1
- d1 PO IG1541 Backdoor Bill's Overstock: 2 plt $1,820
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 24 door 1
- d1 PO IG1542 Bulk Basin Foods: 3 plt $1,604
- d1 placed parked Inbound Bulk Basin Foods day 1 block 25 door 2
- d2 released 1 order(s)
- d2 PO IG2543 Anchor Point Foods: 2 plt $1,224
- d2 placed parked Inbound Anchor Point Foods day 2 block 36 door 1
- d3 released 1 order(s)
- d3 PO IG3544 Homestead Market Co.: 2 plt $1,527
- d3 placed parked Inbound Homestead Market Co. day 3 block 36 door 1
- d3 PO IG3545 Anchor Point Foods: 1 plt $480
- d3 placed parked Inbound Anchor Point Foods day 3 block 37 door 2
- d3 PO IG3546 Backdoor Bill's Overstock: 1 plt $1,020
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 38 door 1
- d4 released 1 order(s)
- d4 PO IG4547 Bulk Basin Foods: 2 plt $1,152
- d4 placed parked Inbound Bulk Basin Foods day 4 block 36 door 1
- d4 PO IG4548 Homestead Market Co.: 1 plt $672
- d4 placed parked Inbound Homestead Market Co. day 4 block 37 door 2
- d5 released 1 order(s)
- d6 released 1 order(s)
- d7 released 1 order(s)
- d7 PO IG7549 Bulk Basin Foods: 1 plt $112
- d7 placed parked Inbound Bulk Basin Foods day 7 block 36 door 1
