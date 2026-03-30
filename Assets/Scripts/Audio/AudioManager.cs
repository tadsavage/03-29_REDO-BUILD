using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    public SoundDefinition soundLibrary;

    private Dictionary<string, SoundDefinition.SoundEntry> soundMap;
    private AudioSource source;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        soundMap = new Dictionary<string, SoundDefinition.SoundEntry>();

        foreach (var s in soundLibrary.sounds)
            soundMap[s.name] = s;
    }

    public static void Play(string soundName)
    {
        if (instance == null) return;

        if (instance.soundMap.TryGetValue(soundName, out var s))
        {
            instance.source.pitch = s.pitch;
            instance.source.PlayOneShot(s.clip, s.volume);
        }
        else
        {
            Debug.LogWarning($"AudioManager: Sound '{soundName}' not found.");
        }
    }
}
