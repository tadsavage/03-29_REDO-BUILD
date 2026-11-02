# Playtest block 1023 — days 1–8

## Score: **11.0** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (5 shipped, 11 late) | 15% | 8.3 |
| Fill rate | 10% | 0.0 |
| Throughput (0 plt vs target 84) | 15% | 0.0 |
| Revenue ($0) | 15% | 0.0 |
| Profit (cash -325,604) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 8.9 |
| Reputation (0 to 0) | 5% | 50.0 |

Orders shipped **5**, late/fined **11**, pallets shipped **0**. Cash $500,000 → $174,396 (-325,604). Stock at cost $0 → $0 (not part of profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Late orders - state when it came due
- 6 x never released

- day 2: OG2104 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,735. Lines 19, 466 cs, released=False.
- day 3: OG3105 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,859. Lines 20, 434 cs, released=False.
- day 4: OG4106 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,716. Lines 12, 329 cs, released=False.
- day 5: OG5107 (Sneaky Pete's Seafood) was never released to the floor. Fined $2,128. Lines 23, 465 cs, released=False.
- day 6: OG6108 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,673. Lines 19, 431 cs, released=False.
- day 7: OG7109 (Sneaky Pete's Seafood) was never released to the floor. Fined $1,525. Lines 12, 312 cs, released=False.

## Bugs found (23 distinct, 97 incidents)

| Incidents | Bug |
|---|---|
| 49 | OVERLAP —  A Chep & A Chep @(#, #) |
| 8 | OVERLAP —  A Chep & A Chep @#A# |
| 5 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 4 | OVERLAP —  OutboundPallet_#-b#f##-b###_# & OutboundPallet_#-b#f##-b###_# @#B# |
| 4 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 4 | OVERLAP —  A Chep & A Chep @#C# |
| 4 | OVERLAP —  A Chep & A Chep @#B# |
| 3 | FLOATING —  OutboundPallet_#-b#f##-b###_# @#B# bottom=# |
| 2 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_#-b#f##-b###_# @#B# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 1 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_###d##-#_# @#A# |
| 1 | OVERLAP —  OutboundPallet_#-b#fa#f#-bf#d#dcf#b_# & OutboundPallet_#-b#fa#f#-bf#d#dcf#b_# @#A# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##cb##-b###_# @#A# |
| 1 | FLOATING —  OutboundPallet_##cb##-b###_# @#A# bottom=# |
| 1 | GHOST-SLOT — RECORD # # x# cell=(#, #) lane= - no object |
| 1 | REPEATING — x# and counting: Internal: JobTempAlloc has allocations that are more than the maximum lifespan of # frames old - this is not allowed and likely a leak |
| 1 | REPEATING — x# and counting: To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations. |
| 1 | NO-TASK —  lane pallet #B# sku=# load=# |
| 1 | REPEATING — x# and counting: [EquipmentBattery] Dockstocker: no path to the charger approach - trying again shortly. |
| 1 | WARN — [ReachTruckOperator] Replenish: could not reach pallet at ##-A# within #m. |
| 1 | WARN — [InventoryService] Lane #B lock was held for over #s - treating it as leaked and taking it over. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **REPEATING** (day 7): x25 and counting: [EquipmentBattery] Dockstocker: no path to the charger approach - trying again shortly.

## Bot actions (first 400)
