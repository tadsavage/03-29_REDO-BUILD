using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SubtaskData
{
    public string id;
    public string title;
    public bool isCompleted;
}// 
[Serializable]
public class TaskData
{
    public string id;
    public string title;
    public string priority; // "Low", "Medium", "High"
    public bool isCompleted;
    public string projectedDate;
    public string completedDate;
    public bool isMilestone;
    public List<SubtaskData> subtasks = new List<SubtaskData>();
}

[Serializable]
public class SectionData
{
    public string id;
    public string title;
    public bool isMilestone;
    public List<TaskData> tasks = new List<TaskData>();
}

[Serializable]
public class ChecklistRoot
{
    public List<SectionData> sections = new List<SectionData>();
}
