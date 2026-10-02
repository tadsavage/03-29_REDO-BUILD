using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Per-(role, gender, slot, variant) selection-weight overrides for the Avatar Object Database
/// system. Edited through the AOD's weights UI (Phase 4) — this asset is just the data it reads
/// and writes.
///
/// Deliberately NOT folded into AvatarPartLibrary.Part.defaultWeight: a weight here can vary by
/// role (e.g. white sneakers weighted higher for Order Selectors than for Receivers), and a single
/// float on Part can't express that. Rules are looked up most-specific-first; anything with no
/// matching rule falls back to the part's own defaultWeight, so the system produces a working
/// (if uniform) random pick before a single rule has ever been entered.
/// </summary>
[CreateAssetMenu(fileName = "AvatarWeightConfig", menuName = "Modular Avatar/Weight Config")]
public class AvatarWeightConfig : ScriptableObject
{
    [Serializable]
    public class WeightRule
    {
        // Role/gender are nullable-by-convention: "" (empty) means "applies to every role/gender".
        // EmployeeRole itself has no null, so role matching is gated by useRole below rather than
        // a sentinel enum value — OrderSelector is a real role and can't double as "any".
        public bool         useRole;
        public EmployeeRole role;
        public string       gender;   // "male" / "female" / "neutral" / "" for any
        public string       slot;
        public string       variant;
        public float        weight = 1f;

        // r is nullable: null means "no role context" (e.g. the editor preview tool). A rule that
        // names a role (useRole) can only match when a concrete role was actually supplied.
        public bool Matches(EmployeeRole? r, string g, string s, string v) =>
            (!useRole || (r.HasValue && role == r.Value)) &&
            (string.IsNullOrEmpty(gender) || string.Equals(gender, g, StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(slot, s, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(variant, v, StringComparison.OrdinalIgnoreCase);

        /// <summary>Rules that name a role are more specific than rules that don't — used to pick
        /// the single best match when more than one rule matches the same part.</summary>
        public int Specificity => (useRole ? 1 : 0) + (string.IsNullOrEmpty(gender) ? 0 : 1);
    }

    public List<WeightRule> rules = new();

    /// <summary>Resolves the weight to use for one part for one role. Most-specific matching rule
    /// wins; falls back to <paramref name="partDefaultWeight"/> when nothing matches. role may be
    /// null for "no role context" (only role-agnostic rules can match then).</summary>
    public float GetWeight(EmployeeRole? role, string gender, string slot, string variant, float partDefaultWeight)
    {
        var match = rules.Where(r => r.Matches(role, gender, slot, variant))
                          .OrderByDescending(r => r.Specificity)
                          .FirstOrDefault();
        return match != null ? match.weight : partDefaultWeight;
    }

    private static AvatarWeightConfig _cached;

    /// <summary>Loads the weight config from Resources (cached). Null if the asset hasn't been
    /// created yet — every caller treats that as "no rules configured", not an error.</summary>
    public static AvatarWeightConfig Load()
    {
        if (_cached == null)
            _cached = Resources.Load<AvatarWeightConfig>("Resource_AvatarSystemAssets/AvatarWeightConfig");
        return _cached;
    }
}
