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

    [Header("Stats (0-100)")]
    [Range(0f, 100f)] public float fatigue = 25f;
    [Range(0f, 100f)] public float safety = 60f;
    [Range(0f, 100f)] public float morale = 90f;
    [Range(0f, 100f)] public float skill = 40f;

    [Header("Skill Level")]
    public int skillLevel = 3;

    [SerializeField] private bool _isConfigured = false;

    private void OnEnable()
    {
        if (!_isConfigured)
        {
            RandomizeId();
            RandomizeStats();
        }
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
}
