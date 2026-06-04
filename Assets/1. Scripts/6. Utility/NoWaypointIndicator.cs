using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class NoWaypointIndicator : MonoBehaviour
{
    [SerializeField] private float headHeight     = 2.75f;
    [SerializeField] private float bobAmplitude   = 0.07f;
    [SerializeField] private float bobSpeed       = 0.9f;
    [SerializeField] private float driftAmplitude = 0.025f;
    [SerializeField] private float fontSize       = 8f;
    [SerializeField] private float fadeDuration   = 1.5f;

    private enum State { Hidden, Showing, FadingOut }

    private AiNavigation _nav;
    private Transform    _pivot;
    private TMP_Text     _label;
    private Camera       _cam;
    private float        _bobT;
    private float        _fadeTimer;
    private State        _state = State.Hidden;

    // Keep Show/Hide so AiNavigation compiles — indicator drives itself.
    public void Show() { }
    public void Hide() { }

    private void Start()
    {
        _nav = GetComponent<AiNavigation>();
        _cam = Camera.main;

        var go = new GameObject("NoWaypoint_?");
        go.transform.SetParent(transform, false);
        _pivot = go.transform;

        _label = go.AddComponent<TextMeshPro>();
        _label.text               = "?";
        _label.fontSize           = fontSize;
        _label.color              = Color.red;
        _label.alignment          = TextAlignmentOptions.Center;
        _label.fontStyle          = FontStyles.Bold;
        _label.outlineWidth       = 0.22f;
        _label.outlineColor       = new Color32(20, 0, 0, 220);
        _label.enableWordWrapping = false;

        go.SetActive(false);
        _state = State.Hidden;
    }

    private void LateUpdate()
    {
        if (_pivot == null) return;

        bool noWaypoints = _nav == null || !_nav.HasWaypoints;

        switch (_state)
        {
            case State.Hidden:
                if (noWaypoints)
                {
                    _label.color = Color.red;
                    _pivot.gameObject.SetActive(true);
                    _state = State.Showing;
                }
                break;

            case State.Showing:
                Bob();
                if (!noWaypoints)
                {
                    _fadeTimer = 0f;
                    _state = State.FadingOut;
                }
                break;

            case State.FadingOut:
                Bob();
                _fadeTimer += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(_fadeTimer / fadeDuration);
                _label.color = new Color(1f, 0f, 0f, alpha);

                if (noWaypoints)
                {
                    // Waypoints removed mid-fade — snap back to showing.
                    _label.color = Color.red;
                    _state = State.Showing;
                }
                else if (_fadeTimer >= fadeDuration)
                {
                    _pivot.gameObject.SetActive(false);
                    _label.color = Color.red;
                    _state = State.Hidden;
                }
                break;
        }
    }

    private void Bob()
    {
        _bobT += Time.deltaTime;
        float y = Mathf.Sin(_bobT * bobSpeed * Mathf.PI * 2f) * bobAmplitude;
        float x = Mathf.Sin(_bobT * bobSpeed * Mathf.PI * 2f * 0.37f) * driftAmplitude;
        _pivot.localPosition = new Vector3(x, headHeight + y, 0f);

        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
            _pivot.forward = _cam.transform.forward;
    }
}
