using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades a YardBackdrop group (North/South/East/West/Corner_*) down to a low, translucent alpha
/// when the camera sits on its far side looking back toward the play area — the camera-to-focal-
/// point sightline would otherwise punch straight through a painted backdrop flat with no warning.
/// Fades per GROUP (each immediate child of this transform, e.g. "North" with its North_1..North_7
/// depth pieces) rather than per individual flat, matching how the art is laid out — the pieces
/// under one direction share a single silhouette.
///
/// Fades toward a low alpha rather than fully vanishing, and toward alpha rather than swapping to
/// a shared "ghost" material (compare WallSeeThrough), because every backdrop material already
/// carries its own painted art (mountains on one side, rooftops on another) that a uniform ghost
/// material would erase — a faint silhouette reads better than a flat placeholder or a hole.
///
/// Needs zero shader changes at runtime: every backdrop material (Mat_EnvironmentNatureBackdrop
/// and its siblings) is already authored as URP Lit, Surface Type Transparent, alpha blend, with
/// _BaseColor.a = 1 — fading is just driving that alpha down per renderer through a
/// MaterialPropertyBlock, which never touches the shared material asset.
/// </summary>
public class YardBackdropFade : MonoBehaviour
{
    [Tooltip("Alpha applied to a group whose sightline to the focal point is blocked by it.")]
    [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.2f;

    [Tooltip("Alpha change per second when transitioning, so the fade isn't a hard pop.")]
    [SerializeField] private float fadeSpeed = 2.5f;

    [Tooltip("How often the occlusion sightline is re-tested, in seconds.")]
    [SerializeField] private float scanInterval = 0.1f;

    [Tooltip("Stop the sightline this far short of the focal point — mirrors WallSeeThrough's own " +
             "stopShortOfFocus, so a backdrop piece directly behind what the player is looking AT " +
             "isn't caught by its own target.")]
    [SerializeField] private float stopShortOfFocus = 3f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId     = Shader.PropertyToID("_Color");

    private class Group
    {
        public Bounds bounds;
        public Renderer[] renderers;
        public Color[] baseColors;
        public float currentAlpha = 1f;
        public float targetAlpha  = 1f;
    }

    private FreeLookCamera _camera;
    private readonly List<Group> _groups = new List<Group>();
    private MaterialPropertyBlock _mpb;
    private float _nextScanTime;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        BuildGroups();
    }

    private void BuildGroups()
    {
        _groups.Clear();

        foreach (Transform child in transform)
        {
            var renderers = child.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) continue;

            var bounds = renderers[0].bounds;
            var baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
                var mat = renderers[i].sharedMaterial;
                baseColors[i] = mat != null && mat.HasProperty(BaseColorId)
                    ? mat.GetColor(BaseColorId)
                    : Color.white;
            }
            // Guard against paper-thin backdrop flats (near-zero depth) missing the sightline test.
            bounds.Expand(0.5f);

            _groups.Add(new Group { bounds = bounds, renderers = renderers, baseColors = baseColors });
        }
    }

    private void Update()
    {
        if (_camera == null)
        {
            _camera = FindAnyObjectByType<FreeLookCamera>();
            if (_camera == null) return;
        }

        if (Time.unscaledTime >= _nextScanTime)
        {
            _nextScanTime = Time.unscaledTime + scanInterval;
            Rescan();
        }

        ApplyFades();
    }

    private void Rescan()
    {
        Vector3 origin    = _camera.transform.position;
        Vector3 toFocus   = _camera.FocalPoint - origin;
        float fullLength  = toFocus.magnitude;
        float length      = fullLength - stopShortOfFocus;

        if (length <= 0.01f)
        {
            foreach (var g in _groups) g.targetAlpha = 1f;
            return;
        }

        var ray = new Ray(origin, toFocus / fullLength);

        foreach (var g in _groups)
            g.targetAlpha = (g.bounds.IntersectRay(ray, out float hitDist) && hitDist <= length)
                ? fadedAlpha
                : 1f;
    }

    private void ApplyFades()
    {
        float step = fadeSpeed * Time.unscaledDeltaTime;

        foreach (var g in _groups)
        {
            if (Mathf.Approximately(g.currentAlpha, g.targetAlpha)) continue;
            g.currentAlpha = Mathf.MoveTowards(g.currentAlpha, g.targetAlpha, step);

            for (int i = 0; i < g.renderers.Length; i++)
            {
                var r = g.renderers[i];
                if (r == null) continue;

                var c = g.baseColors[i];
                c.a = g.currentAlpha;

                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                _mpb.SetColor(ColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
