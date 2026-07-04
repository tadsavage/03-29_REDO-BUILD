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
    [SerializeField] private SkuSizeCategory _sizeCategory;
    [SerializeField] private bool _canStack;
    [SerializeField] private int _maxStackHeight;
    [SerializeField] private int _averageDailyDemand; // Units/day for forecasting
    [SerializeField] private int _tiCount; // Units per case tier (from Excel)
    [SerializeField] private int _hiCount; // Units per pallet layer (from Excel)
    [SerializeField] private float _caseWeight; // Weight in lbs (from Excel)
    [SerializeField] private GameObject _casePrefab; // Prefab of the case to spawn on pallets

    public string SkuId => _skuId;
    public string SkuName => _skuName;
    public int UnitCost => _unitCost;
    public int SellingPrice => _sellingPrice;
    public int ShelfLifeDays => _shelfLifeDays;
    public SkuSizeCategory SizeCategory => _sizeCategory;
    public bool CanStack => _canStack;
    public int MaxStackHeight => _maxStackHeight;
    public int AverageDailyDemand => _averageDailyDemand;
    public int TiCount => _tiCount; // Units per case
    public int HiCount => _hiCount; // Units per pallet layer
    public float CaseWeight => _caseWeight;
    public GameObject CasePrefab => _casePrefab;

    /// <summary>Gross profit per unit = selling price - unit cost.</summary>
    public int GrossProfitPerUnit => _sellingPrice - _unitCost;

    /// <summary>Profit margin as percentage.</summary>
    public float ProfitMarginPercent => _unitCost > 0 ? (GrossProfitPerUnit / (float)_unitCost * 100f) : 0f;

    public enum SkuSizeCategory { Small, Medium, Large }

    /// <summary>Builder method for creating SKU data from import sources.</summary>
    public void Initialize(string skuId, string skuName, int unitCost, int sellingPrice,
        int shelfLifeDays, SkuSizeCategory sizeCategory, bool canStack, int maxStackHeight,
        int averageDailyDemand, int tiCount = 0, int hiCount = 0, float caseWeight = 0f)
    {
        _skuId = skuId;
        _skuName = skuName;
        _unitCost = unitCost;
        _sellingPrice = sellingPrice;
        _shelfLifeDays = shelfLifeDays;
        _sizeCategory = sizeCategory;
        _canStack = canStack;
        _maxStackHeight = maxStackHeight;
        _averageDailyDemand = averageDailyDemand;
        _tiCount = tiCount;
        _hiCount = hiCount;
        _caseWeight = caseWeight;
    }

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
