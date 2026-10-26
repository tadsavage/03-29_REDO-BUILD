using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Diagnostics;

/// <summary>
/// HAL 9000's running score while HAL plays. It sits in the bottom bar just left of the dock schedule and shows the score so far this
/// block plus the last five things that moved it, each with its cause. That is all it does: it does not move or resize. Double-click it
/// to open the full SUMMARY window (HalSummaryWindow). Only appears once a HAL block has started; while you play, the purple box at the
/// top of the screen (PlayerScoreHud) takes its place.
/// </summary>
public static class HalScoreModal
{
    const float DockedWidth = 340f, DockedHeight = 102f, HeaderH = 24f, RowH = 15f;
    static VisualElement _root, _head;
    static Label _score, _sub, _title;
    static VisualElement _list;
    static int _shownCount = -1; static float _shownTotal = -1f;
    static readonly Color Blue = new Color(0.25f, 0.65f, 1f), Good = new Color(0.35f, 0.9f, 0.45f), Bad = new Color(1f, 0.4f, 0.35f), Dim = new Color(0.62f, 0.74f, 0.84f), Purple = new Color(0.72f, 0.40f, 1f);

    /// <summary>Put the modal directly in front of the dock schedule, in whichever bar that currently lives in.</summary>
    public static void DockBefore(VisualElement timeline)
    {
        if (timeline == null || timeline.parent == null) return;
        if (_root == null) Build();
        var bar = timeline.parent;
        timeline.style.marginLeft = 0;   // the modal takes the free space instead, so the two sit side by side
        if (_root.parent != bar || bar.IndexOf(_root) != bar.IndexOf(timeline) - 1)
        {
            _root.RemoveFromHierarchy();
            bar.Insert(bar.IndexOf(timeline), _root);
        }
        _shownCount = -1;
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
        _root.style.width = DockedWidth; _root.style.height = DockedHeight;
        _root.style.alignSelf = Align.Center; _root.style.marginLeft = StyleKeyword.Auto; _root.style.marginRight = 10;
        _root.style.backgroundColor = new Color(0.08f, 0.11f, 0.15f, 0.96f);
        _root.style.borderTopWidth = _root.style.borderBottomWidth = _root.style.borderLeftWidth = _root.style.borderRightWidth = 2;
        _root.style.borderTopColor = _root.style.borderBottomColor = _root.style.borderLeftColor = _root.style.borderRightColor = Blue;
        _root.style.borderTopLeftRadius = _root.style.borderTopRightRadius = _root.style.borderBottomLeftRadius = _root.style.borderBottomRightRadius = 6;
        _root.style.paddingLeft = _root.style.paddingRight = 8; _root.style.paddingTop = 1; _root.style.paddingBottom = 0;
        _root.style.display = DisplayStyle.None;

        _head = new VisualElement();
        _head.style.flexDirection = FlexDirection.Row; _head.style.justifyContent = Justify.SpaceBetween; _head.style.alignItems = Align.Center;
        _head.style.height = HeaderH; _head.style.flexShrink = 0;
        _title = L(9, Purple, true); _title.text = "HAL SCORE"; _title.style.height = HeaderH;
        _sub = L(8, Dim); _sub.style.flexGrow = 1; _sub.style.marginLeft = 8; _sub.style.height = HeaderH;
        _score = L(19, Color.white, true); _score.style.height = HeaderH; _score.style.unityTextAlign = TextAnchor.MiddleRight;
        _head.Add(_title); _head.Add(_sub); _head.Add(_score);
        _root.Add(_head);

        _list = new VisualElement();
        _list.style.flexGrow = 1; _list.style.flexShrink = 1; _list.style.overflow = Overflow.Hidden;
        _root.Add(_list);

        // Double-click anywhere on the box opens the SUMMARY window.
        _root.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0 && e.clickCount == 2) { HalSummaryWindow.Toggle(); e.StopPropagation(); } });
        _root.schedule.Execute(Refresh).Every(400);
    }

    static void Refresh()
    {
        var bot = PlaytestBot.Instance;
        bool show = bot != null && (bot.Running || bot.BlockNo > 0) && !bot.ObserveOnly;   // in a player's block the top-centre score replaces this
        _root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (!show) return;

        _score.text = bot.LiveScore.ToString("0.0");
        _sub.text = bot.Running ? $"BLOCK {bot.BlockNo}  DAY {Mathf.Min(bot.BlockDay, bot.S.daysPerBlock)}/{bot.S.daysPerBlock}" : $"BLOCK {bot.BlockNo} DONE";

        var changes = bot.RecentScoreChanges;
        int want = Mathf.Min(5, changes.Count);
        if (want == _shownCount && Mathf.Approximately(_shownTotal, bot.LiveScore)) return;   // nothing new: do not rebuild
        _shownCount = want; _shownTotal = bot.LiveScore;

        _list.Clear();
        if (changes.Count == 0)
        {
            var none = L(9, Dim); none.text = "No changes yet"; none.style.height = RowH; _list.Add(none);
            return;
        }
        for (int i = 0; i < want; i++)
        {
            var c = changes[i];
            var r = L(9, c.delta >= 0 ? Good : Bad);
            r.style.height = RowH;
            r.text = $"{(c.delta >= 0 ? "+" : "")}{c.delta:0.0}  {c.cause}";
            _list.Add(r);
        }
    }
}
