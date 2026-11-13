# Playtest benchmark notes - 2026-11-02 (archived from the CURRENT WORK block of CLAUDE.md on 2026-11-12)

Archived verbatim. Still-relevant facts were carried into the new CURRENT WORK block ("Still true / open"); everything else is the record of
blocks 29-33, the throughput cap found in the bot, the `AiNavigation` backstop fixes and the cross-aisle layout.

## CURRENT WORK — Playtest bot benchmark (2026-11-02)

The playtest bot (`PlaytestBot`, Tools > Playtest Bot) scores 7-day "blocks". Blocks 29-33 are on the
board. Throughput has been pinned at **6 pallets per block for four blocks running**.

### The headline finding: throughput is capped by the BOT, not the warehouse

- `SignContracts` signs every available non-bulk offer up to `maxRecurringAccounts` (3). Exactly one
  is ever on offer — `Contract_Starter`.
- Bulk orders, where real volume comes from, are gated on `FreePallets() >= max(12, order size)`.
  With one starter contract the bot only buys its 2-day cover (~14 tiny POs), so it never holds 12
  free pallets, so it never signs a bulk order, so demand never grows. Closed loop.
- Every block reloads the day-1 save, so reputation restarts at 0 each time (see the scoreboard:
  "0 to 16", "0 to 18"). The benchmark has measured the first 8 days of a brand-new company 33 times.
- The 12 pallets/day (84/block) target belongs to a mature warehouse. Throughput scoring 7.1/15 and
  revenue ~3/15 for four blocks is that mismatch, not bugs.

### Next step (agreed, NOT yet done): continuous blocks

Set `pauseAtBlockEnd = 0` ("Pause when a block ends" on the HAL tab). `PlaytestBot.FinishBlock`
already ends with `if (S.pauseAtBlockEnd) Time.timeScale = 0f; else StartBlock();` — so blocks roll
straight on, the company carries cash/reputation/contracts/stock forward, and day ranges advance
(block 34 = days 8-15, since `_startDay = time.Day`).

Caveats: `HalPlanner.PlanAndApply` fires on every `StartBlock`, so growth changes compound
unattended, and there is no window to apply fixes between blocks. Run as a batch, then stop.
`saveAtBlockEnd` still writes `bot_block_N` snapshots so you can rewind.

### Changes made this session

1. **`AiNavigation.cs` — seek no-progress backstop.** Progress was measured with
   `agent.remainingDistance`, which is `+Infinity` whenever there is no usable CURRENT path —
   including mid-repath, where `pathPending` is already false and `pathStatus` still reports the
   PREVIOUS path's result. Infinity never beats `_seekBestRemaining`, so every such frame counted as
   no-progress and the backstop fired mid-walk, handing tasks back from 25m out; the receiving
   workflow then refused (max 4.5m) and the task bounced round the queue forever. Now measured
   straight-line to the target. Verified live: the log reads `stopped 9.13m short` instead of
   `stopped Infinity m short`.
2. **`AiNavigation.cs` — `PathPartial` branch.** It declared "arrived" once the agent reached the end
   of the partial path, which can be 15m from the real target (logged `stopped 0.00m short` while the
   worker stood 15.7m away). Added `PartialArrivalMaxGap = 4f`; beyond that it reports the target as
   unreachable. Runtime behaviour is unchanged — same callback either way, which is this file's
   hand-back idiom — but it stops lying, and the new wording is caught by the bug monitor.
3. **`PlaytestBot.cs` — `WatchWarnings`.** A warning only counted as a bug if it matched that list,
   and none of the receiving-bounce messages did (`"cannot reach"` is not `"could not reach"`). Block
   30 scored Reliability 80/100 off TWO bugs while a receive task bounced all block. Added
   `"cannot reach"`, `"no reachable"`, `"not reachable"`, `"only partially reachable"`,
   `"refusing to start receiving"`, `"staging short"`.
   **Bug counts are not comparable with blocks 1-30**, which were scored blind to this.
4. **`HalPlanner.cs` — one change per block.** `PlanAndApply` now skips if the ledger already has a
   row for that block number. Block 29 was started six times and fired SIX dock stocker operators
   (changes.json held six rows, five unmeasured). **If you restart a block from a reloaded save,
   delete that block's row from changes.json first** or it will run without its growth change.
5. **`changes.json`** — removed the five stale unmeasured block-29 rows.
6. **NEW `Resources/HalLayouts/three doors aisle.json`** — `three doors` with lane rows at cell y 83-84
   (z 44.34 / 45.66) removed to give a cross-aisle. Lanes 75 -> 45. `layoutName` now points at it.

### Why the cross-aisle was built, and what it actually did

Staged outbound pallets carve the NavMesh (`OutboundPalletBuilder.SetNavObstacleActive`,
`carving = true`, `ObstacleFootprint = 1.15`). Grid pitch is 1.325 and agent radius 0.5, so two
adjacent staged pallets leave **0.175m** against the **1.0m** an agent needs — *any two adjacent
staged pallets are impassable*. The old 5-deep lane block therefore had interior rows reachable only
through other rows.

The cross-aisle did fix that: block 33's "Fixed / no longer seen" list is six
`has no reachable pick location` entries. It did **not** move throughput, for the headline reason above.

You cannot tune `ObstacleFootprint` out of this: threading 1.0m at 1.325 pitch needs a carve of
<= 0.219, about 18% of the pallet's real footprint.

### Still open

- **Edge-of-floor targets are unreachable.** `(-3.71, 1.13, 50.96)` is ~0.66m past the floor's north
  edge; `(-47.09, 1.12, 45.66)` sits 0.16m inside the walkable band after 0.5m agent erosion.
- **A second reachability problem inside the rack block** (x -37.8..-19.3, z 37.71..49.64), e.g.
  `(-20.59, 1.11, 37.71)`. Separate from the lanes; untouched.
- **Fill rate is falling** (68 -> 66 -> 55) and it is now stock-outs
  (`N case(s) not in stock - cut from the order`), not reachability.
- **"B" was never done.** The map is built at runtime (Sims/SimCity style), so a player can rebuild
  the same trap. The intended systemic fix is a placement rule: *no lane cell more than one row from
  open floor*, i.e. staging at most 2 deep between aisles. That is also how real warehouses are laid
  out, and it would make HAL's layouts correct by construction.

### Geometry reference (`three doors` / `three doors aisle`)

`PlacementGrid`: `CellSize 1.325`, `Origin (-66.3, 0.005, -66.3)`.
World position = `Origin + cell * 1.325 + 0.6625`.

```
RACKS       x -37.81 .. -19.26   z 37.71 .. 49.64    (96 cells)
SHIP LANES  x -13.96 .. +9.89    z 41.69, 43.01, [cross-aisle], 46.99   (45 cells after the fix)
DOCK DOORS  x -11.31, -2.04, +7.24    z 36.39
FOUNDATION  x -47.09 .. +11.21   z 36.39 .. 49.64
```

The X aisles between the three lane groups are 3.97m (1.83m walkable after erosion). The new Z
cross-aisle is 2.83m clear / 1.83m walkable — deliberately matched to the X aisles, which work.

### Gotchas worth keeping

- Saves: `SaveSystem.SaveFolder` returns `Assets/_Saves` only if it is non-empty, otherwise
  `persistentDataPath/Saves`. `autosave.json` and `slot_0_data.json` there are a DIFFERENT, older
  warehouse (all negative Z) — not the one the blocks run in.
- `PlaytestBot` is created with `HideFlags.DontSave`, so it does not show up in a GameObject search.
  Use the console (`[PlaytestBot] Block N started...`) to tell whether a block is running.
- `HalBuilder` has no editor menu item. It is launched only by **INITIATE HAL 9000 on the main menu**
  (PlayerPrefs `HalAutoStart`). With `buildOnly = 1` it builds, hires the crew, then pauses and hands
  over so you can save the result as a baseline.
- Editing any `.cs` while in Play mode triggers a domain reload that kills the running block (and
  strands reach trucks mid-carry). Stop the bot first.
