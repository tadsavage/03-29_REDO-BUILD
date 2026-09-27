using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A submitted/finalized avatar part — one standalone .asset file per part, living under
/// ModularAvatarFinalizer.FinalizedAssetFolder. Created only by ModularAvatarFinalizer.TryFinalize
/// (never hand-created), one file per objectName so re-submitting the same part always overwrites the
/// same asset rather than duplicating it.
///
/// Deliberately a SEPARATE ScriptableObject per part (not embedded in AvatarPartLibrary like the raw
/// Part class) so multiple people/sessions submitting different parts don't collide on one shared
/// file's diff — the exact git-merge pain that motivated this split in the first place.
/// </summary>
public class AvatarPartAsset : ScriptableObject, IAvatarPart
{
    [SerializeField] private string objectName;
    [SerializeField] private string gender;
    [SerializeField] private string slot;
    [SerializeField] private string variant;

    // Direct reference to the finalized, game-ready prefab this part resolves to — NOT a sourceIndex
    // into AvatarPartLibrary.sources, which is fully rebuilt on every rescan and unstable across them.
    [SerializeField] private GameObject finalizedPrefab;

    public List<EmployeeRole> allowedRoles = new();
    public List<AvatarPartLibrary.ColorVariant> colorVariants = new();
    public float defaultWeight = 1f;
    public bool verifiedInGame = false;

    public string ObjectName => objectName;
    public string Gender { get => gender; set => gender = value; }
    public string Slot => slot;
    public string Variant => variant;
    public GameObject FinalizedPrefab => finalizedPrefab;
    public List<EmployeeRole> AllowedRoles => allowedRoles;
    public List<AvatarPartLibrary.ColorVariant> ColorVariants => colorVariants;
    public float DefaultWeight { get => defaultWeight; set => defaultWeight = value; }

    // Existence in AvatarPartLibrary.finalizedParts IS "reviewed" — there is no in-between state for
    // an AvatarPartAsset, unlike the raw Part class's mutable boolean.
    public bool MetadataReviewed => true;
    public bool VerifiedInGame { get => verifiedInGame; set => verifiedInGame = value; }
    public bool AllowsRole(EmployeeRole role) => allowedRoles.Count == 0 || allowedRoles.Contains(role);

    /// <summary>Set once at creation time by ModularAvatarFinalizer (an Editor-only assembly this
    /// runtime class has no reference to, so this can't be `internal` — it's a public API by
    /// necessity, not an invitation to call it elsewhere). Identity fields are never hand-edited
    /// afterward (same rule as the raw Part class's read-only slot/objectName/variant).</summary>
    public void SetIdentity(string objName, string g, string s, string v)
    {
        objectName = objName;
        gender = g;
        slot = s;
        variant = v;
    }

    /// <summary>See <see cref="SetIdentity"/> — public only because ModularAvatarFinalizer, its one
    /// intended caller, lives in a separate Editor-only assembly.</summary>
    public void SetPrefab(GameObject prefab) => finalizedPrefab = prefab;
}
