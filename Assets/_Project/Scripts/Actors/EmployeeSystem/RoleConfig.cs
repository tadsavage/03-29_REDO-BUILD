// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/RoleConfig.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-role economic and morale configuration, edited by designers in the inspector.
/// One <c>RoleConfig</c> asset is created and referenced wherever hiring/promotion
/// costs and morale modifiers are needed.
/// </summary>
[CreateAssetMenu(fileName = "RoleConfig", menuName = "ScriptableObjects/RoleConfig")]
public class RoleConfig : ScriptableObject
{
    [System.Serializable]
    public struct RoleConfigEntry
    {
        public EmployeeRole role;

        [Tooltip("One-time money cost to hire an employee into this role.")]
        [Min(0)] public int hiringCost;

        [Tooltip("One-time money cost to promote an existing employee to this role.")]
        [Min(0)] public int promotionCost;

        [Tooltip("Flat morale modifier applied to the employee while they hold this role. " +
                 "Positive = happier, negative = unhappier.")]
        [Range(-100, 100)] public int moraleModifier;
    }

    [SerializeField] private RoleConfigEntry[] _entries;

    private Dictionary<EmployeeRole, RoleConfigEntry> _lookup;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the config entry for <paramref name="role"/>.
    /// Falls back to a zeroed default if the role has no configured entry.
    /// </summary>
    public RoleConfigEntry GetConfig(EmployeeRole role)
    {
        if (_lookup == null) BuildLookup();
        return _lookup.TryGetValue(role, out var entry) ? entry : new RoleConfigEntry { role = role };
    }

    /// <summary>Hiring cost for <paramref name="role"/>, 0 if unconfigured.</summary>
    public int GetHiringCost(EmployeeRole role) => GetConfig(role).hiringCost;

    /// <summary>Promotion cost for <paramref name="role"/>, 0 if unconfigured.</summary>
    public int GetPromotionCost(EmployeeRole role) => GetConfig(role).promotionCost;

    /// <summary>Flat morale modifier for <paramref name="role"/>, 0 if unconfigured.</summary>
    public int GetMoraleModifier(EmployeeRole role) => GetConfig(role).moraleModifier;

    // ── Internal ───────────────────────────────────────────────────────────────

    private void BuildLookup()
    {
        _lookup = new Dictionary<EmployeeRole, RoleConfigEntry>();
        if (_entries == null) return;
        foreach (var entry in _entries)
        {
            if (!_lookup.ContainsKey(entry.role))
                _lookup[entry.role] = entry;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Invalidate cached lookup so inspector changes are picked up in play mode.
        _lookup = null;
    }
#endif
}
