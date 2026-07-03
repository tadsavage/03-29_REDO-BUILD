using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Always-on service that opens <see cref="LaneSetupUI"/> when the player double-clicks a shipping
/// lane. Lane tiles share a parent collider (per-tile colliders were removed for perf), so we can't
/// raycast a tile directly like a chevron — instead we raycast the ground, convert the hit point to
/// a grid cell, and ask LaneNamingService whether that cell is a lane. Self-bootstraps like the other
/// dock services (hidden DontDestroyOnLoad object), so no scene wiring.
///
/// Only fires while the placement FSM is idle (so it never fights build/move/delete), and never while
/// the modal is already open.
/// </summary>
public class LaneInteractionService : MonoBehaviour
{
    private static LaneInteractionService _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[LaneInteractionService]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LaneInteractionService>();
    }

    private const float DoubleClickThreshold = 0.3f;
    private float _lastClickTime = -1f;
    private PlacementGrid _grid;
    private PlacementStateMachine _fsm;

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasReleasedThisFrame) return;

        float now = Time.unscaledTime;
        bool isDouble = (now - _lastClickTime) < DoubleClickThreshold;
        _lastClickTime = isDouble ? -1f : now; // consume so a triple-click isn't two doubles
        if (!isDouble) return;

        // Don't act on top of the modal, and only when the build FSM is idle.
        var ui = LaneSetupUI.Ensure();
        if (ui.IsOpen) return;
        if (_fsm == null) _fsm = FindAnyObjectByType<PlacementStateMachine>();
        if (_fsm != null && !(_fsm.CurrentState is IdleState)) return;

        if (!TryGetLaneUnderCursor(out int door, out string lane)) return;
        ui.OpenFor(door, lane);
        AudioManager.Play("ValidPlace");
    }

    private bool TryGetLaneUnderCursor(out int doorNumber, out string lane)
    {
        doorNumber = 0; lane = null;

        var cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return false;

        if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
        if (_grid == null) return false;

        Vector2Int cell = _grid.WorldToCell(hit.point);
        if (!LaneNamingService.TryGetSlot(cell, out var slot)) return false;

        doorNumber = slot.DoorNumber;
        lane = slot.Lane;
        return true;
    }
}
