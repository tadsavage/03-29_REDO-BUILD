// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeTerminationWalk.cs
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// "Fired employee storms off" sequence:
///   tint red → wave ~3s with an angry emote overhead (facing the security guard if one is
///   actually on duty AND reachable without a climb/jump — otherwise right where termination
///   caught them) → walk to the dock edge and jump down if elevated → march straight to the
///   yard exit → despawn.
///
/// The wave happens BEFORE any walking on purpose: this script can only play a jump-DOWN
/// animation (JumpDownFromDockIfElevated), not a climb-up one, so the old order — jump down
/// first, then march to the guard's post, then wave — would silently glide the agent back UP
/// onto a dock with no climb animation whenever the guard's post happened to be elevated
/// (e.g. a guard shack built on its own foundation), then jump down a SECOND time afterward
/// with no animation either. Waving first and only ever moving downward afterward avoids any
/// climb entirely.
///
/// Movement is a deliberate DIRECT march (transform driven here), not pathfinding —
/// it overrides the agent's normal route. We disable the agent's nav/anim controllers
/// (AiNavigation moves the transform, AgentAnimation drives the animator + the stuck
/// "!!" wave) so nothing fights us and the stuck-indicator never fires. The angry face
/// uses the existing EmoteBubble so it's placed/billboarded correctly.
///
/// Added at runtime by EmployeeRosterUI on termination. Self-destroys at the exit.
/// </summary>
public class EmployeeTerminationWalk : MonoBehaviour
{
    [SerializeField] private Color angryTint  = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private float marchSpeed = 3.5f;
    [SerializeField] private float turnSpeed  = 360f;
    [SerializeField] private float arriveDist = 0.5f;
    [SerializeField] private float legTimeout = 30f;   // safety: never stall forever

    private Animator    _animator;
    private EmoteBubble _bubble;
    private Rigidbody   _rb;

    /// <summary>
    /// stop = ExitPost, face = GS_Main (optional), exit = yard exit, emote = angry face.
    /// </summary>
    public void Begin(Vector3 stop, Vector3? face, Vector3 exit, Sprite emote, float waveSeconds)
    {
        StartCoroutine(Run(stop, face, exit, emote, waveSeconds));
    }

    private IEnumerator Run(Vector3 stop, Vector3? face, Vector3 exit, Sprite emote, float waveSeconds)
    {
        _animator = GetComponentInChildren<Animator>();
        _bubble   = GetComponent<EmoteBubble>();
        _rb       = GetComponent<Rigidbody>();

        TintRed();
        TakeOver();

        // Only walk over to confront the guard if one is actually on duty AND their post is on
        // the same elevation we're already standing on — this script has no climb-UP animation,
        // so walking to a higher/lower "stop" here would silently glide instead of climbing.
        // Otherwise (no guard, or the guard's post requires a climb we can't animate) they just
        // vent right where termination caught them, per the design: nothing to confront, no
        // reason to go anywhere before leaving.
        bool hasReachableGuard = HasActiveSecurityGuard() && Mathf.Abs(stop.y - transform.position.y) < 0.5f;
        if (hasReachableGuard)
            yield return MarchAlongPath(stop);

        // Stop, wave with the angry emote overhead — facing the guard shack only if we actually
        // walked over to it above.
        SetAnim(walking: false, waving: true);
        Sprite angry = emote != null ? emote : EmoteLibrary.Get("emote_faceAngry");
        if (_bubble != null && angry != null) _bubble.SetPriority(angry);

        float t = 0f;
        while (t < waveSeconds)
        {
            if (hasReachableGuard && face.HasValue) FaceFlat(face.Value);
            t += Time.deltaTime;
            yield return null;
        }

        if (_bubble != null) _bubble.ClearPriority();
        SetAnim(walking: false, waving: false);

        // NOW leave: walk to the dock edge and jump down if elevated (the SAME climb/jump arc
        // every other agent uses), then march to the yard exit and despawn. Every remaining
        // step in this sequence only ever moves DOWNWARD, so JumpDownFromDockIfElevated (jump-
        // down only, no climb-up) is always sufficient from here on.
        yield return JumpDownFromDockIfElevated();
        yield return MarchAlongPath(exit);
        Destroy(gameObject);
    }

    /// <summary>True if a security guard NPC is currently spawned and active — i.e. there's
    /// actually someone to confront with the angry wave. The guard is a TruckYardManager-owned
    /// child (SecurityGuard_Lightweight.prefab, GuardController), not tracked by EmployeeRegistry.</summary>
    private static bool HasActiveSecurityGuard()
    {
        var guard = FindAnyObjectByType<GuardController>();
        return guard != null && guard.gameObject.activeInHierarchy;
    }

    // NavMesh-aware height lookup — searches upward first so an elevated surface (dock/
    // foundation) is preferred over the ground plane directly underneath it. This is the
    // EXACT same offset list/radius as AiNavigation.SnapToNavMeshSurface, which is what keeps
    // every regular (non-terminated) agent correctly standing on top of docks/foundations
    // instead of clipping into the floor below them.
    //
    // The previous implementation used Physics.RaycastAll against world colliders, which finds
    // the visible MESH surface, not the NavMesh surface regular agents actually walk on — the
    // two don't always agree (e.g. the dock's mesh top vs. its baked NavMesh height), which is
    // what caused the drop-to-0-then-pop-back-up-to-1.15 bug while crossing a dock during the
    // storm-off walk: this NavMesh query is the same source of truth as the rest of navigation,
    // so a terminated employee now stays on the correct surface the whole walk, same as anyone else.
    private float GetGroundHeight(Vector3 position)
    {
        float[] yOffsets = { 1.0f, 0.5f, 0f, -0.5f, -1.0f };
        foreach (float offset in yOffsets)
        {
            Vector3 sample = new Vector3(position.x, position.y + offset, position.z);
            if (NavMesh.SamplePosition(sample, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
                return hit.position.y;
        }
        return position.y;
    }

    // Routes a long march through actual connected NavMesh corners instead of a straight line.
    // MarchTo's straight-line + GetGroundHeight follow is only safe across a SINGLE connected
    // surface — if the direct line between two ground points happens to pass under/through a
    // separate elevated island (e.g. a different dock built along the way to the yard exit),
    // GetGroundHeight faithfully follows that island's height too, since it has no concept of
    // path connectivity — producing the exact same no-animation glide-up/glide-down bug as the
    // original dock-then-guard-then-dock issue, just somewhere else on the map. Ground and dock
    // NavMesh are confirmed-separate, unbridged islands (see project notes on dock-climb links),
    // so a calculated path between two ground points can never include dock polygons — corners
    // are guaranteed to stay on the connected ground mesh the whole way.
    private IEnumerator MarchAlongPath(Vector3 dest)
    {
        var path = new NavMeshPath();
        if (NavMesh.CalculatePath(transform.position, dest, HumanAgentFilter(), path)
            && path.status != NavMeshPathStatus.PathInvalid && path.corners.Length > 1)
        {
            for (int i = 1; i < path.corners.Length; i++)
                yield return MarchTo(path.corners[i]);
        }
        else
        {
            yield return MarchTo(dest);
        }
    }

    // Matches AiNavigation.SetupAgentType's lookup — without an explicit agent type, NavMesh.
    // CalculatePath logs "could not determine precisely which agent type" and silently falls
    // back to an arbitrary/default mesh, which can return corners that route through a
    // different elevated NavMesh layer (e.g. MHE) instead of the ground layer a worker
    // actually walks on. Cached once; the settings index doesn't change at runtime.
    private static int s_humanAgentTypeId = int.MinValue;
    private static NavMeshQueryFilter HumanAgentFilter()
    {
        if (s_humanAgentTypeId == int.MinValue)
        {
            s_humanAgentTypeId = 0;
            int count = NavMesh.GetSettingsCount();
            for (int i = 0; i < count; i++)
            {
                var settings = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(settings.agentTypeID) == "Human")
                {
                    s_humanAgentTypeId = settings.agentTypeID;
                    break;
                }
            }
        }
        return new NavMeshQueryFilter { agentTypeID = s_humanAgentTypeId, areaMask = NavMesh.AllAreas };
    }

    private IEnumerator MarchTo(Vector3 dest)
    {
        float t = 0f;
        while (t < legTimeout)
        {
            Vector3 currentPos = transform.position;
            
            // Calculate distance in 2D (XZ plane) to check arrival
            Vector3 currentXZ = new Vector3(currentPos.x, 0f, currentPos.z);
            Vector3 destXZ = new Vector3(dest.x, 0f, dest.z);
            float distXZ = Vector3.Distance(currentXZ, destXZ);
            
            if (distXZ <= arriveDist)
                break;

            FaceFlat(dest);

            // Move XZ position towards destination XZ
            Vector3 nextXZ = Vector3.MoveTowards(currentXZ, destXZ, marchSpeed * Time.deltaTime);

            // Determine the new Y coordinate based on the physical ground at nextXZ
            Vector3 nextPos = new Vector3(nextXZ.x, currentPos.y, nextXZ.z);
            float targetY = GetGroundHeight(nextPos);

            // Smoothly interpolate the Y position so they don't pop instantly
            nextPos.y = Mathf.MoveTowards(currentPos.y, targetY, marchSpeed * 2f * Time.deltaTime);

            transform.position = nextPos;
            if (_rb != null)
                _rb.MovePosition(nextPos);

            SetAnim(walking: true, waving: false);
            
            t += Time.deltaTime;
            yield return null;
        }
        SetAnim(walking: false, waving: false);
    }

    // Walks across to the nearest dock ledge (if currently elevated) and jumps down using the
    // exact same arc/easing/duration AiNavigation.TraverseLink uses for every other agent
    // descending a ledge. No bespoke smooth-glide here — always walk, always jump.
    private IEnumerator JumpDownFromDockIfElevated()
    {
        if (transform.position.y < 0.5f) yield break;

        LedgeLinkMarker nearest = FindNearestLedgeLink();
        if (nearest == null) yield break;

        // Walk across the dock surface to the ledge first.
        yield return MarchTo(nearest.transform.position);

        Vector3 from = transform.position;
        Vector3 fwd = nearest.transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) { fwd = from - nearest.transform.position; fwd.y = 0f; }
        fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;

        // Same ground-point probing CheckDockLedge uses: step outward from the dock edge and
        // sample the GROUND NavMesh so the jump lands exactly where they'll stand.
        Vector3 dockPt  = nearest.transform.position;
        Vector3 floorPt = new Vector3(dockPt.x + fwd.x * 0.5f, 0f, dockPt.z + fwd.z * 0.5f);
        for (float d = 0.5f; d <= 3.0f; d += 0.25f)
        {
            Vector3 probe = new Vector3(dockPt.x + fwd.x * d, 0f, dockPt.z + fwd.z * d);
            if (NavMesh.SamplePosition(probe, out NavMeshHit groundHit, 0.6f, NavMesh.AllAreas)
                && groundHit.position.y < 0.5f)
            {
                floorPt = groundHit.position;
                break;
            }
        }

        Vector3 hDir = floorPt - from; hDir.y = 0f;
        if (hDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(hDir.normalized);

        SetBoolSafe("IsJumpingDown", true);

        float duration = nearest.jumpDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = new Vector3(
                Mathf.Lerp(from.x, floorPt.x, t),
                Mathf.Lerp(from.y, floorPt.y, Mathf.Pow(t, 1.6f)),
                Mathf.Lerp(from.z, floorPt.z, t));
            transform.position = pos;
            if (_rb != null) _rb.MovePosition(pos);
            yield return null;
        }
        transform.position = floorPt;
        if (_rb != null) _rb.MovePosition(floorPt);

        SetBoolSafe("IsJumpingDown", false);
    }

    private LedgeLinkMarker FindNearestLedgeLink()
    {
        LedgeLinkMarker nearest = null;
        float nearestDist = 6f;
        foreach (var m in FindObjectsByType<LedgeLinkMarker>())
        {
            if (m == null || !m.gameObject.name.StartsWith("LedgeLink_")) continue;
            float d = Vector3.Distance(transform.position, m.transform.position);
            if (d < nearestDist) { nearestDist = d; nearest = m; }
        }
        return nearest;
    }

    private void FaceFlat(Vector3 target)
    {
        Vector3 dir = target - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(dir.normalized), turnSpeed * Time.deltaTime);
    }

    // Disable the normal controllers so our direct march wins and the stuck-indicator
    // never fires. The NavMeshAgent stays enabled (other components reference it) but is
    // stopped — with updatePosition=false it can't move the transform anyway.
    private void TakeOver()
    {
        DisableByName("AiNavigation");          // moved the transform from agent.nextPosition
        DisableByName("AgentAnimation");        // drove the animator + the stuck wave
        DisableByName("NoWaypointIndicator");   // the "!!" alert

        var agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (_bubble != null) _bubble.ClearPriority();   // drop the "!!" if it was already up
    }

    private void DisableByName(string typeName)
    {
        var c = GetComponent(typeName) as MonoBehaviour;
        if (c != null) c.enabled = false;
    }

    private void TintRed()
    {
        foreach (var rend in GetComponentsInChildren<Renderer>(true))
        {
            if (rend is SpriteRenderer) continue;   // skip the emote bubble / floating sprites
            foreach (var mat in rend.materials)     // instances — fine, this object is despawning
            {
                if (mat == null) continue;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", angryTint);
                if (mat.HasProperty("_Color"))     mat.SetColor("_Color", angryTint);
            }
        }
    }

    private void SetAnim(bool walking, bool waving)
    {
        SetBoolSafe("IsWalking",      walking);
        SetBoolSafe("IsWaving",       waving);
        SetBoolSafe("IsTurningLeft",  false);
        SetBoolSafe("IsTurningRight", false);
        SetBoolSafe("IsClimbing",     false);
        SetBoolSafe("IsJumpingDown",  false);
    }

    private void SetBoolSafe(string paramName, bool value)
    {
        if (_animator == null) return;
        foreach (var p in _animator.parameters)
            if (p.name == paramName) { _animator.SetBool(paramName, value); return; }
    }
}
