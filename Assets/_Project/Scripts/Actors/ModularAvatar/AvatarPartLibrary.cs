using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Catalog of all modular avatar parts, built by scanning the modular-avatar drop folder
/// (see ModularAvatarImporter). Each FBX dropped there contributes its child meshes as parts,
/// categorised purely from the naming convention:  <c>gender_slot_variant</c>
/// (e.g. "male_body_Black", "male_eyebrows_BrowsNeutral", "female_hair_Bob").
///
/// Slots are STRINGS, not an enum — drop a new FBX with "male_gloves_Leather" and a "gloves"
/// slot simply appears. Nothing here needs editing to support new slot types.
///
/// Lives in Resources so the runtime (hiring board, future character creator) can load it with
/// Resources.Load. The source FBX prefabs are referenced directly, so they're pulled into the
/// build and ModularAvatarAssembler can Instantiate them at runtime without AssetDatabase.
/// </summary>
[CreateAssetMenu(fileName = "AvatarPartLibrary", menuName = "Modular Avatar/Part Library")]
public class AvatarPartLibrary : ScriptableObject
{
    [Serializable]
    public class SourceModel
    {
        public string     fbxPath;   // AssetDatabase path (editor bookkeeping / re-scan)
        public GameObject prefab;    // the imported FBX root — runtime-instantiable
    }

    /// <summary>One selectable color/material option for a Part — e.g. a ballcap that comes in
    /// red/blue/white. Applied to the part's renderer(s) at assembly time. A Part with an empty
    /// list uses its own authored material unchanged (the common case for anything not offered in
    /// multiple colors).</summary>
    [Serializable]
    public class ColorVariant
    {
        public string   name;      // "Red", "Blue", "White" — shown in the AOD UI
        public Material material;

        // Set by the AOD's palette-swatch picker (2026-09-26) — a flat hex color chosen from the
        // simple (non-gradient) Polyperfect atlas, as an alternative to hand-assigning a Material.
        // A variant created this way has `color` set and `material` left null; a variant created via
        // the old drag-a-material flow has `material` set and `color` left default (white). Both are
        // valid — downstream code should prefer `material` when present, else fall back to `color`.
        public Color color = Color.white;
    }

    [Serializable]
    public class Part
    {
        public string objectName;   // child name, e.g. "male_body_Black"
        public string gender;       // "male" / "female" / "neutral" (lower-case)
        public string slot;         // "body","chest","hair",... (lower-case)
        public string variant;      // "Black","BlueShirt","Afro",...
        public int    sourceIndex;  // index into sources[] (which FBX this part lives in)

        // ── AOD metadata (2026-09-26) — set via the Avatar Object Database UI, NOT by the folder
        // scan. Preserved across rescans by ModularAvatarImporter (keyed on objectName) so re-running
        // a scan to pick up new files never wipes metadata already entered for existing ones. ──

        // Empty = every role may use this part. Non-empty = only these roles may. This is a
        // real restriction list, not "hasn't been set up yet" — see metadataReviewed for that.
        public List<EmployeeRole> allowedRoles = new();

        public List<ColorVariant> colorVariants = new();

        // Falls back to this when AvatarWeightConfig has no more specific (role, gender, slot,
        // variant) rule for this part — see AvatarWeightConfig.GetWeight.
        public float defaultWeight = 1f;

        // False for every part the folder scan discovers fresh. The AOD's "missing data" filter
        // keys off this, NOT off allowedRoles being empty — an empty allow-list is a legitimate,
        // deliberate "every role" setting once reviewed, not an indicator of missing setup.
        public bool metadataReviewed = false;

        // True once an assembled avatar has actually used this part in-game (set by
        // ModularAvatarAssembler, which biases toward unverified parts specifically so a freshly
        // added item shows up in the hiring roster quickly instead of waiting on pure random
        // chance). Lets Tad visually confirm a new asset works by hiring and watching it walk
        // around. Preserved across rescans same as the other AOD metadata.
        public bool verifiedInGame = false;

        public bool AllowsRole(EmployeeRole role) => allowedRoles.Count == 0 || allowedRoles.Contains(role);
    }

    [Tooltip("One entry per FBX found in the drop folder.")]
    public List<SourceModel> sources = new();

    [Tooltip("Every part across every source FBX. Rebuilt on each scan.")]
    public List<Part> parts = new();

    // ── Queries ───────────────────────────────────────────────────────────────────
    public IEnumerable<string> Genders() =>
        parts.Select(p => p.gender).Distinct();

    /// <summary>Distinct slot names available for a gender, in first-seen order.</summary>
/// <summary>Distinct slot names available for a gender, in first-seen order. "neutral" parts
    /// (not gender-specific — e.g. hardhat/headphones) count for every gender query.</summary>
    public List<string> SlotsFor(string gender)
    {
        gender = gender?.ToLower();
        var seen = new List<string>();
        foreach (var p in parts)
            if ((p.gender == gender || p.gender == "neutral") && !seen.Contains(p.slot))
                seen.Add(p.slot);
        return seen;
    }

/// <summary>Every variant of a slot available to a gender — includes that gender's own parts
    /// plus any "neutral" parts for the same slot (shared across both genders).</summary>
    public List<Part> VariantsFor(string gender, string slot)
    {
        gender = gender?.ToLower();
        slot   = slot?.ToLower();
        return parts.Where(p => (p.gender == gender || p.gender == "neutral") && p.slot == slot).ToList();
    }

    public GameObject PrefabFor(Part p) =>
        (p != null && p.sourceIndex >= 0 && p.sourceIndex < sources.Count)
            ? sources[p.sourceIndex].prefab : null;

    public int PartCount => parts.Count;

    /// <summary>Parts a specific role may actually use for a gender+slot — VariantsFor filtered by
    /// AllowsRole. What ModularAvatarAssembler should pick from once it's wired to roles (Phase 2).</summary>
    public List<Part> VariantsFor(string gender, string slot, EmployeeRole role) =>
        VariantsFor(gender, slot).Where(p => p.AllowsRole(role)).ToList();

    /// <summary>Every part not yet reviewed in the AOD — freshly scanned-in items with no metadata
    /// entered yet. Backs the AOD's "missing data" filter toggle.</summary>
    public List<Part> UnreviewedParts() => parts.Where(p => !p.metadataReviewed).ToList();
}
