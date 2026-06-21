using System.Collections;
using UnityEngine;

public class ManDoorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform doorTransform;

    [Header("Settings")]
    [SerializeField] private float startRotY = 0f;
    [SerializeField] private float endRotY = -90f;
    [SerializeField] private float speed = 150f; // Degrees per second

    private Coroutine _moveCoroutine;
    private AgentType _allowedAgents = AgentType.Human | AgentType.Rat;

    [Header("Audio")]
    [Tooltip("DoorOpen.wav � plays when an agent enters and the door opens.")]
    [SerializeField] private AudioClip openClip;
    [Tooltip("DoorClose.wav � plays when an agent exits and the door closes.")]
    [SerializeField] private AudioClip closeClip;
    [SerializeField, Range(0f, 1f)] private float doorVolume = 1f;

    [Header("Spatial Audio (3D)")]
    [Tooltip("Distance (meters) at which the door sound is at full volume. " +
             "Inside this radius it does not get any louder.")]
    [SerializeField] private float fullVolumeDistance = 2f;
    [Tooltip("Distance (meters) beyond which the door sound is silent. " +
             "Volume scales up as the camera moves closer than this.")]
    [SerializeField] private float maxHearingDistance = 10f;
    [SerializeField] private AudioSource audioSource;

    private void Awake()
    {
        if (doorTransform == null)
            doorTransform = transform.Find("DoorFrame/Door");

        if (doorTransform != null)
            doorTransform.localRotation = Quaternion.Euler(0, startRotY, 0);
        // Make sure we have an AudioSource to play door sounds through.
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        ConfigureSpatialAudio();
    }
    /// <summary>
    /// Sets the door's AudioSource to emit a 3D positional sound: full volume up
    /// close, fading to silence at <see cref="maxHearingDistance"/> meters away.
    /// </summary>
    private void ConfigureSpatialAudio()
    {
        if (audioSource == null) return;

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;                       // fully 3D � comes from the door
        audioSource.rolloffMode = AudioRolloffMode.Linear;  // clean fade to silence at max distance
        audioSource.minDistance = fullVolumeDistance;
        audioSource.maxDistance = maxHearingDistance;
        audioSource.dopplerLevel = 0f;                       // no pitch shift from movement
    }

    private void Start()
    {
        var bd = GetComponentInParent<BuildingData>();
        if (bd != null && bd.Data != null)
            _allowedAgents = bd.Data.allowedAgents;
    }

    private bool IsAllowed(Collider other)
    {
        var tag = other.GetComponentInParent<AgentTypeTag>();
        return tag != null && (_allowedAgents & tag.agentType) != 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAllowed(other)) return;
        StopMoving();
        _moveCoroutine = StartCoroutine(RotateDoor(endRotY));
        PlayClip(openClip);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsAllowed(other)) return;
        StopMoving();
        _moveCoroutine = StartCoroutine(RotateDoor(startRotY));
        PlayClip(closeClip);
    }
    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) 
        {
                Debug.LogWarning("AudioClip or AudioSource is missing. Cannot play door sound.");
                return;
        }
        audioSource.PlayOneShot(clip, doorVolume);
    }

    private void StopMoving()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
    }

    private IEnumerator RotateDoor(float targetY)
    {
        if (doorTransform == null) yield break;

        Quaternion targetRotation = Quaternion.Euler(0, targetY, 0);

        // Use RotateTowards for a constant, smooth swing speed
        while (Quaternion.Angle(doorTransform.localRotation, targetRotation) > 0.1f)
        {
            doorTransform.localRotation = Quaternion.RotateTowards(
                doorTransform.localRotation,
                targetRotation,
                speed * Time.deltaTime
            );
            yield return null;
        }
        doorTransform.localRotation = targetRotation;
    }
}