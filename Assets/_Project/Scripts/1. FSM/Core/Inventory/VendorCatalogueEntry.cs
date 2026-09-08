using System;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Pairs an existing SkuData reference with a rarity tier, without modifying SkuData itself — a
    /// SKU is a product; how rare it is to buy from THIS vendor is a fact about the vendor
    /// relationship, not the product. Plain [Serializable] wrapper, same convention as every other
    /// inline list already serialized inside VendorData.
    /// </summary>
    [Serializable]
    public class VendorCatalogueEntry
    {
        [SerializeField] private SkuData sku;
        [SerializeField] private ItemRarity rarity;

        public SkuData Sku => sku;
        public ItemRarity Rarity => rarity;
    }
}
