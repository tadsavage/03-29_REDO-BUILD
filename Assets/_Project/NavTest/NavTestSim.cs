using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace NavTest
{
    /// <summary>
    /// GRAYBOX NAV TEST (2026-11-14). A cube copy of the slot 8 "three doors aisle" warehouse with plain NavMeshAgent pallet jacks and
    /// reach trucks driving pick / stage / putaway loops. Uses NO game scripts: no AiNavigation, no traffic rules, no grid planner, no
    /// clearance guard. The point is to see how a virgin NavMesh setup copes with the same racks, aisles and lanes.
    ///
    /// Geometry comes from slot 8's save (PlacementGrid cells: CellSize 1.325, Origin -66.3). Everything is built in this object's
    /// LOCAL space at the warehouse's real coordinates, so move this object to move the whole graybox.
    ///   Racks   : bays at cells x 20-35 (2 cells per bay), rows y 78 (on the dock wall), 83+84 (back to back), 89. 5 m tall.
    ///   Aisles  : y 79-82 and y 85-88, 5.3 m wide, open at both ends.
    ///   Lanes   : x 40-43, 46-50, 53-57, y 81-85 (one lane per cell column, 14 lanes). Door end = south (z 41), exit end = north (z 47.65).
    ///   Wall    : y 77 with the three door gaps. Floor = the foundation, x 14-59, y 77-94. Trucks back up to the doors from the south.
    /// Right-click the component > Build Graybox to rebuild the cubes and points. Two NavMeshes (MHE for machines, Human for the
    /// pickers on foot) are baked at Play start. Settings panel on the right of the Game view; stats table top left.
    /// </summary>
    [DisallowMultipleComponent]
    public class NavTestSim : MonoBehaviour
    {
        private const float Cell = 1.325f, Origin = -66.3f;
        public static float Edge(int c) => Origin + c * Cell;                 // a cell's low edge
        public static float Mid(int c) => Origin + c * Cell + Cell * 0.5f;    // a cell's centre

        private static readonly int[] RackRows = { 78, 83, 84, 89 };
        private static readonly int[] LaneColumns = { 40, 41, 42, 43, 46, 47, 48, 49, 50, 53, 54, 55, 56, 57 };
        private static readonly int[] WallCells = { 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 44, 45, 51, 52, 58 };
        public static readonly float[] DoorX = { -11.31f, -2.04f, 7.24f };
        public static float DockLineZ => Edge(77);                        // the outside face of the dock wall
        private const int FirstBayCell = 21, LastBayCell = 35;   // a bay covers cells (c-1, c), so its centre is Edge(c)
        public static float RackMinX => Edge(FirstBayCell - 1);
        public static float RackMaxX => Edge(LastBayCell + 1);
        private const float RackHeight = 5f;

        [Header("Machines")]
        public int jackCount = 3;
        public int reachTruckCount = 2;
        [Tooltip("Width x height x length (m).")] public Vector3 jackSize = new Vector3(1f, 1f, 2f);
        [Tooltip("Width x height x length (m).")] public Vector3 reachSize = new Vector3(1f, 1f, 1.5f);
        public float jackSpeed = 1.8f, reachSpeed = 2.2f;
        [Tooltip("NavMeshAgent avoidance radius. The baked NavMesh (MHE type) is eroded by 0.5 m whatever this is.")]
        public float jackRadius = 0.6f, reachRadius = 0.6f;
        [Tooltip("Unity avoidance priority: LOWER is more important.")]
        public int jackPriority = 40, reachPriority = 10;

        [Header("Pickers (on the jacks)")]
        public float walkSpeed = 1.3f;
        [Tooltip("Chance the jack stops away from the slot and the picker walks to it (otherwise the jack pulls up at the slot and the picker steps off).")]
        [Range(0f, 1f)] public float stopShortChance = 0.5f;
        [Tooltip("When the jack stops short, it stops this far (m) along the aisle from the slot.")]
        public float minWalk = 1.5f, maxWalk = 5f;
        [Tooltip("Cases per pick: the picker walks slot -> jack this many times.")]
        public int minCases = 1, maxCases = 3;
        public float caseSeconds = 1.5f;

        [Header("Work")]
        public float stageSeconds = 3f;
        public float liftSeconds = 6f;
        [Tooltip("Jacks drive each aisle one way only (aisle 1 west->east, aisle 2 east->west), entering and leaving by the ends.")]
        public bool jacksFollowAisleDirection = false;
        [Tooltip("Reach trucks turn to face the rack before lifting (as the real ones do).")]
        public bool reachFacesRack = true;
        [Tooltip("A machine stuck this long (game seconds) gives the target up and moves on to its next one.")]
        public float giveUpStuckSeconds = 30f;
        public int seed = 1;
        [Range(0f, 8f)] public float timeScale = 1f;

        [Header("Trucks")]
        public bool trucksOn = true;
        [Tooltip("A truck backs into a free door every this many REAL seconds (random between the two).")]
        public float truckEveryMin = 30f, truckEveryMax = 60f;
        [Tooltip("How long a truck stays at the door (game seconds), random between the two.")]
        public float truckDwellMin = 90f, truckDwellMax = 180f;
        public float truckSpeed = 3f;

        [Header("Materials (Build Graybox makes them if empty)")]
        public Material floorMat, rackMat, wallMat, laneMat, jackMat, reachMat;
        public Material personMat, skinMat, trailerMat, cabMat, caseMat, caseBaseMat, steelMat;

        [Header("Points (local space, written by Build Graybox; edit freely)")]
        public Vector3[] jackPicks;
        public int[] jackPickAisle;
        public Vector3[] reachPicks;
        public int[] reachPickAisle;
        public Vector3[] laneExits;
        public Vector3[] jackParking;
        public Vector3[] reachStarts;
        [Tooltip("Per aisle: x = west end, y = east end (local x, outside the racks), z = aisle centre line (local z).")]
        public Vector3[] aisles;

        [Header("HUD")]
        public bool showStats = true;
        public bool showSettings = true;

        public readonly List<NavTestMachine> Machines = new List<NavTestMachine>();
        public System.Random Rng { get; private set; }
        public float SimSeconds { get; private set; }
        public int MachineAgentType { get; private set; }
        public int HumanAgentType { get; private set; }
        public int TrucksArrived { get; private set; }
        private NavMeshSurface _machineSurface, _humanSurface;
        private Transform _machineRoot, _truckRoot;
        private readonly NavTestTruck[] _doorTruck = new NavTestTruck[3];
        private float _nextTruckReal;

        // ════════════════════════════════════════════════════════════════════════════════════
        // Build
        // ════════════════════════════════════════════════════════════════════════════════════

        [ContextMenu("Build Graybox")]
        public void BuildGraybox()
        {
            var old = transform.Find("Layout");
            if (old != null) DestroyImmediate(old.gameObject);
            var layout = new GameObject("Layout").transform;
            layout.SetParent(transform, false);

            EnsureMaterials();

            // Floor: the foundation slab (the Ground plane outside it is NOT baked, so nothing drives off the building).
            float x0 = Edge(14), x1 = Edge(60), z0 = Edge(77), z1 = Edge(95);
            Box("Floor", layout, new Vector3((x0 + x1) * 0.5f, -0.03f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.1f, z1 - z0), floorMat, true);

            // Racks: one cube per bay per row (the back-to-back rows 83/84 touch).
            var racks = new GameObject("Racks").transform; racks.SetParent(layout, false);
            foreach (int row in RackRows)
                for (int c = FirstBayCell; c <= LastBayCell; c += 2)
                    Box($"Rack y{row} x{c}", racks, new Vector3(Edge(c), RackHeight * 0.5f, Mid(row)), new Vector3(2f * Cell - 0.06f, RackHeight, Cell - 0.04f), rackMat, true);

            // Dock wall with the three door gaps.
            var walls = new GameObject("Walls").transform; walls.SetParent(layout, false);
            foreach (int c in WallCells)
                Box($"Wall x{c}", walls, new Vector3(Mid(c), 1.5f, Edge(77) + 0.15f), new Vector3(Cell, 3f, 0.3f), wallMat, true);

            // Lanes: floor paint only (walkable, no collider).
            var lanes = new GameObject("Lanes").transform; lanes.SetParent(layout, false);
            float lz0 = Edge(81), lz1 = Edge(86);
            foreach (int c in LaneColumns)
                Box($"Lane x{c}", lanes, new Vector3(Mid(c), 0.025f, (lz0 + lz1) * 0.5f), new Vector3(Cell - 0.12f, 0.01f, lz1 - lz0), laneMat, false);

            // Aisles: centre lines and the ends just outside the rack block.
            float rackW = RackMinX - 1.5f, rackE = RackMaxX + 1.5f;
            float a1Lo = Edge(79), a1Hi = Edge(83), a2Lo = Edge(85), a2Hi = Edge(89);
            aisles = new[]
            {
                new Vector3(rackW, rackE, (a1Lo + a1Hi) * 0.5f),
                new Vector3(rackW, rackE, (a2Lo + a2Hi) * 0.5f),
            };

            // Jack pick spots: both rack faces of each aisle, 0.9 m off the face (jack centre), one per bay.
            var jp = new List<Vector3>(); var ja = new List<int>();
            var rp = new List<Vector3>(); var ra = new List<int>();
            float[,] faces = { { a1Lo, a1Hi }, { a2Lo, a2Hi } };
            for (int a = 0; a < 2; a++)
                for (int c = FirstBayCell; c <= LastBayCell; c += 2)
                {
                    float x = Edge(c);
                    jp.Add(new Vector3(x, 0f, faces[a, 0] + 0.9f)); ja.Add(a);
                    jp.Add(new Vector3(x, 0f, faces[a, 1] - 0.9f)); ja.Add(a);
                    rp.Add(new Vector3(x, 0f, aisles[a].z)); ra.Add(a);
                }
            jackPicks = jp.ToArray(); jackPickAisle = ja.ToArray();
            reachPicks = rp.ToArray(); reachPickAisle = ra.ToArray();

            // Lane exit ends (north), where jacks stage and reach trucks pull putaways from.
            laneExits = LaneColumns.Select(c => new Vector3(Mid(c), 0f, lz1 + 1.3f)).ToArray();

            // Starting spots from the save: jack parking bays and the two reach trucks.
            jackParking = new[] { new Vector3(-8.66f, 0f, 57.59f), new Vector3(-2.04f, 0f, 57.59f), new Vector3(4.59f, 0f, 57.59f) };
            reachStarts = new[] { new Vector3(-7.34f, 0f, 53.61f), new Vector3(-2.04f, 0f, 53.61f) };

            FrameCamera();
            Debug.Log($"[NavTest] Graybox built: {RackRows.Length * 8} rack bays, {LaneColumns.Length} lanes, {jackPicks.Length} jack picks, {reachPicks.Length} reach picks.");
        }

        public void EnsureMaterials()
        {
            floorMat   = floorMat   ? floorMat   : MakeMat("NavTest Floor",   new Color(0.78f, 0.78f, 0.76f));
            rackMat    = rackMat    ? rackMat    : MakeMat("NavTest Rack",    new Color(0.45f, 0.47f, 0.50f));
            wallMat    = wallMat    ? wallMat    : MakeMat("NavTest Wall",    new Color(0.30f, 0.30f, 0.32f));
            laneMat    = laneMat    ? laneMat    : MakeMat("NavTest Lane",    new Color(0.62f, 0.74f, 0.90f));
            jackMat    = jackMat    ? jackMat    : MakeMat("NavTest Jack",    new Color(1.00f, 0.87f, 0.78f));   // very faded light peach
            reachMat   = reachMat   ? reachMat   : MakeMat("NavTest Reach",   new Color(0.86f, 0.98f, 0.76f));   // very faded light lime
            personMat  = personMat  ? personMat  : MakeMat("NavTest Person",  new Color(1.00f, 0.55f, 0.15f));   // hi-vis orange
            skinMat    = skinMat    ? skinMat    : MakeMat("NavTest Skin",    new Color(0.85f, 0.68f, 0.55f));
            trailerMat = trailerMat ? trailerMat : MakeMat("NavTest Trailer", new Color(0.92f, 0.92f, 0.94f));
            cabMat     = cabMat     ? cabMat     : MakeMat("NavTest Cab",     new Color(0.80f, 0.35f, 0.32f));
            caseMat     = caseMat     ? caseMat     : MakeMat("NavTest Case",      new Color(0.78f, 0.62f, 0.40f));   // cardboard
            caseBaseMat = caseBaseMat ? caseBaseMat : MakeMat("NavTest Case Base", new Color(0.15f, 0.30f, 0.80f));   // blue "pallet" strip
            steelMat    = steelMat    ? steelMat    : MakeMat("NavTest Steel",     new Color(0.32f, 0.33f, 0.36f));
        }

        // ── Machine looks (graybox, but shaped like the real prefabs: flat jack forks, a reach truck with a mast, carriage and forks) ──

        /// <summary>Jack: two flat forks out the front, a low power head, a standing platform at the back and a tiller bar.</summary>
        public void BuildJackVisual(Transform root, Vector3 size)
        {
            float h = size.z * 0.5f;
            Box("ForkL", root, new Vector3(-0.28f, 0.085f, h - 0.65f), new Vector3(0.18f, 0.07f, 1.3f), steelMat, false);
            Box("ForkR", root, new Vector3( 0.28f, 0.085f, h - 0.65f), new Vector3(0.18f, 0.07f, 1.3f), steelMat, false);
            Box("PowerHead", root, new Vector3(0f, 0.32f, h - 1.3f), new Vector3(size.x * 0.62f, 0.5f, 0.5f), jackMat, false);
            Box("Platform", root, new Vector3(0f, 0.09f, -h + 0.35f), new Vector3(size.x * 0.5f, 0.06f, 0.45f), steelMat, false);
            Box("Tiller", root, new Vector3(0f, 0.8f, -h + 0.6f), new Vector3(size.x * 0.5f, 0.06f, 0.06f), wallMat, false);
        }

        /// <summary>Reach truck: low chassis, two mast uprights, and a carriage with two forks that slides up the mast.
        /// Returns the carriage (move its local y) and the load (cases on blue bases, off until a pallet is on the forks).</summary>
        public void BuildReachVisual(Transform root, Vector3 size, out Transform carriage, out GameObject load)
        {
            float h = size.z * 0.5f;
            Box("Chassis", root, new Vector3(0f, 0.4f, 0f), new Vector3(size.x, 0.7f, size.z), reachMat, false);
            float mastZ = h - 0.1f, mastH = 2.6f;
            Box("MastL", root, new Vector3(-0.34f, mastH * 0.5f + 0.05f, mastZ), new Vector3(0.08f, mastH, 0.1f), steelMat, false);
            Box("MastR", root, new Vector3( 0.34f, mastH * 0.5f + 0.05f, mastZ), new Vector3(0.08f, mastH, 0.1f), steelMat, false);
            Box("Guard", root, new Vector3(0f, 1.55f, -h + 0.2f), new Vector3(size.x * 0.9f, 0.05f, 0.5f), wallMat, false);

            var car = new GameObject("Carriage").transform;
            car.SetParent(root, false);
            car.localPosition = new Vector3(0f, ForkEmptyY, h + 0.02f);
            Box("Backplate", car, new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.55f, 0.05f), steelMat, false);
            Box("ForkL", car, new Vector3(-0.27f, 0.03f, 0.56f), new Vector3(0.12f, 0.06f, 1.1f), steelMat, false);
            Box("ForkR", car, new Vector3( 0.27f, 0.03f, 0.56f), new Vector3(0.12f, 0.06f, 1.1f), steelMat, false);

            // Load: 2 x 2 cases per layer, 2 layers, each case sitting on its own blue base strip.
            var l = new GameObject("Load").transform;
            l.SetParent(car, false);
            l.localPosition = new Vector3(0f, 0.06f, 0.56f);
            for (int layer = 0; layer < 2; layer++)
                for (int cx = 0; cx < 2; cx++)
                    for (int cz = 0; cz < 2; cz++)
                    {
                        var c = new Vector3((cx - 0.5f) * 0.4f, layer * 0.36f, (cz - 0.5f) * 0.5f);
                        Box("CaseBase", l, c + new Vector3(0f, 0.03f, 0f), new Vector3(0.38f, 0.06f, 0.48f), caseBaseMat, false);
                        Box("Case", l, c + new Vector3(0f, 0.21f, 0f), new Vector3(0.38f, 0.30f, 0.48f), caseMat, false);
                    }
            l.gameObject.SetActive(false);
            carriage = car; load = l.gameObject;
        }

        public const float ForkEmptyY = 0.05f, ForkTravelY = 0.3f, ForkLiftY = 1.7f;

        public static Material MakeMat(string name, Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { name = name, color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            return m;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localCentre, Vector3 size, Material mat, bool solid)
            => Prim(PrimitiveType.Cube, name, parent, localCentre, size, mat, solid);

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localCentre, Vector3 scale, Material mat, bool solid)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!solid) DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private void FrameCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 centre = transform.TransformPoint(new Vector3((Edge(14) + Edge(60)) * 0.5f, 0f, (Edge(77) + Edge(95)) * 0.5f));
            cam.transform.position = centre + new Vector3(0f, 40f, -40f);
            cam.transform.LookAt(centre);
        }

        // ════════════════════════════════════════════════════════════════════════════════════
        // Run
        // ════════════════════════════════════════════════════════════════════════════════════

        public static int AgentTypeId(string name)
        {
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (NavMesh.GetSettingsNameFromID(id) == name) return id;
            }
            return 0;
        }

        private void Start()
        {
            if (jackPicks == null || jackPicks.Length == 0 || transform.Find("Layout") == null) BuildGraybox();
            EnsureMaterials();
            Time.timeScale = timeScale;

            MachineAgentType = AgentTypeId("MHE");
            HumanAgentType = AgentTypeId("Human");
            foreach (var s in GetComponents<NavMeshSurface>()) Destroy(s);
            _machineSurface = Bake(MachineAgentType);
            _humanSurface = Bake(HumanAgentType);
            Debug.Log($"[NavTest] NavMeshes baked: MHE ({MachineAgentType}) and Human ({HumanAgentType}).");

            _truckRoot = new GameObject("NavTest Trucks").transform;
            _nextTruckReal = Time.unscaledTime + 5f;   // the first truck shows up soon so you can see it
            SpawnMachines();
        }

        private NavMeshSurface Bake(int agentType)
        {
            var s = gameObject.AddComponent<NavMeshSurface>();
            s.agentTypeID = agentType;
            s.collectObjects = CollectObjects.Children;
            s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            s.BuildNavMesh();
            return s;
        }

        /// <summary>(Re)spawns every machine from the current settings and resets the stats.</summary>
        public void SpawnMachines()
        {
            if (_machineRoot != null) Destroy(_machineRoot.gameObject);
            Machines.Clear();
            Rng = new System.Random(seed);
            SimSeconds = 0f;
            _machineRoot = new GameObject("NavTest Machines").transform;   // NOT a child: machines must never be baked into the NavMesh
            for (int i = 0; i < jackCount; i++)
                Spawn(NavTestMachine.Role.Jack, i, jackParking[i % jackParking.Length] + Vector3.right * (i / jackParking.Length) * 2.5f);
            for (int i = 0; i < reachTruckCount; i++)
                Spawn(NavTestMachine.Role.ReachTruck, i, reachStarts[i % reachStarts.Length] + Vector3.right * (i / reachStarts.Length) * 2.5f);
        }

        private void Spawn(NavTestMachine.Role role, int index, Vector3 localPos)
        {
            bool jack = role == NavTestMachine.Role.Jack;
            Vector3 size = jack ? jackSize : reachSize;
            Vector3 world = transform.TransformPoint(localPos);
            if (NavMesh.SamplePosition(world, out var hit, 3f, new NavMeshQueryFilter { agentTypeID = MachineAgentType, areaMask = NavMesh.AllAreas }))
                world = hit.position;

            var go = new GameObject(jack ? $"Jack {index + 1}" : $"Reach {index + 1}");
            go.SetActive(false);
            go.transform.SetParent(_machineRoot, false);
            go.transform.position = world;
            go.transform.rotation = transform.rotation * Quaternion.Euler(0f, jack ? -90f : 0f, 0f);

            var body = Box("Body", go.transform, new Vector3(0f, size.y * 0.5f + 0.05f, 0f), size, jack ? jackMat : reachMat, true);
            // The Body box stays as the invisible collider (the overlap tally measures it); the visible machine is built from parts.
            body.GetComponent<Renderer>().enabled = false;
            Transform carriage = null; GameObject load = null;
            if (jack) BuildJackVisual(go.transform, size);
            else BuildReachVisual(go.transform, size, out carriage, out load);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.agentTypeID = MachineAgentType;
            agent.radius = jack ? jackRadius : reachRadius;
            agent.height = size.y + 0.1f;
            agent.speed = jack ? jackSpeed : reachSpeed;
            agent.acceleration = 3f;
            agent.angularSpeed = 120f;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;
            agent.avoidancePriority = jack ? jackPriority : reachPriority;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            var m = go.AddComponent<NavTestMachine>();
            m.Init(this, role, agent, body.GetComponent<BoxCollider>(), index);
            m.SetReachParts(carriage, load);
            Machines.Add(m);
            go.SetActive(true);
        }

        /// <summary>A little person: hi-vis capsule body and a head. No collider (the overlap tally is for the machines).</summary>
        public GameObject MakePerson(string name)
        {
            var root = new GameObject(name);
            Prim(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.38f, 0.55f, 0.38f), personMat, false);
            Prim(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.3f, 0f), new Vector3(0.26f, 0.26f, 0.26f), skinMat, false);
            return root;
        }

        private void Update()
        {
            if (Mathf.Abs(Time.timeScale - timeScale) > 0.001f) Time.timeScale = timeScale;
            SimSeconds += Time.deltaTime;
            TickTrucks();
        }

        public Vector3 W(Vector3 local) => transform.TransformPoint(local);
        public Vector3 WDir(Vector3 localDir) => transform.TransformDirection(localDir);
        public Vector3 L(Vector3 world) => transform.InverseTransformPoint(world);

        // ════════════════════════════════════════════════════════════════════════════════════
        // Trucks: one backs into a free door every truckEveryMin-Max REAL seconds
        // ════════════════════════════════════════════════════════════════════════════════════

        private void TickTrucks()
        {
            for (int d = 0; d < _doorTruck.Length; d++) if (_doorTruck[d] == null) _doorTruck[d] = null;   // destroyed = free
            if (!trucksOn || Time.timeScale <= 0f || Time.unscaledTime < _nextTruckReal) return;
            _nextTruckReal = Time.unscaledTime + Random.Range(truckEveryMin, Mathf.Max(truckEveryMin, truckEveryMax));
            var free = Enumerable.Range(0, _doorTruck.Length).Where(d => _doorTruck[d] == null).ToList();
            if (free.Count == 0) { Debug.Log("[NavTest] Truck due, but all three doors are taken."); return; }
            int door = free[Random.Range(0, free.Count)];
            var go = new GameObject($"Truck door {door + 1}");
            go.transform.SetParent(_truckRoot, false);
            var t = go.AddComponent<NavTestTruck>();
            t.Init(this, door);
            _doorTruck[door] = t;
            TrucksArrived++;
        }

        // ════════════════════════════════════════════════════════════════════════════════════
        // HUD: stats top left, settings on the right
        // ════════════════════════════════════════════════════════════════════════════════════

        private GUIStyle _mono, _small;
        private Vector2 _settingsScroll;

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            if (_mono == null)
            {
                _mono = new GUIStyle(GUI.skin.label) { font = Font.CreateDynamicFontFromOSFont("Consolas", 13), fontSize = 13, richText = true };
                _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            }
            if (showStats) DrawStats();
            DrawSettings();
        }

        private void DrawStats()
        {
            var sb = new System.Text.StringBuilder();
            int t = Mathf.FloorToInt(SimSeconds);
            sb.AppendLine($"<b>NAV TEST</b>  {t / 60:00}:{t % 60:00} game time   speed {timeScale:0.##}x   trucks {TrucksArrived}");
            sb.AppendLine("machine   state            trips picks walks walk m  stuck s  eps  gave up  longest  m-overlap s  rack-clip s");
            float stuck = 0, mo = 0, rc = 0, wm = 0; int gu = 0, tr = 0;
            foreach (var m in Machines)
            {
                if (m == null) continue;
                sb.AppendLine($"{m.name,-9} {Trim(m.State, 16),-16} {m.Trips,5} {m.Picks,5} {m.Walks,5} {m.WalkMeters,6:0} {m.StuckSeconds,8:0} {m.StuckEpisodes,4} {m.GiveUps,8} {m.LongestStuck,8:0} {m.MachineOverlapSeconds,12:0.0} {m.RackClipSeconds,12:0.0}");
                stuck += m.StuckSeconds; mo += m.MachineOverlapSeconds; rc += m.RackClipSeconds; gu += m.GiveUps; tr += m.Trips; wm += m.WalkMeters;
            }
            sb.AppendLine($"TOTAL     trips {tr}   walked {wm:0} m   stuck {stuck:0} s   gave up {gu}   machines overlapping {mo:0.0} s   clipping racks/walls {rc:0.0} s");
            float h = 34 + 17 * (Machines.Count + 3);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(8, 8, 960, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(16, 12, 950, h), sb.ToString(), _mono);
        }

        private void DrawSettings()
        {
            const float w = 300f;
            var area = new Rect(Screen.width - w - 8, 8, w, showSettings ? Mathf.Min(Screen.height - 16, 760) : 34);
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 6, area.width - 16, area.height - 12));
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>SETTINGS</b>", _mono);
            if (GUILayout.Button(showSettings ? "hide" : "show", GUILayout.Width(50))) showSettings = !showSettings;
            if (GUILayout.Button(showStats ? "stats off" : "stats on", GUILayout.Width(70))) showStats = !showStats;
            GUILayout.EndHorizontal();
            if (showSettings)
            {
                _settingsScroll = GUILayout.BeginScrollView(_settingsScroll);

                Section("Game speed");
                GUILayout.BeginHorizontal();
                foreach (float s in new[] { 0f, 1f, 2f, 4f, 8f })
                    if (GUILayout.Button(s == 0f ? "||" : $"{s:0}x")) timeScale = s;
                GUILayout.EndHorizontal();
                timeScale = Slider("Speed", timeScale, 0f, 8f, "0.0x");

                Section("Equipment");
                jackSpeed = Slider("Jack speed m/s", jackSpeed, 0.3f, 5f);
                reachSpeed = Slider("Reach speed m/s", reachSpeed, 0.3f, 5f);
                jackRadius = Slider("Jack avoid radius", jackRadius, 0.2f, 1.5f);
                reachRadius = Slider("Reach avoid radius", reachRadius, 0.2f, 1.5f);
                jacksFollowAisleDirection = GUILayout.Toggle(jacksFollowAisleDirection, " Jacks one-way in aisles");
                reachFacesRack = GUILayout.Toggle(reachFacesRack, " Reach trucks turn to face rack");
                giveUpStuckSeconds = Slider("Give up after stuck s", giveUpStuckSeconds, 5f, 120f, "0");

                Section("Pickers");
                walkSpeed = Slider("Walk speed m/s", walkSpeed, 0.3f, 3f);
                stopShortChance = Slider("Chance jack stops short", stopShortChance, 0f, 1f, "0%");
                maxWalk = Slider("Max walk m", maxWalk, minWalk, 12f);
                caseSeconds = Slider("Seconds per case", caseSeconds, 0.2f, 6f);
                maxCases = Mathf.RoundToInt(Slider("Max cases per slot", maxCases, minCases, 8f, "0"));

                Section("Work times (game s)");
                stageSeconds = Slider("Stage drop", stageSeconds, 0f, 20f);
                liftSeconds = Slider("Reach lift", liftSeconds, 0f, 30f);

                Section("Trucks");
                trucksOn = GUILayout.Toggle(trucksOn, " Trucks arrive");
                truckEveryMin = Slider("Every min (real s)", truckEveryMin, 5f, 120f, "0");
                truckEveryMax = Slider("Every max (real s)", Mathf.Max(truckEveryMax, truckEveryMin), truckEveryMin, 180f, "0");
                truckSpeed = Slider("Truck speed m/s", truckSpeed, 0.5f, 8f);

                Section("Crew (applies on Respawn)");
                jackCount = Mathf.RoundToInt(Slider("Pallet jacks", jackCount, 0f, 8f, "0"));
                reachTruckCount = Mathf.RoundToInt(Slider("Reach trucks", reachTruckCount, 0f, 6f, "0"));
                GUILayout.Space(4);
                if (GUILayout.Button("Respawn machines + reset stats")) SpawnMachines();

                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }

        private void Section(string title)
        {
            GUILayout.Space(6);
            GUILayout.Label($"<b>{title}</b>", _mono);
        }

        private float Slider(string label, float v, float min, float max, string fmt = "0.00")
        {
            GUILayout.BeginHorizontal();
            string shown = fmt == "0%" ? $"{v * 100f:0}%" : fmt == "0.0x" ? $"{v:0.0}x" : v.ToString(fmt);
            GUILayout.Label($"{label}: {shown}", _small, GUILayout.Width(150));
            v = GUILayout.HorizontalSlider(v, min, max, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            return v;
        }

        private static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n));

        private void OnDrawGizmos()
        {
            void Dots(Vector3[] pts, Color c, float r)
            {
                if (pts == null) return;
                Gizmos.color = c;
                foreach (var p in pts) Gizmos.DrawSphere(transform.TransformPoint(p + Vector3.up * 0.1f), r);
            }
            Dots(jackPicks, new Color(1f, 0.6f, 0.4f), 0.25f);
            Dots(reachPicks, new Color(0.5f, 0.9f, 0.3f), 0.3f);
            Dots(laneExits, Color.cyan, 0.3f);
            Dots(jackParking, Color.white, 0.4f);
            Dots(reachStarts, Color.green, 0.4f);
        }
    }
}
