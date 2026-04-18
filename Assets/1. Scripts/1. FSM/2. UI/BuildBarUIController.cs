using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;

public class BuildBarUIController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ObjDataRegistry registry;

    [Header("FSM Hook")]
    [SerializeField] private PlacementStateMachine fsm;

    // Events for other systems
    public Action OnDeleteClicked;
    public Action OnUndoClicked;
    public Action OnRedoClicked;
    public Action OnMoveClicked;
    public Action OnBuildBarReady;
    public Action<ObjDataSO> OnBuildItemClicked;

    VisualElement root;
    List<Button> buildButtons = new();

    void OnEnable()
    {
        var uiDoc = GetComponent<UIDocument>();
        root = uiDoc.rootVisualElement;

        CacheBuildButtons();
        BindUtilityButtons();
        BindBuildButtons();

        // Notify other systems that UI is ready
        root.schedule.Execute(() => OnBuildBarReady?.Invoke());
    }

    // ---------------------------------------------------------
    // FIND ALL BUILD BUTTONS
    // ---------------------------------------------------------
    void CacheBuildButtons()
    {
        buildButtons = root.Query<Button>().ToList();

        // Remove utility buttons from the list
        buildButtons.Remove(root.Q<Button>("ERASE"));
        buildButtons.Remove(root.Q<Button>("UNDO"));
        buildButtons.Remove(root.Q<Button>("REDO"));
        buildButtons.Remove(root.Q<Button>("MOVE"));
    }

    // ---------------------------------------------------------
    // BIND ERASE / UNDO / REDO / MOVE
    // ---------------------------------------------------------
    void BindUtilityButtons()
    {
        var erase = root.Q<Button>("ERASE");
        if (erase != null)
            erase.clicked += () => OnDeleteClicked?.Invoke();

        var undo = root.Q<Button>("UNDO");
        if (undo != null)
            undo.clicked += () => OnUndoClicked?.Invoke();

        var redo = root.Q<Button>("REDO");
        if (redo != null)
            redo.clicked += () => OnRedoClicked?.Invoke();

        var move = root.Q<Button>("MOVE");
        if (move != null)
            move.clicked += () => OnMoveClicked?.Invoke();
    }

    // ---------------------------------------------------------
    // BIND BUILD ITEM BUTTONS
    // ---------------------------------------------------------
    void BindBuildButtons()
    {
        int count = Mathf.Min(buildButtons.Count, registry.buttonSOs.Count);

        for (int i = 0; i < count; i++)
        {
            Button b = buildButtons[i];
            ObjDataSO data = registry.buttonSOs[i];

            // Clear default background
            b.style.backgroundImage = null;

            // Apply icon AFTER layout
            root.schedule.Execute(() =>
            {
                if (data.icon != null)
                    b.style.backgroundImage = new StyleBackground(data.icon);
            });

            // Tooltip
            b.tooltip = $"{data.objName}\nCost: {data.cost}";

            // Label text (cost)
            b.text = "$" + data.cost;

            // Click event
            b.clicked += () => HandleBuildItemClicked(data);
        }
    }

    // ---------------------------------------------------------
    // CLICK HANDLER
    // ---------------------------------------------------------
    void HandleBuildItemClicked(ObjDataSO data)
    {
        OnBuildItemClicked?.Invoke(data);

        // Optional FSM integration
        // fsm.BuildState.SetData(data);
        // fsm.SetState(fsm.RaycastState);
    }
}
