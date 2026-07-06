using GameCore.Economy;
using GameCore.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PalletBuilder : MonoBehaviour
{
    [Header("Product Config")]
    public GameObject casePrefab;
    [Range(0.1f, 3.0f)] public float maxTotalHeight = 1.0f;

    [Header("Optimizer Link")]
    [Tooltip("SKU whose CaseLength/Width/Height drive PalletOptimizer.OptimizeLoad — see the " +
             "'Auto-Compute Ti/Hi' button in the Pallet Builder inspector. Optional: leave null " +
             "to keep using the manual Ti/Hi override below untouched.")]
    public SkuData linkedSku;

    [Header("Optimizer Results (Read Only)")]
    [SerializeField] private string optimizerPatternDescription;
    [SerializeField] private float optimizerUtilizationPercent;
    [SerializeField] private float optimizerTotalHeightMeters;
    [SerializeField] private float optimizerTargetHeightMeters;

    [Header("Pallet Config")]
    // Real 40"x48" GMA pallet with a 0.16m deck height — world X axis = 48" (long), world Z axis = 40" (short).
    // X = 1.2192m (48"), H = 0.16m, Z = 1.016m (40"). Matches PalletOptimizer and SkuData.PltHeight.
    public Vector3 palletDimensions = new Vector3(1.2192f, 0.16f, 1.016f); // W(48"), H, L(40")

    [Header("Spacing Settings")]
    [Tooltip("Minimum horizontal distance between cases.")]
    [Range(0.01f, 0.2f)] public float spaceBetweenCases = 0.05f;
    [Tooltip("Fixed vertical gap between layers.")]
    [Range(0.01f, 0.25f)] public float verticalGap = 0.025f;

    [Header("Case Overrides")]
    public bool usePrefabBounds = true;
    public Vector3 caseDimensions = new Vector3(0.5f, 0.25f, 0.24f); // W, H, L

    [Header("Aesthetic Settings")]
    [Tooltip("Random Y rotation variation for a realistic look.")]
    [Range(0f, 10f)] public float crookedCase = 2.0f;

    [Header("Overrides (Manual Ti-Hi)")]
    public bool useTiHiOverride = false;
    public int manualTi = 6;
    public int manualHi = 3;

    [Header("Results (Read Only)")]
    [SerializeField] private int casesPerLayer;
    [SerializeField] private int layers;
    [SerializeField] private int totalCases;
    public int CurrentLoadCost { get; private set; }

    /// <summary>Number of cases currently built on this pallet (used by inventory tracking).</summary>
    public int TotalCases => totalCases;

    [System.Serializable]
    public struct BuildSettings
    {
        public int caseDataID;
        public float maxHeight;
        public float spaceBetween;
        public float vertGap;
        public float crooked;
        public bool useOverride;
        public int manualTi;
        public int manualHi;
        public int totalLoadCost;
    }

    private MoneyService _moneyService;
    private PlacedObject _placedObject;

    // Saved once when GhostCases() first ghosts this pallet's cases (cargo, unreceived) so
    // RestoreCaseMaterial() can put the real material back once the pallet is actually received.
    // Only the first case's material is kept — every case on a pallet uses the same case prefab/material.
    private Material _originalCaseMaterial;
    public Material OriginalCaseMaterial => _originalCaseMaterial;

    private struct CasePlacement
    {
        public Vector3 position;
        public float rotation;
    }

    private List<CasePlacement> _bestLayerPattern = new List<CasePlacement>();


    private void Awake()
    {
        _placedObject = GetComponent<PlacedObject>();
    }

    private void Start()
    {
        _moneyService = FindAnyObjectByType<GameContext>()?.MoneyService;

        // Load state if not already loaded by external system (e.g. PlacementSystem.SpawnFromSave)
        if (_placedObject != null && !string.IsNullOrEmpty(_placedObject.customData) && transform.Find("PalletLoad") == null)
        {
            LoadBuildState();
        }
    }

    public void SaveBuildState()
    {
        if (_placedObject == null) return;

        BuildSettings settings = new BuildSettings
        {
            caseDataID = (casePrefab != null) ? GetCaseID(casePrefab) : -1,
            maxHeight = maxTotalHeight,
            spaceBetween = spaceBetweenCases,
            vertGap = verticalGap,
            crooked = crookedCase,
            useOverride = useTiHiOverride,
            manualTi = manualTi,
            manualHi = manualHi,
            totalLoadCost = CurrentLoadCost
        };

        _placedObject.customData = JsonUtility.ToJson(settings);
    }

    private int GetCaseID(GameObject prefab)
    {
        var registry = FindRegistry();
        if (registry == null) return -1;
        foreach (var so in registry.buttonSOs)
        {
            if (so != null && so.prefab == prefab) return so.id;
        }
        return -1;
    }

    private ObjDataRegistry FindRegistry()
    {
        var buildMenu = FindAnyObjectByType<BuildMenuUI>();
        if (buildMenu != null && buildMenu.registry != null) return buildMenu.registry;

        // Fallback: Search Assets/Resources or just Find any registry in scene
        var allRegistries = Resources.FindObjectsOfTypeAll<ObjDataRegistry>();
        if (allRegistries.Length > 0) return allRegistries[0];

        return null;
    }

    public void LoadBuildState()
    {
        if (_placedObject == null) 
        {
            Debug.LogWarning("PalletBuilder: Cannot load state, _placedObject is null.");
            return;
        }
        
        if (string.IsNullOrEmpty(_placedObject.customData))
        {
            //Debug.Log($"PalletBuilder: No custom build data on {_placedObject.name}");
            return;
        }

        try
        {
            BuildSettings settings = JsonUtility.FromJson<BuildSettings>(_placedObject.customData);
            
            maxTotalHeight = settings.maxHeight;
            spaceBetweenCases = settings.spaceBetween;
            verticalGap = settings.vertGap;
            crookedCase = settings.crooked;
            useTiHiOverride = settings.useOverride;
            manualTi = settings.manualTi;
            manualHi = settings.manualHi;
            CurrentLoadCost = settings.totalLoadCost;

            if (settings.caseDataID != -1)
            {
                var registry = FindRegistry();
                if (registry != null)
                {
                    var so = registry.GetByID(settings.caseDataID);
                    if (so != null) 
                    {
                        casePrefab = so.prefab;
                    }
                }
            }

            Build(deductMoney: false);
            //Debug.Log($"PalletBuilder: Restored built state for {_placedObject.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"PalletBuilder: Failed to load build state: {e.Message}");
        }
    }

    /// <summary>
    /// Computes Ti (cases per layer) and Hi (layers) from linkedSku's real case dimensions via
    /// PalletOptimizer, targeting whichever rack tier (1m/1.8m) that case height belongs to, and
    /// applies the result as a manual Ti/Hi override so Build() renders exactly that layout. Does
    /// NOT write back to linkedSku itself — see PalletBuilderEditor's "Submit to Master Record"
    /// button for that (a separate, explicit step so previewing a layout never silently mutates
    /// the SKU's committed data).
    /// </summary>
    public PalletOptimizer.PalletResult ComputeOptimalTiHi()
    {
        if (linkedSku == null)
        {
            Debug.LogWarning("PalletBuilder: no linkedSku assigned, cannot auto-compute Ti/Hi.");
            return default;
        }

        optimizerTargetHeightMeters = PalletOptimizer.DetermineTargetPalletHeight(linkedSku.CaseHeight);
        var result = PalletOptimizer.OptimizeLoad(
            linkedSku.CaseLength * 100f,
            linkedSku.CaseWidth * 100f,
            linkedSku.CaseHeight * 100f,
            optimizerTargetHeightMeters);

        useTiHiOverride = true;
        manualTi = result.CasesPerLayer;
        manualHi = result.Layers;
        optimizerPatternDescription = result.LayerPatternDescription;
        optimizerUtilizationPercent = result.VolumeUtilization;
        optimizerTotalHeightMeters = result.TotalHeightMeters;
        return result;
    }

    [ContextMenu("Build Pallet")]
    public void Build() => Build(true);

    public void Build(bool deductMoney)
    {
        if (casePrefab == null)
        {
            Debug.LogError("PalletBuilder: Case Prefab is missing!");
            return;
        }

        // 1. Determine Dimensions
        Vector3 palletDim = palletDimensions;
        Vector3 caseDim = caseDimensions;
        if (usePrefabBounds) caseDim = GetPrefabDimensions(casePrefab);

        // 2. Calculate Best Layer Pattern
        CalculateBestLayer(palletDim.x, palletDim.z, caseDim.x, caseDim.z);

        // 3. Determine Ti (Cases Per Layer) and Hi (Layers)
        if (useTiHiOverride)
        {
            layers = manualHi;
            casesPerLayer = manualTi;
        }
        else
        {
            float availableHeight = maxTotalHeight - palletDim.y;
            layers = Mathf.FloorToInt((availableHeight + verticalGap) / (caseDim.y + verticalGap));
            casesPerLayer = _bestLayerPattern.Count;
        }

        if (layers <= 0 || (useTiHiOverride ? false : _bestLayerPattern.Count == 0))
        {
            Debug.LogWarning("PalletBuilder: Invalid dimensions or overrides. Cannot build pallet.");
            return;
        }

        totalCases = casesPerLayer * layers;

        // Money Deduction
        if (deductMoney && _moneyService != null)
        {
            var registry = FindAnyObjectByType<BuildMenuUI>()?.registry;
            int caseCost = 0;
            if (registry != null)
            {
                int id = GetCaseID(casePrefab);
                var so = registry.GetByID(id);
                if (so != null) caseCost = so.cost;
            }

            int totalCost = caseCost * totalCases;
            if (!_moneyService.CanAfford(totalCost))
            {
                UIToast.Show("Not enough capital to build this pallet!");
                return;
            }

            _moneyService.Deduct(totalCost, "Inventory");
            CurrentLoadCost += totalCost;
            AudioManager.Play("UI_Buy");
        }

        // 4. Instantiate
        // Clear ALL existing loads to prevent overlapping if multiple exist
        List<GameObject> toDestroy = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "PalletLoad")
            {
                toDestroy.Add(child.gameObject);
            }
        }

        foreach (var obj in toDestroy)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(obj);
            else Destroy(obj);
#else
            Destroy(obj);
#endif
        }
        
        GameObject loadObj = new GameObject("PalletLoad");
        loadObj.transform.SetParent(transform);
        loadObj.transform.localPosition = Vector3.zero;

        for (int h = 0; h < layers; h++)
        {
            // Position the case so its CENTER is at the calculated Y. Case prefabs may have their
            // mesh origin offset from the prefab center (some authored with center-origin, some with
            // bottom-origin, some with top-origin). GetMeshYOffset() detects this and returns the
            // Y-offset to add so the mesh ends up at the right height.
            // CRITICAL FIX (2026-07-05): First layer sits directly on pallet deck with NO gap.
            // Higher layers are spaced by verticalGap. This ensures cases sit flush on the pallet
            // in the trailer, preventing jarring snaps when dropped into staging lanes.
            float meshYOffset = usePrefabBounds ? GetMeshYOffset(casePrefab) : 0f;
            float yPos = palletDim.y + (caseDim.y / 2f) + meshYOffset + (h * (caseDim.y + verticalGap));
            
            int count = 0;
            foreach (var placement in _bestLayerPattern)
            {
                if (useTiHiOverride && count >= manualTi) break;

                Vector3 pos = placement.position;
                pos.y = yPos;

                GameObject instance;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(casePrefab);
                else
                    instance = Instantiate(casePrefab);
#else
                instance = Instantiate(casePrefab);
#endif
                instance.transform.SetParent(loadObj.transform);
                instance.transform.localPosition = pos;
                
                // CRITICAL FIX: The case prefabs have PlacedObject/BuildingData components.
                // When instantiated as part of a pallet, they must NOT register themselves
                // in the global registry or they will appear at (0,0) in the save file.
                var po = instance.GetComponent<PlacedObject>();
                if (po != null) 
                {
                    po.enabled = false; // Immediately unregisters
                    if (Application.isPlaying) Destroy(po); else DestroyImmediate(po);
                }
                var bd = instance.GetComponent<BuildingData>();
                if (bd != null) 
                {
                    if (Application.isPlaying) Destroy(bd); else DestroyImmediate(bd);
                }
                var bh = instance.GetComponent<BuildingHighlighter>();
                if (bh != null)
                {
                    if (Application.isPlaying) Destroy(bh); else DestroyImmediate(bh);
                }

                // Add "Crooked" rotation
                float randomRot = Random.Range(-crookedCase, crookedCase);
                instance.transform.localRotation = Quaternion.Euler(0, placement.rotation + randomRot, 0);

                count++;
            }
        }

        SaveBuildState();

        //Debug.Log($"Pallet Built: {casesPerLayer} Ti x {layers} Hi = {totalCases} total cases. State Saved.");
    }

    /// <summary>
    /// Ghosts every case on this pallet (all renderers/submesh slots under the "PalletLoad" child
    /// Build() creates — NOT the pallet base itself) to <paramref name="ghostMaterial"/>. Saves the
    /// first case's original material as <see cref="OriginalCaseMaterial"/> the first time this runs,
    /// so <see cref="RestoreCaseMaterial"/> can put it back once the pallet is actually received.
    /// </summary>
    public void GhostCases(Material ghostMaterial)
    {
        if (ghostMaterial == null) return;

        var loadObj = transform.Find("PalletLoad");
        if (loadObj == null) return;

        var renderers = loadObj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        if (_originalCaseMaterial == null && renderers[0].sharedMaterials.Length > 0)
            _originalCaseMaterial = renderers[0].sharedMaterials[0];

        foreach (var r in renderers)
        {
            var ghosts = new Material[r.sharedMaterials.Length];
            for (int m = 0; m < ghosts.Length; m++) ghosts[m] = ghostMaterial;
            r.sharedMaterials = ghosts;
        }
    }

    /// <summary>Restores every case's material to what GhostCases() saved. Called once this pallet is
    /// actually received (see ReceiverReceivingWorkflow). No-op if never ghosted.</summary>
    public void RestoreCaseMaterial()
    {
        if (_originalCaseMaterial == null) return;

        var loadObj = transform.Find("PalletLoad");
        if (loadObj == null) return;

        var renderers = loadObj.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            var solids = new Material[r.sharedMaterials.Length];
            for (int m = 0; m < solids.Length; m++) solids[m] = _originalCaseMaterial;
            r.sharedMaterials = solids;
        }
    }

    public void ToggleUI()
    {
        if (!Application.isPlaying) return;
        ToolsWindowController.Instance?.OpenForPallet(this);
    }

    public void CloseUI()
    {
        ToolsWindowController.Instance?.Hide();
    }

    private void OnMouseDown()
    {
        // Only allow bringing up the Pallet Builder UI if the state machine is in IdleState
        var fsm = FindAnyObjectByType<PlacementStateMachine>();
        if (fsm != null && !(fsm.CurrentState is IdleState))
            return;

        // Require Shift + Left Click — plain left click is reserved for future selection
        bool shiftHeld = Keyboard.current != null
            && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        if (!shiftHeld) return;

        ToggleUI();
    }

    /// <summary>Delegates to <see cref="PalletOptimizer.PackLayer"/> — the same search used by the
    /// Editor's Ti/Hi calculator — so what actually renders here always matches what that tool
    /// recommends. See PalletOptimizer for the packing strategy (recursive guillotine split,
    /// maximizing surface coverage rather than favoring a fixed orientation).</summary>
    private void CalculateBestLayer(float pW, float pL, float cW, float cL)
    {
        _bestLayerPattern.Clear();

        var result = PalletOptimizer.PackLayer(pW, pL, cW, cL, spaceBetweenCases);

        // PackLayer returns slots anchored to one corner of the pW x pL region; re-center them
        // around the pallet's own origin to match every other position in this class.
        float halfW = pW / 2f;
        float halfL = pL / 2f;
        foreach (var slot in result.Slots)
        {
            _bestLayerPattern.Add(new CasePlacement
            {
                position = new Vector3(slot.x - halfW, 0, slot.z - halfL),
                rotation = slot.rotationDegrees
            });
        }
    }

    public static Vector3 GetPrefabDimensions(GameObject prefab)
    {
        Vector3 rawSize = new Vector3(1, 1, 1);
        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            rawSize = mf.sharedMesh.bounds.size;
        else
        {
            BoxCollider bc = prefab.GetComponentInChildren<BoxCollider>();
            if (bc != null) rawSize = bc.size;
        }

        // Normalize so the result is always (width, height, length):
        // Assume Y is height (vertical), and between X and Z, pick the larger as length.
        // This handles cases where the mesh was authored with different orientations.
        float xz_min = Mathf.Min(rawSize.x, rawSize.z);
        float xz_max = Mathf.Max(rawSize.x, rawSize.z);
        return new Vector3(xz_min, rawSize.y, xz_max);  // (width, height, length)
    }

    /// <summary>Returns the Y-offset of the case prefab's mesh center from its prefab origin.
    /// Used to correct cases where the mesh is authored with bottom-origin (offset > 0, mesh sits
    /// above origin → would clip into pallet) or top-origin (offset < 0, mesh sits below origin
    /// → would float above correct height). Most cases have center-origin (offset ≈ 0).</summary>
    public static float GetMeshYOffset(GameObject prefab)
    {
        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            return mf.sharedMesh.bounds.center.y;
        return 0f;
    }
}
