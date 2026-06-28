using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace Warehouse
{
    /// <summary>
    /// One placed rack SECTION (a single bay-level — e.g. Rack-FullOrange48 = one 48" bay). It wears
    /// the GhostReplacerOrange material and hides its labels while uninitialized. Aisle setup is
    /// driven by the floor chevron pads (AislePad); on success the owning pad calls
    /// <see cref="RestoreFromGhost"/> on each section in the aisle.
    /// </summary>
    [RequireComponent(typeof(RackLabelDisplay))]
    public class GhostRack : MonoBehaviour
    {
        private readonly Dictionary<Renderer, Material[]> _originalMaterials = new();
        private bool _isInitialized = false;
        private Material _ghostMaterial;

        public bool IsInitialized => _isInitialized;

        private void Awake()
        {
            ApplyGhost();
        }

        // ── Ghosting ─────────────────────────────────────────────────────────────
        private void ApplyGhost()
        {
            _ghostMaterial = Resources.Load<Material>("Materials/GhostReplacerOrange");
            if (_ghostMaterial == null)
            {
                Debug.LogError("[GhostRack] Failed to load Resources/Materials/GhostReplacerOrange.");
                return;
            }

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TextMeshPro>() != null) continue; // never recolor label text
                if (!_originalMaterials.ContainsKey(r))
                    _originalMaterials[r] = r.sharedMaterials;

                var ghosted = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ghosted.Length; i++) ghosted[i] = _ghostMaterial;
                r.sharedMaterials = ghosted;
            }

            // Hide label faces entirely (cube + text) while uninitialized — toggle the label
            // CONTAINER (the cube parent that holds the TMP), not just the TMP child.
            // AisleInitializer re-activates only the aisle-facing faces on success.
            foreach (var tmp in GetComponentsInChildren<TextMeshPro>(true))
            {
                var face = tmp.gameObject;
                if (tmp.transform.parent != null && tmp.transform.parent != transform)
                    face = tmp.transform.parent.gameObject;
                face.SetActive(false);
            }
        }

        /// <summary>Restore real materials and mark this section initialized. Labels are (re)enabled
        /// by AisleInitializer when it stamps the name.</summary>
        public void RestoreFromGhost()
        {
            foreach (var kvp in _originalMaterials)
                if (kvp.Key != null) kvp.Key.sharedMaterials = kvp.Value;
            _originalMaterials.Clear();
            _isInitialized = true;
        }

        // Aisle setup is now driven by the floor chevron pads (AislePad), not by clicking the rack.
        // GhostRack only handles ghosting + RestoreFromGhost now.
    }
}
