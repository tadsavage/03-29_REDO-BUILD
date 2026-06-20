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
    // Unfilled track: transparent dark, matching the recessed slots in our other UIs (rgba(20,32,46,0.6)).
    private static readonly Color TRACK_COLOR  = new Color(20f / 255f, 32f / 255f, 46f / 255f, 0.6f);
    private static readonly Color FILL_COLOR   = FromHex("5C9BC4");   // light-blue fill = the progress
    private static readonly Color TRIM_COLOR   = FromHex("7A4C22");   // dark/brown bottom trim
    private static readonly Color TEXT_COLOR   = FromHex("EAF4FF");   // near-white title

    private const float BAR_W  = 680f;
    private const float BAR_H  = 36f;
    private const float FORK_W = 146f;   // ~28% larger than the old 114
    private const float FORK_H = 96f;    // ~28% larger than the old 75 (aspect preserved)
    private const float FORK_LEAD = 2f; // small gap between the fill's leading edge and the forklift's tail
    private const float FORK_Y    = 0f;  // forklift bottom rests this far above the bar's bottom (0 = flush)

    // Dust-puff effect kicked up behind the forklift while it drives.
    private const int   PUFF_COUNT      = 6;
    private const float PUFF_INTERVAL   = 0.10f;          // seconds between puffs while moving
    private const float PUFF_LIFE       = 0.45f;          // seconds each puff lives
    private const float PUFF_START_SIZE = 14f;
    private const float PUFF_END_SIZE   = FORK_H * 0.5f;  // grows to ~half the forklift's size
    private const float PUFF_MAX_ALPHA  = 0.55f;
    private static readonly Color PUFF_COLOR = new Color(0.82f, 0.75f, 0.60f); // dusty tan

    private CanvasGroup   _group;
    private RectTransform _fillRect;
    private RectTransform _forkRect;

    private float _targetProgress;
    private float _displayProgress;
    private bool  _completing;

    // Dust-puff pool
    private RectTransform[] _puffRt;
    private Image[]         _puffImg;
    private float[]         _puffAge;
    private Vector2[]       _puffOrigin;
    private Vector2[]       _puffVel;
    private float           _emitTimer;
    private float           _prevForkX = float.NaN;

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
        float dt = Time.unscaledDeltaTime;
        if (!_completing)
        {
            _displayProgress = Mathf.MoveTowards(
                _displayProgress, _targetProgress, dt * 0.55f);
            DriveBar(_displayProgress);
        }
        TickPuffs(dt);   // animate the dust every frame, including the final sprint
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
            // The forklift drives just AHEAD of the fill on the empty track, and the blue follows
            // right behind its rear wheels (FORK_LEAD = the small gap between the fill edge and the
            // forklift's tail). 4 px is the inset from the border to the fill start.
            float fillEdgeX   = 4f + w;
            float forkCenterX = fillEdgeX + FORK_LEAD + FORK_W * 0.5f;
            // Keep it on the track — at 100% the forklift settles at the right end and the fill
            // catches up to it rather than the forklift overrunning the bar.
            forkCenterX = Mathf.Clamp(forkCenterX, FORK_W * 0.5f, (4f + BAR_W) - FORK_W * 0.5f);
            _forkRect.anchoredPosition = new Vector2(forkCenterX, FORK_Y);
        }
    }

    // ── Dust puffs ────────────────────────────────────────────────────

    private void BuildPuffPool(RectTransform barRt)
    {
        _puffRt     = new RectTransform[PUFF_COUNT];
        _puffImg    = new Image[PUFF_COUNT];
        _puffAge    = new float[PUFF_COUNT];
        _puffOrigin = new Vector2[PUFF_COUNT];
        _puffVel    = new Vector2[PUFF_COUNT];

        var sprite = SoftCircleSprite();
        for (int i = 0; i < PUFF_COUNT; i++)
        {
            var go  = new GameObject("Puff" + i);
            go.transform.SetParent(barRt, false);
            var img = go.AddComponent<Image>();
            img.sprite        = sprite;
            img.color         = PUFF_COLOR;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);   // sit on the ground, grow upward
            rt.sizeDelta = new Vector2(PUFF_START_SIZE, PUFF_START_SIZE);

            _puffRt[i]  = rt;
            _puffImg[i] = img;
            _puffAge[i] = PUFF_LIFE;                 // start retired
            go.SetActive(false);
        }
    }

    private void TickPuffs(float dt)
    {
        if (_puffRt == null || _forkRect == null) return;

        float forkX = _forkRect.anchoredPosition.x;
        bool moving = !float.IsNaN(_prevForkX) && Mathf.Abs(forkX - _prevForkX) > 0.05f;
        _prevForkX = forkX;

        if (moving)
        {
            _emitTimer -= dt;
            if (_emitTimer <= 0f)
            {
                _emitTimer = PUFF_INTERVAL;
                EmitPuff(forkX - FORK_W * 0.42f);    // at the forklift's rear wheels
            }
        }

        for (int i = 0; i < _puffRt.Length; i++)
        {
            if (_puffAge[i] >= PUFF_LIFE) continue;   // retired
            _puffAge[i] += dt;
            if (_puffAge[i] >= PUFF_LIFE) { _puffRt[i].gameObject.SetActive(false); continue; }

            float k    = _puffAge[i] / PUFF_LIFE;
            float size = Mathf.Lerp(PUFF_START_SIZE, PUFF_END_SIZE, k);
            _puffRt[i].sizeDelta        = new Vector2(size, size);
            _puffRt[i].anchoredPosition = _puffOrigin[i] + _puffVel[i] * _puffAge[i];

            // Quick fade-in, then ease out.
            float a = (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f) * PUFF_MAX_ALPHA;
            var c = PUFF_COLOR; c.a = Mathf.Clamp01(a);
            _puffImg[i].color = c;
        }
    }

    private void EmitPuff(float rearX)
    {
        for (int i = 0; i < _puffRt.Length; i++)
        {
            if (_puffAge[i] < PUFF_LIFE) continue;    // still alive
            _puffAge[i] = 0f;
            _puffRt[i].gameObject.SetActive(true);
            _puffOrigin[i] = new Vector2(rearX + Random.Range(-6f, 6f), Random.Range(3f, 11f));
            _puffVel[i]    = new Vector2(Random.Range(-42f, -16f), Random.Range(16f, 40f)); // drift back + rise
            return;
        }
    }

    private static Sprite _puffSprite;
    private static Sprite SoftCircleSprite()
    {
        if (_puffSprite != null) return _puffSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;     // 0 centre → 1 edge
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a)); // soft radial falloff
            }
        tex.Apply();
        _puffSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        return _puffSprite;
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
        barImg.color = TRACK_COLOR;
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

        // ── Dust puffs (added before the forklift so they render behind it) ─
        BuildPuffPool(barRt);

        // ── Forklift icon — sits on bar edge, outside the fill mask ─
        // Parented to barRt so it can float above the track.
        var forkGo = new GameObject("Forklift");
        forkGo.transform.SetParent(barRt, false);
        var forkImg = forkGo.AddComponent<Image>();
        forkImg.sprite         = LoadForkSprite();
        forkImg.preserveAspect = true;
        if (forkImg.sprite == null) forkImg.color = new Color(1, 1, 1, 0);  // hide if missing
        _forkRect = forkImg.rectTransform;
        // Bottom-anchored so the forklift "sits" on the bottom of the bar (Y axis).
        _forkRect.anchorMin = new Vector2(0f, 0f);
        _forkRect.anchorMax = new Vector2(0f, 0f);
        _forkRect.pivot     = new Vector2(0.5f, 0f);
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
