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

        // Y-offset of the case mesh center from the prefab origin, in meters. Most cases have center-origin
        // (≈ 0); some have bottom-origin (> 0, mesh sits above origin) or top-origin (< 0, mesh sits below).
        // Auto-populated by PalletBuilder's "Auto-Detect Mesh Offsets" utility; used to correct case
        // positioning in Build() so they don't clip into pallets or float above them.
        [SerializeField] private float _meshYOffsetMeters = 0f;

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

        public GameObject Prefab => _prefab;
        public Sprite Icon => _icon;
        public float MeshYOffsetMeters => _meshYOffsetMeters;

        public int ShelfLifeDays => _shelfLifeDays;

        /// <summary>String identity used everywhere else in the codebase (PalletMasterRecord.SkuId,
        /// ShipmentLineItem, SlotAssignmentService, etc.) as a dictionary key — derived from
        /// ItemNumber so those call sites didn't need to change over to an int key.</summary>
        public string SkuId => _itemNumber.ToString();

        /// <summary>Full pallet height (meters) = case height x layers-per-pallet + 0.16m pallet base.
        /// Used to fit a SKU's pallet against rack level heights and PO/trailer height budgets
        /// (trailer interior 2m; 48" slots 1m; 80" slots 1.8m).</summary>
        public float PltHeight => (_caseHeight * _hi) + 0.16f;

        public float GrossProfitPerUnit => _sellValue - _buyValue;
        public float ProfitMarginPercent => _buyValue > 0f ? (GrossProfitPerUnit / _buyValue * 100f) : 0f;

        private void OnValidate()
        {
            if (_shelfLifeDays < -1)
                _shelfLifeDays = -1;
        }

#if UNITY_EDITOR
        /// <summary>Auto-detect mesh Y-offset for this SKU and store it in _meshYOffsetMeters.
        /// Call this context menu to populate a single SKU, or use "Auto-Detect All SKU Mesh Offsets"
        /// from the SkuData editor tool to batch-populate all SKUs at once.</summary>
        [ContextMenu("Auto-Detect Mesh Y Offset")]
        public void AutoDetectMeshYOffset()
        {
            if (_prefab == null)
            {
                Debug.LogWarning($"[SkuData] {name}: Prefab is null, cannot detect mesh offset.");
                return;
            }

            MeshFilter mf = _prefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                _meshYOffsetMeters = mf.sharedMesh.bounds.center.y;
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"[SkuData] {name} (SKU {_itemNumber}): Mesh Y-offset = {_meshYOffsetMeters:F4}m");
            }
            else
            {
                Debug.LogWarning($"[SkuData] {name}: No MeshFilter found in prefab.");
            }
        }
#endif
    }
}
