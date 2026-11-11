# Pallet jack congestion - 8 watched runs (2026-11-10)

Quicksave Day 2 08:20 -> 12:00, eight clean runs (fresh Play each time; the playtest block was running).
Three selectors (Susan Sanders, Elizabeth Barton, Ronald Wright) on one order, OG3936 (Sneaky Pete's, 333 cs, 50 lines).
Recorder: `JackTrafficRecorder` (Diagnostics), raw data + `analyze.py` in `Logs/JackTraffic/` (git-ignored).

## Numbers

| | Susan | Elizabeth | Ronald |
|---|---|---|---|
| picking / off the jack | 85-92% | 19-54% | 3-60% |
| held behind another jack | 0-1% | 15-35% | 3-58% |

- 984 jack-seconds stuck across 8 runs (episodes of 5 s+ aboard and not moving). Longest: 95 s (run 2), 67 s (run 8).
- In 3 h 40 game-minutes, OG3936 got about ONE pallet cubed out across three selectors.
- Hot spots: the mouth of aisle 01 - (-17,43), (-15,45), (-16,43), (-13,44) - and just inside it, (-21..-23, 42..43).
- Events over 8 runs: back-up rule 16, step off and walk 12, pass 5, JackSolid 16, JackStall 36, re-issued drives 33.

## What is actually happening

1. **One door per aisle, and the first picks are in the doorway.** Aisles are one-way, entered from the low-numbered end at a single
   entry point (`lane.Lane(AlongMin - AisleEntryLead)`, aisle 01 = (-17.6, 42.8)). The order's first picks are bays 01-01..01-03, so the
   selector picking them parks right inside that door, and the door stays shut for as long as she picks (85-92% of her morning).
2. **Everybody gets the same pick path.** All three tasks start the serpentine at its beginning. In this save the three OrderSelect tasks
   have no pallet plan (`HasPlan = false`), so each selector simply takes the next unreserved slot from 01-01. Even with plans the
   pallets are consecutive slices of one path (pallet 1, 2, 3 = bays 1-3, 4-6, 7-9), so three selectors still start at the front of aisle 01.
   The result is a convoy: one picks, two wait.
3. **The waiting rules fight each other at the mouth.** Held 5 s -> step off and walk -> "the way ahead has cleared" -> back on -> re-park
   -> held again (Elizabeth, run 2). The 1.5 m back-up fires, then the same two meet again. Passes inside the aisle end in JackSolid against the
   rack end (`Full Bay Orange 80"`) because the pull-out starts too close to the end of the rack.
4. **Mixed traffic at the mouth.** The aisle 01 mouth is also the cross-aisle the dock stockers and the reach truck use for the staging
   lanes (10 "giving way to reach truck", several JackSolid on DS_FULL).
5. **Every run starts 25-34 s late.** The selectors walk ~30 m to the jack parking at x 12.5, the walk gives up ("no progress ... 21-31 m
   short"), retries, then the jacks loop north (z 54) and back round to aisle 01.

## Separate bugs seen (not traffic)

- **F9 on top of a loaded game breaks picking.** After a quickload over a running game, every pick face for OG3936 reads empty
  (inventory says 50/50 lines in stock), selectors hand their pallets back and the order drops to Pending/Open. A fresh Play load is fine.
- **Load mid-pick:** on load a selector "took over the pallet already on the jack (0 cases picked)"; the cases picked before the save are not on it.

## Fix A - built and measured (2026-11-10)

- `OrderSelectionTaskDriver.TryFindBestPickLocation`: while a trip has nothing on its pallets (unplanned trips only), a first stop within
  2 bays behind .. 3 bays ahead of another selector's current stop in the same aisle is moved past it (`StartTooCloseToOtherJack`,
  switch `SpreadTripStarts`). Looks at every selector (`s_allDrivers`), not only mounted ones - selectors walking to their jack had
  already chosen their first stop. Log: "X is working near A - starting this trip further on instead."
- `WorkQueueSystem.TryClaimNextTask`: an order's planned pallets are dealt out far apart (furthest plan number from those being picked).
  Not exercised by this save (its tasks have no plans).
- A failed bay leg (5 retries) no longer carries on BY HAND: cases aboard -> stage them (short pick); nothing aboard -> leave the jack and
  hand the task back (`GiveUpTripNothingPicked`). Run 2 of the first A/B had glued the pallets to Ronald again.

A/B at 5x, 4 runs each, same quicksave, alternating:

| | A off | A on |
|---|---|---|
| stuck jack-seconds per run (excl. start walk) | 29.1 | 19.9 |
| selectors held | 32.9% | 22.5% |
| selectors picking | 45.5% | 57.2% |
| pallets cubed out by noon | 0 | 0.8 |

Every remaining stall is at the aisle 01 door, (-17..-18, 43): a selector picking bays 01-01/02 parks inside the only entrance and the
others (whose stops are now further in) can't pass from outside the aisle. That is fix B.

### Fix A, second pass

The spread first triggered only on a brand-new trip (`_pickFloorKey == MinValue`); selectors resumed from a save already had a floor and
were exactly the ones piling into bay 01-01. Now the trigger is "nothing on this trip's pallets yet". With that, fix A alone (the B-off arm
below) averaged 5.1 stuck jack-seconds per run, down from 29.1 with no fix.

## Fix B - built, no measurable gain on this save

Not the holding-spot ticket: that would only move the queue. Instead, `DoorBlocked` + DriveBayLeg: when a STOPPED jack sits in the door
(first 2 bays, rack lane), this jack's stop is past it and the passing lane is clear, line up on the passing lane at the mouth, drive in past
it and pull back in (or park on the passing lane level with the stop when it is too close to pull in). `DecideHeldInAisle` may also pass /
walk while waiting AT the mouth. Switch: `DoorBypassEnabled`. Log: "X is parked in the door of aisle 01 - going in on the passing lane."

A/B at 5x, fix A on in both arms:

| | B off | B on |
|---|---|---|
| stuck jack-seconds per run | 5.1 | 11.1 |
| held | 7.9% | 14.7% |
| picking | 72.0% | 64.4% |

Both arms are bimodal: 5 of 8 runs had nothing stuck, 3 had ~20-22 s, one of those in the B-off arm and two in the B-on arm. It is the same
event every time: Ronald's first stop lands right beside Elizabeth while she picks in the door, so he waits 5 s and walks. B fired only
twice, both near noon. So the difference is noise; B neither helped nor hurt measurably here.

## Lane spacing + passing (Tad, 2026-11-10)

- Rack lane = jack centre 1.25 m off the rack face (`LaneMinFromSlot` 2.0). Aisles in "three doors" are 6.6 m wide: rack lane at
  +1.31 from the centre line, passing lane 1.5 m to its left (-0.19).
- Passing: out to the left as before; the pull-in starts as soon as the passer's CENTRE is past the front of the jack being passed,
  as a smooth right turn (`SteerIntoLane`: aim 2.2 m ahead on the rack-lane line, 40 deg/s while driving) until parallel with the rack
  faces. Stopped jacks close together in the rack lane count as one obstacle (`ParkedGroupFront`) so it never steers in at the next one.
  A pull-in that reaches the stop ends there.
- Verified in a recorded pass (spread off): up the passing lane past Susan, turn starts as the centre clears her front, heading
  270 -> 286 -> 279 -> 275 -> 272 -> 271, back on the rack-lane line. That run used the gentler 3.0 m / 30 deg/s settings, took ~10 m
  and overshot the stop; the tighter settings and the stop limit are compiled but no pass happened in the run after.
- Removed the same day: parking on the passing lane beside a parked jack (shut both lanes) and lining up behind a door jack's forks
  (put the approach among the staged pallets).
- `AbandonJackOnFoot` no longer re-parents pallets onto the employee from ANY path (the no-progress watchdog did it too): picked
  pallets stay on the jack and the stranded-jack rescue takes it over; nothing picked = task handed back.
- `KeepClearOfOtherJacks` never pushes a park spot out of its aisle into the doorway.

## Leapfrog + fluid case pick (Tad, 2026-11-10)

- LEAPFROG: after a pass (or a door bypass) whose stop is short of "the next facing past the passed jack, plus one more", the trip's
  sequence floor moves there and the pick loop re-chooses its next pick (`PassReplanFloor` / `TakeReplanAfterPass`). Unplanned trips
  only; nothing ahead = keep the old stop. Log: "passed the jack ahead - skipping X and picking on from the bay past it." Before, the
  passer drove out of the far end and came all the way round to its old stop.
- FLUID CASE PICK: from a queue stop the walk-or-drive decision is made right after the case goes on the pallet. Keep picking on foot
  while the pick is within walking reach; get back on only when the walk is longer than MaxPickReach AND there is room to drive
  (`RoomToDriveTo`: no jack in the rack lane between this jack and a jack-length past the slot). The first walk after stepping off
  goes straight to the slot (`WalkRouteFromHere`), no "touch" of the pallet first.
- Spread off (forced convoy): 0 stuck, 82% picking, 2 pallets cubed; Elizabeth went from on/off every case to off once, on once.
  All fixes on: 0 stuck, 72% picking.

## Suggested fix (one change per block, measure with the recorder: baseline 984 stuck jack-seconds)

A. **Spread the selectors out (biggest win).** When a selector starts (or resumes) a trip and another jack is working the same aisle,
   its first stop must be at least ~3 bays beyond that jack's current stop, or in another aisle; it picks from there forward and the
   skipped slots fall to whoever is behind. For planned pallets, deal the plan tasks out so concurrent selectors take far-apart pallets
   (1, 4, 7 instead of 1, 2, 3). This is how real pick zones / leapfrogging work.
B. **Aisle entry ticket.** Like the staging-lane lock: a jack may enter an aisle only when the entry block (first 2 bays) is free;
   otherwise it waits at a holding spot beside the cross-aisle (not in front of the mouth), first come first in.
C. **Simplify the waits once A+B are in.** Drop "step off and walk after 5 s" at the mouth (it causes the on/off loop) and only
   pass inside an aisle when the pull-out is clear of the rack end.
D. **Small:** park jacks nearer the pick area, and fix the first walk to the jack giving up 20-30 m short.
