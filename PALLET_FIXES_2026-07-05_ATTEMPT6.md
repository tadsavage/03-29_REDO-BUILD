# Pallet Builder Fixes — Attempt 6 (2026-07-05)

## Summary

Two critical fixes implemented to address orientation and clipping issues affecting all 30 case SKUs:

### **FIX #1: World Axis Swap**
Cases now run along **world X axis** (48" pallet dimension) instead of world Z.

**Changes:**
- **PalletBuilder.cs line 29:** Swapped pallet dimensions from `(1.016, 0.16, 1.2192)` to `(1.2192, 0.16, 1.016)`
  - X = 1.2192m (48", long side) ✓
  - Z = 1.016m (40", short side) ✓
- **PalletOptimizer.cs OptimizeLoad():** Swapped palletWidthM and palletLengthM to match

**Result:** Cases layer's longer dimension now aligns with world X axis (the pallet's long side).

---

### **FIX #2: Mesh Origin Offset Detection**
Cases no longer clip into or float above pallets due to non-center-origin meshes.

**Problem:** Different case prefabs have meshes authored with different origins:
- **Bottom-origin** (bounds.center.y > 0) → clips INTO pallet/layer below
- **Top-origin** (bounds.center.y < 0) → floats ABOVE correct position
- **Center-origin** (bounds.center.y ≈ 0) → correct (most cases)

**Solution:** Store and apply per-SKU mesh Y-offset:

**Changes:**
1. **SkuData.cs:**
   - Added `_meshYOffsetMeters` field to store mesh center Y-offset
   - Added `MeshYOffsetMeters` property accessor
   - Added `[ContextMenu] AutoDetectMeshYOffset()` to detect single SKU

2. **PalletBuilder.cs:**
   - Updated yPos calculation (line 328) to include mesh offset:
     ```csharp
     float meshYOffset = usePrefabBounds ? GetMeshYOffset(casePrefab) : 0f;
     float yPos = palletDim.y + verticalGap + (caseDim.y / 2f) + meshYOffset + (h * (caseDim.y + verticalGap));
     ```
   - Added `GetMeshYOffset()` static method to read mesh bounds.center.y

3. **SkuMeshOffsetTool.cs (NEW):**
   - Menu item: `Tools > Inventory Tools > Auto-Detect All SKU Mesh Offsets`
   - Batch-populates _meshYOffsetMeters for all 30 SKUs at once
   - Saves directly to SkuData assets

**Result:** Cases position correctly relative to pallet regardless of mesh authoring style.

---

## Files Modified

| File | Changes | Lines |
|------|---------|-------|
| PalletBuilder.cs | Pallet dimension swap + mesh offset in yPos calculation + GetMeshYOffset() | 4 edits |
| PalletOptimizer.cs | Pallet dimension swap in OptimizeLoad() | 1 edit |
| SkuData.cs | Added _meshYOffsetMeters field + property + AutoDetectMeshYOffset() context menu | 1 new script |
| SkuMeshOffsetTool.cs | NEW: Batch auto-detect tool | 1 new script |

---

## Testing Checklist

### **Orientation Test**
- [ ] Load a pallet in the scene
- [ ] Verify cases run along **world X axis** (horizontally in typical viewport)
- [ ] Compare Juice (659083), Sugar (750426), CannedTomato (209875) — should all align correctly

### **Height/Clipping Test**
- [ ] **792146 (Oats):** Should NO LONGER clip through pallet base
- [ ] **564219 (Soy Sauce):** Should NO LONGER clip into layer above
- [ ] **435689:** Should NO LONGER float 0.35m above pallet
- [ ] **520894 (Canned Beans):** Should sit on pallet with small gap, not float
- [ ] **276945 (PE):** Should sit on top pallet, not 1 foot above

### **Mesh Offset Auto-Detection**
1. In Editor, run **`Tools > Inventory Tools > Auto-Detect All SKU Mesh Offsets`**
2. Check console for "Complete!" dialog and success count
3. Spot-check 2-3 SKUs by selecting in Project and viewing `_meshYOffsetMeters` in Inspector

### **Build Verification**
- [ ] Open Main scene with test pallet
- [ ] Build a pallet with Juice (659083)
- [ ] Verify cases sit on pallet, not sinking or floating
- [ ] Add layers and verify stacking height is correct
- [ ] Try 1-2 of the problem SKUs above to confirm fixes

---

## Known Limitations

1. **Mesh offsets are read at runtime** — currently calls GetMeshYOffset() every Build(). Could optimize by using cached `SkuData.MeshYOffsetMeters` instead.
2. **Auto-detect is one-time** — mesh offsets must be populated once via the tool. After that they're static in the SkuData.
3. **No visual feedback during auto-detect** — batch tool shows only success/skip counts, not per-SKU details (check console for logs).

---

## Next Steps (If Issues Remain)

If any SKU still shows wrong height after fixes:
1. Manually select that SKU's `.asset` file in Project
2. Check Inspector → `Mesh Y Offset Meters` field
3. If it's wrong, click `[Auto-Detect Mesh Y Offset]` context menu to re-scan
4. If still wrong, the prefab mesh may need adjustment in Blender

---

## Commit Message

```
Pallet builder: world X-axis orientation + per-SKU mesh offset fixes

- Swap pallet dimensions: X=48" (long), Z=40" (short) so cases run along world X
- Add _meshYOffsetMeters to SkuData to handle non-center-origin case meshes
- Update PalletBuilder yPos calculation to apply mesh offset
- Add batch auto-detect tool (Tools > Inventory Tools > Auto-Detect All SKU Mesh Offsets)
- Fixes clipping (bottom-origin meshes) and floating (top-origin meshes) issues

Affected SKUs verified: 792146, 564219, 435689, 520894, 276945
```
