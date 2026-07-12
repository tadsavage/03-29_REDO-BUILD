using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Labor
{
    /// <summary>
    /// Static utility for bulk operations on pallets currently on the dock (staging lanes).
    /// Used by the Pallet Builder dev tool to rebuild all pallets of a given SKU, fix rotations,
    /// and recalculate stacking heights across the entire dock in one pass.
    /// </summary>
    public static class DockPalletUtility
    {
        /// <summary>Dock foundation top — the surface staging lanes rest on.</summary>
        private const float LaneSurfaceY = 1.15f;

        /// <summary>Small anti-clip gap between a stacked pallet's base and the case-top below it.</summary>
        private const float StackGap = 0.02f;

        /// <summary>Pallet deck height — the surface cases sit on.</summary>
        private const float PalletDeckHeight = 0.165f;

        /// <summary>Resources path for the ghost (unreceived) material.</summary>
        private const string GhostMaterialPath = "Materials/GhostLoweredWall";

        /// <summary>Resources path for the solid (received) material fallback.</summary>
        private const string SolidMaterialPath = "AA_LowPolyCommon";

        /// <summary>Editor-only path for the solid material (more reliable than Resources).</summary>
        private const string SolidMaterialEditorPath = "Assets/_Project/Materials/AA_LowPolyCommon.mat";

        /// <summary>Resolves the SkuData for a dock pallet, checking linkedSku first, then
        /// PalletMasterLink → InventoryService, then PalletData → InventoryService.</summary>
        public static SkuData GetSkuForPallet(GameObject palletGO)
        {
            if (palletGO == null) return null;

            // 1. Direct linkedSku on PalletBuilder
            var builder = palletGO.GetComponent<PalletBuilder>();
            if (builder != null && builder.linkedSku != null)
                return builder.linkedSku;

            // 2. PalletMasterLink → InventoryService → SkuData
            var link = palletGO.GetComponent<PalletMasterLink>();
            if (link != null && ServiceLocator.TryGet<InventoryService>(out var inv) && inv != null)
            {
                var rec = inv.GetPallet(link.PalletId);
                if (rec != null)
                {
                    var sku = inv.GetSkuData(rec.SkuId);
                    if (sku != null) return sku;
                }
            }

            // 3. PalletData → InventoryService → SkuData
            var pdata = palletGO.GetComponent<PalletData>();
            if (pdata != null && !string.IsNullOrEmpty(pdata.ItemNumber))
            {
                if (ServiceLocator.TryGet<InventoryService>(out var inv2) && inv2 != null)
                {
                    var sku = inv2.GetSkuData(pdata.ItemNumber);
                    if (sku != null) return sku;
                }
            }

            // 4. Fallback: match casePrefab against all SkuData assets
            if (builder != null && builder.casePrefab != null)
            {
                var sku = MatchPrefabToSku(builder.casePrefab);
                if (sku != null)
                {
                    // Cache the resolved SKU so future lookups skip this search
                    builder.linkedSku = sku;
                    return sku;
                }
            }

            // 5. Fallback: inspect case children under "PalletLoad" and match by prefab name
            var palletLoad = palletGO.transform.Find("PalletLoad");
            if (palletLoad != null && palletLoad.childCount > 0)
            {
                var firstCase = palletLoad.GetChild(0);
                var sku = MatchCaseChildToSku(firstCase.gameObject);
                if (sku != null)
                {
                    if (builder != null)
                    {
                        builder.linkedSku = sku;
                        builder.casePrefab = sku.Prefab;
                    }
                    return sku;
                }
            }

            return null;
        }

        /// <summary>Loads all SkuData assets and returns the one whose Prefab matches the given casePrefab.</summary>
        private static SkuData MatchPrefabToSku(GameObject casePrefab)
        {
            if (casePrefab == null) return null;
            var skus = Resources.LoadAll<SkuData>("Inventory/SKUs");
            foreach (var sku in skus)
            {
                if (sku != null && sku.Prefab == casePrefab)
                    return sku;
            }
            return null;
        }

        /// <summary>Strips "(Clone)" from a case child's name and matches it against SkuData prefab names.</summary>
        private static SkuData MatchCaseChildToSku(GameObject caseChild)
        {
            if (caseChild == null) return null;
            string childName = caseChild.name.Replace("(Clone)", "").Trim();
            var skus = Resources.LoadAll<SkuData>("Inventory/SKUs");
            foreach (var sku in skus)
            {
                if (sku == null || sku.Prefab == null) continue;
                if (sku.Prefab.name == childName)
                    return sku;
            }
            return null;
        }

        /// <summary>Finds all dock pallet GameObjects via PlacedObjectRegistry (category "Inventory"
        /// with a PalletBuilder component).</summary>
        public static List<GameObject> FindAllDockPallets()
        {
            var result = new List<GameObject>();
            foreach (var entry in PlacedObjectRegistry.All)
            {
                if (entry == null || entry.data == null) continue;
                if (entry.data.category != "Inventory") continue;
                if (entry.GetComponent<PalletBuilder>() == null) continue;
                result.Add(entry.gameObject);
            }
            return result;
        }

        /// <summary>Finds all dock pallets whose SKU matches the given skuId.</summary>
        public static List<GameObject> FindDockPalletsBySku(string skuId)
        {
            return FindAllDockPallets()
                .Where(go =>
                {
                    var sku = GetSkuForPallet(go);
                    return sku != null && sku.SkuId == skuId;
                })
                .ToList();
        }

        /// <summary>Rebuilds all dock pallets matching the template builder's SKU, applying the
        /// template's Ti/Hi and spacing settings. After rebuilding, reseats cases on the deck,
        /// reapplies the correct material (ghost for unreceived, solid for received), and
        /// recalculates all dock Y positions.</summary>
        public static int RebuildAllDockPalletsWithSku(PalletBuilder template)
        {
            if (template == null || template.linkedSku == null) return 0;

            var sku = template.linkedSku;
            var pallets = FindDockPalletsBySku(sku.SkuId);
            if (pallets.Count == 0)
            {
                Debug.LogWarning($"[DockPalletUtility] No dock pallets found with SKU {sku.SkuId} ({sku.ItemDescription}).");
                return 0;
            }

            var ghostMat = LoadGhostMaterial();
            var solidMat = LoadSolidMaterial();

            int rebuilt = 0;
            foreach (var palletGO in pallets)
            {
                var builder = palletGO.GetComponent<PalletBuilder>();
                if (builder == null) continue;

                // Capture material state before rebuild
                bool wasGhosted = IsPalletGhosted(palletGO);

                // Apply template settings
                builder.casePrefab = sku.Prefab;
                builder.linkedSku = sku;
                builder.useTiHiOverride = template.useTiHiOverride;
                builder.manualTi = template.manualTi;
                builder.manualHi = template.manualHi;
                builder.spaceBetweenCases = template.spaceBetweenCases;
                builder.verticalGap = template.verticalGap;
                builder.maxTotalHeight = template.maxTotalHeight;
                builder.crookedCase = template.crookedCase;

                // Rebuild
                builder.Build(deductMoney: false);

                // Reseat cases on deck
                ReseatCasesOnDeck(palletGO.transform);

                // Restore material state
                if (wasGhosted && ghostMat != null)
                    builder.GhostCases(ghostMat);
                else if (!wasGhosted && solidMat != null)
                    ApplySolidMaterial(builder, solidMat);

                rebuilt++;
            }

            // Recalculate all dock Y positions after rebuilding
            RecalculateAllDockYPositions();

            Debug.Log($"[DockPalletUtility] Rebuilt {rebuilt} pallet(s) with SKU {sku.SkuId} ({sku.ItemDescription}).");
            return rebuilt;
        }

        /// <summary>Recalculates Y positions for all dock pallets, grouping by grid cell (from
        /// PlacedObject.gridX/gridY) and stacking from LaneSurfaceY upward with StackGap between
        /// each pallet. Updates both the world transform and the PalletMasterRecord.WorldHeightY.</summary>
        public static void RecalculateAllDockYPositions()
        {
            var pallets = FindAllDockPallets();
            if (pallets.Count == 0) return;

            ServiceLocator.TryGet<InventoryService>(out var inv);

            // Group by grid cell
            var byCell = new Dictionary<(int, int), List<GameObject>>();
            foreach (var go in pallets)
            {
                var po = go.GetComponent<PlacedObject>();
                int cx, cy;
                if (po != null)
                {
                    cx = po.gridX;
                    cy = po.gridY;
                }
                else
                {
                    var pos = go.transform.position;
                    cx = Mathf.RoundToInt(pos.x);
                    cy = Mathf.RoundToInt(pos.z);
                }

                var key = (cx, cy);
                if (!byCell.ContainsKey(key))
                    byCell[key] = new List<GameObject>();
                byCell[key].Add(go);
            }

            int adjusted = 0;
            foreach (var kvp in byCell)
            {
                var cellPallets = kvp.Value;

                // Sort by current Y (lowest first)
                cellPallets.Sort((a, b) =>
                    a.transform.position.y.CompareTo(b.transform.position.y));

                float currentBaseY = LaneSurfaceY;
                foreach (var palletGO in cellPallets)
                {
                    // Set the pallet's Y to currentBaseY, preserving X and Z
                    var pos = palletGO.transform.position;
                    pos.y = currentBaseY;
                    palletGO.transform.position = pos;

                    // Record height in PlacedObject
                    var po = palletGO.GetComponent<PlacedObject>();
                    if (po != null) po.worldSpaceYHeight = currentBaseY;

                    // Record height in PalletMasterRecord
                    if (inv != null)
                    {
                        var link = palletGO.GetComponent<PalletMasterLink>();
                        if (link != null)
                        {
                            var rec = inv.GetPallet(link.PalletId);
                            if (rec != null) rec.WorldHeightY = currentBaseY;
                        }
                    }

                    // Next pallet stacks on top of this one
                    float topY = MeasureTopY(palletGO);
                    if (topY > currentBaseY)
                        currentBaseY = topY + StackGap;

                    adjusted++;
                }
            }

            Debug.Log($"[DockPalletUtility] Recalculated Y positions for {adjusted} dock pallet(s) across {byCell.Count} cell(s).");
        }

        /// <summary>Rotates all dock pallets by the specified angle (degrees) on the Y axis.
        /// Used to fix the incorrect 90-degree clockwise rotation applied by the old offload code.</summary>
        public static void RotateAllDockPallets(float angleY)
        {
            var pallets = FindAllDockPallets();
            int rotated = 0;
            foreach (var go in pallets)
            {
                // Preserve the PalletLoad child's local rotation — only rotate the pallet root.
                var palletLoad = go.transform.Find("PalletLoad");
                Quaternion loadLocalRot = palletLoad != null ? palletLoad.localRotation : Quaternion.identity;

                go.transform.rotation = go.transform.rotation * Quaternion.Euler(0f, angleY, 0f);

                // Restore PalletLoad local rotation so cases don't spin with the root
                if (palletLoad != null)
                    palletLoad.localRotation = loadLocalRot;

                rotated++;
            }
            Debug.Log($"[DockPalletUtility] Rotated {rotated} dock pallet(s) by {angleY}° on Y.");
        }

        /// <summary>Returns true if the pallet is still ghosted/unreceived (no PalletData or
        /// PalletData with an empty LoadId).</summary>
        private static bool IsPalletGhosted(GameObject palletGO)
        {
            var pdata = palletGO.GetComponent<PalletData>();
            if (pdata == null) return true;
            return string.IsNullOrEmpty(pdata.LoadId);
        }

        private static Material LoadGhostMaterial()
        {
            return Resources.Load<Material>(GhostMaterialPath);
        }

        private static Material LoadSolidMaterial()
        {
            Material mat = null;
#if UNITY_EDITOR
            mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(SolidMaterialEditorPath);
#endif
            if (mat == null)
                mat = Resources.Load<Material>(SolidMaterialPath);
            return mat;
        }

        private static void ApplySolidMaterial(PalletBuilder builder, Material solidMat)
        {
            // Instead of forcing a single material on all renderers (which hides tape/labels),
            // use PalletBuilder's own restoration logic which respects the prefab's
            // multi-renderer/multi-material layout.
            builder.RestoreCaseMaterial();
        }

        /// <summary>Shifts cases so the lowest sits on the pallet deck and zeroes PalletLoad's
        /// local rotation — identical to TruckController.BuildOnePallet and TestPalletSpawner.</summary>
        private static void ReseatCasesOnDeck(Transform palletRoot)
        {
            var palletLoad = palletRoot.Find("PalletLoad");
            if (palletLoad == null) return;

            float minCaseY = float.MaxValue;
            for (int i = 0; i < palletLoad.childCount; i++)
                minCaseY = Mathf.Min(minCaseY, palletLoad.GetChild(i).localPosition.y);

            if (palletLoad.childCount > 0 && minCaseY != float.MaxValue)
            {
                float yOffset = minCaseY - PalletDeckHeight;
                for (int i = 0; i < palletLoad.childCount; i++)
                {
                    var c = palletLoad.GetChild(i);
                    var p = c.localPosition; p.y -= yOffset; c.localPosition = p;
                }
            }
            palletLoad.localRotation = Quaternion.identity;
        }

        /// <summary>World-space top (max renderer bounds Y) of a settled pallet + its cases.</summary>
        private static float MeasureTopY(GameObject go)
        {
            float maxY = 0f;
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
                if (r.bounds.max.y > maxY) maxY = r.bounds.max.y;
            return maxY;
        }
    }
}
