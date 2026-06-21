using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Full-screen loading overlay. Matches the dark-blue/orange/light-blue palette of the main menu.
/// Auto-spawns when the "Main" game scene starts (via RuntimeInitializeOnLoadMethod).
/// Also spawnable from MainMenuManager for async transitions.
/// Hooks NavMeshManager.OnNavMeshReady to auto-complete the fill and fade out.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    // ── Palette — matches MainMenu.uss ────────────────────────────────
    private static readonly Color BG_COLOR     = FromHex("1E262E");   // deep navy
    private static readonly Color BORDER_COLOR = FromHex("5C9BC4");   // light blue border
    private static readonly Color FILL_COLOR   = FromHex("B5743A");   // orange fill
    private static readonly Color TRIM_COLOR   = FromHex("7A4C22");   // dark orange (bottom trim)
    private static readonly Color TEXT_COLOR   = FromHex("EAF4FF");   // near-white title

    private const float BAR_W  = 680f;
    private const float BAR_H  = 36f;
    private const float FORK_W = 114f;
    private const float FORK_H = 75f;

    private CanvasGroup   _group;
    private RectTransform _fillRect;
    private RectTransform _forkRect;

    private float _targetProgress;
    private float _displayProgress;
    private bool  _completing;

    // ── Bootstrap: auto-spawn when Main scene loads ───────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "Main") return;
        if (Instance != null) return;
        Spawn();
    }

    /// <summary>Create the loading screen. Safe to call more than once — returns the existing instance.</summary>
    public static LoadingScreenManager Spawn()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[LoadingScreen]");
        DontDestroyOnLoad(go);
        return go.AddComponent<LoadingScreenManager>();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCanvas();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>Push progress forward (0–1). Never goes backwards.</summary>
    public void SetProgress(float t)
        => _targetProgress = Mathf.Max(_targetProgress, Mathf.Clamp01(t));

    /// <summary>Fill to 100 % then fade out.</summary>
    public void CompleteAndHide()
    {
        if (!_completing) StartCoroutine(CompleteRoutine());
    }

    // ── Update ────────────────────────────────────────────────────────

    private void Update()
    {
        if (_completing) return;
        _displayProgress = Mathf.MoveTowards(
            _displayProgress, _targetProgress, Time.unscaledDeltaTime * 0.55f);
        DriveBar(_displayProgress);
    }

    // ── Complete routine ──────────────────────────────────────────────

    private IEnumerator CompleteRoutine()
    {
        _completing = true;

        // Sprint to 100 %
        while (_displayProgress < 1f)
        {
            _displayProgress = Mathf.MoveTowards(
                _displayProgress, 1f, Time.unscaledDeltaTime * 2.0f);
            DriveBar(_displayProgress);
            yield return null;
        }
        DriveBar(1f);
        yield return new WaitForSecondsRealtime(0.4f);

        // Fade out
        float a = 1f;
        while (a > 0f)
        {
            a -= Time.unscaledDeltaTime * 2.5f;
            if (_group) _group.alpha = Mathf.Max(0f, a);
            yield return null;
        }

        Destroy(gameObject);
    }

    // ── Bar driver ────────────────────────────────────────────────────

    private void DriveBar(float t)
    {
        if (_fillRect == null) return;
        float w = BAR_W * Mathf.Clamp01(t);
        _fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);

        if (_forkRect != null)
        {
            // Leading edge: forklift center sits just past the fill's right edge.
            // 4 px is the inset from the border to the fill start.
            float edgeX = 4f + w;
            _forkRect.anchoredPosition = new Vector2(edgeX - FORK_W * 0.5f, 28f);
        }
    }

    // ── Canvas builder ────────────────────────────────────────────────

    private void BuildCanvas()
    {
        // Canvas — screen overlay, always on top
        var cGo = new GameObject("Canvas");
        cGo.transform.SetParent(transform, false);

        var canvas = cGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        cGo.AddComponent<GraphicRaycaster>();

        _group = cGo.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = true;
        _group.interactable   = false;
        _group.alpha          = 1f;

        var scaler = cGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        // ── Background ──────────────────────────────────────────────
        var bg = MakeRect(cGo.transform, "BG");
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.offsetMin = bg.offsetMax = Vector2.zero;
        bg.gameObject.AddComponent<Image>().color = BG_COLOR;

        // ── "LOADING GAME" title ────────────────────────────────────
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(bg, false);
        var titleText = titleGo.AddComponent<Text>();
        titleText.text      = "LOADING GAME";
        titleText.color     = TEXT_COLOR;
        titleText.fontSize  = 96;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.font      = LoadLilitaFont() ?? titleText.font;
        var titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0.55f);
        titleRt.anchorMax = new Vector2(1f, 0.80f);
        titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;

        // ── Bar container (border) ──────────────────────────────────
        var barGo = new GameObject("BarTrack");
        barGo.transform.SetParent(bg, false);
        var barImg = barGo.AddComponent<Image>();
        barImg.color = BORDER_COLOR;
        var barRt = barImg.rectTransform;
        barRt.anchorMin        = new Vector2(0.5f, 0.5f);
        barRt.anchorMax        = new Vector2(0.5f, 0.5f);
        barRt.pivot            = new Vector2(0.5f, 0.5f);
        barRt.sizeDelta        = new Vector2(BAR_W + 8f, BAR_H + 8f);
        barRt.anchoredPosition = new Vector2(0f, -90f);

        // Bottom trim strip (dark orange, mimics the button shadow style)
        var trimGo = new GameObject("BarTrim");
        trimGo.transform.SetParent(barRt, false);
        trimGo.AddComponent<Image>().color = TRIM_COLOR;
        var trimRt = trimGo.GetComponent<RectTransform>();
        trimRt.anchorMin = new Vector2(0f, 0f);
        trimRt.anchorMax = new Vector2(1f, 0f);
        trimRt.pivot     = new Vector2(0.5f, 0f);
        trimRt.sizeDelta        = new Vector2(0f, 6f);
        trimRt.anchoredPosition = Vector2.zero;

        // ── Fill mask (clips fill to bar interior) ──────────────────
        var maskGo = new GameObject("FillMask");
        maskGo.transform.SetParent(barRt, false);
        var maskImg = maskGo.AddComponent<Image>();
        maskImg.color = new Color(0, 0, 0, 0.001f);  // near-invisible but required for Mask
        var maskComp = maskGo.AddComponent<Mask>();
        maskComp.showMaskGraphic = false;
        var maskRt = maskImg.rectTransform;
        maskRt.anchorMin        = new Vector2(0f, 0f);
        maskRt.anchorMax        = new Vector2(0f, 1f);
        maskRt.pivot            = new Vector2(0f, 0.5f);
        maskRt.anchoredPosition = new Vector2(4f, 0f);   // 4 px inset from left border
        maskRt.sizeDelta        = new Vector2(BAR_W, BAR_H);

        // ── Fill (orange, grows left → right) ──────────────────────
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(maskRt, false);
        fillGo.AddComponent<Image>().color = FILL_COLOR;
        _fillRect = fillGo.GetComponent<RectTransform>();
        _fillRect.anchorMin        = new Vector2(0f, 0f);
        _fillRect.anchorMax        = new Vector2(0f, 1f);
        _fillRect.pivot            = new Vector2(0f, 0.5f);
        _fillRect.anchoredPosition = Vector2.zero;
        _fillRect.sizeDelta        = new Vector2(0f, 0f);

        // ── Forklift icon — sits on bar edge, outside the fill mask ─
        // Parented to barRt so it can float above the track.
        var forkGo = new GameObject("Forklift");
        forkGo.transform.SetParent(barRt, false);
        var forkImg = forkGo.AddComponent<Image>();
        forkImg.sprite         = LoadForkSprite();
        forkImg.preserveAspect = true;
        if (forkImg.sprite == null) forkImg.color = new Color(1, 1, 1, 0);  // hide if missing
        _forkRect = forkImg.rectTransform;
        _forkRect.anchorMin = new Vector2(0f, 0.5f);
        _forkRect.anchorMax = new Vector2(0f, 0.5f);
        _forkRect.pivot     = new Vector2(0.5f, 0.5f);
        _forkRect.sizeDelta = new Vector2(FORK_W, FORK_H);
        forkGo.transform.localScale = new Vector3(-1f, 1f, 1f);  // face right (toward unfilled bar)

        DriveBar(0f);
    }

    // ── Asset loaders ─────────────────────────────────────────────────

    private static Font LoadLilitaFont()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
            "Assets/6. Art/UI Packs/Farm Game UI - Simple 2D UI/Font/LilitaOne-Regular.ttf");
#else
        return Resources.Load<Font>("LilitaOne-Regular");
#endif
    }

    private static Sprite LoadForkSprite()
    {
        // Use the same forklift sprite that's already on the settings slider dragger.
#if UNITY_EDITOR
        var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/6. Art/UI/ForkSprite.png");
        if (tex != null)
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f));
        return null;
#else
        return Resources.Load<Sprite>("ForkSprite");
#endif
    }

    // ── Utilities ─────────────────────────────────────────────────────

    private static RectTransform MakeRect(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private static Color FromHex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
