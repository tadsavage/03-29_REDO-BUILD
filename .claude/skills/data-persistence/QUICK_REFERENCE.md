# Data Persistence — Quick Reference

## The Save/Load Flow (30 seconds)

```
F5 (Save)                          F9 (Load)
   ↓                                  ↓
PlacementSystem.BuildSaveData()    PlacementSystem.ApplySaveData()
   ├─ Iterate all placed objects     ├─ ClearAll()
   ├─ Skip id=200 (yards)            ├─ Loop save.placedObjects
   ├─ Skip employees                 ├─ SpawnFromSave(id, x, y, rot, customData, worldY)
   └─ Save: id, gridX, gridY,        ├─ InventoryPersistenceService.InstantiateRestoredPalletVisuals()
      rotation, customData, worldY   └─ RebuildFromRegistry() + NavMesh bake
           ↓
      quicksave.json (JSON)
```

## Y-Position Formulas (Copy-Paste Ready)

**Ground Level (meters):**
```
1.06 (foundation) + 0.06 (floor) + 0.015 (gap) + 0.16 (pallet) + (Hi * CaseHeight)
```

**Stacked (meters):**
```
pallet_below_top + 0.16 (pallet) + 0.015 (gap) + (Hi * CaseHeight)
```

## Code Snippets

### Save New Data Type
```csharp
// In SaveData
[System.Serializable] public struct MySnapshot { public int id; }
public List<MySnapshot> myData = new List<MySnapshot>();

// In BuildSaveData()
if (ServiceLocator.TryGet<MyService>(out var svc))
    foreach (var item in svc.GetAll())
        save.myData.Add(new MySnapshot { id = item.Id });

// In ApplySaveData()
if (save.myData != null && ServiceLocator.TryGet<MyService>(out var svc))
    svc.Restore(save.myData);
```

### Calculate Ground Level Y
```csharp
Vector3 worldPos = grid.GetCellCenter(gridCell);
worldPos.y = PalletHeightCalculator.CalculateGroundLevelY(sku);
GameObject go = Instantiate(prefab, worldPos, Quaternion.identity);
```

### Calculate Stacked Y
```csharp
var existing = grid.GetObjectsInCell(gridCell);
float topY = 0f;
foreach (var obj in existing)
    if (obj.instance != null)
        topY = Mathf.Max(topY, 
            PalletHeightCalculator.GetPalletTop(obj.instance.transform.position.y, sku));

Vector3 worldPos = grid.GetCellCenter(gridCell);
worldPos.y = topY > 0f 
    ? PalletHeightCalculator.CalculateStackedY(topY, sku)
    : PalletHeightCalculator.CalculateGroundLevelY(sku);
Instantiate(prefab, worldPos, Quaternion.identity);
```

### Debug Missing Data
```csharp
// 1. Check if saving
Debug.Log($"[Save] {save.placedObjects.Count} objects");

// 2. Check if loading
Debug.Log($"[Load] Spawning {save.placedObjects.Count} objects");

// 3. Check Y positions
Debug.Log($"[Load] Object at worldY={worldPos.y}");

// 4. Check registration
Debug.Log($"[Registry] {PlacedObjectRegistry.All.Count} objects registered");
```

## Critical Gotchas

| Gotcha | Fix |
|--------|-----|
| Objects don't appear after load | Check: prefab exists, SpawnFromSave called, Y position > -1000 |
| Stacked objects sink into each other | Use PalletHeightCalculator, save worldY, not just gridX/gridY |
| Domain reload breaks load | Restart editor completely, don't just stop Play mode |
| worldY = 0 fails | Use `if (worldY != 0f)` not `if (worldY > 0f)` |
| Child objects phantom at (0,0) | They auto-skip registry — don't persist them |
| Yard floors duplicate | ID 200 is auto-skipped, regenerated on load |
| Employees spawn twice | They're not PlacedObjects, persisted separately with 1-frame delay |

## Files to Touch

| Task | Files |
|------|-------|
| Add new saveable data | SaveData.cs |
| Implement save | PlacementSystem.BuildSaveData() |
| Implement load | PlacementSystem.ApplySaveData() + SpawnFromSave() |
| Handle stacking | PalletHeightCalculator + loop existing objects |
| Debug issues | Add Debug.Log to above methods |

## Testing (Copy-Paste Checklist)

```
□ Place object(s) in scene
□ Save (F5)
□ Check quicksave.json has correct id/x/y/worldY
□ **Restart editor completely**
□ Load (F9)
□ Object appears at same position
□ Stacked objects correct relative height
□ Console shows correct counts/Y values
□ Grid rebuilt, NavMesh baked
```

## When to Use This Skill

Trigger: `/data-persistence`

- Adding new saveable data type
- Save/load bugs (data not appearing, wrong positions)
- Implementing 3D positioning (ground/stacked)
- Debugging persistence issues
- Design persistence for complex objects
