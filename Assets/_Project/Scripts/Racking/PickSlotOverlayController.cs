using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using GameCore.Inventory;

/// <summary>
/// The "Available Pickslots" overlay for the Slotting UI (SlotAssignmentPanel). While active, every
/// live Pick slot's in-world TMP label is recolored — red = already assigned to some SKU (regardless
/// of whether a pallet physically sits there), yellow = unassigned but the candidate SKU's full
/// pallet is too tall for that rack level, green = unassigned and it fits — and a left-click on any
/// label resolves back to that slot's address via a raycast against a small collider added just for
/// the duration of the overlay (same click-detection shape as ChevronController: Camera.main +
/// Mouse.current + Physics.Raycast).
///
/// Spawned on demand by the panel (not a persistent singleton) and destroys itself once a slot is
/// chosen or the overlay is cancelled — Begin()/End() are the only public surface.
/// </summary>
public class PickSlotOverlayController : MonoBehaviour
{
    // Marks a label GameObject as a valid overlay click target and carries its resolved address back.
    private class LabelMarker : MonoBehaviour { public string Address; }

    // First-pass, generous fixed clickable-area size in the label's local space — not measured off
    // the actual rendered text bounds (kept simple to avoid depending on TMP bounds APIs). Tune here
    // if labels prove hard/easy to click once tested in Play mode.
    private static readonly Vector3 ClickTargetSize = new Vector3(0.35f, 0.15f, 0.05f);

    public static readonly Color AssignedColor = Color.red;
    public static readonly Color FitsColor = Color.green;
    public static readonly Color TooTallColor = Color.yellow;

    private struct Restore
    {
        public TextMeshPro Label;
        public Color OriginalColor;
    }

    private readonly List<Restore> _restores = new();
    private Action<string> _onSlotChosen;
    private bool _running;

    /// <summary>Activates the overlay for a candidate SKU. Calling Begin again while already running
    /// first ends the previous run (restores colors) before starting fresh.</summary>
    public void Begin(SkuData candidateSku, Action<string> onSlotChosen)
    {
        if (_running) EndInternal(invokeNothing: true);

        _onSlotChosen = onSlotChosen;
        _running = true;

        float requiredHeight = candidateSku != null ? candidateSku.PltHeight : 0f;

        foreach (var slot in SlotRegistry.PickSlots)
        {
            if (slot.Label == null || slot.Rack == null) continue;

            _restores.Add(new Restore { Label = slot.Label, OriginalColor = slot.Label.color });

            bool assigned = SlotAssignmentService.IsAssigned(slot.Address);
            float clearHeight = slot.Rack.data != null ? slot.Rack.data.objHeight : 0f;
            bool tooTall = !assigned && requiredHeight > clearHeight;

            slot.Label.color = assigned ? AssignedColor : (tooTall ? TooTallColor : FitsColor);

            var go = slot.Label.gameObject;
            if (go.GetComponent<Collider>() == null)
            {
                var col = go.AddComponent<BoxCollider>();
                col.size = ClickTargetSize;
                col.isTrigger = true;
            }
            if (go.GetComponent<LabelMarker>() == null)
                go.AddComponent<LabelMarker>().Address = slot.Address;
        }
    }

    private void Update()
    {
        if (!_running) return;
        if (Mouse.current == null || Camera.main == null) return;
        if (!Mouse.current.leftButton.wasReleasedThisFrame) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        var marker = hit.collider.GetComponent<LabelMarker>();
        if (marker == null) return;

        string address = marker.Address;
        var callback = _onSlotChosen;
        EndInternal(invokeNothing: true);
        callback?.Invoke(address);
    }

    /// <summary>Cancels the overlay without choosing a slot — restores label colors/colliders and
    /// destroys this controller.</summary>
    public void End() => EndInternal(invokeNothing: true);

    private void EndInternal(bool invokeNothing)
    {
        foreach (var r in _restores)
        {
            if (r.Label == null) continue;
            r.Label.color = r.OriginalColor;
            var go = r.Label.gameObject;
            var marker = go.GetComponent<LabelMarker>();
            if (marker != null) Destroy(marker);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
        _restores.Clear();
        _running = false;
        _onSlotChosen = null;
        Destroy(gameObject);
    }
}
