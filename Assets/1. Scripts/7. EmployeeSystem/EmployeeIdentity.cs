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

    [Header("Avatar Display")]
    [SerializeField] private SpriteRenderer _avatarRenderer;

    private bool _recordEnsured;

    public EmployeeRecord Record => _record;

    private void Awake()
    {
        EnsureRecord();
        EmployeeRegistry.Instance?.Register(this);
    }

    private void OnDestroy()
    {
        EmployeeRegistry.Instance?.Unregister(this);
    }

    public EmployeeRecord GetOrCreateRecord()
    {
        EnsureRecord();
        return _record;
    }

    public void EnsureRecord()
    {
        if (_recordEnsured && !IsRecordEmpty(_record))
            return;

        // NOTE: Unity always deserializes a [SerializeField] of a [Serializable] class
        // as a NON-null instance (with empty fields) — so checking `_record == null` is
        // not enough. A freshly-serialized prefab has an empty record, which must still
        // trigger generation, otherwise the ID badge shows a blank name.
        if (IsRecordEmpty(_record))
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

        ApplyAvatarToDisplay();
        _recordEnsured = true;
    }

    /// <summary>True if the record is missing or has no real identity yet (blank name/guid).</summary>
    private static bool IsRecordEmpty(EmployeeRecord record)
    {
        return record == null
            || string.IsNullOrEmpty(record.employeeName)
            || string.IsNullOrEmpty(record.employeeGuid);
    }

    /// <summary>Load the avatar texture from Resources and apply to SpriteRenderer if available.</summary>
    private void ApplyAvatarToDisplay()
    {
        if (_record == null || string.IsNullOrEmpty(_record.avatarResourceKey))
            return;

        if (_avatarRenderer == null)
            _avatarRenderer = GetComponent<SpriteRenderer>();

        if (_avatarRenderer == null)
            return;

        // Load the texture from Resources/EmployeeAssets/Male/ or Female/
        var texture = Resources.Load<Texture2D>($"EmployeeAssets/{_record.avatarResourceKey}");
        if (texture != null)
        {
            // Convert texture to sprite
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);
            _avatarRenderer.sprite = sprite;
        }
        else
        {
            Debug.LogWarning($"[EmployeeIdentity] Avatar not found: {_record.avatarResourceKey}");
        }
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
