# Data Persistence Through Save/Load

Master save/load systems in the warehouse simulation. This skill covers persisting complex data (objects, pallets, world state) with correct 3D positioning and stacking.

## When to Use This Skill

Use `/data-persistence` when:
- Adding new data types to the save system (pallets, shipments, employee state, etc.)
- Fixing save/load bugs (data not appearing, wrong positions, stacking issues)
- Implementing 3D positioning logic (ground level, stacked objects, heights)
- Debugging why saved data doesn't restore correctly
- Designing persistence for complex objects with dependencies

## Architecture Overview

### The Save Pipeline

```
User presses F5 (quicksave)
    ↓
PlacementSystem.BuildSaveData()
    ├─ Iterate PlacedObjectRegistry.All
    ├─ Skip yard floors (id=200) — regenerated on load
    ├─ Skip employees — persisted separately via EmployeeRecords
    ├─ For each: create SavedObject(id, gridX, gridY, rotation, customData, worldY)
    └─ Serialize to JSON → Assets/_Saves/quicksave.json

User presses F9 (quickload)
    ↓
PlacementSystem.ApplySaveData()
    ├─ ClearAll() — destroy old objects
    ├─ Loop save.placedObjects
    ├─ For each: PlacementSystem.SpawnFromSave(so, x, y, rot, customData, worldY)
    │   ├─ If worldY > 0: use it directly
    │   ├─ Else if isStackable: calculate from grid.GetStackHeight()
    │   └─ Instantiate prefab at calculated position
    ├─ Call InventoryPersistenceService.InstantiateRestoredPalletVisuals()
    ├─ Rebuild grid + employee spawn
    └─ Bake NavMesh
```

### Key Components

| Class | Purpose | Key Method |
|-------|---------|-----------|
| `PlacementSystem` | Orchestrates save/load | `BuildSaveData()`, `ApplySaveData()`, `SpawnFromSave()` |
| `SaveData` / `SavedObject` | Serializable container | — (JSON-backed) |
| `PlacedObjectRegistry` | Global registry of placed objects | `PlacedObjectRegistry.All` (static HashSet) |
| `PlacementGrid` | Grid coordinate ↔ world position | `GetCellCenter()`, `WorldToCell()`, `GetStackHeight()` |
| `PalletHeightCalculator` | Y-position math for stacking | `CalculateGroundLevelY()`, `CalculateStackedY()` |

---

## Common Tasks

### Task 1: Save a New Data Type

**Example: Persisting shipment data**

**Step 1: Extend SaveData**
```csharp
[System.Serializable]
public class SaveData
{
    public List<ShipmentSnapshot> shipments = new List<ShipmentSnapshot>();
    // ... existing fields
}

[System.Serializable]
public struct ShipmentSnapshot
{
    public int poNumber;
    public string status;  // "InTransit", "Received", "Shipped"
    public Vector2Int dockedAt;  // grid cell where truck is docked
}
```

**Step 2: Populate in BuildSaveData()**
```csharp
// In PlacementSystem.BuildSaveData(), after employee persistence:
if (ServiceLocator.TryGet<ShipmentService>(out var shipmentService))
{
    foreach (var shipment in shipmentService.GetAllShipments())
    {
        save.shipments.Add(new ShipmentSnapshot {
            poNumber = shipment.PONumber,
            status = shipment.Status.ToString(),
            dockedAt = shipment.DockedTruck?.GridCell ?? Vector2Int.zero
        });
    }
}
```

**Step 3: Restore in ApplySaveData()**
```csharp
// In PlacementSystem.ApplySaveData(), after employee restoration:
if (save.shipments != null && ServiceLocator.TryGet<ShipmentService>(out var shipmentService))
{
    shipmentService.RestoreShipments(save.shipments);
}
```

**Step 4: Implement RestoreShipments()**
```csharp
public void RestoreShipments(List<ShipmentSnapshot> snapshots)
{
    _shipments.Clear();
    foreach (var snap in snapshots)
    {
        var shipment = new ShipmentData { PONumber = snap.poNumber };
        shipment.SetStatus((ShipmentStatus)Enum.Parse(typeof(ShipmentStatus), snap.status));
        // Re-create truck if needed, restore its position
        _shipments[snap.poNumber] = shipment;
    }
}
```

### Task 2: Handle 3D Positioning (Ground Level)

**Problem:** Objects placed on the dock need correct Y position accounting for foundation, floor, pallet height, etc.

**Solution: Use PalletHeightCalculator**

```csharp
// When placing a pallet on the dock:
Vector3 worldPos = grid.GetCellCenter(gridCell);
worldPos.y = PalletHeightCalculator.CalculateGroundLevelY(sku);

GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity);
```

**Formula (meters):**
```
1.06 (foundation) 
  + 0.06 (floor tile)
  + 0.015 (anti-melting gap)
  + 0.16 (pallet base)
  + (Hi * CaseHeight)  // Hi = layers per pallet
```

### Task 3: Handle Stacking (Multiple Objects in One Cell)

**Problem:** Second pallet placed on top of first should sit at correct height.

**Solution: Calculate from existing objects**

```csharp
// Find highest object in this cell
var existingObjects = grid.GetObjectsInCell(gridCell);
float highestTop = 0f;
foreach (var obj in existingObjects)
{
    if (obj.instance != null)
    {
        highestTop = Mathf.Max(highestTop, 
            PalletHeightCalculator.GetPalletTop(obj.instance.transform.position.y, sku));
    }
}

// Place new pallet on top
Vector3 worldPos = grid.GetCellCenter(gridCell);
if (highestTop > 0f)
    worldPos.y = PalletHeightCalculator.CalculateStackedY(highestTop, sku);
else
    worldPos.y = PalletHeightCalculator.CalculateGroundLevelY(sku);

GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity);
```

**Formula (meters):**
```
pallet_below_top 
  + 0.16 (new pallet base)
  + 0.015 (gap)
  + (Hi * CaseHeight)
```

### Task 4: Debug Save/Load Issues

**Symptom: Objects not appearing after load**

**Checklist:**

1. ✅ **Is data being saved?**
   ```csharp
   // Add logging to BuildSaveData()
   Debug.Log($"[PlacementSystem] Saving {save.placedObjects.Count} placed objects");
   ```
   Check quicksave.json — do you see the object IDs?

2. ✅ **Is the prefab valid?**
   ```csharp
   // In SpawnFromSave(), before Instantiate:
   if (so.prefab == null)
   {
       Debug.LogError($"Prefab missing for ObjDataSO id={so.id}");
       return null;
   }
   ```

3. ✅ **Is SpawnFromSave being called?**
   ```csharp
   // Add logging
   Debug.Log($"[PlacementSystem] Spawning object id={so.id} at ({x}, {y})");
   ```

4. ✅ **Is the Y position correct?**
   ```csharp
   Debug.Log($"[PlacementSystem] World Y={worldPos.y} for stackable={so.isStackable}");
   ```

5. ✅ **Is the object being registered?**
   PlacedObject.OnEnable() calls PlacedObjectRegistry.Register(). Check:
   - PlacedObject component exists on prefab
   - It's not a child of another PlacedObject

6. ✅ **Inventory-specific: Is InventoryPersistenceService running?**
   ```csharp
   // Add logging to InventoryPersistenceService.InstantiateRestoredPalletVisuals()
   Debug.Log($"[InventoryPersistenceService] {instantiated}/{allPallets.Count} pallets instantiated");
   ```

### Task 5: Save World Y for Stacking

**Pattern: Always save absolute Y when objects stack**

**In SaveData.SavedObject:**
```csharp
public class SavedObject
{
    public int id, x, y, rot;
    public string customData;
    public float worldY;  // ← Always include for stackable objects
}
```

**In BuildSaveData():**
```csharp
obj.worldY = entry.transform.position.y;  // Absolute world Y
```

**In SpawnFromSave():**
```csharp
float stackY = 0f;
if (worldY > 0f)
{
    // Use saved Y directly — most accurate for stacking
    stackY = worldY - grid.GetCellCenter(root).y;
}
else if (so.isStackable)
{
    // Fallback: calculate from grid
    stackY = grid.GetStackHeight(root);
}

Vector3 worldPos = grid.GetCellCenter(root);
worldPos.y += stackY;
```

---

## Load-Bearing Conventions

### DO:

✅ Save `worldY` for stackable objects  
✅ Calculate stacked Y from existing objects in the cell  
✅ Use `PalletHeightCalculator` for height math  
✅ Add logging at both save and load time  
✅ Test: Save → Restart editor → Load  
✅ Call `grid.RebuildFromRegistry()` after load  
✅ Check PlacedObjectRegistry — don't iterate destroyed objects  

### DON'T:

❌ Derive Y from `gridX` / `gridY` alone (lose stacking info)  
❌ Hardcode heights (different objects = different heights)  
❌ Save inventory data without prefab instantiation  
❌ Assume child objects register in PlacedObjectRegistry (they skip themselves)  
❌ Forget to initialize services before loading (ServiceLocator needs them)  
❌ Mix coordinate systems (grid cells vs. world positions)  

---

## Testing Checklist

Before declaring save/load done:

- [ ] Place object(s) in scene
- [ ] Save (F5)
- [ ] Verify object appears in quicksave.json with correct id/position/worldY
- [ ] **Completely restart the editor** (don't just stop Play mode)
- [ ] Load (F9)
- [ ] Object appears in scene at same position
- [ ] Stacked objects appear at correct relative heights (not sinking)
- [ ] Console logs show correct counts and Y values
- [ ] Grid rebuild completes without errors
- [ ] NavMesh bakes successfully

---

## Files to Modify (by Type)

| Change Type | Files |
|------------|-------|
| Add new saveable data | `SaveData.cs` |
| Implement save logic | `PlacementSystem.BuildSaveData()` |
| Implement load logic | `PlacementSystem.ApplySaveData()` |
| Handle 3D positioning | Create/use `PalletHeightCalculator` |
| Fix stacking | Use `grid.GetObjectsInCell()` + height math |
| Debug issues | Add logging to `BuildSaveData()` / `ApplySaveData()` / `SpawnFromSave()` |

---

## Gotchas (Hard-Won Knowledge)

### 1. **Domain Reload During Play**
When scripts recompile mid-play-session, EventManager and ServiceLocator silently null, breaking load. Solution: Always restart editor completely after recompiling. (`Settings > Project Settings > Editor > Enter Play Mode Options`: uncheck if it's enabled for full reload)

### 2. **worldY = 0 Is Falsy**
```csharp
if (worldY > 0f)  // ← This fails if object is at Y=0!
```
Use `if (worldY != 0f)` or track "is set" separately for objects at ground level.

### 3. **Child PlacedObjects Skip Registry**
A pallet with cases inside has nested PlacedObject children. They auto-skip registration to avoid phantom (0,0) entries. Never rely on them appearing in PlacedObjectRegistry.

### 4. **PlacementGrid.PlacedObject Is a Struct**
```csharp
// PlacementGrid.PlacedObject { public GameObject instance; }
// NOT the MonoBehaviour PlacedObject

var cell = grid.GetObjectsInCell(gridCell);  // List<PlacementGrid.PlacedObject>
foreach (var obj in cell)
{
    obj.instance.transform.position.y;  // ← Correct
}
```

### 5. **Yard Floors Aren't Saved**
ID 200 (yard floor tile) is explicitly skipped during save and regenerated on load. Never try to persist it or you'll duplicate floors.

### 6. **Employee Persistence Is Separate**
Employees are NOT saved as PlacedObjects. They live in `save.employeeRecords` and are respawned via EmployeeSpawner, one frame after old ones are destroyed (to avoid GUID collisions).

---

## Related Skills

- **lane_setup** — Dock/shipping-lane configuration persistence
- **racking-system** — Rack metadata persistence (aisle/bay/level via customData + RackSaveCodec)
- **quick-tweaks** — Adjusting saved values without full recompile

---

## See Also

- Memory: `pallet-persistence-system.md` — Detailed technical reference for dock product save/load (2026-07-09)
- CLAUDE.md → Architecture → Save System
