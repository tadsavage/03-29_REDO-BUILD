using GameCore.Economy;
using GameCore.Services;
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

        // Quicksave (F5)
        if (Keyboard.current.f5Key.isPressed && quicksaveTimer <= 0f)
        {
            SaveGame("quicksave");
            quicksaveTimer = quicksaveCooldown;
            AudioManager.Play("UI_Save");
            UIToast.Show("Quick-save successful");

            if (thumbnailCapture != null)
            {
                string saveDir = System.IO.Path.Combine(Application.dataPath, "_Saves");
                thumbnailCapture.CaptureThumbnail(saveDir, "quicksave_thumb.png", tex =>
                {
                    if (tex != null) Destroy(tex);
                });
            }
        }

        // Quickload (F9)
        if (Keyboard.current.f9Key.wasPressedThisFrame && quicksaveTimer <= 0f)
        {
            LoadGame();
            quicksaveTimer = quicksaveCooldown;
            AudioManager.Play("UI_Load");
            UIToast.Show("Quick-load successful");
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

        bool hasAgent = so.prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;
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

            SavedObject obj = new SavedObject();
            obj.id = entry.data.id;
            obj.x = entry.gridX;
            obj.y = entry.gridY;
            obj.rot = entry.rotation;

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
                var rec = ident.Record.Clone();
                var t = ident.transform;
                rec.posX = t.position.x;
                rec.posY = t.position.y;
                rec.posZ = t.position.z;
                rec.rotY = t.eulerAngles.y;
                rec.hasSavedPosition = true;
                save.employeeRecords.Add(rec);
            }
        }

        // Former employees (terminated/resigned) — persisted so a rehire is possible later.
        // Only snapshot if an archive exists, so saving never spawns one needlessly.
        if (FormerEmployeeArchive.HasInstance)
            save.formerEmployees = FormerEmployeeArchive.Instance.Snapshot();

        // Trim portrait PNGs for anyone no longer in the active character database (e.g. unhired
        // candidates that cycled off the hiring board) so the Portraits folder can't grow unbounded.
        if (EmployeePhotoBooth.Instance != null)
            EmployeePhotoBooth.Instance.PrunePortraits();

        // ── DOCK & INVENTORY PERSISTENCE ──────────────────────────────────────
        // Snapshot all transient state that needs to survive load
        save.dock = DockPersistenceService.Snapshot();
        save.inventory = InventoryPersistenceService.Snapshot();
        save.economy = EconomyPersistenceService.Snapshot();
        save.workQueue = WorkQueuePersistenceService.Snapshot();

        return save;
    }

    private void ApplySaveData(SaveData save)
    {
        // ── RESTORE ECONOMY & INVENTORY ──────────────────────────────────────
        // Restore these BEFORE cleared all objects, so systems are ready to process restored state
        EconomyPersistenceService.Restore(save.economy);
        InventoryPersistenceService.Restore(save.inventory);
        WorkQueuePersistenceService.Restore(save.workQueue);

        moneyService.SetMoney(save.money);
        moneyService.SetSpentToday(save.spentToday);
        ApplySavedSettings(save);
        LaneConfigRegistry.Import(save.laneConfigs);

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

        foreach (var objSave in orderedObjects)
        {
            // Skip yard floor tiles from older saves that still have them serialized —
            // PopulateYardFloors regenerates these below, so spawning them here would
            // re-introduce the load-time freeze for existing save files.
            if (objSave.id == 200) continue;

            ObjDataSO so = registry.GetByID(objSave.id);
            if (so == null)
            {
                Debug.LogWarning($"[PlacementSystem] Skipping saved object with unknown id={objSave.id} at ({objSave.x},{objSave.y}) — not in ObjDataRegistry.");
                continue;
            }
            // Skip employee placements from OLDER saves — employees are restored from
            // employeeRecords below (with their saved identity + position). Spawning them here
            // too would duplicate them with regenerated identities. New saves don't store
            // employees as placed objects at all (see BuildSaveData).
            if (so.category == "Worker" || so.category == "Staff") continue;
            SpawnFromSave(so, objSave.x, objSave.y, objSave.rot, objSave.customData);
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
            if (ident.SystemManaged) continue;
            Object.Destroy(ident.gameObject);
        }

        StartCoroutine(RespawnEmployeesAfterDestroyFlush(save.employeeRecords ?? new List<EmployeeRecord>()));

        grid.RebuildFromRegistry();
        ServiceLocator.Get<EconomyService>()?.RebuildFromRegistry();

        // ── INSTANTIATE PALLET VISUALS ──────────────────────────────────────
        // Inventory data was restored, but the 3D meshes weren't instantiated.
        // Create visual pallet prefabs for all restored pallets so they appear in the world.
        InventoryPersistenceService.InstantiateRestoredPalletVisuals();

        // ── RESTORE DOCK STATE ──────────────────────────────────────────────
        // After all placed objects are restored (trucks, staging lanes are now in scene)
        DockPersistenceService.Restore(save.dock);

        // Rebuild grid AGAIN after pallet visuals are instantiated, since they now affect cell occupancy
        // (This ensures the placement grid knows about restored pallets in lanes)
        grid.RebuildFromRegistry();

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
    private IEnumerator RespawnEmployeesAfterDestroyFlush(List<EmployeeRecord> records)
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
    public PlacedObject SpawnFromSave(ObjDataSO so, int x, int y, int rot, string customData = "")
    {
        EnsureContainer();
        Vector2Int root = new Vector2Int(x, y);
        float rotationDeg = rot * 90f;

        float stackY = 0f;
        if (so.isStackable)
            stackY = grid.GetStackHeight(root);

        Vector3 worldPos = grid.GetCellCenter(root);
        worldPos.y += stackY;

        bool hasAgent = so.prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;
        GameObject go = Instantiate(so.prefab, worldPos,
                                    Quaternion.Euler(0f, rotationDeg, 0f),
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

        // Restore committed-rack metadata (aisle/bay/level/facing/travel) encoded into customData on
        // save. Labels themselves are redrawn later in RefreshAllRackLabelsAfterLoad once every rack
        // exists; here we just get the structured fields back so isRackLive/aisle logic works.
        if (so.category == "Racking" && RackSaveCodec.IsRackData(customData))
            RackSaveCodec.RestoreOnto(po, customData);

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

        // For mobile agents (vehicles, humanoids): the isStackable-gated stackY above is for
        // stacked ITEMS (pallets/boxes), not this — without it, a NavMeshAgent-bearing prefab
        // restored from a save lands at the grid-cell-center height (~0) instead of the actual
        // floor/foundation surface, visibly clipped into the ground. Mirrors the same
        // correction PlacementFinalizer.FinalizePlacement already applies for fresh placements.
        if (go.GetComponent<UnityEngine.AI.NavMeshAgent>() != null)
        {
            float floorTopY = PlacementFinalizer.GetFloorTopY(grid, root);
            go.transform.position = new Vector3(worldPos.x, floorTopY, worldPos.z);
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
                Destroy(obj.gameObject);
            }
        }

        PlacedObjectRegistry.Clear();
        grid.InitializeGrid();
    }
private void OnSlotSaveCompleted(int slotIndex)
    {
        AudioManager.Play("UI_Save");
        UIToast.Show($"Saved to Slot {slotIndex + 1}");
    }

    private void OnSlotLoadCompleted(int slotIndex)
    {
        AudioManager.Play("UI_Load");
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
