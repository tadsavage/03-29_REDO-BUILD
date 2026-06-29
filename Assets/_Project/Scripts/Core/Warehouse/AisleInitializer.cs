using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Warehouse
{
    /// <summary>
    /// Turns a gathered run of rack SECTIONS (each a RackLabelDisplay) into named locations.
    /// An aisle is many sections: distinct heights = levels (bottom→top), distinct positions along
    /// the run's dominant horizontal axis = positions (AA, AB, AC…). Each section gets a name
    /// "AA-PP-L" stamped through its RackLabelDisplay, and a RackLocation record for the registry.
    /// </summary>
    public static class AisleInitializer
    {
        private const float MetersToInches = 39.3701f;

        public static List<RackLocation> InitializeAisle(
            List<RackLabelDisplay> sections,
            int aisleNumber,
            List<LevelConfig> levelConfigs,
            Vector3? corridorCenter = null,
            Vector3? travelDir = null,
            Vector2? chevronPos2D = null)
        {
            var result = new List<RackLocation>();
            if (sections == null || sections.Count == 0) return result;

            // ── Levels: distinct heights, ascending (level 1 = lowest) ──
            var levelKeys = sections
                .Select(s => Round(s.transform.position.y))
                .Distinct()
                .OrderBy(y => y)
                .ToList();

            // ── Positions: project onto the run's dominant horizontal axis, distinct, ordered ──
            bool runAlongX = SpreadAlongX(sections) >= SpreadAlongZ(sections);
            var posKeys = sections
                .Select(s => Round(runAlongX ? s.transform.position.x : s.transform.position.z))
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            // ── World run direction (not used anymore since travelDir is always provided) ──
            // Left for reference: runDir is the direction along the aisle run (X or Z axis)

            foreach (var section in sections)
            {
                float yKey = Round(section.transform.position.y);
                float pKey = Round(runAlongX ? section.transform.position.x : section.transform.position.z);

                int levelIndex = levelKeys.IndexOf(yKey);                 // 0-based, bottom-up
                int posIndex = posKeys.IndexOf(pKey);                     // 0-based along the run

                var cfg = (levelIndex >= 0 && levelIndex < levelConfigs.Count) ? levelConfigs[levelIndex] : null;
                LocationType type = cfg?.Type ?? LocationType.Reserve;
                string levelDesignation = cfg?.Designation ?? ((char)('A' + levelIndex)).ToString();
                string positionCode = PositionCode(posIndex);

                string name = $"{aisleNumber:D2}-{positionCode}-{levelDesignation}";

                // Stamp the label through the existing display component (drives all 4 faces).
                section.labelText = name;
                section.UpdateDisplay();
                foreach (var tmp in section.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                {
                    if (tmp != null)
                    {
                        tmp.enableAutoSizing = true;
                        tmp.fontSizeMin = 0.4f;
                        tmp.fontSizeMax = 1.5f;
                    }
                }

                // Enable labels based on which ones face the chevron (distance comparison)
                if (chevronPos2D.HasValue)
                    EnableLabelsBasedOnDistance(section, chevronPos2D.Value);

                result.Add(new RackLocation
                {
                    LocationName = name,
                    AisleNumber = aisleNumber,
                    PositionCode = positionCode,
                    Level = levelDesignation,
                    WorldPosition = section.transform.position,
                    GridCell = WorldToCell(section.transform.position),
                    Height = (yKey - levelKeys[0]) * MetersToInches,
                    Type = type,
                    Status = LocationStatus.Available,
                    MaxPallets = type == LocationType.Pick ? 1 : 2,
                    CurrentPalletCount = 0
                });
            }

            Debug.Log($"[AisleInitializer] Aisle {aisleNumber:D2}: {levelKeys.Count} levels × {posKeys.Count} positions " +
                      $"→ {result.Count} locations (run along {(runAlongX ? "X" : "Z")}).");
            return result;
        }

        /// <summary>
        /// Detect levels for the modal: one entry per distinct height (bottom→top), with the beam
        /// height in inches and whether it's out of human reach (&gt; 80" ⇒ Reserve-locked). Beam
        /// height of a level = the cumulative opening-height of all sections below it (a 48"+48" rack
        /// gives a reachable level-2 shelf pick at 48", level-3 at 96" = reserve, etc).
        /// </summary>
        public static List<LevelInfo> DetectLevels(List<RackLabelDisplay> sections)
        {
            var byLevel = sections
                .GroupBy(s => Round(s.transform.position.y))
                .OrderBy(g => g.Key)
                .ToList();

            var levels = new List<LevelInfo>();
            float cumulativeInches = 0f;
            int n = 1;
            foreach (var g in byLevel)
            {
                int sectionInches = ParseSectionInches(g.First().gameObject);
                levels.Add(new LevelInfo
                {
                    LevelNumber = n++,
                    BeamHeightInches = cumulativeInches,        // floor this level sits at = where you reach
                    Locked = cumulativeInches > 80f
                });
                cumulativeInches += sectionInches;
            }
            return levels;
        }

        /// <summary>
        /// Enable the two labels on whichever side (front OR rear) is closest to the chevron, and
        /// disable the opposite side — never a mix of front/rear. Only one side of a rack ever shows.
        /// The enabled side's TextMeshPro elements are turned on so they render the stamped name.
        /// Skips racks already made live (IsRackLive flag prevents re-processing).
        /// </summary>
        private static void EnableLabelsBasedOnDistance(RackLabelDisplay section, Vector2 chevronPos2D)
        {
            // Hands-off: already-live racks are owned by this system and must not be re-toggled.
            if (section.IsRackLive) return;

            var rackTransform = section.transform;

            // Label container children. NOTE: the prefab names these with DOTS, not underscores.
            Transform frontL = rackTransform.Find("LabelFront.L");
            Transform frontR = rackTransform.Find("LabelFront.R");
            Transform rearL  = rackTransform.Find("LabelRear.L");
            Transform rearR  = rackTransform.Find("LabelRear.R");

            if (frontL == null || rearL == null)
            {
                Debug.LogWarning($"[AisleInitializer] '{section.name}' is missing label children " +
                                 "(expected LabelFront.L / LabelRear.L). Cannot enable labels.");
                return;
            }

            // Which side faces the chevron? Compare flat (X,Z) distance of the left label on each side.
            float frontDist = Vector2.Distance(new Vector2(frontL.position.x, frontL.position.z), chevronPos2D);
            float rearDist  = Vector2.Distance(new Vector2(rearL.position.x, rearL.position.z), chevronPos2D);
            bool enableFront = frontDist < rearDist;

            // Enable both labels on the facing side; disable both on the far side (no mixing).
            SetLabelSide(frontL, enableFront);
            SetLabelSide(frontR, enableFront);
            SetLabelSide(rearL, !enableFront);
            SetLabelSide(rearR, !enableFront);

            // Mark this rack live so nothing (including this code) re-processes it by accident.
            section.IsRackLive = true;
        }

        /// <summary>
        /// Activate/deactivate a single label container. When enabling, also make sure the child
        /// TextMeshPro element is active and enabled so it actually renders the stamped name.
        /// </summary>
        private static void SetLabelSide(Transform container, bool enabled)
        {
            if (container == null) return;
            container.gameObject.SetActive(enabled);
            if (!enabled) return;

            var tmp = container.GetComponentInChildren<TMPro.TextMeshPro>(true);
            if (tmp != null)
            {
                tmp.gameObject.SetActive(true);
                tmp.enabled = true;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private static float Round(float v) => Mathf.Round(v * 100f) / 100f;

        private static float SpreadAlongX(List<RackLabelDisplay> s) =>
            s.Max(r => r.transform.position.x) - s.Min(r => r.transform.position.x);

        private static float SpreadAlongZ(List<RackLabelDisplay> s) =>
            s.Max(r => r.transform.position.z) - s.Min(r => r.transform.position.z);

        private static string PositionCode(int index)
        {
            char prefix = (char)('A' + (index / 26));
            char suffix = (char)('A' + (index % 26));
            return $"{prefix}{suffix}";
        }

        private static int ParseSectionInches(GameObject go)
        {
            var m = Regex.Match(go.name, @"(\d{2,3})"); // e.g. "Rack-FullOrange48(Clone)" → 48
            return m.Success ? int.Parse(m.Value) : 48;
        }

        private static Vector3Int WorldToCell(Vector3 worldPos)
        {
            const float cellSize = 1.33f;
            return new Vector3Int(
                Mathf.RoundToInt(worldPos.x / cellSize),
                Mathf.RoundToInt(worldPos.y / cellSize),
                Mathf.RoundToInt(worldPos.z / cellSize));
        }
    }

    /// <summary>One vertical level, as surfaced to the modal.</summary>
    public class LevelInfo
    {
        public int LevelNumber;        // 1 = bottom
        public float BeamHeightInches;
        public bool Locked;            // > 80" ⇒ Reserve only
    }

    /// <summary>Per-level choice coming back from the modal.</summary>
    [System.Serializable]
    public class LevelConfig
    {
        public int LevelNumber;
        public LocationType Type;       // Pick or Reserve
        public string Designation;      // "1","2" (pick) or "A","B" (reserve) — filled by the modal
    }
}
