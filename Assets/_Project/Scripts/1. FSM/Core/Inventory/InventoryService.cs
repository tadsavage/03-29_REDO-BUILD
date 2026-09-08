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
    /// - OnPalletReceived(PalletMasterRecord) — new pallet added to inventory
    /// - OnPalletMoved(PalletMasterRecord, Vector2Int from, Vector2Int to) — pallet location changed
    /// - OnPalletPartialPicked(PalletMasterRecord, int quantityRemoved) — order pick reduced quantity
    /// - OnPalletDestroyed(PalletMasterRecord) — pallet removed from inventory (empty or contaminated)
    /// - OnSpoilageDetected(PalletMasterRecord) — pallet marked contaminated
    /// </summary>
    public class InventoryService : IService
    {
        private readonly Dictionary<string, PalletMasterRecord> _palletsByID = new();
        private readonly Dictionary<Vector2Int, List<string>> _palletsByLocation = new();
        private readonly Dictionary<string, SkuData> _skuDataCache = new();
        private EventManager _eventManager;
        private SimulationTimeService _timeService;

        // Events
        public static event System.Action<PalletMasterRecord> OnPalletReceived;
        public static event System.Action<PalletMasterRecord, Vector2Int, Vector2Int> OnPalletMoved;
        public static event System.Action<PalletMasterRecord, int> OnPalletPartialPicked;
        public static event System.Action<PalletMasterRecord> OnPalletDestroyed;
        public static event System.Action<PalletMasterRecord> OnSpoilageDetected;
        public static event System.Action OnInventoryRestored;

        // Debug/diagnostic
        public IReadOnlyDictionary<string, PalletMasterRecord> AllPallets => _palletsByID;
        public IReadOnlyDictionary<Vector2Int, List<string>> PalletsByLocation => _palletsByLocation;

        /// <summary>Every SKU currently loaded (see LoadSkuDatabase). Used by Slotting UI's SKU dropdowns.</summary>
        public IEnumerable<SkuData> AllSkus => _skuDataCache.Values;

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
        public List<PalletMasterRecord> ReceiveShipment(List<(string skuId, int quantity, int expirationDayOffset)> items)
        {
            var created = new List<PalletMasterRecord>();
            int currentDay = _timeService?.Day ?? 0;

            foreach (var (skuId, quantity, expirationOffset) in items)
            {
                int expirationDay = expirationOffset >= 0 ? currentDay + expirationOffset : -1;
                var pallet = new PalletMasterRecord(skuId, quantity, Vector2Int.zero, currentDay, expirationDay);
                _palletsByID[pallet.PalletId] = pallet;

                // Initially in receiving staging (0,0) — will be putaway by employee
                if (!_palletsByLocation.ContainsKey(pallet.CurrentLocation))
                    _palletsByLocation[pallet.CurrentLocation] = new List<string>();
                _palletsByLocation[pallet.CurrentLocation].Add(pallet.PalletId);

                // Set the world Y position based on stack height at receiving location
                pallet.WorldHeightY = CalculateWorldHeightForPalletAtLocation(pallet.CurrentLocation, pallet.PalletId);

                created.Add(pallet);
                OnPalletReceived?.Invoke(pallet);

                Debug.Log($"[InventoryService] Received pallet {pallet.PalletId}: {quantity} × {skuId} (Y={pallet.WorldHeightY:F2})");
            }

            return created;
        }

        /// <summary>
        /// Receive one pallet from an inbound shipment line item (CHUNK 1 — Inbound): assigns it a unique
        /// 10-digit Load ID and drops it in receiving staging (0,0), same placeholder location
        /// ReceiveShipment uses, awaiting a Putaway work task. Called by ReceivingService when a truck's
        /// dock timer completes.
        /// </summary>
        public PalletMasterRecord ReceivePalletWithLoadId(string skuId, int quantity, int shelfLifeDays)
        {
            int currentDay = _timeService?.Day ?? 0;
            int expirationDay = shelfLifeDays >= 0 ? currentDay + shelfLifeDays : -1;
            var pallet = new PalletMasterRecord(skuId, quantity, Vector2Int.zero, currentDay, expirationDay)
            {
                LoadId = GameCore.Labor.LoadIDGenerator.Generate()
            };
            _palletsByID[pallet.PalletId] = pallet;

            if (!_palletsByLocation.ContainsKey(pallet.CurrentLocation))
                _palletsByLocation[pallet.CurrentLocation] = new List<string>();
            _palletsByLocation[pallet.CurrentLocation].Add(pallet.PalletId);

            // Set the world Y position based on stack height at receiving location
            pallet.WorldHeightY = CalculateWorldHeightForPalletAtLocation(pallet.CurrentLocation, pallet.PalletId);

            OnPalletReceived?.Invoke(pallet);
            Debug.Log($"[InventoryService] Received pallet {pallet.PalletId} (Load ID {pallet.LoadId}): {quantity} x {skuId} (Y={pallet.WorldHeightY:F2})");
            return pallet;
        }

        /// <summary>
        /// Create a ledger record for a PHYSICAL pallet already sitting in the world at a grid cell
        /// (a placed Inventory-category object), so the inventory/addressing layer knows about it. Unlike
        /// ReceiveShipment (which drops pallets at the receiving placeholder), this records the pallet at
        /// its real cell immediately. Called by PalletInventoryTracker as pallets are placed/loaded.
        /// </summary>
        public PalletMasterRecord RegisterPhysicalPallet(Vector2Int cell, string skuId, int quantity)
        {
            int currentDay = _timeService?.Day ?? 0;
            var pallet = new PalletMasterRecord(skuId, quantity, cell, currentDay, -1);
            _palletsByID[pallet.PalletId] = pallet;
            if (!_palletsByLocation.ContainsKey(cell))
                _palletsByLocation[cell] = new List<string>();
            _palletsByLocation[cell].Add(pallet.PalletId);

            // Set the world Y position based on stack height at the cell
            pallet.WorldHeightY = CalculateWorldHeightForPalletAtLocation(cell, pallet.PalletId);

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

            // CRITICAL: Calculate and store the world Y position based on stack height
            // The pallet goes on top of any existing pallets at this location
            pallet.WorldHeightY = CalculateWorldHeightForPalletAtLocation(newLocation, palletId);

            OnPalletMoved?.Invoke(pallet, oldLocation, newLocation);
            Debug.Log($"[InventoryService] Moved pallet {palletId} from {oldLocation} to {newLocation} (Y={pallet.WorldHeightY:F2})");

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
        public PalletMasterRecord GetPallet(string palletId)
            => _palletsByID.TryGetValue(palletId, out var p) ? p : null;

        /// <summary>Get total units of a SKU across all locations.</summary>
        public int GetTotalUnitsBySku(string skuId)
        {
            return _palletsByID.Values
                .Where(p => p.SkuId == skuId && !p.IsContaminated)
                .Sum(p => p.Quantity);
        }

        /// <summary>Get all pallets at a specific location.</summary>
        public List<PalletMasterRecord> GetPalletsAtLocation(Vector2Int location)
        {
            if (!_palletsByLocation.TryGetValue(location, out var palletIds))
                return new List<PalletMasterRecord>();

            return palletIds
                .Where(id => _palletsByID.ContainsKey(id))
                .Select(id => _palletsByID[id])
                .ToList();
        }

        /// <summary>Get all pallets for a specific SKU.</summary>
        public List<PalletMasterRecord> GetPalletsBySku(string skuId)
        {
            return _palletsByID.Values
                .Where(p => p.SkuId == skuId && !p.IsContaminated)
                .OrderByDescending(p => p.Quantity) // Largest quantities first (for picking efficiency)
                .ToList();
        }

        /// <summary>Total on-hand units for a SKU across every non-contaminated pallet, wherever it's
        /// sitting. Used by the VENDORS tab's "Pot Scratch Items" count (out-of-stock detection).</summary>
        public int TotalOnHand(string skuId) => GetPalletsBySku(skuId).Sum(p => p.Quantity);

        /// <summary>Get all non-contaminated pallets at receiving staging (0,0).</summary>
        public List<PalletMasterRecord> GetReceivingPallets()
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
        public List<PalletMasterRecord> GetPalletsInLane(int doorNumber, string lane)
        {
            var result = new List<PalletMasterRecord>();
            foreach (var slot in LaneNamingService.GetLane(doorNumber, lane))
                foreach (var pallet in GetPalletsAtLocation(slot.Cell))
                    result.Add(pallet);
            return result;
        }

        /// <summary>
        /// Next slot in a lane that can still take a pallet — scanned from the door outward, and a slot
        /// counts as available until its stack reaches the lane's configured MaxStackHeight (so we fill a
        /// slot's full stack before moving to the next one). Also excludes any slot a live outbound
        /// staging pallet is already sitting on (see OutboundOccupiedCells) so two different orders can't
        /// be handed the same "free" slot and end up superimposed. False if every slot is full/occupied.
        /// </summary>
        public bool TryGetNextFreeSlot(int doorNumber, string lane, out LaneNamingService.LaneSlot slot)
        {
            int maxHeight = Mathf.Max(1, LaneConfigRegistry.Get(doorNumber, lane).MaxStackHeight);
            var laneSlots = LaneNamingService.GetLane(doorNumber, lane);
            var outboundByCell = OutboundPalletsByCell(laneSlots);

            foreach (var s in laneSlots)
            {
                if (OccupiedTiersAt(s.Cell, outboundByCell) < maxHeight) { slot = s; return true; }
            }
            slot = default;
            return false;
        }

        // ── Lane entry mutual exclusion ───────────────────────────────────────────────────────
        /// <summary>
        /// True while exactly one MHE is physically inserting a pallet into this lane — driving down
        /// it, lifting/lowering forks, and setting the pallet down.
        ///
        /// StagingDropBaseY measures whatever is PHYSICALLY standing in a cell (see its own doc); a
        /// pallet still riding forks is invisible to that measurement. Tier reservations
        /// (TryReserveStagingSlot) stop two deliveries from being handed the same CELL, but they don't
        /// stop the height race: if truck B reaches the lane mouth while truck A is still driving its
        /// pallet in, B's height read doesn't see A's pallet yet (still on forks) and both can compute
        /// the same drop Y — landing on top of each other. This lock makes physical insertion into one
        /// lane strictly serial, so by the time a second delivery is allowed in, the first one's pallet
        /// is already standing there to be measured correctly.
        /// </summary>
        private readonly HashSet<(int Door, string Lane)> _laneEntryLocks = new HashSet<(int, string)>();

        /// <summary>Claim exclusive physical entry to a lane. False if someone else already holds it —
        /// the caller should wait outside (poll) rather than proceed. Every successful call MUST be
        /// paired with ReleaseLaneEntry on every exit path, or the lane is blocked for the rest of the
        /// session.</summary>
        public bool TryEnterLaneForDelivery(int doorNumber, string lane) =>
            _laneEntryLocks.Add((doorNumber, lane));

        /// <summary>Hand the lane back once the pallet is down (or the delivery aborted). Safe to call
        /// for a lane that was never locked.</summary>
        public void ReleaseLaneEntry(int doorNumber, string lane) =>
            _laneEntryLocks.Remove((doorNumber, lane));

        // ── In-transit staging reservations ──────────────────────────────────────────────────
        /// <summary>
        /// Cells an MHE has committed to but has not reached yet.
        ///
        /// OutboundOccupiedCells only sees pallets that are already DROPPED and unparented — a pallet
        /// riding a set of forks is deliberately excluded, because it is not standing in the lane yet.
        /// That leaves a window the length of an entire drive between "which slot am I taking"
        /// (resolved before the truck sets off, see ReachTruckOperator.DeliverPalletToStagingLane) and
        /// "that slot now reads as taken" (only once the pallet is set down). Two deliveries that both
        /// resolve inside that window were handed the SAME slot and drove their pallets into each
        /// other — the second pallet clipping through the first instead of taking the next slot along.
        ///
        /// A rack destination never had this problem: ReachTruckOperator reserves the rack address
        /// before pickup and releases it on abort. This is that same contract for staging slots.
        /// </summary>
        private readonly Dictionary<Vector2Int, int> _stagingSlotReservations = new Dictionary<Vector2Int, int>();

        /// <summary>Books ONE TIER of a staging cell for a pallet on its way — a count, not a flag,
        /// because these lanes stack (LaneConfig.MaxStackHeight, 2 by default) and two trucks heading
        /// for the same cell is perfectly legal as long as they land on different tiers.
        /// Resolve-then-reserve needs no locking: coroutines only interleave at yield points and callers
        /// do both in one uninterrupted block. Every call MUST be paired with ReleaseStagingSlot on
        /// every exit path, or that tier is blocked for the rest of the session.</summary>
        public bool TryReserveStagingSlot(Vector2Int cell)
        {
            _stagingSlotReservations.TryGetValue(cell, out int n);
            _stagingSlotReservations[cell] = n + 1;
            return true;
        }

        /// <summary>Hand a tier back — on arrival (the dropped pallet takes over as the marker) or on
        /// abort. Safe to call for a cell that was never reserved.</summary>
        public void ReleaseStagingSlot(Vector2Int cell)
        {
            if (!_stagingSlotReservations.TryGetValue(cell, out int n)) return;
            if (n <= 1) _stagingSlotReservations.Remove(cell);
            else _stagingSlotReservations[cell] = n - 1;
        }

        /// <summary>How many pallets are currently en route to this cell.</summary>
        public int ReservedTiersAt(Vector2Int cell) =>
            _stagingSlotReservations.TryGetValue(cell, out int n) ? n : 0;

        /// <summary>True while at least one pallet is en route to this cell.</summary>
        public bool IsStagingSlotReserved(Vector2Int cell) => ReservedTiersAt(cell) > 0;

        /// <summary>Pallets already standing in, or on their way to, one staging cell — inbound stock,
        /// staged outbound freight and in-flight reservations together. This is the number that has to
        /// stay under the lane MaxStackHeight.</summary>
        private int OccupiedTiersAt(Vector2Int cell, Dictionary<Vector2Int, List<GameObject>> outboundByCell)
        {
            int inbound  = _palletsByLocation.TryGetValue(cell, out var ids) ? ids.Count : 0;
            int outbound = outboundByCell.TryGetValue(cell, out var list) ? list.Count : 0;
            return inbound + outbound + ReservedTiersAt(cell);
        }

        // A staged order's second pallet sits ~1.2m from its sibling along the lane's depth axis (see
        // OrderSelectionTaskDriver.PlacePalletsAtStagingSlot) — comfortably inside one cell (1.33m
        // apart) of the next slot over. This cutoff just excludes pallets that aren't at this lane at all.
        private const float OutboundClaimDistance = 2.5f;

        /// <summary>Maps every live, dropped (unparented — a pallet still riding a selector doesn't
        /// count) outbound staging pallet to whichever of this lane's slots it's physically nearest to.
        /// OutboundPalletBuilder pallets are WIP/staged objects positioned directly in the world — they
        /// are never added to _palletsByLocation, since they're not InventoryService-managed stock —
        /// so without this, TryGetNextFreeSlot can't see them and keeps handing out the same slot to
        /// every order staged into a lane.</summary>
        private static Dictionary<Vector2Int, List<GameObject>> OutboundPalletsByCell(
            List<LaneNamingService.LaneSlot> laneSlots)
        {
            var byCell = new Dictionary<Vector2Int, List<GameObject>>();
            if (laneSlots.Count == 0) return byCell;

            foreach (var pallet in Object.FindObjectsByType<OutboundPalletBuilder>(FindObjectsSortMode.None))
            {
                if (pallet == null || pallet.transform.parent != null) continue;

                Vector2Int nearestCell = default;
                float nearestSqrDist = float.MaxValue;
                foreach (var s in laneSlots)
                {
                    if (!LaneNamingService.TryGetSlotWorldPos(s.Cell, out var slotPos)) continue;
                    // PLANAR distance: a pallet on tier 2 sits a metre above the slot's own world
                    // position, and measuring in 3D would push it past OutboundClaimDistance and make
                    // the cell read as emptier than it is.
                    Vector3 d = pallet.transform.position - slotPos; d.y = 0f;
                    float sqrDist = d.sqrMagnitude;
                    if (sqrDist < nearestSqrDist) { nearestSqrDist = sqrDist; nearestCell = s.Cell; }
                }

                if (nearestSqrDist >= OutboundClaimDistance * OutboundClaimDistance) continue;
                if (!byCell.TryGetValue(nearestCell, out var list))
                    byCell[nearestCell] = list = new List<GameObject>();
                list.Add(pallet.gameObject);
            }
            return byCell;
        }

        /// <summary>Every staged outbound pallet standing in one cell. Callers that need a height
        /// measure the objects themselves — see TrailerOffloadController.StagingDropBaseY.</summary>
        public List<GameObject> GetOutboundPalletObjectsAt(int doorNumber, string lane, Vector2Int cell)
        {
            var laneSlots = LaneNamingService.GetLane(doorNumber, lane);
            var byCell = OutboundPalletsByCell(laneSlots);
            return byCell.TryGetValue(cell, out var list) ? list : new List<GameObject>();
        }

        /// <summary>Next open slot in one SPECIFIC outbound lane (door-outward, same scan
        /// TryGetNextFreeSlot uses for inbound putaway — slot 1 fills first, e.g. 2A-1 then 2A-2,
        /// 2A-3...) — used once an order has been released to a particular staging lane via the
        /// Work Queue panel. See OrderService.ReleaseOrdersToLane / OrderData.AssignedLane.</summary>
        public bool TryFindStagingSlotInLane(int doorNumber, string lane, out LaneNamingService.LaneSlot slot)
        {
            slot = default;
            if (string.IsNullOrEmpty(lane) || !LaneAllowsPicking(doorNumber, lane)) return false;
            // A lane holding received pallets belongs to putaway right now, so outbound must not stage
            // into it — the SAME rule StagingLaneAssignmentService.LanesInStage applies when offering
            // stages in the Work Queue dropdown. Without it the two disagreed: a lane could be assigned
            // to an order while empty, fill with inbound cargo before the selector finished picking,
            // and still be handed back here as the preferred lane (TryFindStagingSlotAtDoor tries
            // AssignedLane before falling back to LanesInStage). That walked the selector straight into
            // the lane the dock stocker was filling instead of overflowing to the next free lane.
            if (StagingLaneAssignmentService.LaneHasInboundStock(this, doorNumber, lane)) return false;
            return TryGetNextFreeSlot(doorNumber, lane, out slot);
        }

        /// <summary>
        /// Staging slot anywhere in this door's STAGE (its whole set of pickable lanes), with overflow.
        /// Tries <paramref name="preferredLane"/> first — the lane the order was released to — then
        /// walks the rest of the Stage in fill order (A, B, C…) until one has room, reporting which
        /// lane it landed in via <paramref name="lane"/>.
        ///
        /// This is what lets an order keep staging when its own lane fills up instead of the selector
        /// giving up and dumping pallets wherever it happened to be standing.
        /// </summary>
        public bool TryFindStagingSlotAtDoor(int doorNumber, string preferredLane,
                                             out string lane, out LaneNamingService.LaneSlot slot)
        {
            slot = default;
            lane = null;

            if (!string.IsNullOrEmpty(preferredLane) &&
                TryFindStagingSlotInLane(doorNumber, preferredLane, out slot))
            {
                lane = preferredLane;
                return true;
            }

            foreach (var candidate in StagingLaneAssignmentService.LanesInStage(this, doorNumber))
            {
                if (candidate == preferredLane) continue; // already tried
                if (!TryFindStagingSlotInLane(doorNumber, candidate, out slot)) continue;
                lane = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// How many more staged pallets this outbound lane can take. Same gates and same door-outward
        /// scan TryGetNextFreeSlot uses, counted across every slot instead of stopping at the first —
        /// one pallet per slot, since outbound stages single-high (see TrailerLoadController's
        /// OutboundStackTier) and OutboundOccupiedCells claims a whole cell per staged pallet.
        /// 0 if the lane can't be staged into at all.
        /// </summary>
        public int CountFreeStagingSlotsInLane(int doorNumber, string lane)
        {
            if (string.IsNullOrEmpty(lane) || !LaneAllowsPicking(doorNumber, lane)) return 0;
            if (StagingLaneAssignmentService.LaneHasInboundStock(this, doorNumber, lane)) return 0;

            int maxHeight = Mathf.Max(1, LaneConfigRegistry.Get(doorNumber, lane).MaxStackHeight);
            var laneSlots = LaneNamingService.GetLane(doorNumber, lane);
            var outboundByCell = OutboundPalletsByCell(laneSlots);

            // TIERS, not cells: 8 slots at MaxStackHeight 2 holds 16 pallets, and
            // TryFindStagingLaneForPallets compares this against a whole order pallet count.
            int free = 0;
            foreach (var s in laneSlots)
                free += Mathf.Max(0, maxHeight - OccupiedTiersAt(s.Cell, outboundByCell));
            return free;
        }

        /// <summary>
        /// The one lane at this door that will hold an ENTIRE order's staged pallets — preferred lane
        /// first, then the rest of the Stage in fill order, returning the first with room for all
        /// <paramref name="palletCount"/> of them.
        ///
        /// An order's pallets must never straddle two lanes. OrderData carries exactly one
        /// AssignedLane; it is what ReleaseOrdersToLoading files its Load task against and what
        /// TrailerLoadController scans to find pallets. Per-pallet overflow (TryFindStagingSlotAtDoor,
        /// called once per pallet) could put pallet 2 in a different lane than pallet 1, and only one
        /// of those lanes can be recorded — the pallet in the other one is then invisible to the
        /// loader. It stays in the lane after the truck departs, the order bills for it anyway, and
        /// because a Shipped order files no further tasks, nothing ever moves it again.
        ///
        /// Reserving the whole order's worth of space up front costs some lane density (a lane with 1
        /// slot left is skipped by a 2-pallet order) but keeps a customer's freight together, which is
        /// how it would really be staged.
        /// </summary>
        public bool TryFindStagingLaneForPallets(int doorNumber, string preferredLane, int palletCount,
                                                 out string lane)
        {
            lane = null;
            if (palletCount < 1) palletCount = 1;

            if (!string.IsNullOrEmpty(preferredLane) &&
                CountFreeStagingSlotsInLane(doorNumber, preferredLane) >= palletCount)
            {
                lane = preferredLane;
                return true;
            }

            foreach (var candidate in StagingLaneAssignmentService.LanesInStage(this, doorNumber))
            {
                if (candidate == preferredLane) continue; // already tried
                if (CountFreeStagingSlotsInLane(doorNumber, candidate) < palletCount) continue;
                lane = candidate;
                return true;
            }
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
        public bool TryGetNextPick(int doorNumber, string lane, out PalletMasterRecord pallet, out LaneNamingService.LaneSlot slot, out int tier)
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
        public PalletMasterRecord PickNextFromLane(int doorNumber, string lane, int quantity)
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

        // ============ PERSISTENCE (SAVE/LOAD) ============

        /// <summary>Get all pallets currently in inventory for saving.</summary>
        public List<PalletMasterRecord> GetAllPallets()
        {
            return _palletsByID.Values.ToList();
        }

        /// <summary>Get a pallet by its Load ID (the 10-digit identifier assigned at receiving).</summary>
        public PalletMasterRecord GetPalletByLoadId(string loadId)
        {
            if (string.IsNullOrEmpty(loadId)) return null;
            return _palletsByID.Values.FirstOrDefault(p => p.LoadId == loadId);
        }

        /// <summary>Clear all pallets (used before restoring from save).</summary>
        public void ClearAllPallets()
        {
            _palletsByID.Clear();
            _palletsByLocation.Clear();
        }

        /// <summary>
        /// Calculate the world Y position for a pallet at a given cell location.
        /// This accounts for stacking: the pallet sits on top of all other pallets already at this location.
        /// Base ground level is 1.15; each pallet height is added from its SKU's PltHeight.
        /// </summary>
        private float CalculateWorldHeightForPalletAtLocation(Vector2Int location, string palletId)
        {
            const float groundLevel = 1.15f;
            float stackHeight = groundLevel;

            // Sum the heights of all OTHER pallets at this location (not including the one being placed)
            if (_palletsByLocation.TryGetValue(location, out var palletIds))
            {
                foreach (var id in palletIds)
                {
                    if (id == palletId) continue; // Skip the pallet being placed

                    if (_palletsByID.TryGetValue(id, out var otherPallet))
                    {
                        var sku = GetSkuData(otherPallet.SkuId);
                        if (sku != null)
                        {
                            stackHeight += sku.PltHeight;
                        }
                        else
                        {
                            // Fallback: assume a standard pallet height if SKU data not found
                            stackHeight += 1.0f;
                            Debug.LogWarning($"[InventoryService] SKU {otherPallet.SkuId} not found, using default pallet height 1.0m");
                        }
                    }
                }
            }

            return stackHeight;
        }

        /// <summary>Register a pallet directly (used when restoring from save).</summary>
        public void RegisterPalletDirect(PalletMasterRecord pallet)
        {
            if (pallet == null) return;

            _palletsByID[pallet.PalletId] = pallet;
            if (!_palletsByLocation.ContainsKey(pallet.CurrentLocation))
                _palletsByLocation[pallet.CurrentLocation] = new List<string>();
            _palletsByLocation[pallet.CurrentLocation].Add(pallet.PalletId);
        }

        /// <summary>
        /// Recalculate WorldHeightY for all pallets based on their current stack order.
        /// Called after restore to ensure pallets are positioned correctly even if restored in different order.
        /// </summary>
        public void RecalculateAllPalletHeights()
        {
            foreach (var pallet in _palletsByID.Values)
            {
                pallet.WorldHeightY = CalculateWorldHeightForPalletAtLocation(pallet.CurrentLocation, pallet.PalletId);
            }
            Debug.Log($"[InventoryService] Recalculated heights for {_palletsByID.Count} pallets.");
        }
    }
}
