using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Runtime dev / cheat console.
///
/// Setup:
///   1. Create a GameObject, add UIDocument + this component.
///   2. Assign DevPanelUI.uxml to the UIDocument's Source Asset.
///   3. Assign a PanelSettings asset (e.g. AppUIPanelSettings) with a sort order
///      high enough to render on top (try 50).
///   4. Enter Play Mode — press backtick (`) or click the ⚙ DEV pill to open.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class DevPanelController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _panel;
    private bool _visible;

    private GameContext _ctx;
    private PlacementStateMachine _fsm;
    private PlacementGrid _grid;

    private Label _balance, _hourly, _spent;
    private Label _time, _speed;
    private Label _objects, _undo;
    private Label _state, _stack;

    // Header drag state
    private bool _dragging;
    private Vector2 _dragStartPointer;
    private Vector2 _panelStartPos;

    private void Awake() => _doc = GetComponent<UIDocument>();

    private void Start()
    {
        _ctx  = FindFirstObjectByType<GameContext>();
        _fsm  = FindFirstObjectByType<PlacementStateMachine>();
        _grid = FindFirstObjectByType<PlacementGrid>();

        var root = _doc.rootVisualElement;
        _panel = root.Q("dev-panel");
        if (_panel == null) return;
        _panel.style.display = DisplayStyle.None;

        // Toggle: backtick (`) — close button inside the panel
        root.Q<Button>("close-btn").clicked += Toggle;

        // Draggable header — moves the entire panel
        var header = root.Q("dev-header");
        header.RegisterCallback<PointerDownEvent>(OnHeaderDown);
        header.RegisterCallback<PointerMoveEvent>(OnHeaderMove);
        header.RegisterCallback<PointerUpEvent>(OnHeaderUp);
        header.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);

        // Stat labels
        _balance = root.Q<Label>("stat-balance");
        _hourly  = root.Q<Label>("stat-hourly");
        _spent   = root.Q<Label>("stat-spent");
        _time    = root.Q<Label>("stat-time");
        _speed   = root.Q<Label>("stat-speed");
        _objects = root.Q<Label>("stat-objects");
        _undo    = root.Q<Label>("stat-undo");
        _state   = root.Q<Label>("stat-state");
        _stack   = root.Q<Label>("stat-stack");

        // Economy buttons
        root.Q<Button>("btn-add-1k").clicked   += () => _ctx?.MoneyService.Refund(1_000,   "Debug");
        root.Q<Button>("btn-add-10k").clicked  += () => _ctx?.MoneyService.Refund(10_000,  "Debug");
        root.Q<Button>("btn-add-100k").clicked += () => _ctx?.MoneyService.Refund(100_000, "Debug");
        root.Q<Button>("btn-zero").clicked     += () => _ctx?.MoneyService.SetMoney(0);

        // Time buttons
        root.Q<Button>("btn-pause").clicked += () => _ctx?.TimeService.SetTimeScale(0f);
        root.Q<Button>("btn-1x").clicked    += () => _ctx?.TimeService.SetTimeScale(1f);
        root.Q<Button>("btn-2x").clicked    += () => _ctx?.TimeService.SetTimeScale(2f);
        root.Q<Button>("btn-5x").clicked    += () => _ctx?.TimeService.SetTimeScale(5f);

        // World buttons
        root.Q<Button>("btn-rebuild").clicked += () => _grid?.RebuildFromRegistry();
        root.Q<Button>("btn-clear").clicked   += ClearAll;

        if (_ctx != null)
            _ctx.MoneyService.OnMoneyChanged += RefreshEconomy;

        RefreshEconomy();
    }

    private void Update()
    {
        if (_panel == null) return;

        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            Toggle();

        if (!_visible || _ctx == null) return;

        var t = _ctx.TimeService;
        _time.text  = $"Day {t.Day}  —  {t.Hour:D2}:{t.Minute:D2}";
        _speed.text = t.TimeScale == 0f ? "PAUSED" : $"{t.TimeScale}×";

        _objects.text = PlacedObjectRegistry.Count.ToString();

        if (_fsm != null)
        {
            _state.text = _fsm.CurrentState?.GetType().Name ?? "—";
            _stack.text = _fsm.DebugStackDepth.ToString();
            _undo.text  = $"{_fsm.History.UndoCount} / {_fsm.History.RedoCount}";
        }
    }

    private void RefreshEconomy()
    {
        if (_ctx == null) return;
        var m = _ctx.MoneyService;
        _balance.text = $"${m.CurrentCapital:N0}";
        _hourly.text  = $"${m.TotalHourlyCost:N0}/hr";
        _spent.text   = $"${m.SpentToday:N0}";
    }

    private void ClearAll()
    {
        var snapshot = PlacedObjectRegistry.GetSnapshot();
        foreach (var obj in snapshot)
        {
            if (obj != null) Destroy(obj.gameObject);
        }
        _grid?.RebuildFromRegistry();
    }

    private void Toggle()
    {
        _visible = !_visible;
        _panel.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ── Header drag ──────────────────────────────────────────────

    private void OnHeaderDown(PointerDownEvent evt)
    {
        _dragging         = true;
        _dragStartPointer = evt.position;
        _panelStartPos    = new Vector2(_panel.resolvedStyle.left, _panel.resolvedStyle.top);

        // Convert right-anchored position to explicit left/top so dragging works
        if (float.IsNaN(_panelStartPos.x))
        {
            var root = _doc.rootVisualElement;
            _panelStartPos.x = root.resolvedStyle.width - _panel.resolvedStyle.width
                                - _panel.resolvedStyle.right;
        }

        _panel.style.right = StyleKeyword.Auto;
        _panel.style.left  = _panelStartPos.x;
        _panel.style.top   = _panelStartPos.y;

        var header = _doc.rootVisualElement.Q("dev-header");
        header.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnHeaderMove(PointerMoveEvent evt)
    {
        if (!_dragging) return;
        var header = _doc.rootVisualElement.Q("dev-header");
        if (!header.HasPointerCapture(evt.pointerId)) return;

        var delta = (Vector2)evt.position - _dragStartPointer;
        _panel.style.left = _panelStartPos.x + delta.x;
        _panel.style.top  = _panelStartPos.y + delta.y;
        evt.StopPropagation();
    }

    private void OnHeaderUp(PointerUpEvent evt)
    {
        var header = _doc.rootVisualElement.Q("dev-header");
        if (header.HasPointerCapture(evt.pointerId))
            header.ReleasePointer(evt.pointerId);
        _dragging = false;
    }

    private void OnDestroy()
    {
        if (_ctx != null)
            _ctx.MoneyService.OnMoneyChanged -= RefreshEconomy;
    }
}
