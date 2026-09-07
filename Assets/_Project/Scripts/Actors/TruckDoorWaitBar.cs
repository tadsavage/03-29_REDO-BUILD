using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// World-space status banner that floats above a truck's cab through its whole yard lifecycle — same
/// visual language as ReceivingFillBar (billboard, horizontal fill), but built entirely in code rather
/// than from a prefab: this session has no live Unity Editor connection to author/verify a new prefab
/// asset, and a truck spawns at runtime with no dedicated prefab slot to hold one anyway (same reasoning
/// ReceivingFillBar itself gives for spawning its own canvas rather than being hand-wired).
///
/// Global namespace to match TruckController, the only thing that ever adds this component.
///
/// ⚠️ NOT visually verified — sizing/scale/anchoring below are reasonable starting guesses, not
/// editor-measured values. Tune _canvas's localScale/sizeDelta and the label font size in-editor once
/// a truck actually parks and waits.
/// </summary>
public class TruckDoorWaitBar : MonoBehaviour
{
    // One color per yard-lifecycle status, applied to both the fill bar and the frame so the whole
    // modal always reads as a single colored unit.
    private static readonly Color HeadingToDoorColor = new Color(0.25f, 0.65f, 0.25f, 1f); // green — has a door
    private static readonly Color NoDoorColor = new Color(0.75f, 0.2f, 0.2f, 1f);           // red — no door yet
    private static readonly Color DepartingColor = new Color(0.2f, 0.45f, 0.85f, 1f);       // blue — leaving

    // How far below the canvas's own bottom edge (0) the label's anchor extends, in the canvas's
    // normalized 0-1 anchor space. Kept in sync with labelRect.anchorMin.y below so the background
    // and frame both stretch far enough down to stay behind the wrapped label text.
    private const float LabelOverflowAnchorY = -0.25f;
    private const float FrameThickness = 3f; // in canvas units (canvas is 240x60 units before scale)

    private Canvas _canvas;
    private Image _fillImage;
    private TextMeshProUGUI _label;
    private Camera _mainCamera;
    private readonly List<Image> _frameEdges = new List<Image>();

    // BUG FIX: confirmed live via a controlled isolated test — Image.Type.Filled does NOT clip at
    // all without a Sprite assigned in this project's render setup; it just draws the full rect
    // regardless of fillAmount (set fillAmount=0.25 on a throwaway test canvas and it still rendered
    // 100% filled). _fillImage below is built entirely at runtime via AddComponent<Image>(), which
    // leaves sprite null — this 1x1 white sprite, generated once and shared, is the minimal fix that
    // lets the fill-clipping geometry actually generate.
    private static Sprite _solidSprite;
    private static Sprite SolidSprite()
    {
        if (_solidSprite != null) return _solidSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _solidSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return _solidSprite;
    }

    private void EnsureBuilt()
    {
        if (_canvas != null) return;

        _mainCamera = Camera.main;

        var canvasGO = new GameObject("TruckDoorWaitCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, 4.2f, 0f); // above the cab roof
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one * 0.0075f; // 3x the previous 0.0025f size, per request

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        var rect = _canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(360f, 60f); // widened from 240 — the longer status messages
                                                  // ("...My dispatcher is gonna hear about this!")
                                                  // need more room per line

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        var bgRect = bgGO.GetComponent<RectTransform>();
        // Stretched down to LabelOverflowAnchorY (instead of stopping at 0) so the background stays
        // behind the label even where the label's own anchor extends below the canvas's bottom edge.
        bgRect.anchorMin = new Vector2(0f, LabelOverflowAnchorY); bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        var fillGO = new GameObject("FillBar");
        fillGO.transform.SetParent(canvasGO.transform, false);
        _fillImage = fillGO.AddComponent<Image>();
        _fillImage.sprite = SolidSprite(); // required for Type.Filled to actually clip — see SolidSprite()
        _fillImage.color = NoDoorColor; // default; each Show*/status call sets the real color
        _fillImage.type = Image.Type.Filled;
        _fillImage.fillMethod = Image.FillMethod.Horizontal;
        _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0.04f, 0.55f);
        fillRect.anchorMax = new Vector2(0.96f, 0.85f);
        fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(canvasGO.transform, false);
        _label = labelGO.AddComponent<TextMeshProUGUI>();
        // Top (not Center) — the label's box now extends well below the canvas to leave room for
        // long wrapped messages, but the text itself should sit flush against the box's top edge
        // (i.e. right underneath the fill bar), not centered in the whole tall box.
        _label.alignment = TextAlignmentOptions.Top;
        _label.color = Color.white;
        _label.textWrappingMode = TextWrappingModes.Normal;
        // BUG FIX: confirmed live — the longer status messages ("Waiting for Door. I will wait for
        // N more minutes and then leave. My dispatcher is gonna hear about this!") spilled out past
        // the frame on both sides at a fixed fontSize=20, regardless of word-wrap being on. Auto-
        // sizing is the robust fix: TMP now shrinks the font as needed so ANY message, however long,
        // is guaranteed to fit inside the label's own box instead of overflowing it.
        _label.enableAutoSizing = true;
        _label.fontSizeMin = 8f;
        _label.fontSizeMax = 20f;
        var labelRect = labelGO.GetComponent<RectTransform>();
        // Top anchor sits right against the fill bar's own bottom edge (0.55) so there's no dead gap
        // between them; extended below the canvas's own bottom edge on the low end — the wrapped
        // 2-line status text (e.g. the long "Waiting for Door..." message) needs more room than the
        // canvas's nominal height before it needs to grow upward into the fill bar.
        labelRect.anchorMin = new Vector2(0.02f, LabelOverflowAnchorY);
        labelRect.anchorMax = new Vector2(0.98f, 0.55f);
        labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;

        BuildFrame(canvasGO.transform);

        canvasGO.SetActive(false);
    }

    /// <summary>
    /// Adds a thin border around the modal's full extent (matching the background's stretched bounds),
    /// initially colored to match the fill bar's default — SetColor keeps them in sync afterward.
    /// </summary>
    private void BuildFrame(Transform canvasTransform)
    {
        var frameGO = new GameObject("Frame");
        frameGO.transform.SetParent(canvasTransform, false);
        var frameRect = frameGO.AddComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0f, LabelOverflowAnchorY);
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero; frameRect.offsetMax = Vector2.zero;

        _frameEdges.Clear();
        _frameEdges.Add(CreateFrameEdge(frameGO.transform, "FrameTop",
            anchorMin: new Vector2(0f, 1f), anchorMax: Vector2.one,
            offsetMin: new Vector2(0f, -FrameThickness), offsetMax: Vector2.zero));
        _frameEdges.Add(CreateFrameEdge(frameGO.transform, "FrameBottom",
            anchorMin: Vector2.zero, anchorMax: new Vector2(1f, 0f),
            offsetMin: Vector2.zero, offsetMax: new Vector2(0f, FrameThickness)));
        _frameEdges.Add(CreateFrameEdge(frameGO.transform, "FrameLeft",
            anchorMin: Vector2.zero, anchorMax: new Vector2(0f, 1f),
            offsetMin: Vector2.zero, offsetMax: new Vector2(FrameThickness, 0f)));
        _frameEdges.Add(CreateFrameEdge(frameGO.transform, "FrameRight",
            anchorMin: new Vector2(1f, 0f), anchorMax: Vector2.one,
            offsetMin: new Vector2(-FrameThickness, 0f), offsetMax: Vector2.zero));
    }

    /// <summary>Creates one thin, full-length edge strip of a border and returns its Image.</summary>
    private static Image CreateFrameEdge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = NoDoorColor;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return img;
    }

    /// <summary>Recolors the fill bar and every frame edge together, so the modal always reads as one
    /// colored unit for its current status.</summary>
    private void SetColor(Color color)
    {
        if (_fillImage != null) _fillImage.color = color;
        for (int i = 0; i < _frameEdges.Count; i++)
            if (_frameEdges[i] != null) _frameEdges[i].color = color;
    }

    /// <summary>Ensures the canvas is built and active before any status is shown.</summary>
    private void PrepareToShow()
    {
        EnsureBuilt();
        if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);
    }

    /// <summary>Truck has a door assigned and is heading straight to it — shown the moment it clears
    /// the gate, and again once a door frees up for a truck that was waiting.</summary>
    public void ShowHeadingToDoor(int doorNumber)
    {
        PrepareToShow();
        SetColor(HeadingToDoorColor);
        if (_fillImage != null) _fillImage.fillAmount = 1f;
        if (_label != null) _label.text = $"Heading to Door {doorNumber}";
        FaceCamera();
    }

    /// <summary>Truck has no door assigned and is heading to the side lot (or generic wait point) —
    /// shown the moment it clears the gate.</summary>
    public void ShowHeadingToSideLot()
    {
        PrepareToShow();
        SetColor(NoDoorColor);
        if (_fillImage != null) _fillImage.fillAmount = 1f;
        if (_label != null) _label.text = "Heading to Side Lot";
        FaceCamera();
    }

    /// <summary>Truck is parked and waiting for a door to free up. <paramref name="minutesRemaining"/>
    /// and <paramref name="minutesTotal"/> are in-game SIM minutes, not real seconds.</summary>
    public void ShowWaitingForDoor(float minutesRemaining, float minutesTotal)
    {
        PrepareToShow();
        SetColor(NoDoorColor);

        // BUG FIX (Tad's spec): "the fill rate bar... is supposed to go down to reflect running out
        // of time" — this used to compute 1 - remaining/total, which FILLS UP toward full as the
        // deadline approaches. A depleting bar (starts full, drains to empty at the deadline) reads
        // correctly as "running out," so it's remaining/total instead.
        float frac = minutesTotal > 0f ? Mathf.Clamp01(minutesRemaining / minutesTotal) : 0f;
        if (_fillImage != null) _fillImage.fillAmount = frac;

        int mins = Mathf.CeilToInt(Mathf.Max(0f, minutesRemaining));
        if (_label != null)
            _label.text = $"Waiting for Door. I will wait for {mins} more minute{(mins == 1 ? "" : "s")} " +
                           "and then leave. My dispatcher is gonna hear about this!";

        FaceCamera();
    }

    /// <summary>Truck has finished offloading and is departing the yard.</summary>
    public void ShowDeparting(int casesReceived, int casesExpected)
    {
        PrepareToShow();
        SetColor(DepartingColor);
        if (_fillImage != null) _fillImage.fillAmount = 1f;
        if (_label != null)
            _label.text = $"My trailer is offloaded, I am departing. {casesReceived} out of {casesExpected} cases received.";
        FaceCamera();
    }

    public void Hide()
    {
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    private void FaceCamera()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_canvas == null || _mainCamera == null) return;

        // Same "point away from the camera" convention ReceivingFillBar uses — a world-space canvas
        // reads correctly with its local +Z pointing away from the viewer.
        Vector3 directionAwayFromCamera = _canvas.transform.position - _mainCamera.transform.position;
        if (directionAwayFromCamera.sqrMagnitude > 0.0001f)
            _canvas.transform.rotation = Quaternion.LookRotation(directionAwayFromCamera);
    }

    private void LateUpdate()
    {
        if (_canvas != null && _canvas.gameObject.activeSelf) FaceCamera();
    }
}
