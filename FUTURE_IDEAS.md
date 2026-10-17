# Future Ideas & Roadmap Notes

Captured 2026-10-13 from Tad's brain-dump. **These are IDEAS, not commitments.** Tad's words: "I'm not planning on implementing all of this" and "maybe down the future you can suggest when it's a good time to start building this up." Claude's job: when the current work makes one of these cheap or natural, say so. Do not build any of it unasked.

Tad has worked in warehouses for 30 years; this game is a bucket-list project. Real domain knowledge is the project's biggest advantage. Prefer authentic operations over invented ones, and let the absurd emerge from the serious (see Design Principle in CLAUDE.md).

Status key: `[core]` should ship before Early Access, `[post]` post-launch / EA updates, `[maybe]` only if the game finds its audience, `[chaos]` belongs behind the "Chaos" rules profile.

---

## 0. Where the game is (Claude's read, 2026-10-13)

The simulation spine exists: inbound (vendors, POs, trucks, receiving, putaway), outbound (contracts, negotiation, orders, picking, staging, loading, billing), money/GL, reputation, dock scheduler, work queue. Tad estimates ~75% of the loop is built. Claude's caution: the last quarter is usually most of the work. Known open items: wholesale full-pallet fulfilment, lighting, onboarding/tutorial, persistence of staged pallets and in-flight trucks, reach-truck/dock-stocker reliability (see `inventory-system` skill).

**Agreed order (Tad approved 2026-10-13; the live copy is the `roadmap` skill):**
1. Stabilize the core loop (including finishing case pick).
2. Tutorial / first-10-minutes onboarding.
3. Player can drive equipment (section 1), with metrics. This is the hands-on loop the game currently lacks.
4. Sanitation + maintenance crews (section 3), giving idle workers purpose and giving physics/rats something to act on.
5. Limited physics events (section 2): case drops and one rack-collapse scenario.
6. Demo + Next Fest. Early Access.
7. Cold chain (section 5), then the competitor layer (section 6), then everything else as content updates.

---

## 1. Player drives equipment `[core]`

Double-click an available forklift / reach truck / pallet jack to jump in and work alongside the AI.

- Metrics tracked per shift: picks/hour, pallets moved, damage events, near-misses, accuracy. Ranked on a leaderboard against the AI crew.
- Output feeds **Reputation** (existing single score) and a new **Manager Prestige / worker respect** stat. Good driving builds respect; hitting a rack (and a rack falling) makes the crew think you are "a clown" (respect and morale drop, gossip, maybe nickname).
- Player claims tasks from the existing Work Queue (`TryClaimSpecificTask`) rather than a separate system, so priorities, reservations and lane locks all still apply.
- Fits the "manager who still gets his hands dirty" fantasy and fixes the "just watching AI" risk.
- Rodeo/muster events become cheap once this exists (section 7).

**Implementation notes:** `ReachTruckOperator` and `TrailerOffloadController` are coroutine-driven, not input-driven, and `SetDriveMode` is the only thing allowed to toggle the NavMeshAgent. Player driving needs a separate controller that takes the vehicle out of AI control (`MHEOperatorSlot` board/vacate), reuses lane-entry locks (`TryEnterLaneForDelivery`) and slot reservations, and releases them on exit. Camera is currently an RTS rig; needs a chase / first-person mode. Driving *feel* is the make-or-break.

## 2. Physics `[core: limited] / [post: full]`

**Design intent (also in `.claude/skills/physics-system/SKILL.md`):**
- Cases become real rigidbodies with box colliders, but **dormant**: kinematic/sleeping until something touches them (forklift hit, being placed, being moved, a rack failing). Cost must stay flat when nothing is happening.
- Scale problem: a busy warehouse is tens of thousands of cases. Never keep them all as live rigidbodies. Pallets stay *data*; on an event, **promote** the affected pallet(s) to physics for a few seconds, then **settle and demote** back to data (or to a debris/loss record).
- Rack collapse: triggered by an impact above a threshold; pre-authored collapse, limited cascade radius, capped number of live bodies.
- Consequences wire into existing systems: rack repair cost and lost inventory go through the finance GL (new lines, e.g. Damage / Repairs), cases lost reduce `PalletMasterRecord` quantity, locations release, `SlotRegistry` / `LocationRegistry` recompute, NavMesh rebakes (debris blocks aisles; sanitation has to clear it).
- Avatars injured in collapses: injury state, downtime, workers' comp cost, safety score.
- Save/load: decide whether scattered debris persists or is converted to a "cleanup needed" record on save.

**Tad's prior implementation (worked well in an earlier project):** two versions of each pallet, a Static and a Dynamic prefab, both children of the pallet root. One big box collider on the root detects any collision; on contact it disables the static version and enables the dynamic one (cases as rigidbodies), which takes over. Cases that fall onto pallets below, or onto the rack, wake those too, so activation cascades. A full rack collapse tanked performance, which is where **ECS/DOTS** came to mind. Notes only, nothing to build yet.

Claude's thoughts for when this is built (not a decision): the static/dynamic swap is the right shape; the same idea as promote/demote above. Add a live-body budget so a cascade is throttled (wake nearest-first, keep far pallets static until reached), put dynamic cases to sleep fast (high sleep threshold, short settle time), use simple box colliders on cases, and consider merging settled debris back into data. ECS/DOTS is probably unnecessary if the live-body budget holds; try the budget first. Pallets built by `PalletBuilder` / `OutboundPalletBuilder` are the natural place to generate the dynamic variant.

**Honest risks:** performance, determinism with the grid/registry model, and NavMesh churn. Prototype with one pallet and one rack before committing.

## 3. Sanitation & Maintenance crews `[core]`

- Not built yet. `Sanitation` role and `Exterminator` already exist in finance routing.
- Sanitation: clears spills/debris, takes trash (Dumpster, Garbage Can, Trash Can objects exist), keeps rats away.
- Maintenance: repairs racks, doors, MHE, later refrigeration; condition decays over time. A tired or drunk maintenance tech can *break* something, creating a scramble (links to cold chain).
- Both are task-queue consumers (new `WorkTaskType`s), which is what the Work Queue was designed for.

## 4. Rats, contamination, and the "chaos" layer

- Rat system design already in CLAUDE.md (hide in pallets, bolt when workers pass, green contamination cloud, breed, damage inventory, morale/inspector/shutdown cascade). Held until the core loop is solid.
- Boss rat ("King of the Rats") `[chaos]`.
- Rotten food that infects workers into zombies, guns for the player and for the crew `[chaos]`. Jump in and defend your associates. Reputation can rise from heroics.
- **Realistic vs Fun profile (Tad's idea):** a rules profile chosen at new game. *Realistic* = rats are a health-inspection problem, rotten food is a recall/quarantine and fine, no guns. *Chaos* = the absurd escalations switch on. Same underlying systems, different consequences. Strongly recommend one codebase, a profile flag and per-event toggles, NOT two games.
- Marketing caution: a Steam page needs to say one thing clearly. Lead with the warehouse sim; present chaos as an optional mode.

## 5. Cold chain & refrigeration `[post]`

- ~1 year out. Authentic to Tad's experience: anhydrous ammonia systems, compressors, evaporators, dock doors, temperature zones.
- Data hooks already exist: `PalletData.AreaCategory { Grocery, Perishable, Frozen }`, shelf life and spoilage, `AreaCode` in the Work Queue.
- Needs: temperature zones per room/rack area, refrigeration equipment as placeable objects with upkeep (GL line), power/upkeep cost, temp logging and compliance audits, door-open temperature drift, alarms, product that spoils above threshold, recall workflow.
- Making it fun: failures and scrambles (a compressor trips at 2 a.m., a drunk tech botches the repair), audits with real stakes, upgrade decisions with visible payoff, and a live "temperature map" overlay. Avoid pure spreadsheet management.
- Tier 3 vendors (caviar, seafood, rare beef) are waiting on this (see PURCHASING_DESIGN.md).

## 6. Industrial park & competition `[maybe]`

- Your warehouse is one building in an industrial park. Sims-style fly-around camera (helicopter), click buildings, double-click to zoom into a rival's interior and watch their bots.
- Competition: vendors give better deals to whoever has better reputation; rivals can poach your customers; contract bidding.
- Dark side: industrial espionage, strikes, arson, theft; loss-prevention / security investment as the answer.
- Honest scope warning: this is effectively a second game. **Cheap first step:** rival companies as an abstract list competing for contracts and a leaderboard (data only, no 3D park). The fly-around park can be a static diorama much later.
- Two Point Hospital (in Tad's Steam library, barely played) is the right reference for the *meta* layer: cheeky tone, ranking, objectives. Play it for a few hours as research before designing this.

## 7. Events & mini-games `[post]`

- Forklift rodeo / "muster" events (these are real events in the industry) and races against bots. Cheap once player driving exists; good for streamers and clips.
- Employee-of-the-month, safety awards, audits, health inspector visits (tie to rats).

## 8. Smaller notes

- Co-op: ruled out as too late; revisit only if the game succeeds.
- Lighting system (purchasable fixtures, safety/accuracy effect) remains on deck before the core loop is "done" (see CLAUDE.md).
- Worker respect / manager prestige should be modelled with the same shape as vendor/customer reputation (one visible score, a few inputs) to avoid a pile of disconnected meters.
- Night shift + darkness + safety is a natural place for the lighting system and sanitation/maintenance to pay off.
- Pallet jack battery charging (Tad, 2026-10-16): a charger placed on a Pallet Jack Parking bay (`PalletJackParkingSpot`, built 2026-10-16) charges the parked jack; jacks drain while driving and must come back to charge. Fits step 3 (player-drivable equipment) or step 4 (maintenance), not step 1.

---

## Decision log
- 2026-10-13: ideas captured; none scheduled. Suggested order recorded in section 0.
