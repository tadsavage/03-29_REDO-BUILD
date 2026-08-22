using UnityEngine;
using System.Collections.Generic;

public class BuildingHighlighter : MonoBehaviour
{
    [Header("Highlights")]
    [SerializeField] private Color validColor = new Color(0.2f, 1.0f, 0.2f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1.0f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color deleteColor = new Color(1.0f, 0.9f, 0.0f, 0.5f); // Updated to Yellow

    public static readonly Color GlobalDeleteColor = new Color(1.0f, 0.9f, 0.0f, 0.5f);

    private bool _isHighlighted;

    private readonly List<RendererData> _rendererData = new();
    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private static Material _validMat;
    private static Material _invalidMat;
    private static Material _deleteMat;

    private struct RendererData
    {
        public Renderer renderer;
        public Material[] originalMaterials;
    }

    private void Awake()
    {
        CacheRenderers();
        _mpb = new MaterialPropertyBlock();

        if (_validMat == null) _validMat = Resources.Load<Material>("Materials/Placement-Valid");
        if (_invalidMat == null) _invalidMat = Resources.Load<Material>("Materials/Placement-Invalid");
        if (_deleteMat == null) _deleteMat = Resources.Load<Material>("Materials/Placement-Delete");
    }

    private void CacheRenderers()
    {
        _rendererData.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r != null)
            {
                // CRITICAL FIX: Prevent caching if the current material is already a highlight.
                // Highlight materials are typically loaded from Resources and have distinct names.
                if (r.sharedMaterial != null && (r.sharedMaterial.name.Contains("Placement-") || r.sharedMaterial.name.Contains("Ghost")))
                {
                    continue;
                }

                _rendererData.Add(new RendererData
                {
                    renderer = r,
                    originalMaterials = r.sharedMaterials
                });
            }
        }
    }

    public void HighlightValid(bool on)
    {
        ApplyHighlight(on ? _validMat : null, on ? validColor : (Color?)null);
    }

    public void HighlightInvalid(bool on)
    {
        ApplyHighlight(on ? _invalidMat : null, on ? invalidColor : (Color?)null);
    }

    public void HighlightDelete(bool on)
    {
        ApplyHighlight(on ? _deleteMat : null, on ? GlobalDeleteColor : (Color?)null);
    }

    public void ClearHighlight()
    {
        ApplyHighlight(null, null);
    }

    private void ApplyHighlight(Material highlightMat, Color? color)
    {
        if (highlightMat != null)
        {
            // Only cache original materials if we have none, OR if we are starting a new
            // highlight session while unhighlighted. This ensures we don't accidentally
            // cache a highlight material as 'original', which causes the 'trail' bug.
            if (_rendererData.Count == 0 || !_isHighlighted)
            {
                CacheRenderers();
            }
            _isHighlighted = true;
        }

        if (color.HasValue)
        {
            _mpb.SetColor(BaseColorID, color.Value);
        }

        foreach (var data in _rendererData)
        {
            if (data.renderer == null) continue;
            
            if (highlightMat != null)
            {
                Material[] mats = new Material[data.originalMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = highlightMat;
                
                data.renderer.sharedMaterials = mats;
                data.renderer.SetPropertyBlock(_mpb);
            }
            else
            {
                // Verify we are not restoring a highlight material as original.
                // If originalMaterials was null or corrupted, this could stay yellow.
                if (data.originalMaterials != null)
                {
                    data.renderer.sharedMaterials = data.originalMaterials;
                    data.renderer.SetPropertyBlock(null);
                }
            }
        }

        if (highlightMat == null)
        {
            _isHighlighted = false;
        }
    }
}
