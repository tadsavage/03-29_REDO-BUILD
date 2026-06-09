using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// RTS/simulation orbital camera.
/// WASD/Arrows  — pan focal point on XZ plane (camera Y never changes)
/// Q / E        — raise / lower camera world Y (clamped by minHeight / maxHeight)
/// Shift        — 3x speed for all movement and zoom
/// Right Mouse  — hold + drag to orbit (yaw and pitch)
/// Scroll Wheel — zoom in / out (changes camera world Y via orbital distance)
/// LMB + RMB    — move forward on XZ only
///
/// Both scroll and Q/E clamp transform.position.y to [minHeight, maxHeight].
public class FreeLookCamera : MonoBehaviour
{
    [SerializeField] private BuildMenuUI buildMenuUI;

    [Header("Pan (WASD)")]
    [SerializeField] private float moveSpeed = 15f;

    [Header("Orbit (Right Mouse)")]
    [SerializeField] private float orbitSensitivity = 0.25f;
    [SerializeField] private float pitchSensitivity = 0.15f;

    [Header("Zoom (Scroll Wheel)")]
    [SerializeField] private float zoomSpeed = 4f;

    [Header("Pitch (Tilt) Limits")]
    [SerializeField] private float pitchMin      = 15f;
    [SerializeField] private float pitchMax      = 80f;
    [SerializeField] private float defaultPitch  = 45f;
    [SerializeField] private float defaultDistance = 15f;

    [Header("Height / Zoom Bounds  (camera world Y)")]
    [Tooltip("Minimum camera world Y — floor for both scroll-zoom and Q/E.")]
    [SerializeField] private float minHeight = 1.5f;
    [Tooltip("Maximum camera world Y — ceiling for both scroll-zoom and Q/E.")]
    [SerializeField] private float maxHeight = 25f;

    [Header("Focal Point XZ Bounds")]
    [SerializeField] private float xMin = -18f;
    [SerializeField] private float xMax =  18f;
    [SerializeField] private float zMin =  -8f;
    [SerializeField] private float zMax =  18f;

    private Vector3 _focalPoint;
    private float   _yaw;
    private float   _pitch;
    private float   _distance;
    private bool    _orbiting;
    private bool    _stateLoadedFromSave;

    private void Awake()
    {
        if (minHeight >= maxHeight) { minHeight = 1.5f; maxHeight = 25f; Debug.LogWarning("[FreeLookCamera] minHeight/maxHeight invalid — reset to defaults."); }
        if (xMin >= xMax)           { xMin = -18f; xMax = 18f;           Debug.LogWarning("[FreeLookCamera] xMin/xMax invalid — reset to defaults."); }
        if (zMin >= zMax)           { zMin =  -8f; zMax = 18f;           Debug.LogWarning("[FreeLookCamera] zMin/zMax invalid — reset to defaults."); }
        if (pitchMin >= pitchMax)   { pitchMin = 15f; pitchMax = 80f;    Debug.LogWarning("[FreeLookCamera] pitchMin/pitchMax invalid — reset to defaults."); }
    }

    private void Start()
    {
        if (_stateLoadedFromSave) { ApplyTransform(); return; }

        _yaw   = transform.eulerAngles.y;
        _pitch = Mathf.Clamp(transform.eulerAngles.x, pitchMin, pitchMax);

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

        _distance = Vector3.Distance(transform.position, _focalPoint);
        if (_distance < 0.5f) _distance = defaultDistance;
        if (_pitch    < 1f)   _pitch    = defaultPitch;

        EnforceYBounds();
        ApplyTransform();
    }

    private void Update()
    {
        if (UIInputGuard.IsTextFieldFocused) return;

        bool overUI = IsPointerOverUI();
        bool fast   = Keyboard.current[Key.LeftShift].isPressed
                   || Keyboard.current[Key.RightShift].isPressed;

        float speed = fast ? moveSpeed * 3f : moveSpeed;

        ProcessPan(speed, overUI);
        ProcessOrbit(overUI);
        ProcessZoom(overUI, fast ? zoomSpeed * 3f : zoomSpeed);
        ApplyTransform();
    }

    private void OnDisable()
    {
        _orbiting      = false;
        Cursor.visible = true;
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

        if (!overUI && Mouse.current.leftButton.isPressed && Mouse.current.rightButton.isPressed)
            delta += flatForward;

        if (delta.sqrMagnitude > 0.01f)
        {
            // XZ only — Y must never change from WASD or LMB+RMB
            var move = delta.normalized * (speed * Time.deltaTime);
            _focalPoint.x += move.x;
            _focalPoint.z += move.z;
        }

        // Q/E raise/lower camera world Y by shifting the focal point vertically.
        // Clamp focal Y so camera world Y (= focalY + sin(pitch)*distance) stays in [minHeight, maxHeight].
        if (Keyboard.current[Key.Q].isPressed)
            _focalPoint.y += speed * Time.deltaTime;
        if (Keyboard.current[Key.E].isPressed)
            _focalPoint.y -= speed * Time.deltaTime;

        float sinP = Mathf.Sin(_pitch * Mathf.Deg2Rad);
        _focalPoint.y = Mathf.Clamp(_focalPoint.y,
            minHeight - sinP * _distance,
            maxHeight - sinP * _distance);

        _focalPoint.x = Mathf.Clamp(_focalPoint.x, xMin, xMax);
        _focalPoint.z = Mathf.Clamp(_focalPoint.z, zMin, zMax);
    }

    private void ProcessOrbit(bool overUI)
    {
        if (Mouse.current.rightButton.wasPressedThisFrame && !overUI)
        {
            _orbiting      = true;
            Cursor.visible = false;
        }
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            _orbiting      = false;
            Cursor.visible = true;
        }

        // LMB+RMB together = forward movement; suppress orbit so both don't fire at once
        if (!_orbiting || Mouse.current.leftButton.isPressed) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        _yaw  += delta.x * orbitSensitivity;
        _pitch = Mathf.Clamp(_pitch - delta.y * pitchSensitivity, pitchMin, pitchMax);
    }

    private void ProcessZoom(bool overUI, float speed)
    {
        if (overUI) return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.001f) return;

        // Scroll up = zoom in = reduce distance (camera moves down/forward along orbital arc)
        _distance -= Mathf.Sign(scroll) * speed;

        // Clamp distance so camera world Y (= focalY + sin(pitch)*distance) stays in [minHeight, maxHeight]
        float sinP = Mathf.Sin(_pitch * Mathf.Deg2Rad);
        if (sinP > 0.001f)
        {
            float dMin = (minHeight - _focalPoint.y) / sinP;
            float dMax = (maxHeight - _focalPoint.y) / sinP;
            _distance  = Mathf.Clamp(_distance, Mathf.Max(0.1f, dMin), Mathf.Max(0.1f, dMax));
        }
        else
        {
            _distance = Mathf.Clamp(_distance, 0.1f, maxHeight);
        }
    }

    // Corrects _distance so camera world Y stays in [minHeight, maxHeight].
    // Catches any path (orbit pitch change, save load) that bypasses per-input clamping.
    private void EnforceYBounds()
    {
        float sinP = Mathf.Sin(_pitch * Mathf.Deg2Rad);
        if (sinP < 0.001f) return;

        float cameraY = _focalPoint.y + sinP * _distance;
        if (cameraY > maxHeight)
            _distance = (maxHeight - _focalPoint.y) / sinP;
        else if (cameraY < minHeight)
            _distance = Mathf.Max(0.1f, (minHeight - _focalPoint.y) / sinP);
    }

    private void ApplyTransform()
    {
        // Safety clamp every frame — catches Y drift from orbit (pitch change) or any other path
        EnforceYBounds();

        var rot = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.SetPositionAndRotation(
            _focalPoint + rot * (Vector3.back * _distance),
            rot);
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

        var fp = state.focusPoint;
        fp.x = Mathf.Clamp(fp.x, xMin, xMax);
        fp.z = Mathf.Clamp(fp.z, zMin, zMax);
        fp.y = Mathf.Min(fp.y, maxHeight); // EnforceYBounds in ApplyTransform handles the rest

        _focalPoint = fp;
        _pitch      = Mathf.Clamp(state.pitch, pitchMin, pitchMax);
        _yaw        = state.yaw;
        _distance   = state.distance > 0.1f ? state.distance : defaultDistance;

        ApplyTransform(); // includes EnforceYBounds
    }
}
