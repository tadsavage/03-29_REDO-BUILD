# Bug log - block 74

Every incident as it happened. Format: day hour:minute | category | what.

- day 1 11:13 | **WARN** | [ReceiverReceivingWorkflow] Arrival callback fired for pallet 393e7034-409a-48fe-a5e6-fe9c73ed610f but the employee is 28.4m away (max 4.5m) — refusing to start receiving.
- day 1 16:39 | **WARN** | [OrderService] Order OG4941 closed with a STAGED pallet (24 cs) that never made the trailer - not billed, returning it to stock.
- day 1 17:38 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Janice Morris': could not reach bay 02-06 after 5 tries with nothing picked - leaving the jack and handing the task back.
- day 1 18:07 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Janice Morris': pallet jack could not finish 'bay 02-06' - only a partial route to (-39.13, 1.11, 61.54) (ends 19.1 m short) (route: ).
- day 1 18:12 | **REPEATING** | x25 and counting: [OrderSelectionTaskDriver] 'Employee_Janice Morris': could not drive the jack to bay 02-06 (try 5/5) - waiting, then trying again.
- day 1 18:51 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 225s while 10 task(s) it could take wait
- day 1 19:53 | **WARN** | [OrderSelectionTaskDriver] 'Employee_Kevin McMurray': could not reach the mouth of lane 1A (try 1/4) - waiting, then trying again.
- day 1 20:06 | **VEHICLE-STATIONARY** | PltJack_FULL(Clone) has a driver, hasn't moved in 225s while 10 task(s) it could take wait
- day 1 20:13 | **WARN** | [OrderSelectionTaskDriver] Order ea55f0d7-a148-4af8-b69f-be72a229cc09 (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 52/280. Still owed (pick faces with stock): 
