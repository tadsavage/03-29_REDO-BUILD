using UnityEngine;

/// <summary>
/// Bridges EmployeeLifecycleService record generation with GameObject instantiation.
///
/// On Start:
/// 1. Subscribes to EmployeeLifecycleService.OnHired so programmatic hires spawn GameObjects.
/// 2. Optionally auto-spawns employees for testing (_autoSpawnOnStart).
///
/// Place on the same GameObject that holds EmployeeLifecycleService / EmployeeRegistry
/// or any persistent manager GameObject. Assign worker prefabs in the Inspector.
/// </summary>
public class EmployeeSpawner : MonoBehaviour
{
    // ─── Prefab references ────────────────────────────────────────────────────
    [Header("Prefabs")]
    [SerializeField] private GameObject _workerMalePrefab;
    [SerializeField] private GameObject _workerFemalePrefab;

    // ─── Auto-spawn (testing) ─────────────────────────────────────────────────
    [Header("Auto-Spawn (Testing)")]
    [SerializeField] private bool _autoSpawnOnStart = false;
    [SerializeField] private int _autoSpawnCount = 5;

    [Header("Spawn Point")]
    [SerializeField] private Transform _spawnPoint;

    private bool _isAutoSpawning;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────
    private void Start()
    {
        if (EmployeeLifecycleService.Instance != null)
            EmployeeLifecycleService.Instance.OnHired += OnEmployeeHired;

        if (_autoSpawnOnStart)
        {
            _isAutoSpawning = true;

            for (int i = 0; i < _autoSpawnCount; i++)
            {
                // Hire via the lifecycle service to generate a record and fire events.
                // If the service is missing, fall back to direct generation.
                var record = EmployeeLifecycleService.Instance != null
                    ? EmployeeLifecycleService.Instance.Hire()
                    : EmployeeGenerator.Generate(EmployeeGender.Random, "WHSE");

                SpawnEmployee(record);
            }

            _isAutoSpawning = false;
        }
    }

    private void OnDestroy()
    {
        if (EmployeeLifecycleService.Instance != null)
            EmployeeLifecycleService.Instance.OnHired -= OnEmployeeHired;
    }

    // ─── Event handler ────────────────────────────────────────────────────────
    /// <summary>
    /// Respond to programmatic Hire() calls from UI or other systems.
    /// Skipped during auto-spawn to avoid double-instantiation.
    /// </summary>
    private void OnEmployeeHired(EmployeeRecord record)
    {
        if (_isAutoSpawning) return; // already handled inline in Start
        SpawnEmployee(record);
    }

    // ─── Spawning ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Instantiate the appropriate worker prefab and assign the record.
    ///
    /// Handles the registration dance: Instantiate triggers EmployeeIdentity.Awake
    /// which auto-generates a record and registers it — we unregister that,
    /// apply the hired record, then re-register.
    /// </summary>
    public EmployeeIdentity SpawnEmployee(EmployeeRecord record)
    {
        if (record == null)
        {
            Debug.LogWarning("[EmployeeSpawner] Cannot spawn — record is null.");
            return null;
        }

        // ── Pick prefab ───────────────────────────────────────────────────────
        GameObject prefab = PickPrefab(record);
        if (prefab == null)
        {
            Debug.LogWarning("[EmployeeSpawner] No worker prefab assigned!");
            return null;
        }

        // ── Instantiate ───────────────────────────────────────────────────────
        Vector3 pos = _spawnPoint != null ? _spawnPoint.position : transform.position;
        Quaternion rot = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;

        var instance = Instantiate(prefab, pos, rot);
        instance.name = $"Employee_{record.employeeName}";

        var identity = instance.GetComponent<EmployeeIdentity>();
        if (identity == null)
        {
            Debug.LogWarning($"[EmployeeSpawner] Prefab '{prefab.name}' has no EmployeeIdentity component!");
            return null;
        }

        // ── Swap the record ───────────────────────────────────────────────────
        // Instantiate already ran Awake → EnsureRecord → Register (auto-gen GUID).
        // Unregister the auto-generated entry, apply the hired record, re-register.
        if (EmployeeRegistry.Instance != null)
            EmployeeRegistry.Instance.Unregister(identity);

        identity.ApplyRecord(record);

        if (EmployeeRegistry.Instance != null)
            EmployeeRegistry.Instance.Register(identity);

        Debug.Log($"[EmployeeSpawner] Spawned {record.employeeName} ({record.employeeId}) at {pos}");

        return identity;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private GameObject PickPrefab(EmployeeRecord record)
    {
        return record.gender == EmployeeGender.Female
            ? _workerFemalePrefab ?? _workerMalePrefab
            : _workerMalePrefab ?? _workerFemalePrefab;
    }
}
