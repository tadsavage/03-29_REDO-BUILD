---
description: Working on the inventory system and everything attached to it — pallets (PalletData/PalletMasterRecord/InventoryService), SKUs, locations and slots (LocationData, LocationStatusRegistry, SlotRegistry, SlotAssignmentService), putaway and replenishment, staging lanes and stacking, pallet building/heights, persistence, and the inventory UIs (New Item, Slot Assignment, Pallet Builder, Purchasing stock readouts). Use whenever the task mentions pallets, SKUs, inventory, on-hand, putaway, replenishment, pick/reserve slots, rack locations, lane slots/stacking, expiry/spoilage, Load IDs, PalletBuilder, the New Item panel (key 5), Slot Assignment, or files InventoryService.cs / PalletData.cs / PalletMasterRecord.cs / SkuData.cs / PutawayLogic.cs / ReplenishmentService.cs / LocationStatusRegistry.cs / SlotRegistry.cs / PalletInventoryTracker.cs.
---

# Inventory system

Companions: `order-system` (orders/POs that create and consume stock), `work-queue` (tasks), `lane_setup` (door/lane naming and `LaneConfig`), `racking-system` (rack slots and labels), `unity:ui-uitk`.

Code: `Assets/_Project/Scripts/1. FSM/Core/Inventory/` (data + services), `.../Core/Labor/` (putaway, replenishment, dock controllers, `DockPalletUtility`), `.../Gameplay/` (`PalletBuilder`, `PalletInventoryTracker`, `LaneConfig`, `LaneNamingService`), `.../Racking/` (`SlotRegistry`, `LocationRegistry`), `.../UI_UX/` (panels).

## Two representations of a pallet (know which you hold)
- **`PalletMasterRecord`** — the data record owned by `InventoryService` (id, SKU, quantity, grid cell, load id, expiry, status, location name). The system of record.
- **`PalletData`** (MonoBehaviour, `Gameplay/…`) — lives on the pallet GameObject in the world. `PalletMasterLink` / `PalletInventoryTracker` tie the two. Phantom records happen when a physical pallet is never linked (`PalletInventoryTracker` must attach `PalletMasterLink`).
- `AreaCategory { Grocery, Perishable, Frozen }`, `PalletStatus { Shippable, QAHold, Lost, OnReserve }` (QAHold/Lost still mostly unused).
- `LoadIDGenerator` makes unique 10-digit load ids; `PalletData.LoadId`, `GetPalletByLoadId`.

## InventoryService (IService, `GameContext`)
- Lifecycle: `ReceiveShipment`, `ReceivePalletWithLoadId`, `RegisterPhysicalPallet`, `RegisterPalletDirect`, `MovePallet`, `PickFromPallet`, `DestroyPallet`, `ClearAllPallets`.
- Queries: `GetPallet`, `GetPalletsBySku`, `TotalOnHand`, `GetPalletsAtLocation`, `GetReceivingPallets`, `GetPalletsInLane`, `GetPalletAddress/Tier`, `TryGetPalletSlot`, `HasCapacityAtLocation`.
- Events (static): `OnPalletReceived/Moved/PartialPicked/Destroyed`, `OnSpoilageDetected`, `OnInventoryRestored`. Daily spoilage check hangs off `OnDayChanged`. **Static events outlive Shutdown — unsubscribe.** `WorkQueueSystem` cancels tasks for a destroyed pallet via `OnPalletDestroyed`.
- SKUs: `SkuData` SOs, loaded with `LoadSkuDatabase(...)` from `Resources/Inventory/SKUs` (call happens in `GameContext.Awake` right after `Initialize`; nothing downstream works if the database is empty). `HiCount` = layers per pallet (the inline comments claiming "units per layer" are backwards). Imported from Excel via `SkuImporter` (99 assets outside Resources). `RequiredPalletHeight() = caseHeight * HiCount + 0.16`.
- Lane/stack API: `TryGetNextFreeSlot`, `PutawayToLane`, `TryGetNextPick`/`PickNextFromLane` (FIFO/LIFO by `LaneConfig`), `LaneAcceptsPutaway`, `LaneAllowsPicking`.

## Staging lanes — tiers, reservations, locks (outbound + inbound share lanes)
- Occupancy is **tier counts, not booleans**. `OccupiedTiersAt(cell)` = inbound stock + staged outbound + in-flight reservations, compared against `LaneConfig.MaxStackHeight` (default 2). `CountFreeStagingSlotsInLane` returns TIERS. `OutboundPalletsByCell` match must use PLANAR distance.
- `TryReserveStagingSlot` / `ReleaseStagingSlot` (count dictionary): reserve immediately after resolving a slot with no `yield` in between; release in the success and abort choke points (`Restore()` / `AbortRoutine()` in `ReachTruckOperator`).
- `TryEnterLaneForDelivery` / `ReleaseLaneEntry` = one vehicle in a lane at a time (caller-agnostic: reach truck, dock stocker, future pallet jack). `finally` runs on exceptions but NOT on `StopCoroutine` or destroy — don't claim otherwise in comments.
- Drop height: `TrailerOffloadController.StagingDropBaseY(door, lane, cell, selfGO)` measures real renderer tops (+ `StackGap`) or `LaneSurfaceY` 1.15. Skip only pallets riding forks via `IsRidingForks` (`GetComponentInParent<MHEOperatorSlot>() != null`) — NOT "has a parent", because restored pallets sit under `PlacedObjectsContainer`.
- Pallet geometry constants: foundation 1.06 + floor 0.06 + gap 0.015 + pallet 0.16 (+ cases). `PalletHeightCalculator`, `PalletPlacementHelper`. Dock pallets save by literal transform + `worldY`.

## Racking slots, locations, status (the split-brain)
- `LocationData` (Inspector MonoBehaviour on each rack slot child) vs **`LocationStatusRegistry`** (static `address → LocationStatus`, the thing putaway actually obeys and the thing that's saved). They can disagree. Status reads in `PutawayLogic`/`ReplenishmentService` must use the registry; `LocationRegistry.Recompute()` pushes the registry value back into `LocationData.SyncStatusDisplay` each heartbeat.
- `LocationData.LoadId` and `PalletData.LocationName` give a readable cross-reference. `ReconcilePhysicalOccupancy()` repairs slots (keyed on "no pallet recorded"). `ReleasePhantomOccupancy` frees a slot Occupied with no pallet within 0.9 m for 3 heartbeats. `ReleaseUnclaimedReservations` runs after a save import to drop orphaned `Reserved` locks. Jump buttons: **Find Location** / **Find Pallet** inspector buttons.
- `SlotRegistry` scans live racks (label text `AA-BB-LP`, numeric level = Pick, letter = Reserve); `SlotAssignmentService` maps pick address → SKU (in memory; not saved yet); `SlotAssignmentPanel` (click a rack) assigns; `PickSlotOverlayController` recolours labels red/yellow/green.

## Putaway and replenishment
- `PutawayLogic.AssignPutawayDestination(palletId, pos)` picks Pick vs Reserve (reserves as close to the pick slot as possible); `CompletePutaway` / `CancelPutaway`. **Resolve and reserve the destination BEFORE physically picking the pallet up.** No destination → leave the pallet untouched, back off 15s (`NoDestinationBackoff`).
- `ReplenishmentService`: scans pick slots (<2 cases), locks pick+reserve slot when the task is created. `TryFindOldestReserve` / `TryFindOldestPalletAnywhere` require the pallet GameObject to exist; replenish tasks with no pallet anywhere are cancelled and slots released.
- `ReachTruckOperator` is the executor: hybrid drive (manual transform for precision, NavMeshAgent via `SeekViaNavMesh` for open-floor legs, `SetDriveMode` is the ONLY toucher of agent enable). Anchors are at SHELF height — flatten to floor Y before navigating. Failed tasks `ReleaseTaskWithBackoff` (60s); 2-minute stuck watchdog. Priorities all start at 100.
- Receiver flow: `ReceiverReceivingWorkflow` + `ReceivingTaskDriver` (watchdog clears `_taskInProgress` if workflow isn't busy for 12s).

## Persistence
- Saved via `PlacementSystem.ApplySaveData` / `SaveData`: pallets (with `worldY`), `locationStatuses`, tasks, orders, shipments, appointments, contracts, reputation, broker, vendors. **Not saved:** staged outbound pallets, in-flight trucks, `SlotAssignmentService`. `ReconcileStagedOrdersAgainstScene` papers over the first.
- After a load call `grid.RebuildFromRegistry()` and `EconomyService.RebuildFromRegistry()`.
- Ghost data: a save with every rack `Occupied` on empty racks caused phantom replenishments; the save-clean + runtime guards exist, but if tasks appear for nonexistent stock, check the saved `locationStatuses` first.
- After a load, `PalletInventoryTracker`-less pallets and unlinked `PalletMasterRecord`s are the usual cause of "reshuffled on F5/F9".

## Inventory UIs
- **New Item panel (key 5)** `NewItemPanel` + `ItemCreatorPanel`: SKU creation, incoming-PO SKU report (listed from the start of the appointment block), refresh interval is scaled time (fine while paused).
- **Slot Assignment** `SlotAssignmentPanel`; **Pallet Builder** `PalletBuilderUI` (shift-click spawns; keep raycasts non-competing); **Purchasing (9)** stock/`WAREHOUSE:` line reads `LocationStatusRegistry`; **Work Queue (7)** pallet id / item / area / from / to columns; **Reports** for fill rate (`FulfillmentStatsService`, `OrderShortsPopup`).
- All are programmatic UI Toolkit panels with inline styles, house palette (navy `#141C26`/`#121A24`, blue `#5C9BC4`, orange `#B5743A`), Lilita One. Use `flexShrink = 0` on fixed-height rows and fixed widths on per-frame text. Preview directional lights (AOD, item creator) must stay OFF except while rendering.

## Gotchas that already cost time
- Two stores for slot state; always trust the registry.
- `PalletBuilder.GetPrefabDimensions` is public static; case prefab dimensions drive heights.
- Statics persist between Play sessions (Domain Reload off) — clear caches on `SubsystemRegistration`.
- Don't stamp `task.ToLocation` before the pallet is on the forks (save routing depends on it).
- Compile gate: `Library/ScriptAssemblies/*.dll` newer than the `.cs`, then grep `Logs/Editor.log` for `error CS`. `read_console` shows only errors/warnings, not `Debug.Log`.

## Verification
Query live services via `Unity_RunCommand`/`execute_code` (counts of pallets, occupied slots vs registry, free tiers per lane) and confirm against what's physically in the scene; measuring the value you set is not the same as verifying the behaviour.

## Rack slot generation (2026-10-13 fix)
Only the 80" rack prefab (`Rack-FullOrange80`) authors a `Location` child with a `LocApproachAnchor` (3 m out from the face). The 48" racks (`Rack-FullOrange48`, `Rack-FullYellow48`) have none, so `LocationRegistry.Recompute` GENERATES one `LocationData` per label face. It used to create a new one every 1 s heartbeat (the lookup searched the label group, the generated object lived under the rack root): ~1,350 duplicates per slot, 27,000 `LocationData` in a session, and none had an approach anchor, so every putaway to a 48" rack aborted with "Approach anchor not found" and the reach truck lifted and replaced the same pallet forever. Now one generated location per face (`_generatedByFace`), duplicates removed, and `EnsureApproachAnchor` adds the anchor (+3 m Front / -3 m Rear). `ReachTruckOperator.NoteMissingAnchor` marks a slot `Problem` after 3 anchor failures so a similar fault can never loop. If you add a new rack prefab, give it a Location + anchor or rely on the generated one; check the scene `LocationData` count if the frame rate sags.

**Update (same day):** all 8 rack prefabs now carry authored `Location` + `LocApproachAnchor` children matching Rack-FullOrange80 (Full racks: L = rack-local x 0, R = x -1.325, depth centre z 0, y 0; Half racks: x -0.07; anchors at z +3 Front / -3 Rear). The pallet is seated ON the Location transform, so a location placed on the label FACE (what the runtime fallback did first) leaves pallets half out of the rack; the fallback now uses the same rack-origin convention. New rack prefabs need the same children.

## Pallet lifecycle + save/load rules (2026-10-17)
- **Status:** `PalletLifecycle { Unknown, Available, InTransit, Reserved, Staged, Loading, Shipped }` via `PalletLifecycleResolver.Resolve(go)` / `ForRecord(rec)`. DERIVED, never stored. Reserved = live Replenish/PalletPick names the pallet, or a pick-face pallet a released order needs. Putaway does not reserve. Shown in the hover popup and PalletData inspector.
- **Exclusive reservation:** any new sourcing code must skip `PalletLifecycleResolver.IsReservedByTask(palletId, out _)` pallets.
- **Billing pays SHIPPED pallets only** (`OrderService.ShipOrder`, `CasesOn`).
- **"Is it standing in the lane?" = `OutboundPalletBuilder.IsOnFloor` / `IsOnFloorTransform`.** Never `transform.parent != null`: restored pallets sit under PlacedObjectsContainer.
- **Who saves which pallet:** rack/lane PlacedObject pallets (incl. staged whole picks) -> `PalletPersistenceService`; trailer cargo (inbound and outbound, with order tag + per-case SKU) -> `TruckPersistenceService`/`TruckController.CaptureTrailerPallets`; reach-truck carries -> `MHEOperatorPersistenceService` (skips outbound-tagged pallets); case-picked pallets and outbound pallets riding selectors/jacks/dock stockers -> `OutboundPalletPersistence` (restore BEFORE `ReconcileStagedOrdersAgainstScene`; carried ones set down in `SetDownCarried`). A new carrier type needs a home in this list.
- Selector trips stage via `StagingLaneAssignmentService.TryFindTripLane` (own door, lanes already used, then nearest other door).

## MHE batteries and charging stations (2026-10-17)
`Actors/EquipmentBattery.cs` on every RT/DS/pallet jack (attached by `MHEOperatorSlot.Start`): 8 game h of use (drains only while moving), 2 h to charge on an `MHE_Station` (`PalletJackParkingSpot` = the bay; every bay is a charger). Saved in `PlacedObject.customData` ("batt=0.73" / "batt=0.43;need"). Flat battery: operator ejected (`MHEOperatorSlot.EjectRider`, never `VacateOperator` - that parks the vehicle nav), the machine finishes the job on its forks at 2x (`SpeedMultiplier`; RT/DS/jack move code multiplies by it), then `ChargeTrip` drives it to the nearest free station (queue at the open end if none; full idle machines `YieldBay`). `AcceptsOperator` blocks boarding until full. Right-click a free machine = "Charge Battery" (`EquipmentBatteryPanels`). Station letters `MHEStationLetter`, lamps `MHEStationLights`. Any new MHE controller must: check `EquipmentBattery.NeedsCharge` before taking more work, multiply its drive speeds by `SpeedMultiplierFor`, and report busy so the battery waits for it (add it to `EquipmentBattery.IsBusy`).
