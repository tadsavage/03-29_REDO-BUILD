using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DevConsoleWindow : EditorWindow
{
    // ── Log ──────────────────────────────────────────────────────────────────────
    private enum LogType { Normal, Command, Error }
    private struct LogEntry { public string text; public LogType type; }
    private readonly List<LogEntry> _log = new();
    private Vector2 _scroll;
    private bool _scrollToBottom;

    // ── Input ─────────────────────────────────────────────────────────────────────
    private string _input = "";
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _refocusInput;

    // ── Styles (lazy) ─────────────────────────────────────────────────────────────
    private GUIStyle _styleNormal;
    private GUIStyle _styleCmd;
    private GUIStyle _styleError;
    private GUIStyle _styleInput;
    private bool _stylesReady;

    [MenuItem("Tools/Dev Console %#`")]
    public static void Open()
    {
        var w = GetWindow<DevConsoleWindow>("Dev Console");
        w.minSize = new Vector2(480, 320);
    }

    private void OnEnable()
    {
        _stylesReady = false;
        Print("Dev Console ready.  Type  help  for available commands.");
    }

    // ── GUI ───────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EnsureStyles();
        HandleHistoryKeys();

        float inputAreaH = 26f;
        float logAreaH   = position.height - inputAreaH - 1f;

        // Dark background
        EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.09f, 0.09f, 0.11f));

        // ── Log area ─────────────────────────────────────────────────────────────
        float contentH = 0f;
        foreach (var e in _log)
            contentH += _styleNormal.CalcHeight(new GUIContent(e.text), position.width - 24f) + 2f;
        contentH = Mathf.Max(contentH, logAreaH);

        var logRect     = new Rect(0, 0, position.width, logAreaH);
        var contentRect = new Rect(0, 0, position.width - 16f, contentH);
        _scroll = GUI.BeginScrollView(logRect, _scroll, contentRect);

        float y = 6f;
        foreach (var entry in _log)
        {
            GUIStyle s = entry.type == LogType.Error   ? _styleError
                       : entry.type == LogType.Command ? _styleCmd
                       : _styleNormal;

            float h = s.CalcHeight(new GUIContent(entry.text), position.width - 24f);
            GUI.Label(new Rect(8f, y, position.width - 24f, h), entry.text, s);
            y += h + 2f;
        }
        GUI.EndScrollView();

        if (_scrollToBottom) { _scroll.y = float.MaxValue; _scrollToBottom = false; Repaint(); }

        // ── Separator ────────────────────────────────────────────────────────────
        EditorGUI.DrawRect(new Rect(0, logAreaH, position.width, 1f), new Color(0.25f, 0.25f, 0.28f));

        // ── Input row ────────────────────────────────────────────────────────────
        GUILayout.BeginArea(new Rect(0, logAreaH + 1f, position.width, inputAreaH));
        GUILayout.BeginHorizontal();

        var promptStyle = new GUIStyle(_styleCmd) { margin = new RectOffset(6, 0, 4, 0) };
        GUILayout.Label(">", promptStyle, GUILayout.Width(14));

        GUI.SetNextControlName("DevConsoleInput");
        _input = GUILayout.TextField(_input, _styleInput, GUILayout.ExpandWidth(true));

        if (GUILayout.Button("Run", GUILayout.Width(42), GUILayout.Height(20)))
            Submit();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        if (_refocusInput)
        {
            EditorGUI.FocusTextInControl("DevConsoleInput");
            _refocusInput = false;
        }

        // Return key submit
        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.Return &&
            GUI.GetNameOfFocusedControl() == "DevConsoleInput")
        {
            Submit();
            Event.current.Use();
        }
    }

    private void HandleHistoryKeys()
    {
        if (Event.current.type != EventType.KeyDown) return;
        if (GUI.GetNameOfFocusedControl() != "DevConsoleInput") return;

        if (Event.current.keyCode == KeyCode.UpArrow && _history.Count > 0)
        {
            _historyIndex = Mathf.Min(_historyIndex + 1, _history.Count - 1);
            _input = _history[_historyIndex];
            Event.current.Use();
        }
        else if (Event.current.keyCode == KeyCode.DownArrow)
        {
            _historyIndex = Mathf.Max(_historyIndex - 1, -1);
            _input = _historyIndex >= 0 ? _history[_historyIndex] : "";
            Event.current.Use();
        }
    }

    private void Submit()
    {
        string raw = _input.Trim();
        _input = "";
        _refocusInput = true;
        if (string.IsNullOrEmpty(raw)) return;

        _history.Insert(0, raw);
        _historyIndex = -1;

        Print($"> {raw}", LogType.Command);
        Dispatch(raw);
        _scrollToBottom = true;
        Repaint();
    }

    // ── Command dispatch ──────────────────────────────────────────────────────────

    private void Dispatch(string raw)
    {
        var parts = raw.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        string[] args = new string[parts.Length - 1];
        if (parts.Length > 1) System.Array.Copy(parts, 1, args, 0, args.Length);

        switch (parts[0].ToLower())
        {
            case "help":       CmdHelp();             break;
            case "clear":
            case "cls":        _log.Clear();          break;
            case "objects":    CmdObjects();          break;
            case "bake":       CmdBake();             break;
            case "money":      CmdMoney(args);        break;
            case "addmoney":   CmdAddMoney(args);     break;
            case "spawn":      CmdSpawn(args);        break;
            case "clearworld": CmdClearWorld();       break;
            case "save":       CmdSave();             break;
            case "load":       CmdLoad();             break;
            case "agents":     CmdAgents();           break;
            case "timescale":  CmdTimescale(args);    break;
            default:
                Error($"Unknown command '{parts[0]}'. Type  help  for the list.");
                break;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────────

    private void CmdHelp()
    {
        Print("─────────────────────────────────────────────────────");
        Print("  help                      — this list");
        Print("  clear / cls               — clear the log");
        Print("  objects                   — list all ObjDataSO ids & names");
        Print("  bake                  [P] — rebuild NavMesh");
        Print("  money <amount>        [P] — set money to exact amount");
        Print("  addmoney <amount>     [P] — add (or subtract) money");
        Print("  spawn <id> <x> <y> [rot]  — place object at grid cell");
        Print("      rot: 0=0° 1=90° 2=180° 3=270°");
        Print("  clearworld            [P] — destroy all placed objects");
        Print("  save                  [P] — quicksave to autosave");
        Print("  load                  [P] — quickload autosave");
        Print("  agents                [P] — list active AI agents");
        Print("  timescale <x>         [P] — set sim-time speed multiplier");
        Print("─────────────────────────────────────────────────────");
        Print("  [P] = requires Play mode");
    }

    private void CmdBake()
    {
        if (!NeedPlay()) return;
        var nm = Object.FindAnyObjectByType<NavMeshManager>();
        if (nm == null) { Error("NavMeshManager not found."); return; }
        nm.BakeSynchronous();
        Print("NavMesh bake complete.");
    }

    private void CmdMoney(string[] args)
    {
        if (!NeedPlay()) return;
        if (args.Length == 0 || !int.TryParse(args[0], out int amount))
        { Error("Usage: money <amount>"); return; }
        var ctx = Object.FindAnyObjectByType<GameContext>();
        if (ctx == null) { Error("GameContext not found."); return; }
        ctx.MoneyService.SetMoney(amount);
        Print($"Money → ${amount:N0}");
    }

    private void CmdAddMoney(string[] args)
    {
        if (!NeedPlay()) return;
        if (args.Length == 0 || !int.TryParse(args[0], out int delta))
        { Error("Usage: addmoney <amount>"); return; }
        var ctx = Object.FindAnyObjectByType<GameContext>();
        if (ctx == null) { Error("GameContext not found."); return; }
        if (delta >= 0) ctx.MoneyService.Refund(delta, "Dev Console");
        else            ctx.MoneyService.Deduct(-delta, "Dev Console");
        Print($"{(delta >= 0 ? "+" : "")}{delta:N0}  →  ${ctx.MoneyService.CurrentCapital:N0}");
    }

    private void CmdSpawn(string[] args)
    {
        if (!NeedPlay()) return;
        if (args.Length < 3 ||
            !int.TryParse(args[0], out int id) ||
            !int.TryParse(args[1], out int x)  ||
            !int.TryParse(args[2], out int y))
        { Error("Usage: spawn <id> <x> <y> [rot 0-3]"); return; }

        int rot = args.Length >= 4 && int.TryParse(args[3], out int r) ? Mathf.Clamp(r, 0, 3) : 0;

        var reg = FindRegistry();
        if (reg == null) { Error("ObjDataRegistry asset not found."); return; }
        var so = reg.GetByID(id);
        if (so == null) { Error($"No object with id {id}. Use  objects  to list ids."); return; }

        var ps = Object.FindAnyObjectByType<PlacementSystem>();
        if (ps == null) { Error("PlacementSystem not found."); return; }

        ps.SpawnFromSave(so, x, y, rot);
        Print($"Spawned '{so.objName}' (id {id}) at ({x},{y}) rot={rot * 90}°");
    }

    private void CmdClearWorld()
    {
        if (!NeedPlay()) return;
        var snapshot = PlacedObjectRegistry.GetSnapshot();
        foreach (var po in snapshot)
            if (po != null) Object.Destroy(po.gameObject);
        Print($"Removed {snapshot.Length} placed object(s).");
    }

    private void CmdSave()
    {
        if (!NeedPlay()) return;
        var ps = Object.FindAnyObjectByType<PlacementSystem>();
        if (ps == null) { Error("PlacementSystem not found."); return; }
        ps.SaveGame("autosave");
        Print("Saved to autosave.");
    }

    private void CmdLoad()
    {
        if (!NeedPlay()) return;
        var ps = Object.FindAnyObjectByType<PlacementSystem>();
        if (ps == null) { Error("PlacementSystem not found."); return; }
        ps.LoadGame();
        Print("Autosave loaded.");
    }

    private void CmdAgents()
    {
        if (!NeedPlay()) return;
        var agents = Object.FindObjectsByType<AiNavigation>();
        Print($"{agents.Length} agent(s):");
        foreach (var a in agents)
            Print($"  {a.gameObject.name,-28} role={a.role}");
    }

    private void CmdTimescale(string[] args)
    {
        if (!NeedPlay()) return;
        if (args.Length == 0 || !float.TryParse(args[0], out float scale))
        { Error("Usage: timescale <multiplier>  (e.g. 0.5  1  10)"); return; }
        var ctx = Object.FindAnyObjectByType<GameContext>();
        if (ctx == null) { Error("GameContext not found."); return; }
        ctx.TimeService.SetTimeScale(scale);
        Print($"Sim time scale → {scale}×");
    }

    private void CmdObjects()
    {
        var reg = FindRegistry();
        if (reg == null) { Error("ObjDataRegistry asset not found."); return; }
        Print($"{reg.buttonSOs.Count} registered objects:");
        foreach (var so in reg.buttonSOs)
            if (so != null)
                Print($"  {so.id,-5} {so.objName,-30} [{so.category}]");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private bool NeedPlay()
    {
        if (EditorApplication.isPlaying) return true;
        Error("This command requires Play mode.");
        return false;
    }

    private void Print(string msg, LogType type = LogType.Normal)
        => _log.Add(new LogEntry { text = msg, type = type });

    private void Error(string msg)
        => _log.Add(new LogEntry { text = msg, type = LogType.Error });

    private ObjDataRegistry FindRegistry()
    {
        string[] guids = AssetDatabase.FindAssets("t:ObjDataRegistry");
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<ObjDataRegistry>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private void EnsureStyles()
    {
        if (_stylesReady) return;
        _stylesReady = true;

        _styleNormal = new GUIStyle(EditorStyles.label)
        {
            wordWrap  = true,
            richText  = true,
            fontSize  = 12,
            normal    = { textColor = new Color(0.78f, 0.78f, 0.82f) }
        };

        _styleCmd = new GUIStyle(_styleNormal)
        {
            normal = { textColor = new Color(0.45f, 0.90f, 0.50f) }
        };

        _styleError = new GUIStyle(_styleNormal)
        {
            normal = { textColor = new Color(1f, 0.38f, 0.38f) }
        };

        _styleInput = new GUIStyle(EditorStyles.textField)
        {
            fontSize = 12,
            margin   = new RectOffset(0, 4, 4, 4)
        };
    }
}
