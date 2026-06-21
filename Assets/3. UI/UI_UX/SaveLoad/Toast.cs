using UnityEngine;
using UnityEngine.UIElements;

public class UIToast : MonoBehaviour
{
    private static Label _toast;
    private static float _timer;
    [SerializeField] private float _defaultDuration = 1.5f;

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null)
        {
            Debug.LogError($"[UIToast] No UIDocument found on GameObject '{gameObject.name}'.", this);
            enabled = false;
            return;
        }
        doc.sortingOrder = 100;
        var root = doc.rootVisualElement;
        if (root == null)
        {
            Debug.LogError($"[UIToast] UIDocument on '{gameObject.name}' has no rootVisualElement.", this);
            enabled = false;
            return;
        }
        root.pickingMode = PickingMode.Ignore;
        _toast = root.Q<Label>("ToastLabel");
        if (_toast == null)
        {
            Debug.LogError($"[UIToast] No Label named 'ToastLabel' found in UIDocument on '{gameObject.name}'.", this);
            enabled = false;
            return;
        }
        _toast.style.position = Position.Absolute;
        _toast.style.opacity  = 0;
    }

    private void Update()
    {
        if (_timer > 0f)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                _toast.style.opacity = 0;
        }
    }

    private void OnDestroy()
    {
        _toast = null;
    }

    public static void Show(string msg, float _defaultDuration = 1.5f)
    {
        if (_toast == null || _toast.panel == null) return;

        _toast.text             = msg;
        _toast.style.opacity    = 1;
        _timer                  = _defaultDuration;

        // Re-center after the label's geometry resolves (text content may change its width)
        _toast.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private static void OnGeometryChanged(GeometryChangedEvent evt)
    {
        _toast.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        CenterToast();
    }

    private static void CenterToast()
    {
        if (_toast == null || _toast.panel == null) return;

        // Convert screen center pixels → panel coordinate space (handles DPI / scale)
        var panelCenter = RuntimePanelUtils.ScreenToPanel(
            _toast.panel,
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

        float w = _toast.resolvedStyle.width;
        float h = _toast.resolvedStyle.height;

        _toast.style.left = panelCenter.x - w * 0.5f;
        _toast.style.top  = panelCenter.y - h * 0.5f;
    }
}
