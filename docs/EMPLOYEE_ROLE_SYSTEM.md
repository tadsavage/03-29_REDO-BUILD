# Employee Role & Trait System — Design Document

**Status:** Foundation implemented (enum, record field, UI hooks). Progression, traits, and skill tree are designed-but-not-built.
**Owner:** Employee System (`Assets/1. Scripts/7. EmployeeSystem/`)
**Last updated:** 2026-06-12

---

## 1. Overview

Every employee has a **Role** (what they do) and, eventually, a set of **Traits** (personality/performance modifiers gained through gameplay). Roles define the performance metric, hiring cost, and which AI tasks the employee can be assigned. Traits layer on top, modifying individual and team performance.

This document is the single source of truth for the full framework. Items marked **[BUILT]** exist in code today; **[DESIGN]** is specified but not yet implemented.

---

## 2. Role Hierarchy & Progression

### 2.1 Roles (current enum) — [BUILT]

| Role | Tier | Performance Metric | Status |
|------|------|--------------------|--------|
| OrderSelector | Entry | CPH (Cases per Hour) | Active |
| ReachTruckOperator | Skilled | PPH (Pallets per Hour) | Active |
| Loader | Skilled | PPH | Active |
| Receiver | Skilled | Indirect | Active |
| Supervisor | Lead | Indirect | Active |
| Boss | Management | Indirect | Active |
| Security | Specialist | Indirect | Active |
| InventoryControl | Specialist | Indirect | Active |
| HR | Specialist | Indirect | **Placeholder** |
| Admin | Specialist | Indirect | **Placeholder** |
| Sanitation | Specialist | Indirect | **Placeholder** |

### 2.2 Progression Structure — [DESIGN]

```
                          ┌──────────┐
                          │   Boss   │   (management)
                          └────▲─────┘
                               │
                          ┌────┴─────┐
                          │Supervisor│   (lead)
                          └────▲─────┘
            ┌──────────────────┼──────────────────┐
   ┌────────┴────────┐ ┌───────┴────────┐ ┌────────┴────────┐
   │ReachTruckOperator│ │     Loader     │ │    Receiver     │  (skilled)
   └────────▲────────┘ └───────▲────────┘ └────────▲────────┘
            └──────────────────┼──────────────────┘
                          ┌────┴─────┐
                          │OrderSelector│  (entry — all new hires start here)
                          └──────────┘

   Specialist branch (parallel, hired directly, not promoted into):
   Security · InventoryControl · HR · Admin · Sanitation
```

- **Entry role:** OrderSelector. All `EmployeeGenerator.Generate()` hires start here. **[BUILT]**
- **Promotion path:** OrderSelector → (ReachTruckOperator | Loader | Receiver) → Supervisor → Boss.
- **Specialist roles** are hired directly into and do not participate in the promotion ladder.

### 2.3 Experience-Based Role Unlock — [DESIGN]

- Each employee accrues **role XP** while working their current role (tie to CPH/PPH output and shifts completed).
- Reaching an XP threshold unlocks **eligibility** for the next tier; the player confirms the promotion (cost + morale event).
- Proposed thresholds (tunable): Tier 1→2 at 40h worked + skill ≥ 50; Tier 2→3 (Supervisor) at 160h + skill ≥ 70; Supervisor→Boss is a hand-picked, limited-slot promotion.
- New fields needed on `EmployeeRecord`: `roleXp` (float), `roleHoursWorked` (float), `unlockedRoles` (bitmask or list). **[DESIGN]**

---

## 3. Skill Tree UI — [DESIGN]

**Goal:** A per-employee skill tree panel, opened from the Employee Info panel ("View Skill Tree" button).

**Layout spec:**
- Vertical tier layout mirroring §2.2: entry at bottom, Boss at top.
- Each node = role icon + label + lock/unlock state (locked = greyed + padlock overlay; unlocked = full color; current = highlighted ring).
- Node click → tooltip with: requirements, hiring/promotion cost, performance metric, morale modifier.
- A horizontal "Specialist" rail along the side for the parallel branch.
- Progress bars under the current node showing role XP toward next unlock.
- Built with UI Toolkit (UXML/USS) consistent with existing `EmployeeUI/` panels; reuse `.row-role-icon` icon styling and the Fredoka/Work Sans font convention.
- Draggable panel pattern identical to `EmployeeInfoUI` (header drag, absolute positioning, clamp to root bounds).

**New assets needed:** `EmployeeSkillTree.uxml`, `EmployeeSkillTree.uss`, a `SkillTreeController.cs`, and role node sprites in `Assets/6. Art/Icons/RoleIcons/`.

---

## 4. Role Economics — [DESIGN]

### 4.1 Hiring / Promotion Costs (tunable starting values)

| Role | Hire Cost | Promote Cost | Notes |
|------|-----------|--------------|-------|
| OrderSelector | $ low | — | Default entry hire |
| ReachTruckOperator | $$ | $ | Requires equipment |
| Loader | $$ | $ | |
| Receiver | $$ | $ | |
| Supervisor | $$$ | $$ | Limited slots |
| Boss | hand-picked | $$$ | 1–2 slots total |
| Security | $$ | — | Direct hire |
| InventoryControl | $$ | — | Direct hire |
| HR / Admin / Sanitation | TBD | — | Placeholder roles |

### 4.2 Morale Modifiers — [DESIGN]

- Each role carries a baseline **morale modifier** (e.g., Supervisor +small for status; Sanitation -small; OrderSelector neutral).
- Promotions grant a one-time morale boost; demotions/role mismatch apply a penalty.
- Store as a per-role table (consider a `RoleConfig` ScriptableObject parallel to `RoleIconLibrary`).

---

## 5. Trait System Framework — [DESIGN]

Traits are modifiers attached to an `EmployeeRecord` that affect that employee and (sometimes) their team. A trait has: id, display name, description, individual modifiers, team modifiers, acquisition trigger, and whether it's positive/negative/neutral.

### 5.1 Example Trait Catalog

| Trait | Polarity | Individual Effect | Team Effect | Acquisition Trigger |
|-------|----------|-------------------|-------------|---------------------|
| suckass | neutral | +morale near Supervisor/Boss | — | proximity to management over time |
| gossiper | negative | small +morale self | -morale to nearby coworkers | idle/clustering events |
| lazy | negative | -CPH/-PPH | slight -team throughput | repeated low output |
| sexist | negative | -morale when working w/ certain coworkers | -team morale | random/backstory |
| thief | negative | occasional inventory shrink | -safety audit score | low morale + opportunity |
| Alpha | positive | +skill gain | +team morale | sustained high output as lead |
| overachiever | positive | +CPH/+PPH | raises team standard | consistently exceeding quota |
| insane | wildcard | random performance swings | unpredictable team morale | extreme fatigue/low morale |
| greedy | negative | demands higher wage | — | high skill + low pay gap |
| speedy | positive | +CPH/+PPH | — | consistent fast cycle times |
| slowpoke | negative | -CPH/-PPH | — | consistent slow cycle times |
| sickly | negative | higher injury/absence chance | — | repeated injury/fatigue |
| sexy | neutral | +morale to nearby coworkers | +team morale (distraction trade-off) | backstory/random |
| CompanyMan | positive | +loyalty, resists poaching | +team morale | **sustained high morale milestone** |

### 5.2 Trait Modifiers

- **Individual modifiers:** flat or % adjustments to CPH/PPH, skill-gain rate, fatigue rate, injury chance, wage demand, morale floor/ceiling.
- **Team modifiers:** radius- or shift-based morale/throughput adjustments applied to coworkers (e.g., overachiever raises nearby quota expectation; gossiper lowers nearby morale).
- Modifiers should stack additively within a category, then clamp to sane bounds.

### 5.3 Trait Acquisition Triggers

- **Gameplay events:** injuries (→ sickly), theft opportunities at low morale (→ thief), sustained fast/slow cycle times (→ speedy/slowpoke).
- **Morale milestones:** sustained high morale over N days → CompanyMan; sustained low morale → insane/gossiper risk.
- **Backstory/random:** assigned at generation with low probability for flavor traits (sexy, sexist).
- Implementation: a `TraitEvaluator` service polling employee stats on a daily tick, rolling against trigger conditions.

### 5.4 Data Model — [DESIGN]

- New file `EmployeeTrait.cs` — enum or string-id + a `TraitDefinition` ScriptableObject (`TraitLibrary` parallel to `RoleIconLibrary`).
- `EmployeeRecord` gains `List<string> traitIds` (serializable, clone-safe — note `Clone()` uses `MemberwiseClone`, so the list is shared-by-reference; **must deep-copy the list in Clone() when traits are added**).
- A `TraitModifierResolver` that, given a record, returns aggregated effective modifiers.

---

## 6. Employee List Panel — [BUILT / PARTIAL]

Columns now present in `EmployeeListItem.uxml` + `EmployeeListPanelController`:
- **Name + role icon** — [BUILT]
- **Location** — clickable button; centers `FreeLookCamera` on the employee and selects them. **[BUILT]**
- **Task** — placeholder `"ToBeImplemented"`. **[DESIGN]** Will map to AI FSM states: "Putting Up Pallet", "Bringing Down Pallet", "Staging a Pallet", etc.
- **Performance** — role-specific: OrderSelector → CPH; Loader/ReachTruckOperator → PPH; others → "Indirect". Currently shows `"--"` placeholder values until work-tracking exists. **[BUILT/PARTIAL]**

---

## 7. Implementation Roadmap

See the master task list in MAESTRO.md (§ Employee System Roadmap). High-level order:
1. **[DONE]** Role enum, record field, default assignment, UI hooks (icons, list columns, camera focus).
2. Role art + `RoleIconLibrary` asset population.
3. Work-tracking service → real CPH/PPH; wire Task column to AI FSM state.
4. Role economics: `RoleConfig` SO (costs + morale modifiers), promotion flow.
5. Role XP & unlock mechanics on `EmployeeRecord`.
6. Skill Tree UI (UXML/USS + controller).
7. Trait system: data model, `TraitLibrary`, `TraitEvaluator`, modifier resolver, Clone() deep-copy fix.
8. Trait → performance/morale integration; trait acquisition triggers.

> **Circle-back note:** Steps 3, 5, 7 depend on systems not yet shelled out (work-tracking, AI FSM task reporting, daily tick service). They are intentionally stubbed with placeholders (`"--"`, `"ToBeImplemented"`) so the UI and data model can ship now and be backfilled without rework.
