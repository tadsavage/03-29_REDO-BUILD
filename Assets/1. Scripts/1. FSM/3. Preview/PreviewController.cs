using UnityEngine;

public class PreviewController : MonoBehaviour
{
    private GameObject _currentPreview;

    // Cached renderers + original materials
    private Renderer[] _renderers;
    private Material[] _originalMaterials;

    // Ghost materials
    [Header("Ghost Materials")]
    [SerializeField] private Material ghostValidMaterial;
    [SerializeField] private Material ghostInvalidMaterial;

    // Smoothing parameters
    [SerializeField] private float moveSmoothTime = 08f;
    private Vector3 _velocity;
    private Vector3 _targetPos;
    private bool _hasTarget;

    // Smoothly move preview towards target position
    private void Update()
    {
        if (_currentPreview == null || !_hasTarget)
            return;

        _currentPreview.transform.position =
            Vector3.SmoothDamp(
                _currentPreview.transform.position,
                _targetPos,
                ref _velocity,
                moveSmoothTime
            );
    }
    // ---------------------------------------------------------
    // CREATE PREVIEW
    // ---------------------------------------------------------
    public void Show(ObjDataSO data)
    {
        // Destroy old preview
        if (_currentPreview != null)
            Destroy(_currentPreview);

        // Spawn new preview
        _currentPreview = Instantiate(data.prefab);
        _currentPreview.SetActive(true);

        // Cache renderers
        _renderers = _currentPreview.GetComponentsInChildren<Renderer>();

        // Cache original materials
        _originalMaterials = new Material[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originalMaterials[i] = _renderers[i].material;
    }

    // ---------------------------------------------------------
    // POSITION + ROTATION
    // ---------------------------------------------------------
    public void MoveTo(Vector3 worldPos)
    {
        _targetPos = worldPos;
        _hasTarget = true;
    }

    public void Rotate(float angle)
    {
        if (_currentPreview != null)
            _currentPreview.transform.rotation = Quaternion.Euler(0f, angle, 0f);

        AudioManager.Play("Rotate");
    }

    // ---------------------------------------------------------
    // GHOST MATERIALS
    // ---------------------------------------------------------
    public void SetGhostValid()
    {
        if (_renderers == null) return;

        foreach (var r in _renderers)
            r.material = ghostValidMaterial;
    }

    public void SetGhostInvalid()
    {
        if (_renderers == null) return;

        foreach (var r in _renderers)
            r.material = ghostInvalidMaterial;
    }

    // ---------------------------------------------------------
    // RESTORE ORIGINAL MATERIALS
    // ---------------------------------------------------------
    public void RestoreMaterials()
    {
        if (_renderers == null || _originalMaterials == null)
            return;

        for (int i = 0; i < _renderers.Length; i++)
            _renderers[i].material = _originalMaterials[i];
    }

    // ---------------------------------------------------------
    // HIDE PREVIEW
    // ---------------------------------------------------------
    public void Hide()
    {
        if (_currentPreview != null)
            _currentPreview.SetActive(false);
    }
}
