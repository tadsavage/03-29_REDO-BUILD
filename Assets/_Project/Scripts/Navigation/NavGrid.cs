using UnityEngine;

namespace GameCore.Navigation
{
    /// <summary>
    /// Walkability layer for FLOOR VEHICLES (pallet jacks, reach trucks, dock stockers), the data half of the grid planner.
    /// It is DERIVED from PlacementGrid (see <see cref="NavGridBuilder"/>) and is the single place the movement rules live as DATA:
    ///   - blocked cells  : static (racks, walls, barriers: from the placement grid) and dynamic (reservations: parked machines, floor pallets)
    ///   - cell cost      : extra cost to enter a cell ("keep distance from the rack" is a cost ring, not runtime code)
    ///   - exit directions: which world directions a vehicle may LEAVE a cell in (a one-way aisle is a mask, not a state machine)
    /// Headings are 0=North(+Z) 1=East(+X) 2=South(-Z) 3=West(-X). Cell (x,z) is the same indexing as PlacementGrid's (x, y).
    /// Pure data, no per-frame work, no Unity objects: it can be built and tested without a scene.
    /// </summary>
    public sealed class NavGrid
    {
        public const int North = 0, East = 1, South = 2, West = 3;
        public static readonly int[] DX = { 0, 1, 0, -1 };
        public static readonly int[] DZ = { 1, 0, -1, 0 };
        public const byte AllExits = 0x0F;

        public readonly int Width, Height;
        public readonly float CellSize;
        public readonly Vector3 Origin;

        private readonly bool[] _staticBlocked;
        private readonly bool[] _dynamicBlocked;
        private readonly float[] _cost;
        private readonly byte[] _exits;

        /// <summary>Bumped on every change, so a path planned against an older version can tell it may be stale.</summary>
        public int Version { get; private set; }

        public NavGrid(int width, int height, float cellSize, Vector3 origin)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            CellSize = Mathf.Max(0.01f, cellSize);
            Origin = origin;
            int n = Width * Height;
            _staticBlocked = new bool[n];
            _dynamicBlocked = new bool[n];
            _cost = new float[n];
            _exits = new byte[n];
            for (int i = 0; i < n; i++) _exits[i] = AllExits;
        }

        public int Index(int x, int z) => z * Width + x;
        public bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Height;

        /// <summary>Outside the grid counts as blocked: a machine never plans off the map.</summary>
        public bool IsBlocked(int x, int z)
        {
            if (!InBounds(x, z)) return true;
            int i = Index(x, z);
            return _staticBlocked[i] || _dynamicBlocked[i];
        }

        public bool IsStaticBlocked(int x, int z) => !InBounds(x, z) || _staticBlocked[Index(x, z)];
        public bool IsDynamicBlocked(int x, int z) => InBounds(x, z) && _dynamicBlocked[Index(x, z)];

        public void SetStaticBlocked(int x, int z, bool blocked)
        {
            if (!InBounds(x, z)) return;
            int i = Index(x, z);
            if (_staticBlocked[i] == blocked) return;
            _staticBlocked[i] = blocked;
            Version++;
        }

        public void SetDynamicBlocked(int x, int z, bool blocked)
        {
            if (!InBounds(x, z)) return;
            int i = Index(x, z);
            if (_dynamicBlocked[i] == blocked) return;
            _dynamicBlocked[i] = blocked;
            Version++;
        }

        public void ClearDynamic()
        {
            bool any = false;
            for (int i = 0; i < _dynamicBlocked.Length; i++)
                if (_dynamicBlocked[i]) { _dynamicBlocked[i] = false; any = true; }
            if (any) Version++;
        }

        public float GetCost(int x, int z) => InBounds(x, z) ? _cost[Index(x, z)] : 0f;

        public void SetCost(int x, int z, float cost)
        {
            if (!InBounds(x, z)) return;
            _cost[Index(x, z)] = Mathf.Max(0f, cost);
            Version++;
        }

        public void AddCost(int x, int z, float extra)
        {
            if (!InBounds(x, z)) return;
            int i = Index(x, z);
            _cost[i] = Mathf.Max(0f, _cost[i] + extra);
            Version++;
        }

        /// <summary>Bitmask of world directions (bit d = heading d) a vehicle may leave this cell in.</summary>
        public byte GetExits(int x, int z) => InBounds(x, z) ? _exits[Index(x, z)] : (byte)0;

        public void SetExits(int x, int z, byte mask)
        {
            if (!InBounds(x, z)) return;
            _exits[Index(x, z)] = (byte)(mask & AllExits);
            Version++;
        }

        /// <summary>Allow leaving this cell only in direction <paramref name="dir"/> (a one-way aisle cell).</summary>
        public void SetOneWay(int x, int z, int dir) => SetExits(x, z, (byte)(1 << (dir & 3)));

        public bool CanExit(int x, int z, int dir) => (GetExits(x, z) & (1 << (dir & 3))) != 0;

        public Vector2Int WorldToCell(Vector3 world)
        {
            Vector3 l = world - Origin;
            return new Vector2Int(Mathf.FloorToInt(l.x / CellSize), Mathf.FloorToInt(l.z / CellSize));
        }

        public Vector3 CellCenter(int x, int z, float y = 0f)
        {
            return new Vector3(Origin.x + (x + 0.5f) * CellSize, y, Origin.z + (z + 0.5f) * CellSize);
        }

        /// <summary>Nearest of the four lattice headings to a world-space forward vector.</summary>
        public static int HeadingOf(Vector3 forward)
        {
            if (Mathf.Abs(forward.x) > Mathf.Abs(forward.z)) return forward.x >= 0f ? East : West;
            return forward.z >= 0f ? North : South;
        }

        public static Vector3 ForwardOf(int heading)
        {
            heading &= 3;
            return new Vector3(DX[heading], 0f, DZ[heading]);
        }
    }
}
