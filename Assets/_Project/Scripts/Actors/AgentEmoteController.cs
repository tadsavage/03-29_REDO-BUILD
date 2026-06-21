using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Foundation element for agent "personality": every <see cref="interval"/> seconds the agent
/// rolls a <see cref="chance"/> to pop a random expression bubble above its head for a few
/// seconds. Right now it's purely cosmetic flavour — later these emotes will be driven by the
/// agent's actual state (mood, fatigue, events) once the core loop exists.
///
/// The pool auto-fills from Resources/Emotes; leave the list empty to use every bubble, or
/// assign a subset in the inspector to restrict which emotes this agent can show.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EmoteBubble))]
public class AgentEmoteController : MonoBehaviour
{
    [Header("Random emote timing")]
    [Tooltip("Seconds between emote rolls.")]
    [SerializeField] private float interval = 30f;
    [Tooltip("Chance (0–1) that a roll actually shows a bubble.")]
    [Range(0f, 1f)]
    [SerializeField] private float chance = 0.5f;
    [Tooltip("How long a random emote stays up, in seconds.")]
    [SerializeField] private float displayDuration = 4f;

    [Header("Emote pool")]
    [Tooltip("Leave empty to auto-load every bubble in Resources/Emotes. Assign a subset to restrict this agent.")]
    [SerializeField] private Sprite[] emotes;

    private EmoteBubble _bubble;
    private float       _timer;

    private void Awake()
    {
        _bubble = GetComponent<EmoteBubble>();
        if (_bubble == null) _bubble = gameObject.AddComponent<EmoteBubble>();
        if (emotes == null || emotes.Length == 0)
            emotes = EmoteLibrary.All;
    }

    private void OnEnable()
    {
        // Stagger the first roll so a crowd of agents don't all fire on the same frame.
        _timer = Random.Range(0f, interval);
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = interval;

        if (emotes == null || emotes.Length == 0) return;
        if (Random.value > chance) return;

        Sprite pick = emotes[Random.Range(0, emotes.Length)];
        _bubble.Play(pick, displayDuration);
    }
}
