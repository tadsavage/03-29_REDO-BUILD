using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
//___
public class BuildPhaseChecklistWindow : EditorWindow
{
    private ChecklistRoot _root;
    private ScrollView _scrollView;

    private enum Priority
    {
        Low,
        Medium,
        High
    }
    //
    [MenuItem("Window/Build Phase Checklist")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<BuildPhaseChecklistWindow>();
        wnd.titleContent = new GUIContent("Build Phase Checklist");
        wnd.minSize = new Vector2(700, 400);
    }

    private void OnEnable()
    {
        _root = ChecklistTaskManager.Load();
        CreateUI();
    }

    private void CreateUI()
    {
        rootVisualElement.Clear();

        var toolbar = new Toolbar();
        toolbar.Add(new Button(AddSection) { text = "Add Section" });
        toolbar.Add(new Button(Save) { text = "Save" });
        toolbar.Add(new Button(Reload) { text = "Reload" });
        toolbar.Add(new Button(Celebrate) { text = "Celebrate Wins 🎉" });
        rootVisualElement.Add(toolbar);

        _scrollView = new ScrollView(ScrollViewMode.Vertical);
        _scrollView.style.flexGrow = 1f;
        rootVisualElement.Add(_scrollView);

        RebuildSectionsUI();
    }
    
    private void RebuildSectionsUI()
    {
        _scrollView.Clear();

        for (int i = 0; i < _root.sections.Count; i++)
        {
            int sectionIndex = i;
            var section = _root.sections[i];

            var sectionContainer = new VisualElement();
            sectionContainer.style.marginBottom = 6;
            sectionContainer.style.borderBottomWidth = 1;
            sectionContainer.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            sectionContainer.style.paddingBottom = 4;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            var dragLabel = new Label("☰");
            dragLabel.style.width = 20;
            dragLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            var foldout = new Foldout { text = section.title, value = true };
            foldout.style.flexGrow = 1;

            var milestoneToggle = new Toggle("Milestone");
            milestoneToggle.SetValueWithoutNotify(section.isMilestone);
            milestoneToggle.style.marginLeft = 4;

            var deleteButton = new Button(() =>
            {
                _root.sections.RemoveAt(sectionIndex);
                RebuildSectionsUI();
            })
            { text = "X" };
            deleteButton.style.width = 24;
            deleteButton.style.marginLeft = 4;

            headerRow.Add(dragLabel);
            headerRow.Add(foldout);
            headerRow.Add(milestoneToggle);
            headerRow.Add(deleteButton);

            sectionContainer.Add(headerRow);

            milestoneToggle.RegisterValueChangedCallback(evt =>
            {
                section.isMilestone = evt.newValue;
            });

            var sectionContent = new VisualElement();
            sectionContent.style.marginLeft = 20;
            foldout.Add(sectionContent);

            var tasksList = new ListView
            {
                itemsSource = section.tasks,
                selectionType = SelectionType.None,
                reorderable = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                makeItem = MakeTaskItem,
                bindItem = (ve, idx) => BindTaskItem(ve, sectionIndex, idx),
                fixedItemHeight = 140
            };

            tasksList.style.marginTop = 4;
            tasksList.style.marginBottom = 4;
            sectionContent.Add(tasksList);

            var addTaskButton = new Button(() =>
            {
                var task = new TaskData
                {
                    id = Guid.NewGuid().ToString(),
                    title = "New Task",
                    priority = Priority.Medium.ToString(),
                    isCompleted = false,
                    projectedDate = "",
                    completedDate = "",
                    isMilestone = false,
                    subtasks = new List<SubtaskData>()
                };
                section.tasks.Add(task);
                tasksList.Rebuild();
            })
            { text = "Add Task" };

            sectionContent.Add(addTaskButton);

            _scrollView.Add(sectionContainer);
        }
    }

    private VisualElement MakeTaskItem()
    {
        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Column;
        container.style.paddingLeft = 4;
        container.style.paddingRight = 4;
        container.style.paddingTop = 4;
        container.style.paddingBottom = 4;
        container.style.borderBottomWidth = 1;
        container.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);

        var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

        var dragHandle = new Label("☰");
        dragHandle.style.width = 20;
        dragHandle.style.unityTextAlign = TextAnchor.MiddleCenter;

        var completedToggle = new Toggle();
        completedToggle.name = "CompletedToggle";
        completedToggle.style.marginRight = 4;

        var titleField = new TextField { name = "TitleField" };
        titleField.style.flexGrow = 1;

        var milestoneToggle = new Toggle("⭐");
        milestoneToggle.name = "MilestoneToggle";
        milestoneToggle.style.marginLeft = 4;

        var deleteButton = new Button { text = "X" };
        deleteButton.name = "DeleteTaskButton";
        deleteButton.style.marginLeft = 4;
        deleteButton.style.width = 24;

        headerRow.Add(dragHandle);
        headerRow.Add(completedToggle);
        headerRow.Add(titleField);
        headerRow.Add(milestoneToggle);
        headerRow.Add(deleteButton);

        var secondRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2, alignItems = Align.Center } };

        var priorityField = new EnumField(Priority.Medium);
        priorityField.name = "PriorityField";
        priorityField.style.width = 100;

        var projectedLabel = new Label("Projected");
        projectedLabel.style.marginLeft = 4;

        var projectedField = new TextField();
        projectedField.name = "ProjectedField";
        projectedField.style.width = 120;
        projectedField.style.marginLeft = 2;

        var completedLabel = new Label("Completed");
        completedLabel.style.marginLeft = 4;

        var completedDateField = new TextField();
        completedDateField.name = "CompletedDateField";
        completedDateField.style.width = 120;
        completedDateField.style.marginLeft = 2;

        secondRow.Add(priorityField);
        secondRow.Add(projectedLabel);
        secondRow.Add(projectedField);
        secondRow.Add(completedLabel);
        secondRow.Add(completedDateField);

        var subtasksFoldout = new Foldout { text = "Subtasks (0)", value = false };
        subtasksFoldout.name = "SubtasksFoldout";
        subtasksFoldout.style.marginTop = 2;

        var subtasksContainer = new VisualElement();
        subtasksContainer.name = "SubtasksContainer";
        subtasksContainer.style.marginLeft = 16;
        subtasksFoldout.Add(subtasksContainer);

        var addSubtaskButton = new Button { text = "Add Subtask" };
        addSubtaskButton.name = "AddSubtaskButton";
        addSubtaskButton.style.marginTop = 2;

        container.Add(headerRow);
        container.Add(secondRow);
        container.Add(subtasksFoldout);
        container.Add(addSubtaskButton);

        return container;
    }

    private void BindTaskItem(VisualElement element, int sectionIndex, int taskIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= _root.sections.Count) return;
        var section = _root.sections[sectionIndex];
        if (taskIndex < 0 || taskIndex >= section.tasks.Count) return;
        var task = section.tasks[taskIndex];

        var completedToggle = element.Q<Toggle>("CompletedToggle");
        var titleField = element.Q<TextField>("TitleField");
        var milestoneToggle = element.Q<Toggle>("MilestoneToggle");
        var priorityField = element.Q<EnumField>("PriorityField");
        var projectedField = element.Q<TextField>("ProjectedField");
        var completedDateField = element.Q<TextField>("CompletedDateField");
        var subtasksFoldout = element.Q<Foldout>("SubtasksFoldout");
        var subtasksContainer = element.Q<VisualElement>("SubtasksContainer");
        var addSubtaskButton = element.Q<Button>("AddSubtaskButton");
        var deleteTaskButton = element.Q<Button>("DeleteTaskButton");

        completedToggle.SetValueWithoutNotify(task.isCompleted);
        titleField.SetValueWithoutNotify(task.title);
        milestoneToggle.SetValueWithoutNotify(task.isMilestone);
        projectedField.SetValueWithoutNotify(task.projectedDate ?? string.Empty);
        completedDateField.SetValueWithoutNotify(task.completedDate ?? string.Empty);

        Priority parsedPriority = Priority.Medium;
        Enum.TryParse(task.priority, out parsedPriority);
        priorityField.Init(parsedPriority);
        priorityField.SetValueWithoutNotify(parsedPriority);

        completedToggle.RegisterValueChangedCallback(evt =>
        {
            task.isCompleted = evt.newValue;
            if (task.isCompleted && string.IsNullOrEmpty(task.completedDate))
            {
                task.completedDate = DateTime.Now.ToString("yyyy-MM-dd");
                completedDateField.SetValueWithoutNotify(task.completedDate);
            }
            RefreshTaskBackground(element, task);
        });

        titleField.RegisterValueChangedCallback(evt => task.title = evt.newValue);
        milestoneToggle.RegisterValueChangedCallback(evt => task.isMilestone = evt.newValue);
        projectedField.RegisterValueChangedCallback(evt => task.projectedDate = evt.newValue);
        completedDateField.RegisterValueChangedCallback(evt => task.completedDate = evt.newValue);

        priorityField.RegisterValueChangedCallback(evt =>
        {
            var p = (Priority)evt.newValue;
            task.priority = p.ToString();
            RefreshTaskBackground(element, task);
        });

        deleteTaskButton.clicked += () =>
        {
            section.tasks.RemoveAt(taskIndex);
            RebuildSectionsUI();
        };

        RefreshTaskBackground(element, task);

        // SUBTASKS
        subtasksContainer.Clear();

        for (int i = 0; i < task.subtasks.Count; i++)
        {
            int subIndex = i;
            var sub = task.subtasks[i];

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;

            var subToggle = new Toggle();
            subToggle.style.marginRight = 4;
            subToggle.SetValueWithoutNotify(sub.isCompleted);

            var subTitle = new TextField();
            subTitle.style.flexGrow = 1;
            subTitle.SetValueWithoutNotify(sub.title);
            var subDelete = new Button { text = "X" };
            subDelete.style.width = 24;
            subDelete.style.marginLeft = 4;

            subToggle.RegisterValueChangedCallback(evt =>
            {
                sub.isCompleted = evt.newValue;
            });

            subTitle.RegisterValueChangedCallback(evt =>
            {
                sub.title = evt.newValue;
            });

            subDelete.clicked += () =>
            {
                task.subtasks.RemoveAt(subIndex);
                BindTaskItem(element, sectionIndex, taskIndex);
            };

            row.Add(subToggle);
            row.Add(subTitle);
            row.Add(subDelete);

            subtasksContainer.Add(row);
        }

        subtasksFoldout.text = $"Subtasks ({task.subtasks.Count})";

        // Foldout: rebuild + force height recalculation when opened
        subtasksFoldout.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue)
            {
                BindTaskItem(element, sectionIndex, taskIndex);

                element.schedule.Execute(() =>
                {
                    element.style.height = StyleKeyword.Auto;
                    element.style.minHeight = StyleKeyword.Auto;
                    element.style.marginBottom = 0;
                    element.MarkDirtyRepaint();
                    element.parent?.MarkDirtyRepaint();
                });
            }
        });

        // Also force height after initial bind (handles already-open foldouts)
        element.schedule.Execute(() =>
        {
            element.style.height = StyleKeyword.Auto;
            element.style.minHeight = StyleKeyword.Auto;
            element.style.marginBottom = 0;
            element.MarkDirtyRepaint();
            element.parent?.MarkDirtyRepaint();
        });

        addSubtaskButton.clicked += () =>
        {
            var sub = new SubtaskData
            {
                id = Guid.NewGuid().ToString(),
                title = "New Subtask",
                isCompleted = false
            };
            task.subtasks.Add(sub);
            BindTaskItem(element, sectionIndex, taskIndex);
        };

        // DATE PICKER POPUPS (mini calendar anchored under field)
        projectedField.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            ShowMiniCalendarForField(projectedField, date =>
            {
                task.projectedDate = date.ToString("yyyy-MM-dd");
                projectedField.SetValueWithoutNotify(task.projectedDate);
            });
        });

        completedDateField.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            ShowMiniCalendarForField(completedDateField, date =>
            {
                task.completedDate = date.ToString("yyyy-MM-dd");
                completedDateField.SetValueWithoutNotify(task.completedDate);
            });
        });
    }

    private void RefreshTaskBackground(VisualElement element, TaskData task)
    {
        Color bg = new Color(0.18f, 0.18f, 0.18f);

        switch (task.priority)
        {
            case "High": bg = new Color(0.82f, 0.82f, 0.82f, 1f); break;
            case "Medium": bg = new Color(0.80f, 0.86f, 0.94f, 1f); break;
            case "Low": bg = new Color(0.22f, 0.22f, 0.22f, 1f); break;
        }

        if (task.isCompleted)
            bg = new Color(0.15f, 0.15f, 0.15f, 0.5f);

        element.style.backgroundColor = bg;
    }

    private void ShowMiniCalendarForField(VisualElement field, Action<DateTime> onPicked)
    {
        var world = field.worldBound;
        var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(world.xMin, world.yMax));
        var rect = new Rect(screenPos.x, screenPos.y, 180, 180);
        MiniCalendarPopup.Show(rect, onPicked);
    }

    private void AddSection()
    {
        var section = new SectionData
        {
            id = Guid.NewGuid().ToString(),
            title = "New Section",
            isMilestone = false,
            tasks = new List<TaskData>()
        };
        _root.sections.Add(section);
        RebuildSectionsUI();
    }

    private void Save()
    {
        ChecklistTaskManager.Save(_root);
    }

    private void Reload()
    {
        _root = ChecklistTaskManager.Load();
        RebuildSectionsUI();
    }

    private void Celebrate()
    {
        var completedSectionMilestones = _root.sections
            .Where(s => s.isMilestone && s.tasks.All(t => t.isCompleted))
            .ToList();

        if (completedSectionMilestones.Count == 0)
        {
            EditorUtility.DisplayDialog("Celebrate Wins", "No completed section milestones yet. Keep going!", "OK");
            return;
        }

        var msg = "Completed section milestones:\n\n" +
                  string.Join("\n", completedSectionMilestones.Select(s => "- " + s.title));
        EditorUtility.DisplayDialog("🎉 Milestones Reached!", msg, "Nice!");
    }
}

public class MiniCalendarPopup : EditorWindow
{
    private DateTime _currentMonth;
    private Action<DateTime> _onPicked;

    public static void Show(Rect anchorRect, Action<DateTime> onPicked)
    {
        var window = CreateInstance<MiniCalendarPopup>();
        window._currentMonth = DateTime.Today;
        window._onPicked = onPicked;
        window.ShowAsDropDown(anchorRect, new Vector2(180, 180));
    }

    private void OnGUI()
    {
        var today = DateTime.Today;
        var firstOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
        int startDayOfWeek = (int)firstOfMonth.DayOfWeek;

        GUILayout.BeginVertical();

        GUILayout.Label(_currentMonth.ToString("MMMM yyyy"), EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        string[] days = { "S", "M", "T", "W", "T", "F", "S" };
        foreach (var d in days)
            GUILayout.Label(d, GUILayout.Width(20));
        GUILayout.EndHorizontal();

        int day = 1;
        int cell = 0;

        while (day <= daysInMonth)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 7; i++)
            {
                if (cell < startDayOfWeek || day > daysInMonth)
                {
                    GUILayout.Label("", GUILayout.Width(20));
                }
                else
                {
                    var date = new DateTime(_currentMonth.Year, _currentMonth.Month, day);
                    GUIStyle style = new GUIStyle(EditorStyles.miniButton);
                    if (date.Date == today)
                        style.fontStyle = FontStyle.Bold;

                    if (GUILayout.Button(day.ToString(), style, GUILayout.Width(20)))
                    {
                        _onPicked?.Invoke(date);
                        Close();
                    }

                    day++;
                }
                cell++;
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }
}
