using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Handles chevron interaction:
/// - Right-click to rotate 180° around X-axis (flip direction)
/// - Double-click to select and open RackSetupUI
/// </summary>
public class ChevronController : MonoBehaviour
{
    private RackCollection _collection;
    private float _currentRotation = 0f; // 0 or 180 degrees
    private float _lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;
    private bool _isSelected = false;

    public event Action<ChevronController> OnChevronSelected;

    public RackCollection Collection => _collection;
    public float CurrentRotation => _currentRotation;

    public void Initialize(RackCollection collection)
    {
        _collection = collection;
    }

    private void Update()
    {
        if (!IsPointerOverChevron()) return;

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            RotateChevron();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            HandleDoubleClick();
        }
    }

    private bool IsPointerOverChevron()
    {
        if (Camera.main == null) return false;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        return Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == gameObject;
    }

    private void RotateChevron()
    {
        _currentRotation = (_currentRotation == 0f) ? 180f : 0f;
        transform.rotation = Quaternion.Euler(_currentRotation, 0f, 0f);
        AudioManager.Play("Rotate");
    }

    private void HandleDoubleClick()
    {
        float timeSinceLastClick = Time.time - _lastClickTime;

        if (timeSinceLastClick < DOUBLE_CLICK_THRESHOLD)
        {
            // Double-click detected
            SelectChevron();
        }

        _lastClickTime = Time.time;
    }

    private void SelectChevron()
    {
        _isSelected = true;
        AudioManager.Play("ValidPlace");
        OnChevronSelected?.Invoke(this);

        // Open RackSetupUI with this chevron's collection and rotation
        var setupUI = FindObjectOfType<RackSetupUI>();
        if (setupUI != null)
        {
            setupUI.SetSelectedChevron(this);
            setupUI.Open();
        }
    }

    public void Highlight()
    {
        var spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f); // Highlight to white
        }
    }

    public void Unhighlight()
    {
        var spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 1f, 1f, 0.7f); // Dim to 70% opacity
        }
    }
}
