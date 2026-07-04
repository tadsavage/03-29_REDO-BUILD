using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;
using System.Collections.Generic;
using System.Linq;

namespace GameCore.Utilities
{
    /// <summary>
    /// A simple tool to trigger warehouse game loop sequences for testing.
    /// </summary>
    public class WarehouseDebugTool : MonoBehaviour
    {
        [Header("Test Shipment Settings")]
        public string supplierName = "SAVAGE DISTRIBUTORS";
        public int palletCount = 12;

        [ContextMenu("Spawn Test Inbound Truck")]
        public void SpawnTestTruck()
        {
            var shipmentService = ServiceLocator.Get<ShipmentService>();
            if (shipmentService == null)
            {
                Debug.LogError("[WarehouseDebugTool] ShipmentService not found!");
                return;
            }

            // Find some random SKUs to fill the shipment
            var skus = Resources.LoadAll<SkuData>("Inventory/SKUs");
            if (skus.Length == 0)
            {
                Debug.LogWarning("[WarehouseDebugTool] No SKUs found in Resources/Inventory/SKUs. Using IDs directly.");
            }

            var lineItems = new List<ShipmentLineItem>();
            for (int i = 0; i < palletCount; i++)
            {
                string skuId = skus.Length > 0 ? skus[Random.Range(0, skus.Length)].SkuId : $"SKU_{3500000 + i}";
                // 100 cases per pallet
                lineItems.Add(new ShipmentLineItem(skuId, 100, 10, 30));
            }

            Debug.Log("[WarehouseDebugTool] Triggering test shipment sequence...");
            shipmentService.CreatePurchaseOrder("SUPP_DEBUG", supplierName, lineItems);
        }
    }
}
