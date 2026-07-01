using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

/// <summary>
/// Handles chevron interaction:
/// - Right-click flips this chevron's SIDE (both chevrons on that side flip together
///   and stay synchronized — a side represents one face of the aisle).
/// - Any click marks this chevron as the collection's selected (green) chevron.
/// - Double-click opens RackSetupUI for this chevron's collection.
/// </summary>
public class ChevronController : MonoBehaviour
{
    private RackCollection _collection;
    private float _currentRotation = 0f; // 0 or 180 — travel-direction flip for this side
    // Flat resting orientation the spawner assigned (points down the run). Flipping
    // spins 180° about the vertical axis FROM this base so the chevron stays flat.
    private Quaternion _baseRotation = Quaternion.Euler(90f, 0f, 0f);
    private float _lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;
    private bool _isSelected = false;

    // The chevrons on the SAME side of the collection (includes this one). They flip
    // together so a side stays synchronized.
    private List<ChevronController> _sideTeam = new();
    // Collection-wide selection group — only one side-team per collection is green.
    private ChevronGroup _group;
    private string _sideKey; // which side this chevron belongs to ("neg"/"pos")

    private Material _normalMat;
    private Material _selectedMat;

    // Tracks per-chevron hover so we only call Show/Hide on the tooltip on transitions,
    // not every frame — avoids re-positioning fights between multiple chevrons.
    private bool _wasHovered;

    public event Action<ChevronController> OnChevronSelected;

    public RackCollection Collection => _collection;
    public float CurrentRotation => _currentRotation;

    public void Initialize(RackCollection collection)
    {
        _collection = collection;
        _baseRotation = transform.rotation;
    }

    /// <summary>
    /// Sets the flat resting orientation (which way the chevron points down the aisle),
    /// re-applying the player's current flip so a live re-facing keeps their choice.
    /// Called by ChevronSpawner whenever the collection's run axis is (re)computed.
    /// </summary>
    public void SetBaseFacing(Quaternion baseRotation)
    {
        _baseRotation = baseRotation;
        transform.rotation = Quaternion.AngleAxis(_currentRotation, Vector3.up) * _baseRotation;
    }

    public void SetSideTeam(List<ChevronController> team) => _sideTeam = team;
    public void SetGroup(ChevronGroup group) => _group = group;
    public void SetSideKey(string key) => _sideKey = key;

    public void SetSelectionMaterials(Material normal, Material selected)
    {
        _normalMat = normal;
        _selectedMat = selected;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && !_isSelected && _normalMat != null)
            sr.material = _normalMat;
    }

    private void Update()
    {
        bool hovered = IsPointerOverChevron();

        // Tooltip on hover — "R-click to change dir. Dbl.-click to enter Aisle setup"
        if (hovered && !_wasHovered)
            ChevronTooltipUI.Ensure().Show(Mouse.current.position.ReadValue());
        else if (!hovered && _wasHovered)
            ChevronTooltipUI.Ensure().Hide();
        _wasHovered = hovered;

        if (!hovered) return;

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            RotateSide();
            Select();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            Select();
            HandleDoubleClick();
        }
    }

    private void OnDisable()
    {
        // Prevent a stale tooltip if this chevron is destroyed mid-hover (e.g. on commit).
        if (_wasHovered)
        {
            var t = ChevronTooltipUI.Ensure();
            if (t != null) t.Hide();
            _wasHovered = false;
        }
    }

    private bool IsPointerOverChevron()
    {
        if (Camera.main == null) return false;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        return Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == gameObject;
    }

    /// <summary>
    /// Flip the whole side (both chevrons) to the same new direction so they stay synced.
    /// </summary>
    private void RotateSide()
    {
        float newRot = (_currentRotation == 0f) ? 180f : 0f;

        if (_sideTeam != null && _sideTeam.Count > 0)
        {
            foreach (var mate in _sideTeam)
                if (mate != null) mate.ApplyRotation(newRot);
        }
        else
        {
            ApplyRotation(newRot);
        }

        AudioManager.Play("Rotate");
    }

    public void ApplyRotation(float rot)
    {
        _currentRotation = rot;
        transform.rotation = Quaternion.AngleAxis(_currentRotation, Vector3.up) * _baseRotation;
    }

    /// <summary>Marks this chevron's whole side as the collection's active (green) selection.</summary>
    private void Select()
    {
        _group?.SelectSideKey(_sideKey);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;

        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (selected && _selectedMat != null) sr.material = _selectedMat;
        else if (!selected && _normalMat != null) sr.material = _normalMat;
    }

    private void HandleDoubleClick()
    {
        float timeSinceLastClick = Time.time - _lastClickTime;

        if (timeSinceLastClick < DOUBLE_CLICK_THRESHOLD)
            OpenSetup();

        _lastClickTime = Time.time;
    }

    private void OpenSetup()
    {
        AudioManager.Play("ValidPlace");
        OnChevronSelected?.Invoke(this);

        // Include inactive: modal is hidden between openings, and default FindObjectOfType
        // skips inactive GOs — that's the bug that made double-click "do nothing".
        var setupUI = FindFirstObjectByType<RackSetupUI>(FindObjectsInactive.Include);
        if (setupUI != null)
        {
            setupUI.SetSelectedChevron(this);
            setupUI.Open();
        }
        else
        {
            Debug.LogError("ChevronController.OpenSetup: no RackSetupUI found in scene. RackingSystemManager should auto-create one on Awake.");
        }
    }
}
