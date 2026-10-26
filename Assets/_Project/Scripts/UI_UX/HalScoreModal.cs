using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Diagnostics;

/// <summary>
/// HAL 9000's running score. Docked, it sits in the bottom bar just left of the dock schedule and shows the score so far this block plus
/// the last five things that moved it, each with its cause. DRAG the title bar to pull it out into a floating window; drag its bottom-right
/// corner to make it as tall or wide as you like (it scrolls through every change this block); double-click the title bar to put it back.
/// Only appears once a block has started.
/// </summary>
public static class HalScoreModal
{
    const float DockedWidth = 340f, DockedHeight = 102f, HeaderH = 24f, RowH = 15f;
    static VisualElement _root, _head, _grip;
    static Label _score, _sub, _title;
    static ScrollView _list;
    static readonly List<Label> _rows = new();
    static bool _floating;
    static int _shownCount = -1; static float _shownTotal = -1f;
    static readonly Color Blue = new Color(0.25f, 0.65f, 1f), Good = new Color(0.35f, 0.9f, 0.45f), Bad = new Color(1f, 0.4f, 0.35f), Dim = new Color(0.62f, 0.74f, 0.84f);

    /// <summary>Put the modal directly in front of the dock schedule, in whichever bar that currently lives in (unless it is floating).</summary>
    public static void DockBefore(VisualElement timeline)
    {
        if (timeline == null || timeline.parent == null) return;
        if (_root == null) Build();
        if (_floating) return;
        var bar = timeline.parent;
        timeline.style.marginLeft = 0;   // the modal takes the free space instead, so the two sit side by side
        if (_root.parent != bar || bar.IndexOf(_root) != bar.IndexOf(timeline) - 1)
        {
            _root.RemoveFromHierarchy();
            bar.Insert(bar.IndexOf(timeline), _root);
        }
        ApplyDockedStyle();
    }

    static Label L(int size, Color col, bool bold = false)
    {
        var l = new Label();
        l.pickingMode = PickingMode.Ignore;
        l.style.fontSize = size; l.style.color = col; l.style.whiteSpace = WhiteSpace.NoWrap; l.style.overflow = Overflow.Hidden;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        if (HalDevTabs.HalFont != null) l.style.unityFontDefinition = FontDefinition.FromFont(HalDevTabs.HalFont);
        l.style.flexShrink = 0;   // the wide Orbitron lines are taller than their box: without this the layout squeezed the header (and the score) out of view
        l.style.paddingTop = l.style.paddingBottom = l.style.paddingLeft = l.style.paddingRight = 0;
        l.style.marginTop = l.style.marginBottom = 0;
        l.style.unityTextAlign = TextAnchor.MiddleLeft;
        return l;
    }

    static void Build()
    {
        _root = new VisualElement { name = "HalScoreModal" };
        _root.pickingMode = PickingMode.Position;
        _root.style.flexShrink = 0; _root.style.overflow = Overflow.Hidden;
        _root.style.backgroundColor = new Color(0.08f, 0.11f, 0.15f, 0.96f);
        _root.style.borderTopWidth = _root.style.borderBottomWidth = _root.style.borderLeftWidth = _root.style.borderRightWidth = 2;
        _root.style.borderTopColor = _root.style.borderBottomColor = _root.style.borderLeftColor = _root.style.borderRightColor = Blue;
        _root.style.borderTopLeftRadius = _root.style.borderTopRightRadius = _root.style.borderBottomLeftRadius = _root.style.borderBottomRightRadius = 6;
        _root.style.paddingLeft = _root.style.paddingRight = 8; _root.style.paddingTop = 1; _root.style.paddingBottom = 0;
        _root.style.display = DisplayStyle.None;

        _head = new VisualElement();
        _head.style.flexDirection = FlexDirection.Row; _head.style.justifyContent = Justify.SpaceBetween; _head.style.alignItems = Align.Center;
        _head.style.height = HeaderH; _head.style.flexShrink = 0;
        _title = L(9, Blue, true); _title.text = "HAL SCORE"; _title.style.height = HeaderH;
        _sub = L(8, Dim); _sub.style.flexGrow = 1; _sub.style.marginLeft = 8; _sub.style.height = HeaderH;
        _score = L(19, Color.white, true); _score.style.height = HeaderH; _score.style.unityTextAlign = TextAnchor.MiddleRight;
        _head.Add(_title); _head.Add(_sub); _head.Add(_score);
        _root.Add(_head);

        _list = new ScrollView(ScrollViewMode.Vertical);
        _list.style.flexGrow = 1; _list.style.flexShrink = 1;
        _list.verticalScrollerVisibility = ScrollerVisibility.Auto;
        _root.Add(_list);

        // resize corner (only shown while floating)
        _grip = new VisualElement();
        _grip.style.position = Position.Absolute; _grip.style.right = 0; _grip.style.bottom = 0; _grip.style.width = 18; _grip.style.height = 18;
        _grip.style.backgroundColor = new Color(0.25f, 0.65f, 1f, 0.55f);
        _grip.style.borderTopLeftRadius = 6;
        _grip.style.display = DisplayStyle.None;
        _root.Add(_grip);

        WireDrag();
        WireResize();
        _root.schedule.Execute(Refresh).Every(400);
    }

    // ── docked / floating ─────────────────────────────────────────────────────

    static void ApplyDockedStyle()
    {
        _root.style.position = Position.Relative;
        _root.style.left = StyleKeyword.Auto; _root.style.top = StyleKeyword.Auto;
        _root.style.width = DockedWidth; _root.style.height = DockedHeight;
        _root.style.alignSelf = Align.Center; _root.style.marginLeft = StyleKeyword.Auto; _root.style.marginRight = 10;
        _grip.style.display = DisplayStyle.None;
        _title.text = "HAL SCORE";
        _shownCount = -1;   // re-render for the 5-row docked view
    }

    static void Undock(Vector2 panelPos)
    {
        var top = _root.panel != null ? _root.panel.visualTree : null;
        if (top == null) return;
        var wb = _root.worldBound;
        _root.RemoveFromHierarchy();
        top.Add(_root);
        _floating = true;
        _root.style.position = Position.Absolute;
        _root.style.alignSelf = Align.Auto; _root.style.marginLeft = 0; _root.style.marginRight = 0;
        _root.style.left = wb.x; _root.style.top = Mathf.Max(0f, wb.y - 260f);
        _root.style.width = 460f; _root.style.height = 380f;
        _grip.style.display = DisplayStyle.Flex;
        _title.text = "HAL SCORE  (double-click to dock)";
        _root.BringToFront();
        _shownCount = -1;
    }

    static void Redock()
    {
        _floating = false;
        var bar = BuildMenuUI.Instance != null ? BuildMenuUI.Instance.ActiveBar : null;
        var timeline = bar != null ? bar.Q<VisualElement>("MiniDockTimeline") : null;
        _root.RemoveFromHierarchy();
        if (timeline != null) DockBefore(timeline);
        _shownCount = -1;
    }

    static void WireDrag()
    {
        Vector2 startPointer = default, startPos = default; bool down = false, moved = false;
        _head.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            if (e.clickCount == 2) { if (_floating) Redock(); e.StopPropagation(); return; }
            down = true; moved = false; startPointer = e.position;
            if (_floating) startPos = new Vector2(_root.resolvedStyle.left, _root.resolvedStyle.top);
            _head.CapturePointer(e.pointerId);
        });
        _head.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!down || !_head.HasPointerCapture(e.pointerId)) return;
            Vector2 d = (Vector2)e.position - startPointer;
            if (!_floating)
            {
                if (d.magnitude < 8f) return;      // a click is not a drag
                Undock(e.position);
                startPos = new Vector2(_root.resolvedStyle.left, _root.resolvedStyle.top);
                startPointer = e.position; d = Vector2.zero;
            }
            moved = true;
            _root.style.left = Mathf.Max(0f, startPos.x + d.x);
            _root.style.top = Mathf.Max(0f, startPos.y + d.y);
        });
        _head.RegisterCallback<PointerUpEvent>(e =>
        {
            down = false;
            if (_head.HasPointerCapture(e.pointerId)) _head.ReleasePointer(e.pointerId);
        });
    }

    static void WireResize()
    {
        Vector2 startPointer = default, startSize = default; bool down = false;
        _grip.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0 || !_floating) return;
            down = true; startPointer = e.position; startSize = new Vector2(_root.resolvedStyle.width, _root.resolvedStyle.height);
            _grip.CapturePointer(e.pointerId); e.StopPropagation();
        });
        _grip.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!down || !_grip.HasPointerCapture(e.pointerId)) return;
            Vector2 d = (Vector2)e.position - startPointer;
            _root.style.width = Mathf.Clamp(startSize.x + d.x, 300f, 1200f);
            _root.style.height = Mathf.Clamp(startSize.y + d.y, 120f, 1000f);
        });
        _grip.RegisterCallback<PointerUpEvent>(e =>
        {
            down = false;
            if (_grip.HasPointerCapture(e.pointerId)) _grip.ReleasePointer(e.pointerId);
        });
    }

    // ── content ───────────────────────────────────────────────────────────────

    static void Refresh()
    {
        var bot = PlaytestBot.Instance;
        bool show = bot != null && (bot.Running || bot.BlockNo > 0) && !bot.ObserveOnly;   // in a player's block the top-centre score replaces this
        _root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (!show) return;

        _score.text = bot.LiveScore.ToString("0.0");
        _sub.text = bot.Running ? $"BLOCK {bot.BlockNo}  DAY {Mathf.Min(bot.BlockDay, bot.S.daysPerBlock)}/{bot.S.daysPerBlock}" : $"BLOCK {bot.BlockNo} DONE";

        var changes = bot.RecentScoreChanges;
        int want = _floating ? changes.Count : Mathf.Min(5, changes.Count);
        if (want == _shownCount && Mathf.Approximately(_shownTotal, bot.LiveScore)) return;   // nothing new: do not rebuild
        _shownCount = want; _shownTotal = bot.LiveScore;

        _list.Clear(); _rows.Clear();
        if (changes.Count == 0)
        {
            var none = L(9, Dim); none.text = "No changes yet"; none.style.height = RowH; _list.Add(none);
            return;
        }
        for (int i = 0; i < want; i++)
        {
            var c = changes[i];
            var r = L(_floating ? 11 : 9, c.delta >= 0 ? Good : Bad);
            r.style.height = _floating ? 18 : RowH;
            r.text = _floating ? $"{c.when}   {(c.delta >= 0 ? "+" : "")}{c.delta:0.0}   {c.cause}   (score {c.total:0.0})" : $"{(c.delta >= 0 ? "+" : "")}{c.delta:0.0}  {c.cause}";
            _list.Add(r);
        }
    }
}
