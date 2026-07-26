using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Live data component for a single rack slot.
    /// Placed on a child GameObject of the rack whose name equals the slot address
    /// (e.g. "01-02-A0"), enabling lookups via rack.transform.Find("01-02-A0")
    /// or GetComponentsInChildren&lt;LocationData&gt;().
    ///
    /// Created and maintained at runtime by LocationRegistry.
    /// Mutation methods mirror state into LocationStatusRegistry for backward compatibility.
    /// </summary>
    public class LocationData : MonoBehaviour
    {
        // ── Identity ─────────────────────────────────────────────────────────────────────

        [Header("Identity")]
        [SerializeField] private string _address;
        [SerializeField] private LocationType _type;

        // ── Status ───────────────────────────────────────────────────────────────────────

        [Header("Status")]
        [SerializeField] private LocationStatus _status = LocationStatus.Available;

        // ── Contents ─────────────────────────────────────────────────────────────────────

        [Header("Contents")]
        /// <summary>The 10-digit human-readable "license plate" of the pallet in this slot. This is
        /// the field to cross-reference against a pallet's own PalletData.LoadId — the internal
        /// PalletId below is a GUID and is useless for eyeballing in the Inspector.</summary>
        [SerializeField] private string _loadId;
        [SerializeField] private string _palletId;
        [SerializeField] private string _skuId;
        [SerializeField] private int    _quantity;
        [SerializeField] private string _expirationDate; // ISO 8601 (YYYY-MM-DD)

        // ── Properties ───────────────────────────────────────────────────────────────────

        /// <summary>Slot address, e.g. "01-02-A0". Mirrors the GameObject name.</summary>
        public string         Address        => _address;

        /// <summary>Whether this is a pick-face or reserve slot.</summary>
        public LocationType   Type           => _type;

        /// <summary>Current operational status.</summary>
        public LocationStatus Status         => _status;

        /// <summary>The human-readable 10-digit Load ID ("license plate") of the pallet currently
        /// here; null/empty when vacant. Cross-references directly with PalletData.LoadId.</summary>
        public string         LoadId         => _loadId;

        /// <summary>PalletId (internal GUID) of the pallet currently here; null/empty when vacant.
        /// Use <see cref="LoadId"/> when a human needs to read/match it.</summary>
        public string         PalletId       => _palletId;

        /// <summary>SKU / item number of the current load; null/empty when vacant.</summary>
        public string         SkuId          => _skuId;

        /// <summary>Unit quantity of the current pallet load; 0 when vacant.</summary>
        public int            Quantity       => _quantity;

        /// <summary>Expiration / best-before in ISO 8601 (YYYY-MM-DD); null if not applicable.</summary>
        public string         ExpirationDate => _expirationDate;

        /// <summary>World-space centre of this slot (rack face at shelf height).</summary>
        public Vector3        WorldPosition  => transform.position;

        /// <summary>True when the slot is empty and eligible for putaway.</summary>
        public bool           IsAvailable    => _status == LocationStatus.Available;

        // ── Initialization ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets identity fields and renames the GameObject to the slot address.
        /// Called by LocationRegistry on create and on every Recompute pass.
        /// </summary>
        public void Initialize(string address, LocationType type)
        {
            _address        = address;
            _type           = type;
            gameObject.name = address;
        }

        /// <summary>
        /// Mirrors this component's displayed <see cref="_status"/> from <see cref="LocationStatusRegistry"/>
        /// (the actual source of truth PutawayLogic queries) WITHOUT writing back into the registry.
        /// Called by LocationRegistry's periodic recompute so the Inspector can never go stale relative
        /// to the registry — some writers (PutawayLogic.AssignPutawayDestination/CompletePutaway,
        /// ReplenishmentService.CreateReplenishTask) lock/release slots directly on the registry rather
        /// than through this component's own Reserve()/Occupy()/Release() mutators.
        /// </summary>
        public void SyncStatusDisplay(LocationStatus status)
        {
            _status = status;
        }

        // ── Mutation ─────────────────────────────────────────────────────────────────────

        /// <summary>Locks the slot for an incoming putaway (Available to Reserved).</summary>
        public void Reserve()
        {
            _status = LocationStatus.Reserved;
            LocationStatusRegistry.Reserve(_address);
        }

        /// <summary>
        /// Marks the slot occupied and stores the pallet's inventory details (Reserved to Occupied).
        /// </summary>
        public void Occupy(string palletId, string skuId, int quantity, string expirationDate = null,
                           string loadId = null)
        {
            _palletId       = palletId;
            _loadId         = loadId;
            _skuId          = skuId;
            _quantity       = quantity;
            _expirationDate = expirationDate;
            _status         = LocationStatus.Occupied;
            LocationStatusRegistry.MarkOccupied(_address);
        }

        /// <summary>Releases the slot back to Available and clears all inventory fields.</summary>
        public void Release()
        {
            _palletId       = null;
            _loadId         = null;
            _skuId          = null;
            _quantity       = 0;
            _expirationDate = null;
            _status         = LocationStatus.Available;
            LocationStatusRegistry.Release(_address);
        }

        /// <summary>Removes `amount` units from this slot's current load (order picking). Clamped
        /// so it can never go negative. Fully draining the slot releases it back to Available —
        /// the same signal ReplenishmentService already scans for — instead of leaving a phantom
        /// Occupied slot with 0 qty and no pallet.</summary>
        public void Pick(int amount)
        {
            _quantity = Mathf.Max(0, _quantity - amount);
            if (_quantity <= 0)
            {
                Release();
            }
        }

        /// <summary>
        /// Places the slot on hold without clearing inventory.
        /// Pass LocationStatus.QAHold or LocationStatus.Problem.
        /// </summary>
        public void SetHold(LocationStatus holdStatus = LocationStatus.QAHold)
        {
            if (holdStatus != LocationStatus.QAHold && holdStatus != LocationStatus.Problem)
            {
                Debug.LogWarning($"[LocationData] SetHold: invalid hold status '{holdStatus}' on '{_address}'.");
                return;
            }
            _status = holdStatus;
            LocationStatusRegistry.Set(_address, holdStatus);
        }
    }
}
