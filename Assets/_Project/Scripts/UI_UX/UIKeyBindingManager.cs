using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages exclusive keybinding UIs (keys 1-7).
/// Only one keybind UI can be open at a time — toggling one closes any currently open one.
/// Panels must implement IUIPanel to be registered.
///
/// Keybindings:
///   1 = Dev Console (ToolsWindowController)
///   2 = Hiring Board (HiringBoardUI)
///   3 = Employee Roster (EmployeeRosterUI)
///   4 = Employee List (EmployeeListPanelController)
///   5 = Shift Manager (ShiftManagerPanel)
///   6 = Slot Assignment (SlotAssignmentPanel)
///   7 = Work Queue
///   9 = Wholesale Contracts (ContractsPanel)
///
/// Registration is also what makes Tab close a panel: PlacementStateMachine's Tab handler calls
/// CloseAll(), which only iterates this registry.
/// </summary>
public class UIKeyBindingManager : MonoBehaviour
{
    private static UIKeyBindingManager _instance;

    /// <summary>
    /// Self-creating singleton. It used to be assigned only in Awake, which made registration depend
    /// on script execution order: TopBarUI.Init runs before this component's Awake, so every panel it
    /// registers (5 Shift Manager, 7 Work Queue, 9 Contracts) hit a null Instance and was skipped
    /// without a word. The symptom was subtle — those panels still opened via their own key handling,
    /// but were absent from the registry, so CloseAll() (i.e. Tab) silently ignored them.
    ///
    /// Prefers an existing scene instance before creating one, so a manager placed in the scene that
    /// simply hasn't Awoken yet is adopted rather than duplicated.
    /// </summary>
    public static UIKeyBindingManager Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = FindFirstObjectByType<UIKeyBindingManager>();
            if (_instance == null)
            {
                var go = new GameObject("[UIKeyBindingManager]");
                _instance = go.AddComponent<UIKeyBindingManager>();
            }
            return _instance;
        }
        private set => _instance = value;
    }

    private Dictionary<int, IUIPanel> _uiPanels = new();
    private int _currentOpenKey = -1;  // -1 = none open

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    /// <summary>Register a UI panel for a keybinding number (1-9).</summary>
    public void RegisterUI(int keyNumber, IUIPanel panel)
    {
        if (keyNumber < 1 || keyNumber > 9)
        {
            Debug.LogError($"[UIKeyBindingManager] Invalid key number {keyNumber}. Must be 1-9.");
            return;
        }

        if (panel == null)
        {
            Debug.LogError($"[UIKeyBindingManager] Cannot register null panel for key {keyNumber}.");
            return;
        }

        _uiPanels[keyNumber] = panel;
        Debug.Log($"[UIKeyBindingManager] Registered {panel.GetType().Name} for key {keyNumber}");
    }

    /// <summary>Unregister a UI panel from its keybinding.</summary>
    public void UnregisterUI(int keyNumber)
    {
        if (_uiPanels.ContainsKey(keyNumber))
        {
            _uiPanels.Remove(keyNumber);
            // If this was the open panel, note it
            if (_currentOpenKey == keyNumber)
                _currentOpenKey = -1;
        }
    }

    /// <summary>Toggle a UI panel: close others, open this one. If already open, close it.</summary>
    public void ToggleUI(int keyNumber)
    {
        Debug.Log($"[UIKeyBindingManager.ToggleUI] Called with keyNumber={keyNumber}, registered panels: {string.Join(", ", _uiPanels.Keys)}");

        if (!_uiPanels.ContainsKey(keyNumber))
        {
            Debug.LogWarning($"[UIKeyBindingManager] No UI registered for key {keyNumber}.");
            return;
        }

        IUIPanel panel = _uiPanels[keyNumber];
        if (panel == null)
        {
            Debug.LogWarning($"[UIKeyBindingManager] Panel for key {keyNumber} is null!");
            return;
        }

        Debug.Log($"[UIKeyBindingManager] ToggleUI key {keyNumber}: {panel.GetType().Name}, IsOpen={panel.IsOpen}");

        // If this panel is already open, close it
        if (_currentOpenKey == keyNumber && panel.IsOpen)
        {
            panel.Hide();
            _currentOpenKey = -1;
            return;
        }

        // Close the currently open panel (if any)
        if (_currentOpenKey != -1 && _currentOpenKey != keyNumber && _uiPanels.ContainsKey(_currentOpenKey))
        {
            var currentPanel = _uiPanels[_currentOpenKey];
            if (currentPanel != null && currentPanel.IsOpen)
                currentPanel.Hide();
        }

        // Open the requested panel
        panel.Show();
        _currentOpenKey = keyNumber;
    }

    /// <summary>Close all keybind UIs.</summary>
    public void CloseAll()
    {
        foreach (var panel in _uiPanels.Values)
        {
            if (panel != null && panel.IsOpen)
                panel.Hide();
        }
        _currentOpenKey = -1;
    }

    /// <summary>Get the currently open keybind UI number, or -1 if none.</summary>
    public int CurrentOpenKey => _currentOpenKey;

    /// <summary>Check if a specific keybind UI is currently open.</summary>
    public bool IsUIOpen(int keyNumber) => _currentOpenKey == keyNumber;
}
