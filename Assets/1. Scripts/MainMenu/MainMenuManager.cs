using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using SaveLoadSystem;
using System.IO;

/// <summary>
/// FORK IT! — Main Menu Manager
///
/// Setup (one-time):
///   1. Create a new scene (MainMenu.unity) in Assets/8. Scenes/
///   2. Add a Camera and this GameObject with UIDocument + MainMenuManager
///   3. UIDocument → Source Asset = MainMenu.uxml
///   4. Assign an AudioMixer if you have one (optional)
///   5. Set gameSceneName to match your main game scene name
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuManager : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Exact name of the main game scene to load on Clock In")]
    [SerializeField] private string gameSceneName = "Main";

    [Header("Logo Image")]
    [Tooltip("Assign RatsAndRaymond.png here")]
    [SerializeField] private Texture2D forklifLogo;

    [Header("Audio (optional)")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string gameMixerParam  = "GameVolume";
    [SerializeField] private string musicMixerParam = "MusicVolume";

    [Header("Menu Audio")]
    [Tooltip("MainMenuCartoonMusic.mp3 — starts the moment the menu appears and " +
             "plays through in full, surviving the transition into build mode.")]
    [SerializeField] private AudioClip menuMusic;
    [Tooltip("MainMenuBoom!.wav — fires the instant the logo lands on the menu.")]
    [SerializeField] private AudioClip logoImpactSfx;
    [SerializeField, Range(0f, 1f)] private float menuMusicVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float impactSfxVolume = 1f;

    // Persistent music source: lives across the MainMenu -> Main scene load so the
    // cartoon track is never cut off. Static so re-entering the menu won't restart it.
    private static AudioSource _persistentMenuMusic;

    // Local 2D source for menu one-shots (the logo boom). Lives with this scene.
    private AudioSource _menuSfxSource;

    // Title intro animation timing (see PlayTitleIntro): the intro class is released
    // 120ms after Start, then the USS scale/rotate transition (~1s, per UXML inline
    // transition-duration) runs. The logo "lands" when that transition completes.
    private const long TitleIntroReleaseMs = 1000;
    private const long TitleTransitionMs   = 1000;

    // ── UI References ─────────────────────────────────────────────────────────
    private VisualElement _root;
    private VisualElement _mainPanel;

    // Popups
    private VisualElement _namePopup;
    private VisualElement _loadPopup;
    private VisualElement _settingsPopup;

    // Name popup
    private TextField _nameField;

    // Load
    private VisualElement _saveSlotContainer;

    // Settings
    private Slider _gameVolumeSlider;
    private Slider _musicVolumeSlider;
    private DropdownField _resolutionDropdown;

    // Graphics preset buttons
    private Button _btnUltra, _btnGood, _btnToaster;

    // Difficulty buttons
    private Button _btnClerk, _btnSupervisor, _btnManager;

    // Tooltip
    private Label _tooltipLabel;

    // ── State ─────────────────────────────────────────────────────────────────
    private string _playerName;

    private static readonly Resolution[] CommonResolutions =
    {
        new Resolution { width = 1280, height = 720  },
        new Resolution { width = 1920, height = 1080 },
        new Resolution { width = 2560, height = 1440 },
        new Resolution { width = 3840, height = 2160 },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        var doc = GetComponent<UIDocument>();
        _root = doc.rootVisualElement;
        _root.pickingMode = PickingMode.Ignore;

        // Local 2D source for menu one-shots (logo boom).
        _menuSfxSource = gameObject.AddComponent<AudioSource>();
        _menuSfxSource.playOnAwake = false;
        _menuSfxSource.spatialBlend = 0f;

        CacheElements();
        WireButtons();
        ApplyStoredSettings();
        HideAllPopups();
        ApplyLogoImage();
        StartMenuMusic();
        PlayTitleIntro();

        // Restore player name if it exists
        _playerName = PlayerPrefs.GetString("PlayerName", "");
    }

    /// <summary>
    /// Starts the cartoon menu music immediately on a DontDestroyOnLoad source so it
    /// plays in its entirety and keeps going across the load into the build scene.
    /// Guarded by a static reference so coming back to the menu won't restart it.
    /// </summary>
    private void StartMenuMusic()
    {
        if (menuMusic == null) return;
        if (_persistentMenuMusic != null && _persistentMenuMusic.isPlaying) return;

        var go = new GameObject("MenuMusicPlayer");
        Object.DontDestroyOnLoad(go);

        var src = go.AddComponent<AudioSource>();
        src.clip = menuMusic;
        src.loop = false;          // play through once, in full
        src.spatialBlend = 0f;     // 2D
        src.playOnAwake = false;
        src.volume = menuMusicVolume;
        src.Play();

        _persistentMenuMusic = src;
    }

    /// <summary>Boom the instant the title finishes landing on the menu.</summary>
    private void PlayLogoImpact()
    {
        if (logoImpactSfx == null || _menuSfxSource == null) return;
        _menuSfxSource.PlayOneShot(logoImpactSfx, impactSfxVolume);
    }

    /// <summary>
    /// Title flies in big &amp; straight (the .title-label--intro USS state set in
    /// UXML), then we strip that class so the USS transition shrinks it down and
    /// rotates it onto its landed angle over the top of the rats image.
    /// </summary>
    private void PlayTitleIntro()
    {
        var title = _root.Q<Label>("title-label");
        if (title == null) return;

        // Make sure the intro (start) state is applied, then release it a beat
        // later so the transition animates from big/straight -> landed/angled.
        title.AddToClassList("title-label--intro");
        _root.schedule.Execute(() => title.RemoveFromClassList("title-label--intro"))
             .StartingIn(TitleIntroReleaseMs);

        // BOOM the moment the logo lands (intro released + transition complete).
        _root.schedule.Execute(PlayLogoImpact)
             .StartingIn(TitleIntroReleaseMs + TitleTransitionMs);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Setup
    // ─────────────────────────────────────────────────────────────────────────

    private void CacheElements()
    {
        _mainPanel     = _root.Q("main-panel");
        _namePopup       = _root.Q("name-popup");
        _loadPopup       = _root.Q("load-popup");
        _settingsPopup   = _root.Q("settings-popup");

        _nameField    = _root.Q<TextField>("name-field");
        _saveSlotContainer = _root.Q("save-slot-container");

        _gameVolumeSlider  = _root.Q<Slider>("slider-game-volume");
        _musicVolumeSlider = _root.Q<Slider>("slider-music-volume");
        _resolutionDropdown = _root.Q<DropdownField>("resolution-dropdown");

        _btnUltra   = _root.Q<Button>("btn-ultra");
        _btnGood    = _root.Q<Button>("btn-good");
        _btnToaster = _root.Q<Button>("btn-toaster");

        _btnClerk      = _root.Q<Button>("btn-diff-clerk");
        _btnSupervisor = _root.Q<Button>("btn-diff-supervisor");
        _btnManager    = _root.Q<Button>("btn-diff-manager");

        _tooltipLabel = _root.Q<Label>("tooltip-label");
    }

    private void ApplyLogoImage()
    {
        if (forklifLogo == null) return;
        var logoImage = _root.Q("logo-image");
        if (logoImage != null)
            logoImage.style.backgroundImage = new StyleBackground(forklifLogo);
    }

    private void WireButtons()
    {
        // Clock In tooltip
        var clockInBtn = _root.Q<Button>("btn-clock-in");
        clockInBtn?.RegisterCallback<MouseEnterEvent>(evt => ShowTooltip(clockInBtn, "New Game"));
        clockInBtn?.RegisterCallback<MouseLeaveEvent>(_ => HideTooltip());

        // Main menu
        clockInBtn?.RegisterCallback<ClickEvent>(_ => OnClockIn());
        _root.Q<Button>("btn-resume")?.RegisterCallback<ClickEvent>(_ => OnResumeShift());
        _root.Q<Button>("btn-settings")?.RegisterCallback<ClickEvent>(_ => ShowPopup(_settingsPopup));
        _root.Q<Button>("btn-punch-out")?.RegisterCallback<ClickEvent>(_ => OnPunchOut());

        // Name popup
        _root.Q<Button>("btn-name-confirm")?.RegisterCallback<ClickEvent>(_ => OnNameConfirmed());
        if (_nameField != null)
            _nameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    OnNameConfirmed();
            });
        _root.Q<Button>("btn-name-back")?.RegisterCallback<ClickEvent>(_ => HideAllPopups());

        // Load popup
        _root.Q<Button>("btn-load-close")?.RegisterCallback<ClickEvent>(_ => HideAllPopups());

        // Settings
        _btnUltra?.RegisterCallback<ClickEvent>(_ => ApplyGraphicsPreset("Ultra"));
        _btnGood?.RegisterCallback<ClickEvent>(_ => ApplyGraphicsPreset("Good"));
        _btnToaster?.RegisterCallback<ClickEvent>(_ => ApplyGraphicsPreset("Toaster"));

        _btnClerk?.RegisterCallback<ClickEvent>(_ => ApplyDifficulty(0));
        _btnSupervisor?.RegisterCallback<ClickEvent>(_ => ApplyDifficulty(1));
        _btnManager?.RegisterCallback<ClickEvent>(_ => ApplyDifficulty(2));

        _root.Q<Button>("btn-settings-close")?.RegisterCallback<ClickEvent>(_ => HideAllPopups());

        // Volume sliders
        if (_gameVolumeSlider != null)
            _gameVolumeSlider.RegisterValueChangedCallback(evt => SetVolume(gameMixerParam, evt.newValue));
        if (_musicVolumeSlider != null)
            _musicVolumeSlider.RegisterValueChangedCallback(evt => SetVolume(musicMixerParam, evt.newValue));

        // Resolution dropdown
        BuildResolutionDropdown();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button Handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnClockIn()
    {
        // Always ask for name — Clock In is always a new game
        if (_nameField != null && !string.IsNullOrEmpty(_playerName))
            _nameField.value = _playerName; // pre-fill but let them change it
        HideTooltip();
        ShowPopup(_namePopup);
        _nameField?.Focus();
    }

    private void OnNameConfirmed()
    {
        string entered = _nameField?.value?.Trim() ?? "";
        if (string.IsNullOrEmpty(entered)) return;

        _playerName = entered;
        PlayerPrefs.SetString("PlayerName", _playerName);
        PlayerPrefs.SetInt("IsNewGame", 1);
        PlayerPrefs.Save();

        // Welcome screen is now handled by WelcomeOverlayManager in the game scene
        LoadGameScene();
    }

    private void OnResumeShift()
    {
        BuildSaveSlotList();
        ShowPopup(_loadPopup);
    }

    private void OnPunchOut()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadGameScene()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Save Slot List (with thumbnails + metadata)
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildSaveSlotList()
    {
        if (_saveSlotContainer == null) return;
        _saveSlotContainer.Clear();

        // Autosave card
        AddSlotCard(-1, "Autosave", "Quick-save (F5)", System.DateTime.Now.Ticks);

        // Numbered slots from metadata
        if (SaveManager.Instance != null)
        {
            var allMetadata = SaveManager.Instance.GetAllMetadata();
            for (int i = 0; i < allMetadata.Length; i++)
            {
                var md = allMetadata[i];
                if (md == null || string.IsNullOrEmpty(md.gameDataFileName)) continue;

                string dataPath = System.IO.Path.Combine(Application.dataPath, "_Saves", md.gameDataFileName);
                if (!System.IO.File.Exists(dataPath)) continue;

                AddSlotCard(i, md.saveName, md.timestamp, md.timestampTicks);
            }
        }
        else
        {
            // Fallback: scan disk directly if SaveManager not available
            string saveDir = System.IO.Path.Combine(Application.dataPath, "_Saves");
            if (!System.IO.Directory.Exists(saveDir)) return;

            for (int i = 0; i < 8; i++)
            {
                string dataPath = System.IO.Path.Combine(saveDir, $"slot_{i}_data.json");
                if (System.IO.File.Exists(dataPath))
                    AddSlotCard(i, $"Slot {i + 1}", "", 0);
            }
        }
    }

    private void AddSlotCard(int slotIndex, string saveName, string timestamp, long timestampTicks)
    {
        // -- Card root --
        var card = new VisualElement();
        card.AddToClassList("save-slot-card");

        // -- Thumbnail --
        var thumb = new VisualElement();
        thumb.AddToClassList("slot-thumb");

        bool hasThumb = false;
        if (SaveManager.Instance != null)
        {
            string thumbPath = SaveManager.Instance.GetThumbnailPath(slotIndex);
            if (!string.IsNullOrEmpty(thumbPath))
            {
                var tex = SaveThumbnailCapture.LoadThumbnailFromDisk(thumbPath);
                if (tex != null)
                {
                    thumb.style.backgroundImage = new StyleBackground(tex);
                    hasThumb = true;
                }
            }
        }

        if (!hasThumb)
        {
            thumb.AddToClassList("slot-thumb--empty");
            var thumbLabel = new Label(slotIndex < 0 ? "F5/F9" : $"SLOT {slotIndex + 1}");
            thumbLabel.AddToClassList("slot-thumb-label");
            thumb.Add(thumbLabel);
        }

        card.Add(thumb);

        // -- Info column --
        var info = new VisualElement();
        info.AddToClassList("slot-info");

        var nameLabel = new Label(saveName);
        nameLabel.AddToClassList("slot-name");
        info.Add(nameLabel);

        if (!string.IsNullOrEmpty(timestamp))
        {
            var tsLabel = new Label(timestamp);
            tsLabel.AddToClassList("slot-timestamp");
            info.Add(tsLabel);
        }

        card.Add(info);

        // -- LOAD button with cycling color --
        var loadBtn = new Button();
        loadBtn.text = "LOAD";
        loadBtn.AddToClassList("slot-load-btn");

        // Cycle through green/blue/orange/purple based on slot index
        string[] colorVariants = { "slot-load-btn-green", "slot-load-btn-blue", "slot-load-btn-orange", "slot-load-btn-purple" };
        int colorIdx = (slotIndex < 0) ? 0 : (slotIndex % colorVariants.Length);
        loadBtn.AddToClassList(colorVariants[colorIdx]);

        int capturedIndex = slotIndex;
        loadBtn.clicked += () =>
        {
            if (capturedIndex < 0)
            {
                // Autosave: use quicksave load path
                PlayerPrefs.SetInt("LoadSlotIndex", -1);
                PlayerPrefs.SetString("LastSaveName", "autosave");
            }
            else
            {
                PlayerPrefs.SetInt("LoadSlotIndex", capturedIndex);
                PlayerPrefs.SetString("LastSaveName", $"slot_{capturedIndex}");
            }
            LoadGameScene();
        };

        card.Add(loadBtn);
        _saveSlotContainer?.Add(card);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Settings
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyGraphicsPreset(string preset)
    {
        var mgr = FindAnyObjectByType<GraphicsPresetManager>();
        if (mgr != null)
        {
            mgr.ApplyPreset((GraphicsPresetManager.Preset)System.Enum.Parse(
                typeof(GraphicsPresetManager.Preset), preset));
        }

        // Update button highlight
        _btnUltra?.RemoveFromClassList("gfx-btn--active");
        _btnGood?.RemoveFromClassList("gfx-btn--active");
        _btnToaster?.RemoveFromClassList("gfx-btn--active");

        switch (preset)
        {
            case "Ultra":   _btnUltra?.AddToClassList("gfx-btn--active");   break;
            case "Good":    _btnGood?.AddToClassList("gfx-btn--active");    break;
            case "Toaster": _btnToaster?.AddToClassList("gfx-btn--active"); break;
        }

        PlayerPrefs.SetString("GraphicsPresetName", preset);
    }

    private void BuildResolutionDropdown()
    {
        if (_resolutionDropdown == null) return;

        var choices = new List<string>();
        foreach (var r in CommonResolutions)
            choices.Add($"{r.width} x {r.height}");
        _resolutionDropdown.choices = choices;

        // Default to current resolution
        int w = Screen.width, h = Screen.height;
        int match = 1; // default 1080p
        for (int i = 0; i < CommonResolutions.Length; i++)
            if (CommonResolutions[i].width == w && CommonResolutions[i].height == h)
                match = i;
        _resolutionDropdown.index = match;

        _resolutionDropdown.RegisterValueChangedCallback(evt =>
        {
            int idx = _resolutionDropdown.index;
            if (idx < 0 || idx >= CommonResolutions.Length) return;
            var r = CommonResolutions[idx];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen);
        });
    }

    private void SetVolume(string param, float linear)
    {
        if (audioMixer == null) return;
        float db = linear > 0.0001f
            ? Mathf.Log10(linear) * 20f
            : -80f;
        audioMixer.SetFloat(param, db);
        PlayerPrefs.SetFloat(param, linear);
    }

    private void ApplyDifficulty(int level)
    {
        PlayerPrefs.SetInt("Difficulty", level);

        _btnClerk?.RemoveFromClassList("gfx-btn--active");
        _btnSupervisor?.RemoveFromClassList("gfx-btn--active");
        _btnManager?.RemoveFromClassList("gfx-btn--active");

        switch (level)
        {
            case 0: _btnClerk?.AddToClassList("gfx-btn--active");      break;
            case 1: _btnSupervisor?.AddToClassList("gfx-btn--active"); break;
            case 2: _btnManager?.AddToClassList("gfx-btn--active");    break;
        }
    }

    private void ShowTooltip(VisualElement anchor, string text)
    {
        if (_tooltipLabel == null) return;
        _tooltipLabel.text = text;
        _tooltipLabel.style.display = DisplayStyle.Flex;

        // Position just above the anchor button
        var bounds = anchor.worldBound;
        _tooltipLabel.style.left = bounds.x + bounds.width * 0.5f - 40f;
        _tooltipLabel.style.top  = bounds.y - 32f;
    }

    private void HideTooltip()
    {
        if (_tooltipLabel != null)
            _tooltipLabel.style.display = DisplayStyle.None;
    }

    private void ApplyStoredSettings()
    {
        // Graphics preset
        string savedPreset = PlayerPrefs.GetString("GraphicsPresetName", "Ultra");
        ApplyGraphicsPreset(savedPreset);

        // Difficulty
        int savedDiff = PlayerPrefs.GetInt("Difficulty", 0);
        ApplyDifficulty(savedDiff);

        // Volumes
        float gv = PlayerPrefs.GetFloat(gameMixerParam, 1f);
        float mv = PlayerPrefs.GetFloat(musicMixerParam, 0.7f);
        if (_gameVolumeSlider  != null) _gameVolumeSlider.value  = gv;
        if (_musicVolumeSlider != null) _musicVolumeSlider.value = mv;
        if (audioMixer != null)
        {
            SetVolume(gameMixerParam, gv);
            SetVolume(musicMixerParam, mv);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Popup helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void HideAllPopups()
    {
        SetVisible(_namePopup,       false);
        SetVisible(_loadPopup,       false);
        SetVisible(_settingsPopup,   false);
    }

    private void ShowPopup(VisualElement popup)
    {
        HideAllPopups();
        if (popup != null) SetVisible(popup, true);
    }

    private static void SetVisible(VisualElement el, bool visible)
    {
        if (el != null)
            el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
