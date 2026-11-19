# MHE traffic rework (2026-11-18)

Tad's ask: reach trucks doing pallet picks, replenishments and putaways, pallet jacks travelling to aisles, picking, going aisle to aisle,
handling dock congestion and backing into staging lanes; reach trucks have right of way; no physics, so a sensor system; everyone keeps
0.25 m from everyone else; no clipping; passing in the aisles; jacks pull up to the pick face with a 0.5 m gap; a trip always starts at the
lowest aisle on the pick list and follows each aisle's chevron, never turning more than 30 degrees or turning round in an aisle.
"Lean on Unity's nav as much as possible, but use anchors / transform-driven moves in the aisles where that is safer."

## What changed: one motor for every machine

All machine travel (pallet jacks, reach trucks, dock stockers, charge / park trips) now goes through **`MheMotor`**
(`Assets/_Project/Scripts/Actors/Mhe/`). The NavMeshAgent no longer steers machines (its RVO avoidance treated a 3.8 m jack as a
circle at its front end, so forks clipped and machines shoved each other sideways).

| File | What it is |
|---|---|
| `MheAisleMap.cs` | Every rack aisle's geometry from the live locations: centre line, rack faces (measured), rack ends, travel = toward higher bays = the chevron. Rebuilt when locations change. |
| `MhePlanner.cs` | Routes. **Open floor = the NavMesh** (`NavMesh.CalculatePath`, MHE agent, aisles excluded), corners/points pushed off walls and pallets for the machine's real size, a straight line-up before a headed goal. **Inside an aisle = the aisle's own lane** (transform-driven): in by an end, gentle lane changes (18 degrees, never past 30), out by an end. Jacks: in at the low-bay end, out at the far end, never backwards more than 1.5 m. Reach trucks: either end, whichever is shorter. A standing machine in the way: cruise on a line that clears it (aisle) or a detour round its freer side (open floor). |
| `MheMotor.cs` | Drives a route: pure pursuit on the machine's CENTRE, turns about its centre, scripted transform moves. **Sensor:** every frame the full outline (forks and carried pallets included) is swept along the route; it brakes to stop 0.25 m from any machine (+0.6 m behind one that is moving), 0.2 m from a person on foot, and short of racks, walls and floor pallets. A last check on each step refuses any overlap. Right of way, standoffs, passing, holding back at someone else's work spot (below). |
| `MheTraffic.cs` | Shared state: oriented-rectangle maths, the NavMesh filters, the aisle entry rule, work-spot reservations, people on foot. |
| `MheTrafficProbe.cs` | Watches every machine in the Editor and logs incidents: `[MheProbe] CLIP / TOO CLOSE / IN RACK / ANGLE / HELD / FAILED`, plus a summary every 30 s. |
| `MheTestBench.cs` | One-call test block: HAL tab > TEST SCENARIO > **RUN MHE TRAFFIC TEST (slot 6, 4 orders, 3x)**. Loads slot 6 ("Order selection test"), releases the 4 smallest orders, suppresses the day-end recap, runs 8 real minutes, logs `[MheTest]` summaries. |

NavMesh: new area 8 **"Rack Aisle"** (`ProjectSettings/NavMeshAreas.asset`), painted over every aisle by `NavMeshManager.AddAisleSources`
(a modifier box per aisle). Machines' open-floor routes exclude it, so the NavMesh can never send a jack down an aisle the wrong way or
across one; people walk it as plain floor.

## The rules as built

- **Right of way:** reach truck 3 > dock stocker 2 > pallet jack 1. A lower-ranked machine treats the stretch a higher-ranked one is
  about to drive (its next 1.2 m + 1.6 s) as occupied, so it gives way at crossings and mouths; if it is already in that stretch it keeps
  going to get out of it.
- **Standoffs:** two machines holding each other up for 1.2 s: the lower rank (between equals, the one driving INTO the other, i.e. a
  follower) makes way: backs off along its own line, or on open floor pulls aside on a diagonal, waits for the other to pass, re-plans.
- **Aisle entry (one direction at a time):** before driving into an aisle a machine asks `MheTraffic.MayEnterAisle`. Jacks go with the
  chevron only. A reach truck going against it waits while anything inside is moving with it; while it waits, new jacks hold at the
  mouth so the aisle drains (reach trucks first). Standing / working machines never block entry.
- **Passing:** a machine standing still (picking, unmanned, working) is driven round: in an aisle the planner picks a cruise line that
  clears it (a jack parked at the face leaves the centre line clear: 0.31 m to spare with this layout); on open floor a detour.
  Machines queued in traffic are not passed.
- **Pick stops:** the jack parks parallel to the aisle, facing the chevron, its side 0.5 m off the pick face, the pallet being loaded
  level with the slot. The selector works in that gap (stands 0.26 m off the face, turns round to the pallet) and walks round the jack's
  end, never through it, for a pick on the other side.
- **Pick order:** ascending slot name (aisle, bay, level, position) = lowest aisle first, then along each aisle's chevron.
  `SpreadTripStarts` is now OFF (it started trips further up the aisle when another jack was near the first stop).
- **Work spots / dock congestion:** each leg reserves its destination. Another machine whose destination is within reach of it (both
  bodies + 0.9 m) and that is not the nearer one stops at a holding line short of it until the owner sets off on its next leg. This is
  the queue at lane mouths, pivot points and pick stops.
- **Staging:** unchanged process, now on the motor: lane claimed when chosen, drive with the jack's CENTRE onto the pivot point (2.5 m out
  from the lane's open end), turn on the spot to face away, reverse straight in until the pallet is on the cell, lower, forks up, straight
  forward to the pivot.
- **Anti-clip guard:** every machine, every frame, whatever moved it (motor, its NavMeshAgent, a reach truck's hand-driven move into a
  lane or rack): a move INTO another machine (closer than 0.1 m) is undone. Moving apart is always allowed. A motor-driven machine that
  something keeps pushing into gets out of its way.
- Jack no-progress watchdog no longer fires while the jack is on a motor leg (at 3x it abandoned jacks queued at the dock after 10 real s);
  the motor gives a leg up after 2 minutes held in traffic, 7 s with no progress, or a turn blocked by a rack for 2.5 s.

## How to test

1. Play, then HAL tab (key 1) > TEST SCENARIO > RUN MHE TRAFFIC TEST. (From code: `GameCore.Actors.MheTestBench.Run(6, 4, 3f, 8f)`.)
2. Watch the log: `[MheTest]` start / per-minute / final lines, `[MheProbe]` incidents.
3. A clean run: clips 0, aisle angle 0, in rack 0, few or no TOO CLOSE, no FAILED legs.

## Results (Editor, "Order selection test" slot 6, 3x, 3 jacks / 2 reach trucks / 3 dock stockers, 2026-11-18)

Eleven blocks were run, fixing between them. What each block showed and what was changed:

| Block | Seen | Fixed |
|---|---|---|
| test drive | idle jack driven to 01-07-00: in at the low-bay mouth, diagonal to the face, stopped parallel, 0.50 m off the face, pallet level with the slot | - |
| 1 | two jacks deadlocked at the lane ends (leader's forks swung back at the follower); route hugged the lane-end pallets; RTs clipped during hand-driven lane moves | follower yields; headway behind moving machines; points pushed off pallets/walls along the whole route |
| 2 | 0 clips (guard live); RT deadlock at lane 3E exit (spot owner stood in the waiter's way) | spot ownership = whoever is at / nearer the spot |
| 3-5 | head-on RTs at the dock; jack abandoned by the 10-game-minute watchdog at 3x; a pushed machine never moved aside | diagonal pull-aside on open floor; watchdog ignores motor legs; "something keeps pushing into me" -> make way |
| 6 | RT starting a leg from inside a staging lane between chep pallets; jack stuck after dropping its pallet (sensor counted the pallet it had just set down) | straight escape out of tight spots; statics the machine is already touching are ignored |
| 7-8 | RT turned round inside an aisle with a load and jammed it; RT reversed into a jack coming the other way; a spot wait timed out and an RT put a pallet down over a jack (the only real clip) | RTs never turn round in an aisle (reverse along it); keep right / go round for oncoming traffic; no time limit on spot waits |
| 9 | 0 clips, 0 aisle-angle, 0 failed legs; pallets staged in 1C by jacks on the motor (pivot / turn / reverse in) | gap check through turns on the spot; real edge-to-edge gap for "already close" machines |
| 11 (5 orders, 10 min) | 0 clips, 0 angle, 0 in rack, smallest gap 0.03 m (once), pallets staged in 3B; 5 legs failed on the 2-minute traffic limit: one jack pinned against the rack while backing 0.6 m to a stop on the OTHER face, and the jacks behind held back for it; RT vs RT waits at lane 3B/3E | jack back-ups stay on their own line (compiled, NOT yet re-run) |
| 12 | same aisle-01 jam: a jack's next stop was on the OTHER face 0.6 m ahead (3 m sideways in 0.6 m along cannot be done within 30 degrees), it pivoted into the rack, jacks behind held for it | a jack already in the aisle parks on the line it is on when the other face is out of reach of a gentle lane change; the selector walks across |
| 13 | aisle jam gone (no aisle holds); 3 stagings; a jack reversed into lane 1D 0.9 m off its centre line (the NavMesh had snapped the pivot point) | the route ends on the exact pivot point when the machine fits there; staging re-lines up if > 0.15 m off the lane line |
| 14 | 0 clips; 5-machine gridlock at the dock junction north of lanes 1A/1B (jacks to aisle 01 / lane pivots, RTs to lane 3B) | chain rule: held 12 s+ by a machine that is itself held = the lower one in each link makes way |
| 15 | no gridlock, no aisle jams, 0 angle; 6 pallets staged (1C x3, 3B, 2B x2), the lane-2B re-line-up fired once (0.64 m); 2 clips at the charging-station row (battery trips: EquipmentBattery's final park-in-bay moves are still hand-driven); a loaded RT's square-up turn hit the rack | square-up backs out to the aisle centre line first (compiled, not re-run) |
| 16 | charger / bay finishing moves (EquipmentBattery Face / MoveStraight / idle Guarded*) moved onto the motor; the dock stockers' idle pocket wiggles on the motor fought each other (3 DS clips); a jack drove beside one turning at a staging pivot and they locked 150 s | idle pocket moves back to the original hand moves; a machine moving at its own spot (turning / reversing in) has its turning circle kept clear by everyone else |
| 17 | station-row clips gone, aisles + staging clean (6 stagings); new DS clips at the door pockets (motor turn about a DS's root - its pivot is near the front - swung the body wide); 2 RTs sent to the same reserve slot 01-13-A1 | dock stockers keep their original parking finish moves; jacks and reach trucks finish on the motor (turn about the root, so they land on the pad) |
| 18 (5 orders, 9 min) | **0 clips, 0 aisle angle**, 7 pallets staged (1C x3, 3B, 2B x3); smallest gap 0.10 m (the guard's limit, on hand-driven moves); 5 failed legs (2 from the duplicate 01-13-A1 assignment, the rest 2-minute waits at the dock); the guard undid ~22k hand-driven moves, mostly dock stockers' trailer / pocket code pushing into neighbours | - |
| 10 | 0 motor clips (2 dock stocker trailer-work overlaps, not motor legs), 0 angle, 0 in rack, 1 failed leg (stall timer counted traffic) | stall timer reset by traffic; mouth entry on the clear side; RTs back out from the rack face and square up |

Still open / not proven:
- Charging stations: jacks and reach trucks now finish into a bay on the motor (turn about the root); dock stockers keep their hand moves
  (guarded). No station clips in blocks 17-18.
- Two reach trucks were assigned the same reserve slot (01-13-A1) in blocks 17-18: a work-queue double assignment, not traffic.
- The dock stockers' own trailer / pocket code keeps pushing into neighbours (the guard undoes it thousands of times a block): it should move
  onto the motor properly, as its own task.
- Dock stockers' own trailer load / offload moves are hand-driven (not the motor); the guard keeps them from closing in, but two overlaps were
  still seen at the dock right after a load (likely a carried pallet growing the outline next to a neighbour).
- A reach truck whose replenishment slot is where a jack is picking waits (behind a holding line, others drive round it) until the jack
  moves on, up to the 2-minute leg limit, then the task goes back on the queue. Priority to the truck would mean moving the jack mid-pick.
- The route from the dock into aisle 01's east mouth squeezes past the lane 1A/1B end pallets; jacks sometimes stop there a few seconds
  until the route bends round them.
- Throughput was not measured against the old NavMeshAgent version.
