using GameCore.Persistence;

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
    [Tooltip("Legacy dedicated model for Inventory Control clerks — superseded by " +
             "_icAvatarModel/_icAvatarModelFemale below (2026-09-21: IC is no longer forced female). " +
             "No longer referenced by PickPrefab; kept only so an existing scene assignment isn't lost.")]
    [SerializeField] private GameObject _clerkPrefab;

    [Header("Fixed-Look Roles (override modular avatar)")]
    [Tooltip("Pre-rigged Humanoid FBX (Boss/Exterminator/IC/Security/TruckDriver) applied as a visual " +
             "overlay on top of the standard worker base, exactly like the modular avatar swap, but " +
             "with a fixed model instead of randomly-assembled parts. Takes priority over " +
             "_useModularAvatars for these roles — they always get their dedicated look, never the " +
             "random assembly. IC and TruckDriver now have a Female counterpart too (2026-09-21) — " +
             "IC used to be hardcoded female-only and TruckDriver used a single unisex model.")]
    [SerializeField] private GameObject _bossAvatarModel;
    [SerializeField] private GameObject _exterminatorAvatarModel;
    [SerializeField] private GameObject _icAvatarModel;
    [SerializeField] private GameObject _icAvatarModelFemale;
    [SerializeField] private GameObject _securityAvatarModel;
    [SerializeField] private GameObject _truckDriverAvatarModel;
    [SerializeField] private GameObject _truckDriverAvatarModelFemale;
    [Tooltip("Receiver / Reach Truck Operator / Dock Stocker Operator (2026-09-21) — these three " +
             "previously had no FixedAvatar assigned at all, so they fell through to the random " +
             "modular avatar system, which renders in T-pose (parts aren't weight-painted). Deliberately " +
             "separate from _workerMaleAvatarModel/_workerFemaleAvatarModel below, which still cover " +
             "the REMAINING roles (Order Selector, Loader, Supervisor, HR/Admin/Sanitation) — not " +
             "touched by this change.")]
    [SerializeField] private GameObject _floorWorkerAvatarModel;
    [SerializeField] private GameObject _floorWorkerAvatarModelFemale;

    [Header("Admin (Polyperfect reporter look)")]
    [Tooltip("Admin previously had no dedicated case here and silently fell through to the generic " +
             "worker default (man_large/woman_large) below, disagreeing with EmployeePhotoBooth's " +
             "portrait, which DID special-case Admin to the reporter look. Fixed 2026-09-22.")]
    [SerializeField] private GameObject _adminAvatarModel;
    [SerializeField] private GameObject _adminAvatarModelFemale;

    [Header("Generic Warehouse Worker (Polyperfect overlay)")]
    [Tooltip("Fixed-look overlay applied to every role that has no other dedicated model above " +
             "(Order Selector, Reach Truck/Dock Stocker Operator, Loader, Receiver, Supervisor, " +
             "HR/Admin/Sanitation placeholders). Takes priority over _useModularAvatars, same as " +
             "the roles above — the modular avatar system stays wired but is effectively unused " +
             "once these are assigned.")]
    [SerializeField] private GameObject _workerMaleAvatarModel;
    [SerializeField] private GameObject _workerFemaleAvatarModel;

    [Header("Randomized Avatar Pools (stable per employee, gender-matched)")]
    [Tooltip("When the pool for an employee's actual gender has entries, it's used INSTEAD of the " +
             "single fixed model above for that role — one prefab is picked per employee, seeded " +
             "from their GUID (same trick the old modular avatar system used), so the pick is " +
             "stable for a given employee across saves/reloads. Split Male/Female so the picked " +
             "model always matches the employee's actual gender — 'random' means random among " +
             "that gender's own variants (room to grow later), never a coin flip on which gender " +
             "shows up. No clothing/accessory parts exist yet, so this is the stand-in for that " +
             "system. Leave both pools for a role empty to fall back to that role's single field above.")]
    [SerializeField] private GameObject[] _bossAvatarModelPoolMale;
    [SerializeField] private GameObject[] _bossAvatarModelPoolFemale;
    [SerializeField] private GameObject[] _securityAvatarModelPoolMale;
    [SerializeField] private GameObject[] _securityAvatarModelPoolFemale;
    [SerializeField] private GameObject[] _exterminatorAvatarModelPoolMale;
    [SerializeField] private GameObject[] _exterminatorAvatarModelPoolFemale;
    [SerializeField] private GameObject[] _workerAvatarModelPoolMale;
    [SerializeField] private GameObject[] _workerAvatarModelPoolFemale;

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

    // ─── Equipment seeking callbacks (for cleanup on employee removal) ────────
    private System.Collections.Generic.Dictionary<EmployeeIdentity, System.Action<MHEOperatorSlot>> _equipmentSeekCallbacks = new();

    /// <summary>Exposed so EmployeeAssignmentService can resolve the same ObjDataSO references
    /// without duplicating the Inspector wiring.</summary>
    public ObjDataSO ReachTruckData => _reachTruckData;
    public ObjDataSO DockStockerData => _dockStockerData;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────
    private void Start()
    {
        if (EmployeeLifecycleService.Instance != null)
            EmployeeLifecycleService.Instance.OnHired += OnEmployeeHired;

        if (EmployeeRegistry.Instance != null)
            EmployeeRegistry.Instance.OnEmployeeRemoved += OnEmployeeRemoved;

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

        if (EmployeeRegistry.Instance != null)
            EmployeeRegistry.Instance.OnEmployeeRemoved -= OnEmployeeRemoved;

        // Clean up all stored callbacks
        _equipmentSeekCallbacks.Clear();
    }

    // ─── Event handlers ───────────────────────────────────────────────────────
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
            // Create the callback and store it so we can unsubscribe later when the employee is removed
            System.Action<MHEOperatorSlot> callback = (slot) => OnEquipmentPlaced(identity, slot, record.role);
            _equipmentSeekCallbacks[identity] = callback;
            MHEPlacementEvent.OnMHEEquipmentPlaced += callback;
        }
    }

    /// <summary>
    /// Respond to employee removal (fired/quit/destroyed). Unsubscribe from equipment events.
    /// </summary>
    private void OnEmployeeRemoved(EmployeeIdentity identity)
    {
        if (identity != null && _equipmentSeekCallbacks.TryGetValue(identity, out var callback))
        {
            MHEPlacementEvent.OnMHEEquipmentPlaced -= callback;
            _equipmentSeekCallbacks.Remove(identity);
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

        var fixedAvatar = FixedAvatarFor(record.role, record.gender, record.employeeGuid);
        if (fixedAvatar != null)
            ApplyFixedAvatar(identity, fixedAvatar);
        else if (_useModularAvatars)
            ApplyModularAvatar(identity);

        // Resume dynamic assignments (e.g. ReceiveInbound) on load. Must happen AFTER modular
        // avatar swaps because some equipment (like the clipboard) anchors to specific hand bones
        // which don't exist until the avatar is applied.
        if (record.hasSavedPosition && record.currentAssignment != EmployeeAssignment.Patrol)
        {
            // The identity already has the record from identity.ApplyRecord above, so Assign
            // will correctly trigger the assignment logic (Equip gun/clipboard, add TaskDriver).
            EmployeeAssignmentService.Assign(identity, record.currentAssignment);
        }

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
        else if (!record.hasSavedPosition)
        {
            // ── EVERY OTHER ROLE STARTS DOING ITS JOB ────────────────────────
            //
            // The MHE roles above got an automatic assignment; nobody else did. A freshly-hired
            // Receiver or Order Selector spawned on Patrol with no task driver attached and simply
            // walked around forever while their queue filled up — the ONLY way to make them work was
            // to find them in the Roster and pick their assignment out of a dropdown by hand.
            //
            // Nothing said so. Observed live: an order sat Pending with an OrderSelect task Available
            // and zero OrderSelectionTaskDriver components anywhere in the scene, because the one
            // Receiver who did work had been assigned by hand at some point and had it persisted.
            //
            // RoleSpecificAssignment is the same map the Roster and Info card already use, so a hire
            // now starts in exactly the state that dropdown would have put them in.
            var roleAssignment = record.role.RoleSpecificAssignment();
            if (roleAssignment.HasValue)
                EmployeeAssignmentService.Assign(identity, roleAssignment.Value);
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

        var slot = MHESlotFinder.FindUnoccupied(targetData);
        if (slot == null)
        {
            // No available MHE found — operator stays on-foot.
            Debug.LogWarning($"[EmployeeSpawner] TryBoardExistingMHE: no unoccupied {role} equipment found — operator spawns on-foot.");
            return false;
        }

        // Found a matching unoccupied vehicle — walk over and board on arrival.
        var nav = identity.GetComponent<AiNavigation>();
        if (nav != null) nav.SeekEquipment(slot);

        identity.Record.currentAssignment = role == EmployeeRole.ReachTruckOperator
            ? EmployeeAssignment.DriveReach
            : EmployeeAssignment.DriveDockstalker;
        return true;
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
        foreach (var slot in FindObjectsByType<MHEOperatorSlot>())
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
        foreach (var wp in FindObjectsByType<Waypoint>())
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
        // InventoryControl used to force the _clerkPrefab base body here regardless of gender
        // (the role was hardcoded female-only). It's gender-flexible now (2026-09-21) — the base
        // body is picked the same way as every other role, and FixedAvatarFor overlays the
        // gender-matched casual look (_icAvatarModel/_icAvatarModelFemale) on top of it.
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

    /// <summary>Dedicated fixed-look FBX for roles that always use the same model — null for
    /// every other role, which then falls through to the random modular avatar (if enabled) or
    /// the default worker mesh.</summary>
private GameObject FixedAvatarFor(EmployeeRole role, EmployeeGender gender, string employeeGuid)
    {
        // Each pool is split by gender so the picked model always matches the employee's actual
        // gender — a female-named hire never ends up with the male body (the bug Tad reported).
        // "Random" means random among that gender's own variants (today just one per gender; room
        // to grow later as more variants get added), never a coin flip on which gender shows up.
        bool female = gender == EmployeeGender.Female;
        GameObject[] pool = role switch
        {
            // HR shares the Boss look (2026-09-21, Tad's ask) — both are office/management staff.
            EmployeeRole.Boss or EmployeeRole.HR
                => PoolOrSingle(female ? _bossAvatarModelPoolFemale : _bossAvatarModelPoolMale, _bossAvatarModel),
            EmployeeRole.Exterminator     => PoolOrSingle(female ? _exterminatorAvatarModelPoolFemale : _exterminatorAvatarModelPoolMale, _exterminatorAvatarModel),
            EmployeeRole.InventoryControl => PoolOrSingle(null, female ? _icAvatarModelFemale : _icAvatarModel),
            EmployeeRole.Security         => PoolOrSingle(female ? _securityAvatarModelPoolFemale : _securityAvatarModelPoolMale, _securityAvatarModel),
            EmployeeRole.TruckDriver      => PoolOrSingle(null, female ? _truckDriverAvatarModelFemale : _truckDriverAvatarModel),
            // Receiver / Reach Truck Operator / Dock Stocker Operator / Order Selector (2026-09-21,
            // Order Selector added same day) — previously fell through to the default branch below
            // with no fixed model assigned, so they rendered as a broken random modular avatar
            // (T-pose). Now use the construction-worker look.
            EmployeeRole.Receiver or EmployeeRole.ReachTruckOperator or EmployeeRole.DockStockerOperator
                or EmployeeRole.OrderSelector
                => PoolOrSingle(null, female ? _floorWorkerAvatarModelFemale : _floorWorkerAvatarModel),
            // Admin uses the reporter look, matching EmployeePhotoBooth's portrait mapping — added
            // 2026-09-22. Previously fell through to the generic default (man_large/woman_large),
            // which disagreed with the portrait and was the actual bug (not the portrait, which was
            // already correctly mapped to reporter — see EmployeePhotoBooth.GetPrefabForRoleAndGender).
            EmployeeRole.Admin => PoolOrSingle(null, female ? _adminAvatarModelFemale : _adminAvatarModel),
            // Every remaining role (Loader, Supervisor, Sanitation placeholders) — still reads
            // as a blend of men and women overall, since the employee population itself is a blend;
            // each individual hire just always matches their own gender now.
            _ => PoolOrSingle(female ? _workerAvatarModelPoolFemale : _workerAvatarModelPoolMale,
                    female ? _workerFemaleAvatarModel : _workerMaleAvatarModel),
        };

        if (pool == null || pool.Length == 0) return null;
        if (pool.Length == 1) return pool[0];

        int seed  = ModularAvatarAssembler.StableSeed(employeeGuid);
        int index = ((seed % pool.Length) + pool.Length) % pool.Length;
        return pool[index];
    }

    /// <summary>Prefers the pool array (random-but-stable pick) when it has entries; otherwise
    /// wraps the single legacy field so existing Inspector wiring keeps working untouched.</summary>
    private static GameObject[] PoolOrSingle(GameObject[] pool, GameObject single)
    {
        if (pool != null && pool.Length > 0) return pool;
        return single != null ? new[] { single } : null;
    }

    // ─── Fixed avatar overlay ───────────────────────────────────────────────────
    // Same overlay technique as ApplyModularAvatar (worker keeps its NavMeshAgent/EmployeeIdentity/
    // scripts; the visual model rides along), but instantiates a fixed, pre-rigged Humanoid FBX
    // instead of randomly-assembled parts — for roles with a dedicated look that should never vary.
    private void ApplyFixedAvatar(EmployeeIdentity identity, GameObject fixedModel)
    {
        if (fixedModel == null) return;

        var workerAnimator = identity.GetComponentInChildren<Animator>(true);

        var avatar = Instantiate(fixedModel);

        // Third-party character packs (e.g. Polyperfect) ship their own locomotion stack on the
        // root — a CharacterController, NavMeshAgent, and wander/AI script — meant for the model
        // to drive itself standalone. Here the avatar is a pure visual overlay riding under the
        // worker's own EmployeeIdentity/NavMeshAgent/AiNavigation, so those components must come
        // off or they'd fight the worker's real movement (double NavMeshAgent, a second collider
        // volume, and a wander script yanking the mesh off in its own direction).
        StripForeignLocomotion(avatar);

        // Hide the worker's own animated mesh — the fixed avatar replaces it visually.
        foreach (var smr in identity.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.enabled = false;

        var t = avatar.transform;
        t.SetParent(identity.transform, worldPositionStays: false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;
        avatar.name = "FixedAvatar";
        SetLayerRecursively(avatar, identity.gameObject.layer);

        foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;

        // The fixed FBX already imported its own Humanoid Avatar (Create From This Model) — keep
        // it, only fall back to the worker's avatar if for some reason it's missing. Always take
        // the worker's controller so the same animation clips drive this model too.
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

    // ─── Overlay cleanup ────────────────────────────────────────────────────────
    /// <summary>Removes locomotion/AI components a third-party character prefab (e.g. Polyperfect)
    /// ships on its own root, which are meant for the model to drive itself standalone and would
    /// otherwise conflict with the worker's own NavMeshAgent/AiNavigation once this is parented on
    /// as a visual-only overlay.</summary>
    private static void StripForeignLocomotion(GameObject avatar)
    {
        // Wander script first — it RequireComponents CharacterController, so Unity refuses to
        // remove the controller while the script is still attached to the same GameObject.
        foreach (var wander in avatar.GetComponentsInChildren<Polyperfect.People.People_WanderScript>(true))
            DestroyImmediate(wander);
        foreach (var cc in avatar.GetComponentsInChildren<CharacterController>(true))
            DestroyImmediate(cc);
        foreach (var nav in avatar.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
            DestroyImmediate(nav);
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
