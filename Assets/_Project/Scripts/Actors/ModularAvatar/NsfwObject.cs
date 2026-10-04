using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put this on ANY object in the game to tag it NSFW. While <see cref="DirtyDev"/> is OFF every renderer under the object is switched off,
/// so it is invisible, but the object itself stays alive and in place (its scripts, colliders and saved data are untouched), and turning
/// Dirty Dev back ON shows it again. Nothing is destroyed or deactivated.
///
/// (Avatar parts are tagged in the AOD instead of with this component: the assembler simply never picks a hidden part.)
/// </summary>
[DisallowMultipleComponent]
public class NsfwObject : MonoBehaviour
{
    private readonly List<(Renderer r, bool wasEnabled)> _renderers = new();
    private bool _applied;

    private void OnEnable()
    {
        DirtyDev.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        DirtyDev.Changed -= Apply;
        Restore();
    }

    private void Apply()
    {
        if (DirtyDev.Enabled) { Restore(); return; }
        if (_applied) return;
        _renderers.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            _renderers.Add((r, r.enabled));
            r.enabled = false;
        }
        _applied = true;
    }

    private void Restore()
    {
        if (!_applied) return;
        foreach (var (r, was) in _renderers)
            if (r != null) r.enabled = was;
        _renderers.Clear();
        _applied = false;
    }

    // A renderer added later (instantiated child) while hidden would show - re-apply cheaply when the hierarchy changes.
    private void OnTransformChildrenChanged()
    {
        if (!DirtyDev.Enabled && _applied) { Restore(); Apply(); }
    }
}
