using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// The physical pallet data component. Attached to the pallet GameObject AFTER receiving is complete.
    ///
    /// This is separate from PalletMasterRecord (the inventory system's overhead record): PalletData
    /// is only present on solid, received pallets. If a pallet has no PalletData script, it's ghosted
    /// and awaiting receiving.
    ///
    /// PalletData holds:
    /// - LoadId: the 10-digit "license plate" (master key linking to PalletMasterRecord)
    /// - Item info: SKU, case quantity, expiration date
    /// - Metadata: area/category, status, icon for UI display
    /// - Current grid location
    /// </summary>
    public class PalletData : MonoBehaviour
    {
        [SerializeField] private string _loadId;
        [SerializeField] private string _itemNumber; // SKU
        [SerializeField] private int _caseQuantity;
        [SerializeField] private int _expirationDay = -1; // In-game day; -1 = non-perishable

        [SerializeField] private AreaCategory _area;
        [SerializeField] private Sprite _iconSprite;

        [SerializeField] private Vector2Int _currentLocation;

        /// <summary>Human-readable name of where this pallet physically is — a rack slot address
        /// ("01-01-A0") once put away, or a staging-lane slot ("1A-3") while on the dock. This is
        /// the field to cross-reference against a slot's LocationData.Address; the Vector2Int grid
        /// cell above is ambiguous (a whole rack bay shares one cell across both positions and every
        /// level) and unreadable at a glance.</summary>
        [SerializeField] private string _locationName;

        [SerializeField] private PalletStatus _status = PalletStatus.Shippable;

        public string LoadId => _loadId;
        public string ItemNumber => _itemNumber;
        public int CaseQuantity => _caseQuantity;
        public int ExpirationDay => _expirationDay;

        public AreaCategory Area => _area;
        public Sprite IconSprite => _iconSprite;

        public Vector2Int CurrentLocation => _currentLocation;

        /// <summary>Human-readable location name — rack slot address ("01-01-A0") or staging-lane
        /// slot ("1A-3"). Cross-references with LocationData.Address.</summary>
        public string LocationName => _locationName;

        public PalletStatus Status => _status;

        /// <summary>Initialize pallet data (called when receiving completes and this component is added).</summary>
        public void Initialize(string loadId, string itemNumber, int caseQuantity, int expirationDay,
                               AreaCategory area, Sprite iconSprite, Vector2Int location)
        {
            _loadId = loadId;
            _itemNumber = itemNumber;
            _caseQuantity = caseQuantity;
            _expirationDay = expirationDay;
            _area = area;
            _iconSprite = iconSprite;
            _currentLocation = location;
            _status = PalletStatus.Shippable; // Default on creation
        }

        /// <summary>Update pallet location (called when pallet is moved).</summary>
        public void SetLocation(Vector2Int newLocation)
        {
            _currentLocation = newLocation;
        }

        /// <summary>Update pallet location AND its human-readable name together — a rack slot
        /// address ("01-01-A0") or a staging-lane slot ("1A-3"). Prefer this over the Vector2Int-only
        /// overload so the readable name can never drift out of sync with the grid cell.</summary>
        public void SetLocation(Vector2Int newLocation, string locationName)
        {
            _currentLocation = newLocation;
            _locationName    = locationName;
        }

        /// <summary>Update pallet status (QA hold, lost, on reserve, etc.).</summary>
        public void SetStatus(PalletStatus newStatus)
        {
            _status = newStatus;
        }

        /// <summary>Reduce case quantity (called during order picking).</summary>
        public void ReduceQuantity(int amount)
        {
            _caseQuantity = Mathf.Max(0, _caseQuantity - amount);
        }

        /// <summary>Check if this pallet is expired based on current day number.</summary>
        public bool IsExpired(int currentDayNumber)
        {
            return _expirationDay >= 0 && currentDayNumber > _expirationDay;
        }

        /// <summary>Check if this pallet is expiring soon (within 2 days).</summary>
        public bool IsExpiringSoon(int currentDayNumber, int daysUntilWarning = 2)
        {
            return _expirationDay >= 0
                && currentDayNumber > _expirationDay - daysUntilWarning
                && currentDayNumber <= _expirationDay;
        }

        public enum AreaCategory
        {
            Grocery,
            Perishable,
            Frozen
        }

        public enum PalletStatus
        {
            Shippable,    // Live, ready to move/pick
            QAHold,       // Under quality inspection
            Lost,         // System can't locate it
            OnReserve     // Reserved for specific order
        }
    }
}
