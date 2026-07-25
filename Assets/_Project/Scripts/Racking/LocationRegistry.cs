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
    private static readonly HashSet<string> _reconciledPalletIds = new();

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
    ///   2. Find the hardcoded <see cref="LocationData"/> child authored under that label's
    ///      parent transform (sibling of the TMP label, e.g. LabelFront.L/Location) — found by
    ///      component type, not name, since Initialize() renames the GameObject to the address.
    ///   3. Fall back to generating a loose location object only if the rack prefab has none.
    ///
    /// Locations whose parent rack has been removed are pruned from the dictionary.
    /// Existing <see cref="LocationData"/> state (status, inventory) is preserved on recompute —
    /// only identity is refreshed. A hardcoded location's authored transform is never moved; it's
    /// the pallet-parenting / fork-approach anchor point and must stay exactly where it was placed
    /// in the prefab.
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

                // The hardcoded Location child lives as a sibling of the label under the same
                // per-position parent (e.g. LabelFront.L / LabelFront.R). Search by component
                // type rather than transform.Find(address): Find only checks direct children of
                // the rack root, missing this one level deeper, and the object isn't named
                // `address` until Initialize() runs on it below.
                LocationData data = label.transform.parent != null
                    ? label.transform.parent.GetComponentInChildren<LocationData>(true)
                    : null;

                if (data == null)
                {
                    // No hardcoded slot on this rack prefab — fall back to a generated location,
                    // positioned from the label's true face position. The label GameObjects sit
                    // at localPosition (0,0,0), so transform.position gives the rack corner;
                    // Renderer.bounds.center reads the mesh vertices in world space and gives the
                    // correct face-centre regardless of pivot placement.
                    var go = new GameObject(address);
                    go.transform.SetParent(po.transform, worldPositionStays: false);
                    data = go.AddComponent<LocationData>();

                    var parentRenderer = label.transform.parent?.GetComponent<Renderer>();
                    var ownRenderer    = label.GetComponent<Renderer>();
                    Vector3 faceCenter = parentRenderer != null
                        ? parentRenderer.bounds.center
                        : (ownRenderer != null && ownRenderer.bounds.size.sqrMagnitude > 0.001f
                            ? ownRenderer.bounds.center
                            : label.transform.position);
                    // Use the label's own Z position, not the renderer bounds Z (which includes label thickness).
                    faceCenter.z = label.transform.position.z;
                    data.transform.position = faceCenter;

                    Debug.Log($"[LocationRegistry] Created location '{address}' on rack '{po.name}' (no hardcoded slot found).");
                }

                // Always re-initialise identity in case the label text changed. Position is left
                // untouched for hardcoded slots — their authored transform is the anchor everything
                // else (putaway parenting, fork approach) targets.
                data.Initialize(address, type);

                // Keep the Inspector-visible status honest: LocationStatusRegistry is the actual
                // gatekeeper PutawayLogic queries, but some writers (PutawayLogic itself, and
                // ReplenishmentService locking both ends of a replenish task) set it directly without
                // going through this component's own Reserve()/Occupy()/Release(). Without this
                // resync, a slot the registry has locked can display "Available" here indefinitely.
                data.SyncStatusDisplay(LocationStatusRegistry.Get(address));

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

        ReconcilePhysicalOccupancy();
    }

    /// <summary>
    /// Restored reserve/pick pallets never call <see cref="LocationData.Occupy"/> -- only a live
    /// Reach Truck delivery (DeliverPalletToRack) does. Without this, a pallet restored from a
    /// save sits physically on a shelf but its slot reads Available forever, so
    /// ReplenishmentService can never find it as reserve stock. Runs once per pallet id (tracked
    /// in _reconciledPalletIds), not every heartbeat -- a pallet that doesn't match a slot now
    /// (e.g. still in staging) won't match later either; putaway/replenishment already call
    /// Occupy() directly when they place one for real.
    /// </summary>
    private static void ReconcilePhysicalOccupancy()
    {
        if (_byAddress.Count == 0) return; // rack registry not populated yet -- try again next heartbeat
        if (!GameCore.Services.ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return;

        const float ToleranceSq = 0.25f * 0.25f;
        foreach (var link in PalletMasterLink.All)
        {
            if (link == null || string.IsNullOrEmpty(link.PalletId)) continue;
            if (_reconciledPalletIds.Contains(link.PalletId)) continue; // already resolved

            var record = inv.GetPallet(link.PalletId);
            if (record == null) continue;

            LocationData nearest = null;
            float bestSq = ToleranceSq;
            foreach (var loc in _byAddress.Values)
            {
                float d = (loc.transform.position - link.transform.position).sqrMagnitude;
                if (d < bestSq) { bestSq = d; nearest = loc; }
            }

            if (nearest == null) continue; // no slot at this position (yet) -- retry next heartbeat

            // Only mark this pallet resolved once we've actually found its slot -- otherwise an
            // early pass (before racks finish registering) would permanently skip it with no match.
            _reconciledPalletIds.Add(link.PalletId);
            if (nearest.IsAvailable)
                nearest.Occupy(link.PalletId, record.SkuId, record.Quantity);
        }
    }
}
