using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Controls the Employee Photo Booth system. Intercepts hiring of employees,
/// instantiates the selected role/gender low-poly model in the photo booth,
/// takes a waist-up studio portrait using a dedicated camera and spotlights,
/// and stores the sprite in memory so all game UIs display it automatically.
/// </summary>
public class EmployeePhotoBooth : MonoBehaviour
{
    public static EmployeePhotoBooth Instance { get; private set; }

    [Header("Studio Prefabs")]
    [SerializeField] private GameObject _workerMalePrefab;
    [SerializeField] private GameObject _workerFemalePrefab;
    [SerializeField] private GameObject _bossPrefab;
    [SerializeField] private GameObject _securityPrefab;
    [SerializeField] private GameObject _clerkPrefab;
    [SerializeField] private GameObject _exterminatorPrefab;
    [SerializeField] private GameObject _truckDriverPrefab;

    [Header("Studio Setup")]
    [Tooltip("Clear color of the camera (backdrop color).")]
    [SerializeField] private Color _backdropColor = new Color(0.322f, 0.419f, 0.401f, 1.0f);
    [SerializeField] private float _studioLightIntensity = 0.75f;

    // Runtime cache for custom portrait sprites in memory
    public static readonly Dictionary<string, Sprite> CustomAvatarCache = new Dictionary<string, Sprite>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Pre-load existing custom portraits from disk so they survive game restarts/saves
        LoadAllPortraitsFromDisk();
    }

    private void Start()
    {
        // Subscribe to hire event in Start to guarantee all Awake methods have run
        if (EmployeeLifecycleService.Instance != null)
        {
            EmployeeLifecycleService.Instance.OnHired += OnEmployeeHired;
        }
        else
        {
            Debug.LogError("[EmployeePhotoBooth] EmployeeLifecycleService.Instance is null in Start!");
        }
    }

    private void OnDestroy()
    {
        if (EmployeeLifecycleService.Instance != null)
        {
            EmployeeLifecycleService.Instance.OnHired -= OnEmployeeHired;
        }
    }

    private void OnEmployeeHired(EmployeeRecord record)
    {
        if (record == null) return;

        // 1. Determine model and force matching gender
        GameObject prefab = GetPrefabForRoleAndGender(record.role, record.gender, out EmployeeGender finalGender);
        record.gender = finalGender;

        if (prefab == null)
        {
            Debug.LogWarning($"[EmployeePhotoBooth] No prefab found for role={record.role}, gender={record.gender}. Using WorkerMale as default.");
            prefab = _workerMalePrefab;
        }

        // 2. Capture and save portrait
        CapturePortrait(record, prefab);
    }

    private GameObject GetPrefabForRoleAndGender(EmployeeRole role, EmployeeGender gender, out EmployeeGender finalGender)
    {
        // Default to Male for specific male-only model roles
        finalGender = EmployeeGender.Male;

        switch (role)
        {
            case EmployeeRole.Boss:
                return _bossPrefab;

            case EmployeeRole.Security:
                return _securityPrefab;

            case EmployeeRole.InventoryControl:
                finalGender = EmployeeGender.Female; // clerk model is female
                return _clerkPrefab;

            case EmployeeRole.Exterminator:
                return _exterminatorPrefab;

            case EmployeeRole.TruckDriver:
                return _truckDriverPrefab;

            default:
                // Floor workers, supervisor, etc.
                if (gender == EmployeeGender.Female)
                {
                    finalGender = EmployeeGender.Female;
                    return _workerFemalePrefab;
                }
                else
                {
                    finalGender = EmployeeGender.Male;
                    return _workerMalePrefab;
                }
        }
    }

    private void CapturePortrait(EmployeeRecord record, GameObject prefab)
    {
        if (prefab == null) return;

        // 1. Instantiate temporary model at photo booth origin
        GameObject modelInstance = Instantiate(prefab, transform);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.Euler(0, 90, 0);
        modelInstance.name = $"PhotoBooth_Temp_{record.employeeName}";

        // Ensure low-poly character has settled its pose/anim
        Animator animator = modelInstance.GetComponent<Animator>();
        if (animator != null)
        {
            animator.Update(1.0f);
        }

        // Disable standard components that might roam or register with gameplay
        var agent = modelInstance.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        var identity = modelInstance.GetComponent<EmployeeIdentity>();
        if (identity != null) identity.enabled = false;

        // 2. Spawn temporary camera for passport driver/license style portrait (waist up)
        GameObject camGO = new GameObject("PhotoBooth_Camera");
        camGO.transform.SetParent(transform);
        camGO.transform.localPosition = new Vector3(2.0f, 1.45f, 0f); // Match camera preview position
        camGO.transform.LookAt(modelInstance.transform.position + new Vector3(0f, 1.35f, 0f));

        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = _backdropColor;
        cam.fieldOfView = 26f; // Match camera preview FOV
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 10f;

        // Solve lighting issue: Create a nice Point light to get shadows/angles on low poly polygons
        GameObject lightGO = new GameObject("PhotoBooth_Light");
        lightGO.transform.SetParent(transform);
        // Positioned close and slightly to the side/front to create high-contrast shadows on low-poly edges
        lightGO.transform.localPosition = new Vector3(0.10f, 1.38f, 0.81f);
        
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = _studioLightIntensity;
        light.range = 2.0f;
        light.color = new Color(0.872f, 0.857f, 0.834f, 1.0f);
        light.shadows = LightShadows.Hard; // Hard shadows for low poly look
        lightGO.transform.LookAt(modelInstance.transform.position + new Vector3(0f, 1.4f, 0f));

        // 3. Render to temporary texture
        RenderTexture rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        // 4. Read back pixels to Texture2D
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        // 5. Create Sprite
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));

        // 6. Cache it
        string customKey = "Custom_" + record.employeeGuid;
        CustomAvatarCache[customKey] = sprite;
        record.avatarResourceKey = customKey;

        // 7. Save to disk so saves can load it
        byte[] pngBytes = tex.EncodeToPNG();
        SavePortraitToDisk(record.employeeGuid, pngBytes);

        // 8. Clean up temporary objects immediately
        Destroy(modelInstance);
        Destroy(camGO);
        Destroy(lightGO);
        rt.Release();
        Destroy(rt);

        Debug.Log($"[EmployeePhotoBooth] Successfully captured studio portrait for {record.employeeName} ({record.role})");
    }

    private void SavePortraitToDisk(string guid, byte[] pngBytes)
    {
        try
        {
            string buildDir = Path.Combine(Application.persistentDataPath, "Portraits");
            Directory.CreateDirectory(buildDir);
            string buildPath = Path.Combine(buildDir, guid + ".png");
            File.WriteAllBytes(buildPath, pngBytes);

#if UNITY_EDITOR
            string editorDir = Path.Combine(Application.dataPath, "Sprites/Portraits");
            Directory.CreateDirectory(editorDir);
            string editorPath = Path.Combine(editorDir, guid + ".png");
            File.WriteAllBytes(editorPath, pngBytes);
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EmployeePhotoBooth] Failed to save portrait to disk: {ex.Message}");
        }
    }

    private void LoadAllPortraitsFromDisk()
    {
        try
        {
            // Load from persistent data path (standalone build/persistent state)
            string buildDir = Path.Combine(Application.persistentDataPath, "Portraits");
            if (Directory.Exists(buildDir))
            {
                foreach (string file in Directory.GetFiles(buildDir, "*.png"))
                {
                    string guid = Path.GetFileNameWithoutExtension(file);
                    byte[] bytes = File.ReadAllBytes(file);
                    Texture2D tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                    tex.LoadImage(bytes);
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    CustomAvatarCache["Custom_" + guid] = sprite;
                }
            }

#if UNITY_EDITOR
            // Load from Editor path so it registers during development
            string editorDir = Path.Combine(Application.dataPath, "Sprites/Portraits");
            if (Directory.Exists(editorDir))
            {
                foreach (string file in Directory.GetFiles(editorDir, "*.png"))
                {
                    string guid = Path.GetFileNameWithoutExtension(file);
                    string key = "Custom_" + guid;
                    if (!CustomAvatarCache.ContainsKey(key))
                    {
                        byte[] bytes = File.ReadAllBytes(file);
                        Texture2D tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                        tex.LoadImage(bytes);
                        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        CustomAvatarCache[key] = sprite;
                    }
                }
            }
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"[EmployeePhotoBooth] Failed to load portraits from disk: {ex.Message}");
        }
    }
}
