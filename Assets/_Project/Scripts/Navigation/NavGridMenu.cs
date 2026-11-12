#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameCore.Navigation
{
    /// <summary>
    /// Tools &gt; Navigation: switch each role between the NavMeshAgent and the grid planner without a scene object. The flags are PlayerPrefs,
    /// so they stick between Play sessions: set them BEFORE a playtest block, and change ONE at a time (HAL rule: one change per block).
    /// The block start line in the console says which nav it ran on (<see cref="NavGridService.FlagSummary"/>).
    /// </summary>
    static class NavGridMenu
    {
        private const string Root = "Tools/Navigation/";

        [MenuItem(Root + "Grid legs - Pallet Jacks")]
        static void ToggleJacks() { NavGridService.UseForJacks = !NavGridService.UseForJacks; Report(); }
        [MenuItem(Root + "Grid legs - Pallet Jacks", true)]
        static bool ValidateJacks() { Menu.SetChecked(Root + "Grid legs - Pallet Jacks", NavGridService.UseForJacks); return true; }

        [MenuItem(Root + "Grid legs - Reach Trucks")]
        static void ToggleRt() { NavGridService.UseForReachTrucks = !NavGridService.UseForReachTrucks; Report(); }
        [MenuItem(Root + "Grid legs - Reach Trucks", true)]
        static bool ValidateRt() { Menu.SetChecked(Root + "Grid legs - Reach Trucks", NavGridService.UseForReachTrucks); return true; }

        [MenuItem(Root + "Grid legs - Dock Stockers")]
        static void ToggleDs() { NavGridService.UseForDockStockers = !NavGridService.UseForDockStockers; Report(); }
        [MenuItem(Root + "Grid legs - Dock Stockers", true)]
        static bool ValidateDs() { Menu.SetChecked(Root + "Grid legs - Dock Stockers", NavGridService.UseForDockStockers); return true; }

        [MenuItem(Root + "All back to NavMesh")]
        static void AllOff()
        {
            NavGridService.UseForJacks = false; NavGridService.UseForReachTrucks = false; NavGridService.UseForDockStockers = false;
            Report();
        }

        [MenuItem(Root + "Audit Reachability (in Play, paused)")]
        static void Audit()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[NavAudit] Enter Play mode with the warehouse loaded first (the clock can stay paused)."); return; }
            string report = NavAudit.Run();
            Debug.LogWarning("[NavAudit]\n" + report);
        }

        static void Report() => Debug.LogWarning("[NavGrid] " + NavGridService.FlagSummary());
    }
}
#endif
