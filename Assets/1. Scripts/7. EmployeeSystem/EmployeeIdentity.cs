using UnityEngine;

public class EmployeeIdentity : MonoBehaviour
{
    [Header("Existing EmployeeData (optional)")]
    [SerializeField] private EmployeeData _employeeData;

    [Header("Generated Record")]
    [SerializeField] private EmployeeRecord _record;

    [Header("Generator Settings")]
    [SerializeField] private string _idPrefix = "WHSE";
    [SerializeField] private EmployeeGender _gender = EmployeeGender.Random;

    private bool _recordEnsured;

    public EmployeeRecord Record => _record;

    private void Awake()
    {
        EnsureRecord();
    }

    public EmployeeRecord GetOrCreateRecord()
    {
        EnsureRecord();
        return _record;
    }

    public void EnsureRecord()
    {
        if (_recordEnsured && _record != null)
            return;

        if (_record == null)
        {
            if (_employeeData != null)
            {
                _record = EmployeeGenerator.GenerateFromTemplate(_employeeData);
            }
            else
            {
                _record = EmployeeGenerator.Generate(_gender, _idPrefix);
            }
        }

        _recordEnsured = true;
    }

    public void ApplyRecord(EmployeeRecord record)
    {
        if (record == null) return;
        _record = record;
        _recordEnsured = true;
    }

    public EmployeeData GetDisplayData()
    {
        if (_employeeData == null)
        {
            _employeeData = ScriptableObject.CreateInstance<EmployeeData>();
        }

        if (_record != null)
        {
            _employeeData.ApplyRecord(_record);
            _employeeData.SetConfigured();
        }

        return _employeeData;
    }

    public void SetEmployeeData(EmployeeData data)
    {
        _employeeData = data;
        _recordEnsured = false;
        EnsureRecord();
    }

#if UNITY_EDITOR
    [ContextMenu("Generate New Record")]
    private void EditorGenerate()
    {
        _record = EmployeeGenerator.Generate(_gender, _idPrefix);
        _recordEnsured = true;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
