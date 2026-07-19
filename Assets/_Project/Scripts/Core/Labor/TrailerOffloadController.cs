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
        private const float  LaneStackStep      = 0.9f;   // pallet height — cosmetic fork-lower offset per stacked tier
        private const float  StackGap           = 0.02f;  // small anti-clip gap between a stacked pallet's base and the case-top below it
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
        private static readonly Dictionary<Vector2Int, int> _pendingDrops = new();

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

            // Cargo pallets, ordered so the DS (a) never drives through a still-loaded stack to reach a
            // deeper one, and (b) NEVER grabs a bottom pallet while another is resting on top of it.
            Vector3 into = TrailerIntoDir(truck);
            var pallets = new List<Transform>();
            var load = truck.LoadContainer;
            if (load != null)
                foreach (Transform child in load) pallets.Add(child);
            pallets.Sort((a, b) =>
            {
                float la = Vector3.Dot(a.position, into);
                float lb = Vector3.Dot(b.position, into);
                // Different columns (depth into the trailer differs meaningfully) → nearest the rear
                // opening first, so the DS clears near stacks before driving deeper.
                if (Mathf.Abs(la - lb) > 0.25f) return la.CompareTo(lb);
                // Same column — stacked pallets share XZ, differ only in height. Take the TOP one FIRST.
                // Grabbing the bottom first is what left the upper pallet "hanging" mid-air.
                return b.position.y.CompareTo(a.position.y);
            });

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
                DropPallet(pallet, ds.position, LaneSurfaceY, palletWorldScale, ds.rotation);
                
                // CRITICAL: Even if it's not in a lane, it must be registered to the inventory service
                // so the Receiver can find it, and it must have a CurrentLocation (even if it's (0,0) or stand-still).
                // We use (0,0) as the "no slot" fallback.
                RegisterAndQueue(inv, truck, Vector2Int.zero, pallet, ds.rotation, palletIndex);
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
            // 11. Register the pallet to InventoryService FIRST so it has a master record + link, THEN
            //     compute where it lands. dropBaseY is derived from the pallets ALREADY SETTLED in this
            //     cell, explicitly EXCLUDING this pallet (which is still up on the forks) — see
            //     ComputeDropBaseY. This one value is the single source of truth: it positions the pallet
            //     AND is recorded as its saved height, so the visual and the record can never disagree.
            // Align the pallet lengthwise with the staging lane direction — no extra rotation.
            // The pallet's long axis (world X = 48") should run parallel to the lane, matching how
            // TestPalletSpawner and PalletPersistenceService place pallets.
            Quaternion laneRotation = Quaternion.LookRotation(Flat(downLane));
            Quaternion rotatedPlacement = laneRotation;

            RegisterAndQueue(inv, truck, cell, pallet, rotatedPlacement, palletIndex);
            
            // Clear the reservation now that the pallet is registered in InventoryService
            if (_pendingDrops.ContainsKey(cell))
            {
                _pendingDrops[cell]--;
                if (_pendingDrops[cell] <= 0) _pendingDrops.Remove(cell);
            }

            float dropBaseY = ComputeDropBaseY(cell, pallet.gameObject);

            // 12. Lower the forks toward the stack (cosmetic — DropPallet sets the exact final Y), then
            //     unparent the pallet onto the lane at dropBaseY.
            if (forks != null) yield return LiftForks(forks, forkRestY + tier * LaneStackStep);
            
            DropPallet(pallet, targetW, dropBaseY, palletWorldScale, rotatedPlacement);
            // 12b. Record the authoritative base-Y everywhere the pallet's height is tracked.
            RecordPalletHeight(pallet.gameObject, dropBaseY, inv);

            // DIAGNOSTIC: exact landing of every offloaded pallet so a tower/mis-stack can be read
            // straight from the Editor.log (cell, stack tier, computed base-Y, final world position,
            // and how many pallets the inventory now believes are in this cell).
            int nowInCell = inv.GetPalletsAtLocation(cell).Count;
            Debug.Log($"[TrailerOffload][DROP] pallet#{palletIndex} → door {door} lane {laneLetter} cell ({cell.x},{cell.y}) " +
                      $"tier={tier} dropBaseY={dropBaseY:F2} finalPos={pallet.position} cellCount(now)={nowInCell} " +
                      $"cellCenter={targetW}");
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

        private void DropPallet(Transform pallet, Vector3 laneWorld, float baseY, Vector3 worldScale, Quaternion rotation)
        {
            pallet.SetParent(null, worldPositionStays: true);

            // baseY is precomputed by ComputeDropBaseY (self-excluded, authoritative). Just place the
            // pallet there — no re-measuring here (re-measuring used to include this very pallet while it
            // was still up on the forks, which stacked it on top of ITSELF and sent it climbing ~3m).
            pallet.position = new Vector3(laneWorld.x, baseY, laneWorld.z);
            pallet.rotation = rotation; // FIX: Apply authoritative rotation
            pallet.localScale = worldScale; // parent is null now, so local == world scale

            // NOTE: Cases are already rotated 90 degrees in TruckController when built in the trailer,
            // so we don't need to rotate them again here. They arrive at the staging lane correctly oriented.
            // (Previously we rotated here, but that caused jank — the trailer rotation fix handles it now.)

            // Fix case Y positioning: PalletBuilder positioned cases with gaps during build.
            // Now that the pallet is at its final position, move cases to sit directly on the pallet
            // surface (local Y = 0.165m, the pallet deck height).
            var palletLoad = pallet.Find("PalletLoad");
            if (palletLoad != null)
            {
                // FIX CASE ORIENTATION: Zero out the default 90-degree Y rotation on PalletLoad
                // so cases align properly with the pallet direction. This is identical to 
                // TestPalletSpawner and critical for persistence.
                palletLoad.localRotation = Quaternion.identity;

                const float palletDeckHeight = 0.165f;

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
        /// World-space top (max renderer bounds Y) of a settled pallet + its cases. Used to stack the
        /// next pallet on top of it. 0 if the object has no renderers.
        /// </summary>
        private static float MeasureTopY(GameObject go)
        {
            float maxY = 0f;
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
                if (r.bounds.max.y > maxY) maxY = r.bounds.max.y;
            return maxY;
        }

        // ── Lane targeting + inventory/task creation ─────────────────────────────────────────────

        // Finds the next open staging slot, restricted to the lanes owned by the truck's dock DOOR
        // (so a truck at door 2 stages in door 2's lanes, not the globally-first lane).
        //
        // FILL RULE (matches the physical constraint — the DS enters a lane from ONE end and CANNOT
        // drive through a pallet): walk in from the ENTRY (the end nearest the dock door) toward the far
        // end and stop at the first OCCUPIED slot, because the DS can't pass it. The chosen drop is the
        // DEEPEST reachable slot with room:
        //   • empty slots are passable — keep walking deeper;
        //   • the first occupied slot is the frontier: if it still has room under MaxStackHeight, STACK
        //     on it ("position 6 holds one → room on top for one more"); either way, STOP — never route
        //     past it to a deeper open slot ("position 6 holds two → drop in 5", never drive through 6).
        // On a fresh lane this still fills far-end-first (FIFO flow-through for the reach truck at the
        // exit), but it can no longer drive through pallets left by an earlier batch/other truck.
        // Returns the lane identity so the drop maneuver can route in via the entry. doorNumber <= 0
        // falls back to any door.
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

                // Order ENTRY→FAR: entry = the end nearest the dock door (where the DS drives in).
                var entryToFar = slots.OrderBy(s => (_grid.GetCellCenter(s.Cell) - doorPos).sqrMagnitude).ToList();

                int chosenIndex = -1;
                int chosenTier  = 0;
                for (int idx = 0; idx < entryToFar.Count; idx++)
                {
                    var s = entryToFar[idx];
                    int stacked = inv.GetPalletsAtLocation(s.Cell).Count;
                    int pending = _pendingDrops.TryGetValue(s.Cell, out int p) ? p : 0;
                    int count   = stacked + pending;

                    if (count == 0)
                    {
                        // Empty and reachable — remember it as the deepest reachable slot, keep going.
                        chosenIndex = idx; chosenTier = 0;
                        continue;
                    }

                    // Occupied slot: the DS cannot drive PAST it. It's the frontier.
                    if (count < maxH)
                    {
                        // Room on top — stack HERE (this is the "one more on top" case).
                        chosenIndex = idx; chosenTier = count;
                    }
                    // Full or partial, we can't go deeper. Stop.
                    break;
                }

                if (chosenIndex >= 0)
                {
                    var chosen = entryToFar[chosenIndex];
                    door = d; laneLetter = lane; cell = chosen.Cell; tier = chosenTier;

                    // Reserve the slot immediately so other stockers don't target it.
                    int pv = _pendingDrops.TryGetValue(cell, out int existing) ? existing : 0;
                    _pendingDrops[cell] = pv + 1;
                    return true;
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

        private void RegisterAndQueue(InventoryService inv, TruckController truck, Vector2Int cell, Transform pallet, Quaternion rotation, int palletIndex = 0)
        {
            // CRITICAL FIX (2026-07-09): Don't guess the SKU from the shipment list using the 
            // spatial-sort index. TruckController.BuildOnePallet already attached PalletData with 
            // the correct SKU and case quantity (Ti * Hi) — read those directly from the pallet
            // so we're byte-for-byte consistent with TestPalletSpawner.
            var pdata = pallet.gameObject.GetComponent<PalletData>();
            string skuId = (pdata != null && !string.IsNullOrEmpty(pdata.ItemNumber)) ? pdata.ItemNumber : "PHYS";
            int quantity = (pdata != null) ? pdata.CaseQuantity : 1;

            var data = inv.RegisterPhysicalPallet(cell, skuId, quantity);

            // LoadId is a "license plate" assigned AT RECEIVING (ReceiverReceivingWorkflow), not here —
            // this pallet is still ghosted/unreceived the moment it lands in the lane.
            PalletMasterLink.Attach(pallet.gameObject, data.PalletId);

            // Re-enable the pallet's PlacedObject now that it has a real grid cell.
            var placed = pallet.gameObject.GetComponent<PlacedObject>();
            if (placed != null)
            {
                placed.gridX = cell.x;
                placed.gridY = cell.y;
                placed.enabled = true;

                // Sync the PalletData location so the hover popup works immediately
                if (pdata != null) pdata.Initialize(pdata.LoadId, pdata.ItemNumber, pdata.CaseQuantity, pdata.ExpirationDay, pdata.Area, pdata.IconSprite, cell);

                // BuildingData was destroyed while this pallet was cargo — recreate it now.
                var bd = pallet.gameObject.GetComponent<BuildingData>();
                if (bd == null) bd = pallet.gameObject.AddComponent<BuildingData>();
                if (placed.data != null)
                {
                    float rotDeg = rotation.eulerAngles.y;
                    Vector2Int[] offsets = placed.data.GetFootprintOffsets(-rotDeg);
                    bd.Initialize(cell, rotDeg, offsets, placed.data);
                }

                Debug.Log($"[TrailerOffload] Registered pallet {data.PalletId} (SKU {skuId}, Qty {quantity}) at grid cell ({cell.x}, {cell.y}).");
            }
            else
            {
                Debug.LogWarning($"[TrailerOffload] Pallet GameObject has no PlacedObject component! Grid position not recorded.");
            }

            ReceivingService.CreateReceiveTaskForPallet(data);
        }

        /// <summary>
        /// Authoritative base-Y for a pallet about to be dropped in <paramref name="cell"/>. Ground =
        /// LaneSurfaceY; stacked = the top of the highest pallet ALREADY SETTLED in the cell + a small
        /// gap. The pallet being dropped (<paramref name="selfGO"/>) is EXCLUDED — it's still up on the
        /// forks, so measuring it would stack it on top of itself and send the stack climbing (the
        /// "pallets float ~3m up" bug). This is the SINGLE source of truth: the same value positions the
        /// pallet AND is recorded as its saved height, so the visual and the record can't drift apart.
        /// </summary>
        private float ComputeDropBaseY(Vector2Int cell, GameObject selfGO)
        {
            float baseY = LaneSurfaceY;
            if (ServiceLocator.TryGet<InventoryService>(out var inv) && inv != null)
            {
                float highestTop = 0f;
                foreach (var rec in inv.GetPalletsAtLocation(cell))
                {
                    var go = PalletMasterLink.Find(rec.PalletId)?.gameObject;
                    if (go == null || go == selfGO) continue; 
                    
                    // CRITICAL FIX: Skip any pallet that is currently being carried (has a parent).
                    // This prevents measuring the pallet on the current (or any other) stocker's forks,
                    // which is the root cause of "stacking to the ceiling."
                    if (go.transform.parent != null) continue;

                    float top = MeasureTopY(go);
                    if (top > highestTop) highestTop = top;
                }
                if (highestTop > 0f) baseY = highestTop + StackGap;
            }
            return baseY;
        }

        /// <summary>
        /// Write the authoritative base-Y everywhere the pallet's height is tracked (its PlacedObject and
        /// its InventoryService master record), so save/load and any later height query all agree with
        /// where the pallet actually sits. Save/load of dock pallets is literal-transform (see
        /// PalletPersistenceService), so the transform is what really matters — but keeping these fields
        /// in sync avoids future confusion.
        /// </summary>
        private void RecordPalletHeight(GameObject palletGO, float baseY, InventoryService inv)
        {
            var placed = palletGO.GetComponent<PlacedObject>();
            if (placed != null)
            {
                placed.worldSpaceYHeight = baseY;

                // CRITICAL for save/load: register the pallet in PlacedObjectRegistry now.
                // RegisterAndQueue enabled its PlacedObject WHILE it was still parented to the dock
                // stocker's forks (a PlacedObject ancestor), so OnEnable's "skip if child of a
                // PlacedObject" guard silently skipped registration — and DropPallet's SetParent(null)
                // doesn't re-fire OnEnable. The pallet was therefore never in the registry, so
                // PalletPersistenceService.CaptureAll saved ZERO dock pallets (dockPallets=0 → no pallets
                // appear after loading). Now that DropPallet has unparented it onto the lane, register it
                // explicitly (Register is a HashSet add — idempotent and safe if already present).
                PlacedObjectRegistry.Register(placed);
            }

            var link = palletGO.GetComponent<PalletMasterLink>();
            if (link != null && inv != null)
            {
                var rec = inv.GetPallet(link.PalletId);
                if (rec != null) rec.WorldHeightY = baseY;
            }
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
