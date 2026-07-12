using UnityEngine;
using System.Collections.Generic;

public class BuildingHighlighter : MonoBehaviour
{
    [Header("Highlights")]
    [SerializeField] private Color validColor = new Color(0.2f, 1.0f, 0.2f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1.0f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color deleteColor = new Color(1.0f, 0.9f, 0.0f, 0.5f); // Updated to Yellow

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
        ApplyHighlight(on ? _deleteMat : null, on ? deleteColor : (Color?)null);
    }

    public void ClearHighlight()
    {
        ApplyHighlight(null, null);
    }

    private void ApplyHighlight(Material highlightMat, Color? color)
    {
        // For dynamic objects (Pallets with changing cases), we need to refresh the renderer list.
        // However, we MUST only cache when we are NOT currently highlighted.
        // If we cache while the red highlight is active, the "original" materials will be saved as RED,
        // causing the highlight to get stuck forever as shown in your image.
        bool isPallet = GetComponent<PalletBuilder>() != null;

        if (highlightMat != null)
        {
            // Only cache if empty or if starting a new highlight session on a dynamic object.
            if (_rendererData.Count == 0 || (isPallet && !_isHighlighted))
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
                data.renderer.sharedMaterials = data.originalMaterials;
                data.renderer.SetPropertyBlock(null);
            }
        }

        if (highlightMat == null)
        {
            _isHighlighted = false;
        }
    }
}
