using UnityEngine;

/// <summary>
/// Defines the master data for a single SKU (Stock Keeping Unit) — a product type.
/// Stores pricing, shelf life, stacking rules, and demand info.
/// </summary>
[CreateAssetMenu(fileName = "SKU_", menuName = "Warehouse/SKU")]
public class SkuData : ScriptableObject
{
    [SerializeField] private string _skuId;
    [SerializeField] private string _skuName;
    [SerializeField] private int _unitCost; // What we paid the supplier
    [SerializeField] private int _sellingPrice; // What customers pay (revenue per unit)
    [SerializeField] private int _shelfLifeDays; // Days until expires (-1 if non-perishable)
    [SerializeField] private SizeCategory _sizeCategory;
    [SerializeField] private bool _canStack;
    [SerializeField] private int _maxStackHeight;
    [SerializeField] private int _averageDailyDemand; // Units/day for forecasting

    public string SkuId => _skuId;
    public string SkuName => _skuName;
    public int UnitCost => _unitCost;
    public int SellingPrice => _sellingPrice;
    public int ShelfLifeDays => _shelfLifeDays;
    public SizeCategory SizeCategory => _sizeCategory;
    public bool CanStack => _canStack;
    public int MaxStackHeight => _maxStackHeight;
    public int AverageDailyDemand => _averageDailyDemand;

    /// <summary>Gross profit per unit = selling price - unit cost.</summary>
    public int GrossProfitPerUnit => _sellingPrice - _unitCost;

    /// <summary>Profit margin as percentage.</summary>
    public float ProfitMarginPercent => _unitCost > 0 ? (GrossProfitPerUnit / (float)_unitCost * 100f) : 0f;

    public enum SizeCategory { Small, Medium, Large }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_skuId))
            _skuId = System.Guid.NewGuid().ToString();

        if (_shelfLifeDays < -1)
            _shelfLifeDays = -1;

        if (_maxStackHeight < 1)
            _maxStackHeight = 1;
    }
}
