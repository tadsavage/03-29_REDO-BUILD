using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls the Employee Photo Booth system. Intercepts hiring of employees,
/// instantiates the selected role/gender low-poly model in the photo booth,
/// takes a waist-up studio portrait using a dedicated camera and spotlights,
/// and stores the sprite in memory so all game UIs display it automatically.
/// </summary>
public class EmployeePhotoBooth : MonoBehaviour
{
    public static EmployeePhotoBooth Instance { get; private set; }

    [Header("Studio Prefabs")]
    [SerializeField] private GameObject _workerMalePrefab;
    [SerializeField] private GameObject _workerFemalePrefab;
    [SerializeField] private GameObject _bossPrefab;
    [SerializeField] private GameObject _bossPrefabFemale;
    [SerializeField] private GameObject _securityPrefab;
    [SerializeField] private GameObject _securityPrefabFemale;
    [SerializeField] private GameObject _clerkPrefab;
    [SerializeField] private GameObject _clerkPrefabFemale;
    [SerializeField] private GameObject _exterminatorPrefab;
    [SerializeField] private GameObject _exterminatorPrefabFemale;
    [SerializeField] private GameObject _truckDriverPrefab;
    [SerializeField] private GameObject _truckDriverPrefabFemale;
    [SerializeField] private GameObject _floorWorkerPrefab;
    [SerializeField] private GameObject _floorWorkerPrefabFemale;

    [Header("Studio Setup")]
    [Tooltip("Clear color of the camera (backdrop color).")]
    [SerializeField] private Color _backdropColor = new Color(0.322f, 0.419f, 0.401f, 1.0f);
    [SerializeField] private float _studioLightIntensity = 1.0f;
    [Tooltip("Vertical offset for the IC Clerk model in the booth. The clerk model is ~0.17 units shorter than the Worker models, so without this it sits noticeably lower in frame than Worker portraits.")]
    [SerializeField] private float _clerkVerticalOffset = 0.17f;

    [Header("Live Feed Settings")]
    [SerializeField] private float _cameraRotationSpeed = 0.5f;
    [SerializeField] private float _cameraMaxAngle = 45f;
    [SerializeField] private float _cameraDistance = 2.75f;
    [SerializeField] private float _cameraHeight = 1.45f;
    [SerializeField] private float _lookAtHeight = 1.35f;
    [Tooltip("Downward shift applied to the live-feed model only, so heads aren't cut off at the top of the EmployeeInfoUI avatar frame (~0.3 of the frame height at the current camera distance).")]
    [SerializeField] private float _avatarFrameShift = -0.38f;
    [SerializeField] private float _waveIntervalMin = 5f;
    [SerializeField] private float _waveIntervalMax = 5f;

    [Header("Post-Processing")]
    [Tooltip("Volume profile applied only to the photo booth's cameras (live feed + portrait capture) — gives the 'Kodak Instamatic' look (DOF, saturation, contrast, vignette, grain) without affecting the main game camera.")]
    [SerializeField] private VolumeProfile _photoBoothProfile;

    private Camera _liveCamera;
    private Light _liveLight;
    private GameObject _liveModelInstance;
    private RenderTexture _liveRenderTexture;
    private bool _isLiveFeedActive = false;
    private float _nextGestureTime = 0f;
    private float _gestureEndTime = 0f;
    private bool _isGesturing = false;
    private EmployeeMood _liveMood = EmployeeMood.Neutral;
    private Animator _liveAnimator;
    private float _liveStartTime;
    private bool _warmedUp; // first portrait render of the session is a throwaway warm-up

    public RenderTexture LiveRenderTexture => _liveRenderTexture;
    public bool IsLiveFeedActive => _isLiveFeedActive;

    // Runtime cache for custom portrait sprites in memory
    public static readonly Dictionary<string, Sprite> CustomAvatarCache = new Dictionary<string, Sprite>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Clear the cache first to discard any destroyed sprites from previous play sessions/domain loads
        CustomAvatarCache.Clear();

        // Pre-load existing custom portraits from disk so they survive game restarts/saves
        LoadAllPortraitsFromDisk();

        // Initialize RenderTexture for live feed
        _liveRenderTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        _liveRenderTexture.Create();

        // Dedicated post-processing volume for the photo booth's cameras only. It lives on
        // its own layer (PhotoBoothFX) so it never affects the main game camera's view.
        if (_photoBoothProfile != null)
        {
            GameObject volumeGO = new GameObject("PhotoBoothPostFX");
            volumeGO.transform.SetParent(transform);
            volumeGO.layer = LayerMask.NameToLayer("PhotoBoothFX");

            Volume volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = _photoBoothProfile;
        }
    }

    /// <summary>Routes a camera's rendering through the photo booth's dedicated post-processing volume only.</summary>
    private static void ApplyPhotoBoothPostFX(Camera cam)
    {
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.volumeLayerMask = LayerMask.GetMask("PhotoBoothFX");
    }

    private void Start()
    {
        // Subscribe to hire event in Start to guarantee all Awake methods have run
        if (EmployeeLifecycleService.Instance != null)
        {
            EmployeeLifecycleService.Instance.OnHired += OnEmployeeHired;
        }
        else
        {
            Debug.LogError("[EmployeePhotoBooth] EmployeeLifecycleService.Instance is null in Start!");
        }

        // Retroactively generate portraits for any already-registered active employees
        // who might have been spawned before we finished subscribing to OnHired.
        // Snapshot to a list first: GeneratePortraitForRecord instantiates a temporary
        // EmployeeIdentity in the photo booth, whose Awake()/OnDestroy() register/unregister
        // against this same EmployeeRegistry — mutating the live `All` view mid-enumeration
        // throws "Collection was modified".
        if (EmployeeRegistry.Instance != null)
        {
            foreach (var emp in new List<EmployeeIdentity>(EmployeeRegistry.Instance.All))
            {
                var record = emp.Record;
                if (record != null)
                {
                    string key = "Custom_" + record.employeeGuid;
                    if (!CustomAvatarCache.TryGetValue(key, out var sprite) || sprite == null)
                    {
                        GeneratePortraitForRecord(record);
                    }
                }
            }
        }
    }

    private void Update()
    {
        if (!_isLiveFeedActive || _liveCamera == null || _liveModelInstance == null) return;

        // 1. Rotate camera slowly from -45 to 45 degrees
        float timeSinceStart = Time.time - _liveStartTime;
        float angle = Mathf.Sin(timeSinceStart * _cameraRotationSpeed) * _cameraMaxAngle;
        
        // Position camera on a horizontal arc around the model (which is at Vector3.zero)
        // Note: Model faces local X+ (90 degrees rotation), so 0 angle should be (2, height, 0)
        float rad = angle * Mathf.Deg2Rad;
        float x = Mathf.Cos(rad) * _cameraDistance;
        float z = Mathf.Sin(rad) * _cameraDistance;
        
        _liveCamera.transform.localPosition = new Vector3(x, _cameraHeight, z);
        _liveCamera.transform.LookAt(transform.position + new Vector3(0f, _lookAtHeight, 0f));

        // 2. Handle mood-driven gesture animation (happy = wave, angry = rude gesture, ...)
        if (_liveAnimator != null)
        {
            string gestureParam = EmployeeMoodAnimator.GestureParam(_liveMood);
            if (gestureParam != null)
            {
                if (!_isGesturing && Time.time >= _nextGestureTime)
                {
                    _isGesturing = true;
                    _liveAnimator.SetBool(gestureParam, true);
                    _gestureEndTime = Time.time + 2.0f; // Gesture for 2 seconds
                }
                else if (_isGesturing && Time.time >= _gestureEndTime)
                {
                    _isGesturing = false;
                    _liveAnimator.SetBool(gestureParam, false);
                    _nextGestureTime = Time.time + UnityEngine.Random.Range(_waveIntervalMin, _waveIntervalMax);
                }
            }
        }
    }

    public void StartLiveFeed(EmployeeRecord record)
    {
        StopLiveFeed(); // Clean up any existing feed

        // GetPrefabForRoleAndGender already picked the exact intended model for this role+gender —
        // do NOT follow this with ApplyModularAvatar (removed 2026-09-21). It unconditionally hid
        // this prefab's own mesh and replaced it with a randomly-assembled generic modular body,
        // which is why every live feed/portrait used to show a generic outfit (e.g. a Boss showing
        // up in coveralls) regardless of which fixed-look prefab was assigned above.
        GameObject prefab = GetPrefabForRoleAndGender(record.role, record.gender, out _);
        if (prefab == null) prefab = _workerMalePrefab;

        _liveModelInstance = Instantiate(prefab, transform);
        DisableWanderScripts(_liveModelInstance);
        _liveModelInstance.transform.localPosition = new Vector3(0f, GetModelVerticalOffset(record.role) + _avatarFrameShift, 0f);
        _liveModelInstance.transform.localRotation = Quaternion.Euler(0, 90, 0);
        _liveModelInstance.name = $"LiveFeed_{record.employeeName}";

        var identity = _liveModelInstance.GetComponent<EmployeeIdentity>();
        if (identity != null)
        {
            identity.ApplyRecord(record);
            identity.enabled = false;
        }

        // Posture (slouch) and gesture (wave/rude) are independent: a tired-but-happy
        // employee can slouch AND still wave — fatigue no longer blocks a morale gesture.
        EmployeeMood postureMood = EmployeeMoodEvaluator.EvaluatePosture(record);
        _liveMood = EmployeeMoodEvaluator.EvaluateGesture(record);

        _liveAnimator = _liveModelInstance.GetComponent<Animator>();
        if (_liveAnimator != null)
        {
            // See the matching note in CapturePortrait — this raw prefab's own Animator is otherwise
            // free to physically translate the transform if its current state has baked-in root motion.
            _liveAnimator.applyRootMotion = false;
            _liveAnimator.SetBool("IsWalking", false);
            _liveAnimator.SetBool(EmployeeMoodAnimator.WavingParam, false);
            _liveAnimator.SetBool(EmployeeMoodAnimator.AngryParam, false);
            EmployeeMoodAnimator.ApplyPersistent(_liveAnimator, postureMood);
        }

        // Disable movement and the in-world animation driver — AgentAnimation calls
        // SetAllBools(false) every frame while off the NavMesh, which would stomp the
        // mood-driven IsWaving/IsAngry/IsTired bools we set below.
        var agent = _liveModelInstance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        if (identity != null) identity.enabled = false;
        var agentAnimation = _liveModelInstance.GetComponent<AgentAnimation>();
        if (agentAnimation != null) agentAnimation.enabled = false;

        // Camera
        GameObject camGO = new GameObject("LiveFeed_Camera");
        camGO.transform.SetParent(transform);
        _liveCamera = camGO.AddComponent<Camera>();
        _liveCamera.clearFlags = CameraClearFlags.SolidColor;
        _liveCamera.backgroundColor = _backdropColor;
        _liveCamera.fieldOfView = 26f;
        _liveCamera.targetTexture = _liveRenderTexture;
        ApplyPhotoBoothPostFX(_liveCamera);

        // Light
        GameObject lightGO = new GameObject("LiveFeed_Light");
        lightGO.transform.SetParent(transform);
        lightGO.transform.localPosition = new Vector3(0.10f, 1.38f, 0.81f);
        _liveLight = lightGO.AddComponent<Light>();
        _liveLight.type = LightType.Point;
        _liveLight.intensity = _studioLightIntensity;
        _liveLight.range = 2.0f;
        _liveLight.color = new Color(0.872f, 0.857f, 0.834f, 1.0f);
        _liveLight.shadows = LightShadows.Hard;

        _liveStartTime = Time.time;
        _isGesturing = false;
        _nextGestureTime = Time.time + UnityEngine.Random.Range(_waveIntervalMin, _waveIntervalMax);
        _isLiveFeedActive = true;
    }

    public void StopLiveFeed()
    {
        _isLiveFeedActive = false;
        if (_liveModelInstance != null) Destroy(_liveModelInstance);
        if (_liveCamera != null) Destroy(_liveCamera.gameObject);
        if (_liveLight != null) Destroy(_liveLight.gameObject);
        _liveModelInstance = null;
        _liveCamera = null;
        _liveLight = null;
        _liveAnimator = null;
    }


    private void OnDestroy()
    {
        if (EmployeeLifecycleService.Instance != null)
        {
            EmployeeLifecycleService.Instance.OnHired -= OnEmployeeHired;
        }
    }

    // Unity calls this both when a built game quits AND when Play Mode is stopped in the
    // Editor, so this is what keeps the Portraits folder from growing unbounded across
    // repeated Play/Stop cycles where no explicit save happens in between.
    private void OnApplicationQuit()
    {
        PrunePortraits();
    }

    public void GeneratePortraitForRecord(EmployeeRecord record)
    {
        if (record == null) return;

        // Determine model and force matching gender
        GameObject prefab = GetPrefabForRoleAndGender(record.role, record.gender, out EmployeeGender finalGender);
        record.gender = finalGender;

        if (prefab == null)
        {
            Debug.LogWarning($"[EmployeePhotoBooth] No prefab found for role={record.role}, gender={record.gender}. Using WorkerMale as default.");
            prefab = _workerMalePrefab;
        }

        // Capture and save portrait
        CapturePortrait(record, prefab);
    }

    private void OnEmployeeHired(EmployeeRecord record)
    {
        EnsurePortrait(record);
    }

    /// <summary>
    /// Guarantees the given employee has a generated Custom_ portrait, regardless of how they
    /// entered the scene (fresh hire via OnHired, auto-spawn testing, or save-restore via
    /// EmployeeSpawner). Reuses the cached portrait if one already exists (e.g. captured while
    /// still a hiring-board candidate, or loaded from disk at startup) rather than re-rendering.
    /// Save-restored employees previously never reached this code at all — they came from
    /// EmployeeSpawner.SpawnEmployee() directly, which doesn't go through OnHired — so legacy
    /// records with a static stock-photo avatarResourceKey would keep that key forever. Calling
    /// this from SpawnEmployee for every spawn path closes that gap.
    /// </summary>
    public void EnsurePortrait(EmployeeRecord record)
    {
        if (record == null) return;

        string key = "Custom_" + record.employeeGuid;
        if (CustomAvatarCache.TryGetValue(key, out var sprite) && sprite != null)
        {
            record.avatarResourceKey = key;
            return;
        }

        GeneratePortraitForRecord(record);
    }

    private GameObject GetPrefabForRoleAndGender(EmployeeRole role, EmployeeGender gender, out EmployeeGender finalGender)
    {
        // Default to the employee's OWN gender, not a hardcoded Male — this used to be hardcoded
        // Male so that a role with no female-specific PORTRAIT body available (Boss/Security/
        // Exterminator, until the _xPrefabFemale fields above) would permanently overwrite
        // record.gender to Male the instant a portrait was generated, clobbering a correctly-rolled
        // Female record before EmployeeSpawner's own gender-matched avatar pools ever saw it (Tad's
        // "50/50" ask couldn't work while this ran first). Falling back to a male placeholder BODY
        // for the portrait photo is fine; silently rewriting the employee's real gender is not.
        finalGender = gender == EmployeeGender.Female ? EmployeeGender.Female : EmployeeGender.Male;

        switch (role)
        {
            // Boss/Security/Exterminator use a dedicated female portrait body when one is assigned,
            // otherwise fall through to the shared male body above WITHOUT touching finalGender.
            // HR shares the Boss portrait look too (2026-09-21, Tad's ask).
            case EmployeeRole.Boss:
            case EmployeeRole.HR:
                if (gender == EmployeeGender.Female && _bossPrefabFemale != null)
                {
                    finalGender = EmployeeGender.Female;
                    return _bossPrefabFemale;
                }
                return _bossPrefab;

            case EmployeeRole.Security:
                if (gender == EmployeeGender.Female && _securityPrefabFemale != null)
                {
                    finalGender = EmployeeGender.Female;
                    return _securityPrefabFemale;
                }
                return _securityPrefab;

            // InventoryControl is no longer forced Female (2026-09-21) — it used to hardcode
            // finalGender = Female here, clobbering the real gender the same way Boss/Security/
            // Exterminator used to. Now behaves like them: dedicated female body when assigned,
            // otherwise the shared male body.
            case EmployeeRole.InventoryControl:
                if (gender == EmployeeGender.Female && _clerkPrefabFemale != null)
                {
                    finalGender = EmployeeGender.Female;
                    return _clerkPrefabFemale;
                }
                return _clerkPrefab;

            case EmployeeRole.Exterminator:
                if (gender == EmployeeGender.Female && _exterminatorPrefabFemale != null)
                {
                    finalGender = EmployeeGender.Female;
                    return _exterminatorPrefabFemale;
                }
                return _exterminatorPrefab;

            case EmployeeRole.TruckDriver:
                if (gender == EmployeeGender.Female && _truckDriverPrefabFemale != null)
                {
                    finalGender = EmployeeGender.Female;
                    return _truckDriverPrefabFemale;
                }
                return _truckDriverPrefab;

            // Receiver / Reach Truck Operator / Dock Stocker Operator / Order Selector (2026-09-21) —
            // previously fell through to the generic default branch below (plain WorkerMale/
            // WorkerFemale placeholder portrait); now use the construction-worker look, matching the
            // world model change.
            case EmployeeRole.Receiver:
            case EmployeeRole.ReachTruckOperator:
            case EmployeeRole.DockStockerOperator:
            case EmployeeRole.OrderSelector:
                if (gender == EmployeeGender.Female && _floorWorkerPrefabFemale != null)
                {
                    finalGender = EmployeeGender.Female;
                    return _floorWorkerPrefabFemale;
                }
                return _floorWorkerPrefab;

            default:
                // Floor workers, supervisor, etc.
                if (gender == EmployeeGender.Female)
                {
                    finalGender = EmployeeGender.Female;
                    return _workerFemalePrefab;
                }
                else
                {
                    finalGender = EmployeeGender.Male;
                    return _workerMalePrefab;
                }
        }
    }

    /// <summary>Per-role vertical correction so every model frames the same in the booth.</summary>
    private float GetModelVerticalOffset(EmployeeRole role) => role switch
    {
        EmployeeRole.InventoryControl => _clerkVerticalOffset,
        _ => 0f
    };

    /// <summary>Neutralizes PolyPerfect's own wander/idle AI (People_WanderScript / Common_WanderScript)
    /// on a photo-booth or live-feed temp instance, WITHOUT leaving the Animator undriven.
    ///
    /// Found 2026-09-21, in two parts:
    /// 1. Left running, this script weight-randomizes among several idle animation states
    ///    (Common_WanderScript.idleStates, picked via Random.Range) via an Animator bool per state —
    ///    completely independent of EmployeeIdentity/the Animator state this class otherwise controls.
    ///    Some idle variants aren't a plain standing pose (a "drop/crouch" state froze Boss/Security
    ///    portraits low in frame; a state with forward root motion made another candidate look like
    ///    she'd stepped into the camera) — so this used to just be flatly disabled.
    /// 2. But flatly disabling it (mb.enabled = false immediately after Instantiate) also means its
    ///    Start() — which is what actually calls SetBool to kick the Animator into ANY of those idle
    ///    states in the first place — never runs. With NOTHING ever setting an idle bool true, the
    ///    Animator just sits in its raw, never-played entry state, which is a T-pose. That's what
    ///    "disable it entirely" produced once verified against a real rendered portrait (previously
    ///    assumed fixed from log/behavior alone, without looking at the actual pixels).
    ///
    /// Fix: read idleStates via reflection (no compile-time reference to the Polyperfect assembly,
    /// same reasoning as the type-name match below), deterministically pick the entry with the
    /// HIGHEST stateWeight (matches the script's own bias toward its "primary" idle — e.g. for
    /// man/woman_construction_worker that's "Waving", weight 1, vs "Texting" at weight 0), then
    /// disable the script so nothing can later drift to a different (possibly bad) state.
    ///
    /// Found 2026-09-22: setting the matching Animator BOOL (e.g. "isWaving") and letting a normal
    /// transition carry the state machine there does NOT work here — these rigs' Animator Controller
    /// default state is literally a state named "Tpose" (a T-Pose motion clip, presumably a rigger's
    /// placeholder), and there is no "Tpose -> Waving" (nor catch-all Any State) transition wired to
    /// it, so the bool sits true forever while the state machine never leaves Tpose. Confirmed live:
    /// GetCurrentAnimatorStateInfo(0).shortNameHash after SetBool+Update matched the "Tpose" state's
    /// hash exactly, not "Waving"'s. Fix: call Animator.Play(stateName, ...) directly using the idle
    /// entry's own `stateName` field ("Waving") to jump straight into that state, bypassing the
    /// transition graph entirely — a plain SetBool alone is not reliable for this rig.</summary>
    private static void DisableWanderScripts(GameObject modelInstance)
    {
        var animator = modelInstance.GetComponentInChildren<Animator>(true);
        foreach (var mb in modelInstance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || !mb.GetType().Name.Contains("WanderScript")) continue;

            if (animator != null)
            {
                var idleStatesField = mb.GetType().GetField("idleStates",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (idleStatesField?.GetValue(mb) is System.Array idleStates && idleStates.Length > 0)
                {
                    object best = null;
                    int bestWeight = int.MinValue;
                    foreach (var state in idleStates)
                    {
                        var weightField = state.GetType().GetField("stateWeight");
                        int weight = weightField != null ? (int)weightField.GetValue(state) : 0;
                        if (weight > bestWeight) { bestWeight = weight; best = state; }
                    }
                    var boolField = best?.GetType().GetField("animationBool");
                    string animationBool = boolField?.GetValue(best) as string;
                    if (!string.IsNullOrEmpty(animationBool))
                        animator.SetBool(animationBool, true);

                    var nameField = best?.GetType().GetField("stateName");
                    string stateName = nameField?.GetValue(best) as string;
                    if (!string.IsNullOrEmpty(stateName))
                        animator.Play(stateName, 0, 0f);
                }
            }

            mb.enabled = false;
        }
    }

    private void CapturePortrait(EmployeeRecord record, GameObject prefab)
    {
        if (prefab == null) return;

        // 1. Instantiate temporary model at photo booth origin
        GameObject modelInstance = Instantiate(prefab, transform);

        // Remove NavMesh agent entirely — not needed in photobooth
        var agent = modelInstance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) Destroy(agent);

        modelInstance.transform.localPosition = new Vector3(0f, GetModelVerticalOffset(record.role), 0f);
        modelInstance.transform.localRotation = Quaternion.Euler(0, 90, 0);
        modelInstance.name = $"PhotoBooth_Temp_{record.employeeName}";

        var identity = modelInstance.GetComponent<EmployeeIdentity>();
        if (identity != null)
        {
            identity.ApplyRecord(record);
            identity.enabled = false;
        }

        // Ensure low-poly character has settled its pose/anim
        Animator animator = modelInstance.GetComponent<Animator>();
        if (animator != null)
        {
            // applyRootMotion used to be forced off inside the now-removed ApplyModularAvatar — losing
            // that meant this raw prefab's OWN Animator was free to physically translate the transform
            // during the settle Update below if its default state has baked-in root motion (a "step
            // into place" intro clip, common on PolyPerfect rigs). Caught 2026-09-21: a candidate
            // rendered zoomed in / off-center, as if she'd walked toward the camera mid-capture.
            animator.applyRootMotion = false;
            // Forces the state machine to actually initialize/enter its default state. Without this,
            // a freshly Instantiate()'d Animator that never goes through a normal Update() cycle (this
            // whole capture happens synchronously in one frame, then the instance is destroyed) can sit
            // in its raw bind pose instead — found 2026-09-21 on WorkerMale/WorkerFemale (used by
            // Loader/Supervisor/Admin/Sanitation), which have no WanderScript to kick them via SetBool
            // the way the raw PolyPerfect fixed-look prefabs do. The old (now-removed) ApplyModularAvatar
            // always called this on ITS OWN nested animator for the same reason — it just never got
            // applied to the animator actually used by this direct (non-modular) capture path.
            //
            // MUST run BEFORE DisableWanderScripts (which sets an idle bool true, e.g. "isWaving" for
            // construction-worker rigs) — Rebind() resets Animator parameters back to the Controller's
            // authored defaults, so calling it AFTER that SetBool silently wiped the override straight
            // back to false, undoing the whole fix. Caught 2026-09-22 the same way as everything else
            // in this file: rendering an actual portrait and looking at it (T-pose again) rather than
            // trusting that "compiles + no errors" meant the earlier fix still worked.
            animator.Rebind();
        }

        DisableWanderScripts(modelInstance);

        if (animator != null)
        {
            // Animator.Play() only takes effect on the NEXT Update — calling Update(1.0f) as the very
            // first Update after Play() spends its whole delta just processing the switch and lands
            // exactly on normalizedTime 0 (verified live), which can read as a static/awkward first
            // frame. A zero-delta Update processes the Play() itself; the following real Update then
            // advances properly from inside the target state.
            animator.Update(0f);
            animator.Update(1.0f);
        }

        // Clean up redundant scripts/components on the temporary clone
        StripNonVisualComponents(modelInstance);

        // 2. Spawn temporary camera for passport driver/license style portrait (waist up)
        GameObject camGO = new GameObject("PhotoBooth_Camera");
        camGO.transform.SetParent(transform);
        camGO.transform.localPosition = new Vector3(2.0f, 1.45f, 0f); // Match camera preview position
        camGO.transform.LookAt(modelInstance.transform.position + new Vector3(0f, 1.35f, 0f));

        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = _backdropColor;
        cam.fieldOfView = 26f; // Match camera preview FOV
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;
        ApplyPhotoBoothPostFX(cam);

        // Solve lighting issue: Create a nice Point light to get shadows/angles on low poly polygons
        GameObject lightGO = new GameObject("PhotoBooth_Light");
        lightGO.transform.SetParent(transform);
        // Positioned close and slightly to the side/front to create high-contrast shadows on low-poly edges
        lightGO.transform.localPosition = new Vector3(0.10f, 1.38f, 0.81f);
        
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = _studioLightIntensity;
        light.range = 2.0f;
        light.color = new Color(0.872f, 0.857f, 0.834f, 1.0f);
        light.shadows = LightShadows.Hard; // Hard shadows for low poly look
        lightGO.transform.LookAt(modelInstance.transform.position + new Vector3(0f, 1.4f, 0f));

        // 3. Render to temporary texture
        RenderTexture rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        // The first capture of the session can come back wrong (shader / post-volume /
        // skinning warm-up) — that's why candidate #0's portrait looked off. Prime the
        // pipeline with a throwaway render the first time, then render for real.
        if (!_warmedUp) { cam.Render(); _warmedUp = true; }
        cam.Render();

        // 4. Read back pixels to Texture2D
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        // 5. Create Sprite
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));

        // 6. Cache it
        string customKey = "Custom_" + record.employeeGuid;
        CustomAvatarCache[customKey] = sprite;
        record.avatarResourceKey = customKey;

        // 7. Save to disk so saves can load it
        byte[] pngBytes = tex.EncodeToPNG();
        SavePortraitToDisk(record.employeeGuid, pngBytes);

        // 8. Clean up temporary objects immediately.
        // DestroyImmediate is required here (not Destroy) because portraits for
        // multiple candidates are captured back-to-back in the same frame —
        // Destroy() defers removal until end-of-frame, leaving the previous
        // model/camera/light visible (and rendered) in the next candidate's shot.
        DestroyImmediate(modelInstance);
        DestroyImmediate(camGO);
        DestroyImmediate(lightGO);
        rt.Release();
        DestroyImmediate(rt);

    }

    private void SavePortraitToDisk(string guid, byte[] pngBytes)
    {
        try
        {
            string buildDir = Path.Combine(Application.persistentDataPath, "Portraits");
            Directory.CreateDirectory(buildDir);
            string buildPath = Path.Combine(buildDir, guid + ".png");
            File.WriteAllBytes(buildPath, pngBytes);

#if UNITY_EDITOR
            string editorDir = Path.Combine(Application.dataPath, "_Project/Sprites/Portraits");
            Directory.CreateDirectory(editorDir);
            string editorPath = Path.Combine(editorDir, guid + ".png");
            File.WriteAllBytes(editorPath, pngBytes);
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EmployeePhotoBooth] Failed to save portrait to disk: {ex.Message}");
        }
    }

    private void LoadAllPortraitsFromDisk()
    {
        try
        {
            // Load from persistent data path (standalone build/persistent state)
            string buildDir = Path.Combine(Application.persistentDataPath, "Portraits");
            if (Directory.Exists(buildDir))
            {
                foreach (string file in Directory.GetFiles(buildDir, "*.png"))
                {
                    string guid = Path.GetFileNameWithoutExtension(file);
                    byte[] bytes = File.ReadAllBytes(file);
                    Texture2D tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                    tex.LoadImage(bytes);
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    CustomAvatarCache["Custom_" + guid] = sprite;
                }
            }

#if UNITY_EDITOR
            // Load from Editor path so it registers during development
            string editorDir = Path.Combine(Application.dataPath, "_Project/Sprites/Portraits");
            if (Directory.Exists(editorDir))
            {
                foreach (string file in Directory.GetFiles(editorDir, "*.png"))
                {
                    string guid = Path.GetFileNameWithoutExtension(file);
                    string key = "Custom_" + guid;
                    if (!CustomAvatarCache.ContainsKey(key))
                    {
                        byte[] bytes = File.ReadAllBytes(file);
                        Texture2D tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                        tex.LoadImage(bytes);
                        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        CustomAvatarCache[key] = sprite;
                    }
                }
            }
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EmployeePhotoBooth] Failed to load portraits from disk: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes portrait PNGs on disk (build + editor folders) for anyone no longer in the active
    /// character database — i.e. not a current employee, not in the former-employee archive, and
    /// not a candidate still on the hiring board. Unhired candidates that cycle off the board are
    /// the main source of accumulation. Returns the number of portrait files removed.
    /// </summary>
    [ContextMenu("Prune Orphan Portraits")]
    public int PrunePortraits()
    {
        HashSet<string> keepGuids = CollectActiveCharacterGuids();

        int removed = 0;
        removed += PrunePortraitFolder(Path.Combine(Application.persistentDataPath, "Portraits"), keepGuids, deleteMeta: false);
#if UNITY_EDITOR
        removed += PrunePortraitFolder(Path.Combine(Application.dataPath, "_Project/Sprites/Portraits"), keepGuids, deleteMeta: true);
#endif

        return removed;
    }

    public const string TerminatedPlaceholderLabel = "TERMINATED — NOT ELIGIBLE FOR REHIRE";

    private static Sprite _terminatedPlaceholderSprite;

    /// <summary>
    /// Generic dark head-and-shoulders silhouette shown in place of a real portrait for
    /// terminated employees, whose actual photo is pruned from disk (see PrunePortraits).
    /// Generated once and cached in memory; never written to disk.
    /// </summary>
    public static Sprite GetTerminatedPlaceholderSprite()
    {
        if (_terminatedPlaceholderSprite != null) return _terminatedPlaceholderSprite;

        const int size = 256;
        var backdrop = new Color(0.10f, 0.10f, 0.10f, 1f);
        var silhouette = new Color(0.30f, 0.07f, 0.07f, 1f);

        var headCenter = new Vector2(size * 0.5f, size * 0.66f);
        float headRadiusX = size * 0.16f;
        float headRadiusY = size * 0.19f;
        var shoulderCenter = new Vector2(size * 0.5f, size * 0.05f);
        float shoulderRadiusX = size * 0.40f;
        float shoulderRadiusY = size * 0.32f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inHead = IsInsideEllipse(x, y, headCenter, headRadiusX, headRadiusY);
                bool inShoulders = IsInsideEllipse(x, y, shoulderCenter, shoulderRadiusX, shoulderRadiusY);
                pixels[y * size + x] = (inHead || inShoulders) ? silhouette : backdrop;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        _terminatedPlaceholderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return _terminatedPlaceholderSprite;
    }

    private static bool IsInsideEllipse(int x, int y, Vector2 center, float radiusX, float radiusY)
    {
        float dx = (x - center.x) / radiusX;
        float dy = (y - center.y) / radiusY;
        return dx * dx + dy * dy <= 1f;
    }

    /// <summary>
    /// Resolves the sprite that should be displayed for a given employee record: the terminated
    /// placeholder for Terminated status, otherwise the cached custom portrait (or null if none
    /// is cached, leaving callers to fall back to their own default-avatar logic).
    /// </summary>
    public static Sprite ResolveDisplaySprite(EmployeeRecord record)
    {
        if (record == null) return null;
        if (record.status == EmploymentStatus.Terminated) return GetTerminatedPlaceholderSprite();

        if (!string.IsNullOrEmpty(record.avatarResourceKey) &&
            record.avatarResourceKey.StartsWith("Custom_") &&
            CustomAvatarCache.TryGetValue(record.avatarResourceKey, out var cachedSprite) &&
            cachedSprite != null)
        {
            return cachedSprite;
        }
        return null;
    }

    /// <summary>
    /// Builds the set of employee GUIDs whose portraits must be preserved: everyone currently
    /// employed, everyone in the former-employee archive (kept for rehire / HR history), and every
    /// candidate still on the hiring board (so a not-yet-hired applicant never loses their photo).
    /// </summary>
    private static HashSet<string> CollectActiveCharacterGuids()
    {
        var guids = new HashSet<string>();

        if (EmployeeRegistry.Instance != null)
            foreach (var employee in EmployeeRegistry.Instance.All)
                AddGuid(guids, employee?.Record?.employeeGuid);

        // Resigned (and other non-terminated separations) keep their real photo; Terminated
        // employees do not — their portrait is pruned and a generic placeholder is shown instead
        // (see GetTerminatedPlaceholderSprite), since "not eligible for rehire" means no photo on file.
        if (FormerEmployeeArchive.HasInstance)
            foreach (var record in FormerEmployeeArchive.Instance.All)
                if (record != null && record.status != EmploymentStatus.Terminated)
                    AddGuid(guids, record.employeeGuid);

        if (HiringService.Instance != null)
            foreach (var candidate in HiringService.Instance.Roster)
                AddGuid(guids, candidate?.record?.employeeGuid);

        return guids;
    }

    private static void AddGuid(HashSet<string> set, string guid)
    {
        if (!string.IsNullOrEmpty(guid)) set.Add(guid);
    }

    /// <summary>
    /// Removes every "{guid}.png" in a folder whose GUID isn't in the keep-set. Also drops the
    /// matching in-memory cache entry and (in the editor) the Unity ".meta" sidecar so the asset
    /// database stays clean.
    /// </summary>
    private static int PrunePortraitFolder(string folder, HashSet<string> keepGuids, bool deleteMeta)
    {
        if (!Directory.Exists(folder)) return 0;

        int removed = 0;
        foreach (string file in Directory.GetFiles(folder, "*.png"))
        {
            string guid = Path.GetFileNameWithoutExtension(file);
            if (keepGuids.Contains(guid)) continue;

            try
            {
                File.Delete(file);
                if (deleteMeta && File.Exists(file + ".meta")) File.Delete(file + ".meta");
                CustomAvatarCache.Remove("Custom_" + guid);
                removed++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EmployeePhotoBooth] Failed to delete orphan portrait '{file}': {ex.Message}");
            }
        }
        return removed;
    }

    private void StripNonVisualComponents(GameObject go)
    {
        if (go == null) return;

        // Disable standard navigation agent immediately
        var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        // Disable or destroy all MonoBehaviours except the Animator
        var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var b in behaviours)
        {
            if (b == null) continue;
            if (b == this) continue;
            
            // Disable the behaviour so its Awake/Start/Update don't run or trigger warnings
            b.enabled = false;
        }

        // Disable colliders so physics doesn't interact with them
        var colliders = go.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            if (c != null) c.enabled = false;
        }

        // Disable rigidbodies so gravity or force doesn't move them
        var rbs = go.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rbs)
        {
            if (rb != null) rb.isKinematic = true;
        }
    }

}
