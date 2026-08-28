using GameCore.Economy;
using GameCore.Services;
using GameCore.Labor;
using GameCore.Inventory;
using GameCore.Persistence;
using SaveLoadSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles placing objects into the world during gameplay
/// and spawning them when loading from a save file.
/// </summary>
public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private ObjDataRegistry registry;
    [SerializeField] private PlacementGrid grid;
    [SerializeField] private SaveLoadWindowController saveLoadWindowController;
    [SerializeField] private FreeLookCamera freeLookCamera;
    [SerializeField] private SaveLoadSystem.SaveThumbnailCapture thumbnailCapture;

    [Header("Employee")]
    [SerializeField] private List<EmployeeData> _employeeDataTemplates;

    private MoneyService moneyService;

    private float quicksaveCooldown = 1.0f;
    private float quicksaveTimer = 0f;
    private string lastSaveName = "quicksave";

    // Called by GameContext
    public void Initialize(MoneyService money)
    {
        moneyService = money;
    }
    private void Start()
    {
        if (freeLookCamera == null)
            freeLookCamera = Camera.main.GetComponent<FreeLookCamera>();

        // Subscribe to slot save/load events for toast + SFX
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveCompleted += OnSlotSaveCompleted;
            SaveManager.Instance.OnLoadCompleted += OnSlotLoadCompleted;
        }
    }
    private void Update()
    {
        if (quicksaveTimer > 0f)
            quicksaveTimer -= Time.deltaTime;

        // F5/F9 stand down while a text-entry modal owns the keyboard — quick-saving or, far worse,
        // quick-LOADING out from under the Save/Load dialog the player is mid-way through using is
        // never what they meant. F6 below is exempt: it toggles this very window, so it stays live as
        // a way to dismiss it.
        bool modalCapturing = UIModalGuard.IsCapturing;

        // Quicksave (F5)
        if (!modalCapturing && Keyboard.current.f5Key.wasPressedThisFrame && quicksaveTimer <= 0f)
        {
            if (SaveLoadSystem.SaveManager.Instance != null)
            {
                SaveLoadSystem.SaveManager.Instance.SaveToSlot(-1, "quicksave");
                quicksaveTimer = quicksaveCooldown;
            }
            else
            {
                SaveGame("quicksave");
                quicksaveTimer = quicksaveCooldown;
                AudioManager.Play("UI_Save");
                UIToast.Show("Quick-save successful");
            }
        }

        // Quickload (F9)
        if (!modalCapturing && Keyboard.current.f9Key.wasPressedThisFrame && quicksaveTimer <= 0f)
        {
            bool usedSaveManager = SaveLoadSystem.SaveManager.Instance != null;
            QuickLoad();
            quicksaveTimer = quicksaveCooldown;
            if (!usedSaveManager)
            {
                AudioManager.Play("UI_Load");
                UIToast.Show("Quick-load successful");
            }
        }

        // Save/Load window (F6)
        if (Keyboard.current.f6Key.wasPressedThisFrame)
        {
            if (saveLoadWindowController.IsOpen)
                saveLoadWindowController.Close();
            else
                saveLoadWindowController.Open(SaveLoadMode.Save);
        }
    }

    // ---------------------------------------------------------
    // NORMAL GAMEPLAY PLACEMENT
    // ---------------------------------------------------------
    private Transform _objectsContainer;

    private void EnsureContainer()
    {
        if (_objectsContainer == null)
        {
            var go = GameObject.Find("PlacedObjectsContainer");
            if (go == null) 
            {
                go = new GameObject("PlacedObjectsContainer");
                // Massive performance win for Editor: hide the container from hierarchy
                // to prevent the Hierarchy window from trying to render/sort 3,000+ items.
                go.hideFlags = HideFlags.HideInHierarchy;
            }
            _objectsContainer = go.transform;
        }
    }

    private EmployeeData FindEmployeeTemplate(ObjDataSO so)
    {
        if (_employeeDataTemplates == null || _employeeDataTemplates.Count == 0)
            return null;

        string objNameNormalized = so.name.ToLowerInvariant().Replace(" ", "").Replace("_", "");
        foreach (var template in _employeeDataTemplates)
        {
            if (template == null) continue;
            string templateNameNormalized = template.name.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            if (templateNameNormalized.Contains(objNameNormalized) || objNameNormalized.Contains(templateNameNormalized))
                return template;
        }
        return null;
    }

    private void EnsureEmployeeComponents(GameObject go, ObjDataSO so)
    {
        if (so == null || go == null) return;

        // Only add employee components to staff/worker types
        bool isEmployeeCategory = so.category == "Staff" || so.category == "Worker";
        if (!isEmployeeCategory) return;

        // ── Collider (EmployeeClickHandler requires it at runtime) ──
        if (go.GetComponent<Collider>() == null)
        {
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.5f, 0f);
            capsule.radius = 0.3f;
            capsule.height = 1.5f;
        }

        // ── EmployeeIdentity ──
        EmployeeIdentity identity = go.GetComponent<EmployeeIdentity>();
        EmployeeData template = FindEmployeeTemplate(so);

        if (identity == null)
        {
            identity = go.AddComponent<EmployeeIdentity>();
        }

        // Set template data if found and identity doesn't already have one assigned
        if (template != null && identity.Record == null)
        {
            identity.SetEmployeeData(template);
        }

        // ── EmployeeClickHandler ──
        if (go.GetComponent<EmployeeClickHandler>() == null)
        {
            go.AddComponent<EmployeeClickHandler>();
        }
    }

    public PlacedObject PlaceObject(ObjDataSO so, int x, int y, int rot)
    {
        EnsureContainer();
        Vector2Int cell = new Vector2Int(x, y);
        Vector3 worldPos = grid.GetCellCenter(cell);

        // Mobile agents need visual surface correction (e.g. sit on foundation meshes)
        bool hasAgent = so.prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;
        if (hasAgent)
        {
            worldPos.y = PlacementFinalizer.GetFloorTopY(grid, cell);
            if (so.worldYOffset != 0)
                worldPos.y += so.worldYOffset;
        }

        GameObject go = Instantiate(so.prefab, worldPos,
                                    Quaternion.Euler(0f, rot * 90f, 0f),
                                    hasAgent ? null : _objectsContainer);

        PlacedObject po = go.GetComponent<PlacedObject>();
        if (po == null)
        {
            Debug.LogError($"[PlacementSystem] Prefab '{so.name}' is missing a PlacedObject component. Destroying instance.");
            Destroy(go);
            return null;
        }
        po.Initialize(so, x, y, rot);
        go.GetComponent<MHEOperatorSlot>()?.NotifyPlaced();

        EnsureEmployeeComponents(go, so);

        PlacedObjectRegistry.Register(po);
        grid.AddStackObject(cell, go, so);

        return po;
    }

    // ---------------------------------------------------------
    // QUICKSAVE (F5) — writes via your existing SaveSystem class
    // ---------------------------------------------------------
    public void SaveGame(string saveName)
    {
        lastSaveName = saveName;
        SaveData save = BuildSaveData(saveName);
        SaveSystem.Save(save);
    }

    // ---------------------------------------------------------
    // QUICKLOAD (F9) — reads via your existing SaveSystem class
    // ---------------------------------------------------------
    public void LoadGame()
    {
        //Debug.Log($"Attempting to load save: {lastSaveName}");
        SaveData save = SaveSystem.Load(lastSaveName);
        if (save == null)
        {
            Debug.LogWarning($"LoadGame: no save file found for {lastSaveName}");
            return;
        }
        ApplySaveData(save);
    }

    public void LoadGame(string saveName)
    {
        lastSaveName = saveName;
        LoadGame();
    }

    /// <summary>
    /// Loads exactly what F9 loads (the quicksave). Single source of truth so
    /// "continue game" on boot can never diverge from the in-game quickload.
    /// </summary>
    public void QuickLoad()
    {
        if (SaveLoadSystem.SaveManager.Instance != null)
        {
            Debug.Log("[PlacementSystem.QuickLoad] Using SaveManager.LoadFromSlot(-1).");
            SaveLoadSystem.SaveManager.Instance.LoadFromSlot(-1);
        }
        else
        {
            Debug.Log("[PlacementSystem.QuickLoad] SaveManager not found, falling back to SaveSystem.Load(\"quicksave\").");
            LoadGame();
        }
    }

    // ---------------------------------------------------------
    // SLOT SAVE/LOAD — called by SaveManager for multi-slot UI.
    // Same data format, different file path. Quicksave untouched.
    // ---------------------------------------------------------

    /// <summary>
    /// Serializes the full game state to a JSON string.
    /// SaveManager writes this to its own per-slot file.
    /// </summary>
    public string SerializeToJson()
    {
        SaveData save = BuildSaveData("slot_save");
        return JsonUtility.ToJson(save, true);
    }

    /// <summary>
    /// Deserializes a JSON string and applies it to the world.
    /// SaveManager reads from its own per-slot file.
    /// </summary>
    public void DeserializeFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        SaveData save = JsonUtility.FromJson<SaveData>(json);
        if (save == null)
        {
            Debug.LogError("[PlacementSystem] DeserializeFromJson: bad JSON");
            return;
        }
        ApplySaveData(save);
    }

    // ---------------------------------------------------------
    // SHARED HELPERS — used by BOTH quicksave AND slot save
    // ---------------------------------------------------------

    private SaveData BuildSaveData(string saveName)
    {
        SaveData save = new SaveData();
        save.saveName = saveName;
        save.money = moneyService.CurrentCapital;
        save.spentToday = moneyService.SpentToday;

        if (freeLookCamera != null)
            save.cameraData = freeLookCamera.GetState();

        save.devSettings = CollectDevSettings();
        save.laneConfigs = LaneConfigRegistry.Export();
        save.slotAssignments = SlotAssignmentService.Export();
        save.shiftDefinitions = ShiftDefinitionRegistry.Export();
        save.locationStatuses = LocationStatusRegistry.Export();

        if (ServiceLocator.TryGet(out GameCore.Inventory.ShipmentService shipmentService))
            save.shipments = shipmentService.Export();
        if (ServiceLocator.TryGet(out GameCore.Inventory.OrderService orderService))
            save.orders = orderService.Export();
        if (ServiceLocator.TryGet(out GameCore.Inventory.OrderArrivalService orderArrivalService))
        {
            save.contracts = orderArrivalService.Export();
            save.generatedOffers = orderArrivalService.ExportGeneratedOffers();
        }
        if (ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService dockScheduleService))
            save.dockAppointments = dockScheduleService.Export();

        if (ServiceLocator.TryGet(out GameCore.Inventory.MarketService marketService))
            save.market = marketService.Export();

        if (ServiceLocator.TryGet(out GameCore.Inventory.ReputationService reputationService))
            save.reputation = reputationService.Export();

        if (ServiceLocator.TryGet(out GameCore.Inventory.BrokerService brokerService))
            save.broker = brokerService.Export();

        if (ToolsWindowController.Instance != null)
        {
            var pos = ToolsWindowController.Instance.GetWindowPosition();
            save.toolsWindowX = pos.x;
            save.toolsWindowY = pos.y;
        }

        // Guidance lines
        var guid = Object.FindAnyObjectByType<NavAgentGuidance>();
        if (guid != null) save.guidanceLinesVisible = guid.showGuidanceLine;

        // Hover popup
        var hoverUI = Object.FindAnyObjectByType<WorldHoverPopupUI>();
        if (hoverUI != null) save.hoverPopupEnabled = hoverUI.IsEnabled;

        // Waypoint visibility — read from first Waypoint's mesh renderer
        var wp = Object.FindAnyObjectByType<Waypoint>();
        if (wp != null)
        {
            var mr = wp.GetComponentInChildren<MeshRenderer>();
            if (mr != null) save.waypointsVisible = mr.enabled;
        }

        // ── Game settings (source of truth lives in PlayerPrefs, written by the UI) ──
        save.gameVolume      = PlayerPrefs.GetFloat("GameVolume", 1f);
        save.musicVolume     = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        save.difficulty      = PlayerPrefs.GetInt("Difficulty", 0);
        save.resolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 1);
        save.screenWidth     = Screen.width;
        save.screenHeight    = Screen.height;

        // Graphics preset: prefer the live manager, fall back to PlayerPrefs.
        if (GraphicsPresetManager.Instance != null)
            save.graphicsPreset = GraphicsPresetManager.Instance.CurrentPreset.ToString();
        else
            save.graphicsPreset = PlayerPrefs.GetString("GraphicsPresetName", "Ultra");

        // ── ECONOMY PERSISTENCE ──────────────────────────────────────────────
        // Snapshot hourly costs by GL_Line and fractional accumulators
        if (ServiceLocator.TryGet(out EconomyService economyService))
        {
            save.economy = new EconomySnapshot();

            // Snapshot hourly costs per GL_Line
            var hourlyByGLLine = economyService.GetHourlyByGLLine();
            if (hourlyByGLLine != null)
            {
                foreach (var kvp in hourlyByGLLine)
                {
                    save.economy.hourlyByGLLine.Add(new EconomyGLLineEntry
                    {
                        glLine = kvp.Key,
                        hourlyAmount = kvp.Value
                    });
                }
            }

            // Snapshot fractional accumulators
            var fractionalByGLLine = economyService.GetFractionalByGLLine();
            if (fractionalByGLLine != null)
            {
                foreach (var kvp in fractionalByGLLine)
                {
                    save.economy.fractionalByGLLine.Add(new EconomyFractionalEntry
                    {
                        glLine = kvp.Key,
                        fractionalRemainder = kvp.Value
                    });
                }
            }
        }

        // ── TIME PERSISTENCE ────────────────────────────────────────────────
        // Snapshot current hour/minute/day so time resumes from same point on load
        save.time = new TimeSnapshot();
        SimulationTimeService timeService = null;
        if (!ServiceLocator.TryGet(out timeService))
        {
            // Fallback: try to get from GameContext directly
            var ctx = Object.FindAnyObjectByType<GameContext>();
            if (ctx != null)
                timeService = ctx.TimeService;
        }

        if (timeService != null)
        {
            save.time.hour = timeService.Hour;
            save.time.minute = timeService.Minute;
            save.time.day = timeService.Day;
            save.time.fractionalMinutes = 0f;
        }

        // ── WORK QUEUE PERSISTENCE ──────────────────────────────────────────
        // Snapshot all pending work tasks so employees can resume assigned work
        if (ServiceLocator.TryGet(out WorkQueueSystem workQueueSystem))
        {
            save.workQueue = new List<WorkTaskSnapshot>();
            foreach (var task in workQueueSystem.GetAllTasks())
            {
                if (task == null) continue;
                save.workQueue.Add(new WorkTaskSnapshot
                {
                    taskId = task.TaskId,
                    type = (int)task.Type,
                    requiredRole = (int)task.RequiredRole,
                    palletId = task.PalletId,
                    description = task.Description,
                    status = (int)task.Status,
                    assignedToEmployeeGuid = task.AssignedToEmployeeGuid,
                    fromLocation = task.FromLocation,
                    toLocation = task.ToLocation,
                    area = (int)task.Area,
                    priority = task.Priority,
                    orderId = task.OrderId,
                    skuId = task.SkuId
                });
            }
        }

        // ── PALLET PERSISTENCE ──────────────────────────────────────────────
        // Snapshot all pallets with their XYZ coordinates for exact restoration
        if (ServiceLocator.TryGet(out InventoryService inventoryService))
        {
            var allPallets = inventoryService.GetAllPallets();
            save.pallets = new List<PalletSnapshot>();

            foreach (var palletRecord in allPallets)
            {
                if (palletRecord == null) continue;

                // Check if pallet is in a staging lane using LaneNamingService
                // Returns e.g., "1A-03" if in a lane, null otherwise
                string laneAddress = LaneNamingService.AddressAt(palletRecord.CurrentLocation);
                string laneId = null;
                if (!string.IsNullOrEmpty(laneAddress))
                {
                    // Extract just the lane ID ("1A" from "1A-03")
                    laneId = laneAddress.Split('-')[0];
                }

                save.pallets.Add(new PalletSnapshot
                {
                    palletId = palletRecord.PalletId,
                    loadId = palletRecord.LoadId,
                    skuId = palletRecord.SkuId,
                    quantity = palletRecord.Quantity,
                    receivedDayNumber = palletRecord.ReceivedDayNumber,
                    expirationDayNumber = palletRecord.ExpirationDayNumber,
                    isContaminated = palletRecord.IsContaminated,
                    locationX = palletRecord.CurrentLocation.x,  // **XYZ: Grid X**
                    locationY = palletRecord.CurrentLocation.y,  // **XYZ: Grid Y**
                    worldHeightY = palletRecord.WorldHeightY,    // **XYZ: World Height**
                    stagingLaneId = laneId  // e.g., "1A", "2B", or null if in storage
                });
            }

            if (save.pallets.Count > 0)
                Debug.Log($"[PlacementSystem] Saved {save.pallets.Count} pallets");
        }

        // ── DOCK PALLET VISUAL PERSISTENCE ────────────────────────────────────
        // Literal world-transform capture of every live pallet GameObject (root + built cases).
        // See PalletPersistenceService for why this replaced the old SKU/Ti-Hi-driven
        // reconstruction. Pallets are excluded from the generic placedObjects loop below (see the
        // "Inventory" category skip there) so they aren't captured twice.
        save.dockPallets = GameCore.Persistence.PalletPersistenceService.CaptureAll();

        // ── TRUCK YARD PERSISTENCE ─────────────────────────────────────────────
        // Snapshot every truck that is still in the yard (not yet departed). Departing trucks
        // are skipped and their PO is stamped Departed here so they don't respawn next load.
        // Any pallets on a dock stocker's forks are folded back into the truck's snapshot so
        // they restore on the trailer rather than disappearing.
        save.trucks = GameCore.Persistence.TruckPersistenceService.CaptureAll();

        foreach (var entry in PlacedObjectRegistry.All)
        {
            // Yard floor tiles (id 200) aren't saved individually — they make up the vast
            // majority of placedObjects (~9,400 of ~10,250) and are regenerated on load by
            // GameContext.PopulateYardFloors, which fills every empty cell with the yard
            // tile (skipping cells that already have a different floor). Saving/spawning
            // them all individually is what caused the multi-second freeze on load.
            if (entry.data.id == 200) continue;

            // Employees are persisted via employeeRecords (identity + position), NEVER as grid
            // placements. Saving them as placed objects regenerates a fresh random identity on
            // load AND double-spawns them — that's the "strangers appear after load" bug.
            if (entry.GetComponent<EmployeeIdentity>() != null) continue;

            // Pallets are captured above by PalletPersistenceService (exact world transform +
            // cases) — skip them here so they aren't saved twice under two different schemes.
            if (entry.data.category == "Inventory") continue;

            // Trucks are handled separately by TruckPersistenceService. Capture them there and
            // skip them here to prevent duplicate "boilerplate" clones on load.
            if (entry.GetComponent<TruckController>() != null) continue;

            SavedObject obj = new SavedObject();
            obj.id = entry.data.id;
            obj.x = entry.gridX;
            obj.y = entry.gridY;
            obj.rot = entry.rotation;
            // Prefer the explicitly-recorded worldSpaceYHeight if set; otherwise use current transform Y
            obj.worldY = entry.worldSpaceYHeight > 0f ? entry.worldSpaceYHeight : entry.transform.position.y;

            // Mobile objects (MHE/Vehicles): capture exact world position and rotation so they resume
            // exactly where they were instead of snapping back to their placement cell center.
            if (entry.data.category == "MHE" || entry.GetComponent<UnityEngine.AI.NavMeshAgent>() != null)
            {
                obj.hasTransform = true;
                obj.pos = entry.transform.position;
                obj.rotation = entry.transform.rotation;
            }

            // Committed aisle racks carry their location metadata (aisle/bay/level/facing/travel) only
            // in memory on the PlacedObject — the save format persists customData, so encode that
            // metadata into it. Without this, loaded racks revert to their prefab default label.
            if (entry.isRackLive && entry.data.category == "Racking")
                obj.customData = RackSaveCodec.Encode(entry);
            else
                obj.customData = entry.customData;

            save.placedObjects.Add(obj);
        }

        // Employee persistence — snapshot only REGISTERED (active) employees, each with their
        // live world position so they resume where they were. Using the registry rather than a
        // scene scan means a terminated employee still walking off to the exit is NOT saved
        // (they were already unregistered), so they don't come back as active on load.
        if (EmployeeRegistry.Instance != null)
        {
            foreach (var ident in EmployeeRegistry.Instance.All)
            {
                if (ident == null || ident.Record == null) continue;

                // System-managed identities (Guard, vehicle operators) should NOT be saved in
                // the roster — they are recreated by their owning objects (GuardShack, RT, DS).
                // Saving them here causes duplication because the recreated shack/vehicle will
                // spawn a second one on load.
                if (ident.SystemManaged) continue;

                var rec = ident.Record.Clone();
                var t = ident.transform;
                rec.posX = t.position.x;
                rec.posY = t.position.y;
                rec.posZ = t.position.z;
                rec.rotY = t.eulerAngles.y;
                rec.hasSavedPosition = true;

                // MHE boarding — record which vehicle (by stable grid cell) this operator was
                // riding, if any, so RestoreBoarding can re-seat them on load.
                if (ident.AssignedSlot != null)
                {
                    var vehiclePo = ident.AssignedSlot.GetComponent<PlacedObject>();
                    if (vehiclePo != null)
                    {
                        rec.hasBoardedVehicle = true;
                        rec.boardedVehicleGridX = vehiclePo.gridX;
                        rec.boardedVehicleGridY = vehiclePo.gridY;
                        Vector3 vPos = vehiclePo.transform.position;
                        rec.boardedVehicleWorldX = vPos.x;
                        rec.boardedVehicleWorldY = vPos.y;
                        rec.boardedVehicleWorldZ = vPos.z;
                    }
                }

                save.employeeRecords.Add(rec);
            }
        }

        // Former employees (terminated/resigned) — persisted so a rehire is possible later.
        // Only snapshot if an archive exists, so saving never spawns one needlessly.
        if (FormerEmployeeArchive.HasInstance)
            save.formerEmployees = FormerEmployeeArchive.Instance.Snapshot();

        // ── MHE OPERATOR CARRIED-PALLET PERSISTENCE ──────────────────────────
        // Any pallet currently riding an operator's forks/anchor mid-task (mid-putaway on a Reach
        // Truck, or the rare late-stage Dock Stocker carry) — see MHEOperatorPersistenceService.
        save.carriedPallets = GameCore.Persistence.MHEOperatorPersistenceService.CaptureAll();

        // Trim portrait PNGs for anyone no longer in the active character database (e.g. unhired
        // candidates that cycled off the hiring board) so the Portraits folder can't grow unbounded.
        if (EmployeePhotoBooth.Instance != null)
            EmployeePhotoBooth.Instance.PrunePortraits();

        return save;
    }

    private void ApplySaveData(SaveData save)
    {
        // RestoreCapital, NOT SetMoney: SetMoney books the difference between starting capital and
        // the saved balance as a real "Debug" transaction, so every load charged that gap to lifetime
        // expenses. On a real save that was $26,731 of $26,879 total — 99.4% of everything the player
        // had apparently spent was one load correction, and it made a healthy economy read as a 30:1
        // burn on every financial panel.
        moneyService.RestoreCapital(save.money);
        moneyService.SetSpentToday(save.spentToday);

        // ── RESET TRUCK YARD ─────────────────────────────────────────────────
        // Destroy all live trucks and release dock slots BEFORE ClearAll() wipes the
        // ShippingDoor placed objects (which DockSlot.Release references). Trucks are NOT
        // in PlacedObjectRegistry, so ClearAll() doesn't touch them.
        var yardManager = Object.FindAnyObjectByType<TruckYardManager>();
        yardManager?.ResetYard();

        // ── RESTORE ECONOMY STATE ────────────────────────────────────────────
        // Restore hourly costs and fractional accumulators so economy continues from saved state
        if (ServiceLocator.TryGet(out EconomyService economyService) && save.economy != null)
        {
            // Convert snapshot lists back to dictionaries
            var hourlyByGLLine = new Dictionary<string, int>();
            if (save.economy.hourlyByGLLine != null)
            {
                foreach (var entry in save.economy.hourlyByGLLine)
                    hourlyByGLLine[entry.glLine] = entry.hourlyAmount;
            }

            var fractionalByGLLine = new Dictionary<string, float>();
            if (save.economy.fractionalByGLLine != null)
            {
                foreach (var entry in save.economy.fractionalByGLLine)
                    fractionalByGLLine[entry.glLine] = entry.fractionalRemainder;
            }

            economyService.RestoreHourlyState(hourlyByGLLine, fractionalByGLLine);
        }

        // ── RESTORE TIME STATE ──────────────────────────────────────────────
        // Restore hour/minute/day so time resumes from saved point
        SimulationTimeService timeService = null;
        if (!ServiceLocator.TryGet(out timeService))
        {
            // Fallback: try to get from GameContext directly
            var ctx = Object.FindAnyObjectByType<GameContext>();
            if (ctx != null)
                timeService = ctx.TimeService;
        }

        if (timeService != null && save.time != null)
        {
            timeService.RestoreTime(save.time.day, save.time.hour, save.time.minute, save.time.fractionalMinutes);
        }

        // ── RESTORE PALLET STATE ─────────────────────────────────────────────
        // Restore all pallets with their XYZ coordinates to exact saved locations
        if (ServiceLocator.TryGet(out InventoryService inventoryService))
        {
            Debug.Log($"[PlacementSystem] Clearing pallets before restore...");
            inventoryService.ClearAllPallets();

            if (save.pallets != null && save.pallets.Count > 0)
            {
                Debug.Log($"[PlacementSystem] Restoring {save.pallets.Count} pallets from save file");

                foreach (var palletSnap in save.pallets)
                {
                    if (palletSnap == null) continue;

                    // Reconstruct PalletMasterRecord from snapshot
                    var restored = new PalletMasterRecord(
                        palletSnap.skuId,
                        palletSnap.quantity,
                        new Vector2Int(palletSnap.locationX, palletSnap.locationY),  // **XYZ: Grid coords**
                        palletSnap.receivedDayNumber,
                        palletSnap.expirationDayNumber
                    );

                    // Restore all fields (LoadId is the identifier, PalletId is preserved for tasks)
                    restored.PalletId = palletSnap.palletId;
                    restored.LoadId = palletSnap.loadId;  // **10-digit license plate**
                    LoadIDGenerator.Seed(palletSnap.loadId); // prevent future collisions with this restored ID
                    restored.IsContaminated = palletSnap.isContaminated;
                    restored.WorldHeightY = palletSnap.worldHeightY;  // **XYZ: World height**
                    restored.StagingLaneId = palletSnap.stagingLaneId;  // Lane ID if on dock, null if in storage

                    inventoryService.RegisterPalletDirect(restored);
                }

                var finalCount = inventoryService.GetAllPallets().Count;
                Debug.Log($"[PlacementSystem] Restore complete: {finalCount} pallets now in InventoryService");
            }
            else
            {
                Debug.Log($"[PlacementSystem] No pallets in save file to restore");
            }
        }

        // ── RESTORE WORK QUEUE STATE ─────────────────────────────────────────
        // Restore all pending work tasks so employees can continue assigned work
        if (ServiceLocator.TryGet(out WorkQueueSystem workQueueSystem) && save.workQueue != null && save.workQueue.Count > 0)
        {
            workQueueSystem.ClearAllTasks();
            foreach (var snapshot in save.workQueue)
            {
                if (snapshot == null) continue;

                // Reconstruct WorkTask from snapshot
                var task = new GameCore.Labor.WorkTask(
                    (GameCore.Labor.WorkTaskType)snapshot.type,
                    (EmployeeRole)snapshot.requiredRole,
                    snapshot.palletId,
                    snapshot.description,
                    snapshot.fromLocation,
                    snapshot.toLocation,
                    (GameCore.Inventory.PalletData.AreaCategory)snapshot.area,
                    snapshot.priority
                );

                // Restore the task's original TaskId and status via reflection
                var taskIdField = task.GetType().GetProperty("TaskId");
                if (taskIdField != null && taskIdField.CanWrite)
                {
                    // If the property is read-only, we'll need a different approach
                    // For now, just register the task with its new ID
                }

                // Set status and assignment
                task.Status = (GameCore.Labor.WorkTaskStatus)snapshot.status;
                task.AssignedToEmployeeGuid = snapshot.assignedToEmployeeGuid;
                task.OrderId = snapshot.orderId;
                task.SkuId = snapshot.skuId;

                workQueueSystem.RegisterRestoredTask(task);
            }
        }

        ApplySavedSettings(save);
        LaneConfigRegistry.Import(save.laneConfigs);
        SlotAssignmentService.Import(save.slotAssignments);
        ShiftDefinitionRegistry.Import(save.shiftDefinitions);
        LocationStatusRegistry.Import(save.locationStatuses);

        // A Reserved slot only stays meaningful while some WorkTask is still actively holding it (as
        // its FromLocation/ToLocation) — a putaway captured mid-carry resumes via ResumeDeliverToRack
        // using that exact ToLocation, and a Replenish task locks both ends at creation. If a Reserved
        // address in the just-imported registry matches no surviving task, its owning coroutine died
        // before this save (deleted vehicle, fired employee, domain reload) and nothing will ever
        // release it — it would sit permanently "full" to PutawayLogic while its LocationData shows
        // Available in the Inspector. Clean those up right after restoring the task list.
        if (workQueueSystem != null)
        {
            var claimedAddresses = new HashSet<string>();
            foreach (var t in workQueueSystem.Tasks)
            {
                // Cancelled tasks hold no claim either — leaving them in would keep their reserve slot
                // pinned as Reserved forever with no live task ever coming to fill it.
                if (t.Status == GameCore.Labor.WorkTaskStatus.Complete) continue;
                if (t.Status == GameCore.Labor.WorkTaskStatus.Cancelled) continue;
                if (!string.IsNullOrEmpty(t.ToLocation)) claimedAddresses.Add(t.ToLocation);
                if (!string.IsNullOrEmpty(t.FromLocation)) claimedAddresses.Add(t.FromLocation);
            }
            int releasedCount = LocationStatusRegistry.ReleaseUnclaimedReservations(claimedAddresses);
            if (releasedCount > 0)
                Debug.Log($"[PlacementSystem] Released {releasedCount} orphaned Reserved location(s) on load with no matching in-progress task.");
        }

        if (ServiceLocator.TryGet(out GameCore.Inventory.ShipmentService shipmentService))
            shipmentService.Import(save.shipments);
        if (ServiceLocator.TryGet(out GameCore.Inventory.OrderService orderService))
            orderService.Import(save.orders);
        if (ServiceLocator.TryGet(out GameCore.Inventory.OrderArrivalService orderArrivalRestore))
        {
            orderArrivalRestore.Import(save.contracts);
            // AFTER Import: the rebuilt offers are added to the catalog the authored ones already
            // occupy, and the loss-cooldown check they're filtered by reads the signed records above.
            orderArrivalRestore.ImportGeneratedOffers(save.generatedOffers);
        }
        // After orderService.Import: appointments reference order ids, and importing them against an
        // already-restored order list keeps the two consistent from the first frame.
        if (ServiceLocator.TryGet(out GameCore.Inventory.DockScheduleService dockScheduleRestore))
            dockScheduleRestore.Import(save.dockAppointments);

        // Prices and spot deals. Import re-seeds anything missing and tops the offer board back up,
        // so a save written on day 3 and loaded on day 6 doesn't come back with three expired cards.
        if (ServiceLocator.TryGet(out GameCore.Inventory.MarketService marketRestore))
            marketRestore.Import(save.market);

        if (ServiceLocator.TryGet(out GameCore.Inventory.ReputationService reputationRestore))
            reputationRestore.Import(save.reputation);

        if (ServiceLocator.TryGet(out GameCore.Inventory.BrokerService brokerRestore))
            brokerRestore.Import(save.broker);

        if (save.cameraData != null && freeLookCamera != null)
            freeLookCamera.SetState(save.cameraData);

        ClearAll();

        // Restore the former-employee archive (best-effort; older saves simply have none).
        FormerEmployeeArchive.Instance.LoadFrom(save.formerEmployees);

        // Restore grounds/foundations/floors FIRST. save.placedObjects has no guaranteed
        // order (an object can easily be serialized before the floor it sits on) — and
        // SpawnFromSave's NavMeshAgent floor-height correction (GetFloorTopY) reads whatever's
        // already in the grid at that moment. A vehicle restored before its foundation finds
        // an empty cell, computes floorTopY=0, and ends up clipped into the ground — which one
        // vehicle hits and another doesn't depends purely on each one's arbitrary position in
        // the save list. OrderBy is a stable sort, so relative order within each group (and for
        // everything else) is otherwise unchanged.
        var orderedObjects = save.placedObjects.OrderBy(o => SurfaceLoadPriority(registry.GetByID(o.id)));

        // A combined-prefab Foundation (FoundationFloorGroup) ships its 4 default floor tiles as
        // real prefab children — Instantiate (inside SpawnFromSave below) brings them along for
        // free, and each self-registers into PlacedObjectRegistry via its own OnEnable
        // (RegisterDespiteNestedParent), then gets placed into the grid by RebuildFromRegistry
        // further down (which derives cell from world Transform position, not from anything set
        // here). But BuildSaveData still wrote an ordinary SavedObject entry for each of those 4
        // children too — they're PlacedObjectRegistry members like anything else — so without this
        // precheck the loop below would ALSO spawn 4 independent FloorTile instances at the same
        // cells, doubling them. Precompute which (cell, floorTileId) pairs a combined foundation's
        // own defaults already cover so the loop can skip those redundant standalone entries and
        // only spawn genuine customizations (a different id at that cell) or tiles outside any
        // foundation's footprint.
        var claimedDefaultTileCells = new HashSet<(Vector2Int cell, int id)>();
        foreach (var groundSave in save.placedObjects)
        {
            ObjDataSO groundSo = registry.GetByID(groundSave.id);
            if (groundSo == null) continue;
            if (groundSo.category != "Foundation" && groundSo.category != "Grounds") continue;
            if (groundSo.defaultFloorTile == null) continue;
            if (groundSo.prefab == null || groundSo.prefab.GetComponent<FoundationFloorGroup>() == null) continue;

            Vector2Int[] tileOffsets = groundSo.GetFootprintOffsets(-(groundSave.rot * 90f));
            foreach (var o in tileOffsets)
                claimedDefaultTileCells.Add((new Vector2Int(groundSave.x, groundSave.y) + o, groundSo.defaultFloorTile.id));
        }

        // Counts of saved ids the registry can't resolve, summarised into ONE loud line after the
        // loop. A per-object warning is technically the same information, but 270 identical lines
        // scroll past as noise — which is exactly how a dropped registry entry once flattened an
        // entire warehouse to Y=0 without anyone spotting the cause. See the summary below.
        var unresolvedIds = new Dictionary<int, int>();

        foreach (var objSave in orderedObjects)
        {
            // Skip yard floor tiles from older saves that still have them serialized —
            // PopulateYardFloors regenerates these below, so spawning them here would
            // re-introduce the load-time freeze for existing save files.
            if (objSave.id == 200) continue;

            ObjDataSO so = registry.GetByID(objSave.id);
            if (so == null)
            {
                unresolvedIds.TryGetValue(objSave.id, out int seen);
                unresolvedIds[objSave.id] = seen + 1;
                continue;
            }

            // Skip a standalone Floor Tile entry that a combined Foundation's own prefab children
            // already cover (see claimedDefaultTileCells above) — spawning it too would double the
            // tile at this cell. Only an exact (cell, id) match at the default rotation is skipped;
            // a swapped/custom tile has a different id and still spawns normally.
            if (so.isFloor && objSave.rot == 0 &&
                claimedDefaultTileCells.Contains((new Vector2Int(objSave.x, objSave.y), so.id)))
            {
                continue;
            }
            // Skip employee placements from OLDER saves — employees are restored from
            // employeeRecords below (with their saved identity + position). Spawning them here
            // too would duplicate them with regenerated identities. New saves don't store
            // employees as placed objects at all (see BuildSaveData).
            if (so.category == "Worker" || so.category == "Staff") continue;

            // Skip pallet placements — pallets (category "Inventory") aren't written into
            // save.placedObjects at all anymore (see the matching skip in BuildSaveData above) and
            // are restored separately by PalletPersistenceService.RestoreAll below, which also
            // rebuilds their cases at exact captured transforms. This `continue` only matters for
            // OLDER save files that still have Inventory entries in placedObjects.
            if (so.category == "Inventory") continue;

            // Skip truck placements from OLDER saves — trucks are restored from snapshots
            // separately. Restoring them here as placed objects creates a generic clone
            // that stacks on the correct restored instance.
            if (so.prefab != null && so.prefab.GetComponent<TruckController>() != null) continue;

            SpawnFromSave(so, objSave.x, objSave.y, objSave.rot, objSave.customData, objSave.worldY, objSave.hasTransform, objSave.pos, objSave.rotation);
        }

        // LogError, not a warning: an id the registry can't resolve means those objects are silently
        // absent from the loaded world, and if any of them were GROUNDS everything that was stacked
        // on top loses the height it was standing on and collapses toward Y=0 — the floor drops to
        // half a tile-thickness, walls and racks follow it down, and nothing in the scene says why.
        // That is a corrupted load, not a cosmetic hiccup, and it is almost always one cause: an
        // ObjDataSO that still exists on disk but was dropped out of the ObjDataRegistry list.
        if (unresolvedIds.Count > 0)
        {
            var parts = new List<string>();
            int total = 0;
            foreach (var kv in unresolvedIds) { parts.Add($"id {kv.Key} x{kv.Value}"); total += kv.Value; }
            Debug.LogError(
                $"[PlacementSystem] LOAD INCOMPLETE — {total} saved object(s) were skipped because their " +
                $"id is not in the ObjDataRegistry: {string.Join(", ", parts)}. " +
                "Add the matching ObjDataSO asset(s) back to the Obj Data Registry — if any of them are " +
                "Grounds/Foundations, everything above them will have loaded at the wrong height.");
        }

        // Lower number restores first. Grounds/Foundations/floors must exist in the grid
        // before anything else (vehicles, racking) queries it for floor height. Unknown ids
        // (so == null) sort last — SpawnFromSave's own null check skips them anyway.
        static int SurfaceLoadPriority(ObjDataSO so)
        {
            if (so == null) return 2;
            if (so.category == "Foundation" || so.category == "Grounds" || so.isFloor) return 0;
            return 1;
        }

        // Re-sync ShippingDoor numbers from their just-restored customData BEFORE trucks try to
        // match doors by number below. Each door's DockSlot.OnEnable() fires synchronously inside
        // Instantiate() above — BEFORE SpawnFromSave has a chance to write objSave.customData onto
        // its PlacedObject a few lines later — so DockSlot.AssignDoorNumbers() runs once per door
        // with an empty customData and hands out fresh sequential numbers instead of each door's
        // real saved number. That mismatch made TruckPersistenceService's door-number lookup fail
        // for otherwise-correctly-docked trucks, orphaning them (no dock claimed) forever after.
        // Now that every door's customData is the correct restored value, re-running this fixes
        // DockSlot.DoorNumber to match before anything below depends on it.
        DockSlot.AssignDoorNumbers();

        // Rebuild the employee set from the save UNCONDITIONALLY — even when the save has zero
        // employees. Destroy whatever employees are currently in the scene, then respawn exactly
        // the saved ones. (Previously this was skipped when the save had 0 employees, which left
        // the current scene's employees alive → "phantom" workers after loading an empty roster.)
        //
        // Why deferred: Object.Destroy defers teardown to end-of-frame, so each old employee's
        // OnDestroy → Unregister(guid) also runs at end-of-frame. The old employees may hold the
        // same GUIDs as the records we respawn. If we respawn in the SAME frame, the new
        // employees register under those GUIDs and are then wiped by the old employees' deferred
        // Unregister — leaving the registry empty (verified by end-to-end test). Yielding one
        // frame lets the old Unregister calls complete first. (DestroyImmediate is unsafe here —
        // it can abort the load mid-restore during play mode.)
        // System-managed identities (yard Guard, vehicle operators nested in prefabs like the
        // ReachTruck) are skipped — they're never in employeeRecords, and their own owning
        // system (the vehicle/spawner prefab respawned above) already recreated them. Destroying
        // them here with nothing to recreate them would leave the vehicle's operator seat empty.
        var existingEmployees = Object.FindObjectsByType<EmployeeIdentity>();
        foreach (var ident in existingEmployees)
        {
            // Destroy EVERY employee identity, including system-managed ones (Guard, etc).
            // This ensures a clean slate. The owning objects (Shack, Vehicles) are about
            // to be destroyed by ClearAll() and will recreate their managed employees
            // when they are re-spawned later in the load cycle.
            if (ident != null && ident.gameObject != null)
                Object.Destroy(ident.gameObject);
        }

        StartCoroutine(RespawnEmployeesAfterDestroyFlush(save.employeeRecords ?? new List<EmployeeRecord>(), save.carriedPallets));

        grid.RebuildFromRegistry();
        ServiceLocator.Get<EconomyService>()?.RebuildFromRegistry();

        // ── INSTANTIATE PALLET VISUALS ──────────────────────────────────────
        // Literal world-transform restore (exact position/rotation + exact case transforms) —
        // see PalletPersistenceService. Replaces the old InventoryPersistenceService SKU/Ti-Hi
        // reconstruction (still present in the codebase, unused, kept for reference/rollback).
        // Must run after save.pallets has restored PalletMasterRecords above, since a received
        // pallet's PalletData is rebuilt from that record's SKU/quantity/expiration.
        GameCore.Persistence.PalletPersistenceService.RestoreAll(save.dockPallets, grid);

        // Rebuild grid AGAIN after pallet visuals are instantiated, since they now affect cell occupancy
        // (This ensures the placement grid knows about restored pallets in lanes)
        grid.RebuildFromRegistry();

        // ── RESTORE TRUCK YARD ───────────────────────────────────────────────
        // Respawn all saved trucks at their exact saved transforms. DockSlots are already
        // live in DockSlot.All (spawned above by the placedObjects loop), so door lookups
        // work correctly here. Any trucks that were departing at save time were not captured
        // (their PO was stamped Departed by TruckPersistenceService.CaptureAll instead).
        // Re-find the yard manager here: the old instance referenced in `yardManager` above
        // was destroyed by ClearAll() (guard shack is a PlacedObject) and a fresh one was
        // spawned in the SpawnFromSave loop above.
        if (save.trucks != null && save.trucks.Count > 0)
        {
            var freshYardManager = Object.FindAnyObjectByType<TruckYardManager>();
            if (freshYardManager != null)
                GameCore.Persistence.TruckPersistenceService.RestoreAll(save.trucks, freshYardManager);
            else
                Debug.LogWarning("[PlacementSystem] TruckYardManager not found after scene restore — trucks not restored.");
        }

        // Outbound staging pallets are the one physical thing the save never captures, while the
        // orders that own them DO persist their Staged/Loading status and lane. Now that every pallet
        // and truck that CAN be restored has been, reconcile the two: an order still claiming freight
        // in a lane that holds none goes back to picking. Same species of load-time cleanup as the
        // orphaned-Reserved-locations pass above — restored bookkeeping that outlived its physical
        // counterpart. Must run after PalletPersistenceService.RestoreAll and the work-queue restore,
        // never inside OrderService.Import, or it would judge the world before it finished loading.
        if (ServiceLocator.TryGet(out GameCore.Inventory.OrderService orderServiceForReconcile))
            orderServiceForReconcile.ReconcileStagedOrdersAgainstScene();

        // NOTE: there used to be a DockScheduleService.SweepUnbookedOrders() call here, giving every
        // live order a dock appointment it didn't already have. It's gone along with auto-booking —
        // booking a door is now the player's decision and re-making it for them on every load would
        // undo it. A save written before this change loads with its freight stranded until they book
        // it, which is the intended behaviour, not a regression.

        // Refresh rack labels: PlacedObject fields are restored but TMP text isn't.
        // Must happen before yard floors are populated (which triggers NavMesh bake).
        var aisleInit = FindAnyObjectByType<AisleInitializer>();
        if (aisleInit != null)
            aisleInit.RefreshAllRackLabelsAfterLoad();

        // Apply saved dev-settings to all matching scene components
        if (save.devSettings != null && save.devSettings.Count > 0)
            ApplyDevSettings(save.devSettings);

        // Restore Tools Window position
        ToolsWindowController.Instance?.SetWindowPosition(save.toolsWindowX, save.toolsWindowY);

        // Every pallet/vehicle GameObject the player had previously selected via ctrl-click was just
        // destroyed and recreated above — drop any stale selection referencing the old instances so
        // the panel doesn't fall back to an arbitrary "first found" item on next open.
        ToolsWindowController.Instance?.ClearSelections();

        // Restore guidance lines
        foreach (var g in Object.FindObjectsByType<NavAgentGuidance>())
            g.showGuidanceLine = save.guidanceLinesVisible;

        // Restore hover popup state
        var hoverUI = Object.FindAnyObjectByType<WorldHoverPopupUI>();
        if (hoverUI != null) hoverUI.SetEnabled(save.hoverPopupEnabled);

        // Restore waypoints visibility
        foreach (var w in Object.FindObjectsByType<Waypoint>())
            foreach (var r in w.GetComponentsInChildren<MeshRenderer>())
                r.enabled = save.waypointsVisible;

        // Bake must be deferred one frame so Unity's deferred Destroy() calls flush first.
        // BakeSynchronous() called in the same frame as Destroy() feeds stale geometry to
        // CollectSources() (old + new objects both alive), producing a doubled NavMesh that
        // blocks door passages. Yielding one frame lets the old objects disappear first.
        StartCoroutine(BakeAfterDestroyFlush());
    }

    private IEnumerator BakeAfterDestroyFlush()
    {
        yield return null; // wait one frame for Destroy() to flush

        // Rebuild again now that the old objects' deferred Destroy() → Unregister has
        // completed — the RebuildFromRegistry called synchronously above (same frame as
        // ClearAll) still saw those about-to-be-destroyed objects.
        grid.RebuildFromRegistry();
        ServiceLocator.Get<EconomyService>()?.RebuildFromRegistry();

        // Refresh rack labels again (they were refreshed earlier, but grid rebuild may have
        // changed the registry state — ensure labels match the final registry state).
        var aisleInit = FindAnyObjectByType<AisleInitializer>();
        if (aisleInit != null)
        {
            aisleInit.RefreshAllRackLabelsAfterLoad();
        }

        // Yard floor tiles aren't saved to disk (BuildSaveData skips id 200 — see comment
        // there), so they must be regenerated on every load, not just the initial scene
        // Start. PopulateYardFloors fills every empty cell and performs its own
        // RebuildFromRegistry + NavMesh bake when done.
        var ctx = Object.FindAnyObjectByType<GameContext>();
        if (ctx != null)
        {
            yield return ctx.StartCoroutine(ctx.PopulateYardFloors(grid));
        }
        else if (NavMeshManager.Instance != null)
        {
            // Async bake instead of BakeSynchronous() — a synchronous build over the full
            // registry + ~2,500 yard-floor sources froze the main thread for several
            // seconds on every load. The one-frame delay above (for Destroy() flush) is
            // unaffected; only the bake itself is now off-thread.
            NavMeshManager.Instance.BakeImmediate();
        }
    }

    // Respawn saved employees one frame after the old ones are destroyed, so their
    // deferred OnDestroy → Unregister(guid) completes first and frees the saved GUIDs.
    // Spawning in the same frame as Destroy() lets the old Unregister wipe the new
    // employees (shared GUIDs), leaving the registry empty.
    private IEnumerator RespawnEmployeesAfterDestroyFlush(List<EmployeeRecord> records, List<CarriedPalletSnapshot> carriedPallets)
    {
        yield return null; // wait one frame for Destroy() → Unregister to flush

        var spawner = Object.FindAnyObjectByType<EmployeeSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("[PlacementSystem] No EmployeeSpawner found — saved employees not restored.");
            yield break;
        }

        foreach (var rec in records)
        {
            if (rec != null && !string.IsNullOrEmpty(rec.employeeGuid))
                spawner.SpawnEmployee(rec.Clone());
        }

        // ── RESTORE MHE BOARDING + IN-PROGRESS CARRIED PALLETS ───────────────
        // Runs after every employee AND every vehicle (spawned earlier, synchronously, by the
        // placedObjects loop) exists. Boarding first (carried-pallet restore needs AssignedSlot).
        GameCore.Persistence.MHEOperatorPersistenceService.RestoreBoarding(records);
        GameCore.Persistence.MHEOperatorPersistenceService.RestoreCarriedPallets(carriedPallets, grid);
    }

    // ---------------------------------------------------------
    // RESTORE GAME SETTINGS FROM A SAVE
    // Each block is guarded by its sentinel so loading an older
    // save (without these fields) leaves current settings intact.
    // ---------------------------------------------------------
    private void ApplySavedSettings(SaveData save)
    {
        // --- Audio (SFX/game + music) ---
        if (save.gameVolume >= 0f)
        {
            PlayerPrefs.SetFloat("GameVolume", save.gameVolume);
            if (AudioManager.instance != null)
                AudioManager.instance.SetSfxVolume(save.gameVolume);
        }
        if (save.musicVolume >= 0f)
        {
            PlayerPrefs.SetFloat("MusicVolume", save.musicVolume);
            if (AudioManager.instance != null)
                AudioManager.instance.SetMusicVolume(save.musicVolume);
        }

        // --- Graphics preset ---
        if (!string.IsNullOrEmpty(save.graphicsPreset))
        {
            PlayerPrefs.SetString("GraphicsPresetName", save.graphicsPreset);
            if (GraphicsPresetManager.Instance != null &&
                System.Enum.TryParse(save.graphicsPreset, out GraphicsPresetManager.Preset preset))
            {
                GraphicsPresetManager.Instance.ApplyPreset(preset, notify: false);
            }
        }

        // --- Difficulty (also restores the matching sell-back refund rate) ---
        if (save.difficulty >= 0)
        {
            PlayerPrefs.SetInt("Difficulty", save.difficulty);
            if (moneyService != null)
            {
                float sellBackRate = save.difficulty switch
                {
                    0 => 1.0f,  // Clerk
                    1 => 0.75f, // Supervisor
                    2 => 0.5f,  // Manager
                    _ => 0.5f
                };
                moneyService.SetSellBackRate(sellBackRate);
            }
        }

        // --- Resolution ---
        if (save.resolutionIndex >= 0)
            PlayerPrefs.SetInt("ResolutionIndex", save.resolutionIndex);
        if (save.screenWidth > 0 && save.screenHeight > 0)
            Screen.SetResolution(save.screenWidth, save.screenHeight, Screen.fullScreen);

        PlayerPrefs.Save();
    }

        // ---------------------------------------------------------
    // LOAD GAME SPAWNING
    // ---------------------------------------------------------
    public PlacedObject SpawnFromSave(ObjDataSO so, int x, int y, int rot, string customData = "", float worldY = 0f, 
                                      bool hasTransform = false, Vector3 pos = default, Quaternion rotation = default)
    {
        EnsureContainer();
        Vector2Int root = new Vector2Int(x, y);
        float rotationDeg = rot * 90f;

        // Use saved worldY if provided (for stacked pallets), otherwise calculate from grid
        float stackY = 0f;
        if (worldY > 0f)
        {
            stackY = worldY - grid.GetCellCenter(root).y;
        }
        else if (so.isStackable)
        {
            stackY = grid.GetStackHeight(root);
        }

        Vector3 worldPos = grid.GetCellCenter(root);
        worldPos.y += stackY;

        // Mobile objects: use exact saved position/rotation if available.
        if (hasTransform)
        {
            worldPos = pos;
            rotationDeg = rotation.eulerAngles.y;
        }

        bool hasAgent = so.prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;
        GameObject go = Instantiate(so.prefab, worldPos,
                                    hasTransform ? rotation : Quaternion.Euler(0f, rotationDeg, 0f),
                                    hasAgent ? null : _objectsContainer);

        PlacedObject po = go.GetComponent<PlacedObject>();
        if (po == null)
        {
            Debug.LogError($"[PlacementSystem] Prefab '{so.name}' (id={so.id}) is missing a PlacedObject component. Skipping.");
            Destroy(go);
            return null;
        }
        po.Initialize(so, x, y, rot);
        po.customData = customData;
        po.worldSpaceYHeight = worldPos.y; // Restore world-space Y for stacked objects

        if (hasTransform)
        {
            po.hasSavedTransform = true;
            po.savedWorldPos = pos;
            po.savedWorldRot = rotation;
        }

        // Restore committed-rack metadata (aisle/bay/level/facing/travel) encoded into customData on
        // save. Labels themselves are redrawn later in RefreshAllRackLabelsAfterLoad once every rack
        // exists; here we just get the structured fields back so isRackLive/aisle logic works.
        if (so.category == "Racking" && RackSaveCodec.IsRackData(customData))
            RackSaveCodec.RestoreOnto(po, customData);

        // Parent all rack objects under RackingSystemManager for testing visibility.
        if (so.category == "Racking")
        {
            var rackingManager = Object.FindAnyObjectByType<RackingSystemManager>();
            if (rackingManager != null)
                go.transform.SetParent(rackingManager.transform, worldPositionStays: true);
        }

        go.GetComponent<MHEOperatorSlot>()?.NotifyPlaced();

        EnsureEmployeeComponents(go, so);

        // Ensure PalletBuilder loads its state if it exists
        var pb = go.GetComponent<PalletBuilder>();
        if (pb != null) pb.LoadBuildState();

        BuildingData bd = go.GetComponent<BuildingData>();
        if (bd == null)
        {
            Debug.LogError($"[PlacementSystem] Prefab '{so.name}' (id={so.id}) is missing a BuildingData component. Skipping.");
            Destroy(go);
            return null;
        }
        Vector2Int[] offsets = so.GetFootprintOffsets(-rotationDeg);
        bd.Initialize(root, rotationDeg, offsets, so);

        PlacedObjectRegistry.Register(po);

        foreach (var o in offsets)
        {
            Vector2Int c = root + o;
            grid.AddStackObject(c, go, so);
        }

        // For mobile agents (vehicles, humanoids): use exact saved position if available.
        // Otherwise, apply floor-height correction for fresh placements.
        if (go.GetComponent<UnityEngine.AI.NavMeshAgent>() != null)
        {
            if (hasTransform)
            {
                go.transform.position = pos;
                go.transform.rotation = rotation;
            }
            else
            {
                float floorTopY = PlacementFinalizer.GetFloorTopY(grid, root);
                float targetY = floorTopY;
                if (so.worldYOffset != 0) targetY += so.worldYOffset;

                go.transform.position = new Vector3(worldPos.x, targetY, worldPos.z);
            }

            var navAgent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
                navAgent.Warp(go.transform.position);
        }

        return po;
    }

    // ---------------------------------------------------------
    // CLEAR ALL OBJECTS
    // ---------------------------------------------------------
    // Assets/3. UI/3.SaveLoadSystem/SaveLoadScripts/PlacementSystem.cs

    public void ClearAll()
    {
        // Use a snapshot to avoid modification issues while iterating
        var snapshot = PlacedObjectRegistry.GetSnapshot();
        
        for (int i = snapshot.Length - 1; i >= 0; i--)
        {
            var obj = snapshot[i];
            if (obj != null)
            {
                Vector2Int cell = new Vector2Int(obj.gridX, obj.gridY);
                grid.RemoveStackObject(cell, obj.gameObject, obj.data);

                // Destroy() is deferred to end-of-frame, but a load can synchronously Instantiate the
                // NEW placed objects (including doors) later in this SAME call stack (see
                // PlacementSystem's save-load path, which calls AssignDoorNumbers() right after
                // spawning). Until the deferred Destroy actually runs, an old DockSlot here is still
                // sitting in DockSlot.All, so it and the freshly-restored door can briefly BOTH carry
                // the same persisted door number — that's what produced two physical structures both
                // reading as the same door after a reload. Disabling first fires DockSlot.OnDisable()
                // (which removes it from DockSlot.All) immediately and synchronously, before Destroy
                // ever runs, so the old door is already gone from the registry by the time the new one
                // is numbered.
                var dock = obj.GetComponent<DockSlot>();
                if (dock != null) dock.enabled = false;

                Destroy(obj.gameObject);
            }
        }

        PlacedObjectRegistry.Clear();
        grid.InitializeGrid();
    }
    private void OnSlotSaveCompleted(int slotIndex)
    {
        AudioManager.Play("UI_Save");
        if (slotIndex == -1)
            UIToast.Show("Quick-save successful");
        else
            UIToast.Show($"Saved to Slot {slotIndex + 1}");
    }

    private void OnSlotLoadCompleted(int slotIndex)
    {
        AudioManager.Play("UI_Load");
        if (slotIndex == -1)
            UIToast.Show("Quick-load successful");
        else
            UIToast.Show($"Loaded Slot {slotIndex + 1}");
    }

    private void OnDestroy()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveCompleted -= OnSlotSaveCompleted;
            SaveManager.Instance.OnLoadCompleted -= OnSlotLoadCompleted;
        }
    }

    // ---------------------------------------------------------
    // DEV SETTINGS PERSISTENCE
    // Scans the same script types as ToolsWindowController and
    // serialises every tunable (float/int/bool) serialized field.
    // On load, applies values to ALL instances of each type so
    // balance changes affect every agent / object in the scene.
    // ---------------------------------------------------------

    private static readonly HashSet<string> DevScanTypes = new()
    {
        "FreeLookCamera", "AiNavigation", "RatBehavior",
        "WallVisibilityManager", "LightPulse", "Gate_Open_Close",
        "NavMeshManager", "VehicleThrottleAudio", "AmbientMumble", "PalletBuilder",
    };

    private static readonly BindingFlags DevFieldFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static List<DevSettingEntry> CollectDevSettings()
    {
        var result = new List<DevSettingEntry>();
        var seen   = new HashSet<string>();

        foreach (var mb in FindObjectsByType<MonoBehaviour>())
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            if (!DevScanTypes.Contains(typeName) || seen.Contains(typeName)) continue;
            seen.Add(typeName);

            foreach (var field in mb.GetType().GetFields(DevFieldFlags))
            {
                if (!IsTunableField(field)) continue;
                var val = field.GetValue(mb);
                if (val == null) continue;
                result.Add(new DevSettingEntry
                {
                    key = $"{typeName}.{field.Name}",
                    val = val.ToString()
                });
            }
        }
        return result;
    }

    private static void ApplyDevSettings(List<DevSettingEntry> settings)
    {
        // Group all matching scene components by type name
        var byType = new Dictionary<string, List<MonoBehaviour>>();
        foreach (var mb in FindObjectsByType<MonoBehaviour>())
        {
            if (mb == null) continue;
            string tn = mb.GetType().Name;
            if (!DevScanTypes.Contains(tn)) continue;
            if (!byType.ContainsKey(tn)) byType[tn] = new List<MonoBehaviour>();
            byType[tn].Add(mb);
        }

        foreach (var entry in settings)
        {
            int dot = entry.key.IndexOf('.');
            if (dot < 0) continue;
            string typeName  = entry.key.Substring(0, dot);
            string fieldName = entry.key.Substring(dot + 1);
            if (!byType.TryGetValue(typeName, out var instances) || instances.Count == 0) continue;

            var field = instances[0].GetType().GetField(fieldName, DevFieldFlags);
            if (field == null) continue;

            object parsed = null;
            if      (field.FieldType == typeof(float) && float.TryParse(entry.val, out float f)) parsed = f;
            else if (field.FieldType == typeof(int)   && int.TryParse(entry.val,   out int   i)) parsed = i;
            else if (field.FieldType == typeof(bool)  && bool.TryParse(entry.val,  out bool  b)) parsed = b;
            if (parsed == null) continue;

            // Apply to every instance of this type — balance settings should be universal
            foreach (var mb in instances)
                field.SetValue(mb, parsed);
        }
    }

    private static bool IsTunableField(FieldInfo f)
    {
        if (f.FieldType != typeof(float) && f.FieldType != typeof(int) && f.FieldType != typeof(bool))
            return false;
        return (f.IsPublic && f.GetCustomAttribute<HideInInspector>() == null)
            || (!f.IsPublic && f.GetCustomAttribute<SerializeField>() != null);
    }
}
