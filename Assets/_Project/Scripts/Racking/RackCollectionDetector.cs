using UnityEngine;
using System.Collections.Generic;
using System;
using GameCore.Events;

/// <summary>
/// Listens for rack placement events and groups racks into RackCollections.
///
/// A collection is a run of racks whose bays are ADJACENT ON TILE CELLS and that
/// RUN THE SAME DIRECTION (same axis). Adjacency and orientation are evaluated in
/// grid-cell space (via RackGridUtil), not world-space dot products.
///
/// Placed racks are kept in orange-transparent "ghost" state (RackGhost) until their
/// aisle is initialized — they're planning placeholders, not yet real racks.
///
/// Also cleans up after deletes: when a rack is removed it leaves its collection, and
/// an emptied collection is destroyed (its chevrons are children, so they go with it).
/// </summary>
public class RackCollectionDetector : MonoBehaviour
{
    private readonly List<RackCollection> _activeCollections = new();
    private PlacementGrid _grid;
    private AisleInitializer _aisleInitializer;
    private Material _ghostMaterial;
    private EventManager _eventManager;
    private bool _subscribedToDeletes;

    public event Action<RackCollection> OnCollectionCreated;
    public event Action<RackCollection> OnCollectionAdded;
    // survivor, absorbed — fired when 'absorbed' is merged into 'survivor'
    public event Action<RackCollection, RackCollection> OnCollectionMerged;
    // Fired right before an emptied collection is destroyed, so trackers can drop it.
    public event Action<RackCollection> OnCollectionRemoved;

    public void SetGrid(PlacementGrid grid) => _grid = grid;

    private void OnEnable()
    {
        RackPlacedEvent.OnRackPlaced += HandleRackPlaced;
    }

    private void OnDisable()
    {
        RackPlacedEvent.OnRackPlaced -= HandleRackPlaced;
    }

    private void Start()
    {
        SubscribeToDeletes();
    }

    private void OnDestroy()
    {
        if (_subscribedToDeletes && _eventManager != null)
            _eventManager.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, HandleRackDeleted);
    }

    private void SubscribeToDeletes()
    {
        _eventManager = EventManager.Instance;
        if (_eventManager == null) return;
        _eventManager.Subscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, HandleRackDeleted);
        _subscribedToDeletes = true;
    }

    private void HandleRackPlaced(GameObject rackGO)
    {
        if (_grid == null) _grid = FindFirstObjectByType<PlacementGrid>();

        // Placed directly on top of an already-live rack? That's a VERTICAL extension of an
        // existing aisle — not a new one. Commit it in place (no ghost, no collection, no
        // chevrons, no setup UI): it inherits aisle+bay from below and bumps the level.
        if (_aisleInitializer == null) _aisleInitializer = GetComponent<AisleInitializer>();
        if (_aisleInitializer != null && _aisleInitializer.TryCommitStackedRack(rackGO))
            return;

        // Keep the rack as an orange-transparent planning placeholder until its aisle is set up.
        ApplyGhost(rackGO);

        var adjacent = FindAllAdjacentCollections(rackGO);

        if (adjacent.Count == 0)
        {
            var newCollection = CreateNewCollection(rackGO);
            _activeCollections.Add(newCollection);
            OnCollectionCreated?.Invoke(newCollection);
            return;
        }

        // Add the rack to the first adjacent collection, then fold any others into it.
        var primary = adjacent[0];
        primary.AddRack(rackGO);

        for (int i = 1; i < adjacent.Count; i++)
            MergeCollections(primary, adjacent[i]);

        OnCollectionAdded?.Invoke(primary);
    }

    private void HandleRackDeleted(string eventId, PlacedObject deleted)
    {
        if (deleted == null || deleted.data == null) return;
        if (deleted.data.category != "Racking") return;

        var go = deleted.gameObject;
        var collection = FindCollectionByRack(go);
        if (collection == null) return;

        collection.RemoveRack(go);

        if (collection.Racks.Count == 0)
        {
            // Emptied — retire the collection. Chevrons are children of it, so they
            // die with it (this is what stops chevrons orphaning in the scene).
            _activeCollections.Remove(collection);
            OnCollectionRemoved?.Invoke(collection);
            if (collection != null)
                Destroy(collection.gameObject);
        }
        else if (!collection.Initialized)
        {
            // Still has racks and not yet set up — reposition chevrons to the new extent.
            OnCollectionAdded?.Invoke(collection);
        }
    }

    private void ApplyGhost(GameObject rackGO)
    {
        if (_ghostMaterial == null && PreviewController.Instance != null)
            _ghostMaterial = PreviewController.Instance.GhostMaterial;
        if (_ghostMaterial == null) return;

        var ghost = rackGO.GetComponent<RackGhost>();
        if (ghost == null) ghost = rackGO.AddComponent<RackGhost>();
        ghost.ApplyGhost(_ghostMaterial);
    }

    /// <summary>
    /// All uninitialized collections this rack is adjacent to (same axis + a cell
    /// within Manhattan distance 1 of the rack's cells). Usually 0 or 1; more than
    /// one means the rack bridges collections that should be merged.
    /// </summary>
    private List<RackCollection> FindAllAdjacentCollections(GameObject rackGO)
    {
        var result = new List<RackCollection>();
        var newCells = RackGridUtil.GetCells(rackGO, _grid);

        foreach (var collection in _activeCollections)
        {
            if (collection.Initialized) continue;
            if (IsAdjacentToCollection(collection, rackGO, newCells))
                result.Add(collection);
        }

        return result;
    }

    /// <summary>
    /// A rack joins a collection only if it EXTENDS the row along the row's run axis —
    /// NOT if it's placed beside it (a parallel row). Two adjacent parallel rows must stay
    /// separate collections (they become back-to-back aisles). We derive the run axis from
    /// the collection's overall shape, since a single half-bay (1×1) has no direction of its own.
    /// </summary>
    private bool IsAdjacentToCollection(RackCollection collection, GameObject rackGO, List<Vector2Int> newCells)
    {
        var collCells = new List<Vector2Int>();
        foreach (var r in collection.Racks)
            if (r != null) collCells.AddRange(RackGridUtil.GetCells(r, _grid));
        if (collCells.Count == 0 || newCells.Count == 0) return false;

        CellBounds(collCells, out var cMin, out var cMax);
        CellBounds(newCells, out var nMin, out var nMax);

        // Must physically touch the collection (share an edge with one of its cells).
        bool touching = false;
        foreach (var a in collCells)
        {
            foreach (var b in newCells)
                if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1) { touching = true; break; }
            if (touching) break;
        }
        if (!touching) return false;

        int cxExt = cMax.x - cMin.x;
        int cyExt = cMax.y - cMin.y;

        // Point/square collection: no established run direction yet — the first touching rack
        // sets it, so allow the merge.
        if (cxExt == cyExt) return true;

        // Established run axis = the collection's longer span. Only merge a rack whose
        // perpendicular range OVERLAPS the row's line (it continues the same row). A rack
        // placed to the side (parallel row) doesn't overlap on the perpendicular axis.
        bool runAlongY = cyExt > cxExt;
        bool perpOverlap = runAlongY
            ? (nMin.x <= cMax.x && cMin.x <= nMax.x)
            : (nMin.y <= cMax.y && cMin.y <= nMax.y);

        return perpOverlap;
    }

    private static void CellBounds(List<Vector2Int> cells, out Vector2Int min, out Vector2Int max)
    {
        min = new Vector2Int(int.MaxValue, int.MaxValue);
        max = new Vector2Int(int.MinValue, int.MinValue);
        foreach (var c in cells)
        {
            if (c.x < min.x) min.x = c.x;
            if (c.y < min.y) min.y = c.y;
            if (c.x > max.x) max.x = c.x;
            if (c.y > max.y) max.y = c.y;
        }
    }

    private RackCollection CreateNewCollection(GameObject rackGO)
    {
        var collectionGO = new GameObject($"RackCollection_{_activeCollections.Count}");
        collectionGO.transform.parent = transform;

        var collection = collectionGO.AddComponent<RackCollection>();
        collection.AddRack(rackGO);

        return collection;
    }

    private void MergeCollections(RackCollection survivor, RackCollection absorbed)
    {
        if (survivor == absorbed) return;

        foreach (var rack in absorbed.Racks)
            survivor.AddRack(rack);

        _activeCollections.Remove(absorbed);
        OnCollectionMerged?.Invoke(survivor, absorbed);

        if (absorbed != null)
            Destroy(absorbed.gameObject);
    }

    public RackCollection FindCollectionByRack(GameObject rackGO)
    {
        foreach (var collection in _activeCollections)
        {
            if (collection.Contains(rackGO))
                return collection;
        }
        return null;
    }

    public List<RackCollection> GetUninitializedCollections()
    {
        var uninitialized = new List<RackCollection>();
        foreach (var collection in _activeCollections)
        {
            if (!collection.Initialized)
                uninitialized.Add(collection);
        }
        return uninitialized;
    }
}
