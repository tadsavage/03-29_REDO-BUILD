using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Master data for a single outbound customer — a small hand-authored roster (25 fictional
    /// vendor/brand customers), same spirit as SkuData's hand-authored SKU list. Used to generate
    /// customer orders (OrderData.CustomerId/CustomerName) and to show a face/icon for the customer
    /// in reports and other UI.
    /// </summary>
    [CreateAssetMenu(fileName = "Customer_", menuName = "Warehouse/Customer")]
    public class CustomerData : ScriptableObject
    {
        [SerializeField] private string _companyName;
        [SerializeField, TextArea(2, 4)] private string _productDescription;
        [SerializeField] private string _productCategory;
        [SerializeField] private Sprite _icon;

        public string CompanyName => _companyName;
        public string ProductDescription => _productDescription;
        public string ProductCategory => _productCategory;
        public Sprite Icon => _icon;

        /// <summary>String identity used as OrderData.CustomerId — derived from the company name
        /// since this is a small hand-authored roster, not an imported/numbered catalog (mirrors
        /// how SkuData.SkuId is derived rather than hand-entered).</summary>
        public string CustomerId => _companyName;
    }
}
