using UnityEngine;
using UnityEngine.InputSystem;

public class Light_Adjustments : MonoBehaviour
{
    // Serialized field for the Light component and light intensity and light color adjustments 
    [SerializeField] private Light _light;
    [SerializeField] private GameObject _lightBulb;
    [SerializeField] private float _lightIntensityAdjustment = 1.0f; 
    [SerializeField] private Color _lightColorAdjustment = Color.white;
    [SerializeField] private float _lightRangeAdjustment = 10.0f;
    [SerializeField] private float _lightSpotAngleAdjustment = 30.0f;

    BoxCollider _boxCollider;
    [SerializeField] private Material _lightsOn;
    [SerializeField] private Material _lightsOff;

    private PlacedObject _placedObject;
    private bool _isOn = false;

    private void Awake()
    {
        _placedObject = GetComponent<PlacedObject>();
    }

    private void Start()
    {
        // Off by default, but check if we have a saved state
        if (_placedObject != null && !string.IsNullOrEmpty(_placedObject.customData))
        {
            LoadState();
        }
        else
        {
            SetState(false); // Force default off
        }
    }

    private void LoadState()
    {
        if (_placedObject == null || string.IsNullOrEmpty(_placedObject.customData)) return;

        if (bool.TryParse(_placedObject.customData, out bool savedState))
        {
            SetState(savedState);
        }
    }

    private void SaveState()
    {
        if (_placedObject != null)
        {
            _placedObject.customData = _isOn.ToString();
        }
    }

    public void SetState(bool on)
    {
        _isOn = on;
        ApplyVisuals(_isOn);
        SaveState();
    }

    private void ApplyVisuals(bool on)
    {
        if (_light != null) _light.enabled = on;
        if (_lightBulb != null)
        {
            var r = _lightBulb.GetComponent<Renderer>();
            if (r != null) r.material = on ? _lightsOn : _lightsOff;
        }
    }

    private void OnMouseDown()
    {
        // Require Shift + left click to toggle lights, so a plain left click never triggers it
        var keyboard = Keyboard.current;
        bool shiftHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        if (!shiftHeld)
            return;

        // Only allow toggling if the state machine is in IdleState
        var fsm = FindAnyObjectByType<PlacementStateMachine>();
        if (fsm != null && !(fsm.CurrentState is IdleState))
            return;

        // Toggle all lights together based on the new state of this light
        bool newState = !_isOn;
        Light_Adjustments[] allLights = Object.FindObjectsByType<Light_Adjustments>();
        foreach (var l in allLights)
        {
            l.SetState(newState);
        }
    }
}