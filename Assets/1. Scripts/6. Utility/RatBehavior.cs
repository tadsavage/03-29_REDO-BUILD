using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Rat AI — scurries, hides, breeds, scavenges, investigates new objects.
///
/// Sound names to add to SoundLibrary:
///   "Rat_Squeak"  — short squeak when startled
///   "Rat_Scurry"  — soft scuttling loop while moving
///   "Rat_Gnaw"    — gnawing sound during scavenge
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

    // ── Hiding & Exterminator ─────────────────────────────────────────────────
    [Header("Hiding & Exterminator")]
    [SerializeField] private float   detectionRange   = 4.0f;
    [SerializeField] private string  exterminatorName = "Exterminator";
    [SerializeField] private float   hidingChance     = 0.6f;
    [SerializeField] private float   panicDuration    = 8.0f;
    [SerializeField] private string[] palletNames     = { "A Chep", "StackPlts", "Cases" };
    [SerializeField] private string   palletCategory  = "Inventory";

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

    // ── Runtime state ─────────────────────────────────────────────────────────
    private NavMeshAgent agent;
    private Animator     animator;
    private Renderer[]   visuals;

    private bool  isHiding              = false;
    private bool  isScurryingAway       = false;
    private float lastDetectionTime;
    private bool  exterminatorNearCached = false;

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
        visuals  = GetComponentsInChildren<Renderer>();

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
        // Immediate exterminator panic
        if (!isScurryingAway && IsExterminatorNear())
        {
            StopAllCoroutines();
            TryPlaySound(clipSqueak);
            StartCoroutine(ScurryAwayRoutine());
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
        foreach (var other in FindObjectsByType<RatBehavior>(FindObjectsSortMode.None))
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

    // ── Core behaviour loop ───────────────────────────────────────────────────

    private IEnumerator BehaviorRoutine()
    {
        while (true)
        {
            if (!agent.isOnNavMesh)
            {
                yield return StartCoroutine(WaitUntilOnNavMesh());
                continue;
            }

            if (isHiding)
            {
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

            // Scavenge occasionally (more often after hours)
            float scavengeRoll = scavengeChance * (IsWorkHours() ? 0.3f : 1.5f);
            if (Random.value < scavengeRoll)
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
            yield return new WaitForSeconds(gnawDuration + Random.Range(0f, 2f));
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
        Transform spot = FindNearestHidingSpot();
        if (spot == null || !agent.isOnNavMesh) yield break;

        agent.SetDestination(spot.position);
        yield return StartCoroutine(WaitForPath(0.1f));

        if (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance < 0.5f)
        {
            isHiding        = true;
            agent.isStopped = true;
            agent.enabled   = false;
            SetVisuals(false);
        }
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

    // ── Panic / exterminator ──────────────────────────────────────────────────

    private IEnumerator ScurryAwayRoutine()
    {
        isScurryingAway = true;
        isHiding        = false;

        SetVisuals(true);

        // Snap to nearest NavMesh point before re-enabling — otherwise Unity throws
        // "Failed to create agent because it is not close enough to the NavMesh"
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit snapHit, 5f, NavMesh.AllAreas))
            transform.position = snapHit.position;

        agent.enabled = true;
        yield return null;

        yield return StartCoroutine(WaitUntilOnNavMesh());

        if (agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed     = scurrySpeed * 1.5f;

            float endTime = Time.time + panicDuration;
            while (Time.time < endTime && agent.isOnNavMesh)
            {
                Vector3 dir    = Random.insideUnitSphere * 12f;
                dir.y          = 0;
                Vector3 target = transform.position + dir;

                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, agent.areaMask))
                {
                    agent.SetDestination(hit.position);
                    float timeout = Time.time + 3f;
                    while (Time.time < timeout && Time.time < endTime && agent.isOnNavMesh)
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
        StartCoroutine(BehaviorRoutine());
    }

    private bool IsExterminatorNear()
    {
        if (Time.time - lastDetectionTime < 0.2f) return exterminatorNearCached;
        lastDetectionTime       = Time.time;
        exterminatorNearCached  = false;
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || po.data == null) continue;
            if (po.data.objName == exterminatorName &&
                Vector3.Distance(transform.position, po.transform.position) < detectionRange)
            {
                exterminatorNearCached = true;
                return true;
            }
        }
        return false;
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
                if (Mathf.Abs(hit.position.y - transform.position.y) < 2f || retries > 10)
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

    private void SetVisuals(bool visible)
    {
        if (visuals == null) return;
        foreach (var r in visuals) r.enabled = visible;
    }

    private void SetAnimWalking(bool walking)
    {
        if (animator != null) animator.SetBool("IsWalking", walking);
    }
}
