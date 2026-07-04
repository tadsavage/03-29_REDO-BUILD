using UnityEngine;
using System.Collections.Generic;

namespace GameCore.Gameplay
{
    /// <summary>
    /// Represents a designated floor area (e.g., Receiving, Shipping, Storage)
    /// where pallets can be assigned and staged.
    /// </summary>
    public class StagingLane : MonoBehaviour
    {
        public enum LaneType { Receiving, Shipping, Storage, Buffer }

        [Header("Lane Settings")]
        [SerializeField] private string laneName;
        [SerializeField] private LaneType type = LaneType.Storage;
        [SerializeField] private Color laneColor = Color.cyan;
        
        [Header("Capacity")]
        [SerializeField] private int maxPallets = 5;
        private List<GameObject> _currentPallets = new();

        public string LaneName => laneName;
        public LaneType Type => type;
        public bool IsFull => _currentPallets.Count >= maxPallets;

        public void Initialize(string name, LaneType laneType, int capacity)
        {
            laneName = name;
            type = laneType;
            maxPallets = capacity;
            
            // Visual indicator setup (if any) could go here
            UpdateVisuals();
        }

        public bool TryAssignPallet(GameObject pallet)
        {
            if (IsFull) return false;
            
            _currentPallets.Add(pallet);
            // logic to position pallet within lane bounds would go here
            return true;
        }

        public void RemovePallet(GameObject pallet)
        {
            _currentPallets.Remove(pallet);
        }

        private void UpdateVisuals()
        {
            // Implementation for changing floor decals or line colors
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = laneColor;
            }
        }
    }
}
