using UnityEngine;

public class PlacementController : MonoBehaviour
{
    [SerializeField] private PlacementStateMachine _fsm;
    [SerializeField] private BuildBarUIController _buildBarUIController;

    private void Awake()
    {
        // UI → FSM transitions
        _buildBarUIController.OnBuildBarReady += HandleBuildBarReady;
        _buildBarUIController.OnBuildItemClicked += HandleBuildItemClicked;
        _buildBarUIController.OnDeleteClicked += HandleDeleteClicked;
        _buildBarUIController.OnMoveClicked += HandleMoveClicked;
        _buildBarUIController.OnUndoClicked += HandleUndoClicked;
        _buildBarUIController.OnRedoClicked += HandleRedoClicked;
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




