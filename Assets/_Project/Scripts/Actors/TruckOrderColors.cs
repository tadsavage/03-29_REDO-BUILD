using System.Linq;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// Single source of truth for the "order type" color used to tell trucks apart at a glance —
/// orange for an inbound delivery, green for a Bulk/Wholesale outbound appointment, blue for a
/// recurring outbound appointment. Both WorldHoverPopupUI's tooltip border and TruckHighlighter's
/// selection outline key off this so the two always agree.
/// </summary>
public static class TruckOrderColors
{
    public static readonly Color Recurring = new Color(0x4A / 255f, 0x90 / 255f, 0xD9 / 255f, 1f); // blue
    public static readonly Color Bulk      = new Color(0x4C / 255f, 0xAF / 255f, 0x50 / 255f, 1f); // green
    public static readonly Color Inbound   = new Color(0xE6 / 255f, 0x7E / 255f, 0x22 / 255f, 1f); // orange

    /// <summary>Classifies a truck's order type and returns its color. Inbound trailers are always
    /// orange. Outbound trailers look up whichever DockAppointment currently holds their assigned
    /// door (works as soon as a door is claimed, even before the truck physically docks) and return
    /// green for Bulk/Wholesale or blue for a recurring (Outbound) appointment — defaulting to blue
    /// if no appointment can be resolved yet.</summary>
    public static Color GetColor(TruckController truck)
    {
        if (truck == null) return Recurring;
        if (!truck.IsOutbound) return Inbound;

        var dock = truck.AssignedDock;
        if (dock != null && ServiceLocator.TryGet(out DockScheduleService dockSchedule))
        {
            int today = dockSchedule.CurrentDay;
            var appt = dockSchedule.Appointments.FirstOrDefault(a =>
                !a.Parked && a.Kind != AppointmentKind.Inbound &&
                a.DoorNumber == dock.DoorNumber && a.Day == today);

            if (appt != null)
                return (appt.Kind == AppointmentKind.Bulk || appt.Kind == AppointmentKind.Wholesale) ? Bulk : Recurring;
        }

        return Recurring;
    }
}
