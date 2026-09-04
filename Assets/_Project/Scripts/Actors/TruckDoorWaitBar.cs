using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// World-space fill bar + label that floats above a truck's cab while it waits for a free door — same
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
    private Canvas _canvas;
    private Image _fillImage;
    private TextMeshProUGUI _label;
    private Camera _mainCamera;

    private void EnsureBuilt()
    {
        if (_canvas != null) return;

        _mainCamera = Camera.main;

        var canvasGO = new GameObject("TruckDoorWaitCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = new Vector3(0f, 4.2f, 0f); // above the cab roof
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one * 0.01f;

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        var rect = _canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240f, 60f);

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        var fillGO = new GameObject("FillBar");
        fillGO.transform.SetParent(canvasGO.transform, false);
        _fillImage = fillGO.AddComponent<Image>();
        // Amber, not red — this is a normal "still waiting" state, not an error.
        _fillImage.color = new Color(0.85f, 0.65f, 0.15f, 1f);
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
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontSize = 20f;
        _label.color = Color.white;
        _label.enableWordWrapping = true;
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.02f, 0.05f);
        labelRect.anchorMax = new Vector2(0.98f, 0.5f);
        labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;

        canvasGO.SetActive(false);
    }

    /// <summary>Shows (or updates) the bar. <paramref name="minutesRemaining"/> and
    /// <paramref name="minutesTotal"/> are in-game SIM minutes, not real seconds.</summary>
    public void Show(float minutesRemaining, float minutesTotal)
    {
        EnsureBuilt();
        if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);

        float frac = minutesTotal > 0f ? Mathf.Clamp01(1f - minutesRemaining / minutesTotal) : 0f;
        if (_fillImage != null) _fillImage.fillAmount = frac;

        int mins = Mathf.CeilToInt(Mathf.Max(0f, minutesRemaining));
        if (_label != null)
            _label.text = $"Driver is waiting {mins} minute{(mins == 1 ? "" : "s")} more";

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
