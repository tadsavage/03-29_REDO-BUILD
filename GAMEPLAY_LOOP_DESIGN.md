# Warehouse Simulation: Gameplay Loop Design

**Status:** Design Phase  
**Date:** 2026-06-26  
**Priority:** #1 (Foundation for all future gameplay)

---

## Core Premise

The player (Tug Dudley, warehouse manager) manages an active warehouse that operates on a continuous **day/night cycle**. The primary loop is:

1. **Orders arrive** (from customers/suppliers) → 2. **Goods are received** → 3. **Inventory is stored** → 4. **Orders are fulfilled** → 5. **Revenue is earned** → 6. **Operating costs deducted** → back to step 1

Each phase involves employees performing specific tasks. Success = meeting demand while controlling costs. Failure = stockouts, overstocking, cash flow problems.

---

## Phase 1: RECEIVING

**Trigger:** A shipment arrives at the loading dock (generated at start of day or on-demand)

### Receiving Mechanics

**Inbound Shipment Data:**
- Order ID (internal reference)
- List of SKUs and quantities (e.g., 50 × "Widget A", 30 × "Widget B")
- Supplier name (who sent it)
- Shipment date/expected arrival time
- Cost per unit (import cost, before markup)
- Shelf life (if perishable — triggers spoilage after N days)

**Receiving Workflow:**
1. Truck arrives at loading dock (visual trigger — dock busy)
2. Dock Worker or Receiver scans/counts inbound goods
3. Goods marked as "received" and moved to receiving staging area
4. Each SKU gets a **Pallet** (or partial pallet if partial shipment)
5. Pallet is created in inventory tracking system with metadata:
   - SKU ID
   - Quantity
   - Received date
   - Expiration date (if perishable)
   - Current location (initially: Receiving Staging)

**Employee Involvement:**
- **Receiver** role: Assigned to dock, scans goods, authorizes receipt
- **Dock Stocker**: Physically moves goods from truck bed to receiving staging area

**UI/Feedback:**
- "Shipment received: 50 units Widget A, 30 units Widget B"
- Floating popup showing cost impact (shipment cost deducted from capital)
- Queue indicator on dock (red=full, yellow=busy, green=empty)

---

## Phase 2: PALLET PUTAWAY

**Trigger:** Pallets exist in Receiving Staging area

### Putaway Mechanics

**What is Pallet Putaway?**
Physically moving pallets from the receiving area to designated storage locations in the warehouse (racking, floor storage, cold storage, etc.) to free up dock space for new incoming shipments.

**Storage Location System:**
- Each placed **Storage object** (rack, shelving, bin) has a fixed capacity per grid cell
- Capacity is measured in "pallet slots" (e.g., a 2×2 racking unit = 4 slots)
- Each pallet occupies 1 slot; multiple pallets can stack vertically per **Pallet Stacking Rules**

**Pallet Stacking Rules:**
- Heavy items (raw materials) → cannot stack
- Light items (packaged goods) → can stack up to 3 high
- Fragile items → cannot stack
- Cold storage items → cold room only (not mixed with regular storage)
- Hazardous → restricted zones only

**Putaway Logic (Automated or Manual?):**

Two approaches to explore:
- **Option A (Simple):** Employee walks to a receiving pallet, manually carries/drags it to a designated storage location
- **Option B (Realistic):** Storage system auto-suggests "optimal" location based on ABC analysis (fast-movers near dock, slow-movers deep) — employee navigates there with equipment

**For MVP, recommend Option A** (simpler to implement, clearer player control):
1. Dock Stocker or Reach Truck Operator gets assigned a pallet
2. Navigates to available storage location
3. Deposits pallet; location is updated in inventory system
4. Pallet now trackable by location (e.g., "Rack A5, slot 2")

**Employee Involvement:**
- **Dock Stocker**: Can move pallets short distances on foot
- **Reach Truck Operator**: Can move pallets farther/stack higher with equipment
- **Inventory Control**: Can assign putaway tasks, monitor stock locations

**Cost Impact:**
- Fulfilling putaway tasks consumes employee labor (wage cost)
- Delaying putaway → shipment sits in expensive dock staging area → cash flow pressure
- Overstocking creates space pressure; underfilled storage wastes capacity

**UI/Feedback:**
- "Dock Staging: 4 pallets waiting | Capacity: 6 slots"
- Task assignment UI: "Putaway: Widget A (Pallet #1023) → Rack C2"
- Storage location indicator on hover/inspect

---

## Phase 3: INVENTORY MANAGEMENT

**Ongoing:** Goods are stored and tracked

### Inventory System Core

**SKU Master Data:**
- SKU ID (e.g., "WGT-A-001")
- Name (e.g., "Widget A Standard")
- Unit cost (what you paid the supplier)
- Selling price (markup — what customers pay)
- Shelf life (days until spoils, -1 if non-perishable)
- Size category (small/medium/large → stacking rules)
- Demand forecast (orders per day — for reordering logic)

**Inventory Ledger:**
- Total units on hand (all locations combined)
- Breakout by location (e.g., "Rack A: 50 units", "Rack B: 30 units")
- Age of oldest unit (relevant for perishables)
- Flagged as "low stock" if < reorder point

**Perishable Spoilage:**
- Every day, check all perishable pallets
- If received date + shelf_life < today → **Contaminated** status
- Contaminated goods must be removed (no revenue, just loss)
- Player gets warning: "Widget A expiring in 2 days!" (opportunity to discount/offload)
- If player doesn't act, goods rot → visible rot effect, then auto-destroy + write-off loss

---

## Phase 4: ORDER SELECTION (PICKING)

**Trigger:** Customer order arrives (daily or on-demand)

### Order Mechanics

**Inbound Customer Order:**
- Order ID (customer reference)
- List of SKUs and quantities (e.g., 20 × "Widget A", 10 × "Widget B")
- Customer name / delivery address
- Due date (when does it need to ship? Today, tomorrow, this week?)
- Payment terms (prepaid, COD, invoice — affects cash flow timing)

**Order Backlog System:**
- Orders queue up as they arrive
- Player sees backlog: "5 orders pending | Oldest due today"
- Each order has a status: **Pending** → **Picking** → **Picked** → **Staged** → **Shipped**

### Order Picking Workflow

**Step 1: Assign Picker**
- Order Selection role (or Loader can do both) assigned to an order
- System shows which pallets contain the SKUs needed (location list)
- Picker navigates to first location, scans/removes items

**Step 2: Item Removal**
- Picker takes items from pallet
- Inventory system decrements: "Widget A: 50 → 35 units in Rack A"
- If pallet now empty: pallet destroyed, storage space freed

**Step 3: Item Consolidation**
- Picked items moved to **Order Staging Area** (near shipping dock)
- Order status updates: "Picking: 18/20 Widget A | 8/10 Widget B"
- Partial picks visible (e.g., "Incomplete — 2 Widget A on backorder")

**Order Fulfillment Rules:**
- **Full Order:** Player can ship immediately (all SKUs available)
- **Partial Order:** Player can: (a) Hold & wait for more stock, (b) Split & ship what's available, (c) Cancel & refund
- **Backorder:** If items unavailable, order waits in queue. Player gets warning: "Order #123 backorder in 3 days"

**Employee Involvement:**
- **Order Selector/Picker**: Walks warehouse, retrieves items from storage, brings to staging
- **Loader**: Moves staged items onto truck for shipping

**Cost Impact:**
- Picking consumes labor (wage)
- Delaying orders → customer dissatisfaction (reputation hit?) or order cancellation
- Backorders → lost revenue potential

**UI/Feedback:**
- Order card shows progress: "Order #456 | Widget A: 20/20 ✓ | Widget B: 10/10 ✓ | READY TO SHIP"
- Incomplete orders flagged in red: "Widget C: 5/5 not in stock"
- Task assignment: "Pick Widget A (20 units) from Rack A"

---

## Phase 5: STAGING & LOADING

**Trigger:** Order is fully picked and ready to ship

### Staging Area

**Physical Space:**
- Designated zone near loading dock where picked orders accumulate
- Limited capacity (e.g., 10 pallets or 100 cartons)
- If full, no new orders can be picked → bottleneck

**Staging Process:**
1. Picked order boxes/cartons sit in staging area
2. Player views staging queue: "3 orders staged | Capacity: 10 slots"
3. Loader scans order and confirms all items present
4. Order moves to **Ready to Ship** status

### Loading & Truck Assignment

**Truck Dispatch:**
- Trucks can be scheduled in advance (at start of day, or manually assigned)
- Each truck has:
  - Destination (address or route)
  - Capacity (number of pallets/cartons it can carry)
  - Scheduled departure time (creates urgency)
  - Driver assigned (Truck Driver role)

**Load Planning:**
- Player manually assigns orders to trucks (drag-and-drop UI?) OR
- System auto-assigns if space available
- Loading sequence matters: orders loaded in reverse of drop-off sequence (last-loaded = first stop)

**Loading Workflow:**
1. Truck Driver gets assigned to truck + order list
2. Loader places staged boxes onto truck
3. Truck status: "Loading: 3/5 orders loaded"
4. Once full or all orders loaded: "Truck ready to depart"

**Departure:**
- Truck departs warehouse (visual: truck exits on screen)
- Order status → **Shipped**
- Revenue is credited when truck departs (cash-basis accounting, not invoice-basis)

**Cost Impact:**
- Shipping cost depends on truck utilization (efficiency metric)
- Underutilized trucks = wasted capacity cost
- Delayed shipments = potential customer penalties (SLA miss)

**UI/Feedback:**
- Truck board showing all trucks, load status, ETA
- "Truck #1 departs in 30 minutes — 3 orders staged, 2 slots free"
- "Truck #2 to Customer ABC | Loaded: 4/5 orders"

---

## Phase 6: INVOICING & REVENUE

**Trigger:** Truck departs (order shipped)

### Revenue Recognition

**When Revenue is Earned:**
- At truck departure (order shipped)
- Amount = sum of selling prices for all SKUs in order
- Credited to **Sales** line in FinanceCategory
- Broken down by customer if multi-customer order

**Invoice Data:**
- Order ID linked to shipment
- Customer name
- Items shipped (qty × unit price)
- Total sale amount
- Shipping cost (if customer pays)
- Payment method (prepaid, COD, NET 30)

### Cash Flow Timing

**Three Payment Methods:**

1. **Prepaid** → Revenue credited immediately (cash-in when order ships)
2. **COD (Cash on Delivery)** → Revenue credited at delivery (more delay, slight risk)
3. **Invoice (NET 30)** → Revenue credited at ship, but cash arrives in 30 days (timing risk)

For MVP, recommend **Prepaid only** (simpler, no AR/collections system).

### Profit Calculation

**Per-Order Profit:**
- Gross Profit = Selling Price − Unit Cost
- Net Profit = Gross Profit − Labor (picker + loader wages) − Shipping Cost
- Margin % = Net Profit ÷ Selling Price

**Daily Summary:**
- Total Revenue: sum of all shipped orders
- Total COGS: sum of unit costs for shipped items
- Total Labor: wages for pickers/loaders worked that day
- Total Operating Costs: hourly building/equipment maintenance
- **Net Daily Profit = Revenue − COGS − Labor − Operating**

**UI/Feedback:**
- "Order #123 Shipped | Revenue: +$500 | Profit Margin: 35%"
- Daily financial summary (already exists in FinancialBreakdownPanel)

---

## Game Loop Timeline (Per In-Game Day)

**06:00 - Warehouse Opens**
- Daily lease charged (already implemented)
- Shipments processed overnight (goods ready in receiving dock)
- Employee shifts begin
- Player has ~16 in-game hours to work

**06:00 - 18:00 (Day Phase)**
1. Dock Worker scans inbound shipment
2. Receiving team stages goods
3. Dock Stocker/Reach Operator assigns putaway tasks
4. Employees work through putaway queue
5. Customer orders arrive throughout day
6. Pickers assigned to orders as they arrive
7. Loaders stage picked orders
8. Trucks depart on schedule
9. Revenue credits for each departure
10. Spoilage check (perishables nearing expiration)

**18:00 - 22:00 (Evening Phase)**
- Skeleton crew or closed (configurable)
- Overnight maintenance/restocking (future expansion)

**22:00 - 06:00 (Night Phase)**
- Closed
- Next shipments arrive in queue (processed at 06:00 restart)

**22:00 Daily Close-Out**
- Financial summary shown
- Profit/loss calculated
- Balance updated
- Day counter incremented
- Next day begins (or game ends if cash depleted)

---

## Systems Integration Points

### 1. Employee AI & Task Assignment
- Dock Worker → Scan & receive shipments
- Dock Stocker → Putaway from receiving area
- Order Selector → Pick items from storage for orders
- Loader → Move staged items to trucks
- Reach Truck Operator → Long-distance putaway & material handling

**Critical:** Task system must queue and prioritize (old orders picked first, docks must not back up, etc.)

### 2. Inventory Tracking
- PlacedObjectRegistry extended: pallets are "placed" objects with inventory metadata
- Or: New InventoryService tracks pallet locations separately from placement grid
- (TBD: Exact architecture — see Technical Design section below)

### 3. Pricing & Cost
- Unit costs per SKU stored in ObjDataSO or new SKUData asset
- Selling prices stored similarly
- Revenue credited via existing MoneyService + FinanceCategory system

### 4. Time System
- Uses existing SimulationTimeService (days, hours, minutes)
- Shipment arrival times pegged to in-game clock
- Truck departure times trigger revenue

### 5. UI Layers
- Order board (pending, picking, staged, shipped)
- Truck board (capacity, load status, departure schedule)
- Inventory dashboard (stock levels by location, aging, spoilage warnings)
- Daily financial summary (already exists)

---

## Implementation Roadmap (Rough Milestones)

### Milestone 1: Receiving & Putaway (1-2 weeks)
- [ ] Shipment generator (spawns incoming goods at day start)
- [ ] Pallet object and inventory tracking system
- [ ] Receiving task UI (dock worker assigns putaway)
- [ ] Putaway mechanics (dock stocker/reach operator moves pallets)
- [ ] Storage location tracking

### Milestone 2: Order Selection (1-2 weeks)
- [ ] Customer order generator
- [ ] Order queue UI
- [ ] Picking task assignment
- [ ] Inventory decrement on pick
- [ ] Partial-order handling

### Milestone 3: Staging & Loading (1 week)
- [ ] Staging area with capacity tracking
- [ ] Truck scheduling UI
- [ ] Load assignment (orders → trucks)
- [ ] Truck departure trigger

### Milestone 4: Revenue & Invoicing (1 week)
- [ ] Revenue calculation on shipment
- [ ] Invoice UI
- [ ] Daily profit/loss reporting
- [ ] Integration with FinanceCategory

### Milestone 5: Perishable Spoilage (1 week)
- [ ] Expiration date tracking per pallet
- [ ] Daily spoilage check
- [ ] Spoilage warning/visual feedback
- [ ] Write-off loss calculation

### Milestone 6: Employee AI for Warehouse Tasks (2-3 weeks)
- [ ] Pathfinding to storage locations
- [ ] Task execution (pick, place, load)
- [ ] Priority queuing
- [ ] (See separate Employee AI design doc)

---

## Technical Design (TBD)

### Pallet & Inventory Architecture

**Option A: Pallets as PlacedObjects**
- Each pallet is a GameObject with PlacedObject component
- Parented under storage location (rack, floor cell, etc.)
- Metadata stored in PlacedObject.data or custom component

**Pros:** Consistent with existing placement system; visual; stackable
**Cons:** Heavy on GameObjects; grid-based only; harder to track partial quantities

**Option B: Pallets as Data (Separate Inventory Service)**
- PlacedObjects only for storage locations (racks)
- Pallets tracked in pure data (InventoryService with ledger)
- Visualization via lightweight 3D prefabs (visual only, not colliders)

**Pros:** Lightweight; flexible; can exceed grid bounds; scalable
**Cons:** Decoupled from placement system; requires new service architecture

**Recommendation:** Hybrid approach (Option B for MVP)
- InventoryService manages pallet data and locations
- Lightweight visual pallets rendered on top of storage (not colliders)
- Storage locations are PlacedObjects (for physics/footprint)
- Pallet visual updates when moved in data layer

### Order & Revenue System

**New Services Needed:**
- `OrderService`: Generates, tracks, prioritizes customer orders
- `ShipmentService`: Tracks inbound deliveries, triggers receiving tasks
- `RevenueService`: Calculates and credits revenue on shipment (integrates with MoneyService)

### Task Assignment

**TBD in separate Employee AI design document.** High-level:
- Task queue (FIFO or priority-based)
- Task types: Receive, Putaway, Pick, Load
- Task assignment to employee roles
- Completion triggers (pallet moved, order picked, truck loaded)

---

## Success Metrics (Gameplay Depth)

**Player Challenges:**
1. **Capacity Management** — Limited dock, staging, and storage space → decisions on when to receive/ship
2. **Cash Flow** — Starting capital finite; big orders require capital tie-up → risk/reward
3. **Order Fulfillment** — Backorders create pressure; failing to ship on time = revenue loss
4. **Labor Cost** — Picking/loading labor is expensive; must stay efficient
5. **Spoilage** — Perishables age quickly; old stock not prioritized = loss
6. **Truck Utilization** — Empty truck space = wasted cost; overpacking = efficiency
7. **Demand Variability** — Some days surge, some slow → inventory planning challenge

**Victory Conditions (TBD):**
- Reach target daily profit
- Fulfill N orders without backorder
- Achieve X% on-time ship rate
- (Level-based progression with escalating difficulty)

**Failure Conditions (TBD):**
- Cash depletes to $0 (game over)
- Backlog exceeds capacity for N days (game over or restart)
- Spoilage losses exceed threshold (warning/penalty)

---

## Next Steps

1. **Review & feedback** — Does this loop feel right? Any gaps or over-scoping?
2. **Prioritize milestones** — Which to build first (recommend Milestone 1: Receiving & Putaway)
3. **Technical deep-dives** — Create separate docs for:
   - InventoryService architecture
   - Employee AI task assignment
   - Order/Shipment data models
4. **Prototype receiving** — Build a minimal receiving flow to validate mechanics before scaling

