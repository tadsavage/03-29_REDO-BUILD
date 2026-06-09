using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class EmployeeClickHandler : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EmployeeData _employeeData;

    [Header("Optional: Override UI reference")]
    [SerializeField] private EmployeeInfoUI _uiOverride;

    private EmployeeInfoUI _employeeUI;
    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
        if (_employeeData == null)
        {
            Debug.LogWarning($"[EmployeeClickHandler] No EmployeeData on {gameObject.name}. Creating dummy data.");
            _employeeData = ScriptableObject.CreateInstance<EmployeeData>();
            _employeeData.employeeName = "Jordan Barnes";
            _employeeData.RandomizeId();
            _employeeData.RandomizeStats();
        }
    }

    private void Update()
    {
        if (!Mouse.current.rightButton.wasPressedThisFrame) return;

        Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject == gameObject)
            {
                OnRightClicked();
            }
        }
    }

    private void OnRightClicked()
    {
        if (_employeeUI == null)
        {
            _employeeUI = _uiOverride ?? FindAnyObjectByType<EmployeeInfoUI>();
        }

        if (_employeeUI != null)
        {
            _employeeUI.Show(_employeeData);
        }
        else
        {
            Debug.LogError("[EmployeeClickHandler] No EmployeeInfoUI found in scene.");
        }
    }
}
