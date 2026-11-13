# Nav A/B checklist: jacks on the NavMeshAgent vs the grid planner (2026-11-12)

Question: with ONLY the pallet jack switch changed, does the crew get more picking done and spend less time held?
Method: the other session's recorder (`JackTrafficRecorder`), same stocked warehouse, bot stopped, several runs per arm, compared as ranges.
Background: `HANDOFF_2026-11-12_nav.md`, `HalStressScenario.md`, `JackTraffic_2026-11-10.md`, and the "CURRENT WORK" block of `CLAUDE.md`.

## What I read in the recorder (`Scripts/1. FSM/Core/Diagnostics/JackTrafficRecorder.cs`)

- Started FROM CODE only. There is no button or menu: `GameCore.Diagnostics.JackTrafficRecorder.Begin(runs, speed, endHour = 12, reload = true, firstRun = 1)`.
- Per run it sets `Time.timeScale = speed` (and holds it there), samples every jack twice a real second, and stops when the game clock reaches
  `endHour` (default 12:00) or after 30 real minutes. Then it writes `Logs/JackTraffic/run<N>_samples.csv` and `run<N>_log.txt` and pauses.
- `reload = true` calls `QuickLoad()` between runs. **Do not use it**: its own comment says quickloading over an already-loaded scene leaves every pick face
  reading empty. For clean runs start Play fresh, load the save, and call `Begin(1, 5f, 12, reload: false, firstRun: N)` once per run.
- It records no nav setting. **The run number is the only label**, so number the arms: A (all NavMesh) = 1-6, B (jacks on the grid) = 11-16.
- It only reads private selector fields by reflection and changes nothing in the simulation. The grid jack leg sets the same fields (`_legTarget`, `_travelDir`,
  held clocks), so the columns are filled for both arms.
- `Logs/` is git-ignored. The earlier raw data and `analyze.py` are not in the repo (and not on this machine): use `analyze_jack_traffic.py` (next to this file).

## Blockers to clear first (checked 2026-11-12)

- [ ] **Slot 8 does not exist on this machine.** `%USERPROFILE%/AppData/LocalLow/SavageDev/Warehouse/Saves` has `slot_0..slot_4` only; the stress scenario was saved on the other
      machine / clone. Either copy `slot_7_data.json` + its `metadata.json` entry across, or build it here: load a warehouse with 3+ selectors and jacks,
      then HAL tab > TEST SCENARIO > BUILD THE SCENARIO (saves to slot 8 by default). Check the clock reads Day 1 ~08:13 when you load it.
- [ ] The scenario you want ("outbound orders in place") is this same save. Make it ONCE and use that one file for every run in both arms.
- [ ] Nothing else running in the editor: no HAL block, no other session's Play.

## One run (about 4 minutes when Unity has focus: ~2.3 min of play at 5x from 08:13 to 12:00, plus loading)

1. [ ] **Set the arm.** Tools > Navigation: Arm A = "All back to NavMesh". Arm B = tick only "Grid legs - Pallet Jacks" (Reach Trucks and Dock Stockers stay unticked).
       The checkmarks show the state. The switches are read live, so set them BEFORE step 4.
2. [ ] **Press Play (fresh).** Do not reuse a Play session that already ran a recording.
3. [ ] **Load the scenario save without starting HAL** (preferred): main menu > Resume Shift > slot 8.
       Or, if you use INITIATE HAL 9000 with `startFromSlot = 8`: turn `plannerEnabled` off first, then press STOP on the HAL tab as soon as it loads.
       Either way the bot must not be running. The always-on observer ("PLAYER block ... observe only") is harmless: it makes no decisions.
4. [ ] **Start the recorder** (ask me, or run it through the Unity bridge): `GameCore.Diagnostics.JackTrafficRecorder.Begin(1, 5f, 12, false, N)` where N is this run's number.
       Do not touch the game speed.
5. [ ] **Watch for** `[JackTrafficRecorder] run N done at D1 12:00 after NNs real.` Then stop Play.
6. [ ] **Write down**: run number, arm, anything odd (a pop-up, a long freeze, a jack that never moved, orders dropping to Pending).
       **Sanity check on the first run of a session**: in the first minute at least one selector should be mounted on a jack and orders should not be dropping back to
       Pending/Open. If every pick face reads empty, the load went wrong: stop Play and start over. Treat the very first run as a dry run and discard it.

## Order of runs

Interleave so drift (Unity warming up, the machine busy) cannot favour one arm: A1, B11, A2, B12, A3, B13, ... up to 6 per arm.
Minimum 3 per arm; the other session's runs swung picking 3% to 21%, so fewer than 3 tells you nothing.
Twelve runs is about 50 minutes with Unity focused. A Unity in the background runs several times slower: do not leave it unfocused.

## Reading the result

From the project root:

    python Assets/_Project/Docs/Playtest/analyze_jack_traffic.py --a 1-6 --b 11-16

It prints mean and range per arm for: picking %, held %, corridor % (share of held time at z 48-59), stuck seconds, and counts of `[JackSolid]`, `[JackStall]`,
`[GridNav]` (errors), "giving way", step-off-and-walk and re-issued drives, then a verdict helper per metric.

- B better: picking up and held / stuck / corridor down, AND the B range sits clear of the A range.
- Ranges overlap: no conclusion. Run more, or accept it makes no difference. Never judge from one run.
- Any `[GridNav] ... no route` in arm B is a bug in the grid leg, not noise: read `run<N>_log.txt` for it and tell me which run.
- The script's definitions are its own (see its header). Compare arm with arm from the SAME script, not with the old notes' numbers.

## Afterwards

- [ ] Untick "Grid legs - Pallet Jacks" (or "All back to NavMesh") so the next block starts on the NavMesh.
- [ ] Keep `Logs/JackTraffic/` (git-ignored) until the result is written up; copy the A and B summary into `HANDOFF_...` or `CLAUDE.md` "CURRENT WORK".
- [ ] B clearly better -> repeat with only "Grid legs - Reach Trucks". Not better -> the planned fallback is manned machines as a soft cost in the planner.

## Not verified

I have read the recorder and tested the analysis script on synthetic files only. I have NOT run the recorder, and I have not run this exact sequence end to end.
