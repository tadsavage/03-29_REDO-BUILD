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
        // This is the HUD document (top bar + the panels built into it), NOT the toast's layer — the
        // toast moves to its own document below. Kept high because the panels built into this document
        // have to cover the bottom bar.
        doc.sortingOrder = UILayers.Hud;
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
        MoveToOwnLayer(doc, root);

        _toast.style.position = Position.Absolute;
        _toast.style.opacity  = 0;
    }

    /// <summary>Sorting order of the toast's own document. Above every panel, including the windows
    /// that deliberately outrank the HUD (see UILayers).</summary>
    public const float ToastSortingOrder = 1000100f;

    /// <summary>
    /// Moves the toast onto a UIDocument of its own, above everything else.
    ///
    /// It is authored in HUD.uxml inside "Root" (.hud-root), a sibling of the top bar and in the SAME
    /// document as it. That is a dead end for layering: documents are ordered against each other by
    /// sortingOrder, so any window raised above the top bar's document was necessarily above the toast
    /// too, and any window kept below the toast was stuck below the top bar. Panels 1/2/3 own separate
    /// documents and hit exactly that wall. A dedicated top layer breaks the tie — the toast is no
    /// longer bound to the HUD's sortingOrder, so windows are free to sit between the two.
    ///
    /// The stylesheets are carried across explicitly: USS is resolved per visual tree, so a label
    /// moved to a fresh document would otherwise lose `.toast-label` entirely and render as bare text.
    /// </summary>
    private void MoveToOwnLayer(UIDocument sourceDoc, VisualElement sourceRoot)
    {
        // Created inactive so panelSettings is assigned BEFORE the document's first enable — a
        // UIDocument builds its tree on enable, and configuring it afterwards is the documented way
        // to end up with an empty panel.
        var layerGo = new GameObject("[ToastLayer]");
        layerGo.transform.SetParent(transform, worldPositionStays: false);
        layerGo.SetActive(false);

        var layerDoc = layerGo.AddComponent<UIDocument>();
        layerDoc.panelSettings = sourceDoc.panelSettings; // same panel, so sortingOrder is comparable
        layerDoc.sortingOrder = ToastSortingOrder;
        layerGo.SetActive(true);

        var layerRoot = layerDoc.rootVisualElement;
        if (layerRoot == null)
        {
            // Never observed, but a silent failure here would leave the toast unparented and invisible.
            Debug.LogWarning("[UIToast] Toast layer document produced no root; leaving the toast on the HUD.");
            if (_toast.parent != sourceRoot) sourceRoot.Add(_toast);
            return;
        }

        for (int i = 0; i < sourceRoot.styleSheets.count; i++)
            layerRoot.styleSheets.Add(sourceRoot.styleSheets[i]);

        layerRoot.pickingMode = PickingMode.Ignore;
        layerRoot.Add(_toast); // reparents out of the HUD tree
    }

    private void Update()
    {
        // Unscaled: the duration itself is already scaled by game speed once, in Show() below.
        // Decrementing with a SCALED deltaTime here would scale it a second time (and in the
        // opposite direction), since Time.deltaTime is already multiplied by Time.timeScale.
        if (_timer > 0f)
        {
            _timer -= Time.unscaledDeltaTime;
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

        // The requested duration is scaled by the current game speed (Time.timeScale, set by
        // TopBarUI's speed buttons): half speed keeps the toast up half as long in real time,
        // double speed keeps it up twice as long. That way the toast always spans the same
        // amount of IN-GAME time no matter how fast/slow the simulation is running. At 0×
        // (paused) this multiplies out to 0, so the toast simply stays visible until the game
        // is unpaused and the timer can start counting down again.
        float gameSpeedScale = Time.timeScale;
        _timer                  = _defaultDuration * gameSpeedScale;

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
    /// <summary>
    /// Re-asserts the toast's place at the very top after something else raised itself.
    ///
    /// RaiseAboveEverything only runs on Show, so a panel opened WHILE a toast is still on screen
    /// would otherwise overtake it — sibling order is decided by whoever called BringToFront last.
    /// Panels that deliberately raise themselves (the order screens, which must clear the top and
    /// bottom bars) call this immediately afterwards so the toast keeps the top slot.
    ///
    /// No-op when no toast is showing, so callers can invoke it unconditionally.
    /// </summary>
    public static void KeepOnTop()
    {
        if (_toast == null || _toast.panel == null) return;
        if (_timer <= 0f) return; // nothing on screen to protect
        RaiseAboveEverything();
    }

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
