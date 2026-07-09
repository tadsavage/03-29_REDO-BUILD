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
        /// Instantiates visual pallet prefabs for all pallets restored in InventoryService.
        /// </summary>
        public static void InstantiateRestoredPalletVisuals(PlacementGrid grid)
        {
            if (grid == null)
            {
                Debug.LogError("[InventoryPersistenceService] Cannot restore visuals: PlacementGrid is null.");
                return;
            }

            if (!ServiceLocator.TryGet<InventoryService>(out var inventoryService)) return;

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
            Debug.Log($"[InventoryPersistenceService] Instantiating visuals for {allPallets.Count} pallets...");

            Transform container = GameObject.Find("PlacedObjectsContainer")?.transform;
            if (container == null)
                Debug.LogWarning("[InventoryPersistenceService] PlacedObjectsContainer not found!");

            int instantiated = 0;
            foreach (var record in allPallets)
            {
                if (record == null) continue;

                // 1. Calculate correct world Y for pallet (accounting for stacking)
                Vector3 worldPos = grid.GetCellCenter(record.CurrentLocation);
                var sku = inventoryService.GetSkuData(record.SkuId);

                // Use saved WorldHeightY if available, otherwise calculate from grid
                if (record.WorldHeightY > 0f)
                {
                    worldPos.y = record.WorldHeightY;
                }
                else
                {
                    // Fall back to calculation based on existing pallets in this cell
                    var existingInCell = grid.GetObjectsInCell(record.CurrentLocation);
                    if (existingInCell == null || existingInCell.Count == 0)
                    {
                        worldPos.y = PalletHeightCalculator.CalculateGroundLevelY(sku);
                    }
                    else
                    {
                        float highestTop = 0f;
                        foreach (var obj in existingInCell)
                        {
                            if (obj.instance != null)
                            {
                                highestTop = Mathf.Max(highestTop,
                                    PalletHeightCalculator.GetPalletTop(obj.instance.transform.position.y, sku));
                            }
                        }
                        worldPos.y = highestTop > 0f
                            ? PalletHeightCalculator.CalculateStackedY(highestTop, sku)
                            : PalletHeightCalculator.CalculateGroundLevelY(sku);
                    }
                }

                GameObject go = Object.Instantiate(palletPrefab, worldPos, Quaternion.identity, container);
                go.name = $"RestoredPallet_{record.SkuId}_{record.PalletId.Substring(0, 5)}";
                instantiated++;
                Debug.Log($"[InventoryPersistenceService] Instantiated pallet at {record.CurrentLocation} (worldY={worldPos.y:F2})");

                // 2. Configure PalletBuilder
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

                // 3. Link State (Ghosted vs Received)
                if (string.IsNullOrEmpty(record.LoadId))
                {
                    // Unreceived/Ghosted: Use PalletMasterLink + Ghost Material
                    PalletMasterLink.Attach(go, record.PalletId);
                    if (builder != null && _ghostMaterial != null)
                        builder.GhostCases(_ghostMaterial);
                }
                else
                {
                    // Received/Solid: Add PalletData
                    var data = go.AddComponent<PalletData>();
                    var area = sku != null ? sku.StorageArea : PalletData.AreaCategory.Grocery;
                    var icon = sku != null ? sku.Icon : null;
                    data.Initialize(record.LoadId, record.SkuId, record.Quantity, record.ExpirationDayNumber, area, icon, record.CurrentLocation);
                }
            }

            Debug.Log($"[InventoryPersistenceService] Successfully instantiated {instantiated}/{allPallets.Count} pallets.");
        }
    }
}
