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

        public BuildPlacementActions()
        {
            _map = new InputActionMap("BuildPlacement");

            // Actions created without bindings so you can assign them in the Input System or in code later.
            Place = _map.AddAction("Place", InputActionType.Button);
            Rotate = _map.AddAction("Rotate", InputActionType.Value);
            Cancel = _map.AddAction("Cancel", InputActionType.Button);
            ModeBuild = _map.AddAction("ModeBuild", InputActionType.Button);
            ModeDelete = _map.AddAction("ModeDelete", InputActionType.Button);
            ModeMove = _map.AddAction("ModeMove", InputActionType.Button);
        }

        public void Enable() => _map.Enable();
        public void Disable() => _map.Disable();
        public void Dispose() => _map.Dispose();

        // Optional helper to set bindings in code (example)
        public void BindPlaceToMouseLeft()
        {
            Place.AddBinding("<Mouse>/leftButton");
        }

        public void BindRotateTo_R()
        {
            Rotate.AddBinding("<Keyboard>/r");
        }

        // Optional helper to set bindings from an InputActionAsset
        public void LoadBindingsFromAsset(InputActionAsset asset)
        {
            if (asset == null) return;
            var map = asset.FindActionMap("BuildPlacement");
            if (map == null) return;

            // Replace the internal map with the one from the asset.
            // Note: this is a simple approach; if you want to keep existing references,
            // copy bindings from `map` to the actions above instead.
            Disable();
            _map.Dispose();

            // Recreate actions from the asset map
            // (Simpler approach: keep a reference to the asset and use asset.FindAction(...) directly)
        }
    }
}
