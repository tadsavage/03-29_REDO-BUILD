using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Small Painter2D charts for the Reports windows. Painter2D draws shapes but not text, so axis/value labels are
/// ordinary Label children, repositioned by <see cref="Refresh"/> (never from inside the repaint callback, which
/// must not change the hierarchy). Call Refresh() after changing a chart's data or whenever its size may have changed.
/// </summary>
public abstract class ReportChartBase : VisualElement
{
    protected static readonly Color GridCol   = new Color(1f, 1f, 1f, 0.10f);
    protected static readonly Color AxisCol   = new Color(0.36f, 0.61f, 0.77f, 0.9f);
    protected static readonly Color PlotBgCol = new Color(0.04f, 0.08f, 0.12f, 0.55f);
    protected static readonly Color LabelCol  = new Color(0.70f, 0.80f, 0.88f, 1f);

    private readonly List<Label> _labels = new();
    private int _labelsUsed;

    protected ReportChartBase()
    {
        style.flexShrink = 0;
        generateVisualContent += ctx => Draw(ctx.painter2D, contentRect);
        RegisterCallback<GeometryChangedEvent>(_ => Refresh());
    }

    protected abstract void Draw(Painter2D p, Rect r);
    protected abstract void LayoutLabels(Rect r);

    public void Refresh()
    {
        var r = contentRect;
        if (r.width < 4f || r.height < 4f) return;
        _labelsUsed = 0;
        LayoutLabels(r);
        for (int i = _labelsUsed; i < _labels.Count; i++) _labels[i].style.display = DisplayStyle.None;
        MarkDirtyRepaint();
    }

    /// <summary>Places a text label. (pivotX, pivotY) pick which point of the label sits at (x, y): 0 = left/top, 0.5 = centre, 1 = right/bottom.</summary>
    protected Label Text(string text, float x, float y, float pivotX, float pivotY, int size = 12, Color? color = null, bool bold = false)
    {
        Label l;
        if (_labelsUsed < _labels.Count) l = _labels[_labelsUsed];
        else
        {
            l = new Label();
            l.pickingMode = PickingMode.Ignore;
            l.style.position = Position.Absolute;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            Add(l);
            _labels.Add(l);
        }
        _labelsUsed++;
        l.text = text;
        l.style.display = DisplayStyle.Flex;
        l.style.left = x;
        l.style.top = y;
        l.style.fontSize = size;
        l.style.color = color ?? LabelCol;
        l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        l.style.translate = new Translate(Length.Percent(-pivotX * 100f), Length.Percent(-pivotY * 100f));
        return l;
    }

    protected static float NiceMax(float v)
    {
        if (v <= 1f) return 1f;
        float mag = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(v)));
        float n = v / mag;
        // Steps chosen so max/4 stays a tidy number (e.g. 48 -> 12 per tick), not 12.5.
        float[] steps = { 1f, 1.2f, 1.6f, 2f, 2.4f, 3.2f, 4f, 4.8f, 6f, 8f, 10f };
        foreach (var st in steps) if (n <= st + 0.0001f) return st * mag;
        return 10f * mag;
    }

    protected static string Short(float v)
    {
        if (v >= 1000000f) return (v / 1000000f).ToString("0.#") + "M";
        if (v >= 1000f) return (v / 1000f).ToString("0.#") + "k";
        return Mathf.RoundToInt(v).ToString();
    }
}

/// <summary>A line over game time that scrolls left as the clock advances (the right edge is always "now").</summary>
public class LineChart : ReportChartBase
{
    private const float PadL = 56f, PadR = 16f, PadT = 14f, PadB = 30f;

    /// <summary>x = absolute game hours, y = value. Must be ordered by x.</summary>
    public List<Vector2> Points = new();
    public float NowHours;
    public float WindowHours = 96f;
    /// <summary>Earliest hour that may appear on the axis (start of the first recorded day): the chart fills from the left
    /// and only starts scrolling once the window is full, instead of showing days that never happened.</summary>
    public float MinHours;
    private float StartHours => Mathf.Max(NowHours - WindowHours, MinHours);
    public Color LineColor = new Color(0.42f, 0.78f, 1f, 1f);
    public Func<float, string> YFormat = v => v >= 1000f ? Short(v) : (Mathf.Abs(v - Mathf.Round(v)) < 0.05f ? Mathf.RoundToInt(v).ToString() : v.ToString("0.#"));
    private float _yMax = 1f;

    private static Rect PlotOf(Rect r) => new Rect(r.x + PadL, r.y + PadT, r.width - PadL - PadR, r.height - PadT - PadB);

    protected override void LayoutLabels(Rect r)
    {
        var plot = PlotOf(r);
        float start = StartHours;
        float max = 0f;
        foreach (var p in Points) if (p.x >= start - 1f) max = Mathf.Max(max, p.y);
        _yMax = NiceMax(Mathf.Max(max * 1.15f, 4f));

        for (int i = 0; i <= 4; i++)
        {
            float f = i / 4f;
            Text(YFormat(_yMax * f), plot.x - 6f, plot.yMax - f * plot.height, 1f, 0.5f, 12);
        }
        // One label per day, centred on the visible part of that day.
        int firstDay = Mathf.FloorToInt(start / 24f), lastDay = Mathf.FloorToInt(NowHours / 24f);
        for (int d = firstDay; d <= lastDay; d++)
        {
            float a = Mathf.Max(d * 24f, start), b = Mathf.Min((d + 1) * 24f, NowHours);
            if (b - a < 3f) continue;   // too little of the day showing for a label
            float cx = plot.x + ((a + b) * 0.5f - start) / WindowHours * plot.width;
            Text("Day " + d, cx, plot.yMax + 6f, 0.5f, 0f, 12);
        }
    }

    protected override void Draw(Painter2D p, Rect r)
    {
        var plot = PlotOf(r);
        p.fillColor = PlotBgCol;
        p.BeginPath(); p.MoveTo(new Vector2(plot.x, plot.y)); p.LineTo(new Vector2(plot.xMax, plot.y));
        p.LineTo(new Vector2(plot.xMax, plot.yMax)); p.LineTo(new Vector2(plot.x, plot.yMax)); p.ClosePath(); p.Fill();

        p.lineWidth = 1f; p.strokeColor = GridCol;
        for (int i = 1; i <= 4; i++)
        {
            float y = plot.yMax - i / 4f * plot.height;
            p.BeginPath(); p.MoveTo(new Vector2(plot.x, y)); p.LineTo(new Vector2(plot.xMax, y)); p.Stroke();
        }
        float start = StartHours;
        for (int d = Mathf.CeilToInt(start / 24f); d <= Mathf.FloorToInt(start + WindowHours) / 24; d++)
        {
            float x = plot.x + (d * 24f - start) / WindowHours * plot.width;
            p.BeginPath(); p.MoveTo(new Vector2(x, plot.y)); p.LineTo(new Vector2(x, plot.yMax)); p.Stroke();
        }
        p.lineWidth = 2f; p.strokeColor = AxisCol;
        p.BeginPath(); p.MoveTo(new Vector2(plot.x, plot.y)); p.LineTo(new Vector2(plot.x, plot.yMax)); p.LineTo(new Vector2(plot.xMax, plot.yMax)); p.Stroke();

        if (Points.Count == 0) return;
        Vector2 Map(float h, float v) => new Vector2(
            plot.x + Mathf.Clamp01((h - start) / WindowHours) * plot.width,
            plot.yMax - Mathf.Clamp01(v / _yMax) * plot.height);

        p.lineWidth = 3f; p.strokeColor = LineColor; p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
        p.BeginPath();
        bool started = false;
        for (int i = 0; i < Points.Count; i++)
        {
            var pt = Points[i];
            if (pt.x < start)
            {
                // The segment that crosses the left edge: start the line exactly on it.
                if (i + 1 < Points.Count && Points[i + 1].x >= start)
                {
                    var nx = Points[i + 1];
                    float t = (start - pt.x) / Mathf.Max(0.0001f, nx.x - pt.x);
                    p.MoveTo(Map(start, Mathf.Lerp(pt.y, nx.y, t)));
                    started = true;
                }
                continue;
            }
            if (!started) { p.MoveTo(Map(pt.x, pt.y)); started = true; }
            else p.LineTo(Map(pt.x, pt.y));
        }
        p.Stroke();

        // Marker on the newest value.
        var last = Points[Points.Count - 1];
        var m = Map(last.x, last.y);
        p.fillColor = LineColor;
        p.BeginPath(); p.Arc(m, 4.5f, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree)); p.Fill();
    }
}

/// <summary>Vertical bars with a value above each and a caption below.</summary>
public class BarChart : ReportChartBase
{
    private const float PadL = 56f, PadR = 16f, PadT = 22f, PadB = 30f;
    public List<(string label, float value)> Bars = new();
    public Color BarColor = new Color(0.30f, 0.72f, 0.45f, 1f);
    public Func<float, string> Format = v => Short(v);
    private float _yMax = 1f;

    private static Rect PlotOf(Rect r) => new Rect(r.x + PadL, r.y + PadT, r.width - PadL - PadR, r.height - PadT - PadB);

    protected override void LayoutLabels(Rect r)
    {
        var plot = PlotOf(r);
        float max = 0f;
        foreach (var b in Bars) max = Mathf.Max(max, b.value);
        _yMax = NiceMax(Mathf.Max(max * 1.15f, 4f));
        for (int i = 0; i <= 4; i++)
        {
            float f = i / 4f;
            Text(Format(_yMax * f), plot.x - 6f, plot.yMax - f * plot.height, 1f, 0.5f, 12);
        }
        int n = Mathf.Max(1, Bars.Count);
        float slot = plot.width / n;
        for (int i = 0; i < Bars.Count; i++)
        {
            float cx = plot.x + (i + 0.5f) * slot;
            float h = Mathf.Clamp01(Bars[i].value / _yMax) * plot.height;
            Text(Bars[i].label, cx, plot.yMax + 6f, 0.5f, 0f, 12);
            Text(Format(Bars[i].value), cx, plot.yMax - h - 3f, 0.5f, 1f, 12, Color.white, true);
        }
    }

    protected override void Draw(Painter2D p, Rect r)
    {
        var plot = PlotOf(r);
        p.fillColor = PlotBgCol;
        p.BeginPath(); p.MoveTo(new Vector2(plot.x, plot.y)); p.LineTo(new Vector2(plot.xMax, plot.y));
        p.LineTo(new Vector2(plot.xMax, plot.yMax)); p.LineTo(new Vector2(plot.x, plot.yMax)); p.ClosePath(); p.Fill();
        p.lineWidth = 1f; p.strokeColor = GridCol;
        for (int i = 1; i <= 4; i++)
        {
            float y = plot.yMax - i / 4f * plot.height;
            p.BeginPath(); p.MoveTo(new Vector2(plot.x, y)); p.LineTo(new Vector2(plot.xMax, y)); p.Stroke();
        }
        p.lineWidth = 2f; p.strokeColor = AxisCol;
        p.BeginPath(); p.MoveTo(new Vector2(plot.x, plot.y)); p.LineTo(new Vector2(plot.x, plot.yMax)); p.LineTo(new Vector2(plot.xMax, plot.yMax)); p.Stroke();

        int n = Mathf.Max(1, Bars.Count);
        float slot = plot.width / n;
        float barW = Mathf.Min(slot * 0.62f, 70f);
        p.fillColor = BarColor;
        for (int i = 0; i < Bars.Count; i++)
        {
            float cx = plot.x + (i + 0.5f) * slot;
            float h = Mathf.Clamp01(Bars[i].value / _yMax) * plot.height;
            if (h < 1f) continue;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - barW * 0.5f, plot.yMax)); p.LineTo(new Vector2(cx - barW * 0.5f, plot.yMax - h));
            p.LineTo(new Vector2(cx + barW * 0.5f, plot.yMax - h)); p.LineTo(new Vector2(cx + barW * 0.5f, plot.yMax));
            p.ClosePath(); p.Fill();
        }
    }
}

/// <summary>A donut showing how full something is. Pulses red like a heartbeat from <see cref="HotThreshold"/> up.</summary>
public class PieChart : ReportChartBase
{
    public const float HotThreshold = 0.95f;
    public float Fraction;
    public Color FillColor = new Color(0.30f, 0.72f, 0.45f, 1f);
    /// <summary>False for donuts where a high value is GOOD (fill rate): they never turn red or pulse.</summary>
    public bool PulseWhenHot = true;
    /// <summary>Replaces the default "NN%" in the middle; CenterSub is a smaller second line under it.</summary>
    public string CenterText, CenterSub;
    public bool Hot => PulseWhenHot && Fraction >= HotThreshold;
    private static readonly Color HotDark = new Color(0.62f, 0.07f, 0.07f, 1f);
    private static readonly Color HotBright = new Color(1f, 0.20f, 0.18f, 1f);
    private static readonly Color BackCol = new Color(0.12f, 0.17f, 0.22f, 1f);
    private float _pulse;

    /// <summary>Lub-dub: two quick pumps then a rest, once per 1.1 s.</summary>
    public static float Heartbeat(float timeSeconds)
    {
        float t = Mathf.Repeat(timeSeconds, 1.1f);
        float lub = Mathf.Exp(-Mathf.Pow((t - 0.06f) / 0.06f, 2f));
        float dub = 0.7f * Mathf.Exp(-Mathf.Pow((t - 0.30f) / 0.06f, 2f));
        return Mathf.Clamp01(lub + dub);
    }

    /// <summary>Call every frame-ish while Hot: grows/shrinks the whole chart and flashes the colour. Resets when no longer hot.</summary>
    public void Pulse(float timeSeconds)
    {
        float k = Hot ? Heartbeat(timeSeconds) : 0f;
        if (Mathf.Abs(k - _pulse) < 0.002f) return;
        _pulse = k;
        float s = 1f + 0.10f * k;
        style.scale = new Scale(new Vector3(s, s, 1f));
        MarkDirtyRepaint();
    }

    protected override void LayoutLabels(Rect r)
    {
        int big = Mathf.RoundToInt(Mathf.Min(r.width, r.height) * 0.17f);
        string main = CenterText ?? (Mathf.RoundToInt(Fraction * 100f) + "%");
        var col = Hot ? new Color(1f, 0.55f, 0.52f) : Color.white;
        if (string.IsNullOrEmpty(CenterSub))
            Text(main, r.center.x, r.center.y, 0.5f, 0.5f, big, col, true);
        else
        {
            Text(main, r.center.x, r.center.y - 2f, 0.5f, 1f, big, col, true);
            Text(CenterSub, r.center.x, r.center.y + 2f, 0.5f, 0f, Mathf.Max(11, big / 2 - 2), LabelCol);
        }
    }

    protected override void Draw(Painter2D p, Rect r)
    {
        float radius = Mathf.Min(r.width, r.height) * 0.5f - 8f;
        if (radius < 6f) return;
        float ring = radius * 0.34f;
        float mid = radius - ring * 0.5f;
        var c = r.center;

        p.lineWidth = ring; p.lineCap = LineCap.Butt;
        p.strokeColor = BackCol;
        p.BeginPath(); p.Arc(c, mid, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree)); p.Stroke();

        float f = Mathf.Clamp01(Fraction);
        if (f > 0.001f)
        {
            p.strokeColor = Hot ? Color.Lerp(HotDark, HotBright, _pulse) : FillColor;
            float a0 = -90f, a1 = a0 + 360f * Mathf.Min(f, 0.9995f);
            p.BeginPath(); p.Arc(c, mid, new Angle(a0, AngleUnit.Degree), new Angle(a1, AngleUnit.Degree), ArcDirection.Clockwise); p.Stroke();
        }
    }
}
