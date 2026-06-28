using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Warehouse
{
    public class AisleInitializer : MonoBehaviour
    {
        /// <summary>
        /// Initialize an aisle from placed rack objects
        /// </summary>
        public static List<RackLocation> InitializeAisle(
            List<GameObject> rackLocations,
            int aisleNumber,
            AisleSide workerSide,
            List<LevelConfig> levelConfigs)
        {
            var result = new List<RackLocation>();

            // Determine which locations are on which side
            var (activeSideLocations, inactiveSideLocations) = DivideBySide(rackLocations, workerSide);

            // Disable inactive side to reduce overhead
            DisableSide(inactiveSideLocations);

            // Group locations by their Z height (vertical level)
            var locationsByHeight = GroupLocationsByHeight(activeSideLocations);

            // Validate and get position codes
            var positionCodes = AssignPositionCodes(activeSideLocations);

            // Generate location names
            int levelIndex = 0;
            foreach (var (height, locs) in locationsByHeight.OrderBy(kvp => kvp.Key))
            {
                if (levelIndex >= levelConfigs.Count)
                {
                    Debug.LogWarning("More physical levels than configured levels. Stopping.");
                    break;
                }

                var levelConfig = levelConfigs[levelIndex];
                string levelDesignation = GetLevelDesignation(levelIndex, levelConfig.Type);

                foreach (var loc in locs)
                {
                    if (!positionCodes.TryGetValue(loc, out string posCode))
                    {
                        Debug.LogWarning($"No position code assigned for location at {loc.transform.position}");
                        continue;
                    }

                    string locationName = FormatLocationName(aisleNumber, posCode, levelDesignation);
                    float locHeight = GetLocationHeight(loc);

                    var rackLoc = new RackLocation
                    {
                        LocationName = locationName,
                        AisleNumber = aisleNumber,
                        PositionCode = posCode,
                        Level = levelDesignation,
                        WorldPosition = loc.transform.position,
                        GridCell = WorldToGridCell(loc.transform.position),
                        Height = locHeight,
                        Type = levelConfig.Type,
                        Status = LocationStatus.Available,
                        MaxPallets = GetMaxPalletsForType(levelConfig.Type),
                        CurrentPalletCount = 0
                    };

                    result.Add(rackLoc);

                    // Update the TextMeshPro label on the location
                    UpdateLocationLabel(loc, locationName);
                }

                levelIndex++;
            }

            return result;
        }

        /// <summary>
        /// Group rack locations by their physical height (vertical level)
        /// </summary>
        private static Dictionary<float, List<GameObject>> GroupLocationsByHeight(List<GameObject> locations)
        {
            var grouped = new Dictionary<float, List<GameObject>>();

            foreach (var loc in locations)
            {
                float height = Mathf.Round(loc.transform.position.y, 2); // Round to avoid floating point issues

                if (!grouped.ContainsKey(height))
                {
                    grouped[height] = new List<GameObject>();
                }
                grouped[height].Add(loc);
            }

            return grouped;
        }

        /// <summary>
        /// Assign position codes (AA, AB, AC, AD, AE, AF, AG, AH, etc.) based on grid layout
        /// Uses left-to-right, front-to-back assignment
        /// </summary>
        private static Dictionary<GameObject, string> AssignPositionCodes(List<GameObject> locations)
        {
            var result = new Dictionary<GameObject, string>();

            // Get unique X positions (left-right), sort left to right
            var uniqueX = locations.Select(l => Mathf.Round(l.transform.position.x, 2))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            // Get unique Z positions (front-back), sort front to back
            var uniqueZ = locations.Select(l => Mathf.Round(l.transform.position.z, 2))
                .Distinct()
                .OrderBy(z => z)
                .ToList();

            // Assign position codes sequentially
            // Format: AA, AB, AC, AD, AE, AF, AG, AH, ... BA, BB, BC, etc.
            int positionIndex = 0;
            string currentPrefix = "A";
            int prefixCounter = 0;

            foreach (var z in uniqueZ)
            {
                foreach (var x in uniqueX)
                {
                    var matchingLoc = locations.FirstOrDefault(l =>
                        Mathf.Abs(l.transform.position.x - x) < 0.1f &&
                        Mathf.Abs(l.transform.position.z - z) < 0.1f);

                    if (matchingLoc != null)
                    {
                        // Generate position code (AA, AB, AC, etc.)
                        char prefix = (char)('A' + prefixCounter);
                        char suffix = (char)('A' + positionIndex);
                        string posCode = $"{prefix}{suffix}";

                        result[matchingLoc] = posCode;

                        positionIndex++;
                        if (positionIndex >= 26) // Reset after Z
                        {
                            positionIndex = 0;
                            prefixCounter++;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get the level designation (numeric for pick, alpha for reserve)
        /// </summary>
        private static string GetLevelDesignation(int levelIndex, LocationType type)
        {
            if (type == LocationType.Pick)
            {
                return (levelIndex + 1).ToString(); // "1", "2", "3", etc.
            }
            else // Reserve
            {
                return ((char)('A' + levelIndex)).ToString(); // "A", "B", "C", etc.
            }
        }

        /// <summary>
        /// Format the full location name
        /// </summary>
        private static string FormatLocationName(int aisleNumber, string posCode, string level)
        {
            return $"{aisleNumber:D2}-{posCode}-{level}";
        }

        /// <summary>
        /// Get the physical height of a location (in inches, for validation)
        /// </summary>
        private static float GetLocationHeight(GameObject location)
        {
            // Get the Y scale or mesh bounds to determine height
            // For now, using a placeholder; adjust based on your rack structure
            var collider = location.GetComponent<Collider>();
            if (collider != null)
            {
                return collider.bounds.size.y * 39.37f; // Convert meters to inches
            }

            return location.transform.localScale.y * 39.37f;
        }

        /// <summary>
        /// Convert world position to grid cell
        /// </summary>
        private static Vector3Int WorldToGridCell(Vector3 worldPos)
        {
            // Placeholder; adjust based on your grid system
            const float cellSize = 1.33f;
            return new Vector3Int(
                Mathf.RoundToInt(worldPos.x / cellSize),
                Mathf.RoundToInt(worldPos.y / cellSize),
                Mathf.RoundToInt(worldPos.z / cellSize)
            );
        }

        /// <summary>
        /// Get max pallets for a location type
        /// </summary>
        private static int GetMaxPalletsForType(LocationType type)
        {
            return type == LocationType.Pick ? 1 : 2; // Picks: 1 pallet, Reserves: 2 pallets (can stack)
        }

        /// <summary>
        /// Update the TextMeshPro label on the location with its name
        /// </summary>
        private static void UpdateLocationLabel(GameObject location, string locationName)
        {
            // Find TextMeshPro child element
            var textMesh = location.GetComponentInChildren<TMPro.TextMeshPro>();
            if (textMesh != null)
            {
                textMesh.text = locationName;
            }
            else
            {
                Debug.LogWarning($"No TextMeshPro found on location at {location.transform.position}");
            }
        }

        /// <summary>
        /// Divide locations into active side and inactive side based on worker side
        /// </summary>
        private static (List<GameObject> activeSide, List<GameObject> inactiveSide) DivideBySide(
            List<GameObject> rackLocations,
            AisleSide workerSide)
        {
            // Find the X midpoint
            var xPositions = rackLocations.Select(l => l.transform.position.x).OrderBy(x => x).ToList();
            float midX = (xPositions.First() + xPositions.Last()) / 2f;

            var activeSide = new List<GameObject>();
            var inactiveSide = new List<GameObject>();

            foreach (var loc in rackLocations)
            {
                bool isLeftSide = loc.transform.position.x < midX;
                bool isActive = (workerSide == AisleSide.Left && isLeftSide) ||
                               (workerSide == AisleSide.Right && !isLeftSide);

                if (isActive)
                    activeSide.Add(loc);
                else
                    inactiveSide.Add(loc);
            }

            return (activeSide, inactiveSide);
        }

        /// <summary>
        /// Disable all renderers, colliders, and TextMeshPro on inactive side
        /// </summary>
        private static void DisableSide(List<GameObject> locations)
        {
            foreach (var loc in locations)
            {
                // Disable renderer
                var renderer = loc.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.enabled = false;

                // Disable collider
                var collider = loc.GetComponent<Collider>();
                if (collider != null)
                    collider.enabled = false;

                // Disable TextMeshPro
                var textMesh = loc.GetComponentInChildren<TMPro.TextMeshPro>();
                if (textMesh != null)
                    textMesh.enabled = false;

                // Disable the GameObject itself to reduce overhead
                loc.SetActive(false);

                Debug.Log($"Disabled inactive location at {loc.transform.position}");
            }
        }
    }

    /// <summary>
    /// Level configuration during aisle initialization
    /// </summary>
    [System.Serializable]
    public class LevelConfig
    {
        public int LevelNumber;
        public LocationType Type; // Pick or Reserve
    }

    /// <summary>
    /// Which side workers enter/exit from
    /// </summary>
    public enum AisleSide
    {
        Left,
        Right
    }
}
