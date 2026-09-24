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
    /// orange. Outbound trailers use the appointment they're here for (see ResolveOutboundAppointment)
    /// and return green for Bulk/Wholesale or blue for a recurring (Outbound) appointment — defaulting
    /// to blue if no appointment can be resolved yet.</summary>
    public static Color GetColor(TruckController truck)
    {
        if (truck == null) return Recurring;
        if (!truck.IsOutbound) return Inbound;

        var appt = ResolveOutboundAppointment(truck);
        if (appt != null)
            return (appt.Kind == AppointmentKind.Bulk || appt.Kind == AppointmentKind.Wholesale) ? Bulk : Recurring;
        return Recurring;
    }

    /// <summary>
    /// The dock appointment an outbound truck is here for. Its own OutboundAppointmentId when it has
    /// one; otherwise a guess from its door, preferring the block open right now.
    ///
    /// BUG FIX 2026-09-23: this used to take the FIRST outbound appointment at the door today, so a
    /// Bulk pickup (Honest Harvest, 06:00) got the blue outline of whichever recurring booking happened
    /// to come first in the list (Sneaky Pete's, 16:00). Shared with WorldHoverPopupUI so the card and
    /// the outline can never disagree about which order a truck is carrying.
    /// </summary>
    public static DockAppointment ResolveOutboundAppointment(TruckController truck)
    {
        if (truck == null || !truck.IsOutbound) return null;
        if (!ServiceLocator.TryGet(out DockScheduleService schedule) || schedule == null) return null;

        if (!string.IsNullOrEmpty(truck.OutboundAppointmentId))
        {
            var own = schedule.FindById(truck.OutboundAppointmentId);
            if (own != null) return own;
        }

        int door = truck.AssignedDock != null ? truck.AssignedDock.DoorNumber : truck.OutboundDoorNumber;
        if (door <= 0) return null;

        var todays = schedule.Appointments.Where(a =>
            !a.Parked && a.Kind != AppointmentKind.Inbound &&
            a.DoorNumber == door && a.Day == schedule.CurrentDay).ToList();
        return todays.FirstOrDefault(a => a.BlockIndex == schedule.CurrentBlock && !a.ClosedOut)
            ?? todays.FirstOrDefault(a => !a.ClosedOut)
            ?? todays.FirstOrDefault();
    }
}
