using UnityEngine;

public class PlacementController : MonoBehaviour
{
    [SerializeField] private PlacementStateMachine _fsm;

    [SerializeField] private PreviewController _preview;
    [SerializeField] private PlacementValidator _validator;
    [SerializeField] private PlacementFinalizer _finalizer;
    [SerializeField] private PlacementGrid _grid;
    [SerializeField] private BuildBarBinder _buildBarBinder;

    private PlacementActions _actions;

    private void Awake()
    {
        _actions = new PlacementActions();
        Debug.Log("PlacementController using FSM instance: " + _fsm);
        // Listen for the UI being ready
        _buildBarBinder.OnBuildBarReady += HandleBuildBarReady;
        // Listen for button clicks on the Build Bar
        _buildBarBinder.OnBuildButtonClickedEvent += HandleBuildButtonClicked;
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

    private void HandleBuildBarReady()
    {
        _fsm.SetState(_fsm.IdleState);
    }
    private void HandleBuildButtonClicked(ObjDataSO data)
    {
        Debug.Log("CLICK HANDLER: Using FSM instance: " + _fsm);
        Debug.Log("CLICK HANDLER: Calling SetState(BuildState)");
        AudioManager.Play("ButtonClick");
        // Set the build data in the FSM so that RaycastState can access it
        _fsm.BuildState.SetBuildData(data);
        _fsm.SetState(_fsm.BuildState);
    }
}



