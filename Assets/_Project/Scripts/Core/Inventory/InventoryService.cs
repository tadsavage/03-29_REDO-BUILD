using GameCore.Services;
using GameCore.Events;
using GameCore.Economy;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Manages all warehouse inventory: pallets, locations, stock levels, spoilage, and ledger.
/// Separate data layer from PlacedObjectRegistry — pallets are inventory entities, not grid objects.
///
/// LIFECYCLE:
/// 1. Initialize() at game start
/// 2. ReceivePallet() when shipment arrives
/// 3. MovePallet() when putaway/pick occurs
/// 4. PickFromPallet() when order picking reduces quantity
/// 5. CheckSpoilage() daily to mark expired items
/// 6. Shutdown() at game end
///
/// EVENTS PUBLISHED:
/// - OnPalletReceived(PalletData) — new pallet added to inventory
/// - OnPalletMoved(PalletData, Vector2Int from, Vector2Int to) — pallet location changed
/// - OnPalletPartialPicked(PalletData, int quantityRemoved) — order pick reduced quantity
/// - OnPalletDestroyed(PalletData) — pallet removed from inventory (empty or contaminated)
/// - OnSpoilageDetected(PalletData) — pallet marked contaminated
/// </summary>
public class InventoryService : IService
{
    private readonly Dictionary<string, PalletData> _palletsByID = new();
    private readonly Dictionary<Vector2Int, List<string>> _palletsByLocation = new();
    private readonly Dictionary<string, SkuData> _skuDataCache = new();
    private EventManager _eventManager;
    private SimulationTimeService _timeService;

    // Events
    public static event System.Action<PalletData> OnPalletReceived;
    public static event System.Action<PalletData, Vector2Int, Vector2Int> OnPalletMoved;
    public static event System.Action<PalletData, int> OnPalletPartialPicked;
    public static event System.Action<PalletData> OnPalletDestroyed;
    public static event System.Action<PalletData> OnSpoilageDetected;

    // Debug/diagnostic
    public IReadOnlyDictionary<string, PalletData> AllPallets => _palletsByID;
    public IReadOnlyDictionary<Vector2Int, List<string>> PalletsByLocation => _palletsByLocation;

    // ============ LIFECYCLE ============

    public void Initialize()
    {
        Debug.Log("[InventoryService] Initializing...");

        _eventManager = EventManager.Instance;
        if (_eventManager == null)
        {
            Debug.LogError("[InventoryService] EventManager not found.");
            return;
        }

        _timeService = ServiceLocator.Get<SimulationTimeService>();
        if (_timeService == null)
        {
            Debug.LogError("[InventoryService] SimulationTimeService not found.");
            return;
        }

        // Subscribe to daily tick for spoilage checks
        _eventManager.Subscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);

        Debug.Log("[InventoryService] Initialized.");
    }

    public void Shutdown()
    {
        Debug.Log("[InventoryService] Shutting down...");

        if (_eventManager != null)
            _eventManager.Unsubscribe<int>(GameEvents.Time.OnDayChanged, OnDayChanged);

        _palletsByID.Clear();
        _palletsByLocation.Clear();
        _skuDataCache.Clear();

        Debug.Log("[InventoryService] Shut down complete.");
    }

    // ============ PALLET OPERATIONS ============

    /// <summary>Receive a new shipment and create pallets for each SKU.</summary>
    public List<PalletData> ReceiveShipment(List<(string skuId, int quantity, int expirationDayOffset)> items)
    {
        var created = new List<PalletData>();
        int currentDay = _timeService?.Day ?? 0;

        foreach (var (skuId, quantity, expirationOffset) in items)
        {
            int expirationDay = expirationOffset >= 0 ? currentDay + expirationOffset : -1;
            var pallet = new PalletData(skuId, quantity, Vector2Int.zero, currentDay, expirationDay);
            _palletsByID[pallet.PalletId] = pallet;

            // Initially in receiving staging (0,0) — will be putaway by employee
            if (!_palletsByLocation.ContainsKey(pallet.CurrentLocation))
                _palletsByLocation[pallet.CurrentLocation] = new List<string>();
            _palletsByLocation[pallet.CurrentLocation].Add(pallet.PalletId);

            created.Add(pallet);
            OnPalletReceived?.Invoke(pallet);

            Debug.Log($"[InventoryService] Received pallet {pallet.PalletId}: {quantity} × {skuId}");
        }

        return created;
    }

    /// <summary>Move a pallet from one location to another (putaway or relocation).</summary>
    public bool MovePallet(string palletId, Vector2Int newLocation)
    {
        if (!_palletsByID.TryGetValue(palletId, out var pallet))
        {
            Debug.LogWarning($"[InventoryService] Pallet {palletId} not found.");
            return false;
        }

        Vector2Int oldLocation = pallet.CurrentLocation;
        pallet.CurrentLocation = newLocation;

        // Update location index
        if (_palletsByLocation.TryGetValue(oldLocation, out var oldList))
            oldList.Remove(palletId);

        if (!_palletsByLocation.ContainsKey(newLocation))
            _palletsByLocation[newLocation] = new List<string>();
        _palletsByLocation[newLocation].Add(palletId);

        OnPalletMoved?.Invoke(pallet, oldLocation, newLocation);
        Debug.Log($"[InventoryService] Moved pallet {palletId} from {oldLocation} to {newLocation}");

        return true;
    }

    /// <summary>Remove units from a pallet (e.g., for order picking). Returns quantity actually removed.</summary>
    public int PickFromPallet(string palletId, int quantityToRemove)
    {
        if (!_palletsByID.TryGetValue(palletId, out var pallet))
        {
            Debug.LogWarning($"[InventoryService] Pallet {palletId} not found for pick.");
            return 0;
        }

        int actualRemoved = Mathf.Min(quantityToRemove, pallet.Quantity);
        pallet.Quantity -= actualRemoved;

        OnPalletPartialPicked?.Invoke(pallet, actualRemoved);

        if (pallet.Quantity <= 0)
            DestroyPallet(palletId);

        Debug.Log($"[InventoryService] Picked {actualRemoved} units from pallet {palletId}");
        return actualRemoved;
    }

    /// <summary>Remove a pallet from inventory (empty or contaminated).</summary>
    public void DestroyPallet(string palletId)
    {
        if (!_palletsByID.TryGetValue(palletId, out var pallet))
            return;

        if (_palletsByLocation.TryGetValue(pallet.CurrentLocation, out var list))
            list.Remove(palletId);

        _palletsByID.Remove(palletId);
        OnPalletDestroyed?.Invoke(pallet);

        Debug.Log($"[InventoryService] Destroyed pallet {palletId}");
    }

    // ============ INVENTORY QUERIES ============

    /// <summary>Get total units of a SKU across all locations.</summary>
    public int GetTotalUnitsBySku(string skuId)
    {
        return _palletsByID.Values
            .Where(p => p.SkuId == skuId && !p.IsContaminated)
            .Sum(p => p.Quantity);
    }

    /// <summary>Get all pallets at a specific location.</summary>
    public List<PalletData> GetPalletsAtLocation(Vector2Int location)
    {
        if (!_palletsByLocation.TryGetValue(location, out var palletIds))
            return new List<PalletData>();

        return palletIds
            .Where(id => _palletsByID.ContainsKey(id))
            .Select(id => _palletsByID[id])
            .ToList();
    }

    /// <summary>Get all pallets for a specific SKU.</summary>
    public List<PalletData> GetPalletsBySku(string skuId)
    {
        return _palletsByID.Values
            .Where(p => p.SkuId == skuId && !p.IsContaminated)
            .OrderByDescending(p => p.Quantity) // Largest quantities first (for picking efficiency)
            .ToList();
    }

    /// <summary>Get all non-contaminated pallets at receiving staging (0,0).</summary>
    public List<PalletData> GetReceivingPallets()
    {
        return GetPalletsAtLocation(Vector2Int.zero)
            .Where(p => !p.IsContaminated)
            .ToList();
    }

    /// <summary>Check if location has capacity for another pallet.</summary>
    public bool HasCapacityAtLocation(Vector2Int location, ObjDataSO storageData = null)
    {
        if (!_palletsByLocation.TryGetValue(location, out var palletIds))
            return true; // Empty location has capacity

        // TODO: Implement actual capacity checking based on storage object footprint
        // For MVP, assume 4-slot capacity per cell
        return palletIds.Count < 4;
    }

    // ============ EVENT HANDLERS ============

    private void OnDayChanged(string eventId, int newDay)
    {
        Debug.Log($"[InventoryService] Day changed to {newDay}. Checking spoilage...");
        CheckSpoilage();
    }

    private void CheckSpoilage()
    {
        int currentDay = _timeService?.Day ?? 0;
        var expiredPallets = _palletsByID.Values
            .Where(p => !p.IsContaminated && p.IsExpired(currentDay))
            .ToList();

        foreach (var pallet in expiredPallets)
        {
            pallet.IsContaminated = true;
            OnSpoilageDetected?.Invoke(pallet);
            Debug.Log($"[InventoryService] SPOILAGE: Pallet {pallet.PalletId} ({pallet.SkuId}) expired.");
        }
    }

    // ============ SKU CACHING ============

    /// <summary>Load or cache SKU master data.</summary>
    public SkuData GetSkuData(string skuId)
    {
        if (_skuDataCache.TryGetValue(skuId, out var cached))
            return cached;

        // TODO: Load SKU data from Resources or AssetDatabase
        // For MVP, return null (SKU must be pre-cached via LoadSkuDatabase)
        return null;
    }

    /// <summary>Pre-load all SKU data at startup.</summary>
    public void LoadSkuDatabase(SkuData[] skus)
    {
        foreach (var sku in skus)
        {
            if (sku != null)
                _skuDataCache[sku.SkuId] = sku;
        }
        Debug.Log($"[InventoryService] Loaded {_skuDataCache.Count} SKUs.");
    }
}
