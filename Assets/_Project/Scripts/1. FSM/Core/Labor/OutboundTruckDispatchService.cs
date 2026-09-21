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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[OutboundTruckDispatchService]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<OutboundTruckDispatchService>();
        }

        private PlacementGrid _grid;
        private TruckYardManager _truckYard;
        private float _nextScan;
        private bool _subscribed;

        private void OnEnable() => TrySubscribe();

        // EventManager may not exist yet at AfterSceneLoad; keep trying until it does — same pattern
        // DockNumberingService uses for the same reason.
        private void Update()
        {
            if (!_subscribed) TrySubscribe();

            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 1f;

            ScanStagedPallets();
        }

        private void TrySubscribe()
        {
            var em = EventManager.Instance;
            if (em == null) return;

            em.Subscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
            _subscribed = true;

            // Catch a block that was ALREADY open the moment this subscription goes live (e.g. a
            // save loaded mid-block, or this component only just finished spinning up) — otherwise
            // it would sit dark until the NEXT hour boundary, which could itself be up to 2 hours away.
            DispatchDueAppointments();
        }

        private void OnDestroy()
        {
            var em = EventManager.Instance;
            if (em == null || !_subscribed) return;
            em.Unsubscribe<int>(GameEvents.Time.OnHourChanged, OnHourChanged);
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
                if (schedule.CurrentBlock < appt.BlockIndex) continue; // block hasn't started yet

                var dock = DockSlot.All.FirstOrDefault(d => d.DoorNumber == appt.DoorNumber);
                if (dock == null || dock.IsOccupied) continue; // already has a truck

                _truckYard.SpawnOutboundTruck(appt.DoorNumber);
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
                if (schedule != null)
                {
                    var appt = schedule.Appointments.FirstOrDefault(a =>
                        a.DoorNumber == doorNumber && a.Day == schedule.CurrentDay &&
                        a.Kind != AppointmentKind.Inbound && !a.Parked && !a.ClosedOut);
                    if (appt != null && schedule.CurrentBlock < appt.BlockIndex) continue;
                }

                _truckYard.SpawnOutboundTruck(doorNumber);
            }
        }
    }
}
