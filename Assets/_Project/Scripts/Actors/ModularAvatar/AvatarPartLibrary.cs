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

    [Serializable]
    public class Part
    {
        public string objectName;   // child name, e.g. "male_body_Black"
        public string gender;       // "male" / "female" (lower-case)
        public string slot;         // "body","chest","hair",... (lower-case)
        public string variant;      // "Black","BlueShirt","Afro",...
        public int    sourceIndex;  // index into sources[] (which FBX this part lives in)
    }

    [Tooltip("One entry per FBX found in the drop folder.")]
    public List<SourceModel> sources = new();

    [Tooltip("Every part across every source FBX. Rebuilt on each scan.")]
    public List<Part> parts = new();

    // ── Queries ───────────────────────────────────────────────────────────────────
    public IEnumerable<string> Genders() =>
        parts.Select(p => p.gender).Distinct();

    /// <summary>Distinct slot names available for a gender, in first-seen order.</summary>
    public List<string> SlotsFor(string gender)
    {
        gender = gender?.ToLower();
        var seen = new List<string>();
        foreach (var p in parts)
            if (p.gender == gender && !seen.Contains(p.slot))
                seen.Add(p.slot);
        return seen;
    }

    public List<Part> VariantsFor(string gender, string slot)
    {
        gender = gender?.ToLower();
        slot   = slot?.ToLower();
        return parts.Where(p => p.gender == gender && p.slot == slot).ToList();
    }

    public GameObject PrefabFor(Part p) =>
        (p != null && p.sourceIndex >= 0 && p.sourceIndex < sources.Count)
            ? sources[p.sourceIndex].prefab : null;

    public int PartCount => parts.Count;
}
