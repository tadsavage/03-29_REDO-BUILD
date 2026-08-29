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
        private const float  ForkLowerDistance  = 1.0f;   // (legacy) unused since the approach-lift rewrite
        private const float  ForkRaiseStandoff  = 1.0f;   // halt this far short of the pallet (fork CARRY POINT → pallet, XZ) and raise the forks THERE, stopped, before any further forward motion
        private const float  PalletLiftClearance = 0.10f; // how far to lift a grabbed pallet off the deck before backing out
        private const float  TrailerBackoutDistance = 1.25f; // reverse this far straight out of the trailer after a grab, before lowering the load to travel height
        private const float  ForkTravelClearance = 0.20f; // fork height ABOVE rest that a loaded DS travels at — the load rides here, not all the way down on the deck
        private const float  StackApproachClearance = 0.10f; // carried pallet rides this far above the target's TOP while driving over it, then lowers by exactly this much to seat
        private const float  StackSeatLowerSpeed = 0.15f;  // slow, deliberate final descent onto the stack (units/sec) — quarter of ForkLiftSpeed
        private const float  LaneBackoutDistance = 1.25f;  // reverse this far after setting a pallet down, before lowering the forks
        private const float  LaneExitForkClearance = 0.15f; // fork height above rest for the empty run back out of the lane
        private const float  GrabThreshold      = 0.2f;   // fork grab-point within this XZ of the pallet → seat it on the forks
        private const float  PivotFrontDistance = 2.0f;   // lane entry pivot sits this far in FRONT of the lane entry (also the legacy fallback for the trailer pivot)
        private const float  PivotDoorOffset    = 1.0f;   // TrailerPivotPoint sits this far OUT from the dock door, onto the dock — the DS's turning spot
        // Lane-drop height. PlacementGrid.GetCellCenter returns the grid PLANE Y (~0), but the staging
        // lanes physically sit on the dock foundation, so pallets dropped at the cell's Y sink into the
        // mesh. LaneSurfaceY is that foundation top; the first pallet sits there. Each stacked pallet
        // above it sits on the ACTUAL measured top of the pallet below (ComputeDropBaseY, via
        // MeasureTopY) + StackGap — never a fixed per-tier height, since pallet types vary in height
        // (a wire-tote pallet is noticeably taller than a standard case pallet). Assumes lanes are on
        // the standard dock height — if lanes ever sit at other heights this should become a downward
        // raycast instead.
        private const float  LaneSurfaceY       = 1.15f;  // dock foundation top the lanes rest on
        private const float  StackGap           = 0.02f;  // small anti-clip gap between a stacked pallet's base and the case-top below it
        private const bool   InvertTrailerAxis  = false;  // flip if the DS drives AWAY from the trailer instead of into it

        // Where the pallet sits ON the forks while carried (local to the Forks transform). The pallet
        // is SNAPPED to this pose on pickup instead of inheriting whatever misalignment the drive left,
        // so it always reads as seated on the tines. Tune these until a carried pallet looks right:
        // typically centered (X≈0), resting on the tines (small Y), pushed forward onto them (Z along
        // the forks' forward axis).
        private static readonly Vector3 ForkCarryLocalPos   = new Vector3(0f, 0f, -0.2f);
        // Identity — see the matching note in TrailerLoadController. Yawing this 180 to correct a
        // flipped pallet is wrong: the carry pose is shared by every pickup, so it just moves the
        // flip onto all the pallets that were previously correct.
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

            StartCoroutine(OffloadRoutine(truck, slot, inv, queue));
        }

        private MHEOperatorSlot FindAvailableDockStocker()
        {
            foreach (var slot in FindObjectsByType<MHEOperatorSlot>())
            {
                // DockEquipmentCommandeerRegistry is shared with TrailerLoadController (outbound) —
                // AiNavigation is already disabled from the moment an operator boards, whether idle
                // or actively driven, so it can't be used as an "in use" signal; IsOccupied likewise
                // stays true the whole time they're boarded. The registry is the only signal that
                // actually distinguishes "boarded and idle" from "a controller is driving them right now".
                if (!slot.IsOccupied || DockEquipmentCommandeerRegistry.IsCommandeered(slot)) continue;
                var op = slot.CurrentOperator;
                if (op == null || op.Record == null) continue;
                if (op.Record.role != EmployeeRole.DockStockerOperator) continue;
                return slot;
            }
            return null;
        }

        private IEnumerator OffloadRoutine(TruckController truck, MHEOperatorSlot slot, InventoryService inv, WorkQueueSystem queue)
        {
            truck.ClaimForOffload();
            DockEquipmentCommandeerRegistry.Commandeer(slot);

            Transform ds = slot.transform;
            // Where the DS was standing BEFORE we took it over — by definition a valid, patrol-reachable
            // spot on the working surface. Kept as the fallback for the restore below.
            Vector3 homePos = ds.position;

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
                // Rig destroyed mid-run — stop dispatching pallets and FALL THROUGH to the restore
                // block below, rather than piling up more exceptions and skipping cleanup entirely.
                if (ds == null) break;
                yield return OffloadOnePallet(ds, forks, forkRestY, truck, pallet, into, openingLong, doorNumber, doorPos, inv, queue, i);
            }

            // ── Restore the DS to patrol ──
            if (forks != null) SetForkLocalY(forks, forkRestY);
            if (agent != null)
            {
                agent.enabled = agentWas;
                RestoreAgentToWorkingSurface(ds, agent, homePos);
            }
            if (nav != null)
            {
                nav.enabled = navWas;
                nav.GoToRandomWaypoint();
            }

            DockEquipmentCommandeerRegistry.Release(slot);
            truck.CompleteOffload();
            Debug.Log($"[TrailerOffload] {truck.name} fully offloaded — released dock stocker to patrol.");

            // VENDORS tab's "Avg Daily Pallets" — recorded here, the one place both the vendor
            // (AssignedShipment.SupplierId) and the real pallet count (`pallets`, built at the top of
            // this routine) are both in scope after a completed inbound offload.
            var shipment = truck.AssignedShipment;
            if (shipment != null && !string.IsNullOrEmpty(shipment.SupplierId) &&
                ServiceLocator.TryGet<VendorPerformanceTracker>(out var perfTracker))
            {
                var gameCtx = UnityEngine.Object.FindAnyObjectByType<GameContext>();
                int day = gameCtx != null ? gameCtx.TimeService.Day : 0;
                perfTracker.RecordPallets(shipment.SupplierId, pallets.Count, day);
            }
        }

        private IEnumerator OffloadOnePallet(Transform ds, Transform forks, float forkRestY,
                                             TruckController truck, Transform pallet, Vector3 into,
                                             float openingLong, int doorNumber, Vector3 doorPos,
                                             InventoryService inv, WorkQueueSystem queue, int palletIndex = 0)
        {
            Vector3 P = pallet.position;
            Vector3 palletWorldScale = pallet.lossyScale; // preserve visual size across the reparenting

            // ── THE TRAILER PIVOT POINT ───────────────────────────────────────────────────
            // The single ground point in front of the trailer where ALL trailer-side pivoting happens,
            // for this pallet. Every entry into and exit out of the trailer routes through it, so the DS
            // only ever turns out here in the open and drives dead straight once it is inside.
            //
            // It is a flat (X,Z) point:
            //   • LATERAL  — the pallet's own row offset, so the DS is already squared up on the pallet's
            //                line and needs no steering correction on the way in;
            //   • LONGITUDINAL — anchored to the DOCK DOOR, PivotDoorOffset further OUT onto the dock.
            //                It deliberately does NOT come from the pallet: using the pallet's own depth
            //                would put the "pivot in front of the trailer" INSIDE the trailer, where
            //                there is no room to turn. It used to be derived from the rear-most pallet
            //                as a stand-in for the opening, which parked the pivot right up against the
            //                trailer mouth; measuring from the door itself puts it clear on the dock.
            // Y is just the DS's drive height, carried through unchanged.
            Vector3 rightAxis = Vector3.Cross(Vector3.up, into);        // horizontal, ⟂ to `into` (unit)
            float   palletLat = Vector3.Dot(P, rightAxis);             // this pallet's row (lateral) offset
            // `into` points INTO the trailer, so SUBTRACTING moves out onto the dock. Falls back to the
            // old opening-proxy if this truck somehow has no DockedAt door to measure from.
            float   pivotLong = truck.DockedAt != null
                              ? Vector3.Dot(doorPos, into) - PivotDoorOffset
                              : openingLong - PivotFrontDistance;
            Vector3 trailerPivotPoint = into * pivotLong                // longitudinal: out from the door
                          + rightAxis * palletLat                      // lateral: on the pallet's row
                          + Vector3.up * ds.position.y;                // keep the DS's drive height

            // 1. Pull up to the TrailerPivotPoint for this pallet's row (left/right).
            yield return DriveTailFirst(ds, trailerPivotPoint);
            // 2. Rotate IN PLACE at the pivot — outside the trailer — so the forks face straight down
            //    this pallet's row before any forward motion. All turning happens here; the drive-in
            //    below no longer steers, so the DS never rotates once it is inside the trailer.
            yield return FaceForks(ds, into);
            // 2b. Drop the forks to rest height for the approach. They stay low until the DS is close
            //     (see DriveInToGrab), then come up to meet this pallet's height.
            if (forks != null) yield return LiftForks(forks, forkRestY);
            // 3. Drive straight in, forks low; raise to the pallet's own Y within ForkRaiseDistance,
            //    and stop when the fork carry point reaches the pallet.
            yield return DriveInToGrab(ds, forks, forkRestY, pallet, into);
            // 4. Seat the pallet on the forks, then lift just enough to take its weight off the deck.
            Transform carrier = forks != null ? forks : ds;
            // Captured BEFORE parenting: once it's a child its world rotation is already the
            // carrier's, leaving nothing to compare against.
            Vector3 facingBeforePickup = pallet.forward;
            pallet.SetParent(carrier, worldPositionStays: false);
            pallet.localPosition = ForkCarryLocalPos;
            pallet.localRotation = NearestFacing(Quaternion.Euler(ForkCarryLocalEuler),
                                                 carrier.InverseTransformDirection(facingBeforePickup));
            // A short lift off the deck — NOT the old fixed `forkRestY + ForkLiftHeight` (1m), which
            // yanked the load a metre up regardless of what height it was picked from. Relative to
            // wherever the forks actually are now, so it behaves the same on any tier.
            if (forks != null) yield return LiftForks(forks, forks.localPosition.y + PalletLiftClearance);
            // 5. Reverse straight out TrailerBackoutDistance first, so the load is clear of whatever
            //    it was sitting on, THEN drop the forks back to rest height and carry it low for the
            //    rest of the trip. (Carrying at pick height all the way to the lane is what made a
            //    pallet taken off an upper tier float across the dock.) If a lowered pallet is ever
            //    seen clipping the stack it came off, raise TrailerBackoutDistance — the descent
            //    starts the moment this short reverse finishes.
            yield return DriveTailFirst(ds, ds.position - into * TrailerBackoutDistance);
            //    Settle to TRAVEL height, not all the way to rest — the load rides just off the deck
            //    for the trip out rather than being set back down on it.
            if (forks != null) yield return LiftForks(forks, forkRestY + ForkTravelClearance);
            // 5b. Continue out to the SAME TrailerPivotPoint we entered through (clear of the trailer) —
            //     cab-first, forks (and pallet) still pointing into the trailer, no spin.
            yield return DriveTailFirst(ds, trailerPivotPoint);

            // ── DROP OFF (enter the lane from its door-end entry, never across lanes) ──────
            // 7. Pick the next open Inbound/Both slot in one of THIS truck's dock-door lanes.
            if (!TryFindLaneTarget(inv, queue, doorNumber, doorPos, out int door, out string laneLetter, out var cell, out int tier))
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

            // 7b. Still parked at the trailer pivot: turn IN PLACE to face the way we're about to
            //     travel, before setting off for the lane. DriveTailFirst would otherwise swing the
            //     body around while already rolling, which reads as the DS slewing sideways across
            //     the dock. Turn first, then drive straight.
            yield return FaceDir(ds, entryPivot - ds.position);
            // 8. Pull up to a pivot just in front of the lane entry (outside the lane, door side).
            yield return DriveTailFirst(ds, entryPivot);

            // ── Wait for exclusive lane entry ───────────────────────────────────────
            // Parked at entryPivot now — outside the lane, on the dock, at its door-side mouth. One
            // piece of equipment (dock stocker, reach truck — whatever else moves in here later) may
            // be physically inside a lane at a time: StagingDropBaseY/ComputeDropBaseY both measure
            // whatever is ALREADY STANDING in a cell, and a pallet still on forks is invisible to that
            // measurement, so two deliveries mid-insertion at once can compute the same drop height
            // and land on top of each other. Sit here and wait until the lane is free, then check
            // the height fresh once inside. Same lock ReachTruckOperator waits on — keyed by
            // (door, lane), so it serialises DS-vs-DS, truck-vs-truck, AND DS-vs-truck on any lane
            // either can reach.
            while (!inv.TryEnterLaneForDelivery(door, laneLetter))
                yield return null;

            try
            {
                // 9. Spin IN PLACE at the lane entry so the forks (and the carried pallet) face straight
                //    down the lane at the slot we're driving into — forks FIRST.
                yield return FaceForks(ds, downLane);

                // 9b. STACK APPROACH HEIGHT — raise so the CARRIED pallet's base rides exactly
                //     StackApproachClearance above whatever it is going to land on, then drive over it at
                //     that height. targetTopY is the measured TOP of the highest pallet already settled in
                //     this cell, or the lane surface itself when the cell is empty.
                //
                //     The old version set the fork's local Y to `highestPalletHeight` — a pallet's top MINUS
                //     its base, i.e. how TALL the pallet is. That conflates the size of an object with a
                //     position on the mast: a 1.2m-tall pallet sent the forks to local Y 1.2 no matter how
                //     high the stack it was landing on actually sat, which is why the load went up far more
                //     than it ever needed to. The height we want is derived the same way DriveInToGrab
                //     derives matchLocalY — measure the WORLD Y the load must reach, then shift the forks by
                //     the difference — so it is correct for any pallet type and any tier.
                float targetTopY = LaneSurfaceY; // empty cell → land on the lane surface itself
                if (ServiceLocator.TryGet<InventoryService>(out var invStack) && invStack != null)
                {
                    foreach (var rec in invStack.GetPalletsAtLocation(cell))
                    {
                        var go = PalletMasterLink.Find(rec.PalletId)?.gameObject;
                        if (go == null || go == pallet.gameObject) continue;
                        if (go.transform.parent != null) continue; // skip carried pallets
                        float top = MeasureTopY(go);
                        if (top > targetTopY) targetTopY = top;
                    }
                }
                if (forks != null)
                {
                    // pallet.position.y is the carried pallet's BASE (DropPallet/ComputeDropBaseY both treat
                    // it that way), so this delta is exactly what the mast has to travel.
                    float approachLocalY = forks.localPosition.y + ((targetTopY + StackApproachClearance) - pallet.position.y);
                    Debug.Log($"[TrailerOffload] Stack approach: target top={targetTopY:F3}m, " +
                              $"forks {forks.localPosition.y:F3} → {approachLocalY:F3} (+{StackApproachClearance:F2} clearance).");
                    yield return LiftForks(forks, approachLocalY);
                }

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
                // Lengthwise along the lane either way — but keep whichever end is already leading, so the
                // pallet doesn't spin 180 at the moment it leaves the forks. Both variants look identical
                // once parked; only the transition between them is visible.
                Quaternion rotatedPlacement = NearestFacing(laneRotation, pallet.forward);

                RegisterAndQueue(inv, truck, cell, pallet, rotatedPlacement, palletIndex);
            
                // Clear the reservation now that the pallet is registered in InventoryService
                if (_pendingDrops.ContainsKey(cell))
                {
                    _pendingDrops[cell]--;
                    if (_pendingDrops[cell] <= 0) _pendingDrops.Remove(cell);
                }

                float dropBaseY = ComputeDropBaseY(cell, pallet.gameObject);

                // 12. SEAT IT. Now parked directly over the target, lower SLOWLY (StackSeatLowerSpeed) by
                //     the approach clearance until the carried pallet's base is exactly on dropBaseY — the
                //     same authoritative value DropPallet is about to use. Targeting dropBaseY rather than
                //     "the height we raised from" means the load comes to rest precisely on the stack with
                //     nothing left for DropPallet to correct, so there is no snap at the end of the descent.
                if (forks != null)
                {
                    float seatLocalY = forks.localPosition.y + (dropBaseY - pallet.position.y);
                    yield return LiftForks(forks, seatLocalY, StackSeatLowerSpeed);
                }

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
                // 12c. Reverse clear of the pallet just set down BEFORE touching the mast — dropping the
                //      tines while still directly over it would drag them down its face.
                yield return DriveTailFirst(ds, ds.position - downLane * LaneBackoutDistance);
                // 12d. Forks are empty now — settle them to the return-trip height so the DS makes the whole
                //      run back to the trailer at a consistent height rather than arriving still raised to
                //      the last drop's stack height (which then had to be corrected mid-approach).
                if (forks != null) yield return LiftForks(forks, forkRestY + LaneExitForkClearance);
                // 13. Reverse straight back OUT of the lane to the entry pivot (never across the lanes) —
                //     cab-first, forks trailing, no spin. From here the next iteration pulls up to the next
                //     pallet's TrailerPivotPoint, which is where all trailer-side turning happens.
                yield return DriveTailFirst(ds, entryPivot);
            }
            finally
            {
                // Covers normal completion AND an exception thrown mid-drop — e.g. the DS or the
                // pallet being destroyed while this is actively running (several direct ds./forks.
                // dereferences below aren't null-guarded, so that surfaces as an exception here rather
                // than a graceful bail). NOT a guarantee against StopCoroutine or this component's own
                // GameObject being destroyed while the coroutine sits paused at a yield — Unity does
                // not run IEnumerator finally blocks for either of those (verified empirically; it's a
                // known gap in Unity's coroutine scheduler, not a C# language limitation). Neither
                // currently happens to this routine anywhere in the codebase, and a lock that outlives
                // the routine only matters for the rest of the current session — it isn't persisted.
                inv.ReleaseLaneEntry(door, laneLetter);
            }
        }

        /// <summary>
        /// Hands the agent back onto a NavMesh position we have actually VALIDATED, instead of blindly
        /// Warping to wherever the scripted run happened to finish.
        ///
        /// Warp snaps to the NEAREST NavMesh, which near a dock edge can be the yard a metre below
        /// rather than the dock itself. That is how a dock stocker ended a load run parked at ground
        /// level out in the open yard, isOnNavMesh=true but on a disconnected island with no route back
        /// — invisible to every existing recovery. So: require the sampled surface to be at the height
        /// the DS was working at, and fall back to its pre-commandeer position (known good) if it isn't.
        /// </summary>
        private static void RestoreAgentToWorkingSurface(Transform ds, NavMeshAgent agent, Vector3 homePos)
        {
            if (ds == null || agent == null || !agent.isActiveAndEnabled) return;

            const float HeightTolerance = 0.5f; // a dock is ~1.15 above the yard — half that separates them cleanly
            const float SampleRadius    = 2.0f;

            bool ok = NavMesh.SamplePosition(ds.position, out NavMeshHit hit, SampleRadius, agent.areaMask)
                      && Mathf.Abs(hit.position.y - homePos.y) <= HeightTolerance;

            if (!ok)
            {
                Debug.LogWarning($"[TrailerOffload] {ds.name} finished its run at {ds.position} with no NavMesh at its " +
                                 $"working height ({homePos.y:F2}) within {SampleRadius}m — returning it to {homePos} " +
                                 $"rather than stranding it off the dock.");
                if (!NavMesh.SamplePosition(homePos, out hit, SampleRadius, agent.areaMask))
                {
                    ds.position = homePos;
                    agent.Warp(homePos);
                    agent.isStopped = false;
                    return;
                }
            }

            ds.position = hit.position;
            agent.Warp(hit.position);
            agent.isStopped = false;
        }

        // ── Movement primitives (scripted transform choreography) ────────────────────────────────

        // Drives the DS forks-first toward the pallet (body faces -into so the rear forks point +into)
        // in three DISCRETE phases — approach low, stop and lift, then enter — so the tines are never
        // moving vertically and horizontally at the same time:
        //   1. roll in with the forks at rest until the fork carry point is ForkRaiseStandoff from the
        //      pallet, and STOP;
        //   2. stopped and clear, raise the forks to this pallet's own height and wait for the lift to
        //      finish;
        //   3. only then close the remaining gap, stopping the instant the carry point (where the
        //      pallet will seat) reaches the pallet's XZ (within GrabThreshold).
        // A travel cap keeps it from driving through the trailer if the pallet is somehow unreachable.
        private IEnumerator DriveInToGrab(Transform ds, Transform forks, float forkRestY, Transform pallet, Vector3 into)
        {
            Vector3 start = ds.position;
            const float maxTravel = 8f;

            // Fork local Y that puts the CARRY POINT (where the pallet actually seats) level with
            // this pallet. Measured once here, before the forks move: the delta is world-vertical, so
            // it maps 1:1 onto the forks' local Y. Measuring the forks' origin instead of the carry
            // point would leave a fixed offset, showing as the pallet riding high or low on the tines.
            float matchLocalY = forkRestY;
            if (forks != null)
            {
                Vector3 carryPoint = forks.TransformPoint(ForkCarryLocalPos);
                matchLocalY = forks.localPosition.y + (pallet.position.y - carryPoint.y);
            }

            // PHASE 1 — APPROACH, forks still DOWN. Roll straight in until the fork carry point is
            // ForkRaiseStandoff short of the pallet, then stop. Distance is measured from the CARRY
            // POINT rather than the DS root because the tines are what has to stay clear: the root
            // sits a good way behind them, so a root-based standoff can have the tines already buried
            // in the pallet face by the time the lift is allowed to start.
            while (true)
            {
                if (ds == null || pallet == null) yield break; // destroyed mid-run — see DriveInternal
                Vector3 grab = forks != null ? forks.TransformPoint(ForkCarryLocalPos) : ds.position + into;
                Vector3 gd = pallet.position - grab; gd.y = 0f;
                if (gd.magnitude <= ForkRaiseStandoff) break;

                // Straight in only — NO steering. The DS was squared up at the pivot before entering
                // (FaceForks at the call site), and rotating inside the trailer looked wrong.
                ds.position += into * (DriveSpeed * Time.deltaTime);

                if ((ds.position - start).magnitude >= maxTravel)
                {
                    Debug.LogWarning("[TrailerOffload] DriveInToGrab hit the travel cap on approach — lifting and seating anyway.");
                    break;
                }
                yield return null;
            }

            // PHASE 2 — STOPPED and clear of the pallet: raise to this pallet's own height and let the
            // lift RUN TO COMPLETION before any further motion. Doing the lift concurrently with the
            // drive (the old single-loop version) is what dragged the tines up through the pallet it
            // was about to pick, and left the height still settling as the pallet got parented.
            if (forks != null) yield return LiftForks(forks, matchLocalY);

            // PHASE 3 — at pocket height now: close the last stretch straight in until the carry point
            // reaches the pallet and it can be seated.
            while (true)
            {
                if (ds == null || pallet == null) yield break; // destroyed mid-run — see DriveInternal
                Vector3 grab = forks != null ? forks.TransformPoint(ForkCarryLocalPos) : ds.position + into;
                Vector3 gd = pallet.position - grab; gd.y = 0f;
                if (gd.magnitude <= GrabThreshold) break;

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
                // The rig can be destroyed mid-run (equipment deleted, scene torn down, domain reload
                // during play). Without this the coroutine throws MissingReferenceException on
                // t.position and dies PART WAY THROUGH, which skips the restore block at the end of the
                // routine and leaves the DS commandeered with its agent still disabled — permanently
                // frozen. Bail cleanly instead and let the caller finish its cleanup.
                if (t == null) yield break;

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
            if (t == null) yield break;
            Quaternion want = Quaternion.LookRotation(Flat(dir));
            while (Quaternion.Angle(t.rotation, want) > FaceThreshold)
            {
                t.rotation = Quaternion.RotateTowards(t.rotation, want, TurnSpeed * Time.deltaTime);
                yield return null;
                if (t == null) yield break; // destroyed mid-turn — see DriveInternal
            }
            t.rotation = want;
        }

        // Rotate in place so the FORKS point along worldForkDir (the body faces the opposite way on a
        // rear-fork model). Use whenever the forklift must "aim its forks" — squaring up to a pallet in
        // the trailer or squaring up to a staging lane.
        /// <summary>
        /// The 180-degree variant of <paramref name="want"/> closest to how the pallet already faces.
        /// A pallet's footprint is symmetric under a 180 yaw, so both variants seat it equally
        /// squarely and picking the nearer one means it never visibly spins on pickup or set-down.
        /// Mirror of TrailerLoadController.NearestFacing — see the fuller note there for why an
        /// absolute carry pose cannot work for both trailer-sourced and lane-sourced pallets.
        /// </summary>
        private static Quaternion NearestFacing(Quaternion want, Vector3 currentForward)
        {
            Vector3 wantFwd = Flat(want * Vector3.forward);
            Vector3 curFwd = Flat(currentForward);
            if (curFwd.sqrMagnitude < 0.0001f || wantFwd.sqrMagnitude < 0.0001f) return want;
            return Vector3.Dot(wantFwd, curFwd) >= 0f ? want : want * Quaternion.Euler(0f, 180f, 0f);
        }

        private IEnumerator FaceForks(Transform t, Vector3 worldForkDir) => FaceDir(t, BodyForwardForForks(worldForkDir));

        // World direction the body's transform.forward must point so the FORKS aim along worldForkDir.
        private static Vector3 BodyForwardForForks(Vector3 worldForkDir) => worldForkDir * ForkAxisSign;

        // speedOverride > 0 runs the mast at a different rate than ForkLiftSpeed — used for the slow,
        // deliberate final descent that seats a pallet onto a stack.
        private IEnumerator LiftForks(Transform forks, float targetLocalY, float speedOverride = -1f)
        {
            if (forks == null) yield break;
            float speed = speedOverride > 0f ? speedOverride : ForkLiftSpeed;
            Vector3 lp = forks.localPosition;
            while (Mathf.Abs(lp.y - targetLocalY) > 0.001f)
            {
                lp.y = Mathf.MoveTowards(lp.y, targetLocalY, speed * Time.deltaTime);
                forks.localPosition = lp;
                yield return null;
                if (forks == null) yield break; // destroyed mid-lift — see DriveInternal
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
        //   • the first occupied slot is the frontier: if it still has room under MaxStackHeight AND its
        //     current top pallet isn't already claimed for an in-flight Putaway, STACK on it ("position 6
        //     holds one → room on top for one more"); either way, STOP — never route past it to a deeper
        //     open slot ("position 6 holds two → drop in 5", never drive through 6).
        // On a fresh lane this still fills far-end-first (FIFO flow-through for the reach truck at the
        // exit), but it can no longer drive through pallets left by an earlier batch/other truck.
        // Returns the lane identity so the drop maneuver can route in via the entry. doorNumber <= 0
        // falls back to any door.
        private bool TryFindLaneTarget(InventoryService inv, WorkQueueSystem queue, int doorNumber, Vector3 doorPos,
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
                    var slotPallets = inv.GetPalletsAtLocation(s.Cell);
                    int stacked = slotPallets.Count;
                    int pending = _pendingDrops.TryGetValue(s.Cell, out int p) ? p : 0;
                    int count   = stacked + pending;

                    if (count == 0)
                    {
                        // Empty and reachable — remember it as the deepest reachable slot, keep going.
                        chosenIndex = idx; chosenTier = 0;
                        continue;
                    }

                    // Occupied slot: the DS cannot drive PAST it. It's the frontier. Room on top alone
                    // isn't enough to stack here — a Reach Truck Operator locks in its Putaway target
                    // (this cell's current top pallet) the instant it claims the task, but doesn't
                    // physically remove it / update InventoryService until several seconds later after
                    // the drive/grab animation. Stacking a new pallet on top during that window leaves it
                    // floating in mid-air the moment the RTO drives off with the one underneath it — so
                    // skip stacking (treat this slot as unusable, same as "full") whenever the current
                    // top pallet already has an Assigned Putaway task in flight.
                    bool topInFlight = stacked > 0 && IsPutawayInFlight(queue, slotPallets[stacked - 1].PalletId);
                    if (count < maxH && !topInFlight)
                    {
                        // Room on top — stack HERE (this is the "one more on top" case).
                        chosenIndex = idx; chosenTier = count;
                    }
                    // Full, partial-but-in-flight, or otherwise blocked — we can't go deeper. Stop.
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

        // True if a Putaway task for this pallet is currently claimed (Assigned) by a Reach Truck
        // Operator — i.e. an RTO has already locked this pallet in as its pickup target and may be
        // mid-drive toward it, even though InventoryService still shows the pallet sitting in its cell
        // (that only updates once the RTO physically grabs it, well after claiming the task). Linear
        // scan is fine here — called at most once per candidate slot per drop decision, not per frame.
        private static bool IsPutawayInFlight(WorkQueueSystem queue, string palletId)
        {
            if (queue == null || string.IsNullOrEmpty(palletId)) return false;
            foreach (var t in queue.Tasks)
            {
                if (t.Type == WorkTaskType.Putaway && t.Status == WorkTaskStatus.Assigned && t.PalletId == palletId)
                    return true;
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
            if (pdata == null || string.IsNullOrEmpty(pdata.ItemNumber))
            {
                // PalletData never got initialized — a genuinely unresolvable SKU (not merely a
                // missing case prefab; TruckController.BuildOnePallet now still initializes PalletData
                // for those). Used to silently register a dummy "PHYS" SKU here — real money already
                // spent turning into dead-air inventory nobody could ever pick or sell. Refuse instead:
                // no receive task is queued and the pallet is left un-registered rather than lying
                // about what's on it.
                Debug.LogError($"[TrailerOffload] Pallet '{pallet.name}' has no valid SKU (PalletData never initialized) — skipping registration instead of creating phantom inventory.");
                return;
            }
            string skuId = pdata.ItemNumber;
            int quantity = pdata.CaseQuantity;

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
        /// <summary>
        /// Base Y for a pallet about to be set down in a staging cell — the ONE stacking rule, shared
        /// by both directions. Inbound offload has always used this math (see ComputeDropBaseY below);
        /// outbound staging now calls it too, so a staged pallet sits ON the one under it instead of
        /// clipping through it.
        ///
        /// Measures the ACTUAL renderer top of whatever is already in the cell rather than assuming a
        /// fixed per-tier height, because pallet types differ in height (a wire-tote pallet is well
        /// taller than a standard case pallet). Two sources, because staged outbound freight is
        /// deliberately dropped from InventoryService (see ReachTruckOperator's set-down) and so is
        /// invisible to GetPalletsAtLocation:
        ///   • inbound stock recorded against the cell, and
        ///   • OutboundPalletBuilder pallets physically standing in it.
        /// Anything still parented is riding forks and is not standing in the cell yet, so it is
        /// skipped — the bug that produced "stacking to the ceiling" when it wasn't.
        /// </summary>
        public static float StagingDropBaseY(int doorNumber, string lane, Vector2Int cell, GameObject selfGO)
        {
            float highestTop = 0f;

            if (ServiceLocator.TryGet<InventoryService>(out var inv) && inv != null)
            {
                foreach (var rec in inv.GetPalletsAtLocation(cell))
                {
                    var go = PalletMasterLink.Find(rec.PalletId)?.gameObject;
                    if (go == null || go == selfGO || go.transform.parent != null) continue;
                    float top = MeasureTopY(go);
                    if (top > highestTop) highestTop = top;
                }

                foreach (var go in inv.GetOutboundPalletObjectsAt(doorNumber, lane, cell))
                {
                    if (go == null || go == selfGO || go.transform.parent != null) continue;
                    float top = MeasureTopY(go);
                    if (top > highestTop) highestTop = top;
                }
            }

            return highestTop > 0f ? highestTop + StackGap : LaneSurfaceY;
        }

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
