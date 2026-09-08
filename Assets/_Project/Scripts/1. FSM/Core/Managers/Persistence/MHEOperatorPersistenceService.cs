using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;
using GameCore.Labor;
using GameCore.Actors;

namespace GameCore.Persistence
{
    /// <summary>
    /// Handles save/load persistence for MHE operator↔vehicle boarding and any pallet physically
    /// riding an operator's forks/carry-anchor at save time.
    ///
    /// Two things a plain save/load previously lost completely:
    /// 1. Who was boarded on which vehicle — EmployeeIdentity.AssignedSlot is runtime-only, so a
    ///    reload always dropped every Reach Truck / Dock Stocker operator back to free-roaming.
    /// 2. A pallet mid-carry — riding the forks isn't a normal grid placement (its PlacedObject is
    ///    disabled or, for a Dock Stocker mid-offload, not even registered yet — see
    ///    TrailerOffloadController.RegisterAndQueue's comment), so it fell through every other
    ///    persistence path and simply vanished on reload while the vehicle respawned with empty forks.
    ///
    /// Capture rule for WHICH carried pallets belong here vs. TruckPersistenceService's existing
    /// fork-pallet fold-back: a pallet only gets captured by this service once it has a real
    /// PalletMasterLink (i.e. InventoryService already knows about it — true for the entire Reach
    /// Truck putaway carry, and true for a Dock Stocker carry only in the brief window after
    /// TrailerOffloadController.RegisterAndQueue runs). Raw, still-unreceived trailer cargo (no
    /// PalletMasterLink yet) is TruckPersistenceService's job — see its BuildForkPalletMap, which
    /// now explicitly excludes anything this service would already claim.
    ///
    /// Restore order (see PlacementSystem.ApplySaveData): RestoreBoarding must run AFTER employees
    /// and vehicles both exist (so AssignedSlot lookups resolve) but is otherwise independent;
    /// RestoreCarriedPallets must run AFTER RestoreBoarding (needs AssignedSlot) and after
    /// WorkQueueSystem's tasks are restored (needs to find the matching Assigned WorkTask to hand
    /// off to ReachTruckOperator.ResumeDeliverToRack).
    /// </summary>
    public static class MHEOperatorPersistenceService
    {
        // ── Capture ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans every currently-boarded operator for a pallet riding their vehicle's forks/anchor.
        /// Call this from PlacementSystem.BuildSaveData(). Boarding itself (who's on what vehicle)
        /// is captured separately, per-employee, onto EmployeeRecord — see BuildSaveData's own loop.
        /// </summary>
        public static List<CarriedPalletSnapshot> CaptureAll()
        {
            var result = new List<CarriedPalletSnapshot>();
            if (EmployeeRegistry.Instance == null) return result;

            ServiceLocator.TryGet<InventoryService>(out var inv);

            foreach (var identity in EmployeeRegistry.Instance.All)
            {
                if (identity == null || identity.AssignedSlot == null) continue;

                var link = identity.AssignedSlot.GetComponentInChildren<PalletMasterLink>();
                if (link == null || string.IsNullOrEmpty(link.PalletId)) continue;

                var rec = inv?.GetPallet(link.PalletId);
                var placed  = link.GetComponent<PlacedObject>();
                var builder = link.GetComponentInChildren<PalletBuilder>();
                var pdata   = link.GetComponent<PalletData>();

                var snap = new CarriedPalletSnapshot
                {
                    employeeGuid      = identity.Record?.employeeGuid ?? "",
                    carrierAnchorName = link.transform.parent != null ? link.transform.parent.name : "",
                    localPosition     = link.transform.localPosition,
                    localRotation     = link.transform.localRotation,
                    objDataId         = placed != null && placed.data != null ? placed.data.id : -1,
                    inventoryPalletId = link.PalletId,
                    loadId            = pdata != null && !string.IsNullOrEmpty(pdata.LoadId) 
                                         ? pdata.LoadId 
                                         : (rec != null ? (rec.LoadId ?? "") : ""),
                    skuId             = pdata != null && !string.IsNullOrEmpty(pdata.ItemNumber)
                                         ? pdata.ItemNumber
                                         : (builder?.linkedSku != null ? builder.linkedSku.SkuId : (rec?.SkuId ?? "")),
                    capturedTi        = builder != null ? builder.manualTi : 0,
                    capturedHi        = builder != null ? builder.manualHi : 0,
                };

                var palletLoad = link.transform.Find("PalletLoad");
                if (palletLoad != null)
                {
                    for (int i = 0; i < palletLoad.childCount; i++)
                    {
                        snap.caseLocalPositions.Add(palletLoad.GetChild(i).localPosition);
                        snap.caseLocalRotations.Add(palletLoad.GetChild(i).localRotation);
                    }
                }

                result.Add(snap);
                Debug.Log($"[MHEOperatorPersistenceService] Captured carried pallet '{link.PalletId}' on operator {snap.employeeGuid}'s {snap.carrierAnchorName}.");
            }

            return result;
        }

        // ── Restore: boarding ────────────────────────────────────────────────────

        /// <summary>
        /// Re-boards every operator who was riding an MHE at save time. Must run after both
        /// employees and vehicles have been spawned. Vehicles are matched by grid cell (stable
        /// across save/load since they're restored to the exact same cell by the normal
        /// placedObjects pipeline).
        /// </summary>
        public static void RestoreBoarding(List<EmployeeRecord> records)
        {
            if (records == null || records.Count == 0) return;
            if (EmployeeRegistry.Instance == null) return;

            foreach (var record in records)
            {
                if (record == null || !record.hasBoardedVehicle) continue;

                var identity = EmployeeRegistry.Instance.GetByGuid(record.employeeGuid);
                if (identity == null)
                {
                    Debug.LogWarning($"[MHEOperatorPersistenceService] No spawned identity for {record.employeeGuid} — cannot re-board.");
                    continue;
                }

                Vector3 savedVehiclePos = new Vector3(record.boardedVehicleWorldX, record.boardedVehicleWorldY, record.boardedVehicleWorldZ);
                var vehiclePo = PlacedObjectRegistry.All
                    .Where(po => po != null && po.GetComponent<MHEOperatorSlot>() != null && !po.GetComponent<MHEOperatorSlot>().IsOccupied)
                    .OrderBy(po => Vector3.Distance(po.transform.position, savedVehiclePos))
                    .FirstOrDefault();

                // Fallback to grid cell if distance is too far or world position wasn't saved (older saves)
                if (vehiclePo == null || Vector3.Distance(vehiclePo.transform.position, savedVehiclePos) > 1f)
                {
                    vehiclePo = PlacedObjectRegistry.All.FirstOrDefault(po =>
                        po != null && po.gridX == record.boardedVehicleGridX && po.gridY == record.boardedVehicleGridY
                        && po.GetComponent<MHEOperatorSlot>() != null && !po.GetComponent<MHEOperatorSlot>().IsOccupied);
                }

                var slot = vehiclePo != null ? vehiclePo.GetComponent<MHEOperatorSlot>() : null;
                if (slot == null)
                {
                    Debug.LogWarning($"[MHEOperatorPersistenceService] No MHE found at ({record.boardedVehicleGridX},{record.boardedVehicleGridY}) for {record.employeeGuid} — operator resumes on-foot.");
                    continue;
                }

                slot.AssignOperator(identity);
                Debug.Log($"[MHEOperatorPersistenceService] Re-boarded {record.employeeName} onto {vehiclePo.name}.");
            }
        }

        // ── Restore: carried pallets + task resume ──────────────────────────────

        /// <summary>
        /// Rebuilds every pallet that was mid-carry at save time and hands each one back to its
        /// operator to finish the job. Must run AFTER RestoreBoarding and after WorkQueueSystem's
        /// tasks have been restored (RegisterRestoredTask). Also resumes any re-boarded Reach Truck
        /// Operator whose task was Assigned but who hadn't picked up the pallet yet at save time
        /// (no carried-pallet snapshot exists for them — ToLocation is still null) by restarting
        /// PutawayRoutine for their exact task, same as a fresh claim would.
        /// </summary>
        public static void RestoreCarriedPallets(List<CarriedPalletSnapshot> snapshots, PlacementGrid grid)
        {
            ResumeUnstartedTasks(snapshots);

            if (snapshots == null || snapshots.Count == 0) return;
            if (EmployeeRegistry.Instance == null) return;

            var registry = FindRegistry();
            if (registry == null)
            {
                Debug.LogError("[MHEOperatorPersistenceService] No ObjDataRegistry found — cannot restore carried pallets.");
                return;
            }

            ServiceLocator.TryGet<InventoryService>(out var inv);
            ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue);

            foreach (var snap in snapshots)
            {
                if (snap == null || string.IsNullOrEmpty(snap.employeeGuid)) continue;

                var identity = EmployeeRegistry.Instance.GetByGuid(snap.employeeGuid);
                if (identity == null || identity.AssignedSlot == null)
                {
                    Debug.LogWarning($"[MHEOperatorPersistenceService] Operator {snap.employeeGuid} isn't boarded — carried pallet '{snap.inventoryPalletId}' dropped.");
                    continue;
                }

                var so = registry.GetByID(snap.objDataId);
                if (so == null || so.prefab == null)
                {
                    Debug.LogWarning($"[MHEOperatorPersistenceService] Unknown pallet objDataId={snap.objDataId} — skipping carried pallet '{snap.inventoryPalletId}'.");
                    continue;
                }

                Transform vehicleRoot = identity.AssignedSlot.transform;
                Transform anchor = !string.IsNullOrEmpty(snap.carrierAnchorName)
                    ? FindDeepChild(vehicleRoot, snap.carrierAnchorName)
                    : null;
                if (anchor == null) anchor = vehicleRoot;

                var sku = inv?.GetSkuData(snap.skuId);

                GameObject go = Object.Instantiate(so.prefab);
                go.name = so.objName;
                go.transform.SetParent(anchor, worldPositionStays: false);
                go.transform.localPosition = snap.localPosition;
                go.transform.localRotation = snap.localRotation;

                // Cargo-in-transit, same as TruckController.BuildOnePallet: keep PlacedObject
                // disabled (unregistered) while it rides — not a real grid placement yet.
                var po = go.GetComponent<PlacedObject>();
                if (po != null) po.enabled = false;
                var bd = go.GetComponent<BuildingData>();
                if (bd != null) Object.Destroy(bd);

                var builder = go.GetComponentInChildren<PalletBuilder>();
                GameObject casePrefab = sku?.Prefab;

                if (builder != null && casePrefab != null && snap.caseLocalPositions.Count > 0)
                {
                    var existing = go.transform.Find("PalletLoad");
                    if (existing != null) Object.Destroy(existing.gameObject);

                    var loadObj = new GameObject("PalletLoad");
                    loadObj.transform.SetParent(go.transform, false);
                    loadObj.transform.localPosition = Vector3.zero;
                    loadObj.transform.localRotation = Quaternion.identity;

                    int count = Mathf.Min(snap.caseLocalPositions.Count, snap.caseLocalRotations.Count);
                    for (int i = 0; i < count; i++)
                    {
                        var caseGO = Object.Instantiate(casePrefab, loadObj.transform);
                        caseGO.transform.localPosition = snap.caseLocalPositions[i];
                        caseGO.transform.localRotation = snap.caseLocalRotations[i];

                        var casePo = caseGO.GetComponent<PlacedObject>();
                        if (casePo != null) { casePo.enabled = false; Object.Destroy(casePo); }
                        var caseBd = caseGO.GetComponent<BuildingData>();
                        if (caseBd != null) Object.Destroy(caseBd);
                    }

                    builder.casePrefab = casePrefab;
                    builder.linkedSku  = sku;
                    if (snap.capturedTi > 0 && snap.capturedHi > 0)
                    {
                        builder.useTiHiOverride = true;
                        builder.manualTi = snap.capturedTi;
                        builder.manualHi = snap.capturedHi;
                    }
                }

                // Re-link InventoryService identity — this pallet was already received/registered
                // at save time, so restore the same link rather than treating it as fresh cargo.
                PalletMasterLink.Attach(go, snap.inventoryPalletId);
                var record = inv?.GetAllPallets().FirstOrDefault(p => p.PalletId == snap.inventoryPalletId);

                string loadId = snap.loadId ?? "";
                if (string.IsNullOrEmpty(loadId) && record != null && !string.IsNullOrEmpty(record.LoadId))
                {
                    loadId = record.LoadId;
                }

                // GHOST VS SOLID RESTORATION (2026-07-10)
                if (string.IsNullOrEmpty(loadId))
                {
                    if (builder != null)
                    {
                        var ghostMat = Resources.Load<Material>("Materials/GhostLoweredWall");
                        if (ghostMat != null)
                            builder.GhostCases(ghostMat);
                    }
                    
                    // Ghosted pallets must not have PalletData
                    var staleData = go.GetComponent<PalletData>();
                    if (staleData != null) Object.Destroy(staleData);
                }
                else
                {
                    var pdata = go.GetComponent<PalletData>();
                    if (pdata == null) pdata = go.AddComponent<PalletData>();
                    
                    var area = sku != null ? sku.StorageArea : PalletData.AreaCategory.Grocery;
                    int qty  = record != null ? record.Quantity : 0;
                    int exp  = record != null ? record.ExpirationDayNumber : -1;
                    
                    pdata.Initialize(loadId, snap.skuId ?? "", qty, exp, area, sku?.Icon,
                        record?.CurrentLocation ?? Vector2Int.zero);

                    if (!string.IsNullOrEmpty(loadId))
                        LoadIDGenerator.Seed(loadId);
                }

                // Not a nav obstacle while carried — matches the normal pickup sequence.
                var obstacle = go.GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
                if (obstacle != null) obstacle.enabled = false;

                // Hand off to the operator to finish the job.
                var rto = identity.AssignedSlot.GetComponent<ReachTruckOperator>();
                WorkTask task = workQueue?.Tasks.FirstOrDefault(t =>
                    t.AssignedToEmployeeGuid == snap.employeeGuid && t.Status == WorkTaskStatus.Assigned);

                if (rto != null && task != null && !string.IsNullOrEmpty(task.ToLocation))
                {
                    rto.ResumeDeliverToRack(task, go.transform);
                    Debug.Log($"[MHEOperatorPersistenceService] Resuming putaway for '{snap.inventoryPalletId}' → {task.ToLocation}.");
                }
                else if (record != null && grid != null)
                {
                    // No per-vehicle resume script to hand off to (the rare Dock Stocker
                    // post-registration edge case) — the pallet already has a real InventoryService
                    // location, so just stage it there directly rather than leaving it stuck on
                    // the forks with nothing driving it the rest of the way.
                    go.transform.SetParent(null, worldPositionStays: true);
                    Vector3 worldPos = grid.GetCellCenter(record.CurrentLocation);
                    worldPos.y = record.WorldHeightY > 0f ? record.WorldHeightY : worldPos.y;
                    go.transform.position = worldPos;
                    if (obstacle != null) obstacle.enabled = true;
                    if (po != null)
                    {
                        po.enabled = true;
                        PlacedObjectRegistry.Register(po);
                    }
                    Debug.Log($"[MHEOperatorPersistenceService] No resumable task for '{snap.inventoryPalletId}' — staged directly at {record.CurrentLocation}.");
                }
                else
                {
                    Debug.LogWarning($"[MHEOperatorPersistenceService] Carried pallet '{snap.inventoryPalletId}' restored but nothing could resume it — left on operator's forks.");
                }
            }
        }

        /// <summary>
        /// Resumes any re-boarded Reach Truck Operator whose task is Assigned but who has no
        /// carried-pallet snapshot (meaning they hadn't reached the pallet yet at save time).
        /// </summary>
        private static void ResumeUnstartedTasks(List<CarriedPalletSnapshot> carriedSnapshots)
        {
            if (EmployeeRegistry.Instance == null) return;
            if (!ServiceLocator.TryGet<WorkQueueSystem>(out var workQueue) || workQueue == null) return;

            var carriedGuids = new HashSet<string>(
                (carriedSnapshots ?? new List<CarriedPalletSnapshot>())
                    .Where(s => s != null && !string.IsNullOrEmpty(s.employeeGuid))
                    .Select(s => s.employeeGuid));

            foreach (var task in workQueue.Tasks)
            {
                if (task.Status != WorkTaskStatus.Assigned) continue;
                if (task.RequiredRole != EmployeeRole.ReachTruckOperator) continue;
                if (string.IsNullOrEmpty(task.AssignedToEmployeeGuid)) continue;
                if (carriedGuids.Contains(task.AssignedToEmployeeGuid)) continue; // handled by RestoreCarriedPallets instead

                var identity = EmployeeRegistry.Instance.GetByGuid(task.AssignedToEmployeeGuid);
                var rto = identity?.AssignedSlot?.GetComponent<ReachTruckOperator>();
                if (rto == null)
                {
                    Debug.LogWarning($"[MHEOperatorPersistenceService] Assigned task {task.TaskId} but operator {task.AssignedToEmployeeGuid} isn't boarded on a Reach Truck — task left Assigned.");
                    continue;
                }

                rto.ResumeTask(task);
                Debug.Log($"[MHEOperatorPersistenceService] Resuming unstarted putaway task {task.TaskId} for pallet {task.PalletId}.");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform c in parent)
            {
                if (c.name == childName) return c;
                var r = FindDeepChild(c, childName);
                if (r != null) return r;
            }
            return null;
        }

        private static ObjDataRegistry FindRegistry()
        {
            var buildMenu = Object.FindAnyObjectByType<BuildMenuUI>();
            if (buildMenu != null && buildMenu.registry != null) return buildMenu.registry;

            var all = Resources.FindObjectsOfTypeAll<ObjDataRegistry>();
            return all.Length > 0 ? all[0] : null;
        }
    }
}
