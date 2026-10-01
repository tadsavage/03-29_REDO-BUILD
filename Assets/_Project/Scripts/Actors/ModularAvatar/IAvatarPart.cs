using System.Collections.Generic;

/// <summary>
/// Common shape shared by a raw, not-yet-reviewed scan result (<see cref="AvatarPartLibrary.Part"/>)
/// and a submitted/finalized part (<see cref="AvatarPartAsset"/>). Everything downstream of the AOD
/// (the assembler, the preview stage, EmployeeSpawner's availability check) works against this
/// interface so it doesn't care which stage of the pipeline a given part is in.
///
/// Slot/ObjectName/Variant are read-only everywhere — they're derived from the source file's naming
/// convention at scan time and are never hand-edited (changing them would change which bone the
/// assembler attaches the part to, a Blender-side authoring decision, not a metadata tweak).
/// </summary>
public interface IAvatarPart
{
    string ObjectName { get; }
    string Gender { get; set; }
    string Slot { get; }
    string Variant { get; }
    List<EmployeeRole> AllowedRoles { get; }
    List<AvatarPartLibrary.ColorVariant> ColorVariants { get; }
    float DefaultWeight { get; set; }

    /// <summary>Body-part slots (from <see cref="ModularAvatarAssembler.BodySlots"/>) this part hides
    /// when worn — e.g. coveralls hide "body"/"arms"/"legs" so they don't clip through the cloth.
    /// Only meaningful for a clothing part (a part whose OWN slot is not itself a body slot); empty
    /// for a body part. "head" is never respected here even if present — see
    /// ModularAvatarAssembler.Build's masking pass.</summary>
    List<string> HiddenBodySlots { get; }

    /// <summary>True once this part has actually been reviewed/submitted through the AOD. For a raw
    /// <see cref="AvatarPartLibrary.Part"/> this is always false (submitting one REMOVES it from the
    /// raw list and produces an <see cref="AvatarPartAsset"/> instead — see AODPanel.Submit); for an
    /// AvatarPartAsset it's always true (existence in the finalized list IS "reviewed").</summary>
    bool MetadataReviewed { get; }
    bool VerifiedInGame { get; set; }
    bool AllowsRole(EmployeeRole role);
}
