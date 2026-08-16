using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages keybinding UIs (keys 1-8).
/// Only one EXCLUSIVE keybind UI can be open at a time — toggling one closes any currently open one.
/// Panels registered with floating: true opt out of that and coexist with whatever else is showing.
/// Panels must implement IUIPanel to be registered.
///
/// Keybindings:
///   1 = Dev Console (ToolsWindowController)
///   2 = Hiring Board (HiringBoardUI)
///   3 = Employee Roster (EmployeeRosterUI)
///   4 = Employee List (EmployeeListPanelController)
///   5 = Shift Manager (ShiftManagerPanel)
///   6 = Contracts (ContractsPanel) — New Contracts / Bulk Orders / Accounts / Schedule. Replaced
///       Slot Assignment, which is now opened by clicking a rack rather than by a number key
///   7 = Work Queue
///   8 = New Item / Slotter
///   9 = Purchasing (PurchasingPanel) — raise POs for inbound stock; the inbound counterpart to 6
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

    /// <summary>
    /// Panels with no hotkey of their own that must still close on Tab — sub-popups opened by clicking
    /// something inside another panel (e.g. the Work Queue's Fill Rate shorts readout).
    ///
    /// Kept separate from _uiPanels because that dictionary is keyed by hotkey number and only has
    /// nine slots, all spoken for. An auxiliary is never the "currently open" panel for exclusivity
    /// purposes either — it floats over whatever opened it rather than replacing it.
    /// </summary>
    private readonly List<IUIPanel> _auxiliaryPanels = new();

    /// <summary>
    /// Hotkey panels exempt from the one-at-a-time rule — floating windows rather than modals.
    ///
    /// A modal owns the screen: it dims what's behind it and swallows clicks, so closing whatever was
    /// already open costs the player nothing. A floating window is the opposite bargain — it's draggable
    /// and lets clicks through precisely so it can be read ALONGSIDE another panel, and exclusivity would
    /// take that back the instant the player opened the thing they wanted to compare it against.
    ///
    /// Kept out of _currentOpenKey entirely: that field means "the exclusive panel currently holding the
    /// screen", and a floating window doesn't hold it. Anything that needs to know whether a specific
    /// panel is showing should ask IsPanelOpen rather than compare against CurrentOpenKey.
    ///
    /// Empty today — Contracts (6) was the only floating panel and is now exclusive. The option stays
    /// because IsPanelOpen and the play bar's mask are already written around it.
    /// </summary>
    private readonly HashSet<int> _floatingKeys = new();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    /// <summary>Register a UI panel for a keybinding number (1-9). Pass floating: true for a window
    /// that should coexist with other panels instead of replacing them — see _floatingKeys.</summary>
    public void RegisterUI(int keyNumber, IUIPanel panel, bool floating = false)
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
        if (floating) _floatingKeys.Add(keyNumber); else _floatingKeys.Remove(keyNumber);
        Debug.Log($"[UIKeyBindingManager] Registered {panel.GetType().Name} for key {keyNumber}" +
                  (floating ? " (floating)" : ""));
    }

    /// <summary>Unregister a UI panel from its keybinding.</summary>
    public void UnregisterUI(int keyNumber)
    {
        if (_uiPanels.ContainsKey(keyNumber))
        {
            _uiPanels.Remove(keyNumber);
            _floatingKeys.Remove(keyNumber);
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

        // A floating window toggles purely on its own visibility and leaves _currentOpenKey — and
        // therefore whatever exclusive panel is showing — completely alone in both directions.
        if (_floatingKeys.Contains(keyNumber))
        {
            if (panel.IsOpen) panel.Hide(); else panel.Show();
            return;
        }

        // Any exclusive toggle clears the screen, and a sub-popup is part of what's on it — the panel
        // that opened it is about to be hidden either way, so leaving its popup floating over the new
        // panel would strand a readout belonging to something no longer visible.
        CloseAuxiliaries();

        // If this panel is already open, close it
        if (_currentOpenKey == keyNumber && panel.IsOpen)
        {
            panel.Hide();
            _currentOpenKey = -1;
            return;
        }

        // Close the currently open panel (if any) before opening the new one.
        if (_currentOpenKey != -1 && _currentOpenKey != keyNumber && _uiPanels.ContainsKey(_currentOpenKey))
        {
            var currentPanel = _uiPanels[_currentOpenKey];
            if (currentPanel != null && currentPanel.IsOpen)
            {
                // The Shift Manager is the one panel that can hold unsaved work, so it gets a say in
                // whether it's closed. Unedited it closes silently like any other; edited it raises
                // its confirmation and the requested panel opens only if the player agrees. Opening
                // is deferred into the callback rather than continuing here, otherwise the new panel
                // would appear on top of the confirmation the player hasn't answered yet.
                if (currentPanel is ShiftManagerPanel shiftManager && shiftManager.HasUnsavedChanges)
                {
                    int pendingKey = keyNumber;
                    var pendingPanel = panel;
                    shiftManager.RequestClose(() =>
                    {
                        pendingPanel.Show();
                        _currentOpenKey = pendingKey;
                    });
                    return;
                }

                currentPanel.Hide();
            }
        }

        // Open the requested panel
        panel.Show();
        _currentOpenKey = keyNumber;
    }

    /// <summary>Register a hotkey-less popup so Tab still closes it. See _auxiliaryPanels.</summary>
    public void RegisterAuxiliary(IUIPanel panel)
    {
        if (panel == null || _auxiliaryPanels.Contains(panel)) return;
        _auxiliaryPanels.Add(panel);
    }

    public void UnregisterAuxiliary(IUIPanel panel)
    {
        if (panel != null) _auxiliaryPanels.Remove(panel);
    }

    /// <summary>Closes any open auxiliary popup. Returns true if it closed at least one — callers use
    /// that to swallow the keypress, so Escape dismisses the popup INSTEAD of also opening the pause
    /// menu behind it.</summary>
    public bool CloseAuxiliaries()
    {
        bool closedAny = false;
        foreach (var panel in _auxiliaryPanels)
        {
            if (panel == null || !panel.IsOpen) continue;
            panel.Hide();
            closedAny = true;
        }
        return closedAny;
    }

    /// <summary>True while any hotkey-less popup is showing.</summary>
    public bool AnyAuxiliaryOpen => _auxiliaryPanels.Any(p => p != null && p.IsOpen);

    /// <summary>Close all keybind UIs.</summary>
    public void CloseAll()
    {
        CloseAuxiliaries();

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

    /// <summary>Whether the panel on this key is actually showing, asked of the panel itself rather
    /// than inferred from CurrentOpenKey — a floating window is never the "current" key, so anything
    /// mirroring open state (the play bar's highlight) has to come through here to see it.</summary>
    public bool IsPanelOpen(int keyNumber)
        => _uiPanels.TryGetValue(keyNumber, out var panel) && panel != null && panel.IsOpen;
}
