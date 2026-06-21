using UnityEngine.InputSystem;

/// <summary>
/// Lightweight, hand-authored replacement for the generated Input System wrapper.
/// Instantiate with `new PlacementActions()` in Awake, call Enable/Disable in OnEnable/OnDisable,
/// and subscribe to the actions via the BuildPlacement property.
/// </summary>
public class PlacementActions
{
    // Public access to the action map so callers can subscribe: e.g. _actions.BuildPlacement.Place.performed += ...
    public BuildPlacementActions BuildPlacement { get; }

    public PlacementActions()
    {
        BuildPlacement = new BuildPlacementActions();
    }

    public void Enable() => BuildPlacement.Enable();
    public void Disable() => BuildPlacement.Disable();

    public void Dispose()
    {
        BuildPlacement.Dispose();
    }

    // Nested typed action map for clarity and discoverability
    public class BuildPlacementActions
    {
        private readonly InputActionMap _map;

        public InputAction Place { get; }
        public InputAction Rotate { get; }
        public InputAction Cancel { get; }
        public InputAction ModeBuild { get; }
        public InputAction ModeDelete { get; }
        public InputAction ModeMove { get; }
        public InputAction Undo { get; }
        public InputAction Redo { get; }

        public BuildPlacementActions()
        {
            _map = new InputActionMap("BuildPlacement");

            // Actions created without bindings so you can assign them in the Input System or in code later.
            Place = _map.AddAction("Place", InputActionType.Button);
            Rotate = _map.AddAction("Rotate", InputActionType.Button); 
            Cancel = _map.AddAction("Cancel", InputActionType.Button);
            ModeBuild = _map.AddAction("ModeBuild", InputActionType.Button);
            ModeDelete = _map.AddAction("ModeDelete", InputActionType.Button);
            ModeMove = _map.AddAction("ModeMove", InputActionType.Button);
            Undo = _map.AddAction("Undo", InputActionType.Button);
            Redo = _map.AddAction("Redo", InputActionType.Button);
        }

        public void Enable() => _map.Enable();
        public void Disable() => _map.Disable();
        public void Dispose() => _map.Dispose();

        // Optional helper to set bindings in code
        public void BindPlaceToMouseLeft()
{
            Place.AddBinding("<Mouse>/leftButton")
                 .WithInteraction("Press(behavior=2)");
        }

        // Optional helper to set bindings in code
        public void BindRotateTo_R()
        {
            Rotate.AddBinding("<Keyboard>/r");
        }
        // Optional helper to set bindings in code
        public void BindCancelTo_RMB()
        {
            Cancel.AddBinding("<Mouse>/rightButton");
        }
    }
}
