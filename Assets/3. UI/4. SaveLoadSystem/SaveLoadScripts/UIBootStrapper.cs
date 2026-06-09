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
    [SerializeField] private EmployeeInfoUI _employeeInfoUI;
    [SerializeField] private SaveLoadSystem.SaveLoadWindowController _saveLoadController;

    [Header("Game Services")]
    [SerializeField] private GameContext _context;

    [Header("State Machine")]
    [SerializeField] private PlacementStateMachine _fsm;

    private void Awake()
    {
        // Delay initialization if splash screen is active
        SplashScreenController splash = GetComponent<SplashScreenController>();
        if (splash != null && splash.ShouldPlaySplash)
        {
            if (_fsm != null) _fsm.enabled = false;
            return;
        }

        InitializeAll();
    }

    public void InitializeAll()
    {
        InitializeHUD();
        InitializeBuildMenu();
        if (_fsm != null) _fsm.enabled = true;
    }

    private void InitializeHUD()
    {
        if (_hudDocument == null)
        {
            // FIX: Changed "WorldHoverPopup" query to "TopBar" so it identifies the HUD document correctly
            UIDocument[] docs = Object.FindObjectsByType<UIDocument>();
            foreach (var d in docs)
            {
                if (d.rootVisualElement != null && d.rootVisualElement.Q("TopBar") != null)
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

        // Root fills the screen — must be Ignore so it doesn't block game-world raycasts.
        _hudDocument.rootVisualElement.pickingMode = PickingMode.Ignore;

        // Preview cost UI
        if (_costUI != null) _costUI.Init(_hudDocument);

        // Top bar UI
        if (_topBarUI != null) _topBarUI.Init(_hudDocument, _context.MoneyService, _context.TimeService, _saveLoadController);

        // Employee info UI
        if (_employeeInfoUI != null) _employeeInfoUI.Init(_hudDocument);
    }

    private void InitializeBuildMenu()
    {
        if (_buildMenuDocument == null)
        {
            UIDocument[] docs = Object.FindObjectsByType<UIDocument>();
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

        // Root fills the screen — must be Ignore so PanelRaycaster doesn't block
        // game-world raycasts outside the actual build bar elements.
        _buildMenuDocument.rootVisualElement.pickingMode = PickingMode.Ignore;

        // 1. Initialize the build menu layout engine
        _buildMenuUI.Initialize(_context.MoneyService);

        // 2. Fetch or dynamically generate the stationed panel container box
        if (_hoverUI != null)
        {
            VisualElement stationedElement = _buildMenuUI.GetStationedPopup();

            if (stationedElement != null)
            {
                _hoverUI.Init(stationedElement);
                _hoverUI.SetFSM(_fsm);
                _fsm.SetHoverUI(_hoverUI);
            }
            // CLEANED UP: Wiped out the restrictive old UXML error trap block that was throwing the false alarm
        }

        // 3. Wire up remaining UI events
        _buildMenuUI.OnCancelClicked += _fsm.ReturnToPrevious;
        _buildMenuUI.OnRotateClicked += () => { };
    }

}
