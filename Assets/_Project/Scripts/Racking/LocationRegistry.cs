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
        // HideInHierarchy + DontDestroyOnLoad, NOT HideAndDontSave: with Enter Play Mode Options (no domain
        // reload) a HideAndDontSave object survives exiting Play, so every session/recompile left another
        // copy running (6 found live 2026-09-23). This one is destroyed on Play exit; sweep any leftovers.
        foreach (var stale in Resources.FindObjectsOfTypeAll<LocationRegistry>())
            if (stale != null && stale != _instance) DestroyImmediate(stale.gameObject);
        if (_instance != null) return;
        var go = new GameObject("[LocationRegistry]") { hideFlags = HideFlags.HideInHierarchy };
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
    private EventManager _subscribedTo;
    private bool  _dirty = true;
    private float _nextHeartbeat;

    // The static maps above outlive a Play session (domain reload is disabled). A new instance means a
    // new session: drop the previous session's (destroyed) LocationData refs and reconciled pallet ids —
    // pallet ids survive save/load, so a stale entry would skip reconciling that pallet this session.
    private void Awake()
    {
        _byAddress.Clear();
        _reconciledPalletIds.Clear();
    }

    private void OnEnable() => TrySubscribe();

    private void Update()
    {
        // Re-subscribe if EventManager.Instance has been replaced (new Play session / scene load).
        if (!_subscribed || EventManager.Instance != _subscribedTo) TrySubscribe();

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
        if (em == null) { _subscribed = false; return; }
        _subscribedTo?.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnChanged);
        _subscribedTo?.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnChanged);
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnChanged);
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnChanged);
        _subscribedTo = em;
        _subscribed = true;
        _dirty = true;
    }

    private void OnChanged(string _, PlacedObject __) => _dirty = true;

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_subscribedTo == null || !_subscribed) return;
        _subscribedTo.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnChanged);
        _subscribedTo.Unsubscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnChanged);
    }

    // ── Recompute ────────────────────────────────────────────────────────────────────────

    /// <summary>Forces an immediate rebuild rather than waiting for the 1s heartbeat — used by
    /// AisleRenameService so lookups (PutawayLogic, OrderPickPath, SlotAssignmentPanel) are
    /// consistent with a just-submitted rename before the next frame, not up to a second later.</summary>
    public static void ForceRecompute() => _instance?.Recompute();

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
                    // No hardcoded slot on this rack prefab (the 48" racks have none) - use ONE generated
                    // location per label face. It lives under the rack root, NOT under the label group, so the
                    // lookup above can never find it again: without remembering it, every heartbeat built a
                    // brand new one (thousands per rack, 27,000 in a session) and none of them ever carried
                    // an approach anchor, so every putaway to those racks aborted and looped.
                    data = GetOrCreateGeneratedLocation(po, label, address);
                }

                EnsureApproachAnchor(data, label);

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
        ReleasePhantomOccupancy();
    }

    // One generated LocationData per label face, keyed by the label's group (stable even if the address is
    // renamed, which Initialize() does by renaming the GameObject).
    private static readonly Dictionary<Transform, LocationData> _generatedByFace = new Dictionary<Transform, LocationData>();
    private const string ApproachAnchorName = "LocApproachAnchor";
    // Matches the authored anchor on the 80" rack prefab: 3 m straight out from the face, aisle side.
    private const float ApproachAnchorDistance = 3f;

    private static LocationData GetOrCreateGeneratedLocation(PlacedObject po, TextMeshPro label, string address)
    {
        Transform face = label.transform.parent != null ? label.transform.parent : label.transform;

        if (_generatedByFace.TryGetValue(face, out var existing) && existing != null)
        {
            RemoveDuplicateGeneratedLocations(po.transform, existing);
            return existing;
        }

        // A previous pass in this Play session (statics survive without a domain reload) may have left one
        // under the rack root already: adopt the first named for this address and delete the rest.
        LocationData adopted = null;
        foreach (Transform child in po.transform)
        {
            if (child.name != address) continue;
            var ld = child.GetComponent<LocationData>();
            if (ld == null) continue;
            if (adopted == null) adopted = ld;
        }
        if (adopted != null)
        {
            _generatedByFace[face] = adopted;
            RemoveDuplicateGeneratedLocations(po.transform, adopted);
            return adopted;
        }

        var go = new GameObject(address);
        go.transform.SetParent(po.transform, worldPositionStays: false);
        var data = go.AddComponent<LocationData>();

        // Seat the pallet where the prefab-authored slots do (Rack-FullOrange80): at the rack's own origin on
        // the rack's depth centre, shifted along the rack only for the right-hand position of a full bay.
        // Placing it on the label FACE (the old way) left pallets half hanging out of the rack.
        string faceName = face.name;
        float x = faceName.Contains(".R") ? -1.325f : faceName.Contains(".L") ? 0f : -0.07f;
        data.transform.position = po.transform.TransformPoint(new Vector3(x, 0f, 0f));
        data.transform.rotation = po.transform.rotation;

        _generatedByFace[face] = data;
        Debug.Log($"[LocationRegistry] Created location '{address}' on rack '{po.name}' (no hardcoded slot found).");
        return data;
    }

    /// <summary>Deletes every extra generated location under the rack root other than the one being kept.</summary>
    private static void RemoveDuplicateGeneratedLocations(Transform rack, LocationData keep)
    {
        if (keep == null) return;
        for (int i = rack.childCount - 1; i >= 0; i--)
        {
            var child = rack.GetChild(i);
            if (child == keep.transform || child.name != keep.name) continue;
            if (child.GetComponent<LocationData>() == null) continue;
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    /// <summary>Gives a location the approach anchor a reach truck drives to. Prefab-authored slots already
    /// have one; a generated slot (48" racks) gets one 3 m out from its face on the aisle side - the label
    /// group's own direction (Front = rack +Z, Rear = rack -Z), the same layout the 80" prefab authors.</summary>
    private static void EnsureApproachAnchor(LocationData data, TextMeshPro label)
    {
        if (data == null || data.transform.Find(ApproachAnchorName) != null) return;

        bool rear = label.transform.parent != null &&
                    label.transform.parent.name.IndexOf("Rear", System.StringComparison.OrdinalIgnoreCase) >= 0;
        var anchor = new GameObject(ApproachAnchorName).transform;
        anchor.SetParent(data.transform, worldPositionStays: false);
        anchor.localPosition = new Vector3(0f, 0f, rear ? -ApproachAnchorDistance : ApproachAnchorDistance);
        anchor.localRotation = Quaternion.identity;
    }

    private static readonly Dictionary<string, int> _phantomStrikes = new Dictionary<string, int>();
    private const int PhantomStrikesToRelease = 3;   // consecutive heartbeats (~3s) so a pallet mid-placement is never mistaken for a ghost

    /// <summary>A slot the status registry calls Occupied while no pallet is physically standing at it is a ghost -- left behind by a save
    /// whose pallets were not restored (or by a deleted pallet). Putaway, replenishment and the Slot panel all trust that status, so a
    /// ghost makes an empty rack look full and lets tasks be filed against stock that is not there. Released after it has stayed
    /// unmatched for a few consecutive heartbeats.</summary>
    private static void ReleasePhantomOccupancy()
    {
        if (_byAddress.Count == 0) return;
        const float RadiusSq = 0.9f * 0.9f;
        var palletPositions = new List<Vector3>();
        foreach (var link in PalletMasterLink.All) if (link != null) palletPositions.Add(link.transform.position);

        foreach (var kv in _byAddress)
        {
            string address = kv.Key;
            if (LocationStatusRegistry.Get(address) != LocationStatus.Occupied) { _phantomStrikes.Remove(address); continue; }
            var loc = kv.Value;
            if (loc == null) continue;

            bool physical = false;
            Vector3 lp = loc.transform.position;
            foreach (var pp in palletPositions)
            {
                float dx = pp.x - lp.x, dz = pp.z - lp.z;
                if (dx * dx + dz * dz <= RadiusSq) { physical = true; break; }
            }
            if (physical) { _phantomStrikes.Remove(address); continue; }

            _phantomStrikes.TryGetValue(address, out int strikes);
            strikes++;
            if (strikes < PhantomStrikesToRelease) { _phantomStrikes[address] = strikes; continue; }

            _phantomStrikes.Remove(address);
            Debug.LogWarning($"[LocationRegistry] Slot {address} was marked Occupied but no pallet is standing there -- releasing it.");
            loc.Release();
        }
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

            // Fill the slot's CONTENTS whenever they're missing -- do NOT gate on IsAvailable.
            // LocationStatusRegistry (status) IS persisted across save/load, but LocationData's
            // contents are NOT, so a restored pallet's slot comes back already marked Occupied with
            // an empty PalletId/SkuId/Quantity. The old `if (nearest.IsAvailable)` guard was
            // therefore false for precisely the slots this reconciliation exists to repair, and
            // those slots stayed "Occupied with blank contents" forever (measured live: 91 occupied
            // slots, 90 of them blank). Keying off "has no pallet recorded" instead fixes restored
            // slots while still never clobbering a slot whose contents are already known.
            string existingPallet = nearest.PalletId;
            if (string.IsNullOrEmpty(existingPallet))
            {
                string expiry = record.ExpirationDayNumber >= 0 ? record.ExpirationDayNumber.ToString() : null;
                nearest.Occupy(link.PalletId, record.SkuId, record.Quantity, expiry, record.LoadId);

                // Backfill the PALLET's own readable slot address too, so the cross-reference works
                // in both directions for save-restored pallets (which never went through the live
                // putaway path that normally sets this).
                var pd = link.GetComponent<PalletData>();
                if (pd != null) pd.SetLocation(record.CurrentLocation, nearest.Address);
            }
        }
    }
}
