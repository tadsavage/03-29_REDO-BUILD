using UnityEngine;
using System.Collections.Generic;

public class BuildingHighlighter : MonoBehaviour
{
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;
    [SerializeField] private Material deleteMaterial;

    private readonly List<Renderer> _renderers = new();
    private readonly List<Material> _originalMaterials = new();

    void Awake()
    {
        // Collect ALL renderers (MeshRenderer + SkinnedMeshRenderer)
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r)
                continue;

            _renderers.Add(r);
            _originalMaterials.Add(r.material);
        }
    }

    public void HighlightValid(bool on)
    {
        SetMaterial(on ? validMaterial : null);
    }

    public void HighlightInvalid(bool on)
    {
        SetMaterial(on ? invalidMaterial : null);
    }

    public void HighlightDelete(bool on)
    {
        SetMaterial(on ? deleteMaterial : null);
    }

    private void SetMaterial(Material overrideMat)
    {
        // Clean out destroyed renderers
        for (int i = _renderers.Count - 1; i >= 0; i--)
        {
            if (_renderers[i] == null || !_renderers[i])
            {
                _renderers.RemoveAt(i);
                _originalMaterials.RemoveAt(i);
            }
        }

        // Apply materials safely
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];

            if (r == null || !r)
                continue;

            r.material = overrideMat != null ? overrideMat : _originalMaterials[i];
        }
    }
}
