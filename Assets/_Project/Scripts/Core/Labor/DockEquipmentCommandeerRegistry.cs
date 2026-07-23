using System.Collections.Generic;

namespace GameCore.Labor
{
    /// <summary>
    /// Tracks which MHEOperatorSlots are currently commandeered by a choreographed dock-equipment
    /// controller (TrailerOffloadController, TrailerLoadController) — a transform-driving takeover
    /// distinct from just "boarded" (MHEOperatorSlot.IsOccupied stays true the whole time an operator
    /// is boarded, whether idle or actively being driven by a controller; AiNavigation is likewise
    /// already disabled from the moment they board, not just while a controller is using them).
    ///
    /// Shared across controllers so two different ones competing for the same pool of Dock Stocker
    /// Operators can never grab the same physical employee in the same frame — each controller's own
    /// scan already prevents re-claiming a slot IT holds, but that guard is per-instance and can't see
    /// a claim held by a different controller.
    /// </summary>
    internal static class DockEquipmentCommandeerRegistry
    {
        private static readonly HashSet<MHEOperatorSlot> _commandeered = new();

        public static bool IsCommandeered(MHEOperatorSlot slot) => slot != null && _commandeered.Contains(slot);
        public static void Commandeer(MHEOperatorSlot slot) { if (slot != null) _commandeered.Add(slot); }
        public static void Release(MHEOperatorSlot slot) { if (slot != null) _commandeered.Remove(slot); }
    }
}
