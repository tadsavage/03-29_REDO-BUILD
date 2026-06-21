using UnityEngine;
using System.Collections.Generic;

public class AmbientMumble : MonoBehaviour
{
    [Header("Mumble Clips")]
    [SerializeField] private AudioClip[] mumbleClips;

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField, Range(0.5f, 2f)] private float minPitch = 0.85f;
    [SerializeField, Range(0.5f, 2f)] private float maxPitch = 1.15f;
    [SerializeField, Range(0f, 100f)] private float chanceToMumble = 50f;

    [Header("Spatial Audio")]
    [Tooltip("Full-volume radius in meters (same as ManDoor's fullVolumeDistance).")]
    [SerializeField] private float fullVolumeDistance = 2f;
    [Tooltip("Beyond this distance the mumble is completely silent.")]
    [SerializeField] private float maxHearingDistance = 15f;

    private AudioSource _source;
    private List<AudioClip> _shuffled = new List<AudioClip>();
    private int _index = 0;

    private void Awake()
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake  = false;
        _source.spatialBlend = 1f;
        _source.rolloffMode  = AudioRolloffMode.Linear;
        _source.minDistance  = fullVolumeDistance;
        _source.maxDistance  = maxHearingDistance;
        _source.dopplerLevel = 0f;

        if (mumbleClips != null && mumbleClips.Length > 0)
            Reshuffle();
    }

    public void TryMumble()
    {
        if (mumbleClips == null || mumbleClips.Length == 0) return;
        if (_source.isPlaying) return;
        if (Random.Range(0f, 100f) > chanceToMumble) return;

        if (_index >= _shuffled.Count)
            Reshuffle();

        _source.pitch = Random.Range(minPitch, maxPitch);
        _source.PlayOneShot(_shuffled[_index], volume);
        _index++;
    }

    private void Reshuffle()
    {
        _shuffled.Clear();
        _shuffled.AddRange(mumbleClips);
        for (int i = _shuffled.Count - 1; i > 0; i--)
        {
            int k = Random.Range(0, i + 1);
            (_shuffled[k], _shuffled[i]) = (_shuffled[i], _shuffled[k]);
        }
        _index = 0;
    }
}
