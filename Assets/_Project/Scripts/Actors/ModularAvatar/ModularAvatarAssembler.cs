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
    // "head" and "hands" added 2026-09-30 for the new multi-part body exports (woman_head_bodyA /
    // woman_hands_bodyA) — per Tad, a body must always show a head, same mandatory tier as the
    // torso itself.
    private static readonly HashSet<string> CoreSlots = new()
        { "body", "torso", "head", "hands", "eyes", "eyebrows", "mouth", "face", "chest", "legs", "feet" };

    // The real, skinned BODY slots (2026-09-30 real-workflow pass) — "body" is kept as the torso
    // token for backward compatibility with already-finalized parts (woman.bodyA etc) rather than
    // renaming it to "torso". "head" is deliberately a member (so a clothing item COULD in principle
    // list it) but Build()'s masking pass unconditionally refuses to hide it — per Tad, Head and Hair
    // must always stay enabled regardless of what any clothing item's HiddenBodySlots says.
    public static readonly HashSet<string> BodySlots = new()
        { "body", "torso", "head", "arms", "hands", "waist", "legs", "feet" };

    /// <summary>"neck" (collars, headphones worn round the neck, scarves, necklaces...) is an optional ACCESSORY slot, not a body
    /// slot: no nude "Body" default, and pieces may be rigid (unskinned) - they then ride the Neck_M bone. A garment can still
    /// hide it through HiddenBodySlots (see ApplyBodyPartMasking).</summary>
    public const string NeckSlot = "neck";

    public static bool IsBodySlot(string slot) => !string.IsNullOrEmpty(slot) && BodySlots.Contains(slot.ToLower());

    /// <summary>"body" is the legacy token for the torso (woman.bodyA); Avatar 2.0 names it "torso".</summary>
    public static bool IsTorsoSlot(string slot) => slot == "body" || slot == "torso";

    /// <summary>Avatar 2.0 rule (2026-10-02): the variant "Body" is RESERVED. In every body slot it is the nude
    /// default skin (Female_Torso_Body, Female_Legs_Body, ...). It is never a wardrobe pick; it is shown only when
    /// nothing else is worn in that slot, so an avatar can never be missing a mesh. A slot that another part HIDES
    /// (ApplyBodyPartMasking) is a different case: there the default is chosen and then switched off.</summary>
    public static bool IsBodyDefault(IAvatarPart p) =>
        p != null && IsBodySlot(p.Slot) && string.Equals(p.Variant, "Body", System.StringComparison.OrdinalIgnoreCase);

    // Expression slots — default to the "Neutral" variant; the runtime morale/fatigue system
    // swaps these later. (Detected by the variant name containing "neutral".)
    private static readonly HashSet<string> ExpressionSlots = new() { "eyebrows", "mouth" };

    // Hair is the only slot that occupies the HEAD position exclusively — bald OR one hair
    // variant, never both, so a hairstyle never bakes into the body mesh. Hats/props (hardhat,
    // headphones — see OptionalSlotChance's "hat" entry) are a separate, independent optional
    // slot layered on top instead of competing with hair for one pick; per Tad, these are worn
    // over/with hair rather than replacing it.
    private static readonly HashSet<string> HeadPositionSlots = new() { "hair" };

    // The old, all-in-one Male_Modular_Staff.fbx / Female_Modular_Staff.fbx used to carry a LEGACY
    // "head" slot that mixed hairstyles and hats into one list (male_head_Afro, male_head_Helmet,
    // female_head_hatGray, etc), which stacked with the new hair/hat system if both were scanned at
    // once. That source lives under XXX_Obsolete_Humanoids, which ModularAvatarImporter already
    // skips outright, so nothing scans into "head" today except the new 2026-09-30 body-part
    // exports (woman_head_bodyA) — "head" is a real, wanted CoreSlot again (see above). Kept as an
    // empty set (rather than deleted) so a future legacy-source slot can be added back here without
    // re-deriving this history.
    private static readonly HashSet<string> DeprecatedSlots = new();

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
        { "neck", 0.35f },   // neck accessories (collar, headphones round the neck, ...): ~1 in 3 employees wears one
        { "face", 0.50f },   // face accessories that sit ON the base head (gag, blindfold, piercings...): the head mesh itself is never swapped
        { "hat", 0.50f },
        { "waist", 0.40f },    // belts: a skinned layer over the hips, independent of legs/torso
        { "glasses", 0.30f },  // shades etc.: its OWN slot so they stack with the gag (face) instead of competing with it
    };

    // Independent 50% chance of wearing headphones — gender-neutral, stacks with the hard hat roll.
    private const float HeadphonesChance = 0.5f;
    // Baseball cap: chance of wearing one when no hard hat was rolled (0.6 x ~50% no-hardhat = ~30% of everyone).
    private const float CapChance = 0.60f;

    /// <summary>The four cosmetic "categories" the AOD's per-employee "Pimp My Employee" editor is
    /// allowed to override (2026-09-27) — deliberately excludes identity/clothing slots (body, vest,
    /// eyes, chest, legs, feet, expressions) since Tad hasn't figured out clothing/skin color yet.
    /// Each key maps to a (slot, variant-filter) pair used both by the assembler's override
    /// post-pass below and by AODPanel to build its category tabs. "hat" splits into two
    /// independent keys because hardhat and headphones are two independent rolls that can both be
    /// worn at once — see the Build loop's own "hat" handling.</summary>
    public static readonly string[] EditableOverrideKeys = { "hair", "hat.hardhat", "hat.cap", "hat.headphones", "facialhair", "neck",
                                                                       "glasses", "waist", "face", "torso", "hands", "legs", "feet" };

    public static (string slot, System.Func<IAvatarPart, bool> matches) OverrideCategoryInfo(string key) => key switch
    {
        "hair"           => ("hair", (System.Func<IAvatarPart, bool>)(p => true)),
        "hat.hardhat"    => ("hat",  (System.Func<IAvatarPart, bool>)(p => p.Variant.ToLower().Contains("hardhat"))),
        "hat.cap"        => ("hat",  (System.Func<IAvatarPart, bool>)(p => p.Variant.ToLower().StartsWith("cap"))),
        "hat.headphones" => ("hat",  (System.Func<IAvatarPart, bool>)(p => p.Variant.ToLower().Contains("headphones"))),
        "facialhair"     => ("facialhair", (System.Func<IAvatarPart, bool>)(p => true)),
        "neck"           => ("neck", (System.Func<IAvatarPart, bool>)(p => true)),
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

            // Body slots (torso, legs, hands, head, feet, ...): wardrobe items replace the slot's nude "Body"
            // default; with nothing to wear (none exist, none allowed for this role, or all weighted 0) the
            // default is shown instead of leaving a hole. The default ignores role filtering and weight on purpose.
            if (IsBodySlot(slot) && allVariants.Any(IsBodyDefault))
            {
                var wardrobe = variants.Where(v => !IsBodyDefault(v)).ToList();
                var bare = allVariants.First(IsBodyDefault);
                IAvatarPart bodyPick = wardrobe.Count > 0 ? PickVariant(wardrobe, rng, role, gender) : null;

                // BARE CHANCE (Tad, 2026-10-03): the nude Body part's own AOD weight is its share of employees who wear NOTHING in this slot,
                // relative to the clothing items: P(bare) = bareWeight / (bareWeight + sum of clothing weights). E.g. socks 100 + Body 100 = 50%
                // bare; socks 100 + Body 0 = never bare (the old behaviour). Role-specific weight rules (AvatarWeightConfig) apply to it too.
                if (bodyPick != null)
                {
                    float bareW = EffectiveWeight(bare, role, gender);
                    if (bareW > 0f)
                    {
                        float clothingW = 0f;
                        foreach (var w in wardrobe) clothingW += EffectiveWeight(w, role, gender);
                        if (rng.NextDouble() < bareW / (bareW + clothingW)) bodyPick = null;
                    }
                }

                bodyPick ??= bare;   // nothing worn (none allowed / all 0% / bare roll won): the slot's nude default
                chosen.Add(bodyPick);
                continue;
            }
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
                var vestPick = PickVariant(variants, rng, role, gender);
                if (vestPick != null) chosen.Add(vestPick);
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
                bool hardhatWorn = false;
                if (hardhats.Count > 0 && rng.NextDouble() <= OptionalSlotChance["hat"])
                {
                    var hardhatPick = PickVariant(hardhats, rng, role, gender);
                    if (hardhatPick != null) { chosen.Add(hardhatPick); hardhatWorn = true; }
                }

                // Baseball caps (variant starts with "Cap"): only when no hard hat landed (two hats can't share a head).
                var caps = variants.Where(v => v.Variant.ToLower().StartsWith("cap")).ToList();
                if (!hardhatWorn && caps.Count > 0 && rng.NextDouble() <= CapChance)
                {
                    var capPick = PickVariant(caps, rng, role, gender);
                    if (capPick != null) chosen.Add(capPick);
                }

                var headphones = variants.Where(v => v.Variant.ToLower().Contains("headphones")).ToList();
                if (headphones.Count > 0 && rng.NextDouble() <= HeadphonesChance)
                {
                    var headphonesPick = PickVariant(headphones, rng, role, gender);
                    if (headphonesPick != null) chosen.Add(headphonesPick);
                }

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

            // pick can now be null (2026-09-30) — every candidate in this slot explicitly
            // zero-weighted, meaning "show nothing here" rather than "fall back to something."
            if (pick != null) chosen.Add(pick);
        }

        // Apply the single chosen head item, if any (null = bald).
        if (headItem != null)
            chosen.Add(headItem);

        // Matching sets (Tad, 2026-10-03): stockings on the feet bring the stockings on the legs and vice versa.
        ApplyLinkedVariants(lib, gender, role, chosen);

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
        chosenOut["body"] = chosen.FirstOrDefault(p => IsTorsoSlot(p.Slot));
        chosenOut["head"] = chosen.FirstOrDefault(p => p.Slot == "head");   // lets callers react to the head variant
        chosenOut["face"] = chosen.FirstOrDefault(p => p.Slot == "face");   // face accessory (gag, blindfold...) - callers react to its variant

        if (chosen.Count == 0) return null;

        // ── Pick the "primary" source: the prefab that holds the body (it carries the armature) ──
        // The nude torso carries the rig the Animator is built from, so it anchors the avatar even when a garment
        // replaces it in `chosen` (the garment then rebinds onto this skeleton like any other part).
        var bodyPart = lib.VariantsFor(gender, "torso").Concat(lib.VariantsFor(gender, "body")).FirstOrDefault(IsBodyDefault)
                       ?? chosen.FirstOrDefault(p => IsTorsoSlot(p.Slot)) ?? chosen[0];
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
            if (t.name.Contains("_eyebrows_") || IsExpressionFaceName(t.name.ToLower()))
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
        // Built BEFORE any merge so it only ever contains the body's own skeleton.
        Dictionary<string, Transform> rootBonesByName = root.GetComponentsInChildren<Transform>(true)
                                .GroupBy(b => b.name)
                                .ToDictionary(g => g.Key, g => g.First());
        var rebound = new HashSet<string>();

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
                // A finalized prefab's ROOT often shares the part's name (root > spare armature copy > mesh), so FindDeep can return the
                // root. For a rigid (unskinned) part prefer the actual mesh-bearing object, so only the mesh moves onto the head/neck
                // bone and the prefab's spare armature copy is destroyed with `temp` instead of riding along under the bone.
                if (FindSkinnedMesh(temp.transform, part.ObjectName) == null)
                {
                    var meshObj = temp.GetComponentsInChildren<Renderer>(true)
                        .Select(r => r.transform).FirstOrDefault(t => t.name == part.ObjectName);
                    if (meshObj != null) child = meshObj;
                }

                // Skinned part: move ONLY the mesh object onto the body's skeleton and let the part's own
                // armature copy be destroyed with `temp`. (Previously the whole prefab root came along, so
                // every part dragged a private skeleton + Animator with it and nothing followed the body.)
                var partMesh = FindSkinnedMesh(temp.transform, part.ObjectName);
                if (partMesh != null &&
                    TryRebindToBody(root, temp.transform, partMesh, part, primaryPrefab, rootBonesByName))
                {
                    rebound.Add(part.ObjectName);
                    continue;
                }

                if (child == temp.transform) tempReparentedWhole = true;

                var smr = child.GetComponent<SkinnedMeshRenderer>();
                bool isSkinned = smr != null && smr.bones != null && smr.bones.Length > 0;

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
                // Rigid neck accessories (collar, headphones round the neck) ride the neck bone the same way.
                else if (!isSkinned && part.Slot == NeckSlot &&
                         rootBonesByName.TryGetValue("Neck_M", out var neckBone))
                    targetParent = neckBone;

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

        ApplyOrientationFixes(root, chosen, rebound);
        ApplyBodyPartMasking(root, chosen);
        TrimHairUnderHats(root);
        ApplyMoodExpression(root, gender, EmployeeMood.Neutral);

        return root;
    }

    // ── Hair under hats (Tad, 2026-10-05) ───────────────────────────────────────────────────────────────────────────────
    // A hard hat or cap sits ON the hair, so the crown of the hairstyle used to poke through the hat. Instead of authoring a second
    // hair mesh for every hat, we cut the hair at the hat's rim: every triangle whose centre lies above the rim is dropped, and everything
    // hanging below it (bangs, sides, back) stays visible. Trimmed meshes are cached per (hair mesh, rim height) so each combination is
    // built once. Mesh space keeps the Blender axes (Z is up), so the up axis is detected from the hat's own bounds.
    private const float HatTrimMargin = 0.004f;   // metres above the rim before hair is cut (keeps the hairline tucked in just under the brim)
    private static readonly Dictionary<(Mesh mesh, int rimMm), Mesh> TrimmedHairCache = new();

    private static void TrimHairUnderHats(GameObject root)
    {
        var all = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var hats = new List<SkinnedMeshRenderer>();
        foreach (var r in all)
            if (r != null && r.sharedMesh != null && r.gameObject.activeSelf && (r.name.Contains("Hardhat") || r.name.Contains("_Cap")))
                hats.Add(r);
        if (hats.Count == 0) return;

        var hb = hats[0].sharedMesh.bounds;
        int up = Mathf.Abs(hb.center.y) >= Mathf.Abs(hb.center.z) ? 1 : 2;
        float rim = float.MaxValue;
        foreach (var h in hats) rim = Mathf.Min(rim, h.sharedMesh.bounds.min[up]);
        if (rim == float.MaxValue) return;
        float cut = rim + HatTrimMargin;

        foreach (var r in all)
        {
            if (r == null || r.sharedMesh == null || !r.name.Contains("_Hair_")) continue;
            var src = r.sharedMesh;
            var key = (src, Mathf.RoundToInt(rim * 1000f));
            if (!TrimmedHairCache.TryGetValue(key, out var trimmed) || trimmed == null)
            {
                var verts = src.vertices;
                trimmed = UnityEngine.Object.Instantiate(src);
                trimmed.name = src.name + "_hatTrim";
                trimmed.hideFlags = HideFlags.HideAndDontSave;

                // Vertices are welded by POSITION (hard-edged meshes duplicate vertices per face, so index adjacency would find nothing).
                static long Weld(Vector3 p) => ((long)Mathf.RoundToInt(p.x * 10000f) * 73856093L) ^ ((long)Mathf.RoundToInt(p.y * 10000f) * 19349663L) ^ ((long)Mathf.RoundToInt(p.z * 10000f) * 83492791L);
                var keptKeys = new HashSet<long>();
                var subTris = new int[src.subMeshCount][];
                for (int s = 0; s < src.subMeshCount; s++)
                {
                    subTris[s] = src.GetTriangles(s);
                    var tris = subTris[s];
                    for (int i = 0; i + 2 < tris.Length; i += 3)
                    {
                        float c = (verts[tris[i]][up] + verts[tris[i + 1]][up] + verts[tris[i + 2]][up]) / 3f;
                        if (c <= cut) { keptKeys.Add(Weld(verts[tris[i]])); keptKeys.Add(Weld(verts[tris[i + 1]])); keptKeys.Add(Weld(verts[tris[i + 2]])); }
                    }
                }
                for (int s = 0; s < src.subMeshCount; s++)
                {
                    var tris = subTris[s];
                    var keep = new List<int>(tris.Length);
                    for (int i = 0; i + 2 < tris.Length; i += 3)
                    {
                        float c = (verts[tris[i]][up] + verts[tris[i + 1]][up] + verts[tris[i + 2]][up]) / 3f;
                        // below the cut: always kept. Above it: kept only if it TOUCHES a kept triangle = exactly one extra ring of polys,
                        // so the low-poly hair reaches up under the hat instead of ending in a gap.
                        bool touchesKept = keptKeys.Contains(Weld(verts[tris[i]])) || keptKeys.Contains(Weld(verts[tris[i + 1]])) || keptKeys.Contains(Weld(verts[tris[i + 2]]));
                        if (c <= cut || touchesKept) { keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]); }
                    }
                    trimmed.SetTriangles(keep, s);
                }
                TrimmedHairCache[key] = trimmed;
            }
            r.sharedMesh = trimmed;
        }
    }

    /// <summary>Slot pairs whose wardrobe items come as MATCHING SETS. If one slot of a pair wears a variant (e.g. "Stockings-Gray") and the
    /// other slot offers the SAME variant name, the other slot wears it too, in either direction, so a pair of stockings is never half on.
    /// Matching is by exact variant name (case-insensitive), so "Stockings-Black" on the feet pairs with "Stockings-Black" on the legs and
    /// never with "Stockings-Gray". It only fires when exactly one slot of the pair is dressed: if both are bare, or both already wear
    /// something (even different items), they are left alone. Role restrictions still apply; the partner's weight is ignored (it is a set).
    /// A bare slot does not pull its partner bare: with feet bare + legs stockings the feet get stockings, so bare feet only happen when the
    /// legs are bare too.</summary>
    private static readonly (string a, string b)[] LinkedSlotPairs = { ("feet", "legs") };

    private static void ApplyLinkedVariants(AvatarPartLibrary lib, string gender, EmployeeRole? role, List<IAvatarPart> chosen)
    {
        foreach (var (slotA, slotB) in LinkedSlotPairs)
        {
            var pa = chosen.FirstOrDefault(p => p.Slot == slotA);
            var pb = chosen.FirstOrDefault(p => p.Slot == slotB);
            if (pa == null || pb == null) continue;
            bool wearA = !IsBodyDefault(pa), wearB = !IsBodyDefault(pb);
            if (wearA == wearB) continue;                      // both bare, or both already dressed: nothing to link

            var source = wearA ? pa : pb;                      // the slot that is wearing something
            var targetSlot = wearA ? slotB : slotA;
            var target = wearA ? pb : pa;                      // the bare slot that should join the set
            var match = FilterRole(lib.VariantsFor(gender, targetSlot), role)
                .FirstOrDefault(v => !IsBodyDefault(v) &&
                                     string.Equals(v.Variant, source.Variant, System.StringComparison.OrdinalIgnoreCase));
            if (match == null) continue;                       // the partner slot has no matching item: leave it bare

            int i = chosen.IndexOf(target);
            if (i >= 0) chosen[i] = match;
        }
    }

    // ── Skeleton merge ──────────────────────────────────────────────────────────────────────────
    // Each part FBX ships its own copy of the armature, and those copies do NOT share the body's rest pose
    // (measured 2026-10-02: sleeves/boots/gloves/coveralls are rotated 180 deg about Y, the head is turned
    // 90 deg, hair/bare arms/bare hands match). A skinned vertex is  bone.localToWorld * bindpose * v,  so
    // pointing a part at the body's bones is only correct if the bindposes are re-based to the body's rest
    // pose. newBindpose = bodyBone^-1 * correction * partBone * oldBindpose  keeps the part looking exactly
    // as it did on its own armature at rest, and from then on it follows the body's animation.
    private static readonly Dictionary<(Mesh, GameObject, int), Mesh> ReboundMeshCache = new();

    private static SkinnedMeshRenderer FindSkinnedMesh(Transform partRoot, string objectName)
    {
        var all = partRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var s in all)
            if (s.name == objectName && s.sharedMesh != null && s.bones != null && s.bones.Length > 0) return s;
        if (all.Length == 1 && all[0].sharedMesh != null && all[0].bones != null && all[0].bones.Length > 0)
            return all[0];
        return null;
    }

    /// <summary>World-space (avatar space) transform baked into a part's bindposes to cancel an authoring
    /// orientation error. Mirrors the old root-transform stop-gaps in ApplyOrientationFixes.</summary>
    /// <summary>The head/hair/hands/hard-hat orientation stop-gaps below were written for the OLD pipeline's wrongly oriented
    /// meshes (head and hair authored facing -Z, hands rolled 180). Avatar 2.0 exports every part from the one shared rig with
    /// the saved Unity_Avatar preset, so parts arrive already oriented and applying the old flips turns them BACKWARDS (found
    /// 2026-10-03: the black bob sat on the head reversed). Left in place but OFF; flip this only to load legacy assets.</summary>
    private const bool ApplyLegacyOrientationFixes = false;

    private static int CorrectionKind(IAvatarPart part)
    {
        if (!ApplyLegacyOrientationFixes) return 0;
        // Hair is authored with the same 180 deg flip as the head (thick mass ends up over the face, fringe at the
        // back), so it gets the head's correction.
        if (IsBodyDefault(part)) return 0;   // Avatar 2.0 body parts are exported on the shared rig, already oriented
        if (part.Slot == "head" || part.Slot == "hair") return 1;
        if (part.Slot == "hands" && !part.ObjectName.StartsWith("neutral_hands_", System.StringComparison.OrdinalIgnoreCase)) return 2;
        return 0;
    }

    private static Matrix4x4 CorrectionMatrix(int kind) => kind switch
    {
        1 => Matrix4x4.TRS(HeadRootPos, HeadRootRot, Vector3.one),
        2 => Matrix4x4.TRS(HandsRootPos, HandsRootRot, Vector3.one),
        _ => Matrix4x4.identity,
    };

    private static bool TryRebindToBody(GameObject root, Transform partRoot, SkinnedMeshRenderer smr,
                                        IAvatarPart part, GameObject bodyPrefab,
                                        Dictionary<string, Transform> bodyBones)
    {
        var mesh = smr.sharedMesh;
        var oldBones = smr.bones;
        var newBones = new Transform[oldBones.Length];
        var rootInvForMatch = root.transform.worldToLocalMatrix;
        var partInvForMatch = partRoot.worldToLocalMatrix;
        for (int i = 0; i < oldBones.Length; i++)
        {
            if (oldBones[i] == null || !bodyBones.TryGetValue(oldBones[i].name, out newBones[i]))
            {
                Debug.LogWarning($"[ModularAvatar] '{part.ObjectName}': bone '{(oldBones[i] != null ? oldBones[i].name : "null")}' " +
                                 "not found on the body skeleton — leaving this part on its own armature.");
                return false;
            }

            // Some part exports have their whole armature turned 180 deg about Y, so the bone NAMED "_L" physically
            // sits where the body's "_R" bone is. Binding by name alone would make the geometry on the right side
            // follow the LEFT arm/leg's animation. Pick whichever of {same name, opposite side} is closer at rest.
            string swapped = SwapSide(oldBones[i].name);
            if (swapped != null && bodyBones.TryGetValue(swapped, out var alt))
            {
                Vector3 partPos = partInvForMatch.MultiplyPoint3x4(oldBones[i].position);
                float dSame = (rootInvForMatch.MultiplyPoint3x4(newBones[i].position) - partPos).sqrMagnitude;
                float dAlt  = (rootInvForMatch.MultiplyPoint3x4(alt.position) - partPos).sqrMagnitude;
                if (dAlt < dSame) newBones[i] = alt;
            }
        }

        int kind = CorrectionKind(part);
        var cacheKey = (mesh, bodyPrefab, kind);
        if (!ReboundMeshCache.TryGetValue(cacheKey, out var useMesh) || useMesh == null)
        {
            var bp = mesh.bindposes;
            var corr = CorrectionMatrix(kind);
            var rootInv = root.transform.worldToLocalMatrix;
            var partInv = partRoot.worldToLocalMatrix;
            var newBp = new Matrix4x4[bp.Length];
            bool changed = false;
            for (int i = 0; i < bp.Length && i < oldBones.Length; i++)
            {
                var cPart = partInv * oldBones[i].localToWorldMatrix;
                var cBody = rootInv * newBones[i].localToWorldMatrix;
                newBp[i] = cBody.inverse * corr * cPart * bp[i];
                if (!changed && !Approx(newBp[i], bp[i])) changed = true;
            }
            if (changed)
            {
                useMesh = Object.Instantiate(mesh);
                useMesh.name = mesh.name + "_rebound";
                useMesh.bindposes = newBp;
                useMesh.hideFlags = HideFlags.HideAndDontSave;
            }
            else useMesh = mesh;
            ReboundMeshCache[cacheKey] = useMesh;
        }

        smr.sharedMesh = useMesh;
        smr.bones = newBones;
        if (smr.rootBone != null && bodyBones.TryGetValue(smr.rootBone.name, out var rb)) smr.rootBone = rb;
        else if (bodyBones.TryGetValue("Root_M", out var r)) smr.rootBone = r;

        smr.transform.SetParent(root.transform, false);
        smr.transform.localPosition = Vector3.zero;
        smr.transform.localRotation = Quaternion.identity;
        smr.transform.localScale = Vector3.one;
        return true;
    }

    private static string SwapSide(string boneName)
    {
        if (boneName.EndsWith("_L")) return boneName.Substring(0, boneName.Length - 2) + "_R";
        if (boneName.EndsWith("_R")) return boneName.Substring(0, boneName.Length - 2) + "_L";
        return null;
    }

    private static bool Approx(Matrix4x4 a, Matrix4x4 b)
    {
        for (int i = 0; i < 16; i++) if (Mathf.Abs(a[i] - b[i]) > 1e-4f) return false;
        return true;
    }

    // Hand-tuned in the scene by Tad (2026-10-01) and baked here so every build gets it. These are the
    // transforms the part's ROOT GameObject must have on the assembled avatar:
    //   HEAD  -> position (0,0,0), rotation (0,180,0)   (the head mesh is authored facing -Z; the body faces +Z)
    //   HANDS -> position (0, 0.02762616, 0.268777), rotation (349.341, 0, 0)   (gloves + bare hands: fixes the
    //            180-degree roll and seats them at the wrists)
    private static readonly Vector3 HeadRootPos  = Vector3.zero;
    private static readonly Quaternion HeadRootRot = Quaternion.Euler(0f, 180f, 0f);
    private static readonly Vector3 HandsRootPos  = new Vector3(0f, 0.02762616f, 0.268777f);
    private static readonly Quaternion HandsRootRot = Quaternion.Euler(349.341f, 0f, 0f);
    //   NEUTRAL GLOVES (neutral_hands_glovesBrown / White; mesh on the root, scale 0.01) -> position (0, 0, -0.07),
    //            rotation (-92.774, 180, 0), set absolutely.
    private static readonly Vector3 NeutralGlovesRootPos  = new Vector3(0f, 0f, -0.07f);
    private static readonly Quaternion NeutralGlovesRootRot = Quaternion.Euler(-92.774f, 180f, 0f);

    /// <summary>Corrects parts whose source assets were authored/exported with the wrong orientation relative to
    /// the body (found 2026-10-01 by rendering the assembled avatar from four sides; the body, coveralls, hair
    /// and boots face +Z, the avatar's forward, but the head faces -Z, the hard hats' brims point -Z, and the
    /// hands are rolled 180 degrees).
    ///
    /// Applied as an OFFSET on top of whatever root transform the part already has — world = offset * current —
    /// rather than overwriting it, so a part whose own root carries an import rotation/scale (the old
    /// neutral_hands_gloves* prefabs: -90 X, 0.01 scale) keeps that and still gets the same correction. For the
    /// woman_* parts the current root is identity, so the offset IS the final transform Tad specified.
    /// Works because every part currently carries its OWN armature copy (see the note in Build), so mesh and bones
    /// move together. Runs while the avatar is still at world origin / identity, so world == avatar-local here.
    /// Stop-gap: the real fix is correcting the meshes in Blender and re-exporting, then delete this.
    /// Hair, boots, coveralls and headphones are correct as-is.</summary>
    private static void ApplyOrientationFixes(GameObject root, List<IAvatarPart> chosen, HashSet<string> rebound)
    {
        if (!ApplyLegacyOrientationFixes) return;
        foreach (var p in chosen)
        {
            // Parts rebound onto the body skeleton already had their correction baked into the bindposes.
            if (rebound != null && rebound.Contains(p.ObjectName)) continue;
            if (IsBodyDefault(p)) continue;   // Avatar 2.0 body parts need no orientation stop-gap
            bool isHead    = p.Slot == "head";
            // Unskinned head-worn props (hard hats, headphones, old unskinned hair) ride the Head_M bone and are
            // authored facing -Z like the head, so they turn 180 about the same axis the head uses.
            bool isHardhat = (p.Slot == "hat" && p.Variant != null &&
                              (p.Variant.ToLower().Contains("hardhat") || p.Variant.ToLower().Contains("headphones")))
                             || p.Slot == "hair";
            bool isHands   = p.Slot == "hands";
            if (!isHead && !isHardhat && !isHands) continue;

            var t = FindDeep(root.transform, p.ObjectName);
            if (t == null) continue;

            if (isHands && p.ObjectName.StartsWith("neutral_hands_", System.StringComparison.OrdinalIgnoreCase))
                // The older neutral gloves (mesh directly on the root, scale 0.01) get their own hand-tuned FINAL
                // transform (Tad, 2026-10-01) — set absolutely, scale stays 0.01.
                t.SetPositionAndRotation(NeutralGlovesRootPos, NeutralGlovesRootRot);
            else if (isHands)
                ApplyRootOffset(t, HandsRootPos, HandsRootRot);
            else if (isHead)
                ApplyRootOffset(t, HeadRootPos, HeadRootRot);
            else
                // Hard hats ride the head, so they turn the same 180 about the SAME vertical axis (the avatar's
                // origin) the head now uses — otherwise the hat and head would end up offset from each other.
                t.RotateAround(root.transform.position, Vector3.up, 180f);
        }
    }

    private static void ApplyRootOffset(Transform t, Vector3 offsetPos, Quaternion offsetRot)
    {
        t.SetPositionAndRotation(offsetPos + offsetRot * t.position, offsetRot * t.rotation);
    }

    /// <summary>Skyrim-style clipping fix (2026-09-30), opened up to EVERY chosen part (2026-10-01) —
    /// not just clothing. Originally restricted to non-body-slot items on the assumption a body part
    /// could never need "worn over" semantics; wrong per Tad, whose own body mesh (woman_bodyA_Cauc)
    /// ships with a full default arm baked in and needs to hide the separate "arms" slot
    /// (armsCauc/sleevesBlk) until that slot has real content worth showing — a body-slot item
    /// hiding ANOTHER body-slot, not clothing hiding skin. "we should just leave the hider buttons on
    /// for everything" (Tad, 2026-10-01). Any chosen part can carry a
    /// <see cref="IAvatarPart.HiddenBodySlots"/> list; this unions every chosen part's hide-list, then
    /// disables the renderer for each chosen BODY-MESH part (see <see cref="IsBodySlot"/>) whose own
    /// slot lands in that union. "head" is force-excluded no matter what anything lists — per Tad, the
    /// head (and hair, which isn't a body slot at all so it's never touched by this pass) must always
    /// stay visible. Runs AFTER the prune/merge above so every surviving chosen part is already
    /// parented under root under its original object name (FindDeep looks it up by that name).</summary>
    private static void ApplyBodyPartMasking(GameObject root, List<IAvatarPart> chosen)
    {
        var hidden = new HashSet<string>();
        foreach (var p in chosen)
        {
            if (p.HiddenBodySlots == null) continue;
            string own = (p.Slot ?? "").ToLower();
            foreach (var s in p.HiddenBodySlots)
            {
                if (string.IsNullOrEmpty(s)) continue;
                string slot = s.ToLower();
                // A part NEVER hides its own slot. Parts in the same slot are alternatives (one pick per slot), so a garment already
                // replaces the slot's nude "Body" default just by being chosen; ticking its own slot in the AOD hide list used to make
                // it switch ITSELF off in game (found 2026-10-03: Female_Feet_Socks-Gray hid "feet" and the socks vanished).
                if (slot == own || (IsTorsoSlot(slot) && IsTorsoSlot(own))) continue;
                hidden.Add(slot);
            }
        }
        hidden.Remove("head"); // invariant: head is never auto-hidden, regardless of what's configured
        if (hidden.Contains("body")) hidden.Add("torso");   // legacy token and Avatar 2.0 token are the same slot
        if (hidden.Contains("torso")) hidden.Add("body");
        if (hidden.Count == 0) return;

        foreach (var p in chosen)
        {
            if (!(IsBodySlot(p.Slot) || p.Slot == NeckSlot) || !hidden.Contains(p.Slot.ToLower())) continue;
            var t = FindDeep(root.transform, p.ObjectName);
            var rend = t != null ? t.GetComponent<Renderer>() : null;   // skinned OR rigid (neck accessories can be unskinned)
            if (rend != null) rend.enabled = false;
        }
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
            else if (IsExpressionFaceName(nameLower))
            {
                smr.enabled = smr.name.Equals(faceName, System.StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // The legacy mood-swap system owns meshes named gender_face_<expression> (Neutral/Smile/Frown...). The "face" slot is ALSO the Avatar 2.0
    // accessory slot (gag, blindfold, piercings - e.g. Female_Face_Gag_NSFW), so only true expression variants may be switched by it;
    // otherwise it would disable every face accessory the moment the avatar is built.
    private static readonly HashSet<string> ExpressionFaceVariants = new() { "neutral", "smile", "frown", "sad", "happy", "angry", "surprise", "surprised" };
    private static bool IsExpressionFaceName(string lowerName)
    {
        int i = lowerName.IndexOf("_face_");
        if (i < 0) return false;
        string v = lowerName.Substring(i + 6);
        int u = v.IndexOf('_'); if (u >= 0) v = v.Substring(0, u);
        return ExpressionFaceVariants.Contains(v);
    }

    private static AvatarPartLibrary _cachedLib;

    /// <summary>Loads the part library from Resources (cached). Null if it hasn't been scanned yet.</summary>
    public static AvatarPartLibrary LoadLibrary()
    {
        if (_cachedLib == null)
            _cachedLib = Resources.Load<AvatarPartLibrary>("Resource_AvatarSystemAssets/AvatarPartLibrary");
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
        // Mirrors ModularAvatarImporter.ParseName's dotted-name case (e.g. "woman.bodyA") — a body
        // export's main torso mesh, which must also count as a part object here or the prune loop
        // would leave it untouched only by accident rather than by a consistent rule.
        if (name.Contains('.') && !name.Contains('_'))
        {
            var dotSeg = name.Split('.');
            if (dotSeg.Length == 2)
            {
                string dg = dotSeg[0].ToLower();
                if (dg == "male" || dg == "female" || dg == "man" || dg == "woman" || dg == "neutral")
                    return true;
            }
        }

        // Mirrors ModularAvatarImporter.ParseName: "Female_Neck.Collar" (one underscore then a dot) counts as gender_slot.variant.
        string norm = name;
        if (name.IndexOf('_') >= 0 && name.IndexOf('_') == name.LastIndexOf('_'))
        {
            int us = name.IndexOf('_'), dot = name.IndexOf('.', us);
            if (dot > us + 1 && dot < name.Length - 1) norm = name.Substring(0, dot) + "_" + name.Substring(dot + 1);
        }
        var seg = norm.Split('_');
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
        // The "show new items quickly" bias only considers candidates that could actually be PICKED
        // (effective weight > 0). Found 2026-10-01: woman_hair_bobBlonde was unverified AND set to 0% in
        // the AOD, so it was the ONLY "unverified" candidate -> the pool collapsed to just it -> total
        // weight 0 -> WeightedPick returned null -> NO female ever got hair (0 of 200 test builds), while
        // the three hair colors that DID have weight were never even considered. A zero-weight item can
        // never be verified (it's never picked), so it must not be allowed to hijack the pool.
        var unverified = candidates.Where(c => !c.VerifiedInGame && EffectiveWeight(c, role, gender) > 0f).ToList();
        var pool = unverified.Count > 0 ? unverified : candidates;
        var pick = WeightedPick(pool, rng, role, gender);
        if (pick == null) return null; // every candidate in this slot is explicitly zero-weighted

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

    /// <summary>The weight WeightedPick would use for this candidate — AvatarWeightConfig's most
    /// specific rule, else the part's own defaultWeight, clamped at 0. Single definition so the
    /// "unverified bias" in PickVariant and the real pick can never disagree about what "weight 0" means.</summary>
    private static float EffectiveWeight(IAvatarPart p, EmployeeRole? role, string gender)
    {
        var cfg = AvatarWeightConfig.Load();
        float w = cfg != null ? cfg.GetWeight(role, gender, p.Slot, p.Variant, p.DefaultWeight) : p.DefaultWeight;
        return Mathf.Max(0f, w);
    }

    /// <summary>Weighted random selection over a pool that's already been through the
    /// verification/role filtering above. Weight per candidate comes from AvatarWeightConfig's
    /// most-specific (role, gender, slot, variant) rule, falling back to the part's own
    /// defaultWeight when no rule matches or the config asset doesn't exist yet — so this behaves
    /// as plain uniform-by-default selection until the weights UI (Phase 4) actually configures
    /// anything, exactly like the rest of this system.</summary>
    private static IAvatarPart WeightedPick(List<IAvatarPart> pool, System.Random rng, EmployeeRole? role, string gender)
    {
        // NOTE: deliberately no "pool.Count == 1 -> return it unconditionally" shortcut (removed
        // 2026-09-30) — that bypassed weight entirely, so a slot with exactly one candidate (e.g.
        // female "feet" with only woman_feet_bootsBlack defined) always showed up even after being
        // set to 0% in the AOD, ignoring the setting completely. Weight is now always checked
        // regardless of pool size.
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
        // Per Tad (2026-09-30): a slot where every candidate is explicitly zero-weighted means
        // "nothing here right now" — e.g. boots/gloves set to 0% while the body is finished but
        // clothing isn't. Returning null (rather than falling back to a uniform random pick) lets
        // that slot go empty instead of forcing an item the AOD says shouldn't show.
        if (total <= 0f) return null;

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

        var replacement = lib.AllParts.FirstOrDefault(p => p.ObjectName == objectName && DirtyDev.IsVisible(p.Nsfw) &&
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
