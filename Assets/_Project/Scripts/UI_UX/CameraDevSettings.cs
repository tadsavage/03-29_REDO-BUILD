using UnityEngine;

/// <summary>
/// Single persistent source of truth for player camera feel — move speed, zoom speed,
/// and orbit/pitch sensitivity. Backed by PlayerPrefs so it survives scene reloads and editor
/// restarts. FreeLookCamera reads from here instead of its own Inspector-serialized fields
/// (which is the "something else" that kept drifting/overwriting values); the Tools window's
/// Dev Settings panel is the only place these get edited.
/// </summary>
public static class CameraDevSettings
{
    private const string KeyMoveSpeed         = "DevCam_MoveSpeed";
    private const string KeyZoomSpeed         = "DevCam_ZoomSpeed";
    private const string KeyPitchSensitivity  = "DevCam_PitchSensitivity";
    private const string KeyOrbitSensitivity  = "DevCam_OrbitSensitivity";
    private const string KeyMinCameraHeight   = "DevCam_MinCameraHeight";

    private const int   DefaultMoveSpeed        = 8;
    private const float DefaultZoomSpeed        = 4f;
    private const float DefaultPitchSensitivity = 0.15f;
    private const float DefaultOrbitSensitivity = 0.25f;
    // Minimum camera world Y position — prevents clipping through the ground
    // when zooming/pitching low.
    private const float DefaultMinCameraHeight  = 2f;

    public const int   MoveSpeedMin = 1,  MoveSpeedMax = 15;
    public const float ZoomSpeedMin = 1f, ZoomSpeedMax = 5f;
    public const float PitchSensitivityMin = 0f, PitchSensitivityMax = 2f;
    public const float OrbitSensitivityMin = 0f, OrbitSensitivityMax = 2f;
    public const float MinCameraHeightMin = 0f, MinCameraHeightMax = 5f;

    /// <summary>Fired whenever any setting changes, so live camera instances re-pull values
    /// immediately instead of waiting for their next Awake/scene load.</summary>
    public static event System.Action OnChanged;

    public static int MoveSpeed
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyMoveSpeed, DefaultMoveSpeed), MoveSpeedMin, MoveSpeedMax);
        set { PlayerPrefs.SetInt(KeyMoveSpeed, Mathf.Clamp(value, MoveSpeedMin, MoveSpeedMax)); OnChanged?.Invoke(); }
    }

    public static float ZoomSpeed
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeyZoomSpeed, DefaultZoomSpeed), ZoomSpeedMin, ZoomSpeedMax);
        set { PlayerPrefs.SetFloat(KeyZoomSpeed, Mathf.Clamp(value, ZoomSpeedMin, ZoomSpeedMax)); OnChanged?.Invoke(); }
    }

    public static float PitchSensitivity
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeyPitchSensitivity, DefaultPitchSensitivity), PitchSensitivityMin, PitchSensitivityMax);
        set { PlayerPrefs.SetFloat(KeyPitchSensitivity, Mathf.Clamp(value, PitchSensitivityMin, PitchSensitivityMax)); OnChanged?.Invoke(); }
    }

    public static float OrbitSensitivity
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeyOrbitSensitivity, DefaultOrbitSensitivity), OrbitSensitivityMin, OrbitSensitivityMax);
        set { PlayerPrefs.SetFloat(KeyOrbitSensitivity, Mathf.Clamp(value, OrbitSensitivityMin, OrbitSensitivityMax)); OnChanged?.Invoke(); }
    }

    /// <summary>Floor for the camera's world Y position — ApplyTransform() clamps to this so
    /// zooming/pitching low toward a focal point near ground level can never clip the camera
    /// through the floor.</summary>
    public static float MinCameraHeight
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(KeyMinCameraHeight, DefaultMinCameraHeight), MinCameraHeightMin, MinCameraHeightMax);
        set { PlayerPrefs.SetFloat(KeyMinCameraHeight, Mathf.Clamp(value, MinCameraHeightMin, MinCameraHeightMax)); OnChanged?.Invoke(); }
    }
}
