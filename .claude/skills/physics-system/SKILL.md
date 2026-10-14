---
description: DESIGN INTENT (not built yet, 2026-10-13) for the warehouse physics system — dormant rigidbody cases that wake on impact, pallet/rack promotion and demotion, rack collapse, damage and injury consequences, and how it must integrate with the grid, inventory, finance, NavMesh and save systems. Use whenever the task mentions physics, rigidbody, colliders on cases/pallets/racks, collisions with forklifts, falling racks, spilled cases, debris, damage, injuries, or the player driving equipment into things. Read FUTURE_IDEAS.md section 2 too.
---

# Physics system (planned)

Nothing here is built. This skill exists so the first implementation follows the agreed design. For generic PhysX symptom debugging (triggers not firing, tunneling, etc.) also use `unity:physics-3d-collision`. Companions: `inventory-system`, `finance-system`, `racking-system`.

## Agreed design (Tad)
- Cases are real rigidbodies with box colliders, **dormant** until touched: hit by a forklift, placed, moved, or a rack failing.
- Goal: as cheap as possible. Cost with nothing happening should be ~zero.
- Consequences are meaningful: rack falls, cases scatter, avatars get injured, repair cost vs recouping lost product is large, sanitation/maintenance crews clean up.

## Tad's earlier approach (proven in a past project)
Static and Dynamic variants of each pallet as children of the pallet root. One large box collider on the root detects any collision, then disables the static variant and enables the dynamic one (cases as rigidbodies). Falling cases wake pallets below and the rack in a cascade. A whole-rack collapse tanked performance (ECS/DOTS was considered). Treat this as the starting design; add a live-body budget and fast sleeping before reaching for DOTS. Notes only; nothing is to be built until roadmap step 5.

## Architecture rules to follow
1. **Data is the source of truth, physics is a temporary view.** Inventory lives in `PalletMasterRecord` / `PalletData`. Never make physics the record of what exists.
2. **Promote/demote.** Pallets stay non-physical. On an impact event, promote only the affected pallet(s)/cases within a radius to live rigidbodies, cap the live-body budget, let them settle, then demote: either re-seat as a pallet, or convert to a debris/loss record and reduce the record's quantity.
3. **Dormant means no cost:** kinematic or sleeping bodies, no per-frame scripts, collision callbacks only from the moving equipment (forklift forks/body hit volumes), not from every case.
4. **Colliders:** prefer one box collider per case only while promoted; the existing parent-collider approach (floors, foundations) shows what happens when thousands of colliders exist always.
5. **Grid and registries must hear about it.** A collapsed rack goes through the same cleanup path as deletion (placement grid, `PlacedObjectRegistry`, `LocationRegistry`/`SlotRegistry` recompute, location status release, reservations released, NavMesh rebake) plus a damage event, not a silent `Destroy`.
6. **Money:** add finance routing before writing code (new GL lines such as Damage/Repairs and Workers' Comp via `FinanceCategory.ForGLLine`); use `Deduct` for one-time costs.
7. **Save/load:** decide up front if debris persists; the current save does not persist staged pallets or in-flight trucks.
8. **Domain Reload is OFF** in Play Mode: static caches and physics state survive; reset on `SubsystemRegistration`.
9. **Pause:** the game runs at timeScale 0 when paused; physics events and their UI feedback must behave sanely when paused or at 4x.
10. **Prototype first:** one pallet, one rack, one forklift hit, measured cost, before any general system.

## Hooks that already exist
- `PalletBuilder` builds case visuals (strips `PlacedObject`/`BuildingData` from cases; case dimensions via `GetPrefabDimensions`).
- Equipment drive states: `ReachTruckOperator` (`SetDriveMode` is the only toucher of NavMeshAgent), `TrailerOffloadController`, `TrailerLoadController`.
- `RatBehavior`, `ContaminationState` for the later sanitation hooks.
- Injury/safety/morale: `EmployeeStatSystem` (daily tick currently dormant).

## Open questions
- Which events cause damage: speed threshold, fork-tip contact, overload, mast height?
- Is rack collapse partial (one bay) or structural (whole run)?
- Do workers dodge, and can the player be injured?
