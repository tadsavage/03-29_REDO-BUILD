using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Calculates world Y position for pallets, accounting for stacking and floor/foundation heights.
    /// All measurements in meters (not grid units).
    /// </summary>
    public static class PalletHeightCalculator
    {
        // Constants — tuned per warehouse geometry (2026-07-09)
        private const float CHEP_PALLET_HEIGHT = 0.1f;      // ChepEmpty base height
        private const float FLOOR_TILE_HEIGHT = 0.0f;       // Included in foundation height
        private const float FOUNDATION_HEIGHT = 1.15f;      // Dock/ground level (foundation ground face)
        private const float GAP_BETWEEN_OBJECTS = 0.02f;    // Anti-clipping grace space

        /// <summary>
        /// Calculate world Y for a pallet at ground level (on dock/staging lane).
        /// Formula: foundation(1.06) + floor_tile(0.06) + gap(0.015) + pallet_height(0.16) + case_stack_height
        /// </summary>
        public static float CalculateGroundLevelY(SkuData sku)
        {
            if (sku == null) return FOUNDATION_HEIGHT + FLOOR_TILE_HEIGHT + GAP_BETWEEN_OBJECTS;

            float caseStackHeight = CalculateCaseStackHeight(sku);
            return FOUNDATION_HEIGHT + FLOOR_TILE_HEIGHT + GAP_BETWEEN_OBJECTS + CHEP_PALLET_HEIGHT + caseStackHeight;
        }

        /// <summary>
        /// Calculate world Y for a stacked pallet on top of another pallet.
        /// Formula: pallet_below_top + pallet_height(0.16) + gap(0.015) + case_stack_height
        /// </summary>
        public static float CalculateStackedY(float palletBelowTop, SkuData sku)
        {
            if (sku == null) return palletBelowTop + CHEP_PALLET_HEIGHT + GAP_BETWEEN_OBJECTS;

            float caseStackHeight = CalculateCaseStackHeight(sku);
            return palletBelowTop + CHEP_PALLET_HEIGHT + GAP_BETWEEN_OBJECTS + caseStackHeight;
        }

        /// <summary>
        /// Calculate just the case stack height: Hi (layers) * case_height.
        /// This is used to determine the total vertical extent of the pallet's cargo.
        /// </summary>
        public static float CalculateCaseStackHeight(SkuData sku)
        {
            if (sku == null || sku.Prefab == null) return 0f;

            // Get case prefab dimensions
            var caseDims = PalletBuilder.GetPrefabDimensions(sku.Prefab);
            float caseHeight = caseDims.y;
            int layers = Mathf.Max(1, sku.Hi);

            return caseHeight * layers;
        }

        /// <summary>
        /// Get the top of a pallet (Y position + height to top of cases).
        /// Useful for stacking calculations.
        /// </summary>
        public static float GetPalletTop(float palletY, SkuData sku)
        {
            if (sku == null) return palletY + CHEP_PALLET_HEIGHT;
            return palletY + CHEP_PALLET_HEIGHT + CalculateCaseStackHeight(sku);
        }
    }
}
