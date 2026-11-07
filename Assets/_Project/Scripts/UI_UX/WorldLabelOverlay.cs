using UnityEngine;

/// <summary>
/// Turns a world-space label canvas into a screen-space OVERLAY canvas that follows the same world point, at the same apparent size.
/// Overlay canvases are drawn after post-processing, so the tilt-shift depth of field never blurs them (Tad, 2026-11-07). The cleaner
/// "draw a layer after post" Render Objects route fails under the render graph with 4x MSAA (sample-count mismatch on
/// _CameraColorAfterPostProcessing), so it is not used. Trade-off: the label draws over walls instead of hiding behind them.
///
/// Call <see cref="Convert"/> once the canvas and its children are built. The canvas keeps its parent (so it is disabled / destroyed with
/// its owner); its original local position becomes the world anchor, its lossy scale the world size of one canvas unit.
/// Any code that rotated the canvas to face the camera must skip that once the canvas is no longer WorldSpace.
/// </summary>
[DisallowMultipleComponent]
public class WorldLabelOverlay : MonoBehaviour
{
    /// <summary>Below the UI Toolkit panels and modals, so a floating label never covers a window.</summary>
    private const int SortingOrder = -100;

    private Transform _anchorParent;
    private Vector3 _anchorLocal;
    private Vector3 _anchorWorld;   // used when the canvas had no parent
    private float _worldPerUnit;    // world metres per canvas unit (the old world-space localScale)
    private RectTransform _panel;
    private Camera _cam;

    public static WorldLabelOverlay Convert(Canvas canvas)
    {
        if (canvas == null) return null;
        var existing = canvas.GetComponent<WorldLabelOverlay>();
        if (existing != null) return existing;

        var rect = canvas.GetComponent<RectTransform>();
        var o = canvas.gameObject.AddComponent<WorldLabelOverlay>();
        o._anchorParent = canvas.transform.parent;
        o._anchorLocal = canvas.transform.localPosition;
        o._anchorWorld = canvas.transform.position;
        o._worldPerUnit = Mathf.Abs(canvas.transform.lossyScale.x);
        Vector2 size = rect.sizeDelta;

        // The overlay canvas becomes screen-sized, so its old contents move into a panel the size the canvas used to be.
        var panelGO = new GameObject("OverlayPanel", typeof(RectTransform));
        o._panel = panelGO.GetComponent<RectTransform>();
        o._panel.SetParent(canvas.transform, false);
        o._panel.anchorMin = o._panel.anchorMax = Vector2.zero;
        o._panel.pivot = new Vector2(0.5f, 0.5f);
        o._panel.sizeDelta = size;
        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = canvas.transform.GetChild(i);
            if (child == o._panel) continue;
            child.SetParent(o._panel, false);
            child.SetAsFirstSibling();   // keep the original draw order (walking backwards)
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        if (scaler != null) { scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1f; }
        o.Follow();
        return o;
    }

    private void LateUpdate() => Follow();

    private void Follow()
    {
        if (_panel == null) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 world = _anchorParent != null ? _anchorParent.TransformPoint(_anchorLocal) : _anchorWorld;
        Vector3 sp = _cam.WorldToScreenPoint(world);
        bool visible = sp.z > _cam.nearClipPlane;
        if (_panel.gameObject.activeSelf != visible) _panel.gameObject.SetActive(visible);
        if (!visible) return;

        // Pixels per world metre at that depth, so the label keeps the size it had in the world.
        float pxPerMetre = _cam.orthographic
            ? Screen.height / (2f * _cam.orthographicSize)
            : Screen.height / (2f * sp.z * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        _panel.position = new Vector3(sp.x, sp.y, 0f);
        _panel.localScale = Vector3.one * (_worldPerUnit * pxPerMetre);
    }
}
