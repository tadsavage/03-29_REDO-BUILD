using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

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

    // ── UI References ─────────────────────────────────────────────────────────
    private VisualElement _root;
    private VisualElement _mainPanel;

    // Popups
    private VisualElement _namePopup;
    private VisualElement _welcomeBackdrop;  // full-screen dim layer
    private VisualElement _loadPopup;
    private VisualElement _settingsPopup;

    // Name popup
    private TextField _nameField;

    // Welcome
    private Label _welcomeText;

    // Load
    private VisualElement _saveSlotList;

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

        CacheElements();
        WireButtons();
        ApplyStoredSettings();
        HideAllPopups();
        ApplyLogoImage();

        // Restore player name if it exists
        _playerName = PlayerPrefs.GetString("PlayerName", "");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Setup
    // ─────────────────────────────────────────────────────────────────────────

    private void CacheElements()
    {
        _mainPanel     = _root.Q("main-panel");
        _namePopup       = _root.Q("name-popup");
        _welcomeBackdrop = _root.Q("welcome-backdrop");
        _loadPopup       = _root.Q("load-popup");
        _settingsPopup   = _root.Q("settings-popup");

        _nameField    = _root.Q<TextField>("name-field");
        _welcomeText  = _root.Q<Label>("welcome-text");
        _saveSlotList = _root.Q("save-slot-list");

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

        // Welcome popup (kept in UXML for Resume path, unused for now)
        _root.Q<Button>("btn-build")?.RegisterCallback<ClickEvent>(_ => LoadGameScene());

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

        // Go straight to the game — the welcome overlay shows there
        LoadGameScene();
    }

    private void ShowWelcome(string name)
    {
        string msg =
            $"Well, {name}! Time to get to it.\n\n" +
            $"You're already late on your first day.\n\n" +
            $"Get building! I suggest starting with the foundation first, " +
            $"but make sure you check your money — this shit ain't cheap.\n\n" +
            $"And once you run out, you'll need to sell stuff back... but you lose 10% each time. " +
            $"So every time you screw up, you have less to work with.\n\n" +
            $"Good luck!!";

        var welcomeText = _root.Q<Label>("welcome-text");
        if (welcomeText != null) welcomeText.text = msg;

        HideAllPopups();
        SetVisible(_welcomeBackdrop, true);
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
    // Save Slot List (simple version for proto)
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildSaveSlotList()
    {
        if (_saveSlotList == null) return;
        _saveSlotList.Clear();

        // Check autosave
        AddSlotButton("autosave", "Autosave");

        // Check slots 0-7
        for (int i = 0; i < 8; i++)
        {
            string slotName = $"slot_{i}";
            string path = System.IO.Path.Combine(Application.dataPath, "_Saves", slotName + ".json");
            if (System.IO.File.Exists(path))
                AddSlotButton(slotName, $"Slot {i + 1}");
        }
    }

    private void AddSlotButton(string saveName, string displayName)
    {
        var btn = new Button();
        btn.text = displayName;
        btn.AddToClassList("popup-btn");
        btn.AddToClassList("popup-btn-grey");
        btn.style.marginBottom = 6;

        string sn = saveName;
        btn.clicked += () =>
        {
            PlayerPrefs.SetString("LastSaveName", sn);
            LoadGameScene();
        };
        _saveSlotList?.Add(btn);
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
        SetVisible(_welcomeBackdrop, false);
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
