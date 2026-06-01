using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class WelcomeOverlayManager : MonoBehaviour
{
    [Tooltip("Assign MyBoss.png here")]
    [SerializeField] private Texture2D bossImage;

    [Tooltip("Assign MyBoss.wav here")]
    [SerializeField] private AudioClip bossClip;

    private AudioSource _audioSource;

    public void Show(string playerName)
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        var backdrop = root.Q("welcome-backdrop");

        var bossAvatar = root.Q("boss-avatar");
        if (bossAvatar != null && bossImage != null)
            bossAvatar.style.backgroundImage = new StyleBackground(bossImage);

        var welcomeText = root.Q<Label>("welcome-text");
        if (welcomeText != null)
            welcomeText.text = BuildMessage(playerName);

        if (backdrop != null)
            backdrop.style.display = DisplayStyle.Flex;

        PlayBossAudio();

        root.Q<Button>("btn-dismiss")?.RegisterCallback<ClickEvent>(_ =>
        {
            StopBossAudio();
            if (backdrop != null)
                backdrop.style.display = DisplayStyle.None;
        });
    }

    private void PlayBossAudio()
    {
        if (bossClip == null) return;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.clip   = bossClip;
        _audioSource.volume = 1f;   // AudioSource clamps to 1 — this is max
        _audioSource.pitch  = 1.25f;
        _audioSource.loop   = true;
        _audioSource.Play();
    }

    private void StopBossAudio()
    {
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
    }

    private static string BuildMessage(string name)
    {
        return $"Listen up, {name}! You're already late on your FIRST day.\n\n" +
               "Get building — start with the foundation. " +
               "Check your money. This stuff ain't cheap.\n\n" +
               "Run dry? You can sell things back, but you lose 10% every time. " +
               "So every screw-up costs you.\n\n" +
               "Now stop standing around and GET TO WORK!";
    }
}
