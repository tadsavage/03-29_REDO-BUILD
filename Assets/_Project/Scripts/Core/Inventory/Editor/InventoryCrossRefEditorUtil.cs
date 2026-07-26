using UnityEditor;
using UnityEngine;

namespace GameCore.Inventory.EditorTools
{
    /// <summary>
    /// Shared plumbing for the "jump to the other side of the cross-reference" buttons on
    /// PalletData (→ its rack slot) and LocationData (→ the pallet it holds).
    ///
    /// Both registries these look through (LocationRegistry, PalletMasterLink) are RUNTIME
    /// dictionaries populated during Play, so every lookup falls back to a plain scene scan. That
    /// makes the buttons work in edit mode too — which matters, because inspecting a save-loaded
    /// scene after exiting Play is exactly when you want to chase down a mis-placed pallet.
    /// </summary>
    public static class InventoryCrossRefEditorUtil
    {
        /// <summary>Selects <paramref name="target"/>, pings it in the Hierarchy, and frames the
        /// Scene view camera on it.</summary>
        public static void SelectAndFrame(GameObject target)
        {
            if (target == null) return;

            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);

            // Frame an explicit small bounds rather than FrameSelected(): a location/pallet anchor
            // often has no renderer of its own, and FrameSelected on a rendererless object zooms to
            // an arbitrary distance. A fixed 3m box always gives a usable framing.
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.Frame(new Bounds(target.transform.position, Vector3.one * 3f), false);
                view.Repaint();
            }
        }

        /// <summary>Finds the LocationData whose Address matches, runtime-registry first then a
        /// scene scan. Returns null when the address isn't present in the open scene.</summary>
        public static LocationData FindLocationByAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;

            if (Application.isPlaying && LocationRegistry.TryGet(address, out var fromRegistry) && fromRegistry != null)
                return fromRegistry;

            foreach (var ld in Object.FindObjectsByType<LocationData>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ld != null && ld.Address == address) return ld;
            }
            return null;
        }

        /// <summary>Finds the pallet GameObject for a PalletId (preferred) or LoadId (fallback),
        /// runtime-registry first then a scene scan. Returns null when neither matches.</summary>
        public static GameObject FindPallet(string palletId, string loadId)
        {
            if (Application.isPlaying && !string.IsNullOrWhiteSpace(palletId))
            {
                var link = PalletMasterLink.Find(palletId);
                if (link != null) return link.gameObject;
            }

            if (!string.IsNullOrWhiteSpace(palletId))
            {
                foreach (var l in Object.FindObjectsByType<PalletMasterLink>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (l != null && l.PalletId == palletId) return l.gameObject;
                }
            }

            // LoadId is the human-readable plate — worth trying on its own, since a pallet whose
            // master record was lost can still be identified by the plate printed on it.
            if (!string.IsNullOrWhiteSpace(loadId))
            {
                foreach (var pd in Object.FindObjectsByType<PalletData>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (pd != null && pd.LoadId == loadId) return pd.gameObject;
                }
            }

            return null;
        }

        /// <summary>Standard "nothing matched" feedback — a dialog (so it can't be missed) plus a
        /// console line naming what was searched for.</summary>
        public static void ReportNotFound(string what, string id)
        {
            string msg = string.IsNullOrWhiteSpace(id)
                ? $"No {what} recorded on this component — the field is empty."
                : $"Could not find {what} '{id}' anywhere in the open scene.\n\n" +
                  "If you are not in Play mode, the object may only exist at runtime — press Play and try again.";
            Debug.LogWarning($"[InventoryCrossRef] {msg}");
            EditorUtility.DisplayDialog("Not found", msg, "OK");
        }
    }
}
