using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Small floating tooltip that follows the cursor while it hovers a chevron.
/// Created lazily by ChevronController on the first hover — no scene wiring required.
/// Owns its own UIDocument + PanelSettings, so it's independent of the rest of the UI.
/// </summary>
public class ChevronTooltipUI : MonoBehaviour
{
    private const string TOOLTIP_TEXT = "R-click to change dir.\nDbl.-click to enter Aisle setup";
    private const float CURSOR_OFFSET_X = 16f;
    private const float CURSOR_OFFSET_Y = 16f;

    private static ChevronTooltipUI _instance;

    private UIDocument _doc;
    private Label _label;
    private VisualElement _root;
    private bool _visible;

    public static ChevronTooltipUI Ensure()
    {
        if (_instance != null) return _instance;

        var go = new GameObject("ChevronTooltipUI");
        _instance = go.AddComponent<ChevronTooltipUI>();
        _instance.Build();
        return _instance;
    }

    private void Build()
    {
        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = FindPanelSettings();
        _doc.sortingOrder = 200; // sits above toasts (100) and normal UI

        _root = _doc.rootVisualElement;
        if (_root == null) return;

        _root.pickingMode = PickingMode.Ignore;
        _root.style.position = Position.Absolute;
        _root.style.left = 0;
        _root.style.top = 0;
        _root.style.right = 0;
        _root.style.bottom = 0;

        _label = new Label(TOOLTIP_TEXT);
        _label.pickingMode = PickingMode.Ignore;
        _label.style.position = Position.Absolute;
        _label.style.paddingLeft = 10;
        _label.style.paddingRight = 10;
        _label.style.paddingTop = 6;
        _label.style.paddingBottom = 6;
        _label.style.backgroundColor = new StyleColor(new Color(0.08f, 0.11f, 0.14f, 0.92f));
        _label.style.borderTopWidth = 1;
        _label.style.borderBottomWidth = 1;
        _label.style.borderLeftWidth = 1;
        _label.style.borderRightWidth = 1;
        var border = new StyleColor(new Color(0.36f, 0.61f, 0.77f, 1f));
        _label.style.borderTopColor = border;
        _label.style.borderBottomColor = border;
        _label.style.borderLeftColor = border;
        _label.style.borderRightColor = border;
        _label.style.borderTopLeftRadius = 6;
        _label.style.borderTopRightRadius = 6;
        _label.style.borderBottomLeftRadius = 6;
        _label.style.borderBottomRightRadius = 6;
        _label.style.color = new StyleColor(new Color(0.92f, 0.96f, 1f, 1f));
        _label.style.fontSize = 12;
        _label.style.whiteSpace = WhiteSpace.Normal;
        _label.style.opacity = 0f;

        _root.Add(_label);
    }

    private static PanelSettings FindPanelSettings()
    {
        // Reuse whatever PanelSettings other UI documents in the scene are using so the
        // scaling / DPI matches. If nothing is found, Unity provides a default asset.
        var existing = FindFirstObjectByType<UIDocument>();
        return existing != null ? existing.panelSettings : null;
    }

    public void Show(Vector2 screenPos)
    {
        _visible = true;
        if (_label == null) return;

        _label.style.opacity = 1f;
        PositionAt(screenPos);
    }

    public void Hide()
    {
        _visible = false;
        if (_label != null) _label.style.opacity = 0f;
    }

    private void Update()
    {
        // While shown, follow the cursor. Cheap — a single label transform per frame.
        if (!_visible || _label == null || _label.panel == null) return;
        PositionAt(UnityEngine.InputSystem.Mouse.current != null
            ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition);
    }

    private void PositionAt(Vector2 screenPos)
    {
        if (_label == null || _label.panel == null) return;

        // ScreenToPanel already flips screen (Y-up-from-bottom) → panel (Y-down-from-top),
        // so pass the raw cursor coords. Offset in screen space to place below-right.
        var panelPos = RuntimePanelUtils.ScreenToPanel(_label.panel,
            new Vector2(screenPos.x + CURSOR_OFFSET_X, screenPos.y - CURSOR_OFFSET_Y));
        _label.style.left = panelPos.x;
        _label.style.top = panelPos.y;
    }
}
