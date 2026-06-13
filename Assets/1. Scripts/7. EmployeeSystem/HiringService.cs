// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/HiringService.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the pool of applicants on the Hiring Board and the hire / counter-offer
/// logic. UI (HiringBoardUI) reads <see cref="Roster"/> and listens to
/// <see cref="OnRosterChanged"/>; it never mutates candidates directly.
///
/// Attach to a persistent manager GameObject (alongside EmployeeRegistry /
/// EmployeeLifecycleService).
/// </summary>
public class HiringService : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static HiringService Instance { get; private set; }

    [Tooltip("How many applicants to keep on the board.")]
    [SerializeField] private int _rosterSize = 8;

    private readonly List<HiringCandidate> _roster = new List<HiringCandidate>();

    /// <summary>Current applicants on the board (read-only).</summary>
    public IReadOnlyList<HiringCandidate> Roster => _roster;

    /// <summary>Fired whenever the roster changes (refresh, hire, counter result).</summary>
    public event Action OnRosterChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (_roster.Count == 0)
            RefreshRoster();
    }

    // ─── Roster ────────────────────────────────────────────────────────────────
    /// <summary>Discard the current applicants and generate a fresh batch.</summary>
    public void RefreshRoster()
    {
        _roster.Clear();
        for (int i = 0; i < _rosterSize; i++)
            _roster.Add(HiringCandidateGenerator.Generate());
        OnRosterChanged?.Invoke();
    }

    /// <summary>Top the board back up to roster size without replacing existing applicants.</summary>
    public void TopUpRoster()
    {
        while (_roster.Count < _rosterSize)
            _roster.Add(HiringCandidateGenerator.Generate());
        OnRosterChanged?.Invoke();
    }

    // ─── Hire ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Hire the applicant at their effective salary. Commits the wrapped record
    /// to the lifecycle service (which fires OnHired → EmployeeSpawner spawns it),
    /// then removes them from the board.
    /// </summary>
    public EmployeeRecord Hire(HiringCandidate candidate)
    {
        if (candidate == null || !_roster.Contains(candidate)) return null;

        candidate.record.hourlyWage = candidate.EffectiveSalary;

        EmployeeRecord hired = EmployeeLifecycleService.Instance != null
            ? EmployeeLifecycleService.Instance.HireRecord(candidate.record)
            : candidate.record;

        _roster.Remove(candidate);
        OnRosterChanged?.Invoke();
        return hired;
    }

    // ─── Counter-offer ─────────────────────────────────────────────────────────
    /// <summary>
    /// Make a one-time counter-offer at the applicant's experience-fair wage.
    /// Acceptance chance scales with how generous the counter is versus their ask.
    /// On accept: salary drops to the counter and they stay (hireable at the lower
    /// rate). On reject: they walk and are removed from the board.
    /// Returns true if the counter was accepted.
    /// </summary>
    public bool CounterOffer(HiringCandidate candidate)
    {
        if (candidate == null || !_roster.Contains(candidate)) return false;
        if (candidate.counterUsed) return candidate.counterAccepted;

        candidate.counterUsed = true;

        float fair = Mathf.Lerp(candidate.minWage, candidate.maxWage,
                                candidate.yearsExperience / 10f);
        float counter = Mathf.Round(fair * 100f) / 100f;

        // Counter at/above their ask → always accept. Counter far below → reject.
        // Linear ramp: >=95% of ask ~certain, <=75% ~impossible.
        float ratio = counter / Mathf.Max(0.01f, candidate.askingSalary);
        float acceptChance = Mathf.Clamp01((ratio - 0.75f) / 0.20f);
        bool accepted = UnityEngine.Random.value <= acceptChance;

        if (accepted)
        {
            candidate.counterAccepted = true;
            candidate.agreedSalary = counter;
        }
        else
        {
            _roster.Remove(candidate);
        }

        OnRosterChanged?.Invoke();
        return accepted;
    }
}
