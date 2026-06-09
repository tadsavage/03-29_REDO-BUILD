# NavAgent Ledge-Traversal Debug — Session Handoff

**Last updated:** mid-session (user stepped away ~20 min). Unity was in Play Mode on a fresh test scene (L-shaped raised dock + flat ground + 1 WorkerFemale).

## THE PROBLEM
NavMesh agents teleport / "flash across the screen" / sink into the ground and misbehave when traversing ledge links (climb up onto dock / jump down to ground) between the ground level and the raised dock/foundation level.

## KEY FILE
`Assets/1. Scripts/6. Utility/AiNavigation.cs` — owns agent nav + the **manual** ledge traversal (`CheckDockLedge` → `TraverseLink` coroutine). Related: `AgentAnimation.cs`, `LedgeLinkMarker.cs`.

## TOOLING NOTES (important)
- `advanced-unity-mcp` (manage_* tools) is **flaky / frequently disconnects**.
- `unity-mcp` `Unity_RunCommand` **works** — compiles & runs C# in the editor and returns logs in the tool result. Use it for live state. **Constraints:** class must be `CommandScript`, `internal`; **`System.Reflection` is BLOCKED**; `NavMeshLink` type not referenceable (different assembly).
- `Unity_GetConsoleLogs` returns **EMPTY** — console capture is broken. **Do not rely on `Debug.Log`.** That's why we log to a file instead.

## RULED OUT (with hard evidence — do NOT re-investigate)
- **Rigidbody**: confirmed `isKinematic=true, useGravity=false, interpolation=None`. Not the cause.
- **Root motion**: Animator `applyRootMotion=false`.
- **Interpolation / gravity / autoTraverseOffMeshLink**: all off/correct.
- **`agent.nextPosition` writes**: proven they do NOT move the transform when `updatePosition=false`. During every climb, logged `lerpPos == tPos` every frame — the transform follows a clean arc.

## CONFIRMED ROOT FACTS (live data)
1. **Ground and dock are SEPARATE NavMesh islands** — NavMeshLinks do NOT bridge them. Paths between levels come back `PathPartial` (dock unreachable from ground). This is WHY the manual `CheckDockLedge` climb fallback exists.
2. **Two marker types** (`LedgeLinkMarker`): `Foundation` at y≈0 (ground) and `LedgeLink_N/S/E/W` at y≈1.06 (dock top). `CheckDockLedge` selects nearest by **XZ within radius 2.5**.

## FIX ALREADY APPLIED & VERIFIED COMPILED
In `CheckDockLedge`, the jump-down floor target (`floorPt`) used to be `marker + 0.5m` with hardcoded `y=0`, which landed in the navmesh gap → end-of-traversal `agent.Warp()` snapped the agent ~1.1m sideways = the teleport. **New code samples the ground NavMesh outward** (`d=0.5→3.0`, radius 0.6, require `y<0.5`).
- **Verified live:** jump-down `to.y` is now `0.05` (sampled) vs old `0.00`; end teleport gap shrank from ~1.1m → ~0.18m. Fix IS running.

## WHY USER STILL SEES CHAOS ("no change, flashing, sinking")
The fix above was a MINOR contributor. **Dominant problems remain** (seen in fresh-scene per-frame logs):

1. **Big diagonal swoop on climb-up.** Example: `from=(10.83,0.05,-5.99) → to=(9.23,1.06,-4.69)` = ~2 units horizontal slide while climbing 1 unit up, over 1.2s. The agent triggers the climb from wherever it stands (up to 2.5u from the marker) and lerps **diagonally** straight to the marker — looks like swooping across, not a clean vertical climb.
2. **Constant oscillation.** Because the dock is unreachable via navmesh, EVERY arrival re-triggers a traversal. The agent perpetually climbs up → jumps down → climbs up, swooping around dock edges forever. This is most of the "flashing across screen."
3. **Residual ~0.18m end snap** (minor).
4. **"Sinking into ground" — NOT yet located.** Logged END y-values are 0.05/1.06, never negative. Needs investigation (candidates: model pivot/`baseOffset`, behavior during normal walking, or ground-visual height vs navmesh y).

## REAL STRATEGY (decide on resume)
The manual `CheckDockLedge` climb is a band-aid for "NavMeshLinks don't bridge the islands" and is inherently janky. Two paths:
- **(A) Proper fix:** make NavMesh actually connect ground↔dock via working NavMeshLinks/OffMeshLinks so agents path normally and the manual fallback rarely/never fires.
- **(B) Rework manual climb:** (i) first **steer the agent to the marker's XZ edge, THEN climb straight up/down** (kills the diagonal swoop); (ii) prevent infinite re-triggering when no progress is possible; (iii) confirm that once ON the dock, dock waypoints are reachable (same island).
Recommended: B short-term for clean visuals, A for the correct long-term.

## NEXT STEPS (resume here)
1. Locate the "sinking" — RunCommand to dump `agent.baseOffset`, model child pivot Y, and compare navmesh y vs visible ground y. Read more per-frame around any negative/low-Y dip.
2. Pick strategy A vs B (lean B first).
3. Implement: in `CheckDockLedge`/`TraverseLink`, walk to marker XZ before vertical climb; guard against oscillation.

## TEMP DIAGNOSTIC CODE TO REMOVE once done (all in AiNavigation.cs)
- `TravLog(...)` static method + `s_travLogPath` field.
- All `TravLog(...)` calls: CheckDockLedge start, TRAV START, both while-loop samples, TRAV END.
- `int _dbgFrame` + the `if (_dbgFrame...) TravLog(...)` blocks in both loops.
- Delete `Assets/_trav_debug.txt`.

## OTHER CHANGES MADE THIS SESSION
**Keep:**
- `AiNavigation.Awake`: kinematic RB enforcement (`_rb.isKinematic=true; _rb.useGravity=false;`) — defensive, correct.
- `FXPool.cs`: `DisabledKeys` HashSet + early-out in `Play()` (graphics-preset task).
- `GraphicsPresetManager.cs`: Toaster preset disables dust + SSAO + FullScreenPass + low particleRaycastBudget. **Action item:** assign `PC_Renderer.asset` to the new `rendererData` field in the inspector or the SSAO/FullScreenPass toggles no-op.

**Optional cleanup:** the `agent.nextPosition = pos` writes in the traversal loops are harmless but pointless — can be removed.

## KNOWN LATENT BUG (still to patch)
`GoToRandomWaypoint` throws `IndexOutOfRangeException` if the waypoint array shrinks (stale `currentIndex`). Fix: clamp `currentIndex` into range right after `FindWaypoints()`. Not the teleport cause.
