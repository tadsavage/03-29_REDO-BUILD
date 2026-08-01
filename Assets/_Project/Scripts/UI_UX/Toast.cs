using UnityEngine;
using UnityEngine.UIElements;

public class UIToast : MonoBehaviour
{
    private static Label _toast;
    private static float _timer;
#pragma warning disable CS0414
    [SerializeField] private float _defaultDuration = 1.5f;
#pragma warning restore CS0414

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null)
        {
            Debug.LogError($"[UIToast] No UIDocument found on GameObject '{gameObject.name}'.", this);
            enabled = false;
            return;
        }
        // Toasts must sit ABOVE every other panel so warnings are always visible
        // Set to maximum to ensure visibility above all modals (NewItemPanel, RackSetupUI, etc.)
        doc.sortingOrder = 999999;
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

        RaiseAboveEverything();

        // Re-center after the label's geometry resolves (text content may change its width)
        _toast.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    /// <summary>
    /// Puts the toast in front of every runtime panel.
    ///
    /// The document's sortingOrder (999999, set in Awake) only orders this document against OTHER
    /// documents. Every full-screen panel — Work Queue, Contracts, Shift Manager, New Item — is built
    /// at runtime into THIS SAME document's root, so z-order between them and the toast is SIBLING
    /// ORDER, and a panel added later draws on top.
    ///
    /// A bare _toast.BringToFront() (the previous fix) wasn't enough: ToastLabel is nested inside a
    /// wrapper in the UXML, so raising it only reordered it against its own siblings inside that
    /// wrapper — the wrapper itself stayed wherever it was in the root's child list, still underneath
    /// the panels. Walking the whole ancestor chain is what actually gets it to the top, and it has to
    /// run per-Show because any panel opened since the last toast would otherwise overtake it again.
    ///
    /// Deliberately not reparenting the label to the root instead: the wrapper may carry USS that
    /// positions or styles it.
    /// </summary>
    private static void RaiseAboveEverything()
    {
        var e = (VisualElement)_toast;
        while (e?.parent != null)
        {
            e.BringToFront();
            e = e.parent;
        }
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
