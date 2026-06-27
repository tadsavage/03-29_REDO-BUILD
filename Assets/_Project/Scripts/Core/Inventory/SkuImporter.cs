using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

#if UNITY_EDITOR

/// <summary>
/// Imports SKU data from CSV files into SkuData ScriptableObjects.
///
/// USAGE: Export Excel sheets as CSV files first:
/// 1. Open ItemFilesForForkIT.xlsx
/// 2. Save Sheet 1 "Days Supply" as: SKU_DaysSupply.csv
/// 3. Save Sheet 2 "ItemSetup" as: SKU_ItemSetup.csv
/// 4. Place both CSV files in Assets/_Project/Data/Inventory/Import/
/// 5. Run: Warehouse > Import SKUs from CSV
///
/// Creates SkuData assets with realistic pricing, demand, and physical properties.
/// </summary>
public class SkuImporter
{
    private const string IMPORT_DIR = "Assets/_Project/Data/Inventory/Import";
    private const string SUPPLY_CSV = "SKU_DaysSupply.csv";
    private const string SETUP_CSV = "SKU_ItemSetup.csv";

    [MenuItem("Warehouse/Import SKUs from CSV")]
    public static void ImportFromCsv()
    {
        string supplyPath = Path.Combine(IMPORT_DIR, SUPPLY_CSV);
        string setupPath = Path.Combine(IMPORT_DIR, SETUP_CSV);

        if (!File.Exists(supplyPath))
        {
            EditorUtility.DisplayDialog("Error", $"File not found: {supplyPath}\n\nExport the 'Days Supply' sheet from Excel as CSV first.", "OK");
            return;
        }

        if (!File.Exists(setupPath))
        {
            EditorUtility.DisplayDialog("Error", $"File not found: {setupPath}\n\nExport the 'ItemSetup' sheet from Excel as CSV first.", "OK");
            return;
        }

        try
        {
            // Read both CSV files
            var itemsBySkuData = ReadSupplyCsv(supplyPath);
            Debug.Log($"[SkuImporter] Read {itemsBySkuData.Count} items from Days Supply CSV");

            var setupBySkuSetup = ReadSetupCsv(setupPath);
            Debug.Log($"[SkuImporter] Read {setupBySkuSetup.Count} items from ItemSetup CSV");

            // Validate cross-reference
            ValidateAndReportCrossReference(itemsBySkuData, setupBySkuSetup);

            // Merge and create SkuData assets
            var created = MergeAndCreateSkuData(itemsBySkuData, setupBySkuSetup);
            EditorUtility.DisplayDialog("Success", $"Created {created} SkuData assets in Assets/_Project/Data/Inventory/SKUs/", "OK");
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to import SKUs: {ex.Message}", "OK");
        }
    }

    /// <summary>Read Days Supply CSV (general item data)</summary>
    private static Dictionary<string, Sheet1Data> ReadSupplyCsv(string csvPath)
    {
        var result = new Dictionary<string, Sheet1Data>();
        var lines = File.ReadAllLines(csvPath);

        for (int i = 1; i < lines.Length && i < 100; i++) // Skip header, limit to 100 for testing
        {
            try
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] fields = line.Split(',');
                if (fields.Length < 9) continue;

                string sku = fields[0].Trim();
                if (string.IsNullOrEmpty(sku)) continue;

                var data = new Sheet1Data
                {
                    Sku = sku,
                    VendorNum = fields[1].Trim(),
                    VendorName = fields[2].Trim(),
                    Description = fields[3].Trim(),
                    Facility = fields[4].Trim(),
                    DailyMovement = ParseInt(fields[5]),
                    DaysSupply = ParseInt(fields[6]),
                    CasesInHouse = ParseInt(fields[7]),
                    PalletsInHouse = ParseInt(fields[8]),
                };

                if (!result.ContainsKey(data.Sku))
                    result[data.Sku] = data;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SkuImporter] Error reading supply CSV line {i}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>Read ItemSetup CSV (physical dimensions)</summary>
    private static Dictionary<string, Sheet2Data> ReadSetupCsv(string csvPath)
    {
        var result = new Dictionary<string, Sheet2Data>();
        var lines = File.ReadAllLines(csvPath);

        for (int i = 1; i < lines.Length && i < 100; i++) // Skip header, limit to 100 for testing
        {
            try
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] fields = line.Split(',');
                if (fields.Length < 9) continue;

                string sku = fields[0].Trim();
                if (string.IsNullOrEmpty(sku)) continue;

                var data = new Sheet2Data
                {
                    Sku = sku,
                    Description = fields[1].Trim(),
                    Location = fields[2].Trim(),
                    Hi = ParseInt(fields[3]),
                    Ti = ParseInt(fields[4]),
                    CaseHeight = ParseFloat(fields[5]),
                    CaseDepth = ParseFloat(fields[6]),
                    CaseWidth = ParseFloat(fields[7]),
                    CaseWeight = ParseFloat(fields[8]),
                };

                if (!result.ContainsKey(data.Sku))
                    result[data.Sku] = data;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SkuImporter] Error reading setup CSV line {i}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>Validate and report cross-reference between the two CSVs.</summary>
    private static void ValidateAndReportCrossReference(Dictionary<string, Sheet1Data> sheet1Data, Dictionary<string, Sheet2Data> sheet2Data)
    {
        int matched = 0;
        int onlyInDaysSupply = 0;
        int onlyInItemSetup = 0;

        // Check Days Supply items that have matching ItemSetup data
        foreach (var sku in sheet1Data.Keys)
        {
            if (sheet2Data.ContainsKey(sku))
                matched++;
            else
                onlyInDaysSupply++;
        }

        // Check ItemSetup items that don't have Days Supply data
        foreach (var sku in sheet2Data.Keys)
        {
            if (!sheet1Data.ContainsKey(sku))
                onlyInItemSetup++;
        }

        Debug.Log($"[SkuImporter] === CROSS-REFERENCE REPORT ===");
        Debug.Log($"Days Supply SKUs: {sheet1Data.Count}");
        Debug.Log($"ItemSetup SKUs: {sheet2Data.Count}");
        Debug.Log($"Successfully matched (Days Supply + ItemSetup): {matched}");
        Debug.Log($"Only in Days Supply (missing dimensions): {onlyInDaysSupply}");
        Debug.Log($"Only in ItemSetup (no demand data): {onlyInItemSetup}");
        Debug.Log($"Match rate: {(matched / (float)sheet1Data.Count * 100):F1}%");
    }

    /// <summary>Merge data from both sheets and create SkuData assets.</summary>
    private static int MergeAndCreateSkuData(Dictionary<string, Sheet1Data> sheet1Data, Dictionary<string, Sheet2Data> sheet2Data)
    {
        // Ensure output directory exists
        string outputDir = "Assets/_Project/Data/Inventory/SKUs";
        if (!System.IO.Directory.Exists(outputDir))
            System.IO.Directory.CreateDirectory(outputDir);

        int created = 0;
        int skipped = 0;

        // Merge: for each item in sheet1, find matching dimensions in sheet2
        foreach (var kvp in sheet1Data)
        {
            string sku = kvp.Key;
            var item1 = kvp.Value;

            Sheet2Data item2 = null;
            sheet2Data.TryGetValue(sku, out item2);

            // Only create asset if we have at least the Days Supply data
            if (item2 == null)
            {
                skipped++;
                continue; // Skip SKUs without dimension data
            }

            try
            {
                var skuData = CreateSkuData(sku, item1, item2);
                if (skuData != null)
                {
                    // Sanitize filename (remove invalid characters)
                    string safeName = System.Text.RegularExpressions.Regex.Replace(sku, "[^a-zA-Z0-9_-]", "");
                    string assetPath = $"{outputDir}/SKU_{safeName}.asset";
                    AssetDatabase.CreateAsset(skuData, assetPath);
                    created++;

                    // Log successful creation with Ti, Hi, Weight
                    if (item2 != null)
                        Debug.Log($"[SkuImporter] ✓ {sku}: Ti={item2.Ti}, Hi={item2.Hi}, Weight={item2.CaseWeight}lbs");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SkuImporter] Failed to create SKU {sku}: {ex.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SkuImporter] === CREATION SUMMARY ===");
        Debug.Log($"Created: {created} assets");
        Debug.Log($"Skipped (no dimension data): {skipped}");
        return created;
    }

    /// <summary>Create a single SkuData asset from merged data.</summary>
    private static SkuData CreateSkuData(string sku, Sheet1Data sheet1, Sheet2Data sheet2)
    {
        var asset = ScriptableObject.CreateInstance<SkuData>();

        // SKU ID
        string skuId = sku;
        string skuName = sheet1?.Description ?? sheet2?.Description ?? $"SKU {sku}";

        // Pricing: Generate based on weight/size (heavier = more expensive)
        int baseCost = 20; // Default wholesale cost per case
        if (sheet2 != null && sheet2.CaseWeight > 0)
            baseCost = Mathf.Max(10, Mathf.RoundToInt(sheet2.CaseWeight * 1.5f));
        int sellingPrice = Mathf.RoundToInt(baseCost * 1.5f); // 50% markup for retail

        // Demand: Use DailyMovement from sheet1
        int dailyDemand = sheet1?.DailyMovement ?? 5;

        // Size category: Based on case weight
        var sizeCategory = SkuData.SkuSizeCategory.Medium;
        if (sheet2 != null)
        {
            if (sheet2.CaseWeight < 5f)
                sizeCategory = SkuData.SkuSizeCategory.Small;
            else if (sheet2.CaseWeight > 15f)
                sizeCategory = SkuData.SkuSizeCategory.Large;
        }

        // Stacking: Lighter items can stack higher
        bool canStack = true;
        int maxStackHeight = 3;
        if (sheet2 != null)
        {
            if (sheet2.CaseWeight > 20f)
                maxStackHeight = 1; // Heavy items: no stacking
            else if (sheet2.CaseWeight > 10f)
                maxStackHeight = 2; // Medium weight: 2 high
        }

        // Shelf life: Based on DaysSupply (lower = more perishable)
        int shelfLifeDays = -1;
        if (sheet1 != null && sheet1.DaysSupply > 0)
        {
            if (sheet1.DaysSupply <= 7)
                shelfLifeDays = 3; // Very short shelf life (fresh produce/dairy)
            else if (sheet1.DaysSupply <= 14)
                shelfLifeDays = 7;
            else if (sheet1.DaysSupply <= 30)
                shelfLifeDays = 14;
            else
                shelfLifeDays = -1; // Long shelf life = non-perishable
        }

        // Initialize the asset
        asset.Initialize(
            skuId: skuId,
            skuName: skuName,
            unitCost: baseCost,
            sellingPrice: sellingPrice,
            shelfLifeDays: shelfLifeDays,
            sizeCategory: sizeCategory,
            canStack: canStack,
            maxStackHeight: maxStackHeight,
            averageDailyDemand: dailyDemand,
            tiCount: sheet2?.Ti ?? 0,
            hiCount: sheet2?.Hi ?? 0,
            caseWeight: sheet2?.CaseWeight ?? 0f
        );

        return asset;
    }

    private static int ParseInt(object value)
    {
        if (value == null) return 0;
        if (int.TryParse(value.ToString(), out int result)) return result;
        return 0;
    }

    private static float ParseFloat(object value)
    {
        if (value == null) return 0f;
        if (float.TryParse(value.ToString(), out float result)) return result;
        return 0f;
    }

    // Helper classes to hold sheet data
    private class Sheet1Data
    {
        public string Sku;
        public string VendorNum;
        public string VendorName;
        public string Description;
        public string Facility;
        public int DailyMovement;
        public int DaysSupply;
        public int CasesInHouse;
        public int PalletsInHouse;
    }

    private class Sheet2Data
    {
        public string Sku;
        public string Description;
        public string Location;
        public int Hi; // Units per pallet layer
        public int Ti; // Units per case/tier
        public float CaseHeight;
        public float CaseDepth;
        public float CaseWidth;
        public float CaseWeight;
    }
}

#endif
