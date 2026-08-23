using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Swinging door controller for the Wall-Win-Entrance prefab.
/// Humanoids (and Rats) trigger the door open; MHE does not.
/// Allowed agents are read from the parent BuildingData.allowedAgents at runtime,
/// matching the same pattern as ManDoorController.
///
/// NavBlockers (thin NavMeshObstacle slabs at the frame edges) are created
/// in Awake() if they don't already exist, funnelling NavMesh agents through
/// the centre of the opening instead of clipping through the door frame.
/// </summary>
public class EntranceDoorController : MonoBehaviour
{
    [Header("Door Panel")]
    [Tooltip("Assign the WinDoor child transform here, or leave empty to auto-find.")]
    [SerializeField] private Transform doorTransform;
    [SerializeField] private float startRotY = 0f;
    [SerializeField] private float endRotY   = -90f;
    [SerializeField] private float speed     = 150f;

    [Header("Audio")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField, Range(0f, 1f)] private float doorVolume = 1f;
    [SerializeField] private float fullVolumeDistance = 2f;
    [SerializeField] private float maxHearingDistance = 15f;
    [SerializeField] private AudioSource audioSource;

    private Coroutine _moveCoroutine;
    private AgentType _allowedAgents = AgentType.Human | AgentType.Rat;

    // Candidate paths tried in order until a match is found in Awake().
    private static readonly string[] DoorSearchPaths =
    {
        "WinDoor",
        "WinDrFrame/WinDoor",
        "DoorFrame/Door",
        "Door",
    };

    private void Awake()
    {
        // ── Find door panel ────────────────────────────────────────────────
        if (doorTransform == null)
        {
            foreach (var path in DoorSearchPaths)
            {
                doorTransform = transform.Find(path);
                if (doorTransform != null) break;
            }
        }

        if (doorTransform != null)
            doorTransform.localRotation = Quaternion.Euler(0f, startRotY, 0f);

        // ── Audio ──────────────────────────────────────────────────────────
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake  = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode  = AudioRolloffMode.Linear;
        audioSource.minDistance  = fullVolumeDistance;
        audioSource.maxDistance  = maxHearingDistance;
        audioSource.dopplerLevel = 0f;

        // ── NavBlockers (frame-edge carvers) ────────────────────────────────
        EnsureNavBlockers();
    }

    private void Start()
    {
        var bd = GetComponentInParent<BuildingData>();
        if (bd != null && bd.Data != null)
            _allowedAgents = bd.Data.allowedAgents;
    }

    // ---------------------------------------------------------
    // NAV BLOCKERS
    // ---------------------------------------------------------
    private void EnsureNavBlockers()
    {
        // Skip if already added (e.g. via prefab override in the Editor).
        if (transform.Find("NavBlockerLeft") != null) return;

        // Thin box obstacles at each frame edge.
        // Center coords are in local space of this GameObject (the root).
        // The opening is ~1.33 units wide; the frame takes ~0.22 units per side,
        // leaving ~0.89 units of clear passthrough in the middle.
        AddNavBlocker("NavBlockerLeft",
            center:  new Vector3(-0.55f, 1.0f, 0f),
            size:    new Vector3(0.22f,  2.0f, 0.1f));

        AddNavBlocker("NavBlockerRight",
            center:  new Vector3( 0.55f, 1.0f, 0f),
            size:    new Vector3(0.22f,  2.0f, 0.1f));
    }

    private void AddNavBlocker(string goName, Vector3 center, Vector3 size)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.layer = gameObject.layer;

        var obs               = go.AddComponent<NavMeshObstacle>();
        obs.shape             = NavMeshObstacleShape.Box;
        obs.center            = center;
        obs.size              = size;
        obs.carving             = true;
        obs.carveOnlyStationary = true;
        obs.carvingMoveThreshold = 0.1f;
    }

    // ---------------------------------------------------------
    // TRIGGER — open / close
    // ---------------------------------------------------------
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
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, doorVolume * AudioManager.GameVolumeLevel);
    }

    private void StopMoving()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
    }

    // ---------------------------------------------------------
    // ROTATION COROUTINE
    // ---------------------------------------------------------
    private IEnumerator RotateDoor(float targetY)
    {
        if (doorTransform == null) yield break;

        Quaternion target = Quaternion.Euler(0f, targetY, 0f);

        while (Quaternion.Angle(doorTransform.localRotation, target) > 0.1f)
        {
            doorTransform.localRotation = Quaternion.RotateTowards(
                doorTransform.localRotation,
                target,
                speed * Time.deltaTime);
            yield return null;
        }

        doorTransform.localRotation = target;
    }
}
