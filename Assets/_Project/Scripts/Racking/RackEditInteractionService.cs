using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Always-on service that reopens RackSetupUI in EDIT mode when the player double-clicks an
/// already-committed ("live") rack — even one currently holding pallets — so its aisle number
/// and/or Pick/Reserve level scheme can be changed after the fact. Mirrors LaneInteractionService's
/// self-bootstrapping double-click pattern (hidden DontDestroyOnLoad object, no scene wiring).
///
/// ChevronController's own double-click only fires on chevrons, which AisleInitializer deletes the
/// moment an aisle is first committed — a live aisle has nothing left to double-click without this.
///
/// Walks every collider along the ray (closest first), not just the single closest hit — a rack's
/// own big trigger BoxCollider spans its whole footprint, so a pallet resting on one of its shelves
/// often sits closer to the camera and would otherwise block the rack hit entirely (same reasoning
/// as PlacementStateMachine's hover raycast).
/// </summary>
public class RackEditInteractionService : MonoBehaviour
{
    private static RackEditInteractionService _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // HideInHierarchy + DontDestroyOnLoad, NOT HideAndDontSave: with Enter Play Mode Options (no domain
        // reload) a HideAndDontSave object survives exiting Play, so every session/recompile left another
        // copy running (6 found live 2026-09-23). This one is destroyed on Play exit; sweep any leftovers.
        foreach (var stale in Resources.FindObjectsOfTypeAll<RackEditInteractionService>())
            if (stale != null && stale != _instance) DestroyImmediate(stale.gameObject);
        if (_instance != null) return;
        var go = new GameObject("[RackEditInteractionService]") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<RackEditInteractionService>();
    }

    private const float DoubleClickThreshold = 0.3f;
    private const int MaxHits = 16;

    private float _lastClickTime = -1f;
    private PlacementStateMachine _fsm;
    private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasReleasedThisFrame) return;

        float now = Time.unscaledTime;
        bool isDouble = (now - _lastClickTime) < DoubleClickThreshold;
        _lastClickTime = isDouble ? -1f : now; // consume so a triple-click isn't two doubles
        if (!isDouble) return;

        // Don't act on top of the modal, and only when the build FSM is idle — same guards
        // LaneInteractionService uses for its own double-click.
        var ui = FindAnyObjectByType<RackSetupUI>(FindObjectsInactive.Include);
        if (ui == null || ui.IsOpen) return;

        if (_fsm == null) _fsm = FindAnyObjectByType<PlacementStateMachine>();
        if (_fsm != null && !(_fsm.CurrentState is IdleState)) return;

        if (!TryGetLiveRackAisleUnderCursor(out int aisle)) return;

        ui.OpenForEdit(aisle);
        AudioManager.Play("ValidPlace");
    }

    private bool TryGetLiveRackAisleUnderCursor(out int aisle)
    {
        aisle = -1;

        var cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        int count = Physics.RaycastNonAlloc(ray, _hits, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(_hits, 0, count, Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

        for (int i = 0; i < count; i++)
        {
            var po = _hits[i].collider.GetComponentInParent<PlacedObject>();
            if (po != null && po.isRackLive && po.data != null && po.data.category == "Racking")
            {
                aisle = po.rackAisle;
                return true;
            }
        }

        return false;
    }
}
