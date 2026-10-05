using UnityEngine;

/// <summary>
/// Tuning values for <see cref="GagMouthMotion"/>. One shared asset (Resources/Resource_AvatarSystemAssets/GagMouthSettings) that every
/// gagged avatar reads, so you can edit it in the Inspector - even while the game is running - and every gagged worker updates at once.
/// Create it with the menu Assets > Create > Avatar > Gag Mouth Settings if it is ever missing (GagMouthMotion falls back to the defaults below).
/// </summary>
[CreateAssetMenu(menuName = "Avatar/Gag Mouth Settings", fileName = "GagMouthSettings")]
public class GagMouthSettings : ScriptableObject
{
    [Header("Jaw angle (degrees)")]
    [Tooltip("How far the jaw is held open even between groans. Lets the lower lip drop away from the gag ball.")]
    [Range(0f, 40f)] public float restOpenDegrees = 16f;
    [Tooltip("Peak jaw opening of the strongest groan.")]
    [Range(0f, 50f)] public float maxOpenDegrees = 24f;
    [Tooltip("The weakest groan is this fraction of the maximum.")]
    [Range(0f, 1f)] public float minStrength = 0.55f;
    [Tooltip("Slight sideways jaw drift while groaning (degrees).")]
    [Range(0f, 10f)] public float wobbleDegrees = 2.5f;

    [Header("Timing (seconds)")]
    [Tooltip("Length of one groan (open + close): shortest.")] public float groanSecondsMin = 0.85f;
    [Tooltip("Length of one groan (open + close): longest.")] public float groanSecondsMax = 1.25f;
    [Tooltip("Groans per burst: fewest.")] [Range(1, 6)] public int groansPerBurstMin = 1;
    [Tooltip("Groans per burst: most.")] [Range(1, 6)] public int groansPerBurstMax = 3;
    [Tooltip("Pause between bursts: shortest.")] public float pauseSecondsMin = 0.45f;
    [Tooltip("Pause between bursts: longest.")] public float pauseSecondsMax = 1.40f;
    [Tooltip("SmoothDamp time on the jaw: higher = softer, laggier.")] [Range(0.01f, 0.5f)] public float smoothing = 0.07f;
}
