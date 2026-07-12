using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Labor
{
    /// <summary>
    /// DEBUG-ONLY. Instantly drops a stack of test pallets into a staging lane so persistence /
    /// receiving can be play-tested without spawning a truck and waiting for the whole
    /// arrive → dock → offload sequence. The pallets it produces are byte-for-byte equivalent to what
    /// TrailerOffloadController leaves in the lane: real ChepEmpty pallet GameObjects with built + GHOSTED
    /// cases (unreceived), registered in PlacedObjectRegistry, linked to an InventoryService record
    /// (no LoadId → still ghosted), captured by PalletPersistenceService on save, and each carrying a
    /// Receive WorkTask so a Receiver still walks out and receives them. That keeps the simulation
    /// "real" for data-persistence testing.
    ///
    /// Wired to the DEV CONSOLE "CREATE TEST PALLETS" / "CLEAR SCENE" buttons (see ToolsWindowController).
    /// </summary>
    public static class TestPalletSpawner
    {
        // Pallet prefab = ChepEmpty, resolved from the registry's Inventory SO (see ResolvePalletPrefab).
        private const float LaneSurfaceY = 1.15f; // dock foundation top (matches TrailerOffloadController)
        private const float StackGap     = 0.02f; // gap between a stacked pallet's base and the case-top below it
        private const string GhostMaterialPath  = "Materials/GhostLoweredWall";

        /// <summary>
        /// Add up to <paramref name="batchSize"/> pallets (default 14 = a truck's worth) to the dock,
        /// filling the next OPEN lane slots. STATELESS + INCREMENTAL: every click reads current
        /// occupancy and fills the next available tier-slots in order — lowest door number first, then
        /// that door's lanes (A, B, C…), then each slot in the lane, stacking up to each lane's
        /// MaxStackHeight. So repeated clicks progressively fill one lane, spill into the next lane of
        /// that door, then move on to the next door — until the whole dock is full. Returns how many
        /// pallets were actually created this click.
        /// </summary>
        public static int SpawnStackedTestPallets(int batchSize = 10)
        {
            var grid = Object.FindAnyObjectByType<PlacementGrid>();
            if (grid == null) { Debug.LogError("[TestPalletSpawner] No PlacementGrid in scene."); return 0; }
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null)
            { Debug.LogError("[TestPalletSpawner] InventoryService not available."); return 0; }

            var palletPrefab = ResolvePalletPrefab();
            if (palletPrefab == null)
            { Debug.LogError("[TestPalletSpawner] Could not resolve the Inventory pallet prefab (ChepEmpty) from ObjDataRegistry."); return 0; }

            // All eligible SKUs with committed Ti/Hi and a real case prefab.
            var validSkus = inv.AllSkus
                .Where(s => s != null && s.Ti > 0 && s.Hi > 0 && s.Prefab != null)
                .ToList();

            if (validSkus.Count == 0)
            { Debug.LogError("[TestPalletSpawner] No SKU with committed Ti/Hi + case prefab found — run the Pallet Optimizer batch first."); return 0; }

            var ghostMat = Resources.Load<Material>(GhostMaterialPath);
            if (ghostMat == null)
                Debug.LogWarning($"[TestPalletSpawner] Ghost material not found at Resources/{GhostMaterialPath} — cases won't read as unreceived.");

            // Next open tier-slots across the whole dock, in fill order (lowest door → lane → slot).
            List<Vector2Int> openTierSlots = CollectOpenTierSlots(inv, grid);
            if (openTierSlots.Count == 0)
            { Debug.LogWarning("[TestPalletSpawner] No open staging-lane slots — the dock's Inbound/Both lanes are full (or none exist)."); return 0; }

            var container = GameObject.Find("PlacedObjectsContainer")?.transform;

            int made = 0;
            foreach (var cell in openTierSlots)
            {
                if (made >= batchSize) break;

                // Randomly pick a SKU for each pallet in the assortment.
                var sku = validSkus[Random.Range(0, validSkus.Count)];

                // Sits on whatever's already in this cell (measured live), or the lane surface if empty —
                // so consecutive tiers of the same cell stack, and a partly-filled cell from a prior
                // click continues stacking correctly.
                float baseY = ComputeBaseYForCell(cell, inv);
                var go = BuildPalletInCell(palletPrefab, sku, cell, baseY, ghostMat, container, grid, inv);
                if (go != null) made++;
            }

            grid.RebuildFromRegistry();
            int remaining = openTierSlots.Count - made;
            Debug.Log($"[TestPalletSpawner] Created {made} test pallets (ghosted + Receive-tasked). {remaining} lane slots still open on the dock.");
            return made;
        }

        /// <summary>
        /// Wipe every pallet on the dock + all inventory data + pallet-related work tasks, leaving all
        /// other scene objects (employees, MHE, walls, racks, floors, doors, lanes) untouched. Lets you
        /// start a clean persistence test without reloading the scene.
        /// </summary>
        public static int ClearDockAndInventory()
        {
            // 1. Destroy every live pallet GameObject (category "Inventory") + its cases (children).
            int destroyed = 0;
            var grid = Object.FindAnyObjectByType<PlacementGrid>();
            foreach (var po in PlacedObjectRegistry.GetSnapshot())
            {
                if (po == null || po.data == null || po.data.category != "Inventory") continue;
                grid?.RemoveStackObject(new Vector2Int(po.gridX, po.gridY), po.gameObject, po.data);
                Object.Destroy(po.gameObject);
                destroyed++;
            }

            // 2. Clear the inventory ledger + all pallet-related work tasks.
            if (ServiceLocator.TryGet<InventoryService>(out var inv) && inv != null)
                inv.ClearAllPallets();
            if (ServiceLocator.TryGet<WorkQueueSystem>(out var queue) && queue != null)
                queue.ClearAllTasks();

            // 3. Grid + economy resync now that the pallets are gone (deferred one frame would be safer,
            //    but Destroy's OnDisable already unregisters each pallet synchronously here).
            grid?.RebuildFromRegistry();
            ServiceLocator.Get<EconomyService>()?.RebuildFromRegistry();

            Debug.Log($"[TestPalletSpawner] Cleared dock: destroyed {destroyed} pallet objects, wiped inventory + work queue. Scene objects untouched.");
            return destroyed;
        }

        // ── Internals ────────────────────────────────────────────────────────────────────────────

        private static GameObject BuildPalletInCell(GameObject palletPrefab, SkuData sku, Vector2Int cell,
                                                    float baseY, Material ghostMat, Transform container,
                                                    PlacementGrid grid, InventoryService inv)
        {
            Vector3 worldPos = grid.GetCellCenter(cell);
            worldPos.y = baseY;

            var go = Object.Instantiate(palletPrefab, worldPos, Quaternion.identity, container);
            go.name = $"TestPallet_{cell.x}_{cell.y}_{Mathf.RoundToInt(baseY * 100)}";

            var po = go.GetComponent<PlacedObject>();
            if (po == null || po.data == null)
            {
                Debug.LogError("[TestPalletSpawner] Pallet prefab (ChepEmpty) is missing a PlacedObject/data — cannot spawn.");
                Object.Destroy(go);
                return null;
            }
            po.Initialize(po.data, cell.x, cell.y, 0);
            po.worldSpaceYHeight = baseY;

            // Build the cases (same as TruckController.BuildOnePallet: SKU Ti/Hi, no money charge).
            var builder = go.GetComponent<PalletBuilder>();
            if (builder != null)
            {
                builder.casePrefab = sku.Prefab;
                builder.useTiHiOverride = true;
                builder.manualTi = sku.Ti;
                builder.manualHi = sku.Hi;
                builder.Build(deductMoney: false);
                ReseatCasesOnDeck(go.transform);
                if (ghostMat != null) builder.GhostCases(ghostMat); // unreceived look
            }

            // BuildingData + grid occupancy (mirror PalletPersistenceService.RestoreAll).
            var bd = go.GetComponent<BuildingData>();
            if (bd == null) bd = go.AddComponent<BuildingData>();
            Vector2Int[] offsets = po.data.GetFootprintOffsets(0f);
            bd.Initialize(cell, 0f, offsets, po.data);

            PlacedObjectRegistry.Register(po);
            foreach (var o in offsets) grid.AddStackObject(cell + o, go, po.data);

            // InventoryService record — NO LoadId (still ghosted/unreceived), plus the Receive task.
            var record = inv.RegisterPhysicalPallet(cell, sku.SkuId, Mathf.Max(1, sku.Ti * sku.Hi));
            record.WorldHeightY = baseY;
            PalletMasterLink.Attach(go, record.PalletId);
            ReceivingService.CreateReceiveTaskForPallet(record);

            return go;
        }

        // Shift cases so the lowest sits on the pallet deck (0.165m) and zero PalletLoad's default 90°
        // spin — identical to TruckController.BuildOnePallet's post-build fix.
        private static void ReseatCasesOnDeck(Transform palletRoot)
        {
            var palletLoad = palletRoot.Find("PalletLoad");
            if (palletLoad == null) return;

            const float palletDeckHeight = 0.165f;
            float minCaseY = float.MaxValue;
            for (int i = 0; i < palletLoad.childCount; i++)
                minCaseY = Mathf.Min(minCaseY, palletLoad.GetChild(i).localPosition.y);

            if (palletLoad.childCount > 0 && minCaseY != float.MaxValue)
            {
                float yOffset = minCaseY - palletDeckHeight;
                for (int i = 0; i < palletLoad.childCount; i++)
                {
                    var c = palletLoad.GetChild(i);
                    var p = c.localPosition; p.y -= yOffset; c.localPosition = p;
                }
            }
            palletLoad.localRotation = Quaternion.identity;
        }

        // World-space top (max renderer bounds Y) of a pallet + its cases; 0 if none.
        private static float MeasureTopY(GameObject go)
        {
            if (go == null) return 0f;
            float maxY = 0f;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                if (r.bounds.max.y > maxY) maxY = r.bounds.max.y;
            return maxY;
        }

        // The single-pallet prefab the REAL system uses — ChepEmpty — resolved via the "A Chep"
        // Inventory ObjDataSO in the registry. This is the SAME prefab PalletPersistenceService
        // instantiates on load, so test pallets are identical to what save/restore produces. Do NOT
        // Resources.Load a pallet by name: three ambiguous "ChepEmpty" prefabs live under Resources/,
        // and "ChepStack" is a decorative TALL STACK of empties (the tower bug), not a single pallet.
        private static GameObject ResolvePalletPrefab()
        {
            var registry = FindRegistry();
            if (registry == null) return null;
            foreach (var so in registry.buttonSOs)
            {
                if (so == null || so.prefab == null) continue;
                if (so.category != "Inventory") continue;
                if (so.prefab.GetComponent<PalletBuilder>() == null) continue; // must be a buildable single pallet
                return so.prefab;
            }
            return null;
        }

        private static ObjDataRegistry FindRegistry()
        {
            var buildMenu = Object.FindAnyObjectByType<BuildMenuUI>();
            if (buildMenu != null && buildMenu.registry != null) return buildMenu.registry;
            var all = Resources.FindObjectsOfTypeAll<ObjDataRegistry>();
            return all.Length > 0 ? all[0] : null;
        }

        // Every OPEN tier-slot across the dock, in fill order: lowest door number first, then that
        // door's lanes (A, B, C…), then each slot in the lane. A cell appears once per FREE tier
        // (MaxStackHeight − current occupancy), so consecutive duplicates stack within one click, and a
        // cell that's already full contributes nothing.
        private static List<Vector2Int> CollectOpenTierSlots(InventoryService inv, PlacementGrid grid)
        {
            var result = new List<Vector2Int>();
            var lanes = LaneNamingService.AllLanes()
                .Where(t => inv.LaneAcceptsPutaway(t.door, t.lane))
                .OrderBy(t => t.door).ThenBy(t => t.lane, System.StringComparer.OrdinalIgnoreCase);

            foreach (var (door, lane) in lanes)
            {
                int maxStack = Mathf.Max(1, LaneConfigRegistry.Get(door, lane).MaxStackHeight);
                var slots = LaneNamingService.GetLane(door, lane);
                if (slots == null) continue;
                foreach (var slot in slots)
                {
                    int occ = inv.GetPalletsAtLocation(slot.Cell).Count;
                    for (int tier = occ; tier < maxStack; tier++)
                        result.Add(slot.Cell);
                }
            }
            return result;
        }

        // Base Y for a NEW pallet dropping into `cell`: on top of the highest pallet already there
        // (measured), or the lane surface if the cell is empty.
        private static float ComputeBaseYForCell(Vector2Int cell, InventoryService inv)
        {
            float highestTop = 0f;
            foreach (var rec in inv.GetPalletsAtLocation(cell))
            {
                var go = PalletMasterLink.Find(rec.PalletId)?.gameObject;
                if (go == null) continue;
                float top = MeasureTopY(go);
                if (top > highestTop) highestTop = top;
            }
            return highestTop > 0f ? highestTop + StackGap : LaneSurfaceY;
        }
    }
}
