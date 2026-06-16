using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallVisibilityManager : MonoBehaviour
{
    public static WallVisibilityManager Instance { get; private set; }

    [Header("Visibility Settings")]
    [Tooltip("The height of the foundation/floor surface.")]
    [SerializeField] private float foundationHeight = 1.06f;

    [Tooltip("The amount of wall that stays visible above the foundation in 'Cut' mode.")]
    [SerializeField] private float cutHeight = 1.25f;

    [Tooltip("How long the slide animation takes (seconds).")]
    [SerializeField] private float slideDuration = 2.0f;

    [Header("Audio")]
    [Tooltip("The sound played while walls are sliding.")]
    [SerializeField] private AudioClip slideSound;
    private AudioSource _audioSource;

    [Header("Material Settings")]
    [Tooltip("The transparent material used when walls are lowered.")]
    [SerializeField] private Material loweredMaterial;

    [Tooltip("The normal material used when walls are raised.")]
    [SerializeField] private Material raisedMaterial;

    [Header("Layer Configuration")]
    [Tooltip("The layer assigned to your main wall objects.")]
    [SerializeField] private LayerMask wallLayer;

    [Tooltip("Include layers (Props/Decor) for objects sitting on top of walls that should vanish.")]
    [SerializeField] private LayerMask overlappingDecorLayers;

    private List<WallData> _trackedWalls = new();
    private List<Renderer> _dynamicallyFoundDecor = new();
    private WallVisibilityMode _currentMode = WallVisibilityMode.Full;
    private Coroutine _activeSlideCoroutine;

    public enum WallVisibilityMode { Full, Cut, Hidden }

    private class WallData
    {
        public Transform transform;
        public List<Renderer> renderers;
        public List<Material[]> originalMaterials; 
        public Vector3 originalPosition;
        public float originalWorldTopY;
        public Collider mainCollider;
        public bool isInitialized = false;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Ensure AudioSource exists on the manager's GameObject
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            _audioSource.spatialBlend = 0f; // 2D sound for global feedback
        }
    }

    private void Start() => RefreshWallList();

    public void RefreshWallList()
    {
        _trackedWalls.RemoveAll(w => w.transform == null);

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (((1 << obj.layer) & wallLayer.value) != 0)
            {
                if (obj.transform.parent != null && ((1 << obj.transform.parent.gameObject.layer) & wallLayer.value) != 0)
                    continue;

                WallData existing = _trackedWalls.Find(w => w.transform == obj.transform);
                if (existing == null)
                {
                    Renderer[] rends = obj.GetComponentsInChildren<Renderer>(true);
                    if (rends.Length == 0) continue;

                    Collider col = obj.GetComponent<Collider>() ?? obj.GetComponentInChildren<Collider>();
                    WallData data = new WallData { transform = obj.transform, renderers = new List<Renderer>(rends), mainCollider = col };
                    InitializeOriginalState(data);
                    _trackedWalls.Add(data);
                }
            }
        }
    }

    private void InitializeOriginalState(WallData wall)
    {
        if (wall.isInitialized) return;
        wall.originalPosition = wall.transform.position;
        wall.originalMaterials = new List<Material[]>();

        Bounds b = new Bounds();
        bool set = false;
        foreach (var r in wall.renderers)
        {
            if (r == null) continue;

            wall.originalMaterials.Add(r.sharedMaterials);

            if (!set) { b = r.bounds; set = true; }
            else b.Encapsulate(r.bounds);
        }
        wall.originalWorldTopY = set ? b.max.y : wall.transform.position.y;
        wall.isInitialized = true;
    }

    public void SetVisibilityMode(WallVisibilityMode mode)
    {
        if (_currentMode == mode) return;
        _currentMode = mode;
        RefreshWallList();

        if (_activeSlideCoroutine != null) StopCoroutine(_activeSlideCoroutine);
        _activeSlideCoroutine = StartCoroutine(SlideWallsRoutine());
    }

    public WallVisibilityMode CurrentMode => _currentMode;

    // Three-stage stepping: Full → Cut → Hidden (down) and back (up). Clamped at the ends,
    // so it takes two "down" clicks to go all the way down and two "up" to come all the way up.
    public void StepDown() => SetVisibilityMode((WallVisibilityMode)Mathf.Min((int)_currentMode + 1, (int)WallVisibilityMode.Hidden));
    public void StepUp()   => SetVisibilityMode((WallVisibilityMode)Mathf.Max((int)_currentMode - 1, (int)WallVisibilityMode.Full));

    private IEnumerator SlideWallsRoutine()
    {
        if (_audioSource != null && slideSound != null)
        {
            _audioSource.clip = slideSound;
            _audioSource.Play();
        }

        Dictionary<WallData, Vector3> startPositions = new Dictionary<WallData, Vector3>();
        Dictionary<WallData, Vector3> endPositions = new Dictionary<WallData, Vector3>();
        float targetTopY = foundationHeight + cutHeight;

        foreach (var wall in _trackedWalls)
        {
            if (wall.transform == null) continue;
            startPositions[wall] = wall.transform.position;

            Vector3 targetPos = wall.originalPosition;
            if (_currentMode == WallVisibilityMode.Cut && wall.originalWorldTopY > targetTopY)
            {
                float delta = targetTopY - wall.originalWorldTopY;
                targetPos = new Vector3(wall.originalPosition.x, wall.originalPosition.y + delta, wall.originalPosition.z);
            }
            endPositions[wall] = targetPos;

            if (_currentMode == WallVisibilityMode.Full)
            {
                SetRenderersEnabled(wall.renderers, true);
                SetWallMaterials(wall, false);
                foreach (var decor in _dynamicallyFoundDecor) if (decor != null) decor.enabled = true;
            }
        }

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);

            foreach (var wall in _trackedWalls)
            {
                if (wall.transform == null) continue;
                wall.transform.position = Vector3.Lerp(startPositions[wall], endPositions[wall], t);
            }
            yield return null;
        }

        foreach (var wall in _trackedWalls)
        {
            if (wall.transform == null) continue;
            wall.transform.position = endPositions[wall];

            if (_currentMode == WallVisibilityMode.Cut)
                SetWallMaterials(wall, true);
        }

        if (_currentMode == WallVisibilityMode.Full) _dynamicallyFoundDecor.Clear();
        if (_audioSource != null) _audioSource.Stop();
        _activeSlideCoroutine = null;
    }

    private void SetWallMaterials(WallData wall, bool useLowered)
    {
        if (wall == null || wall.renderers == null) return;

        for (int i = 0; i < wall.renderers.Count; i++)
        {
            Renderer r = wall.renderers[i];
            if (r == null) continue;

            if (useLowered && loweredMaterial != null)
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int j = 0; j < mats.Length; j++) mats[j] = loweredMaterial;
                r.sharedMaterials = mats;
            }
            else
            {
                if (i < wall.originalMaterials.Count)
                {
                    r.sharedMaterials = wall.originalMaterials[i];
                }
                else if (raisedMaterial != null)
                {
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int j = 0; j < mats.Length; j++) mats[j] = raisedMaterial;
                    r.sharedMaterials = mats;
                }
            }
        }
    }

    private void SetRenderersEnabled(List<Renderer> rends, bool state)
    {
        foreach (var r in rends) if (r != null) r.enabled = state;
    }

    private void HideFloatingObjectsAbove(Collider wallCollider)
    {
        Bounds b = wallCollider.bounds;
        Vector3 scanCenter = new Vector3(b.center.x, b.max.y + 2.5f, b.center.z);
        Vector3 scanHalfExtents = new Vector3(b.extents.x * 0.95f, 2.5f, b.extents.z * 0.95f);
        Collider[] hits = Physics.OverlapBox(scanCenter, scanHalfExtents, wallCollider.transform.rotation, overlappingDecorLayers);

        foreach (var hit in hits)
        {
            if (hit.gameObject == wallCollider.gameObject || hit.transform.IsChildOf(wallCollider.transform)) continue;
            Renderer r = hit.GetComponent<Renderer>() ?? hit.GetComponentInChildren<Renderer>();
            if (r != null && r.enabled) { r.enabled = false; _dynamicallyFoundDecor.Add(r); }
        }
    }

    public LayerMask GetDynamicPlacementMask(LayerMask defaultBaseMask) => _currentMode == WallVisibilityMode.Full ? defaultBaseMask : (defaultBaseMask & ~wallLayer);
}