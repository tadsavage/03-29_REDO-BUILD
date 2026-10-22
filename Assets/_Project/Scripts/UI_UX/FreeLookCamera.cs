using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// RTS/simulation orbital camera. The focal point is ALWAYS centered on screen — the camera is
/// positioned on an orbit around it and looks straight at it. The controls are fully
/// decoupled so none of them disturbs the others:
///
/// WASD / Arrows — pan the focal point on the XZ plane (relative to current yaw)
/// Q / E         — rotate (yaw) around the focal point; Q counter-clockwise, E clockwise
/// Right Mouse   — hold + drag to translate the focal point ("grab" the play screen and drag it)
/// Middle Mouse  — hold + drag to orbit (yaw = drag X, pitch = drag Y)
/// Scroll Wheel  — zoom in / out (changes orbit distance only)
/// Shift         — 3x speed for movement and zoom
///
/// Distance is controlled ONLY by zoom, pitch/yaw ONLY by orbit, focal point ONLY by pan/drag.
/// Nothing rewrites distance behind the user's back, so orbiting never dollies and panning
/// never creeps. Camera world height is a natural consequence of distance + pitch.
public class FreeLookCamera : MonoBehaviour
{
    [SerializeField] private BuildMenuUI buildMenuUI;

    // Move speed, zoom speed, orbit sensitivity, and pitch sensitivity are deliberately NOT
    // [SerializeField] — they are pulled exclusively from CameraDevSettings (PlayerPrefs-backed,
    // edited only via the Tools window's Dev Settings panel) so nothing in the Inspector/prefab
    // can silently drift or override them. See ApplyDevSettings().
    private float moveSpeed;
    private float orbitSensitivity;
    private float pitchSensitivity;
    private float zoomSpeed;
    private float minCameraHeight;
    private float rotateSpeed;

    [Header("Diorama Depth of Field (Ultra)")]
    [Tooltip("Drives a lens-style (Bokeh) depth of field that keeps the floor under the screen centre sharp and softly blurs the near and far distance, for a miniature / diorama look. Ultra preset only; Good keeps the project default and Toaster turns depth of field off.")]
    [SerializeField] private bool  dioramaDepthOfField = true;
    [Tooltip("Telephoto focal length in mm. Longer flattens perspective and thins the sharp zone (70-95 is subtle, 110+ is strong).")]
    [SerializeField] private float dofFocalLengthMm = 90f;
    [Tooltip("F-stop. Lower = thinner sharp zone and stronger blur (1.2 is strong, 1.8-2.8 subtle).")]
    [SerializeField] private float dofFStop = 1.6f;
    [Tooltip("Ultra's tilt-shift strength (2026-10-22, Tad wanted strong DoF): replaces the focal length above. Blur grows with focal length squared.")]
    [SerializeField] private float dofUltraFocalLengthMm = 120f;
    [Tooltip("Ultra's f-stop; replaces the f-stop above. URP's minimum is 1.0 (strongest).")]
    [SerializeField] private float dofUltraFStop = 1.0f;
    [Tooltip("Aperture blades; 5-6 gives angular highlights that suit the low-poly look.")]
    [SerializeField] private int   dofBladeCount = 6;
    [Tooltip("F-stop used while a clicked employee is in focus. Higher = a deeper sharp zone, so the whole person (head to feet) stays crisp while the surroundings still soften. 3-5 is good; set equal to the main f-stop for no change.")]
    [SerializeField] private float dofFStopFocused = 3.6f;
    [Tooltip("F-stop while a clicked pallet is in focus. Very high = the depth-of-field blur is effectively gone, so the pallet and everything around it is sharp.")]
    [SerializeField] private float dofFStopPalletFocus = 32f;
    [Tooltip("How quickly focus follows zoom/pan, in seconds. 0 = instant.")]
    [SerializeField] private float dofFocusSmoothTime = 0.12f;

    [Header("Pitch (Tilt) Limits")]
    [SerializeField] private float pitchMin       = 15f;
    [SerializeField] private float pitchMax       = 80f;
    [SerializeField] private float defaultPitch   = 45f;

    [Header("Zoom Distance Bounds  (orbit radius)")]
    [Tooltip("Closest the camera can zoom toward the focal point.")]
    [SerializeField] private float minDistance     = 4f;
    [Tooltip("Farthest the camera can zoom out from the focal point.")]
    [SerializeField] private float maxDistance     = 45f;
    [SerializeField] private float defaultDistance = 15f;

    [Header("Focal Point XZ Bounds")]
    [SerializeField] private float xMin = -180f;
    [SerializeField] private float xMax =  180f;
    [SerializeField] private float zMin =  -180f;
    [SerializeField] private float zMax =  180f;

    [Header("Drag Pan (Right Mouse)")]
    [Tooltip("World units the focal point travels per pixel of right-mouse drag, at defaultDistance. Scales with current zoom distance so the drag still feels 1:1 with the screen when zoomed in/out.")]
    private float dragPanSensitivity = 0.001f;

    private Vector3 _focalPoint;
    private float   _yaw;
    private float   _pitch;
    private float   _distance;
    private bool    _orbiting;
    private bool    _dragPanning;
    private bool    _stateLoadedFromSave;

    // Set when FocusOn snaps to a target outside the normal pan bounds (e.g. "find my
    // employee" out in the yard). While true the per-frame pan clamp is suspended so the
    // off-bounds focal point sticks; the first manual pan clears it and bounds re-engage.
    private bool    _focusBeyondBounds;

    // When set, the focal point tracks this transform's XZ every frame (the camera "follows"
    // a selected employee, Sims/SimCity style). A manual WASD pan clears it; orbit/zoom keep following.
    private Transform _followTarget;

    private void Awake()
    {
        if (minDistance >= maxDistance)   { minDistance = 4f;  maxDistance = 45f;    Debug.LogWarning("[FreeLookCamera] minDistance/maxDistance invalid — reset to defaults."); }
        if (xMin >= xMax)                 { xMin = -18f; xMax = 18f;                 Debug.LogWarning("[FreeLookCamera] xMin/xMax invalid — reset to defaults."); }
        if (zMin >= zMax)                 { zMin =  -8f; zMax = 18f;                 Debug.LogWarning("[FreeLookCamera] zMin/zMax invalid — reset to defaults."); }
        if (pitchMin >= pitchMax)         { pitchMin = 15f; pitchMax = 80f;          Debug.LogWarning("[FreeLookCamera] pitchMin/pitchMax invalid — reset to defaults."); }

        defaultDistance = Mathf.Clamp(defaultDistance, minDistance, maxDistance);
        defaultPitch    = Mathf.Clamp(defaultPitch, pitchMin, pitchMax);

        ApplyDevSettings();
        EnsureDoorTriggerSetup();

        // Auto-added for the same reason AgentTypeTag is above: nothing to wire in the Inspector and
        // nothing to lose to a prefab revert. It disables itself if the Walls layer or the ghost
        // material is missing, so adding it blind is safe.
        if (GetComponent<WallSeeThrough>() == null)
            gameObject.AddComponent<WallSeeThrough>();
    }

    /// <summary>
    /// Doors (ManDoorController/EntranceDoorController/RollupDoorController) react to
    /// OnTriggerEnter/Exit, filtered by AgentTypeTag — the camera has neither by default, so
    /// walking it through a doorway (zoomed in close, a pseudo-first-person view) never opened
    /// anything. Auto-adds what's needed, mirroring how AiNavigation auto-adds AgentTypeTag for
    /// agents. A kinematic Rigidbody is required for Unity to fire trigger events against an
    /// object whose transform is moved directly (ApplyTransform sets position every frame,
    /// never via physics) rather than via the physics system.
    /// </summary>
    private void EnsureDoorTriggerSetup()
    {
        if (GetComponent<AgentTypeTag>() == null)
        {
            var tag = gameObject.AddComponent<AgentTypeTag>();
            tag.agentType = AgentType.Human;
        }

        if (GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        if (GetComponent<BoxCollider>() == null)
        {
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size      = Vector3.one * 0.6f;
        }
    }

    private void OnEnable()
    {
        CameraDevSettings.OnChanged += ApplyDevSettings;
    }

    /// <summary>Pulls move/zoom speed and orbit/pitch sensitivity from CameraDevSettings.
    /// Called on Awake and whenever the Tools window's Dev Settings panel changes a value,
    /// so edits apply instantly without needing a scene reload.</summary>
    private void ApplyDevSettings()
    {
        moveSpeed        = CameraDevSettings.MoveSpeed;
        zoomSpeed        = CameraDevSettings.ZoomSpeed;
        orbitSensitivity = CameraDevSettings.OrbitSensitivity;
        pitchSensitivity = CameraDevSettings.PitchSensitivity;
        minCameraHeight  = CameraDevSettings.MinCameraHeight;
        rotateSpeed      = CameraDevSettings.RotateSpeed;
    }

    private void Start()
    {
        if (_stateLoadedFromSave) { ApplyTransform(); return; }

        _yaw   = transform.eulerAngles.y;
        _pitch = Mathf.Clamp(NormalizePitch(transform.eulerAngles.x), pitchMin, pitchMax);

        // Project the camera's forward onto a ground plane to find the focal point.
        float dy = transform.forward.y;
        if (dy < -0.001f)
        {
            float t = transform.position.y / -dy;
            _focalPoint = transform.position + transform.forward * Mathf.Max(t, 0f);
        }
        else
        {
            // Camera isn't looking downward — fall back to a default look-at in front of it.
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            _focalPoint = transform.position + rot * Vector3.forward * defaultDistance;
        }

        _focalPoint.x = Mathf.Clamp(_focalPoint.x, xMin, xMax);
        _focalPoint.z = Mathf.Clamp(_focalPoint.z, zMin, zMax);

        _distance = Mathf.Clamp(Vector3.Distance(transform.position, _focalPoint), minDistance, maxDistance);

        ApplyTransform();
    }

    private void Update()
    {
        // A scripted glide (log-link "go to door") owns the camera until it lands — ahead of the
        // modal guard, since it's usually kicked off from UI and must finish regardless.
        if (_gliding) { ProcessGlide(); ApplyTransform(); return; }

        // IsTextFieldFocused alone only covers "the user clicked into a field". A modal can be up and
        // owning input without any field focused — WASD would still fly the camera behind the dialog.
        if (UIInputGuard.IsTextFieldFocused || UIModalGuard.IsCapturing) return;

        bool overUI = IsPointerOverUI();
        bool fast   = Keyboard.current[Key.LeftShift].isPressed
                   || Keyboard.current[Key.RightShift].isPressed;

        float speed = fast ? moveSpeed * 3f : moveSpeed;

        ProcessPan(speed, overUI);
        ProcessDragPan(overUI);
        ProcessKeyboardRotate(fast);
        ProcessOrbit(overUI);
        ProcessZoom(overUI, fast ? zoomSpeed * 3f : zoomSpeed);
        ProcessFollow();
        ProcessPalletFocusClick(overUI);
        ApplyTransform();
    }

    // ── Click a pallet: focus on it and remove the depth-of-field blur ──────────────────────────────────
    private Transform _focusedPallet;
    private PlacementStateMachine _placementFsm;

    /// <summary>Plain left-click on a pallet (or on any case on it - the click resolves to the pallet it sits on)
    /// pulls focus onto it and takes the blur out; click it again, or click anywhere else, to release.
    /// Only while the placement state machine is idle, so build/move/delete clicks are never eaten, and never with
    /// shift held (shift-click follows an employee).</summary>
    private void ProcessPalletFocusClick(bool overUI)
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (overUI || UIInputGuard.IsPointerOverUIToolkit()) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) return;

        if (_placementFsm == null) _placementFsm = FindFirstObjectByType<PlacementStateMachine>();
        if (_placementFsm != null && !(_placementFsm.CurrentState is IdleState)) return;

        var cam = GetComponent<Camera>() != null ? GetComponent<Camera>() : Camera.main;
        if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit)) { _focusedPallet = null; return; }

        Transform pallet = null;
        var link = hit.collider.GetComponentInParent<GameCore.Inventory.PalletMasterLink>();
        if (link != null) pallet = link.transform;
        else
        {
            var builder = hit.collider.GetComponentInParent<PalletBuilder>();
            if (builder != null) pallet = builder.transform;
            else
            {
                var outbound = hit.collider.GetComponentInParent<OutboundPalletBuilder>();
                if (outbound != null) pallet = outbound.transform;
            }
        }
        _focusedPallet = (pallet != null && pallet != _focusedPallet) ? pallet : null;
    }

    // Track a followed target on the XZ plane (the player keeps orbit/zoom control).
    // ProcessPan clears _followTarget the moment the player pans manually, so this is a no-op
    // until then.
    private void ProcessFollow()
    {
        if (_followTarget == null) return;
        _focalPoint.x = _followTarget.position.x;
        _focalPoint.z = _followTarget.position.z;
        // Height too (smoothed): a followed employee is tracked at chest height, and a vehicle going up a ramp keeps its height.
        _focalPoint.y = Mathf.Lerp(_focalPoint.y, _followTarget.position.y, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        _focusBeyondBounds = true;   // a followed unit may roam beyond the normal pan bounds
    }

    // ── Diorama depth of field ───────────────────────────────────────────────────────────────────────
    // The game camera deliberately ignores every scene Volume (its volume layer mask is 0), so the only grade it
    // sees is the project's default profile. To drive depth of field without touching that asset, this builds its
    // own global Volume on a private layer and opens just that layer on this camera. The focus distance is the
    // distance along the view axis to the orbit's focal point, i.e. the floor under the screen centre, so it
    // follows zoom, pan and orbit without a target to assign.
    private const int DofVolumeLayer = 30;
    private Volume _dofVolume;
    private VolumeProfile _dofProfile;
    private DepthOfField _dof;
    private float _dofFocusVelocity;
    private float _dofFocus = 15f;
    private float _dofPalletBlend;
    private float _dofFocusedBlend;          // 0 = floor focus, 1 = an employee is in focus
    private bool _dofFocusInitialised;

    private void EnsureDioramaDof()
    {
        if (_dof != null) return;
        var cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        var go = new GameObject("[DioramaDoF]") { hideFlags = HideFlags.DontSave };
        go.layer = DofVolumeLayer;
        _dofVolume = go.AddComponent<Volume>();
        _dofVolume.isGlobal = true;
        _dofVolume.priority = 100f;
        _dofVolume.weight = 1f;
        _dofProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _dof = _dofProfile.Add<DepthOfField>(true);
        _dofVolume.sharedProfile = _dofProfile;

        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null) data.volumeLayerMask |= 1 << DofVolumeLayer;
    }

    private void LateUpdate() => UpdateDioramaDof();

    private void UpdateDioramaDof()
    {
        var preset = GraphicsPresetManager.Instance != null ? GraphicsPresetManager.Instance.CurrentPreset : GraphicsPresetManager.Preset.Ultra;

        // Good leaves the project default alone (our volume off); Toaster must not blur at all.
        bool wantVolume = dioramaDepthOfField && preset != GraphicsPresetManager.Preset.Good;
        if (!wantVolume) { if (_dofVolume != null) _dofVolume.enabled = false; return; }

        EnsureDioramaDof();
        if (_dof == null) return;
        _dofVolume.enabled = true;

        if (preset == GraphicsPresetManager.Preset.Toaster)
        {
            _dof.mode.Override(DepthOfFieldMode.Off);
            return;
        }

        // Focus pull: while an employee's banner is open, focus ON that person (chest height) so they stay
        // crisp while the foreground and background blur. Otherwise focus the floor under the screen centre.
        Vector3 focusPoint = _focalPoint;
        var banners = EmployeeWorldBanners.Existing;
        var focused = banners != null ? banners.FocusedEmployee : null;
        if (focused != null) focusPoint = focused.transform.position + Vector3.up * 1.0f;
        // A clicked pallet wins over everything: focus lands on it (about mid-height).
        bool palletFocus = _focusedPallet != null;
        if (palletFocus) focusPoint = _focusedPallet.position + Vector3.up * 0.6f;

        float target = Mathf.Max(0.1f, Vector3.Dot(focusPoint - transform.position, transform.forward));
        if (!_dofFocusInitialised || dofFocusSmoothTime <= 0f) { _dofFocus = target; _dofFocusInitialised = true; }
        else _dofFocus = Mathf.SmoothDamp(_dofFocus, target, ref _dofFocusVelocity, dofFocusSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        // With someone in focus, deepen the sharp zone (higher f-stop) so all of them is crisp, not just the plane
        // at chest height; eases back out when the banner closes.
        _dofFocusedBlend = Mathf.MoveTowards(_dofFocusedBlend, focused != null ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        float baseStop = dofUltraFStop;   // only Ultra gets this far (Good/Toaster return above)
        float fStop = Mathf.Lerp(baseStop, Mathf.Max(baseStop, dofFStopFocused), _dofFocusedBlend);
        _dofPalletBlend = Mathf.MoveTowards(_dofPalletBlend, palletFocus ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        fStop = Mathf.Lerp(fStop, Mathf.Max(fStop, dofFStopPalletFocus), _dofPalletBlend);

        _dof.mode.Override(DepthOfFieldMode.Bokeh);
        _dof.focusDistance.Override(_dofFocus);
        _dof.focalLength.Override(dofUltraFocalLengthMm);
        _dof.aperture.Override(fStop);
        _dof.bladeCount.Override(Mathf.Clamp(dofBladeCount, 3, 9));
        _dof.bladeCurvature.Override(1f);
    }

    private void OnDestroy()
    {
        if (_dofVolume != null) Destroy(_dofVolume.gameObject);
        if (_dofProfile != null) Destroy(_dofProfile);
    }

    private void OnDisable()
    {
        _orbiting      = false;
        _dragPanning   = false;
        Cursor.visible = true;
        CameraDevSettings.OnChanged -= ApplyDevSettings;
    }

    private void ProcessPan(float speed, bool overUI)
    {
        var flatRot     = Quaternion.Euler(0f, _yaw, 0f);
        var flatForward = flatRot * Vector3.forward;
        var flatRight   = flatRot * Vector3.right;

        var delta = Vector3.zero;

        if (Keyboard.current[Key.W].isPressed || Keyboard.current[Key.UpArrow].isPressed)
            delta += flatForward;
        if (Keyboard.current[Key.S].isPressed || Keyboard.current[Key.DownArrow].isPressed)
            delta -= flatForward;
        if (Keyboard.current[Key.D].isPressed || Keyboard.current[Key.RightArrow].isPressed)
            delta += flatRight;
        if (Keyboard.current[Key.A].isPressed || Keyboard.current[Key.LeftArrow].isPressed)
            delta -= flatRight;

        if (delta.sqrMagnitude > 0.01f)
        {
            var move = delta.normalized * (speed * Time.unscaledDeltaTime);
            _focalPoint.x += move.x;
            _focalPoint.z += move.z;
            _followTarget = null;   // manual pan ends "follow selected employee"
            _focusBeyondBounds = false;
        }

        // Apply XZ bounds (Y is now free, controlled only by zoom/pitch)
        if (!_focusBeyondBounds)
        {
            _focalPoint.x = Mathf.Clamp(_focalPoint.x, xMin, xMax);
            _focalPoint.z = Mathf.Clamp(_focalPoint.z, zMin, zMax);
        }
    }

    /// <summary>Right-mouse "grab and drag" pan — translates the focal point opposite the drag
    /// direction so the play screen moves WITH the mouse, like dragging a map. Distinct from
    /// ProcessPan's WASD keys: this reads raw mouse delta rather than a fixed speed, and scales
    /// with the current zoom distance so a given drag covers the same apparent screen distance
    /// whether zoomed in or out.</summary>
    private void ProcessDragPan(bool overUI)
    {
        if (Mouse.current.rightButton.wasPressedThisFrame && !overUI)
        {
            _dragPanning   = true;
            Cursor.visible = false;
        }
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            _dragPanning   = false;
            Cursor.visible = true;
        }

        if (!_dragPanning) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        if (delta.sqrMagnitude < 0.0001f) return;

        var flatRot     = Quaternion.Euler(0f, _yaw, 0f);
        var flatForward = flatRot * Vector3.forward;
        var flatRight   = flatRot * Vector3.right;

        float scale = dragPanSensitivity * Mathf.Max(_distance, 0.001f);
        var move = (-flatRight * delta.x - flatForward * delta.y) * scale;

        _focalPoint.x += move.x;
        _focalPoint.z += move.z;
        _followTarget = null;   // manual drag ends "follow selected employee"
        _focusBeyondBounds = false;

        _focalPoint.x = Mathf.Clamp(_focalPoint.x, xMin, xMax);
        _focalPoint.z = Mathf.Clamp(_focalPoint.z, zMin, zMax);
    }

    /// <summary>
    /// Q/E yaw around the focal point — the keyboard equivalent of a middle-mouse orbit's
    /// horizontal axis. Pitch is deliberately left out: it has hard limits and a wrong pitch is
    /// far more disorienting to recover from than a wrong yaw, so it stays a mouse-only gesture.
    ///
    /// Not gated on overUI, unlike orbit/zoom/drag. Those all begin with a click or a scroll, so
    /// letting them fire while the cursor sits over a panel would steal the panel's own input.
    /// A held key isn't aimed at anything, and WASD panning already works regardless of where the
    /// cursor happens to be resting — gating this would make the camera stop rotating whenever
    /// the mouse drifted over the build menu, which reads as the key having failed.
    /// Text fields and modals are already handled by the guard at the top of Update.
    /// </summary>
    private void ProcessKeyboardRotate(bool fast)
    {
        float dir = 0f;
        if (Keyboard.current[Key.E].isPressed) dir += 1f;   // clockwise
        if (Keyboard.current[Key.Q].isPressed) dir -= 1f;   // counter-clockwise
        if (Mathf.Approximately(dir, 0f)) return;           // both held cancels out, as it should

        _yaw += dir * rotateSpeed * (fast ? 3f : 1f) * Time.unscaledDeltaTime;
    }

    private void ProcessOrbit(bool overUI)
    {
        if (Mouse.current.middleButton.wasPressedThisFrame && !overUI)
        {
            _orbiting      = true;
            Cursor.visible = false;
        }
        if (Mouse.current.middleButton.wasReleasedThisFrame)
        {
            _orbiting      = false;
            Cursor.visible = true;
        }

        if (!_orbiting) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        _yaw  += delta.x * orbitSensitivity;
        _pitch = Mathf.Clamp(_pitch - delta.y * pitchSensitivity, pitchMin, pitchMax);
    }

    private void ProcessZoom(bool overUI, float speed)
    {
        if (overUI) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.001f) return;

        // Scroll up = zoom in = reduce orbit distance. Distance is the ONLY thing zoom touches.
        _distance -= Mathf.Sign(scroll) * speed;
        _distance  = Mathf.Clamp(_distance, minDistance, maxDistance);
    }

    private void ApplyTransform()
    {
        // Pure orbital placement: sit on the orbit around the focal point and look straight at it.
        // This is what guarantees the focal point is always dead-center on screen.
        var rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 pos = _focalPoint + rot * (Vector3.back * _distance);

        // Floor clamp (CameraDevSettings.MinCameraHeight) — a low pitch + close zoom toward a
        // focal point near ground/foundation height would otherwise put the camera position
        // below the floor, clipping through it.
        if (pos.y < minCameraHeight) pos.y = minCameraHeight;

        transform.SetPositionAndRotation(pos, rot);
    }

    private static float NormalizePitch(float eulerX)
    {
        // Euler X comes back in [0,360); convert the >180 range to negative so the clamp behaves.
        return eulerX > 180f ? eulerX - 360f : eulerX;
    }

    private bool IsPointerOverUI()
        => (buildMenuUI != null && buildMenuUI.IsPointerOverBuildMenu)
        || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        || UIInputGuard.IsPointerOverUIToolkit();

    public CameraSaveData GetState()
        => new CameraSaveData
        {
            focusPoint = _focalPoint,
            pitch      = _pitch,
            yaw        = _yaw,
            distance   = _distance
        };

    public void SetState(CameraSaveData state)
    {
        if (state == null) return;
        _stateLoadedFromSave = true;
        _followTarget = null;          // a loaded camera isn't following anyone
        _focusBeyondBounds = false;

        var fp = state.focusPoint;
        fp.x = Mathf.Clamp(fp.x, xMin, xMax);
        fp.z = Mathf.Clamp(fp.z, zMin, zMax);

        _focalPoint = fp;
        _pitch      = Mathf.Clamp(state.pitch, pitchMin, pitchMax);
        _yaw        = state.yaw;
        _distance   = Mathf.Clamp(state.distance > 0.1f ? state.distance : defaultDistance, minDistance, maxDistance);

        ApplyTransform();
    }

    /// <summary>Recenter the camera's focal point on a world position (keeps current
    /// pitch/yaw/distance). Snaps to the target even if it's outside the normal pan bounds —
    /// so "find my employee" can reach anyone in the yard. Normal panning re-engages the
    /// bounds (see ProcessPan). Pass clampToBounds:true to keep the XZ clamped.</summary>
    public void FocusOn(Vector3 worldPosition, bool clampToBounds = false)
    {
        if (clampToBounds)
        {
            _focalPoint.x = Mathf.Clamp(worldPosition.x, xMin, xMax);
            _focalPoint.z = Mathf.Clamp(worldPosition.z, zMin, zMax);
            _focalPoint.y = worldPosition.y;
            _focusBeyondBounds = false;
        }
        else
        {
            _focalPoint = worldPosition;
            _focusBeyondBounds = true;
        }
        ApplyTransform();
    }

    // Scripted glide state — see GlideTo.
    private bool _gliding;
    private float _glideT, _glideDuration;
    private Vector3 _glideFromFocus, _glideToFocus;
    private float _glideFromYaw, _glideToYaw, _glideFromPitch, _glideToPitch, _glideFromDist, _glideToDist;

    /// <summary>Smoothly flies the camera to a new framing: focal point, yaw, pitch and orbit distance
    /// all eased together over <paramref name="seconds"/>. Player input is ignored until it lands. Runs
    /// on unscaled time so it still works while the game is paused. Like FocusOn, the target may lie
    /// outside the normal pan bounds; ordinary panning re-engages them afterwards.</summary>
    public void GlideTo(Vector3 focus, float yaw, float pitch, float distance, float seconds = 0.6f)
    {
        _followTarget = null;
        _focusBeyondBounds = true;

        _glideFromFocus = _focalPoint;
        _glideFromYaw = _yaw;
        _glideFromPitch = _pitch;
        _glideFromDist = _distance;

        _glideToFocus = focus;
        _glideToYaw = _yaw + Mathf.DeltaAngle(_yaw, yaw); // shortest way round
        _glideToPitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        _glideToDist = Mathf.Clamp(distance, minDistance, maxDistance);

        _glideT = 0f;
        _glideDuration = Mathf.Max(0.01f, seconds);
        _gliding = true;
    }

    private void ProcessGlide()
    {
        _glideT += Time.unscaledDeltaTime / _glideDuration;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_glideT));
        _focalPoint = Vector3.Lerp(_glideFromFocus, _glideToFocus, t);
        _yaw = Mathf.Lerp(_glideFromYaw, _glideToYaw, t);
        _pitch = Mathf.Lerp(_glideFromPitch, _glideToPitch, t);
        _distance = Mathf.Lerp(_glideFromDist, _glideToDist, t);
        if (_glideT >= 1f) _gliding = false;
    }

    /// <summary>Have the camera continuously follow a transform on the XZ plane (the player keeps
    /// Y/orbit/zoom control). Pass null to stop following. A manual WASD pan also stops it.</summary>
    public void SetFollowTarget(Transform target)
    {
        _followTarget = target;
        if (target != null) _focusBeyondBounds = true;
    }

    /// <summary>The transform the camera is currently following, or null.</summary>
    public Transform FollowTarget => _followTarget;

    /// <summary>The point the camera orbits and always keeps dead-centre on screen — i.e. what the
    /// player is looking AT, as opposed to where the camera happens to be. WallSeeThrough needs it to
    /// know which walls stand between the two.</summary>
    public Vector3 FocalPoint => _focalPoint;
}
