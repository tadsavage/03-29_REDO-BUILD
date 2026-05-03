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
            _fsm.SetHoverUI(_hoverUI);   // <-- THIS LINE FIXES EVERYTHING
        }

    }

    private void InitializeBuildMenu()
    {
        if (_buildMenuDocument == null)
        {
            Debug.LogError("[UIBootstrapper] Build Menu Document is missing!");
            return;
        }

        // BuildMenuUI initializes itself in OnEnable()
    }
}
