using UnityEngine;
using TMPro;

/// <summary>
/// Shows a softly-bobbing red "?" above an agent when they have no navigation waypoints.
/// Initialized in Start() so ghosts (which never reach Start) never build the pivot child.
/// Call Show()/Hide() from AiNavigation after FindWaypoints() runs.
/// </summary>
[DisallowMultipleComponent]
public class NoWaypointIndicator : MonoBehaviour
{
    [Tooltip("Height above the agent's local origin (should clear the model's head).")]
    [SerializeField] private float headHeight = 2.0f;
    [Tooltip("How many units the '?' bobs up and down each cycle.")]
    [SerializeField] private float bobAmplitude = 0.07f;
    [Tooltip("Bob cycles per second.")]
    [SerializeField] private float bobSpeed = 0.9f;
    [Tooltip("Subtle left/right drift amplitude (x-axis).")]
    [SerializeField] private float driftAmplitude = 0.025f;
    [Tooltip("Font size of the world-space '?' label.")]
    [SerializeField] private float fontSize = 8f;

    private Transform _pivot;
    private TMP_Text _label;
    private float _t;
    private Camera _cam;
    private bool _visible;
    private bool _pendingShow; // buffered if Show() is called before Start()

    // Start() is NOT called on ghost preview objects (they are destroyed before Start runs),
    // so moving creation here ensures ghosts never build the pivot child.
    private void Start()
    {
        _cam = Camera.main;

        var go = new GameObject("NoWaypoint_?");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, headHeight, 0f);
        _pivot = go.transform;

        _label = go.AddComponent<TextMeshPro>();
        _label.text = "?";
        _label.color = Color.red;
        _label.fontSize = fontSize;
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontStyle = FontStyles.Bold;
        _label.outlineWidth = 0.22f;
        _label.outlineColor = new Color32(20, 0, 0, 220);
        _label.enableWordWrapping = false;

        go.SetActive(_pendingShow);
        _visible = _pendingShow;
    }

    public void Show()
    {
        if (_pivot != null) // Unity == catches destroyed objects
        {
            if (_visible) return;
            _visible = true;
            _pivot.gameObject.SetActive(true);
        }
        else
        {
            _pendingShow = true; // Start() hasn't run yet; activate when it does
        }
    }

    public void Hide()
    {
        _pendingShow = false;
        if (!_visible) return;
        _visible = false;
        if (_pivot != null)
            _pivot.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_pivot == null || !_visible) return;

        _t += Time.deltaTime;

        float yBob   = Mathf.Sin(_t * bobSpeed * Mathf.PI * 2f) * bobAmplitude;
        float xDrift = Mathf.Sin(_t * bobSpeed * Mathf.PI * 2f * 0.37f) * driftAmplitude;

        _pivot.localPosition = new Vector3(xDrift, headHeight + yBob, 0f);

        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
            _pivot.forward = _cam.transform.forward;
    }
}
