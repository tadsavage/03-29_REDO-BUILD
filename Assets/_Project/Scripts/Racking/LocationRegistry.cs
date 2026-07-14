using System.Collections.Generic;
using System.Linq;
using GameCore.Inventory;
using TMPro;
using UnityEngine;
using GameCore.Events;

/// <summary>
/// Creates and maintains one <see cref="LocationData"/> child GameObject per live rack slot.
///
/// Each child is named exactly after its slot address (e.g. "01-02-A0") so any code can
/// resolve it with <c>rack.transform.Find("01-02-A0")</c> without needing a registry lookup.
/// The registry itself provides a fast <see cref="TryGet"/> path for code that already has
/// the address string.
///
/// Self-bootstraps after scene load (same pattern as <see cref="SlotRegistry"/>).
/// Recomputes whenever racks are placed or deleted, plus a 1 s heartbeat to catch
/// racks restored from a save (which bypass placement events).
///
/// Slot identity (address + type) is derived from the active <see cref="TextMeshPro"/>
/// labels already processed by <see cref="SlotRegistry"/>. The registry therefore
/// runs one pass AFTER <see cref="SlotRegistry"/> has already populated its dictionary.
/// </summary>
public class LocationRegistry : MonoBehaviour
{
    private static LocationRegistry _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[LocationRegistry]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LocationRegistry>();
    }

    // ── Static Dictionary ────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, LocationData> _byAddress = new();

    /// <summary>Retrieve the <see cref="LocationData"/> for a slot address. Returns false if
    /// the slot has not yet been registered (rack not yet placed / not yet live).</summary>
    public static bool TryGet(string address, out LocationData data)
        => _byAddress.TryGetValue(address, out data);

    /// <summary>All currently registered locations.</summary>
    public static IEnumerable<LocationData> All => _byAddress.Values;

    /// <summary>All registered locations whose <see cref="LocationData.Type"/> is Pick.</summary>
    public static IEnumerable<LocationData> PickLocations
        => _byAddress.Values.Where(d => d.Type == LocationType.Pick);

    /// <summary>All registered locations whose <see cref="LocationData.Type"/> is Reserve.</summary>
    public static IEnumerable<LocationData> ReserveLocations
        => _byAddress.Values.Where(d => d.Type == LocationType.Reserve);

    /// <summary>All registered locations that are currently Available.</summary>
    public static IEnumerable<LocationData> Available
        => _byAddress.Values.Where(d => d.IsAvailable);

    // ── Instance / Heartbeat ─────────────────────────────────────────────────────────────

    private bool  _subscribed;
    private bool  _dirty = true;
    private float _nextHeartbeat;

    private void OnEnable() => TrySubscribe();

    private void Update()
    {
        if (!_subscribed) TrySubscribe();

        if (Time.unscaledTime >= _nextHeartbeat)
        {
            _nextHeartbeat = Time.unscaledTime + 1f;
            _dirty = true;
        }

        if (_dirty)
        {
            _dirty = false;
            Recompute();
        }
    }

    private void TrySubscribe()
    {
        var em = EventManager.Instance;
        if (em == null) return;
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnChanged);
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnChanged);
        _subscribed = true;
        _dirty = true;
    }

    private void OnChanged(string _, PlacedObject __) => _dirty = true;

    private void OnDestroy()
    {
        var em = EventManager.Instance;
        if (em == null || !_subscribed) return;
        em.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnChanged);
        em.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnChanged);
    }

    // ── Recompute ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the location map from the live rack scene graph.
    ///
    /// For every live rack with exactly 2 active TMP labels:
    ///   1. Look up each label's address in <see cref="SlotRegistry"/>.
    ///   2. Find or create a child GameObject named after that address on the rack root.
    ///   3. Ensure a <see cref="LocationData"/> component exists and is initialised.
    ///   4. Snap the child's position to the label's world position (slot face at shelf height).
    ///
    /// Locations whose parent rack has been removed are pruned from the dictionary.
    /// Existing <see cref="LocationData"/> state (status, inventory) is preserved on recompute
    /// — only the identity and world position are refreshed.
    /// </summary>
    public void Recompute()
    {
        // Collect addresses we successfully register this pass so we can prune stale entries.
        var seen = new HashSet<string>();

        foreach (var po in PlacedObjectRegistry.All)
        {
            if (po == null || !po.isRackLive) continue;
            if (po.data == null || po.data.category != "Racking") continue;

            // Active labels: must be exactly 2 (one per aisle-facing position).
            var labels = po.GetComponentsInChildren<TextMeshPro>(false);
            if (labels == null || labels.Length != 2) continue;

            foreach (var label in labels)
            {
                string address = label.text;
                if (string.IsNullOrWhiteSpace(address)) continue;

                // Derive type: numeric level char = Pick, alphabetic = Reserve.
                if (!SlotRegistry.TryGet(address, out var slot)) continue;
                LocationType type = slot.IsPick ? LocationType.Pick : LocationType.Reserve;

                // Find or create the location child on the rack root.
                Transform locTransform = po.transform.Find(address);
                LocationData data;

                if (locTransform == null)
                {
                    // First time this slot has been seen — create the location object.
                    var go = new GameObject(address);
                    go.transform.SetParent(po.transform, worldPositionStays: false);
                    data = go.AddComponent<LocationData>();
                    Debug.Log($"[LocationRegistry] Created location '{address}' on rack '{po.name}'.");
                }
                else
                {
                    // Already exists — retrieve or add the component without resetting state.
                    data = locTransform.GetComponent<LocationData>();
                    if (data == null)
                        data = locTransform.gameObject.AddComponent<LocationData>();
                }

                // Snap the location child to the label's true face position. The label GameObjects
                // sit at localPosition (0,0,0), so transform.position gives the rack corner.
                // Renderer.bounds.center reads the mesh vertices in world space and gives the
                // correct face-centre regardless of pivot placement.
                var parentRenderer = label.transform.parent?.GetComponent<Renderer>();
                var ownRenderer    = label.GetComponent<Renderer>();
                Vector3 faceCenter = parentRenderer != null
                    ? parentRenderer.bounds.center
                    : (ownRenderer != null && ownRenderer.bounds.size.sqrMagnitude > 0.001f
                        ? ownRenderer.bounds.center
                        : label.transform.position);
                data.transform.position = faceCenter;

                // Always re-initialise identity in case the label text changed.
                data.Initialize(address, type);

                _byAddress[address] = data;
                seen.Add(address);
            }
        }

        // Prune addresses whose rack no longer exists.
        var stale = _byAddress.Keys.Where(k => !seen.Contains(k)).ToList();
        foreach (var key in stale)
        {
            _byAddress.Remove(key);
            LocationStatusRegistry.Release(key); // free the slot in the status registry too
        }
    }
}
