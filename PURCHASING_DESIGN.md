# Purchasing & Vendor System — Design Doc

> **Status:** **Phases 1, 2 and 4 BUILT and live-verified 2026-08-18** (Phase 3's core came with 2). Phase 5 (cold chain) is design only.
> **Authored:** 2026-08-18 (Tad + Claude design session)
> **Scope:** How product gets INTO the warehouse. The inbound counterpart to
> `GAMEPLAY_LOOP_DESIGN.md` and the contract/customer system documented in `CLAUDE.md`.

---

## Context — why this exists

Inbound procurement is the weakest link in the loop. Today the player opens the Purchasing panel
(key `9`), builds a basket against a flat list of 31 SKUs, pays, and a truck arrives carrying exactly
what was ordered. There is one supplier — hardcoded `"PLAYER_SUPPLIER"` / `"Wholesale Supply"` at
`PurchasingPanel.cs:968` — one price per SKU that never moves, and no way for the order to go wrong.

Tad's description: *"a two dimensional pick items from a list like you're at a restaurant and order
them and they get delivered to your warehouse."*

The fix agreed in this session: **vendor tiers gated by a Reputation score**, plus the supporting
mechanics that turn a purchase into a bet instead of arithmetic.

This is a standing reference because purchasing is going to be a multi-session build. It lives in the
repo (not just local memory) so it's available on either machine.

---

## Grounded findings

Measured against the live scene and the assets on disk during the design review — not assumed.

**1. Revenue has never fired.**
`RevenueToday = 0`, `RevenueYesterday = 0`, `RevenueThisWeek = 0` against `ExpensesThisWeek = 26,286`
on Day 4 of a real save. Money has only ever left the building. Whatever purchasing becomes, the
first thing that has to be true is that shipping a load **pays you and you see it land** — until
then, every buying decision is unmotivated by construction.

**2. Receiving variance is plumbed but structurally impossible.**
`ShipmentLineItem.Overage` and `.Shortage` are computed and logged, but `UpdateReceivedQuantity` is
called from exactly one place — `ShipmentReceivingCoordinator.cs:96` — with the quantity of the
pallet that physically arrived. And the trailer is built from the PO. **Received always equals
ordered.** The gun is built and unloaded.

**3. Current margins run backwards from the intended tiering.**

| SKU | Buy | Sell | Markup |
|---|---|---|---|
| Water | 5 | 12 | **140%** |
| Salt | 10 | 18 | **80%** |
| Bread | 15 | 26 | 73% |
| Coffee | 45 | 72 | 60% |
| Honey | 42 | 65 | 55% |
| Maple Syrup | 55 | 85 | **55%** |

Today the cheap staples are the *fattest* margins — the exact opposite of the design below.

**4. All 31 SKUs are identical in kind.** Every one is `AreaCategory.Grocery` with
`shelfLifeDays = -1`. No perishables exist. (SKU `999999` "Booty Cheese" is test data.)

**5. Useful things already in the code, currently unused:**
- `PalletData.AreaCategory { Grocery, Perishable, Frozen }` — the cold-chain enum already exists
- `PalletData.PalletStatus { Shippable, QAHold, Lost, OnReserve }` — `QAHold` is a ready-made hook
  for damaged/contaminated product
- `SignedContract.SatisfactionPercent` — per-customer standing, 0–100, persisted, −5 per miss /
  +1 per on-time. A proven, tuned precedent for per-counterparty relationship tracking.
- `ContractData.CreateRuntime(...)` + `OrderArrivalService.AddOffer(...)` — the pattern for
  generating counterparties at runtime, already working (the Dev Console's TEST CUSTOMER button).

---

## Scope decisions

Three ideas from the brainstorm were at risk of over-scoping. All three resolved:

| Idea | Verdict | Reasoning |
|---|---|---|
| Oblivion-style persuasion / barter minigame | **Cut** | Tad self-rejected; agreed. The fantasy here is *operational* mastery, not social skill — and it would need writing, faces, and dialogue trees to land. |
| Literal contraband (Buccaneer illegal goods) | **Reskin to salvage / close-out** | Real contraband implies inspections, seizure, and fines — a whole second failure system with its own UI, and it shifts the game's tone. Salvage delivers the same "rare vendor, huge margin, real risk" thrill, is authentically warehouse, and needs no enforcement system. |
| Luxury tier = caviar / rare beef / seafood | **Defer to a cold-chain milestone** | All three are cold chain, which means Perishable/Frozen storage areas, refrigerated rooms in build mode, and power upkeep. Tier 3 launches **ambient** so the vendor system isn't blocked behind a build-mode feature. |

---

## The spine: one Reputation, two consumers

`CLAUDE.md` already carries a designed-but-unbuilt reputation system for the **customer** side
(gating which contracts get offered). The vendor reputation described here is **the same number**.

```
                       ┌──────────────────────┐
    ship well   ─────► │  ReputationService   │ ─────► which CUSTOMERS offer you contracts
    receive well ────► │       0 – 1000       │ ─────► which VENDORS answer your calls
                       └──────────────────────┘
```

One score, one system to build and tune, one number for the player to read — and both halves of the
game feed the same spine. Shipping well earns you better buyers *and* better sellers at once.

### Bands

| Range | Title | Unlocks |
|---|---|---|
| 0–99 | Unknown | Tier 1 vendors only |
| 100–299 | Known | Tier 2 vendors |
| 300–599 | Respected | Tier 3 vendors |
| 600–849 | Preferred | Better terms across all tiers; the Broker starts calling |
| 850–1000 | Untouchable | Allocation priority; first refusal on rare loads |

### Inputs

Slow to gain, fast to lose — deliberately mirroring the `SatisfactionPercent` philosophy already
tuned and proven in the codebase (−5 per miss vs. +1 per on-time).

**Gains**

| Event | Points |
|---|---|
| Order shipped on time at 100% fill | **+5** |
| Order shipped on time below 100% fill | **+2** |
| Inbound trailer offloaded inside its booked dock block | **+2** |
| Clean week (no late, no scratch, no driver kept waiting) | **+15** |

**Losses**

| Event | Points |
|---|---|
| Order shipped late | **−10** |
| Order shipped under 90% fill | **−8** |
| Order cancelled or failed outright | **−25** |
| Driver kept waiting past the booked block, per hour | **−3** |
| Contaminated or damaged product shipped to a customer | **−40** |

That last line is the economic hook for the deferred rat system and for fire/smoke damage. It's what
eventually makes the chaos layer *matter* rather than being pure flavor.

### Two layers, not one

- **Reputation (global)** gates **who will deal with you**.
- **Standing (per-vendor)** gates **the terms you get inside that vendor** — mirroring the existing
  per-customer `SatisfactionPercent` exactly, so the inbound and outbound sides read identically to
  the player. Standing rises with volume and on-time offloads; falls with cancelled POs and refused
  allocations.

---

## Vendor roster

Four tiers. Every vendor is a **character with terms you learn the hard way**, not a price list.

### Tier 1 — "Everyone's first call" *(Reputation 0)*

Two vendors. The starter set every player begins with.

- **Goods:** Water, Salt, Flour, Sugar, Rice, Oats, Canned Corn, Canned Beans, Canned Tomato,
  Spaghetti, White Vinegar, Bread
- **Terms:** ~92% on-time, ~97% fill, no minimum order, pay on placement
- **Margins:** thin — target **25–40%** markup (the cheapest SKUs round upward out of the band;
  Water at a $5 buy has no integer sell price between 20% and 40%)
- **Character:** dependable, dull, never generous. They will never be the reason you win. But they're
  always there, and demand for staples never stops.

### Tier 2 — "Household" *(Reputation 100+)*

Three vendors, deliberately differentiated so that **the choice between them is the mechanic**.

- **Goods:** Ketchup, Mayonnaise, Peanut Butter, Cereal, Cookies, Crackers, Potato Chips, Juice,
  Macaroni Cheese, Soy Sauce, Vegetable Oil, Tea, Pancake Mix, Tuna — plus new non-food household
  items (laundry soap, paper goods; butter once cold chain lands)
- **Differentiation axes:**
  - *Cheap but flaky* — 85% on-time, 90% fill, lowest price
  - *Expensive and reliable* — 98% on-time, 99% fill, premium price
  - *Net-30 terms* — pay in 30 days instead of on placement, which makes **cash flow** a real
    mechanic: you buy before you're paid
- **Margins:** mid — target **45–60%**

### Tier 3 — "Specialty" *(Reputation 300+)*

Two to three vendors. **Ambient only for now** — cold-chain headliners come in a later milestone.

- **Goods:** single-origin Coffee, Maple Syrup, Honey, Peppercorn, plus new items — truffle oil,
  saffron, aged balsamic, imported olive oil, aged spirits, artisan chocolate
- **Terms:** *allocation-based* rather than open ordering — **"we release 4 pallets a week; at your
  standing, you get 1."** High minimums. Refusing an allocation costs Standing.
- **Margins:** fat — target **80–140%**
- **Character:** gatekeepy. They are evaluating you, and they say so.

### Tier 4 — "The Broker" *(Reputation 600+, appears irregularly)*

One vendor, and the most interesting one. **This is the Buccaneer slot.**

**Salvage / close-out loads, bought sight-unseen.** The Broker offers a whole trailer at a flat price
with a manifest that is partial or simply wrong:

> **MIXED GROCERY — approx 18 plt — AS IS — $4,000 flat**

You find out what you bought by **physically breaking it down on the dock.** That single sentence is
what makes this fun — and it's what finally gives the Receiver character a real job. Right now she
counts things that are always correct.

**Contents roll, per pallet:**

| Outcome | Chance | Effect |
|---|---|---|
| Ordinary sellable stock | 60% | Normal inventory |
| Short-dated | 20% | Genuinely valuable, but only if you can move it fast |
| Damaged | 15% | → `PalletStatus.QAHold` → write-off |
| Jackpot | 5% | A pallet of Tier 3 goods you cannot buy through any other channel |

**The risk is operational, not legal:** the load occupies dock time and rack slots whether or not it
turns out to be worth anything. Buy a salvage trailer on a busy day and you've blocked your own dock.

**This is also the natural vector for the deferred rat system.** A salvage load is exactly how an
infestation gets into a building. Ship a contaminated pallet, take the −40, and the whole chaos layer
suddenly has economic teeth instead of being bolted on.

---

## The Anti-Obsolescence Rule

**This is the load-bearing constraint of the whole design.**

If Tier 3 is strictly better than Tier 1, the game becomes *"grind rep, buy caviar, win,"* and roughly
25 of the 31 existing SKUs become dead content by hour three.

**Tiers differ on a risk/velocity axis, never on a strictly-better axis:**

| | Margin | Demand shape | Holding cost | Cost of a scratch |
|---|---|---|---|---|
| **T1 staples** | thin | constant, high volume | low | low |
| **T2 household** | mid | steady | mid | mid |
| **T3 specialty** | fat | lumpy, rare, high-value | high (slot-hungry, spoils) | severe |

A mature player still runs flour as their base load, because staples are what keep **fill rate**
healthy across the constant orders — and fill rate is the score. This is the Anno / Patrician
structure: tier-one goods never become obsolete because demand for them scales with everything else.

### The repricing pass this requires

Finding #3 above means the existing 31 SKUs must be repriced or the tiers read backwards on day one.

| SKU | Tier | Now | Becomes | New markup |
|---|---|---|---|---|
| Water | T1 | 5 → 12 (140%) | 5 → 7 | 40% |
| Salt | T1 | 10 → 18 (80%) | 10 → 13 | 30% |
| Maple Syrup | T3 | 55 → 85 (55%) | 55 → 110 | 100% |

**The repricing is not cleanup — it's where the tiers actually get their character.**

---

## Supporting mechanics

These are what turn the tier structure into a decision rather than a menu with more pages. Three
ingredients make any procurement loop fun, and the current system has none of them: **commitment
under uncertainty**, **variance in the outcome**, and **a binding constraint that isn't money**.

**1. Prices move.** A daily price per SKU with a visible 7-day sparkline. Buy-low becomes a real
skill. *(The Anno lesson: uncertainty is only fun if it's readable in about two seconds.)*

**2. Spot deals — the draft.** Three offers a day, expiring:
*"6 pallets canned tomato, 40% off, arrives 06:00 tomorrow."* Highest fun-per-line-of-code on this
list. It's not a shop, it's a draft — taking one means losing two. *(The Slay the Spire card-reward
lesson.)*

**3. Receiving variance.** Load the gun that's already built (finding #2). Sometimes 11 pallets
instead of 12, sometimes damaged, occasionally the wrong SKU. Vendor on-time% and fill% become things
the player **learns** — which is what makes vendors characters instead of rows.

**4. "Will it fit?"** One line in the PO footer: *"18 pallets · 12 reserve slots free · 6 to the
floor."* The dock schedule and rack capacity are already binding constraints, but they bite hours
later, invisibly, far from the decision that caused them. A constraint the player can't feel at the
moment of choosing isn't a constraint — it's a surprise.

---

## Build order

**Phase 1 — Make purchasing a bet. ✅ BUILT 2026-08-18.**
Price movement + sparkline, spot-deal draft, receiving variance, fit indicator. **This phase makes
the current single vendor interesting before adding more of them** — done first precisely because it
validates that the mechanics, not the roster size, are what's missing.

What shipped:
- **`MarketService`** (`Core/Inventory/MarketService.cs`, new `IService`) — per-SKU daily price on a
  mean-reverting random walk clamped to 0.70×–1.35× of authored `BuyValue`, 7 days of history, and
  the 3-a-day expiring spot-deal board. Registered in `GameContext` **after**
  `inventoryService.LoadSkuDatabase` — it seeds from the SKU database, and an empty one at that
  moment means a market with no prices for the whole session. Persisted via `SaveData.market`.
- **`ShipmentLineItem.Dropped`** + `ShipmentService.ApplySupplierVariance` — 25% of player POs arrive
  short by 1–3 pallets, rolled once at dispatch (before `TruckController.LoadShipment` reads the
  manifest, so trailer/PO list/receiving all agree), never more than half the load, never a
  single-pallet PO, never non-player freight. The player is **credited** for what didn't arrive.
  `TruckController.LoadShipment` skips dropped lines, so `ReceivedQuantity` stays 0 and `Shortage`
  finally reports something real.
- **Every price on the panel** routes through one `UnitPrice(sku)` helper — card, line cost, order
  total and the PO's actual line items — so the quoted number and the charged number cannot diverge.
- **`WAREHOUSE:` line** under the trailer meter: pallets inbound vs. free reserve slots, read from
  `LocationStatusRegistry` (the registry the reach truck obeys, not `LocationData`).

**Verified live, not inferred:** 31/31 SKUs priced with full history; prices held $13–$16 around a
$14 normal across 20 simulated days with history capped at 7; the deal board held exactly 3 every day
with no record accumulation; claim is one-shot; save round-trip preserved price and history; variance
measured at 27% over 400 POs (target 25%), avg 2.0 pallets, 0 wipeouts, 0 stranded singles, 0
non-player POs touched; and taking a live 40%-off deal moved capital by exactly its cost and produced
a 6-line `Spot Market` PO priced at the deal rate.

**One bug found and fixed during verification:** `EnsureDealsForToday` re-read the clock instead of
using the day carried by `OnDayChanged`. When the two disagreed it expired the board against the new
day and then refused to rebuild it against the old one — the offer board would have emptied at the
first midnight and never returned.

**Phase 2 — `VendorData` + roster. ✅ BUILT 2026-08-18.**

- **`VendorData`** / **`VendorRegistry`** mirroring `ContractData` / `ContractRegistry`, registry
  loaded from `Resources` by name so it resolves in a built player.
- **Seven vendors** on a deliberate ladder rather than three lumps at the band boundaries:
  Bulk Basin Foods (0) · Cornerstone Staples (0) · Halloran Household (60) · Fairweather Trading Co.
  (100) · Meridian Provisions (160) · Vessel & Vine Imports (250) · Ambrose Fine Foods (340).
- **Three axes of difference, and only three, because all three are consumed today:** price
  multiplier against the market, short-shipment chance (which now drives `ApplySupplierVariance`
  instead of the flat 25%), and minimum order in cases. **On-time % and net-30 payment terms were
  drafted and CUT** rather than authored unconsumed — late delivery and trade credit don't exist yet,
  and a field nothing reads is exactly how `Overage`/`Shortage` sat dead for months.
- **Repricing done.** All 31 SKUs, 0 unmapped: Tier 1 now 30–40% markup (Water 5→7, Salt 10→13),
  Tier 2 50–56% (Ketchup 20→30, Mayonnaise 28→43), Tier 3 100–117% (Coffee 45→95, Maple Syrup
  55→110). The catalogue splits 13 / 14 / 4.
- **One PO is one vendor.** Switching supplier clears the basket; carrying lines over and silently
  repricing them would leave the player looking at quantities they chose against numbers that no
  longer applied.
- **Locked vendors are shown, greyed, with the gap to unlock.** A locked door you can see is a goal.

**Phase 3's core came forward into Phase 2**, because gating without a score source is unusable — at
reputation 0 only 13 staple SKUs are buyable and nothing could ever change that. `ReputationService`
ships with the score, the bands, persistence, and **three of the five designed inputs wired** (orders
shipped, fined, cancelled). Driver wait time and contaminated product are NOT wired: neither raises an
event yet, and handlers for events nobody sends are the dead-plumbing pattern again.

**Known gap:** spot deals are deliberately NOT vendor-gated. The spot market is a separate channel,
and letting a Tier 3 item occasionally appear there early is a feature — it's a taste of what you're
working toward. Revisit if it undercuts the ladder in play.

**Phase 3 — remaining reputation work.** The score exists (built with Phase 2). Still to do: wire
driver wait time at the dock (`AwaitingOffload` → `CompleteOffload`) and contaminated product, and
point **customer contract arrival** at the same score — the second half of "one reputation, two
consumers" that makes the shared-score decision pay off.

**Phase 4 — The Broker. ✅ BUILT 2026-08-18.**

`BrokerService` (`Core/Inventory/BrokerService.cs`), `Vendor_Broker` asset gated at **600 reputation**.

- **35% chance per day** of a load, max one on the board, expiring after two days. Rare on purpose —
  an offer that's always there is a shop, not an event.
- **Contents are rolled AT OFFER TIME and persisted with the offer.** That ordering is load-bearing:
  the trailer genuinely contains something specific before the player decides, so reloading a save
  can't reroll a bad load into a good one. A gamble you can save-scum isn't one.
- Measured mix over 3,897 pallets: **59.4% ordinary / 20.1% short-dated / 15.2% damaged / 5.4%
  jackpot** against targets of 60/20/15/5.
- **Short-dated needs no new plumbing** — it stamps a real 4-day `ShelfLifeDays`, which flows through
  the existing receive path into a genuine expiration day and the spoilage system already watching
  for it. That pipeline had been waiting for something to actually use it.
- **Damaged pallets are marked `Dropped`**, so no pallet is built on the trailer and none reaches
  inventory — refused at the door. The line item stays on the manifest carrying its condition, which
  is what lets the PO list report "3 damaged, written off" rather than silently handing back a
  smaller trailer than the one that was sold.
- **The jackpot pool is whatever no unlocked vendor carries**, so a jackpot is by construction
  something the player cannot simply go and buy.
- **The PO list keeps the secret.** A salvage PO shows `SEALED · contents unknown until it's broken
  down on the dock` until something has physically landed; then it reports the verdict. If the
  manifest were readable the moment it was bought, "sight-unseen" would be a flavour word.
- **The Broker is excluded from the supplier bar** (`VendorRegistry.Unlocked`/`Locked` filter him
  out). He has no catalogue — he sells whole trailers — and as a chip he'd read "0 items available".

**Tuning, and a real bug found by measuring it.** The asking price is a flat 40–92% of the FULL
manifest value. It originally excluded damaged pallets from that calculation, on the reasoning that
charging for worthless product was unfair — and measurement showed **300 of 300 loads were
profitable**, because the price could never exceed the usable value. The gamble had no downside at
all; every card was a free win wearing a warning label. Pricing on the full manifest (junk included)
is the entire risk. Final measured behaviour over 800 loads: **88% profitable, 12% losses, +42%
average margin on money risked, best +163%, worst −46%.**

**Known limitation:** damaged product is refused at the dock rather than physically arriving and
needing disposal. The more interesting version — junk that occupies a lane slot until someone hauls
it out — needs a disposal mechanic that doesn't exist yet. `PalletMasterRecord` also has no status
field, so `PalletStatus.QAHold` is still unused; that's where it would go.

**Phase 5+ — Deferred.**
Cold chain (Perishable/Frozen storage, refrigerated rooms, spoilage) and with it the caviar /
seafood / rare-beef headliners. Rats plugging into the contamination penalty.

---

## Related

- `GAMEPLAY_LOOP_DESIGN.md` — the outbound half of the loop
- `CLAUDE.md` → *Session 2026-08-01* → "Contract arrival by REPUTATION" — the customer-side
  reputation design this shares its score with
- `CLAUDE.md` → *Economy & Financial Reporting System* — where purchase costs land
  (`MoneyService.Deduct` → Spent Today → Purchases)
