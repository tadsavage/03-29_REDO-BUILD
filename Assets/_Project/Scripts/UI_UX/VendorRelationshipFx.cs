using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space green reward callout — "Vendor Relationship +N!" — that rises up and fades out of a
/// truck's tractor the instant it beats an offload Standard (see TruckController.
/// ApplyOffloadStandardBonus / BeginDeparture) and pulls out.
///
/// Same runtime-only construction as FloatingMoneyText (no prefab/scene setup needed), but that
/// class writes through TextMeshPro, which needs an SDF FontAsset — this project has never generated
/// one for Lilita, only the plain Font every UI Toolkit panel already loads via Resources for its own
/// headline text. Legacy UI.Text is the component that actually accepts a plain Font, so this rides a
/// small runtime World Space Canvas instead of TextMeshPro.
/// </summary>
public class VendorRelationshipFx : MonoBehaviour
{
    // ── Tunables ────────────────────────────────────────────────────────────────
    private const float RiseHeight = 2.4f;  // world units the text floats upward over its life
    private const float Lifetime   = 2f;    // seconds before it deletes itself
    private const float FadeStart  = 0.55f; // 0–1 of lifetime where fade begins
    private const int   FontSize   = 48;    // per Tad's explicit ask

    // Same canvas scale/pixel density TruckDoorWaitBar already uses above a truck's cab, so this
    // reads at a consistent size/distance to anything else floating over a truck.
    private const float CanvasLocalScale = 0.0075f;
    private const float CanvasDynamicPixelsPerUnit = 10f;

    private static readonly Color RewardGreen = new Color(0.30f, 0.86f, 0.37f);

    private static Font _lilita;
    private static Font LilitaFont()
    {
        if (_lilita != null) return _lilita;
#if UNITY_EDITOR
        var guids = UnityEditor.AssetDatabase.FindAssets("Lilita t:Font");
        if (guids.Length > 0)
            _lilita = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
        if (_lilita == null) _lilita = Resources.Load<Font>("LilitaOne-Regular");
        return _lilita;
    }

    private Canvas _canvas;
    private Text   _label;
    private Transform _cam;
    private Vector3 _startPos;
    private float _age;

    /// <summary>Spawns the reward callout at <paramref name="worldPos"/> (the truck's tractor,
    /// slightly above roof height) with <paramref name="message"/> — e.g. "Vendor Relationship +5!".
    /// Self-destructs once its lifetime elapses; nothing else needs to track or clean it up.</summary>
    public static VendorRelationshipFx Show(Vector3 worldPos, string message)
    {
        var go = new GameObject("VendorRelationshipFx");
        go.transform.position = worldPos;
        var fx = go.AddComponent<VendorRelationshipFx>();
        fx.Init(message);
        return fx;
    }

    private void Init(string message)
    {
        _startPos = transform.position;

        var canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localScale = Vector3.one * CanvasLocalScale;

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = CanvasDynamicPixelsPerUnit;
        var canvasRect = _canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 150f); // wide enough for the whole line at 48pt

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(canvasGO.transform, false);
        _label = labelGO.AddComponent<Text>();
        _label.font = LilitaFont();
        _label.text = message;
        _label.color = RewardGreen;
        _label.fontSize = FontSize;
        _label.fontStyle = FontStyle.Bold;
        _label.alignment = TextAnchor.MiddleCenter;
        // Overflow (not Wrap/Truncate) so a canvas sized a little tight never clips the message —
        // this is celebratory feedback, it must always read in full.
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        _label.verticalOverflow = VerticalWrapMode.Overflow;
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var outline = labelGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        _cam = Camera.main != null ? Camera.main.transform : null;
        FaceCamera();
    }

    private void FaceCamera()
    {
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
        if (_canvas == null || _cam == null) return;

        // Same "point away from the camera" convention TruckDoorWaitBar/ReceivingFillBar use — a
        // world-space canvas reads correctly with its local +Z pointing away from the viewer.
        Vector3 directionAwayFromCamera = _canvas.transform.position - _cam.position;
        if (directionAwayFromCamera.sqrMagnitude > 0.0001f)
            _canvas.transform.rotation = Quaternion.LookRotation(directionAwayFromCamera);
    }

    private void LateUpdate()
    {
        FaceCamera();

        // Unscaled: this is feedback about something that already happened, so it rises and fades
        // over a fixed REAL duration at any game speed, same reasoning FloatingMoneyText uses.
        _age += Time.unscaledDeltaTime;
        float t = _age / Lifetime;

        // Ease-out rise.
        float eased = 1f - (1f - t) * (1f - t);
        transform.position = _startPos + Vector3.up * (RiseHeight * eased);

        if (t >= FadeStart && _label != null)
        {
            float f = Mathf.InverseLerp(FadeStart, 1f, t);
            Color c = _label.color;
            c.a = 1f - f;
            _label.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
