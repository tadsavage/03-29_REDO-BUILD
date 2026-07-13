using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;
using GameCore.Labor;

namespace GameCore.Persistence
{
    /// <summary>
    /// Literal, world-transform-based persistence for dock/lane pallets (ChepEmpty root +
    /// whatever cases PalletBuilder built on it). Captures exact world position/rotation for the
    /// pallet and exact local position/rotation for every case, and restores them verbatim.
    ///
    /// Why this exists instead of extending the old <see cref="InventoryPersistenceService"/>:
    /// that service re-derives pallet visuals from InventoryService's SKU/Ti/Hi data, which
    /// doesn't necessarily match what was actually built (dummy "PHYS" SKU registers 1 case
    /// regardless of how many cases PalletBuilder actually rendered), never restores rotation
    /// (always Quaternion.identity), and resolves the pallet prefab via
    /// <c>Resources.Load("ChepEmpty")</c> — ambiguous, since three separate "ChepEmpty" assets
    /// exist under different Resources/ folders. This service sidesteps all of that: it reads
    /// exactly what's in the scene at save time (via PlacedObjectRegistry + each pallet's actual
    /// PalletBuilder/PalletMasterLink/PalletData components) and resolves every prefab reference
    /// through ObjDataRegistry (the same registry-by-id lookup PlacedObject/BuildingData already
    /// use everywhere else), never Resources.Load.
    ///
    /// Identity requires <see cref="PlacedObject"/> to be present and its `data` field intact.
    /// Dock pallets get this from TruckController.BuildOnePallet DISABLING (not destroying)
    /// PlacedObject while the pallet rides as cargo, and TrailerOffloadController.RegisterAndQueue
    /// re-enabling it once the pallet has a real grid cell — see comments at both call sites.
    /// </summary>
    public static class PalletPersistenceService
    {
        // (cases now resolve their prefab from the SKU when not a registered ObjData — see RestoreCases)
        /// <summary>
        /// Scans PlacedObjectRegistry for every live pallet (category "Inventory" + a PalletBuilder
        /// component) and captures its exact world transform plus every case's exact local
        /// transform. Call this from PlacementSystem.BuildSaveData().
        /// </summary>
        public static List<DockPalletSnapshot> CaptureAll()
        {
            var result = new List<DockPalletSnapshot>();
            var registry = FindRegistry();
            ServiceLocator.TryGet<InventoryService>(out var inv);

            foreach (var entry in PlacedObjectRegistry.All)
            {
                if (entry == null || entry.data == null) continue;
                if (entry.data.category != "Inventory") continue;

                var builder = entry.GetComponent<PalletBuilder>();
                if (builder == null) continue; // not a pallet root (shouldn't happen for category Inventory, but be defensive)

                var snap = new DockPalletSnapshot
                {
                    objDataId = entry.data.id,
                    worldPosition = entry.transform.position,
                    worldRotation = entry.transform.rotation,
                    worldSpaceYHeight = entry.worldSpaceYHeight > 0f ? entry.worldSpaceYHeight : entry.transform.position.y,
                };

                var link = entry.GetComponent<PalletMasterLink>();
                if (link != null)
                {
                    snap.inventoryPalletId = link.PalletId ?? "";
                    // SKU id from the master record — lets restore resolve the case prefab from the SKU
                    // even when the case prefab isn't a registered ObjData (caseObjDataId == -1).
                    var rec = inv?.GetPallet(link.PalletId);
                    if (rec != null) snap.skuId = rec.SkuId ?? "";
                }

                var pdata = entry.GetComponent<PalletData>();
                if (pdata != null)
                {
                    snap.loadId = pdata.LoadId ?? "";
                    if (string.IsNullOrEmpty(snap.skuId)) snap.skuId = pdata.ItemNumber ?? "";
                }

                // If no SKU found yet, check the builder's own linkedSku reference (closes the loop for built pallets)
                if (string.IsNullOrEmpty(snap.skuId) && builder.linkedSku != null)
                {
                    snap.skuId = builder.linkedSku.SkuId;
                }

                var loadObj = entry.transform.Find("PalletLoad");
                if (loadObj != null && builder.casePrefab != null && loadObj.childCount > 0)
                {
                    snap.caseObjDataId = ResolveObjDataId(builder.casePrefab, registry);

                    // If we have cases but still no SKU id, try to resolve it from the case prefab itself
                    if (string.IsNullOrEmpty(snap.skuId))
                    {
                        var matchedSku = MatchPrefabToSku(builder.casePrefab);
                        if (matchedSku != null) snap.skuId = matchedSku.SkuId;
                    }

                    for (int i = 0; i < loadObj.childCount; i++)
                    {
                        var c = loadObj.GetChild(i);
                        snap.casePositions.Add(c.localPosition);
                        snap.caseRotations.Add(c.localRotation);
                    }
                }

                result.Add(snap);
            }

            if (result.Count > 0)
                Debug.Log($"[PalletPersistenceService] Captured {result.Count} dock pallets ({result.Sum(s => s.casePositions.Count)} total cases).");

            return result;
        }

        /// <summary>
        /// Instantiates every captured pallet at its exact saved world transform, rebuilds its
        /// cases at their exact saved local transforms, and re-links it to InventoryService if it
        /// was tracked there. Call this from PlacementSystem.ApplySaveData() AFTER save.pallets has
        /// already restored PalletMasterRecords into InventoryService (so the SKU/quantity lookup
        /// below has something to find).
        /// </summary>
        public static void RestoreAll(List<DockPalletSnapshot> snapshots, PlacementGrid grid)
        {
            if (snapshots == null || snapshots.Count == 0) return;

            var registry = FindRegistry();
            if (registry == null)
            {
                Debug.LogError("[PalletPersistenceService] No ObjDataRegistry found in scene — cannot restore dock pallets.");
                return;
            }

            Transform container = GameObject.Find("PlacedObjectsContainer")?.transform;
            ServiceLocator.TryGet<InventoryService>(out var inv);

            int restored = 0;
            foreach (var snap in snapshots)
            {
                if (snap == null) continue;

                var so = registry.GetByID(snap.objDataId);
                if (so == null || so.prefab == null)
                {
                    Debug.LogWarning($"[PalletPersistenceService] Unknown pallet objDataId={snap.objDataId} — skipping one dock pallet.");
                    continue;
                }

                GameObject go = Object.Instantiate(so.prefab, snap.worldPosition, snap.worldRotation, container);
                go.name = so.objName;

                // Ensure a trigger BoxCollider exists for the hover popup. If the prefab is missing one
                // (rare, but possible with ambiguous prefab variants), add a minimal fallback.
                // The actual bounds will be recalculated after cases are restored (see RecalculateColliderForRestoredPallet).
                var col = go.GetComponent<BoxCollider>();
                if (col == null)
                {
                    col = go.AddComponent<BoxCollider>();
                    col.isTrigger = true;
                    Debug.LogWarning($"[PalletPersistenceService] Restored pallet '{go.name}' (prefab '{so.objName}') had no BoxCollider — added a fallback. Bounds will be recalculated after cases load.");
                }

                var po = go.GetComponent<PlacedObject>();
                if (po == null)
                {
                    Debug.LogWarning($"[PalletPersistenceService] Restored pallet prefab '{so.objName}' has no PlacedObject component — skipping.");
                    Object.Destroy(go);
                    continue;
                }

                Vector2Int cell = grid != null ? grid.WorldToCell(snap.worldPosition) : Vector2Int.zero;
                int rotIndex = Mathf.RoundToInt(snap.worldRotation.eulerAngles.y / 90f) & 3; // 0-3, wraps like % 4 for negatives too
                po.Initialize(so, cell.x, cell.y, rotIndex);
                // Initialize() snaps rotation to rotIndex*90 exactly — restore the precise captured
                // transform afterward so pallets dropped at a slightly off angle aren't re-snapped.
                go.transform.SetPositionAndRotation(snap.worldPosition, snap.worldRotation);
                po.worldSpaceYHeight = snap.worldSpaceYHeight > 0f ? snap.worldSpaceYHeight : snap.worldPosition.y;

                var bd = go.GetComponent<BuildingData>();
                if (bd == null) bd = go.AddComponent<BuildingData>();
                float rotDeg = rotIndex * 90f;
                Vector2Int[] offsets = so.GetFootprintOffsets(-rotDeg);
                bd.Initialize(cell, rotDeg, offsets, so);

                PlacedObjectRegistry.Register(po);
                if (grid != null)
                {
                    foreach (var o in offsets)
                        grid.AddStackObject(cell + o, go, so);
                }

                RestoreCases(go, snap, registry, inv);
                RestoreInventoryLink(go, snap, inv, cell);

                // After cases are restored, recalculate the collider to encompass all cases + pallet base.
                // This ensures the hover popup trigger matches the visual bounds pre- and post-save/load.
                RecalculateColliderForRestoredPallet(go);

                restored++;
            }

            Debug.Log($"[PalletPersistenceService] Restored {restored}/{snapshots.Count} dock pallets.");
        }

        private static void RestoreCases(GameObject palletGO, DockPalletSnapshot snap, ObjDataRegistry registry, InventoryService inv)
        {
            if (snap.casePositions == null || snap.casePositions.Count == 0)
                return;

            // Resolve the case prefab. Cases usually AREN'T registered build-menu items, so
            // caseObjDataId is typically -1 — in that case fall back to the SKU's own case prefab
            // (SkuData.Prefab, loaded from Resources), which is exactly what these cases were built from.
            GameObject casePrefab = null;
            if (snap.caseObjDataId >= 0)
            {
                var caseSo = registry.GetByID(snap.caseObjDataId);
                if (caseSo != null) casePrefab = caseSo.prefab;
            }
            if (casePrefab == null && !string.IsNullOrEmpty(snap.skuId) && inv != null)
            {
                var sku = inv.GetSkuData(snap.skuId);
                if (sku != null) casePrefab = sku.Prefab;
            }
            if (casePrefab == null)
            {
                Debug.LogWarning($"[PalletPersistenceService] Could not resolve case prefab for pallet '{palletGO.name}' (caseObjDataId={snap.caseObjDataId}, skuId='{snap.skuId}') — {snap.casePositions.Count} cases skipped.");
                return;
            }

            // Defensive: PalletBuilder.Start()/LoadBuildState() might already have created a
            // PalletLoad child from stale customData before we get here. Replace it so cases never
            // duplicate.
            var existing = palletGO.transform.Find("PalletLoad");
            if (existing != null) Object.Destroy(existing.gameObject);

            var loadObj = new GameObject("PalletLoad");
            loadObj.transform.SetParent(palletGO.transform, false);
            loadObj.transform.localPosition = Vector3.zero;
            loadObj.transform.localRotation = Quaternion.identity;

            int count = Mathf.Min(snap.casePositions.Count, snap.caseRotations.Count);
            for (int i = 0; i < count; i++)
            {
                var caseGO = Object.Instantiate(casePrefab, loadObj.transform);
                caseGO.transform.localPosition = snap.casePositions[i];
                caseGO.transform.localRotation = snap.caseRotations[i];

                // Same stripping PalletBuilder.Build() applies to freshly-built cases — a case
                // instance must never self-register in the world registry.
                var casePo = caseGO.GetComponent<PlacedObject>();
                if (casePo != null) { casePo.enabled = false; Object.Destroy(casePo); }
                var caseBd = caseGO.GetComponent<BuildingData>();
                if (caseBd != null) Object.Destroy(caseBd);
                var caseHi = caseGO.GetComponent<BuildingHighlighter>();
                if (caseHi != null) Object.Destroy(caseHi);
            }

            var builder = palletGO.GetComponent<PalletBuilder>();
            if (builder != null)
            {
                builder.casePrefab = casePrefab;
                // Closed-loop persistence: restore the linkedSku reference so any future save 
                // (like a quicksave) knows what product this is.
                if (builder.linkedSku == null && inv != null)
                {
                    builder.linkedSku = MatchPrefabToSku(casePrefab);
                }
            }
        }

        private static SkuData MatchPrefabToSku(GameObject prefab)
        {
            if (prefab == null) return null;
            var skus = Resources.LoadAll<SkuData>("Inventory/SKUs");
            return skus.FirstOrDefault(s => s != null && s.Prefab == prefab);
        }

        private static void RestoreInventoryLink(GameObject palletGO, DockPalletSnapshot snap, InventoryService inv, Vector2Int cell)
        {
            if (string.IsNullOrEmpty(snap.inventoryPalletId))
                return;

            if (string.IsNullOrEmpty(snap.loadId))
            {
                // Still ghosted/unreceived — just the tag link, no PalletData component.
                PalletMasterLink.Attach(palletGO, snap.inventoryPalletId);
                return;
            }

            LoadIDGenerator.Seed(snap.loadId); // prevent future collisions with this restored ID

            // Received/solid — reconstruct PalletData from whatever InventoryService already
            // restored into its PalletMasterRecord (SKU, quantity, expiration). save.pallets is
            // restored before dockPallets in PlacementSystem.ApplySaveData, so this lookup should
            // succeed; fall back to sane defaults if the record is somehow missing.
            PalletMasterRecord record = inv?.GetAllPallets().FirstOrDefault(p => p.PalletId == snap.inventoryPalletId);
            SkuData sku = (inv != null && record != null) ? inv.GetSkuData(record.SkuId) : null;

            var pdata = palletGO.AddComponent<PalletData>();
            var area = sku != null ? sku.StorageArea : PalletData.AreaCategory.Grocery;
            var icon = sku != null ? sku.Icon : null;
            int qty = record != null ? record.Quantity : 0;
            int exp = record != null ? record.ExpirationDayNumber : -1;
            string skuId = record != null ? record.SkuId : "";
            pdata.Initialize(snap.loadId, skuId, qty, exp, area, icon, cell);

            // PalletData restored → this pallet is solid, no ghosted-cases material to reapply
            // (PalletMasterLink is optional for solid pallets, but attach it too so anything that
            // looks pallets up by PalletMasterLink.Find still finds it).
            PalletMasterLink.Attach(palletGO, snap.inventoryPalletId);
        }

        private static int ResolveObjDataId(GameObject prefab, ObjDataRegistry registry)
        {
            if (prefab == null || registry == null) return -1;
            foreach (var so in registry.buttonSOs)
                if (so != null && so.prefab == prefab) return so.id;
            return -1;
        }

        private static ObjDataRegistry FindRegistry()
        {
            var buildMenu = Object.FindAnyObjectByType<BuildMenuUI>();
            if (buildMenu != null && buildMenu.registry != null) return buildMenu.registry;

            var all = Resources.FindObjectsOfTypeAll<ObjDataRegistry>();
            return all.Length > 0 ? all[0] : null;
        }

        private static void RecalculateColliderForRestoredPallet(GameObject palletGO)
        {
            // Calculate bounds encompassing the pallet deck + all restored cases.
            // This ensures the hover popup trigger matches what the user sees visually.
            var loadObj = palletGO.transform.Find("PalletLoad");
            var col = palletGO.GetComponent<BoxCollider>();
            if (col == null) return; // No collider to recalculate

            Bounds bounds = new Bounds(palletGO.transform.position, Vector3.zero);
            bool boundsSet = false;

            // Include all case renderers
            if (loadObj != null)
            {
                foreach (var renderer in loadObj.GetComponentsInChildren<Renderer>(true))
                {
                    if (!boundsSet)
                    {
                        bounds = renderer.bounds;
                        boundsSet = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            // If no cases found, just cover the pallet base (fallback)
            if (!boundsSet)
            {
                bounds = new Bounds(palletGO.transform.position, new Vector3(1f, 0.16f, 1.25f));
            }

            // Convert world bounds to local space relative to the pallet root
            Vector3 localCenter = palletGO.transform.worldToLocalMatrix.MultiplyPoint(bounds.center);
            Vector3 localSize = bounds.size;

            col.center = localCenter;
            col.size = localSize;
            col.isTrigger = true;
        }
    }
}
