# Playtest block 1026 — days 1–8

## Score: **19.6** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 6 late) | 15% | 0.0 |
| Fill rate | 10% | 0.0 |
| Throughput (0 plt vs target 84) | 15% | 0.0 |
| Revenue ($0) | 15% | 0.0 |
| Profit (cash -325,604) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 48.5 |
| Reputation (0 to 0) | 5% | 50.0 |

Orders shipped **0**, late/fined **6**, pallets shipped **0**. Cash $500,000 → $174,396 (-325,604). Stock at cost $0 → $0 (not part of profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Late orders - state when it came due
- 6 x never released

- day 2: OG2104 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,735. Lines 19, 466 cs, released=False.
- day 3: OG3105 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,859. Lines 20, 434 cs, released=False.
- day 4: OG4106 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,716. Lines 12, 329 cs, released=False.
- day 5: OG5107 (Sneaky Pete's Seafood) was never released to the floor. Fined $2,128. Lines 23, 465 cs, released=False.
- day 6: OG6108 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,673. Lines 19, 431 cs, released=False.
- day 7: OG7109 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,525. Lines 12, 312 cs, released=False.

## Bugs found (2 distinct, 7 incidents)

| Incidents | Bug |
|---|---|
| 6 | TRUCK-STALLED —  Truck→Outbound_Door# state=DepartToApproach #s at (#, #, #) |
| 1 | REPEATING — x# and counting: [EquipmentBattery] Dockstocker: no path to the charger approach - trying again shortly. |

## First sightings (with stack traces)

- **TRUCK-STALLED** (day 2): TRUCK-STALLED Truck→Outbound_Door1 state=DepartToApproach 300s at (-11.3, 0.0, 27.9)
- **REPEATING** (day 7): x25 and counting: [EquipmentBattery] Dockstocker: no path to the charger approach - trying again shortly.

## Bot actions (first 400)
