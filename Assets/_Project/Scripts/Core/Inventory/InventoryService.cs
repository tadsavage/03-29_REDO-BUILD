using GameCore.Services;
using GameCore.Events;
using GameCore.Economy;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace GameCore.Inventory
{
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
            //Debug.Log("[InventoryService] Initializing...");

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

            //Debug.Log("[InventoryService] Initialized.");
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

        /// <summary>
        /// Receive one pallet from an inbound shipment line item (CHUNK 1 — Inbound): assigns it a unique
        /// 10-digit Load ID and drops it in receiving staging (0,0), same placeholder location
        /// ReceiveShipment uses, awaiting a Putaway work task. Called by ReceivingService when a truck's
        /// dock timer completes.
        /// </summary>
        public PalletData ReceivePalletWithLoadId(string skuId, int quantity, int shelfLifeDays)
        {
            int currentDay = _timeService?.Day ?? 0;
            int expirationDay = shelfLifeDays >= 0 ? currentDay + shelfLifeDays : -1;
            var pallet = new PalletData(skuId, quantity, Vector2Int.zero, currentDay, expirationDay)
            {
                LoadId = GameCore.Labor.LoadIDGenerator.Generate()
            };
            _palletsByID[pallet.PalletId] = pallet;

            if (!_palletsByLocation.ContainsKey(pallet.CurrentLocation))
                _palletsByLocation[pallet.CurrentLocation] = new List<string>();
            _palletsByLocation[pallet.CurrentLocation].Add(pallet.PalletId);

            OnPalletReceived?.Invoke(pallet);
            Debug.Log($"[InventoryService] Received pallet {pallet.PalletId} (Load ID {pallet.LoadId}): {quantity} x {skuId}");
            return pallet;
        }

        /// <summary>
        /// Create a ledger record for a PHYSICAL pallet already sitting in the world at a grid cell
        /// (a placed Inventory-category object), so the inventory/addressing layer knows about it. Unlike
        /// ReceiveShipment (which drops pallets at the receiving placeholder), this records the pallet at
        /// its real cell immediately. Called by PalletInventoryTracker as pallets are placed/loaded.
        /// </summary>
        public PalletData RegisterPhysicalPallet(Vector2Int cell, string skuId, int quantity)
        {
            int currentDay = _timeService?.Day ?? 0;
            var pallet = new PalletData(skuId, quantity, cell, currentDay, -1);
            _palletsByID[pallet.PalletId] = pallet;
            if (!_palletsByLocation.ContainsKey(cell))
                _palletsByLocation[cell] = new List<string>();
            _palletsByLocation[cell].Add(pallet.PalletId);
            OnPalletReceived?.Invoke(pallet);
            return pallet;
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

        /// <summary>Look up a single pallet by id, or null if not tracked.</summary>
        public PalletData GetPallet(string palletId)
            => _palletsByID.TryGetValue(palletId, out var p) ? p : null;

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

        // ============ STAGING-LANE ADDRESSING ============
        // The geometry of lanes/slots lives in LaneNamingService (source of truth, derived from world
        // position every second). Here we just cross-reference a pallet's grid cell against that map to
        // answer "what spot is this pallet in?" and lane-level occupancy questions. A pallet is "in" a
        // slot when its CurrentLocation cell matches a lane tile's cell.

        /// <summary>The lane spot (e.g. door 1, lane A, slot 3) a pallet currently occupies, if any.</summary>
        public bool TryGetPalletSlot(string palletId, out LaneNamingService.LaneSlot slot)
        {
            slot = default;
            return _palletsByID.TryGetValue(palletId, out var pallet)
                   && LaneNamingService.TryGetSlot(pallet.CurrentLocation, out slot);
        }

        /// <summary>
        /// Full lane address of a pallet, including its stack tier: "1A-03-2" (door 1, lane A, slot 3,
        /// tier 2). Tier is 1-based from the bottom of the stack. Returns null if the pallet isn't in a
        /// lane. The spatial part ("1A-03") comes from lane geometry; the tier comes from this layer,
        /// which is the only thing that knows how many pallets are stacked in a cell.
        /// </summary>
        public string GetPalletAddress(string palletId)
        {
            if (!_palletsByID.TryGetValue(palletId, out var pallet)) return null;
            if (!LaneNamingService.TryGetSlot(pallet.CurrentLocation, out var slot)) return null;
            int tier = GetPalletTier(palletId);
            return tier > 0 ? $"{slot.Name}-{tier}" : slot.Name;
        }

        /// <summary>1-based stack tier of a pallet within its cell (1 = bottom, in putaway order). 0 if
        /// the pallet isn't tracked or isn't stored anywhere.</summary>
        public int GetPalletTier(string palletId)
        {
            if (!_palletsByID.TryGetValue(palletId, out var pallet)) return 0;
            if (_palletsByLocation.TryGetValue(pallet.CurrentLocation, out var ids))
            {
                int idx = ids.IndexOf(palletId);
                if (idx >= 0) return idx + 1;
            }
            return 0;
        }

        /// <summary>
        /// Put a pallet away into the next open slot of a lane — nearest the door first, stacking up to
        /// the lane's configured MaxStackHeight before advancing to the next slot. Returns the resulting
        /// full address ("1A-03-2") on success, or null if the pallet is unknown or the lane is full.
        /// This is the entry point a receiving/putaway task (or a manual test) calls to actually place a
        /// pallet in staging so GetPalletAddress starts returning a real spot for it.
        /// </summary>
        public string PutawayToLane(string palletId, int doorNumber, string lane)
        {
            if (!_palletsByID.ContainsKey(palletId))
            {
                Debug.LogWarning($"[InventoryService] Putaway failed: pallet {palletId} not found.");
                return null;
            }
            if (!LaneAcceptsPutaway(doorNumber, lane))
            {
                Debug.LogWarning($"[InventoryService] Putaway rejected: lane {doorNumber}{lane} is Shipping-only.");
                return null;
            }
            if (!TryGetNextFreeSlot(doorNumber, lane, out var slot))
            {
                Debug.LogWarning($"[InventoryService] Putaway failed: lane {doorNumber}{lane} is full or unknown.");
                return null;
            }
            MovePallet(palletId, slot.Cell);
            string address = GetPalletAddress(palletId);
            Debug.Log($"[InventoryService] Put pallet {palletId} away at {address}");
            return address;
        }

        /// <summary>All pallets currently sitting in a lane, ordered slot 1..N out from the door.</summary>
        public List<PalletData> GetPalletsInLane(int doorNumber, string lane)
        {
            var result = new List<PalletData>();
            foreach (var slot in LaneNamingService.GetLane(doorNumber, lane))
                foreach (var pallet in GetPalletsAtLocation(slot.Cell))
                    result.Add(pallet);
            return result;
        }

        /// <summary>
        /// Next slot in a lane that can still take a pallet — scanned from the door outward, and a slot
        /// counts as available until its stack reaches the lane's configured MaxStackHeight (so we fill a
        /// slot's full stack before moving to the next one). False if every slot is full.
        /// </summary>
        public bool TryGetNextFreeSlot(int doorNumber, string lane, out LaneNamingService.LaneSlot slot)
        {
            int maxHeight = LaneConfigRegistry.Get(doorNumber, lane).MaxStackHeight;
            foreach (var s in LaneNamingService.GetLane(doorNumber, lane))
            {
                int stacked = _palletsByLocation.TryGetValue(s.Cell, out var ids) ? ids.Count : 0;
                if (stacked < maxHeight) { slot = s; return true; }
            }
            slot = default;
            return false;
        }

        // ── Usage gating (Receiving/Shipping/Both) ───────────────────────────────
        // Receiving = Inbound (pallets arrive here → putaway only), Shipping = Outbound (pallets leave
        // here → picking only), Both = either. A lane never used yet defaults to Both.

        /// <summary>True if putaway may drop pallets into this lane (Receiving or Both).</summary>
        public bool LaneAcceptsPutaway(int doorNumber, string lane)
            => LaneConfigRegistry.Get(doorNumber, lane).Usage != LaneUsage.Outbound;

        /// <summary>True if pallets may be picked/loaded out of this lane (Shipping or Both).</summary>
        public bool LaneAllowsPicking(int doorNumber, string lane)
            => LaneConfigRegistry.Get(doorNumber, lane).Usage != LaneUsage.Inbound;

        // ── FIFO / LIFO picking ──────────────────────────────────────────────────

        /// <summary>
        /// Every pallet in a lane in PUTAWAY order: slot 1→N (door outward), then tier bottom→top within
        /// a slot. So element 0 is the first pallet ever placed in the lane and the last element is the
        /// most recently placed — which is exactly what FIFO/LIFO pick from opposite ends.
        /// </summary>
        private List<(LaneNamingService.LaneSlot slot, int tier, string palletId)> LaneContentsInPutawayOrder(int doorNumber, string lane)
        {
            var result = new List<(LaneNamingService.LaneSlot, int, string)>();
            foreach (var s in LaneNamingService.GetLane(doorNumber, lane))
            {
                if (!_palletsByLocation.TryGetValue(s.Cell, out var ids)) continue;
                for (int i = 0; i < ids.Count; i++)
                    if (_palletsByID.ContainsKey(ids[i]))
                        result.Add((s, i + 1, ids[i]));
            }
            return result;
        }

        /// <summary>
        /// The next pallet that would be picked from a lane, honoring its Usage (must allow picking) and
        /// its FIFO/LIFO order. False if the lane is Receiving-only or empty.
        /// </summary>
        public bool TryGetNextPick(int doorNumber, string lane, out PalletData pallet, out LaneNamingService.LaneSlot slot, out int tier)
        {
            pallet = null; slot = default; tier = 0;
            if (!LaneAllowsPicking(doorNumber, lane)) return false;

            var contents = LaneContentsInPutawayOrder(doorNumber, lane);
            if (contents.Count == 0) return false;

            var order = LaneConfigRegistry.Get(doorNumber, lane).Order;
            var chosen = order == LaneStackOrder.LIFO ? contents[contents.Count - 1] : contents[0];
            pallet = _palletsByID[chosen.palletId];
            slot = chosen.slot;
            tier = chosen.tier;
            return true;
        }

        /// <summary>
        /// Pick units from the lane's next pallet per its FIFO/LIFO order. Returns the pallet picked from
        /// (may now be empty/destroyed), or null if the lane can't be picked or is empty.
        /// </summary>
        public PalletData PickNextFromLane(int doorNumber, string lane, int quantity)
        {
            if (!TryGetNextPick(doorNumber, lane, out var pallet, out _, out _)) return null;
            PickFromPallet(pallet.PalletId, quantity);
            return pallet;
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
        }
    }
}
