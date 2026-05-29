using UnityEngine;
using System.Collections.Generic;

public class PalletBuilder : MonoBehaviour
{
    [Header("Product Config")]
    public GameObject casePrefab;
    public float maxTotalHeight = 1.0f;

    [Header("Pallet Config")]
    public Vector3 palletDimensions = new Vector3(1.0f, 0.15f, 1.22f); // W, H, L

    [Header("Spacing Settings")]
    [Tooltip("Minimum horizontal distance between cases.")]
    public float spaceBetweenCases = 0.05f;
    [Tooltip("Fixed vertical gap between layers.")]
    public float verticalGap = 0.025f;

    [Header("Case Overrides")]
    public bool usePrefabBounds = true;
    public Vector3 caseDimensions = new Vector3(0.5f, 0.25f, 0.24f); // W, H, L

    [Header("Aesthetic Settings")]
    [Tooltip("Random Y rotation variation for a realistic look.")]
    public float crookedCase = 2.0f;

    [Header("Overrides (Manual Ti-Hi)")]
    public bool useTiHiOverride = false;
    public int manualTi = 6;
    public int manualHi = 3;

    [Header("Results (Read Only)")]
    [SerializeField] private int casesPerLayer;
    [SerializeField] private int layers;
    [SerializeField] private int totalCases;
    public int CurrentLoadCost { get; private set; }

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

    private struct CasePlacement
    {
        public Vector3 position;
        public float rotation;
    }

    private List<CasePlacement> _bestLayerPattern = new List<CasePlacement>();


    private void Start()
    {
        _moneyService = FindAnyObjectByType<GameContext>()?.MoneyService;
        _placedObject = GetComponent<PlacedObject>();

        // Load state if not already loaded by external system
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
            Debug.Log($"PalletBuilder: No custom build data on {_placedObject.name}");
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
            float yPos = palletDim.y + (h * (caseDim.y + verticalGap));
            
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
        ToggleUI();
    }

    private void CalculateBestLayer(float pW, float pL, float cW, float cL)
    {
        _bestLayerPattern.Clear();
        float g = spaceBetweenCases;

        // Strategy 1: Uniform Orientation A (cW || pW)
        List<CasePlacement> patternA = GetUniformPattern(pW, pL, cW, cL, 0f, g);
        
        // Strategy 2: Uniform Orientation B (cL || pW)
        List<CasePlacement> patternB = GetUniformPattern(pW, pL, cL, cW, 90f, g);

        // Strategy 3: Split Block (Lengthwise split)
        List<CasePlacement> patternC = GetSplitPattern(pW, pL, cW, cL, true, g);

        // Strategy 4: Split Block (Widthwise split)
        List<CasePlacement> patternD = GetSplitPattern(pW, pL, cW, cL, false, g);

        // Pick best
        List<CasePlacement> best = patternA;
        if (patternB.Count > best.Count) best = patternB;
        if (patternC.Count > best.Count) best = patternC;
        if (patternD.Count > best.Count) best = patternD;

        _bestLayerPattern = best;
    }

    private List<CasePlacement> GetUniformPattern(float pW, float pL, float iW, float iL, float rot, float g)
    {
        List<CasePlacement> pattern = new List<CasePlacement>();
        int countW = Mathf.FloorToInt((pW + g) / (iW + g));
        int countL = Mathf.FloorToInt((pL + g) / (iL + g));

        if (countW <= 0 || countL <= 0) return pattern;

        float totalW = (countW * iW) + ((countW - 1) * g);
        float totalL = (countL * iL) + ((countL - 1) * g);
        float startX = -totalW / 2f + (iW / 2f);
        float startZ = -totalL / 2f + (iL / 2f);

        for (int l = 0; l < countL; l++)
        {
            for (int w = 0; w < countW; w++)
            {
                pattern.Add(new CasePlacement {
                    position = new Vector3(startX + w * (iW + g), 0, startZ + l * (iL + g)),
                    rotation = rot
                });
            }
        }
        return pattern;
    }

    private List<CasePlacement> GetSplitPattern(float pW, float pL, float cW, float cL, bool splitLength, float g)
    {
        List<CasePlacement> bestPattern = new List<CasePlacement>();
        
        float dimToSplit = splitLength ? pL : pW;
        float fixedDim = splitLength ? pW : pL;

        // Iterate through split points based on case dimensions
        // Try every possible row count for orientation 1
        int maxRows = Mathf.FloorToInt((dimToSplit + g) / (cL + g));
        
        for (int rowsA = 1; rowsA < maxRows; rowsA++)
        {
            float splitPoint = (rowsA * cL) + ((rowsA - 1) * g);
            float remaining = dimToSplit - splitPoint - g;
            
            if (remaining < cW) continue; // Must fit at least one sideways case

            List<CasePlacement> current = new List<CasePlacement>();
            
            // Block A: rowsA of cL cases
            if (splitLength)
                current.AddRange(GetUniformPattern(pW, splitPoint, cW, cL, 0f, g, -pL/2f + splitPoint/2f, true));
            else
                current.AddRange(GetUniformPattern(splitPoint, pL, cW, cL, 0f, g, -pW/2f + splitPoint/2f, false));

            // Block B: cases in 'remaining' dimension, oriented sideways
            if (splitLength)
                current.AddRange(GetUniformPattern(pW, remaining, cL, cW, 90f, g, pL/2f - remaining/2f, true));
            else
                current.AddRange(GetUniformPattern(remaining, pL, cL, cW, 90f, g, pW/2f - remaining/2f, false));

            if (current.Count > bestPattern.Count) bestPattern = current;
        }

        return bestPattern;
    }

    // Helper for split blocks that supports offset centers
    private List<CasePlacement> GetUniformPattern(float pW, float pL, float iW, float iL, float rot, float g, float offset, bool isOffsetL)
    {
        List<CasePlacement> pattern = new List<CasePlacement>();
        int countW = Mathf.FloorToInt((pW + g) / (iW + g));
        int countL = Mathf.FloorToInt((pL + g) / (iL + g));

        if (countW <= 0 || countL <= 0) return pattern;

        float totalW = (countW * iW) + ((countW - 1) * g);
        float totalL = (countL * iL) + ((countL - 1) * g);
        
        float startX = isOffsetL ? (-totalW / 2f + iW / 2f) : offset;
        float startZ = isOffsetL ? offset : (-totalL / 2f + iL / 2f);

        for (int l = 0; l < countL; l++)
        {
            for (int w = 0; w < countW; w++)
            {
                pattern.Add(new CasePlacement {
                    position = new Vector3(
                        isOffsetL ? startX + w * (iW + g) : startX, 
                        0, 
                        isOffsetL ? startZ : startZ + l * (iL + g)
                    ),
                    rotation = rot
                });
            }
        }
        return pattern;
    }

    private Vector3 GetPrefabDimensions(GameObject prefab)
    {
        MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.bounds.size;
        BoxCollider bc = prefab.GetComponentInChildren<BoxCollider>();
        if (bc != null) return bc.size;
        return new Vector3(1, 1, 1);
    }
}
