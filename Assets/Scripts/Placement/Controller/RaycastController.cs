using UnityEngine;
using UnityEngine.InputSystem;

public class RaycastController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PlacementGrid grid;
    [SerializeField] private PlacementStateMachine stateMachine;

    [SerializeField] private LayerMask groundMask;

    [Header("Ray Visualizer")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float rayLength = 100f;

    public bool HasHit { get; private set; }
    public Vector3 HitPoint { get; private set; }
    public Vector2Int HitCell { get; private set; }

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    // -------------------------
    // FSM-CONTROLLED METHODS
    // -------------------------

    public void EnableRay()
    {
        lineRenderer.enabled = true;
    }

    public void DisableRay()
    {
        lineRenderer.enabled = false;

        //Cursor.lockState = CursorLockMode.None;
        //Cursor.visible = true;

        HasHit = false;
    }

    public void Tick()
    {
        if (CheckForCancel())
            return;

        CastRay();
        UpdateRayVisualizer();
        UpdateGridCell();
    }

    // -------------------------
    // INTERNAL LOGIC
    // -------------------------

    private bool CheckForCancel()
    {
        if (!stateMachine.CurrentState.IsPlacementState)
            return false;

        if (Mouse.current.rightButton.isPressed || Keyboard.current.escapeKey.isPressed)
        {
            AudioManager.Play("Cancel");
            stateMachine.SetState(stateMachine.IdleState);

            DisableRay();
            return true;
        }

        return false;
    }

    private void CastRay()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, rayLength, groundMask))
        {
            HasHit = true;
            HitPoint = hit.point;
        }
        else
        {
            HasHit = false;
        }
    }

    private void UpdateRayVisualizer()
    {
        if (!lineRenderer) return;

        if (HasHit)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, mainCamera.transform.position - (mainCamera.transform.up * .01f) + (mainCamera.transform.forward * .01f));
            lineRenderer.SetPosition(1, HitPoint);
        }
        else
        {
            lineRenderer.enabled = false;
        }
    }

    private void UpdateGridCell()
    {
        if (!HasHit) return;

        HitCell = grid.WorldToCell(HitPoint);

    }
}

