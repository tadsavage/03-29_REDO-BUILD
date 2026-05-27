using UnityEngine;
using UnityEngine.InputSystem;

public class FreeLookCamera : MonoBehaviour
{
    [Header("UI Protection Link")]
    [SerializeField] private BuildMenuUI buildMenuUI; // Drag & drop your BuildMenu object here in inspector

    [Header("Movement Settings")]
    public float movementSpeed = 15f;
    public float fastMovementSpeed = 35f;

    [Header("Rotation Settings")]
    public float freeLookSensitivity = 0.5f;
    public float minPitch = 5f;
    public float maxPitch = 85f;

    [Header("Zoom Settings")]
    public float zoomSensitivity = 5f;
    public float minDistance = 1f;
    public float maxDistance = 60f;

    [Header("Height Settings")]
    public float heightMin = 0.5f;
    public float heightMax = 40f;

    [Header("Boundary Settings")]
    public float X_Min = -100f;
    public float X_Max = 100f;
    public float Z_Min = -100f;
    public float Z_Max = 100f;

    [Header("Current State (Debug)")]
    [SerializeField] private Vector3 _focusPoint;
    [SerializeField] private float _distance = 20f;
    [SerializeField] private float _pitch = 45f;
    [SerializeField] private float _yaw = 0f;

    private bool _looking = false;

    private void Start()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter))
        {
            _focusPoint = ray.GetPoint(enter);
        }
        else
        {
            _focusPoint = transform.position + transform.forward * 10f;
            _focusPoint.y = 0;
        }

        _distance = Vector3.Distance(transform.position, _focusPoint);
        _distance = Mathf.Clamp(_distance, minDistance, maxDistance);
        _yaw = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;
        if (_pitch > 180) _pitch -= 360;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        _focusPoint.y = Mathf.Clamp(_focusPoint.y, heightMin, heightMax);
        UpdateCameraTransform();
    }

    private void Update()
    {
        if (UIInputGuard.IsTextFieldFocused) return;
        HandleInput();
        UpdateCameraTransform();
    }

    private void HandleInput()
    {
        var fastMode = Keyboard.current[Key.LeftShift].isPressed;
        var currentMoveSpeed = fastMode ? fastMovementSpeed : movementSpeed;

        // --- 1. Rotation (Right Mouse) ---
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            _looking = true;
            Cursor.visible = false;
        }
        else if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            _looking = false;
            Cursor.visible = true;
        }

        if (_looking)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            _yaw += mouseDelta.x * freeLookSensitivity;
            _pitch -= mouseDelta.y * freeLookSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        // --- 2. Zoom (Scroll Wheel) - PROTECTED FROM UI BLOWBACK ---
        // GUARD: If UI pointer tracker exists AND your mouse is hovering over the menu, block scroll processing
        bool isMouseOverMenu = buildMenuUI != null && buildMenuUI.IsPointerOverBuildMenu;

        if (!isMouseOverMenu)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.1f)
            {
                _distance -= (scroll / 120f) * zoomSensitivity;
                _distance = Mathf.Clamp(_distance, minDistance, maxDistance);
            }
        }

        // --- 3. Panning (WASD) ---
        Vector2 moveInput = Vector2.zero;
        if (Keyboard.current[Key.W].isPressed || Keyboard.current[Key.UpArrow].isPressed) moveInput.y += 1;
        if (Keyboard.current[Key.S].isPressed || Keyboard.current[Key.DownArrow].isPressed) moveInput.y -= 1;
        if (Keyboard.current[Key.A].isPressed || Keyboard.current[Key.LeftArrow].isPressed) moveInput.x -= 1;
        if (Keyboard.current[Key.D].isPressed || Keyboard.current[Key.RightArrow].isPressed) moveInput.x += 1;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;
            _focusPoint += moveDir * currentMoveSpeed * Time.deltaTime;
        }

        // --- 4. Vertical (Q: Up, E: Down) ---
        //if (Keyboard.current[Key.E].isPressed) _focusPoint.y -= currentMoveSpeed * Time.deltaTime;
        //if (Keyboard.current[Key.Q].isPressed) _focusPoint.y += currentMoveSpeed * Time.deltaTime;

        // --- 5. Final Clamping ---
        _focusPoint.x = Mathf.Clamp(_focusPoint.x, X_Min, X_Max);
        _focusPoint.y = Mathf.Clamp(_focusPoint.y, heightMin, heightMax);
        _focusPoint.z = Mathf.Clamp(_focusPoint.z, Z_Min, Z_Max);
    }

    private void UpdateCameraTransform()
    {
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
        transform.position = _focusPoint - (rotation * Vector3.forward * _distance);
        transform.rotation = rotation;
    }

    private void OnDisable() => Cursor.visible = true;

    public CameraSaveData GetState()
    {
        return new CameraSaveData { focusPoint = _focusPoint, distance = _distance, pitch = _pitch, yaw = _yaw };
    }

    public void SetState(CameraSaveData state)
    {
        if (state == null) return;
        _focusPoint = state.focusPoint;
        _distance = state.distance;
        _pitch = state.pitch;
        _yaw = state.yaw;
        UpdateCameraTransform();
    }
}
