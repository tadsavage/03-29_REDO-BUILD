# Playtest block 7 — days 1–8

## Score: **41.6** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (7 shipped, 16 late) | 15% | 30.4 |
| Fill rate | 10% | 34.0 |
| Throughput (39 plt vs target 84) | 15% | 46.4 |
| Revenue ($23,908) | 15% | 42.7 |
| Profit (cash -113,854) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 85.1 |
| Reliability (bugs) | 25% | 52.2 |
| Reputation (0 to 6) | 5% | 60.0 |

Orders shipped **7**, late/fined **16**, pallets shipped **39**. Cash $139,039 → $25,185 (-113,854).
Bot did: 6 contracts signed, 22 dock bookings, 7 orders released, 11 POs, 4 rush POs.

## Bugs found (6 distinct, 7 incidents)

| Incidents | Bug |
|---|---|
| 2 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Cheryl Thompson' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | REPEATING — x# and counting: [OrderService] Order OG#: no reserve or pick-face pallet of # left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it. |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **WARN** (day 2): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Cheryl Thompson' as its operator though nobody is on it or running it - clearing the stale link.
- **REPEATING** (day 2): x25 and counting: [OrderService] Order OG5164: no reserve or pick-face pallet of 897034 left to fill a pallet pick — filed unassigned; a Reach Truck resolves a source once one claims it.
- **REPEATING** (day 3): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (3.24, 1.11, 35.22) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **REPEATING** (day 3): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-23.82, 1.11, 34.66) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **REPEATING** (day 3): x25 and counting: [ReachTruckOperator] 'ReachTruck' found no Available Putaway/Replenish tasks this poll.
- **WARN** (day 7): [OrderService] Order OG7170 closed with a STAGED pallet (41 cs) that never made the trailer - not billed, returning it to stock.

## Bot actions (first 400)
- d4 placed parked Inbound Rush Courier day 4 block 1 door 2
- d4 RUSH PO IG4164 Rush Courier: 4 plt $5,788
- d5 placed parked Inbound Rush Courier day 5 block 2 door 1
- d5 PO IG5166 Last Call Liquidators: 4 plt $2,880
- d5 placed parked Inbound Cornerstone Staples day 5 block 3 door 1
- d6 placed parked Inbound Rush Courier day 6 block 1 door 2
- d6 PO IG6169 Salvage Row Traders: 1 plt $1,000
- d7 placed parked Inbound Rush Courier day 7 block 9 door 1
- d7 PO IG7171 Riverside Housewares: 4 plt $3,264
