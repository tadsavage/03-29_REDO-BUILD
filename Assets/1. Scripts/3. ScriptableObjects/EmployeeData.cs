using UnityEngine;

[CreateAssetMenu(fileName = "EmployeeData", menuName = "ScriptableObjects/EmployeeData")]
public class EmployeeData : ScriptableObject
{
    [Header("Identity")]
    public string employeeName = "Jordan Barnes";
    public string employeeIdPrefix = "WHSE";
    [HideInInspector] public string employeeId;

    [Header("Portrait")]
    public Sprite avatarSprite;
    public Sprite jobIcon;

    [Header("Stats (0-100)")]
    [Range(0f, 100f)] public float fatigue = 25f;
    [Range(0f, 100f)] public float safety = 60f;
    [Range(0f, 100f)] public float morale = 90f;
    [Range(0f, 100f)] public float skill = 40f;

    [Header("Skill Level")]
    public int skillLevel = 3;

    [SerializeField] private bool _isConfigured = false;

    public bool IsConfigured => _isConfigured;

    // OnEnable must NOT auto-randomize â€” that would overwrite saved asset data on reload.
    // Randomization only happens via EnsureConfigured() for legacy unconfigured assets.
    private void OnEnable()
    {
        // Intentionally empty. Randomization is opt-in via EnsureConfigured().
    }

    public void EnsureConfigured()
    {
        if (_isConfigured) return;
        RandomizeId();
        RandomizeStats();
        _isConfigured = true;
    }

    public void RandomizeId()
    {
        employeeId = $"{employeeIdPrefix} {Random.Range(1, 101):D3}";
    }

    public void RandomizeStats()
    {
        fatigue = Random.Range(10f, 95f);
        safety = Random.Range(20f, 95f);
        morale = Random.Range(30f, 100f);
        skill = Random.Range(15f, 90f);
        skillLevel = Random.Range(1, 6);
    }

    public void SetConfigured()
    {
        _isConfigured = true;
    }

    public void ApplyRecord(EmployeeRecord record)
    {
        if (record == null) return;
        employeeName = record.employeeName;
        employeeIdPrefix = record.employeeIdPrefix;
        employeeId = record.employeeId;
        fatigue = record.fatigue;
        safety = record.safety;
        morale = record.morale;
        skill = record.skill;
        skillLevel = record.skillLevel;

        // Load portrait sprite from Resources using the avatar key on the record
        if (!string.IsNullOrEmpty(record.avatarResourceKey))
        {
            string fullKey = $"EmployeeAssets/{record.avatarResourceKey}";
            // Try as Sprite first, then Texture2D fallback
            var sprite = Resources.Load<Sprite>(fullKey);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(fullKey);
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new UnityEngine.Vector2(0.5f, 0.5f));
            }
            avatarSprite = sprite;

            if (avatarSprite == null)
                Debug.LogWarning($"[EmployeeData] Portrait not found at Resources/{fullKey} for {employeeName}");
        }

        _isConfigured = true;
    }

    public EmployeeRecord ExportToRecord()
    {
        // Ensure the data is valid before exporting
        if (string.IsNullOrEmpty(employeeId))
            EnsureConfigured();

        EmployeeRecord record = new EmployeeRecord
        {
            employeeName = employeeName,
            employeeIdPrefix = employeeIdPrefix,
            employeeId = employeeId,
            gender = EmployeeGender.Neutral,
            fatigue = fatigue,
            safety = safety,
            morale = morale,
            skill = skill,
            skillLevel = skillLevel
        };
        return record;
    }
}
