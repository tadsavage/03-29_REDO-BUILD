# Playtest block 1002 — days 1–8

## Score: **34.4** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (5 shipped, 14 late) | 15% | 12.5 |
| Fill rate | 10% | 43.0 |
| Throughput (40 plt vs target 84) | 15% | 47.6 |
| Revenue ($32,481) | 15% | 58.0 |
| Profit (cash + stock -43,706) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 97.3 |
| Reliability (bugs) | 25% | 27.1 |
| Reputation (21 to 0) | 5% | 15.0 |

Orders shipped **5**, late/fined **14**, pallets shipped **40**. Cash $82,168 → $13,450 (-68,718). Stock at cost $47,249 → $72,261; cash + stock -43,706.
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Bugs found (9 distinct, 17 incidents)

| Incidents | Bug |
|---|---|
| 2 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 3 | NO-TASK —  lane pallet #B# sku=# load=# |
| 3 | NO-TASK —  lane pallet #A# sku=# load=# |
| 2 | ORPHAN-OUTBOUND —  #D# (order #-ada##cf-b### gone) |
| 1 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 2 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Daniel Sanders' as its operator though nobody is on it or running it - clearing the stale link. |
| 1 | ORPHAN-OUTBOUND —  #C# (order ##b#-b#c### gone) |
| 2 | WARN — [AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **NO-TASK** (day 1): NO-TASK lane pallet 1B-5 sku=113489 load=0245169499
- **NO-TASK** (day 1): NO-TASK lane pallet 1A-1 sku=113489 load=5660366934
- **WARN** (day 1): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Daniel Sanders' as its operator though nobody is on it or running it - clearing the stale link.
- **WARN** (day 2): [OrderService] Order OG1496 closed with a STAGED pallet (34 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 2): ORPHAN-OUTBOUND 1D-1 (order 7e94fd20-ada5-42cf-b294-4134ddbdd87c gone)
- **TRUCK-STALLED** (day 2): TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 540s at (7.2, 0.0, 6.7)
- **ORPHAN-OUTBOUND** (day 2): ORPHAN-OUTBOUND 1C-1 (order b5946e3f-220b-4129-b6c4-6f2bc3d6e1bd gone)
- **WARN** (day 2): [AiNavigation] ReachTruck: stuck at (5.69, 1.16, 20.76) — walking out to (1.22, 0.08, 12.28) (8.65m to mesh).
- **DOOR-DEADLOCK** (day 4): OG4499 is Loading but door 1's truck has waited 90 min with no one loading it.

## Bot actions (first 400)
