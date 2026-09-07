using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Economy;
using GameCore.Events;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// Watches every scheduled outbound/bulk appointment and, once it's 4 hours or less from its dock
/// time, fires exactly ONE narrated readiness check into the Systems Log per order: whether on-hand
/// stock (plus anything already inbound that will land before the appointment) actually covers what
/// the order needs. Self-bootstrapping (DontDestroyOnLoad hidden object), same pattern as
/// SystemsLogWindow/TruckYardManager's other always-on services — ticks off the same
/// GameEvents.Time.OnHourChanged event DockScheduleService itself uses.
/// </summary>
public class SchedulerWarningService : MonoBehaviour
{
    /// <summary>How far ahead of an appointment to start checking it — matches Tad's "4 hours or
    /// less out" spec.</summary>
    private const int WarningWindowHours = 4;

    private static SchedulerWarningService _instance;
    private readonly HashSet<string> _warnedOrderIds = new HashSet<string>();

    private EventManager _eventManager;
    private bool _subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[SchedulerWarningService]");
        DontDestroyOnLoad(go);
        go.AddComponent<SchedulerWarningService>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    private void OnDestroy()
    {
        _eventManager?.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        if (_subscribed) return;
        _eventManager = EventManager.Instance;
        if (_eventManager == null) return;

        _eventManager.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
        _subscribed = true;
    }

    private void OnHourChanged(string eventId, int newHour)
    {
        if (!ServiceLocator.TryGet(out DockScheduleService dockSchedule) || dockSchedule == null) return;
        if (!ServiceLocator.TryGet(out OrderService orders) || orders == null) return;
        if (!ServiceLocator.TryGet(out InventoryService inventory) || inventory == null) return;
        if (!ServiceLocator.TryGet(out ShipmentService shipments) || shipments == null) return;
        if (!ServiceLocator.TryGet(out SimulationTimeService clock) || clock == null) return;

        // Keep the "already warned" set from growing forever — once an order leaves ActiveOrders
        // (shipped, cancelled, whatever) there's nothing left to warn about, so its slot is free again.
        var liveIds = new HashSet<string>(orders.ActiveOrders.Select(o => o.OrderId));
        _warnedOrderIds.RemoveWhere(id => !liveIds.Contains(id));

        int nowTotalHours = clock.Day * 24 + clock.Hour;

        foreach (var appt in dockSchedule.Appointments)
        {
            if (appt.Parked || appt.ClosedOut) continue;
            if (appt.Kind != AppointmentKind.Outbound && appt.Kind != AppointmentKind.Bulk) continue;
            if (appt.OrderIds.Count == 0) continue;

            int hoursUntil = (appt.Day * 24 + appt.StartHour) - nowTotalHours;
            if (hoursUntil < 0 || hoursUntil > WarningWindowHours) continue;

            foreach (var orderId in appt.OrderIds)
            {
                if (_warnedOrderIds.Contains(orderId)) continue;

                var order = orders.ActiveOrders.FirstOrDefault(o => o.OrderId == orderId);
                if (order == null) continue;

                _warnedOrderIds.Add(orderId);
                AnnounceReadiness(order, appt, orders, inventory, dockSchedule, shipments);
            }
        }
    }

    private void AnnounceReadiness(OrderData order, DockAppointment appt, OrderService orders,
        InventoryService inventory, DockScheduleService dockSchedule, ShipmentService shipments)
    {
        string apptTime = $"{appt.StartHour:00}:00";
        string displayNumber = string.IsNullOrEmpty(order.OrderNumber) ? order.OrderId : order.OrderNumber;
        string header = $"Order {displayNumber} {order.CustomerName} is at {apptTime}";

        var missing = new List<(string label, int shortfall, string arrivalLabel)>();
        foreach (var li in order.LineItems)
        {
            int onHand = inventory.GetTotalUnitsBySku(li.SkuId);
            int shortfall = li.QuantityNeeded - onHand;
            if (shortfall <= 0) continue;

            var sku = inventory.GetSkuData(li.SkuId);
            string label = sku != null ? $"{li.SkuId} {sku.ItemDescription}" : li.SkuId;
            string arrivalLabel = FindIncomingArrival(li.SkuId, appt.Day * 24 + appt.StartHour, dockSchedule, shipments);
            missing.Add((label, shortfall, arrivalLabel));
        }

        if (missing.Count == 0)
        {
            SystemsLogWindow.LogSystem($"{header} and looks like you got all the quantities ordered in-house. Great Job!");
            return;
        }

        string itemsPhrase = string.Join(" and ", missing.Select(m => $"{m.label} needs {m.shortfall} more case(s)"));
        string body = $"{header} and there are {missing.Count} item(s) missing. {itemsPhrase}.";

        var covered = missing.Where(m => m.arrivalLabel != null).ToList();
        var uncovered = missing.Where(m => m.arrivalLabel == null).ToList();

        string message;
        if (covered.Count == 0)
        {
            message = $"{body} There is nothing on order.";
        }
        else
        {
            string coveredPhrase = string.Join(" and ", covered.Select(m => $"the {m.label} will be here at {m.arrivalLabel}"));
            if (uncovered.Count == 0)
                message = $"{body} However, {coveredPhrase} — nothing further to order.";
            else
                message = $"{body} However, {coveredPhrase}, but you still need to order the " +
                           string.Join(" and ", uncovered.Select(m => m.label)) + ".";
        }

        SystemsLogWindow.LogWarning(message);
    }

    /// <summary>Looks for a pending inbound PO carrying this SKU with a dock appointment strictly
    /// before <paramref name="beforeTotalHours"/> (the order's own appointment). Returns the arrival
    /// time label, or null if nothing inbound covers it in time.</summary>
    private static string FindIncomingArrival(string skuId, int beforeTotalHours,
        DockScheduleService dockSchedule, ShipmentService shipments)
    {
        foreach (var inbound in dockSchedule.Appointments)
        {
            if (inbound.Kind != AppointmentKind.Inbound) continue;
            if (inbound.Parked || string.IsNullOrEmpty(inbound.ShipmentPoNumber)) continue;

            int inboundTotalHours = inbound.Day * 24 + inbound.StartHour;
            if (inboundTotalHours >= beforeTotalHours) continue;

            var shipment = shipments.PendingShipments.FirstOrDefault(s => s.PONumber == inbound.ShipmentPoNumber);
            if (shipment == null) continue;
            if (shipment.LineItems.Any(li => li.SkuId == skuId))
                return $"{inbound.StartHour:00}:00";
        }
        return null;
    }
}
