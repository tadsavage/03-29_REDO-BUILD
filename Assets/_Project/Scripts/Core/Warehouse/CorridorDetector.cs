using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Warehouse
{
    /// <summary>
    /// Derives the warehouse's aisles (corridors) from the placed rack rows. Rows run along the
    /// dominant horizontal axis; a corridor is the walkway between two adjacent parallel rows (or
    /// outside an end row, against a wall). Pure geometry — no scene mutation here.
    /// </summary>
    public static class CorridorDetector
    {
        // Tuning (metres). Grid cell = 1.33; a min aisle is ~3 cells ≈ 4m.
        // FIXED 2026-06-28: increased thresholds so ONLY wide corridors are detected, not bay-to-bay gaps.
        private const float RowBinSize = 3.2f;     // group sections into a row if within this perp distance (bay-width+gap)
        private const float MinWalkway = 3.4f;     // centre-to-centre row gap above this = a real corridor (excludes bay gaps ~0.3m)
        private const float MaxWalkway = 8.0f;     // beyond this the rows aren't a shared aisle
        private const float EndAisleOffset = 4.0f; // assumed walkway width for a one-sided end aisle (wider than bay gap)

        public static List<Corridor> Detect()
        {
            var all = RackLabelDisplay.AllLabels.Where(l => l != null).ToList();
            if (all.Count < 1) return new List<Corridor>();

            Debug.Log($"[CorridorDetector] Starting detection with {all.Count} rack sections");

            // Dominant horizontal axis = the one with the larger spread → rows run along it.
            float spreadX = all.Max(l => l.transform.position.x) - all.Min(l => l.transform.position.x);
            float spreadZ = all.Max(l => l.transform.position.z) - all.Min(l => l.transform.position.z);
            bool runAlongX = spreadX >= spreadZ;
            Vector3 runAxis = runAlongX ? Vector3.right : Vector3.forward;

            Debug.Log($"[CorridorDetector] SpreadX={spreadX:F2}, SpreadZ={spreadZ:F2} → rows run along {(runAlongX ? "X" : "Z")}");

            float Perp(RackLabelDisplay l) => runAlongX ? l.transform.position.z : l.transform.position.x;
            float Along(RackLabelDisplay l) => runAlongX ? l.transform.position.x : l.transform.position.z;

            // Bin sections into rows by their perpendicular coordinate.
            var rows = new List<Row>();
            var perpPositions = all.Select(Perp).Distinct().OrderBy(x => x).ToList();
            Debug.Log($"[CorridorDetector] Distinct Perp positions found: {string.Join(", ", perpPositions.Select(p => p.ToString("F2")))}");

            foreach (var s in all.OrderBy(Perp))
            {
                float p = Perp(s);
                var row = rows.FirstOrDefault(r => Mathf.Abs(r.Perp - p) <= RowBinSize);
                if (row == null) { row = new Row { Perp = p }; rows.Add(row); }
                row.Sections.Add(s);
                row.Perp = row.Sections.Average(Perp); // refine centre
            }
            rows = rows.OrderBy(r => r.Perp).ToList();

            Debug.Log($"[CorridorDetector] Binned into {rows.Count} rows (RowBinSize={RowBinSize}):");
            for (int i = 0; i < rows.Count; i++)
            {
                Debug.Log($"  Row {i}: Perp={rows[i].Perp:F2}, Sections={rows[i].Sections.Count}");
            }

            float groundY = all.Min(l => l.transform.position.y);
            var corridors = new List<Corridor>();

            // Between-row corridors.
            Debug.Log($"[CorridorDetector] Checking gaps between rows (MinWalkway={MinWalkway}, MaxWalkway={MaxWalkway}):");
            for (int i = 0; i < rows.Count - 1; i++)
            {
                float gap = rows[i + 1].Perp - rows[i].Perp;
                bool passes = gap >= MinWalkway && gap <= MaxWalkway;
                Debug.Log($"  Gap between Row {i} (Perp={rows[i].Perp:F2}) and Row {i+1} (Perp={rows[i+1].Perp:F2}): {gap:F2}m → {(passes ? "ACCEPTED" : "REJECTED")}");
                if (!passes) continue;
                corridors.Add(MakeCorridor(rows[i], rows[i + 1], runAlongX, runAxis, groundY, Along));
            }

            // One-sided end aisles (outside the first and last row).
            if (rows.Count > 0)
            {
                Debug.Log($"[CorridorDetector] Adding end aisles...");
                corridors.Add(MakeEndCorridor(rows.First(), -1, runAlongX, runAxis, groundY, Along));
                corridors.Add(MakeEndCorridor(rows.Last(), +1, runAlongX, runAxis, groundY, Along));
            }

            Debug.Log($"[CorridorDetector] Total corridors detected: {corridors.Count}");
            return corridors;
        }

        private static Corridor MakeCorridor(Row a, Row b, bool runAlongX, Vector3 runAxis, float groundY,
            System.Func<RackLabelDisplay, float> along)
        {
            float perpMid = (a.Perp + b.Perp) * 0.5f;
            // Along-run overlap of the two rows.
            float minAlong = Mathf.Max(a.Sections.Min(along), b.Sections.Min(along));
            float maxAlong = Mathf.Min(a.Sections.Max(along), b.Sections.Max(along));
            if (maxAlong < minAlong) { float t = minAlong; minAlong = maxAlong; maxAlong = t; }

            return BuildCorridor(perpMid, minAlong, maxAlong, b.Perp - a.Perp, runAlongX, runAxis, groundY, a.Sections, b.Sections);
        }

        private static Corridor MakeEndCorridor(Row row, int dir, bool runAlongX, Vector3 runAxis, float groundY,
            System.Func<RackLabelDisplay, float> along)
        {
            float perpMid = row.Perp + dir * EndAisleOffset;
            float minAlong = row.Sections.Min(along);
            float maxAlong = row.Sections.Max(along);
            var sideA = dir < 0 ? row.Sections : new List<RackLabelDisplay>();
            var sideB = dir < 0 ? new List<RackLabelDisplay>() : row.Sections;
            return BuildCorridor(perpMid, minAlong, maxAlong, EndAisleOffset, runAlongX, runAxis, groundY, sideA, sideB);
        }

        private static Corridor BuildCorridor(float perpMid, float minAlong, float maxAlong, float width,
            bool runAlongX, Vector3 runAxis, float groundY, List<RackLabelDisplay> sideA, List<RackLabelDisplay> sideB)
        {
            Vector3 PointAt(float alongVal) => runAlongX
                ? new Vector3(alongVal, groundY, perpMid)
                : new Vector3(perpMid, groundY, alongVal);

            return new Corridor
            {
                RunAxis = runAxis,
                Width = Mathf.Abs(width),
                Centerline = PointAt((minAlong + maxAlong) * 0.5f),
                EndA = PointAt(minAlong),
                EndB = PointAt(maxAlong),
                SideRowA = sideA,
                SideRowB = sideB
            };
        }

        private class Row
        {
            public float Perp;
            public readonly List<RackLabelDisplay> Sections = new();
        }
    }
}
