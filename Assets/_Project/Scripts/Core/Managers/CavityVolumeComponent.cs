using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Volume-stack-exposed parameters for the cavity/curvature full-screen pass
/// (Cavity_Material06-07, driven by CavityVolumeDriver). Add a "Cavity" override
/// to any VolumeProfile (e.g. the BuildModeOverride profile) to tune or blend
/// these values the same way built-in effects like Depth of Field do.
///
/// Defaults mirror Cavity_Material06-07's current baked-in values, so a scene
/// with no Cavity override anywhere in the stack renders identically to before
/// this component existed.
/// </summary>
[Serializable, VolumeComponentMenu("Custom/Cavity")]
public class CavityVolumeComponent : VolumeComponent, IPostProcessComponent
{
    public BoolParameter enabled = new BoolParameter(true);

    public ClampedFloatParameter intensity = new ClampedFloatParameter(3f, 0f, 10f);
    public ClampedFloatParameter radius = new ClampedFloatParameter(5f, 1f, 10f);
    public ClampedFloatParameter sharpness = new ClampedFloatParameter(0.531f, 0f, 1f);
    public ClampedFloatParameter multiplier = new ClampedFloatParameter(0.75f, 0f, 2f);

    public bool IsActive() => enabled.value;
    public bool IsTileCompatible() => false;
}
