using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draw-call saver for a pallet's cases. A loaded pallet is ~200 separate renderers (every case, tape strip and label), and a
/// stocked warehouse had ~9,500 of them: SRP Batcher flush + shadow extraction dominated the frame (7-8 fps on Toaster).
///
/// While the load is UNCHANGED for a few seconds this merges every case mesh into one mesh (one submesh per material) and
/// switches the original renderers off. The original case objects stay in place, so everything that reads or edits them
/// (ComputeTiHiFromLayout, case removal, ghosting, save) keeps working. The moment the load changes (child count or material,
/// e.g. a case is picked or the pallet is ghosted) the merged mesh is dropped and the originals are shown again; it re-merges
/// once things settle.
/// </summary>
[DisallowMultipleComponent]
public class PalletLoadBatcher : MonoBehaviour
{
    private const float CheckInterval = 0.75f;
    private const float StableSeconds = 2.5f;

    private float _nextCheck;
    private int _signature;
    private float _stableSince;
    private bool _merged;
    private GameObject _combinedGO;
    private Mesh _combinedMesh;
    private readonly List<Renderer> _hidden = new List<Renderer>();

    private void OnEnable()
    {
        _nextCheck = Time.unscaledTime + Random.value * CheckInterval;   // spread the pallets over time
        _stableSince = Time.unscaledTime;
        _signature = 0;
    }

    // Pallets arrive by Build(), by save restore, or pre-built in prefabs, so attach by scanning rather than at each creation site.
    private class Scanner : MonoBehaviour
    {
        private float _next;
        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 3f;
            foreach (var pb in FindObjectsByType<PalletBuilder>(FindObjectsInactive.Exclude))
            {
                var load = pb.transform.Find("PalletLoad");
                if (load != null && load.GetComponent<PalletLoadBatcher>() == null) load.gameObject.AddComponent<PalletLoadBatcher>();
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        foreach (var stale in Resources.FindObjectsOfTypeAll<Scanner>())
            if (stale != null) Destroy(stale.gameObject);
        var go = new GameObject("[PalletLoadBatcherScanner]") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        go.AddComponent<Scanner>();
    }

    private void OnDisable() => Unmerge();
    private void OnDestroy() => Unmerge();

    private void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + CheckInterval;

        int sig = ComputeSignature();
        if (sig != _signature)
        {
            _signature = sig;
            _stableSince = Time.unscaledTime;
            if (_merged) Unmerge();
            return;
        }
        if (!_merged && Time.unscaledTime - _stableSince >= StableSeconds) Merge();
    }

    private int ComputeSignature()
    {
        int h = transform.childCount * 31;
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            var r = c.GetComponentInChildren<Renderer>(true);
            h = h * 17 + (r != null && r.sharedMaterial != null ? r.sharedMaterial.GetHashCode() : 0);
            h = h * 17 + (c.gameObject.activeSelf ? 1 : 0);
            h = h * 17 + c.localPosition.GetHashCode();
        }
        return h;
    }

    private void Merge()
    {
        var byMaterial = new Dictionary<Material, List<CombineInstance>>();
        var toHide = new List<Renderer>();
        Matrix4x4 world2local = transform.worldToLocalMatrix;
        UnityEngine.Rendering.ShadowCastingMode shadow = UnityEngine.Rendering.ShadowCastingMode.On;
        bool shadowSet = false;

        foreach (var mr in GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!mr.enabled) continue;
            var mf = mr.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null || !mesh.isReadable) continue;
            var mats = mr.sharedMaterials;
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                if (mats[s] == null) continue;
                if (!byMaterial.TryGetValue(mats[s], out var list)) byMaterial[mats[s]] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = world2local * mf.transform.localToWorldMatrix });
            }
            if (!shadowSet) { shadow = mr.shadowCastingMode; shadowSet = true; }
            toHide.Add(mr);
        }
        if (toHide.Count < 4 || byMaterial.Count == 0) return;   // not worth it

        var perMaterial = new List<CombineInstance>();
        var materials = new List<Material>();
        var temps = new List<Mesh>();
        foreach (var kv in byMaterial)
        {
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(kv.Value.ToArray(), true, true);
            temps.Add(m);
            perMaterial.Add(new CombineInstance { mesh = m, subMeshIndex = 0, transform = Matrix4x4.identity });
            materials.Add(kv.Key);
        }
        _combinedMesh = new Mesh { name = "PalletLoad_Combined", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        _combinedMesh.CombineMeshes(perMaterial.ToArray(), false, false);
        foreach (var t in temps) Destroy(t);

        _combinedGO = new GameObject("PalletLoad_Combined");
        // Parented to the pallet ROOT (PalletLoad sits at identity under it): several routines enumerate PalletLoad's children
        // as cases (ComputeTiHiFromLayout, persistence, RestoreCaseMaterial) and must not see this.
        _combinedGO.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
        _combinedGO.transform.localPosition = transform.localPosition;
        _combinedGO.transform.localRotation = transform.localRotation;
        _combinedGO.transform.localScale = transform.localScale;
        _combinedGO.AddComponent<MeshFilter>().sharedMesh = _combinedMesh;
        var rend = _combinedGO.AddComponent<MeshRenderer>();
        rend.sharedMaterials = materials.ToArray();
        rend.shadowCastingMode = shadow;
        rend.receiveShadows = true;

        foreach (var r in toHide) { r.enabled = false; _hidden.Add(r); }
        _merged = true;
        _signature = ComputeSignature();   // the extra child must not read as a change
    }

    private void Unmerge()
    {
        foreach (var r in _hidden) if (r != null) r.enabled = true;
        _hidden.Clear();
        if (_combinedGO != null) Destroy(_combinedGO);
        if (_combinedMesh != null) Destroy(_combinedMesh);
        _combinedGO = null;
        _combinedMesh = null;
        _merged = false;
    }
}
