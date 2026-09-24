using System.Linq;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// Clickable "hyperlinks" inside Systems Log messages.
///
/// A message embeds a link with one of the builders below (Po / Order / Door), which wraps the visible
/// text in bold + underline plus a TextCore &lt;link&gt; tag carrying a routing id. SystemsLogWindow
/// makes any row containing a link pickable and forwards a left-click on the link text to
/// <see cref="Handle"/>, which decides where to take the player:
///
///   po:&lt;PO&gt;            → Scheduler, on that PO's appointment (highlighted, tooltip open)
///   order:&lt;orderId&gt;    → Scheduler, on the appointment carrying that order
///   door:&lt;n&gt;:&lt;PO&gt;       → camera glides to the truck at that door, truck card pinned
///
/// Ids are resolved LIVE at click time, not baked when the message is logged — a trailer can be
/// moved to a different block/door after the message was written, and the link should follow it.
/// </summary>
public static class LogLinks
{
    // Door-link framing: a pulled-out, ~45° overhead three-quarter view of the whole rig.
    private const float DoorShotPitch = 45f;
    private const float DoorShotDistance = 24f;
    /// <summary>Degrees the camera is swung off the truck's own axis, so the shot sees the trailer's
    /// side and the door wall at an angle instead of looking straight down the trailer.</summary>
    private const float DoorShotYawOffset = 40f;
    private const float DoorShotGlideSeconds = 0.7f;

    // ── Builders ─────────────────────────────────────────────────────────

    public static string Po(string poNumber, string display = null) =>
        Wrap($"po:{poNumber}", display ?? $"PO {poNumber}");

    public static string Order(string orderId, string display) =>
        Wrap($"order:{orderId}", display);

    /// <summary>A specific dock appointment — for outbound pickups, which have no PO number.</summary>
    public static string Appointment(string appointmentId, string display) =>
        string.IsNullOrEmpty(appointmentId) ? display : Wrap($"appt:{appointmentId}", display);

    /// <summary><paramref name="truckKey"/> identifies the truck to frame: an inbound PO number or an
    /// outbound truck's appointment id. Null = whatever truck is at / waiting for that door.</summary>
    public static string Door(int doorNumber, string truckKey = null, string display = null) =>
        Wrap($"door:{doorNumber}:{truckKey}", display ?? $"Door {doorNumber}");

    private static string Wrap(string id, string text) => $"<link=\"{id}\"><b><u>{text}</u></b></link>";

    // ── Routing ──────────────────────────────────────────────────────────

    public static void Handle(string linkId)
    {
        if (string.IsNullOrEmpty(linkId)) return;
        int colon = linkId.IndexOf(':');
        string kind = colon < 0 ? linkId : linkId.Substring(0, colon);
        string arg = colon < 0 ? "" : linkId.Substring(colon + 1);

        switch (kind)
        {
            case "po":    OpenSchedulerOn(FindAppointmentForPo(arg), $"PO {arg}"); break;
            case "order": OpenSchedulerOn(FindAppointmentForOrder(arg), $"Order {OrderLabel(arg)}"); break;
            case "appt":  OpenSchedulerOn(FindAppointment(arg), "That pickup"); break;
            case "door":  GoToDoor(arg); break;
            default: Debug.LogWarning($"[LogLinks] Unknown link id '{linkId}'."); break;
        }
    }

    private static void OpenSchedulerOn(DockAppointment appt, string what)
    {
        var topBar = Object.FindAnyObjectByType<TopBarUI>();
        var scheduler = topBar != null ? topBar.SchedulerPanel : null;
        if (scheduler == null)
        {
            UIToast.Show("Couldn't open the scheduler — the Scheduler panel isn't loaded.");
            return;
        }

        // Same route PurchasingPanel.OpenScheduler takes: close whatever else is open first.
        UIKeyBindingManager.Instance?.CloseAll();

        if (appt == null || appt.Parked)
        {
            scheduler.Show();
            UIToast.Show(appt == null ? $"{what} doesn't have a dock appointment right now."
                                      : $"{what} is parked — it's in the unscheduled strip, not on a door.");
            return;
        }

        scheduler.FocusAppointment(appt);
    }

    private static void GoToDoor(string arg)
    {
        string[] parts = arg.Split(':');
        int.TryParse(parts[0], out int doorNumber);
        string key = parts.Length > 1 ? parts[1] : null; // PO number or outbound appointment id

        var trucks = Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None)
                           .Where(t => t != null && !t.IsLeaving).ToList();
        TruckController truck =
            (!string.IsNullOrEmpty(key)
                ? trucks.FirstOrDefault(t => t.AssignedShipment?.PONumber == key || t.OutboundAppointmentId == key)
                : null)
            ?? trucks.FirstOrDefault(t => t.AssignedDock != null && t.AssignedDock.DoorNumber == doorNumber)
            // An outbound driver still parked in the side lot waiting for this door.
            ?? trucks.FirstOrDefault(t => t.IsOutbound && t.OutboundDoorNumber == doorNumber);

        // A modal would sit between the player and the shot we're about to frame.
        UIKeyBindingManager.Instance?.CloseAll();

        var cam = Object.FindAnyObjectByType<FreeLookCamera>();

        if (truck == null)
        {
            var dock = DockSlot.All.FirstOrDefault(d => d != null && d.DoorNumber == doorNumber);
            if (dock != null && cam != null)
                cam.GlideTo(dock.transform.position, cam.transform.eulerAngles.y, DoorShotPitch, DoorShotDistance, DoorShotGlideSeconds);
            UIToast.Show(string.IsNullOrEmpty(key) ? $"No truck at Door {doorNumber} right now."
                                                   : $"That truck has already left Door {doorNumber}.");
            return;
        }

        if (cam != null)
        {
            Bounds b = TruckBounds(truck);
            // Truck drives on transform.forward (cab end), so -forward points down the trailer toward
            // the door it's backed into. Look that way, swung off-axis for a three-quarter view.
            Vector3 towardDoor = -truck.transform.forward;
            float yaw = Mathf.Atan2(towardDoor.x, towardDoor.z) * Mathf.Rad2Deg + DoorShotYawOffset;
            Vector3 focus = new Vector3(b.center.x, b.min.y, b.center.z);
            cam.GlideTo(focus, yaw, DoorShotPitch, DoorShotDistance, DoorShotGlideSeconds);
        }

        Object.FindAnyObjectByType<WorldHoverPopupUI>()?.PinTruck(truck);
    }

    // ── Lookups ──────────────────────────────────────────────────────────

    private static DockAppointment FindAppointment(string id)
    {
        if (!ServiceLocator.TryGet(out DockScheduleService schedule) || schedule == null) return null;
        return schedule.FindById(id);
    }

    private static DockAppointment FindAppointmentForPo(string po)
    {
        if (!ServiceLocator.TryGet(out DockScheduleService schedule) || schedule == null) return null;
        return Best(schedule.Appointments.Where(a => a.ShipmentPoNumber == po));
    }

    private static DockAppointment FindAppointmentForOrder(string orderId)
    {
        if (!ServiceLocator.TryGet(out DockScheduleService schedule) || schedule == null) return null;
        return Best(schedule.Appointments.Where(a => a.OrderIds != null && a.OrderIds.Contains(orderId)));
    }

    /// <summary>A live booking beats a closed-out record of an earlier, missed one; among equals the
    /// latest wins (a rebooked trailer's newest slot is where it actually is).</summary>
    private static DockAppointment Best(System.Collections.Generic.IEnumerable<DockAppointment> candidates) =>
        candidates.OrderBy(a => a.ClosedOut).ThenByDescending(a => a.Day).ThenByDescending(a => a.BlockIndex)
                  .FirstOrDefault();

    private static string OrderLabel(string orderId)
    {
        if (ServiceLocator.TryGet(out OrderService orders) && orders != null)
        {
            var o = orders.ActiveOrders.Concat(orders.OrderHistory).FirstOrDefault(x => x.OrderId == orderId);
            if (o != null && !string.IsNullOrEmpty(o.OrderNumber)) return o.OrderNumber;
        }
        return orderId;
    }

    private static Bounds TruckBounds(TruckController truck)
    {
        var renderers = truck.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(truck.transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
