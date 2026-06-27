using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Staffing breakdown panel showing total headcount by shift and role.
/// Triggered by the "Headcount" label in TopBarUI.
/// </summary>
public class StaffingPanel : ITopBarPanel
{
    private readonly VisualElement _panel;
    private bool _visible;
    private Font _lilita;
    private static readonly Color HeaderBg = new Color(0.16f, 0.21f, 0.31f, 1f);
    private static readonly Color RowABg = new Color(0.12f, 0.15f, 0.22f, 1f);
    private static readonly Color RowBBg = new Color(0.14f, 0.18f, 0.26f, 1f);
    private static readonly Color TextColor = new Color(0.85f, 0.90f, 0.98f, 1f);
    private static readonly Color ShiftHeaderColor = new Color(0.45f, 0.90f, 0.50f, 1f);
    private static readonly Color BorderColor = new Color(0.40f, 0.52f, 0.72f, 1f);

    public StaffingPanel(VisualElement root)
    {
        LoadLilitaFont();
        _panel = Build();
        _panel.style.display = DisplayStyle.None;
        root.Add(_panel);
    }

    private void LoadLilitaFont()
    {
        _lilita = Resources.Load<Font>("Fonts/Lilita One");
        if (_lilita == null)
        {
            Debug.LogWarning("[StaffingPanel] Lilita One font not found in Resources");
        }
    }

    public bool IsVisible => _visible;
    public VisualElement Root => _panel;
    public void Toggle() { if (_visible) Hide(); else Show(); }

    public void Show()
    {
        _visible = true;
        _panel.style.display = DisplayStyle.Flex;
        Refresh();
    }

    public void Hide()
    {
        _visible = false;
        _panel.style.display = DisplayStyle.None;
    }

    public void Dispose()
    {
        if (_panel.parent != null) _panel.RemoveFromHierarchy();
    }

    private VisualElement Build()
    {
        var panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.top = 90;  // Moved down 50px to clear TopBar
        panel.style.left = 0;
        panel.style.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 0.95f);
        panel.style.borderTopLeftRadius = 4;
        panel.style.borderTopRightRadius = 4;
        panel.style.borderBottomLeftRadius = 4;
        panel.style.borderBottomRightRadius = 4;
        panel.style.borderTopColor = BorderColor;
        panel.style.borderBottomColor = BorderColor;
        panel.style.borderLeftColor = BorderColor;
        panel.style.borderRightColor = BorderColor;
        panel.style.borderTopWidth = 1;
        panel.style.borderBottomWidth = 1;
        panel.style.borderLeftWidth = 1;
        panel.style.borderRightWidth = 1;
        panel.style.minWidth = 320;
        panel.style.paddingTop = 0;
        panel.style.paddingBottom = 0;
        panel.style.paddingLeft = 0;
        panel.style.paddingRight = 0;
        panel.pickingMode = PickingMode.Position;
        panel.RegisterCallback<ClickEvent>((evt) => evt.StopPropagation());

        return panel;
    }

    private void Refresh()
    {
        _panel.Clear();

        var registry = EmployeeRegistry.Instance;
        if (registry == null)
        {
            AddErrorRow("Employee Registry not found");
            return;
        }

        // Get all active employees
        var activeEmployees = registry.All
            .Where(e => e?.Record?.status == EmploymentStatus.Active)
            .ToList();

        if (activeEmployees.Count == 0)
        {
            AddRow("No employees hired", TextColor, 18f, RowABg);
            return;
        }

        // Group by shift
        var byShift = new Dictionary<WorkShift, List<EmployeeRecord>>();
        foreach (var shift in System.Enum.GetValues(typeof(WorkShift)).Cast<WorkShift>())
        {
            byShift[shift] = new List<EmployeeRecord>();
        }

        foreach (var emp in activeEmployees)
        {
            if (emp.Record != null && byShift.ContainsKey(emp.Record.shift))
            {
                byShift[emp.Record.shift].Add(emp.Record);
            }
        }

        // Display by shift
        int rowIndex = 0;
        foreach (var shift in new[] { WorkShift.Day, WorkShift.Evening, WorkShift.Night, WorkShift.Flexible })
        {
            if (!byShift.ContainsKey(shift) || byShift[shift].Count == 0) continue;

            var employees = byShift[shift];
            var shiftName = ShiftName(shift);
            var bgColor = rowIndex++ % 2 == 0 ? RowABg : RowBBg;

            // Shift header (28pt Lilita)
            AddRow(shiftName, ShiftHeaderColor, 28f, bgColor, bold: true);

            // Group by role within shift
            var byRole = employees
                .GroupBy(e => e.role)
                .OrderBy(g => g.Key.ToString())
                .ToList();

            foreach (var roleGroup in byRole)
            {
                var roleText = $"  {RoleName(roleGroup.Key)}: {roleGroup.Count()}";
                bgColor = rowIndex++ % 2 == 0 ? RowABg : RowBBg;
                AddRow(roleText, TextColor, 18f, bgColor);
            }
        }

        // Total count (36pt Lilita)
        var totalText = $"Total: {activeEmployees.Count}";
        AddRow(totalText, ShiftHeaderColor, 36f, HeaderBg, bold: true);
    }

    private void AddRow(string text, Color textColor, float fontSize, Color bgColor, bool bold = false)
    {
        var row = new VisualElement();
        row.style.paddingLeft = 12;
        row.style.paddingRight = 12;
        row.style.paddingTop = 8;
        row.style.paddingBottom = 8;
        row.style.backgroundColor = bgColor;
        row.style.borderBottomWidth = 1;
        row.style.borderBottomColor = new Color(0.20f, 0.25f, 0.35f, 1f);

        var label = new Label(text);
        if (_lilita != null) label.style.unityFont = _lilita;
        label.style.fontSize = fontSize;
        label.style.color = textColor;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;

        row.Add(label);
        _panel.Add(row);
    }

    private void AddErrorRow(string text)
    {
        AddRow(text, new Color(0.95f, 0.40f, 0.40f, 1f), 10f, RowABg);
    }

    private static string ShiftName(WorkShift shift) => shift switch
    {
        WorkShift.Day => "Day Shift (08:00 – 16:00)",
        WorkShift.Evening => "Evening Shift (16:00 – 00:00)",
        WorkShift.Night => "Night Shift (00:00 – 08:00)",
        WorkShift.Flexible => "Flexible",
        _ => "Unknown"
    };

    private static string RoleName(EmployeeRole role) => role.DisplayName();
}
