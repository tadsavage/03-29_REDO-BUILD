using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Warehouse
{
    public class WarehouseLocationsRegistry : MonoBehaviour
    {
        private static WarehouseLocationsRegistry _instance;
        public static WarehouseLocationsRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<WarehouseLocationsRegistry>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("WarehouseLocationsRegistry");
                        _instance = obj.AddComponent<WarehouseLocationsRegistry>();
                    }
                }
                return _instance;
            }
        }

        [SerializeField] private List<RackLocation> _allLocations = new List<RackLocation>();
        [SerializeField] private List<int> _usedAisles = new List<int>();

        private string _dataPath;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _dataPath = Path.Combine(Application.persistentDataPath, "warehouse_locations.json");
            LoadFromJson();
        }

        /// <summary>
        /// Add a new location to the registry
        /// </summary>
        public void AddLocation(RackLocation location)
        {
            if (_allLocations.Any(l => l.LocationName == location.LocationName))
            {
                Debug.LogWarning($"Location {location.LocationName} already exists!");
                return;
            }

            _allLocations.Add(location);

            // Track aisle usage
            if (!_usedAisles.Contains(location.AisleNumber))
            {
                _usedAisles.Add(location.AisleNumber);
                _usedAisles.Sort();
            }

            SaveToJson();
        }

        /// <summary>
        /// Add multiple locations (bulk operation)
        /// </summary>
        public void AddLocations(List<RackLocation> locations)
        {
            foreach (var loc in locations)
            {
                if (!_allLocations.Any(l => l.LocationName == loc.LocationName))
                {
                    _allLocations.Add(loc);

                    if (!_usedAisles.Contains(loc.AisleNumber))
                    {
                        _usedAisles.Add(loc.AisleNumber);
                    }
                }
            }

            _usedAisles.Sort();
            SaveToJson();
        }

        /// <summary>
        /// Get a location by name
        /// </summary>
        public RackLocation GetLocation(string locationName)
        {
            return _allLocations.FirstOrDefault(l => l.LocationName == locationName);
        }

        /// <summary>
        /// Get all locations in an aisle
        /// </summary>
        public List<RackLocation> GetAisleLocations(int aisleNumber)
        {
            return _allLocations.Where(l => l.AisleNumber == aisleNumber).ToList();
        }

        /// <summary>
        /// Check if an aisle number is already used
        /// </summary>
        public bool IsAisleUsed(int aisleNumber)
        {
            return _usedAisles.Contains(aisleNumber);
        }

        /// <summary>
        /// Get all aisle numbers currently in use
        /// </summary>
        public List<int> GetUsedAisles()
        {
            return new List<int>(_usedAisles);
        }

        /// <summary>
        /// Update a location's status
        /// </summary>
        public void UpdateLocationStatus(string locationName, LocationStatus status)
        {
            var loc = GetLocation(locationName);
            if (loc != null)
            {
                loc.Status = status;
                SaveToJson();
            }
        }

        /// <summary>
        /// Get all locations
        /// </summary>
        public List<RackLocation> GetAllLocations()
        {
            return new List<RackLocation>(_allLocations);
        }

        /// <summary>
        /// Clear all locations (dangerous — use with caution)
        /// </summary>
        public void Clear()
        {
            _allLocations.Clear();
            _usedAisles.Clear();
            SaveToJson();
        }

        /// <summary>
        /// Save locations to JSON file
        /// </summary>
        private void SaveToJson()
        {
            try
            {
                var wrapper = new LocationListWrapper { locations = _allLocations };
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(_dataPath, json);
                Debug.Log($"Warehouse locations saved to {_dataPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save warehouse locations: {e.Message}");
            }
        }

        /// <summary>
        /// Load locations from JSON file
        /// </summary>
        private void LoadFromJson()
        {
            try
            {
                if (File.Exists(_dataPath))
                {
                    string json = File.ReadAllText(_dataPath);
                    var wrapper = JsonUtility.FromJson<LocationListWrapper>(json);
                    _allLocations = wrapper.locations ?? new List<RackLocation>();

                    // Rebuild aisle list
                    _usedAisles = _allLocations.Select(l => l.AisleNumber).Distinct().OrderBy(a => a).ToList();
                    Debug.Log($"Loaded {_allLocations.Count} locations from {_dataPath}");
                }
                else
                {
                    Debug.Log("No warehouse locations file found. Starting fresh.");
                    _allLocations = new List<RackLocation>();
                    _usedAisles = new List<int>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load warehouse locations: {e.Message}");
                _allLocations = new List<RackLocation>();
                _usedAisles = new List<int>();
            }
        }

        [System.Serializable]
        private class LocationListWrapper
        {
            public List<RackLocation> locations = new List<RackLocation>();
        }
    }
}
