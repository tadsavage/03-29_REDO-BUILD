// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/HiringCandidateGenerator.cs
using UnityEngine;

/// <summary>
/// Builds <see cref="HiringCandidate"/> applicants for the Hiring Board.
///
/// Each applicant gets a full EmployeeRecord (via <see cref="EmployeeGenerator"/>)
/// for the chosen role, plus applicant-only data:
///  - Years experience (1–10) drives starting skill and asking salary.
///  - Asking salary = lerp(min,max, years/10) nudged by a random "ambition"
///    factor, so some green applicants over-ask (counter-offer fodder).
///  - Starting skill scales with experience and is dampened by difficulty.
/// </summary>
public static class HiringCandidateGenerator
{
    /// <summary>Roles that can currently appear on the hiring board.</summary>
    private static readonly EmployeeRole[] HireableRoles =
    {
        EmployeeRole.OrderSelector,
        EmployeeRole.ReachTruckOperator,
        EmployeeRole.DockStockerOperator,
        EmployeeRole.Loader,
        EmployeeRole.Receiver,
        EmployeeRole.Security,
        EmployeeRole.InventoryControl,
        EmployeeRole.TruckDriver,
        EmployeeRole.HR,
    };

    public static HiringCandidate Generate(EmployeeRole? forcedRole = null)
    {
        EmployeeRole role = forcedRole
            ?? HireableRoles[Random.Range(0, HireableRoles.Length)];

        string prefix = EmployeeGenerator.PrefixForRole(role);

        // Inventory Control only has a female clerk model — only generate
        // female candidates for this role so name/gender/model stay consistent.
        EmployeeGender gender = (role == EmployeeRole.InventoryControl)
            ? EmployeeGender.Female
            : EmployeeGender.Random;

        EmployeeRecord record = EmployeeGenerator.Generate(gender, prefix, role);

        int years = Random.Range(1, 11); // 1..10 inclusive

        // Starting skill scales with experience, dampened by difficulty.
        record.skill = ComputeStartingSkill(years);
        record.skillLevel = Mathf.Clamp(Mathf.CeilToInt(years / 2f), 1, 5);

        var (min, max) = EmployeeGenerator.GetWageRange(role);

        // Fair value for their experience, then a random ambition nudge.
        float fair = Mathf.Lerp(min, max, years / 10f);
        float ambition = Random.Range(0.95f, 1.18f);
        float asking = Mathf.Clamp(fair * ambition, min, max * 1.10f);

        return new HiringCandidate
        {
            record = record,
            role = role,
            yearsExperience = years,
            strength = HiringTraitLoader.RandomStrength(),
            weakness = HiringTraitLoader.RandomWeakness(),
            minWage = min,
            maxWage = max,
            askingSalary = Round2(asking),
        };
    }

    /// <summary>
    /// Skill from experience (0–100), reduced on harder difficulties.
    /// Difficulty: 0 = Clerk (easy), 1 = Supervisor, 2 = Manager (hard).
    /// </summary>
    private static float ComputeStartingSkill(int years)
    {
        int difficulty = PlayerPrefs.GetInt("Difficulty", 1);
        float baseSkill = (years / 10f) * 90f;          // 9..90
        float skill = baseSkill - difficulty * 8f + Random.Range(-5f, 5f);
        return Mathf.Clamp(skill, 5f, 99f);
    }

    private static float Round2(float v) => Mathf.Round(v * 100f) / 100f;
}
