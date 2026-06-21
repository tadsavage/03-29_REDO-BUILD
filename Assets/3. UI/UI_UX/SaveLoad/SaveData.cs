using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    public string saveName;
    public int money;
    public int spentToday;
    public CameraSaveData cameraData;
    public List<SavedObject> placedObjects = new();
    public List<DevSettingEntry> devSettings = new();
    public float toolsWindowX = 12f;   // panel-space position of the Tools Window
    public float toolsWindowY = 50f;
    public bool  guidanceLinesVisible = true;
    public bool  waypointsVisible     = true;
    public bool  hoverPopupEnabled    = true;

    public List<EmployeeRecord> employeeRecords = new();

    // Former employees (terminated / resigned) — kept on file for rehire, union reinstatement,
    // and HR history. Populated from FormerEmployeeArchive. Absent in older saves (empty list).
    public List<EmployeeRecord> formerEmployees = new();

    // ── Game settings captured per-save ──────────────────────────────────────
    // Sentinel defaults (-1 / empty) mean "not stored in this file" so that
    // loading an OLD save does not overwrite the player's current settings.
    public float  gameVolume      = -1f;   // SFX/game volume   (PlayerPrefs "GameVolume")
    public float  musicVolume     = -1f;   // music volume      (PlayerPrefs "MusicVolume")
    public string graphicsPreset  = "";    // "Ultra"/"Good"/"Toaster"
    public int    difficulty      = -1;    // 0 Clerk, 1 Supervisor, 2 Manager
    public int    resolutionIndex = -1;    // dropdown index (PlayerPrefs "ResolutionIndex")
    public int    screenWidth     = 0;     // actual width  applied via Screen.SetResolution
    public int    screenHeight    = 0;     // actual height applied via Screen.SetResolution
}

/// <summary>Key = "TypeName.fieldName", Val = serialized value string.</summary>
[System.Serializable]
public class DevSettingEntry
{
    public string key;
    public string val;
}

[System.Serializable]
public class CameraSaveData
{
    public Vector3 focusPoint;
    public float distance;
    public float pitch;
    public float yaw;
}

[System.Serializable]
public class SavedObject
{
    public int id;   // ObjDataSO ID
    public int x;    // Grid X
    public int y;    // Grid Y
    public int rot;  // Rotation index (0–3)
    public string customData;
}
