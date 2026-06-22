using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]  
public class NavAgentGuidance : MonoBehaviour
{
    public enum GuidanceColorType
    {
        White, Red, Green, Blue, Yellow, Cyan, Magenta, Black
    }

    [Header("Visibility")]
    [Tooltip("Turn the guidance line on or off for this agent.")]
    public bool showGuidanceLine = true;

    [Header("Visuals")]
    [Tooltip("Choose the color of the guidance line.")]
    public GuidanceColorType colorType = GuidanceColorType.Green;
    
    [Tooltip("Width of the guidance line.")]
    [Range(0.01f, 1.0f)] public float lineWidth = 0.1f;

    [Tooltip("Dashed line effect.")]
    public bool isDashed = false;

    [Tooltip("Smoothing level of the line.")]
    [SerializeField] private int pointsPerSegment = 10;

    [Tooltip("Vertical offset from the ground to prevent Z-fighting.")]
    [SerializeField] private float verticalOffset = 0.05f;

    [Header("Material (Auto-assigned)")]
    [SerializeField] private Material guidanceMaterial;

    private NavMeshAgent _agent;
    private LineRenderer _lineRenderer;
    private Material _instanceMaterial;
    
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
        {
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        // Setup LineRenderer
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;
        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth;
        _lineRenderer.textureMode = LineTextureMode.Tile;
        _lineRenderer.alignment = LineAlignment.View;
        
        if (guidanceMaterial != null)
        {
            _instanceMaterial = new Material(guidanceMaterial);
            _lineRenderer.material = _instanceMaterial;
        }
    }

    private void Update()
    {
        UpdateLineVisibility();
        if (showGuidanceLine && _lineRenderer.enabled)
        {
            UpdateLineColor();
            UpdateLineVisuals();
            DrawPath();
        }
    }

    private void UpdateLineVisibility()
    {
        // Only show if the toggle is on and the agent actually has a path
        _lineRenderer.enabled = showGuidanceLine && _agent.hasPath && _agent.path.corners.Length >= 2;
    }

    private void UpdateLineColor()
    {
        if (_instanceMaterial == null) return;
        Color c = GetColor(colorType);
        
        // Shader uses _Color and _color2 for its motion rendering effect
        _instanceMaterial.SetColor("_Color", c);
        
        if (_instanceMaterial.HasProperty("_color2"))
        {
            // Set secondary color with a bit of transparency/darkness for depth
            Color c2 = c;
            c2.a *= 0.8f;
            _instanceMaterial.SetColor("_color2", c2);
        }
    }

    private void UpdateLineVisuals()
    {
        if (_lineRenderer.startWidth != lineWidth)
        {
            _lineRenderer.startWidth = lineWidth;
            _lineRenderer.endWidth = lineWidth;
        }

        if (_instanceMaterial != null)
        {
            if (isDashed)
            {
                // Tune these values for a nice dashed effect
                _instanceMaterial.SetFloat("_Length", 30.0f);
                _instanceMaterial.SetFloat("_DotSize", 40.0f);
                _instanceMaterial.SetFloat("_Reveal", 0.6f);
            }
            else
            {
                // Standard continuous line
                _instanceMaterial.SetFloat("_Length", 1.0f);
                _instanceMaterial.SetFloat("_DotSize", 1.0f);
                _instanceMaterial.SetFloat("_Reveal", 1.0f);
            }
        }
    }

    private Color GetColor(GuidanceColorType type)
    {
        switch (type)
        {
            case GuidanceColorType.Red: return Color.red;
            case GuidanceColorType.Green: return Color.green;
            case GuidanceColorType.Blue: return Color.blue;
            case GuidanceColorType.Yellow: return Color.yellow;
            case GuidanceColorType.Cyan: return Color.cyan;
            case GuidanceColorType.Magenta: return Color.magenta;
            case GuidanceColorType.White: return Color.white;
            case GuidanceColorType.Black: return Color.black;
            default: return Color.green;
        }
    }

    private void DrawPath()
    {
        Vector3[] corners = _agent.path.corners;
        
        List<Vector3> smoothedPoints = new List<Vector3>();
        Vector3 offset = new Vector3(0, verticalOffset, 0);
        
        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 p0 = i == 0 ? corners[i] : corners[i - 1];
            Vector3 p1 = corners[i];
            Vector3 p2 = corners[i + 1];
            Vector3 p3 = i == corners.Length - 2 ? corners[i + 1] : corners[i + 2];

            for (int j = 0; j < pointsPerSegment; j++)
            {
                float t = (float)j / (pointsPerSegment - 1);
                // Don't add the very last point of a segment if it's not the final segment to avoid duplication
                if (j == pointsPerSegment - 1 && i < corners.Length - 2) continue;
                
                smoothedPoints.Add(CatmullRom(p0, p1, p2, p3, t) + offset);
            }
        }

        _lineRenderer.positionCount = smoothedPoints.Count;
        _lineRenderer.SetPositions(smoothedPoints.ToArray());
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2.0f * p1) +
            (-p0 + p2) * t +
            (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
            (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3);
    }
}
