using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Labor
{
    /// <summary>
    /// DEBUG-ONLY (Tad, 2026-11-08): "fill the racks with several pallets of every item in the SKU listing". Every SKU gets a pick
    /// face (an unassigned pick slot is assigned to it if it has none, in pick-sequence order so 01-01 fills first), the face gets a
    /// full RECEIVED pallet, and the SKU's reserve is topped up to <c>palletsPerSku</c> racked pallets in the nearest free reserve
    /// slots that fit under the beam. The pallets are the same as a reach-truck putaway leaves: ChepEmpty with built cases, a Load ID,
    /// PalletData, a PalletMasterLink, slot contents (Occupy) and Occupied status, so saves, replenishment and picking treat them as
    /// real stock. Free: no money is charged. Wired to the Dev Console's FILL RACKS + ORDERS button (ToolsWindowController).
    /// </summary>
    public static class TestRackStocker
    {
        public struct Result { public int Pallets, NewFaces, SkusStocked, SkusNoFace, ReserveShort; }

        public static Result FillRacks(int palletsPerSku = 3)
        {
            var r = new Result();
            var grid = Object.FindAnyObjectByType<PlacementGrid>();
            if (grid == null) { Debug.LogError("[TestRackStocker] No PlacementGrid in scene."); return r; }
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null)
            { Debug.LogError("[TestRackStocker] InventoryService not available."); return r; }
            var prefab = ResolvePalletPrefab();
            if (prefab == null) { Debug.LogError("[TestRackStocker] Could not resolve the Inventory pallet prefab (ChepEmpty)."); return r; }

            var skus = inv.AllSkus.Where(s => s != null && s.Ti > 0 && s.Hi > 0 && s.Prefab != null)
                                  .OrderBy(s => s.SkuId, System.StringComparer.OrdinalIgnoreCase).ToList();
            if (skus.Count == 0) { Debug.LogError("[TestRackStocker] No SKU with committed Ti/Hi + case prefab."); return r; }

            var container = GameObject.Find("PlacedObjectsContainer")?.transform;   // where a save restores racked pallets

            // Free slots, pick faces in walking order (01-01 first).
            var freePick = SlotRegistry.PickSlots.Where(s => !SlotAssignmentService.IsAssigned(s.Address) && IsFree(s.Address))
                                                 .OrderBy(s => OrderPickPath.SequenceKey(s.Address)).ToList();
            var freeReserve = SlotRegistry.ReserveSlots.Where(s => IsFree(s.Address)).ToList();

            foreach (var sku in skus)
            {
                var faces = SlotAssignmentService.GetSlotsForSku(sku.SkuId);
                if (faces.Count == 0)
                {
                    int i = freePick.FindIndex(s => Fits(s, sku.PltHeight));
                    if (i < 0) { r.SkusNoFace++; continue; }
                    SlotAssignmentService.Assign(freePick[i].Address, sku.SkuId);
                    freePick.RemoveAt(i);
                    faces = SlotAssignmentService.GetSlotsForSku(sku.SkuId);
                    r.NewFaces++;
                }

                int racked = inv.GetPalletsBySku(sku.SkuId).Count;
                foreach (var face in faces)
                {
                    if (!IsFree(face) || !SlotRegistry.TryGet(face, out var fs)) continue;
                    if (PlacePallet(fs, sku, prefab, container, grid, inv)) { r.Pallets++; racked++; }
                }

                // Reserve, nearest to the first face, until the SKU has palletsPerSku pallets in the building.
                Vector3 near = LocationRegistry.TryGet(faces[0], out var faceLoc) && faceLoc != null ? faceLoc.transform.position : Vector3.zero;
                while (racked < palletsPerSku)
                {
                    int best = -1; float bestD = float.MaxValue;
                    for (int i = 0; i < freeReserve.Count; i++)
                    {
                        if (!Fits(freeReserve[i], sku.PltHeight) || !LocationRegistry.TryGet(freeReserve[i].Address, out var rl) || rl == null) continue;
                        float d = (rl.transform.position - near).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = i; }
                    }
                    if (best < 0) { r.ReserveShort++; break; }
                    var slot = freeReserve[best];
                    freeReserve.RemoveAt(best);
                    if (!PlacePallet(slot, sku, prefab, container, grid, inv)) continue;
                    r.Pallets++; racked++;
                }
                r.SkusStocked++;
            }

            grid.RebuildFromRegistry();
            ServiceLocator.Get<EconomyService>()?.RebuildFromRegistry();
            LocationRegistry.ForceRecompute();
            Debug.LogWarning($"[TestRackStocker] Filled racks: {r.Pallets} pallet(s) for {r.SkusStocked} SKU(s), {r.NewFaces} new pick face(s) assigned, " +
                             $"{r.SkusNoFace} SKU(s) with no free pick slot, {r.ReserveShort} SKU(s) short of reserve room.");
            return r;
        }

        /// <summary>Empty and not reserved, by both the status registry and the slot's recorded contents.</summary>
        private static bool IsFree(string address)
        {
            if (LocationStatusRegistry.Get(address) != LocationStatus.Available) return false;
            return LocationRegistry.TryGet(address, out var loc) && loc != null && string.IsNullOrEmpty(loc.PalletId);
        }

        /// <summary>Same rule as PutawayLogic.Fits: the pallet must clear the beam above, unless this is the top level of the bay.</summary>
        private static bool Fits(SlotRegistry.Slot s, float palletHeight)
        {
            if (palletHeight <= 0f || s.Rack == null || s.Rack.data == null) return true;
            int level = s.Rack.rackLevelIndex;
            bool topLevel = !SlotRegistry.AllSlots.Any(o => o.Rack != null && o.Aisle == s.Aisle && o.Bay == s.Bay && o.Rack.rackLevelIndex > level);
            return topLevel || palletHeight <= s.Rack.data.objHeight + 0.01f;
        }

        private static bool PlacePallet(SlotRegistry.Slot slot, SkuData sku, GameObject prefab, Transform container, PlacementGrid grid, InventoryService inv)
        {
            if (!LocationRegistry.TryGet(slot.Address, out var loc) || loc == null) return false;
            Transform lt = loc.transform;
            Vector2Int cell = slot.Rack != null ? new Vector2Int(slot.Rack.gridX, slot.Rack.gridY) : grid.WorldToCell(lt.position);

            var go = Object.Instantiate(prefab, lt.position, lt.rotation, container);
            go.name = $"RackPallet_{slot.Address}";
            var po = go.GetComponent<PlacedObject>();
            if (po == null || po.data == null) { Object.Destroy(go); return false; }
            int rotIndex = Mathf.RoundToInt(lt.eulerAngles.y / 90f) & 3;
            po.Initialize(po.data, cell.x, cell.y, rotIndex);
            go.transform.SetPositionAndRotation(lt.position, lt.rotation);   // Initialize snaps to 90 degrees; sit square on the slot
            po.worldSpaceYHeight = lt.position.y;

            var builder = go.GetComponent<PalletBuilder>();
            if (builder != null)
            {
                builder.casePrefab = sku.Prefab;
                builder.linkedSku = sku;
                builder.useTiHiOverride = true;
                builder.manualTi = sku.Ti;
                builder.manualHi = sku.Hi;
                builder.Build(deductMoney: false);
                ReseatCasesOnDeck(go.transform);
            }

            // Grid + registry, the same as PalletPersistenceService.RestoreAll does for a racked pallet.
            var bd = go.GetComponent<BuildingData>() ?? go.AddComponent<BuildingData>();
            float rotDeg = rotIndex * 90f;
            Vector2Int[] offsets = po.data.GetFootprintOffsets(-rotDeg);
            bd.Initialize(cell, rotDeg, offsets, po.data);
            PlacedObjectRegistry.Register(po);
            foreach (var o in offsets) grid.AddStackObject(cell + o, go, po.data);

            // A received pallet (Load ID), moved into the rack cell.
            int qty = Mathf.Max(1, sku.Ti * sku.Hi);
            var rec = inv.ReceivePalletWithLoadId(sku.SkuId, qty, sku.ShelfLifeDays > 0 ? sku.ShelfLifeDays : -1);
            inv.MovePallet(rec.PalletId, cell);
            rec.WorldHeightY = lt.position.y;
            PalletMasterLink.Attach(go, rec.PalletId);

            var pd = go.GetComponent<PalletData>() ?? go.AddComponent<PalletData>();
            pd.Initialize(rec.LoadId, sku.SkuId, qty, rec.ExpirationDayNumber, sku.StorageArea, sku.Icon, cell);
            pd.SetLocation(cell, slot.Address);

            string expiry = rec.ExpirationDayNumber >= 0 ? rec.ExpirationDayNumber.ToString() : null;
            LocationStatusRegistry.MarkOccupied(slot.Address);
            loc.Occupy(rec.PalletId, sku.SkuId, qty, expiry, rec.LoadId);
            return true;
        }

        // Same as TestPalletSpawner / TruckController.BuildOnePallet: lowest case on the deck (0.165 m), PalletLoad unrotated.
        private static void ReseatCasesOnDeck(Transform palletRoot)
        {
            var palletLoad = palletRoot.Find("PalletLoad");
            if (palletLoad == null || palletLoad.childCount == 0) return;
            const float palletDeckHeight = 0.165f;
            float minCaseY = float.MaxValue;
            for (int i = 0; i < palletLoad.childCount; i++) minCaseY = Mathf.Min(minCaseY, palletLoad.GetChild(i).localPosition.y);
            float yOffset = minCaseY - palletDeckHeight;
            for (int i = 0; i < palletLoad.childCount; i++)
            {
                var c = palletLoad.GetChild(i);
                var p = c.localPosition; p.y -= yOffset; c.localPosition = p;
            }
            palletLoad.localRotation = Quaternion.identity;
        }

        private static GameObject ResolvePalletPrefab()
        {
            var buildMenu = Object.FindAnyObjectByType<BuildMenuUI>();
            var registry = buildMenu != null && buildMenu.registry != null ? buildMenu.registry : Resources.FindObjectsOfTypeAll<ObjDataRegistry>().FirstOrDefault();
            if (registry == null) return null;
            foreach (var so in registry.buttonSOs)
                if (so != null && so.prefab != null && so.category == "Inventory" && so.prefab.GetComponent<PalletBuilder>() != null)
                    return so.prefab;
            return null;
        }
    }
}
