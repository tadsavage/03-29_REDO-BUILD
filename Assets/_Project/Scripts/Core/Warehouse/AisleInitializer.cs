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
            AisleSide workerSide,
            List<LevelConfig> levelConfigs,
            Vector3? corridorCenter = null)
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

            // ── Which world direction is the worker (aisle) side? Used to cull interior labels. ──
            Vector3 runDir = runAlongX ? Vector3.right : Vector3.forward;
            Vector3 rightOfRun = Vector3.Cross(Vector3.up, runDir).normalized;
            Vector3 workerSideDir = (workerSide == AisleSide.Right) ? rightOfRun : -rightOfRun;

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
                    tmp.enabled = true;
                    // Fit the name on one line of the label cube (was overflowing/wrapping at the
                    // prefab's size 2). Auto-size shrinks to fit; no-wrap keeps it on one line.
                    tmp.enableWordWrapping = false;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 0.4f;
                    tmp.fontSizeMax = 1.5f;
                }

                // Cull interior faces — keep only the labels facing this aisle. With a corridor
                // centre, "this aisle" = the face pointing toward the walkway centre (so each
                // corridor lights the two facing rows). Otherwise fall back to the worker side.
                Vector3 keepDir = corridorCenter.HasValue
                    ? new Vector3(corridorCenter.Value.x - section.transform.position.x, 0f,
                                  corridorCenter.Value.z - section.transform.position.z).normalized
                    : workerSideDir;
                CullInteriorLabels(section, keepDir);

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
                      $"→ {result.Count} locations (run along {(runAlongX ? "X" : "Z")}, worker side {workerSide}).");
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
        /// Disable the label faces (cube + TMP GameObject) that point AWAY from the worker aisle,
        /// leaving only the outside/aisle-facing labels enabled. A label's outward direction is its
        /// horizontal offset from the section centre; faces whose outward direction agrees with the
        /// worker-side direction are kept, the rest are turned off.
        /// </summary>
        private static void CullInteriorLabels(RackLabelDisplay section, Vector3 keepDir)
        {
            Vector3 center = section.transform.position;
            foreach (var tmp in section.GetComponentsInChildren<TMPro.TextMeshPro>(true))
            {
                // The face direction comes from the TMP's own world position (it carries the
                // ±front/rear offset), but we toggle the whole label CONTAINER — the cube parent
                // that holds both the white backing and the text — so both turn off together.
                Vector3 outward = tmp.transform.position - center;
                outward.y = 0f;

                var face = tmp.gameObject;
                var parent = tmp.transform.parent;
                if (parent != null && parent != section.transform) face = parent.gameObject;

                if (outward.sqrMagnitude < 0.0001f) { face.SetActive(true); continue; } // centred, keep
                bool facesAisle = Vector3.Dot(outward.normalized, keepDir) > 0f;
                face.SetActive(facesAisle);
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

    public enum AisleSide { Left, Right }
}
