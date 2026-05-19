using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    public SoundDefinition soundLibrary;

    [Header("Music Settings")]
    [SerializeField] private List<AudioClip> musicPlaylist = new List<AudioClip>();
    [SerializeField] private float fadeDuration = 2.0f;
    [SerializeField] private float musicVolume = 0.5f;
    [SerializeField] private bool shuffle = true;

    private Dictionary<string, SoundDefinition.SoundEntry> soundMap;
    private AudioSource sfxSource;
    private AudioSource musicSource;
    
    private List<AudioClip> playOrder = new List<AudioClip>();
    private int currentTrackIndex = -1;

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
            StartCoroutine(MusicLoop());
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
            instance.sfxSource.PlayOneShot(s.clip, s.volume);
        }
        else
        {
            Debug.LogWarning($"AudioManager: Sound '{soundName}' not found.");
        }
    }

    private IEnumerator MusicLoop()
    {
        while (true)
        {
            if (musicPlaylist.Count == 0) yield return new WaitForSeconds(1f);

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
                musicSource.volume = Mathf.Lerp(0, musicVolume, timer / fadeDuration);
                yield return null;
            }
            musicSource.volume = musicVolume;

            // Wait for track to near end
            float waitTime = clip.length - (fadeDuration * 2);
            if (waitTime > 0)
            {
                yield return new WaitForSeconds(waitTime);
            }

            // Fade Out
            timer = 0;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(musicVolume, 0, timer / fadeDuration);
                yield return null;
            }
            musicSource.volume = 0;
            musicSource.Stop();
            
            yield return new WaitForSeconds(0.5f); // Short gap between tracks
        }
    }
}
