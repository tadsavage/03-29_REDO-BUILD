using UnityEngine;
using UnityEngine.UIElements;

public class TopBarSettingsPanel : MonoBehaviour
{
    // Graphics presets
    private Button _igBtnScreamin, _igBtnGood, _igBtnToaster;

    // Difficulty
    private Button _igBtnClerk, _igBtnSupervisor, _igBtnManager;

    // Resolution & audio
    private DropdownField _igResolutionDropdown;
    private Slider _igGameVolumeSlider, _igMusicVolumeSlider;
    private int _igCurrentResIdx = 1;
    private int _igPendingResIdx = -1;

    // UI state
    private VisualElement _igSettingsOverlay;
    private VisualElement _igResConfirmOverlay;

    private static readonly Resolution[] CommonResolutions =
    {
        new Resolution { width = 1280, height = 720  },
        new Resolution { width = 1920, height = 1080 },
        new Resolution { width = 2560, height = 1440 },
        new Resolution { width = 3840, height = 2160 },
    };

    private const string GameVolParam  = "GameVolume";
    private const string MusicVolParam = "MusicVolume";

    public void Init(VisualElement hudRoot)
    {
        _igSettingsOverlay    = hudRoot.Q<VisualElement>("in-game-settings-overlay");
        _igResConfirmOverlay  = hudRoot.Q<VisualElement>("ig-res-confirm-overlay");
        _igBtnScreamin        = hudRoot.Q<Button>("ig-btn-screamin");
        _igBtnGood            = hudRoot.Q<Button>("ig-btn-good");
        _igBtnToaster         = hudRoot.Q<Button>("ig-btn-toaster");
        _igBtnClerk           = hudRoot.Q<Button>("ig-btn-diff-clerk");
        _igBtnSupervisor      = hudRoot.Q<Button>("ig-btn-diff-supervisor");
        _igBtnManager         = hudRoot.Q<Button>("ig-btn-diff-manager");
        _igResolutionDropdown = hudRoot.Q<DropdownField>("ig-resolution-dropdown");
        _igGameVolumeSlider   = hudRoot.Q<Slider>("ig-slider-game-volume");
        _igMusicVolumeSlider  = hudRoot.Q<Slider>("ig-slider-music-volume");

        // Graphics preset buttons
        _igBtnScreamin?.RegisterCallback<ClickEvent>(_ => ApplyGraphicsPreset("Ultra"));
        _igBtnGood?.RegisterCallback<ClickEvent>(_ => ApplyGraphicsPreset("Good"));
        _igBtnToaster?.RegisterCallback<ClickEvent>(_ => ApplyGraphicsPreset("Toaster"));

        // Difficulty buttons
        _igBtnClerk?.RegisterCallback<ClickEvent>(_ => ApplyDifficulty(0));
        _igBtnSupervisor?.RegisterCallback<ClickEvent>(_ => ApplyDifficulty(1));
        _igBtnManager?.RegisterCallback<ClickEvent>(_ => ApplyDifficulty(2));

        // Settings done button
        hudRoot.Q<Button>("ig-settings-done")?.RegisterCallback<ClickEvent>(_ => Close());

        // Resolution confirmation
        hudRoot.Q<Button>("ig-btn-res-yes")?.RegisterCallback<ClickEvent>(_ => OnResolutionAccepted());
        hudRoot.Q<Button>("ig-btn-res-no")?.RegisterCallback<ClickEvent>(_ => OnResolutionCancelled());

        // Volume sliders
        if (_igGameVolumeSlider  != null) _igGameVolumeSlider.RegisterValueChangedCallback(evt => SetVolume(GameVolParam, evt.newValue));
        if (_igMusicVolumeSlider != null) _igMusicVolumeSlider.RegisterValueChangedCallback(evt => SetVolume(MusicVolParam, evt.newValue));

        BuildResolutionDropdown();
        ApplyStoredSettings();
    }

    public void Open()
    {
        if (_igSettingsOverlay != null)
        {
            _igSettingsOverlay.style.display = DisplayStyle.Flex;
            _igSettingsOverlay.pickingMode   = PickingMode.Position;
        }
    }

    public void Close()
    {
        if (_igSettingsOverlay != null)
        {
            _igSettingsOverlay.style.display = DisplayStyle.None;
            _igSettingsOverlay.pickingMode   = PickingMode.Ignore;
        }
        if (_igResConfirmOverlay != null)
        {
            _igResConfirmOverlay.style.display = DisplayStyle.None;
            _igResConfirmOverlay.pickingMode   = PickingMode.Ignore;
        }
        _igPendingResIdx = -1;
    }

    private void ApplyStoredSettings()
    {
        string preset = PlayerPrefs.GetString("GraphicsPresetName", "Ultra");
        ApplyGraphicsPreset(preset, notify: false);

        int diff = PlayerPrefs.GetInt("Difficulty", 0);
        ApplyDifficulty(diff);

        float gv = PlayerPrefs.GetFloat(GameVolParam, 1f);
        float mv = PlayerPrefs.GetFloat(MusicVolParam, 0.7f);
        if (_igGameVolumeSlider  != null) _igGameVolumeSlider.SetValueWithoutNotify(gv);
        if (_igMusicVolumeSlider != null) _igMusicVolumeSlider.SetValueWithoutNotify(mv);
    }

    private void ApplyGraphicsPreset(string preset, bool notify = true)
    {
        var mgr = FindAnyObjectByType<GraphicsPresetManager>();
        if (mgr != null)
            mgr.ApplyPreset((GraphicsPresetManager.Preset)System.Enum.Parse(
                typeof(GraphicsPresetManager.Preset), preset), notify);

        _igBtnScreamin?.RemoveFromClassList("gfx-btn--active");
        _igBtnGood?.RemoveFromClassList("gfx-btn--active");
        _igBtnToaster?.RemoveFromClassList("gfx-btn--active");
        switch (preset)
        {
            case "Ultra":   _igBtnScreamin?.AddToClassList("gfx-btn--active"); break;
            case "Good":    _igBtnGood?.AddToClassList("gfx-btn--active");     break;
            case "Toaster": _igBtnToaster?.AddToClassList("gfx-btn--active");  break;
        }
        PlayerPrefs.SetString("GraphicsPresetName", preset);
    }

    private void ApplyDifficulty(int level)
    {
        PlayerPrefs.SetInt("Difficulty", level);
        _igBtnClerk?.RemoveFromClassList("gfx-btn--active");
        _igBtnSupervisor?.RemoveFromClassList("gfx-btn--active");
        _igBtnManager?.RemoveFromClassList("gfx-btn--active");
        switch (level)
        {
            case 0: _igBtnClerk?.AddToClassList("gfx-btn--active");      break;
            case 1: _igBtnSupervisor?.AddToClassList("gfx-btn--active"); break;
            case 2: _igBtnManager?.AddToClassList("gfx-btn--active");    break;
        }
    }

    private void BuildResolutionDropdown()
    {
        if (_igResolutionDropdown == null) return;

        var choices = new System.Collections.Generic.List<string>();
        foreach (var r in CommonResolutions)
            choices.Add($"{r.width} x {r.height}");
        _igResolutionDropdown.choices = choices;

        int w = Screen.width, h = Screen.height;
        _igCurrentResIdx = 1;
        for (int i = 0; i < CommonResolutions.Length; i++)
            if (CommonResolutions[i].width == w && CommonResolutions[i].height == h)
                _igCurrentResIdx = i;
        _igResolutionDropdown.SetValueWithoutNotify(choices[_igCurrentResIdx]);

        _igResolutionDropdown.RegisterValueChangedCallback(evt =>
        {
            int idx = _igResolutionDropdown.index;
            if (idx < 0 || idx >= CommonResolutions.Length || idx == _igCurrentResIdx) return;
            _igPendingResIdx = idx;
            if (_igResConfirmOverlay != null)
            {
                _igResConfirmOverlay.style.display = DisplayStyle.Flex;
                _igResConfirmOverlay.pickingMode   = PickingMode.Position;
            }
        });
    }

    private void OnResolutionAccepted()
    {
        if (_igPendingResIdx >= 0 && _igPendingResIdx < CommonResolutions.Length)
        {
            var r = CommonResolutions[_igPendingResIdx];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen);
            _igCurrentResIdx = _igPendingResIdx;
            PlayerPrefs.SetInt("ResolutionIndex", _igCurrentResIdx);
        }
        _igPendingResIdx = -1;
        if (_igResConfirmOverlay != null)
        {
            _igResConfirmOverlay.style.display = DisplayStyle.None;
            _igResConfirmOverlay.pickingMode   = PickingMode.Ignore;
        }
    }

    private void OnResolutionCancelled()
    {
        if (_igResolutionDropdown != null)
            _igResolutionDropdown.SetValueWithoutNotify(_igResolutionDropdown.choices[_igCurrentResIdx]);
        _igPendingResIdx = -1;
        if (_igResConfirmOverlay != null)
        {
            _igResConfirmOverlay.style.display = DisplayStyle.None;
            _igResConfirmOverlay.pickingMode   = PickingMode.Ignore;
        }
    }

    private void SetVolume(string param, float linear)
    {
        if (AudioManager.instance != null)
        {
            if (param == GameVolParam)  AudioManager.instance.SetSfxVolume(linear);
            else                        AudioManager.instance.SetMusicVolume(linear);
        }
        PlayerPrefs.SetFloat(param, linear);
    }
}
