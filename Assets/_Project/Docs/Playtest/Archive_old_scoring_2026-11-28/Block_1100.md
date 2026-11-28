# Playtest block 1100 — days 4–7

## Score: **63.8** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (15 shipped, 2 late) | 10% | 86.7 |
| Fill rate | 10% | 93.5 |
| Throughput (156 plt vs target 36) | 10% | 100.0 |
| Revenue ($378,906) | not scored | 100.0 |
| Net Profit (+191,508) | 10% | 100.0 |
| Cash health (floor $15,000) | 0% | 100.0 |
| Reliability (bugs) | 50% | 31.6 |
| Reputation (231 to 385) | 10% | 100.0 |

Orders shipped **15**, late/fined **2**, pallets shipped **156**. Cash $173,511 → $381,754 (+208,243). Stock at cost $98,693 → $81,958 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went
*A save load zeroed the money ledger during this block, so this covers only the time since the load.*

Income **$128,732**. Expenses **$39,565** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 125,183 |
| Handling | 3,549 |

| Expense | $ |
|---|---|
| Inventory | 28,947 |
| Fines | 7,220 |
| Transportation | 1,123 |
| Wages | 970 |
| Maintenance | 585 |
| Lease / Mortgage | 420 |
| Groundskeeping | 293 |
| Loss Prevention | 7 |

## Late orders - state when it came due
- 1 x loaded but the truck had not left

- day 6: OG7031 (Sprout Scout Foods) was loaded but the truck had not left. Fined $7,220. Lines 2, 1160 cs, released=True.

## Bugs found (13 distinct, 16 incidents)

| Incidents | Bug |
|---|---|
| 4 | VEHICLE-STATIONARY — DS_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 1 | OVERLAP —  A Chep & OutboundPallet_##e#a#fc#a#e#_restored @#B# |
| 1 | FLOATING —  OutboundPallet_##e#a#fc#a#e#_restored @#B# bottom=# |
| 1 | FLOATING —  A Chep @#A# bottom=# |
| 1 | OVERLAP —  A Chep & A Chep @#A# |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##e#a#fc#a#e# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ]  |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Betty Peterson': could not reach the mouth of lane #C (try #/#) - waiting, then trying again. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##c##e##f## (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ]  |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-e###a## (Odd-Shape Orchards) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ]  |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-f#e###f#b-# (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# [###:#(behind) ] # x# [###:#(behind) ] # x# (all held by other selectors / planned elsewhere) [###:# ] # x# (all held by other selectors / planned elsewhere) [###:#(behind) ###:# ]  |
| 1 | WARN — [OrderSelectionTaskDriver] 'Employee_Martha Clark': could not reach the mouth of lane #A (try #/#) - waiting, then trying again. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-f#e###f#b-# (Barnacle Bill's Bait & Bite) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage #. |
| 1 | WARN — [OrderSelectionTaskDriver] Order #-ead###d#-# (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. Still owed (pick faces with stock): # x# [###:#(behind) ] # x# (all held by other selectors / planned elsewhere) [###:#(behind) ###:#(behind) ]  |

## First sightings (with stack traces)

- **FLOATING** (day 6): FLOATING A Chep @1A-1 bottom=1.90  <<HISTORY: d6 10:01 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d6 10:01 adopted as outbound freight of order 3bace2b6-855b-49a5-a184-b126fd21e5b2 (80 cs) by OutboundPalletBuilder.AdoptFullPallet>>
- **OVERLAP** (day 6): OVERLAP A Chep & A Chep @1A-1  <<HISTORY: d6 10:01 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d6 10:01 adopted as outbound freight of order 3bace2b6-855b-49a5-a184-b126fd21e5b2 (80 cs) by OutboundPalletBuilder.AdoptFullPallet>>  <<HISTORY: d6 10:01 PalletPick task Available->Complete by PlacementSystem.ApplySaveData | d6 10:01 adopted as outbound freight of order 3bace2b6-855b-49a5-a184-b126fd21e5b2 (47 cs) by OutboundPalletBuilder.AdoptFullPallet>>
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 178e608b-f3e0-4007-8f5b-d0f853835875 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 488/504. Still owed (pick faces with stock): 517634 x2 [01-16-00:36(behind) ] 615873 x1 [02-01-01:16(behind) ] 178439 x1 [01-08-00:60(behind) ] 856320 x3 (all held by other selectors / planned elsewhere) [02-06-00:40 ] 152431 x9 (all held by other selectors / planned elsewhere) [01-06-00:0(behind) 02-15-00:0 ] 
- **WARN** (day 6): [OrderSelectionTaskDriver] 'Employee_Martha Clark': could not reach the mouth of lane 3A (try 1/4) - waiting, then trying again.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order 178e608b-f3e0-4007-8f5b-d0f853835875 (Barnacle Bill's Bait & Bite) has no reachable pick location for any line item and nothing was picked — returning it to Open and releasing Stage 3.
- **WARN** (day 6): [OrderSelectionTaskDriver] Order edde045d-ead9-4896-96d1-f5bb61b48687 (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at 74/82. Still owed (pick faces with stock): 520894 x2 [01-16-01:40(behind) ] 152431 x6 (all held by other selectors / planned elsewhere) [01-06-00:0(behind) 02-15-00:0(behind) ] 

## Bot actions (first 400)
