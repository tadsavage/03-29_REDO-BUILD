using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Common surface for the four TopBar dropdown panels (Capital/Hourly/Spent Today/Time)
/// so TopBarUI can toggle/hide them uniformly without knowing each concrete type. Root exposes
/// the panel's own VisualElement so TopBarUI can register hover tracking on it for auto-close.</summary>
public interface ITopBarPanel
{
    bool IsVisible { get; }
    VisualElement Root { get; }
    void Show();
    void Hide();
}

/// <summary>
/// Shared visual building blocks for the Capital/Hourly/Spent Today/Shift dropdown panels
/// (TopBarUI). Extracted from the original single FinancialBreakdownPanel so all four panels
/// share one consistent look instead of duplicating the same row/label/color code four times.
/// </summary>
public static class FinanceUIKit
{
    // ── Palette (matches TopBar / HiringBoard) ────────────────────────────────
    public static readonly Color ColBg          = new Color(0.078f, 0.110f, 0.149f, 0.97f);
    public static readonly Color ColOrange      = new Color(0.941f, 0.494f, 0.176f, 1f);   // rgb(240,126,45)
    public static readonly Color ColOrangeDark  = new Color(0.65f,  0.32f,  0.09f,  1f);
    public static readonly Color ColBlueDark    = new Color(0.05f,  0.22f,  0.36f,  1f);
    public static readonly Color ColBlueTint    = new Color(0.75f,  0.88f,  0.96f,  1f);
    public static readonly Color ColRowA        = new Color(0.10f,  0.14f,  0.18f,  1f);
    public static readonly Color ColRowB        = new Color(0.12f,  0.17f,  0.22f,  1f);
    public static readonly Color ColValueBg     = new Color(0.06f,  0.13f,  0.22f,  1f);
    public static readonly Color ColTotalBg     = new Color(0.05f,  0.19f,  0.31f,  1f);
    public static readonly Color ColHoverRow    = new Color(0.18f,  0.26f,  0.35f,  1f);
    public static readonly Color ColNetPos      = new Color(0.65f,  0.32f,  0.09f,  1f);
    public static readonly Color ColNetNeg      = new Color(0.48f,  0.07f,  0.07f,  1f);
    public static readonly Color ColTooltipBg   = new Color(0.05f,  0.08f,  0.12f,  0.98f);
    public static readonly Color ColBorder      = new Color(0.36f,  0.61f,  0.77f,  0.5f);
    public static readonly Color ColLabelNormal = new Color(0.75f,  0.80f,  0.85f,  1f);
    public static readonly Color ColLabelHover  = new Color(0.88f,  0.92f,  0.96f,  1f);

    // Capital tab's "Revenue" header — green bg, light yellow text.
    public static readonly Color ColRevenueGreen  = new Color(0.16f, 0.45f, 0.20f, 1f);
    public static readonly Color ColRevenueYellow = new Color(0.98f, 0.95f, 0.65f, 1f);

    // Hourly tab's "Expenses" header — red bg, white text.
    public static readonly Color ColExpenseRed    = new Color(0.55f, 0.10f, 0.10f, 1f);
    public static readonly Color ColExpenseWhite  = Color.white;
    public static readonly Color ColMuted       = new Color(0.50f,  0.55f,  0.60f,  1f);

    public const float Width        = 360f;
    public const float TooltipWidth = 230f;
    public const float RowHeight    = 28f;
    public const float HeaderHeight = 24f;
    public const float ValueWidth   = 100f;
    public const string FontClass   = "fin-lilita";

    // "Large" scheme — same font size as the Reputation dropdown, shared so every TopBar panel
    // that adopts it (Capital/Hourly/Spent Today/Shift Status) reads consistently. Row/header
    // height are deliberately NOT a full 2x (that made the 19-row Hourly panel run off the bottom
    // of the screen) — tightened to the minimum that still comfortably fits a 26px label without
    // clipping, rather than shrinking the font to solve the same problem.
    public const float LargeKeySize    = 26f;
    public const float LargeValueSize  = 26f;
    public const float LargeHeaderSize = 28f;
    public const float LargeRowHeight    = 43f;
    public const float LargeHeaderHeight = 40f;
    public const float LargeValueWidth = ValueWidth * 1.4f;
    public const float LargeWidth      = Width * 1.3f;

    public static VisualElement Panel()
    {
        var panel = new VisualElement();
        panel.style.position                = Position.Absolute;
        panel.style.top                     = 44f;
        panel.style.left                    = 0f;
        panel.style.width                   = Width;
        panel.style.backgroundColor         = new StyleColor(ColBg);
        panel.style.borderBottomLeftRadius  = 6f;
        panel.style.borderBottomRightRadius = 6f;
        panel.style.borderBottomColor       = new StyleColor(ColBorder);
        panel.style.borderBottomWidth       = 1f;
        panel.style.borderLeftColor         = new StyleColor(ColBorder);
        panel.style.borderLeftWidth         = 1f;
        panel.style.borderRightColor        = new StyleColor(ColBorder);
        panel.style.borderRightWidth        = 1f;
        panel.pickingMode                   = PickingMode.Position;
        return panel;
    }

    public static VisualElement SectionHeader(string text, Color bg, Color textColor)
        => SectionHeader(text, bg, textColor, 14f, HeaderHeight);

    public static VisualElement SectionHeader(string text, Color bg, Color textColor, float fontSize, float height)
    {
        var row = new VisualElement();
        row.style.backgroundColor = new StyleColor(bg);
        row.style.height          = height;
        row.style.justifyContent  = Justify.Center;
        row.style.alignItems      = Align.Center;
        row.style.borderTopWidth  = 1f;
        row.style.borderTopColor  = new StyleColor(new Color(1f, 1f, 1f, 0.08f));

        var lbl = Lbl(text, bold: true, size: fontSize);
        lbl.style.color          = new StyleColor(textColor);
        lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
        row.Add(lbl);
        return row;
    }

    public static VisualElement DataRow(string key, Label val, Color bg, bool expandable)
        => DataRow(key, val, bg, expandable, 13f, RowHeight, ValueWidth);

    public static VisualElement DataRow(string key, Label val, Color bg, bool expandable,
        float keySize, float rowHeight, float valueWidth)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.backgroundColor = new StyleColor(bg);
        row.style.height          = rowHeight;
        row.style.alignItems      = Align.Center;

        var keyLbl = Lbl(expandable ? key + " ▾" : key, size: keySize);
        keyLbl.style.flexGrow    = 1f;
        keyLbl.style.paddingLeft = 10f;
        keyLbl.style.color       = new StyleColor(expandable ? ColLabelHover : ColLabelNormal);

        val.style.fontSize        = keySize;
        val.style.width           = valueWidth;
        val.style.unityTextAlign  = TextAnchor.MiddleRight;
        val.style.paddingRight    = 10f;
        val.style.backgroundColor = new StyleColor(ColValueBg);

        row.Add(keyLbl);
        row.Add(val);
        return row;
    }

    public static VisualElement TotalRow(string key, Label val, Color bg, Color keyColor)
        => TotalRow(key, val, bg, keyColor, 13f, RowHeight + 2f, ValueWidth);

    public static VisualElement TotalRow(string key, Label val, Color bg, Color keyColor,
        float keySize, float rowHeight, float valueWidth)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.backgroundColor = new StyleColor(bg);
        row.style.height          = rowHeight;
        row.style.alignItems      = Align.Center;
        row.style.borderTopWidth  = 1f;
        row.style.borderTopColor  = new StyleColor(new Color(1f, 1f, 1f, 0.08f));

        var keyLbl = Lbl(key, bold: true, size: keySize);
        keyLbl.style.flexGrow    = 1f;
        keyLbl.style.paddingLeft = 10f;
        keyLbl.style.color       = new StyleColor(keyColor);

        val.style.fontSize       = keySize;
        val.style.width          = valueWidth;
        val.style.unityTextAlign = TextAnchor.MiddleRight;
        val.style.paddingRight   = 10f;

        row.Add(keyLbl);
        row.Add(val);
        return row;
    }

    /// <summary>Flush against the trigger's own left edge and directly below it — shared by every
    /// TopBar dropdown so each opens under ITS OWN box rather than Panel()'s generic top:44/left:0
    /// default (which only happens to line up for Capital, the leftmost box).</summary>
    public static void PositionUnderTrigger(VisualElement panel, VisualElement trigger)
    {
        if (trigger == null) return;
        var b = trigger.worldBound;
        if (float.IsNaN(b.x) || float.IsNaN(b.y)) return; // not yet laid out
        panel.style.left = b.x;
        panel.style.top = b.yMax;
    }

    public static VisualElement TipRow(string key, int value, bool alt)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.backgroundColor = new StyleColor(alt ? ColRowA : ColRowB);
        row.style.height          = 24f;
        row.style.alignItems      = Align.Center;

        var keyLbl = Lbl(key, size: 11f);
        keyLbl.style.flexGrow    = 1f;
        keyLbl.style.paddingLeft = 8f;
        keyLbl.style.color       = new StyleColor(ColLabelNormal);

        var valLbl = Lbl(FormatMoney(value), size: 11f);
        valLbl.style.width           = 82f;
        valLbl.style.unityTextAlign  = TextAnchor.MiddleRight;
        valLbl.style.paddingRight    = 8f;
        valLbl.style.backgroundColor = new StyleColor(ColValueBg);
        valLbl.style.color           = new StyleColor(ColOrange);

        row.Add(keyLbl);
        row.Add(valLbl);
        return row;
    }

    public static VisualElement EmptyMsg(string msg)
    {
        var lbl = Lbl(msg, size: 11f);
        lbl.style.color         = new StyleColor(ColMuted);
        lbl.style.paddingTop    = 6f;
        lbl.style.paddingBottom = 6f;
        lbl.style.paddingLeft   = 8f;
        return lbl;
    }

    public static Label Lbl(string text = "", bool bold = false, float size = 13f)
    {
        var lbl = new Label(text);
        lbl.AddToClassList(FontClass);
        lbl.style.color                   = new StyleColor(Color.white);
        lbl.style.fontSize                = size;
        lbl.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        lbl.style.unityTextAlign          = TextAnchor.MiddleLeft;
        return lbl;
    }

    public static Label ValueLabel(bool bold = false, Color? color = null)
    {
        var lbl = Lbl("$0", bold);
        if (color.HasValue) lbl.style.color = new StyleColor(color.Value);
        return lbl;
    }

    public static string FormatMoney(int amount)
        => (amount < 0 ? "-$" : "$") + Mathf.Abs(amount).ToString("N0");
}
