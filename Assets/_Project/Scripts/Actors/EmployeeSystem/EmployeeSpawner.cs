using UnityEngine;

/// <summary>
/// Bridges EmployeeLifecycleService record generation with GameObject instantiation.
///
/// On Start:
/// 1. Subscribes to EmployeeLifecycleService.OnHired so programmatic hires spawn GameObjects.
/// 2. Optionally auto-spawns employees for testing (_autoSpawnOnStart).
///
/// Place on the same GameObject that holds EmployeeLifecycleService / EmployeeRegistry
/// or any persistent manager GameObject. Assign worker prefabs in the Inspector.
/// </summary>
public class EmployeeSpawner : MonoBehaviour
{
    // ─── Prefab references ────────────────────────────────────────────────────
    [Header("Prefabs")]
    [SerializeField] private GameObject _workerMalePrefab;
    [SerializeField] private GameObject _workerFemalePrefab;

    [Header("Modular Avatars")]
    [Tooltip("When ON, spawned employees use a modular avatar (assembled from AvatarPartLibrary, " +
             "seeded from their GUID so the look is stable) instead of the default worker mesh. " +
             "NOTE: parts aren't weight-painted yet, so they render in T-pose until weights exist. " +
             "Turn OFF to use the animated worker model. Females fall back to the default model " +
             "until female parts are added.")]
    [SerializeField] private bool _useModularAvatars = true;
    [Tooltip("Dedicated model for Inventory Control clerks. Used exclusively for the InventoryControl role, which is exclusively female.")]
    [SerializeField] private GameObject _clerkPrefab;

    // ─── Auto-spawn (testing) ─────────────────────────────────────────────────
    [Header("Auto-Spawn (Testing)")]
    [SerializeField] private bool _autoSpawnOnStart = false;
    [SerializeField] private int _autoSpawnCount = 5;

    [Header("Spawn Point")]
    [SerializeField] private Transform _spawnPoint;

    [Header("MHE Equipment (Reach Truck / Dock Stocker hiring)")]
    [Tooltip("Reach Truck ObjDataSO (RT). Used to spawn a brand-new Reach Truck when hiring a " +
             "Reach Truck Operator finds no unoccupied RT to board.")]
    [SerializeField] private ObjDataSO _reachTruckData;
    [Tooltip("Dock Stocker ObjDataSO (DS). Used to spawn a brand-new Dock Stocker when hiring a " +
             "Dock Stocker Operator finds no unoccupied DS to board.")]
    [SerializeField] private ObjDataSO _dockStockerData;

    private bool _isAutoSpawning;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────
    private void Start()
    {
        if (EmployeeLifecycleService.Instance != null)
            EmployeeLifecycleService.Instance.OnHired += OnEmployeeHired;

        if (_autoSpawnOnStart)
        {
            _isAutoSpawning = true;

            for (int i = 0; i < _autoSpawnCount; i++)
            {
                // Hire via the lifecycle service to generate a record and fire events.
                // If the service is missing, fall back to direct generation.
                var record = EmployeeLifecycleService.Instance != null
                    ? EmployeeLifecycleService.Instance.Hire()
                    : EmployeeGenerator.Generate(EmployeeGender.Random, "WHSE");

                SpawnEmployee(record);
            }

            _isAutoSpawning = false;
        }
    }

    private void OnDestroy()
    {
        if (EmployeeLifecycleService.Instance != null)
            EmployeeLifecycleService.Instance.OnHired -= OnEmployeeHired;
    }

    // ─── Event handler ────────────────────────────────────────────────────────
    /// <summary>
    /// Respond to programmatic Hire() calls from UI or other systems.
    /// Skipped during auto-spawn to avoid double-instantiation.
    /// </summary>
    private void OnEmployeeHired(EmployeeRecord record)
    {
        if (_isAutoSpawning) return; // already handled inline in Start
        var identity = SpawnEmployee(record);

        // Subscribe to equipment placement so idle operators can seek equipment
        if (identity != null && (record.role == EmployeeRole.ReachTruckOperator
                                 || record.role == EmployeeRole.DockStockerOperator
                                 || record.role == EmployeeRole.Loader))
        {
            MHEPlacementEvent.OnMHEEquipmentPlaced += (slot) => OnEquipmentPlaced(identity, slot, record.role);
        }
    }

    /// <summary>Called when equipment is placed; check if this idle operator should seek it.</summary>
    private void OnEquipmentPlaced(EmployeeIdentity identity, MHEOperatorSlot slot, EmployeeRole role)
    {
        if (identity == null || identity.AssignedSlot != null) return;  // Already has equipment

        var vehicleData = slot.GetComponent<PlacedObject>()?.data;
        if (vehicleData == null) return;

        // Determine what equipment this operator role can use
        ObjDataSO targetData = role == EmployeeRole.ReachTruckOperator ? _reachTruckData
                             : (role == EmployeeRole.DockStockerOperator || role == EmployeeRole.Loader) ? _dockStockerData
                             : null;

        if (vehicleData != targetData) return;  // Wrong equipment type

        // Operator is idle and matches this equipment's role — seek it
        var nav = identity.GetComponent<AiNavigation>();
        if (nav != null)
        {
            nav.SeekEquipment(slot);
        }
    }

    // ─── Spawning ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Instantiate the appropriate worker prefab and assign the record.
    ///
    /// Handles the registration dance: Instantiate triggers EmployeeIdentity.Awake
    /// which auto-generates a record and registers it — we unregister that,
    /// apply the hired record, then re-register.
    /// </summary>
    public EmployeeIdentity SpawnEmployee(EmployeeRecord record)
    {
        if (record == null)
        {
            Debug.LogWarning("[EmployeeSpawner] Cannot spawn — record is null.");
            return null;
        }

        // ── Pick prefab ───────────────────────────────────────────────────────
        GameObject prefab = PickPrefab(record);
        if (prefab == null)
        {
            Debug.LogWarning("[EmployeeSpawner] No worker prefab assigned!");
            return null;
        }

        // ── Instantiate ───────────────────────────────────────────────────────
        // Prefer the employee's last-known position (restored from a save) so they resume
        // exactly where they were; otherwise use the spawn point (fresh hires).
        Vector3 pos;
        Quaternion rot;
        if (record.hasSavedPosition)
        {
            pos = new Vector3(record.posX, record.posY, record.posZ);
            rot = Quaternion.Euler(0f, record.rotY, 0f);
        }
        else
        {
            pos = _spawnPoint != null ? _spawnPoint.position : transform.position;
            rot = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;
        }

        var instance = Instantiate(prefab, pos, rot);
        instance.name = $"Employee_{record.employeeName}";

        // Re-snap the nav agent to the restored spot (Warp keeps it on the NavMesh).
        if (record.hasSavedPosition)
        {
            var agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled)
                agent.Warp(pos);
        }

        var identity = instance.GetComponent<EmployeeIdentity>();
        if (identity == null)
        {
            Debug.LogWarning($"[EmployeeSpawner] Prefab '{prefab.name}' has no EmployeeIdentity component!");
            return null;
        }

        // ── Swap the record ───────────────────────────────────────────────────
        // Instantiate already ran Awake → EnsureRecord → Register (auto-gen GUID).
        // Unregister the auto-generated entry, apply the hired record, re-register.
        if (EmployeeRegistry.Instance != null)
            EmployeeRegistry.Instance.Unregister(identity);

        // Guarantee a modern Custom_ portrait before the record is applied/displayed.
        // This is the single chokepoint for every spawn path (fresh hire, auto-spawn test,
        // AND save-restore via PlacementSystem.RespawnEmployeesAfterDestroyFlush) — the latter
        // calls SpawnEmployee() directly, bypassing the OnHired event that normally triggers
        // portrait generation, so legacy/save-loaded employees would otherwise keep their old
        // static stock-photo avatarResourceKey forever. EnsurePortrait is a no-op if a portrait
        // is already cached, so this is safe to call unconditionally.
        if (EmployeePhotoBooth.Instance != null)
            EmployeePhotoBooth.Instance.EnsurePortrait(record);

        identity.ApplyRecord(record);

        if (EmployeeRegistry.Instance != null)
            EmployeeRegistry.Instance.Register(identity);

        if (_useModularAvatars)
            ApplyModularAvatar(identity);

        // Fresh ReachTruckOperator/DockStockerOperator/Loader attempt to board an existing MHE.
        // Equipment must be placed manually via the build menu first — this system no longer auto-creates
        // equipment. If no unoccupied MHE exists, the operator spawns on-foot. Skipped for save-restored
        // employees (hasSavedPosition) — operator<->vehicle pairing isn't persisted, so a reload
        // intentionally drops them back to free-roaming until a fresh hire re-pairs them.
        if (!record.hasSavedPosition &&
            (record.role == EmployeeRole.ReachTruckOperator || record.role == EmployeeRole.DockStockerOperator
             || record.role == EmployeeRole.Loader))
        {
            TryBoardExistingMHE(identity, record.role);
        }

        return identity;
    }

    // ─── MHE operator assignment ────────────────────────────────────────────────
    /// <summary>
    /// Attempts to send a freshly-hired operator walking toward an existing unoccupied MHE
    /// matching their role (boards on arrival via AiNavigation's normal seek-and-board flow —
    /// matches the behavior of an idle operator reacting to equipment placed after they were
    /// hired, instead of teleporting straight onto the vehicle). Does NOT spawn new equipment —
    /// players must place equipment via the build menu first. If no free vehicle exists, the
    /// operator stays on-foot.
    /// </summary>
    private bool TryBoardExistingMHE(EmployeeIdentity identity, EmployeeRole role)
    {
        ObjDataSO targetData = role == EmployeeRole.ReachTruckOperator ? _reachTruckData
                             : (role == EmployeeRole.DockStockerOperator || role == EmployeeRole.Loader) ? _dockStockerData
                             : null;
        if (targetData == null)
        {
            Debug.LogWarning($"[EmployeeSpawner] TryBoardExistingMHE: no ObjDataSO wired for role {role}");
            return false;
        }

        // Search for any unoccupied MHE matching this operator's role.
        foreach (var slot in FindObjectsByType<MHEOperatorSlot>(FindObjectsSortMode.None))
        {
            if (slot.IsOccupied) continue;
            var vehicleObj = slot.GetComponent<PlacedObject>();
            if (vehicleObj == null || vehicleObj.data != targetData) continue;

            // Found a matching unoccupied vehicle — walk over and board on arrival.
            var nav = identity.GetComponent<AiNavigation>();
            if (nav != null) nav.SeekEquipment(slot);
            return true;
        }

        // No available MHE found — operator stays on-foot.
        Debug.LogWarning($"[EmployeeSpawner] TryBoardExistingMHE: no unoccupied {role} equipment found — operator spawns on-foot.");
        return false;
    }

    /// <summary>
    /// Boards a freshly-hired ReachTruckOperator/DockStockerOperator/Loader onto an MHE: reuses an
    /// existing unoccupied matching vehicle if one exists, otherwise spawns a brand-new one (preferring
    /// an MHE waypoint, falling back to an unoccupied Foundation cell, falling back to the generic spawn
    /// point) and boards that instead.
    ///
    /// DEPRECATED: This method is kept for backward compatibility only. Use TryBoardExistingMHE() instead.
    /// </summary>
    private void AssignToMHE(EmployeeIdentity identity, EmployeeRole role)
    {
        ObjDataSO so = role == EmployeeRole.ReachTruckOperator ? _reachTruckData
                     : (role == EmployeeRole.DockStockerOperator || role == EmployeeRole.Loader) ? _dockStockerData
                     : null;
        if (so == null)
        {
            Debug.LogWarning($"[EmployeeSpawner] AssignToMHE: no ObjDataSO wired for role {role} — operator stays on foot.");
            return;
        }

        // 1) Reuse an existing unoccupied matching vehicle.
        foreach (var slot in FindObjectsByType<MHEOperatorSlot>(FindObjectsSortMode.None))
        {
            if (slot.IsOccupied) continue;
            var existingPo = slot.GetComponent<PlacedObject>();
            if (existingPo == null || existingPo.data != so) continue;

            slot.AssignOperator(identity);
            return;
        }

        // 2) Spawn a brand-new vehicle via the normal placement pipeline (sellable, registered,
        // grid-tracked) — MHE waypoint, else unoccupied Foundation, else the generic spawn point.
        var grid = FindAnyObjectByType<PlacementGrid>();
        var placement = FindAnyObjectByType<PlacementSystem>();
        if (grid == null || placement == null)
        {
            Debug.LogWarning("[EmployeeSpawner] AssignToMHE: no PlacementGrid/PlacementSystem in scene — operator stays on foot.");
            return;
        }

        Vector2Int cell;
        int rotSteps;
        if (!TryFindMHEWaypointCell(grid, out cell, out rotSteps) &&
            !TryFindUnoccupiedFoundationCell(grid, out cell))
        {
            rotSteps = 0;
            Vector3 fallbackPos = _spawnPoint != null ? _spawnPoint.position : transform.position;
            cell = grid.WorldToCell(fallbackPos);
        }

        PlacedObject newPo = placement.PlaceObject(so, cell.x, cell.y, rotSteps);
        if (newPo == null)
        {
            Debug.LogWarning($"[EmployeeSpawner] AssignToMHE: PlaceObject failed for '{so.objName}' — operator stays on foot.");
            return;
        }

        var newSlot = newPo.GetComponent<MHEOperatorSlot>();
        if (newSlot == null)
        {
            Debug.LogWarning($"[EmployeeSpawner] AssignToMHE: spawned '{so.objName}' has no MHEOperatorSlot — operator stays on foot.");
            return;
        }

        newSlot.AssignOperator(identity);
    }

    private static bool TryFindMHEWaypointCell(PlacementGrid grid, out Vector2Int cell, out int rotSteps)
    {
        cell = default;
        rotSteps = 0;
        foreach (var wp in FindObjectsByType<Waypoint>(FindObjectsSortMode.None))
        {
            if (!wp.AllowsGroup(Waypoint.WaypointGroup.MHE)) continue;
            cell = grid.WorldToCell(wp.transform.position);
            rotSteps = Mathf.RoundToInt(wp.transform.eulerAngles.y / 90f) % 4;
            return true;
        }
        return false;
    }

    private static bool TryFindUnoccupiedFoundationCell(PlacementGrid grid, out Vector2Int cell)
    {
        cell = default;
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || po.data == null || po.data.category != "Foundation") continue;

            Vector2Int candidate = new Vector2Int(po.gridX, po.gridY);
            if (grid.IsOccupied(candidate)) continue;

            cell = candidate;
            return true;
        }
        return false;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private GameObject PickPrefab(EmployeeRecord record)
    {
        // Inventory Control has a dedicated clerk model and is exclusively female.
        if (record.role == EmployeeRole.InventoryControl && _clerkPrefab != null)
            return _clerkPrefab;

        return record.gender == EmployeeGender.Female
            ? _workerFemalePrefab ?? _workerMalePrefab
            : _workerMalePrefab ?? _workerFemalePrefab;
    }

    // ─── Modular avatar swap ────────────────────────────────────────────────────
    // Replaces the default animated worker mesh with an assembled modular avatar. The NavMesh
    // agent / Animator / scripts on the root stay (so the employee still moves); the modular
    // avatar rides along. Look is seeded from the GUID, so it's stable + consistent across loads.
    private void ApplyModularAvatar(EmployeeIdentity identity)
    {
        var lib = ModularAvatarAssembler.LoadLibrary();
        if (lib == null || lib.PartCount == 0) return;

        var rec = identity.Record;
        if (rec == null) return;

        string gender = rec.gender == EmployeeGender.Female ? "female" : "male";
        int seed = ModularAvatarAssembler.StableSeed(rec.employeeGuid);

        // The worker's Animator (driven by AgentAnimation). Captured before we touch the hierarchy.
        var workerAnimator = identity.GetComponentInChildren<Animator>(true);

        var avatar = ModularAvatarAssembler.Build(lib, gender, seed);
        if (avatar == null) return;   // no parts for that gender yet → keep the default model

        // Hide the worker's own animated mesh — the modular avatar replaces it visually.
        foreach (var smr in identity.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.enabled = false;

        // Parent the avatar UNDER the worker so it inherits movement, lifecycle, layer and stays
        // discoverable for selection/outline. It keeps its OWN skeleton + Animator (the modular FBX
        // builds its own humanoid avatar from its own rest pose — we can't share the worker's avatar
        // or skeleton because the two FBX exports use different bone orientations).
        var t = avatar.transform;
        t.SetParent(identity.transform, worldPositionStays: false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;
        avatar.name = "ModularAvatar";
        SetLayerRecursively(avatar, identity.gameObject.layer);

        // Force per-frame bounds so frustum culling can't hide the animated mesh.
        foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;

        // Use the avatar's OWN Animator (the modular FBX imported Humanoid as Modular_StaffAvatar via
        // Create-From-This-Model — built from its own rest pose, so retargeting deforms it correctly).
        // Give it the worker's controller and re-bind so it drives the modular skeleton.
        var modAnimator = avatar.GetComponent<Animator>();
        if (modAnimator == null) modAnimator = avatar.AddComponent<Animator>();
        if (modAnimator.avatar == null && workerAnimator != null) modAnimator.avatar = workerAnimator.avatar;
        if (workerAnimator != null) modAnimator.runtimeAnimatorController = workerAnimator.runtimeAnimatorController;
        modAnimator.applyRootMotion = false;
        modAnimator.enabled = true;
        modAnimator.Rebind();

        var sampleBone = FindDeepByName(avatar.transform, "LowerLeg.R");
        avatar.AddComponent<ModularAvatarRig>().Init(workerAnimator, modAnimator, sampleBone);

    }

    private static Transform FindDeepByName(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;
        foreach (Transform c in parent)
        {
            var r = FindDeepByName(c, boneName);
            if (r != null) return r;
        }
        return null;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursively(c.gameObject, layer);
    }
}
