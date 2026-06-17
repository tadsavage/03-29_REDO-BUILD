using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Builds a complete avatar GameObject from an AvatarPartLibrary by picking one part per slot.
/// Random by default (the hiring board just asks for a gender) — wild combinations are a feature.
///
/// Strategy: instantiate the source FBX that holds the body (so the armature comes along for
/// future animation), then PRUNE it down to the chosen parts. Parts that live in OTHER source
/// FBXs (if you ever split parts across files) are extracted and re-parented onto the same root.
///
/// Works in both edit mode (preview tooling) and play mode (runtime spawning).
/// </summary>
public static class ModularAvatarAssembler
{
    // Slots whose chosen variant is ALWAYS applied (the character would look broken without them).
    private static readonly HashSet<string> CoreSlots = new()
        { "body", "eyes", "eyebrows", "mouth", "face", "chest", "legs", "feet" };

    // Expression slots — default to the "Neutral" variant; the runtime morale/fatigue system
    // swaps these later. (Detected by the variant name containing "neutral".)
    private static readonly HashSet<string> ExpressionSlots = new() { "eyebrows", "mouth" };

    // Slots that all occupy the HEAD — only ONE item is ever worn (hair OR hat OR nothing).
    // The head is one slot now; "hair" is kept here too so any not-yet-renamed legacy parts
    // still land in the head pool instead of stacking and clipping.
    private static readonly HashSet<string> HeadPositionSlots = new() { "head", "hair" };

    // Chance an avatar wears nothing on its head (bald / no hat).
    private const float BaldChance = 0.15f;

    // Accessory slots: not everyone wears them. value = chance (0–1) the slot is included.
    // NOTE: keys MUST be lower-case — slot names are lower-cased when parsed (see importer).
    // hair + head(hats) are NOT here — they share the head position and are chosen as a
    // mutually-exclusive group (see ChooseHeadSlot). vest is mandatory (see Build).
    private static readonly Dictionary<string, float> OptionalSlotChance = new()
    {
        { "facialhair", 0.30f },
    };

    /// <summary>Build a random avatar for a gender. Returns null if the library has no parts for it.</summary>
    public static GameObject Build(AvatarPartLibrary lib, string gender, System.Random rng = null)
    {
        if (lib == null) { Debug.LogWarning("[ModularAvatar] No library."); return null; }
        rng ??= new System.Random();
        gender = gender.ToLower();

        var slots = lib.SlotsFor(gender);
        if (slots.Count == 0) { Debug.LogWarning($"[ModularAvatar] No parts for gender '{gender}'."); return null; }

        // ── Choose one part per slot ──────────────────────────────────────────────
        var chosen = new List<AvatarPartLibrary.Part>();

        // The head is ONE slot — at most one item across all head-position slots (hair/hat),
        // or nothing (bald). Decided up-front; those slots are skipped in the loop below.
        var headItem = ChooseHeadItem(lib, gender, rng);

        foreach (var slot in slots)
        {
            if (HeadPositionSlots.Contains(slot)) continue;   // handled by the head pick below

            var variants = lib.VariantsFor(gender, slot);
            if (variants.Count == 0) continue;

            // Safety vests are mandatory in a warehouse — every employee wears one (50/50 type).
            if (slot == "vest")
            {
                chosen.Add(variants[rng.Next(variants.Count)]);
                continue;
            }

            // Other accessory slots (e.g. facial hair) may be skipped entirely.
            if (OptionalSlotChance.TryGetValue(slot, out float chance) && rng.NextDouble() > chance)
                continue;

            // Core/expression slots (body, eyes, eyebrows, mouth, face, chest, legs, feet) always
            // get a part; eyebrows/mouth default to the Neutral expression.
            AvatarPartLibrary.Part pick = ExpressionSlots.Contains(slot)
                ? (PickNeutral(variants) ?? variants[rng.Next(variants.Count)])
                : variants[rng.Next(variants.Count)];

            chosen.Add(pick);
        }

        // Apply the single chosen head item, if any (null = bald).
        if (headItem != null)
            chosen.Add(headItem);

        if (chosen.Count == 0) return null;

        // ── Pick the "primary" source: the FBX that holds the body (it carries the armature) ──
        var bodyPart = chosen.FirstOrDefault(p => p.slot == "body") ?? chosen[0];
        int primarySource = bodyPart.sourceIndex;
        var primaryPrefab = lib.sources[primarySource].prefab;
        if (primaryPrefab == null) { Debug.LogWarning("[ModularAvatar] Primary source prefab missing."); return null; }

        // Instantiate the primary FBX and prune it to the chosen parts (keeps the armature).
        var root = Object.Instantiate(primaryPrefab);
        root.name = $"Avatar_{gender}_{bodyPart.variant}";

        var chosenNames = new HashSet<string>(chosen.Where(p => p.sourceIndex == primarySource)
                                                    .Select(p => p.objectName));

        foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (t == null || t == root.transform) continue;
            // A "part" is any mesh whose name parses to gender_slot_variant. Anything that
            // isn't a recognised part (the armature, bones, empties) is left untouched.
            if (!IsPartObject(t)) continue;
            if (!chosenNames.Contains(t.name))
                SafeDestroy(t.gameObject);
        }

        // ── Merge in chosen parts that come from OTHER source FBXs ────────────────
        foreach (var grp in chosen.Where(p => p.sourceIndex != primarySource).GroupBy(p => p.sourceIndex))
        {
            var prefab = lib.sources[grp.Key].prefab;
            if (prefab == null) continue;
            var temp = Object.Instantiate(prefab);
            foreach (var part in grp)
            {
                var child = FindDeep(temp.transform, part.objectName);
                if (child == null) continue;
                child.SetParent(root.transform, worldPositionStays: false);
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale    = Vector3.one;
            }
            SafeDestroy(temp);
        }

        return root;
    }

    /// <summary>Deterministic build — same seed always produces the same avatar (so an employee
    /// keeps a stable look across sessions). Seed an employee from their GUID via StableSeed.</summary>
    public static GameObject Build(AvatarPartLibrary lib, string gender, int seed)
        => Build(lib, gender, new System.Random(seed));

    private static AvatarPartLibrary _cachedLib;

    /// <summary>Loads the part library from Resources (cached). Null if it hasn't been scanned yet.</summary>
    public static AvatarPartLibrary LoadLibrary()
    {
        if (_cachedLib == null)
            _cachedLib = Resources.Load<AvatarPartLibrary>("ModularAvatar/AvatarPartLibrary");
        return _cachedLib;
    }

    /// <summary>Stable hash for a string → seed. (System.String.GetHashCode is randomised per
    /// process run, so it can't be used for a look that must persist across sessions.)</summary>
    public static int StableSeed(string s)
    {
        if (string.IsNullOrEmpty(s)) return 1;
        unchecked
        {
            int hash = 23;
            foreach (char c in s) hash = hash * 31 + c;
            return hash == 0 ? 1 : hash;
        }
    }

    private static bool IsPartObject(Transform t)
    {
        // Must carry geometry and parse as a part name; that excludes the armature & bones.
        bool hasMesh = t.GetComponent<MeshFilter>() != null || t.GetComponent<SkinnedMeshRenderer>() != null;
        return hasMesh && ParsesAsPart(t.name);
    }

    private static bool ParsesAsPart(string name)
    {
        var seg = name.Split('_');
        return seg.Length >= 3 && (seg[0].ToLower() == "male" || seg[0].ToLower() == "female");
    }

    // Pick the ONE item worn on the head, pooled across all head-position slots (hair + hats),
    // or null = bald. One slot, one item.
    private static AvatarPartLibrary.Part ChooseHeadItem(AvatarPartLibrary lib, string gender, System.Random rng)
    {
        var pool = new List<AvatarPartLibrary.Part>();
        foreach (var s in HeadPositionSlots)
            pool.AddRange(lib.VariantsFor(gender, s));
        if (pool.Count == 0) return null;
        if (rng.NextDouble() < BaldChance) return null;       // bald / no hat
        return pool[rng.Next(pool.Count)];
    }

    private static AvatarPartLibrary.Part PickNeutral(List<AvatarPartLibrary.Part> variants) =>
        variants.FirstOrDefault(v => v.variant.ToLower().Contains("neutral"));

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform c in parent)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    private static void SafeDestroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }
}
