using UnityEngine;

/// <summary>
/// Manages camera focus on a pick slot, allowing the player to inspect it and surrounding items
/// for cohesive slotting decisions. Restricts camera to Y-axis rotation only, displays a toast,
/// and returns to original position on spacebar or any button click.
/// </summary>
public class PickSlotCameraFocus : MonoBehaviour
{
    private static PickSlotCameraFocus _instance;

    private Camera _camera;
    private Vector3 _originalCameraPos;
    private Quaternion _originalCameraRot;
    private bool _isActive;
    private float _originalFov;
    private const float TargetFov = 25f; // Zoomed in / wide view
    private const float FocusY = 1f; // Camera height at pick slot
    private const float FocusDistance = 2f; // Cells in front of pick slot
    private const float CELL_SIZE = 1.33f;

    private void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        _camera = Camera.main;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (!_isActive) return;

        // Only allow Y-axis rotation
        if (Input.GetMouseButton(1)) // Right mouse drag for rotation
        {
            float deltaX = Input.GetAxis("Mouse X");
            // Unscaled: this is direct mouse-drag camera control, which must stay responsive at any
            // game speed and keep working while the game is paused.
            _camera.transform.RotateAround(_camera.transform.position, Vector3.up, deltaX * 100f * Time.unscaledDeltaTime);
        }

        // Spacebar or any button click returns to normal view
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape))
        {
            ExitFocusView();
        }
    }

    public static void EnterFocusView(Vector3 pickSlotWorldPos, Vector3 aisleForward)
    {
        if (_instance == null)
        {
            var go = new GameObject("[PickSlotCameraFocus]") { hideFlags = HideFlags.HideAndDontSave };
            _instance = go.AddComponent<PickSlotCameraFocus>();
        }

        _instance.DoEnterFocusView(pickSlotWorldPos, aisleForward);
    }

    public static void ExitFocusView()
    {
        if (_instance != null && _instance._isActive)
            _instance.DoExitFocusView();
    }

    private void DoEnterFocusView(Vector3 pickSlotWorldPos, Vector3 aisleForward)
    {
        if (_isActive) return; // Already in focus

        // Save current camera state
        _originalCameraPos = _camera.transform.position;
        _originalCameraRot = _camera.transform.rotation;
        _originalFov = _camera.fieldOfView;

        // Position camera 2 cells in front of the pick slot, facing it
        Vector3 cameraPos = pickSlotWorldPos - aisleForward * (FocusDistance * CELL_SIZE);
        cameraPos.y = FocusY;

        _camera.transform.position = cameraPos;
        _camera.transform.LookAt(pickSlotWorldPos + Vector3.up * 0.5f, Vector3.up);
        _camera.fieldOfView = TargetFov;

        _isActive = true;

        // Show toast
        UIToast.Show("Press SPACE to return to Warehouse view");
    }

    private void DoExitFocusView()
    {
        _camera.transform.position = _originalCameraPos;
        _camera.transform.rotation = _originalCameraRot;
        _camera.fieldOfView = _originalFov;

        _isActive = false;
    }
}
