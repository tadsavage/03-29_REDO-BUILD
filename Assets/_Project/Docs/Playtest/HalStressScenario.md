# HAL stress scenario: large case-pick orders (2026-11-12)

**What it is.** One button (HAL tab > TEST SCENARIO: LARGE CASE-PICK ORDERS > BUILD THE SCENARIO, in Play mode) that turns the loaded warehouse
into a repeatable stress test, then saves it to a slot. `PlaytestBotSettings.startFromSlot` (HAL tab: "Start from saved game") makes
INITIATE HAL 9000 load that slot, so every block starts from the same stocked warehouse and the same big orders.

**Steps it runs** (`HalStressScenario.Build`, `Scripts/1. FSM/Core/Diagnostics/HalStressScenario.cs`):
1. `TestRackStocker.FillRacks(palletsPerSku)`: every SKU gets a pick face and reserve pallets (real pallets, free).
2. N large orders (default 6 x 38-50 lines x 4-9 cases = about 150-450 cases each). Sized against everything on hand (faces + reserve) minus what
   open orders owe, so every line is fillable and the reserve -> pick-face replenishment loop is part of the test. Lines stay under 45% of a
   pallet (a case-pick test, not pallet picks).
3. Released to the staging lanes the way the Scheduler's RELEASE ORDERS does.
4. Saved to the chosen slot (default 8).

**Why.** The saved game of 2026-11-11 had six orders of 245-518 cases but only 2 of 64 pick faces stocked (44 cases): stock-outs, not
traffic, were what limited it. The old Dev Console button (FILL RACKS + ORDERS) makes 12 orders of about 60 cases, which never needed more than one
selector at a time.

**Use.** 1) Load a warehouse with the crew you want (3+ selectors and jacks for a real test). 2) Build the scenario. 3) HAL tab: "Start from saved game"
= that slot, INITIATE HAL 9000. Compare blocks with the recorder (`JackTrafficRecorder`) if you want the aisle-level numbers.

**Built and verified (2026-11-12).** Compiles; ran on the "Built_Hired" slot (Day 1 08:08, 3 selectors, 3 jacks, 2 reach trucks, 3 dock
stockers, four 245-518 case orders): 192 pallets stocked, 64/64 pick faces, 6 more large orders, 10 orders / 3,284 cases, all released. Saved to
SLOT 8 (file slot_7_data.json). Baseline 08:11 -> 14:00 at 5x with the current code: crew picking 10-30% of the time, HELD 61-77%, 360 jack-seconds
stuck. The jams: the dock / staging corridor (x -7..3, z 55-58), the aisle 01 door (x -23..-17, z 42) and reach truck vs jack standoffs
("cannot reverse safely for RT_Full" / "cannot find a safe way round PltJack_FULL"). A tie-break that dropped the jack's 1 m courtesy buffer
after 8 s of waiting on a stationary truck was tried and removed: it fired but just moved the jam (it is a crowd, not a pair).

**Where the held time really is (2026-11-12, run from slot 8).** Not the dock / staging corridor (about 15% of held samples at z 48-58): 61% was
at the AISLE 01 MOUTH and just inside it (x -24..-12, z 40-44), about half of that behind a reach truck replenishing a reserve rack in the same
aisle. A second run from the same slot was far worse (crew picking 3%, held 87%, three jacks wedged at x -11..-8, z 41.5 in front of the aisle 01
door with a reach truck beside them), so the scenario is chaotic: tiny start differences change who gets stuck. Compare several runs, never one.
Tried and removed: (a) 8 s tie-break dropping the 1 m reach-truck buffer; (b) passing a stationary reach truck on the lane with 1.3 m beside it
(never fired: the stalls are at the mouth, before the jack is inside the aisle).
Likely root cause: "three doors aisle" has TWO stocked aisles and ONE entry per aisle, while the scenario crew is 3 selectors + 3 jacks + 2 reach
trucks + 3 dock stockers, and FillRacks assigns faces in pick order so every order's first lines are in aisle 01. Next ideas: spread selectors
across ORDERS by start aisle (WorkQueueSystem.TryClaimNextTask already deals plan numbers apart within one order), treat a reach truck in the
door like a parked jack in the door (DoorBlocked), or size the scenario crew to the building (2 selectors for 2 aisles).

**Incident.** `BugFocus` quicksaves over `quicksave.json` whenever a bug toast pauses and zooms the game. During a test run that replaced Tad's
Day 1 13:19 quicksave, and the next Play loaded it. Fixed: the file being replaced is first copied to `quicksave_before_bug.json`. Test runs
should start from slot 8 (or any slot), never rely on `quicksave`. Also fixed: `RotatingBeacon.LateUpdate` threw every frame after a recompile in
Play (null `_mpb`), which wrote a 15 GB Editor log.

**Not built yet / limits.** The crew is whatever the loaded game has (it does not hire). Customers are random per order. Verified: not yet
(written with Unity closed; needs a compile and one live run).
