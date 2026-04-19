using UnityEngine;

public class PlacementController : MonoBehaviour
{
    [SerializeField] private PlacementStateMachine _fsm;
    [SerializeField] private BuildMenuUI _buildMenuUI;

    private void Awake()
    {
        // UI → FSM transitions
        _buildMenuUI.OnBuildItemClicked += HandleBuildItemClicked;
        _buildMenuUI.OnDeleteClicked += HandleDeleteClicked;
        _buildMenuUI.OnMoveClicked += HandleMoveClicked;
        _buildMenuUI.OnUndoClicked += HandleUndoClicked;
        _buildMenuUI.OnRedoClicked += HandleRedoClicked;
    }

    private void HandleBuildBarReady()
    {
        _fsm.EnterIdle();
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




