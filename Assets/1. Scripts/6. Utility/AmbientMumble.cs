using UnityEngine;
using System.Collections.Generic;

public class AmbientMumble : MonoBehaviour
{
    [Header("Mumble Settings")]
    [SerializeField] private AudioClip[] mumbleClips;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField, Range(0.5f, 2f)] private float minPitch = 0.8f;
    [SerializeField, Range(0.5f, 2f)] private float maxPitch = 1.2f;
    [SerializeField, Range(0f, 100f)] private float chanceToMumble = 50f;

    private AudioSource _audioSource;
    private List<AudioClip> _shuffledClips = new List<AudioClip>();
    private int _currentIndex = 0;

    private void Awake()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 1.0f; // 3D sound
        _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        _audioSource.minDistance = 2f;
        _audioSource.maxDistance = 20f;
        _audioSource.playOnAwake = false;
        
        if (mumbleClips != null && mumbleClips.Length > 0)
        {
            ResetShuffle();
        }
    }

    private void ResetShuffle()
    {
        _shuffledClips.Clear();
        _shuffledClips.AddRange(mumbleClips);
        
        // Fisher-Yates shuffle
        for (int i = _shuffledClips.Count - 1; i > 0; i--)
        {
            int k = Random.Range(0, i + 1);
            AudioClip value = _shuffledClips[k];
            _shuffledClips[k] = _shuffledClips[i];
            _shuffledClips[i] = value;
        }
        
        _currentIndex = 0;
    }

    public void TryMumble()
    {
        if (mumbleClips == null || mumbleClips.Length == 0) return;

        if (Random.Range(0f, 100f) <= chanceToMumble)
        {
            if (_currentIndex >= _shuffledClips.Count)
            {
                ResetShuffle();
            }

            _audioSource.pitch = Random.Range(minPitch, maxPitch);
            _audioSource.PlayOneShot(_shuffledClips[_currentIndex], volume);
            _currentIndex++;
        }
    }
}
