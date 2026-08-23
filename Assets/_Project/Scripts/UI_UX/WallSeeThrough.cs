using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cuts a see-through hole in the warehouse shell when the camera is outside looking in.
///
/// The hole is made of whole wall SEGMENTS rather than a screen-space circle, and that is a
/// deliberate choice, not a shortcut. Walls here are modular one-cell pieces placed through the build
/// FSM, so the segments between the camera and what the player is looking at already form a
/// grid-aligned opening — there is nothing to cut. Doing it in screen space would mean swapping every
/// occluding wall onto a custom clip shader, and every wall in this project renders through URP Lit
/// against a Forward+ light loop and the cavity post pass; a hand-written stand-in would not match its
/// neighbours, and the mismatch would be most visible on exactly the walls being cut. Reusing the
/// existing see-through material means the hole is lit like everything else because it IS everything
/// else.
///
/// GATING — only fires when the camera is genuinely outside looking in:
///   • the camera's own cell has no foundation under it (it is over the yard), AND
///   • the focal point's cell does (the player is looking at something indoors).
/// Panning around the yard, or flying inside the building, both leave walls alone. Without the second
/// test the shell would dissolve every time the camera swung wide past a corner while still looking
/// outdoors.
///
/// Two per-wall vetoes then keep individual walls solid even inside that corridor — when the camera is
/// far enough ABOVE a wall to see over it anyway, or far enough AWAY from it that the opening reads as
/// missing geometry rather than a way in. See StaysSolid.
///
/// Lives on the camera in the scene so its tuning values actually serialize. FreeLookCamera.Awake adds
/// one at runtime if it is ever missing, but that fallback instance necessarily runs on code defaults —
/// a component created by AddComponent has nothing to deserialize from. If the numbers below stop
/// responding to the Inspector, the component is being created at runtime rather than found.
/// </summary>
[RequireComponent(typeof(FreeLookCamera))]
public class WallSeeThrough : MonoBehaviour
{
    // Every wall prefab in the project sits on this layer (verified across all 11), which is what
    // makes the occlusion test one cheap layer-masked cast instead of a component filter over
    // everything the ray touches.
    private const string WallLayerName = "Walls";

    [Tooltip("Radius of the sight corridor swept between the camera and the focal point. Wider than a " +
             "hairline ray so the opening is big enough to actually see through, rather than a pinhole " +
             "that flickers as the camera drifts.")]
    [SerializeField] private float sightRadius = 4f;

    [Tooltip("How often the occlusion sweep runs, in seconds. The result only changes when the camera " +
             "moves, and a wall a frame late is invisible to the player.")]
    [SerializeField] private float scanInterval = 0.08f;

    [Tooltip("Stop the sweep short of the focal point so the wall the player is looking AT from inside " +
             "isn't dissolved along with the ones in front of it.")]
    [SerializeField] private float stopShortOfFocus = 3f;

    [Header("Leave the wall solid when...")]

    [Tooltip("The camera is at least this far ABOVE the top of that particular wall. Looking down from " +
             "up here you already clear it, so dissolving it only removes detail you can see anyway. " +
             "Measured per wall against its own height, so a tall shipping door and a short wall drop " +
             "out at different camera altitudes. Raise it very high to disable this rule.")]
    [SerializeField] private float solidWhenCameraAboveTop = 10f;

    [Tooltip("The camera is at least this far from that particular wall on EITHER ground axis — X or Z, " +
             "measured separately, not as a diagonal. Height is excluded on purpose: it is already the " +
             "rule above, and folding it in here would make a high camera read as 'far away' and double- " +
             "count the same situation. At long range the opening stops reading as a way in and just " +
             "looks like the building is missing pieces. Measured to the nearest point on the wall, not " +
             "its centre, so long wall runs behave the same along their whole length. Raise it very high " +
             "to disable this rule.")]
    [SerializeField] private float solidWhenCameraFartherThan = 20f;

    private FreeLookCamera _camera;
    private PlacementGrid _grid;
    private Material _ghostMaterial;
    private int _wallMask;
    private float _nextScanTime;

    // Renderers currently swapped to the ghost material, holding their real materials so they can be
    // put back exactly. Keyed by renderer because a single wall piece (a shipping door has 19) can
    // carry several, each with its own submesh slots.
    private readonly Dictionary<Renderer, Material[]> _ghosted = new Dictionary<Renderer, Material[]>();

    // Scratch, reused every scan — this runs several times a second and allocating a fresh set each
    // time is exactly the kind of steady garbage that shows up as a hitch later.
    private readonly HashSet<Renderer> _shouldGhost = new HashSet<Renderer>();
    private readonly List<Renderer> _toRestore = new List<Renderer>();
    private RaycastHit[] _hits = new RaycastHit[64];

    private void Awake()
    {
        _camera = GetComponent<FreeLookCamera>();

        int layer = LayerMask.NameToLayer(WallLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[WallSeeThrough] No '{WallLayerName}' layer in this project — see-through " +
                             "is disabled. Nothing else breaks; walls simply stay solid.", this);
            enabled = false;
            return;
        }
        _wallMask = 1 << layer;

        // Same material the trailer uses while docked, so "you can see through this" reads the same way
        // everywhere in the game rather than inventing a second visual language for the same idea.
        _ghostMaterial = Resources.Load<Material>("Materials/GhostLoweredWall");
        if (_ghostMaterial == null)
        {
            Debug.LogWarning("[WallSeeThrough] Resources/Materials/GhostLoweredWall not found — " +
                             "see-through is disabled.", this);
            enabled = false;
        }
    }

    private void OnDisable() => RestoreAll();

    private void LateUpdate()
    {
        // LateUpdate, not Update: FreeLookCamera writes its transform in Update, so scanning any
        // earlier would test the previous frame's position and lag the opening behind the camera.
        if (Time.unscaledTime < _nextScanTime) return;
        _nextScanTime = Time.unscaledTime + scanInterval;

        if (!ShouldSeeThrough())
        {
            RestoreAll();
            return;
        }

        _shouldGhost.Clear();
        CollectOccluders(_shouldGhost);
        Apply(_shouldGhost);
    }

    /// <summary>
    /// True only when the camera is outside the building and looking at something inside it.
    ///
    /// "Inside" is defined as having a foundation in the cell, because that is what the warehouse is
    /// actually built on — testing against walls instead would fail in a doorway, and testing a
    /// bounding box would be wrong the moment the player builds a second structure.
    /// </summary>
    private bool ShouldSeeThrough()
    {
        var grid = Grid();
        if (grid == null) return false;

        bool cameraIndoors = HasFoundation(grid, transform.position);
        if (cameraIndoors) return false;                       // already inside — nothing to see past

        return HasFoundation(grid, _camera.FocalPoint);        // looking at something indoors
    }

    private bool HasFoundation(PlacementGrid grid, Vector3 worldPos)
    {
        var cell = grid.WorldToCell(worldPos);
        if (!grid.IsInsideGrid(cell)) return false;

        var contents = grid.GetObjectsInCell(cell);
        if (contents == null) return false;

        for (int i = 0; i < contents.Count; i++)
        {
            var data = contents[i].data;
            if (data == null) continue;
            // Foundations are authored under the "Grounds" category — the slab the building stands on.
            if (data.category == "Grounds") return true;
        }
        return false;
    }

    /// <summary>
    /// Sweeps a fat ray from the camera to just short of the focal point and collects every wall
    /// renderer it passes through.
    ///
    /// SphereCast rather than Raycast so the opening has real width, and NonAlloc + Collide because
    /// most wall colliders in this project are triggers (only ManDoor has a solid one) — the default
    /// QueryTriggerInteraction would find almost nothing and the whole feature would silently do
    /// nothing on 10 of the 11 wall types.
    /// </summary>
    private void CollectOccluders(HashSet<Renderer> into)
    {
        Vector3 origin = transform.position;
        Vector3 toFocus = _camera.FocalPoint - origin;
        float distance = toFocus.magnitude - stopShortOfFocus;
        if (distance <= 0.01f) return;

        Vector3 dir = toFocus / toFocus.magnitude;

        int count = Physics.SphereCastNonAlloc(origin, sightRadius, dir, _hits, distance,
                                               _wallMask, QueryTriggerInteraction.Collide);

        // NonAlloc silently truncates at the buffer length rather than reporting overflow, which would
        // read as "some walls just don't dissolve". Grow once and re-sweep instead.
        if (count == _hits.Length && _hits.Length < 512)
        {
            _hits = new RaycastHit[_hits.Length * 2];
            count = Physics.SphereCastNonAlloc(origin, sightRadius, dir, _hits, distance,
                                               _wallMask, QueryTriggerInteraction.Collide);
        }

        for (int i = 0; i < count; i++)
        {
            var col = _hits[i].collider;
            if (col == null) continue;
            if (StaysSolid(col, origin)) continue;

            foreach (var r in col.GetComponentsInChildren<Renderer>(false))
            {
                // Door number labels live on the same objects and are TextMeshPro renderers; pushing a
                // lit wall material onto one turns the number into a solid quad.
                if (r is MeshRenderer && r.GetComponent<TMPro.TextMeshPro>() == null)
                    into.Add(r);
            }
        }
    }

    /// <summary>
    /// Per-wall veto: even though this wall is in the sight corridor, leave it solid.
    ///
    /// Judged against THIS wall rather than globally, because both rules are about the wall's own
    /// geometry. A camera 12m up clears a short wall but is still buried behind a shipping door, and a
    /// single long wall run can be 5m away at one end and 30m at the other — a global test would pop
    /// the whole run in and out together as the camera drifted.
    ///
    /// Distance is measured to the NEAREST POINT on the wall (via its bounds) rather than its centre,
    /// for that same reason: centre distance makes a long wall's behaviour depend on where the modeller
    /// happened to put its origin.
    ///
    /// Distance is also GROUND distance, per axis — X and Z compared separately against the threshold,
    /// never as a diagonal and never including height. Two reasons. Height is already its own rule, so
    /// including Y here would let an overhead camera trip the distance veto and make the two settings
    /// interfere. And per-axis rather than radial means the corner case behaves: a camera 19m out on
    /// both X and Z is 27m away as the crow flies, which a radial test would veto even though it is
    /// comfortably within 20 of the wall in the sense the player is thinking about.
    /// </summary>
    private bool StaysSolid(Collider wall, Vector3 cameraPos)
    {
        Bounds b = wall.bounds;

        // Well above the top of it — from up here you already see over the wall, so dissolving it just
        // deletes detail without revealing anything.
        if (cameraPos.y - b.max.y >= solidWhenCameraAboveTop) return true;

        // Nearest point on the wall, measured at the wall's own height so its vertical extent can't
        // pull the closest point up or down and skew the ground offsets below.
        Vector3 probe = new Vector3(cameraPos.x, b.center.y, cameraPos.z);
        Vector3 nearest = b.ClosestPoint(probe);

        float offsetX = Mathf.Abs(cameraPos.x - nearest.x);
        float offsetZ = Mathf.Abs(cameraPos.z - nearest.z);
        if (offsetX >= solidWhenCameraFartherThan || offsetZ >= solidWhenCameraFartherThan) return true;

        return false;
    }

    private void Apply(HashSet<Renderer> target)
    {
        // Restore anything that has stepped out of the corridor since the last scan.
        _toRestore.Clear();
        foreach (var kv in _ghosted)
            if (kv.Key == null || !target.Contains(kv.Key)) _toRestore.Add(kv.Key);

        for (int i = 0; i < _toRestore.Count; i++) Restore(_toRestore[i]);

        foreach (var r in target)
        {
            if (r == null || _ghosted.ContainsKey(r)) continue;   // already ghosted — leave it alone

            _ghosted[r] = r.sharedMaterials;

            // Every submesh slot gets the ghost, or a multi-material wall goes half solid.
            var ghosts = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < ghosts.Length; i++) ghosts[i] = _ghostMaterial;
            r.sharedMaterials = ghosts;
        }
    }

    private void Restore(Renderer r)
    {
        if (r != null && _ghosted.TryGetValue(r, out var original))
            r.sharedMaterials = original;
        _ghosted.Remove(r);
    }

    private void RestoreAll()
    {
        if (_ghosted.Count == 0) return;

        foreach (var kv in _ghosted)
            if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;

        _ghosted.Clear();
    }

    /// <summary>
    /// Resolved lazily and re-resolved if it goes away. The grid is a scene object that does not exist
    /// yet when this component wakes on the camera, and caching a null on the first frame would leave
    /// the feature permanently dead with no symptom other than walls never dissolving.
    /// </summary>
    private PlacementGrid Grid()
    {
        if (_grid == null) _grid = Object.FindFirstObjectByType<PlacementGrid>();
        return _grid;
    }
}
