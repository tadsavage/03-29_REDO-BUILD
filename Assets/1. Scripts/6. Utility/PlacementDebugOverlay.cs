using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlacementDebugOverlay : MonoBehaviour
{
    [SerializeField] private PlacementStateMachine fsm;
    [SerializeField] private RaycastController raycast;
    [SerializeField] private PlacementGrid grid;
    [SerializeField] private PlacementValidator validator;

    private TextMeshProUGUI _text;
    private Canvas _canvas;

    private float _fpsTimer;
    private int _fpsCount;
    private float _fps;

    private void Start()
    {
        EnsureCanvas();
        EnsureText();
    }

    private void EnsureCanvas()
    {
        var existing = GameObject.Find("PlacementDebugCanvas");
        if (existing != null)
        {
            _canvas = existing.GetComponent<Canvas>();
            return;
        }

        GameObject c = new GameObject("PlacementDebugCanvas");
        _canvas = c.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 32767;

        var scaler = c.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        c.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(c);
    }

    private void EnsureText()
    {
        if (_text != null)
            return;

        GameObject go = new GameObject("PlacementDebugText");
        go.transform.SetParent(_canvas.transform, false);

        _text = go.AddComponent<TextMeshProUGUI>();
        _text.color = Color.antiqueWhite;
        _text.fontSize = 14;
        _text.enableAutoSizing = true;
        _text.fontSizeMin = 6;
        _text.fontSizeMax = 18;
        _text.alignment = TextAlignmentOptions.TopLeft;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(700, 500);
        rt.anchoredPosition = new Vector2(10, -10);
    }

    private void Update()
    {
        if (_text == null)
            return;

        UpdateFPS();

        // --- State name ---
        string stateName = fsm != null
            ? fsm.CurrentState?.GetType().Name ?? "None"
            : "No FSM";

        // --- Hit cell ---
        Vector2Int hitCell = raycast != null && raycast.HasHit
            ? raycast.HitCell
            : new Vector2Int(-1, -1);

        // --- Stack height ---
        float stackHeight = (grid != null && raycast != null && raycast.HasHit)
            ? grid.GetStackHeight(hitCell)
            : 0f;

        // --- Validator result ---
        bool valid = false;
        if (validator != null && raycast != null && raycast.HasHit && fsm.CurrentState is BuildState bs)
        {
            valid = validator.IsCellValid(hitCell, bs.CurrentData);
        }

        // --- Drag state ---
        bool dragging = fsm.CurrentState is BuildState build && build.IsDragging;

        // --- MoveState info ---
        string moveInfo = "None";
        if (fsm.CurrentState is MoveState move)
        {
            moveInfo = $"Moving: {move.ObjectName}, TargetCell: {hitCell}";
        }

        _text.text =
            $"FPS: {_fps:F1}\n" +
            $"State: {stateName}\n" +
            $"Hit Cell: {hitCell}\n" +
            $"Stack Height: {stackHeight}\n" +
            $"Valid Placement: {valid}\n" +
            $"Dragging: {dragging}\n" +
            $"MoveState: {moveInfo}";
    }

    private void UpdateFPS()
    {
        _fpsCount++;
        _fpsTimer += Time.unscaledDeltaTime;

        if (_fpsTimer >= 0.5f)
        {
            _fps = _fpsCount / _fpsTimer;
            _fpsCount = 0;
            _fpsTimer = 0f;
        }
    }
}
