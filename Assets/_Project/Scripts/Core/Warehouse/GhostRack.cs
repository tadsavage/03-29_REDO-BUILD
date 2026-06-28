using UnityEngine;
using System.Collections.Generic;

namespace Warehouse
{
    /// <summary>
    /// Marks a rack as "under construction" (ghost prefab mode)
    /// Handles double-click detection for initialization
    /// </summary>
    public class GhostRack : MonoBehaviour
    {
        [SerializeField] private float _doubleClickTimeWindow = 0.3f;
        [SerializeField] private Material _ghostMaterial;

        private List<GameObject> _rackLocations = new List<GameObject>();
        private float _lastClickTime = 0f;
        private bool _isInitialized = false;

        public void SetupAsGhost(List<GameObject> locations)
        {
            _rackLocations = locations;
            _isInitialized = false;

            // Apply ghost material to all locations
            ApplyGhostMaterial();

            // Add a collider for click detection if needed
            if (GetComponent<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                // Size the collider to encompass all locations
                UpdateColliderBounds();
            }
        }

        private void ApplyGhostMaterial()
        {
            if (_ghostMaterial == null)
            {
                Debug.LogWarning("Ghost material not assigned to GhostRack");
                return;
            }

            foreach (var loc in _rackLocations)
            {
                var renderer = loc.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mats = new Material[renderer.materials.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = new Material(_ghostMaterial);
                    }
                    renderer.materials = mats;
                }
            }
        }

        private void UpdateColliderBounds()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;

            var bounds = new Bounds(_rackLocations[0].transform.position, Vector3.zero);
            foreach (var loc in _rackLocations)
            {
                bounds.Encapsulate(loc.transform.position);
            }

            box.center = bounds.center - transform.position;
            box.size = bounds.size + Vector3.one * 0.5f; // Add padding
        }

        private void OnMouseUp()
        {
            if (_isInitialized) return;

            float timeSinceLastClick = Time.time - _lastClickTime;

            if (timeSinceLastClick < _doubleClickTimeWindow)
            {
                // Double-click detected!
                InitializeAisle();
            }

            _lastClickTime = Time.time;
        }

        private void InitializeAisle()
        {
            // Create modal
            var modalGO = new GameObject("AisleInitializationModal");
            var modal = modalGO.AddComponent<AisleInitializationModal>();
            modal.Open(_rackLocations);

            _isInitialized = true;
        }

        public bool IsInitialized => _isInitialized;
    }
}
