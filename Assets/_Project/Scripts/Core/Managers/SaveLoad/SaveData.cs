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

    // Per-lane operational config (max stack, usage, FIFO/LIFO). Empty in older saves.
    public List<LaneConfigEntry> laneConfigs = new();

    // Former employees (terminated / resigned) — kept on file for rehire, union reinstatement,
    // and HR history. Populated from FormerEmployeeArchive. Absent in older saves (empty list).
    public List<EmployeeRecord> formerEmployees = new();

    // ── ECONOMY PERSISTENCE ─────────────────────────────────────────────────
    // Hourly cost tracking by GL_Line + fractional accumulator. Absent in older saves.
    public EconomySnapshot economy = new();

    // ── TIME PERSISTENCE ────────────────────────────────────────────────────
    // Current hour/minute/day + fractional time accumulator. Absent in older saves.
    public TimeSnapshot time = new();

    // ── WORK QUEUE PERSISTENCE ──────────────────────────────────────────────
    // Pending work tasks (Putaway, Replenish, OrderSelect, Load). Absent in older saves.
    public List<WorkTaskSnapshot> workQueue = new();

    // ── PALLET PERSISTENCE ──────────────────────────────────────────────────
    // All pallets with their locations, SKUs, quantities, and expiration. Absent in older saves.
    public List<PalletSnapshot> pallets = new();

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

[System.Serializable]
public class EconomySnapshot
{
    public List<EconomyGLLineEntry> hourlyByGLLine = new();     // Hourly costs per GL_Line
    public List<EconomyFractionalEntry> fractionalByGLLine = new(); // Fractional accumulators
}

[System.Serializable]
public class EconomyGLLineEntry
{
    public string glLine;
    public int hourlyAmount;
}

[System.Serializable]
public class EconomyFractionalEntry
{
    public string glLine;
    public float fractionalRemainder;
}

[System.Serializable]
public class TimeSnapshot
{
    public int hour = 8;           // 0-23 (default to morning)
    public int minute = 0;         // 0-59
    public int day = 1;            // 1+ (default to day 1)
    public float fractionalMinutes = 0f; // Sub-minute accumulator for precision
}

[System.Serializable]
public class WorkTaskSnapshot
{
    public string taskId;                    // GUID to preserve task identity
    public int type;                         // WorkTaskType as int enum
    public int requiredRole;                 // EmployeeRole as int enum
    public string palletId;
    public string description;
    public int status;                       // WorkTaskStatus as int enum
    public int assignedToEmployeeGuid;       // (nullable → -1 if null)
    public string fromLocation;
    public string toLocation;
    public int area;                         // PalletData.AreaCategory as int enum
}

[System.Serializable]
public class PalletSnapshot
{
    // Identity & Tracking
    public string palletId;                  // GUID (unique per pallet instance)
    public string loadId;                    // 10-digit "license plate" (master key)

    // Inventory Data
    public string skuId;                     // Product SKU
    public int quantity;                     // Case count
    public int receivedDayNumber;            // In-game day received
    public int expirationDayNumber;          // -1 if non-perishable
    public bool isContaminated;

    // **CRITICAL FOR XYZ**: Location coordinates
    public int locationX;                    // Grid cell X
    public int locationY;                    // Grid cell Y
    public float worldHeightY;               // World Y elevation (for stacking)
}
