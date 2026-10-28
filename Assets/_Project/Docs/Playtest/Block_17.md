# Playtest block 17 — days 1–8

## Score: **29.4 (-5.6)** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (3 shipped, 14 late) | 15% | 0.0 (-12.5) |
| Fill rate | 10% | 63.3 (+20.4) |
| Throughput (29 plt vs target 84) | 15% | 34.5 (-13.1) |
| Revenue ($20,792) | 15% | 37.1 (-10.1) |
| Profit (cash + stock -104,700) | 10% | 0.0 (0.0) |
| Cash health (floor $15,000) | 5% | 84.9 (-12.5) |
| Reliability (bugs) | 25% | 22.4 (-6.7) |
| Reputation (0 to 0) | 5% | 50.0 (0.0) |

Orders shipped **3**, late/fined **14**, pallets shipped **29**. Cash $139,040 → $7,358 (-131,682). Stock at cost $0 → $26,982; cash + stock -104,700.
Bot did: 9 contracts signed, 30 dock bookings, 11 orders released, 25 POs, 0 rush POs.

## Change since last block
- Fixed / no longer seen: **4**
  - ORPHAN-OUTBOUND| #D# (order #-ada##cf-b### gone)
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Daniel Sanders' as its operator though nobody is on it or running it - clearing the stale link.
  - ORPHAN-OUTBOUND| #C# (order ##b#-b#c### gone)
  - WARN|[AiNavigation] ReachTruck: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh).
- New this block: **10**
  - VEHICLE-STATIONARY|Dockstocker has a driver, hasn't moved in #s while # task(s) it could take wait
  - NO-TASK| lane pallet #D# sku=# load=#
  - OVERLAP| Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #)
  - ORPHAN-OUTBOUND| #B# (order ##b#-b#d## gone)
  - REPEATING|x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol.
  - NO-TASK| lane pallet #C# sku=# load=#
  - REPEATING|x# and counting: [NoWaypointIndicator] Alert emote 'emote_exclamations' not found in Resources/Emotes.
  - REPEATING|x# and counting: [OrderSelectionTaskDriver] Order #-df##e-b#aa-# (Sneaky Pete's Seafood) can't reach staging — no lane at Stage # or any nearby door has room for # pallet(s) (every lane full, held by another customer, or an inbound trailer is docked). Waiting with the pallet(s), retrying in #s.
  - VEHICLE-STATIONARY|PalletJack has a driver, hasn't moved in #s while # task(s) it could take wait
  - WARN|[OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jack Torres' as its operator though nobody is on it or running it - clearing the stale link.

## Bugs found (14 distinct, 26 incidents)

| Incidents | Bug |
|---|---|
| 10 | VEHICLE-STATIONARY — Dockstocker has a driver, hasn't moved in #s while # task(s) it could take wait |
| 2 | NO-TASK —  lane pallet #A# sku=# load=# |
| 2 | NO-TASK —  lane pallet #B# sku=# load=# |
| 2 | NO-TASK —  lane pallet #D# sku=# load=# |
| 1 | DOOR-DEADLOCK — OG# is Loading but door #'s truck has waited # min with no one loading it. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @(#, #) |
| 1 | ORPHAN-OUTBOUND —  #B# (order ##b#-b#d## gone) |
| 1 | REPEATING — x# and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (#, #, #) (no waypoint reachable for #s) — warping to (#, #, #) to rejoin patrol. |
| 1 | NO-TASK —  lane pallet #C# sku=# load=# |
| 1 | REPEATING — x# and counting: [NoWaypointIndicator] Alert emote 'emote_exclamations' not found in Resources/Emotes. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Order #-df##e-b#aa-# (Sneaky Pete's Seafood) can't reach staging — no lane at Stage # or any nearby door has room for # pallet(s) (every lane full, held by another customer, or an inbound trailer is docked). Waiting with the pallet(s), retrying in #s. |
| 1 | VEHICLE-STATIONARY — PalletJack has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jack Torres' as its operator though nobody is on it or running it - clearing the stale link. |

## First sightings (with stack traces)

- **NO-TASK** (day 2): NO-TASK lane pallet 2A-3 sku=564219 load=1004543544
- **NO-TASK** (day 2): NO-TASK lane pallet 2B-4 sku=564219 load=9920931872
- **WARN** (day 3): [OrderService] Order OG3513 closed with a STAGED pallet (35 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 3): ORPHAN-OUTBOUND 1B-1 (order 286f1198-293b-4765-b20d-7a98d21e0616 gone)
- **REPEATING** (day 3): x25 and counting: [AiNavigation] ReachTruck: stranded on an isolated NavMesh island at (-29.50, 1.11, 36.89) (no waypoint reachable for 8s) — warping to (1.94, 0.01, 39.04) to rejoin patrol.
- **NO-TASK** (day 4): NO-TASK lane pallet 2D-2 sku=193587 load=8720845842
- **NO-TASK** (day 4): NO-TASK lane pallet 2C-2 sku=792146 load=5039816913
- **VEHICLE-STATIONARY** (day 4): Dockstocker has a driver, hasn't moved in 255s while 6 task(s) it could take wait
- **REPEATING** (day 4): x25 and counting: [NoWaypointIndicator] Alert emote 'emote_exclamations' not found in Resources/Emotes.
- **OVERLAP** (day 6): OVERLAP Pallet_05_Slot2_Tier1 & Pallet_00_Slot0_Tier0 @(37, 67)
- **DOOR-DEADLOCK** (day 6): OG4514 is Loading but door 2's truck has waited 90 min with no one loading it.
- **REPEATING** (day 6): x25 and counting: [OrderSelectionTaskDriver] Order 88f2e1c1-df86-444e-b1aa-e655b5af580f (Sneaky Pete's Seafood) can't reach staging — no lane at Stage 1 or any nearby door has room for 0 pallet(s) (every lane full, held by another customer, or an inbound trailer is docked). Waiting with the pallet(s), retrying in 15s.
- **VEHICLE-STATIONARY** (day 7): PalletJack has a driver, hasn't moved in 1680s while 10 task(s) it could take wait
- **WARN** (day 7): [OrderSelectionTaskDriver] Pallet jack still listed 'Employee_Jack Torres' as its operator though nobody is on it or running it - clearing the stale link.

## Bot actions (first 400)
- d1 signed recurring Contract_Starter
- d1 PO IG1740 Anchor Point Foods: 3 plt $1,848
- d1 placed parked Inbound Anchor Point Foods day 1 block 5 door 1
- d1 PO IG1741 Homestead Market Co.: 2 plt $1,134
- d1 placed parked Inbound Homestead Market Co. day 1 block 5 door 2
- d1 PO IG1742 Homestead Market Co.: 1 plt $672
- d1 placed parked Inbound Homestead Market Co. day 1 block 6 door 1
- d1 PO IG1743 Backdoor Bill's Overstock: 2 plt $512
- d1 placed parked Inbound Backdoor Bill's Overstock day 1 block 6 door 2
- d1 PO IG1744 Bulk Basin Foods: 3 plt $1,980
- d1 placed parked Inbound Bulk Basin Foods day 1 block 7 door 1
- d1 PO IG1745 Ambrose Fine Foods: 7 plt $13,008
- d1 signed bulk BULK_Crunch-Time Cereals_D1_0
- d1 signed bulk BULK_Chubby Chipmunk Cookies_D1_1
- d1 placed parked Inbound Ambrose Fine Foods day 1 block 7 door 2
- d1 PO IG1746 Fairweather Trading Co.: 12 plt $7,257
- d1 placed parked Inbound Fairweather Trading Co. day 1 block 8 door 1
- d1 PO IG1747 Bulk Basin Foods: 2 plt $1,024
- d1 placed parked Inbound Bulk Basin Foods day 1 block 8 door 2
- d2 PO IG2748 Fairweather Trading Co.: 9 plt $10,113
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 1 door 1
- d2 PO IG2749 Backdoor Bill's Overstock: 2 plt $2,800
- d2 placed parked Inbound Backdoor Bill's Overstock day 2 block 1 door 2
- d2 booked Chubby Chipmunk Cookies day 2 block 6 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 31 + 3.3h of work)
- d2 PO IG2750 Fairweather Trading Co.: 11 plt $8,208
- d2 placed parked Inbound Fairweather Trading Co. day 2 block 2 door 1
- d2 released 2 order(s)
- d2 signed bulk BULK_Neon Nectar Juice Co._D2_0
- d3 PO IG3751 Homestead Market Co.: 6 plt $11,040
- d3 placed parked Inbound Homestead Market Co. day 3 block 1 door 1
- d3 PO IG3752 Anchor Point Foods: 5 plt $2,860
- d3 PO IG3753 Backdoor Bill's Overstock: 2 plt $2,800
- d3 placed parked Inbound Anchor Point Foods day 3 block 1 door 2
- d3 placed parked Inbound Backdoor Bill's Overstock day 3 block 2 door 1
- d3 released 1 order(s)
- d3 signed bulk BULK_Puff & Stuff Pastries_D3_0
- d3 signed bulk BULK_The Happy Herd Co._D3_1
- d3 PO IG3754 Bulk Basin Foods: 5 plt $3,360
- d3 placed parked Inbound Bulk Basin Foods day 3 block 11 door 1
- d4 PO IG4755 Bulk Basin Foods: 14 plt $7,504
- d4 placed parked Inbound Bulk Basin Foods day 4 block 1 door 1
- d4 PO IG4756 Bulk Basin Foods: 5 plt $3,202
- d4 placed parked Inbound Bulk Basin Foods day 4 block 1 door 2
- d4 booked Neon Nectar Juice Co. day 4 block 6 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 79 + 3.3h of work)
- d4 booked The Happy Herd Co. day 4 block 5 door 1 (pallet-only: stock ready hour 79 + 1.5h of work)
- d4 PO IG4757 Homestead Market Co.: 4 plt $4,546
- d4 placed parked Inbound Homestead Market Co. day 4 block 2 door 1
- d4 booked Crunch-Time Cereals day 4 block 6 door 2 (pallet-only: stock ready hour 81 + 1.5h of work)
- d4 released 3 order(s) (waiting for lane)
- d4 released 1 order(s)
- d4 signed bulk BULK_The Big Cheese Inc._D4_0
- d4 signed bulk BULK_Honest Harvest Rice_D4_1
- d5 PO IG5758 Homestead Market Co.: 2 plt $1,680
- d5 placed parked Inbound Homestead Market Co. day 5 block 1 door 1
- d5 PO IG5759 Backdoor Bill's Overstock: 1 plt $256
- d5 placed parked Inbound Backdoor Bill's Overstock day 5 block 1 door 2
- d5 PO IG5760 Bulk Basin Foods: 2 plt $800
- d5 placed parked Inbound Bulk Basin Foods day 5 block 2 door 1
- d5 booked Honest Harvest Rice day 5 block 7 door 1 (case-pick, 1 picklist(s) / 2 selector(s): stock ready hour 105 + 3.3h of work)
- d5 PO IG5761 Fairweather Trading Co.: 6 plt $6,075
- d5 placed parked Inbound Fairweather Trading Co. day 5 block 2 door 2
- d5 released 1 order(s)
- d5 released 1 order(s)
- d5 signed bulk BULK_The Happy Herd Co._D5_0
- d6 PO IG6762 Homestead Market Co.: 1 plt $880
- d6 placed parked Inbound Homestead Market Co. day 6 block 1 door 1
- d6 PO IG6763 Anchor Point Foods: 2 plt $800
- d6 placed parked Inbound Anchor Point Foods day 6 block 1 door 2
- d6 PO IG6764 Bulk Basin Foods: 9 plt $2,512
- d6 placed parked Inbound Bulk Basin Foods day 6 block 2 door 1
- d6 released 1 order(s) (waiting for lane)
- d7 released 1 order(s)
