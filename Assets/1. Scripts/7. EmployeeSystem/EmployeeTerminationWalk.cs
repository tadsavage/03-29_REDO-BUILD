// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeTerminationWalk.cs
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// "Fired employee storms off" sequence:
///   tint red → march straight to GuardAnchors/ExitPost → face GS_Main and wave for
///   ~3s with an angry emote overhead → march straight to the yard exit → despawn.
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

        // March to ExitPost.
        yield return MarchTo(stop);

        // Stop, face GS_Main, wave with the angry emote overhead.
        SetAnim(walking: false, waving: true);
        Sprite angry = emote != null ? emote : EmoteLibrary.Get("emote_faceAngry");
        if (_bubble != null && angry != null) _bubble.SetPriority(angry);

        float t = 0f;
        while (t < waveSeconds)
        {
            if (face.HasValue) FaceFlat(face.Value);
            t += Time.deltaTime;
            yield return null;
        }

        if (_bubble != null) _bubble.ClearPriority();
        SetAnim(walking: false, waving: false);

        // March to the exit and leave for good.
        yield return MarchTo(exit);
        Destroy(gameObject);
    }

    private float GetGroundHeight(Vector3 position)
    {
        // Cast a ray from 2m above the current position downward.
        Vector3 origin = new Vector3(position.x, position.y + 2f, position.z);
        float groundY = position.y;

        // Perform a RaycastAll to find the highest non-trigger collider that does not belong to this character
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 10f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        float highestY = -9999f;
        bool found = false;

        foreach (var hit in hits)
        {
            // Ignore ourselves (any collider on this GameObject or its children)
            if (hit.transform.root == transform.root)
                continue;

            if (hit.point.y > highestY)
            {
                highestY = hit.point.y;
                found = true;
            }
        }

        if (found)
        {
            groundY = highestY;
        }
        else
        {
            // Fallback: try NavMesh.SamplePosition
            if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 3.0f, NavMesh.AllAreas))
            {
                groundY = navHit.position.y;
            }
        }

        return groundY;
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
