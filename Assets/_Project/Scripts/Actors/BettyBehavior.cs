using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Betty Pages: pure eye candy. She wanders the yard/dock on her own, struts the catwalk, turns, dances, and once in a while
/// drops a cube and sits on it swinging her legs. Nothing drives her; every choice is a dice roll.
/// Animator states are looked up BY NAME (built by Tools > Betty > Build Betty): Idle, CatwalkWalking, CatwalkShow, Samba, HipHop, Sitting, SittingDisbelief.
/// </summary>
[DisallowMultipleComponent]
public class BettyBehavior : MonoBehaviour
{
    [Header("Roaming")]
    [SerializeField, Tooltip("How far from where she was placed she wanders (metres).")] float roamRadius = 14f;
    [SerializeField, Tooltip("Catwalk speed in metres per second.")] float strutSpeed = 1.1f;

    [Header("Odds (relative weights)")]
    [SerializeField] float strutWeight = 30f;
    [SerializeField, Tooltip("The full runway pass: walk out, catwalk turn, walk back, turn.")] float showWeight = 30f;
    [SerializeField] float danceWeight = 25f;
    [SerializeField] float sitWeight = 12f;
    [SerializeField] float poseWeight = 8f;

    void Awake()
    {
        // Same fix the employees have: the "Avatars" layer is redrawn over the SSAO pass by a Render Objects feature (PC_Renderer, Good_Renderer),
        // so the occlusion pass can't turn her into coal dust (Tad 2026-10-11). Her material is the shared avatar one, which already has
        // Receive Shadows off. Only the rendered model moves layers; the root keeps its own layer for click detection and the NavMesh agent.
        int avatars = LayerMask.NameToLayer("Avatars");
        if (avatars < 0) return;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = avatars;
    }

    [Header("Sitting")]
    [SerializeField, Tooltip("Height of the cube she sits on (metres).")] float seatHeight = 1.0f;
    [SerializeField] float seatWidth = 0.6f;
    [SerializeField, Tooltip("Length of the box front-to-back (metres). The extra length runs behind her so she can lean back on it.")] float seatDepth = 1.4f;
    [SerializeField, Tooltip("Raise/lower her root relative to the cube top so her hips land on it. Tune live.")] float sitRootYOffset = -0.3f;
    [SerializeField, Tooltip("How far the cube is placed in front of her (metres).")] float seatDistance = 0.55f;

    [Header("Wiring")]
    [SerializeField, Tooltip("Her animation controller (assigned by Tools > Betty > Build Betty).")] RuntimeAnimatorController controller;

    Animator _anim;
    NavMeshAgent _agent;
    Vector3 _home;
    GameObject _seat;

    // Sample/raycast on HER agent type's mesh (the project default type 0 is the Rat mesh; workers use "Human").
    NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = _agent != null ? _agent.agentTypeID : 0, areaMask = NavMesh.AllAreas };

    void Start()
    {
        _home = transform.position;
        _anim = GetComponentInChildren<Animator>();
        if (_anim != null && controller != null && _anim.runtimeAnimatorController != controller) _anim.runtimeAnimatorController = controller;
        if (_anim != null) { _anim.applyRootMotion = false; _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        _agent = GetComponent<NavMeshAgent>();
        StartCoroutine(Brain());
    }

    void OnDisable() { if (_seat != null) Destroy(_seat); }

    void Play(string state, float fade = 0.3f)
    {
        if (_anim == null || !_anim.isActiveAndEnabled) return;
        _anim.CrossFadeInFixedTime(state, fade);
    }

    IEnumerator Brain()
    {
        yield return null;
        TrySnapToNavMesh();
        while (true)
        {
            Play("Idle");
            yield return new WaitForSeconds(Random.Range(1.5f, 4f));

            float total = strutWeight + showWeight + danceWeight + sitWeight + poseWeight;
            float r = Random.value * total;
            if ((r -= strutWeight) < 0f) yield return Strut();
            else if ((r -= showWeight) < 0f) yield return RunwayShow();
            else if ((r -= danceWeight) < 0f) yield return Dance();
            else if ((r -= sitWeight) < 0f) yield return SitOnCube();
            else yield return Pose();
        }
    }

    void TrySnapToNavMesh()
    {
        if (_agent == null) return;
        if (!_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 6f, Filter))
        {
            _agent.enabled = false; transform.position = hit.position; _agent.enabled = true;
        }
    }

    bool TryPickPoint(out Vector3 p)
    {
        for (int i = 0; i < 12; i++)
        {
            Vector2 c = Random.insideUnitCircle * roamRadius;
            var candidate = _home + new Vector3(c.x, 0f, c.y);
            if (NavMesh.SamplePosition(candidate, out var hit, 3f, Filter) && (hit.position - transform.position).sqrMagnitude > 9f)
            {
                var path = new NavMeshPath();
                if (_agent != null && _agent.isOnNavMesh && _agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                { p = hit.position; return true; }
            }
        }
        p = default; return false;
    }

    // Walk to one to three spots with the catwalk loop, and do the catwalk turn at each one.
    IEnumerator Strut()
    {
        if (_agent == null || !_agent.isOnNavMesh) yield break;
        int legs = Random.Range(1, 4);
        for (int i = 0; i < legs; i++)
        {
            if (!TryPickPoint(out var dest)) yield break;
            _agent.speed = strutSpeed; _agent.angularSpeed = 140f; _agent.stoppingDistance = 0.1f; _agent.isStopped = false;
            _agent.SetDestination(dest);
            Play("CatwalkWalking");
            float timeout = Time.time + 60f;
            while (Time.time < timeout && (_agent.pathPending || _agent.remainingDistance > 0.25f)) yield return null;

            _agent.isStopped = true;
        }
        _agent.isStopped = true;
    }


    // The authored "Catwalk Walk" clip is a whole runway pass: ~5 m out, a 180 degree catwalk turn, ~5 m back, turn again.
    // Its root motion drives her; the agent is parked meanwhile and she is re-seated on the NavMesh afterwards.
    IEnumerator RunwayShow()
    {
        if (_agent == null || !_agent.isOnNavMesh || _anim == null) yield break;
        _agent.isStopped = true;
        // Face somewhere with room for the 5.5 m run-out.
        bool found = false;
        for (int i = 0; i < 8 && !found; i++)
        {
            float yaw = Random.Range(0f, 360f);
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            if (!NavMesh.Raycast(transform.position, transform.position + dir * 6f, out _, Filter)) { transform.rotation = Quaternion.Euler(0f, yaw, 0f); found = true; }
        }
        if (!found) yield break;

        _agent.enabled = false;
        _anim.applyRootMotion = true;
        Play("CatwalkShow", 0.2f);
        yield return new WaitForSeconds(0.25f);
        float len = 15f;
        foreach (var c in _anim.runtimeAnimatorController.animationClips) if (c.name == "Catwalk Walk") len = c.length;
        yield return new WaitForSeconds(Mathf.Max(1f, len - 0.4f));

        _anim.applyRootMotion = false;
        // Root motion moved the model child; carry that over to this object so the agent can take over from where she ended.
        var model = _anim.transform;
        if (model != transform)
        {
            Vector3 p = model.position; Quaternion q = Quaternion.Euler(0f, model.eulerAngles.y, 0f);
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity;
            transform.SetPositionAndRotation(p, q);
        }
        _agent.enabled = true;
        TrySnapToNavMesh();
    }

    IEnumerator Dance()
    {
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        Play(Random.value < 0.5f ? "Samba" : "HipHop");
        yield return new WaitForSeconds(Random.Range(8f, 16f));
    }

    IEnumerator Pose()
    {
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        Play("Idle");
        yield return new WaitForSeconds(Random.Range(3f, 6f));
    }

    // Drop a cube in front of her, hop up, sit and swing her legs for a while, then step down and take the cube away.
    IEnumerator SitOnCube()
    {
        if (_agent == null || !_agent.isOnNavMesh) yield break;
        _agent.isStopped = true;
        Vector3 floor = transform.position;
        Vector3 fwd = transform.forward;
        Vector3 seatCentre = floor - fwd * seatDistance;         // behind her: she backs onto it, so she sits facing the way she was looking

        _seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _seat.name = "Betty_Seat";
        var col = _seat.GetComponent<Collider>(); if (col != null) Destroy(col);   // no physics: keeps the build-mode raycasts and the NavMesh clean
        var seatRenderer = _seat.GetComponent<Renderer>();
        if (seatRenderer != null)
        {
            var cardboard = new Color(0.72f, 0.54f, 0.33f);
            var mat = seatRenderer.material;                       // instance, so the shared default cube material stays untouched
            mat.color = cardboard;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cardboard);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
        }
        // The box is longer than it is wide: the front edge stays under her, the extra length runs BEHIND her so she leans back onto it.
        Vector3 boxCentre = seatCentre - fwd * ((seatDepth - seatWidth) * 0.5f);
        _seat.transform.position = boxCentre + Vector3.up * (seatHeight * 0.5f);
        _seat.transform.rotation = transform.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)    // pop up out of the floor
        {
            _seat.transform.localScale = new Vector3(seatWidth, Mathf.Max(0.01f, seatHeight * t), seatDepth);
            _seat.transform.position = boxCentre + Vector3.up * (seatHeight * t * 0.5f);
            yield return null;
        }

        _agent.enabled = false;                                   // she is lifted by hand from here; the agent would pull her back to the floor
        Play("Sitting", 0.4f);
        Vector3 seated = seatCentre + Vector3.up * (seatHeight + sitRootYOffset);
        Vector3 start = transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            transform.position = Vector3.Lerp(start, seated, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.position = seated;

        float sitFor = Random.Range(10f, 20f);
        float end = Time.time + sitFor;
        // Mostly the leg-swinging sit; now and then she switches to the disbelief sit for the back half.
        float switchAt = Random.value < 0.35f ? Time.time + sitFor * Random.Range(0.4f, 0.7f) : float.MaxValue;
        bool disbelief = false;
        while (Time.time < end)
        {
            if (!disbelief && Time.time >= switchAt) { disbelief = true; Play("SittingDisbelief"); }
            yield return null;
        }

        Play("Idle", 0.3f);
        Vector3 down = new Vector3(floor.x, floor.y, floor.z);
        start = transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            transform.position = Vector3.Lerp(start, down, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.position = down;
        _agent.enabled = true;
        TrySnapToNavMesh();

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
        {
            if (_seat == null) break;
            _seat.transform.localScale = new Vector3(seatWidth, Mathf.Max(0.01f, seatHeight * (1f - t)), seatDepth);
            _seat.transform.position = boxCentre + Vector3.up * (seatHeight * (1f - t) * 0.5f);
            yield return null;
        }
        if (_seat != null) Destroy(_seat);
        _seat = null;
    }
}
