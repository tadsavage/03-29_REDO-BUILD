# Excel Data Import Guide

**File:** `ItemFilesForForkIT.xlsx` (in Downloads)  
**Sheets:** 
1. "Days Supply" — General item data (SKU, vendor, description, daily movement, shelf life)
2. "ItemSetup" — Physical specifications (dimensions, weight, palletization metrics)

---

## Data Structure

### Sheet 1: Days Supply (13,225 items)
| Column | Description | Game Use |
|--------|-------------|----------|
| Item | Stock Keeping Unit (SKU) ID | SkuData.SkuId |
| Vendor | Vendor/Supplier number | For supplier matching |
| Vendor Name | Supplier name | ShipmentData.SupplierName |
| Description | Product name/description | SkuData.SkuName |
| Facility | Warehouse facility code | (ignored for MVP) |
| Daily Movement | Units sold per day (demand) | SkuData.AverageDailyDemand |
| Days Supply | Inventory coverage (shelf life indicator) | Determines SkuData.ShelfLifeDays |
| Cases In House | Current inventory | (used for testing) |
| Pallets In House | Current pallet count | (used for testing) |

### Sheet 2: ItemSetup (7,562 items)
| Column | Description | Game Use |
|--------|-------------|----------|
| Wskusku | SKU ID (matches Sheet 1) | Join key |
| Description | Product name | Cross-reference |
| Location | Warehouse location code | (ignored for MVP) |
| High (Hi) | Units per pallet layer | SkuData.HiCount |
| Tie (Ti) | Units per case/tier | SkuData.TiCount |
| Case Height | Height in inches | (future: 3D visualization) |
| Case Depth | Depth in inches | (future: 3D visualization) |
| Case Width | Width in inches | (future: 3D visualization) |
| Case Weight | Weight in lbs | SkuData.CaseWeight → determines stacking rules |

---

## Import Workflow

### Step 1: Export Excel Sheets as CSV

1. Open `ItemFilesForForkIT.xlsx` in Excel
2. Select Sheet 1 "Days Supply"
3. **Save As** → `SKU_DaysSupply.csv` (CSV UTF-8 format)
4. Select Sheet 2 "ItemSetup"
5. **Save As** → `SKU_ItemSetup.csv` (CSV UTF-8 format)
6. Place both CSV files in: `Assets/_Project/Data/Inventory/Import/`
   - Create the directory if it doesn't exist

### Step 2: Import SkuData Assets (Editor-Only)

In the Unity Editor, go to menu: **Warehouse > Import SKUs from CSV**

This will:
1. Read both CSV files
2. Match items by SKU across sheets
3. Generate pricing and shelf-life categories:
   - **Cost:** Based on case weight (heavier = more expensive)
   - **Price:** 50% markup from cost (wholesale pricing)
   - **Shelf Life:** Based on Days Supply value
     - ≤7 days: 3 days (highly perishable)
     - 8-14 days: 7 days
     - 15-30 days: 14 days
     - >30 days: -1 (non-perishable)
   - **Stacking:** Based on case weight
     - <5 lbs: Small (can stack 3 high)
     - 5-15 lbs: Medium (can stack 2 high)
     - >15 lbs: Large (can stack 1 high only)

4. Create SkuData assets in: `Assets/_Project/Data/Inventory/SKUs/`
5. Assets named: `SKU_<ITEM_NUMBER>.asset`

**First Run:** Limit to 100 items for testing (edit line 20 in SkuImporter.cs to increase after validating)

### Step 2: Load SKU Database at Runtime

In **GameContext.cs**, after initializing InventoryService:
```csharp
var skus = Resources.LoadAll<SkuData>("Inventory/SKUs");
InventoryService inventoryService = new InventoryService();
inventoryService.Initialize();
inventoryService.LoadSkuDatabase(skus);
```

This caches all imported SKU data in InventoryService for fast lookups during gameplay.

### Step 3: Generate Test Data

In GameContext or a test scene, add **TestDataGenerator** MonoBehaviour:
```csharp
[SerializeField] private TestDataGenerator _testGen;
```

Or call directly:
```csharp
var testGen = GetComponent<TestDataGenerator>();
var (shipments, orders) = testGen.GenerateDay(currentDay);
```

**TestDataGenerator** will:
1. Randomly select SKUs from imported database
2. Generate realistic shipment data (supplier, items, costs)
3. Generate realistic order data (customer, items, deadlines, revenue)
4. Use actual pricing/shelf-life from imported SkuData

---

## Data Mapping

### Shipment Creation
```
Excel Row → ShipmentLineItem
├─ Wskusku (Item) → SkuId
├─ Quantity (random 10-100) → Quantity
├─ ShelfLifeDays from SkuData → ShelfLifeDays
└─ UnitCost from SkuData → UnitCost
```

### Order Creation
```
Excel Row → OrderLineItem
├─ Wskusku (Item) → SkuId
├─ Quantity (random 5-50) → QuantityNeeded
├─ UnitCost from SkuData → UnitCost
└─ SellingPrice from SkuData → SellingPrice
```

### Pallet Calculations
```
Units per Case = TiCount (from Excel "Tie")
Units per Pallet = HiCount × TiCount
Weight per Case = CaseWeight (from Excel)
```

---

## Testing Workflow

### 1. Create Small Test Set
- Edit SkuImporter.cs line 51: Change 100 to 20
- Run: **Warehouse > Import SKUs from Excel**
- Creates 20 test products in Assets/_Project/Data/Inventory/SKUs/

### 2. Load and Generate Data
- Add TestDataGenerator to scene
- In Inspector, drag imported SKU folder to _allSkus array
- Play scene
- Right-click TestDataGenerator component → **Generate Sample Day**
- Check Console for generated shipment/order data

### 3. Build Receiving UI
- Display the generated ShipmentData in a UI panel
- "Scan & Receive" button calls `InventoryService.ReceiveShipment()`
- Verify pallets appear in `InventoryService.GetReceivingPallets()`

---

## Performance Notes

- **13,225 items:** Full import takes ~30 seconds
- **7,562 with setup data:** Only these have dimension data
- **Overlap:** ~7,000 items in both sheets (can join safely)

For MVP testing: Import just first 100-500 items. Full import needed only for production quality assurance.

---

## Future Enhancements

1. **3D Visualization:** Use CaseHeight/Width/Depth for box mesh generation
2. **Supplier Relationships:** Build supplier repeat order patterns from historical data
3. **Demand Forecasting:** Use DailyMovement for order arrival frequency
4. **ABC Analysis:** Classify items as fast/medium/slow movers for optimal location assignment
5. **Pricing Tiers:** Vendor-specific markup percentages instead of flat 50%
6. **Min/Max Stock:** Auto-generate reorder points from DaysSupply

