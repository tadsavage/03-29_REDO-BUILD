using System.Collections.Generic;
using UnityEngine;
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
        if (_instance != null) return;
        var go = new GameObject("[PalletInventoryTracker]") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PalletInventoryTracker>();
    }

    // Placed pallet object → its PalletData id in InventoryService.
    private readonly Dictionary<PlacedObject, string> _linked = new();
    private readonly List<PlacedObject> _stale = new();

    private bool _subscribed;
    private float _nextHeartbeat;
    private bool _dirty = true;

    private void Update()
    {
        if (!_subscribed) TrySubscribe();

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
        if (em == null) return;
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectPlaced, OnChanged);
        em.Subscribe<PlacedObject>(GameEvents.Build.OnObjectDeleted, OnChanged);
        _subscribed = true;
        _dirty = true;
    }

    private void OnChanged(string _, PlacedObject __) => _dirty = true;

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
            inv.DestroyPallet(_linked[po]);
            _linked.Remove(po);
        }

        // 2. Add/refresh links for every placed pallet currently in the world.
        foreach (var po in PlacedObjectRegistry.All)
        {
            if (!IsPallet(po)) continue;
            var cell = new Vector2Int(po.gridX, po.gridY);

            if (_linked.TryGetValue(po, out var id))
            {
                // Existing link — keep its cell current if the pallet was moved.
                var pallet = inv.GetPallet(id);
                if (pallet != null && pallet.CurrentLocation != cell)
                    inv.MovePallet(id, cell);
            }
            else
            {
                string sku = po.data != null ? po.data.name : "PHYS";
                int qty = po.GetComponent<PalletBuilder>()?.TotalCases ?? 1;
                var pallet = inv.RegisterPhysicalPallet(cell, sku, Mathf.Max(1, qty));
                _linked[po] = pallet.PalletId;
            }
        }
    }
}
