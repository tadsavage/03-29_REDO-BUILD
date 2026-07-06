using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Represents a collection of adjacent racks placed on the same X-axis.
/// All racks in a collection are initially in preview (orange) mode until
/// the aisle is initialized via RackSetupUI.
/// </summary>
public class RackCollection : MonoBehaviour
{
    [SerializeField] private List<GameObject> _racks = new();
    [SerializeField] private bool _initialized = false;

    private Vector3 _collectionCenter;
    private Bounds _collectionBounds;

    public List<GameObject> Racks => _racks;
    public bool Initialized => _initialized;
    public Vector3 CollectionCenter => _collectionCenter;
    public Bounds CollectionBounds => _collectionBounds;

    public void Initialize()
    {
        _initialized = true;
    }

    public void AddRack(GameObject rack)
    {
        if (!_racks.Contains(rack))
        {
            _racks.Add(rack);
            UpdateBounds();
        }
    }

    public bool Contains(GameObject rack)
    {
        return _racks.Contains(rack);
    }

    /// <summary>Removes a rack (e.g. when the player deletes it). Returns true if it was present.</summary>
    public bool RemoveRack(GameObject rack)
    {
        bool removed = _racks.Remove(rack);
        if (removed)
            UpdateBounds();
        return removed;
    }

    private void UpdateBounds()
    {
        if (_racks.Count == 0) return;

        _collectionBounds = new Bounds(_racks[0].transform.position, Vector3.zero);
        foreach (var rack in _racks)
        {
            _collectionBounds.Encapsulate(rack.transform.position);
        }

        _collectionCenter = _collectionBounds.center;
    }

    public void ApplyPreviewMaterial(Material previewMat)
    {
        foreach (var rack in _racks)
        {
            var renderers = rack.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.material = previewMat;
            }
        }
    }
}
