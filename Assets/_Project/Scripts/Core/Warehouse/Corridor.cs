using System.Collections.Generic;
using UnityEngine;

namespace Warehouse
{
    /// <summary>
    /// A corridor/walkway = one AISLE. It is bordered by up to two rack rows (the side rows facing
    /// each other across the walkway). End aisles against a wall have only one side.
    /// Geometry is on the ground plane: the corridor runs along <see cref="RunAxis"/>, its two open
    /// ends are <see cref="EndA"/> / <see cref="EndB"/> (world points on the centreline).
    /// </summary>
    public class Corridor
    {
        public Vector3 RunAxis;          // unit vector along the aisle (direction of travel candidate)
        public Vector3 Centerline;       // a world point at the middle of the walkway, mid-length
        public Vector3 EndA;             // one open end (centreline)
        public Vector3 EndB;             // the other open end (centreline)
        public float Width;              // walkway width (perp gap between the two rows)

        // The rack sections on each side (all stack levels included). One may be empty for an end aisle.
        public List<RackLabelDisplay> SideRowA = new();   // row on the -perp side of the centreline
        public List<RackLabelDisplay> SideRowB = new();   // row on the +perp side

        public bool IsOneSided => SideRowA.Count == 0 || SideRowB.Count == 0;
    }
}
