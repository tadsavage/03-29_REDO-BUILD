using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Master data for a single SKU (product type). Redesigned 2026-07-04 to a small hand-authored
    /// schema (10-20 items total, not an Excel/CSV-imported catalog) — see Tad's spec. Every field
    /// below is player-entered per item; PltHeight is the only derived value.
    /// </summary>
    [CreateAssetMenu(fileName = "SKU_", menuName = "Warehouse/SKU")]
    public class SkuData : ScriptableObject
    {
        [SerializeField] private int _itemNumber;
        [SerializeField] private string _itemDescription;

        [SerializeField] private float _caseLength; // meters
        [SerializeField] private float _caseWidth;  // meters
        [SerializeField] private float _caseHeight; // meters
        [SerializeField] private float _csWeight;   // lbs

        [SerializeField] private PalletData.AreaCategory _storageArea;

        [SerializeField] private float _buyValue;  // cost per case
        [SerializeField] private float _sellValue; // revenue per case

        [SerializeField] private int _ti; // cases per pallet layer
        [SerializeField] private int _hi; // layers per pallet

        [SerializeField] private GameObject _prefab;
        [SerializeField] private Sprite _icon;

        // Kept for the existing spoilage system (InventoryService.CheckSpoilage) — not part of
        // Tad's new field list, but Perishable items still need an expiration window. -1 = non-perishable.
        [SerializeField] private int _shelfLifeDays = -1;

        public int ItemNumber => _itemNumber;
        public string ItemDescription => _itemDescription;

        public float CaseLength => _caseLength;
        public float CaseWidth => _caseWidth;
        public float CaseHeight => _caseHeight;
        public float CaseWeight => _csWeight;

        public PalletData.AreaCategory StorageArea => _storageArea;

        public float BuyValue => _buyValue;
        public float SellValue => _sellValue;

        public int Ti => _ti;
        public int Hi => _hi;

        /// <summary>Runtime-safe Ti/Hi update — lets the dock (the actual, physical case layout
        /// on a pallet) become the new master spec for future pallets of this SKU. Works in a
        /// build, not just the Editor (unlike the Editor-only SerializedObject-based "Submit"
        /// button in ToolsWindowController, which still separately persists to the asset on disk
        /// when running in-editor).</summary>
        public void SetTiHi(int ti, int hi)
        {
            _ti = ti;
            _hi = hi;
        }

        public GameObject Prefab => _prefab;
        public Sprite Icon => _icon;

        public int ShelfLifeDays => _shelfLifeDays;

        /// <summary>String identity used everywhere else in the codebase (PalletMasterRecord.SkuId,
        /// ShipmentLineItem, SlotAssignmentService, etc.) as a dictionary key — derived from
        /// ItemNumber so those call sites didn't need to change over to an int key.</summary>
        public string SkuId => _itemNumber.ToString();

        /// <summary>Full pallet height (meters) = case height x layers-per-pallet + 0.165m pallet base.
        /// Used to fit a SKU's pallet against rack level heights and PO/trailer height budgets
        /// (trailer interior 2m; 48" slots 1m; 80" slots 1.8m).</summary>
        public float PltHeight => (_caseHeight * _hi) + 0.165f;

        public float GrossProfitPerUnit => _sellValue - _buyValue;
        public float ProfitMarginPercent => _buyValue > 0f ? (GrossProfitPerUnit / _buyValue * 100f) : 0f;

        private void OnValidate()
        {
            if (_shelfLifeDays < -1)
                _shelfLifeDays = -1;
        }
    }
}
