// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/HiringCandidate.cs
using System;

/// <summary>
/// A job applicant shown on the Hiring Board. Wraps a fully-generated
/// <see cref="EmployeeRecord"/> (name, gender, avatar, role, skill) plus the
/// applicant-only data used during negotiation. When hired, the wrapped record
/// is committed via <see cref="EmployeeLifecycleService.HireRecord"/> with the
/// agreed wage written onto it.
///
/// Strengths/weaknesses are flavor for now — randomly assigned, no gameplay
/// effect yet. They become meaningful once the core loop and traits land.
/// </summary>
[Serializable]
public class HiringCandidate
{
    /// <summary>The underlying employee record (built at generation time).</summary>
    public EmployeeRecord record;

    /// <summary>Position the applicant is applying for.</summary>
    public EmployeeRole role;

    /// <summary>Years of experience in the applied-for role (1–10). Drives starting skill + salary.</summary>
    public int yearsExperience;

    /// <summary>Single-word strength (flavor only for now).</summary>
    public string strength;

    /// <summary>Single-word weakness (flavor only for now).</summary>
    public string weakness;

    /// <summary>Role wage floor (entry-level, 0 years).</summary>
    public float minWage;

    /// <summary>Role wage ceiling (veteran, 10 years).</summary>
    public float maxWage;

    /// <summary>What the applicant is asking per hour.</summary>
    public float askingSalary;

    /// <summary>Wage agreed after a successful counter-offer (only valid when <see cref="counterAccepted"/>).</summary>
    public float agreedSalary;

    /// <summary>True once we've made a counter-offer (one attempt allowed).</summary>
    public bool counterUsed;

    /// <summary>True if the applicant accepted our counter-offer.</summary>
    public bool counterAccepted;

    /// <summary>The wage the applicant would be hired at right now.</summary>
    public float EffectiveSalary => counterAccepted ? agreedSalary : askingSalary;

    /// <summary>First name (everything before the first space).</summary>
    public string FirstName
    {
        get
        {
            if (string.IsNullOrEmpty(record?.employeeName)) return "—";
            int i = record.employeeName.IndexOf(' ');
            return i < 0 ? record.employeeName : record.employeeName.Substring(0, i);
        }
    }

    /// <summary>Last name (everything after the first space).</summary>
    public string LastName
    {
        get
        {
            if (string.IsNullOrEmpty(record?.employeeName)) return "";
            int i = record.employeeName.IndexOf(' ');
            return i < 0 ? "" : record.employeeName.Substring(i + 1);
        }
    }
}
