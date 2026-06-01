using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Smoothly disables Depth of Field when the player enters any precision
/// placement state (Build / Move / Delete), then restores it on exit.
///
/// Setup:
///   1. Add this component to a GameObject in the Main scene.
///   2. Add a Volume component to the same (or any) GameObject, set priority
///      higher than your global post-processing volume (e.g. 2 vs 1).
///   3. Create a VolumeProfile that overrides ONLY DepthOfField with
///      IsActive = false (or leave it empty — the script handles it at runtime).
///   4. Assign that Volume to the 'overrideVolume' field in the Inspector.
/// </summary>
public class BuildModeOverride : MonoBehaviour
{
    public static BuildModeOverride Instance { get; private set; }

    [SerializeField] private Volume overrideVolume;
    [SerializeField] private float transitionDuration = 1.25f;

    private Coroutine _current;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (overrideVolume == null)
            overrideVolume = GetComponent<Volume>();

        // Start invisible — no override active
        if (overrideVolume != null)
            overrideVolume.weight = 0f;
    }

    // Called by BuildState / MoveState / DeleteState OnEnter
    public void Activate()   => Transition(1f);

    // Called by BuildState / MoveState / DeleteState OnExit
    public void Deactivate() => Transition(0f);

    private void Transition(float target)
    {
        if (overrideVolume == null) return;
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(LerpWeight(target));
    }

    private IEnumerator LerpWeight(float target)
    {
        float start   = overrideVolume.weight;
        float elapsed = 0f;

        // Scale duration by how far we still need to travel — feels consistent
        // if interrupted mid-transition (no snapping to 0 or 1 start point).
        float distance = Mathf.Abs(target - start);
        float duration = transitionDuration * distance;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            overrideVolume.weight = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        overrideVolume.weight = target;
        _current = null;
    }
}
