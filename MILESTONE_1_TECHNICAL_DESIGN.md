# Milestone 1: Receiving & Putaway — Technical Design

**Completed:** 2026-06-26  
**Status:** Architecture ready for implementation  
**Duration:** 1-2 weeks implementation  

---

## Overview

Milestone 1 implements the first two phases of the gameplay loop:
1. **Receiving** — Shipments arrive; dock worker scans and authorizes goods
2. **Putaway** — Dock stocker/reach operator moves pallets from receiving to storage locations

This is the foundational milestone — everything downstream (picking, shipping, revenue) depends on a solid inventory system.

---

## Core Architecture

### Data Layer

**New Classes Created:**

| Class | Purpose | Location |
|-------|---------|----------|
| `PalletData` | Individual pallet entity (SKU, qty, location, age, expiration) | `Core/Inventory/PalletData.cs` |
| `SkuData` (SO) | Master product data (cost, price, shelf life, stacking) | `Core/Inventory/SkuData.cs` |
| `ShipmentData` | Inbound shipment from supplier | `Core/Inventory/ShipmentData.cs` |
| `ShipmentLineItem` | Single SKU in a shipment | `Core/Inventory/ShipmentData.cs` |
| `OrderData` | Customer order with fulfillment tracking | `Core/Inventory/OrderData.cs` |
| `OrderLineItem` | Single SKU in a customer order | `Core/Inventory/OrderData.cs` |

### Service Layer

**InventoryService** (`Core/Inventory/InventoryService.cs`)

Core inventory tracking and pallet management. Registered with ServiceLocator; initialized in GameContext.Awake().

**Public API:**
```csharp
// Receiving
List<PalletData> ReceiveShipment(List<(string skuId, int quantity, int expirationDayOffset)> items);

// Putaway / Movement
bool MovePallet(string palletId, Vector2Int newLocation);

// Order Picking
int PickFromPallet(string palletId, int quantityToRemove);
void DestroyPallet(string palletId);

// Inventory Queries
int GetTotalUnitsBySku(string skuId);
List<PalletData> GetPalletsAtLocation(Vector2Int location);
List<PalletData> GetPalletsBySku(string skuId);
List<PalletData> GetReceivingPallets(); // At (0,0)
bool HasCapacityAtLocation(Vector2Int location, ObjDataSO storageData = null);
```

**Events Published:**
```csharp
OnPalletReceived(PalletData)
OnPalletMoved(PalletData, Vector2Int from, Vector2Int to)
OnPalletPartialPicked(PalletData, int quantityRemoved)
OnPalletDestroyed(PalletData)
OnSpoilageDetected(PalletData)
```

**Design Notes:**
- Pallets stored as pure data in dictionaries (not PlacedObjects)
- Lightweight; flexible; scalable to large inventories
- Indexed by pallet ID and by location for fast queries
- Daily spoilage check via `OnDayChanged` event
- Integrates with SimulationTimeService for day-based expiration

### Storage Location System

**Current Implementation (MVP):**
- Receiving staging area: **Vector2Int.zero (0, 0)**
- All other storage: grid cells where PlacedObjects (racks, shelves, bins) are placed
- Capacity checking: TODO (placeholder allows 4 pallets per cell for MVP)

**Future Enhancements:**
- Actual capacity from storage object footprint
- ABC analysis for fast-mover positioning
- Stacking rules enforcement (fragile, heavy, cold-storage-only)

---

## Services Yet to Be Created (Next Tasks)

### 1. ShipmentService (NEW)
**Responsibility:** Manage inbound shipments queue, trigger receiving tasks

**Design:**
```csharp
public class ShipmentService : IService
{
    // Active shipments in queue
    List<ShipmentData> incomingShipments;
    
    // Generate sample shipments for testing
    ShipmentData GenerateShipment(int dayNumber, int timeMinute);
    
    // Check if any shipments arrived this minute, publish event
    void CheckArrivals(int dayNumber, int timeMinute);
    
    // Mark shipment as received
    void ReceiveShipment(string shipmentId);
    
    // Event: OnShipmentArrived(ShipmentData)
}
```

**Integration Points:**
- TimeService: Subscribe to minute/hour/day tick
- InventoryService: Call ReceiveShipment() to create pallets
- Task system: Publish task to assign Receiver role

### 2. OrderService (NEW)
**Responsibility:** Manage customer order queue, trigger picking tasks

**Design:**
```csharp
public class OrderService : IService
{
    // Queue of pending orders
    List<OrderData> orderQueue;
    
    // Generate sample orders for testing
    OrderData GenerateOrder(int dayNumber, int timeMinute);
    
    // Check if orders arrived, publish event
    void CheckArrivals(int dayNumber, int timeMinute);
    
    // Mark items as picked for an order line
    void RecordPick(string orderId, string skuId, int quantityPicked);
    
    // Check if order is fully picked, auto-transition to Staged
    void UpdateOrderStatus(string orderId);
    
    // Event: OnOrderArrived(OrderData)
    // Event: OnOrderStatusChanged(OrderData)
}
```

**Integration Points:**
- TimeService: Subscribe to time tick
- InventoryService: Query stock levels, call PickFromPallet()
- Task system: Assign Picker/Selector tasks

### 3. Task System Extension (EXISTING + NEW)
**Current State:**
- EmployeeAssignmentService exists (assigns roles to employees)
- AiNavigation exists (pathfinding for employees)

**What's Needed:**
- Task queue (FIFO or priority-based)
- Task types: `Receive`, `Putaway`, `Pick`, `Load`
- Task data: SKU, quantity, source location, destination location
- Task assignment to roles: Receiver, DockStocker, ReachOperator, Picker, Loader
- Task completion detection: pallet moved, order picked, truck loaded
- Publishing to employee AI for execution

**Design (Outline):**
```csharp
public abstract class WarehouseTask
{
    string TaskId;
    WarehouseTaskType Type;
    EmployeeIdentity AssignedEmployee;
    TaskStatus Status; // Pending, InProgress, Completed
    
    // Called when employee claims task
    abstract void Execute();
    
    // Called when task criteria met (pallet moved, items picked, etc.)
    abstract void CheckCompletion();
}

enum WarehouseTaskType { Receive, Putaway, Pick, Load }
```

---

## UI/UX Requirements

### Receiving UI (TO BUILD)
**Dock Display:**
- "Shipment Arrived" notification with supplier name
- Inbound list: SKU, quantity, cost per unit
- "Scan & Receive" button → triggers ReceiveShipment()
- Visual feedback: goods appear in receiving staging area (0, 0)

**Receiving Staging Display:**
- Shows all pallets at Vector2Int.zero
- Pallet card: SKU, quantity, received time
- Status: "Ready for Putaway"

### Putaway UI (TO BUILD)
**Task Assignment:**
- "Putaway queue: 3 pallets waiting"
- Task card: Pallet#ID, SKU, qty, source (0,0), destination (???)
- "Assign to [Dock Stocker / Reach Operator]" button
- Drag-and-drop or selection to pick destination location

**Visual Feedback:**
- Highlight available storage locations (have capacity)
- Show pallet at source location (receiving area)
- Show pallet at destination when moved

**Storage Location Indicator:**
- Racks/shelves show occupancy: "Rack A: 2/4 slots"
- Color code: green (capacity), yellow (3/4 full), red (full)

---

## Integration with Existing Systems

### MoneyService
**When:** Shipment received
**What:** Deduct shipment cost from capital
```csharp
// When ShipmentData arrives
int shipmentCost = shipment.TotalCost;
MoneyService.Deduct(shipmentCost, "Inventory", "Inbound Shipment");
```

### EconomyService (Hourly Costs)
**When:** Pallets placed in storage
**What:** Hourly storage cost (TODO - not yet designed)

**For MVP:** Assume zero hourly storage cost (implement later in Milestone 5)

### FinanceCategory
**New Category Needed:** "Inventory/Shipments" (cost of goods inbound)
**Existing:** "Wages" (already deducts Dock Stocker and Reach Operator labor)

### Task Assignment & Employee AI
**New Task Types:** Receive, Putaway
**Employee Roles Involved:**
- **Receiver**: Scans & authorizes shipments
- **Dock Stocker**: Moves pallets short distances
- **Reach Truck Operator**: Moves pallets far distances with equipment

**Pathfinding:** Existing AiNavigation can navigate to pallet location and storage location

---

## Testing Strategy

### Unit Tests (C# / NUnit)

1. **PalletData**
   - Create pallet, verify properties
   - Check expiration logic (IsExpired, IsExpiringSoon)
   - Check age calculation

2. **InventoryService**
   - ReceiveShipment creates pallets correctly
   - MovePallet updates location and indices
   - PickFromPallet decrements quantity
   - GetPalletsByLocation returns correct pallets
   - GetReceivingPallets returns pallets at (0,0)
   - HasCapacityAtLocation checks correctly

3. **ShipmentData & OrderData**
   - Creation and property access
   - Status transitions
   - Revenue/profit calculations

### Integration Tests (In-Game)

1. **Receiving Flow**
   - Generate shipment
   - Verify it appears in receiving UI
   - Click "Receive"
   - Verify pallets created in inventory at (0,0)
   - Verify capital deducted
   - Verify InventoryService.OnPalletReceived event fired

2. **Putaway Flow**
   - Assign Dock Stocker to putaway task
   - Employee navigates to receiving area (0,0)
   - Employee picks up pallet (visual feedback)
   - Assign destination location
   - Employee navigates to storage location
   - Employee places pallet
   - Verify pallet location updated in inventory
   - Verify InventoryService.OnPalletMoved event fired

3. **Storage Capacity**
   - Place rack with 4-slot capacity
   - Receive 6 pallets
   - Assign putaway for first 4
   - Verify rack full (red)
   - Try to assign 5th pallet to full rack
   - Verify toast: "Storage location full"
   - Putaway to alternate location

4. **Spoilage**
   - Receive perishable pallet (shelf_life = 3 days)
   - Advance day 3 times
   - Verify pallet marked contaminated on day 3
   - Verify InventoryService.OnSpoilageDetected event fired

---

## Acceptance Criteria (MVP Done When...)

- [ ] InventoryService fully functional (create, move, pick pallets)
- [ ] ShipmentService generates inbound shipments
- [ ] OrderService generates customer orders
- [ ] Receiving UI shows incoming shipments, "Receive" button works
- [ ] Receiving workflow: shipment → pallets created at (0,0)
- [ ] Putaway UI shows tasks and destination selection
- [ ] Putaway workflow: employee moves pallet to storage location
- [ ] Pallet locations persist across save/load
- [ ] Spoilage detection works (marks expired pallets on daily tick)
- [ ] Integration tests pass (no NullRefs, events fire correctly)
- [ ] Game compiles and runs without errors

---

## Known Gaps / TBD

1. **Storage Capacity Model** — Currently hardcoded to 4 slots per cell. Need to:
   - Query ObjDataSO for actual footprint
   - Calculate slots based on building size
   - Track 3D stacking (Y-axis)

2. **Stacking Rules** — ShipmentData has `ShelfLifeDays`, PalletData can be marked perishable, but:
   - No enforcement of fragile/heavy/cold-storage-only rules
   - Assume all items can stack in MVP

3. **Pallet Visualization** — Currently inventory is pure data. Need:
   - 3D model for pallets (lightweight, no physics)
   - Visual representation at pallet locations
   - Stacking visualization (higher = Y offset)

4. **Save/Load** — InventoryService pallets not yet persisted:
   - Add to PlacementSystem.BuildSaveData
   - Restore in PlacementSystem.ApplySaveData

5. **Financial Integration** — Shipment costs deducted, but:
   - No revenue credited yet (that's Milestone 4)
   - No hourly storage costs yet (deferred)

---

## Next Immediate Steps

1. **ShipmentService** — Create and register in GameContext
2. **OrderService** — Create and register in GameContext
3. **Receiving UI** — Build UI panel, wire to ShipmentService events
4. **Putaway UI** — Build task assignment UI, wire to task system
5. **Task System** — Extend with WarehouseTask base class, Receive/Putaway tasks
6. **Integration Tests** — Play in-editor, test receiving → putaway flow
7. **Save/Load** — Persist pallets to disk

