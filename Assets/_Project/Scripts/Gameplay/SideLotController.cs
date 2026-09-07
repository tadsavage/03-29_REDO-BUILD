using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sits on the SideLot prefab root. A truck with no free door at the gate parks here instead of
/// leaving (see TruckController.BeginDoorWait).
///
/// BUG FIX (Tad's spec): this used to expose a single Anchor/Entry pair (the FIRST of each found
/// under the prefab) and one shared Occupant, even though the fence is built from 6 duplicated
/// Jersey_barrier segments — each one its own physical parking spot with its own SideLotAnchor and
/// SideLotEntry children. The whole lot behaved as if it only had room for one truck ever, when it
/// actually has up to 6. Anchor/Entry are now grouped per-barrier into a Slot (paired by shared
/// parent transform, since each barrier segment parents exactly one Anchor + one Entry), each with
/// its own independent occupancy — claimed the instant a truck decides to come here (before it
/// physically arrives, so two trucks clearing the gate close together can't both target the same
/// slot) and released the instant it starts pulling out, whether it got a door or ran out of time.
///
/// Clicking anywhere on the lot (its own BoxCollider, same raycast-on-click pattern as
/// EmployeeClickHandler) opens SideLotModalUI listing every truck currently parked across ALL
/// SideLotControllers in the scene.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SideLotController : MonoBehaviour
{
    public static readonly List<SideLotController> All = new();

    /// <summary>One physical parking spot (one Jersey_barrier segment's Anchor + Entry pair) and its
    /// own independent occupancy.</summary>
    public class Slot
    {
        public Transform Anchor;
        public Transform Entry;
        public TruckController Occupant { get; private set; }
        public bool IsOccupied => Occupant != null;
        public void Claim(TruckController truck) => Occupant = truck;
        public void Release() => Occupant = null;
    }

    /// <summary>Every parking spot this lot has, one per Jersey_barrier segment with a SideLotAnchor.</summary>
    public IReadOnlyList<Slot> Slots => _slots;
    private readonly List<Slot> _slots = new();

    public bool HasFreeSlot => _slots.Any(s => !s.IsOccupied);
    public int FreeSlotCount => _slots.Count(s => !s.IsOccupied);
    public int TotalSlotCount => _slots.Count;

    /// <summary>First unoccupied slot, or null if the lot is full (or has no slots at all).</summary>
    public Slot FindFreeSlot() => _slots.FirstOrDefault(s => !s.IsOccupied);

    private Camera _mainCamera;

    private void Awake()
    {
        var anchors = new List<Transform>();
        var entries = new List<Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "SideLotAnchor") anchors.Add(t);
            else if (t.name == "SideLotEntry") entries.Add(t);
        }

        if (anchors.Count == 0)
        {
            Debug.LogWarning($"[SideLotController] No 'SideLotAnchor' children found under {name} — trucks can't park here.");
            return;
        }

        // Pair each Anchor with the Entry that shares its immediate parent (the same Jersey_barrier
        // segment) — this is what keeps a barrier's own Anchor+Entry together regardless of the
        // traversal order GetComponentsInChildren happens to return them in.
        foreach (var anchor in anchors)
        {
            var entry = entries.FirstOrDefault(e => e.parent == anchor.parent);
            _slots.Add(new Slot { Anchor = anchor, Entry = entry });
            if (entry == null)
                Debug.LogWarning($"[SideLotController] Barrier segment '{anchor.parent?.name}' under {name} has a SideLotAnchor but no matching SideLotEntry — that slot will pull straight to the Anchor instead of staging at an entry point first.");
        }

        Debug.Log($"[SideLotController] {name} has {_slots.Count} parking slot(s).");
    }

    private void OnEnable()
    {
        All.Add(this);
        _mainCamera = Camera.main;
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Update()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        if ((UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            || UIInputGuard.IsPointerOverUIToolkit())
            return;

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.transform.IsChildOf(transform))
        {
            SideLotModalUI.Show();
            AudioManager.Play("UIClick");
        }
    }
}
