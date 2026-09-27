using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Builds a complete avatar GameObject from an AvatarPartLibrary by picking one part per slot.
/// Random by default (the hiring board just asks for a gender) — wild combinations are a feature.
///
/// Strategy: instantiate the source prefab that holds the body (so the armature comes along for
/// future animation), then PRUNE it down to the chosen parts. Parts that resolve to OTHER prefabs
/// (a different raw FBX, or a different finalized AvatarPartAsset prefab) are extracted and
/// re-parented onto the same root.
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

    // Hair is the only slot that occupies the HEAD position exclusively — bald OR one hair
    // variant, never both, so a hairstyle never bakes into the body mesh. Hats/props (hardhat,
    // headphones — see OptionalSlotChance's "hat" entry) are a separate, independent optional
    // slot layered on top instead of competing with hair for one pick; per Tad, these are worn
    // over/with hair rather than replacing it.
    private static readonly HashSet<string> HeadPositionSlots = new() { "hair" };

    // The old, all-in-one Male_Modular_Staff.fbx / Female_Modular_Staff.fbx (still scanned for
    // chest/legs/feet/eyebrows/face/vest — see ModularAvatarImporter's Obsolete_Humanoids skip)
    // carry a LEGACY "head" slot that mixes hairstyles and hats into one list (male_head_Afro,
    // male_head_Helmet, female_head_hatGray, etc). It predates the new "hair"/"hat" slots above
    // and was never gated the same way — it's an ordinary always-on slot, so every avatar was
    // getting a legacy head item from THIS slot in addition to a pick from the new hair/hat
    // system, stacking two head meshes at once. Per Tad, the new hair+hat slots now fully replace
    // it for both genders — skipped here entirely rather than added to `chosen`.
    private static readonly HashSet<string> DeprecatedSlots = new() { "head" };

    // Chance an avatar wears nothing on its head (bald / no hat). Females are never bald
    // (see ChooseHeadItem) — this only ever applies to males. Per Tad: 50/50 hair-or-bald for men.
    private const float BaldChance = 0.5f;

    // Accessory slots: not everyone wears them. value = chance (0–1) the slot is included.
    // NOTE: keys MUST be lower-case — slot names are lower-cased when parsed (see importer).
    // hair is NOT here — it's chosen separately as the exclusive head-position pick (bald vs one
    // hairstyle; see ChooseHeadItem). vest is mandatory (see Build). "hat" (hardhat only — see the
    // Build loop) and headphones are two INDEPENDENT 50% rolls, each gender-neutral, that can both
    // land — per Tad, headphones aren't exclusive with a hard hat.
    private static readonly Dictionary<string, float> OptionalSlotChance = new()
    {
        { "facialhair", 0.30f },
        { "hat", 0.50f },
    };

    // Independent 50% chance of wearing headphones — gender-neutral, stacks with the hard hat roll.
    private const float HeadphonesChance = 0.5f;

    /// <summary>The four cosmetic "categories" the AOD's per-employee "Pimp My Employee" editor is
    /// allowed to override (2026-09-27) — deliberately excludes identity/clothing slots (body, vest,
    /// eyes, chest, legs, feet, expressions) since Tad hasn't figured out clothing/skin color yet.
    /// Each key maps to a (slot, variant-filter) pair used both by the assembler's override
    /// post-pass below and by AODPanel to build its category tabs. "hat" splits into two
    /// independent keys because hardhat and headphones are two independent rolls that can both be
    /// worn at once — see the Build loop's own "hat" handling.</summary>
    public static readonly string[] EditableOverrideKeys = { "hair", "hat.hardhat", "hat.headphones", "facialhair" };

    public static (string slot, System.Func<IAvatarPart, bool> matches) OverrideCategoryInfo(string key) => key switch
    {
        "hair"           => ("hair", (System.Func<IAvatarPart, bool>)(p => true)),
        "hat.hardhat"    => ("hat",  (System.Func<IAvatarPart, bool>)(p => p.Variant.ToLower().Contains("hardhat"))),
        "hat.headphones" => ("hat",  (System.Func<IAvatarPart, bool>)(p => p.Variant.ToLower().Contains("headphones"))),
        "facialhair"     => ("facialhair", (System.Func<IAvatarPart, bool>)(p => true)),
        _ => (key, (System.Func<IAvatarPart, bool>)(p => true)),
    };

    /// <summary>Build a random avatar for a gender. Returns null if the library has no parts for it.
    /// <paramref name="role"/> restricts every pick to parts that allow that role
    /// (IAvatarPart.AllowsRole) and feeds AvatarWeightConfig's per-role weighting — pass
    /// null for "no role context" (the editor preview tool uses this to show full variety).</summary>
    public static GameObject Build(AvatarPartLibrary lib, string gender, System.Random rng = null, EmployeeRole? role = null)
        => Build(lib, gender, rng ?? new System.Random(), role, null, out _);

    /// <summary>Deterministic build — same seed always produces the same avatar (so an employee
    /// keeps a stable look across sessions). Seed an employee from their GUID via StableSeed.</summary>
    public static GameObject Build(AvatarPartLibrary lib, string gender, int seed, EmployeeRole? role = null)
        => Build(lib, gender, new System.Random(seed), role, null, out _);

    /// <summary>Full overload backing both the plain random build above AND the "Pimp My Employee"
    /// per-employee override editor. <paramref name="overrides"/> maps an
    /// <see cref="EditableOverrideKeys"/> entry to a specific part's objectName ("" = explicitly
    /// none/bald/removed); a missing key means "let the normal random roll decide", exactly as
    /// before overrides existed. <paramref name="chosenOut"/> reports the FINAL part actually used
    /// for each editable category (null = none equipped) — the AOD editor uses this to show what's
    /// currently on an employee before picking something else.
    ///
    /// Overrides are applied as a POST-PASS after the normal random pick loop runs to completion
    /// completely unmodified — this is deliberate: skipping rng consumption for an overridden slot
    /// would shift every later Random.Next() call's position in the sequence, silently changing
    /// which body/vest/eyebrow variant a re-seeded rebuild produces even though only e.g. the
    /// hairstyle was meant to change. Running the full unmodified pass first and only swapping the
    /// FINAL result afterward keeps every non-overridden pick byte-for-byte identical.</summary>
    public static GameObject Build(AvatarPartLibrary lib, string gender, System.Random rng, EmployeeRole? role,
        IReadOnlyDictionary<string, string> overrides, out Dictionary<string, IAvatarPart> chosenOut)
    {
        chosenOut = new Dictionary<string, IAvatarPart>();
        if (lib == null) { Debug.LogWarning("[ModularAvatar] No library."); return null; }
        rng ??= new System.Random();
        gender = gender.ToLower();

        var slots = lib.SlotsFor(gender);
        if (slots.Count == 0) { Debug.LogWarning($"[ModularAvatar] No parts for gender '{gender}'."); return null; }

        // ── Choose one part per slot ──────────────────────────────────────────────
        var chosen = new List<IAvatarPart>();

        // The head is ONE slot — at most one item across all head-position slots (hair/hat),
        // or nothing (bald). Decided up-front; those slots are skipped in the loop below.
        var headItem = ChooseHeadItem(lib, gender, rng, role);

        foreach (var slot in slots)
        {
            if (HeadPositionSlots.Contains(slot)) continue;   // handled by the head pick below
            if (DeprecatedSlots.Contains(slot)) continue;     // legacy head slot — see comment above

            var allVariants = lib.VariantsFor(gender, slot);
            var variants = FilterRole(allVariants, role);
            // Safety net: role filtering should never leave a CORE slot (body, eyes, ...) with
            // nothing to pick — that would build a character missing a body part rather than just
            // skipping an accessory. Falls back to the unfiltered list and logs it, since it means
            // someone set allowedRoles on a core-slot part in a way that excludes this role entirely.
            if (variants.Count == 0 && CoreSlots.Contains(slot) && allVariants.Count > 0)
            {
                Debug.LogWarning($"[ModularAvatar] Role '{role}' has no allowed '{slot}' parts for gender " +
                                  $"'{gender}' — falling back to the unfiltered list so the avatar isn't missing a core part.");
                variants = allVariants;
            }
            if (variants.Count == 0) continue;

            // Safety vests are mandatory in a warehouse — every employee wears one (50/50 type).
            if (slot == "vest")
            {
                chosen.Add(PickVariant(variants, rng, role, gender));
                continue;
            }

            // Hard hat and headphones are two INDEPENDENT rolls sharing the same "hat" slot/head
            // position (both can land on one avatar). Hard hat: 50% chance of wearing one at all
            // (OptionalSlotChance["hat"]), then a clean 50/50 between colors — restricted to
            // "hardhat" variants specifically so headphones (handled separately below) can't dilute
            // that color split to 33/33/33. Headphones: independent 50% chance, gender-neutral.
            if (slot == "hat")
            {
                var hardhats = variants.Where(v => v.Variant.ToLower().Contains("hardhat")).ToList();
                if (hardhats.Count > 0 && rng.NextDouble() <= OptionalSlotChance["hat"])
                    chosen.Add(PickVariant(hardhats, rng, role, gender));

                var headphones = variants.Where(v => v.Variant.ToLower().Contains("headphones")).ToList();
                if (headphones.Count > 0 && rng.NextDouble() <= HeadphonesChance)
                    chosen.Add(PickVariant(headphones, rng, role, gender));

                continue;
            }

            // Other accessory slots (e.g. facial hair) may be skipped entirely.
            if (OptionalSlotChance.TryGetValue(slot, out float chance) && rng.NextDouble() > chance)
                continue;

            // Core/expression slots (body, eyes, eyebrows, mouth, face, chest, legs, feet) always
            // get a part; eyebrows/mouth default to the Neutral expression.
            IAvatarPart pick = ExpressionSlots.Contains(slot)
                ? (PickNeutral(variants) ?? PickVariant(variants, rng, role, gender))
                : PickVariant(variants, rng, role, gender);

            chosen.Add(pick);
        }

        // Apply the single chosen head item, if any (null = bald).
        if (headItem != null)
            chosen.Add(headItem);

        // ── Per-employee overrides (Pimp My Employee, 2026-09-27) — see the full-overload doc
        // comment above for why this runs as a post-pass rather than short-circuiting the loop.
        if (overrides != null && overrides.Count > 0)
            foreach (var key in EditableOverrideKeys)
                ApplyCategoryOverride(chosen, overrides, key, lib, gender);

        foreach (var key in EditableOverrideKeys)
        {
            var (slot, matches) = OverrideCategoryInfo(key);
            chosenOut[key] = chosen.FirstOrDefault(p => p.Slot == slot && matches(p));
        }
        // Not an editable category — reported so callers (EmployeeSpawner's bodiless-avatar safety
        // net) can verify a real body part was actually used WITHOUT relying on the assembled
        // GameObject's names, which the "primary source" rename below deliberately destroys for a
        // single-mesh-on-root body source (see root.name assignment just below — it clobbers the
        // very "_body_" substring a name-based check would otherwise look for).
        chosenOut["body"] = chosen.FirstOrDefault(p => p.Slot == "body");

        if (chosen.Count == 0) return null;

        // ── Pick the "primary" source: the prefab that holds the body (it carries the armature) ──
        var bodyPart = chosen.FirstOrDefault(p => p.Slot == "body") ?? chosen[0];
        var primaryPrefab = lib.PrefabFor(bodyPart);
        if (primaryPrefab == null) { Debug.LogWarning("[ModularAvatar] Primary source prefab missing."); return null; }

        // Instantiate the primary prefab and prune it to the chosen parts (keeps the armature).
        var root = Object.Instantiate(primaryPrefab);
        root.name = $"Avatar_{gender}_{bodyPart.Variant}";

        // Grouped by resolved PREFAB REFERENCE, not sourceIndex — a finalized AvatarPartAsset has no
        // sourceIndex at all (each one is its own standalone, single-part prefab), so prefab identity
        // is the only grouping key that works for both raw Parts and finalized parts uniformly.
        var chosenNames = new HashSet<string>(chosen.Where(p => lib.PrefabFor(p) == primaryPrefab)
                                                    .Select(p => p.ObjectName));

        foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (t == null || t == root.transform) continue;
            // A "part" is any mesh whose name parses to gender_slot_variant. Anything that
            // isn't a recognised part (the armature, bones, empties) is left untouched.
            if (!IsPartObject(t)) continue;

            // Keep all expression parts (eyebrows and face/mouth) so they can be swapped dynamically based on mood.
            if (t.name.Contains("_eyebrows_") || t.name.Contains("_face_"))
            {
                var smr = t.GetComponent<SkinnedMeshRenderer>();
                if (smr != null) smr.enabled = false;
                continue;
            }

            if (!chosenNames.Contains(t.name))
                SafeDestroy(t.gameObject);
        }

        // ── Merge in chosen parts that come from OTHER prefabs ────────────────
        // A merged-in mesh is still skinned to ITS OWN source's skeleton, which lives on
        // `temp` and is about to be destroyed. Without rebinding, the SkinnedMeshRenderer's
        // bones[]/rootBone keep pointing at those (soon-null) transforms, so it renders
        // collapsed at its bind-pose origin — looking like a stray piece left at world zero,
        // even though the GameObject itself is correctly parented under `root` the whole time.
        Dictionary<string, Transform> rootBonesByName = null;

        foreach (var grp in chosen.Where(p => lib.PrefabFor(p) != primaryPrefab).GroupBy(p => lib.PrefabFor(p)))
        {
            var prefab = grp.Key;
            if (prefab == null) continue;
            var temp = Object.Instantiate(prefab);
            // Object.Instantiate appends "(Clone)" to the name, which breaks FindDeep's exact-name
            // match on any single-mesh source (hair/hat/prop FBX exported with the mesh ON the
            // root, no children) — the part's objectName is the ORIGINAL name (e.g.
            // "neutral_hat_hardhat"), so temp.transform itself would never match. Stripping the
            // suffix back off makes FindDeep's first check (parent.name == name) work whether the
            // part lives on the root or a nested child.
            temp.name = prefab.name;

            // When a source is a single-mesh FBX (the mesh sits ON the root, no children — every
            // prop/hair file exported this way), FindDeep matches temp.transform ITSELF, so
            // reparenting "child" reparents `temp` whole. Destroying `temp` afterwards (below)
            // would then destroy the very object just moved under `root`. Tracked so the destroy
            // at the end of this group can be skipped in that case.
            bool tempReparentedWhole = false;

            foreach (var part in grp)
            {
                var child = FindDeep(temp.transform, part.ObjectName);
                if (child == null) continue;
                if (child == temp.transform) tempReparentedWhole = true;

                var smr = child.GetComponent<SkinnedMeshRenderer>();
                bool isSkinned = smr != null && smr.bones != null && smr.bones.Length > 0;

                rootBonesByName ??= root.GetComponentsInChildren<Transform>(true)
                                        .GroupBy(b => b.name)
                                        .ToDictionary(g => g.Key, g => g.First());

                // Unskinned head-worn props (hair/hardhat/headphones) have no bones[] of their own
                // to deform with, so they only ever move by riding their PARENT transform. Parenting
                // them to `root` (the avatar's top-level object) meant they only followed the whole-
                // character root motion — which is disabled (modAnimator.applyRootMotion = false in
                // EmployeeSpawner) — so they sat dead still while every bone-driven animation (head
                // turns, walking bob, arm swing) played underneath them: "not moving with the body".
                // Parenting to the actual Head_M bone makes them ride that bone's own animated
                // transform instead, exactly like a real hat/hairstyle would.
                Transform targetParent = root.transform;
                if (!isSkinned && (part.Slot == "hair" || part.Slot == "hat") &&
                    rootBonesByName.TryGetValue("Head_M", out var headBone))
                    targetParent = headBone;

                // worldPositionStays: TRUE is load-bearing — every merged-in source prefab is
                // instantiated fresh at world origin/identity, exactly like `root` itself, so a
                // static prop authored to sit on the head in the SOURCE file's own coordinate space
                // (measured: hair's world bounds center landed within 2cm of the body's Head_M bone)
                // is ALREADY in the right place the instant it's reparented — no offset needed.
                // Reparenting onto Head_M with worldPositionStays:true re-derives the correct local
                // offset from THAT bone instead of root, so the position fix above still holds.
                child.SetParent(targetParent, worldPositionStays: true);

                if (!isSkinned) continue;

                var remappedBones = new Transform[smr.bones.Length];
                for (int i = 0; i < smr.bones.Length; i++)
                {
                    var bone = smr.bones[i];
                    remappedBones[i] = (bone != null && rootBonesByName.TryGetValue(bone.name, out var match))
                        ? match : bone;
                }
                smr.bones = remappedBones;

                if (smr.rootBone != null && rootBonesByName.TryGetValue(smr.rootBone.name, out var rootMatch))
                    smr.rootBone = rootMatch;
            }
            if (!tempReparentedWhole) SafeDestroy(temp);
        }

        ApplyMoodExpression(root, gender, EmployeeMood.Neutral);

        return root;
    }

    /// <summary>
    /// Applies the appropriate expression parts (eyebrows and face) based on the employee's mood.
    /// </summary>
    public static void ApplyMoodExpression(GameObject avatarRoot, string gender, EmployeeMood mood)
    {
        if (avatarRoot == null) return;
        gender = gender?.ToLower();

        // Map mood to specific variants
        string targetEyebrow = "BrowsNeutral";
        string targetFace = "Neutral";

        switch (mood)
        {
            case EmployeeMood.Happy:
                targetEyebrow = "BrowsNeutral";
                targetFace = "Smile";
                break;
            case EmployeeMood.Angry:
                targetEyebrow = "BrowsMad";
                targetFace = "Frown";
                break;
            case EmployeeMood.Tired:
                targetEyebrow = "BrowsSad";
                targetFace = "Neutral";
                break;
            default: // Neutral
                targetEyebrow = "BrowsNeutral";
                targetFace = "Neutral";
                break;
        }

        string eyebrowName = $"{gender}_eyebrows_{targetEyebrow}";
        string faceName = $"{gender}_face_{targetFace}";

        foreach (var smr in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string nameLower = smr.name.ToLower();
            if (nameLower.Contains("_eyebrows_"))
            {
                smr.enabled = smr.name.Equals(eyebrowName, System.StringComparison.OrdinalIgnoreCase);
            }
            else if (nameLower.Contains("_face_"))
            {
                smr.enabled = smr.name.Equals(faceName, System.StringComparison.OrdinalIgnoreCase);
            }
        }
    }

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
        if (seg.Length < 3) return false;
        string g = seg[0].ToLower();
        return g == "male" || g == "female" || g == "man" || g == "woman" || g == "neutral";
    }

    // Pick the ONE item worn on the head, pooled across all head-position slots (hair + hats),
    // or null = bald. One slot, one item.
    private static IAvatarPart ChooseHeadItem(AvatarPartLibrary lib, string gender, System.Random rng, EmployeeRole? role)
    {
        var pool = new List<IAvatarPart>();
        foreach (var s in HeadPositionSlots)
            pool.AddRange(lib.VariantsFor(gender, s));
        var filtered = FilterRole(pool, role);
        if (filtered.Count > 0) pool = filtered;  // same core-slot-style safety net as the main loop — hair is optional (bald is valid), so an empty filtered pool just means "skip role filtering here" rather than "force bald"
        if (pool.Count == 0) return null;
        if (gender != "female" && rng.NextDouble() < BaldChance) return null;   // bald only for males
        return PickVariant(pool, rng, role, gender);
    }

    private static IAvatarPart PickNeutral(List<IAvatarPart> variants) =>
        variants.FirstOrDefault(v => v.Variant.ToLower().Contains("neutral"));

    /// <summary>Role is nullable everywhere in this file: null means "no role context" (used by the
    /// editor preview tool), in which case no role filtering is applied at all.</summary>
    private static List<IAvatarPart> FilterRole(List<IAvatarPart> candidates, EmployeeRole? role) =>
        role.HasValue ? candidates.Where(p => p.AllowsRole(role.Value)).ToList() : candidates;

    /// <summary>Picks one part from a candidate list, biasing toward any not-yet-verified-in-game
    /// part so a freshly added AOD item surfaces in the hiring roster quickly instead of waiting on
    /// pure random chance (Tad, 2026-09-26 — "always pick up at least one of the objects added").
    /// Marks the pick verified immediately; falls back to a WEIGHTED random pick (AvatarWeightConfig,
    /// falling back further to each part's own defaultWeight) once nothing in the list still needs
    /// verifying.</summary>
    private static IAvatarPart PickVariant(List<IAvatarPart> candidates, System.Random rng, EmployeeRole? role, string gender)
    {
        var unverified = candidates.Where(c => !c.VerifiedInGame).ToList();
        var pool = unverified.Count > 0 ? unverified : candidates;
        var pick = WeightedPick(pool, rng, role, gender);

        if (!pick.VerifiedInGame)
        {
            pick.VerifiedInGame = true;
#if UNITY_EDITOR
            // Persist right away — this is a rare, one-time-per-item flip (not something that fires
            // on every hire long-term), so the occasional extra disk write here is cheap. Without
            // it, verification achieved during a Play Mode test session would be lost the moment
            // Play Mode stops, and the same item would keep getting force-picked forever.
            if (pick is AvatarPartLibrary.Part)
            {
                var lib = LoadLibrary();
                if (lib != null)
                {
                    UnityEditor.EditorUtility.SetDirty(lib);
                    UnityEditor.AssetDatabase.SaveAssets();
                }
            }
            else if (pick is AvatarPartAsset asset)
            {
                UnityEditor.EditorUtility.SetDirty(asset);
                UnityEditor.AssetDatabase.SaveAssets();
            }
#endif
        }
        return pick;
    }

    /// <summary>Weighted random selection over a pool that's already been through the
    /// verification/role filtering above. Weight per candidate comes from AvatarWeightConfig's
    /// most-specific (role, gender, slot, variant) rule, falling back to the part's own
    /// defaultWeight when no rule matches or the config asset doesn't exist yet — so this behaves
    /// as plain uniform-by-default selection until the weights UI (Phase 4) actually configures
    /// anything, exactly like the rest of this system.</summary>
    private static IAvatarPart WeightedPick(List<IAvatarPart> pool, System.Random rng, EmployeeRole? role, string gender)
    {
        if (pool.Count == 1) return pool[0];

        var cfg = AvatarWeightConfig.Load();
        var weights = new float[pool.Count];
        float total = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            float w = cfg != null
                ? cfg.GetWeight(role, gender, pool[i].Slot, pool[i].Variant, pool[i].DefaultWeight)
                : pool[i].DefaultWeight;
            weights[i] = Mathf.Max(0f, w);
            total += weights[i];
        }
        if (total <= 0f) return pool[rng.Next(pool.Count)]; // everything zero-weighted — fall back to uniform rather than divide by zero

        double r = rng.NextDouble() * total;
        double cumulative = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += weights[i];
            if (r <= cumulative) return pool[i];
        }
        return pool[pool.Count - 1]; // floating-point rounding fallback
    }

    /// <summary>Swaps the FINAL pick for one editable category (see <see cref="EditableOverrideKeys"/>)
    /// after the normal random pass has already run — removes whatever the random roll picked for
    /// this category (if anything) and, unless the override is an explicit "" (none/bald/removed),
    /// adds the requested replacement instead. Searches across ALL genders for the replacement
    /// since several editable categories (hardhat, headphones) are "neutral" parts shared by both.</summary>
    private static void ApplyCategoryOverride(List<IAvatarPart> chosen, IReadOnlyDictionary<string, string> overrides,
        string key, AvatarPartLibrary lib, string gender)
    {
        if (!overrides.TryGetValue(key, out var objectName)) return; // no override for this category — leave the random pick as-is

        var (slot, matches) = OverrideCategoryInfo(key);
        chosen.RemoveAll(p => p.Slot == slot && matches(p));

        if (string.IsNullOrEmpty(objectName)) return; // explicit "none" — stays removed

        var replacement = lib.AllParts.FirstOrDefault(p => p.ObjectName == objectName &&
            (p.Gender == gender || p.Gender == "neutral") && p.Slot == slot && matches(p));
        if (replacement != null) chosen.Add(replacement);
    }

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
        // Always use DestroyImmediate so that unchosen parts are pruned synchronously.
        // This is critical for systems like the Photo Booth that instantiate, prune, and
        // render a portrait on the exact same frame before the end-of-frame cleanups run.
        Object.DestroyImmediate(o);
    }

    /// <summary>Instantiates a single part's source prefab in isolation, hiding/destroying every
    /// sibling "part" mesh in that same source (a source prefab can carry many gender_slot_variant
    /// meshes on one shared root — e.g. every hardhat color) so only the requested one survives.
    /// Used by ModularAvatarFinalizer to produce a clean, standalone prefab from a raw scanned part —
    /// deliberately reuses the exact same IsPartObject/naming logic Build() uses to prune, so a
    /// finalized prefab is structurally identical to what Build() would have isolated at runtime.
    /// Caller owns the returned instance's lifetime (destroy it once done, e.g. after
    /// PrefabUtility.SaveAsPrefabAsset). Returns null if the part's source prefab or the target mesh
    /// itself can't be resolved.</summary>
    public static GameObject IsolatePart(AvatarPartLibrary lib, IAvatarPart part)
    {
        var sourcePrefab = lib.PrefabFor(part);
        if (sourcePrefab == null) return null;

        var instance = Object.Instantiate(sourcePrefab);

        Transform target = null;
        foreach (var t in instance.GetComponentsInChildren<Transform>(true))
        {
            bool hasMesh = t.GetComponent<MeshFilter>() != null || t.GetComponent<SkinnedMeshRenderer>() != null;
            if (!hasMesh) continue;
            // Instantiate() only renames the CLONED ROOT (appends "(Clone)") — children keep their
            // original names, so only the root needs the suffix stripped before comparing.
            string cleaned = t == instance.transform && t.name.EndsWith("(Clone)")
                ? t.name.Substring(0, t.name.Length - "(Clone)".Length)
                : t.name;
            if (cleaned == part.ObjectName) { target = t; continue; }
            if (IsPartObject(t)) SafeDestroy(t.gameObject); // a sibling part (e.g. a different hardhat color) — remove it
        }

        if (target == null) { Object.DestroyImmediate(instance); return null; }

        instance.name = part.ObjectName;
        return instance;
    }
}
