using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Keeps a placed rack in orange-transparent "preview" state until its aisle is
/// initialized. The rack is a real grid object (occupies cells, is deletable) but is
/// NOT yet registered in any warehouse database — it's a visual placeholder for the
/// player planning their floor. On aisle setup submit, RestoreReal() puts the rack's
/// real materials back.
///
/// TMP label renderers are deliberately left untouched — overwriting their material
/// would wreck the text.
/// </summary>
public class RackGhost : MonoBehaviour
{
    private readonly List<Renderer> _renderers = new();
    private readonly List<Material[]> _originalMaterials = new();
    private bool _ghosted;

    public bool IsGhosted => _ghosted;

    public void ApplyGhost(Material ghostMaterial)
    {
        if (_ghosted || ghostMaterial == null) return;

        _renderers.Clear();
        _originalMaterials.Clear();

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            if (r.GetComponent<TMP_Text>() != null) continue; // never touch labels

            _renderers.Add(r);
            _originalMaterials.Add(r.sharedMaterials);

            var ghostArray = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < ghostArray.Length; i++)
                ghostArray[i] = ghostMaterial;
            r.sharedMaterials = ghostArray;
        }

        _ghosted = true;
    }

    public void RestoreReal()
    {
        if (!_ghosted) return;

        for (int i = 0; i < _renderers.Count; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].sharedMaterials = _originalMaterials[i];
        }

        _renderers.Clear();
        _originalMaterials.Clear();
        _ghosted = false;
    }
}
