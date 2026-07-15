using System.Collections.Generic;
using System.Linq;
using GameCore.Services;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Decoupled service that implements the putaway destination search algorithm.
    /// Registered with <see cref="ServiceLocator"/> in <see cref="GameContext.Awake"/>.
    ///
    /// Primary entry point: <see cref="AssignPutawayDestination"/> — called by the
    /// <c>ReachTruckOperator</c> the moment it picks up a pallet from staging. The TO location
    /// is intentionally left null on the WorkTask at creation time and is only resolved here.
    ///
    /// Search decision tree (full detail in domain spec / skill):
    ///   1. If SKU has no pick slot → limbo fallback (closest-to-dock Available reserve)
    ///   2. If pick slot is empty (qty &lt; 1 case) → fill pick slot directly
    ///   3. If pick slot is full → proximity search for an Available reserve:
    ///      adjacent slots → ±5 bays same side → opposite side same aisle →
    ///      full same aisle → adjacent aisle behind → aisle across → alternating outward
    ///   4. If no Available reserve found anywhere → limbo fallback
    ///
    /// <see cref="PutawayVariance"/> is the maximum allowable XY delta (in metres) between the
    /// pallet's pivot point and the target slot world position before the pallet may be released.
    /// Adjust via code or a future config ScriptableObject while the physical sequence is tuned.
    /// </summary>
    public class PutawayLogic : IService
    {
        // ============ CONFIGURATION ============

        private const float DefaultPutawayVariance = 0.1f;
        private const int AdjacencySearchRadius = 5;

        private float _putawayVariance = DefaultPutawayVariance;

        /// <summary>Maximum allowable XY delta (metres) for the extend-phase variance check.
        /// Clamped 0–0.25. Tune this value to tighten or relax the precision requirement
        /// on the physical putaway sequence.</summary>
        public float PutawayVariance
        {
            get => _putawayVariance;
            set => _putawayVariance = Mathf.Clamp(value, 0f, 0.25f);
        }

        // ============ DEPENDENCIES ============

        private InventoryService _inventoryService;

        // ============ LIFECYCLE ============

        public void Initialize()
        {
            ServiceLocator.TryGet<InventoryService>(out _inventoryService);
        }

        public void Shutdown() { }

        // ============ PRIMARY ENTRY POINT ============

        /// <summary>
        /// Resolves and reserves the TO location for a putaway task, called by
        /// <c>ReachTruckOperator</c> at the moment it physically picks up the pallet.
        /// Returns the assigned slot address (e.g. "01-02-A0"), or null if no destination
        /// could be found. On success the slot is immediately locked to
        /// <see cref="LocationStatus.Reserved"/> so no other RTO can target it.
        /// </summary>
        /// <param name="palletId">The pallet ID from the WorkTask.</param>
        /// <param name="rtoPickupPosition">XZ position of the RTO at pickup (used for limbo aisle proximity).</param>
        public string AssignPutawayDestination(string palletId, Vector2 rtoPickupPosition)
        {
            var pallet = _inventoryService?.GetPallet(palletId);
            if (pallet == null)
            {
                Debug.LogWarning($"[PutawayLogic] Pallet '{palletId}' not found in InventoryService.");
                return null;
            }

            string destination = ResolveDestination(pallet, rtoPickupPosition);

            if (!string.IsNullOrEmpty(destination))
            {
                LocationStatusRegistry.Reserve(destination);
                Debug.Log($"[PutawayLogic] Pallet '{palletId}' (SKU {pallet.SkuId}) → '{destination}' (Reserved).");
            }
            else
            {
                Debug.LogWarning($"[PutawayLogic] No available destination found for pallet '{palletId}' (SKU {pallet.SkuId}).");
            }

            return destination;
        }

        /// <summary>
        /// Call when a putaway completes successfully (pallet released at TO location).
        /// Transitions the slot from Reserved → Occupied and updates the pallet's inventory location.
        /// </summary>
        /// <param name="palletId">The pallet that was put away.</param>
        /// <param name="toAddress">The slot address that received the pallet.</param>
        /// <param name="toGridCell">The grid cell that corresponds to the TO address rack.</param>
        public void CompletePutaway(string palletId, string toAddress, Vector2Int toGridCell)
        {
            LocationStatusRegistry.MarkOccupied(toAddress);
            _inventoryService?.MovePallet(palletId, toGridCell);
            Debug.Log($"[PutawayLogic] Putaway complete: pallet '{palletId}' now at '{toAddress}' (Occupied).");
        }

        /// <summary>
        /// Call if a putaway task is cancelled before completion.
        /// Releases the Reserved slot back to Available.
        /// </summary>
        public void CancelPutaway(string toAddress)
        {
            if (!string.IsNullOrEmpty(toAddress))
            {
                LocationStatusRegistry.Release(toAddress);
                Debug.Log($"[PutawayLogic] Putaway cancelled — '{toAddress}' released back to Available.");
            }
        }

        // ============ RESOLUTION LOGIC ============

        private string ResolveDestination(PalletMasterRecord pallet, Vector2 rtoPickupPosition)
        {
            var pickSlotAddresses = SlotAssignmentService.GetSlotsForSku(pallet.SkuId);

            if (pickSlotAddresses.Count == 0)
            {
                Debug.Log($"[PutawayLogic] SKU '{pallet.SkuId}' has no pick slot assigned — using limbo fallback.");
                return FindLimboDestination(rtoPickupPosition);
            }

            foreach (var pickAddress in pickSlotAddresses)
            {
                if (!SlotRegistry.TryGet(pickAddress, out var pickSlot)) continue;

                if (IsPickSlotEmpty(pickSlot))
                {
                    Debug.Log($"[PutawayLogic] Pick slot '{pickAddress}' is empty — filling directly.");
                    return pickAddress;
                }

                string reserve = FindAvailableReserve(pickSlot, rtoPickupPosition);
                if (!string.IsNullOrEmpty(reserve))
                    return reserve;
            }

            Debug.Log($"[PutawayLogic] No available reserve found for SKU '{pallet.SkuId}' — using limbo fallback.");
            return FindLimboDestination(rtoPickupPosition);
        }

        // ============ PICK SLOT CHECK ============

        /// <summary>True if no pallet is recorded at the pick slot's rack grid cell (qty < 1 case).
        /// Also ensures the slot is Available (not already Reserved by another RTO).</summary>
        private bool IsPickSlotEmpty(SlotRegistry.Slot pickSlot)
        {
            if (pickSlot.Rack == null) return false;

            // If the slot is already Reserved or Occupied, it's not "empty" for a new putaway.
            if (!LocationStatusRegistry.IsAvailable(pickSlot.Address)) return false;

            var cell = new Vector2Int(pickSlot.Rack.gridX, pickSlot.Rack.gridY);
            var pallets = _inventoryService?.GetPalletsAtLocation(cell);
            return pallets == null || pallets.Count == 0;
        }

        // ============ RESERVE PROXIMITY SEARCH ============

        private string FindAvailableReserve(SlotRegistry.Slot pickSlot, Vector2 rtoPickupPosition)
        {
            // 1. Adjacent slots: above, left, right — same aisle and same side (Position), bay ±1
            string candidate = SearchReserveInRadius(pickSlot.Aisle, pickSlot.Position, pickSlot.Bay, 1);
            if (candidate != null) return candidate;

            // 2. ±5 bays from pick slot — same aisle side only
            candidate = SearchReserveInRadius(pickSlot.Aisle, pickSlot.Position, pickSlot.Bay, AdjacencySearchRadius);
            if (candidate != null) return candidate;

            // 3. Opposite side of same aisle
            int oppositePos = pickSlot.Position == 0 ? 1 : 0;
            candidate = SearchAllReserveInAisleSide(pickSlot.Aisle, oppositePos);
            if (candidate != null) return candidate;

            // 4. Continue down both directions of same aisle (entire aisle exhausted, same side first)
            candidate = SearchAllReserveInAisleSide(pickSlot.Aisle, pickSlot.Position);
            if (candidate != null) return candidate;

            // 5–7. Expand to adjacent and alternating aisles (behind → across → further behind → further across…)
            candidate = SearchAlternatingAisles(pickSlot.Aisle);
            if (candidate != null) return candidate;

            return null;
        }

        /// <summary>Searches Available reserve slots within a bay radius on a specific aisle side,
        /// ordered by distance from the bay origin. Also verifies no physical pallet exists via InventoryService.</summary>
        private string SearchReserveInRadius(int aisle, int position, int bayOrigin, int radius)
        {
            var candidates = SlotRegistry.ReserveSlots
                .Where(s => s.Aisle == aisle
                         && s.Position == position
                         && Mathf.Abs(s.Bay - bayOrigin) <= radius
                         && LocationStatusRegistry.IsAvailable(s.Address))
                .OrderBy(s => Mathf.Abs(s.Bay - bayOrigin));

            foreach (var s in candidates)
            {
                if (IsSlotPhysicallyEmpty(s)) return s.Address;
            }

            return null;
        }

        /// <summary>Returns the first Available reserve on a specific aisle+side, ordered by bay.
        /// Also verifies no physical pallet exists via InventoryService.</summary>
        private string SearchAllReserveInAisleSide(int aisle, int position)
        {
            var candidates = SlotRegistry.ReserveSlots
                .Where(s => s.Aisle == aisle
                         && s.Position == position
                         && LocationStatusRegistry.IsAvailable(s.Address))
                .OrderBy(s => s.Bay);

            foreach (var s in candidates)
            {
                if (IsSlotPhysicallyEmpty(s)) return s.Address;
            }

            return null;
        }

        /// <summary>True if the slot has no pallets registered in InventoryService.</summary>
        private bool IsSlotPhysicallyEmpty(SlotRegistry.Slot slot)
        {
            if (slot.Rack == null) return false;
            var cell = new Vector2Int(slot.Rack.gridX, slot.Rack.gridY);
            var pallets = _inventoryService?.GetPalletsAtLocation(cell);
            // RULE: Slot is empty if it has no pallets OR if the pallets it has are not at this slot's grid height.
            // But for now, we assume one pallet per rack cell (column) for simplicity or check counts.
            return pallets == null || pallets.Count == 0;
        }

        /// <summary>Searches aisles alternating outward from the pick slot's aisle
        /// (behind → across → further behind → further across…) until an Available reserve is found.</summary>
        private string SearchAlternatingAisles(int originAisle)
        {
            // Gather all unique aisle numbers, excluding the origin aisle (already exhausted above)
            var allAisles = SlotRegistry.ReserveSlots
                .Select(s => s.Aisle)
                .Distinct()
                .Where(a => a != originAisle)
                .OrderBy(a => Mathf.Abs(a - originAisle))
                .ToList();

            foreach (int aisle in allAisles)
            {
                // Try both sides
                for (int pos = 0; pos <= 1; pos++)
                {
                    string candidate = SearchAllReserveInAisleSide(aisle, pos);
                    if (candidate != null) return candidate;
                }
            }

            return null;
        }

        // ============ LANE ACCESSIBILITY ============

        /// <summary>
        /// Finds the most accessible pallet in a staging lane when approached from the exit side.
        /// <para>
        /// Slots are numbered 1..N with 1 nearest the dock and N nearest the exit (open floor).
        /// Scanning from slot N downward, the first slot that contains at least one pallet is
        /// returned — it is reachable without moving anything else out of the way.
        /// </para>
        /// <para>
        /// Returns <c>false</c> if <paramref name="fromLocation"/> is not a valid lane address,
        /// the lane has no tiles, or no pallets are present anywhere in the lane.
        /// </para>
        /// </summary>
        /// <param name="fromLocation">Lane slot address from the WorkTask (e.g. "1C-3").</param>
        /// <param name="bestSlot">The most accessible lane slot that contains a pallet.</param>
        /// <param name="worldPos">World-space centre of <paramref name="bestSlot"/>.</param>
        public bool TryFindAccessiblePickupSlot(
            string fromLocation,
            out LaneNamingService.LaneSlot bestSlot,
            out Vector3 worldPos)
        {
            bestSlot = default;
            worldPos = Vector3.zero;

            // fromLocation arrives as "STG1C-3" from ReceiverReceivingWorkflow; strip the prefix.
            string laneAddr = fromLocation?.StartsWith("STG", System.StringComparison.OrdinalIgnoreCase) == true
                ? fromLocation.Substring(3)
                : fromLocation;

            if (!LaneNamingService.TryParseLaneAddress(laneAddr,
                    out int doorNumber, out string laneLetter, out _))
                return false;

            var slots = LaneNamingService.GetLane(doorNumber, laneLetter);
            if (slots.Count == 0) return false;

            // Slots are ordered 1..N from door. The exit side is slot N (highest index).
            // Walk from exit inward; the first slot that has a pallet is the most accessible.
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                var slot    = slots[i];
                var pallets = _inventoryService?.GetPalletsAtLocation(slot.Cell);
                if (pallets == null || pallets.Count == 0) continue;

                if (!LaneNamingService.TryGetSlotWorldPos(slot.Cell, out worldPos)) continue;

                bestSlot = slot;
                return true;
            }

            return false;
        }

        // ============ LIMBO FALLBACK ============

        /// <summary>
        /// Assigns the pallet to the Available reserve closest to the dock, in the aisle
        /// nearest to the RTO's current pickup position. Used when no pick slot is assigned
        /// for the SKU, or when no Available reserve is found anywhere in the full search.
        /// </summary>
        private string FindLimboDestination(Vector2 rtoPickupPosition)
        {
            // Compute the nearest aisle by XZ proximity — the aisle whose racks are closest
            // to the RTO's current position is the most convenient carry path to the dock.
            var allReserves = SlotRegistry.ReserveSlots
                .Where(s => LocationStatusRegistry.IsAvailable(s.Address) && s.Rack != null && IsSlotPhysicallyEmpty(s))
                .ToList();

            if (allReserves.Count == 0)
            {
                Debug.LogWarning("[PutawayLogic] Limbo fallback: no Available reserve slots in the entire building.");
                return null;
            }

            // Order: nearest aisle to RTO, then closest to dock (lowest bay number)
            int nearestAisle = GetAisleNearestToPosition(rtoPickupPosition, allReserves);

            var candidate = allReserves
                .OrderBy(s => Mathf.Abs(s.Aisle - nearestAisle))
                .ThenBy(s => s.Bay)
                .First();

            Debug.Log($"[PutawayLogic] Limbo fallback destination: '{candidate.Address}' (aisle {candidate.Aisle}, bay {candidate.Bay}).");
            return candidate.Address;
        }

        /// <summary>Returns the aisle number whose racks are closest (XZ) to the given world XZ position.</summary>
        private int GetAisleNearestToPosition(Vector2 xzPosition, List<SlotRegistry.Slot> candidates)
        {
            int bestAisle = candidates[0].Aisle;
            float bestSqr = float.PositiveInfinity;

            foreach (var slot in candidates)
            {
                Vector3 rackPos = slot.Rack.transform.position;
                float dx = rackPos.x - xzPosition.x;
                float dz = rackPos.z - xzPosition.y;
                float sqr = dx * dx + dz * dz;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestAisle = slot.Aisle;
                }
            }

            return bestAisle;
        }
    }
}
