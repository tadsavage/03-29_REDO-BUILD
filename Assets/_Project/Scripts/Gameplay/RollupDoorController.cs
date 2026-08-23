using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    // Everything currently standing in the doorway. The door used to open on ANY enter and close on
    // ANY exit, with no idea how many things were inside — so with two agents in the doorway the
    // first one out shut the door on the second. _forcedOpen was bolted on to stop trucks suffering
    // that, but workers still did. Counting occupants fixes the actual problem, and is what lets
    // SetForcedOpen(false) know whether it is safe to close.
    private readonly HashSet<Collider> _inside = new HashSet<Collider>();

    // A collider that is destroyed or disabled never fires OnTriggerExit — and a departing truck is
    // destroyed, so its entry would sit in the set forever and wedge the door open. Swept while
    // occupied rather than every frame; nothing needs sub-second accuracy here.
    private const float StalePruneInterval = 0.5f;
    private float _nextPruneTime;

    /// <summary>
    /// Holds the door open (or opens it immediately) and ignores occupant-driven closes until
    /// released.
    ///
    /// Releasing it CLOSES the door if the doorway is empty, rather than waiting for a trigger exit
    /// that may never arrive. The old code assumed "the truck's own exit through the trigger will
    /// close it normally as it drives out", and that assumption fails in both directions: any exit
    /// the truck did fire while the hold was active was deliberately swallowed below, and the truck
    /// is destroyed on departure, so no exit event can be produced afterwards either. The result was
    /// a dock door left standing open forever with nothing holding it — observed on door 1 with
    /// panelY sitting at the fully-open endY, _forcedOpen false, and zero trucks alive in the scene.
    /// </summary>
    public void SetForcedOpen(bool forced)
    {
        _forcedOpen = forced;

        if (forced)
        {
            OpenDoor();
            return;
        }

        PruneStaleOccupants();
        if (_inside.Count == 0) CloseDoor();
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
        // Only the FIRST arrival opens the door — a second agent walking into an already-open
        // doorway shouldn't restart the animation or re-trigger the sound.
        if (!_inside.Add(other)) return;
        if (_inside.Count == 1) OpenDoor();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_inside.Remove(other)) return;

        if (_forcedOpen) return;      // a truck is docked here — it decides when this door closes
        if (_inside.Count > 0) return; // somebody is still standing in the doorway

        CloseDoor();
    }

    private void Update()
    {
        // Cheap and only while something is (or claims to be) in the doorway.
        if (_inside.Count == 0 || Time.time < _nextPruneTime) return;
        _nextPruneTime = Time.time + StalePruneInterval;

        int before = _inside.Count;
        PruneStaleOccupants();
        if (before > 0 && _inside.Count == 0 && !_forcedOpen) CloseDoor();
    }

    /// <summary>Drops occupants that can no longer report leaving — destroyed (a departed truck),
    /// deactivated, or their collider switched off. Without this the door stays open forever waiting
    /// on an OnTriggerExit that physics will never raise.</summary>
    private void PruneStaleOccupants()
    {
        _inside.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
    }

    private void OpenDoor()
    {
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveDoor(endY));
        PlayClip(openClip);
    }

    private void CloseDoor()
    {
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveDoor(startY));
        PlayClip(closeClip);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, doorVolume * AudioManager.GameVolumeLevel);
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
