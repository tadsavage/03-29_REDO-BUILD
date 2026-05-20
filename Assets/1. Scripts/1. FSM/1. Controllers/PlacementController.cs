using UnityEngine;

public class PlacementController : MonoBehaviour
{
    [SerializeField] private PlacementStateMachine _fsm;
    [SerializeField] private BuildMenuUI _buildMenuUI;

    private PlacementActions _actions;

    private void Awake()
    {
        _actions = new PlacementActions();
        
        if (_buildMenuUI != null)
        {
            _buildMenuUI.OnBuildItemClicked += HandleBuildButtonClicked;
            _buildMenuUI.OnDeleteClicked += HandleDeleteClicked;
            _buildMenuUI.OnMoveClicked += HandleMoveClicked;
            _buildMenuUI.OnUndoClicked += HandleUndoClicked;
            _buildMenuUI.OnRedoClicked += HandleRedoClicked;
        }
    }

    private void OnEnable()
    {
        _actions?.Enable();
    }

    private void OnDisable()
    {
        _actions?.Disable();
    }

    private void HandleBuildButtonClicked(ObjDataSO data)
    {
        AudioManager.Play("ButtonClick");
        _fsm.EnterBuild(data);
    }

    private void HandleDeleteClicked()
    {
        _fsm.EnterDelete();
    }

    private void HandleMoveClicked()
    {
        _fsm.EnterMove();
    }

    private void HandleUndoClicked()
    {
        _fsm.Undo();
    }

    private void HandleRedoClicked()
    {
        _fsm.Redo();
    }
}



