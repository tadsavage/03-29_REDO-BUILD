using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using GameCore.Labor;
using GameCore.Services;
using UnityEngine;

namespace GameCore.Persistence
{
    /// <summary>
    /// Saves and restores OUTBOUND pallets that no other persistence path owns (2026-10-17 save/load audit):
    ///   - a case-picked (mixed SKU) pallet standing in a staging lane. The selector strips its PlacedObject, so
    ///     PalletPersistenceService never saw it and it vanished on load while the order still counted its cases picked;
    ///   - any outbound pallet riding a selector, a pallet jack or a dock stocker (mid-pick, mid-load).
    /// Whole picked pallets standing in a lane keep their PlacedObject and stay with PalletPersistenceService; pallets
    /// already in a trailer stay with TruckPersistenceService (TrailerPalletSnapshot carries the order tag now).
    ///
    /// Restore is two-phase. <see cref="RestoreAll"/> runs inside ApplySaveData BEFORE ReconcileStagedOrdersAgainstScene,
    /// so an order whose freight is being restored is never mistaken for one that lost it. Pallets that were being
    /// carried are first stood on the floor where they were; <see cref="SetDownCarried"/> (called once lane geometry
    /// exists, from the employee respawn coroutine) then moves each into a free slot of its order's lanes as STAGED. The
    /// unfinished pick or load task is released by the normal stale-task sweep and simply carries on from there.
    /// </summary>
    public static class OutboundPalletPersistence
    {
        private static readonly List<OutboundPalletBuilder> s_toSetDown = new();

        public static List<OutboundPalletSnapshot> CaptureAll()
        {
            var result = new List<OutboundPalletSnapshot>();
            foreach (var p in Object.FindObjectsByType<OutboundPalletBuilder>(FindObjectsSortMode.None))
            {
                if (p == null || string.IsNullOrEmpty(p.OrderId)) continue;
                if (p.GetComponentInParent<TruckController>() != null) continue;      // trailer cargo: TruckPersistenceService
                if (p.IsOnFloor && OwnedByDockPalletPath(p)) continue;               // whole pallet in a lane: PalletPersistenceService

                var snap = new OutboundPalletSnapshot
                {
                    orderId = p.OrderId,
                    stage = (int)p.Stage,
                    wasCarried = !p.IsOnFloor || p.Stage == OutboundPalletBuilder.PalletStage.Loading
                                              || p.Stage == OutboundPalletBuilder.PalletStage.Moving,
                    worldPosition = p.transform.position,
                    worldRotation = p.transform.rotation,
                };

                var pd = p.GetComponent<PalletData>();
                if (p.CaseCountsBySku.Count == 0 && pd != null && !string.IsNullOrEmpty(pd.ItemNumber))
                {
                    snap.wholeSkuId = pd.ItemNumber;
                    snap.wholeCases = pd.CaseQuantity > 0 ? pd.CaseQuantity : p.TotalCases;
                }

                var load = p.transform.Find("PalletLoad");
                if (load != null)
                {
                    int n = load.childCount;
                    for (int i = 0; i < n; i++)
                    {
                        var c = load.GetChild(i);
                        snap.caseLocalPositions.Add(c.localPosition);
                        snap.caseLocalRotations.Add(c.localRotation);
                        snap.caseSkus.Add(p.CaseSkus.Count == n ? p.CaseSkus[i] : snap.wholeSkuId);
                    }
                }
                result.Add(snap);
            }
            if (result.Count > 0)
                Debug.Log($"[OutboundPalletPersistence] Captured {result.Count} outbound pallet(s) ({result.Count(s => s.wasCarried)} mid-carry).");
            return result;
        }

        /// <summary>PalletPersistenceService captures it: a registered, enabled Inventory PlacedObject with a PalletBuilder.</summary>
        private static bool OwnedByDockPalletPath(OutboundPalletBuilder p)
        {
            var po = p.GetComponent<PlacedObject>();
            return po != null && po.enabled && po.data != null && po.data.category == "Inventory"
                   && p.GetComponent<PalletBuilder>() != null && PlacedObjectRegistry.All.Contains(po);
        }

        public static void RestoreAll(List<OutboundPalletSnapshot> snapshots)
        {
            s_toSetDown.Clear();
            if (snapshots == null || snapshots.Count == 0) return;

            var prefab = ResolveChepEmptyPrefab();
            if (prefab == null)
            {
                Debug.LogError("[OutboundPalletPersistence] No buildable pallet prefab in the Inventory build menu - cannot restore outbound pallets.");
                return;
            }
            ServiceLocator.TryGet<InventoryService>(out var inv);

            int restored = 0;
            foreach (var snap in snapshots)
            {
                if (snap == null || string.IsNullOrEmpty(snap.orderId)) continue;

                var go = Object.Instantiate(prefab);
                go.name = $"OutboundPallet_{snap.orderId}_restored";
                foreach (var c in go.GetComponents<PlacedObject>()) { c.enabled = false; Object.Destroy(c); }
                var bd = go.GetComponent<BuildingData>(); if (bd != null) Object.Destroy(bd);
                var bh = go.GetComponent<BuildingHighlighter>(); if (bh != null) Object.Destroy(bh);
                var pb = go.GetComponent<PalletBuilder>(); if (pb != null) Object.DestroyImmediate(pb);
                var stale = go.transform.Find("PalletLoad"); if (stale != null) Object.DestroyImmediate(stale.gameObject);

                Vector3 pos = snap.worldPosition;
                if (snap.wasCarried) pos.y = 1.15f;   // lane surface height: stand it on the floor until SetDownCarried
                go.transform.SetPositionAndRotation(pos, snap.worldRotation);

                var outbound = go.AddComponent<OutboundPalletBuilder>();
                outbound.OrderId = snap.orderId;
                int count = Mathf.Min(snap.caseLocalPositions.Count, snap.caseLocalRotations.Count);
                for (int i = 0; i < count; i++)
                {
                    string skuId = i < snap.caseSkus.Count && !string.IsNullOrEmpty(snap.caseSkus[i]) ? snap.caseSkus[i] : snap.wholeSkuId;
                    var sku = inv?.GetSkuData(skuId);
                    Vector3 dim = sku != null ? new Vector3(sku.CaseWidth, sku.CaseHeight, sku.CaseLength) : new Vector3(0.4f, 0.3f, 0.3f);
                    outbound.RestoreCase(skuId, sku?.Prefab, dim, sku != null ? Mathf.Max(1, sku.Ti) : 1,
                                         sku != null ? Mathf.Max(1, sku.Hi) : 1, snap.caseLocalPositions[i], snap.caseLocalRotations[i]);
                }

                // A whole pallet keeps a PalletData so ReclaimOrphanedFreight can return it to stock if its order is gone.
                if (!string.IsNullOrEmpty(snap.wholeSkuId))
                {
                    var pdata = go.GetComponent<PalletData>() ?? go.AddComponent<PalletData>();
                    var sku = inv?.GetSkuData(snap.wholeSkuId);
                    pdata.Initialize("", snap.wholeSkuId, snap.wholeCases, -1,
                                     sku != null ? sku.StorageArea : PalletData.AreaCategory.Grocery, sku?.Icon, Vector2Int.zero);
                }

                if (snap.wasCarried)
                {
                    outbound.Stage = OutboundPalletBuilder.PalletStage.Moving;   // IN TRANSIT until it lands in a lane
                    outbound.SetNavObstacleActive(false);
                    s_toSetDown.Add(outbound);
                }
                else
                {
                    var stage = (OutboundPalletBuilder.PalletStage)snap.stage;
                    outbound.Stage = stage == OutboundPalletBuilder.PalletStage.Shipped ? OutboundPalletBuilder.PalletStage.Staged : stage;
                    outbound.SetNavObstacleActive(true);
                }
                restored++;
            }
            Debug.Log($"[OutboundPalletPersistence] Restored {restored}/{snapshots.Count} outbound pallet(s); {s_toSetDown.Count} to set down in a lane.");
        }

        /// <summary>Phase 2: every pallet that was mid-carry goes into a free slot of its order's lanes, STAGED. Needs lane
        /// geometry, so it runs from the post-load coroutine. A pallet with nowhere to go stays on the floor and is reported.</summary>
        public static void SetDownCarried()
        {
            if (s_toSetDown.Count == 0) return;
            ServiceLocator.TryGet<InventoryService>(out var inv);
            ServiceLocator.TryGet<OrderService>(out var orders);

            foreach (var p in s_toSetDown)
            {
                if (p == null) continue;
                var order = orders?.ActiveOrders.FirstOrDefault(o => o.OrderId == p.OrderId);
                bool placed = false;
                if (order != null && inv != null &&
                    StagingLaneAssignmentService.TryFindTripLane(orders, inv, order, 1, out int door, out string lane) &&
                    inv.TryFindStagingSlotInLane(door, lane, out var slot) &&
                    LaneNamingService.TryGetSlotWorldPos(slot.Cell, out var slotPos))
                {
                    float y = TrailerOffloadController.StagingDropBaseY(door, lane, slot.Cell, p.gameObject);
                    p.transform.position = new Vector3(slotPos.x, y, slotPos.z);
                    if (LaneNamingService.TryGetLaneGeometry(door, lane, out var geo) && geo.DepthAxis.sqrMagnitude > 0.0001f)
                        p.transform.rotation = Quaternion.LookRotation(geo.DepthAxis.normalized, Vector3.up);
                    if (!(door == order.EffectiveStagingDoor && lane == order.AssignedLane)) order.AddOverflowLane(door, lane);
                    placed = true;
                    Debug.LogWarning($"[OutboundPalletPersistence] Pallet for order {order.OrderNumber} was mid-carry at save time - set down STAGED in {door}{lane}.");
                }
                else
                {
                    Debug.LogWarning($"[OutboundPalletPersistence] Pallet for order {p.OrderId} was mid-carry at save time and has no lane to go to - left on the floor at {p.transform.position}.");
                }
                p.Stage = OutboundPalletBuilder.PalletStage.Staged;
                p.SetNavObstacleActive(true);
                if (placed && order != null && order.Status == OrderData.OrderStatus.Loaded)
                    order.Status = OrderData.OrderStatus.Loading;   // freight back in the lane: the loader must come for it
            }
            s_toSetDown.Clear();
        }

        private static GameObject ResolveChepEmptyPrefab()
        {
            var buildMenu = Object.FindAnyObjectByType<BuildMenuUI>();
            var registry = buildMenu != null ? buildMenu.registry : null;
            if (registry == null)
            {
                var all = Resources.FindObjectsOfTypeAll<ObjDataRegistry>();
                registry = all.Length > 0 ? all[0] : null;
            }
            if (registry == null) return null;
            foreach (var so in registry.buttonSOs)
                if (so != null && so.prefab != null && so.category == "Inventory" && so.prefab.GetComponent<PalletBuilder>() != null)
                    return so.prefab;
            return null;
        }
    }
}
