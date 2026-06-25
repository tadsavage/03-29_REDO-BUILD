using GameCore.Economy;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Rat AI — scurries, hides, breeds, scavenges, investigates new objects, and (the new
/// mini-game layer) reacts to humans and the exterminator.
///
/// Hiding no longer disables the renderers — instead the rat's own materials are tinted
/// to a SEE-THROUGH GRAY ghost (hiding in shadows). When a worker gets close it snaps
/// back to fully opaque, squeaks, and flees. Only the EXTERMINATOR can actually catch a
/// rat: walking near flushes it out, and a serialized %-chance roll decides whether it is
/// caught (it dies: stop → sink + fade → destroy) or escapes.
///
/// While scavenging, the rat adds <see cref="ContaminationState"/> to the product it
/// gnaws on; left unattended long enough, that product spoils.
///
/// Sound names / clips:
///   clipSqueak — short squeak when startled
///   clipScurry — soft scuttling while moving
///   clipGnaw   — gnawing during scavenge
/// (Only the squeak currently exists in the project — other mechanics surface as Debug.Log.)
/// </summary>
public class RatBehavior : MonoBehaviour
{
    // ── Movement ──────────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float circleDiameter = 3.5f;
    [SerializeField] private int   circlePoints   = 8;
    [SerializeField] private float scurrySpeed    = 5f;
    [SerializeField] private float angularSpeed   = 720f;
    [SerializeField] private float acceleration   = 20f;

    // ── Hiding, Humans & Exterminator ─────────────────────────────────────────
    [Header("Hiding, Humans & Exterminator")]
    [Tooltip("How close a human/exterminator must be (m) before the rat reacts.")]
    [SerializeField] private float   detectionRange   = 4.0f;
    [Tooltip("Name of the exterminator ObjDataSO — only this agent can catch rats.")]
    [SerializeField] private string  exterminatorName = "Exterminator";
    [Tooltip("Chance (0–1) that the exterminator actually catches & kills a flushed rat.")]
    [SerializeField, Range(0f, 1f)] private float catchChance = 0.3f;
    [SerializeField] private float   hidingChance     = 0.6f;
    [Tooltip("Seconds a rat stays put in a hiding spot before peeking out (if coast is clear).")]
    [SerializeField] private float   hideStayDuration = 12f;
    [SerializeField] private float   panicDuration    = 8.0f;
    [SerializeField] private string[] palletNames     = { "A Chep", "StackPlts", "Cases" };
    [SerializeField] private string   palletCategory  = "Inventory";

    [Header("Ghosting (hiding look)")]
    [Tooltip("Semi-transparent gray the rat fades to while hidden in shadow.")]
    [SerializeField] private Color ghostColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

    // ── Age & Growth ──────────────────────────────────────────────────────────
    [Header("Age & Growth")]
    [SerializeField] private float maxAge      = 300f;   // seconds to reach full size
    [SerializeField] public  float RatFromScale = 0.4f;  // scale when newborn
    [SerializeField] public  float RatToScale   = 1.4f;  // scale at max age

    // ── Breeding ──────────────────────────────────────────────────────────────
    [Header("Breeding")]
    [SerializeField] private GameObject ratPrefab;
    [SerializeField] private float breedingRadius   = 2.5f;
    [SerializeField] private float breedingDuration = 12f;
    [SerializeField] private float breedingCooldown = 90f;
    [SerializeField] private int   maxRatPopulation = 15;

    // ── Scavenging ────────────────────────────────────────────────────────────
    [Header("Scavenging")]
    [SerializeField] private float scavengeChance = 0.25f;
    [SerializeField] private float gnawDuration   = 3f;

    // ── Investigation ─────────────────────────────────────────────────────────
    [Header("Investigation")]
    [SerializeField] private float investigationRadius = 6f;

    // ── Sounds ────────────────────────────────────────────────────────────────
    [Header("Spatial Audio")]
    [SerializeField] private float    hearingDistance = 6f;
    [SerializeField] private AudioClip clipSqueak;
    [SerializeField] private AudioClip clipScurry;
    [SerializeField] private AudioClip clipGnaw;

    // ── Death ─────────────────────────────────────────────────────────────────
    [Header("Death")]
    [Tooltip("Seconds for the simple sink + fade death (placeholder for the fancy animation).")]
    [SerializeField] private float deathDuration = 1.5f;
    [SerializeField] private float deathSinkDepth = 1.2f;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private NavMeshAgent agent;
    private Animator     animator;
    private Renderer[]   visuals;

    // Material-tint ghosting: per-renderer original + ghost material sets.
    private Material[][] _originalMats;
    private Material[][] _ghostMats;
    private bool         _ghosted;

    private float _spawnTime;
    private bool  isHiding              = false;
    private bool  isScurryingAway       = false;
    private bool  _dying                = false;
    private float _hideTimer            = 0f;

    // Threat scan cache (throttled)
    private float     _lastThreatScan      = -1f;
    private Transform _cachedHuman          = null;
    private Transform _cachedExterminator   = null;

    // Age
    private float _age;

    // Breeding
    private float _timeNearMate  = 0f;
    private float _lastBreedTime = -999f;
    private static int _globalRatCount = 0;

    // Services
    private SimulationTimeService _timeService;

    // Spatial audio
    private AudioSource _spatialAudio;
    private float       _lastScurrySound = 0f;

    // Investigation
    private int       _lastRegistryCount = 0;
    private Transform _queuedInvestigation;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _spawnTime = Time.time;
        _globalRatCount++;
    }

    private void OnDestroy()
    {
        _globalRatCount = Mathf.Max(0, _globalRatCount - 1);
    }

    private IEnumerator Start()
    {
        agent    = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        CacheMaterials();

        if (GetComponent<AgentTypeTag>() == null)
            gameObject.AddComponent<AgentTypeTag>().agentType = AgentType.Rat;

        // Inherit prefab reference from own PlacedObject if not manually set
        if (ratPrefab == null)
        {
            var po = GetComponent<PlacedObject>();
            if (po != null && po.data != null)
                ratPrefab = po.data.prefab;
        }

        // Grab time service for time-of-day behaviour
        _timeService = FindAnyObjectByType<GameContext>()?.TimeService;

        // 3D spatial audio — steep exponential rolloff so rats are nearly silent
        // until you're close, then rapidly build. hearingDistance controls the radius.
        _spatialAudio = gameObject.AddComponent<AudioSource>();
        _spatialAudio.spatialBlend  = 1f;
        _spatialAudio.playOnAwake   = false;
        _spatialAudio.loop          = false;
        _spatialAudio.minDistance   = 0.3f;
        _spatialAudio.maxDistance   = hearingDistance;
        _spatialAudio.rolloffMode   = AudioRolloffMode.Custom;
        var expCurve = new AnimationCurve(
            new Keyframe(0f,    1f,   0f,  -6f),
            new Keyframe(0.15f, 0.4f, 0f,   0f),
            new Keyframe(0.4f,  0.05f, 0f,  0f),
            new Keyframe(1f,    0f,   0f,   0f)
        );
        _spatialAudio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, expCurve);

        agent.speed        = scurrySpeed;
        agent.acceleration = acceleration;
        agent.updateRotation = false;

        yield return StartCoroutine(WaitUntilOnNavMesh());

        _lastRegistryCount = PlacedObjectRegistry.Count;
        StartCoroutine(BehaviorRoutine());
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_dying) return;

        RefreshThreats();

        // 1. Exterminator: flushes the rat out and may catch it.
        if (!isScurryingAway && _cachedExterminator != null)
        {
            HandleExterminator();
            return;
        }

        // 2. Any human nearby → expose (snap opaque), squeak, flee the opposite way.
        if (!isScurryingAway && _cachedHuman != null)
        {
            if (isHiding)
            {
                Debug.Log("[Rat] A worker strayed near my hiding spot — exposing and bolting!");
                // Employee reaction. No "!" emote asset yet, so log it for now.
                Debug.Log("[Employee] (!) noticed a rat!");
            }

            StopAllCoroutines();
            ExposeNow();
            TryPlaySound(clipSqueak);
            StartCoroutine(FleeFromRoutine(_cachedHuman.position));
            return;
        }

        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            SetAnimWalking(false);
            return;
        }

        UpdateAge();
        UpdateBreeding();
        UpdateInvestigationTrigger();
        UpdateSoundEffects();

        // Animator sync
        float speed   = agent.velocity.magnitude;
        bool  moving  = speed > 0.1f && !agent.isStopped;
        SetAnimWalking(moving);

        // Rotate to face movement direction (model is backwards so flip 180)
        if (moving && agent.velocity.sqrMagnitude > 0.05f)
        {
            Quaternion target = Quaternion.LookRotation(agent.velocity.normalized)
                              * Quaternion.Euler(0, 180, 0);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, angularSpeed * Time.deltaTime);
        }
    }

    // ── Threat scanning (throttled) ─────────────────────────────────────────────

    /// <summary>Re-scans for the nearest human and the exterminator at most ~5×/sec,
    /// caching the results so per-frame logic stays cheap.</summary>
    private void RefreshThreats()
    {
        if (Time.time - _lastThreatScan < 0.2f) return;
        _lastThreatScan = Time.time;

        _cachedHuman        = null;
        _cachedExterminator = null;
        float nearestHuman  = detectionRange;

        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || po.data == null) continue;

            float d = Vector3.Distance(transform.position, po.transform.position);
            if (d >= detectionRange) continue;

            // Exterminator identified by ObjDataSO name (it is the only thing that catches rats).
            if (po.data.objName == exterminatorName)
            {
                _cachedExterminator = po.transform;
                continue;
            }

            // Otherwise: is this a human worker?
            var tag = po.GetComponent<AgentTypeTag>();
            bool isHuman = tag != null && (tag.agentType & AgentType.Human) != 0;
            if (isHuman && d < nearestHuman)
            {
                nearestHuman = d;
                _cachedHuman = po.transform;
            }
        }
    }

    // ── Age & Growth ──────────────────────────────────────────────────────────

    private void UpdateAge()
    {
        _age += Time.deltaTime;
        float t = Mathf.Clamp01(_age / maxAge);
        transform.localScale = Vector3.one * Mathf.Lerp(RatFromScale, RatToScale, t);
        agent.speed = scurrySpeed * (1f + t * 0.4f);
    }

    // ── Breeding ──────────────────────────────────────────────────────────────

    private void UpdateBreeding()
    {
        if (Time.time - _lastBreedTime < breedingCooldown) return;
        if (_globalRatCount >= maxRatPopulation) return;
        if (isHiding || isScurryingAway) return;

        bool nearMate = false;
        foreach (var other in FindObjectsByType<RatBehavior>())
        {
            if (other == this) continue;
            if (Vector3.Distance(transform.position, other.transform.position) < breedingRadius)
            { nearMate = true; break; }
        }

        if (nearMate)
        {
            _timeNearMate += Time.deltaTime;
            if (_timeNearMate >= breedingDuration)
            {
                SpawnBabyRat();
                _timeNearMate  = 0f;
                _lastBreedTime = Time.time;
            }
        }
        else
        {
            _timeNearMate = Mathf.Max(0f, _timeNearMate - Time.deltaTime * 0.5f);
        }
    }

    private void SpawnBabyRat()
    {
        if (ratPrefab == null) return;

        Vector3 spawnPos = transform.position + (Vector3)(Random.insideUnitCircle * 1.5f);
        spawnPos.y = transform.position.y;

        if (!NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return;

        GameObject baby = Instantiate(ratPrefab, hit.position, Quaternion.identity);
        baby.transform.localScale = Vector3.one * RatFromScale;

        // Strip placement-system components — bred rats aren't saved objects
        foreach (var type in new System.Type[] {
            typeof(PlacedObject), typeof(BuildingData), typeof(BuildingHighlighter) })
        {
            var comp = baby.GetComponent(type);
            if (comp != null) Destroy(comp);
        }

        // Pass prefab reference so this generation can also breed
        var babyBehavior = baby.GetComponent<RatBehavior>();
        if (babyBehavior != null)
            babyBehavior.ratPrefab = ratPrefab;

        TryPlaySound(clipSqueak);
    }

    // ── Time-of-day ───────────────────────────────────────────────────────────

    private bool IsWorkHours()
    {
        if (_timeService == null) return false;
        int h = _timeService.Hour;
        return h >= 8 && h < 18;
    }

    // Returns 0–1: how bold the rat is right now (0 = very shy, 1 = bold)
    private float Boldness() => IsWorkHours() ? 0.35f : 1.0f;

    // ── Investigation trigger ─────────────────────────────────────────────────

    private void UpdateInvestigationTrigger()
    {
        int current = PlacedObjectRegistry.Count;
        if (current > _lastRegistryCount)
        {
            _lastRegistryCount = current;
            if (!isHiding && !isScurryingAway && Boldness() > 0.5f)
            {
                var target = FindNearestNewObject();
                if (target != null)
                    _queuedInvestigation = target;
            }
        }
    }

    private Transform FindNearestNewObject()
    {
        Transform nearest  = null;
        float     minDist  = investigationRadius;
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || po.data == null) continue;
            if (po.data.category == "Workers" || po.data.category == "Characters") continue;
            float d = Vector3.Distance(transform.position, po.transform.position);
            if (d < minDist) { minDist = d; nearest = po.transform; }
        }
        return nearest;
    }

    // ── Sound effects ─────────────────────────────────────────────────────────

    private void UpdateSoundEffects()
    {
        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        if (agent.velocity.magnitude > 0.5f && Time.time - _lastScurrySound > 2.5f)
        {
            TryPlaySound(clipScurry);
            _lastScurrySound = Time.time;
        }
    }

    private void TryPlaySound(AudioClip clip)
    {
        if (clip != null && _spatialAudio != null)
            _spatialAudio.PlayOneShot(clip);
    }

    // ── Wall-hugging direction ────────────────────────────────────────────────

    private Vector3 GetWallHuggingDirection()
    {
        Vector3 toWall = Vector3.zero;
        int numRays = 8;

        for (int i = 0; i < numRays; i++)
        {
            float   angle = i * Mathf.PI * 2f / numRays;
            Vector3 dir   = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

            // NavMesh.Raycast returns true when it hits a navmesh boundary (i.e. a wall)
            if (NavMesh.Raycast(transform.position, transform.position + dir * 6f,
                out NavMeshHit hit, NavMesh.AllAreas))
            {
                float weight = 1f / Mathf.Max(0.1f, hit.distance);
                toWall += dir * weight;
            }
        }

        Vector3 random = Random.insideUnitSphere;
        random.y = 0;
        if (toWall.sqrMagnitude < 0.01f) return random.normalized;

        // 60% toward nearest wall, 40% random — feels natural without being robotic
        return Vector3.Lerp(random.normalized, toWall.normalized, 0.6f).normalized;
    }

    /// <summary>A flee heading: mostly directly away from <paramref name="threat"/>,
    /// blended toward the nearest wall so rats run for cover rather than into the open.</summary>
    private Vector3 GetFleeDirection(Vector3 threat)
    {
        Vector3 away = transform.position - threat;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = Random.insideUnitSphere;
            away.y = 0f;
        }
        away.Normalize();

        Vector3 wall = GetWallHuggingDirection();
        return Vector3.Lerp(away, wall, 0.4f).normalized;
    }

    // ── Core behaviour loop ───────────────────────────────────────────────────

    private IEnumerator BehaviorRoutine()
    {
        while (true)
        {
            if (_dying) yield break;

            if (!agent.isOnNavMesh)
            {
                yield return StartCoroutine(WaitUntilOnNavMesh());
                continue;
            }

            if (isHiding)
            {
                // Stay hidden for a while, then peek out if no humans/exterminator are near.
                _hideTimer += 0.5f;
                if (_hideTimer >= hideStayDuration && _cachedHuman == null && _cachedExterminator == null)
                    Unhide();

                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Investigate queued new object
            if (_queuedInvestigation != null)
            {
                yield return StartCoroutine(InvestigateRoutine(_queuedInvestigation));
                _queuedInvestigation = null;
                continue;
            }

            float boldness = Boldness();

            // During work hours rats hide much more; after hours they scavenge more
            float adjustedHidingChance = hidingChance * (IsWorkHours() ? 1.8f : 0.4f);
            if (Random.value < adjustedHidingChance)
            {
                yield return StartCoroutine(GoToHidingSpot());
                if (isHiding) continue;
            }

            // Scavenge occasionally (more often after hours) — only when no one's watching.
            float scavengeRoll = scavengeChance * (IsWorkHours() ? 0.3f : 1.5f);
            if (_cachedHuman == null && Random.value < scavengeRoll)
                yield return StartCoroutine(ScavengeRoutine());

            yield return StartCoroutine(ScurryInCircles());
            yield return StartCoroutine(ScurryOff());

            if (Random.value > 0.1f)
                yield return StartCoroutine(SniffRoutine());

            // After-hours: extra scurry pass when bold
            if (!IsWorkHours() && Random.value < 0.5f)
                yield return StartCoroutine(ScurryOff());
        }
    }

    // ── Investigation ─────────────────────────────────────────────────────────

    private IEnumerator InvestigateRoutine(Transform target)
    {
        if (!agent.isOnNavMesh || target == null) yield break;

        agent.SetDestination(target.position);
        yield return StartCoroutine(WaitForPath(1.5f));

        // Sniff at the object
        agent.isStopped = true;
        animator.SetTrigger("Sniff");
        yield return new WaitForSeconds(Random.Range(1.5f, 3f));
        agent.isStopped = false;
    }

    // ── Scavenging ────────────────────────────────────────────────────────────

    private IEnumerator ScavengeRoutine()
    {
        Transform target = FindNearestInventoryItem();
        if (target == null || !agent.isOnNavMesh) yield break;

        agent.SetDestination(target.position);
        yield return StartCoroutine(WaitForPath(1.5f));

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
            TryPlaySound(clipGnaw);

            float gnaw = gnawDuration + Random.Range(0f, 2f);
            yield return new WaitForSeconds(gnaw);

            // Gnawing on this product pushes it toward contamination. The component is
            // added on demand and accumulates infestation time across visits/rats.
            if (target != null)
            {
                var contam = target.GetComponent<ContaminationState>();
                if (contam == null) contam = target.gameObject.AddComponent<ContaminationState>();
                contam.AddInfestation(gnaw);
            }

            if (agent.isOnNavMesh)
                agent.isStopped = false;
        }
    }

    private Transform FindNearestInventoryItem()
    {
        Transform nearest = null;
        float     minDist = 15f;
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || po.data == null) continue;
            bool isFood = po.data.category == palletCategory;
            if (!isFood)
            {
                foreach (string n in palletNames)
                    if (po.data.objName.Contains(n)) { isFood = true; break; }
            }
            if (!isFood) continue;

            float d = Vector3.Distance(transform.position, po.transform.position);
            if (d < minDist) { minDist = d; nearest = po.transform; }
        }
        return nearest;
    }

    // ── Scurry in circles ─────────────────────────────────────────────────────

    private IEnumerator ScurryInCircles()
    {
        if (!agent.isOnNavMesh) yield break;

        Vector3 center = transform.position;
        float   radius = circleDiameter / 2f;
        int rotations  = Random.Range(1, 3);

        for (int r = 0; r < rotations; r++)
        {
            for (int i = 0; i < circlePoints; i++)
            {
                if (!agent.isOnNavMesh) yield break;

                float   angle  = i * Mathf.PI * 2 / circlePoints;
                Vector3 target = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                agent.SetDestination(target);
                yield return StartCoroutine(WaitForPath(0.2f));
            }
        }
    }

    // ── Scurry off (wall-hugging) ─────────────────────────────────────────────

    private IEnumerator ScurryOff()
    {
        if (!agent.isOnNavMesh) yield break;

        Vector3 dir    = GetWallHuggingDirection();
        Vector3 target = transform.position + dir * Random.Range(5f, 12f);
        target.y       = transform.position.y;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3.0f, agent.areaMask))
        {
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(hit.position);
                yield return StartCoroutine(WaitForPath(0.5f));
            }
        }
        yield return new WaitForSeconds(Random.Range(0.1f, 0.5f));
    }

    // ── Sniff ─────────────────────────────────────────────────────────────────

    private IEnumerator SniffRoutine()
    {
        if (agent.isOnNavMesh)
            agent.isStopped = true;

        animator.SetTrigger("Sniff");
        yield return new WaitForSeconds(4f);

        if (agent.isOnNavMesh)
            agent.isStopped = false;
    }

    // ── Hiding ────────────────────────────────────────────────────────────────

    private IEnumerator GoToHidingSpot()
    {
        if (Time.time - _spawnTime < 10f) yield break;

        Transform spot = FindNearestHidingSpot();
        if (spot == null || !agent.isOnNavMesh) yield break;

        agent.SetDestination(spot.position);
        yield return StartCoroutine(WaitForPath(0.1f));

        if (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 0.5f)
        {
            // Keep the agent ENABLED (just stopped) so the rat can instantly bolt when a
            // human/exterminator gets close — no NavMesh re-warp dance needed.
            isHiding        = true;
            _hideTimer      = 0f;
            agent.isStopped = true;
            SetGhosted(true);
            Debug.Log($"[Rat] Reached hiding spot '{spot.name}' — ghosting to see-through gray.");
        }
    }

    /// <summary>Leave a hiding spot voluntarily (coast is clear) — un-tint and resume.</summary>
    private void Unhide()
    {
        isHiding   = false;
        _hideTimer = 0f;
        SetGhosted(false);
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
    }

    private Transform FindNearestHidingSpot()
    {
        Transform nearest = null;
        float     minDist = 15f;
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || po.data == null) continue;
            bool isPallet = po.data.category == palletCategory;
            if (!isPallet)
                foreach (string n in palletNames)
                    if (po.data.objName.Contains(n)) { isPallet = true; break; }

            if (!isPallet) continue;
            float d = Vector3.Distance(transform.position, po.transform.position);
            if (d < minDist) { minDist = d; nearest = po.transform; }
        }
        return nearest;
    }

    // ── Exterminator / panic ──────────────────────────────────────────────────

    /// <summary>The exterminator is within range. The rat is flushed out, squeaks, and a
    /// %-chance roll decides whether it is caught (dies) or escapes (panic-flees).</summary>
    private void HandleExterminator()
    {
        StopAllCoroutines();
        ExposeNow();
        TryPlaySound(clipSqueak);

        float roll = Random.value;
        Debug.Log($"[Rat] Exterminator flushed a rat — catch roll {roll:F2} vs chance {catchChance:F2}.");

        if (roll < catchChance)
        {
            Debug.Log("[Rat] Caught by the exterminator! It's done for.");
            Die();
        }
        else
        {
            Debug.Log("[Rat] Slipped away from the exterminator!");
            StartCoroutine(ScurryAwayRoutine());
        }
    }

    /// <summary>Flee directly away from a threat position for the panic duration.</summary>
    private IEnumerator FleeFromRoutine(Vector3 threatPos)
    {
        isScurryingAway = true;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit snapHit, 5f, NavMesh.AllAreas))
            transform.position = snapHit.position;

        yield return StartCoroutine(WaitUntilOnNavMesh());

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed     = scurrySpeed * 1.5f;

            float endTime = Time.time + panicDuration;
            while (Time.time < endTime && agent.isOnNavMesh && !_dying)
            {
                Vector3 dir    = GetFleeDirection(threatPos) * Random.Range(6f, 12f);
                Vector3 target = transform.position + dir;
                target.y       = transform.position.y;

                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, agent.areaMask))
                {
                    agent.SetDestination(hit.position);
                    float timeout = Time.time + 2.5f;
                    while (Time.time < timeout && Time.time < endTime && agent.isOnNavMesh && !_dying)
                    {
                        if (!agent.pathPending && agent.remainingDistance <= 0.5f) break;
                        yield return null;
                    }
                }
                yield return null;
            }

            if (agent.isOnNavMesh) agent.speed = scurrySpeed;
        }

        isScurryingAway = false;
        if (!_dying) StartCoroutine(BehaviorRoutine());
    }

    private IEnumerator ScurryAwayRoutine()
    {
        isScurryingAway = true;
        isHiding        = false;

        ExposeNow();

        // Snap to nearest NavMesh point before moving — otherwise Unity may throw
        // "Failed to create agent because it is not close enough to the NavMesh"
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit snapHit, 5f, NavMesh.AllAreas))
            transform.position = snapHit.position;

        yield return StartCoroutine(WaitUntilOnNavMesh());

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed     = scurrySpeed * 1.5f;

            float endTime = Time.time + panicDuration;
            while (Time.time < endTime && agent.isOnNavMesh && !_dying)
            {
                Vector3 dir    = Random.insideUnitSphere * 12f;
                dir.y          = 0;
                Vector3 target = transform.position + dir;

                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, agent.areaMask))
                {
                    agent.SetDestination(hit.position);
                    float timeout = Time.time + 3f;
                    while (Time.time < timeout && Time.time < endTime && agent.isOnNavMesh && !_dying)
                    {
                        if (!agent.pathPending && agent.remainingDistance <= 0.5f) break;
                        yield return null;
                    }
                }
                yield return null;
            }

            if (agent.isOnNavMesh)
                agent.speed = scurrySpeed;
        }

        isScurryingAway = false;
        if (!_dying) StartCoroutine(BehaviorRoutine());
    }

    // ── Death (placeholder: stop → sink + fade → destroy) ──────────────────────

    /// <summary>Kill this rat. For now this is the simple placeholder death the user
    /// approved (no flip/land animation yet): stop moving, sink into the ground while
    /// fading out, then delete.</summary>
    public void Die()
    {
        if (_dying) return;
        _dying = true;
        StopAllCoroutines();
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        SetAnimWalking(false);
        if (agent != null && agent.isActiveAndEnabled)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }

        // Start from the ghost material set so we can fade alpha smoothly to 0.
        SetGhosted(true);

        Vector3 start = transform.position;
        Vector3 end   = start + Vector3.down * deathSinkDepth;

        float t = 0f;
        while (t < deathDuration)
        {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / deathDuration);
            transform.position = Vector3.Lerp(start, end, f);
            SetGhostAlpha(1f - f);
            yield return null;
        }

        Destroy(gameObject);
    }

    // ── Material-tint ghosting ──────────────────────────────────────────────────

    /// <summary>Caches each renderer's original materials and builds a parallel set of
    /// transparent-gray "ghost" instances we can swap in while hiding / dying.</summary>
    private void CacheMaterials()
    {
        visuals       = GetComponentsInChildren<Renderer>();
        _originalMats = new Material[visuals.Length][];
        _ghostMats    = new Material[visuals.Length][];

        for (int i = 0; i < visuals.Length; i++)
        {
            var src = visuals[i].sharedMaterials;
            _originalMats[i] = src;

            var ghosts = new Material[src.Length];
            for (int j = 0; j < src.Length; j++)
            {
                if (src[j] == null) continue;
                var g = new Material(src[j]);
                MakeTransparent(g, ghostColor);
                ghosts[j] = g;
            }
            _ghostMats[i] = ghosts;
        }
    }

    /// <summary>Swap the rat between its opaque originals and the see-through ghost set.</summary>
    private void SetGhosted(bool on)
    {
        if (visuals == null) return;

        _ghosted = on;
        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] == null) continue;
            visuals[i].sharedMaterials = on ? _ghostMats[i] : _originalMats[i];
        }
    }

    /// <summary>Snap fully opaque (back to original materials) — used when exposed/fleeing.</summary>
    private void ExposeNow()
    {
        if (_ghosted) SetGhosted(false);
        isHiding = false;
    }

    /// <summary>Fade the currently-shown ghost materials' alpha (used by the death dissolve).</summary>
    private void SetGhostAlpha(float a)
    {
        if (_ghostMats == null) return;
        for (int i = 0; i < _ghostMats.Length; i++)
        {
            if (_ghostMats[i] == null) continue;
            foreach (var m in _ghostMats[i])
            {
                if (m == null) continue;
                Color c = ghostColor; c.a *= a;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
            }
        }
    }

    /// <summary>Reconfigure a URP/Lit (or fallback) material instance to render
    /// transparent at runtime, tinted to <paramref name="color"/>.</summary>
    private static void MakeTransparent(Material m, Color color)
    {
        if (m == null) return;

        // URP/Lit runtime opaque → transparent switch. The fragment outputs alpha when
        // _Surface == 1; the blend states + render queue do the actual see-through.
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);  // 0 = opaque, 1 = transparent
        if (m.HasProperty("_Blend"))   m.SetFloat("_Blend", 0f);    // 0 = alpha blend
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite"))  m.SetFloat("_ZWrite", 0f);

        // Straight alpha blending — premultiply must stay OFF or the blend math fights us.
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        // A ghost shouldn't write depth or cast shadows, or it reads as solid.
        m.SetShaderPassEnabled("ShadowCaster", false);
        m.SetShaderPassEnabled("DepthOnly", false);

        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", color);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEnumerator WaitUntilOnNavMesh()
    {
        int retries = 0;
        while (agent != null && !agent.isOnNavMesh && retries < 60)
        {
            if (agent.isActiveAndEnabled &&
                NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                if (Mathf.Abs(hit.position.y - transform.position.y) < 0.5f || retries > 10)
                    try { agent.Warp(hit.position); } catch { }
            }
            if (agent.isOnNavMesh) break;
            retries++;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator WaitForPath(float stoppingDist)
    {
        yield return null;
        while (agent.isActiveAndEnabled)
        {
            if (!agent.isOnNavMesh)
            {
                yield return StartCoroutine(WaitUntilOnNavMesh());
                if (!agent.isOnNavMesh) break;
            }
            if (!agent.pathPending && agent.remainingDistance <= stoppingDist) break;
            yield return null;
        }
    }

    private void SetAnimWalking(bool walking)
    {
        if (animator != null) animator.SetBool("IsWalking", walking);
    }
}
