using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameCore.Services;

/// <summary>
/// Drives a truck through a simple scripted yard route using direct Transform movement.
/// No NavMeshAgent — trucks follow a fixed set of waypoints.
///
/// Route (clean 4-point maneuver + guard gate):
///   Spawn → GateStop (guard inspection)
///   → Xform 1  GateEnterNoTurn        (drive through, NO stop)
///   → Xform 2  _drApproach-DepartPoint (drive straight, NO stop)
///   → Xform 3  _drBackup               (slight curve in, then STOP, wait 1.5s, NO mesh spin)
///   → reverse Bézier into the assigned door  (≈30° tractor/trailer jackknife)
///   → Docked (unload timer)
///   → pull out forward to Xform 2
///   → Xform 5  GateLeaveNoTurn         (drive through, NO stop)
///   → Exit point → shrink + destroy
///
/// Xform 2 and Xform 3 are SINGLE SHARED waypoints (named children of the guard
/// shack) used by every truck regardless of which door it's assigned. The door
/// (Xform 4) is the assigned DockSlot's DockPosition / DockRotation — the reverse
/// into the door is unchanged from before.
/// </summary>
public class TruckController : MonoBehaviour
{
    public enum TruckState
    {
        Idle,
        Queuing, GuardCheck,   // lining up at / holding the gate
        ToEnterNoTurn,     // → Xform 1
        ToApproach,        // → Xform 2
        ToBackup,          // → Xform 3 (slight curve)
        WaitingAtBackup,   // stop at Xform 3, 1.5s
        Reversing,         // Xform 3 → beginBackupTurn (curve)
        ReversingToDock,   // beginBackupTurn → dock (straight back-in)
        Docked,
        DepartToApproach,  // door → Xform 2
        ToLeaveNoTurn,     // Xform 2 → Xform 5
        ToExit,            // Xform 5 → Exit point
        Exiting
    }

    [Header("Driving")]
    [SerializeField] private float driveSpeed       = 2.5f;
    [SerializeField] private float driveTurnSpeed   = 180f;
    [SerializeField] private float arrivedThreshold = 0.5f;

    [Header("Forward Bézier curve (Xform 2 → 3 and depart)")]
    [Tooltip("0 = nearly straight, 1 = wide sweeping curve. 0.45 reads as a natural truck arc.")]
    [SerializeField] private float forwardDriveTension = 0.45f;

    [Header("Backup wait")]
    [Tooltip("Seconds the truck sits still at Xform 3 before it starts backing up.")]
    [SerializeField] private float backupWaitDuration = 1.5f;

    [Header("Reversing — Bézier curve into the door")]
    [SerializeField] private float reverseSpeed = 3f;
    [Tooltip("0 = nearly straight, 1 = wide sweeping curve. 0.45 is a natural truck arc. Raise it to widen the back-in turn toward the blue-line shape.")]
    [SerializeField] private float reverseArcTension = 0.45f;
    [Tooltip("Begin the final back-in curve this far BEFORE the truck root reaches beginBackupTurn (≈ half the truck length). Blends the straight reverse into the curve as one smooth S instead of a kink.")]
    [SerializeField] private float turnLeadDistance = 7f;

    [Header("Cab steering (tractor yaws at the hitch)")]
    [Tooltip("Turn the cab on its Y axis so it leads into curves, like a real tractor pivoting at the fifth wheel. Pure yaw — never touches X/Z.")]
    [SerializeField] private bool  articulateCab   = true;
    [Tooltip("Name of the cab child transform that pivots. Origin sits at the hitch, so it swings correctly.")]
    [SerializeField] private string cabChildName   = "Tractor";
    [Tooltip("Most the cab can crank away from the trailer body, in degrees. ~30° simulates the trailer turn while backing.")]
    [SerializeField] private float maxCabSteer     = 30f;
    [Tooltip("Maps how fast the body is turning (deg/sec) to cab steer angle while driving forward.")]
    [SerializeField] private float cabSteerGain    = 0.22f;
    [Tooltip("How quickly the cab swings toward its target steer angle, deg/sec.")]
    [SerializeField] private float cabSteerSlew    = 140f;

    [Header("Cab steering — reversing into dock")]
    [Tooltip("Cab steer gain used ONLY while backing into the dock. Higher keeps the tractor visibly cranked (~30°) while it tucks in.")]
    [SerializeField] private float reverseCabSteerGain = 1.6f;
    [Tooltip("Invert the cab crank direction while reversing — a real tractor steers opposite the trailer's swing when backing.")]
    [SerializeField] private bool  invertCabSteerWhenReversing = true;

    [Header("Unload & exit")]
    [SerializeField] private float unloadDuration = 7f;
    [Tooltip("If no dock stocker claims this docked truck within this many seconds, fall back to a bulk receive and depart so the dock doesn't wedge (e.g. no manned DS available).")]
    [SerializeField] private float offloadFallbackTimeout = 45f;
    [SerializeField] private float exitShrinkTime = 1.2f;

    [Header("Trailer Doors")]
    [SerializeField] private float doorOpenSpeed = 150f;
    [Tooltip("\"Barn door\" swing — real semi trailer doors open fully flat (≈180-185°) against the trailer sides so a docked dock-stocker can reach the product and it's visible from inside the warehouse.")]
    [SerializeField] private float driverDoorOpenAngle = 185f;
    [SerializeField] private float passengerDoorOpenAngle = -185f;

    [Header("Docked Ghosting")]
    [Tooltip("While docked, these renderers switch to the ghost/see-through wall material so you can see the product from inside the warehouse. Assign the trailer mesh + the left & right Savage decals.")]
    [SerializeField] private Renderer[] _ghostWhileDockedRenderers;

    [Header("Cargo (CHUNK 1 test visual)")]
    [Tooltip("Prefab instantiated 12x under Trailer/LorryTrailer/Load to represent loaded pallets — assign ChepStack.prefab (Assets/_Project/Prefabs/Inventory/ChepStack.prefab) or any prefab with a PalletBuilder component.")]
    [SerializeField] private GameObject palletVisualPrefab;
    [SerializeField] private Renderer[] _casesOriginalMaterial; // saved so the original material can be restored once pallets are received by a Receiver. If _casesGhostRenderers is assigned, this array is ignored.
    [Tooltip("Distance from trailer center to each row of 6 (left row at -offset, right row at +offset).")]
    [SerializeField] private float palletLateralOffset = 0.35f;
    [Tooltip("Spacing between the 6 pallets along the trailer's length, centered on the Load anchor.")]
    [SerializeField] private float palletRowSpacing = 0.35f;

    [Header("State (read-only in play)")]
    [SerializeField] private TruckState _state = TruckState.Idle;

    public GameCore.Inventory.ShipmentData AssignedShipment { get; private set; }

    // ── Offload (Chunk 2) handoff ────────────────────────────────────────────────
    private float _dockedTime;
    private bool  _offloadClaimed;
    private bool  _offloadComplete;

    // ── Outbound loading (D1) handoff — mirrors the offload flags above, but for a
    // truck that arrives EMPTY and gets pallets driven ONTO it instead of off of it. Kept as
    // separate fields (not reused) so TrailerLoadController and TrailerOffloadController can never
    // cross-claim each other's trucks even if a bug ever mixed up which list they scan.
    private bool _isOutbound;
    private bool _loadClaimed;
    private bool _loadComplete;

    /// <summary>Current state of the truck (for persistence and debugging).</summary>
    public TruckState CurrentState => _state;

    /// <summary>True while docked and still waiting for a dock stocker to start offloading it.</summary>
    public bool AwaitingOffload => !_isOutbound && _state == TruckState.Docked && !_offloadClaimed && !_offloadComplete;

    /// <summary>True while an outbound (empty-arriving) truck is docked and still waiting for a dock
    /// stocker to start loading it with staged pallets.</summary>
    public bool AwaitingLoad => _isOutbound && _state == TruckState.Docked && !_loadClaimed && !_loadComplete;

    /// <summary>True once this truck has been spawned for outbound pickup (arrives empty, gets loaded
    /// at the dock) rather than inbound delivery (arrives full, gets offloaded).</summary>
    public bool IsOutbound => _isOutbound;

    /// <summary>Marks this truck as outbound — must be called before it reaches Docked (normally right
    /// after AssignAndGo, by whichever spawn path is used for outbound pickups).</summary>
    public void SetOutbound() => _isOutbound = true;

    /// <summary>The dock this truck is currently backed into (null unless docked).</summary>
    public DockSlot DockedAt => _state == TruckState.Docked ? _dock : null;

    /// <summary>The Load container holding this truck's cargo pallets as children, or null.</summary>
    public Transform LoadContainer => transform.Find("Trailer/LorryTrailer/Load") ?? FindDeepChild(transform, "Load");

    /// <summary>Marks this truck as being actively offloaded so no other offloader claims it.</summary>
    public void ClaimForOffload() => _offloadClaimed = true;

    /// <summary>Called by the offload controller once every pallet is off — lets the truck depart.</summary>
    public void CompleteOffload() => _offloadComplete = true;

    /// <summary>Marks this outbound truck as being actively loaded so no other loader claims it.</summary>
    public void ClaimForLoad() => _loadClaimed = true;

    /// <summary>Called by the load controller once every staged pallet for this door is aboard —
    /// lets the truck depart.</summary>
    public void CompleteLoad() => _loadComplete = true;

    /// <summary>Resets the docked idle clock (same "reset timer slightly" pattern the inbound
    /// offload fallback already uses when pallets remain but no dock stocker is free yet) so
    /// offloadFallbackTimeout doesn't force this outbound truck to depart empty while it's still
    /// productively waiting — e.g. for its lane to reach the load-start pallet threshold.</summary>
    public void KeepDockAlive() => _dockedTime = Mathf.Min(_dockedTime, offloadFallbackTimeout - 5f);

    // ── Persistence read-only state ──────────────────────────────────────────────

    /// <summary>True if this truck is in any departure/exit state and should NOT be saved.</summary>
    public bool IsDeparting =>
        _state == TruckState.DepartToApproach ||
        _state == TruckState.ToLeaveNoTurn   ||
        _state == TruckState.ToExit          ||
        _state == TruckState.Exiting         ||
        // Idle only ever occurs mid-ShrinkAndDestroy (StartExiting/ToExit both set it right before
        // starting that coroutine) — it's always "about to be destroyed," never a resumable state.
        // A save landing in that exact window previously captured a state=Idle/door=-1/PO="" ghost
        // that got re-restored every load thereafter with nothing left to resume.
        _state == TruckState.Idle;

    /// <summary>The dock slot claimed by this truck regardless of current state (set at AssignAndGo time).</summary>
    public DockSlot AssignedDock => _dock;

    /// <summary>Seconds the truck has been in the Docked state (used by offload fall-back timer).</summary>
    public float DockedTime => _dockedTime;

    /// <summary>True if a dock stocker has claimed this truck for offloading.</summary>
    public bool OffloadClaimed => _offloadClaimed;

    /// <summary>True if the dock stocker has fully finished offloading the trailer.</summary>
    public bool OffloadComplete => _offloadComplete;

    /// <summary>True if the trailer barn doors are currently open.</summary>
    public bool DoorsOpen => _doorsOpen;

    // ── Persistence movement state (2026-07-15) ──────────────────────────────────
    // Exposed so departing trucks can resume their exact path on load.
    public Vector3 CurrentTarget => _currentTarget;
    public bool    UseBezier     => _useBezier;
    public Vector3 BzP0          => _bzP0;
    public Vector3 BzP1          => _bzP1;
    public Vector3 BzP2          => _bzP2;
    public Vector3 BzP3          => _bzP3;
    public float   BzT           => _bzT;
    public float   BzArcLen      => _bzArcLen;

    // ── Route waypoints (world positions, injected by TruckYardManager) ──────────
private DockSlot        _dock;
    private GuardController  _guard;
    private Vector3?         _gateStop;          // guard inspection
    private Vector3?         _gateEnterNoTurn;   // Xform 1
    private Vector3?         _gateLeaveNoTurn;   // Xform 5
    private Vector3?         _exitWaypoint;      // Exit point
    private System.Action    _onExited;
    // Xform 2 (_drApproach-DepartPoint) and Xform 3 (_drBackup) are computed
    // per-door from DockSlot offsets — see ApproachPoint() / BackupPoint().

    private float       _stateTimer;
    private float       _groundY;
    private Vector3     _currentTarget;
    private Transform   _driverDoor;
    private Transform   _passDoor;
    private bool        _doorsOpen;
    private bool        _useBezier;

    // Cab steering (articulated tractor)
    private Transform   _cab;
    private Quaternion  _cabRest;
    private float       _cabYaw;
    private float       _prevYaw;
    private bool        _cabInit;

    // Bézier segments (forward smoothing legs and the reverse into the door)
    private Vector3 _bzP0, _bzP1, _bzP2, _bzP3;
    private float   _bzT;
    private float   _bzArcLen;

    // Straight reverse (Xform 3 → beginBackupTurn) with gradual yaw
    private Vector3    _revStraightStart, _revStraightTarget;
    private float      _revStraightLen;
    private Quaternion _revFromRot, _revToRot;

    public TruckState State => _state;

    // ── Gate queue ───────────────────────────────────────────────────────────────
    private bool _isFront;       // this truck holds slot 0 (the gate) and may be inspected
    private bool _clearedGate;   // guard waved it through — it's heading into the yard

    /// <summary>True once the truck has passed the guard and left the gate queue.</summary>
    public bool HasClearedGate => _clearedGate;

    /// <summary>Fired once when the guard clears this truck and it leaves the queue.</summary>
    public event System.Action OnClearedGate;

    // Docked ghosting — the see-through material and each ghosted renderer's original materials,
    // so they can be restored on departure.
    private Material _ghostMaterial;

    // Ghost material for cargo CASES (GhostCases()) — uses the same GhostLoweredWall as docked trailer walls
    // so unreceived cases have a consistent see-through look.
    private Material _cargoGhostMaterial;
    private readonly Dictionary<Renderer, Material[]> _originalMaterials = new();

    private void Awake()
    {
        _groundY = transform.position.y;
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        _ghostMaterial = Resources.Load<Material>("Materials/GhostLoweredWall");
        if (_ghostMaterial == null)
            Debug.LogWarning("[TruckController] GhostLoweredWall material not found at Resources/Materials/GhostLoweredWall — docked trailer won't turn see-through.");

        _cargoGhostMaterial = Resources.Load<Material>("Materials/GhostLoweredWall");
        if (_cargoGhostMaterial == null)
            Debug.LogWarning("[TruckController] GhostLoweredWall material not found at Resources/Materials/GhostLoweredWall — unreceived cargo cases won't ghost correctly.");

        
        _driverDoor = FindDeepChild(transform, "TrailerDoor.Driver");
        if (_driverDoor == null) _driverDoor = FindDeepChild(transform, "TrailerDoor");

        _passDoor = FindDeepChild(transform, "TrailerDoor.Pass");
        if (_passDoor == null) _passDoor = FindDeepChild(transform, "TrailerDoor.001");

        _cab = FindDeepChild(transform, cabChildName);
        if (_cab != null) _cabRest = _cab.localRotation;
        else if (articulateCab)
            Debug.LogWarning($"[TruckController] Cab child '{cabChildName}' not found — cab steering disabled.");
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    // ── Setup ───────────────────────────────────────────────────────────────────
    public void Init(Vector3? gateStop, Vector3? gateEnterNoTurn, Vector3? gateLeaveNoTurn,
                     Vector3? exitWaypoint, GuardController guard, System.Action onExited)
    {
        _gateStop        = gateStop;
        _gateEnterNoTurn = gateEnterNoTurn;
        _gateLeaveNoTurn = gateLeaveNoTurn;
        _exitWaypoint    = exitWaypoint;
        _guard           = guard;
        _onExited        = onExited;
    }

    public void AssignAndGo(DockSlot dock)
    {
        if (dock == null || dock.IsOccupied)
        {
            Debug.LogWarning("[TruckController] Dock null or already occupied.");
            return;
        }
        _dock = dock;
        _dock.Claim();

        Vector3 firstTarget = _gateStop ?? ApproachPoint();

        // Snap facing before first Update — no visible first-frame spin.
        Vector3 dir = firstTarget - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir.normalized);

        if (_gateStop.HasValue)
        {
            // Wait in the gate queue. The yard manager assigns our slot via SetQueueSlot().
            _state = TruckState.Queuing;
            _currentTarget = transform.position;
            _useBezier = false;
        }
        else
            GuardClearedToEnter(); // no gate configured — skip straight into the yard
    }

    internal const int PalletSlotCount = 12;
    internal const int PalletsPerRow = 6;

    public void LoadShipment(GameCore.Inventory.ShipmentData shipment)
    {
        AssignedShipment = shipment;
        if (shipment == null) return;

        // Find the load area container. Try the known path first, then fall back to a recursive
        // search for a child literally named "Load" — the container lives inside the nested trailer
        // prefab, so the exact intermediate path ("Trailer/LorryTrailer/…") can drift if those
        // objects are ever renamed. The pallets MUST end up childed under this Load object (it's the
        // anchor the whole cargo layout is positioned relative to, and what the dock-stocker offload
        // will later reparent from).
        var loadParent = transform.Find("Trailer/LorryTrailer/Load") ?? FindDeepChild(transform, "Load");
        if (loadParent == null)
        {
            Debug.LogWarning($"[TruckController] No 'Load' container found on {name} (searched path and recursively). Cannot populate pallets.");
            return;
        }

        if (palletVisualPrefab == null)
        {
            Debug.LogWarning($"[TruckController] Pallet Visual Prefab not assigned on {name} — cannot show cargo. Assign one in the Inspector (e.g. Assets/_Project/Prefabs/Inventory/ChepStack.prefab).");
            return;
        }

        // Clear any pallets from a previous load (in case a truck is ever reused across shipments).
        for (int i = loadParent.childCount - 1; i >= 0; i--)
            Destroy(loadParent.GetChild(i).gameObject);

        if (shipment.LineItems.Count == 0)
        {
            Debug.LogWarning($"[TruckController] PO {shipment.PONumber} has no line items — trailer stays empty.");
            return;
        }

        var inventoryService = GameCore.Services.ServiceLocator.Get<GameCore.Inventory.InventoryService>();

        // Two placement modes. If every line item carries real floor-slot/tier metadata (the
        // RandomDeliveryGenerator path — one line item per physical pallet, tagged with which of
        // the 12 floor positions it sits at and whether it's tier 0 or stacked tier 1), place each
        // pallet exactly there, supporting double-stacking. Otherwise fall back to the legacy fixed
        // 12-slot round-robin cycle (older callers that just hand over undifferentiated line items).
        bool hasSlotMetadata = shipment.LineItems.Count > 0 && shipment.LineItems[0].FloorSlotIndex >= 0;

        int built = 0;
        if (hasSlotMetadata)
        {
            for (int i = 0; i < shipment.LineItems.Count; i++)
            {
                var item = shipment.LineItems[i];
                var sku = inventoryService?.GetSkuData(item.SkuId);
                if (BuildOnePallet(loadParent, sku, item.SkuId, item.FloorSlotIndex, item.PalletTier, i))
                    built++;
            }
        }
        else
        {
            for (int i = 0; i < PalletSlotCount; i++)
            {
                var item = shipment.LineItems[i % shipment.LineItems.Count];
                var sku = inventoryService?.GetSkuData(item.SkuId);
                if (BuildOnePallet(loadParent, sku, item.SkuId, i, 0, i))
                    built++;
            }
        }

        Debug.Log($"[TruckController] PO {shipment.PONumber} loaded. {built} pallet(s) populated with cargo.");
    }

    /// <summary>Instantiates and builds one cargo pallet at the given floor slot/tier. Returns
    /// true if it got real cargo (SkuData + case prefab), false if it was left as an empty base.</summary>
    private bool BuildOnePallet(Transform loadParent, GameCore.Inventory.SkuData sku, string skuId, int floorSlot, int tier, int uniqueIndex)
    {
        var instance = Instantiate(palletVisualPrefab);
        instance.transform.SetParent(loadParent);
        instance.transform.localPosition = SlotLocalPosition(floorSlot, tier, sku);
        instance.transform.localRotation = Quaternion.identity;
        instance.name = $"Pallet_{uniqueIndex:D2}_Slot{floorSlot}_Tier{tier}";

        // palletVisualPrefab is normally a build-menu placeable item (PlacedObject/BuildingData)
        // — as cargo it must not self-register in the world registry at (0,0), same reasoning
        // PalletBuilder.Build() already applies to the individual cases it spawns below.
        //
        // IMPORTANT (2026-07-09, pallet persistence rework): PlacedObject is DISABLED, not
        // destroyed. Disabling still unregisters it from PlacedObjectRegistry via OnDisable (same
        // effect as before) but keeps the component — and critically its `data` field (the
        // ObjDataSO identity, already baked into the prefab) — alive for the whole truck ride.
        // TrailerOffloadController.RegisterAndQueue re-enables it once the pallet has a real grid
        // cell, and PalletPersistenceService reads `data.id` at save time to know which prefab to
        // re-instantiate on load. Destroying it (the old behavior) lost that identity permanently,
        // which is part of why dock pallets could never rebuild correctly after a save/load.
        var po = instance.GetComponent<PlacedObject>();
        if (po != null) po.enabled = false;
        var bd = instance.GetComponent<BuildingData>();
        if (bd != null) Destroy(bd);

        var builder = instance.GetComponentInChildren<PalletBuilder>();
        if (builder == null)
        {
            Debug.LogWarning($"[TruckController] palletVisualPrefab has no PalletBuilder — slot {floorSlot} tier {tier} shows as an empty base.");
            return false;
        }

        if (sku == null || sku.Prefab == null)
        {
            Debug.LogWarning($"[TruckController] Missing SkuData or CasePrefab for SKU: {skuId} — slot {floorSlot} tier {tier} left empty.");
            return false;
        }

        builder.casePrefab = sku.Prefab;
        builder.linkedSku = sku; // CRITICAL: Link the SKU so PalletBuilder knows its real dimensions

        // Cargo pallets must reflect the SKU's real, PalletOptimizer-verified Ti/Hi (the master
        // record) — not PalletBuilder's own independent auto-layout guess — so the trailer
        // visually shows the same load the inventory/putaway systems believe is there. If a SKU
        // was never run through the optimizer (Ti/Hi still 0), fall back to PalletBuilder's own
        // height-based auto-layout rather than building an empty pallet.
        if (sku.Ti > 0 && sku.Hi > 0)
        {
            builder.useTiHiOverride = true;
            builder.manualTi = sku.Ti;
            builder.manualHi = sku.Hi;
        }
        builder.Build(deductMoney: false);

        // FIX FLOATING CASES: Right after building, reposition cases so they sit on the pallet deck,
        // not floating above it. Same fix applied in TrailerOffloadController.DropPallet() but we
        // apply it here too so cases are positioned correctly from the moment they're built in the trailer.
        var palletLoad = instance.transform.Find("PalletLoad");
        if (palletLoad != null)
        {
            const float palletDeckHeight = 0.165f;
            float minCaseY = float.MaxValue;
            var casesList = new System.Collections.Generic.List<Transform>();

            for (int i = 0; i < palletLoad.childCount; i++)
            {
                var child = palletLoad.GetChild(i);
                casesList.Add(child);
                if (child.localPosition.y < minCaseY)
                    minCaseY = child.localPosition.y;
            }

            // Shift all cases down so the lowest sits at pallet deck height
            if (casesList.Count > 0 && minCaseY != float.MaxValue)
            {
                float yOffset = minCaseY - palletDeckHeight;
                foreach (var caseTransform in casesList)
                {
                    var pos = caseTransform.localPosition;
                    pos.y -= yOffset;
                    caseTransform.localPosition = pos;
                }
            }

            // FIX CASE ORIENTATION: Zero out the default 90-degree Y rotation on PalletLoad
            // so cases align properly with the pallet direction.
            palletLoad.localRotation = Quaternion.identity;
        }

        // Cargo pallets are unreceived inventory — their CASES (not the pallet base) must read
        // as "ghosted" the moment they're built and stay that way through the dock-stocker
        // offload. PalletBuilder.GhostCases saves each pallet's original case material so
        // ReceiverReceivingWorkflow can restore it once a Receiver actually processes the pallet.
        if (_cargoGhostMaterial != null)
            builder.GhostCases(_cargoGhostMaterial);

        // CRITICAL FIX (2026-07-05): Each cargo pallet needs its own PalletData component with the
        // correct SKU so the hover tooltip shows the right item. Without this, all pallets resolve
        // to the same parent PalletData and show "stuck" on one item (e.g., "soy sauce" forever).
        var palletData = instance.GetComponent<GameCore.Inventory.PalletData>();
        if (palletData == null)
            palletData = instance.AddComponent<GameCore.Inventory.PalletData>();

        // Initialize PalletData with the cargo SKU info. LoadId is left empty — it will be assigned
        // when the pallet is actually received by a Receiver. CaseQuantity estimated from Ti x Hi.
        int estimatedCases = (sku.Ti > 0 && sku.Hi > 0) ? (sku.Ti * sku.Hi) : 0;
        var gameCtx = FindAnyObjectByType<GameContext>();
        int currentDay = gameCtx != null ? gameCtx.TimeService.Day : 0;
        int expirationDay = sku.ShelfLifeDays >= 0 ? currentDay + sku.ShelfLifeDays : -1;
        palletData.Initialize(
            loadId: "",  // Will be assigned during receiving
            itemNumber: skuId,
            caseQuantity: estimatedCases,
            expirationDay: expirationDay,
            area: sku.StorageArea,
            iconSprite: sku.Icon,
            location: Vector2Int.zero  // Will be set when pallet is actually placed in warehouse
        );

        // Add a trigger BoxCollider that encapsulates the entire pallet (pallet base + all cases).
        // Sized to match pallet dimensions: 48" (1.2192m) × 40" (1.016m) × dynamic height.
        // Height = pallet base (0.16m) + stacked cases (caseHeight × Hi) + small top padding.
        var collider = instance.AddComponent<BoxCollider>();
        float palletHeight = 0.16f + (sku.CaseHeight * sku.Hi) + 0.05f;  // +0.05m padding
        collider.size = new Vector3(1.2192f, palletHeight, 1.016f);  // (W, H, L)
        collider.center = new Vector3(0, palletHeight / 2f, 0);  // Center vertically on the pallet
        collider.isTrigger = true;  // Non-physics trigger for raycasts and collision detection

        // Add a kinematic Rigidbody for simple stacking physics.
        // Kinematic means: affected by gravity (pallets rest on each other naturally),
        // but not active physics simulation (no forces applied, cheap to run).
        // Can be switched to dynamic (isKinematic = false) later for full physics interaction.
        var rb = instance.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;  // Kinematic ignores gravity, but pallets will still rest on each other via collider stacking
        rb.constraints = RigidbodyConstraints.FreezeRotation;  // Prevent unwanted rotation

        return true;
    }

    /// <summary>2 rows of 6, equidistant along the trailer's length, centered on the Load anchor —
    /// left row at -palletLateralOffset, right row at +palletLateralOffset. Tier 1 (a pallet
    /// double-stacked on top of tier 0 at the same floor slot) sits at Y = sku.PltHeight, since
    /// tier 0 and tier 1 at a given slot are always the same SKU (RandomDeliveryGenerator never
    /// mixes SKUs within one stack) — tier 0's own total pallet height IS that offset.</summary>
    internal Vector3 SlotLocalPosition(int slotIndex, int tier, GameCore.Inventory.SkuData sku)
    {
        int row = slotIndex / PalletsPerRow;   // 0 = left, 1 = right
        int col = slotIndex % PalletsPerRow;   // 0..5 along the trailer length
        float x = row == 0 ? -palletLateralOffset : palletLateralOffset;
        float z = (col - (PalletsPerRow - 1) / 2f) * palletRowSpacing;

        // CRITICAL FIX: Calculate total loaded pallet height (pallet + cases), not just pallet deck height.
        // Tier 0 sits at Y=0. Tier 1+ is positioned at (tier * total_pallet_height).
        float y = 0f;
        if (tier > 0 && sku != null)
        {
            // Total pallet height = pallet deck (0.16m) + cases stacked on top
            // Cases height = Hi (layers) * (caseHeight + verticalGap) with one less gap
            const float palletDeckHeight = 0.16f;
            const float verticalGapBetweenLayers = 0.025f;

            // Case height comes from the SKU's case prefab dimensions
            float caseHeight = sku.CaseHeight;
            int numLayers = sku.Hi > 0 ? sku.Hi : 1;  // Default to 1 layer if not set

            // Calculate total case stack height: each layer is caseHeight tall, gaps between them
            float casesStackHeight = (numLayers * caseHeight) + ((numLayers - 1) * verticalGapBetweenLayers);

            // Total height = pallet + cases + small buffer to prevent clipping when stacking
            const float stackingBuffer = 0.01f;  // 1cm buffer between stacked pallets
            float totalLoadedHeight = palletDeckHeight + casesStackHeight + stackingBuffer;
            y = tier * totalLoadedHeight;
        }

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Manager-driven gate-queue slot. <paramref name="isFront"/> = this truck holds the
    /// gate (slot 0) and will be inspected once it arrives. Ignored once past the gate.
    /// </summary>
    public void SetQueueSlot(Vector3 slotPos, bool isFront)
    {
        if (_state != TruckState.Queuing) return;
        _currentTarget = slotPos;
        _isFront = isFront;
        _useBezier = false;
    }

    public void ForceDeparture()
    {
        if (_state == TruckState.Docked) BeginDeparture();
    }

    // ── Persistence API ───────────────────────────────────────────────────────────

    /// <summary>Assigns the shipment reference without spawning cargo — used by
    /// TruckPersistenceService to re-link a restored truck to its PO.</summary>
    public void SetShipment(GameCore.Inventory.ShipmentData shipment)
    {
        AssignedShipment = shipment;
    }

    /// <summary>
    /// Captures every pallet currently under the Load container into snapshots, recording
    /// the SKU, slot/tier position, and exact case transforms.  Called by
    /// TruckPersistenceService before the game is saved.
    /// </summary>
    public List<TrailerPalletSnapshot> CaptureTrailerPallets()
    {
        var result = new List<TrailerPalletSnapshot>();
        var loadParent = LoadContainer;
        if (loadParent == null) return result;

        for (int i = 0; i < loadParent.childCount; i++)
        {
            var pallet  = loadParent.GetChild(i);
            var snap    = new TrailerPalletSnapshot();

            ParseSlotAndTierFromName(pallet.name, out snap.floorSlot, out snap.palletTier);

            var pd      = pallet.GetComponent<GameCore.Inventory.PalletData>();
            var builder = pallet.GetComponentInChildren<PalletBuilder>();
            snap.skuId  = (pd != null && !string.IsNullOrEmpty(pd.ItemNumber)) ? pd.ItemNumber
                        : (builder?.linkedSku != null ? builder.linkedSku.SkuId : "");

            // Capture fallback SkuData fields for robust recovery
            if (builder != null && builder.linkedSku != null)
            {
                var sku = builder.linkedSku;
                snap.itemDescription = sku.ItemDescription;
                snap.caseLength = sku.CaseLength;
                snap.caseWidth = sku.CaseWidth;
                snap.caseHeight = sku.CaseHeight;
                snap.caseWeight = sku.CaseWeight;
                snap.buyValue = sku.BuyValue;
                snap.sellValue = sku.SellValue;
                snap.storageArea = (int)sku.StorageArea;
                snap.shelfLifeDays = sku.ShelfLifeDays;
            }

            if (builder != null)
            {
                snap.capturedTi = builder.manualTi;
                snap.capturedHi = builder.manualHi;
            }

            var palletLoad = pallet.Find("PalletLoad");
            if (palletLoad != null)
            {
                for (int j = 0; j < palletLoad.childCount; j++)
                {
                    snap.caseLocalPositions.Add(palletLoad.GetChild(j).localPosition);
                    snap.caseLocalRotations.Add(palletLoad.GetChild(j).localRotation);
                }
            }

            result.Add(snap);
        }
        return result;
    }

    /// <summary>
    /// Restores this truck from a save snapshot. Must be called AFTER <see cref="Init"/> has
    /// been called (so waypoints are wired up) and after the dock slot has been found.
    /// Sets world transform, claims the dock, rebuilds trailer cargo, applies all docked
    /// visual side-effects, then sets the state machine to the saved state.
    /// </summary>
    public void RestoreFromSnapshot(TruckSnapshot snap, DockSlot dock)
    {
        var restoredState = (TruckState)snap.truckState;
        Debug.Log($"[TruckController.RestoreFromSnapshot] START - state={restoredState}, doorsOpen={snap.doorsOpen}, offloadClaimed={snap.offloadClaimed}, offloadComplete={snap.offloadComplete}");

        // ── Dock assignment ────────────────────────────────────────────────────────
        if (dock != null && !dock.IsOccupied)
        {
            _dock = dock;
            _dock.Claim();
        }

        // ── Transform ─────────────────────────────────────────────────────────────
        _groundY = snap.worldPosition.y;
        transform.SetPositionAndRotation(snap.worldPosition, snap.worldRotation);

        // ── Offload flags ──────────────────────────────────────────────────────────
        // Reset docked time to 0 if the truck was previously claimed or if it's currently 
        // docked. This gives the offload controllers time to re-scan and re-claim the 
        // truck after a load, rather than immediately hitting the offloadFallbackTimeout.
        _dockedTime = 0f;
        
        // Never trust a saved "claimed" flag — the dock stocker coroutine that was driving the
        // offload does not survive a save/reload, and any pallet it was carrying got folded back
        // into this snapshot's trailerPallets specifically so the offload can restart cleanly.
        // Leaving this true would wedge the truck forever: TrailerOffloadController only scans for
        // AwaitingOffload trucks (Docked && !_offloadClaimed), and the fallback departure timer is
        // also gated on !_offloadClaimed — so a stuck "claimed" truck never gets un-stuck.
        _offloadClaimed  = false;
        Debug.Log($"[TruckController.RestoreFromSnapshot] Reset offloadClaimed: false (was {snap.offloadClaimed}) and reset dockedTime: 0 (was {snap.dockedTime})");
        
        // Also reset offloadComplete so the truck can resume offloading on load. If offloading was
        // already complete and the truck was waiting to depart, it will transition to Docked and wait
        // for the fallback timeout or a normal departure trigger, which is correct behavior.
        _offloadComplete = false;
        Debug.Log($"[TruckController.RestoreFromSnapshot] Reset offloadComplete: false (was {snap.offloadComplete})");

        // ── Restore movement state (2026-07-15) ──────────────────────────────────
        // Captured from departing or mid-maneuver trucks so they resume exactly 
        // where they left off.
        _currentTarget = snap.currentTarget;
        _useBezier     = snap.useBezier;
        _bzP0          = snap.bzP0;
        _bzP1          = snap.bzP1;
        _bzP2          = snap.bzP2;
        _bzP3          = snap.bzP3;
        _bzT           = snap.bzT;
        _bzArcLen      = snap.bzArcLen;

        // ── Rebuild trailer cargo ──────────────────────────────────────────────────
        if (snap.trailerPallets != null && snap.trailerPallets.Count > 0)
            RestoreTrailerPallets(snap.trailerPallets);

        // ── Per-state path re-initialisation ─────────────────────────────────────
        // Some states need path data that was computed on first entry (Bezier params,
        // straight-reverse start/target). Re-derive it from the restored transform so
        // the Update loop doesn't consume zero-initialised vectors.
        switch (restoredState)
        {
            case TruckState.Queuing:
                // The yard manager will call SetQueueSlot after this returns.
                _currentTarget = snap.worldPosition;
                _useBezier     = false;
                // Force clearedGate to false on restore so it follows the queue logic
                _clearedGate   = false;
                break;

            case TruckState.GuardCheck:
                // Guard state is transient and isn't worth re-entering on restore.
                // Treat it as already cleared — advance straight into the yard.
                GuardClearedToEnter();
                return;   // state already changed by GuardClearedToEnter

            case TruckState.ToEnterNoTurn:
                _currentTarget = _gateEnterNoTurn ?? ApproachPoint();
                _useBezier     = false;
                break;

            case TruckState.ToApproach:
                _currentTarget = ApproachPoint();
                _useBezier     = false;
                break;

            case TruckState.ToBackup:
                // Re-derive the Bezier from current position toward BackupPoint.
                if (_dock != null) BeginApproachToBackupCurve();
                return;   // state already set by BeginApproachToBackupCurve

            case TruckState.WaitingAtBackup:
                // Reset the wait timer to 0 — BeginStraightReverse runs next frame.
                _stateTimer    = 0f;
                _currentTarget = _dock != null ? BackupPoint() : snap.worldPosition;
                _useBezier     = false;
                break;

            case TruckState.Reversing:
                // Mid-straight-reverse: put back into WaitingAtBackup so the path is
                // cleanly re-derived from the current (restored) position.
                _stateTimer = 0f;
                _useBezier  = false;
                _state      = TruckState.WaitingAtBackup;
                return;

            case TruckState.ReversingToDock:
                // Snap to the dock — the truck was almost there anyway.
                if (_dock != null)
                {
                    transform.SetPositionAndRotation(_dock.DockPosition, _dock.DockRotation);
                    _groundY = _dock.DockPosition.y;
                }
                restoredState = TruckState.Docked;
                ApplyDockedSideEffects();
                break;

            case TruckState.Docked:
                ApplyDockedSideEffects();
                break;

            case TruckState.DepartToApproach:
            case TruckState.ToLeaveNoTurn:
            case TruckState.ToExit:
                // Resumes driving toward the currentTarget (or following the restored Bezier).
                break;

            case TruckState.Exiting:
            case TruckState.Idle:
                // If saved while already exiting/shrinking, just finish the cleanup.
                restoredState = TruckState.Idle;
                StartCoroutine(ShrinkAndDestroy(exitShrinkTime));
                break;
        }

        _state = restoredState;
        Debug.Log($"[TruckController.RestoreFromSnapshot] END - state={restoredState} door={snap.assignedDoorNumber} pallets={snap.trailerPallets?.Count ?? 0}");
        Debug.Log($"[TruckController.RestoreFromSnapshot] Final door state: _doorsOpen={_doorsOpen}, AwaitingOffload={AwaitingOffload}");
    }

    // Applies all visual/controller side-effects that OnDocked() normally sets up.
    // Called from RestoreFromSnapshot when restoring any Docked-equivalent state.
    private void ApplyDockedSideEffects()
    {
        OpenTrailerDoors();
        Debug.Log($"[TruckController.ApplyDockedSideEffects] Doors opened: _doorsOpen={_doorsOpen}");
        SetDockedGhost(true);
        _dock?.LightController?.SetOccupied(true);
        _dock?.SetDoorForcedOpen(true);
        _dockedTime = 0f;
    }

    /// <summary>
    /// Rebuilds the trailer's Load container from saved pallet snapshots.
    /// Inlines pallet construction (rather than delegating to BuildOnePallet) so we
    /// hold a direct reference to each new instance and can replace its PalletLoad with
    /// the exact saved case transforms without relying on child-index heuristics.
    /// </summary>
    private void RestoreTrailerPallets(List<TrailerPalletSnapshot> palletSnaps)
    {
        if (palletVisualPrefab == null)
        {
            Debug.LogWarning($"[TruckController] Cannot restore trailer pallets on '{name}' — palletVisualPrefab not assigned.");
            return;
        }

        // FIX: Explicitly find via the known path first to avoid finding stale/duplicate Load containers
        var loadParent = transform.Find("Trailer/LorryTrailer/Load");
        if (loadParent == null)
            loadParent = FindDeepChild(transform, "Load");

        if (loadParent == null)
        {
            Debug.LogWarning($"[TruckController] Cannot restore trailer pallets on '{name}' — Load container not found.");
            return;
        }

        // FIX: Clean up ALL Load containers in the trailer hierarchy to prevent orphaned duplicates.
        // This handles cases where nested prefabs or prefab misalignment left multiple Load objects.
        var allTrailers = transform.Find("Trailer");
        if (allTrailers != null)
        {
            foreach (Transform child in allTrailers)
            {
                if (child != null && child.name == "LorryTrailer")
                {
                    var allLoads = child.GetComponentsInChildren<Transform>();
                    foreach (var load in allLoads)
                    {
                        if (load != null && load.name == "Load" && load != loadParent)
                        {
                            Debug.LogWarning($"[TruckController] Found duplicate Load container — destroying it to prevent empty trailers.");
                            DestroyImmediate(load.gameObject);
                        }
                    }
                }
            }
        }

        // DestroyImmediate here so the children list is empty before we start adding new
        // ones — using Destroy (deferred) could leave old children in the hierarchy during
        // this method's execution, making child-count indexing unreliable.
        for (int i = loadParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(loadParent.GetChild(i).gameObject);

        Debug.Log($"[TruckController] Cleared Load container '{loadParent.name}' on {name}. Preparing to restore {palletSnaps.Count} pallet(s).");

        var inventoryService = GameCore.Services.ServiceLocator.Get<GameCore.Inventory.InventoryService>();
        var gameCtx = FindAnyObjectByType<GameContext>();
        int currentDay = gameCtx != null ? gameCtx.TimeService.Day : 0;

        for (int i = 0; i < palletSnaps.Count; i++)
        {
            var palletSnap = palletSnaps[i];
            if (palletSnap == null) continue;

            var sku = inventoryService?.GetSkuData(palletSnap.skuId);

            // ── Resolve Case Prefab ──────────────────────────────────────────
            // If SKU is missing or a "PHYS" dummy, fall back to Resources or the snapshot's embedded fields
            GameObject casePrefab = sku?.Prefab;
            if (casePrefab == null && !string.IsNullOrEmpty(palletSnap.skuId))
            {
                casePrefab = Resources.Load<GameObject>($"Inventory/Prefabs/Cases/Case_{palletSnap.skuId}");
            }

            // ── Instantiate pallet root ──────────────────────────────────────
            var instance = Instantiate(palletVisualPrefab);
            instance.transform.SetParent(loadParent);
            instance.transform.localPosition = SlotLocalPosition(palletSnap.floorSlot, palletSnap.palletTier, sku);
            instance.transform.localRotation = Quaternion.identity;
            instance.name = $"Pallet_{i:D2}_Slot{palletSnap.floorSlot}_Tier{palletSnap.palletTier}";

            // Cargo pallets must not register in the build-menu grid (same as BuildOnePallet).
            var po = instance.GetComponent<PlacedObject>();
            if (po != null) po.enabled = false;
            var bd = instance.GetComponent<BuildingData>();
            if (bd != null) Destroy(bd);

            // ── Rebuild PalletLoad ───────────────────────────────────────────
            var builder = instance.GetComponentInChildren<PalletBuilder>();

            bool hasSavedCases = palletSnap.caseLocalPositions != null &&
                                  palletSnap.caseLocalPositions.Count > 0 &&
                                  casePrefab != null;

            if (hasSavedCases)
            {
                // Remove any pre-existing PalletLoad (from the prefab's baked state or
                // an auto-build triggered by PalletBuilder.Start).
                var existingLoad = instance.transform.Find("PalletLoad");
                if (existingLoad != null) DestroyImmediate(existingLoad.gameObject);

                var loadObj = new GameObject("PalletLoad");
                loadObj.transform.SetParent(instance.transform, false);
                loadObj.transform.localPosition = Vector3.zero;
                loadObj.transform.localRotation = Quaternion.identity;

                int caseCount = Mathf.Min(palletSnap.caseLocalPositions.Count,
                                          palletSnap.caseLocalRotations.Count);
                for (int j = 0; j < caseCount; j++)
                {
                    var caseGO = Instantiate(casePrefab, loadObj.transform);
                    caseGO.transform.localPosition = palletSnap.caseLocalPositions[j];
                    caseGO.transform.localRotation = palletSnap.caseLocalRotations[j];

                    var casePo = caseGO.GetComponent<PlacedObject>();
                    if (casePo != null) { casePo.enabled = false; Destroy(casePo); }
                    var caseBd = caseGO.GetComponent<BuildingData>();
                    if (caseBd != null) Destroy(caseBd);
                }

                if (builder != null)
                {
                    builder.casePrefab = casePrefab;
                    builder.linkedSku  = sku;
                    if (palletSnap.capturedTi > 0 && palletSnap.capturedHi > 0)
                    {
                        builder.useTiHiOverride = true;
                        builder.manualTi = palletSnap.capturedTi;
                        builder.manualHi = palletSnap.capturedHi;
                    }
                }
            }
            else if (builder != null && sku != null)
            {
                // No exact case snapshots — fall back to a fresh Ti/Hi build.
                builder.casePrefab = sku.Prefab;
                builder.linkedSku  = sku;
                if ((palletSnap.capturedTi > 0 && palletSnap.capturedHi > 0) ||
                    (sku.Ti > 0 && sku.Hi > 0))
                {
                    builder.useTiHiOverride = true;
                    builder.manualTi = palletSnap.capturedTi > 0 ? palletSnap.capturedTi : sku.Ti;
                    builder.manualHi = palletSnap.capturedHi > 0 ? palletSnap.capturedHi : sku.Hi;
                }
                builder.Build(deductMoney: false);
            }

            // Ghost all cases — this is cargo on the trailer, not yet received.
            if (_cargoGhostMaterial != null && builder != null)
                builder.GhostCases(_cargoGhostMaterial);

            // ── PalletData component ─────────────────────────────────────────
            var palletData = instance.GetComponent<GameCore.Inventory.PalletData>();
            if (palletData == null)
                palletData = instance.AddComponent<GameCore.Inventory.PalletData>();

            int estimatedCases = (sku != null && sku.Ti > 0 && sku.Hi > 0) ? (sku.Ti * sku.Hi) : 0;
            int expirationDay  = sku != null && sku.ShelfLifeDays >= 0
                                 ? currentDay + sku.ShelfLifeDays : -1;
            palletData.Initialize(
                loadId:        "",
                itemNumber:    palletSnap.skuId,
                caseQuantity:  estimatedCases,
                expirationDay: expirationDay,
                area:          sku?.StorageArea ?? GameCore.Inventory.PalletData.AreaCategory.Grocery,
                iconSprite:    sku?.Icon,
                location:      Vector2Int.zero
            );

            // ── Physics components ───────────────────────────────────────────
            var col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            float palletHeight = sku != null ? 0.16f + (sku.CaseHeight * sku.Hi) + 0.05f : 0.5f;
            col.size   = new Vector3(1.2192f, palletHeight, 1.016f);
            col.center = new Vector3(0f, palletHeight / 2f, 0f);
            col.isTrigger = true;

            var rb = instance.GetComponent<Rigidbody>();
            if (rb == null) rb = instance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        Debug.Log($"[TruckController] Restored {palletSnaps.Count} trailer pallet(s) on '{name}'.");
    }

    // ── Shared name-parsing helper ────────────────────────────────────────────────
    private static void ParseSlotAndTierFromName(string name, out int slot, out int tier)
    {
        slot = 0; tier = 0;
        if (string.IsNullOrEmpty(name)) return;
        var parts = name.Split('_');
        foreach (var p in parts)
        {
            if (p.StartsWith("Slot", System.StringComparison.OrdinalIgnoreCase)
                && int.TryParse(p.Substring(4), out int s)) slot = s;
            if (p.StartsWith("Tier", System.StringComparison.OrdinalIgnoreCase)
                && int.TryParse(p.Substring(4), out int t)) tier = t;
        }
    }

    // ── Route points (computed per-door from DockSlot offsets) ───────────────────
    private Vector3 ApproachPoint() => _dock.ApproachDepartPoint;  // Xform 2
    private Vector3 BackupPoint()   => _dock.BackupPoint;          // Xform 3

    // ── Main loop ────────────────────────────────────────────────────────────────
    private void Update()
    {
        UpdateDoors();
        UpdateCabSteering();

        switch (_state)
        {
            case TruckState.Queuing:
                // Drive to our queue slot and idle. Only the front truck (slot 0)
                // triggers the guard inspection once it reaches the gate.
                if (DriveToward(_currentTarget) && _isFront) BeginGuardCheck();
                break;

            case TruckState.GuardCheck:
                // Waiting for GuardClearedToEnter() callback from GuardController.
                break;

            case TruckState.ToEnterNoTurn:
                // Xform 1 — roll through without stopping, straight on to Xform 2.
                if (DriveToward(_currentTarget))
                    SetTarget(TruckState.ToApproach, ApproachPoint());
                break;

            case TruckState.ToApproach:
                // Xform 2 — straight, no stop, then a rounded Bézier corner to Xform 3.
                if (DriveToward(_currentTarget))
                    BeginApproachToBackupCurve();
                break;

            case TruckState.ToBackup:
                // Xform 2 → Xform 3 (rounded corner). On arrival, face AWAY from the
                // door along the Xform 3 → beginBackupTurn line, then stop and wait.
                if (DriveToward(_currentTarget))
                {
                    Vector3 awayDir = BackupPoint() - _dock.BeginBackupTurnPoint;
                    awayDir.y = 0f;
                    if (awayDir.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.LookRotation(awayDir.normalized);

                    _state      = TruckState.WaitingAtBackup;
                    _stateTimer = backupWaitDuration;
                }
                break;

            case TruckState.WaitingAtBackup:
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f) BeginStraightReverse();
                break;

            case TruckState.Reversing:
                StepStraightReverse();
                break;

            case TruckState.ReversingToDock:
                StepFinalReverse();
                break;

            case TruckState.Docked:
                _dockedTime += Time.deltaTime;
                if (_isOutbound) { UpdateDockedOutbound(); break; }

                if (_offloadComplete)
                {
                    // Dock stocker finished pulling all pallets.
                    BeginDeparture();
                }
                else if (!_offloadClaimed && _dockedTime >= offloadFallbackTimeout)
                {
                    // Fallback departure: only happens if the truck is empty or the player
                    // hasn't assigned anyone to it for a long time.
                    // CRITICAL FIX: If there are still pallets on the trailer, we should
                    // NOT depart automatically just because of a timer (user request:
                    // "trailer should never depart until they're fully unloaded").
                    var container = LoadContainer;
                    if (container == null || container.childCount == 0)
                    {
                        BeginDeparture();
                    }
                    else
                    {
                        // Pallets remain — wait for a dock stocker. Reset timer slightly
                        // to prevent log spam or immediate re-check.
                        _dockedTime = offloadFallbackTimeout - 5f;
                    }
                }
                break;

            case TruckState.DepartToApproach:
                // Pull out forward to Xform 2, then on to Xform 5 without stopping.
                if (DriveToward(_currentTarget))
                {
                    if (_gateLeaveNoTurn.HasValue)
                    {
                        Vector3 afterGate = _exitWaypoint ?? _gateLeaveNoTurn.Value;
                        SetTargetCurved(TruckState.ToLeaveNoTurn, _gateLeaveNoTurn.Value,
                                        afterGate - _gateLeaveNoTurn.Value);
                    }
                    else
                        StartExiting();
                }
                break;

            case TruckState.ToLeaveNoTurn:
                // Xform 5 — roll through without stopping, on to the exit.
                if (DriveToward(_currentTarget)) StartExiting();
                break;

            case TruckState.ToExit:
                if (DriveToward(_currentTarget))
                {
                    _state = TruckState.Idle;
                    StartCoroutine(ShrinkAndDestroy(exitShrinkTime));
                }
                break;
        }
    }

    // ── Cab steering (articulated tractor) ────────────────────────────────────────
    private void UpdateCabSteering()
    {
        if (!articulateCab || _cab == null) return;

        float curYaw = transform.eulerAngles.y;

        if (!_cabInit)
        {
            _prevYaw = curYaw;
            _cabInit = true;
            return;
        }

        float dt      = Mathf.Max(Time.deltaTime, 1e-4f);
        float yawRate = Mathf.DeltaAngle(_prevYaw, curYaw) / dt;
        _prevYaw      = curYaw;

        bool  reversing = _state == TruckState.Reversing || _state == TruckState.ReversingToDock;
        float gain      = reversing ? reverseCabSteerGain : cabSteerGain;
        float sign      = (reversing && invertCabSteerWhenReversing) ? -1f : 1f;

        float steerTarget = Mathf.Clamp(yawRate * gain * sign, -maxCabSteer, maxCabSteer);
        _cabYaw           = Mathf.MoveTowards(_cabYaw, steerTarget, cabSteerSlew * dt);

        _cab.localRotation = Quaternion.Euler(0f, _cabYaw, 0f) * _cabRest;
    }

    // ── Forward movement (straight or Bézier) ─────────────────────────────────────
    private bool DriveToward(Vector3 target)
    {
        if (_useBezier)
        {
            _bzT += (driveSpeed / Mathf.Max(_bzArcLen, 0.1f)) * Time.deltaTime;

            if (_bzT >= 1f)
            {
                transform.position = _bzP3;
                _useBezier = false;
                return true;
            }

            Vector3 pos = EvalBezier(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
            pos.y = _groundY;
            transform.position = pos;

            Vector3 tangent = EvalBezierTangent(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 0.001f)
            {
                Quaternion desired = Quaternion.LookRotation(tangent.normalized);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, desired, driveTurnSpeed * 1.5f * Time.deltaTime);
            }
            return false;
        }

        Vector3 flat     = new Vector3(target.x, _groundY, target.z);
        Vector3 toTarget = flat - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude < arrivedThreshold)
        {
            transform.position = flat;
            return true;
        }

        Quaternion desiredLinear = Quaternion.LookRotation(toTarget.normalized);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, desiredLinear, driveTurnSpeed * Time.deltaTime);

        transform.position = Vector3.MoveTowards(
            transform.position, flat, driveSpeed * Time.deltaTime);

        return false;
    }

    private void SetupBezierForward(Vector3 endPos, Vector3 endForward, float tension)
    {
        _bzP0 = transform.position;
        _bzP3 = new Vector3(endPos.x, _groundY, endPos.z);

        float chord = Vector3.Distance(_bzP0, _bzP3);
        float t = chord * tension;

        _bzP1 = _bzP0 + transform.forward * t;
        _bzP2 = _bzP3 - endForward.normalized * t;

        _bzT = 0f;
        _bzArcLen = chord * 1.4f;
        _useBezier = true;
    }

    // ── Backup maneuver ───────────────────────────────────────────────────────────
    // Xform 2 → Xform 3: rounded Bézier corner, control at (Xform2.x, Xform3.z).
    private void BeginApproachToBackupCurve()
    {
        Vector3 x2     = ApproachPoint();
        Vector3 x3     = BackupPoint();
        Vector3 corner = new Vector3(x2.x, _groundY, x3.z);   // (ApproachDepart.x, Backup.z)

        _bzP0 = new Vector3(transform.position.x, _groundY, transform.position.z);
        _bzP1 = corner;
        _bzP2 = corner;   // both controls at the corner → a clean rounded right-angle
        _bzP3 = new Vector3(x3.x, _groundY, x3.z);
        _bzT  = 0f;
        _bzArcLen = (Vector3.Distance(_bzP0, corner) + Vector3.Distance(corner, _bzP3)) * 0.9f;
        _useBezier = true;

        _state         = TruckState.ToBackup;
        _currentTarget = _bzP3;
    }

    // Xform 3 → beginBackupTurn: STRAIGHT reverse path while slowly yawing the mesh
    // to the door-aligned heading (the door-relative "+X" target).
    private void BeginStraightReverse()
    {
        _revStraightStart  = new Vector3(transform.position.x, _groundY, transform.position.z);
        _revStraightTarget = new Vector3(_dock.BeginBackupTurnPoint.x, _groundY, _dock.BeginBackupTurnPoint.z);
        _revStraightLen    = Mathf.Max(0.1f, Vector3.Distance(_revStraightStart, _revStraightTarget));
        _revFromRot        = transform.rotation;
        _revToRot          = _dock.DockRotation;   // door-relative, aligned to back in
        _state             = TruckState.Reversing;
    }

    private void StepStraightReverse()
    {
        transform.position = Vector3.MoveTowards(transform.position, _revStraightTarget, reverseSpeed * Time.deltaTime);

        float t = Mathf.Clamp01(Vector3.Distance(_revStraightStart, transform.position) / _revStraightLen);
        transform.rotation = Quaternion.Slerp(_revFromRot, _revToRot, t);   // slow yaw, straight path

        // Blend into the final curve ~half-a-truck before reaching beginBackupTurn
        // so the straight reverse and the curve join as one smooth S (no kink). The
        // truck is only partly yawed here, leaving real angle for the curve to resolve.
        float lead = Mathf.Min(turnLeadDistance, _revStraightLen * 0.9f);
        if (Vector3.Distance(transform.position, _revStraightTarget) <= Mathf.Max(arrivedThreshold, lead))
            BeginFinalReverse();
    }

    // beginBackupTurn → dock: Bézier reverse into the door, ending at the dock
    // position (dockOffset standoff). The dock direction shapes the curve.
    private void BeginFinalReverse()
    {
        var p0   = new Vector3(transform.position.x, _groundY, transform.position.z);
        var dock = new Vector3(_dock.DockPosition.x, _groundY, _dock.DockPosition.z);

        var dockFwd = _dock.DockRotation * Vector3.forward; dockFwd.y = 0f; dockFwd.Normalize();
        var back    = -transform.forward; back.y = 0f; back.Normalize();

        float chord   = Vector3.Distance(p0, dock);
        float tension = chord * reverseArcTension;

        _bzP0 = p0;
        _bzP1 = p0   + back    * tension;
        _bzP2 = dock + dockFwd * tension;
        _bzP3 = dock;
        _bzT  = 0f;
        _bzArcLen = chord * 1.5f;

        _state = TruckState.ReversingToDock;
    }

    private void StepFinalReverse()
    {
        _bzT += (reverseSpeed / Mathf.Max(_bzArcLen, 0.1f)) * Time.deltaTime;

        if (_bzT >= 1f)
        {
            transform.position = _bzP3;
            transform.rotation = _dock.DockRotation;
            _state = TruckState.Docked;
            OnDocked();
            return;
        }

        Vector3 pos = EvalBezier(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
        pos.y = _groundY;
        transform.position = pos;

        // Mesh faces -tangent (cab points away from travel = reversing).
        Vector3 tangent = EvalBezierTangent(_bzP0, _bzP1, _bzP2, _bzP3, _bzT);
        tangent.y = 0f;
        if (tangent.sqrMagnitude > 0.001f)
        {
            Quaternion desired = Quaternion.LookRotation(-tangent.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, desired, 300f * Time.deltaTime);
        }
    }

    private static Vector3 EvalBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u*u*u*p0 + 3f*u*u*t*p1 + 3f*u*t*t*p2 + t*t*t*p3;
    }

    private static Vector3 EvalBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return 3f*u*u*(p1-p0) + 6f*u*t*(p2-p1) + 3f*t*t*(p3-p2);
    }

    // ── Phase transitions ─────────────────────────────────────────────────────────
    private void SetTarget(TruckState next, Vector3 destination)
    {
        _state         = next;
        _currentTarget = destination;
        _useBezier     = false;
    }

    private void SetTargetCurved(TruckState next, Vector3 destination, Vector3 endForward)
    {
        _state         = next;
        _currentTarget = destination;

        endForward.y = 0f;
        if (endForward.sqrMagnitude < 0.0001f)
        {
            _useBezier = false;
            return;
        }
        SetupBezierForward(destination, endForward.normalized, forwardDriveTension);
    }

    private void BeginGuardCheck()
    {
        _state = TruckState.GuardCheck;
        if (_guard != null)
            _guard.BeginInspection(this, GuardClearedToEnter);
        else
            GuardClearedToEnter();
    }

    public void GuardClearedToEnter()
    {
        // Leave the gate queue — the manager advances everyone behind us.
        _clearedGate = true;
        OnClearedGate?.Invoke();

        // Xform 1: roll through the gate without stopping (straight leg).
        if (_gateEnterNoTurn.HasValue)
            SetTarget(TruckState.ToEnterNoTurn, _gateEnterNoTurn.Value);
        else
            SetTarget(TruckState.ToApproach, ApproachPoint());
    }

    private void OnDocked()
    {
        _dock.LightController?.SetOccupied(true);

        // Reset offload/load handoff — the truck now waits in Docked until a dock stocker offloads
        // or loads it (TrailerOffloadController / TrailerLoadController) or the fallback timeout fires.
        _dockedTime      = 0f;
        _offloadClaimed  = false;
        _offloadComplete = false;
        _loadClaimed     = false;
        _loadComplete    = false;

        // Swing the barn doors fully open and ghost the trailer so the dock-stocker can reach the
        // product and it reads from inside the warehouse.
        OpenTrailerDoors();
        SetDockedGhost(true);

        // Hold the warehouse-side rollup door open for the whole docked duration — its own trigger
        // collider can't tell "truck still here" from "dock stocker/receiver just walked out", so it
        // was swinging shut mid-unload. See RollupDoorController.SetForcedOpen.
        _dock.SetDoorForcedOpen(true);

        Debug.Log($"[TruckController] {name} docked at door {_dock.DoorNumber}. Awaiting {(_isOutbound ? "load" : "offload")}.");
    }

    /// <summary>Outbound counterpart of the inbound Docked branch in Update(): waits for
    /// TrailerLoadController to load staged pallets and call CompleteLoad(), or falls back to
    /// departing anyway if unclaimed for too long (same self-preservation the inbound "no DS
    /// available" case has) — unlike inbound, an outbound trailer STARTS empty and fills up, so
    /// "still empty" is never a reason to leave early the way it is for offload.</summary>
    private void UpdateDockedOutbound()
    {
        if (_loadComplete)
        {
            BeginDeparture();
        }
        else if (!_loadClaimed && _dockedTime >= offloadFallbackTimeout)
        {
            BeginDeparture();
        }
    }

    private void BeginDeparture()
    {
        // Defensive null-guard: a truck should always have a claimed dock by the time it departs,
        // but an orphaned restore (dock lookup failed) previously left this null and threw here
        // every frame forever (the truck never actually left). Null-conditional so a bad restore
        // degrades to "truck leaves without releasing a dock" instead of an infinite NRE loop.
        _dock?.LightController?.SetOccupied(false);
        // Release the rollup door's forced-open hold — the truck's own exit through the trigger will
        // close it normally as it drives out.
        _dock?.SetDoorForcedOpen(false);
        _dock?.Release();

        // Mark shipment as Departed
        if (AssignedShipment != null)
            AssignedShipment.Status = GameCore.Inventory.ShipmentData.ShipmentStatus.Departed;

        // Solid trailer + closed doors again before it drives off.
        SetDockedGhost(false);
        CloseTrailerDoors();

        // Pull out forward to Xform 2, easing toward the exit direction.
        Vector3 ap   = ApproachPoint();
        Vector3 next = _gateLeaveNoTurn ?? _exitWaypoint ?? ap;
        SetTargetCurved(TruckState.DepartToApproach, ap, next - ap);
    }

    /// <summary>
    /// Swap the assigned trailer renderers to the ghost/see-through wall material while docked, and
    /// restore their originals on departure. Handles multi-slot renderers (every submesh slot is
    /// pointed at the ghost material) and is idempotent, so re-calling with the same state is a no-op.
    /// </summary>
    private void SetDockedGhost(bool ghosted)
    {
        if (_ghostWhileDockedRenderers == null) return;

        foreach (var r in _ghostWhileDockedRenderers)
        {
            if (r == null) continue;

            if (ghosted)
            {
                if (_ghostMaterial == null || _originalMaterials.ContainsKey(r)) continue;
                _originalMaterials[r] = r.sharedMaterials;

                var ghosts = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ghosts.Length; i++) ghosts[i] = _ghostMaterial;
                r.sharedMaterials = ghosts;
            }
            else if (_originalMaterials.TryGetValue(r, out var original))
            {
                r.sharedMaterials = original;
                _originalMaterials.Remove(r);
            }
        }
    }

    private void StartExiting()
    {
        if (_exitWaypoint.HasValue)
            SetTarget(TruckState.ToExit, _exitWaypoint.Value);
        else
        {
            _state = TruckState.Idle;
            StartCoroutine(ShrinkAndDestroy(exitShrinkTime));
        }
    }

    // ── Trailer doors ─────────────────────────────────────────────────────────────
    /// <summary>Passenger-side rear trailer door (the guard faces this during inspection).</summary>
    public Transform PassengerDoor => _passDoor;

    public void OpenTrailerDoors()  {
        Debug.Log($"[TruckController.OpenTrailerDoors] Opening doors");
        _doorsOpen = true;
    }
    public void CloseTrailerDoors() {
        Debug.Log($"[TruckController.CloseTrailerDoors] Closing doors");
        _doorsOpen = false;
    }

    private void UpdateDoors()
    {
        if (_driverDoor == null || _passDoor == null) return;

        float targetDriver = _doorsOpen ? driverDoorOpenAngle : 0f;
        float targetPass   = _doorsOpen ? passengerDoorOpenAngle : 0f;

        Quaternion drRot = Quaternion.Euler(0, targetDriver, 0);
        Quaternion paRot = Quaternion.Euler(0, targetPass, 0);

        _driverDoor.localRotation = Quaternion.RotateTowards(_driverDoor.localRotation, drRot, doorOpenSpeed * Time.deltaTime);
        _passDoor.localRotation   = Quaternion.RotateTowards(_passDoor.localRotation, paRot, doorOpenSpeed * Time.deltaTime);
    }

    // ── Teardown ──────────────────────────────────────────────────────────────────
    private void OnDestroy()
    {
        if (_dock != null)
        {
            _dock.Release();
            _dock = null;
        }
        if (_onExited != null)
        {
            _onExited.Invoke();
            _onExited = null;
        }
    }

    private IEnumerator ShrinkAndDestroy(float duration)
    {
        Vector3 startScale = transform.localScale;
        float   elapsed    = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / duration);
            yield return null;
        }
        _onExited?.Invoke();
        Destroy(gameObject);
    }
}
