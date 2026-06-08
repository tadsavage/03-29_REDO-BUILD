using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// City-builder orbital camera.
///
/// WASD / Arrows  — pan focal point across the flat ground plane
/// Q / E          — raise / lower focal point (whole rig moves up or down)
/// Shift          — speed multiplier for all movement and zoom
/// Middle Mouse   — hold + drag horizontal → orbit (yaw)
///                  hold + drag vertical   → tilt (pitch)
/// Scroll Wheel   — zoom in / out (changes distance to focal point)
///
/// The camera always looks at the focal point; it never rolls or tilts freely.
/// </summary>
public class FreeLookCamera : MonoBehaviour
{
    [SerializeField] private BuildMenuUI buildMenuUI;

    [Header("Pan (WASD)")]
    [Tooltip("Focal-point pan speed in world units per second.")]
    [SerializeField] private float moveSpeed     = 15f;
    [Tooltip("Pan speed while Shift is held.")]
    [SerializeField] private float fastMoveSpeed = 35f;

    [Header("Orbit (Middle Mouse)")]
    [Tooltip("Degrees of yaw added per pixel of horizontal mouse movement while MMB is held.")]
    [SerializeField] private float orbitSensitivity = 0.25f;
    [Tooltip("Degrees of pitch added per pixel of vertical mouse movement while MMB is held. Set 0 to disable.")]
    [SerializeField] private float pitchSensitivity = 0.15f;

    [Header("Zoom (Scroll Wheel)")]
    [Tooltip("Distance change per scroll notch at normal speed.")]
    [SerializeField] private float zoomSpeed     = 4f;
    [Tooltip("Distance change per scroll notch while Shift is held.")]
    [SerializeField] private float fastZoomSpeed = 10f;
    [Tooltip("Closest the camera can get to the focal point (metres).")]
    [SerializeField] private float zoomMin       = 4f;
    [Tooltip("Furthest the camera can be from the focal point (metres).")]
    [SerializeField] private float zoomMax       = 40f;

    [Header("Pitch (Tilt) Limits")]
    [Tooltip("Minimum vertical angle — 0° is horizon, 90° is straight down.")]
    [SerializeField] private float pitchMin     = 15f;
    [Tooltip("Maximum vertical angle.")]
    [SerializeField] private float pitchMax     = 80f;
    [Tooltip("Starting pitch angle on scene load (used when no save data is present).")]
    [SerializeField] private float defaultPitch = 45f;
    [Tooltip("Starting distance from focal point on scene load.")]
    [SerializeField] private float defaultDistance = 15f;

    [Header("Focal Point Bounds")]
    [Tooltip("Horizontal X limits the focal point cannot leave.")]
    [SerializeField] private float xMin = -18f;
    [SerializeField] private float xMax =  18f;
    [Tooltip("Vertical Y limits — Q/E are clamped here.")]
    [SerializeField] private float yMin =  0f;
    [SerializeField] private float yMax =  8f;
    [Tooltip("Depth Z limits the focal point cannot leave.")]
    [SerializeField] private float zMin = -8f;
    [SerializeField] private float zMax =  18f;

    // ── Orbital state ─────────────────────────────────────────────────────────
    private Vector3 _focalPoint;
    private float   _yaw;
    private float   _pitch;
    private float   _distance;

    private bool _orbiting;   // true while middle mouse is held

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private bool _stateLoadedFromSave; // prevents Start() from overwriting SetState()

    private void Awake()
    {
        // Auto-heal bounds that serialized as 0 when the script was rewritten
        // (Unity can't migrate renamed SerializeField fields in binary scenes).
        // Awake runs before any Start(), so SetState() called from GameContext (-100) sees correct bounds.
        if (xMin >= xMax)     { xMin = -18f; xMax = 18f;  Debug.LogWarning("[FreeLookCamera] xMin/xMax were 0 — reset to defaults. Re-set them in the Inspector."); }
        if (yMin >= yMax)     { yMin =   0f; yMax =  8f;  Debug.LogWarning("[FreeLookCamera] yMin/yMax were 0 — reset to defaults. Re-set them in the Inspector."); }
        if (zMin >= zMax)     { zMin =  -8f; zMax = 18f;  Debug.LogWarning("[FreeLookCamera] zMin/zMax were 0 — reset to defaults. Re-set them in the Inspector."); }
        if (zoomMin >= zoomMax){ zoomMin = 4f; zoomMax = 40f; Debug.LogWarning("[FreeLookCamera] zoomMin/zoomMax were 0 — reset to defaults. Re-set them in the Inspector."); }
        if (pitchMin >= pitchMax){ pitchMin = 15f; pitchMax = 80f; Debug.LogWarning("[FreeLookCamera] pitchMin/pitchMax were 0 — reset to defaults. Re-set them in the Inspector."); }
    }

    private void Start()
    {
        // If SetState() was already called (e.g. from GameContext.Start() which runs at -100),
        // skip the bootstrap so we don't overwrite the loaded save state.
        if (_stateLoadedFromSave) { ApplyTransform(); return; }

        // Bootstrap from the camera's current scene transform so the designer's
        // editor placement is respected on first play.
        _yaw   = transform.eulerAngles.y;
        _pitch = Mathf.Clamp(transform.eulerAngles.x, pitchMin, pitchMax);

        // Project the camera ray onto Y=0 to find the initial focal point.
        float dy = transform.forward.y;
        if (Mathf.Abs(dy) > 0.001f)
        {
            float t = -transform.position.y / dy;
            _focalPoint = transform.position + transform.forward * Mathf.Max(t, 0f);
        }
        else
        {
            _focalPoint = transform.position + transform.forward * defaultDistance;
        }
        _focalPoint.y = 0f;

        // Derive starting distance from the current camera position.
        _distance = Mathf.Clamp(
            Vector3.Distance(transform.position, _focalPoint),
            zoomMin, zoomMax);

        // Fall back to authored defaults if bootstrap produced degenerate values.
        if (_distance < 0.5f) _distance = defaultDistance;
        if (_pitch    < 1f)   _pitch    = defaultPitch;

        ApplyTransform();
    }

    private void Update()
    {
        if (UIInputGuard.IsTextFieldFocused) return;

        bool overUI = IsPointerOverUI();
        bool fast   = Keyboard.current[Key.LeftShift].isPressed
                   || Keyboard.current[Key.RightShift].isPressed;

        ProcessPan(fast ? fastMoveSpeed : moveSpeed);
        ProcessOrbit(overUI);
        ProcessZoom(overUI, fast ? fastZoomSpeed : zoomSpeed);
        ApplyTransform();
    }

    private void OnDisable()
    {
        _orbiting      = false;
        Cursor.visible = true;
    }

    // ── Pan — WASD / arrow keys ───────────────────────────────────────────────

    private void ProcessPan(float speed)
    {
        // Project the camera's facing direction flat onto the XZ ground plane so
        // W always moves "into" the scene regardless of pitch angle.
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
            _focalPoint += delta.normalized * (speed * Time.deltaTime);

        // Q = raise, E = lower
        if (Keyboard.current[Key.Q].isPressed)
            _focalPoint.y += speed * Time.deltaTime;
        if (Keyboard.current[Key.E].isPressed)
            _focalPoint.y -= speed * Time.deltaTime;

        // Clamp focal point inside world bounds
        _focalPoint.x = Mathf.Clamp(_focalPoint.x, xMin, xMax);
        _focalPoint.y = Mathf.Clamp(_focalPoint.y, yMin, yMax);
        _focalPoint.z = Mathf.Clamp(_focalPoint.z, zMin, zMax);
    }

    // ── Orbit — middle mouse drag ─────────────────────────────────────────────

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

        // Horizontal drag orbits around the focal point (yaw).
        _yaw += delta.x * orbitSensitivity;

        // Vertical drag tilts the camera angle (pitch). Inverted so dragging up
        // raises the camera, matching SimCity / Cities: Skylines convention.
        _pitch = Mathf.Clamp(_pitch - delta.y * pitchSensitivity, pitchMin, pitchMax);
    }

    // ── Zoom — scroll wheel ───────────────────────────────────────────────────

    private void ProcessZoom(bool overUI, float speed)
    {
        if (overUI) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.001f) return;

        // Scrolling up (positive) moves closer; scrolling down moves further away.
        _distance -= Mathf.Sign(scroll) * speed;
        _distance  = Mathf.Clamp(_distance, zoomMin, zoomMax);
    }

    // ── Apply orbital transform each frame ───────────────────────────────────

    private void ApplyTransform()
    {
        // Position the camera behind and above the focal point according to current
        // yaw and pitch, then look directly at the focal point.
        var rot = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.SetPositionAndRotation(
            _focalPoint + rot * (Vector3.back * _distance),
            rot);
    }

    // ── UI blocking ───────────────────────────────────────────────────────────

    private bool IsPointerOverUI()
        => (buildMenuUI != null && buildMenuUI.IsPointerOverBuildMenu)
        || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        || UIInputGuard.IsPointerOverUIToolkit();

    // ── Save / Load ───────────────────────────────────────────────────────────

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

        // Old saves stored the camera's world position as focusPoint (not a ground point).
        // If Y is clearly above ground level, project down to Y=0.
        var fp = state.focusPoint;
        if (fp.y > 1.5f)
        {
            fp.y = 0f;
            // Re-center X/Z inside the allowed bounds as a safe fallback
            fp.x = Mathf.Clamp(fp.x, xMin, xMax);
            fp.z = Mathf.Clamp(fp.z, zMin, zMax);
        }
        _focalPoint = fp;
        _pitch      = Mathf.Clamp(state.pitch, pitchMin, pitchMax);
        _yaw        = state.yaw;

        // Old saves stored distance = 0 (the field wasn't used). Fall back to
        // the authored default so loading those saves doesn't collapse the view.
        _distance = state.distance > 0.1f
            ? Mathf.Clamp(state.distance, zoomMin, zoomMax)
            : defaultDistance;

        ApplyTransform();
    }
}
