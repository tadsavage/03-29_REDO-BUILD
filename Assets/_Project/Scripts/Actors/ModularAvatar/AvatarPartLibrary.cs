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
/// Holds TWO separate lists (2026-09-27 pipeline redesign):
///  - <see cref="parts"/> — raw, not-yet-reviewed scan results. Fully rebuilt on every rescan.
///  - <see cref="finalizedParts"/> — parts that have actually been submitted through the AOD
///    (ModularAvatarFinalizer.TryFinalize), each a standalone AvatarPartAsset file. Never touched by
///    a rescan; this is the permanent, git-diff-friendly source of truth for anything actually live.
/// Use <see cref="AllParts"/> for any query that doesn't care which stage a part is in.
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
    public class Part : IAvatarPart
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

        // Skyrim-style clipping fix (2026-09-30): body-part slots this CLOTHING part hides when
        // worn, e.g. coveralls -> {"body","arms","legs"}. Only meaningful when this part's own slot
        // is not itself a body slot. See ModularAvatarAssembler.BodySlots / Build's masking pass.
        public List<string> hiddenBodySlots = new();

        // Vestigial as of the 2026-09-27 pipeline redesign — a raw Part is ALWAYS unreviewed now
        // (submitting one removes it from AvatarPartLibrary.parts and produces an AvatarPartAsset
        // instead, rather than flipping this bool in place). Kept only so old serialized data
        // deserializes without error; never set true anymore. See IAvatarPart.MetadataReviewed.
        public bool metadataReviewed = false;

        // True once an assembled avatar has actually used this part in-game (set by
        // ModularAvatarAssembler, which biases toward unverified parts specifically so a freshly
        // added item shows up in the hiring roster quickly instead of waiting on pure random
        // chance). Lets Tad visually confirm a new asset works by hiring and watching it walk
        // around. Preserved across rescans same as the other AOD metadata.
        public bool verifiedInGame = false;

        public bool AllowsRole(EmployeeRole role) => allowedRoles.Count == 0 || allowedRoles.Contains(role);

        // ── IAvatarPart ──
        string IAvatarPart.ObjectName => objectName;
        string IAvatarPart.Gender { get => gender; set => gender = value; }
        string IAvatarPart.Slot => slot;
        string IAvatarPart.Variant => variant;
        List<EmployeeRole> IAvatarPart.AllowedRoles => allowedRoles;
        List<ColorVariant> IAvatarPart.ColorVariants => colorVariants;
        float IAvatarPart.DefaultWeight { get => defaultWeight; set => defaultWeight = value; }
        List<string> IAvatarPart.HiddenBodySlots => hiddenBodySlots;
        bool IAvatarPart.MetadataReviewed => false; // see the field's own comment above
        bool IAvatarPart.VerifiedInGame { get => verifiedInGame; set => verifiedInGame = value; }
    }

    [Tooltip("One entry per FBX found in the drop folder.")]
    public List<SourceModel> sources = new();

    [Tooltip("RAW, not-yet-reviewed scan results only. Rebuilt on each scan. Submitting a part " +
             "through the AOD removes it from here and adds it to finalizedParts instead.")]
    public List<Part> parts = new();

    [Tooltip("Submitted/finalized parts — one AvatarPartAsset file per entry, loaded from " +
             "ModularAvatarFinalizer.FinalizedAssetFolder. Never touched by a rescan.")]
    public List<AvatarPartAsset> finalizedParts = new();

    // ── Queries ───────────────────────────────────────────────────────────────────

    /// <summary>Every part regardless of pipeline stage — the query surface most callers should use.</summary>
    public IEnumerable<IAvatarPart> AllParts =>
        parts.Cast<IAvatarPart>().Concat(finalizedParts.Cast<IAvatarPart>());

    public IEnumerable<string> Genders() =>
        AllParts.Select(p => p.Gender).Distinct();

    /// <summary>Distinct slot names available for a gender, in first-seen order. "neutral" parts
    /// (not gender-specific — e.g. hardhat/headphones) count for every gender query.</summary>
    public List<string> SlotsFor(string gender)
    {
        gender = gender?.ToLower();
        var seen = new List<string>();
        foreach (var p in AllParts)
            if ((p.Gender == gender || p.Gender == "neutral") && !seen.Contains(p.Slot))
                seen.Add(p.Slot);
        return seen;
    }

    /// <summary>Every variant of a slot available to a gender — includes that gender's own parts
    /// plus any "neutral" parts for the same slot (shared across both genders).</summary>
    public List<IAvatarPart> VariantsFor(string gender, string slot)
    {
        gender = gender?.ToLower();
        slot   = slot?.ToLower();
        return AllParts.Where(p => (p.Gender == gender || p.Gender == "neutral") && p.Slot == slot).ToList();
    }

    public GameObject PrefabFor(IAvatarPart p) => p switch
    {
        AvatarPartAsset fa => fa.FinalizedPrefab,
        Part rp when rp.sourceIndex >= 0 && rp.sourceIndex < sources.Count => sources[rp.sourceIndex].prefab,
        _ => null,
    };

    public int PartCount => parts.Count + finalizedParts.Count;

    /// <summary>Parts a specific role may actually use for a gender+slot — VariantsFor filtered by
    /// AllowsRole. What ModularAvatarAssembler should pick from once it's wired to roles (Phase 2).</summary>
    public List<IAvatarPart> VariantsFor(string gender, string slot, EmployeeRole role) =>
        VariantsFor(gender, slot).Where(p => p.AllowsRole(role)).ToList();

    /// <summary>Every raw, not-yet-reviewed part — backs the AOD's "missing data" filter toggle.
    /// A finalized AvatarPartAsset is never "unreviewed" by construction, so this only ever needs to
    /// look at the raw list.</summary>
    public List<IAvatarPart> UnreviewedParts() => parts.Cast<IAvatarPart>().ToList();
}
