using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>List of all CustomerData assets, same shape as ObjDataRegistry — a single Inspector
    /// asset the order generator pulls a random customer from. Linear scan, not a cached
    /// dictionary — this is a ~25-entry hand-authored roster, not a hot path, and a cache here
    /// is just a staleness bug waiting to happen if the list is ever edited after first load.</summary>
    [CreateAssetMenu(fileName = "CustomerRegistry", menuName = "Warehouse/Customer Registry")]
    public class CustomerRegistry : ScriptableObject
    {
        public List<CustomerData> customers = new();

        public CustomerData GetById(string customerId)
        {
            foreach (var c in customers)
                if (c != null && c.CustomerId == customerId)
                    return c;
            return null;
        }

        public CustomerData GetRandom()
        {
            if (customers == null || customers.Count == 0) return null;
            return customers[Random.Range(0, customers.Count)];
        }
    }
}
