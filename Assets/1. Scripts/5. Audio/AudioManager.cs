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
            // If music is already playing, wait until it finishes
            if (isMusicPlaying)
            {
                yield return new WaitForSeconds(10f); // Check less frequently while playing
                continue;
            }

            // Wait for the interval
            yield return new WaitForSeconds(freqCheckIntervalTime * 60f);

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
        musicSource.Play();

        // Fade In
        float timer = 0;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
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
                elapsed += Time.deltaTime;
                musicSource.volume = musicVolume;
                yield return null;
            }
        }

        // Fade Out
        timer = 0;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(1f, 0f, timer / fadeDuration) * musicVolume;
            yield return null;
        }
musicSource.volume = 0;
        musicSource.Stop();
        
        isMusicPlaying = false;
    }
}
