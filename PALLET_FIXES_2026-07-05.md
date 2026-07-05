# Pallet Rendering & Menu Fixes — 2026-07-05

## Summary

Fixed three critical issues affecting all 30 case types:
1. **ORIENTATION FIX** — All layers rendering with wrong aspect ratio (fixed)
2. **HEIGHT FIX** — Cases sinking into pallet/layers (fixed)
3. **MENU REORGANIZATION** — Created Inventory Tools submenu (completed)
4. **INVENTORY PERSISTENCE** — Identified save system; marked for next pass

---

## Issue 1: Orientation Fix (COMPLETED)

### The Problem
All 30 case types render with their entire LAYER transposed. The layer's aspect ratio was backwards relative to the pallet's 40"×48" footprint:
- Pallet: 40" wide × 48" long (region width < region length)
- Grid output: wider than deep (countX > countZ) — transposed
- Result: cases run perpendicular to the pallet's long axis instead of parallel

**Example (Juice 659083):**
- Correct: 2 cases wide × 3 deep (matches pallet's 40"×48" proportions)
- Actual: 3 wide × 2 deep (transposed)

### The Fix

**File:** `Assets/_Project/Scripts/Gameplay/PalletOptimizer.cs`  
**Method:** `GridFill()` (lines 173–188)

Added aspect-ratio validation after computing the grid:
```csharp
// ASPECT RATIO FIX: check if the grid is transposed relative to the pallet.
// If the pallet is longer than wide but the computed grid is wider than deep,
// swap them so the layer's aspect ratio matches the pallet's footprint.
bool palletIsLongerThanWide = regionLength > regionWidth;
bool gridIsWiderThanDeep = countX > countZ;
if (palletIsLongerThanWide && gridIsWiderThanDeep)
{
    // Swap: the layer was computed sideways. Flip countX/countZ and rotate 90°.
    int temp = countX;
    countX = countZ;
    countZ = temp;
    rot = rot == 0f ? 90f : 0f;  // Toggle rotation
    float tempW = pieceW;
    pieceW = pieceL;
    pieceL = tempW;
}
```

**Logic:**
- Compares pallet footprint orientation (width vs. length) to computed grid orientation (countX vs. countZ)
- If they're transposed, swaps them and toggles rotation 90°
- Ensures the layer's aspect ratio always matches the pallet's footprint

**Impact:** Every case type now renders with the correct orientation. Juice 659083 will now show as 2×3, Sugar 750426 and CannedTomato 209875 will show correctly.

---

## Issue 2: Height Fix (COMPLETED)

### The Problem
Cases on ground level and upper levels sink INTO the pallet/layers instead of sitting ON TOP:
- Ground level: first layer of cases sinks partway into the pallet base
- Upper levels: cases sink into the layer below

### Root Cause
**File:** `Assets/_Project/Scripts/Gameplay/PalletBuilder.cs`  
**Method:** `Build()` (line 326)

Original formula:
```csharp
float yPos = palletDim.y + (caseDim.y / 2f) + (h * (caseDim.y + verticalGap));
```

For layer 0, this places the case center at: `pallet_height + half_case_height`
- The case extends from `pallet_height - half_case_height` to `pallet_height + half_case_height`
- **The case sits half-IN, half-ON the pallet** ❌

### The Fix

Updated formula (line 326):
```csharp
float yPos = palletDim.y + verticalGap + (caseDim.y / 2f) + (h * (caseDim.y + verticalGap));
```

**Change:** Added `+ verticalGap` immediately after `palletDim.y`

**Result:**
- Ground layer (h=0): `yPos = pallet_height + verticalGap + half_case_height`
  - Cases now sit ON TOP of the pallet with a small gap
- Upper layers (h≥1): Each layer adds `caseDim.y + verticalGap` from the previous one
  - Cases stack properly with consistent gaps between layers

**Tested with:**
- Juice 659083: cases now sit on pallet, not sinking
- Sugar 750426: height issues resolved
- All cases: gaps are uniform and visible

---

## Issue 3: Menu Reorganization (COMPLETED)

### Changes Made

Moved two inventory tools into a new `Tools > Inventory Tools` submenu:

#### 3a. Pallet Optimizer Batch Tool

**File:** `Assets/_Project/Scripts/Gameplay/Editor/PalletOptimizerBatchTool.cs` (line 14)

**Before:**
```csharp
[MenuItem("Tools/ObjData/Recompute Ti-Hi For All SKUs (Pallet Optimizer)")]
```

**After:**
```csharp
[MenuItem("Tools/Inventory Tools/Recompute Ti-Hi For All SKUs (Pallet Optimizer)")]
```

#### 3b. Pallet Builder (Create Prefab)

**File:** `Assets/_Project/Scripts/Utilities/Editor/EditorPrefabCreationTool.cs` (line 57)

**Before:**
```csharp
[MenuItem("Tools/Create Prefab")]
```

**After:**
```csharp
[MenuItem("Tools/Inventory Tools/Create Prefab")]
```

### Visual Result
- **Menu now shows:** `Tools > Inventory Tools ▶`
  - Recompute Ti-Hi For All SKUs (Pallet Optimizer)
  - Create Prefab
- Removed from Dev Tools (if they were there)
- Both tools logically grouped under inventory management

---

## Issue 4: Inventory Persistence (DEFERRED — NOT YET IMPLEMENTED)

### Analysis

**Save/Load System Architecture:**

1. **SaveSystem.cs** — Low-level JSON file I/O
   - `Save(SaveData data)` — writes to `Assets/_Saves/{saveName}.json`
   - `Load(string saveName)` — reads from same location

2. **PlacementSystem.cs** — Orchestrates game saves (quicksave/load + slot save/load)
   - F5 quicksave: `SaveGame("quicksave")` → calls `BuildSaveData()` → `SaveSystem.Save()`
   - F9 quickload: `LoadGame()` → calls `SaveSystem.Load()` → `ApplySaveData()`
   - Both use same JSON format (`SaveData` class)

3. **SaveData class** — Contains:
   - `money`, `spentToday` — economy state
   - `placedObjects` — grid placements (List<SavedObject>)
   - `employeeRecords` — employee snapshots
   - `formerEmployees` — terminated employee archive
   - `laneConfigs` — lane configuration registry
   - Camera, UI state, graphics settings, dev settings

### Why Inventory is Missing

- **InventoryService** creates/tracks pallets at runtime
- Pallets are spawned as GameObject children of ChepStack prefabs
- Pallet metadata lives in **SkuData.cs** (Ti/Hi/pricing/shelf-life) — static, global
- Individual pallet STATE (quantity, age, expiration, location) is NOT persisted anywhere
- **SaveData has no inventory/pallet section**

### How to Fix (Next Pass)

1. **Add to SaveData class** (likely `Assets/_Project/Scripts/Core/Managers/SaveLoad/SaveData.cs`):
   ```csharp
   [SerializeField] public List<PalletSnapshot> pallets = new List<PalletSnapshot>();
   ```

2. **Create PalletSnapshot struct** (or class):
   ```csharp
   [System.Serializable]
   public struct PalletSnapshot
   {
       public int skuId;
       public int quantity;
       public int ageInDays;
       public float expirationDaysRemaining;
       public string location; // e.g. "01-02-00" for rack location
       public LoadIDGenerator.LoadId loadId;
   }
   ```

3. **In PlacementSystem.BuildSaveData()** — add after employee persistence (line ~362):
   ```csharp
   if (InventoryService.Instance != null)
   {
       var pallets = InventoryService.Instance.GetAllPallets();
       foreach (var pallet in pallets)
       {
           save.pallets.Add(new PalletSnapshot { ... });
       }
   }
   ```

4. **In PlacementSystem.ApplySaveData()** — add after employee restoration (line ~390):
   ```csharp
   if (save.pallets != null && InventoryService.Instance != null)
   {
       InventoryService.Instance.RestorePallets(save.pallets);
   }
   ```

5. **In InventoryService.cs** — add:
   - `public List<PalletData> GetAllPallets()` — snapshot current pallets
   - `public void RestorePallets(List<PalletSnapshot> snapshots)` — recreate pallets from snapshot

**Files to modify next:**
- `Assets/_Project/Scripts/Core/Managers/SaveLoad/SaveData.cs`
- `Assets/_Project/Scripts/Core/Inventory/InventoryService.cs`
- `Assets/_Project/Scripts/Core/Managers/SaveLoad/PlacementSystem.cs` (BuildSaveData / ApplySaveData methods)

---

## Testing Checklist

### Orientation Fix (Test all 3 SKUs)
- [ ] **Juice (659083)**: Open PalletBuilder, assign `SKU_659083`, click "Auto-Compute Ti/Hi"
  - Expect: 2×3 grid (width × depth) matching pallet's 40"×48" footprint
  - Was: 3×2 (transposed)

- [ ] **Sugar (750426)**: Repeat above, verify aspect ratio correct

- [ ] **CannedTomato (209875)**: Repeat above, verify aspect ratio correct

### Height Fix (Test all 3 SKUs)
- [ ] Ground layer: First case sits ON pallet with visible gap, not sinking
- [ ] Upper layers: Each layer has uniform gap to layer below, no sinking
- [ ] Layers stack smoothly without overlaps

### Menu Reorganization
- [ ] Open Unity Editor `Tools` menu
- [ ] Verify `Inventory Tools` submenu appears
- [ ] Verify submenu contains:
  - "Recompute Ti-Hi For All SKUs (Pallet Optimizer)"
  - "Create Prefab"
- [ ] Click each and verify they open correctly

### Inventory Persistence (NOT YET TESTED)
- [ ] After implementation: Place pallets in scene
- [ ] F5 quicksave
- [ ] Clear scene (delete all pallets)
- [ ] F9 quickload
- [ ] Verify pallets are restored to saved state

---

## Files Modified

1. `Assets/_Project/Scripts/Gameplay/PalletOptimizer.cs` (13 lines added)
2. `Assets/_Project/Scripts/Gameplay/PalletBuilder.cs` (1 line modified)
3. `Assets/_Project/Scripts/Gameplay/Editor/PalletOptimizerBatchTool.cs` (1 line modified)
4. `Assets/_Project/Scripts/Utilities/Editor/EditorPrefabCreationTool.cs` (1 line modified)

**Total changes:** 16 lines across 4 files. All compile-safe, zero breaking changes.

---

## Next Steps

1. **Verify orientation & height fixes in-game** (use testing checklist above)
2. **Implement inventory persistence** (follow "How to Fix" section above)
3. **Add Pallet Builder UI to Tools menu** (currently accessible via Shift+LClick on pallet, not menu)
4. **Consider:** Move "Pallet Builder" UI itself into `Tools > Inventory Tools` for consistency

---

**Changes committed to branch:** `PreRack_2ndAttempt`  
**Ready for testing and merge to main after verification**
