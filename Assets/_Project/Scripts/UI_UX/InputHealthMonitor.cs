using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Writes a WARNING to the log (Player.log in a build) whenever input is being swallowed for a reason
/// that is almost certainly a bug, so a "nothing is clickable" report from a build can be diagnosed
/// from the log alone. Info logs are muted in builds (DebugLogSwitch), which is why these are warnings.
///
/// Reports only real trouble, never ordinary UI clicks:
///  - a startup summary for the Main scene (placement FSM present/enabled, modal claims, panels
///    without PanelSettings, EventSystem, time scale);
///  - a world click swallowed by a UI element covering most of the screen;
///  - a click while the placement FSM is disabled;
///  - a click or number key while a text-entry modal (UIModalGuard) still holds input.
/// Capped at <see cref="MaxReports"/> lines per scene so it can't flood the log.
/// </summary>
public class InputHealthMonitor : MonoBehaviour
{
    private const int MaxReports = 40;
    private const float SameReasonCooldown = 5f;
    private const float FullScreenFraction = 0.6f;
    private const float StartupReportDelay = 3f;

    private static InputHealthMonitor _instance;

    private int _reports;
    private string _lastReason;
    private float _lastReasonTime = -999f;
    private float _sceneStart;
    private bool _startupReported;
    private PlacementStateMachine _fsm;
    private RaycastController _raycast;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[InputHealthMonitor]") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<InputHealthMonitor>();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
    private void OnDestroy() { if (_instance == this) _instance = null; }

    private void Start() => ResetForScene();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) ResetForScene();
    }

    private void ResetForScene()
    {
        _reports = 0;
        _startupReported = false;
        _sceneStart = Time.realtimeSinceStartup;
        _fsm = null;
        _raycast = null;
    }

    private bool InMain => SceneManager.GetActiveScene().name == "Main";

    private void Update()
    {
        if (!InMain) return;

        if (!_startupReported && Time.realtimeSinceStartup - _sceneStart > StartupReportDelay)
        {
            _startupReported = true;
            ReportStartup();
        }

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
        bool numberKey = keyboard != null && AnyDigitPressed(keyboard);
        if (!clicked && !numberKey) return;

        if (UIModalGuard.IsCapturing)
        {
            Report("modal", $"Input held by a text-entry modal: {UIModalGuard.Describe()}. Hotkeys and world clicks are ignored until it closes.");
            return;
        }
        if (!clicked) return;

        if (_fsm == null) _fsm = FindAnyObjectByType<PlacementStateMachine>();
        if (_fsm == null) { Report("nofsm", "Click ignored: no PlacementStateMachine in the scene."); return; }
        if (!_fsm.enabled) { Report("fsmoff", "Click ignored: PlacementStateMachine is disabled (UI bootstrap did not finish?)."); return; }

        if (_raycast == null) _raycast = FindAnyObjectByType<RaycastController>();
        if (_raycast == null || !_raycast.IsPointerOverUI) return;

        // Over UI. Only worth reporting when the thing under the cursor covers most of the screen:
        // that is an invisible overlay eating every click, not the player pressing a button.
        if (BuildMenuUI.Instance != null && BuildMenuUI.Instance.IsPointerOverBuildMenu) return;
        if (!UIInputGuard.IsPointerOverUIToolkit()) return;
        var blocker = FindBlockingElement(mouse.position.ReadValue(), out float coverage);
        if (blocker != null && coverage >= FullScreenFraction)
            Report("overlay:" + blocker, $"World click swallowed by UI element '{blocker}' covering {coverage:P0} of the screen.");
    }

    private void ReportStartup()
    {
        var fsm = FindAnyObjectByType<PlacementStateMachine>();
        int docs = 0, nullPanel = 0;
        string nullNames = "";
        foreach (var d in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            docs++;
            if (d.panelSettings == null) { nullPanel++; nullNames += d.name + " "; }
        }
        var es = UnityEngine.EventSystems.EventSystem.current;
        Report("startup",
            $"Main ready: FSM={(fsm == null ? "missing" : fsm.enabled ? "enabled" : "DISABLED")}, " +
            $"modal={(UIModalGuard.IsCapturing ? UIModalGuard.Describe() : "none")}, " +
            $"UIDocuments={docs} (no PanelSettings: {nullPanel} {nullNames.Trim()}), " +
            $"EventSystem={(es == null ? "missing" : es.currentInputModule == null ? "no module" : es.currentInputModule.GetType().Name)}, " +
            $"timeScale={Time.timeScale}, screen={Screen.width}x{Screen.height}", force: true);
    }

    /// <summary>Topmost pickable element under the cursor across all documents, with the share of the
    /// screen its bounds cover.</summary>
    private static string FindBlockingElement(Vector2 screenPos, out float coverage)
    {
        coverage = 0f;
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            var root = doc != null ? doc.rootVisualElement : null;
            if (root?.panel == null) continue;
            var panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, screenPos);
            var picked = root.panel.Pick(panelPos);
            if (picked == null || picked == root || picked.pickingMode == PickingMode.Ignore) continue;

            var panelSize = root.panel.visualTree.layout.size;
            float area = panelSize.x * panelSize.y;
            var b = picked.worldBound;
            coverage = area > 0f ? Mathf.Clamp01(b.width * b.height / area) : 0f;
            string parent = picked.parent != null ? picked.parent.name : "";
            return $"{doc.name}/{parent}/{picked.name} ({picked.GetType().Name})";
        }
        return null;
    }

    private static bool AnyDigitPressed(Keyboard k) =>
        k.digit0Key.wasPressedThisFrame || k.digit1Key.wasPressedThisFrame || k.digit2Key.wasPressedThisFrame ||
        k.digit3Key.wasPressedThisFrame || k.digit4Key.wasPressedThisFrame || k.digit5Key.wasPressedThisFrame ||
        k.digit6Key.wasPressedThisFrame || k.digit7Key.wasPressedThisFrame || k.digit8Key.wasPressedThisFrame ||
        k.digit9Key.wasPressedThisFrame;

    private void Report(string reason, string message, bool force = false)
    {
        if (!force)
        {
            if (_reports >= MaxReports) return;
            if (reason == _lastReason && Time.realtimeSinceStartup - _lastReasonTime < SameReasonCooldown) return;
        }
        _lastReason = reason;
        _lastReasonTime = Time.realtimeSinceStartup;
        _reports++;
        Debug.LogWarning("[InputHealth] " + message);
    }
}
