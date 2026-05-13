using UnityEngine;
using UnityEngine.UIElements;

public class UIBootstrapper : MonoBehaviour
{
    [Header("UI Documents")]
    [SerializeField] private UIDocument _hudDocument;
    [SerializeField] private UIDocument _buildMenuDocument;

    [Header("UI Controllers")]
    [SerializeField] private PreviewCostUI _costUI;
    [SerializeField] private BuildMenuUI _buildMenuUI;
    [SerializeField] private TopBarUI _topBarUI;
    [SerializeField] private WorldHoverPopupUI _hoverUI;

    [Header("Game Services")]
    [SerializeField] private GameContext _context;

    [Header("State Machine")]
    [SerializeField] private PlacementStateMachine _fsm;

    private void Awake()
    {
        InitializeHUD();
        InitializeBuildMenu();
    }

    private void InitializeHUD()
    {
        if (_hudDocument == null)
        {
            // Try to find HUD in scene if not assigned
            UIDocument[] docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var d in docs)
            {
                if (d.rootVisualElement != null && d.rootVisualElement.Q("WorldHoverPopup") != null)
                {
                    _hudDocument = d;
                    break;
                }
            }
        }

        if (_hudDocument == null)
        {
            Debug.LogError("[UIBootstrapper] HUD Document is missing!");
            return;
        }

        // Preview cost UI
        if (_costUI != null)
            _costUI.Init(_hudDocument);

        // Top bar UI
        if (_topBarUI != null)
            _topBarUI.Init(_hudDocument, _context.MoneyService, _context.TimeService);

        // Hover popup UI
        if (_hoverUI != null)
        {
            _hoverUI.Init(_hudDocument);
            _hoverUI.SetFSM(_fsm);
            _fsm.SetHoverUI(_hoverUI);
        }
    }

    private void InitializeBuildMenu()
    {
        if (_buildMenuDocument == null)
        {
             // Try to find BottomBar in scene if not assigned
            UIDocument[] docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var d in docs)
            {
                if (d.rootVisualElement != null && d.rootVisualElement.Q("BottomBar") != null)
                {
                    _buildMenuDocument = d;
                    break;
                }
            }
        }

        if (_buildMenuDocument == null || _buildMenuUI == null)
        {
            Debug.LogError("[UIBootstrapper] Build Menu Document or UI is missing!");
            return;
        }

        // 1. Initialize with money service
        _buildMenuUI.Initialize(_context.MoneyService);

        // 2. Wire up all UI events to the FSM
        _buildMenuUI.OnBuildItemClicked += _fsm.EnterBuild;
        _buildMenuUI.OnDeleteClicked += _fsm.EnterDelete;
        _buildMenuUI.OnMoveClicked += _fsm.EnterMove;
        _buildMenuUI.OnUndoClicked += _fsm.Undo;
        _buildMenuUI.OnRedoClicked += _fsm.Redo;
        _buildMenuUI.OnCancelClicked += _fsm.ReturnToPrevious;
        _buildMenuUI.OnRotateClicked += () => {
            // This event might need to be passed to current state if it supports rotation
            // But Build/Move handle their own R key. This is for the UI button.
            //if (_fsm.CurrentState is BuildState bs) bs.OnRotatePerformed(); 
        };
    }
}
