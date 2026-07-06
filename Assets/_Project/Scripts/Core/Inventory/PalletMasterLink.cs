using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Inventory
{
    /// <summary>
    /// Lightweight tag linking a physical pallet GameObject back to its PalletMasterRecord.PalletId.
    ///
    /// Trailer/staging-lane pallets (spawned by TruckController.LoadShipment + carried by
    /// TrailerOffloadController) deliberately have NO PlacedObject/BuildingData component — they must
    /// not self-register in the build-mode PlacedObjectRegistry at (0,0). That means the normal
    /// "find a PlacedObject by grid cell" lookup can't locate them. This tag + static registry is the
    /// substitute: whoever registers the pallet's PalletMasterRecord attaches this component so later
    /// systems (the Receiver's ReceiverReceivingWorkflow) can find the physical pallet for a given
    /// master record id.
    /// </summary>
    public class PalletMasterLink : MonoBehaviour
    {
        public string PalletId { get; private set; }

        private static readonly Dictionary<string, PalletMasterLink> _registry = new();

        public static void Attach(GameObject pallet, string palletId)
        {
            // AddComponent() fires OnEnable() synchronously, BEFORE this method can set PalletId on
            // the next line — so OnEnable's registration guard always saw an empty id and silently
            // skipped it, leaving the pallet permanently unfindable via Find(). Register explicitly
            // here too; OnEnable/OnDisable still handle any later enable/disable cycles correctly
            // since PalletId is set by the time those would fire again.
            var link = pallet.AddComponent<PalletMasterLink>();
            link.PalletId = palletId;
            if (!string.IsNullOrEmpty(palletId))
                _registry[palletId] = link;
        }

        public static PalletMasterLink Find(string palletId)
        {
            return !string.IsNullOrEmpty(palletId) && _registry.TryGetValue(palletId, out var link) ? link : null;
        }

        /// <summary>All currently-tracked physical pallets. Used to find an unoccupied side to stand
        /// on next to a target pallet (see ReceivingTaskDriver) — cheaper and more reliable than a
        /// physics overlap since every tracked pallet is guaranteed to be registered here.</summary>
        public static IEnumerable<PalletMasterLink> All => _registry.Values;

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(PalletId))
                _registry[PalletId] = this;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(PalletId) && _registry.TryGetValue(PalletId, out var current) && current == this)
                _registry.Remove(PalletId);
        }
    }
}
