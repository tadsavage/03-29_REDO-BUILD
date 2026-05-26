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
        public Vector3 originalPosition;
        public float originalWorldTopY;
        public Collider mainCollider;
        public bool isInitialized = false;
        public bool isPersistent = false;
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

        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
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
                    
                    bool isPersistent = obj.name.ToLower().Contains("corner") || obj.name.ToLower().Contains("mandoor");
                    
                    WallData data = new WallData 
                    { 
                        transform = obj.transform, 
                        renderers = new List<Renderer>(rends), 
                        mainCollider = col,
                        isPersistent = isPersistent
                    };
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

        Bounds b = new Bounds();
        bool set = false;
        foreach (var r in wall.renderers)
        {
            if (r == null) continue;
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
                // Optionally: Keep persistent objects at full height even in Cut mode?
                // The user only complained about renderers being disabled, so we'll keep the slide for now.
                float delta = targetTopY - wall.originalWorldTopY;
                targetPos = new Vector3(wall.originalPosition.x, wall.originalPosition.y + delta, wall.originalPosition.z);
            }
            endPositions[wall] = targetPos;

            if (_currentMode == WallVisibilityMode.Full)
            {
                SetRenderersEnabled(wall.renderers, true);
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

            // Do not disable renderers for persistent walls (Corners, ManDoors)
            if (_currentMode == WallVisibilityMode.Hidden && !wall.isPersistent) 
                SetRenderersEnabled(wall.renderers, false);
            
            if (_currentMode == WallVisibilityMode.Cut && wall.mainCollider != null) 
                HideFloatingObjectsAbove(wall.mainCollider);
        }

        if (_currentMode == WallVisibilityMode.Full) _dynamicallyFoundDecor.Clear();
        if (_audioSource != null) _audioSource.Stop();
        _activeSlideCoroutine = null;
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

            // Do not hide objects that are on the wall layer (they are handled by the wall sliding logic)
            if (((1 << hit.gameObject.layer) & wallLayer.value) != 0) continue;

            Renderer r = hit.GetComponent<Renderer>() ?? hit.GetComponentInChildren<Renderer>();
            if (r != null && r.enabled) { r.enabled = false; _dynamicallyFoundDecor.Add(r); }
        }
    }

    public LayerMask GetDynamicPlacementMask(LayerMask defaultBaseMask) => _currentMode == WallVisibilityMode.Full ? defaultBaseMask : (defaultBaseMask & ~wallLayer);
}