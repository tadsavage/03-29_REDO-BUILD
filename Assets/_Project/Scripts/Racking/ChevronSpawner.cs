using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Listens for RackCollections being created and spawns chevrons.
/// - 1 collection = 2 chevrons (left & right)
/// - 2 parallel collections = 3 chevrons (left, middle, right)
/// </summary>
public class ChevronSpawner : MonoBehaviour
{
    [Header("Chevron Configuration")]
    [SerializeField] private Sprite _chevronSprite;
    [SerializeField] private Material _chevronMaterial;
    [SerializeField] private float _chevronHeight = 0.1f;
    [SerializeField] private Vector3 _chevronScale = new(1f, 1f, 1f);

    public void SetSprite(Sprite sprite) => _chevronSprite = sprite;
    public void SetMaterial(Material material) => _chevronMaterial = material;
    public void SetHeight(float height) => _chevronHeight = height;

    private RackCollectionDetector _detector;
    private Dictionary<RackCollection, List<GameObject>> _chevronsByCollection = new();
    private const float PARALLEL_COLLECTION_THRESHOLD = 5f; // Max distance to consider parallel

    private void Start()
    {
        _detector = GetComponent<RackCollectionDetector>();
        if (_detector != null)
        {
            Debug.Log("ChevronSpawner.Start() running");
            _detector.OnCollectionCreated += HandleCollectionCreated;
            _detector.OnCollectionAdded += HandleCollectionAdded;
        }
    }

    private void HandleCollectionCreated(RackCollection collection)
    {
        Debug.Log($"ChevronSpawner.HandleCollectionCreated() - spawning chevrons for collection");
        // Check for parallel collections
        var parallelCollection = FindParallelCollection(collection);

        if (parallelCollection != null && !parallelCollection.Initialized)
        {
            // Create 3 chevrons (left, middle, right)
            SpawnChevronPair(collection, parallelCollection);
        }
        else
        {
            // Create 2 chevrons (left, right)
            SpawnChevronSingle(collection);
        }
    }

    private void HandleCollectionAdded(RackCollection collection)
    {
        // A rack was added to existing collection - update chevron positions
        UpdateChevronPosition(collection);
    }

    private RackCollection FindParallelCollection(RackCollection newCollection)
    {
        var uninitialized = _detector.GetUninitializedCollections();

        foreach (var other in uninitialized)
        {
            if (other == newCollection) continue;

            // Check if parallel (different X range but similar Z range)
            float zDistance = Mathf.Abs(newCollection.CollectionCenter.z - other.CollectionCenter.z);

            if (zDistance < PARALLEL_COLLECTION_THRESHOLD)
            {
                return other;
            }
        }

        return null;
    }

    private void SpawnChevronSingle(RackCollection collection)
    {
        Debug.Log($"SpawnChevronSingle() - spawning chevrons at first and last bay");

        if (collection.Racks.Count == 0) return;

        // Get first and last rack positions
        Vector3 firstRackPos = collection.Racks[0].transform.position;
        Vector3 lastRackPos = collection.Racks[collection.Racks.Count - 1].transform.position;

        float leftX = collection.CollectionBounds.min.x - 2f;
        float rightX = collection.CollectionBounds.max.x + 2f;

        // Spawn chevrons at first bay (front) and last bay (back)
        var leftFront = SpawnChevron(new Vector3(leftX, _chevronHeight, firstRackPos.z), collection, "Left_Front");
        var rightFront = SpawnChevron(new Vector3(rightX, _chevronHeight, firstRackPos.z), collection, "Right_Front");
        var leftRear = SpawnChevron(new Vector3(leftX, _chevronHeight, lastRackPos.z), collection, "Left_Rear");
        var rightRear = SpawnChevron(new Vector3(rightX, _chevronHeight, lastRackPos.z), collection, "Right_Rear");

        if (!_chevronsByCollection.ContainsKey(collection))
            _chevronsByCollection[collection] = new();

        _chevronsByCollection[collection].Add(leftFront);
        _chevronsByCollection[collection].Add(rightFront);
        _chevronsByCollection[collection].Add(leftRear);
        _chevronsByCollection[collection].Add(rightRear);
    }

    private void SpawnChevronPair(RackCollection collection1, RackCollection collection2)
    {
        // For two parallel collections, spawn at first and last bay of each side
        if (collection1.Racks.Count == 0 || collection2.Racks.Count == 0) return;

        Vector3 firstRack1 = collection1.Racks[0].transform.position;
        Vector3 lastRack1 = collection1.Racks[collection1.Racks.Count - 1].transform.position;
        Vector3 firstRack2 = collection2.Racks[0].transform.position;
        Vector3 lastRack2 = collection2.Racks[collection2.Racks.Count - 1].transform.position;

        float leftX = collection1.CollectionBounds.min.x - 2f;
        float rightX = collection2.CollectionBounds.max.x + 2f;

        // Spawn at first and last bay on each side
        var leftFront = SpawnChevron(new Vector3(leftX, _chevronHeight, firstRack1.z), collection1, "Left_Front");
        var leftRear = SpawnChevron(new Vector3(leftX, _chevronHeight, lastRack1.z), collection1, "Left_Rear");
        var rightFront = SpawnChevron(new Vector3(rightX, _chevronHeight, firstRack2.z), collection2, "Right_Front");
        var rightRear = SpawnChevron(new Vector3(rightX, _chevronHeight, lastRack2.z), collection2, "Right_Rear");

        if (!_chevronsByCollection.ContainsKey(collection1))
            _chevronsByCollection[collection1] = new();
        if (!_chevronsByCollection.ContainsKey(collection2))
            _chevronsByCollection[collection2] = new();

        _chevronsByCollection[collection1].Add(leftFront);
        _chevronsByCollection[collection1].Add(leftRear);
        _chevronsByCollection[collection2].Add(rightFront);
        _chevronsByCollection[collection2].Add(rightRear);
    }

    private GameObject SpawnChevron(Vector3 position, RackCollection collection, string side)
    {
        var chevronGO = new GameObject($"Chevron_{side}_{collection.name}");
        Debug.Log($"SpawnChevron() - created GameObject: {chevronGO.name}");

        // Position at Y=1.15 (on top of ground) instead of _chevronHeight
        Vector3 adjustedPos = new Vector3(position.x, 1.15f, position.z);
        chevronGO.transform.position = adjustedPos;

        // Rotate 90° on X-axis to make it horizontal instead of vertical
        chevronGO.transform.rotation = Quaternion.Euler(90, 0, 0);

        Debug.Log($"SpawnChevron() - ChevronSpawner transform: {transform}, parent: {transform.parent}");
        chevronGO.transform.parent = transform;
        Debug.Log($"SpawnChevron() - chevron parent set to: {chevronGO.transform.parent}, active: {chevronGO.activeSelf}");

        // Add sprite renderer
        var spriteRenderer = chevronGO.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = _chevronSprite;
        spriteRenderer.material = _chevronMaterial;
        Debug.Log($"SpawnChevron() - added SpriteRenderer. Sprite: {_chevronSprite != null}, Material: {_chevronMaterial != null}");

        // Add box collider
        var collider = chevronGO.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 0.1f, 1f);
        collider.isTrigger = false;

        // Add chevron controller
        var controller = chevronGO.AddComponent<ChevronController>();
        controller.Initialize(collection);

        // Add a temporary debug component to track destruction
        var debugComponent = chevronGO.AddComponent<DestroyDebugger>();

        Debug.Log($"SpawnChevron() - created chevron at position {position}");
        return chevronGO;
    }

    private void UpdateChevronPosition(RackCollection collection)
    {
        if (!_chevronsByCollection.TryGetValue(collection, out var chevrons))
            return;

        // Update position to center of collection
        Vector3 center = collection.CollectionCenter;
        foreach (var chevron in chevrons)
        {
            if (chevron != null)
                chevron.transform.position = new Vector3(chevron.transform.position.x, _chevronHeight, center.z);
        }
    }

    public void DeleteChevrons(RackCollection collection)
    {
        if (_chevronsByCollection.TryGetValue(collection, out var chevrons))
        {
            foreach (var chevron in chevrons)
            {
                if (chevron != null)
                    Destroy(chevron);
            }
            _chevronsByCollection.Remove(collection);
        }
    }

    public void DeleteAllChevrons(params RackCollection[] collections)
    {
        foreach (var collection in collections)
        {
            DeleteChevrons(collection);
        }
    }
}
