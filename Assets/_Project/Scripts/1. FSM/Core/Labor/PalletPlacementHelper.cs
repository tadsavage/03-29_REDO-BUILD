using UnityEngine;
using GameCore.Inventory;

namespace GameCore.Labor
{
    /// <summary>
    /// Helper for calculating pallet placement Y positions on the dock.
    /// Pallets are now saved as regular PlacedObjects through PlacementSystem,
    /// which automatically saves their worldY position.
    /// </summary>
    public static class PalletPlacementHelper
    {
        /// <summary>
        /// Calculate world Y position for a pallet on the dock at a given grid cell.
        /// This accounts for:
        /// - Foundation height (1.06m)
        /// - Floor tile height (0.06m)
        /// - Pallet base height (0.16m)
        /// - Case stack height (based on SKU Hi/case height)
        /// - Stacking on top of existing pallets in the same cell
        /// </summary>
        public static float CalculatePalletPlacementY(Vector2Int gridCell, SkuData sku, PlacementGrid grid)
        {
            if (grid == null)
                return PalletHeightCalculator.CalculateGroundLevelY(sku);

            // Check if there are already pallets in this cell (stacking case)
            var existingObjects = grid.GetObjectsInCell(gridCell);

            if (existingObjects == null || existingObjects.Count == 0)
            {
                // Ground level: foundation + floor + gap + pallet + cases
                return PalletHeightCalculator.CalculateGroundLevelY(sku);
            }

            // Find the highest existing pallet in this cell
            float highestTop = 0f;
            foreach (var placedObj in existingObjects)
            {
                if (placedObj.instance != null)
                {
                    // Assume all pallets use the same SKU for height calculation (simplification)
                    float top = PalletHeightCalculator.GetPalletTop(placedObj.instance.transform.position.y, sku);
                    highestTop = Mathf.Max(highestTop, top);
                }
            }

            if (highestTop > 0f)
                return PalletHeightCalculator.CalculateStackedY(highestTop, sku);
            else
                return PalletHeightCalculator.CalculateGroundLevelY(sku);
        }
    }
}
