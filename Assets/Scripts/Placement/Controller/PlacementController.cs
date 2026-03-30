using UnityEngine;

public class PlacementController : MonoBehaviour
{
    [SerializeField] private PlacementStateMachine _fsm;

    [SerializeField] private PreviewController _preview;
    [SerializeField] private PlacementValidator _validator;
    [SerializeField] private PlacementFinalizer _finalizer;
    [SerializeField] private PlacementGrid _grid;

    private PlacementActions _actions;

    private void Awake()
    {
        _actions = new PlacementActions();
    }

    private void Start()
    {
        // No state creation here anymore.
        // The FSM will own Idle, Build, Delete, Raycast, etc.
    }

    private void OnEnable()
    {
        _actions.Enable();
    }

    private void OnDisable()
    {
        _actions.Disable();
    }
}



