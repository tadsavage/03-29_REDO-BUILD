// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeRegistry.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central runtime database of all active employees.
/// Lives as a persistent singleton — access via EmployeeRegistry.Instance.
/// EmployeeIdentity registers/unregisters itself automatically on Awake/OnDestroy.
/// </summary>
public class EmployeeRegistry : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static EmployeeRegistry Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Events ───────────────────────────────────────────────────────────────
    /// <summary>Fired when an employee is added to the registry.</summary>
    public event Action<EmployeeIdentity> OnEmployeeAdded;

    /// <summary>Fired when an employee is removed from the registry.</summary>
    public event Action<EmployeeIdentity> OnEmployeeRemoved;

    // ─── Internal storage ─────────────────────────────────────────────────────
    // All registered employees keyed by GUID for O(1) lookup
    private readonly Dictionary<string, EmployeeIdentity> _byGuid
        = new Dictionary<string, EmployeeIdentity>();

    // ─── Avatar tracking ──────────────────────────────────────────────────────
    /// <summary>Tracks which avatars have been assigned (persists across saves).</summary>
    public AvatarRegistry AvatarRegistry { get; private set; } = new AvatarRegistry();

    // ─── Registration ─────────────────────────────────────────────────────────
    /// <summary>Called by EmployeeIdentity.Awake to register itself.</summary>
    public void Register(EmployeeIdentity identity)
    {
        if (identity == null) return;

        string guid = identity.Record?.employeeGuid;
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning($"[EmployeeRegistry] {identity.name} has no GUID — skipping registration.");
            return;
        }

        if (_byGuid.ContainsKey(guid))
        {
            Debug.LogWarning($"[EmployeeRegistry] Duplicate GUID {guid} for {identity.name} — skipping.");
            return;
        }

        _byGuid[guid] = identity;
        OnEmployeeAdded?.Invoke(identity);
    }

    /// <summary>Called by EmployeeIdentity.OnDestroy to unregister itself.</summary>
    public void Unregister(EmployeeIdentity identity)
    {
        if (identity == null) return;

        string guid = identity.Record?.employeeGuid;
        if (string.IsNullOrEmpty(guid)) return;

        if (_byGuid.Remove(guid))
            OnEmployeeRemoved?.Invoke(identity);
    }

    // ─── Lookups ──────────────────────────────────────────────────────────────
    /// <summary>Find an employee by their GUID. Returns null if not found.</summary>
    public EmployeeIdentity GetByGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        _byGuid.TryGetValue(guid, out var identity);
        return identity;
    }

    /// <summary>Find the first employee whose display name matches (case-insensitive).</summary>
    public EmployeeIdentity GetByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var kvp in _byGuid)
        {
            if (string.Equals(kvp.Value.Record?.employeeName, name, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return null;
    }

    /// <summary>Find the first employee whose ID prefix matches (e.g. "WHSE", "SEC").</summary>
    public EmployeeIdentity GetByPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return null;
        foreach (var kvp in _byGuid)
        {
            if (string.Equals(kvp.Value.Record?.employeeIdPrefix, prefix, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return null;
    }

    /// <summary>All employees with a given ID prefix (e.g. all warehouse workers).</summary>
    public List<EmployeeIdentity> GetAllByPrefix(string prefix)
    {
        var result = new List<EmployeeIdentity>();
        if (string.IsNullOrEmpty(prefix)) return result;
        foreach (var kvp in _byGuid)
        {
            if (string.Equals(kvp.Value.Record?.employeeIdPrefix, prefix, StringComparison.OrdinalIgnoreCase))
                result.Add(kvp.Value);
        }
        return result;
    }

    /// <summary>All currently registered employees.</summary>
    public IEnumerable<EmployeeIdentity> All => _byGuid.Values;

    /// <summary>Total number of registered employees.</summary>
    public int Count => _byGuid.Count;

    // ─── Debug ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("Log All Employees")]
    private void EditorLogAll()
    {
        Debug.Log($"[EmployeeRegistry] {Count} employee(s) registered:");
        foreach (var kvp in _byGuid)
        {
            var r = kvp.Value.Record;
            Debug.Log($"  {r?.employeeId} — {r?.employeeName} ({kvp.Key})");
        }
    }
#endif
}
