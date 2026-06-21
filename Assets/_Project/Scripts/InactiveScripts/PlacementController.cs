using UnityEngine;

public class PlacementController : MonoBehaviour
{
    [SerializeField] private PlacementStateMachine _fsm;
    [SerializeField] private GameContext _gameContext;
    [SerializeField] private BuildMenuUI _buildMenuUI;

    private void Awake()
    {
        if (_buildMenuUI == null)
        {
            Debug.LogError("[PlacementController] _buildMenuUI is not assigned. FSM will not receive UI events.");
            return;
        }

        // UI → FSM transitions
        _buildMenuUI.OnBuildItemClicked += HandleBuildItemClicked;
        _buildMenuUI.OnDeleteClicked += HandleDeleteClicked;
        _buildMenuUI.OnMoveClicked += HandleMoveClicked;
        _buildMenuUI.OnUndoClicked += HandleUndoClicked;
        _buildMenuUI.OnRedoClicked += HandleRedoClicked;

        // Inject the real scene GameContext in Awake to ensure it's ready for FSM.Start()
        _fsm.Initialize(_gameContext);
    }

    private void HandleBuildItemClicked(ObjDataSO data)
    {
        AudioManager.Play("ButtonClick");
        _fsm.EnterBuild(data);
    }

    private void HandleDeleteClicked()
    {
        AudioManager.Play("ButtonClick");
        _fsm.EnterDelete();
    }

    private void HandleMoveClicked()
    {
        AudioManager.Play("ButtonClick");
        _fsm.EnterMove();
    }

    private void HandleUndoClicked()
    {
        AudioManager.Play("ButtonClick");
        _fsm.Undo();
    }

    private void HandleRedoClicked()
    {
        AudioManager.Play("ButtonClick");
        _fsm.Redo();
    }
}
