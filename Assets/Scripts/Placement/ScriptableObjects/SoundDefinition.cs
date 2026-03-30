using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Sound Library", fileName = "SoundLibrary")]
public class SoundDefinition : ScriptableObject
{
    [System.Serializable]
    public class SoundEntry
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 2f)] public float pitch = 1f;
    }

    public SoundEntry[] sounds;
}
