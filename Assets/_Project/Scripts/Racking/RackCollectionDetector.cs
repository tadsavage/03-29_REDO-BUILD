using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Listens for rack placement events and detects adjacent racks on the X-axis.
/// Groups adjacent racks into RackCollections and fires OnCollectionCreated event.
/// </summary>
public class RackCollectionDetector : MonoBehaviour
{
    private List<RackCollection> _activeCollections = new();
    private const float ADJACENCY_THRESHOLD = 1.5f; // Slightly larger than grid cell size

    public event Action<RackCollection> OnCollectionCreated;
    public event Action<RackCollection> OnCollectionAdded;

    private void OnEnable()
    {
        Debug.Log("RackCollectionDetector.OnEnable() - subscribing to RackPlacedEvent");
        RackPlacedEvent.OnRackPlaced += HandleRackPlaced;
    }

    private void OnDisable()
    {
        Debug.Log("RackCollectionDetector.OnDisable() - unsubscribing from RackPlacedEvent");
        RackPlacedEvent.OnRackPlaced -= HandleRackPlaced;
    }

    private void HandleRackPlaced(GameObject rackGO)
    {
        Debug.Log($"RackCollectionDetector.HandleRackPlaced() - received event for {rackGO.name}");
        // Check if this rack is adjacent to any existing collection on X-axis
        RackCollection existingCollection = FindAdjacentCollection(rackGO);

        if (existingCollection != null)
        {
            Debug.Log($"Adding rack to existing collection");
            // Add to existing collection
            existingCollection.AddRack(rackGO);
            OnCollectionAdded?.Invoke(existingCollection);
        }
        else
        {
            Debug.Log($"Creating new collection for rack {rackGO.name}");
            // Create new collection
            var newCollection = CreateNewCollection(rackGO);
            _activeCollections.Add(newCollection);
            Debug.Log($"Firing OnCollectionCreated event");
            OnCollectionCreated?.Invoke(newCollection);
        }
    }

    private RackCollection FindAdjacentCollection(GameObject rackGO)
    {
        Vector3 rackPos = rackGO.transform.position;

        foreach (var collection in _activeCollections)
        {
            if (collection.Initialized) continue; // Skip initialized collections

            foreach (var existingRack in collection.Racks)
            {
                Vector3 existingPos = existingRack.transform.position;
                Vector3 distanceVector = rackPos - existingPos;

                // Use the existing rack's local X-axis as the adjacency direction
                Vector3 localXAxis = existingRack.transform.right;

                // Project the distance onto the rack's local X-axis
                float projectionDistance = Mathf.Abs(Vector3.Dot(distanceVector, localXAxis));

                // If projection is small, racks are adjacent along their local X-axis
                if (projectionDistance < ADJACENCY_THRESHOLD)
                {
                    return collection;
                }
            }
        }

        return null;
    }

    private RackCollection CreateNewCollection(GameObject rackGO)
    {
        var collectionGO = new GameObject($"RackCollection_{_activeCollections.Count}");
        collectionGO.transform.parent = transform;

        var collection = collectionGO.AddComponent<RackCollection>();
        collection.AddRack(rackGO);

        return collection;
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
