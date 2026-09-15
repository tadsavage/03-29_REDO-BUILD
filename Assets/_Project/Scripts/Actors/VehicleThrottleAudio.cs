using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class VehicleThrottleAudio : MonoBehaviour
{
    [Header("Spatial Audio")]
    [SerializeField] private float hearingDistance = 25f;

    [Header("Engine Audio Settings")]
    [SerializeField] private AudioClip runningClip;
    [SerializeField] private float minPitch = 0.75f;
    [SerializeField] private float maxPitch = 1.3f;
    [SerializeField] private float engineVolume = 0.5f;
    [SerializeField] private float pitchOffset = 0f;
    [SerializeField] private float fadeSpeed = 4f;

    [Header("Honker Settings")]
    [SerializeField] private bool _honker = true;
    [SerializeField] private AudioClip honkClip;
    [SerializeField] private float honkVolume = 0.7f;
    [SerializeField] private float honkPitch = 1.0f; // Fixed pitch for honks
    [Range(0f, 1f)]
    [SerializeField] private float honkProbability = 0.5f;

    [Header("Backup Alarm Settings")]
    [Tooltip("Plays once when the truck starts its final backing/pivot maneuver into the dock door (see TruckController.BeginReversingArcAroundPivot1 callers). Assign the truck-backing-up.wav clip.")]
    [SerializeField] private AudioClip backupAlarmClip;
    [SerializeField] private float backupAlarmVolume = 0.6f;

    private AudioSource _engineSource;
    private AudioSource _honkSource;
    private AudioSource _backupAlarmSource;
    private NavMeshAgent _agent;
    private float _targetVolume;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();

        // Create Engine Source
        _engineSource = gameObject.AddComponent<AudioSource>();
        _engineSource.clip = runningClip;
        _engineSource.loop = true;
        _engineSource.playOnAwake = false;
        _engineSource.spatialBlend = 1.0f; 
        _engineSource.rolloffMode = AudioRolloffMode.Logarithmic;
        _engineSource.minDistance = 2f;
        _engineSource.maxDistance = hearingDistance;
        _engineSource.volume = 0;

        // Create Honk Source (Fixed Pitch)
        _honkSource = gameObject.AddComponent<AudioSource>();
        _honkSource.loop = false;
        _honkSource.playOnAwake = false;
        _honkSource.spatialBlend = 1.0f;
        _honkSource.rolloffMode = AudioRolloffMode.Logarithmic;
        _honkSource.minDistance = 5f;
        _honkSource.maxDistance = hearingDistance * 1.2f;
        _honkSource.pitch = honkPitch;

        // Create Backup Alarm Source (one-shot, plays once per docking approach)
        _backupAlarmSource = gameObject.AddComponent<AudioSource>();
        _backupAlarmSource.loop = false;
        _backupAlarmSource.playOnAwake = false;
        _backupAlarmSource.spatialBlend = 1.0f;
        _backupAlarmSource.rolloffMode = AudioRolloffMode.Logarithmic;
        _backupAlarmSource.minDistance = 5f;
        _backupAlarmSource.maxDistance = hearingDistance * 1.2f;
    }

    private void Start()
    {
        if (runningClip != null) _engineSource.Play();
    }

    private void Update()
    {
        if (_agent == null || _engineSource == null) return;

        // 1. Throttle Pitch Logic
        float currentSpeed = _agent.velocity.magnitude;
        float maxSpeed = _agent.speed;
        float speedPercent = Mathf.Clamp01(currentSpeed / maxSpeed);
        
        // Apply pitch offset to engine
        _engineSource.pitch = Mathf.Lerp(minPitch, maxPitch, speedPercent) + pitchOffset;

        // 2. Volume Fading Logic (Engine) — scaled by the Settings "Game" volume slider so a
        // continuously-looping engine hum actually responds to it in real time, not just at play().
        _targetVolume = (currentSpeed > 0.1f ? engineVolume : 0f) * AudioManager.GameVolumeLevel;
        _engineSource.volume = Mathf.MoveTowards(_engineSource.volume, _targetVolume, Time.deltaTime * fadeSpeed);
    }

    /// <summary>Plays the truck's reverse-backup alarm once. Call this exactly when the truck begins
    /// its final pivot-and-reverse maneuver into the dock door, not on every frame of that state --
    /// this fires a single PlayOneShot, it does not loop or manage its own stop/start.</summary>
    public void TriggerBackupAlarm()
    {
        if (backupAlarmClip == null || _backupAlarmSource == null) return;
        _backupAlarmSource.PlayOneShot(backupAlarmClip, backupAlarmVolume * AudioManager.GameVolumeLevel);
    }

    public void TriggerArrivalHonk()
    {
        if (!_honker || honkClip == null) return;
        
        if (Random.value > honkProbability) return;

        int honkCount = Random.value > 0.5f ? 3 : 1;
        StartCoroutine(HonkRoutine(honkCount));
    }

    private IEnumerator HonkRoutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Play on the dedicated honk source to ensure fixed pitch
            _honkSource.pitch = honkPitch;
            _honkSource.PlayOneShot(honkClip, honkVolume * AudioManager.GameVolumeLevel);
            yield return new WaitForSeconds(0.25f);
        }
    }
}
