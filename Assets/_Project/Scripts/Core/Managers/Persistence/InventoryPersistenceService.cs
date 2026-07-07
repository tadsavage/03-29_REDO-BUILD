using GameCore.Inventory;
using GameCore.Services;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles saving and loading of inventory state (pallets, locations, SKUs).
/// Works with PalletMasterRecord from InventoryService.
/// </summary>
public static class InventoryPersistenceService
{
    /// <summary>
    /// Snapshot the current inventory state into a serializable format.
    /// </summary>
    public static InventoryPersistenceData Snapshot()
    {
        var data = new InventoryPersistenceData();
        var inventory = ServiceLocator.Get<InventoryService>();

        if (inventory == null)
        {
            Debug.LogWarning("[InventoryPersistenceService] InventoryService not found.");
            return data;
        }

        // Snapshot all pallets in the registry
        foreach (var pallet in inventory.GetAllPallets())
        {
            if (pallet == null) continue;

            data.pallets.Add(new InventoryPersistenceData.PalletSnapshot
            {
                loadId = pallet.LoadId ?? "",
                skuId = pallet.SkuId,
                quantity = pallet.Quantity,
                location = pallet.CurrentLocation.ToString(),
                worldHeightY = pallet.WorldHeightY,
                isReceived = !pallet.IsContaminated,
                shelfLifeDays = pallet.ExpirationDayNumber >= 0 ? pallet.ExpirationDayNumber - pallet.ReceivedDayNumber : 0,
                dateReceived = pallet.ReceivedDayNumber,
                expirationDate = pallet.ExpirationDayNumber
            });
        }

        return data;
    }

    /// <summary>
    /// Restore inventory state from a saved snapshot.
    /// Clears existing inventory and recreates all pallets.
    /// </summary>
    public static void Restore(InventoryPersistenceData data)
    {
        if (data == null || data.pallets == null)
        {
            Debug.LogWarning("[InventoryPersistenceService] No inventory data to restore.");
            return;
        }

        var inventory = ServiceLocator.Get<InventoryService>();
        if (inventory == null)
        {
            Debug.LogError("[InventoryPersistenceService] InventoryService not found during restore. Pallets will NOT be restored!");
            return;
        }

        Debug.Log($"[InventoryPersistenceService] Restoring {data.pallets.Count} pallets...");

        // Clear existing pallets (if any)
        inventory.ClearAllPallets();

        // Recreate each saved pallet
        foreach (var snap in data.pallets)
        {
            Vector2Int location = ParseLocationString(snap.location);

            var pallet = new PalletMasterRecord(snap.skuId, snap.quantity, location, snap.dateReceived, snap.expirationDate)
            {
                LoadId = string.IsNullOrEmpty(snap.loadId) ? null : snap.loadId,
                IsContaminated = snap.isReceived ? false : true,
                WorldHeightY = snap.worldHeightY
            };

            inventory.RegisterPalletDirect(pallet);
        }

        // Note: OnInventoryRestored event invocation removed — event is private to InventoryService
        // The inventory was restored via RegisterPalletDirect calls above

        Debug.Log($"[InventoryPersistenceService] Restored {data.pallets.Count} pallets.");
    }

    /// <summary>
    /// Instantiate visual pallet prefabs for all restored pallets.
    /// Called after Restore() to create the 3D meshes that appear in the world.
    /// Gets the prefab reference from TruckController if available, falls back to Resources.
    /// </summary>
    public static void InstantiateRestoredPalletVisuals()
    {
        var inventory = ServiceLocator.Get<InventoryService>();
        if (inventory == null)
        {
            Debug.LogError("[InventoryPersistenceService] InventoryService not found during visual restore.");
            return;
        }

        var grid = Object.FindAnyObjectByType<PlacementGrid>();
        if (grid == null)
        {
            Debug.LogError("[InventoryPersistenceService] PlacementGrid not found — cannot position pallets.");
            return;
        }

        // Try to get the pallet visual prefab from TruckController (where it's assigned in Inspector)
        GameObject palletVisualPrefab = null;
        var truck = Object.FindAnyObjectByType<TruckController>();
        if (truck != null)
        {
            // TruckController has the prefab but it's private — we'll try to get it via reflection
            var field = typeof(TruckController).GetField("palletVisualPrefab",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                palletVisualPrefab = field.GetValue(truck) as GameObject;
        }

        // Fallback: load from Resources
        if (palletVisualPrefab == null)
        {
            palletVisualPrefab = Resources.Load<GameObject>("Inventory/Prefabs/ChepStack");
        }

        if (palletVisualPrefab == null)
        {
            Debug.LogWarning("[InventoryPersistenceService] Cannot find pallet visual prefab (tried TruckController and Resources/Inventory/Prefabs/ChepStack) — pallets will be invisible.");
            return;
        }

        var allPallets = inventory.GetAllPallets();
        int visualCount = 0;

        // Ensure a container for all pallet visuals
        var palletContainer = GameObject.Find("PalletVisualsContainer");
        if (palletContainer == null)
        {
            palletContainer = new GameObject("PalletVisualsContainer");
            palletContainer.hideFlags = HideFlags.HideInHierarchy;
        }

        Debug.Log($"[InventoryPersistenceService] Instantiating {allPallets.Count} pallet visuals...");

        foreach (var pallet in allPallets)
        {
            if (pallet == null) continue;

            Vector3 worldPos = grid.GetCellCenter(pallet.CurrentLocation);
            // Override the Y coordinate with the saved height (preserves rack/multi-level positioning)
            worldPos.y = pallet.WorldHeightY;

            // Instantiate the pallet visual prefab
            GameObject palletInstance = Object.Instantiate(palletVisualPrefab, worldPos, Quaternion.identity, palletContainer.transform);
            palletInstance.name = $"Pallet_{pallet.LoadId ?? $"SKU{pallet.SkuId}"}";

            // Get the PalletBuilder component and configure it
            PalletBuilder builder = palletInstance.GetComponentInChildren<PalletBuilder>();
            if (builder == null)
            {
                Debug.LogWarning($"[InventoryPersistenceService] Pallet visual prefab has no PalletBuilder component for pallet {pallet.LoadId}");
                Object.Destroy(palletInstance);
                continue;
            }

            // Get the SKU data to find the case prefab
            SkuData sku = inventory.GetSkuData(pallet.SkuId);
            if (sku == null || sku.Prefab == null)
            {
                Debug.LogWarning($"[InventoryPersistenceService] Missing SkuData or CasePrefab for SKU {pallet.SkuId}");
                Object.Destroy(palletInstance);
                continue;
            }

            // Configure the PalletBuilder with the SKU's case prefab and Ti/Hi
            builder.casePrefab = sku.Prefab;
            if (sku.Ti > 0 && sku.Hi > 0)
            {
                builder.useTiHiOverride = true;
                builder.manualTi = sku.Ti;
                builder.manualHi = sku.Hi;
            }

            // Build the pallet cases (deductMoney = false, we're just visualizing)
            builder.Build(deductMoney: false);

            // Fix floating cases (same as TruckController does)
            FixFloatingCases(palletInstance);

            visualCount++;
            Debug.Log($"[InventoryPersistenceService] Restored visual for pallet {pallet.LoadId} (SKU {pallet.SkuId}) at {pallet.CurrentLocation}");
        }

        Debug.Log($"[InventoryPersistenceService] Finished instantiating {visualCount}/{allPallets.Count} pallet visuals.");
    }

    /// <summary>
    /// Fix cases that are floating above the pallet deck.
    /// Repositions them so they sit properly on the pallet (at Y = 0.16m).
    /// </summary>
    private static void FixFloatingCases(GameObject palletInstance)
    {
        var palletLoad = palletInstance.transform.Find("PalletLoad");
        if (palletLoad == null) return;

        const float palletDeckHeight = 0.16f;
        float minCaseY = float.MaxValue;
        var casesList = new List<Transform>();

        for (int i = 0; i < palletLoad.childCount; i++)
        {
            var child = palletLoad.GetChild(i);
            casesList.Add(child);
            if (child.localPosition.y < minCaseY)
                minCaseY = child.localPosition.y;
        }

        // Shift all cases down so the lowest sits at pallet deck height
        if (casesList.Count > 0 && minCaseY != float.MaxValue && minCaseY != palletDeckHeight)
        {
            float shift = palletDeckHeight - minCaseY;
            foreach (var caseTransform in casesList)
            {
                var pos = caseTransform.localPosition;
                caseTransform.localPosition = new Vector3(pos.x, pos.y + shift, pos.z);
            }
        }
    }

    private static Vector2Int ParseLocationString(string locationStr)
    {
        if (string.IsNullOrEmpty(locationStr)) return Vector2Int.zero;

        var parts = locationStr.Split(',');
        if (parts.Length >= 2 && int.TryParse(parts[0].Trim('(', ' '), out int x) && int.TryParse(parts[1].Trim(')', ' '), out int y))
            return new Vector2Int(x, y);

        return Vector2Int.zero;
    }
}
