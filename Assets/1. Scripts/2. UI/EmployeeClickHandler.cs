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

    private EmployeeIdentity _identity;

    private void Start()
    {
        _mainCamera = Camera.main;

        _identity = GetComponent<EmployeeIdentity>();

        if (_identity != null)
        {
            // Use stable identity record; fall back to EmployeeData if identity has a template
            _identity.EnsureRecord();
        }
        else if (_employeeData == null)
        {
            Debug.LogWarning($"[EmployeeClickHandler] No EmployeeData or EmployeeIdentity on {gameObject.name}. Creating dummy data.");
            _employeeData = ScriptableObject.CreateInstance<EmployeeData>();
            _employeeData.employeeName = "Jordan Barnes";
            _employeeData.EnsureConfigured();
        }
        else
        {
            // EmployeeData exists but no identity — ensure it's configured
            _employeeData.EnsureConfigured();
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

        if (_employeeUI == null)
        {
            Debug.LogError("[EmployeeClickHandler] No EmployeeInfoUI found in scene.");
            return;
        }

        // Priority: identity record > EmployeeData > nothing
        if (_identity != null && _identity.Record != null)
        {
            _employeeUI.Show(_identity.Record);
        }
        else if (_employeeData != null)
        {
            _employeeData.EnsureConfigured();
            _employeeUI.Show(_employeeData);
        }
        else
        {
            Debug.LogWarning($"[EmployeeClickHandler] No EmployeeData or EmployeeIdentity record on {gameObject.name}.");
        }
    }
}
