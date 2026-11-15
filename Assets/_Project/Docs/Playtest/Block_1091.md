# Playtest block 1091 — days 1–3

## Score: **31.7** / 100

| Component | Weight | This block |
|---|---|---|
| On-time delivery (1 shipped, 2 late) | 10% | 0.0 |
| Fill rate | 10% | 93.4 |
| Throughput (16 plt vs target 24) | 10% | 66.7 |
| Revenue ($19,575) | not scored | 100.0 |
| Net Profit (-7,919) | 10% | 0.0 |
| Cash health (floor $15,000) | 0% | 100.0 |
| Reliability (bugs) | 50% | 11.5 |
| Reputation (0 to 58) | 10% | 100.0 |

Orders shipped **1**, late/fined **2**, pallets shipped **16**. Cash $500,000 → $118,489 (-381,511). Stock at cost $0 → $47,091 (offsets stock bought in Net Profit).
Bot did: 0 contracts signed, 0 dock bookings, 0 orders released, 0 POs, 0 rush POs.

## Where the money went
*A save load zeroed the money ledger during this block, so this covers only the time since the load.*

Income **$19,575**. Expenses **$11,311** (stock bought shows as Inventory). Loan principal repaid **$0** (debt, not an expense).

| Income | $ |
|---|---|
| Case Pick | 19,575 |

| Expense | $ |
|---|---|
| Fines | 5,288 |
| Wages | 2,835 |
| Maintenance | 1,608 |
| Lease / Mortgage | 784 |
| Groundskeeping | 776 |
| Loss Prevention | 20 |

## Late orders - state when it came due
- 1 x still being picked
- 1 x loaded but the truck had not left

- day 2: OG4790 (Rooted Robotics) was still being picked (680/728 cs). Fined $4,232. Lines 3, 728 cs, released=True.
- day 2: OG2624 (Sneaky Pete's Seafood) was loaded but the truck had not left. Fined $1,056. Lines 27, 248 cs, released=True.

## Bugs found (29 distinct, 73 incidents)

| Incidents | Bug |
|---|---|
| 18 | VEHICLE-STATIONARY — RT_Full(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 12 | VEHICLE-STATIONARY — PltJack_FULL(Clone) has a driver, hasn't moved in #s while # task(s) it could take wait |
| 3 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to trailer: NO NAVMESH PATH to (#, #, #) from (#, #, #) (elapsed #s). |
| 3 | ERROR — [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot. |
| 2 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' dig: → source lane #C: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\#. FSM\Core\Labor\TrailerLoadController.DigOut.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < at UnityEngine.MonoBehaviour.StartCoroutineManaged#_Injected (System.IntPtr , System.Collections.IEnumerator ) [#x#] in <#>:# < ] #s ago] |
| 2 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' dig: → source lane #A: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__#.MoveNext () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\#. FSM\Core\Labor\TrailerLoadController.DigOut.cs:# < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [#x#] in <#>:# < at UnityEngine.MonoBehaviour.StartCoroutineManaged#_Injected (System.IntPtr , System.Collections.IEnumerator ) [#x#] in <#>:# < ] #s ago] |
| 2 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' dig: → source lane #C: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago] |
| 4 | WARN — [AiNavigation] Employee_Shirley Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | NO-RECORD —  lane pallet #B# A Chep |
| 3 | WARN — [AiNavigation] Employee_Douglas Garcia: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [AiNavigation] Employee_Mary Young: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 3 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/#. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #B wait' (#m short) - giving up on this stop. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'staging lane #B line-up' (#m short) - giving up on this stop. |
| 1 | ERROR — [OrderSelectionTaskDriver] Pallet jack never got within #m of 'pallet jack parking' (#m short) - giving up on this stop. |
| 1 | ERROR — [TrailerLoad] 'DS_FULL(Clone)' dig: → source lane #A: never arrived at (#, #, #) (elapsed #s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=#, reissues=#, lastSeekCancelledBy=[at AiNavigation.Update () [#x#] in C:\Users\MURILLO\Documents\GitHub\### REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:# < ] #s ago] |
| 1 | REPEATING — x# and counting: [EquipmentTraffic] 'RT_Full(Clone)' is blocked at (#, #, #): blocked by RT_Full(Clone) for #s with no safe way round. No safe way to move - staying put. |
| 1 | WARN — [AiNavigation] Employee_Charles Edwards: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Larry Morgan: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [AiNavigation] Employee_Mark Peterson: stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##d##a#### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##fc##e#d-a#a### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##fa#bce-ae#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [OrderSelectionTaskDriver] Order ##bc#ec##d-# (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at #/# case(s). |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###a#### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ##f#a-ae### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet ###ac#d### but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [ReceiverReceivingWorkflow] Arrival callback fired for pallet #-b#e###dc#-# but the employee is #m away (max #m) — refusing to start receiving. |
| 1 | WARN — [AiNavigation] DS_FULL(Clone): stuck at (#, #, #) — walking out to (#, #, #) (#m to mesh). |

## First sightings (with stack traces)

- **WARN** (day 1): [AiNavigation] Employee_Larry Morgan: stuck at (4.28, 1.17, 49.60) — walking out to (4.28, 1.11, 36.89) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Douglas Garcia: stuck at (1.83, 1.16, 58.61) — walking out to (12.92, 1.12, 52.11) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mark Peterson: stuck at (-4.08, 1.14, 58.20) — walking out to (-5.53, 1.22, 45.21) (12.08m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Mary Young: stuck at (1.62, 1.16, 58.25) — walking out to (-5.42, 1.24, 46.99) (12.00m to mesh).
- **WARN** (day 1): [AiNavigation] Employee_Shirley Edwards: stuck at (1.62, 1.16, 58.25) — walking out to (-5.42, 1.24, 46.99) (12.00m to mesh).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 60a769fa-22d5-42a2-9146-52e7d7adfef1 but the employee is 6.0m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 72742ccb-3fc6-4e8d-a8a0-3c8de430e2b2 but the employee is 5.8m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 6/248.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet add37490-65fa-4bce-ae80-bd8ab7c1ea46 but the employee is 19.5m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [OrderSelectionTaskDriver] Order a99ac8a2-18bc-4ec5-861d-a92b572e82ae (Sneaky Pete's Seafood) has no reachable pick location for its remaining line item(s) — staging short at 31/248 case(s).
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet d11814be-5251-47a3-8553-5fb337ff4cf5 but the employee is 4.6m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet defcfae5-352f-444a-ae73-995fcc4544e2 but the employee is 12.8m away (max 4.5m) — refusing to start receiving.
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet 83d25e08-4942-46ac-8d65-42e17a9df157 but the employee is 4.9m away (max 4.5m) — refusing to start receiving.
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B wait' (7.6m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 1): RT_Full(Clone) has a driver, hasn't moved in 360s while 38 task(s) it could take wait
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'staging lane 1B line-up' (5.1m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **VEHICLE-STATIONARY** (day 1): PltJack_FULL(Clone) has a driver, hasn't moved in 360s while 3 task(s) it could take wait
- **WARN** (day 1): [ReceiverReceivingWorkflow] Arrival callback fired for pallet ab3eb9d7-b4e6-4576-9dc1-bbb16c641253 but the employee is 6.4m away (max 4.5m) — refusing to start receiving.
- **ERROR** (day 1): [OrderSelectionTaskDriver] Pallet jack never got within 3m of 'pallet jack parking' (5.5m short) - giving up on this stop.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Actors.OrderSelectionTaskDriver/<DriveJackTo>d__227:MoveNext () (at Assets/_Project/Scripts/Actors/OrderSelectionTaskDriver.PalletJack.cs:1810)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' cross-door load: to trailer: NO NAVMESH PATH to (7.99, 1.18, 38.89) from (6.18, 1.11, 39.63) (elapsed 30.0s).
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__103:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:1222)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] Cross-door NavMesh leg to the trailer failed with a pallet on the forks — falling back to a straight slide to the shipping-door pivot.
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<PlaceCarriedInTrailer>d__97:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.cs:981)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' dig: → source lane 2C: never arrived at (-2.04, 1.19, 39.19) (elapsed 30.0s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=8.8, reissues=0, lastSeekCancelledBy=[at GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__32.MoveNext () [0x00249] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\1. FSM\Core\Labor\TrailerLoadController.DigOut.cs:701 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < at UnityEngine.MonoBehaviour.StartCoroutineManaged2_Injected (System.IntPtr , System.Collections.IEnumerator ) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 30.1s ago]
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__32:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:756)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' dig: → source lane 1A: never arrived at (-12.64, 1.19, 39.19) (elapsed 30.0s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=9.5, reissues=0, lastSeekCancelledBy=[at GameCore.Labor.TrailerLoadController+<SeekViaNavMesh>d__32.MoveNext () [0x00249] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\1. FSM\Core\Labor\TrailerLoadController.DigOut.cs:701 < at UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < at UnityEngine.MonoBehaviour.StartCoroutineManaged2_Injected (System.IntPtr , System.Collections.IEnumerator ) [0x00000] in <d5ea965f39d74501b56f4006ef80da34>:0 < ] 30.1s ago]
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__32:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:756)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' dig: → source lane 2C: never arrived at (-2.04, 1.19, 39.19) (elapsed 30.0s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=4.6, reissues=0, lastSeekCancelledBy=[at AiNavigation.Update () [0x00352] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1284 < ] 40.2s ago]
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__32:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:756)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```
- **WARN** (day 2): [AiNavigation] DS_FULL(Clone): stuck at (-7.59, 1.11, 41.74) — walking out to (-7.01, 1.23, 40.35) (13.48m to mesh).
- **REPEATING** (day 2): x25 and counting: [EquipmentTraffic] 'RT_Full(Clone)' is blocked at (-27.21, 1.11, 49.16): blocked by RT_Full(Clone) for 30s with no safe way round. No safe way to move - staying put.
- **ERROR** (day 2): [TrailerLoad] 'DS_FULL(Clone)' dig: → source lane 1A: never arrived at (-12.64, 1.19, 39.19) (elapsed 30.0s) — refusing to straight-line through pallets. [seekingTask=True, onMesh=True, pathStatus=PathComplete, remaining=9.6, reissues=0, lastSeekCancelledBy=[at AiNavigation.Update () [0x00352] in C:\Users\MURILLO\Documents\GitHub\03-29-26 REDO\Assets\_Project\Scripts\Actors\AiNavigation.cs:1284 < ] 38.4s ago]
  ```
  UnityEngine.Debug:LogError (object)
  GameCore.Labor.TrailerLoadController/<SeekViaNavMesh>d__32:MoveNext () (at Assets/_Project/Scripts/1. FSM/Core/Labor/TrailerLoadController.DigOut.cs:756)
  UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
  
  ```

## Bot actions (first 400)
