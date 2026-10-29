using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Economy;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// A deliberately tiny, read-only picture of the dock schedule, docked into the bottom bar on every tab
/// (BuildMenuUI.DockMiniTimeline moves it into whichever bar is showing): a window of VisibleHours (half behind the yellow line, half ahead) that scrolls
/// right-to-left as the clock runs, with door numbers down the left and one coloured square per trailer
/// (recurring / bulk / inbound). The yellow "now" line stays fixed in the middle.
///
/// Only a couple of door rows fit at once. Drag the background left/right to look further ahead (or
/// back) and up/down to reach the other doors. The moment you let go it glides back to the current time
/// and the top row, on both axes.
///
/// A trailer whose block has passed without its freight going out (or coming in) turns red.
/// "Complete" and "missed" come from the SAME source the full Scheduler uses
/// (DockScheduleService.IsComplete and DockAppointment.ClosedOut), so the two can never disagree.
///
/// The structure and look are declared in BuildMenu.uxml and MiniDockTimeline.uss; this class fills in
/// the per-door rows, hour labels and trailer chips, whose number and positions are data-driven. Chips
/// are rebuilt only when the appointments actually change; scrolling just moves them.
/// </summary>
public class MiniDockTimeline
{
    public const string ElementName = "MiniDockTimeline";

    /// <summary>Hours shown across the window: half behind the now line, half ahead (16 = 8 each way).</summary>
    private const float VisibleHours = 16f;
    private const float MaxScrubForward = 24f;
    private const float MaxScrubBack = 6f;
    /// <summary>Height of one door row in px. Must match .mini-timeline-door / -chip in the USS.</summary>
    private const float RowHeight = 20f;
    private const int HourLabelPool = 11;   // VisibleHours / BlockHours + a couple spare

    // Glide back to "now" / the top row after letting go: exponential ease plus a floor so it always
    // finishes. Hours per second / pixels per second.
    private const float ReturnRate = 1.1f;
    private const float MinReturnHoursPerSec = 0.6f;
    private const float MinReturnPixelsPerSec = 40f;
    /// <summary>Pause after letting go before the glide starts, so the player has time to move the
    /// mouse onto whatever they were scrolling to look at (and read its tooltip).</summary>
    private const float ReturnDelaySeconds = 3f;

    private readonly VisualElement _root;
    private readonly VisualElement _hoursStrip;
    private readonly VisualElement _doorColumn;
    private readonly VisualElement _grid;
    private readonly VisualElement _nowLine;
    private readonly Label _emptyLabel;

    private readonly List<Label> _hourLabels = new();
    private readonly List<VisualElement> _vLines = new();
    private readonly HashSet<VisualElement> _keep = new();
    private readonly List<(VisualElement chip, float startAbs, int row)> _chips = new();
    private readonly List<(VisualElement label, int row)> _doorLabels = new();
    private readonly List<(VisualElement line, int row)> _rowLines = new();

    private string _signature;
    private int _rowCount;
    private float _scrubHours;          // how far the view is offset from "now"
    private float _scrollY;             // px the rows are scrolled up from the top
    private bool _dragging;
    private float _lastNowHours;
    private float _lastAnimTime;
    private float _releaseTime;

    public VisualElement Root => _root;

    public MiniDockTimeline(VisualElement root)
    {
        _root = root;
        _hoursStrip = root.Q<VisualElement>("MiniTimelineHours");
        _doorColumn = root.Q<VisualElement>("MiniTimelineDoors");
        _grid = root.Q<VisualElement>("MiniTimelineGrid");
        _nowLine = root.Q<VisualElement>("MiniTimelineNow");
        _emptyLabel = root.Q<Label>("MiniTimelineEmpty");

        _keep.Add(_emptyLabel);
        _keep.Add(_nowLine);

        // Fixed pools, repositioned as the view moves, so scrolling never allocates.
        int vlineCount = Mathf.CeilToInt(VisibleHours / DockScheduleService.BlockHours) + 2;
        for (int i = 0; i < vlineCount; i++)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.AddToClassList("mini-timeline-vline");
            _grid.Insert(0, v);
            _vLines.Add(v);
            _keep.Add(v);
        }
        for (int i = 0; i < HourLabelPool; i++)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("mini-timeline-hour");
            _hoursStrip.Add(l);
            _hourLabels.Add(l);
        }

        _grid.RegisterCallback<PointerDownEvent>(OnPointerDown);
        _grid.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _grid.RegisterCallback<PointerUpEvent>(OnPointerUp);
        _grid.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);

        _lastAnimTime = Time.unscaledTime;
        _root.schedule.Execute(Tick).Every(200);       // data + clock
        _root.schedule.Execute(Animate).Every(16);     // the glide back, ~60 fps
        Tick();
    }

    // ── Dragging ─────────────────────────────────────────────────────────────

    private void OnPointerDown(PointerDownEvent e)
    {
        if (e.button != 0) return;
        // Double-click opens the real Scheduler - the same path the bottom-bar button and the 0 key take,
        // so it also closes whatever panel is open. (The view glides back to now on its own after a drag.)
        if (e.clickCount == 2)
        {
            _dragging = false;
            _scrubHours = 0f; _scrollY = 0f; Layout();
            UIKeyBindingManager.Instance?.ToggleUI(0);
            e.StopPropagation();
            return;
        }
        _dragging = true;
        _grid.CapturePointer(e.pointerId);
        e.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent e)
    {
        if (!_dragging) return;
        float width = _grid.resolvedStyle.width;
        if (width < 1f) return;

        // Dragging left slides the schedule left, i.e. looks further AHEAD in time.
        _scrubHours = Mathf.Clamp(_scrubHours - e.deltaPosition.x * (VisibleHours / width), -MaxScrubBack, MaxScrubForward);
        // Dragging up slides the rows up, revealing the doors below.
        _scrollY = Mathf.Clamp(_scrollY - e.deltaPosition.y, 0f, MaxScrollY());
        Layout();
        e.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent e)
    {
        if (!_dragging) return;
        _dragging = false;
        _releaseTime = Time.unscaledTime;
        if (_grid.HasPointerCapture(e.pointerId)) _grid.ReleasePointer(e.pointerId);
        e.StopPropagation();
    }

    private float MaxScrollY()
    {
        float visible = _grid.resolvedStyle.height;
        return Mathf.Max(0f, _rowCount * RowHeight - (visible > 1f ? visible : RowHeight * 2f));
    }

    // ── Glide back ───────────────────────────────────────────────────────────

    /// <summary>Eases the view back to the current time and the top row once the player lets go.</summary>
    private void Animate()
    {
        float now = Time.unscaledTime;
        float dt = Mathf.Min(0.1f, now - _lastAnimTime);
        _lastAnimTime = now;
        if (_dragging || dt <= 0f) return;
        if (now - _releaseTime < ReturnDelaySeconds) return;
        if (Mathf.Approximately(_scrubHours, 0f) && Mathf.Approximately(_scrollY, 0f)) return;

        _scrubHours = EaseToZero(_scrubHours, dt, ReturnRate, MinReturnHoursPerSec);
        _scrollY = EaseToZero(_scrollY, dt, ReturnRate, MinReturnPixelsPerSec);
        Layout();
    }

    private static float EaseToZero(float value, float dt, float rate, float minSpeed)
    {
        if (value == 0f) return 0f;
        float step = Mathf.Max(Mathf.Abs(value) * (1f - Mathf.Exp(-rate * dt)), minSpeed * dt);
        return Mathf.Abs(value) <= step ? 0f : value - Mathf.Sign(value) * step;
    }

    // ── Tick: data + clock ───────────────────────────────────────────────────

    private void Tick()
    {
        if (_root == null || _root.panel == null || _grid == null) return;
        if (_root.resolvedStyle.display == DisplayStyle.None) return;

        ServiceLocator.TryGet<SimulationTimeService>(out var time);
        ServiceLocator.TryGet<DockScheduleService>(out var schedule);
        int today = time != null ? time.Day : 0;
        _lastNowHours = time != null ? today * 24f + time.Hour + time.Minute / 60f : 0f;

        // Today plus the neighbouring days, so scrolling across midnight still shows trailers.
        var visibleAppts = schedule == null
            ? new List<DockAppointment>()
            : schedule.Appointments
                .Where(a => a != null && !a.Parked && a.DoorNumber > 0 && a.Day >= today - 1 && a.Day <= today + 1)
                .ToList();

        var doors = new SortedSet<int>();
        if (schedule != null) foreach (int d in schedule.OutboundDoors()) doors.Add(d);
        foreach (var a in visibleAppts) doors.Add(a.DoorNumber);
        var doorList = doors.ToList();

        string sig = string.Join(",", doorList) + "|" + string.Join(";",
            visibleAppts.Select(a => $"{a.Id}:{a.Day}:{a.DoorNumber}:{a.BlockIndex}:{(int)a.Kind}:{a.ClosedOut}:{schedule.IsComplete(a)}"));
        if (sig != _signature)
        {
            _signature = sig;
            Rebuild(doorList, visibleAppts, schedule);
        }

        Layout();
    }

    /// <summary>Positions everything that moves: hour labels, block lines, door rows and trailer chips.</summary>
    private void Layout()
    {
        float viewStart = _lastNowHours + _scrubHours - VisibleHours / 2f;

        // Block boundaries (every BlockHours) inside the window.
        int step = DockScheduleService.BlockHours;
        int firstBoundary = Mathf.CeilToInt(viewStart / step) * step;
        for (int i = 0; i < _vLines.Count; i++)
        {
            float t = firstBoundary + i * step;
            bool visible = t <= viewStart + VisibleHours;
            _vLines[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) _vLines[i].style.left = Length.Percent((t - viewStart) / VisibleHours * 100f);
        }
        for (int i = 0; i < _hourLabels.Count; i++)
        {
            float t = firstBoundary + i * step;
            bool visible = t <= viewStart + VisibleHours;
            _hourLabels[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) continue;
            int hour = ((Mathf.RoundToInt(t) % 24) + 24) % 24;
            _hourLabels[i].text = $"{hour:00}:00";
            _hourLabels[i].style.left = Length.Percent((t - viewStart) / VisibleHours * 100f);
        }

        foreach (var (chip, startAbs, row) in _chips)
        {
            chip.style.left = Length.Percent((startAbs - viewStart) / VisibleHours * 100f);
            chip.style.top = row * RowHeight - _scrollY;
        }
        foreach (var (label, row) in _doorLabels) label.style.top = row * RowHeight - _scrollY;
        foreach (var (line, row) in _rowLines) line.style.top = row * RowHeight - _scrollY;
    }

    private void Rebuild(List<int> doors, List<DockAppointment> appts, DockScheduleService schedule)
    {
        _doorColumn.Clear();
        _chips.Clear();
        _doorLabels.Clear();
        _rowLines.Clear();
        // Everything dynamic in the grid goes; the pooled lines, empty label and now line stay.
        for (int i = _grid.childCount - 1; i >= 0; i--)
        {
            var child = _grid[i];
            if (!_keep.Contains(child)) _grid.RemoveAt(i);
        }

        _rowCount = doors.Count;
        _scrollY = Mathf.Clamp(_scrollY, 0f, MaxScrollY());

        bool none = doors.Count == 0;
        _emptyLabel.EnableInClassList("mini-timeline-empty-visible", none);
        if (none) return;

        float widthPct = DockScheduleService.BlockHours / VisibleHours * 100f;

        for (int r = 0; r < doors.Count; r++)
        {
            var label = new Label(doors[r].ToString());
            label.AddToClassList("mini-timeline-door");
            label.pickingMode = PickingMode.Ignore;
            _doorColumn.Add(label);
            _doorLabels.Add((label, r));

            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("mini-timeline-hline");
            _grid.Add(line);
            _rowLines.Add((line, r));
        }

        foreach (var appt in appts)
        {
            int row = doors.IndexOf(appt.DoorNumber);
            if (row < 0) continue;

            bool complete = schedule != null && schedule.IsComplete(appt);
            bool missed = !complete && appt.ClosedOut;

            var chip = new VisualElement();
            chip.AddToClassList("mini-timeline-chip");
            chip.style.width = Length.Percent(widthPct * appt.SpanBlocks);

            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("mini-timeline-face");
            face.AddToClassList(missed ? "mini-timeline-face-missed"
                : TruckOrderColors.IsSameDay(appt) ? "mini-timeline-face-sameday"
                : FaceClass(appt.Kind));
            if (complete) face.AddToClassList("mini-timeline-face-done");   // finished trailers fade back
            chip.Add(face);

            string state = missed ? "MISSED" : complete ? "done" : "scheduled";
            RuntimeTooltip.Attach(chip,
                $"{(string.IsNullOrEmpty(appt.CustomerName) ? "Trailer" : appt.CustomerName)} - {KindName(appt.Kind)}, " +
                $"Door {appt.DoorNumber}, {appt.StartHour:00}:00-{appt.EndHour:00}:00 ({state})");
            _grid.Add(chip);
            _chips.Add((chip, appt.Day * 24f + appt.StartHour, row));
        }

        _nowLine.BringToFront();   // the yellow line sits above every chip
    }

    private static string FaceClass(AppointmentKind kind) => kind switch
    {
        AppointmentKind.Inbound => "mini-timeline-face-inbound",
        AppointmentKind.Bulk or AppointmentKind.Wholesale => "mini-timeline-face-bulk",
        _ => "mini-timeline-face-recurring"
    };

    private static string KindName(AppointmentKind kind) => kind switch
    {
        AppointmentKind.Inbound => "inbound PO",
        AppointmentKind.Bulk or AppointmentKind.Wholesale => "bulk order",
        _ => "recurring order"
    };
}
