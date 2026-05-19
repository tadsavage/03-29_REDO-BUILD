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
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        _renderers.Clear();
        _originalMaterials.Clear();

        // Collect ALL renderers (MeshRenderer + SkinnedMeshRenderer)
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;

            _renderers.Add(r);
            // CRITICAL FIX: Use sharedMaterial instead of material to avoid creating 
            // a unique material instance per renderer, which breaks batching.
            _originalMaterials.Add(r.sharedMaterial);
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
        // Safety: if renderers were destroyed or changed, re-cache
        if (_renderers.Count == 0) CacheRenderers();

        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;

            // CRITICAL FIX: Use sharedMaterial to maintain batching performance
            r.sharedMaterial = overrideMat != null ? overrideMat : _originalMaterials[i];
        }
    }
}
