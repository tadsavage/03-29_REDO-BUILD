using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Full-screen loading overlay.
/// All bar elements are direct children of the background rect, positioned in
/// screen-space using anchorMin/Max = (0.5, 0.5) + anchoredPosition offsets.
/// This avoids all nested-pivot coordinate confusion.
///
/// Visual layers (bottom to top):
///   1. BG          — full-screen navy
///   2. BorderRect  — orange, slightly larger than track
///   3. TrackRect   — dark navy, the empty bar interior
///   4. FillRect    — blue, grows left→right over the track
///   5. Puffs       — dust behind forklift
///   6. Forklift    — rides the fill's leading edge
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    // ── Palette ───────────────────────────────────────────────────────
    private static readonly Color BG_COLOR = FromHex("1E262E");
    private static readonly Color TRACK_COLOR = FromHex("14202E");
    private static readonly Color BORDER_COLOR = new Color(0xE8 / 255f, 0x89 / 255f, 0x2A / 255f, 0.90f);
    private static readonly Color FILL_COLOR = new Color(0x5C / 255f, 0x9B / 255f, 0xC4 / 255f, 0.60f);
    private static readonly Color TRIM_COLOR = FromHex("7A4C22");
    private static readonly Color TEXT_COLOR = FromHex("EAF4FF");

    // Bar geometry
    private const float BAR_W = 680f;
    private const float BAR_H = 36f;
    private const float BORDER = 4f;
    private const float BAR_Y = -90f;
    private const float FORK_W = 146f;
    private const float FORK_H = 96f;

    // Forklift travel range — serialized so you can tune in Inspector.
    // X: anchoredPosition.x from screen center (anchor=0.5,0.5).
    // Y: anchoredPosition.y from screen center; negative = below center.
    [SerializeField] private float forkStartX = -350f;
    [SerializeField] private float forkEndX = 350f;
    [SerializeField] private float forkY = -72f;   // sits just above the bar

    private const float TRACK_LEFT = -BAR_W * 0.5f;

    // Animation — serialized so you can tweak in Inspector without recompiling
    // Total minimum visible time = START_DELAY + FILL_DURATION = 0.5 + 2.0 = 2.5s
    [SerializeField] private float startDelay = 0.5f;   // pause before bar moves
    [SerializeField] private float fillDuration = 3.0f;   // seconds to fill 0→100%

    // Dust puffs
    private const int PUFF_COUNT = 12;
    private const float PUFF_INTERVAL = 0.10f;
    private const float PUFF_LIFE = 0.45f;
    private const float PUFF_START_SIZE = 16f;
    private const float PUFF_END_SIZE = FORK_H * 0.75f;
    private const float PUFF_MAX_ALPHA = 0.55f;
    private static readonly Color PUFF_COLOR = new Color(0.8275f, 0.8575f, 0.8275f);

    // ── State ─────────────────────────────────────────────────────────
    private CanvasGroup _group;
    private RectTransform _fillRect;   // grows width 0→BAR_W
    private RectTransform _forkRect;   // anchoredPosition.x tracks fill edge

    private float _displayProgress;   // 0→1, purely time-driven
    private bool _completing;
    private float _aliveTime;         // seconds since FIRST RENDERED FRAME (not Awake)
    private bool _clockStarted;      // true after first Update tick

    // Puff pool
    private RectTransform[] _puffRt;
    private Image[] _puffImg;
    private float[] _puffAge;
    private Vector2[] _puffOrigin;
    private Vector2[] _puffVel;
    private float _emitTimer;
    private float _prevForkX = float.NaN;

    // ── Bootstrap ─────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "Main") return;
        if (Instance != null) return;
        Spawn();
    }

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

    // SetProgress kept for API compat — display is purely time-driven so
    // it always visually starts at 0 no matter when this is called.
    public void SetProgress(float t) { }

    public void CompleteAndHide()
    {
        if (!_completing) StartCoroutine(CompleteRoutine());
    }

    // ── Update ────────────────────────────────────────────────────────

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Don't start the clock until the first real rendered frame.
        // Awake/Start can fire long before the canvas is visible — we only
        // want _aliveTime to measure time the player can actually SEE the bar.
        if (!_clockStarted)
        {
            _clockStarted = true;
            DriveBar(0f);   // ensure bar is at zero on first visible frame
            TickPuffs(dt);
            return;
        }

        _aliveTime += dt;

        if (!_completing)
        {
            // After startDelay, fill smoothly. Capped at 99% until CompleteAndHide().
            float animTime = Mathf.Max(0f, _aliveTime - startDelay);
            float fillSpeed = fillDuration > 0f ? 1f / fillDuration : 1f;
            _displayProgress = Mathf.Min(animTime * fillSpeed, 0.99f);
            DriveBar(_displayProgress);
        }
        TickPuffs(dt);
    }

    // ── Complete routine ──────────────────────────────────────────────

    private IEnumerator CompleteRoutine()
    {
        _completing = true;

        // Wait until the minimum animation has fully played out.
        // minimum end time = startDelay + fillDuration
        float minimumEnd = startDelay + fillDuration;
        while (_aliveTime < minimumEnd)
        {
            float animTime = Mathf.Max(0f, _aliveTime - startDelay);
            float fillSpeed = fillDuration > 0f ? 1f / fillDuration : 1f;
            _displayProgress = Mathf.Min(animTime * fillSpeed, 0.99f);
            DriveBar(_displayProgress);
            yield return null;
        }

        // Smooth final push to 100% at the same pace (no sprint, no snap)
        float finishSpeed = fillDuration > 0f ? 1f / fillDuration : 1f;
        while (_displayProgress < 1f)
        {
            float dt = Time.unscaledDeltaTime;
            _displayProgress = Mathf.MoveTowards(_displayProgress, 1f, dt * finishSpeed);
            DriveBar(_displayProgress);
            yield return null;
        }

        DriveBar(1f);
        yield return new WaitForSecondsRealtime(0.3f);   // brief pause at full bar

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
    // All rects share anchor=(0.5,0.5) on bg, pivot=(0,0.5) for left-edge elements.
    // TRACK_LEFT = -BAR_W*0.5 = the left edge x in screen-space from bg center.
    // fill.anchoredPosition.x = TRACK_LEFT (fixed, left edge)
    // fill.sizeDelta.x        = BAR_W * t  (grows right)
    // fork.anchoredPosition   = Lerp(forkStartX, forkEndX, t) on X, forkY on Y

    // Smoothstep easing: slow start, full speed in the middle, slow finish.
    // Eliminates the lurch at t=0 and the snap at t=1.
    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);   // classic smoothstep
    }

    private void DriveBar(float t)
    {
        if (_fillRect == null) return;
        float e = Ease(t);
        float fillW = BAR_W * e;
        _fillRect.sizeDelta = new Vector2(fillW, BAR_H);

        if (_forkRect != null)
        {
            float x = Mathf.Lerp(forkStartX, forkEndX, e);
            _forkRect.anchoredPosition = new Vector2(x, forkY);
        }
    }

    // ── Canvas builder ────────────────────────────────────────────────

    private void BuildCanvas()
    {
        var cGo = new GameObject("Canvas");
        cGo.transform.SetParent(transform, false);
        var canvas = cGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        cGo.AddComponent<GraphicRaycaster>();
        _group = cGo.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = true;
        _group.interactable = false;
        _group.alpha = 1f;
        var scaler = cGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // BG — full screen
        var bg = Flat(cGo.transform, "BG", BG_COLOR);
        bg.anchorMin = Vector2.zero;
        bg.anchorMax = Vector2.one;
        bg.offsetMin = bg.offsetMax = Vector2.zero;

        // Title
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(bg, false);
        var tt = titleGo.AddComponent<Text>();
        tt.text = "LOADING GAME";
        tt.color = TEXT_COLOR;
        tt.fontSize = 96;
        tt.alignment = TextAnchor.MiddleCenter;
        tt.font = LoadLilitaFont() ?? tt.font;
        var trt = tt.rectTransform;
        trt.anchorMin = new Vector2(0f, 0.55f);
        trt.anchorMax = new Vector2(1f, 0.80f);
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        // Helper: create a center-anchored, left-pivot rect on bg
        // anchoredPosition.x = left edge x from screen center
        // anchoredPosition.y = vertical center y from screen center
        RectTransform CenterLeft(string name, Color col, float x, float y, float w, float h)
        {
            var rt = Flat(bg, name, col);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);   // left-edge pivot
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        // Orange border — slightly bigger than track, same left edge
        CenterLeft("Border", BORDER_COLOR,
            TRACK_LEFT - BORDER,
            BAR_Y,
            BAR_W + BORDER * 2f,
            BAR_H + BORDER * 2f);

        // Dark track interior
        CenterLeft("Track", TRACK_COLOR,
            TRACK_LEFT, BAR_Y, BAR_W, BAR_H);

        // Bottom trim
        var trimRt = Flat(bg, "Trim", TRIM_COLOR);
        trimRt.anchorMin = new Vector2(0.5f, 0.5f);
        trimRt.anchorMax = new Vector2(0.5f, 0.5f);
        trimRt.pivot = new Vector2(0f, 1f);
        trimRt.sizeDelta = new Vector2(BAR_W + BORDER * 2f, 6f);
        trimRt.anchoredPosition = new Vector2(TRACK_LEFT - BORDER, BAR_Y - BAR_H * 0.5f - BORDER);

        // Blue fill — starts at width 0, grows right
        _fillRect = CenterLeft("Fill", FILL_COLOR, TRACK_LEFT, BAR_Y, 0f, BAR_H);

        // Dust puffs — siblings of fill, same coordinate space
        BuildPuffPool(bg);

        // Forklift — sibling of fill
        var forkGo = new GameObject("Forklift");
        forkGo.transform.SetParent(bg, false);
        var forkImg = forkGo.AddComponent<Image>();
        forkImg.sprite = LoadForkSprite();
        forkImg.preserveAspect = true;
        if (forkImg.sprite == null) forkImg.color = new Color(1, 1, 1, 0);
        _forkRect = forkImg.rectTransform;
        _forkRect.anchorMin = new Vector2(0.5f, 0.5f);
        _forkRect.anchorMax = new Vector2(0.5f, 0.5f);
        _forkRect.pivot = new Vector2(0.5f, 0f);   // center-x, bottom-y
        _forkRect.sizeDelta = new Vector2(FORK_W, FORK_H);
        forkGo.transform.localScale = new Vector3(-1f, 1f, 1f);

        DriveBar(0f);
    }

    // ── Dust puffs ────────────────────────────────────────────────────

    private void BuildPuffPool(RectTransform parent)
    {
        _puffRt = new RectTransform[PUFF_COUNT];
        _puffImg = new Image[PUFF_COUNT];
        _puffAge = new float[PUFF_COUNT];
        _puffOrigin = new Vector2[PUFF_COUNT];
        _puffVel = new Vector2[PUFF_COUNT];
        var sprite = SoftCircleSprite();
        for (int i = 0; i < PUFF_COUNT; i++)
        {
            var go = new GameObject("Puff" + i);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = PUFF_COLOR; img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(PUFF_START_SIZE, PUFF_START_SIZE);
            _puffRt[i] = rt; _puffImg[i] = img; _puffAge[i] = PUFF_LIFE;
            go.SetActive(false);
        }
    }

    private void TickPuffs(float dt)
    {
        if (_puffRt == null || _forkRect == null) return;
        float forkX = _forkRect.anchoredPosition.x;
        bool moving = !float.IsNaN(_prevForkX) && Mathf.Abs(forkX - _prevForkX) > 0.05f;
        _prevForkX = forkX;
        if (moving) { _emitTimer -= dt; if (_emitTimer <= 0f) { _emitTimer = PUFF_INTERVAL; EmitPuff(forkX - FORK_W * 0.42f); } }
        for (int i = 0; i < _puffRt.Length; i++)
        {
            if (_puffAge[i] >= PUFF_LIFE) continue;
            _puffAge[i] += dt;
            if (_puffAge[i] >= PUFF_LIFE) { _puffRt[i].gameObject.SetActive(false); continue; }
            float k = _puffAge[i] / PUFF_LIFE;
            float size = Mathf.Lerp(PUFF_START_SIZE, PUFF_END_SIZE, k);
            _puffRt[i].sizeDelta = new Vector2(size, size);
            _puffRt[i].anchoredPosition = _puffOrigin[i] + _puffVel[i] * _puffAge[i];
            float a = (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f) * PUFF_MAX_ALPHA;
            var c = PUFF_COLOR; c.a = Mathf.Clamp01(a); _puffImg[i].color = c;
        }
    }

    private void EmitPuff(float rearX)
    {
        for (int i = 0; i < _puffRt.Length; i++)
        {
            if (_puffAge[i] < PUFF_LIFE) continue;
            _puffAge[i] = 0f; _puffRt[i].gameObject.SetActive(true);
            _puffOrigin[i] = new Vector2(rearX + Random.Range(-6f, 6f), BAR_Y + BAR_H * 0.5f + Random.Range(3f, 11f));
            _puffVel[i] = new Vector2(Random.Range(-42f, -16f), Random.Range(16f, 40f));
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
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        _puffSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        return _puffSprite;
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
#if UNITY_EDITOR
        var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/6. Art/UI/ForkSprite.png");
        if (tex != null) return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        return null;
#else
        return Resources.Load<Sprite>("ForkSprite");
#endif
    }

    // ── Utilities ─────────────────────────────────────────────────────

    private static RectTransform Flat(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }

    private static Color FromHex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}