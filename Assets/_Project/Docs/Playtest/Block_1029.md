# Playtest block 1029 — days 1–8

## Score: **54.4** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (0 shipped, 0 late) | 15% | 0.0 |
| Fill rate | 10% | 79.9 |
| Throughput (67 plt vs target 84) | 15% | 79.8 |
| Revenue ($51,299) | 15% | 91.6 |
| Profit (cash -483,309) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 100.0 |
| Reliability (bugs) | 25% | 42.9 |
| Reputation (0 to 75) | 5% | 100.0 |

Orders shipped **0**, late/fined **0**, pallets shipped **67**. Cash $500,000 → $16,691 (-483,309). Stock at cost $0 → $56,934 (not part of profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Bugs found (4 distinct, 5 incidents)

| Incidents | Bug |
|---|---|
| 2 | OVERLAP —  A Chep & A Chep @(#, #) |
| 1 | EXCEPTION — MissingReferenceException: The object of type 'UnityEngine.AI.NavMeshAgent' has been destroyed but you are still trying to access it. |
| 1 | FLOATING —  A Chep @(#, #) bottom=# |
| 1 | GHOST-SLOT — RECORD # # x# cell=(#, #) lane= - no object |

## First sightings (with stack traces)

- **OVERLAP** (day 8): OVERLAP A Chep & A Chep @(48, 65)  <<HISTORY: d8 00:00 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d8 00:00 adopted as outbound freight of order 214302c2-e58b-4eba-965b-b365c43f9afc (10 cs) by OutboundPalletBuilder.AdoptFullPallet>>  <<HISTORY: d8 00:00 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d8 00:00 adopted as outbound freight of order 214302c2-e58b-4eba-965b-b365c43f9afc (15 cs) by OutboundPalletBuilder.AdoptFullPallet>>
- **FLOATING** (day 8): FLOATING A Chep @(48, 65) bottom=2.28  <<HISTORY: d8 00:00 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d8 00:00 adopted as outbound freight of order 214302c2-e58b-4eba-965b-b365c43f9afc (15 cs) by OutboundPalletBuilder.AdoptFullPallet>>
- **GHOST-SLOT** (day 8): RECORD bb859a0e 517634 x36 cell=(-1, -1) lane= - no object

## Bot actions (first 400)
