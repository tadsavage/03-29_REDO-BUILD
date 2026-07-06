using UnityEngine;
using System.Collections;

public class RollupDoorController : MonoBehaviour
{
    [SerializeField] private Transform doorPanel;
    [SerializeField] private float startY = 0f;
    [SerializeField] private float endY = 3.8f;
    [SerializeField] private float speed = 2f;

    [Header("Audio")]
    [Tooltip("RytechOpen.wav — plays when an agent enters and the door rolls up.")]
    [SerializeField] private AudioClip openClip;
    [Tooltip("RytechClose.wav — plays when an agent exits and the door rolls down.")]
    [SerializeField] private AudioClip closeClip;
    [SerializeField, Range(0f, 1f)] private float doorVolume = 1f;

    [Header("Spatial Audio (3D)")]
    [Tooltip("Distance (meters) at which the door sound is at full volume. " +
             "Inside this radius it does not get any louder.")]
    [SerializeField] private float fullVolumeDistance = 2f;
    [Tooltip("Distance (meters) beyond which the door sound is silent. " +
             "Volume scales up as the camera moves closer than this.")]
    [SerializeField] private float maxHearingDistance = 15f;
    [SerializeField] private AudioSource audioSource;

    private Vector3 _initialLocalPos;
    private Coroutine _moveCoroutine;

    // Set true while a truck is docked at this door (TruckController.OnDocked/BeginDeparture) so the
    // door can't be swiped shut by unrelated OnTriggerExit noise — a dock stocker or receiver walking
    // back out through the doorway mid-unload, or the trailer's own multiple colliders momentarily
    // clipping the trigger boundary, were both closing the door out from under a truck that never
    // left. Resizing the trigger collider can't fix this — it's an ordering problem (any exit closes
    // it), not a coverage problem — so it's driven explicitly instead.
    private bool _forcedOpen;

    /// <summary>Holds the door open (or opens it immediately) and ignores further OnTriggerExit closes
    /// until released. Call with false once the truck actually departs — the truck's own exit through
    /// the trigger will then close it normally.</summary>
    public void SetForcedOpen(bool forced)
    {
        _forcedOpen = forced;
        if (forced)
        {
            if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
            _moveCoroutine = StartCoroutine(MoveDoor(endY));
        }
    }

    private void Awake()
    {
        if (doorPanel == null)
        {
            doorPanel = transform.Find("DoorPanel");
        }

        if (doorPanel != null)
        {
            _initialLocalPos = doorPanel.localPosition;
            // Ensure it starts at startY
            doorPanel.localPosition = new Vector3(_initialLocalPos.x, startY, _initialLocalPos.z);
        }

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

        audioSource.playOnAwake   = false;
        audioSource.spatialBlend  = 1f;                       // fully 3D — comes from the door
        audioSource.rolloffMode   = AudioRolloffMode.Linear;  // clean fade to silence at max distance
        audioSource.minDistance   = fullVolumeDistance;
        audioSource.maxDistance   = maxHearingDistance;
        audioSource.dopplerLevel  = 0f;                       // no pitch shift from movement
    }

    private void OnTriggerEnter(Collider other)
    {
        // Triggers when an object enters the box collider
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveDoor(endY));
        PlayClip(openClip);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_forcedOpen) return; // a truck is still docked here — ignore unrelated exits

        // Triggers when the object leaves the box collider
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveDoor(startY));
        PlayClip(closeClip);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, doorVolume);
    }

    private IEnumerator MoveDoor(float targetY)
    {
        if (doorPanel == null) yield break;

        Vector3 targetPos = new Vector3(_initialLocalPos.x, targetY, _initialLocalPos.z);
        
        while (Vector3.Distance(doorPanel.localPosition, targetPos) > 0.001f)
        {
            doorPanel.localPosition = Vector3.MoveTowards(doorPanel.localPosition, targetPos, speed * Time.deltaTime);
            yield return null;
        }
        doorPanel.localPosition = targetPos;
    }
}
