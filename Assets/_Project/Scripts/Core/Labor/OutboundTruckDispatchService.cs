using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Labor
{
    /// <summary>
    /// Self-bootstrapping outbound counterpart to the inbound truck arrival flow: watches for any
    /// door whose lane has at least one staged (unparented) OutboundPalletBuilder and no truck
    /// already assigned to that dock, then calls TruckYardManager.SpawnOutboundTruck for it.
    /// Prototype-simple per Tad's 2026-07-23 spec ("just have a trailer show up automatically into
    /// a lane that has at least one order staged in it") -- replaces having to click the DevConsole's
    /// manual "spawn outbound truck" debug button. TrailerLoadController then waits at the dock
    /// until enough pallets have accumulated before actually loading (LoadStartThreshold).
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

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 1f;

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
