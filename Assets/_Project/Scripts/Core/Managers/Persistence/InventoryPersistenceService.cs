using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;
using System.Linq;

namespace GameCore.Persistence
{
    /// <summary>
    /// Handles persistence of pallet visuals after data is restored from save.
    /// When pallets are loaded from SaveData, their inventory records are restored
    /// (data-only) but their visual GameObjects (ChepStack 3D models) must be
    /// instantiated separately. This service bridges that gap.
    /// </summary>
    public static class InventoryPersistenceService
    {
        private static Material _ghostMaterial;

        /// <summary>
        /// Instantiates visual pallet prefabs for all pallets in staging lanes.
        /// Properly stacks pallets by calculating heights based on what's below each pallet.
        /// </summary>
        public static void InstantiateRestoredPalletVisuals(PlacementGrid grid)
        {
            if (grid == null)
            {
                Debug.LogError("[InventoryPersistenceService] Cannot restore visuals: PlacementGrid is null.");
                return;
            }

            if (!ServiceLocator.TryGet<InventoryService>(out var inventoryService)) return;

            // CLEANUP: Destroy any lingering pallet visuals from the previous session
            // These are ChepEmpty instances that weren't cleared on the last save/exit
            var oldPallets = Object.FindObjectsByType<PalletBuilder>(FindObjectsSortMode.None);
            int destroyedCount = 0;
            foreach (var oldPalletBuilder in oldPallets)
            {
                // Only destroy dock pallets (RestoredPallet_*), not racks
                if (oldPalletBuilder.gameObject.name.Contains("RestoredPallet"))
                {
                    Object.Destroy(oldPalletBuilder.gameObject);
                    destroyedCount++;
                }
            }
            if (destroyedCount > 0)
                Debug.Log($"[InventoryPersistenceService] Cleaned up {destroyedCount} old pallet visuals");

            // Load resources
            var palletPrefab = Resources.Load<GameObject>("ChepEmpty");
            if (palletPrefab == null)
            {
                Debug.LogError("[InventoryPersistenceService] ChepEmpty prefab not found in Resources/");
                return;
            }

            if (_ghostMaterial == null)
                _ghostMaterial = Resources.Load<Material>("Materials/GhostLoweredWall");

            var allPallets = inventoryService.GetAllPallets();
            Debug.Log($"[InventoryPersistenceService] Restoring pallet visuals for dock ({allPallets.Count} total in inventory)...");

            Transform container = GameObject.Find("PlacedObjectsContainer")?.transform;
            if (container == null)
                Debug.LogWarning("[InventoryPersistenceService] PlacedObjectsContainer not found!");

            // Sort pallets: group by cell, then by saved height (bottom to top)
            // This ensures we instantiate stack-bottom-first, so height calculations work correctly
            var sortedPallets = allPallets
                .Where(p => p != null && !string.IsNullOrEmpty(p.StagingLaneId))
                .OrderBy(p => p.CurrentLocation.x)
                .ThenBy(p => p.CurrentLocation.y)
                .ThenBy(p => p.WorldHeightY)
                .ToList();

            Debug.Log($"[InventoryPersistenceService] Processing {sortedPallets.Count} dock pallets (sorted by cell + height)");

            // Track instantiated pallets per cell to handle stacking correctly
            var palletsByCell = new System.Collections.Generic.Dictionary<Vector2Int, System.Collections.Generic.List<PalletInstance>>();

            int instantiated = 0;
            foreach (var record in sortedPallets)
            {
                if (record == null) continue;

                // Only instantiate visuals for pallets in staging lanes
                if (string.IsNullOrEmpty(record.StagingLaneId))
                    continue;

                var sku = inventoryService.GetSkuData(record.SkuId);
                Vector3 worldPos = grid.GetCellCenter(record.CurrentLocation);

                // Ensure cell exists in tracking dictionary
                if (!palletsByCell.ContainsKey(record.CurrentLocation))
                    palletsByCell[record.CurrentLocation] = new System.Collections.Generic.List<PalletInstance>();

                var palletsInCell = palletsByCell[record.CurrentLocation];

                // Use saved WorldHeightY if available (it already accounts for case height + stacking)
                // Only recalculate if not saved (shouldn't happen, but safety fallback)
                if (record.WorldHeightY > 0f)
                {
                    worldPos.y = record.WorldHeightY;
                    Debug.Log($"[InventoryPersistenceService] Using saved height: {record.WorldHeightY:F3}");
                }
                else
                {
                    // Fallback: calculate based on what's already in this cell
                    if (palletsInCell.Count == 0)
                    {
                        // First pallet in this cell: ground level
                        worldPos.y = PalletHeightCalculator.CalculateGroundLevelY(sku);
                    }
                    else
                    {
                        // Stack on top of the last pallet in this cell
                        var lastPallet = palletsInCell[palletsInCell.Count - 1];
                        float topOfLastPallet = PalletHeightCalculator.GetPalletTop(lastPallet.worldY, lastPallet.sku);
                        worldPos.y = PalletHeightCalculator.CalculateStackedY(topOfLastPallet, sku);
                    }
                }

                // Instantiate the visual
                GameObject go = Object.Instantiate(palletPrefab, worldPos, Quaternion.identity, container);
                go.name = $"RestoredPallet_{record.SkuId}_{record.PalletId.Substring(0, 5)}";
                instantiated++;
                Debug.Log($"[InventoryPersistenceService] Instantiated pallet at {record.CurrentLocation} (worldY={worldPos.y:F3}, stack={palletsInCell.Count})");

                // Sync the prefab's baked-in PlacedObject component to this restore (mirrors what
                // TrailerOffloadController.RegisterAndQueue does for a freshly-offloaded pallet).
                // Without this, gridX/gridY/worldSpaceYHeight stay at the prefab's (0,0,0) defaults —
                // harmless for grid cell lookup (PlacementGrid.RebuildFromRegistry derives the cell
                // from transform.position, not these fields) but means a subsequent save would fall
                // back to reading transform.position.y for entry.worldY, and customData wouldn't
                // carry the pallet ID for anything that inspects it directly off the component.
                var restoredPO = go.GetComponent<PlacedObject>();
                if (restoredPO != null)
                {
                    restoredPO.gridX = record.CurrentLocation.x;
                    restoredPO.gridY = record.CurrentLocation.y;
                    restoredPO.worldSpaceYHeight = worldPos.y;
                    restoredPO.customData = record.PalletId;
                }

                // Update the record with calculated height
                record.WorldHeightY = worldPos.y;

                // Track this pallet for stacking calculations
                palletsInCell.Add(new PalletInstance { worldY = worldPos.y, sku = sku });

                // Configure PalletBuilder
                var builder = go.GetComponentInChildren<PalletBuilder>();
                if (builder != null && sku != null)
                {
                    builder.casePrefab = sku.Prefab;
                    if (sku.Ti > 0 && sku.Hi > 0)
                    {
                        builder.useTiHiOverride = true;
                        builder.manualTi = sku.Ti;
                        builder.manualHi = sku.Hi;
                    }
                    builder.Build(deductMoney: false);
                }

                // Link State (Ghosted vs Received)
                if (string.IsNullOrEmpty(record.LoadId))
                {
                    // Unreceived/Ghosted
                    PalletMasterLink.Attach(go, record.PalletId);
                    if (builder != null && _ghostMaterial != null)
                        builder.GhostCases(_ghostMaterial);
                }
                else
                {
                    // Received/Solid
                    var data = go.AddComponent<PalletData>();
                    var area = sku != null ? sku.StorageArea : PalletData.AreaCategory.Grocery;
                    var icon = sku != null ? sku.Icon : null;
                    data.Initialize(record.LoadId, record.SkuId, record.Quantity, record.ExpirationDayNumber, area, icon, record.CurrentLocation);
                }
            }

            Debug.Log($"[InventoryPersistenceService] Successfully instantiated {instantiated} dock pallets.");
        }

        private struct PalletInstance
        {
            public float worldY;
            public SkuData sku;
        }
    }
}
