using UnityEngine;
using System.Collections.Generic;

public class BuildingHighlighter : MonoBehaviour
{
    [Header("Highlights")]
    [SerializeField] private Color validColor = new Color(0.2f, 1.0f, 0.2f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1.0f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color deleteColor = new Color(1.0f, 0.9f, 0.0f, 0.5f); // Updated to Yellow

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
        // Leave ghosted (preview) racks untouched. The highlighter cached this object's REAL
        // materials in Awake — before RackGhost swapped in the ghost material — so highlighting
        // (and its restore) would reveal the real material and undo the ghost. Skip entirely
        // while ghosted; the rack keeps its orange-transparent preview look on hover.
        var ghost = GetComponent<RackGhost>();
        if (ghost != null && ghost.IsGhosted) return;

        if (_rendererData.Count == 0) CacheRenderers();

        if (color.HasValue)
        {
            _mpb.SetColor(BaseColorID, color.Value);
        }

        foreach (var data in _rendererData)
        {
            if (data.renderer == null) continue;
            
            if (highlightMat != null)
            {
                // Create a temporary array of the highlight material for each submesh
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
    }
}
