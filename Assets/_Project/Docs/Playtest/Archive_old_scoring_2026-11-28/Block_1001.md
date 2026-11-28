# Playtest block 1001 — days 1–8

## Score: **55.1** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (15 shipped, 13 late) | 15% | 53.6 |
| Fill rate | 10% | 61.0 |
| Throughput (127 plt vs target 84) | 15% | 100.0 |
| Revenue ($115,886) | 15% | 100.0 |
| Profit (cash + stock -19,402) | 10% | 0.0 |
| Cash health (floor $15,000) | 5% | 44.9 |
| Reliability (bugs) | 25% | 15.2 |
| Reputation (20 to 49) | 5% | 98.3 |

Orders shipped **15**, late/fined **13**, pallets shipped **127**. Cash $109,149 → $-31,468 (-140,617). Stock at cost $0 → $121,215; cash + stock -19,402.
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Bugs found (22 distinct, 43 incidents)

| Incidents | Bug |
|---|---|
| 6 | NO-TASK —  lane pallet #A# sku=# load=# |
| 5 | NO-TASK —  lane pallet #B# sku=# load=# |
| 4 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) wait |
| 2 | NO-TASK —  lane pallet #C# sku=# load=# |
| 2 | REPEATING — x# and counting: [ReceiverReceivingWorkflow] Receiving animation stopped, restarting loop (logged once per # s) |
| 2 | ORPHAN-OUTBOUND —  #C# (order ##c##a#-bec#-# gone) |
| 3 | WARN — [AiNavigation] Employee_Alexander Williams: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] Employee_James Martinez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | TRUCK-STALLED —  Truck→PO_IG# state=DepartToApproach #s at (#, #, #) |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop. |
| 1 | OVERLAP —  A Chep & A Chep @(#, #) |
| 2 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→PO_IG# at (#, #, #) |
| 1 | REPEATING — x# and counting: [OrderArrivalService] Signed contract 'BULK_The Daily Grind Tofu_D#_#' has no matching asset in the catalog — skipping. (Asset renamed or removed?) |
| 1 | REPEATING — x# and counting: [OrderArrivalService] Signed contract 'BULK_Puff & Stuff Pastries_D#_#' has no matching asset in the catalog — skipping. (Asset renamed or removed?) |
| 1 | REPEATING — x# and counting: [OrderArrivalService] Signed contract 'BULK_Pony Express Oats_D#_#' has no matching asset in the catalog — skipping. (Asset renamed or removed?) |
| 1 | REPEATING — x# and counting: [OrderArrivalService] Signed contract 'BULK_Barnacle Bill's Bait & Bite_D#_#' has no matching asset in the catalog — skipping. (Asset renamed or removed?) |
| 1 | REPEATING — x# and counting: [OrderArrivalService] Signed contract 'BULK_Salty Sailor Pretzel Co._D#_#' has no matching asset in the catalog — skipping. (Asset renamed or removed?) |
| 2 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | ORPHAN-OUTBOUND —  #B# (order ##c##b#f#-# gone) |
| 1 | REPEATING — x# and counting: [ReachTruckOperator] 'RT_Full(Clone)' found no Available Putaway/Replenish tasks this poll. |
| 1 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |

## First sightings (with stack traces)

- **TRUCK-OVERLAP** (day 4): TRUCK-OVERLAP Truck→PO_IG4314 & Truck→PO_IG4313 at (-6.3, 0.0, 7.9)
- **REPEATING** (day 5): x25 and counting: [OrderArrivalService] Signed contract 'BULK_The Daily Grind Tofu_D1_1' has no matching asset in the catalog — skipping. (Asset renamed or removed?)
- **REPEATING** (day 5): x25 and counting: [OrderArrivalService] Signed contract 'BULK_Puff & Stuff Pastries_D1_2' has no matching asset in the catalog — skipping. (Asset renamed or removed?)
- **REPEATING** (day 5): x25 and counting: [OrderArrivalService] Signed contract 'BULK_Pony Express Oats_D1_0' has no matching asset in the catalog — skipping. (Asset renamed or removed?)
- **REPEATING** (day 5): x25 and counting: [OrderArrivalService] Signed contract 'BULK_Barnacle Bill's Bait & Bite_D1_0' has no matching asset in the catalog — skipping. (Asset renamed or removed?)
- **REPEATING** (day 5): x25 and counting: [OrderArrivalService] Signed contract 'BULK_Salty Sailor Pretzel Co._D1_1' has no matching asset in the catalog — skipping. (Asset renamed or removed?)
- **ERROR** (day 5): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'bay 01-07' (3.2m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__92:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1112)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **OVERLAP** (day 5): OVERLAP A Chep & A Chep @(32, 75)
- **WARN** (day 6): [OrderService] Order OG7241 closed with a STAGED pallet (32 cs) that never made the trailer - not billed, returning it to stock.
- **WARN** (day 7): [OrderService] Order OG7242 closed with a IN TRANSIT pallet (43 cs) that never made the trailer - not billed, returning it to stock.
- **ORPHAN-OUTBOUND** (day 7): ORPHAN-OUTBOUND 1B-1 (order 3510936c-61c0-404b-9f54-fe970c9b90a4 gone)
- **ORPHAN-OUTBOUND** (day 7): ORPHAN-OUTBOUND 1C-1 (order d4550478-2c82-48a6-bec6-fb8f6ad82046 gone)
- **REPEATING** (day 7): x25 and counting: [ReachTruckOperator] 'RT_Full(Clone)' found no Available Putaway/Replenish tasks this poll.

## Bot actions (first 400)
