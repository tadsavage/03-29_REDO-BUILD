# Persistence Architecture Plan — 9 Missing Gaps

## Executive Summary

Current save/load system captures: placed objects, pallets, economy, time, work queue, employees, and settings. **9 critical gaps remain** that break continuity for the 6-chunk gameplay loop (Inbound→Putaway→Replenishment→OrderSelection→Shipping→Invoicing).

**Recommended MVP scope:** Phases 1–3 (Slot Assignments, Shifts, Orders/Trucks). This unblocks the full inbound→receiving→putaway chain. Phases 4–8 are flavor/polish.

---

## Gap Analysis & Phasing Strategy

### Phase 1: Slot Assignments + Shift Schedules (Small, Quick Wins)

**Gap 1: Slot Assignments**
- **Current:** `SlotAssignmentService` maintains in-memory `Dictionary<address, skuId>` — no persistence
- **Impact:** Putaway logic can't route pallets to assigned pick slots on reload; routing reverts to random
- **MVP necessity:** HIGH — putaway logic depends on this for correct destination slot
- **Risk:** LOW — self-contained, no other systems depend on it
- **Scope:** 50–80 lines of code

**Gap 2: Shift Schedules**  
- **Current:** `ShiftManagerPanel` defines player shifts (name, start/end times per day) in-memory only
- **Impact:** Shift definitions revert on reload; shift display resets to empty
- **MVP necessity:** MEDIUM — UI-only per CLAUDE.md ("not yet wired to gameplay"); doesn't block chunks 1–6
- **Risk:** LOW — data structure is already serializable
- **Scope:** 40–60 lines of code

**Why Together:** Both are small, unblock UI features, zero risk. Quick confidence-builder.

---

### Phase 2: Customer Orders & Shipments (Large, Critical Path)

**Gap 4: Customer Orders/Shipments**
- **Current:** `ShipmentService` creates POs; `TruckYardManager` queues them; `TruckController` spawns/routes them
  - `ShipmentData` has fields: supplier, line items, cost, status
  - PO lifecycle: Created → Queued → InTransit (truck spawned) → Received (docked) → Invoiced
- **Missing:** 
  - Full PO persistence (PO ID, line items, status progression, arrival/dock times)
  - Shipment queue state (which POs are pending, in-flight, received)
  - Supplier performance tracking (optional, not MVP)
- **Impact:** Orders reset on reload; can't resume half-received shipments; inbound loop breaks
- **MVP necessity:** CRITICAL — backbone of chunk 1 (Inbound) and ongoing chapters
- **Risk:** MEDIUM — touches multiple state machines (ShipmentService, TruckYardManager, ReceivingService)
- **Scope:** 200–300 lines (new classes + modifications)

**Implementation:**
```
SaveData additions:
  - List<ShipmentSnapshot> shipments
  - List<int> pendingPOIds  // ordered queue

ShipmentSnapshot {
  int poId
  string supplierId
  List<ShipmentLineItem> lineItems  (already serializable)
  int createdDayNumber
  int expectedArrivalDayNumber
  int actualArivalDayNumber  // -1 if not yet arrived
  int receivedDayNumber  // -1 if not yet received
  int shippedDayNumber  // -1 if not yet invoiced
  ShipmentStatus status  (enum)
  int assignedTruckId  // -1 if not yet assigned
  int totalCost
}

Services modified/added:
  - ShipmentService.GetAllShipments(), RestoreShipments()
  - TruckYardManager.GetPendingPOQueue() (if not already public)
  - ReceivingService guards against double-receiving same PO
```

---

### Phase 3: Trucks (Medium, Works Alongside Phase 2)

**Gap 1: Trucks**
- **Current:** `TruckController` manages a single truck lifecycle (yard → gate → dock → unload → depart)
  - Owned by `TruckYardManager` via instantiate-and-forget pattern
  - State machine: YardQueue → Gate → Docked → BeginDeparture → Departed (then destroyed)
  - One pre-built trailer with cargo loaded at spawn time (via `LoadShipment()`)
- **Missing:**
  - Truck position/state snapshot (current state, timer counters, location)
  - PO assignment linkage (which PO this truck is carrying)
  - Cargo state (pallets loaded, their exact positions/rotations, case counts)
  - Trailer door state (open/closed)
  - Dock slot assignment (which dock door/slot, if currently docked)
  - Expected arrival/actual arrival times (for scheduling logic)
- **Impact:** Trucks disappear on reload; if docked with half-unloaded cargo, that cargo vanishes; inbound loop breaks
- **MVP necessity:** CRITICAL — complementary to Phase 2; together they complete inbound chain
- **Risk:** MEDIUM — `TruckController` is already complex (Bezier curves, state machine, door animations)
- **Scope:** 200–280 lines (new classes + modifications)

**Implementation:**
```
SaveData additions:
  - List<TruckSnapshot> trucks

TruckSnapshot {
  int truckInstanceId  // reference key
  int assignedPOId  // which PO this truck is carrying
  TruckState currentState  (enum: YardQueue, Gate, Docked, BeginDeparture, etc.)
  Vector3 currentPosition
  Quaternion currentRotation
  float stateTimerElapsed  // how long in current state
  
  // If docked:
  int dockedAtSlotNumber  // which DockSlot it's in (-1 if not docked)
  bool trailerDoorsOpen
  
  // Cargo state (pallets on trailer, before offload):
  List<CargoSnapshot> cargo  // same as DockPalletSnapshot but with trailer-local coords
  
  // Timing:
  int createdDayNumber
  int expectedArrivalDayNumber
  int actualArrivalDayNumber  // -1 if not yet arrived
  
  // Operational:
  bool isDestroyed  // flag to skip if truck was already cleaned up
}

CargoSnapshot (alternative/complement to DockPalletSnapshot):
  Vector3 truckLocalPosition  // relative to trailer root, not world
  Quaternion rotation
  int caseCount
  string skuId  // fallback for identifying cases
```

**Restore order (critical):**
1. Restore Shipments (Phase 2)
2. Restore Trucks (this phase)
3. TruckController.Update() resumes from saved state; timers continue counting down to next state transition
4. If a truck is in Docked state, ReceivingService checks if offload is in progress and resumes

---

### Phase 4: Vehicle Operator State (Small, Refinement)

**Gap 7: Vehicle Operator State**
- **Current:** Each vehicle (ReachTruck, Dockstocker) has an `MHEOperatorSlot` with a single operator (EmployeeRecord)
  - Operator is a child GameObject, spawned with the vehicle, linked via slot reference
  - Operator role constraints: ReachTruck requires `ReachTruckOperator`; Dockstocker requires `DockStockerOperator`
- **Missing:**
  - Operator GUID persisted so the *same person* re-boards vehicle on load (currently a new hire spawns)
  - Operator's current task ID (if assigned to a Putaway/Replenish task) — so they can resume work
  - Operator patrol state / current waypoint
- **Impact:** Vehicle operator becomes a stranger on reload; continuity break
- **MVP necessity:** MEDIUM — doesn't block chunks but breaks narrative ("why is this a different person?")
- **Risk:** LOW — self-contained in MHEOperatorSlot + operator GUID linkage
- **Scope:** 60–100 lines

**Implementation:**
```
SaveData additions:
  - List<VehicleOperatorSnapshot> vehicleOperators

VehicleOperatorSnapshot {
  int vehicleGridX  // to find the vehicle on load
  int vehicleGridY
  string operatorGuid  // link to EmployeeRecord
  string assignedTaskId  // null if not assigned
  // Patrol state (optional, can leave empty for MVP):
  int currentWaypointIndex  // -1 if not on a waypoint
}

Restore logic in PlacementSystem.ApplySaveData():
  1. After vehicles are restored from placedObjects
  2. For each VehicleOperatorSnapshot:
     - Find vehicle at (gridX, gridY)
     - Find EmployeeRecord by GUID (from restored employeeRecords)
     - MHEOperatorSlot.ReBoardOperator(employee)
     - If assignedTaskId is not null, lookup task and mark as Assigned
```

---

### Phase 5: Employee Detail State (Medium, Polish)

**Gap 6: Employee Detail State**
- **Current:** EmployeeRecord saves/restores; modular avatar system assembles random parts on hire
  - Modular avatar system: scans gender+slot+variant FBX parts → data-driven library → random assembly
  - Employee has `EmployeeIdentity.Record` which holds stats/position but NO outfit state
- **Missing:**
  - Selected modular avatar parts per employee (face, body, clothes, hair)
  - Current action/animation state (idle, walking, animating a task, etc.)
  - Task-in-progress re-establishment (what work was being done when saved)
  - Emotional state (emote, morale context)
- **Impact:** Employees lose their outfit on reload (random re-dress); can't track in-progress work; loses personality
- **MVP necessity:** MEDIUM — employees still function, but lose flavor
- **Risk:** MEDIUM — avatar system complex; animation state reconstruction needed
- **Scope:** 120–180 lines

**Implementation:**
```
SaveData additions to EmployeeRecord:
  - string selectedHeadVariant  // e.g., "Female_Head_1"
  - string selectedBodyVariant  // e.g., "Female_Body_Casual"
  - string selectedHairVariant
  - string selectedOutfitVariant
  
  - int currentAnimationState  // enum: Idle, Walking, Placing, Picking, etc. (optional for MVP)
  - string currentTaskIdInProgress  // task GUID if mid-work (optional for MVP)

EmployeeSpawner changes:
  - When respawning from EmployeeRecord, if selectedHeadVariant is not empty:
    - Use those parts instead of random selection
  - OnStartWork() now stores currentTaskIdInProgress in record (one-frame update before save)
```

---

### Phase 6: Multi-Pallet Lane Stacking (Medium, Late-stage Polish)

**Gap 5: Multi-Pallet Lane Stacking**
- **Current:** Each lane cell (defined by Flr-ShipLane tile row) holds one pallet max
  - `PalletSnapshot.stagingLaneId` records which lane, but implicitly one pallet per cell
  - Lane display shows "Lane 1A: 1 pallet" — no tier numbering
- **Missing:**
  - Layer/tier tracking (cell can have pallet A at y=0, pallet B at y=0.16+height_A, pallet C at y=0.16*2+height_A+height_B)
  - Tier occupancy limits (lane capacity = 3 tiers max, typically)
  - Slot assignment considers tier height (pick slots on tier 0, reserve starts at tier 1)
  - Pathfinding avoids "reach over full tier" scenarios
- **Impact:** Lane overflow impossible; realistic dock congestion can't happen; unloading full trailers creates deadlock
- **MVP necessity:** MEDIUM — MVP works single-pallet-per-cell; stacking is nice-to-have polish
- **Risk:** MEDIUM — affects multiple subsystems (lane routing, putaway logic, UI display, pathfinding)
- **Scope:** 100–150 lines (incremental, not from scratch)

**Implementation:**
```
SaveData changes to PalletSnapshot:
  + int tierIndex  // 0 = first pallet in lane, 1 = second, etc. (default 0)
  + float tierHeightOffset  // world Y offset from base tier

Lane naming changes:
  - Current: "1A" (door 1, lane A)
  - Add tier suffix (optional): "1A-0", "1A-1" for tier 0/1 of same lane
  - Or keep display as "1A (2/3)" showing "pallet 2 of 3 tiers"

Restore logic:
  - PalletSnapshot.tierIndex controls where pallet is stacked
  - PalletPersistenceService.RestoreAll() respects tier for world Y calculation
  - Lane slot assignment ensures picks go to tier 0, reserves start at tier 1
```

---

### Phase 7: Guard Presence/State (Small, Optional)

**Gap 8: Guard Presence/State**
- **Current:** Guard may or may not exist (no guard shack = auto-approve all POs)
  - `GuardController` manages gate inspection (PO scanning, door open/close, timing)
  - Guard optional per CLAUDE.md — not in baseline MVP focus (Tad's on inbound/putaway/shipping, not gate drama)
- **Missing:**
  - Guard existence flag (did the player place a guard shack?)
  - Guard state (idle, inspecting, door opening/closing, directing truck)
  - Current PO being inspected (progress through line items)
- **Impact:** Guard shack optional; if present and docked with truck, guard may disappear on reload
- **MVP necessity:** LOW — fully optional; not in baseline MVP per Tad's current focus
- **Risk:** LOW — guard system is self-contained
- **Scope:** 50–80 lines (trivial if needed)

**Implementation:**
```
SaveData additions (conditional, only if guard exists):
  - GuardSnapshot guard  (null if not placed)

GuardSnapshot {
  bool exists  // did player place guard shack?
  Vector3 guardPosition  // world position
  Quaternion guardRotation
  GuardState currentState  // Idle, Inspecting, DoorOpen, Directing, etc. (enum)
  string currentPOBeingInspected  // PO ID, if any
  int currentLineItemIndex  // which line item in PO
  float stateTimerElapsed
}

Restore logic:
  - If guard snapshot exists and EmployeeRegistry has a "Guard" role employee:
    - Restore their position + state
  - If guard snapshot is null, no guard gets spawned (matches original)
```

---

### Phase 8: Contamination/Incident History (Medium, Deferred)

**Gap 9: Contamination/Incident History**
- **Current:** `PalletData.isContaminated` flag only; no incident tracking
  - Can mark pallet as contaminated, but can't trace *why* (expiration, rat, damage, etc.)
- **Missing:**
  - Incident type (enum: Expired, RatDamage, PhysicalDamage, Contamination, etc.)
  - Incident timestamp (when detected)
  - Incident source (what caused it: time passage, rat activity, worker error, etc.)
  - Incident history (multiple incidents per pallet)
- **Impact:** Can't distinguish incidents; no reporting; no accountability
- **MVP necessity:** LOW — basic expiration works without this; rat system deferred anyway per CLAUDE.md
- **Risk:** LOW — incident system is self-contained, purely additive
- **Scope:** 100–150 lines (deferrable post-MVP)

**Implementation:**
```
SaveData additions (new, optional):
  - List<IncidentSnapshot> incidents  // global incident log

IncidentSnapshot {
  string incidentId  (GUID)
  int inGameDayNumber
  int inGameHourNumber
  string palletId  // which pallet (or null if global)
  IncidentType type  (enum: Expired, RatDamage, PhysicalDamage, Contamination, etc.)
  string description  // "Case damaged during putaway at XX-YY-00"
  int estimatedLoss  // $ amount or case count
  bool isResolved  // false = open incident, true = documented/written off
}

Restore logic:
  - Create a standalone IncidentService that owns the incident log
  - On load, restore all incidents so reporting UI can query them
  - No special state machine needed; purely data
```

---

## Critical Files to Modify

### SaveData.cs
Add new fields (in order of priority):
```csharp
// Phase 1
public List<SlotAssignmentEntry> slotAssignments = new();  
public List<ShiftScheduleSnapshot> shifts = new();

// Phase 2
public List<ShipmentSnapshot> shipments = new();

// Phase 3
public List<TruckSnapshot> trucks = new();

// Phase 4
public List<VehicleOperatorSnapshot> vehicleOperators = new();

// Phase 5
// (embedded in EmployeeRecord — no new SaveData field)

// Phase 6
// (modify existing pallets, add tierIndex field)

// Phase 7
public GuardSnapshot guard;

// Phase 8
public List<IncidentSnapshot> incidents = new();
```

### PlacementSystem.cs — BuildSaveData()
Add collection logic for each phase:
```csharp
private SaveData BuildSaveData(string saveName)
{
    // ... existing code ...
    
    // Phase 1
    save.slotAssignments = SlotAssignmentService.Export();
    save.shifts = ShiftManagerPanel.Export();
    
    // Phase 2
    if (ServiceLocator.TryGet(out ShipmentService shipService))
        save.shipments = shipService.GetAllShipments();
    
    // Phase 3
    save.trucks = TruckController.GetAllTrucksSnapshot();  // or via TruckYardManager
    
    // Phase 4
    save.vehicleOperators = MHEOperatorSlot.GetAllOperatorSnapshots();
    
    // Phase 7
    var guard = FindObjectOfType<GuardController>();
    if (guard != null)
        save.guard = guard.Snapshot();
    
    // Phase 8
    if (ServiceLocator.TryGet(out IncidentService incService))
        save.incidents = incService.ExportAll();
    
    return save;
}
```

### PlacementSystem.cs — ApplySaveData()
Add restoration logic for each phase (in dependency order):
```csharp
private void ApplySaveData(SaveData save)
{
    // ... existing code (camera, money, time, etc.) ...
    
    // Phase 2 FIRST (orders must exist before trucks try to link to them)
    if (ServiceLocator.TryGet(out ShipmentService shipService) && save.shipments != null)
        shipService.RestoreShipments(save.shipments);
    
    // Phase 3 (trucks link to orders)
    if (save.trucks != null && save.trucks.Count > 0)
        TruckYardManager.Instance?.RestoreTrucks(save.trucks);
    
    // Phase 1 (independent)
    SlotAssignmentService.Import(save.slotAssignments ?? new List<SlotAssignmentEntry>());
    ShiftManagerPanel.Import(save.shifts ?? new List<ShiftScheduleSnapshot>());
    
    // Phase 4 (after vehicles are restored)
    if (save.vehicleOperators != null)
        MHEOperatorSlot.RestoreOperators(save.vehicleOperators);
    
    // ... existing employee/pallet/economy restore ...
    
    // Phase 5 (already in EmployeeRecord, no special restore needed)
    
    // Phase 6 (modify pallet restore to use tierIndex)
    // (already handled in InventoryService.RestorePallets if tierIndex field added)
    
    // Phase 7 (guards, if present)
    if (save.guard != null && save.guard.exists)
        GuardController.Instance?.RestoreState(save.guard);
    
    // Phase 8 (deferred post-MVP)
    if (ServiceLocator.TryGet(out IncidentService incService) && save.incidents != null)
        incService.RestoreAll(save.incidents);
    
    // ... rest of existing restore code ...
}
```

### New Snapshot Classes (add to SaveData.cs or separate files)

**Phase 1:**
```csharp
[System.Serializable]
public class SlotAssignmentEntry
{
    public string locationAddress;  // e.g., "01-01-00"
    public string skuId;
}

[System.Serializable]
public class ShiftScheduleSnapshot
{
    public string shiftName;
    public List<DayShiftTimes> schedule = new();  // 7 entries for Sun-Sat
}

[System.Serializable]
public class DayShiftTimes
{
    public int dayOfWeek;  // 0-6
    public int startHour, startMinute;  // -1/-1 = Closed
    public int endHour, endMinute;
}
```

**Phase 2:**
```csharp
[System.Serializable]
public class ShipmentSnapshot
{
    public int poId;
    public string supplierId;
    public List<ShipmentLineItem> lineItems = new();
    public int createdDayNumber;
    public int expectedArrivalDayNumber;
    public int actualArrivalDayNumber = -1;
    public int receivedDayNumber = -1;
    public int shippedDayNumber = -1;
    public int status;  // ShipmentStatus as int
    public int assignedTruckId = -1;
    public int totalCost;
}
```

**Phase 3:**
```csharp
[System.Serializable]
public class TruckSnapshot
{
    public int truckInstanceId;
    public int assignedPOId = -1;
    public int currentState;  // TruckState as int
    public Vector3 currentPosition;
    public Quaternion currentRotation;
    public float stateTimerElapsed;
    public int dockedAtSlotNumber = -1;
    public bool trailerDoorsOpen;
    public List<CargoSnapshot> cargo = new();
    public int createdDayNumber;
    public int expectedArrivalDayNumber;
    public int actualArrivalDayNumber = -1;
    public bool isDestroyed;
}

[System.Serializable]
public class CargoSnapshot
{
    public Vector3 truckLocalPosition;
    public Quaternion rotation;
    public int caseCount;
    public string skuId;
}
```

**Phase 4:**
```csharp
[System.Serializable]
public class VehicleOperatorSnapshot
{
    public int vehicleGridX;
    public int vehicleGridY;
    public string operatorGuid;
    public string assignedTaskId;
    public int currentWaypointIndex = -1;
}
```

**Phase 7:**
```csharp
[System.Serializable]
public class GuardSnapshot
{
    public bool exists;
    public Vector3 guardPosition;
    public Quaternion guardRotation;
    public int currentState;  // GuardState as int
    public string currentPOBeingInspected;
    public int currentLineItemIndex;
    public float stateTimerElapsed;
}
```

**Phase 8:**
```csharp
[System.Serializable]
public class IncidentSnapshot
{
    public string incidentId;
    public int inGameDayNumber;
    public int inGameHourNumber;
    public string palletId;
    public int type;  // IncidentType as int
    public string description;
    public int estimatedLoss;
    public bool isResolved;
}
```

---

## Service Accessor Methods (New or Modified)

### Phase 1
**SlotAssignmentService:**
- `public static List<SlotAssignmentEntry> Export()` — returns current assignments as list
- `public static void Import(List<SlotAssignmentEntry> entries)` — restores from snapshot

**ShiftManagerPanel:**
- `public static List<ShiftScheduleSnapshot> Export()` — returns shift definitions
- `public static void Import(List<ShiftScheduleSnapshot> shifts)` — restores shift definitions

### Phase 2
**ShipmentService (new methods):**
- `public List<ShipmentSnapshot> GetAllShipments()` — serialize all active shipments
- `public void RestoreShipments(List<ShipmentSnapshot> snapshots)` — deserialize and queue
- `public ShipmentData GetShipmentByPOId(int poId)` — lookup existing PO (for truck linkage)

### Phase 3
**TruckYardManager or TruckController (new methods):**
- `public static List<TruckSnapshot> GetAllTrucksSnapshot()` — capture all active trucks
- `public void RestoreTrucks(List<TruckSnapshot> snapshots)` — spawn and position trucks from snapshot
- Each TruckController instance should expose `GetSnapshot()` / `RestoreFromSnapshot()`

### Phase 4
**MHEOperatorSlot (new methods):**
- `public static List<VehicleOperatorSnapshot> GetAllOperatorSnapshots()` — capture all vehicle operators
- `public static void RestoreOperators(List<VehicleOperatorSnapshot> snapshots)` — re-board operators
- `public void ReBoardOperator(EmployeeRecord employee)` — link specific employee to this slot

### Phase 7
**GuardController (new methods):**
- `public GuardSnapshot Snapshot()` — capture guard state
- `public void RestoreState(GuardSnapshot snap)` — restore guard to saved state

### Phase 8
**IncidentService (new, or add to existing system):**
- `public List<IncidentSnapshot> ExportAll()` — serialize all incidents
- `public void RestoreAll(List<IncidentSnapshot> snapshots)` — deserialize incident log

---

## Restore Order & Dependency Graph

**Critical:** Restore in this order to avoid null-reference exceptions:

```
1. MONEY / TIME / SETTINGS  (already done)
   ↓
2. SHIPMENTS (Phase 2)       ← orders must exist first
   ↓
3. TRUCKS (Phase 3)          ← trucks link to orders
   ↓
4. PLACED OBJECTS            ← foundations, floors, MHE equipment, guards (already done)
   ↓
5. VEHICLE OPERATORS (Phase 4) ← find vehicles from step 4, re-board
   ↓
6. PALLETS (already done, but modify for Phase 6 tierIndex)
   ↓
7. WORK QUEUE (already done)
   ↓
8. EMPLOYEES (already done, modify EmployeeRecord for Phase 5 outfit)
   ↓
9. SLOT ASSIGNMENTS (Phase 1) ← can be anytime (independent)
   ↓
10. SHIFTS (Phase 1)          ← can be anytime (independent)
   ↓
11. GUARD (Phase 7)           ← optional, safe to restore anytime
    ↓
12. INCIDENTS (Phase 8)       ← deferred, safe to restore anytime
```

---

## Known Risks & Mitigations

### Risk 1: Truck State Machine Complexity
**Problem:** TruckController is already a complex state machine (YardQueue → Gate → Docked → etc.). Adding restoration mid-state could break timing.

**Mitigation:**
- Snapshot includes `currentState` enum and `stateTimerElapsed` (how far through current state)
- On restore, place truck in exact state with timer at exact elapsed time
- TruckController.Update() resumes counting down from there
- Test: restore a truck mid-dock, verify it continues unload sequence correctly

### Risk 2: PO-Truck Assignment Fragility
**Problem:** Trucks link to POIds; if a PO is deleted/cancelled, truck's link becomes stale.

**Mitigation:**
- When restoring: verify `truck.assignedPOId` still exists in ShipmentService before trusting it
- If PO missing, either (a) orphan the truck (state = Idle) or (b) auto-cancel the truck's arrival
- Log a warning so player can investigate (e.g., "PO 5 lost during save file corruption")

### Risk 3: Operator GUID Mismatch
**Problem:** Operator GUID in VehicleOperatorSnapshot may not match any EmployeeRecord if employee was fired between save and load.

**Mitigation:**
- On restore: try to find EmployeeRecord by GUID
- If not found, either (a) leave seat empty (vehicle has no operator) or (b) assign a new hire
- Log warning so player knows operator was replaced

### Risk 4: Lane Tier Overflow
**Problem:** Pallet tier indices could be corrupted (e.g., tier 5 when lane max is 3), breaking lane routing.

**Mitigation:**
- On restore, validate `pallet.tierIndex <= lane.maxCapacity`
- If invalid, auto-assign to first available tier; log warning
- Add a "Pallet Tier Validation" step in PlacementSystem.BakeAfterDestroyFlush()

### Risk 5: Multi-Reference Consistency
**Problem:** Same pallet may be referenced in `save.pallets` (InventoryService) AND `save.dockPallets` (PalletPersistenceService). Restoring both could create duplicates.

**Mitigation:**
- `save.pallets` is inventory data (SKU, quantity, location, expiration) — one record per pallet
- `save.dockPallets` is visual GameObject data (position, rotation, cases) — same pallet
- Link them via `PalletSnapshot.palletId <-> DockPalletSnapshot.inventoryPalletId`
- On restore:
  1. Restore pallets → creates PalletMasterRecords in InventoryService
  2. Restore dock pallets → instantiates GameObjects, links to matching PalletMasterRecord by ID
- **Do NOT restore both without cross-referencing** or you'll have ghost data

### Risk 6: Domain Reload Nullification
**Problem:** If code recompiles mid-Play-session, EventManager, ServiceLocator, and all restored state nulls out (known issue per CLAUDE.md).

**Mitigation:**
- This is already a known issue; no fix needed in persistence code
- Just document: "Always stop Play Mode before modifying code. Recompiling mid-Play breaks the save."
- This applies to all 8 phases equally

---

## Testing Strategy (Per Phase)

### Phase 1 Tests
- **Slot Assignment Persist:** Place slots, assign SKUs, save, reload, verify assignments still exist
- **Shift Schedule Persist:** Define 2 shifts, save, reload, verify both shifts reappear with correct times

### Phase 2 Tests
- **PO Persistence:** Create 3 POs (one pending, one in-transit, one received), save mid-state, reload
  - Verify each PO's status still correct and queue order preserved
- **PO-Truck Linkage:** Create PO, truck spawns and docks, cargo starts offload, save, reload
  - Verify truck still docked, cargo still there, offload can resume

### Phase 3 Tests
- **Truck State Snapshot:** Truck at each state (YardQueue, Gate, Docked, Departing), save, reload
  - Verify truck in same state, timers resume counting
- **Half-Unloaded Trailer:** Dock fully loaded trailer, offload 8/12 pallets, save, reload
  - Verify 8 pallets now in lanes, 4 still in trailer, offload continues
- **Multi-Truck State:** Save with 2+ trucks (different states), reload, verify all restored independently

### Phase 4 Tests
- **Operator Persistence:** Hire ReachTruck operator, assign to truck, save, reload
  - Verify same person (by GUID) in seat, not a new hire
- **Operator Orphaning:** Hire operator, fire them, truck still exists, save, reload
  - Verify truck seat is empty (handled gracefully, no crash)

### Phase 5 Tests (Manual, visual verification)
- **Avatar Outfit Persist:** Hire employee with specific outfit (head/body/hair), save, reload
  - Verify they appear in same outfit, not re-randomized
- **Task-In-Progress:** Employee mid-putaway, save, reload
  - Verify they resume task at same location (or nearest work point)

### Phase 6 Tests (Lane Stacking)
- **Multi-Tier Lane:** Place 3 pallets in same lane (auto-stacked), save, reload
  - Verify all 3 in correct tier order, correct Y heights
- **Tier Validation:** Corrupt save file manually (set tierIndex=99), reload
  - Verify auto-fix warning logged, pallet assigned to first available tier

### Phase 7 Tests (Optional, if guard in scene)
- **Guard State:** Place guard, have guard inspect PO mid-line-items, save, reload
  - Verify guard in same position, same PO, same line-item progress

### Phase 8 Tests (Deferred)
- **Incident Log:** Create incident (pallet spoils), save, reload
  - Verify incident still in log with correct timestamp/details

---

## Estimate Summary

| Phase | Gap | Scope | Risk | Priority | Estimate |
|-------|-----|-------|------|----------|-----------|
| 1 | Slot Assignments + Shifts | Small | LOW | HIGH (unblocks UI) | 40–80 LOC |
| 2 | Customer Orders/Shipments | Large | MED | CRITICAL | 200–300 LOC |
| 3 | Trucks | Large | MED | CRITICAL | 200–280 LOC |
| 4 | Vehicle Operators | Small | LOW | MEDIUM | 60–100 LOC |
| 5 | Employee Detail State | Medium | MED | MEDIUM | 120–180 LOC |
| 6 | Multi-Pallet Lane Stacking | Medium | MED | MEDIUM | 100–150 LOC |
| 7 | Guard Presence/State | Small | LOW | LOW (optional) | 50–80 LOC |
| 8 | Contamination/Incidents | Medium | LOW | LOW (deferred) | 100–150 LOC |
| **MVP** | **Phases 1–3** | **~500 LOC** | **MED** | **CRITICAL** | **2–3 days** |
| **Full** | **All Phases** | **~1,200 LOC** | **MED** | **— | **1–2 weeks** |

---

## Recommended MVP Execution Path

### Day 1: Phase 1 + Phase 2
- Implement SlotAssignmentService.Export/Import (20 min)
- Implement ShiftManagerPanel.Export/Import (20 min)
- Implement ShipmentSnapshot + ShipmentService.Get/RestoreShipments (2 hrs)
- Modify PlacementSystem.BuildSaveData/ApplySaveData for all three (1 hr)
- Quick test: create shifts, assign slots, create 2 POs, save, reload, verify all present (30 min)

### Day 2: Phase 3
- Implement TruckSnapshot + TruckYardManager.GetAll/RestoreTrucks (2 hrs)
- Modify PlacementSystem for truck restore order (dependency on Phase 2) (45 min)
- Test: create PO, let truck dock, save mid-unload, reload, verify cargo/state (1 hr)
- Fix any edge cases found in test (30 min)

### Day 3: Integration + Polish
- Re-order ApplySaveData() to enforce dependency order (30 min)
- Add validation/error-handling for edge cases (Risk 2–4) (1 hr)
- Full end-to-end test: create PO → truck arrives → unload → save → reload → offload continues (1 hr)
- Document any deviations from this plan (30 min)

**Total MVP:** ~2–3 days. Then defer Phases 4–8 until core gameplay loop solidifies.

---

## Decision Points for Tad

1. **Multi-Pallet Lane Stacking (Phase 6):** Should lanes support 2+ tiers in MVP, or keep single-tier for Phase 1?
   - Single-tier = simpler (no new layer indexing), but unrealistic lane overflow after 1 trailer
   - Multi-tier = more complex, but enables full dock simulation
   - **Recommendation:** Start single-tier (Phase 3), add multi-tier as Phase 6 polish

2. **Guard Shack (Phase 7):** Is guard in the baseline MVP, or optional?
   - Optional per your current focus (inbound/putaway/shipping, not gate drama) → defer Phase 7
   - **Recommendation:** Defer until post-MVP

3. **Contamination Tracking (Phase 8):** Do you want basic spoilage (expires) or full incident history?
   - Basic = just the `isContaminated` flag (already persisted)
   - Full = type/timestamp/source (Phase 8)
   - **Recommendation:** Defer until rat system is built; basic spoilage handles MVP

4. **Employee Outfit Persistence (Phase 5):** Nice-to-have or necessary?
   - Nice-to-have = employees respawn, work fine, just look random → defer Phase 5
   - Necessary = players get attached to employee looks, saves feel wrong → prioritize Phase 5
   - **Recommendation:** Defer; gameplay doesn't depend on it

---

## Questions for Architecture Review

1. **ShipmentService scope:** Is it responsible for creating trucks, or does TruckYardManager do that? Need to clarify so restore order is rock-solid.

2. **Guard shack ownership:** If guard exists, is it a PlacedObject? Or a hard-coded singleton? (Affects whether it's in the placed-objects loop or needs its own restore.)

3. **Operator re-boarding:** When a vehicle is restored, should it auto-spawn its operator from the EmployeeRecord, or expect the operator restoration to manually board them? (Affects whether Phase 4 restore happens before or after vehicle restore.)

4. **Incident service:** Should contamination/spoilage be reported as incidents, or only special events (rat damage, physical damage)? (Affects what gets tracked in Phase 8.)

5. **Save file versioning:** If SaveData structure changes (adding new fields), do old save files silently ignore new fields? (They should, thanks to JsonUtility's graceful default-filling, but confirm.)

---

## Next Steps

1. **Tad reviews this plan** — approves direction, answers decision points above
2. **Phases 1–3 implementation** — start with slot assignments (confidence builder), move to orders/trucks
3. **Integration testing** — full inbound→receiving→putaway chain with save/load mid-way
4. **Phases 4–8 deferred** — revisit after core loop is solid and gameplay feels right

---

**End of Plan. Ready for architecture review & feedback.**
