using UnityEngine;
using System.Collections.Generic;

public class BuildingHighlighter : MonoBehaviour
{
    [Header("Highlights")]
    [SerializeField] private Color validColor = new Color(0.2f, 1.0f, 0.2f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1.0f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color deleteColor = new Color(1.0f, 0.5f, 0.2f, 0.5f);

    private readonly List<Renderer> _renderers = new();
    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        CacheRenderers();
        _mpb = new MaterialPropertyBlock();
    }

    private void CacheRenderers()
    {
        _renderers.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r != null) _renderers.Add(r);
        }
    }

    public void HighlightValid(bool on)
    {
        ApplyHighlight(on ? validColor : (Color?)null);
    }

    public void HighlightInvalid(bool on)
    {
        ApplyHighlight(on ? invalidColor : (Color?)null);
    }

    public void HighlightDelete(bool on)
    {
        ApplyHighlight(on ? deleteColor : (Color?)null);
    }

    public void ClearHighlight()
    {
        ApplyHighlight(null);
    }

    private void ApplyHighlight(Color? color)
    {
        if (_renderers.Count == 0) CacheRenderers();

        if (color.HasValue)
        {
            _mpb.SetColor(BaseColorID, color.Value);
        }

        foreach (var r in _renderers)
        {
            if (r == null) continue;
            
            if (color.HasValue)
                r.SetPropertyBlock(_mpb);
            else
                r.SetPropertyBlock(null);
        }
    }
}
