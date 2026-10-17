---
description: The agreed 7-step development roadmap and scope guard for the warehouse sim. Use at the START of any planning discussion, when Tad proposes a new feature or idea, when deciding what to work on next, when scope is creeping, or when he asks "what's next" / "are we on track". Keeps Claude helping Tad stick to the plan (finish the loop, ship an Early Access demo, make money) instead of chasing new ideas.
---

# Roadmap & scope guard (agreed 2026-10-13)

Tad's goal: finish this game, make money from it, and stay realistic about it. Streamer/YouTube clip potential matters for sales. Tad asked Claude to **help him stick to this plan**. Ideas live in `FUTURE_IDEAS.md`; this file is the ORDER. Do not build ideas from that file unasked, and when a new idea comes up, file it there and steer back to the current step.

## The 7 steps
1. **Stabilize the core loop.** Order product, receive it, put it away, customers order, pick/pull, ship, get paid, stay in business. Fix jank, reliability (reach truck/dock stocker livelocks, stuck workers), persistence gaps (staged pallets, in-flight trucks), and finish the case-pick path (see below).
2. **Tutorial / first-10-minutes onboarding.** A new player must understand the loop and the panels in minutes. Without this the game gets refunded.
3. **Player-drivable equipment with metrics and ranking.** Double-click an available forklift/pallet jack, work alongside the AI. Feeds reputation and manager prestige/worker respect.
4. **Sanitation and maintenance crews.** Small, uses the Work Queue, gives workers purpose and gives later physics/rats something to act on.
5. **Limited physics events.** Case drops and one rack-collapse scenario first. See `physics-system` skill.
6. **Demo, Steam Next Fest, Early Access.** Trailer and clips showing trucks docking, forklifts working, orders shipping. Build wishlists before launch.
7. **Cold chain, then the competitor/industrial-park layer, then everything else** as post-launch content.

**Current step: 1 (stabilize the core loop), specifically the case-pick path.** Tad's target (2026-10-13): keep play-testing and CLOSE the core loop THIS WEEK, then start the TUTORIAL (step 2) hopefully next week. See the 2026-10-13 night handoff addendum at the bottom of the repo CLAUDE.md for exactly what is built and what is mid-test.

## Core loop definition (Tad, 2026-10-13)
Customer asks for a product -> player has it or orders it from a vendor -> schedule so product arrives before the customer -> receive -> putaway -> order released -> pick -> stage -> load -> close-out and get paid -> margin (~10-20%) must cover wages, maintenance and utilities -> repeat. Early game: buy only what customers need; later, buy ahead (e.g. 20 pallets of mayonnaise on hand). Random events are NOT part of the core loop. The only score for now is overall reputation. Upgrades, employee levelling/training and better equipment come later.

## Step 1 sub-plan: case pick
Status: orders -> release -> tasks is built; Work Queue no longer releases orders (release happens from the Scheduler tile's RELEASE ORDERS button). Open items, in order:
1. Play-test release: confirm it creates the PalletPick and case-pick (OrderSelect) tasks in the Work Queue. IN PROGRESS 2026-10-13 night: four root-cause fixes are in code but untested together (held tasks must be Open; IsAwaitingRelease; selector must not reset a released order; cancel refiles putaway). Then load a trailer and close out an order.
2. Order selector animation/behaviour: get an empty CHEP pallet onto a pallet jack, drive to pick slots, dismount, pick cases, return them to the pallet. BUILT + play-verified 2026-10-16 (`OrderSelectionTaskDriver.PalletJack.cs`): one full order picked and staged by jack. Needs a placed Pallet Jack, otherwise selectors pick by hand.
3. Pallet building: dimension-aware "heavy to light" stacking (sturdy base, mid-density, flimsy top), square pallets, no clipping. Needs case dimensions/weight/crush strength per SKU. PARTLY DONE 2026-10-16: cases now fill down into the lowest spot (height map, 0.05 clip tolerance) so mixed-SKU pallets have no air layers; heavy-to-light ordering not started.
4. Stretch wrap: special material/model, player wraps the pallet.
5. Staging: pallet jack backs into the staging lane (rear toward the entry, reverses into the empty position, lowers). BUILT with item 2 (enters from the open end facing away from the door; door end if the far side is blocked). Stacking onto an existing staged pallet still snaps up (no lift animation).
Slotting (pick-slot assignment, inventory control role) supports step 3 later.

## How to use this skill
- Start by stating the current step and what is unfinished within it.
- If asked about something from a later step, say which step it belongs to and suggest parking it in `FUTURE_IDEAS.md`, unless it is cheap or unblocks the current step.
- When a step finishes, update "Current step" and the sub-plan here.
- Be candid about scope and risk. Tad explicitly wants straight, critical feedback.
