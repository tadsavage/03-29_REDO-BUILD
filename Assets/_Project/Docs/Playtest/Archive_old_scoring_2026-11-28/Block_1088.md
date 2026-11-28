# Playtest block 1088 — days 1–8

## Score: **46.9** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (14 shipped, 8 late) | 10% | 55.6 |
| Fill rate | 10% | 92.4 |
| Throughput (138 plt vs target 84) | 10% | 100.0 |
| Revenue ($271,369) | not scored | 100.0 |
| Net Profit (+67,467) | 10% | 100.0 |
| Cash health (floor $15,000) | 0% | 100.0 |
| Reliability (bugs) | 50% | 4.2 |
| Reputation (0 to 194) | 10% | 100.0 |

Orders shipped **14**, late/fined **8**, pallets shipped **138**. Cash $500,000 → $187,941 (-312,059). Stock at cost $0 → $63,968 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went
*A save load zeroed the money ledger during this block, so this covers only the time since the load.*

Income **$0**. Expenses **$7,950** (stock bought shows as Inventory). Loan principal repaid **$5,000** (debt, not an expense).

| Income | $ |
|---|---|

| Expense | $ |
|---|---|
| Lease / Mortgage | 7,868 |
| Loan Principal | 5,000 |
| Maintenance | 49 |
| Groundskeeping | 20 |
| Transportation | 12 |
| Loss Prevention | 1 |

## Bugs found (69 distinct, 220 incidents)

| Incidents | Bug |
|---|---|
| 10 | FLOATING —  Pallet_#_Slot#_Tier# @#D# bottom=# |
| 10 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#D# |
| 8 | FLOATING —  Pallet_#_Slot#_Tier# @#A# bottom=# |
| 8 | FLOATING —  Pallet_#_Slot#_Tier# @#B# bottom=# |
| 7 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#A# |
| 7 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#B# |
| 6 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#E# |
| 6 | FLOATING —  Pallet_#_Slot#_Tier# @#E# bottom=# |
| 6 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'bay ##' (#m short) - giving up on this stop. |
| 16 | WARN — [AiNavigation] RT_Full(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 15 | WARN — [AiNavigation] Employee_Lauren Jones: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | OVERLAP —  Pallet_#_Slot#_Tier# & Pallet_#_Slot#_Tier# @#C# |
| 4 | FLOATING —  Pallet_#_Slot#_Tier# @#C# bottom=# |
| 4 | ERROR — [OrderSelectionTaskDriver] Pallet jack did not reach 'bay ##' ((#, #, #)) after #s. |
| 11 | WARN — [AiNavigation] Employee_Edward Graham: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 9 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | OVERLAP —  OutboundPallet_##d##e###bd#_# & OutboundPallet_##d##e###bd#_# @#B# |
| 3 | FLOATING —  OutboundPallet_##d##e###bd#_# @#B# bottom=# |
| 9 | WARN — [AiNavigation] Employee_Kathleen Gutierrez: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 2 | DOOR-DEADLOCK — OG# is Staged but door #'s truck has waited # min with no one loading it.  —  lane #A: [#: own/Staged/y#, own/Staged/y#] [#: own/Staged/y#]  |
| 5 | WARN — [AiNavigation] PltJack_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 5 | WARN — [AiNavigation] Employee_Jose King: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [OrderSelectionTaskDriver] Order ##b#e#a#-b#ff## (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 4 | WARN — [AiNavigation] Employee_Brandon Reyes: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 4 | WARN — [OrderSelectionTaskDriver] Order ##a##df#-b#-# (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 3 | WARN — [AiNavigation] RT_Full(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 3 | WARN — [AiNavigation] Employee_Judith Dixon: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##d#a#be##a###_# @#A# |
| 1 | OVERLAP —  OutboundPallet_##d#a#be##a###_# & OutboundPallet_##d#a#be##a###_# @#A# |
| 1 | FLOATING —  OutboundPallet_##d#a#be##a###_# @#A# bottom=# |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##df##c##b###_# @#C# |
| 1 | FLOATING —  OutboundPallet_##df##c##b###_# @#C# bottom=# |
| 1 | FLOATING —  A Chep @#A# bottom=# |
| 1 | OVERLAP —  A Chep & A Chep @#A# |
| 1 | ERROR — [TrailerLoad] 'PltJack_FULL(Clone)' cross-door load: to lane #C: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s). |
| 1 | ERROR — [TrailerLoad] Could not reach lane #C — leaving OutboundPallet_##df##c##b###_restored staged. |
| 1 | OVERLAP —  Pallet_#_Slot#_Tier# & OutboundPallet_##d##bd#ff###_# @#B# |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #A line-up' (#m short) - giving up on this stop. |
| 1 | ERROR — [ReachTruckOperator] 'RT_Full(Clone)' deliver: → location ##-A# anchor: NO NAVMESH PATH. anchor=(#, #, #) → driveTarget=(#, #, #), from=(#, #, #), elapsed=#s, timedOut=True, agentOnMeshAtEnd=True, navEnabled=True, seekingTask=True, pathStatus=PathComplete, hasPath=True, remaining=#, reissues=#, batteryNeedsCharge=False, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago. Refusing to straight-line through obstacles — aborting this leg. |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Order OG# pallet # of #: every pick face is empty - handing the task back until replenishment stocks them. |
| 2 | WARN — [AiNavigation] DS_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | TRUCK-OVERLAP —  Truck→PO_IG# & Truck→Outbound_Door# at (#, #, #) |
| 1 | REPEATING — x# and counting: [ReceivingTaskDriver] Employee_Lauren Jones: receive workflow cancelled (out of range) — releasing the task and resuming patrol. |
| 1 | ORPHAN-OUTBOUND —  #C# (order ##df##c##b### gone) |
| 2 | WARN — [OrderSelectionTaskDriver] Order ##d##bd#ff### (Rooted Robotics) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ORPHAN-OUTBOUND —  #B# (order ##d##bd#ff### gone) |
| 1 | REPEATING — x# and counting: [OrderSelectionTaskDriver] Pallet jack stopped #m short of 'bay ##' - re-issuing the drive (#/#). |
| 1 | WARN — To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-c#a#f-b#e#d# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-a#b##e#a### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] PltJack_FULL(Clone) cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##d#a#be##a### (Happy Cow Creamery) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [AiNavigation] Employee_Edward Graham cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-ec#b#### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-b#d##d### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##f##d#-afdd#b#bc but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ad##bed-bd### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##a##abd-a### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##ce#ca##fd-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ####d### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-ca##d#d#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] Employee_Brandon Reyes cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderService] Order OG# closed with a IN TRANSIT pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###c#-a#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderService] Order OG# closed with a STAGED pallet (# cs) that never made the trailer - not billed, returning it to stock. |
| 1 | WARN — [AiNavigation] Employee_Lauren Jones cannot reach task position at (#, #, #) (path invalid) — abandoning the walk and handing the task back. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##e##e-b### (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##a##df#-b#-# (Barnacle Bill's Bait & Bite) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [AiNavigation] Employee_Brandon Reyes: task target (#, #, #) is not reachable — the partial path ends #m short of it. Handing the task back. |

## First sightings (with stack traces)

_none_

## Bot actions (first 400)
