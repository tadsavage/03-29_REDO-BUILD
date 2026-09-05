using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using System.Collections;

public class SplashScreenController : MonoBehaviour
{
    [SerializeField] private bool PlaySplashScreen = true;
    [SerializeField] private Sprite LogoSprite;
    [SerializeField] private AudioClip SplashMusic;
    [SerializeField, Range(0f, 1f)] private float splashVolume = 0.5f;
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.15f, 0.2f); // Dark grayish blue
    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float displayDuration = 3.0f;
    [SerializeField] private float _startLogoSize = 0.5f;
    [SerializeField] private float _maxLogoSize = 1.0f;

    public bool ShouldPlaySplash => PlaySplashScreen;

    private void Start()
    {
        if (PlaySplashScreen)
        {
            StartCoroutine(PlaySplashSequence());
        }
    }

    private IEnumerator PlaySplashSequence()
    {
        // 1. Store original visibility and hide UI Documents
        UIDocument[] allDocs = Object.FindObjectsByType<UIDocument>();
        System.Collections.Generic.Dictionary<UIDocument, DisplayStyle> originalVis = new();

        foreach (var doc in allDocs)
        {
            if (doc.rootVisualElement != null)
            {
                originalVis[doc] = doc.rootVisualElement.resolvedStyle.display;
                doc.rootVisualElement.style.display = DisplayStyle.None;
            }
        }

        // Setup Splash UI
        GameObject canvasGO = new GameObject("SplashCanvas");
        Canvas splashCanvas = canvasGO.AddComponent<Canvas>();
        splashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        splashCanvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform);
        UnityEngine.UI.Image background = bgGO.AddComponent<UnityEngine.UI.Image>();
        background.color = backgroundColor;
        RectTransform bgRect = background.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.localPosition = Vector3.zero;

        // Logo
        GameObject logoGO = new GameObject("Logo");
        logoGO.transform.SetParent(canvasGO.transform);
        UnityEngine.UI.Image logoImage = logoGO.AddComponent<UnityEngine.UI.Image>();
        if (LogoSprite != null)
        {
            logoImage.sprite = LogoSprite;
            logoImage.preserveAspect = true;
        }
        
        RectTransform logoRect = logoImage.rectTransform;
        // Set logo to fill the entire screen (0 to 1)
        logoRect.anchorMin = Vector2.zero;
        logoRect.anchorMax = Vector2.one;
        logoRect.sizeDelta = Vector2.zero;
        logoRect.localPosition = Vector3.zero;
        logoImage.color = new Color(1, 1, 1, 0);
        logoGO.transform.localScale = Vector3.one * _startLogoSize;

        // Music
        AudioSource audioSource = gameObject.AddComponent<AudioSource>();
        if (SplashMusic != null)
        {
            audioSource.clip = SplashMusic;
            audioSource.volume = splashVolume;
            audioSource.Play();
        }

        // Fade In and Grow — unscaled time deliberately: TopBarUI.SetSpeed(0f) ("always start paused
        // per Tad's explicit request") runs independently of this splash gating and can freeze
        // Time.timeScale at 0 before or during this coroutine. On scaled time these loops would then
        // never advance, the splash would never finish, and UIBootstrapper.InitializeAll() below
        // would never run — confirmed live: the whole game stuck on the splash screen forever with
        // Time.timeScale=0. This is presentation before gameplay even starts, so unscaled time is
        // correct regardless — same reasoning already applied to UIToast/FloatingMoneyText elsewhere.
        float timer = 0;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = timer / fadeDuration;
            logoImage.color = new Color(1, 1, 1, Mathf.Lerp(0, 1, progress));
            logoGO.transform.localScale = Vector3.one * Mathf.Lerp(_startLogoSize, _maxLogoSize, progress);
            yield return null;
        }
        logoImage.color = Color.white;
        logoGO.transform.localScale = Vector3.one * _maxLogoSize;

        // Hold
        yield return new WaitForSecondsRealtime(displayDuration);

        // Fade Out
        timer = 0;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(1, 0, timer / fadeDuration);
            logoImage.color = new Color(1, 1, 1, alpha);
            background.color = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, alpha);
            yield return null;
        }

        // Cleanup Splash
        Destroy(canvasGO);
        if (audioSource != null) Destroy(audioSource);

        // 2. Restore UI Documents to their ORIGINAL state
        foreach (var doc in allDocs)
        {
            if (doc != null && doc.rootVisualElement != null && originalVis.ContainsKey(doc))
                doc.rootVisualElement.style.display = originalVis[doc];
        }

        GetComponent<UIBootstrapper>().InitializeAll();
    }
}
