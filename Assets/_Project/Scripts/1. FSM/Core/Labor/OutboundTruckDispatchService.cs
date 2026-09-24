using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;
using GameCore.Events;

namespace GameCore.Labor
{
    /// <summary>
    /// Self-bootstrapping outbound counterpart to the inbound truck arrival flow. Spawns a truck for
    /// a door on either of two triggers:
    ///
    ///   STAGED  — the door's lane has at least one staged (unparented) OutboundPalletBuilder and no
    ///             truck already assigned. Prototype-simple per Tad's 2026-07-23 spec ("just have a
    ///             trailer show up automatically into a lane that has at least one order staged in
    ///             it") -- replaces having to click the DevConsole's manual "spawn outbound truck"
    ///             debug button. Checked on a 1-real-second poll (ScanStagedPallets) — fine here
    ///             because nothing about "did a pallet just get staged" is clock-aligned.
    ///   BLOCK START — the door has a live (non-Parked, non-ClosedOut) outbound/bulk appointment
    ///             whose booked 2-hour block has started, regardless of whether anything has been
    ///             staged yet. Added per Tad's 2026-09-21 ask: without this, an order nobody had
    ///             started picking for could sit booked forever with no trailer ever arriving, no
    ///             matter how the appointment was rescheduled — the STAGED trigger above never fires
    ///             until picking has already begun, so the appointment's time did nothing on its own.
    ///
    ///             THIS ONE RUNS OFF GameEvents.Time.OnHourChanged, NOT the 1-second poll — checked
    ///             and fixed live 2026-09-21: an appointment booked for 00:00 didn't get its truck
    ///             until ~01:00-01:30 with the poll-based version. A 1-real-second poll is fine for
    ///             something that can happen at any arbitrary moment (staging), but wrong for "has
    ///             the clock crossed an exact hour" — at any game speed above 1x, the poll's own
    ///             cadence (once per REAL second, unaffected by Time.timeScale) can miss the instant
    ///             a block starts by up to a full in-game HOUR before the next poll happens to catch
    ///             up. Reacting to the real hour-change event instead removes that gap entirely: the
    ///             check runs in the same frame the hour actually ticks over, at any game speed.
    ///
    /// TrailerLoadController then waits at the dock until enough pallets have accumulated before
    /// actually loading (LoadStartThreshold) — that part is unchanged either way the truck got there.
    /// </summary>
    public class OutboundTruckDispatchService : MonoBehaviour
    {
        private static OutboundTruckDispatchService _instance;

        // BUG FIX 2026-09-23 ("drivers never showed up"): this used to be HideAndDontSave. The project
        // runs with Enter Play Mode Options (no domain reload), and HideAndDontSave objects are NOT
        // destroyed when Play stops — so every session left one behind (5 found live), each still
        // "subscribed" to a previous session's EventManager. From the second Play session on, the
        // BLOCK START trigger below never fired at all: no scheduled outbound truck ever came.
        // Now: HideInHierarchy + DontDestroyOnLoad (destroyed on exiting Play), any leftovers are
        // cleaned up here, and Update re-subscribes whenever EventManager.Instance is a new object.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            foreach (var stale in Resources.FindObjectsOfTypeAll<OutboundTruckDispatchService>())
                if (stale != null && stale != _instance) DestroyImmediate(stale.gameObject);
            if (_instance != null) return;

            var go = new GameObject("[OutboundTruckDispatchService]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<OutboundTruckDispatchService>();
        }

        private PlacementGrid _grid;
        private TruckYardManager _truckYard;
        private float _nextScan;
        private bool _subscribed;
        private EventManager _subscribedTo;

        private void OnEnable() => TrySubscribe();

        // EventManager may not exist yet at AfterSceneLoad; keep trying until it does — same pattern
        // DockNumberingService uses for the same reason. Also re-subscribes if the EventManager has
        // been replaced since (a new Play session / scene load builds a new one).
        private void Update()
        {
            if (!_subscribed || EventManager.Instance != _subscribedTo) TrySubscribe();

            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 1f;

            ScanStagedPallets();
        }

        private void TrySubscribe()
        {
            var em = EventManager.Instance;
            if (em == null) { _subscribed = false; return; }

            _subscribedTo?.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            em.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            _subscribedTo = em;
            _subscribed = true;

            // Catch a block that was ALREADY open the moment this subscription goes live (e.g. a
            // save loaded mid-block, or this component only just finished spinning up) — otherwise
            // it would sit dark until the NEXT hour boundary, which could itself be up to 2 hours away.
            DispatchDueAppointments();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_subscribedTo == null || !_subscribed) return;
            _subscribedTo.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
        }

        private void OnHourChanged(string eventId, int newHour) => DispatchDueAppointments();

        /// <summary>The BLOCK START trigger — see the class doc for why this is event-driven rather
        /// than polled.</summary>
        private void DispatchDueAppointments()
        {
            if (_truckYard == null) _truckYard = FindAnyObjectByType<TruckYardManager>();
            if (_truckYard == null) return;
            if (!ServiceLocator.TryGet<DockScheduleService>(out var schedule) || schedule == null) return;

            foreach (var appt in schedule.Appointments)
            {
                if (appt.Kind == AppointmentKind.Inbound) continue;
                if (appt.Parked || appt.ClosedOut) continue;
                if (appt.Day != schedule.CurrentDay) continue;
                // Only the block that is open RIGHT NOW. `< BlockIndex` alone let a block that had already
                // ENDED but not yet been swept (a save loaded at 11:00 still holding an open 06:00
                // appointment) dispatch its driver hours after the window — who then "sat there for
                // their whole appointment" and left. A driver doesn't turn up for a window that's gone.
                if (schedule.CurrentBlock != appt.BlockIndex) continue;
                if (schedule.IsComplete(appt)) continue;                // already picked up

                // Deliberately NOT skipped when the door is busy (2026-09-23). It used to be, which meant
                // an inbound trailer parked at the door through this block made its outbound driver a
                // silent no-show — skipped every hour until the block was swept. The driver always comes
                // now and waits in the side lot if the door isn't free (see SpawnOutboundTruck).
                // Only skip if a truck for THIS appointment is already here, or an unattributed one (the
                // staged-pallet trigger fired before the block and is already serving the door).
                if (TruckYardManager.HasOutboundTruckFor(appt.DoorNumber, appt.Id)) continue;
                if (HasUnattributedOutboundTruck(appt.DoorNumber)) continue;

                _truckYard.SpawnOutboundTruck(appt.DoorNumber, appt.Id);
            }
        }

        /// <summary>The STAGED trigger — see the class doc.</summary>
        private void ScanStagedPallets()
        {
            if (_grid == null) _grid = FindAnyObjectByType<PlacementGrid>();
            if (_grid == null) return;
            if (_truckYard == null) _truckYard = FindAnyObjectByType<TruckYardManager>();
            if (_truckYard == null) return;

            var doorsWithStaged = new HashSet<int>();
            foreach (var pallet in FindObjectsByType<OutboundPalletBuilder>())
            {
                if (pallet == null || pallet.transform.parent != null) continue;
                var cell = _grid.WorldToCell(pallet.transform.position);
                if (!LaneNamingService.TryGetSlot(cell, out var slot)) continue;
                doorsWithStaged.Add(slot.DoorNumber);
            }

            ServiceLocator.TryGet<DockScheduleService>(out var schedule);

            foreach (var doorNumber in doorsWithStaged)
            {
                var dock = DockSlot.All.FirstOrDefault(d => d.DoorNumber == doorNumber);
                if (dock == null || dock.IsOccupied) continue; // already has a truck coming/docked

                // Don't let an early-staged order pull the trailer in ahead of its booked time — per
                // Tad, a bulk or recurring order's truck should arrive at the START of its scheduled
                // block, not the moment enough pallets happen to be staged (which can be well before
                // the block if staging finishes early). Find today's outbound/bulk appointment at this
                // door and hold the spawn until the clock has actually reached its BlockIndex. No
                // matching appointment (e.g. a debug-spawned pallet with nothing booked) falls back to
                // the old immediate-dispatch behavior — there's no scheduled time to wait for.
                // Staged freight only pulls a trailer in during a booked window: the appointment whose
                // block is open now. If the door has bookings today but none is open (the next one is
                // later, or an earlier one ended un-swept), wait — FirstOrDefault used to grab whichever
                // booking came first in the list, which could be a finished window, and dispatch a
                // truck outside any pickup time. Only a door with nothing booked today at all falls
                // back to immediate dispatch (debug-staged pallets).
                DockAppointment appt = null;
                if (schedule != null)
                {
                    var todays = schedule.Appointments.Where(a =>
                        a.DoorNumber == doorNumber && a.Day == schedule.CurrentDay &&
                        a.Kind != AppointmentKind.Inbound && !a.Parked && !a.ClosedOut).ToList();
                    appt = todays.FirstOrDefault(a => a.BlockIndex == schedule.CurrentBlock);
                    if (appt == null && todays.Count > 0) continue;
                }

                // Tag it with the appointment it's serving so the BLOCK START trigger recognises it
                // rather than sending a second driver for the same booking.
                if (appt != null && TruckYardManager.HasOutboundTruckFor(doorNumber, appt.Id)) continue;
                _truckYard.SpawnOutboundTruck(doorNumber, appt?.Id);
            }
        }

        /// <summary>An outbound truck at/for this door that wasn't dispatched for any particular
        /// appointment (debug button, order-release path, or an older save).</summary>
        private static bool HasUnattributedOutboundTruck(int doorNumber)
        {
            foreach (var t in FindObjectsByType<TruckController>(FindObjectsSortMode.None))
            {
                if (t == null || !t.IsOutbound || t.IsLeaving || !string.IsNullOrEmpty(t.OutboundAppointmentId)) continue;
                int door = t.AssignedDock != null ? t.AssignedDock.DoorNumber : t.OutboundDoorNumber;
                if (door == doorNumber) return true;
            }
            return false;
        }
    }
}
