using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    public SoundDefinition soundLibrary;

    [Header("Music Settings")]
    [SerializeField] private List<AudioClip> musicPlaylist = new List<AudioClip>();
    [SerializeField] private float fadeDuration = 5.0f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1.0f;
    [SerializeField] private bool shuffle = true;

    [Header("Music Trigger Settings")]
    [SerializeField, Tooltip("Time between checks in minutes")] 
    private float freqCheckIntervalTime = 5f;
    [SerializeField, Range(0, 100)] 
    private float likelihoodOfSongPlaying = 50f;

    private Dictionary<string, SoundDefinition.SoundEntry> soundMap;
    private AudioSource sfxSource;
    private AudioSource musicSource;

    private List<AudioClip> playOrder = new List<AudioClip>();
    private int currentTrackIndex = -1;
    private bool isMusicPlaying = false;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        sfxSource = gameObject.AddComponent<AudioSource>();
        
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = false;
        musicSource.playOnAwake = false;
        musicSource.volume = 0;

        // Apply saved volume settings (written by MainMenuManager on Done)
        sfxVolume   = PlayerPrefs.GetFloat("GameVolume",  sfxVolume);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", musicVolume);

        soundMap = new Dictionary<string, SoundDefinition.SoundEntry>();

        if (soundLibrary != null)
        {
            foreach (var s in soundLibrary.sounds)
                soundMap[s.name] = s;
        }
    }

    private void Start()
    {
        if (musicPlaylist.Count > 0)
        {
            StartCoroutine(MusicFrequencyCheck());
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void SetSfxVolume(float v)   { sfxVolume   = v; }
    public void SetMusicVolume(float v) { musicVolume = v; }

    /// <summary>Current "Game" volume slider level (0-1), for the many gameplay AudioSources that
    /// live outside AudioManager (doors, forklifts, ambient chatter, UI clicks) and play their own
    /// one-shots directly. Each of those multiplies its own base volume by this at play time, so the
    /// slider affects them immediately without needing a shared AudioMixer.</summary>
    public static float GameVolumeLevel => instance != null ? instance.sfxVolume : 1f;

    /// <summary>Current "Music" volume slider level (0-1).</summary>
    public static float MusicVolumeLevel => instance != null ? instance.musicVolume : 1f;

    public static void Play(string soundName)
    {
        if (instance == null) return;

        if (instance.soundMap.TryGetValue(soundName, out var s))
        {
            instance.sfxSource.pitch = s.pitch;
            instance.sfxSource.PlayOneShot(s.clip, s.volume * instance.sfxVolume);
        }
else
        {
            Debug.LogWarning($"AudioManager: Sound '{soundName}' not found.");
        }
    }

    private IEnumerator MusicFrequencyCheck()
    {
        while (true)
        {
            // Realtime, not WaitForSeconds — music scheduling is presentation, not simulation, so
            // pausing (timeScale 0) must not freeze it forever and speeding up (2x/4x) must not compress
            // how often a track gets a chance to play. WaitForSeconds is scaled and would do both.
            if (isMusicPlaying)
            {
                yield return new WaitForSecondsRealtime(10f); // Check less frequently while playing
                continue;
            }

            // Wait for the interval
            yield return new WaitForSecondsRealtime(freqCheckIntervalTime * 60f);

            // Roll for chance
            if (Random.Range(0f, 100f) <= likelihoodOfSongPlaying)
            {
                StartCoroutine(PlayNextTrack());
            }
        }
    }

    private IEnumerator PlayNextTrack()
    {
        if (musicPlaylist.Count == 0) yield break;
        isMusicPlaying = true;

        // Prepare play order if empty or finished
        if (playOrder.Count == 0 || currentTrackIndex >= playOrder.Count - 1)
        {
            playOrder = new List<AudioClip>(musicPlaylist);
            if (shuffle)
            {
                for (int i = 0; i < playOrder.Count; i++)
                {
                    AudioClip temp = playOrder[i];
                    int randomIndex = Random.Range(i, playOrder.Count);
                    playOrder[i] = playOrder[randomIndex];
                    playOrder[randomIndex] = temp;
                }
            }
            currentTrackIndex = -1;
        }

        currentTrackIndex++;
        AudioClip clip = playOrder[currentTrackIndex];
        
        musicSource.clip = clip;
        // AudioSource playback itself always runs at real speed regardless of Time.timeScale — Unity
        // doesn't scale audio automatically — but this coroutine's OWN fade/wait timers were built on
        // Time.deltaTime, so at 0x (paused) a fade would freeze the track stuck at partial volume
        // forever, and at 2x/4x the fades (and the "hold at full volume" middle section) would run
        // faster than the audio itself, drifting out of sync with the clip. Time.unscaledDeltaTime
        // keeps every fade/hold timer matched to the actual audio regardless of sim speed.
        musicSource.Play();

        // Fade In
        float timer = 0;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(0, 1f, timer / fadeDuration) * musicVolume;
            yield return null;
        }

        // Main Playback Loop (keeps volume in sync)
        float waitTime = clip.length - (fadeDuration * 2);
        if (waitTime > 0)
        {
            float elapsed = 0;
            while (elapsed < waitTime)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = musicVolume;
                yield return null;
            }
        }

        // Fade Out
        timer = 0;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(1f, 0f, timer / fadeDuration) * musicVolume;
            yield return null;
        }
musicSource.volume = 0;
        musicSource.Stop();
        
        isMusicPlaying = false;
    }
}
