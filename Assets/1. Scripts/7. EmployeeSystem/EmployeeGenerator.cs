// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/EmployeeGenerator.cs
using System;
using UnityEngine;

public static class EmployeeGenerator
{
    // Wage range per prefix — extend this table as more roles are added.
    // Values are hourly dollars.
    private static readonly System.Collections.Generic.Dictionary<string, (float min, float max)> WageTable
        = new System.Collections.Generic.Dictionary<string, (float, float)>(StringComparer.OrdinalIgnoreCase)
    {
        { "WHSE",  (14f, 22f) },   // General warehouse worker
        { "BOSS",  (30f, 50f) },   // Manager / boss
        { "SEC",   (16f, 24f) },   // Security guard
        { "CLERK", (13f, 18f) },   // IC Clerk
        { "EXT",   (18f, 28f) },   // Exterminator
        { "TRKD",  (20f, 30f) },   // Truck driver
        {"HR",    (18f, 35f) },   // HR staff
        {"ADMIN", (20f, 40f) },   // Admin staff
        {"SAN",   (13f, 20f) }    // Sanitation worker
    };

    // Mon–Fri mask  = bits 1+2+3+4+5 = 0b0111110 = 62
    private const int WeekdayMask = 0b0111110;
    // Mon–Sat mask  = bits 1–6 = 0b1111110 = 126
    private const int SixDayMask  = 0b1111110;

    // ─── Public API ───────────────────────────────────────────────────────────

    public static EmployeeRecord Generate(EmployeeGender gender = EmployeeGender.Random,
                                          string idPrefix = "WHSE",
                                          EmployeeRole role = EmployeeRole.OrderSelector)
    {
        if (gender == EmployeeGender.Random)
            gender = RandomGender();

        EmployeeNameListJson names = EmployeeNameListLoader.Load();

        string firstName = PickFirstName(names, gender);
        string lastName  = PickRandom(names.lastNames, "Barnes");
        int    idNum     = UnityEngine.Random.Range(1, 1000);
        string guid      = Guid.NewGuid().ToString();

        EmployeeRecord record = new EmployeeRecord
        {
            employeeGuid = guid,
            employeeName = $"{firstName} {lastName}",
            employeeIdPrefix = idPrefix,
            employeeId = $"{idPrefix} {idNum:D3}",
            gender = gender,

            // Stats
            fatigue = UnityEngine.Random.Range(10f, 95f),
            safety = UnityEngine.Random.Range(20f, 95f),
            morale = UnityEngine.Random.Range(30f, 100f),
            skill = UnityEngine.Random.Range(15f, 90f),
            skillLevel = UnityEngine.Random.Range(1, 6),

            // Avatar — assign from gender-specific pool
            avatarResourceKey = EmployeeRegistry.Instance?.AvatarRegistry.AssignAvatar(gender, guid)
                                ?? (gender == EmployeeGender.Female ? "Female/avatar_01" : "Male/avatar_01"),

            // Role — uses parameter, defaults to OrderSelector
            role = role,
            status = EmploymentStatus.Active,
            hourlyWage = RandomWage(idPrefix),
            totalWagesPaid = 0f,
            hireDateIso = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            separationDateIso = string.Empty,

            // Work Schedule — default Mon–Fri, random Day/Evening shift
            // (will be populated via SetDefaultSchedule below)

            // Injury — start healthy
            isInjured = false,
            injuryDescription = string.Empty,
            injuryRecoveryDaysLeft = 0
        };

        // Set up a default Mon–Fri schedule
        SetDefaultSchedule(record, RandomShift());

        return record;
    }

    public static EmployeeRecord GenerateFromTemplate(EmployeeData template)
    {
        if (template == null)
            return Generate();

        EmployeeGender gender = EmployeeGender.Random;
        if (template.employeeName != null)
        {
            string lower = template.employeeName.ToLower();
            if (lower.StartsWith("mr. ") || lower.EndsWith(" m"))
                gender = EmployeeGender.Male;
            else if (lower.StartsWith("ms. ") || lower.EndsWith(" f"))
                gender = EmployeeGender.Female;
        }

        return Generate(gender, template.employeeIdPrefix);
    }

    // ─── Role ↔ wage helpers (public — used by the hiring board) ───────────────

    /// <summary>
    /// Maps an EmployeeRole to its WageTable prefix so role-based callers
    /// (e.g. the hiring board) share the same single wage source of truth.
    /// </summary>
    public static string PrefixForRole(EmployeeRole role) => role switch
    {
        EmployeeRole.Boss             => "BOSS",
        EmployeeRole.Security         => "SEC",
        EmployeeRole.InventoryControl => "CLERK",
        EmployeeRole.Exterminator     => "EXT",
        EmployeeRole.TruckDriver      => "TRKD",
        EmployeeRole.HR               => "HR",
        EmployeeRole.Admin            => "ADMIN",
        EmployeeRole.Sanitation       => "SAN",
        // OrderSelector, ReachTruckOperator, Loader, Receiver, Supervisor
        _                             => "WHSE"
    };

    /// <summary>Min/max hourly wage range for a role (from the shared WageTable).</summary>
    public static (float min, float max) GetWageRange(EmployeeRole role)
    {
        string prefix = PrefixForRole(role);
        if (WageTable.TryGetValue(prefix, out var range))
            return (range.min, range.max);
        return (13f, 20f);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static float RandomWage(string prefix)
    {
        if (WageTable.TryGetValue(prefix, out var range))
            return Mathf.Round(UnityEngine.Random.Range(range.min, range.max) * 100f) / 100f;

        // Unknown prefix — fall back to a generic entry-level wage
        return Mathf.Round(UnityEngine.Random.Range(13f, 20f) * 100f) / 100f;
    }

    private static WorkShift RandomShift()
    {
        float roll = UnityEngine.Random.value;
        return roll < 0.6f ? WorkShift.Day : WorkShift.Evening;
    }

    private static EmployeeGender RandomGender()
    {
        float roll = UnityEngine.Random.value;
        return roll < 0.45f ? EmployeeGender.Male
             : roll < 0.90f ? EmployeeGender.Female
             : EmployeeGender.Neutral;
    }

    private static string PickFirstName(EmployeeNameListJson names, EmployeeGender gender)
    {
        if (gender == EmployeeGender.Male)   return PickRandom(names.maleFirstNames,    "James");
        if (gender == EmployeeGender.Female) return PickRandom(names.femaleFirstNames,  "Jennifer");
        return PickRandom(names.neutralFirstNames, "Alex");
    }

    private static string PickRandom(System.Collections.Generic.List<string> list, string fallback)
    {
        if (list == null || list.Count == 0) return fallback;
        return list[UnityEngine.Random.Range(0, list.Count)];
    }

    /// <summary>Set up a default Mon–Fri schedule (5 days, no overtime).</summary>
    private static void SetDefaultSchedule(EmployeeRecord record, WorkShift shift)
    {
        if (record.workSchedule == null)
            record.workSchedule = new EmployeeWorkSchedule();

        // Days 0–4 = Mon–Fri
        for (int i = 0; i < 5; i++)
            record.workSchedule.SetShift(i, shift);

        // Days 5–6 = Sat–Sun (unscheduled)
        record.workSchedule.SetShift(5, WorkShift.Flexible);
        record.workSchedule.SetShift(6, WorkShift.Flexible);

        record.workSchedule.overtimeDaysMask = 0; // No overtime by default
    }
}
