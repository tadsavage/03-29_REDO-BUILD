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
///   7 = Inbound Test (InboundTestPanel)
/// </summary>
public class UIKeyBindingManager : MonoBehaviour
{
    public static UIKeyBindingManager Instance { get; private set; }

    private Dictionary<int, IUIPanel> _uiPanels = new();
    private int _currentOpenKey = -1;  // -1 = none open

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>Register a UI panel for a keybinding number (1-7).</summary>
    public void RegisterUI(int keyNumber, IUIPanel panel)
    {
        if (keyNumber < 1 || keyNumber > 7)
        {
            Debug.LogError($"[UIKeyBindingManager] Invalid key number {keyNumber}. Must be 1-7.");
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
