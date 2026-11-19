using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;

namespace GameCore.Actors
{
    /// <summary>
    /// MHE TRAFFIC TEST BENCH (2026-11-18). One call (or the HAL tab's "RUN MHE TRAFFIC TEST" button) sets up a repeatable traffic test:
    ///   1. loads a save slot (default 6, "Order selection test": three doors aisle, stocked, unreleased case-pick orders, a crew of
    ///      order selectors, reach truck drivers and dock stocker drivers),
    ///   2. releases the N smallest unreleased orders to their staging lanes (selectors pick them on pallet jacks; whatever is a full
    ///      pallet goes to the reach trucks as pallet picks; emptied pick faces raise replenishments; inbound trailers raise putaways),
    ///   3. switches off the day-end recap (it pauses the game), sets the game speed, zeroes MheTrafficProbe,
    ///   4. writes "[MheTest]" lines to the log: the start, a probe summary every real minute, and the result after the run time.
    /// Judge a run by its "[MheProbe]" incidents (CLIP / TOO CLOSE / IN RACK / ANGLE / HELD / FAILED) and its final summary.
    /// </summary>
    public sealed class MheTestBench : MonoBehaviour
    {
        public static bool Running { get; private set; }
        public static string LastResult { get; private set; }
        private static MheTestBench s_host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Running = false; LastResult = null; s_host = null; }

        /// <param name="slotNumber">Save slot as numbered in the save window (1-8).</param>
        /// <param name="orders">How many unreleased orders to release (smallest first).</param>
        /// <param name="speed">Game speed (Time.timeScale).</param>
        /// <param name="minutes">Real minutes to run before the result is written (0 = run until stopped).</param>
        public static void Run(int slotNumber = 6, int orders = 3, float speed = 3f, float minutes = 8f)
        {
            if (!Application.isPlaying) { Debug.LogWarning("[MheTest] enter Play mode first."); return; }
            if (s_host == null)
            {
                var go = new GameObject("[MheTestBench]") { hideFlags = HideFlags.DontSave };
                DontDestroyOnLoad(go);
                s_host = go.AddComponent<MheTestBench>();
            }
            s_host.StopAllCoroutines();
            s_host.StartCoroutine(s_host.Block(slotNumber, orders, speed, minutes));
        }

        private IEnumerator Block(int slotNumber, int orders, float speed, float minutes)
        {
            Running = true;
            DailyObjectivesHud.SuppressRecap = true;
            if (GameCore.Diagnostics.PlaytestBot.Instance != null && GameCore.Diagnostics.PlaytestBot.Instance.Running) GameCore.Diagnostics.PlaytestBot.Instance.StopBot();

            var sm = SaveLoadSystem.SaveManager.Instance;
            if (sm == null || !sm.LoadFromSlot(Mathf.Clamp(slotNumber, 1, 8) - 1))
            { Debug.LogWarning($"[MheTest] could not load save slot {slotNumber}."); Running = false; yield break; }

            // let the load settle (NavMesh bake, restored machines and pallets)
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 3f || (!NavMeshManager.IsReady && Time.realtimeSinceStartup - t0 < 20f)) yield return null;

            var released = new List<string>();
            if (ServiceLocator.TryGet<OrderService>(out var os) && os != null)
            {
                var candidates = os.ActiveOrders.Where(o => o != null && o.Status == OrderData.OrderStatus.Pending && !o.ReleasedToFloor)
                                                .OrderBy(o => o.TotalUnits).Take(Mathf.Max(0, orders)).ToList();
                foreach (var o in candidates)
                    if (os.ReleaseOrdersToStages(new List<string> { o.OrderId }, out _, out _, out _))
                        released.Add($"{o.OrderNumber} ({o.TotalUnits} cases, lane {o.AssignedDoorNumber}{o.AssignedLane})");
            }
            MheTrafficProbe.ResetCounts();
            var topBar = FindAnyObjectByType<TopBarUI>();
            if (topBar != null) topBar.SetGameSpeed(speed); else Time.timeScale = speed;
            Debug.LogWarning($"[MheTest] block started: slot {slotNumber}, {speed:0.#}x, released {released.Count}: {string.Join(", ", released)}");

            float start = Time.realtimeSinceStartup, nextSummary = start + 60f;
            while (minutes <= 0f || Time.realtimeSinceStartup - start < minutes * 60f)
            {
                if (Time.realtimeSinceStartup >= nextSummary)
                {
                    nextSummary += 60f;
                    Debug.LogWarning($"[MheTest] {(Time.realtimeSinceStartup - start) / 60f:0} min: {MheTrafficProbe.Summary()}");
                }
                yield return null;
            }
            LastResult = MheTrafficProbe.Summary();
            Debug.LogWarning($"[MheTest] block finished after {minutes:0.#} real min: {LastResult}");
            if (topBar != null) topBar.SetGameSpeed(0f); else Time.timeScale = 0f;
            Running = false;
        }
    }
}
