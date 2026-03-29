using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UIElements;

public class PlacementController : MonoBehaviour
{
    // References assigned in Inspector
    [SerializeField] private PreviewController _preview;
    [SerializeField] private PlacementValidator _validator;
    [SerializeField] private PlacementFinalizer _finalizer;
    [SerializeField] private PlacementGrid _grid;

    private PlacementActions _actions;   // Created in Awake
    private StateMachine _fsm;

    // States
    private IdleState _idle;
    private BuildState _build;
    private DeleteState _delete;

    private void Awake()
    {
        _actions = new PlacementActions();   // Required
        _fsm = new StateMachine();
    }

    private void Start()
    {
        // Instantiate states here
        _idle = new IdleState();
        _build = new BuildState(_actions, _preview, _validator, _finalizer, _grid, _fsm);
        _delete = new DeleteState(_actions, _finalizer, _fsm);

        _fsm.ChangeState(_idle);
    }

    private void OnEnable()
    {
        _actions.Enable();
        // Hook input events here later
    }

    private void OnDisable()
    {
        _actions.Disable();
    }

    private void Update()
    {
        _fsm.Tick();
    }
}
