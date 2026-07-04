# Core Gameplay Architecture Plan

**Status:** Ready for Review & Sign-Off  
**Date:** 2026-07-03  
**Goal:** Complete pallet/case lifecycle (Inbound → Invoicing) in 6 sequential chunks

---

## PART 1: EXISTING SYSTEMS REVIEW

### 1. Inventory System ✓ (Data Layer COMPLETE)

**Location:** `Assets/_Project/Scripts/Core/Inventory/`

**Components:**
- `InventoryService` — Manages pallets, locations, stock levels, spoilage
  - Events: `OnPalletReceived`, `OnPalletMoved`, `OnPalletPartialPicked`, `OnPalletDestroyed`, `OnSpoilageDetected`
  - Methods: `ReceivePallet()`, `MovePallet()`, `PickFromPallet()`, `CheckSpoilage()`
- `PalletData` — Pallet entity (SKU, qty, load ID, location, age, expiration)
- `SkuData` — Master product data (cost, price, shelf-life, Ti/Hi, stacking rules)
- `OrderData` / `OrderService` — Customer orders, tracking, fulfillment
- `ShipmentData` / `ShipmentService` — Inbound shipments, tracking

**Status:** Core data structures ready. Missing: UI/service integrations for inbound/outbound flows.

---

### 2. Staffing System ✓ (Employee Framework COMPLETE)

**Location:** `Assets/_Project/Scripts/Actors/EmployeeSystem/`

**Components:**
- `EmployeeRole` enum — 13 roles defined
  - **Directly relevant:** OrderSelector, ReachTruckOperator, DockStockerOperator, Loader, Receiver
  - All have wage tiers, performance metrics (CPH/PPH)
- `EmployeeData` — Employee stats, role, skill level
- `EmployeeRecord` — Runtime employee instance, active/former, wage tracking
- `EmployeeAssignmentService` — Manual assignment (Patrol, DriveReach, DriveDockStalker, OrderSelection)
- `EmployeeSpawner` — Spawns employee prefabs, handles MHE (Reach Truck, Dock Stocker) equipment

**Status:** Role/assignment framework ready. Missing: Work queue task assignments (currently manual via UI).

---

### 3. Staging Lanes & Locations ✓ (Basic System Ready)

**Location:** `Assets/_Project/Scripts/Gameplay/`

**Components:**
- `StagingLane` — Physical floor zones for pallets
  - Supports: Receiving, Shipping, Storage, Buffer types
  - Tracks: pallet count, capacity, visual color
- `PalletInventoryTracker` — Bridges physical placed pallets to inventory data
  - Syncs: PlacedObject ↔ PalletData
  - Auto-creates/updates PalletData when pallets move
- `DockSlot` / `DockNumberingService` — Dock door numbering (1-12 per design)
  - Persistent door numbers via `PlacedObject.customData`
  - Handles: placement, deletion, renumbering

**Status:** Basic structure ready. Missing: Per-door outbound staging lane assignments, replenishment logic.

---

### 4. Truck/Trailer System ✓ (Visual + Guard Shack)

**Location:** `Assets/_Project/Scripts/Gameplay/`

**Components:**
- `TruckYardManager` — Truck spawning, guard shack coordination
- `TrailerArrival` / `TrailerDeparture` — Animations, events
- Existing: Trailer backs into door, door opens, PO exchange with guard

**Status:** Trailer mechanics exist. Missing: PO matching to trailer, inbound pallet spawning.

---

### 5. Labor Management ⚠️ (GAPS TO FILL)

**Current State:**
- Manual role assignment (UI buttons → EmployeeAssignmentService)
- No automated task queue
- No work-queue-to-employee binding

**What Needs to Be Built:**
- `WorkQueueSystem` — Central queue of pending tasks
- `WorkQueueEntry` — Individual task (type, assigned role, priority, requester)
- Work Queue → Task Assignment → Employee Notification flow
- Performance tracking (tasks completed, time taken)

---

## PART 2: THE 6 CHUNKS (Detailed Architecture)

### CHUNK 1: INBOUND PROCESS

**Objective:** Truck arrives → Pallets offloaded → Received → Inventory entities created with Load IDs

**Flow Diagram:**
```
PO Created (POSystem)
  ↓
Trailer Arrives (12 Chep Pallets)
  ↓ [TrailerArrivalEvent]
Guard Receives PO from Driver
  ↓
Guard Assigns PO to Trailer (PO ↔ Trailer match)
  ↓
Trailer Backs Into Door
  ↓ [Box Collider trigger on door exterior]
Door Opens (ManDoor or RollupDoor)
  ↓
Dock Stocker Offloads (12 pallets)
  ├─ Mounts pallet jack
  ├─ Enters trailer
  ├─ Picks up pallet #1-12
  ├─ Backs out, stages in inbound staging lane
  ↓
Receiver Receives (Per pallet)
  ├─ Animation: walks with clipboard + RF gun
  ├─ Receives pallet
  ├─ Load ID created (10-digit license plate)
  ├─ PalletData created in InventoryService
  ├─ Fires: InventoryService.OnPalletReceived(pallet)
  ↓
Work Queue Entry Created
  ├─ Type: "Putaway"
  ├─ Pallet: [Load ID]
  ├─ Source: Inbound Staging
  ├─ Status: Pending
  ↓
Trailer Departs (once all unloaded)
```

**New Services/Components:**

1. **POSystem** (`Assets/_Project/Scripts/Core/PO/POSystem.cs`)
   - `GeneratePO(vendor, skuId, qty)` → PO object created
   - `GetPOByNumber(poNumber)` → lookup
   - Event: `OnPOCreated(poNumber, details)`
   - Stores: PO number, vendor, SKU, qty, arrival time, cost

2. **TrailerArrivalService** (`Assets/_Project/Scripts/Gameplay/TrailerArrivalService.cs`)
   - `OnTrailerArrival(poNumber, doorSlot)` — matches PO to trailer
   - Validates: PO exists, trailer capacity matches qty
   - Spawns: 12 Chep pallets inside trailer (visual only, no physics)
   - Spawns: 12 blank placeholder pallets (temp entities, will be received)
   - Events: `OnTrailerReadyForUnload(trailer, door, poNumber)`

3. **DoorColliderTrigger** (`Assets/_Project/Scripts/Gameplay/DoorColliderTrigger.cs`)
   - Box Collider on EXTERIOR of door
   - `OnTriggerEnter(trailer)` → Opens door (only if trailer present)
   - `OnTriggerExit(trailer)` → Does NOT close (manual close or timeout)
   - Prevents: Opening door from inside

4. **DockStockerUnloadTask** (part of WorkQueueSystem)
   - Signals DockStockerOperator: "Unload trailer at door X"
   - Once all 12 pallets staged: task closes
   - Updates: `PlacedObject` positions in inbound lane

5. **ReceiverAnimationService** (`Assets/_Project/Scripts/Actors/ReceiverAnimationService.cs`)
   - Animation: Walks to pallet with clipboard + RF gun
   - Scans pallet (animation)
   - Audio: Beep/scan SFX
   - Duration: ~2-3 seconds per pallet × 12 = 24-36 seconds total
   - Once complete: Load ID assigned, PalletData created, putaway work queue entry

6. **LoadIDGenerator** (`Assets/_Project/Scripts/Core/Inventory/LoadIDGenerator.cs`)
   - `GenerateLoadID()` → 10-digit unique ID
   - Format: `[timestamp][random]` or `[sequence]` (TBD)
   - Stored in: `PalletData.loadId`

7. **WorkQueueSystem** (NEW — foundation for entire core loop)
   - `WorkQueueEntry` — Task object
     - `id` (unique)
     - `type` (enum: Unload, Putaway, Replenish, Pick, Load, Receive)
     - `requiredRole` (EmployeeRole)
     - `priority` (int, for sorting)
     - `targetPallet` (PalletData.loadId OR null)
     - `targetLocation` (Vector2Int OR door number OR lane)
     - `status` (Pending, Assigned, InProgress, Complete)
     - `assignedEmployee` (EmployeeIdentity OR null)
   - `WorkQueueManager` (singleton)
     - `AddEntry(WorkQueueEntry)`
     - `AssignToEmployee(entry, employee)`
     - `CompleteEntry(entry)`
     - `GetPendingByRole(role)` → ordered list
     - Events: `OnWorkQueueEntryCreated`, `OnWorkQueueEntryAssigned`, `OnWorkQueueEntryCompleted`

**Dependencies:**
- GameContext, ServiceLocator (existing)
- EventManager (existing)
- InventoryService (existing)
- TruckYardManager (existing)
- ManDoor / RollupDoor controllers (existing)

**Data Created:**
- `PalletData` (12 instances, one per inbound pallet)
- `WorkQueueEntry` (at least 1: putaway task)
- `LoadID` (12 instances, one per pallet)

**Output Validation:**
- [ ] 12 pallets in inbound staging lane
- [ ] Each pallet has a unique Load ID
- [ ] Each pallet has a PalletData record in InventoryService
- [ ] Work queue has "Putaway" task for each pallet (or 1 batch task)
- [ ] PalletInventoryTracker synced (placed pallet ↔ InventoryService)

---

### CHUNK 2: PUTAWAY PROCESS

**Objective:** Staging → Reserve/Pick Slots (Reach Truck Operator)

**Flow Diagram:**
```
Putaway Work Queue Entry Created (from Chunk 1)
  ↓
Reach Truck Operator Receives Signal
  ├─ WorkQueueManager.OnWorkQueueEntryCreated event
  ├─ If role == ReachTruckOperator: notify (UI toast, audio cue, task list update)
  ↓
Operator Drives Pallet Jack to Inbound Staging
  ├─ AiNavigation.SeekEquipment(reachTruckSlot)
  ├─ Boards reach truck
  ├─ AiNavigation.NavigateTo(inboundStagingLane)
  ↓
Operator Picks Up Pallet
  ├─ Animation: forklift engages, lifts pallet
  ├─ Pallet parent: set to reach truck
  ↓
Operator Drives to Assigned Slot
  ├─ PutawayLogic determines: Pick Slot OR Reserve Slot?
  │   ├─ If order pending for SKU AND pick slot empty → Pick Slot
  │   └─ Otherwise → Reserve Slot (default)
  ├─ AiNavigation.NavigateTo(targetSlot)
  ↓
Operator Sets Down Pallet
  ├─ Animation: forklift lowers, disengages
  ├─ Pallet parent: unparent, set position to slot
  ├─ InventoryService.MovePallet(pallet, newLocation)
  ├─ WorkQueueManager.CompleteEntry(putawayTask)
  ↓
[Repeat for all 12 pallets]
```

**New Services/Components:**

1. **PutawayLogic** (`Assets/_Project/Scripts/Gameplay/PutawayLogic.cs`)
   - `DeterminePutawayLocation(palletData, orderService)` → PickSlot OR ReserveSlot
   - Decision tree:
     ```
     if (orderService.IsOrderPendingForSKU(pallet.sku))
       if (PickSlot.IsEmpty(sku))
         return PickSlot(sku)
       else
         return ReserveSlot(sku)
     else
       return ReserveSlot(sku)  // default
     ```

2. **PickSlot** (Component on rack cells)
   - `Location` (Vector2Int)
   - `OccupantSKU` (SkuData ID)
   - `IsEmpty` (bool)
   - `CurrentPallet` (PalletData.loadId)
   - `ReplenishmentThreshold` (default 2 cases)
   - Public: `SetPallet(pallet)`, `GetPallet()`, `Clear()`

3. **ReserveSlot** (Component on reserve racks)
   - Similar structure to PickSlot
   - `Location` (Vector2Int)
   - Visually distinct (different color/label)

4. **PutawayTask** (type of WorkQueueEntry)
   - Role: ReachTruckOperator
   - Source location: Inbound Staging Lane
   - Target location: PickSlot OR ReserveSlot (computed at assignment)
   - Completion: when pallet set down at target

**Dependencies:**
- EmployeeAssignmentService (existing)
- AiNavigation (existing)
- MHE equipment (ReachTruck — existing)
- InventoryService (existing)
- OrderService (from Chunk 3+)
- WorkQueueSystem (from Chunk 1)
- EventManager (existing)

**Data Updated:**
- `PalletData.location` → changed from inbound to pick/reserve slot
- `PickSlot.currentPallet` → assigned pallet
- `WorkQueueEntry.status` → Complete

**Output Validation:**
- [ ] All 12 pallets moved from inbound staging
- [ ] Pallets distributed to pick or reserve slots
- [ ] InventoryService location tracking updated
- [ ] No pallets left in inbound staging
- [ ] PalletInventoryTracker synced

---

### CHUNK 3: REPLENISHMENT PROCESS

**Objective:** Pick Slot Low/Empty → Fill from Reserve

**Flow Diagram:**
```
Pick Slot Monitored for Occupancy
  ↓
Daily/Hourly Check: replenishment trigger?
  ├─ if (PickSlot.CurrentPalletQty < ReplenishmentThreshold)
  │   └─ Trigger: ReplenishmentNeeded event
  ↓
Work Queue Entry Created
  ├─ Type: "Replenish"
  ├─ Target: PickSlot location
  ├─ Required Role: ReachTruckOperator
  ├─ Status: Pending
  ↓
Reach Truck Operator Assigned
  ├─ WorkQueueManager.AssignToEmployee
  ├─ Operator notified (toast, task list)
  ↓
Operator Drives to Reserve Slot
  ├─ ReserveSlotFinder.FindBySKU(pickSlot.SKU) → reserve slot
  ├─ AiNavigation.NavigateTo(reserveSlot)
  ↓
Operator Picks Up Pallet from Reserve
  ├─ Animation: lifts pallet
  ├─ Pallet parent: reach truck
  ↓
Operator Drives to Pick Slot
  ├─ AiNavigation.NavigateTo(pickSlot)
  ↓
Operator Sets Down Pallet
  ├─ Animation: lowers, disengages
  ├─ InventoryService.MovePallet(pallet, pickSlotLocation)
  ├─ PickSlot.SetPallet(pallet)
  ├─ ReserveSlot.Clear()
  ├─ WorkQueueManager.CompleteEntry(replenishTask)
  ↓
[Repeat as needed throughout day]
```

**New Services/Components:**

1. **PickSlotReplenishmentMonitor** (`Assets/_Project/Scripts/Gameplay/PickSlotReplenishmentMonitor.cs`)
   - Runs on schedule (daily, or event-driven)
   - For each PickSlot:
     - Check: `currentPallet.qty < threshold`
     - If yes: Fire `OnReplenishmentNeeded(pickSlot)`
   - Threshold: configurable, default 2 cases

2. **ReserveSlotFinder** (`Assets/_Project/Scripts/Gameplay/ReserveSlotFinder.cs`)
   - `FindBySKU(skuId)` → list of reserve slots holding this SKU, ordered by distance
   - Returns: nearest slot with full pallet of requested SKU

3. **ReplenishmentTask** (type of WorkQueueEntry)
   - Role: ReachTruckOperator
   - Source location: ReserveSlot (computed dynamically)
   - Target location: PickSlot (known)
   - Completion: when pallet set down at pick slot

**Dependencies:**
- PickSlot component (from Chunk 2)
- ReserveSlot component (from Chunk 2)
- InventoryService (existing)
- WorkQueueSystem (from Chunk 1)
- AiNavigation (existing)
- MHE equipment (ReachTruck — existing)

**Data Updated:**
- `PickSlot.currentPallet` → new full pallet
- `ReserveSlot.currentPallet` → cleared
- `PalletData.location` → moved
- `WorkQueueEntry.status` → Complete

**Output Validation:**
- [ ] Pick slots stay stocked (qty ≥ threshold at all times)
- [ ] Replenishment triggered correctly
- [ ] Reserve slots consumed in order (FIFO by distance)
- [ ] No order selector waits for empty pick slot

---

### CHUNK 4: ORDER SELECTION PROCESS

**Objective:** Orders → Pick → Stage (Order Selector)

**Flow Diagram:**
```
Customer Order Arrives
  ├─ OrderService.GenerateOrder(customerId, skuId, qty)
  ├─ E.g., "12 pallets of SKU 035-12345"
  ↓
Empty Outbound Trailer Requested
  ├─ TruckYardManager.RequestEmptyTrailer()
  ├─ Trailer backed into empty door
  ↓
Outbound Staging Lane Assigned to Door
  ├─ OutboundStagingLane created/associated with door
  ├─ Order linked to staging lane
  ↓
Work Queue Entry Created
  ├─ Type: "OrderSelection"
  ├─ Order ID: reference to OrderData
  ├─ Required Role: OrderSelector
  ├─ Status: Pending
  ├─ TargetLane: outbound staging lane for this door
  ↓
Order Selector Receives Signal
  ├─ Notified: "Order #123 ready for selection"
  ├─ Shows: which pick slots contain order SKU
  ↓
[Per pallet needed for order (repeat):]
Selector Drives to Pick Slot
  ├─ AiNavigation.NavigateTo(pickSlot)
  ↓
Selector Picks Cases (Hand Picking)
  ├─ Animation: reaches, grabs case, places on jack
  ├─ Audio: crates/case noise
  ├─ InventoryService.PickFromPallet(pallet, qty)
  ├─ Cases added to selector's pallet jack (visual)
  ├─ If pallet now empty: InventoryService.DestroyPallet(pallet)
  ↓
Selector's Jack Load Check
  ├─ if (jack.loadCount >= 2 pallets)
  │   └─ Drive to staging lane (full)
  ├─ else if (all order cases picked)
  │   └─ Drive to staging lane (partial/final)
  ↓
Selector Stages Pallets
  ├─ AiNavigation.NavigateTo(outboundStagingLane)
  ├─ Animation: places pallet in lane
  ├─ Pallet parent: staging lane
  ├─ StagingLane.AddPallet(pallet)
  ├─ OrderData.AddStagedPallet(pallet)
  ↓
[Repeat until all order items staged]
↓
Order Status Updated
  ├─ Status: "Staged, ready to load"
  ├─ WorkQueueManager.CompleteEntry(orderSelectionTask)
  ↓
Next Work Queue Entry Created
  └─ Type: "LoadOrder" (for Chunk 5)
```

**New Services/Components:**

1. **OutboundStagingLane** (extends StagingLane)
   - Per-door staging area
   - Linked to: Order + Trailer + Door number
   - Tracks: pallets staged for this order
   - Validates: no overflow (capacity limit)

2. **OrderSelectionTask** (type of WorkQueueEntry)
   - Role: OrderSelector
   - Source locations: Pick slots (per order SKU)
   - Target location: Outbound staging lane
   - Tracks: progress (cases picked / cases needed)

3. **OrderSelectorAnimation** (new MonoBehaviour on OrderSelector employee)
   - Hand-picking animation: reach → grab → place on jack
   - Jack-loading animation: visual pallet stack grows
   - Plays when `PickFromPallet()` called

4. **OrderProgressTracker** (part of OrderData)
   - `totalCasesNeeded` (from order)
   - `casesPicked` (running total)
   - `palletsStagedCount` (running total)
   - `IsComplete` → (casesPicked >= casesNeeded)

**Dependencies:**
- OrderService (existing)
- InventoryService (existing)
- PickSlot (from Chunk 2)
- OutboundStagingLane (new)
- WorkQueueSystem (from Chunk 1)
- AiNavigation (existing)
- Employee animation system (existing)
- TruckYardManager (existing)

**Data Created/Updated:**
- `OutboundStagingLane` instances (1 per active order/door)
- `OrderData.stagedPallets` → populated
- `OrderData.status` → "Staged"
- `InventoryService`: pallets moved to staging lane location

**Output Validation:**
- [ ] All order cases picked from pick slots
- [ ] Picked cases staged in correct lane
- [ ] Order marked "Staged"
- [ ] No overflow in staging lane
- [ ] Order selector max 2 pallets per trip honored

**NOTE:** This chunk involves significant animation work. **Tad will be deeply involved** in refining picking animation, case visual stack, etc.

---

### CHUNK 5: SHIPPING PROCESS

**Objective:** Load → Depart

**Flow Diagram:**
```
Order Staged (from Chunk 4)
  ↓
Work Queue Entry Created
  ├─ Type: "LoadOrder"
  ├─ Order ID: reference
  ├─ Target door: where trailer is backed
  ├─ Status: Pending
  ↓
Loader Receives Signal
  ├─ "Order #123 ready for loading — 12 pallets"
  ├─ Notified via toast/task list
  ↓
Loader Drives to Outbound Staging Lane
  ├─ AiNavigation.NavigateTo(stagingLane)
  ↓
[Per pallet in order (repeat):]
Loader Picks Up Pallet
  ├─ Animation: walks, engages pallet, lifts
  ├─ Pallet parent: loader's hands/truck bed
  ↓
Loader Walks to Trailer
  ├─ AiNavigation.NavigateTo(door)
  ├─ Enters trailer
  ↓
Loader Sets Down Pallet in Trailer
  ├─ Animation: places, releases
  ├─ Pallet parent: trailer (visual position inside)
  ├─ TrailerLoadingZone.AddPallet(pallet)
  ├─ OrderData.AddLoadedPallet(pallet)
  ↓
[Repeat until all pallets loaded]
↓
Truck Status Updated
  ├─ Status: "Loaded, ready to depart"
  ├─ LoadedPalletCount: 12/12 ✓
  ├─ WorkQueueManager.CompleteEntry(loadTask)
  ↓
Driver Departs
  ├─ Animation: trailer pulled away
  ├─ TruckYardManager.DepartureProcedure()
  ├─ Order Status: "Shipped"
  ├─ Fire: OnOrderShipped(order)
  ↓
Next Work Queue Entry Created
  └─ Type: "Invoice" (for Chunk 6)
```

**New Services/Components:**

1. **LoaderTask** (type of WorkQueueEntry)
   - Role: Loader
   - Source location: Outbound staging lane
   - Target: Trailer at specific door
   - Tracks: pallets loaded / pallets needed

2. **TrailerLoadingZone** (Component on trailer interior)
   - Tracks: pallet slots (1-12)
   - `IsFull` → loadedCount >= 12
   - `AddPallet(pallet)` → visual + data
   - Events: `OnTrailerFilled`, `OnTrailerPartiallyLoaded`

3. **LoaderAnimation** (new MonoBehaviour on Loader)
   - Walking animation: from staging → trailer
   - Lifting animation: picks pallet
   - Placing animation: sets in trailer
   - Repeat 12×

4. **OrderDepartureHandler** (part of TruckYardManager)
   - On trailer full (or timer): `InitiateDeparture(trailer)`
   - Animation: truck pulls away
   - Triggers: `OnOrderShipped(order)` event

**Dependencies:**
- OrderService (existing)
- TruckYardManager (existing)
- Loader role (existing)
- OutboundStagingLane (from Chunk 4)
- WorkQueueSystem (from Chunk 1)
- AiNavigation (existing)
- Animation system (existing)

**Data Updated:**
- `OrderData.status` → "Shipped"
- `OrderData.loadedPallets` → populated
- `TrailerLoadingZone.palletCount` → incremented
- Order moved out of game world (visual only)

**Output Validation:**
- [ ] All 12 pallets loaded into trailer
- [ ] Trailer departs when full
- [ ] Order marked "Shipped"
- [ ] No pallet left in staging lane

---

### CHUNK 6: INVOICING PROCESS

**Objective:** Charge Customer

**Flow Diagram:**
```
Order Shipped (from Chunk 5)
  ↓ [OnOrderShipped event]
Invoicing System Triggered
  ↓
Calculate Charges
  ├─ Cost of Goods (COGS)
  │   └─ unit_cost × case_count
  ├─ Handling Fee
  │   └─ $1/case × case_count
  ├─ Subtotal: COGS + Handling
  ├─ Taxes (if applicable, else $0)
  └─ Total: Subtotal + Taxes
  ↓
Invoice Generated
  ├─ OrderID, CustomerID
  ├─ Line items (SKU, qty, unit price, extended)
  ├─ Subtotal, handling fee, total
  ├─ Date/time, terms (Prepaid assumed)
  ↓
Revenue Credited
  ├─ MoneyService.AddCapital(totalAmount, "Sales — Order #123")
  ├─ FinanceCategory: "Sales"
  ├─ GL_Line: "Sales" OR per-category breakdown (TBD)
  ├─ PublishEvent: GameEvents.Economy.OnRevenueRecognized
  ↓
Order Marked "Invoiced"
  ├─ OrderData.status = "Invoiced"
  ├─ OrderData.invoiceDate = now
  ├─ OrderData.totalRevenue = amount
  ↓
Work Queue Entry Closed
  └─ Type: "Invoice" (completed)
  ↓
Daily Summary Updated
  ├─ TopBar: Capital panel shows new balance
  ├─ Hourly tab: Sales revenue rolled up
  ├─ Spent Today panel: (no impact — revenue only)
```

**New Services/Components:**

1. **InvoicingService** (`Assets/_Project/Scripts/Core/Economy/InvoicingService.cs`)
   - `GenerateInvoice(order)` → InvoiceData
   - `CalculateCharges(order)` → { cogs, handlingFee, subtotal, taxes, total }
   - `CreditRevenue(amount, orderId)` → MoneyService
   - Events: `OnInvoiceGenerated`, `OnRevenueRecognized`

2. **InvoiceData** (data class)
   - orderId, customerId
   - lineItems (SKU, qty, unitPrice, extended)
   - subtotal, handlingFee, taxes, total
   - dateGenerated, paymentMethod

3. **OrderCostCalculator** (utility)
   - `CalculateCOGS(order)` → total unit cost × qty
   - `CalculateHandlingFee(qty)` → qty × $1/case
   - `CalculateTaxes(subtotal)` → subtotal × taxRate (or $0)

**Dependencies:**
- OrderService (existing)
- MoneyService (existing)
- FinanceCategory system (existing)
- EventManager (existing)

**Data Updated:**
- `OrderData.status` → "Invoiced"
- `OrderData.invoiceDate`, `totalRevenue`
- `MoneyService.Capital` → incremented
- Financial reporting updated

**Output Validation:**
- [ ] Revenue correctly calculated
- [ ] Money added to player capital
- [ ] TopBar capital panel reflects new balance
- [ ] Order closed and archived

---

## PART 3: IMPLEMENTATION DEPENDENCIES & SEQUENCING

### Dependency Tree

```
CHUNK 1: Inbound
  ├─ NEW: POSystem
  ├─ NEW: TrailerArrivalService
  ├─ NEW: DoorColliderTrigger
  ├─ NEW: ReceiverAnimationService
  ├─ NEW: LoadIDGenerator
  ├─ NEW: WorkQueueSystem ⭐ (CRITICAL — used by all chunks)
  ├─ EXISTING: GameContext, InventoryService, TruckYardManager

CHUNK 2: Putaway
  ├─ NEW: PutawayLogic
  ├─ NEW: PickSlot component
  ├─ NEW: ReserveSlot component
  ├─ EXISTING: WorkQueueSystem (from Chunk 1) ⭐
  ├─ EXISTING: InventoryService, EmployeeAssignmentService

CHUNK 3: Replenishment
  ├─ NEW: PickSlotReplenishmentMonitor
  ├─ NEW: ReserveSlotFinder
  ├─ EXISTING: PickSlot, ReserveSlot (from Chunk 2)
  ├─ EXISTING: WorkQueueSystem (from Chunk 1) ⭐

CHUNK 4: Order Selection
  ├─ NEW: OutboundStagingLane
  ├─ NEW: OrderSelectorAnimation
  ├─ NEW: OrderProgressTracker
  ├─ EXISTING: PickSlot (from Chunk 2)
  ├─ EXISTING: WorkQueueSystem (from Chunk 1) ⭐
  ├─ EXISTING: OrderService, InventoryService

CHUNK 5: Shipping
  ├─ NEW: TrailerLoadingZone
  ├─ NEW: LoaderAnimation
  ├─ NEW: OrderDepartureHandler
  ├─ EXISTING: OutboundStagingLane (from Chunk 4)
  ├─ EXISTING: WorkQueueSystem (from Chunk 1) ⭐

CHUNK 6: Invoicing
  ├─ NEW: InvoicingService
  ├─ NEW: OrderCostCalculator
  ├─ EXISTING: MoneyService, FinanceCategory (existing)
  ├─ EXISTING: OrderService
```

### Critical Path

**WorkQueueSystem MUST be built first in Chunk 1.** It is the backbone for all subsequent chunks. Without it:
- No automated task assignments
- No employee notifications
- No progress tracking

### Suggested Build Order

1. **Chunk 1: Inbound** (1-2 days)
   - Includes WorkQueueSystem (critical)
   - Establishes PO ↔ Trailer matching
   - First inventory entities created
   - **Gate:** Unload 12 pallets, receive them, create work queue entries

2. **Chunk 2: Putaway** (1 day)
   - Uses WorkQueueSystem + InventoryService
   - Distributes pallets to storage
   - **Gate:** All 12 pallets in pick/reserve slots

3. **Chunk 3: Replenishment** (½ day)
   - Lightweight, builds on Chunk 2
   - Keeps pick slots stocked
   - **Gate:** Pick slots auto-filled when low

4. **Chunk 4: Order Selection** (2 days)
   - Complex animations
   - **Tad's heavy involvement**
   - Hand-picking mechanics
   - **Gate:** Order cases picked and staged

5. **Chunk 5: Shipping** (1 day)
   - Loading animations + departure
   - **Gate:** Trailer loads and departs

6. **Chunk 6: Invoicing** (½ day)
   - Revenue crediting
   - **Gate:** Player receives payment

---

## PART 4: TESTING GATES

Each chunk has a gate that must pass before moving to the next:

### CHUNK 1 GATE
- [ ] PO created in POSystem
- [ ] Trailer arrives and matches PO
- [ ] 12 pallets spawn in trailer (visual)
- [ ] Guard assigns PO to trailer
- [ ] Door opens on trailer collision (external trigger only)
- [ ] Dock stocker offloads all 12 pallets to inbound staging
- [ ] Receiver animation plays per pallet
- [ ] 12 Load IDs generated and unique
- [ ] 12 PalletData records created in InventoryService
- [ ] 12 Putaway work queue entries created

### CHUNK 2 GATE
- [ ] All 12 pallets move from inbound to pick/reserve slots
- [ ] PickSlot component properly tracks occupancy
- [ ] PalletData location updated correctly
- [ ] PalletInventoryTracker synced

### CHUNK 3 GATE
- [ ] Pick slot replenishment triggers correctly
- [ ] Reach truck operator fills from reserve
- [ ] Pick slots stay at/above threshold

### CHUNK 4 GATE
- [ ] Order created in OrderService
- [ ] Outbound trailer backed in, staging lane assigned
- [ ] Order selector picks cases
- [ ] Max 2-pallet jack capacity enforced
- [ ] Cases properly removed from pick pallets
- [ ] All order cases staged in correct lane
- [ ] Pick pallets emptied and destroyed

### CHUNK 5 GATE
- [ ] Loader picks staged pallets
- [ ] Loader places in trailer
- [ ] Trailer fills to 12/12
- [ ] Trailer departs (visual animation)
- [ ] Order marked "Shipped"

### CHUNK 6 GATE
- [ ] Invoice generated on order shipment
- [ ] Revenue calculated correctly (COGS + $1/case)
- [ ] Capital credited to player
- [ ] TopBar reflects new balance
- [ ] Order marked "Invoiced"

---

## PART 5: KNOWN UNKNOWNS & DECISIONS

**To be finalized with Tad:**

1. **Work Queue Priority** — FIFO initially, or by order due date?
2. **Multiple Trailers** — Support simultaneous trailers at multiple doors? (design for it now or later?)
3. **Partial Orders** — Can customer orders be split across multiple shipments? (not required for MVP)
4. **Animation Durations** — Receiver (~2s per pallet), picker (variable), loader (variable)
5. **Staging Lane Capacity** — How many pallets per outbound lane?
6. **Order Batching** — Do multiple orders wait for a full truck, or depart separately?
7. **Taxes** — Include tax calculation in invoicing, or flat $0?

---

## Summary

This plan breaks the core gameplay loop into 6 sequential, testable chunks:

1. **Inbound** — Trucks arrive, pallets received, inventory created
2. **Putaway** — Pallets distributed to storage
3. **Replenishment** — Pick slots refilled from reserve
4. **Order Selection** — Pallets picked and staged
5. **Shipping** — Trailer loaded and departs
6. **Invoicing** — Customer charged

Each chunk has clear dependencies, specific components to build, and a testing gate to validate completion.

**Next Step:** Review, feedback, sign-off → proceed to Chunk 1 implementation.

---

**Ready for your review, Tad. Feedback on architecture, unknowns, or sequencing?**
