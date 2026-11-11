using System;
using System.Collections.Generic;
using GameCore.Inventory;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Swaps world meshes other than workers and MHE to one material while enabled.</summary>
public sealed class StationaryMaterialView : IDisposable
{
    private Material _material;
    private readonly List<Renderer> _renderers = new();
    private readonly Dictionary<Renderer, Material[]> _originals = new();
    private float _nextRefresh;

    public bool IsEnabled { get; private set; }

    public StationaryMaterialView(Material material) => _material = material;

    public void Toggle()
    {
        if (IsEnabled) { Disable(); return; }
        var settings = Resources.Load<StationaryMaterialSettings>("StationaryMaterialSettings");
        if (settings != null && settings.material != null) _material = settings.material;
        if (_material == null)
        {
            Debug.LogError("[StationaryMaterialView] Warehouse view material is missing from StationaryMaterialSettings.");
            UIToast.Show("Warehouse material is missing from its settings asset.");
            return;
        }

        IsEnabled = true;
        RefreshRenderers();
        Apply();
    }

    public void Update()
    {
        if (!IsEnabled || Time.unscaledTime < _nextRefresh) return;
        RefreshRenderers();
        Apply();
    }

    private void RefreshRenderers()
    {
        _nextRefresh = Time.unscaledTime + 2f;
        _renderers.Clear();
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (renderer != null && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) &&
                renderer.GetComponent<TMPro.TMP_Text>() == null && renderer.GetComponentInParent<Canvas>() == null)
                _renderers.Add(renderer);
        }
    }

    private static bool IsWorkerOrEquipment(Renderer renderer)
    {
        // A pallet can be parented to equipment: it is freight, not part of the machine.
        for (Transform current = renderer.transform; current != null; current = current.parent)
        {
            if (current.GetComponent<PalletData>() != null || current.GetComponent<PalletMasterLink>() != null ||
                current.GetComponent<OutboundPalletBuilder>() != null) return false;
            if (current.GetComponent<EmployeeIdentity>() != null || current.GetComponent<MHEOperatorSlot>() != null)
                return true;
            var agent = current.GetComponent<AgentTypeTag>();
            if (agent != null && (agent.agentType & (AgentType.Human | AgentType.MHE)) != 0) return true;
            var obj = current.GetComponent<PlacedObject>();
            if (obj != null && obj.data != null &&
                (obj.data.category == "Staff" || obj.data.category == "Worker" || obj.data.category == "MHE"))
                return true;
        }
        return false;
    }

    private void Apply()
    {
        foreach (var renderer in _renderers)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy || _originals.ContainsKey(renderer) ||
                IsWorkerOrEquipment(renderer)) continue;
            var originals = renderer.sharedMaterials;
            if (originals.Length == 0 || HasTemporaryMaterial(originals)) continue;
            var replacement = new Material[originals.Length];
            for (int i = 0; i < replacement.Length; i++) replacement[i] = _material;
            _originals.Add(renderer, originals);
            renderer.sharedMaterials = replacement;
        }
    }
    private static bool HasTemporaryMaterial(Material[] materials)
    {
        foreach (var material in materials)
            if (material != null && (material.name.IndexOf("ghost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     material.name.IndexOf("Placement-", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
        return false;
    }

    private void Restore()
    {
        foreach (var pair in _originals)
        {
            if (pair.Key == null) continue;
            var current = pair.Key.sharedMaterials;
            if (current.Length > 0 && current[0] == _material) pair.Key.sharedMaterials = pair.Value;
        }
        _originals.Clear();
    }
    private void Disable()
    {
        Restore();
        _renderers.Clear();
        IsEnabled = false;
    }

    public void Dispose() => Disable();
}
