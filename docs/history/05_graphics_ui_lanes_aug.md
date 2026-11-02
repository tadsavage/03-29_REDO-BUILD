# Graphics presets, floor collider, unscaled UI time, staging/lane locks (Aug 2026)

> Archived from CLAUDE.md on 2026-11-02 (verbatim). Historical detail — read only when working in this area. The lean CLAUDE.md holds the rules that always apply.

## Graphics performance & quality presets (2026-08-21)

Ultra sat at **26.5 FPS** at 1080p on a scene drawing only ~179k triangles / ~345 draw calls, and the
three presets felt nearly identical. Profiled with `Time.unscaledDeltaTime` medians over 120–150 frame
windows per config (medians, not means — each `Unity_RunCommand` compiles an assembly and stalls the
editor, which poisons a mean). Result: **Ultra 26.5 → 60.8 FPS**, Good 125.5, Toaster 115.8.

### The two real costs (everything else was noise)

**1. Backdrop "groundrow" lights — 15ms.** `BackdropDepthLighting`'s three `Gap_*` rigs held **60 point
lights at `range = 200`** (some up to 385), each flagged `shadows = Hard`. The renderer is **Forward+**
(`m_RenderingMode: 2`), which bins lights into screen clusters — a range-200 light on a ~200-unit-wide
backdrop lands in *every* cluster, so every pixel iterated all 60 lights. Disabling additional lights
entirely took Ultra 36.5 → 81.4 FPS.

Measured: **light COUNT is the lever, not range.** Range 120 × 60 lights was still 27.7 FPS; range 90
only got to 44.6. But 20 lights at range 150 hit 64.3. Dropping range alone to 30/60 *does* hit ~95 FPS
but visibly darkens the midground — those lights were doubling as a scene-wide ambient fill, not just
backdrop lighting.

Applied: **kept 24 of 60** (every 3rd child per rig), `range = 150`, **`intensity × 3`** to preserve the
total fill, `shadows = None`. Screenshot-verified against the original. The `shadows = Hard` flag was
always inert — `m_AdditionalLightShadowsSupported` is false in all three URP assets — but it's a trap
if that's ever enabled.

**2. The cavity full-screen pass — ~9.5ms.** `Assets/Plugins/cavifree-LowPolyShader/GetAverageCurvature.hlsl`
runs a `(2r+1)²` nested loop and does **4 normal-buffer samples per iteration**:

| `_Radius` | taps/pixel | @1080p |
|---|---|---|
| 5 (was Ultra + Good) | 484 | ~1.0 billion samples/frame |
| 3 (now Ultra) | 196 | |
| 1 (now Good) | 36 | |
| 0.5 (Toaster) | 4 | |

`_Radius` was **5** — cost is essentially linear in taps. r5→r1 saved 9.6ms; r1→off only saved 1.2ms
more, so radius is the whole story. Note `int r = (int)radius`, so 0.5 → r=0 → one iteration.

### Things that turned out NOT to matter (don't re-investigate)

- **Post-processing: 0.9ms for the entire 19-effect PP_Ultra stack.** DoF, MotionBlur,
  ScreenSpaceLensFlare, ChromaticAberration all measured within noise individually. It's a *look* lever,
  not a perf lever.
- **Shadows: ~0ms.** Distance 100→40 and 3→2 cascades changed nothing measurable.
- **HDR color buffer precision** (64-bit vs 32-bit): 0.2ms.
- **Dynamic batching:** disabling it was ~0.8ms *worse*. Left alone.
- MSAA is real but secondary: 8×→4× = 3.6ms, 4×→off = 4.3ms.

### Why the presets felt the same

`URP_Good` pointed at **`PC_Renderer` — the same renderer as Ultra** — so Good inherited the identical
radius-5 cavity pass, the single most expensive thing in the frame. Good also had `renderScale 1.0`
(no fill-rate saving at all) and a main-light shadowmap of **512 while Toaster's was 1024** — backwards.

Now each tier owns its renderer and cavity material:

| | Ultra | Good | Toaster |
|---|---|---|---|
| URP asset | PC_RPAsset | URP_Good | URP_Toaster |
| Renderer | PC_Renderer | **Good_Renderer** (new) | Toaster_Renderer |
| Cavity material | Cavity_Material06-07 **r=3** | **Cavity_Material_Good r=1** (new) | Cavity_Material_Toaster r=0.5 |
| MSAA | **4×** (was 8×) | 2× | off |
| renderScale | 1.0 | **0.9** (was 1.0) | 0.77 + FSR |
| Main shadowmap | 2048 | **1024** (was 512) | **512** (was 1024) |
| SSAO | high | **downsampled, medium** | off |

`GraphicsPresetManager.SetRendererFeatures()` was **deleted**. It toggled features on the single
serialized `rendererData` (PC_Renderer) for *every* preset — so selecting Toaster disabled SSAO on the
*Ultra* renderer (Toaster doesn't even use PC_Renderer) and left the asset dirty on disk. Each renderer
data now carries its own feature set; nothing mutates a renderer asset at runtime.

### Measurement caveat

Below ~8ms everything is CPU/editor-bound, so Good (125.5) and Toaster (115.8) are indistinguishable
*in the editor* — Toaster measuring marginally slower is noise, not a regression. Their extra savings
only show on GPU-bound hardware, which is exactly the hardware Toaster exists for.

### Known remaining headroom (not done)

The cavity shader's inner loop re-samples each texel ~4× (each point's 4-tap cross overlaps its
neighbours'). Because curvature is computed per-point and then **linearly** weight-averaged, it could be
split into two passes — one computing per-pixel curvature, one blurring it — for roughly 4× on that
effect, or ~18× with a separable blur approximation. A circular kernel (skip `i²+j² > r²`) is a free
~40–48% with near-identical output.

---

## FloorTile.prefab had no collider — nothing could be placed on warehouse floor (2026-08-22)

**Symptom:** could place on yard and shipping-lane tiles but not on regular floor tiles.

**Not the validator.** `PlacementValidator.IsValidPlacement` returns true for every buildable object on a
FloorTile cell — verified by sweeping all ~105 `ObjDataSO`s against a floor cell vs. a bare-yard cell
(0 mismatches). `PlaceCommand.Execute()` also places and undoes correctly on FloorTile, Flr-ShipLane and
bare yard. The whole grid/command layer was fine.

**Root cause:** `FloorTile.prefab` was the only Floor prefab with **zero colliders**. Flr-Yard,
Flr-ShipLane, Flr-Pedest and Flr-MHE all carry `BoxCollider center=(0,0,0) size=(1.325, 0.05, 1.325)`
on the root, layer 3 (Ground). So all 1026 placed floor tiles were invisible to physics, and
`RaycastController.Tick()` got neither an object hit nor a ground hit — `HasHit` stayed false and
`BuildState` early-returns (`if (!_raycast.HasHit) { ClearAll(); Hide(); return; }`) long before any
validation runs.

It only showed up indoors because **`YardFloorMeshBuilder` deliberately skips cells already covered by a
placed surface** — so under the warehouse floor there is no yard-mesh collider either. Outdoors the yard
chunks provided a ground hit regardless; indoors there was nothing at all.

**Fix:** added the matching BoxCollider to `FloorTile.prefab`. The 4 FloorTile children of
`grd_Foundation_Combined` inherit it automatically.

**`isTrigger` must be FALSE here** (matching Flr-Yard, not the other three). `RaycastController`'s ground
pass uses `QueryTriggerInteraction.Ignore`, and that pass is what supplies `HitCell` — the deliberate
parallax-free "anchor" (see its Perspective-Jumping comment). With a trigger collider the object pass
still sets `HasHit`, but `HitCell` falls back to the object hit point, so anything hanging in front of a
tile (a Round Light) resolves the cursor to the wrong cell. Measured over 586 on-screen floor cells:

| | no collider | trigger | non-trigger |
|---|---|---|---|
| HasHit | 24 fail | 586 | 586 |
| correct cell | 0 | 409 | **586** |

NavMesh-neutral: all three `NavMeshSurface`s use `useGeometry = RenderMeshes`, so physics colliders never
feed the bake (re-checked: 40/40 sampled floor cells still on the NavMesh).

**Lesson:** when placement fails, check `RaycastController.HasHit` before suspecting `PlacementValidator` —
a missing collider fails the gesture several steps before any rule is evaluated.

---

## UI/FX must run on unscaled time (2026-08-22)

Game speed is `Time.timeScale`, set by `TopBarUI.SetGameSpeed` (pause = 0). Anything presentational that
ticked on scaled time therefore crawled at 1/4x and **froze permanently at 0x**.

**The toast was the worst of it.** `UIToast.Show` did `_timer = _defaultDuration * Time.timeScale`. At 0x
that is `_timer = 0`, so `Update`'s `if (_timer > 0f)` never ran, opacity was never reset, and the toast
stuck on screen **forever** — it did not even recover on unpause, because `Show()` is what seeds the
timer. At 1/4x it flashed past four times too fast. Now simply `_timer = _defaultDuration`, decremented
with `Time.unscaledDeltaTime` (`Update` already was unscaled — the two halves disagreed).

Switched to `Time.unscaledDeltaTime`: `FloatingMoneyText` (the rising -$X popups), `WorldHoverPopupUI`
(4 hover-delay timers + cursor-follow smoothing — at 0x the tooltip could never appear at all),
`PreviewCostUI` (cursor-follow smoothing), `PickSlotCameraFocus` (right-drag camera orbit).

**Particles:** `FXPool.CreateInstance` now sets `main.useUnscaledTime = true`, and
`DustPoofParent.prefab`'s 3 systems are authored that way too. This is not just cosmetic — `FXPool`'s
`ReturnWhenDone` coroutine waits on `ps.IsAlive()`, so a timeScale-frozen system never completes and the
pooled instance is **never returned to the pool**, draining it over time.

**Already fine, do not "fix":** `MoneyFlightFx` and `UnhappyCustomerFx` animate via UI Toolkit
`experimental.animation` / `schedule`, which run on the panel's real-time clock and ignore `Time.timeScale`.

**Deliberately still scaled:** everything in `Gameplay/` (doors, gates, contamination), `TimeDriver`,
`AudioManager` fades, `NavMeshManager`, `PlacementSystem`'s quicksave timer, `NewItemPanel`'s refresh
interval (nothing changes while paused anyway), and `SplashScreenController` (runs before gameplay).

Measured toast/floating-text lifetimes after the change — flat across the whole speed range:

| timeScale | 0.25x | 0.5x | 1x | 2x | 4x |
|---|---|---|---|---|---|
| toast (1.5s) | 1.50 | 1.50 | 1.51 | 1.50 | 1.51 |
| money text (1.15s) | 1.16 | 1.16 | 1.15 | 1.16 | 1.15 |

Also verified at `timeScale = 0`: toast, floating text and dust particles all complete and clean
themselves up while the simulation is frozen.

**Rule for new UI/FX:** if it is feedback *about* the simulation rather than part of it, it ticks on
`Time.unscaledDeltaTime` (or `useUnscaledTime` for particles).

---

## Two fixes: crushed title bar, and staged pallets driving into each other (2026-08-23)

### 1. Title-bar buttons overhanging the top of the panel

`ContractsPanel`'s title bar is authored `height = 52` but had the **default `flexShrink = 1`**. The modal
is a fixed-height column, and that row was its ONLY shrinkable fixed-height child — every other row is
`flexShrink = 0` or is the content that grows. So on any tab whose content overflows (Recurring Orders
with a long ORDER DETAILS list is the easy repro) flex took the entire shortfall out of the title bar and
crushed it **52 -> 9.75px**. The 48px buttons inside are `flexShrink = 0`, so they did NOT shrink with it —
they overflowed the collapsed row and spilled ~19.5px out through the top of the panel.

Fix: `titleBar.style.flexShrink = 0` (and the same guard added to `PurchasingPanel`, which has the same
shape). Measured after: row back to 51.75px, and all four children (title, Back to Purchasing, min/max,
close) sit at y=19.5-67.5, inside both the row and the panel.

**A fixed `height` on a flex child is a suggestion until you also set `flexShrink = 0`.**

### 2. Reach trucks staging pallets into each other

`ReachTruckOperator.DeliverPalletToStagingLane` resolved its staging slot at the TOP of the routine, then
drove the whole way there before setting the pallet down. Nothing claimed the cell in between.

`InventoryService.OutboundOccupiedCells` only counts pallets that are **dropped and unparented** — a
pallet riding forks is deliberately excluded — so for the entire length of that drive the cell still read
as free. Any second delivery resolving inside that window was handed the *same* slot and drove its pallet
into the first one. Rack destinations never had this: they already reserve the address before pickup and
release it in `AbortRoutine`.

Fix mirrors that contract: `InventoryService._stagingSlotReservations` (+ `TryReserveStagingSlot` /
`ReleaseStagingSlot` / `IsStagingSlotReserved`), gating **both** `TryGetNextFreeSlot` and
`CountFreeStagingSlotsInLane`. The truck reserves immediately after resolving — no `yield` between the two
lines, so it is atomic under cooperative coroutine scheduling — and releases via `Restore()` (success) and
`AbortRoutine()` (failure), the two choke points every exit passes through. A leaked reservation would
block a slot permanently, hence releasing at choke points rather than at each call site.

Reproduced and verified directly against the live service: two consecutive resolves with no reservation
both returned cell (40,59); with it reserved the next resolve returned (40,60), free count 7->6,
double-reserve refused, and release restored (40,59) and the count.

Note this was verified at the allocator level, NOT by watching two trucks physically stage at once.

---

## Outbound staging now stacks, matching inbound (2026-08-23, follow-up)

The prior fix (same date, above) stopped a second delivery from being handed the same cell — but it did
that by treating a staging cell as single-occupancy, one pallet per cell, moving straight to the next
slot. The user corrected this: staging lanes are meant to STACK (LaneConfig.MaxStackHeight defaults to
2), the same as inbound receiving already does into these same lanes. Reworked to match.

**Occupancy and reservations are now TIER counts, not per-cell booleans.**
`InventoryService._stagingSlotReservations` is `Dictionary<Vector2Int,int>`; `OutboundOccupiedCells`
(bool set) became `OutboundPalletsByCell` (`Dictionary<Vector2Int,List<GameObject>>`, new public
`GetOutboundPalletObjectsAt`). A new `OccupiedTiersAt` sums inbound stock + staged outbound pallets +
in-flight reservations for one cell; `TryGetNextFreeSlot` and `CountFreeStagingSlotsInLane` both compare
that sum against the lane's `MaxStackHeight` instead of a 0/1 flag. `CountFreeStagingSlotsInLane` returns
TIERS now (was cells) — a 7-slot lane at MaxStackHeight 2 reports 14, not 7 — matching what
`TryFindStagingLaneForPallets` needs it for (comparing against a whole order's pallet count).

One correction inside `OutboundPalletsByCell`: the nearest-slot match must use PLANAR distance. A pallet
on tier 2 sits roughly a metre above the slot's own world Y, and the old 3D `sqrMagnitude` would push it
outside `OutboundClaimDistance` and make an occupied cell read as empty.

**Drop height is the SAME rule inbound already used**, now shared instead of inbound-only:
`TrailerOffloadController.StagingDropBaseY(door, lane, cell, selfGO)` — measures the actual renderer top
of whatever's already in the cell (inbound stock via `GetPalletsAtLocation`, staged outbound via the new
`GetOutboundPalletObjectsAt`; anything still parented/on forks is skipped) and returns `top + StackGap`,
or `LaneSurfaceY` (1.15) if the cell is empty. Both outbound placement sites now call it and use the
result for Y (XZ still comes from the slot):
- `ReachTruckOperator.DeliverPalletToStagingLane` — resolved once at the lane mouth, reused for the mast
  lift height AND the final set-down so they can't disagree.
- `OrderSelectionTaskDriver.PlacePalletsAtStagingSlot` — the selector's per-pallet loop; the previous
  pallet of the same order is already unparented by the time the next one resolves, so it counts and the
  next stacks on top of it.

Verified directly against the live service (not by watching pallets stack in-game):
```
resolve #1 -> slot 1 cell (40,59)
resolve #2 -> slot 1 cell (40,59)   SAME cell = tier 2
resolve #3 -> slot 2 cell (40,60)   moved on once tier 2 filled

StagingDropBaseY EMPTY   = 1.150
probe pallet top         = 2.150
StagingDropBaseY stacked = 2.170   (top + 0.02 gap, exact)
```

---

## Reach trucks now queue for lane entry — fixes the remaining stack-overlap race (2026-08-23, follow-up 2)

The tier-stacking fix (above) made `StagingDropBaseY` measure whatever's PHYSICALLY standing in a cell —
but that's exactly the gap two trucks entering the same lane at once fall through. A pallet still riding
forks is deliberately excluded from that measurement (it isn't standing in the cell yet). So if truck B
reaches the lane mouth while truck A is still driving its pallet in, B's height read doesn't see A's
pallet at all — both can resolve the SAME drop Y and land on top of each other. Tier reservations
(the earlier fix) stop two trucks being handed the same CELL up front, but say nothing about the physical
race once they're both mid-insertion.

Fix, exactly as asked: make lane entry mutually exclusive. `InventoryService._laneEntryLocks`
(`HashSet<(int Door, string Lane)>`) + `TryEnterLaneForDelivery` / `ReleaseLaneEntry`. In
`ReachTruckOperator.DeliverPalletToStagingLane`, right after the truck arrives at the lane mouth (the
open dock floor just outside the slot area) and right before the insertion sequence begins:

```csharp
while (!_inventoryService.TryEnterLaneForDelivery(door, resolvedLane))
    yield return null;
_heldLaneLock = (door, resolvedLane);
```

A truck that loses the race simply sits at the mouth — no extra navigation code needed, since it hasn't
driven past that point yet. Released via the same two choke points as the staging-cell reservation
(`Restore()` on success, `AbortRoutine()` on failure) — audited: the method has exactly one `Restore()`
call and two `AbortRoutine()` calls, both of the latter BEFORE the lock is ever acquired, so every path
out either releases a real lock or no-ops on one that was never taken. No `StopCoroutine`/`OnDisable`
path exists on this component that could skip both choke points and leak the lock.

Verified directly against the service: second entrant into a held lane returns false, a different lane
is unaffected, and release-then-retry succeeds. Not verified by watching two trucks queue in-game.

Deliberately NOT extended to `OrderSelectionTaskDriver` (the walking-selector staging path) — the user's
report was specifically about reach trucks, and unlike the truck's multi-frame coroutine (many yields
across driving/lifting), a selector's `PlacePalletsAtStagingSlot` runs synchronously with no yields once
its nav callback fires, so it can't interleave mid-computation the way two truck coroutines can. The same
`TryEnterLaneForDelivery`/`ReleaseLaneEntry` pair is available if a selector-vs-truck race ever surfaces.

---

## Lane-entry lock extended to Dock Stockers too (2026-08-23, follow-up 3)

User's ask: "one piece of equipment in a lane at a time" for ALL vehicle types — Reach Trucks, Dock
Stockers, Pallet Jacks — not just reach trucks.

**Dock Stockers (`TrailerOffloadController`, inbound trailer → staging lane) now wait on the exact same
lock** `InventoryService.TryEnterLaneForDelivery`/`ReleaseLaneEntry` — no new primitive needed, since it's
already keyed by `(door, lane)` with no notion of caller identity. That means it was ALWAYS going to
serialize DS-vs-DS, truck-vs-truck, AND DS-vs-truck the moment both sides called it; the DS side just
hadn't been wired up yet. Same trigger the reach-truck fix addressed: `ComputeDropBaseY`/
`StagingDropBaseY` both measure whatever is PHYSICALLY standing in a cell, and a pallet on forks is
invisible to that measurement — so two deliveries mid-insertion into the same "Both"-usage lane (which a
DS and a reach truck CAN both target, unlike Inbound-only/Shipping-only lanes) could race exactly like two
reach trucks could.

Wired into `OffloadOnePallet` right after the DS arrives at `entryPivot` (the lane mouth — outside the
slot area, on the dock) and right before it turns to face down the lane. Everything from there through
the pallet landing and the DS driving itself back out to `entryPivot` (steps 9 through 13) is now wrapped
in `try { ... } finally { inv.ReleaseLaneEntry(...); }`.

**Correction made to my own first draft of this comment, worth keeping in mind for any future coroutine
lock/reservation work:** I initially claimed the `finally` block also protects against `StopCoroutine` and
this component's GameObject being destroyed mid-coroutine. Verified empirically that this is FALSE — Unity
does not run an `IEnumerator`'s `finally` for either of those; it silently abandons the enumerator. What
`finally` DOES genuinely guarantee (also verified) is running when an exception propagates during ACTIVE
execution — e.g. an unguarded `ds.` or `forks.` dereference after the equipment was destroyed mid-run,
which is exactly the failure mode several unguarded reads in this method are exposed to. Confirmed with a
synthetic coroutine: `StopCoroutine`/`DestroyImmediate` → `finally` does NOT run; a thrown exception mid-
try → `finally` DOES run and the lock came back available. Neither `TrailerOffloadController` nor
`ReachTruckOperator` currently calls `StopCoroutine` on these routines anywhere, so this gap is
theoretical for now — but don't write "protects against the coroutine being stopped" into a comment again
without checking; it isn't true in Unity.

**Pallet Jacks have no implementation to wire up.** Grepped the codebase: `PalletJack` exists only as a
Finance/GL category (`FinanceCategory.PalletJackGL`) and a placeholder animation-controller name comment
in `MHEOperatorSlot` — there is no `EmployeeRole.PalletJackOperator`, no driving actor, no lane-entry code
path at all. Nothing to change today. When a Pallet Jack controller is eventually built, it should call
the same `InventoryService.TryEnterLaneForDelivery`/`ReleaseLaneEntry` pair at its own "arrived at the lane
mouth" point — no changes to InventoryService needed, the primitive is already caller-agnostic.

---

## Chevrons became unclickable — fallout from the floor-tile collider fix (2026-08-23, follow-up)

**Root cause:** the FloorTile.prefab collider fix earlier in this session (added a real, non-trigger
BoxCollider to `FloorTile.prefab` so placement raycasts work on regular floor) had a side effect nobody
anticipated: `ChevronController.IsPointerOverChevron()` does an UNMASKED `Physics.Raycast` and requires the
single CLOSEST hit to be the chevron's own GameObject. Floor tiles never had a collider before, so this
never had competition. Now every chevron sitting on a `grd_Foundation_Combined`'s FloorTile child (i.e.
every rack-row chevron) has a paper-thin (~5cm) Ground-layer collider directly beneath it, nearly
coincident in height with the chevron's own oversized click volume (0.36m tall, a deliberate fudge factor
for easy clicking) — so the floor tile started winning the "closest hit" contest and the chevron's own
click handler never fired.

**Fix:** `ChevronController`'s raycast now excludes the `Ground` layer (index 3), mirroring the
object-mask/ground-mask split `RaycastController` already uses elsewhere in this codebase — ground is
meant to be a fallback surface, never something that should outrank an interactive object standing on it.
Cached `LayerMask.NameToLayer("Ground")` once in a static field (it never changes at runtime).

Verified by placing a live rack, generating real chevrons, and raycasting at each one's exact screen
position: before the fix 4/4 were vulnerable (2 were live-blocked by their floor tile in this repro), after
the fix all floor-tile blocks resolved to the chevron correctly.

**Two of four chevrons in the same test still didn't resolve — verified this is NOT a bug.**
`Physics.RaycastAll` on that ray showed the rack's own frame (`Full Bay Yellow 80"`, Default layer, and
which has ALWAYS had a collider) blocking at 38m, chevron at 41m — no Ground-layer object anywhere in that
hit list. That's ordinary 3D line-of-sight occlusion from the current camera angle (the "Neg" side
chevrons sit behind the rack structure relative to that viewpoint), unrelated to the floor-tile
regression and unrelated to anything changed this session. Re-verified from a second, closer test-camera
angle and the rack still legitimately blocked it — not a coincidence of the first viewpoint. Making
chevrons clickable through solid rack geometry would be the wrong fix; a player rotates the camera to the
correct aisle, same as any other 3D interactive object.

**Checked but not touched:** three other scripts share the exact same unmasked-raycast-plus-exact-match
pattern (`EmployeeClickHandler`, `MHEOperatorSlot`, `PickSlotOverlayController`) and are theoretically
exposed to the same class of regression if their target ever sits close enough to a floor tile. Live-tested
the one with an instance in the scene (`EmployeeClickHandler` on "Guard") and it resolved correctly — not
broken in practice, so left alone rather than speculatively patched. `MHEOperatorSlot`/
`PickSlotOverlayController` had no live instances to test. If either shows the same "unresponsive click"
symptom, the fix is identical: exclude the Ground layer from that raycast's mask.

---


