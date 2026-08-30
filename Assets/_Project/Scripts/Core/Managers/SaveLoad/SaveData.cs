using System.Collections.Generic;
using GameCore.Inventory;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public string saveName;
    public int money;
    public int spentToday;
    public CameraSaveData cameraData;
    public List<SavedObject> placedObjects = new();
    public List<DevSettingEntry> devSettings = new();
    public float toolsWindowX = 12f;   // panel-space position of the Tools Window
    public float toolsWindowY = 50f;
    public bool  guidanceLinesVisible = true;
    public bool  waypointsVisible     = true;
    public bool  hoverPopupEnabled    = true;

    public List<EmployeeRecord> employeeRecords = new();

    // Per-lane operational config (max stack, usage, FIFO/LIFO). Empty in older saves.
    public List<LaneConfigEntry> laneConfigs = new();

    // Standalone lane Zone name -> pseudo-door-id identities (ZoneRegistry). Empty in older saves
    // or if no zones have been created.
    public List<ZoneEntry> laneZones = new();

    // Pick-slot -> SKU assignments from the Slotting UI (SlotAssignmentService). Empty in older saves.
    public List<SlotAssignmentEntry> slotAssignments = new();

    // Reserved/Occupied status per rack slot (LocationStatusRegistry) — without this, every
    // completed putaway's "this slot is full" status (and any in-progress Reserved destination)
    // silently reset to Available on reload. Empty in older saves.
    public List<LocationStatusEntry> locationStatuses = new();

    // Player-defined shift templates from the Shift Manager UI (ShiftDefinitionRegistry). Empty in older saves.
    public List<ShiftDefinitionSnapshot> shiftDefinitions = new();

    // Pending inbound shipments (Purchase Orders) from ShipmentService. Empty in older saves.
    public List<ShipmentSnapshot> shipments = new();

    // Active customer orders from OrderService. Empty in older saves.
    public List<OrderSnapshot> orders = new();

    // Contracts the player has signed, from OrderArrivalService. Carries each one's last-generated
    // day, without which a load would either re-run a day's arrivals or silently skip one.
    // Empty in older saves.
    public List<ContractSnapshot> contracts = new();

    // Contract offers GENERATED at runtime rather than authored as assets — the daily Bulk Orders
    // board. ContractRegistry can only resolve authored assets, so without this the board empties on
    // every load and any signed-but-unshipped bulk order reloads pointing at nothing.
    // Empty in older saves.
    public List<GeneratedOfferSnapshot> generatedOffers = new();

    // Dock appointments (which customer holds which door in which two-hour block), from
    // DockScheduleService. Empty in older saves — those simply load with an empty schedule and
    // repopulate as new orders arrive.
    public List<DockAppointmentSnapshot> dockAppointments = new();

    /// <summary>Per-SKU market prices, their 7-day history, and today's spot deals.
    /// Empty in a save written before the market existed — MarketService.Import treats that as
    /// "no prices yet" and re-seeds from the SKU assets, so old saves come up with a fresh
    /// market rather than a catalogue priced at zero.</summary>
    public MarketSnapshot market = new();

    /// <summary>The player's standing in the trade. Score 0 in a save written before reputation
    /// existed, which is exactly right — that player hasn't earned anything yet.</summary>
    public ReputationSnapshot reputation = new();

    /// <summary>Live salvage offers AND their hidden contents. The contents must persist or a
    /// reload would reroll the trailer, turning a sight-unseen gamble into a save-scum.</summary>
    public BrokerSnapshot broker = new();

    // Former employees (terminated / resigned) — kept on file for rehire, union reinstatement,
    // and HR history. Populated from FormerEmployeeArchive. Absent in older saves (empty list).
    public List<EmployeeRecord> formerEmployees = new();

    // ── ECONOMY PERSISTENCE ─────────────────────────────────────────────────
    // Hourly cost tracking by GL_Line + fractional accumulator. Absent in older saves.
    public EconomySnapshot economy = new();

    // ── TIME PERSISTENCE ────────────────────────────────────────────────────
    // Current hour/minute/day + fractional time accumulator. Absent in older saves.
    public TimeSnapshot time = new();

    // ── WORK QUEUE PERSISTENCE ──────────────────────────────────────────────
    // Pending work tasks (Putaway, Replenish, OrderSelect, Load). Absent in older saves.
    public List<WorkTaskSnapshot> workQueue = new();

    // ── PALLET PERSISTENCE ──────────────────────────────────────────────────
    // All pallets with their locations, SKUs, quantities, and expiration. Absent in older saves.
    public List<PalletSnapshot> pallets = new();

    // ── DOCK PALLET VISUAL PERSISTENCE (2026-07-09) ─────────────────────────
    // Literal capture of every dock/lane pallet GameObject (ChepEmpty root + built cases): exact
    // world position/rotation and every case's exact local transform. Separate from `pallets`
    // above (which is InventoryService's data-only record — SKU/quantity/location bookkeeping,
    // no visuals). This is what PalletPersistenceService reads/writes; see that class for why the
    // old SKU/Ti-Hi-driven visual reconstruction (InventoryPersistenceService) was unreliable.
    public List<DockPalletSnapshot> dockPallets = new();

    // ── TRUCK YARD PERSISTENCE ───────────────────────────────────────────────
    // All trucks currently in the yard — their route state, world transform, assigned dock,
    // linked PO, offload progress, and the pallets still riding on the trailer. Absent in
    // older saves (empty list → yard starts empty, which was the old behaviour).
    public List<TruckSnapshot> trucks = new();

    // ── MHE OPERATOR CARRIED-PALLET PERSISTENCE ──────────────────────────────
    // Any pallet physically riding an MHE operator's forks/anchor at save time (mid-putaway on a
    // Reach Truck, or the rare late-stage Dock Stocker carry after the pallet already has an
    // InventoryService record). See MHEOperatorPersistenceService. Absent in older saves.
    public List<CarriedPalletSnapshot> carriedPallets = new();

    // ── Game settings captured per-save ──────────────────────────────────────
    // Sentinel defaults (-1 / empty) mean "not stored in this file" so that
    // loading an OLD save does not overwrite the player's current settings.
    public float  gameVolume      = -1f;   // SFX/game volume   (PlayerPrefs "GameVolume")
    public float  musicVolume     = -1f;   // music volume      (PlayerPrefs "MusicVolume")
    public string graphicsPreset  = "";    // "Ultra"/"Good"/"Toaster"
    public int    difficulty      = -1;    // 0 Clerk, 1 Supervisor, 2 Manager
    public int    resolutionIndex = -1;    // dropdown index (PlayerPrefs "ResolutionIndex")
    public int    screenWidth     = 0;     // actual width  applied via Screen.SetResolution
    public int    screenHeight    = 0;     // actual height applied via Screen.SetResolution
}

/// <summary>Key = "TypeName.fieldName", Val = serialized value string.</summary>
[System.Serializable]
public class DevSettingEntry
{
    public string key;
    public string val;
}

[System.Serializable]
public class CameraSaveData
{
    public Vector3 focusPoint;
    public float distance;
    public float pitch;
    public float yaw;
}

[System.Serializable]
public class SavedObject
{
    public int id;   // ObjDataSO ID
    public int x;    // Grid X
    public int y;    // Grid Y
    public int rot;  // Rotation index (0–3)
    public string customData;
    public float worldY;  // World Y (absolute height) — used for stacked pallets on dock

    // Absolute transform for mobile objects (vehicles)
    public bool hasTransform;
    public Vector3 pos;
    public Quaternion rotation;
}

[System.Serializable]
public class EconomySnapshot
{
    public List<EconomyGLLineEntry> hourlyByGLLine = new();     // Hourly costs per GL_Line
    public List<EconomyFractionalEntry> fractionalByGLLine = new(); // Fractional accumulators
}

[System.Serializable]
public class EconomyGLLineEntry
{
    public string glLine;
    public int hourlyAmount;
}

[System.Serializable]
public class EconomyFractionalEntry
{
    public string glLine;
    public float fractionalRemainder;
}

[System.Serializable]
public class TimeSnapshot
{
    public int hour = 8;           // 0-23 (default to morning)
    public int minute = 0;         // 0-59
    public int day = 1;            // 1+ (default to day 1)
    public float fractionalMinutes = 0f; // Sub-minute accumulator for precision
}

[System.Serializable]
public class WorkTaskSnapshot
{
    public string taskId;                    // GUID to preserve task identity
    public int type;                         // WorkTaskType as int enum
    public int requiredRole;                 // EmployeeRole as int enum
    public string palletId;
    public string description;
    public int status;                       // WorkTaskStatus as int enum
    public string assignedToEmployeeGuid;    // (nullable)
    public string fromLocation;
    public string toLocation;
    public int area;                         // PalletData.AreaCategory as int enum
    public int priority = GameCore.Labor.WorkTask.DefaultPriority; // older saves lack this key; field initializer covers it
    public string orderId;                   // OrderSelect/PalletPick tasks only; null for everything else (older saves lack this key, deserializes to null either way)
    public string skuId;                     // PalletPick tasks only — the SKU whose full pallet is wanted; null for everything else
}

[System.Serializable]
public class PalletSnapshot
{
    // Identity & Tracking
    public string palletId;                  // GUID (unique per pallet instance)
    public string loadId;                    // 10-digit "license plate" (master key)

    // Inventory Data
    public string skuId;                     // Product SKU
    public int quantity;                     // Case count
    public int receivedDayNumber;            // In-game day received
    public int expirationDayNumber;          // -1 if non-perishable
    public bool isContaminated;

    // **CRITICAL FOR XYZ**: Location coordinates
    public int locationX;                    // Grid cell X
    public int locationY;                    // Grid cell Y
    public float worldHeightY;               // World Y elevation (for stacking)

    // Staging lane (if pallet is on dock, e.g., "2A", "2B", null if in storage)
    public string stagingLaneId;             // Lane ID or null if in racks
}

/// <summary>
/// Literal world-transform capture of one dock/lane pallet GameObject, used by
/// PalletPersistenceService. See that class for the capture/restore logic.
/// </summary>
[System.Serializable]
public class DockPalletSnapshot
{
    // Identity: which ObjDataSO/prefab this pallet root actually is (e.g. "A Chep" id=1,
    // "StackPlts" id=64). Resolved via ObjDataRegistry, NOT Resources.Load — avoids the
    // ambiguous-duplicate-"ChepEmpty"-in-multiple-Resources-folders problem.
    public int objDataId = -1;

    // Exact world transform at save time. Restored VERBATIM (not re-derived from grid cell +
    // rotation index) so a pallet dropped at a non-grid-perfect angle still restores exactly.
    public Vector3 worldPosition;
    public Quaternion worldRotation;
    public float worldSpaceYHeight;

    // Optional link back to InventoryService's PalletMasterRecord (data-only bookkeeping — SKU,
    // quantity, expiration — lives in `SaveData.pallets`/PalletSnapshot, restored separately).
    // Empty string = this pallet isn't tracked by InventoryService (e.g. a decorative/manually
    // placed empty pallet via the build menu).
    public string inventoryPalletId = "";
    // Empty = still ghosted/unreceived (PalletMasterLink only). Non-empty = received/solid
    // (gets a real PalletData component on restore).
    public string loadId = "";

    // Cases built on this pallet (the "PalletLoad" child's children). All cases on one pallet
    // share the same prefab (PalletBuilder.casePrefab), so one objDataId covers every entry in
    // casePositions/caseRotations. -1 + empty lists = pallet has no cases (bare pallet).
    // caseObjDataId is often -1 because case prefabs usually AREN'T registered build-menu ObjData;
    // in that case restore falls back to the SKU's own case prefab, resolved via `skuId` below.
    public int caseObjDataId = -1;
    // SKU whose case prefab these cases were built from (SkuData.Prefab). Loaded at runtime from
    // Resources, so it's a reliable fallback for resolving the case prefab when caseObjDataId = -1.
    public string skuId = "";
    public List<Vector3> casePositions = new();
    public List<Quaternion> caseRotations = new();

    // Ground-truth Ti (cases per layer) / Hi (layer count), computed from the ACTUAL case layout
    // at capture time (PalletBuilder.ComputeTiHiFromLayout) — not read from manualTi/manualHi or
    // any SkuData master value, both of which can silently drift from what's physically on the
    // dock. Applied back onto the restored PalletBuilder's manualTi/manualHi on load so the
    // "Current" reading in the dev panel and the editable value can never disagree again.
    public int capturedTi = 0;
    public int capturedHi = 0;
}

/// <summary>
/// Serializable snapshot of one truck currently in the yard. Captures everything needed to
/// restore the truck at the exact state it was saved in — route position, assigned dock,
/// linked PO, offload progress, and all pallets still riding on the trailer.
/// </summary>
[System.Serializable]
public class TruckSnapshot
{
    /// <summary>PO number that links this truck back to a <see cref="ShipmentSnapshot"/> in
    /// <see cref="SaveData.shipments"/>. Empty if the truck has no assigned shipment.</summary>
    public string poNumber;

    /// <summary>Door number of the <see cref="DockSlot"/> claimed by this truck. -1 = not yet
    /// assigned (possible if truck is still queuing before it picks a free dock).</summary>
    public int assignedDoorNumber = -1;

    /// <summary><see cref="TruckController.TruckState"/> cast to int.</summary>
    public int truckState;

    /// <summary>Exact world position at save time.</summary>
    public Vector3 worldPosition;

    /// <summary>Exact world rotation at save time.</summary>
    public Quaternion worldRotation;

    /// <summary>How long (seconds) the truck has been in the Docked state. Used so the
    /// fall-back offload timer picks up from where it left off rather than resetting to zero.</summary>
    public float dockedTime;

    /// <summary>True if a dock stocker has claimed this truck for offloading.</summary>
    public bool offloadClaimed;

    /// <summary>True if the dock stocker has fully offloaded the trailer.</summary>
    public bool offloadComplete;

    /// <summary>True if the trailer barn doors were open at save time (docked trucks).</summary>
    public bool doorsOpen;

    /// <summary>Saved position index in the gate queue (0 = front). Only meaningful when
    /// <see cref="truckState"/> is Queuing or GuardCheck. Used to restore queue order.</summary>
    public int gateQueueIndex;

    /// <summary>Target position the truck is currently driving toward.</summary>
    public Vector3 currentTarget;

    /// <summary>True if the truck is currently mid-Bezier curve.</summary>
    public bool useBezier;

    /// <summary>Bezier control points and progress — used to resume curved maneuvers (corners, 
    /// reverse-to-dock, departures) pixel-perfectly on load.</summary>
    public Vector3 bzP0, bzP1, bzP2, bzP3;
    public float bzT;
    public float bzArcLen;

    /// <summary>Pallets still physically on the trailer at save time. Excludes any pallets
    /// that have already been offloaded to a staging lane (those are captured by
    /// PalletPersistenceService). Pallets that were on a dock stocker's forks at save time
    /// are also folded in here so they restore on the truck instead of being lost.</summary>
    public List<TrailerPalletSnapshot> trailerPallets = new();
}

/// <summary>
/// One pallet still on a truck's trailer at save time. Captures the slot/tier position within
/// the Load container (so it can be re-placed at the exact same local offset) plus the SKU
/// identity and every case's exact local transform (for pixel-perfect visual restoration).
/// </summary>
[System.Serializable]
public class TrailerPalletSnapshot
{
    /// <summary>SKU identifier — used to resolve <c>SkuData</c> (case prefab, dimensions,
    /// Ti/Hi) on restore.</summary>
    public string skuId;

    // Embedded SkuData fields for robust recovery (matches DockPalletSnapshot)
    public string itemDescription = "";
    public float  caseLength;
    public float  caseWidth;
    public float  caseHeight;
    public float  caseWeight;
    public float  buyValue;
    public float  sellValue;
    public int    storageArea;
    public int    shelfLifeDays = -1;

    /// <summary>Floor slot index (0-11) encoding which of the 12 positions in the trailer
    /// this pallet occupies. Matches the <c>SlotN</c> fragment in the pallet's GameObject
    /// name (e.g. "Pallet_02_Slot5_Tier0" → floorSlot=5).</summary>
    public int floorSlot;

    /// <summary>Vertical tier: 0 = floor level, 1 = stacked on top of the tier-0 pallet at
    /// the same slot. Matches the <c>TierN</c> fragment in the pallet's name.</summary>
    public int palletTier;

    /// <summary>Ground-truth Ti (cases-per-layer) captured from the actual built layout at
    /// save time. 0 = not yet computed / SKU never optimised.</summary>
    public int capturedTi;

    /// <summary>Ground-truth Hi (layer count) captured at save time. 0 = not yet computed.</summary>
    public int capturedHi;

    /// <summary>Local-space position of each case child under PalletLoad. Parallel array with
    /// <see cref="caseLocalRotations"/>.</summary>
    public List<Vector3> caseLocalPositions = new();

    /// <summary>Local-space rotation of each case child under PalletLoad.</summary>
    public List<Quaternion> caseLocalRotations = new();
}

/// <summary>
/// One pallet physically riding an MHE operator's forks/carry-anchor at save time — mid-putaway
/// on a Reach Truck (the common case: wide window, always has a real InventoryService record and
/// a WorkTask with a known destination) or, rarely, still on a Dock Stocker's forks after
/// <c>RegisterAndQueue</c> already gave it a record but before <c>DropPallet</c> unparented it.
/// See MHEOperatorPersistenceService for capture/restore. Pallets that are still raw, unreceived
/// trailer cargo (no InventoryService record yet) are NOT captured here — those are folded back
/// into the truck's own TrailerPalletSnapshot list by TruckPersistenceService instead.
/// </summary>
[System.Serializable]
public class CarriedPalletSnapshot
{
    /// <summary>GUID of the operator who was carrying this pallet — links it back to an
    /// EmployeeRecord (and, via that record's boardedVehicleGridX/Y, the vehicle) on restore.</summary>
    public string employeeGuid;

    /// <summary>Name of the vehicle-hierarchy child the pallet was parented to (e.g. "Forks" or
    /// "PalletAnchor") — restore re-parents onto the same-named child on the resumed vehicle.</summary>
    public string carrierAnchorName;

    /// <summary>Local position/rotation relative to the carrier anchor at save time.</summary>
    public Vector3 localPosition;
    public Quaternion localRotation;

    /// <summary>Which ObjDataSO/prefab this pallet root is — resolved via ObjDataRegistry, same
    /// pattern as DockPalletSnapshot.</summary>
    public int objDataId = -1;

    /// <summary>InventoryService PalletMasterRecord id (PalletMasterLink.PalletId). Always
    /// non-empty for a captured carried pallet — see class summary.</summary>
    public string inventoryPalletId = "";
    public string loadId = "";
    public string skuId = "";

    public int capturedTi;
    public int capturedHi;

    public List<Vector3> caseLocalPositions = new();
    public List<Quaternion> caseLocalRotations = new();
}
