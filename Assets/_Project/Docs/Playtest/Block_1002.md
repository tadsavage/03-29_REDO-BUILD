# Playtest block 1002 — days 1–8

## Score: **39.3** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (5 shipped, 11 late) | 15% | 8.3 |
| Fill rate | 10% | 67.7 |
| Throughput (57 plt vs target 84) | 15% | 67.9 |
| Revenue ($40,861) | 15% | 73.0 |
| Profit (cash + stock -106,181) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 82.3 |
| Reliability (bugs) | 25% | 21.1 |
| Reputation (21 to 0) | 5% | 15.0 |

Orders shipped **5**, late/fined **11**, pallets shipped **57**. Cash $82,168 → $8,550 (-73,618). Stock at cost $47,249 → $14,686; cash + stock -106,181.
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Bugs found (11 distinct, 29 incidents)

| Incidents | Bug |
|---|---|
| 14 | VEHICLE-STATIONARY — Dockstocker has a driver, hasn't moved in #s while # task(s) wait |
| 3 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 2 | NO-TASK —  lane pallet #C# sku=# load=# |
| 2 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 1 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it. |
| 2 | NO-TASK —  lane pallet #B# sku=# load=# |
| 1 | REPEATING — x# and counting: Parameter 'IsWaving' does not exist. |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | REPEATING — x# and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | ORPHAN-OUTBOUND —  #B# (order #-a#c#fe##a### gone) |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **REPEATING** (day 1): x25 and counting: Parameter 'IsWaving' does not exist.
- **NO-TASK** (day 2): NO-TASK lane pallet 1C-5 sku=659083 load=8273443677
- **TRUCK-OVERLAP** (day 3): TRUCK-OVERLAP Truck→PO_IG2619 & Truck→PO_IG2618 at (-7.4, 0.0, -18.5)
- **DOOR-DEADLOCK** (day 3): OG2443 is Staged but door 1's truck has waited 90 min with no one loading it.
- **REPEATING** (day 3): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-23.71, 1.11, 34.16) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **REPEATING** (day 4): x25 and counting: [AiNavigation] Dockstocker: stranded on an isolated NavMesh island at (-8.61, 1.11, 40.10) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **DOOR-DEADLOCK** (day 4): OG3444 is Loading but door 1's truck has waited 90 min with no one loading it.
- **WARN** (day 4): [OrderService] Order OG3444 closed with a STAGED pallet (26 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 4): ORPHAN-OUTBOUND 1B-1 (order 78321068-a89c-4fe6-98a0-322d23810771 gone)
- **NO-TASK** (day 5): NO-TASK lane pallet 1B-5 sku=482913 load=3229705121
- **VEHICLE-STATIONARY** (day 5): Dockstocker has a driver, hasn't moved in 255s while 10 task(s) wait

## Bot actions (first 400)
