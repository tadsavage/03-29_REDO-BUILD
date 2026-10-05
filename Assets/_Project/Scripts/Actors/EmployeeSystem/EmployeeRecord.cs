// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeRecord.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ─── Employment status ────────────────────────────────────────────────────────
public enum EmploymentStatus
{
    Active,       // Working normally
    OnLeave,      // Scheduled time off
    Injured,      // Hurt — cannot work
    Suspended,    // Disciplinary hold
    Terminated,   // Fired
    Resigned,     // Quit
    Deceased      // Gone for good
}

// ─── Work schedule ────────────────────────────────────────────────────────────
public enum WorkShift
{
    Day,          // 08:00 – 16:00
    Evening,      // 16:00 – 00:00
    Night,        // 00:00 – 08:00
    Flexible      // No fixed shift
}

[Serializable]
public class EmployeeRecord : ISerializationCallbackReceiver
{
    // Saves written before the old "Loader" role was retired hold its integer value (3) — turn it into
    // DockStockerOperator the moment the record is read, so no code ever sees a role that no longer exists.
    public void OnBeforeSerialize() { }
    public void OnAfterDeserialize() { role = role.Normalize(); }

    // ─── Identity ─────────────────────────────────────────────────────────────
    public string employeeGuid;
    public string employeeName;
    public string employeeId;
    public string employeeIdPrefix;
    public EmployeeGender gender;

    // ─── Role ─────────────────────────────────────────────────────────────────
    public EmployeeRole role; // = EmployeeRole.OrderSelector;

    /// <summary>Current job assignment chosen via the Actions dropdown. Defaults to Patrol
    /// (value 0) for legacy records/saves predating this field.</summary>
    public EmployeeAssignment currentAssignment;

    // ─── Stats (0–100) ────────────────────────────────────────────────────────
    public float fatigue;
    public float safety;
    public float morale;
    public float skill;
    public int   skillLevel;

    // ─── Asset keys ───────────────────────────────────────────────────────────
    public string avatarResourceKey;
    public string jobIconResourceKey;

    // ─── Employment status ────────────────────────────────────────────────────
    public EmploymentStatus status;

    // ─── Salary & wages ───────────────────────────────────────────────────────
    /// <summary>Hourly wage in dollars.</summary>
    public float hourlyWage;

    /// <summary>Total wages paid out to this employee (lifetime, for accounting).</summary>
    public float totalWagesPaid;

    // ─── Dates (stored as ISO-8601 strings for JSON serialisation) ────────────
    /// <summary>Date the employee was hired. Set once at generation time.</summary>
    public string hireDateIso;

    /// <summary>Date the employee left (terminated/resigned/deceased). Empty while active.</summary>
    public string separationDateIso;

    // ─── Schedule ─────────────────────────────────────────────────────────────
    public WorkShift shift;

    /// <summary>Days the employee is scheduled to work. Bit-flags: 0=Sun,1=Mon,...,6=Sat.</summary>
    public int scheduledDaysMask;

    // ─── Injury ───────────────────────────────────────────────────────────────
    public bool  isInjured;

    /// <summary>Human-readable description of the injury (e.g. "Forklift incident").</summary>
    public string injuryDescription;

    /// <summary>Number of in-game days remaining before the employee can return to work.</summary>
    public int   injuryRecoveryDaysLeft;

    // ─── Work Schedule ────────────────────────────────────────────────────────
    /// <summary>Weekly schedule defining shifts, overtime, and availability.</summary>
    public EmployeeWorkSchedule workSchedule;

    // ─── Last-known world transform (save/restore of roaming position) ────────────
    // Captured at save time so a loaded employee resumes exactly where they were instead of
    // teleporting back to the spawn point. hasSavedPosition is false for freshly hired staff
    // and for saves made before this field existed → those spawn at the spawn point.
    public float posX, posY, posZ;
    public float rotY;
    public bool  hasSavedPosition;

    // ─── MHE boarding (save/restore of operator↔vehicle pairing) ──────────────
    // Captured at save time from EmployeeIdentity.AssignedSlot's PlacedObject grid cell — the
    // vehicle's grid cell is stable across save/load (it's restored to the exact same cell via
    // the normal placedObjects pipeline), so it doubles as a durable vehicle identifier without
    // needing a new GUID system. hasBoardedVehicle false / grid -1,-1 = was on-foot at save time.
    public bool hasBoardedVehicle;
    public int  boardedVehicleGridX = -1;
    public int  boardedVehicleGridY = -1;
    public float boardedVehicleWorldX, boardedVehicleWorldY, boardedVehicleWorldZ;

    // ─── Per-employee appearance overrides ("Pimp My Employee", 2026-09-27) ───────────
    // Forces a SPECIFIC part into one of ModularAvatarAssembler.EditableOverrideKeys' cosmetic
    // categories (hair, hardhat, headphones, facial hair) instead of that category's normal random
    // roll. Deliberately scoped to cosmetics only — clothing/skin color aren't editable yet (no
    // per-part color-swap application exists at runtime). A List, not a Dictionary, because
    // Dictionary doesn't round-trip through this project's JSON save system the way a plain
    // serializable class does. Only employees someone has actually edited carry any entries here —
    // everyone else keeps rolling their normal GUID-seeded random look untouched.
    [Serializable]
    public class AvatarOverride
    {
        public string key;        // one of ModularAvatarAssembler.EditableOverrideKeys
        public string objectName; // the part's AvatarPartLibrary.Part.objectName, or "" for explicitly none/bald/removed
    }
    public List<AvatarOverride> avatarOverrides = new();

    public string GetAvatarOverride(string key) => avatarOverrides.FirstOrDefault(o => o.key == key)?.objectName;

    public void SetAvatarOverride(string key, string objectName)
    {
        var existing = avatarOverrides.FirstOrDefault(o => o.key == key);
        if (existing != null) existing.objectName = objectName;
        else avatarOverrides.Add(new AvatarOverride { key = key, objectName = objectName });
    }

    public void ClearAvatarOverride(string key) => avatarOverrides.RemoveAll(o => o.key == key);

    /// <summary>Converts to the plain dictionary ModularAvatarAssembler.Build's override
    /// parameter expects.</summary>
    public Dictionary<string, string> AvatarOverridesDict() =>
        avatarOverrides.ToDictionary(o => o.key, o => o.objectName);

    // ─── Constructor ──────────────────────────────────────────────────────────
    public EmployeeRecord()
    {
        employeeGuid = Guid.NewGuid().ToString();
        status = EmploymentStatus.Active;
        workSchedule = new EmployeeWorkSchedule();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    public bool IsAvailableForWork =>
        status == EmploymentStatus.Active && !isInjured;

    public EmployeeRecord Clone() => (EmployeeRecord)MemberwiseClone();
}
