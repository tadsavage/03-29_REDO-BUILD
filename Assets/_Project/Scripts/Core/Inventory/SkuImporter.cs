using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR

/// <summary>
/// Imports SKU data from Excel spreadsheet into SkuData ScriptableObjects.
/// Two-sheet format:
/// - Sheet 1 "Days Supply": Item, Vendor, Vendor Name, Description, Daily Movement, Days Supply, etc.
/// - Sheet 2 "ItemSetup": SKU, Description, High (Hi), Tie (Ti), Case dimensions, Weight
///
/// Creates SkuData assets with realistic pricing, demand, and physical properties.
/// </summary>
public class SkuImporter
{
    [MenuItem("Warehouse/Import SKUs from Excel")]
    public static void ImportFromExcel()
    {
        string excelPath = "C:\\Users\\MURILLO\\Downloads\\ItemFilesForForkIT.xlsx";

        if (!System.IO.File.Exists(excelPath))
        {
            EditorUtility.DisplayDialog("Error", $"Excel file not found: {excelPath}", "OK");
            return;
        }

        // Load Excel
        dynamic excel = System.Activator.CreateInstance(System.Type.GetTypeFromProgID("Excel.Application"));
        excel.Visible = false;
        dynamic workbook = excel.Workbooks.Open(excelPath);

        try
        {
            // Read Sheet 1: Days Supply (general item data)
            var sheet1 = workbook.Sheets(1);
            var itemsBySkuData = ReadSheet1(sheet1);
            Debug.Log($"[SkuImporter] Read {itemsBySkuData.Count} items from Sheet 1 (Days Supply)");

            // Read Sheet 2: ItemSetup (physical dimensions)
            var sheet2 = workbook.Sheets(2);
            var setupBySkuSetup = ReadSheet2(sheet2);
            Debug.Log($"[SkuImporter] Read {setupBySkuSetup.Count} items from Sheet 2 (ItemSetup)");

            // Merge and create SkuData assets
            var created = MergeAndCreateSkuData(itemsBySkuData, setupBySkuSetup);
            EditorUtility.DisplayDialog("Success", $"Created {created} SkuData assets in Assets/_Project/Data/Inventory/SKUs/", "OK");
        }
        finally
        {
            workbook.Close();
            excel.Quit();
            System.Runtime.InteropServices.Marshal.ReleaseComObject(excel);
        }
    }

    /// <summary>Read Sheet 1: Days Supply (general item data)</summary>
    private static Dictionary<string, Sheet1Data> ReadSheet1(dynamic sheet)
    {
        var result = new Dictionary<string, Sheet1Data>();
        var usedRange = sheet.UsedRange;
        int rows = usedRange.Rows.Count;
        int cols = usedRange.Columns.Count;

        for (int row = 2; row <= rows && row < 100; row++) // Limit to first 100 for testing
        {
            try
            {
                string sku = (sheet.Cells(row, 1).Value2 ?? "").ToString().Trim();
                if (string.IsNullOrEmpty(sku)) continue;

                var data = new Sheet1Data
                {
                    Sku = sku,
                    VendorNum = (sheet.Cells(row, 2).Value2 ?? "").ToString().Trim(),
                    VendorName = (sheet.Cells(row, 3).Value2 ?? "").ToString().Trim(),
                    Description = (sheet.Cells(row, 4).Value2 ?? "").ToString().Trim(),
                    Facility = (sheet.Cells(row, 5).Value2 ?? "").ToString().Trim(),
                    DailyMovement = ParseInt(sheet.Cells(row, 6).Value2),
                    DaysSupply = ParseInt(sheet.Cells(row, 7).Value2),
                    CasesInHouse = ParseInt(sheet.Cells(row, 8).Value2),
                    PalletsInHouse = ParseInt(sheet.Cells(row, 9).Value2),
                };

                if (!result.ContainsKey(data.Sku))
                    result[data.Sku] = data;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SkuImporter] Error reading Sheet1 row {row}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>Read Sheet 2: ItemSetup (physical dimensions)</summary>
    private static Dictionary<string, Sheet2Data> ReadSheet2(dynamic sheet)
    {
        var result = new Dictionary<string, Sheet2Data>();
        var usedRange = sheet.UsedRange;
        int rows = usedRange.Rows.Count;
        int cols = usedRange.Columns.Count;

        for (int row = 2; row <= rows && row < 100; row++)
        {
            try
            {
                string sku = (sheet.Cells(row, 1).Value2 ?? "").ToString().Trim();
                if (string.IsNullOrEmpty(sku)) continue;

                var data = new Sheet2Data
                {
                    Sku = sku,
                    Description = (sheet.Cells(row, 2).Value2 ?? "").ToString().Trim(),
                    Location = (sheet.Cells(row, 3).Value2 ?? "").ToString().Trim(),
                    Hi = ParseInt(sheet.Cells(row, 4).Value2),
                    Ti = ParseInt(sheet.Cells(row, 5).Value2),
                    CaseHeight = ParseFloat(sheet.Cells(row, 6).Value2),
                    CaseDepth = ParseFloat(sheet.Cells(row, 7).Value2),
                    CaseWidth = ParseFloat(sheet.Cells(row, 8).Value2),
                    CaseWeight = ParseFloat(sheet.Cells(row, 9).Value2),
                };

                if (!result.ContainsKey(data.Sku))
                    result[data.Sku] = data;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SkuImporter] Error reading Sheet2 row {row}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>Merge data from both sheets and create SkuData assets.</summary>
    private static int MergeAndCreateSkuData(Dictionary<string, Sheet1Data> sheet1Data, Dictionary<string, Sheet2Data> sheet2Data)
    {
        // Ensure output directory exists
        string outputDir = "Assets/_Project/Data/Inventory/SKUs";
        if (!System.IO.Directory.Exists(outputDir))
            System.IO.Directory.CreateDirectory(outputDir);

        int created = 0;

        // Merge: for each item in sheet1, find matching dimensions in sheet2
        foreach (var kvp in sheet1Data)
        {
            string sku = kvp.Key;
            var item1 = kvp.Value;

            Sheet2Data item2 = null;
            sheet2Data.TryGetValue(sku, out item2);

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
                    Debug.Log($"[SkuImporter] Created {assetPath}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SkuImporter] Failed to create SKU {sku}: {ex.Message}");
            }
        }

        AssetDatabase.SaveAssets();
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
