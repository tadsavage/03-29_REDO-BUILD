using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates location names in AA-BB-LC format:
/// AA = Aisle number (01-99)
/// BB = Bay number (following Z-pick sequence, even/odd sides)
/// L = Level (0,1 for picks, A-F for reserves)
/// C = Column/Position (0, 1)
/// </summary>
public class LocationNameGenerator
{
    public class LocationName
    {
        public string fullName; // e.g., "01-02-00"
        public int aisle;
        public int bay;
        public string level; // "0", "1", "A", "B", etc.
        public int position; // 0 or 1
    }

    /// <summary>
    /// Generate all location names for an aisle setup.
    /// </summary>
    public static List<LocationName> GenerateAisleLocations(
        int aisleNumber,
        RackCollection[] collections,
        string[] levelDesignations,
        float chevronRotation)
    {
        var locations = new List<LocationName>();

        // Determine if collections are on even or odd side based on chevron direction
        bool isFirstCollectionEven = DetermineEvenOddSide(collections, chevronRotation);

        // Generate bay sequences for each collection
        for (int colIndex = 0; colIndex < collections.Length; colIndex++)
        {
            var collection = collections[colIndex];
            bool isEvenSide = (colIndex == 0) ? isFirstCollectionEven : !isFirstCollectionEven;

            // Generate bay numbers for this collection using Z-pick pattern
            var bayNumbers = GenerateBayNumbers(collection.Racks.Count, isEvenSide);

            // For each bay, generate all levels and positions
            for (int bayIdx = 0; bayIdx < bayNumbers.Count; bayIdx++)
            {
                int bayNum = bayNumbers[bayIdx];

                for (int levelIdx = 0; levelIdx < 6; levelIdx++)
                {
                    string levelDesignation = levelDesignations[levelIdx];
                    string levelChar = ConvertLevelToChar(levelIdx, levelDesignation);

                    for (int pos = 0; pos < 2; pos++)
                    {
                        var location = new LocationName
                        {
                            aisle = aisleNumber,
                            bay = bayNum,
                            level = levelChar,
                            position = pos
                        };
                        location.fullName = $"{location.aisle:D2}-{location.bay:D2}-{location.level}{location.position}";

                        locations.Add(location);
                    }
                }
            }
        }

        return locations;
    }

    private static bool DetermineEvenOddSide(RackCollection[] collections, float chevronRotation)
    {
        if (collections.Length == 0) return true;

        // Chevron rotation determines which side is "even"
        // This is based on the direction of travel set by the chevron
        // For now: rotation 0 = first collection is even, rotation 180 = first collection is odd
        return chevronRotation < 90f;
    }

    private static List<int> GenerateBayNumbers(int rackCount, bool isEvenSide)
    {
        var bays = new List<int>();

        if (isEvenSide)
        {
            // Even side: 02, 04, 06, 08, 10...
            for (int i = 0; i < rackCount; i++)
            {
                bays.Add(2 + (i * 2));
            }
        }
        else
        {
            // Odd side: 01, 03, 05, 07, 09...
            for (int i = 0; i < rackCount; i++)
            {
                bays.Add(1 + (i * 2));
            }
        }

        return bays;
    }

    private static string ConvertLevelToChar(int levelIndex, string designation)
    {
        if (designation == "Pick")
        {
            // Levels 0-1 are picks (numeric)
            return levelIndex.ToString();
        }
        else
        {
            // Levels 2-5 become A-D (reserves)
            // Level 2 = A, Level 3 = B, Level 4 = C, Level 5 = D
            return ((char)('A' + (levelIndex - 2))).ToString();
        }
    }

    /// <summary>
    /// Get a location name for a specific rack, level, and position.
    /// </summary>
    public static LocationName GetLocationName(
        int aisleNumber,
        int bayNumber,
        int levelIndex,
        string levelDesignation,
        int position)
    {
        string levelChar = ConvertLevelToChar(levelIndex, levelDesignation);

        return new LocationName
        {
            aisle = aisleNumber,
            bay = bayNumber,
            level = levelChar,
            position = position,
            fullName = $"{aisleNumber:D2}-{bayNumber:D2}-{levelChar}{position}"
        };
    }
}
