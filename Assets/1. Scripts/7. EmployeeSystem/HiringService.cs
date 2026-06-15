// METADATA file_path: Assets/1. Scripts/7. EmployeeSystem/HiringService.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the pool of applicants on the Hiring Board and the hire / counter-offer
/// logic. UI (HiringBoardUI) reads <see cref="Roster"/> and listens to
/// <see cref="OnRosterChanged"/>; it never mutates candidates directly.
///
/// Scarcity model:
///  - The board starts with a difficulty-based number of candidates (Manager 10,
///    Supervisor 15, Clerk 24), ~70% Order Selectors with a skilled/special mix.
///  - That starting count is also the CAP — the board never holds more.
///  - Candidates only refill over in-game time. Hiring (or losing someone to a
///    rejected counter) frees a slot that slowly fills back up. Be too cheap and
///    you run dry.
///
/// Replenishment is split into two tiers, each on its own timer (in-game minutes,
/// driven by SimulationTimeService.OnMinuteChanged):
///  - Tier 1 "Floor Associates": Sanitation, Order Selector, Reach Truck, Loader.
///  - Tier 2 "Skilled": Inventory Control, Receiver, Admin, Security, Supervisor.
/// Base intervals are tuned at Manager difficulty; easier difficulties divide the
/// interval by a speed multiplier (Supervisor 2x, Clerk 4x faster).
///
/// Attach to a persistent manager GameObject (alongside EmployeeRegistry /
/// EmployeeLifecycleService).
/// </summary>
public class HiringService : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────────────────────
    public static HiringService Instance { get; private set; }

    // ─── Tunables (serialized, private) ─────────────────────────────────────────
    [Header("Initial roster size / cap — by difficulty")]
    [Tooltip("Clerk (easy, difficulty 0).")]      [SerializeField] private int _initialClerk = 24;
    [Tooltip("Supervisor (normal, difficulty 1).")][SerializeField] private int _initialSupervisor = 15;
    [Tooltip("Manager (hard, difficulty 2).")]    [SerializeField] private int _initialManager = 10;

    [Header("Initial composition")]
    [Tooltip("Fraction of the starting roster that are Order Selectors. The rest is a skilled/special mix.")]
    [Range(0f, 1f)] [SerializeField] private float _orderSelectorShare = 0.70f;

    [Header("Replenishment — base interval in IN-GAME MINUTES (at Manager difficulty)")]
    [Tooltip("Floor associates: Sanitation, Order Selector, Reach Truck, Loader. Manager = 1 per 30 min.")]
    [SerializeField] private float _tier1BaseMinutes = 30f;
    [Tooltip("Skilled: Inventory Control, Receiver, Admin, Security, Supervisor. Manager = 1 per 2 hours.")]
    [SerializeField] private float _tier2BaseMinutes = 120f;

    [Header("Difficulty speed multipliers (interval is divided by this)")]
    [SerializeField] private float _speedManager = 1f;
    [SerializeField] private float _speedSupervisor = 2f;
    [SerializeField] private float _speedClerk = 4f;

    [Header("Counter-offer")]
    [Tooltip("Placeholder multiplier on counter-offer rejection chance. " +
             "Will be driven by a global Difficulty class later (1 = neutral).")]
    [SerializeField] private float _difficultyRejectMultiplier = 1f;

    // ─── Role tiers ─────────────────────────────────────────────────────────────
    private static readonly EmployeeRole[] Tier1Roles =
    {
        EmployeeRole.Sanitation, EmployeeRole.OrderSelector,
        EmployeeRole.ReachTruckOperator, EmployeeRole.Loader,
    };
    private static readonly EmployeeRole[] Tier2Roles =
    {
        EmployeeRole.InventoryControl, EmployeeRole.Receiver,
        EmployeeRole.Admin, EmployeeRole.Security, EmployeeRole.Supervisor,
    };
    // Skilled/special roles used to fill the non-OrderSelector slice of the start roster.
    private static readonly EmployeeRole[] StartingSkilledRoles =
    {
        EmployeeRole.Boss, EmployeeRole.Security, EmployeeRole.Admin,
        EmployeeRole.InventoryControl, EmployeeRole.Receiver, EmployeeRole.Supervisor,
    };

    // ─── Runtime state ──────────────────────────────────────────────────────────
    private readonly List<HiringCandidate> _roster = new List<HiringCandidate>();
    private int _cap;
    private float _tier1Interval, _tier2Interval;   // resolved in-game-minute intervals
    private float _tier1Accum, _tier2Accum;

    private SimulationTimeService _time;
    private bool _subscribed;

    /// <summary>Current applicants on the board (read-only).</summary>
    public IReadOnlyList<HiringCandidate> Roster => _roster;

    /// <summary>Maximum candidates the board can hold (difficulty-based).</summary>
    public int Cap => _cap;

    /// <summary>Fired whenever the roster changes (start, replenish, hire, counter result).</summary>
    public event Action OnRosterChanged;

    // ─── Unity lifecycle ────────────────────────────────────────────────────────
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
        int difficulty = PlayerPrefs.GetInt("Difficulty", 0); // 0 Clerk, 1 Supervisor, 2 Manager

        _cap = difficulty switch
        {
            2 => _initialManager,
            1 => _initialSupervisor,
            _ => _initialClerk,
        };

        float speed = difficulty switch
        {
            2 => _speedManager,
            1 => _speedSupervisor,
            _ => _speedClerk,
        };
        speed = Mathf.Max(0.0001f, speed);

        _tier1Interval = _tier1BaseMinutes / speed;
        _tier2Interval = _tier2BaseMinutes / speed;

        if (_roster.Count == 0)
            BuildInitialRoster();

        SubscribeToTime();
    }

    private void OnDestroy()
    {
        if (_subscribed && _time != null)
            _time.OnMinuteChanged -= OnGameMinute;
    }

    // ─── Time wiring ────────────────────────────────────────────────────────────
    private void SubscribeToTime()
    {
        if (_subscribed) return;
        var ctx = FindAnyObjectByType<GameContext>();
        _time = ctx != null ? ctx.TimeService : null;
        if (_time == null)
        {
            Debug.LogWarning("[HiringService] No GameContext/TimeService found — replenishment disabled.");
            return;
        }
        _time.OnMinuteChanged += OnGameMinute;
        _subscribed = true;
    }

    private void OnGameMinute()
    {
        bool changed = false;
        _tier1Accum += 1f;
        _tier2Accum += 1f;
        changed |= Replenish(ref _tier1Accum, _tier1Interval, Tier1Roles);
        changed |= Replenish(ref _tier2Accum, _tier2Interval, Tier2Roles);
        if (changed) OnRosterChanged?.Invoke();
    }

    private bool Replenish(ref float accum, float interval, EmployeeRole[] pool)
    {
        if (interval <= 0f) return false;
        bool added = false;
        while (accum >= interval && _roster.Count < _cap)
        {
            accum -= interval;
            var candidate = HiringCandidateGenerator.Generate(pool[UnityEngine.Random.Range(0, pool.Length)]);
            
            // Generate custom studio portrait for the candidate immediately
            if (EmployeePhotoBooth.Instance != null)
                EmployeePhotoBooth.Instance.GeneratePortraitForRecord(candidate.record);
                
            _roster.Add(candidate);
            added = true;
        }
        // At cap: hold a single interval's worth ready so a freed slot fills promptly,
        // but don't bank an unbounded backlog.
        if (_roster.Count >= _cap && accum > interval) accum = interval;
        return added;
    }

    // ─── Roster building ────────────────────────────────────────────────────────
    private void BuildInitialRoster()
    {
        _roster.Clear();

        int osCount = Mathf.Clamp(Mathf.RoundToInt(_cap * _orderSelectorShare), 0, _cap);
        for (int i = 0; i < osCount; i++)
        {
            var candidate = HiringCandidateGenerator.Generate(EmployeeRole.OrderSelector);
            if (EmployeePhotoBooth.Instance != null)
                EmployeePhotoBooth.Instance.GeneratePortraitForRecord(candidate.record);
            _roster.Add(candidate);
        }

        int remaining = _cap - osCount;
        for (int i = 0; i < remaining; i++)
        {
            var role = StartingSkilledRoles[UnityEngine.Random.Range(0, StartingSkilledRoles.Length)];
            var candidate = HiringCandidateGenerator.Generate(role);
            if (EmployeePhotoBooth.Instance != null)
                EmployeePhotoBooth.Instance.GeneratePortraitForRecord(candidate.record);
            _roster.Add(candidate);
        }

        OnRosterChanged?.Invoke();
    }

    /// <summary>
    /// Dev/manual reset: regenerate a fresh starting roster (to the difficulty cap).
    /// Note: this bypasses the time-based scarcity, so it's mainly for testing.
    /// </summary>
    public void RefreshRoster()
    {
        _tier1Accum = 0f;
        _tier2Accum = 0f;
        BuildInitialRoster();
    }

    // ─── Hire ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Hire the applicant at their effective salary. Commits the wrapped record
    /// to the lifecycle service (which fires OnHired → EmployeeSpawner spawns it),
    /// then removes them from the board (freeing a slot to refill over time).
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
    /// Rejection chance rises with experience. On accept: salary drops to the
    /// counter, morale takes an experience-scaled hit, and they stay (hireable at
    /// the lower rate). On reject: they walk and the slot empties.
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

        // Rejection chance rises with experience — veterans hold out for their ask.
        // Scaled by a difficulty multiplier (placeholder = 1; wired to a global
        // Difficulty class later).
        float rejectChance = Mathf.Clamp01(RejectionChance(candidate.yearsExperience)
                                           * _difficultyRejectMultiplier);
        bool accepted = UnityEngine.Random.value > rejectChance;

        if (accepted)
        {
            candidate.counterAccepted = true;
            candidate.agreedSalary = counter;

            // Settling for less than they asked stings — morale takes an
            // experience-scaled hit (veterans resent it most). Multiplicative,
            // so a 50% hit lands them at 50% morale or lower.
            float hit = MoraleHitPercent(candidate.yearsExperience) / 100f;
            candidate.record.morale =
                Mathf.Clamp(candidate.record.morale * (1f - hit), 0f, 100f);
        }
        else
        {
            _roster.Remove(candidate);
        }

        OnRosterChanged?.Invoke();
        return accepted;
    }

    /// <summary>
    /// Base chance (0–1) that a counter-offer is REJECTED, by years of experience.
    /// ~10% under 3 years, 25% at 3, 50% at 5, up to 75% at 10.
    /// TODO: fold in a global difficulty setting once that class exists.
    /// </summary>
    private static float RejectionChance(int years)
    {
        if (years < 3) return 0.10f;
        if (years <= 5) return Mathf.Lerp(0.25f, 0.50f, (years - 3) / 2f);
        return Mathf.Lerp(0.50f, 0.75f, Mathf.Clamp01((years - 5) / 5f));
    }

    /// <summary>
    /// Morale penalty (%) for accepting a counter-offer, by years of experience.
    /// Years 1–5 ramp linearly (5% → 25%); years 6–10 ramp exponentially to 50%.
    /// Applied multiplicatively, so the penalty caps starting morale accordingly.
    /// TODO: shell out further (difficulty, traits) once core mechanics land.
    /// </summary>
    private static float MoraleHitPercent(int years)
    {
        if (years <= 5) return years * 5f;                          // 5,10,15,20,25
        float t = (years - 5) / 5f;                                 // 0..1 over yrs 6–10
        return 25f + 25f * Mathf.Pow(t, 2f);                        // 25 → 50 (exponential)
    }
}
