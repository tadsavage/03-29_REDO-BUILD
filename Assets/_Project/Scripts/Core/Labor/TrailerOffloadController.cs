using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Labor
{
    /// <summary>
    /// CHUNK 2 (Offload) — drives a manned dock stocker through the physical offload of a docked
    /// trailer: for each of the 12 cargo pallets it lines the DS up with the pallet, slides the forks
    /// under it, lifts, backs out onto the dock, spins 180°, drives forks-first to the next open
    /// inbound/both staging-lane slot, sets the pallet down, and files a Putaway work task.
    ///
    /// Self-bootstrapping like the other dock services (no scene wiring). It polls for a docked truck
    /// that's AwaitingOffload plus an idle, manned dock stocker (an MHEOperatorSlot whose operator's
    /// role is DockStockerOperator). When it finds a pair it CLAIMS the truck, COMMANDEERS the DS —
    /// disabling its patrol AiNavigation + NavMeshAgent so this controller can move the transform
    /// directly (scripted choreography, same style as TruckController's yard route) — runs the
    /// sequence, then restores the DS to patrol and tells the truck it may depart.
    ///
    /// The movement geometry is all TUNABLE via the constants at the top. Because this is a hidden
    /// bootstrapped object (no Inspector), those are code constants for now — adjust and recompile to
    /// dial the maneuver in, or promote this to a scene component with [SerializeField]s if live
    /// slider tuning is wanted.
    ///
    /// NOT handled yet (later chunks): the operator walking over to BOARD the DS (assumed already
    /// aboard via the existing hire/auto-board flow), obstacle avoidance on the dock→lane hop
    /// (scripted straight-line movement), and Chunk 3+ actually consuming the Putaway tasks.
    /// </summary>
    public class TrailerOffloadController : MonoBehaviour
    {
        // ── Tunable choreography (code constants — see class summary) ────────────────────────────
        private const float  DriveSpeed         = 3.0f;   // units/sec ground travel
        private const float  TurnSpeed          = 140f;   // deg/sec rotation
        private const float  ArriveThreshold    = 0.15f;  // units — "reached" a drive target
        private const float  FaceThreshold      = 3f;     // deg — "finished" a turn-in-place
        private const string ForkChildName      = "Forks";
        private const float  ForkLiftHeight     = 1.0f;   // loaded fork rise (local Y) — carries the pallet ~1m up
        private const float  ForkLiftSpeed      = 0.6f;   // units/sec fork raise/lower
        private const float  ForkPickupMatchY   = 0f;     // fork local Y that matches the chep pallet's fork-pocket height
        private const float  ForkLowerDistance  = 1.0f;   // start lowering the forks once this close (XZ) to the pallet
        private const float  GrabThreshold      = 0.2f;   // fork grab-point within this XZ of the pallet → seat it on the forks
        private const float  PivotFrontDistance = 2.0f;   // pickup/drop pivot sits this far in FRONT of the trailer opening / lane entry
        // Lane-drop height. PlacementGrid.GetCellCenter returns the grid PLANE Y (~0), but the staging
        // lanes physically sit on the dock foundation, so pallets dropped at the cell's Y sink into the
        // mesh. LaneSurfaceY is that foundation top; the first pallet sits there, each stacked pallet
        // above it by LaneStackStep (= one pallet's height). Assumes lanes are on the standard dock
        // height — if lanes ever sit at other heights this should become a downward raycast instead.
        private const float  LaneSurfaceY       = 1.15f;  // dock foundation top the lanes rest on
        private const float  LaneStackStep      = 0.9f;   // pallet height — vertical offset per stacked pallet in a cell
        private const bool   InvertTrailerAxis  = false;  // flip if the DS drives AWAY from the trailer instead of into it

        // Where the pallet sits ON the forks while carried (local to the Forks transform). The pallet
        // is SNAPPED to this pose on pickup instead of inheriting whatever misalignment the drive left,
        // so it always reads as seated on the tines. Tune these until a carried pallet looks right:
        // typically centered (X≈0), resting on the tines (small Y), pushed forward onto them (Z along
        // the forks' forward axis).
        private static readonly Vector3 ForkCarryLocalPos   = new Vector3(0f, 0f, -0.2f);
        private static readonly Vector3 ForkCarryLocalEuler = Vector3.zero;

        // Which way the forks point in the DS's LOCAL frame. On this model the forks/mast are on the
        // BACK (local -Z), opposite transform.forward (the cab/counterweight). So to aim the forks in a
        // world direction the BODY must face the OPPOSITE way, and "forks-first" travel runs along
        // -transform.forward ("driving backwards" relative to the body — exactly what Tad described).
        // Set to +1f if a forklift model instead has its forks on +Z (transform.forward).
        private const float ForkAxisSign = -1f;

        private static TrailerOffloadController _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[TrailerOffloadController]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TrailerOffloadController>();
        }

        private readonly HashSet<MHEOperatorSlot> _busySlots = new();
        private PlacementGrid _grid;
        private float _nextScan;

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;

            if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
            if (_grid == null) return;
            if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return;
            if (!ServiceLocator.TryGet<WorkQueueSystem>(out var queue) || queue == null) return;

            TruckController truck = null;
            foreach (var t in FindObjectsByType<TruckController>())
                if (t.AwaitingOffload) { truck = t; break; }
            if (truck == null) return;

            var slot = FindAvailableDockStocker();
            if (slot == null) return; // no manned DS free — truck keeps waiting (falls back after its timeout)

            StartCoroutine(OffloadRoutine(truck, slot, inv));
        }

        private MHEOperatorSlot FindAvailableDockStocker()
        {
            foreach (var slot in FindObjectsByType<MHEOperatorSlot>())
            {
                if (!slot.IsOccupied || _busySlots.Contains(slot)) continue;
                var op = slot.CurrentOperator;
                if (op == null || op.Record == null) continue;
                if (op.Record.role != EmployeeRole.DockStockerOperator) continue;
                return slot;
            }
            return null;
        }

        private IEnumerator OffloadRoutine(TruckController truck, MHEOperatorSlot slot, InventoryService inv)
        {
            truck.ClaimForOffload();
            _busySlots.Add(slot);

            Transform ds = slot.transform;

            // ── Commandeer the DS: silence its patrol AI so we own the transform ──
            var nav   = ds.GetComponent<AiNavigation>();
            var agent = ds.GetComponent<NavMeshAgent>();
            bool navWas   = nav   != null && nav.enabled;
            bool agentWas = agent != null && agent.enabled;
            if (agent != null) agent.enabled = false;
            if (nav   != null) nav.enabled   = false;

            Transform forks = FindDeepChild(ds, ForkChildName);
            float forkRestY = forks != null ? forks.localPosition.y : 0f;

            // Cargo pallets, ordered nearest-the-rear-opening first so the DS never drives through one.
            Vector3 into = TrailerIntoDir(truck);
            var pallets = new List<Transform>();
            var load = truck.LoadContainer;
            if (load != null)
                foreach (Transform child in load) pallets.Add(child);
            pallets.Sort((a, b) => Vector3.Dot(a.position, into).CompareTo(Vector3.Dot(b.position, into)));

            // Longitudinal position (projected onto `into`) of the rear-most pallet — a proxy for the
            // trailer opening. The pickup pivots sit PivotFrontDistance in front of this, on the dock.
            float openingLong = pallets.Count > 0 ? Vector3.Dot(pallets[0].position, into) : 0f;

            // Pallets stage in the lanes belonging to THIS truck's dock door, not the first lane globally.
            // doorPos is used to decide which END of a lane is the entry (dock-stocker side, nearest the
            // door) vs the exit (reach-truck side, far end).
            int doorNumber = truck.DockedAt != null ? truck.DockedAt.DoorNumber : -1;
            Vector3 doorPos = truck.DockedAt != null ? truck.DockedAt.transform.position : ds.position;

            Debug.Log($"[TrailerOffload] Offloading {pallets.Count} pallets from {truck.name} (door {doorNumber}) with dock stocker {ds.name}.");

            for (int i = 0; i < pallets.Count; i++)
            {
                var pallet = pallets[i];
                if (pallet == null) continue;
                yield return OffloadOnePallet(ds, forks, forkRestY, truck, pallet, into, openingLong, doorNumber, doorPos, inv, i);
            }

            // ── Restore the DS to patrol ──
            if (forks != null) SetForkLocalY(forks, forkRestY);
            if (agent != null)
            {
                agent.enabled = agentWas;
                if (agent.isActiveAndEnabled) { agent.Warp(ds.position); agent.isStopped = false; }
            }
            if (nav != null)
            {
                nav.enabled = navWas;
                nav.GoToRandomWaypoint();
            }

            _busySlots.Remove(slot);
            truck.CompleteOffload();
            Debug.Log($"[TrailerOffload] {truck.name} fully offloaded — released dock stocker to patrol.");
        }

        private IEnumerator OffloadOnePallet(Transform ds, Transform forks, float forkRestY,
                                             TruckController truck, Transform pallet, Vector3 into,
                                             float openingLong, int doorNumber, Vector3 doorPos,
                                             InventoryService inv, int palletIndex = 0)
        {
            Vector3 P = pallet.position;
            Vector3 palletWorldScale = pallet.lossyScale; // preserve visual size across the reparenting

            // ── PICK UP (pivot-in-front-of-door approach) ─────────────────────────────────
            // The pivot sits PivotFrontDistance in FRONT of the trailer opening, on the same side/row
            // as this pallet (there's effectively one pivot per row — left/right — since all pallets in
            // a row share a lateral offset). The DS pulls up to it, turns on Y until its forks point
            // straight down the row at the pallet, then drives straight in forks-first.
            Vector3 rightAxis = Vector3.Cross(Vector3.up, into);        // horizontal, ⟂ to `into` (unit)
            float   palletLat = Vector3.Dot(P, rightAxis);             // this pallet's row (lateral) offset
            Vector3 pivot = into * (openingLong - PivotFrontDistance)  // longitudinal: in front of the opening
                          + rightAxis * palletLat                      // lateral: on the pallet's row
                          + Vector3.up * ds.position.y;                // keep the DS's drive height

            // 1. Pull up to the pivot at the trailer entry for this pallet's row (left/right).
            yield return DriveTailFirst(ds, pivot);
            // 2. Rotate until the forks (on the DS's back) face straight into the trailer, down the row.
            yield return FaceForks(ds, into);
            // 3. Drive in forks-first: lower the forks to the chep pallet's pocket height once within 1m,
            //    and stop when the forks reach the pallet's XZ (real fork engagement).
            yield return DriveInToGrab(ds, forks, forkRestY, pallet, into);
            // 4. Seat the pallet on the forks (fixed carry pose), then lift it ~1m.
            Transform carrier = forks != null ? forks : ds;
            pallet.SetParent(carrier, worldPositionStays: false);
            pallet.localPosition = ForkCarryLocalPos;
            pallet.localRotation = Quaternion.Euler(ForkCarryLocalEuler);
            if (forks != null) yield return LiftForks(forks, forkRestY + ForkLiftHeight);
            // 5. Reverse straight back out to the pivot in front of the door (clear of the trailer) —
            //    cab-first, forks (and pallet) still pointing into the trailer, no spin.
            yield return DriveTailFirst(ds, pivot);

            // ── DROP OFF (enter the lane from its door-end entry, never across lanes) ──────
            // 7. Pick the next open Inbound/Both slot in one of THIS truck's dock-door lanes.
            if (!TryFindLaneTarget(inv, doorNumber, doorPos, out int door, out string laneLetter, out var cell, out int tier))
            {
                Debug.LogWarning($"[TrailerOffload] No free Inbound/Both staging-lane slot for door {doorNumber} — dropping pallet where the DS stands.");
                DropPallet(pallet, ds.position, 0, palletWorldScale, ds.rotation);
                yield break;
            }

            // Lane geometry: the ENTRY is the lane end nearest the dock DOOR (dock-stocker side); the
            // run direction (downLane) points from there toward the far EXIT end (reach-truck side). The
            // DS enters and exits ONLY through this entry — straight down the lane, forks-first, and
            // straight back out — never laterally across the lanes.
            LaneEntryGeometry(LaneNamingService.GetLane(door, laneLetter), doorPos, out Vector3 entryW, out Vector3 downLane);
            float driveY = ds.position.y;
            Vector3 targetW  = _grid.GetCellCenter(cell);
            Vector3 entryXZ  = new Vector3(entryW.x,  driveY, entryW.z);
            Vector3 targetXZ = new Vector3(targetW.x, driveY, targetW.z);
            Vector3 entryPivot = entryXZ - downLane * PivotFrontDistance;

            // 8. Pull up to a pivot just in front of the lane entry (outside the lane, door side).
            yield return DriveTailFirst(ds, entryPivot);
            // 9. Spin so the forks (and the carried pallet) face straight down the lane — forks FIRST.
            yield return FaceForks(ds, downLane);
            // 10. Drive forward down the lane until the carried pallet's XZ lines up over the target tile
            //     (aim so the PALLET — offset ahead on the forks — lands on the tile, not the DS root).
            Vector3 palletOffset = pallet.position - ds.position; palletOffset.y = 0f;
            yield return DriveForksFirst(ds, targetXZ - palletOffset);
            // 11. Register the pallet to InventoryService FIRST so DropPallet can see it and calculate
            //     correct stacking height. (Must happen before DropPallet so GetPalletsAtLocation finds it.)
            RegisterAndQueue(inv, truck, cell, pallet, palletIndex);

            // 12. Slowly lower the forks to the TOP of whatever's already stacked here — NOT the floor —
            //     then unparent the pallet onto the lane surface. Lowering to forkRestY every time drove
            //     a stacked pallet down through the pallet below it (toward its pivot), and DropPallet
            //     then snapped it back up to LaneSurfaceY + tier*LaneStackStep — that snap read as jank.
            //     tier*LaneStackStep is the stack-top offset and matches DropPallet's final Y, so the
            //     pallet now comes to rest exactly where the forks leave it.
            if (forks != null) yield return LiftForks(forks, forkRestY + tier * LaneStackStep);
            // Rotate pallet 90 degrees additionally so product sits correctly
            Quaternion laneRotation = Quaternion.LookRotation(Flat(downLane));
            Quaternion rotatedPlacement = laneRotation * Quaternion.Euler(0f, 90f, 0f);
            DropPallet(pallet, targetW, tier, palletWorldScale, rotatedPlacement);
            // 13. Reverse straight back OUT of the lane to the entry pivot (never across the lanes) —
            //     cab-first, forks trailing, no spin.
            yield return DriveTailFirst(ds, entryPivot);
        }

        // ── Movement primitives (scripted transform choreography) ────────────────────────────────

        // Drives the DS forks-first toward the pallet (body faces -into so the rear forks point +into)
        // — real fork engagement: once the DS is within ForkLowerDistance of the pallet it eases the
        // forks down to the chep pallet's pocket height (ForkPickupMatchY), and it stops the instant the
        // fork grab-point (where the pallet will seat) reaches the pallet's XZ (within GrabThreshold). A
        // travel cap keeps it from driving through the trailer if the pallet is somehow unreachable.
        private IEnumerator DriveInToGrab(Transform ds, Transform forks, float forkRestY, Transform pallet, Vector3 into)
        {
            Quaternion face = Quaternion.LookRotation(Flat(BodyForwardForForks(into)));
            Vector3 start = ds.position;
            const float maxTravel = 8f;

            while (true)
            {
                Vector3 grab = forks != null ? forks.TransformPoint(ForkCarryLocalPos) : ds.position + into;
                Vector3 gd = pallet.position - grab; gd.y = 0f;
                if (gd.magnitude <= GrabThreshold) break;

                Vector3 dsToP = pallet.position - ds.position; dsToP.y = 0f;
                if (forks != null && dsToP.magnitude <= ForkLowerDistance)
                {
                    Vector3 lp = forks.localPosition;
                    lp.y = Mathf.MoveTowards(lp.y, forkRestY + ForkPickupMatchY, ForkLiftSpeed * Time.deltaTime);
                    forks.localPosition = lp;
                }

                ds.rotation  = Quaternion.RotateTowards(ds.rotation, face, TurnSpeed * Time.deltaTime);
                ds.position += into * (DriveSpeed * Time.deltaTime);

                if ((ds.position - start).magnitude >= maxTravel)
                {
                    Debug.LogWarning("[TrailerOffload] DriveInToGrab hit the travel cap before reaching the pallet — seating it anyway.");
                    break;
                }
                yield return null;
            }
        }

        // Drive to a flat target with the FORKS leading (load-first) — the body turns so its rear forks
        // point along the travel direction. Used to drive a carried pallet down a lane onto its tile.
        private IEnumerator DriveForksFirst(Transform t, Vector3 target) => DriveInternal(t, target, forksLead: true);

        // Drive to a flat target with the CAB leading (forks trailing) — normal transit, and backing a
        // grabbed pallet straight out of the trailer/lane: the forks keep pointing where they came from,
        // no 180° spin. Body faces the direction of travel.
        private IEnumerator DriveTailFirst(Transform t, Vector3 target) => DriveInternal(t, target, forksLead: false);

        // Drives the DS toward a flat target each frame. forksLead=true → body faces so the rear forks
        // point along travel (BodyForwardForForks); forksLead=false → body faces travel directly, forks
        // trail behind. Explicit square-ups (fork insertion, the 180° spin) are done via FaceForks().
        private IEnumerator DriveInternal(Transform t, Vector3 target, bool forksLead)
        {
            while (true)
            {
                Vector3 flat = new Vector3(target.x, t.position.y, target.z);
                Vector3 to = flat - t.position; to.y = 0f;
                if (to.magnitude <= ArriveThreshold) { t.position = flat; break; }
                Vector3 travel = Flat(to);
                Vector3 bodyFwd = forksLead ? BodyForwardForForks(travel) : travel;
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(bodyFwd), TurnSpeed * Time.deltaTime);
                t.position = Vector3.MoveTowards(t.position, flat, DriveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator FaceDir(Transform t, Vector3 dir)
        {
            Quaternion want = Quaternion.LookRotation(Flat(dir));
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                yield return null;
            }
            t.rotation = want;
        }

        // Rotate in place so the FORKS point along worldForkDir (the body faces the opposite way on a
        // rear-fork model). Use whenever the forklift must "aim its forks" — squaring up to a pallet in
        // the trailer or squaring up to a staging lane.
        private IEnumerator FaceForks(Transform t, Vector3 worldForkDir) => FaceDir(t, BodyForwardForForks(worldForkDir));

        // World direction the body's transform.forward must point so the FORKS aim along worldForkDir.
        private static Vector3 BodyForwardForForks(Vector3 worldForkDir) => worldForkDir * ForkAxisSign;

        private IEnumerator LiftForks(Transform forks, float targetLocalY)
        {
            Vector3 lp = forks.localPosition;
            while (Mathf.Abs(lp.y - targetLocalY) > 0.001f)
            {
                lp.y = Mathf.MoveTowards(lp.y, targetLocalY, ForkLiftSpeed * Time.deltaTime);
                forks.localPosition = lp;
                yield return null;
            }
        }

        private static void SetForkLocalY(Transform forks, float y)
        {
            Vector3 lp = forks.localPosition; lp.y = y; forks.localPosition = lp;
        }

        private void DropPallet(Transform pallet, Vector3 laneWorld, int tier, Vector3 worldScale, Quaternion rotation)
        {
            pallet.SetParent(null, worldPositionStays: true);

            // Calculate target Y: For tier 0, sit on the dock surface. For tier > 0, find the
            // actual height of the pallet already in this cell and stack on top of it.
            Vector2Int cell = _grid.WorldToCell(laneWorld);
            float targetY = LaneSurfaceY;

            if (tier > 0 && ServiceLocator.TryGet<InventoryService>(out var inv) && inv != null)
            {
                // Find the highest pallet already registered in this cell
                var palletsInCell = inv.GetPalletsAtLocation(cell);
                float maxHeightInCell = 0f;

                foreach (var palletData in palletsInCell)
                {
                    // Find the pallet's GameObject to measure its actual height
                    var link = PalletMasterLink.Find(palletData.PalletId);
                    var palletGO = link?.gameObject;
                    if (palletGO != null)
                    {
                        // Measure actual height from mesh bounds
                        Bounds? meshBounds = null;
                        var renderers = palletGO.GetComponentsInChildren<MeshRenderer>();
                        if (renderers.Length > 0)
                        {
                            meshBounds = renderers[0].bounds;
                            foreach (var r in renderers)
                                if (r.bounds.max.y > meshBounds.Value.max.y)
                                    meshBounds = r.bounds;
                        }

                        float palletTop = meshBounds?.max.y ??
                                         CalculatePalletHeightFromChildren(palletGO);

                        if (palletTop > maxHeightInCell)
                            maxHeightInCell = palletTop;
                    }
                }

                // Stack on top of the highest pallet found
                targetY = maxHeightInCell > 0f ? maxHeightInCell + 0.01f : LaneSurfaceY;
            }

            pallet.position = new Vector3(laneWorld.x, targetY, laneWorld.z);
            pallet.localScale = worldScale; // parent is null now, so local == world scale

            // NOTE: Cases are already rotated 90 degrees in TruckController when built in the trailer,
            // so we don't need to rotate them again here. They arrive at the staging lane correctly oriented.
            // (Previously we rotated here, but that caused jank — the trailer rotation fix handles it now.)

            // Fix case Y positioning: PalletBuilder positioned cases with gaps during build.
            // Now that the pallet is at its final position, move cases to sit directly on the pallet
            // surface (local Y = 0.16m, the pallet deck height).
            var palletLoad = pallet.Find("PalletLoad");
            if (palletLoad != null)
            {
                const float palletDeckHeight = 0.16f;

                // Collect all case positions and find the minimum Y to determine layer offset
                float minCaseY = float.MaxValue;
                var cases = new List<Transform>();
                for (int i = 0; i < palletLoad.childCount; i++)
                {
                    var child = palletLoad.GetChild(i);
                    cases.Add(child);
                    if (child.localPosition.y < minCaseY)
                        minCaseY = child.localPosition.y;
                }

                // If cases exist, adjust them so the lowest layer sits on the pallet deck
                if (cases.Count > 0 && minCaseY != float.MaxValue)
                {
                    float yOffset = minCaseY - palletDeckHeight;
                    foreach (var caseTransform in cases)
                    {
                        var pos = caseTransform.localPosition;
                        pos.y -= yOffset;  // Shift all cases down so minimum Y = pallet deck height
                        caseTransform.localPosition = pos;
                    }
                }
            }

            // Now that the pallet is staged (unparented from the forks) it's a static obstacle other
            // agents should path around — turn its NavMesh Obstacle back on. (Left off while carried so
            // it didn't carve the navmesh as it rode along on the forks.)
            var obstacle = pallet.GetComponentInChildren<NavMeshObstacle>(true);
            if (obstacle != null) obstacle.enabled = true;
        }

        /// <summary>
        /// Calculate pallet's actual height by measuring all child renderers' world-space bounds.
        /// Returns the maximum Y coordinate (top of the tallest child).
        /// </summary>
        private static float CalculatePalletHeightFromChildren(GameObject pallet)
        {
            float maxY = 0f;
            var renderers = pallet.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (r.bounds.max.y > maxY)
                    maxY = r.bounds.max.y;
            }
            return maxY > 0f ? maxY : 0f;
        }

        // ── Lane targeting + inventory/task creation ─────────────────────────────────────────────

        // Finds the next open staging slot, restricted to the lanes owned by the truck's dock DOOR
        // (so a truck at door 2 stages in door 2's lanes, not the globally-first lane). Within a lane it
        // fills from the FAR end (the reach-truck/exit end) back toward the door (the dock-stocker/entry
        // end): the lanes are FIFO flow-through — first pallet in ends up deepest so the reach truck at
        // the far end picks it first. Returns the lane identity too so the drop maneuver can route in via
        // the entry. doorNumber <= 0 falls back to any door.
        private bool TryFindLaneTarget(InventoryService inv, int doorNumber, Vector3 doorPos,
                                       out int door, out string laneLetter, out Vector2Int cell, out int tier)
        {
            door = 0; laneLetter = null; cell = default; tier = 0;
            foreach (var (d, lane) in LaneNamingService.AllLanes())
            {
                if (doorNumber > 0 && d != doorNumber) continue; // only this truck's dock-door lanes
                if (!inv.LaneAcceptsPutaway(d, lane)) continue;  // Inbound or Both only

                int maxH = LaneConfigRegistry.Get(d, lane).MaxStackHeight;
                var slots = LaneNamingService.GetLane(d, lane);
                if (slots.Count == 0) continue;

                // FIFO flow-through: fill from the EXIT end (far from the door) back toward the ENTRY end
                // (nearest the door) so the reach truck at the exit picks the first-placed pallet first.
                // Slot order in `slots` may run either way relative to the door, so order by distance
                // from the door DESCENDING (far first).
                var byFar = slots.OrderByDescending(s => (_grid.GetCellCenter(s.Cell) - doorPos).sqrMagnitude);
                foreach (var s in byFar)
                {
                    int stacked = inv.GetPalletsAtLocation(s.Cell).Count;
                    if (stacked < maxH)
                    {
                        door = d; laneLetter = lane; cell = s.Cell; tier = stacked;
                        return true;
                    }
                }
            }
            return false;
        }

        // Resolves a lane's ENTRY (the end nearest the dock door — where the dock stocker drives in) and
        // its down-lane direction (from the entry toward the far exit end). LaneNamingService numbers
        // slots "out from the dock wall," which is NOT necessarily the door end, so we pick by distance.
        private void LaneEntryGeometry(List<LaneNamingService.LaneSlot> slots, Vector3 doorPos,
                                       out Vector3 entryW, out Vector3 downLane)
        {
            Vector3 end0 = _grid.GetCellCenter(slots[0].Cell);
            Vector3 endN = _grid.GetCellCenter(slots[slots.Count - 1].Cell);
            bool zeroIsEntry = (end0 - doorPos).sqrMagnitude <= (endN - doorPos).sqrMagnitude;
            entryW = zeroIsEntry ? end0 : endN;
            Vector3 exitW = zeroIsEntry ? endN : end0;
            downLane = slots.Count > 1 ? Flat(exitW - entryW) : Flat(entryW - doorPos);
        }

        private void RegisterAndQueue(InventoryService inv, TruckController truck, Vector2Int cell, Transform pallet, int palletIndex = 0)
        {
            string sku = "PHYS";
            if (truck.AssignedShipment != null && truck.AssignedShipment.LineItems.Count > palletIndex)
            {
                sku = truck.AssignedShipment.LineItems[palletIndex].SkuId;
            }

            var data = inv.RegisterPhysicalPallet(cell, sku, 1); // dummy SKU = 1 case/pallet

            // LoadId is a "license plate" assigned AT RECEIVING (ReceiverReceivingWorkflow), not here —
            // this pallet is still ghosted/unreceived the moment it lands in the lane.
            PalletMasterLink.Attach(pallet.gameObject, data.PalletId);

            // ── Snapshot the pallet's BASE position (not case-top) into BOTH systems ──
            // worldSpaceYHeight must be the PALLET BASE Y, not the pallet+cases Y. This is critical
            // for save/load: when we restore from JSON, we place the pallet at this Y, then the cases
            // rebuild as children at their correct local offsets. If worldSpaceYHeight includes cases,
            // the pallet will float in air on load.
            float palletBaseY = CalculatePalletBaseY(cell);

            // Store in PalletMasterRecord (for save/load via InventoryService)
            data.WorldHeightY = palletBaseY;

            // Store in PlacedObject (for PlacementSystem / PalletPersistenceService).
            // PlacedObject was only DISABLED (not destroyed) when this pallet became cargo in
            // TruckController.BuildOnePallet, so `.data` (the ObjDataSO identity) survived the
            // whole truck ride intact — re-enabling it here re-registers it with
            // PlacedObjectRegistry now that it finally has a real grid cell. Do NOT write the
            // pallet GUID into `customData` — that field belongs to PalletBuilder's own
            // SaveBuildState()/LoadBuildState() JSON (case prefab id + Ti/Hi); stomping it here was
            // silently corrupting that state. The inventory link is carried separately via
            // PalletMasterLink (attached above), which PalletPersistenceService reads directly.
            var placed = pallet.gameObject.GetComponent<PlacedObject>();
            if (placed != null)
            {
                placed.gridX = cell.x;
                placed.gridY = cell.y;
                placed.worldSpaceYHeight = palletBaseY;
                placed.enabled = true;

                // BuildingData was destroyed while this pallet was cargo — recreate it now that
                // the pallet has a real cell/rotation, same as PlacementSystem.SpawnFromSave does.
                var bd = pallet.gameObject.GetComponent<BuildingData>();
                if (bd == null) bd = pallet.gameObject.AddComponent<BuildingData>();
                if (placed.data != null)
                {
                    float rotDeg = pallet.eulerAngles.y;
                    Vector2Int[] offsets = placed.data.GetFootprintOffsets(-rotDeg);
                    bd.Initialize(cell, rotDeg, offsets, placed.data);
                }

                Debug.Log($"[TrailerOffload] Registered pallet {data.PalletId} at grid cell ({cell.x}, {cell.y}), palletBaseY={palletBaseY:F3}");
            }
            else
            {
                Debug.LogWarning($"[TrailerOffload] Pallet GameObject has no PlacedObject component! Grid position not recorded.");
            }

            ReceivingService.CreateReceiveTaskForPallet(data);
        }

        /// <summary>
        /// Calculate the pallet's BASE Y position (not including cases) for this grid cell.
        /// Looks at what's already in the cell via PlacementGrid and stacks on top if needed.
        /// </summary>
        private float CalculatePalletBaseY(Vector2Int cell)
        {
            const float PALLET_HEIGHT = 0.165f;
            const float DOCK_SURFACE_Y = 1.15f;

            if (_grid == null) return DOCK_SURFACE_Y;

            // Get all objects currently in this cell from the PlacementGrid
            var objectsInCell = _grid.GetObjectsInCell(cell);
            if (objectsInCell == null || objectsInCell.Count == 0)
                return DOCK_SURFACE_Y; // Empty cell: pallet sits on dock surface

            // Find the highest object's top Y by checking bounds of all renderers
            float maxTopY = DOCK_SURFACE_Y;
            foreach (var gridObj in objectsInCell)
            {
                if (gridObj.instance == null) continue;

                // Measure the actual height of this object via its renderers
                var renderers = gridObj.instance.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length > 0)
                {
                    foreach (var r in renderers)
                    {
                        if (r.bounds.max.y > maxTopY)
                            maxTopY = r.bounds.max.y;
                    }
                }
            }

            // Stack this pallet's base on top of the highest object + pallet height
            return maxTopY + PALLET_HEIGHT;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────────────────

        private static Vector3 TrailerIntoDir(TruckController truck)
        {
            // Truck backs into the dock: cab (transform.forward) points out to the yard, so the trailer
            // interior extends along +forward from the rear opening at the dock. The DS drives that way
            // to reach the pallets. Flip InvertTrailerAxis if the model's forward is reversed.
            Vector3 f = Flat(truck.transform.forward);
            return InvertTrailerAxis ? -f : f;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

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
    }
}
