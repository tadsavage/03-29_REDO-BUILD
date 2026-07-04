using GameCore.Inventory;
using GameCore.Services;
using UnityEngine;

namespace GameCore.Labor
{
    /// <summary>
    /// Bridges a docked truck's shipment to the inventory/work-queue layer: converts the shipment's line
    /// items into received pallets (each with a unique Load ID) sitting in inbound staging, then creates a
    /// Putaway work task per pallet. This is the CHUNK 1 (Inbound) finish line — the receiver-with-clipboard
    /// animation from the spec doesn't exist yet, so the truck's existing unload timer stands in for the
    /// physical offload; this is the "receiving" step that turns that timer's completion into real inventory.
    /// </summary>
    public static class ReceivingService
    {
        public static void ProcessReceiving(ShipmentData shipment)
        {
            if (shipment == null) return;
            if (shipment.Status != ShipmentData.ShipmentStatus.InTransit) return; // already processed
            if (!ServiceLocator.TryGet<InventoryService>(out var inventory) || inventory == null) return;
            if (!ServiceLocator.TryGet<WorkQueueSystem>(out var queue) || queue == null) return;

            foreach (var item in shipment.LineItems)
            {
                var pallet = inventory.ReceivePalletWithLoadId(item.SkuId, item.Quantity, item.ShelfLifeDays);
                queue.CreateTask(
                    WorkTaskType.Putaway,
                    EmployeeRole.ReachTruckOperator,
                    pallet.PalletId,
                    $"Putaway pallet [{pallet.LoadId}] ({item.Quantity} x {item.SkuId})");
            }

            shipment.Status = ShipmentData.ShipmentStatus.Received;
            Debug.Log($"[ReceivingService] Shipment {shipment.ShipmentId} received: {shipment.LineItems.Count} pallets, Load IDs assigned, Putaway tasks queued.");
        }
    }
}
