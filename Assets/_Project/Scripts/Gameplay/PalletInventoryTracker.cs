using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;
using GameCore.Events;

/// <summary>
/// Bridges the PHYSICAL world (placed Inventory-category objects — pallets/cases) to the DATA layer
/// (InventoryService.PalletData). Without this, a pallet you place in a lane is just a GameObject the
/// inventory system knows nothing about, so it has no address. This tracker keeps a PalletData record
/// in sync with every placed pallet: creates one when a pallet appears, updates its cell when the
/// pallet moves, and removes it when the pallet is deleted. That's what makes GetPalletAddress /
/// GetPalletsInLane / putaway operate on the real pallets sitting in staging.
///
/// Self-bootstrapping like the other dock services. Runs off placement events plus a slow heartbeat
/// (the heartbeat catches save-loads, which restore pallets by direct Instantiate, bypassing events).
/// Link identity is runtime-only (a PlacedObject → PalletData id map); pallet ids are regenerated on
/// reload, which is fine because nothing external references them yet.
/// </summary>
public class PalletInventoryTracker : MonoBehaviour
{
    private static PalletInventoryTracker _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // _instance is a plain static field, which a script recompile (domain reload) resets to null
        // WITHOUT destroying the old GameObject it pointed to — so this guard alone let a second
        // tracker spin up after every recompile.
        // Two trackers each keep their OWN _linked map, and each treats a pallet the OTHER one
        // already adopted as "not mine yet" — which raced into destroying a still-physically-present
        // pallet's inventory record and silently re-registering it under a second PalletMasterLink
        // with a new id and no WorkTask, orphaning it from the queue forever. Scan for a surviving
        // instance by object identity (not just the static field) before creating a new one.
        //
        // 2026-09-23: survivors are now DESTROYED, not adopted. Adopting one carried over a previous
        // session's _linked map and a subscription to that session's (dead) EventManager. The object is
        // HideInHierarchy + DontDestroyOnLoad now (HideAndDontSave outlived exiting Play mode), and
        // FindObjectsOfTypeAll (not FindFirstObjectByType) is still needed to see hidden objects.
        foreach (var stale in Resources.FindObjectsOfTypeAll<PalletInventoryTracker>())
            if (stale != null && stale != _instance) DestroyImmediate(stale.gameObject);
        if (_instance != null) return;

        var go = new GameObject("[PalletInventoryTracker]") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PalletInventoryTracker>();
    }

    // Placed pallet object → its PalletData id in InventoryService.
    private readonly Dictionary<PlacedObject, string> _linked = new();
    private readonly List<PlacedObject> _stale = new();

    private bool _subscribed;
    private EventManager _subscribedTo;
    private float _nextHeartbeat;
    private bool _dirty = true;

    private void Update()
    {
        // Re-subscribe if EventManager.Instance has been replaced (new Play session / scene load).
        if (!_subscribed || EventManager.Instance != _subscribedTo) TrySubscribe();

        if (Time.unscaledTime >= _nextHeartbeat)
        {
            _nextHeartbeat = Time.unscaledTime + 1f;
            _dirty = true;
        }
        if (!_dirty) return;
        _dirty = false;
        Sync();
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

    /// <summary>True when a DIFFERENT live object already carries a link to this record.</summary>
    private static bool HeldByAnotherObject(string palletId, GameObject self)
    {
        var holder = PalletMasterLink.Find(palletId);
        return holder != null && holder.gameObject != self && holder.isActiveAndEnabled;
    }

    private static bool IsPallet(PlacedObject po)
        => po != null && po.gameObject.activeInHierarchy && po.data != null && po.data.category == "Inventory";

    private void Sync()
    {
        if (!ServiceLocator.TryGet<InventoryService>(out var inv) || inv == null) return;

        // 1. Drop links whose pallet object is gone / no longer a valid pallet.
        _stale.Clear();
        foreach (var kv in _linked)
            if (!IsPallet(kv.Key)) _stale.Add(kv.Key);
        foreach (var po in _stale)
        {
            string staleId = _linked[po];
            _linked.Remove(po);
            // An in-session load (F9 / load slot) destroys the old pallet objects but RESTORES records under the
            // very same ids, so this link's id now belongs to the freshly restored pallet. Destroying the record
            // here wiped the restored stock and every task pointing at it (6 of 6 records lost in the 2026-10-19
            // round-trip test; a cold boot was unaffected because this map starts empty). Only retire the record
            // when no live pallet object still carries it.
            if (HeldByAnotherObject(staleId, po != null ? po.gameObject : null)) continue;
            inv.DestroyPallet(staleId);
        }

        // 2. Add/refresh links for every placed pallet currently in the world.
        // Iterate a SNAPSHOT: registering a record below raises inventory events whose listeners can place or
        // remove objects, which mutated the registry mid-loop — "Collection was modified" every frame, so the
        // sync never finished (found by the 2026-10-17 soak test).
        foreach (var po in PlacedObjectRegistry.All.ToList())
        {
            if (!IsPallet(po)) continue;
            // Staged outbound freight is NOT inventory: its record was deliberately destroyed when a truck or
            // selector staged it for an order. Its PalletMasterLink still points at that dead record, so without
            // this skip the branch below read it as "link with no record" and registered a PHANTOM pallet of the
            // prefab-name SKU "A Chep" x84 for every staged pallet (24 of them were found in one save) - phantom
            // stock the putaway system could never act on, standing in the lanes after every reload.
            if (po.GetComponent<OutboundPalletBuilder>() != null) continue;
            var cell = new Vector2Int(po.gridX, po.gridY);

            if (_linked.TryGetValue(po, out var id))
            {
                // Existing link — keep its cell current if the pallet was moved.
                var pallet = inv.GetPallet(id);
                if (pallet != null && pallet.CurrentLocation != cell)
                    inv.MovePallet(id, cell);

                // Linked to a record that no longer exists, while ANOTHER record carries this very Load ID: this
                // object is a duplicate copy of a pallet that is already tracked (a load restored it twice). One
                // Load ID is one physical pallet - remove the copy rather than leave it standing in the lane,
                // untracked and untouchable by any putaway.
                if (pallet == null)
                {
                    var copyData = po.GetComponent<PalletData>();
                    var byLoad = copyData != null && !string.IsNullOrEmpty(copyData.LoadId)
                        ? inv.GetAllPallets().FirstOrDefault(r => r.LoadId == copyData.LoadId) : null;
                    if (byLoad != null && po.GetComponent<OutboundPalletBuilder>() == null)
                    {
                        if (HeldByAnotherObject(byLoad.PalletId, po.gameObject))
                        {
                            // A genuine second copy of a pallet that another object already carries.
                            _linked.Remove(po);
                            Destroy(po.gameObject);
                            continue;
                        }
                        // Nobody else holds that record: this IS the pallet. Re-link rather than delete it.
                        PalletMasterLink.Attach(po.gameObject, byLoad.PalletId);
                        _linked[po] = byLoad.PalletId;
                        if (byLoad.CurrentLocation != cell) inv.MovePallet(byLoad.PalletId, cell);
                    }
                }
            }
            else
            {
                // Already tracked elsewhere? Offload pallets (TrailerOffloadController) register their
                // OWN PalletMasterRecord and tag the GameObject with a PalletMasterLink. Adopt that
                // existing record instead of creating a DUPLICATE via RegisterPhysicalPallet — otherwise
                // every offloaded pallet spawns a phantom second record (wrong SKU = the ObjData name
                // "A Chep", no loadId, and a bogus "stacked" WorldHeightY because CalculateWorldHeight
                // sees the real pallet already in the cell). Those phantoms are what corrupted the save
                // (42 records / 21 real pallets) and made stacking look broken.
                var existing = po.GetComponent<PalletMasterLink>();
                if (existing != null && !string.IsNullOrEmpty(existing.PalletId) && inv.GetPallet(existing.PalletId) != null)
                {
                    _linked[po] = existing.PalletId;
                }
                else
                {
                    // A RECEIVED pallet already knows what it is (PalletData: SKU, cases, Load ID). When its inventory
                    // record has gone missing, rebuild the record from THAT - registering it under the prefab name
                    // "A Chep" x84 instead made phantom stock of the wrong SKU and left the real pallet unrecoverable.
                    var knownData = po.GetComponent<PalletData>();
                    bool known = knownData != null && !string.IsNullOrEmpty(knownData.ItemNumber) && knownData.CaseQuantity > 0;

                    // A second object for a pallet that already has a record is a duplicate copy (a load restored the
                    // same pallet twice: once from its record, once from the saved dock objects). One Load ID = one
                    // physical pallet, so the copy goes instead of becoming a second ledger entry for the same stock.
                    //
                    // BUT only when another object really holds that record. A pallet restored with a stale link
                    // whose record WAS restored (same Load ID, new id) is the only object for it — the 2026-10-17
                    // soak test found this branch deleting all four such pallets on load. Re-link it instead.
                    var sameLoad = known && !string.IsNullOrEmpty(knownData.LoadId)
                        ? inv.GetAllPallets().FirstOrDefault(r => r.LoadId == knownData.LoadId) : null;
                    if (sameLoad != null)
                    {
                        if (HeldByAnotherObject(sameLoad.PalletId, po.gameObject))
                        {
                            Destroy(po.gameObject);
                            continue;
                        }
                        PalletMasterLink.Attach(po.gameObject, sameLoad.PalletId);
                        _linked[po] = sameLoad.PalletId;
                        if (sameLoad.CurrentLocation != cell) inv.MovePallet(sameLoad.PalletId, cell);
                        continue;
                    }
                    string sku = known ? knownData.ItemNumber : (po.data != null ? po.data.name : "PHYS");
                    int qty = known ? knownData.CaseQuantity : (po.GetComponent<PalletBuilder>()?.TotalCases ?? 1);
                    var pallet = inv.RegisterPhysicalPallet(cell, sku, Mathf.Max(1, qty));
                    if (known && !string.IsNullOrEmpty(knownData.LoadId)) pallet.LoadId = knownData.LoadId;
                    _linked[po] = pallet.PalletId;

                    // Attach a real PalletMasterLink, not just this tracker's own in-memory map —
                    // without it, PalletPersistenceService.CaptureAll() sees no PalletMasterLink on
                    // this pallet, saves it with an empty inventoryPalletId, and on the next load
                    // RestoreInventoryLink() skips attaching one too — so the pallet comes back
                    // link-less every time and gets phantom-registered again on the next heartbeat.
                    // That repeat phantom-registration (each with the wrong generic SKU name) is
                    // what corrupted saves in the past ("42 records / 21 real pallets").
                    PalletMasterLink.Attach(po.gameObject, pallet.PalletId);
                }
            }
        }
    }
}
