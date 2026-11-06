using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameCore.Services;
using GameCore.Inventory;

namespace GameCore.Economy
{
    /// <summary>One loan on the books. Plain data so it saves as-is (SaveData.loans).</summary>
    [Serializable]
    public class Loan
    {
        public string id;
        public string offerId;        // "mortgage", "starter", "equipment", "expansion", "credit"
        public string name;
        public int    amount;         // borrowed
        public float  balance;        // still owed (principal)
        public float  apr;            // 0.12 = 12% a year
        public int    termWeeks;
        public int    weeklyPayment;
        public int    startDay;
        public int    nextDueDay;
        public int    lastRefinanceDay = -999;
        public int    missedPayments;
        public int    interestPaid;

        // Fixed-rate loans (the First-Time Build mortgage): weekly interest on the balance and a flat weekly principal floor.
        // weeklyRate == 0 means a legacy APR loan, which uses apr/52 and the amortised weeklyPayment.
        public float  weeklyRate;
        public int    minPrincipal;
        // Player-set extra principal every week, applied after the minimum is paid (the "overpayment" in the Bank window).
        public int    weeklyOverpayment;

        public bool IsFixedWeekly => weeklyRate > 0f;
        public float EffectiveWeeklyRate => IsFixedWeekly ? weeklyRate : apr / 52f;

        /// <summary>Weekly minimum due right now: interest on the balance plus the principal floor (fixed-rate loans only).</summary>
        public int MinimumDue()
        {
            if (balance <= 0.5f) return 0;
            int interest = Mathf.CeilToInt(balance * EffectiveWeeklyRate);
            int principal = Mathf.Min(minPrincipal, Mathf.CeilToInt(balance));
            return interest + principal;
        }

        public int WeeksLeft(float rWeekly)
        {
            if (balance <= 0.5f) return 0;
            if (weeklyPayment <= 0) return termWeeks;
            if (rWeekly <= 0f) return Mathf.CeilToInt(balance / weeklyPayment);
            double x = 1.0 - balance * rWeekly / weeklyPayment;
            if (x <= 0) return 999;
            return Mathf.CeilToInt((float)(-Math.Log(x) / Math.Log(1.0 + rWeekly)));
        }
    }

    [Serializable]
    public class LoanSnapshot
    {
        public List<Loan> loans = new();
        public bool mortgageCreated;
    }

    /// <summary>A loan the bank will write, gated by reputation band (greyed out in the Bank window until reached).</summary>
    public class LoanOffer
    {
        public string Id, Name, Blurb;
        public int Amount, TermWeeks;
        public ReputationBand Requires;
    }

    /// <summary>
    /// THE BANK (Tad, 2026-11-05): "I don't want to start the game with negative cash flow anymore ... the warehouse is now a
    /// loan debt ... pay it once a week ... interest based off reputation ... like SimCity / Cities: Skylines".
    ///
    /// - New game: the starting capital is the MORTGAGE on the warehouse (net worth starts at $0). Repaid weekly over
    ///   <see cref="MortgageWeeks"/> game weeks.
    /// - Every loan is amortised: a fixed weekly payment, part interest (booked as an expense, Lease / Mortgage > Loan Interest)
    ///   and part principal (reduces the debt; not an expense).
    /// - APR comes from the reputation band when the loan is taken or refinanced.
    /// - Can't make a payment: it is missed - a 5% fee goes on the balance and reputation drops 10.
    /// - Pay Extra / Pay Off at any time; Refinance at Respected or better, once every 4 game weeks.
    /// Money in and principal out move the balance WITHOUT touching income/expense ledgers (a loan is not revenue).
    /// </summary>
    public class LoanService : IService
    {
        public const int MortgageWeeks = 104;
        public const float MissedPaymentFeePercent = 0.05f;
        public const int MissedPaymentReputation = 10;
        public const int RefinanceCooldownDays = 28;

        // First-Time Build mortgage (easy mode, Tad 2026-11-05): 1.5% interest per week on the balance, plus a minimum
        // principal of 1% of the original loan per week. $500k => $7,500 interest + $5,000 principal = $12,500 minimum.
        public const float FirstTimeBuildWeeklyRate = 0.015f;
        public const float FirstTimeBuildPrincipalPercent = 0.01f;
        public const ReputationBand RefinanceBand = ReputationBand.Respected;

        public static readonly LoanOffer[] Offers =
        {
            new LoanOffer { Id = "starter",   Name = "Starter Loan",    Amount = 25000,  TermWeeks = 12, Requires = ReputationBand.Unknown,   Blurb = "A small cushion to get the doors open." },
            new LoanOffer { Id = "equipment", Name = "Equipment Loan",  Amount = 50000,  TermWeeks = 26, Requires = ReputationBand.Known,     Blurb = "Trucks, jacks and racking." },
            new LoanOffer { Id = "expansion", Name = "Expansion Loan",  Amount = 100000, TermWeeks = 52, Requires = ReputationBand.Respected, Blurb = "More doors, more lanes, more building." },
            new LoanOffer { Id = "credit",    Name = "Line of Credit",  Amount = 150000, TermWeeks = 52, Requires = ReputationBand.Preferred, Blurb = "The bank trusts you now." },
        };

        public static float AprFor(ReputationBand band) => band switch
        {
            ReputationBand.Unknown     => 0.16f,
            ReputationBand.Known       => 0.12f,
            ReputationBand.Respected   => 0.09f,
            ReputationBand.Preferred   => 0.07f,
            _                          => 0.05f,
        };

        public static float WeeklyRate(float apr) => apr / 52f;

        public static int PaymentFor(float principal, float apr, int weeks)
        {
            if (weeks <= 0) return Mathf.CeilToInt(principal);
            float r = WeeklyRate(apr);
            if (r <= 0f) return Mathf.CeilToInt(principal / weeks);
            double p = principal * r / (1.0 - Math.Pow(1.0 + r, -weeks));
            return Mathf.CeilToInt((float)p);
        }

        private readonly List<Loan> _loans = new();
        private bool _mortgageCreated;
        private SimulationTimeService _time;
        private MoneyService _money;

        public IReadOnlyList<Loan> Loans => _loans;
        public int TotalDebt => Mathf.CeilToInt(_loans.Sum(l => Mathf.Max(0f, l.balance)));
        public int WeeklyPayments => _loans.Where(l => l.balance > 0.5f).Sum(l => l.IsFixedWeekly ? l.MinimumDue() : l.weeklyPayment);
        public static event Action OnLoansChanged;

        public void Initialize()
        {
            ServiceLocator.TryGet(out _time);
            ServiceLocator.TryGet(out _money);
            if (_time != null) _time.OnDayChanged += HandleDayChanged;
        }

        public void Shutdown()
        {
            if (_time != null) _time.OnDayChanged -= HandleDayChanged;
        }

        private int Today => _time != null ? _time.Day : 1;

        public ReputationBand CurrentBand =>
            ServiceLocator.TryGet(out ReputationService rep) && rep != null ? rep.Band : ReputationBand.Unknown;

        // ── Opening loans ──────────────────────────────────────────────────

        /// <summary>New game: the starting capital becomes the mortgage on the warehouse (no cash added - the player already has it).</summary>
        public void CreateStartingMortgage(int amount)
        {
            if (_mortgageCreated || amount <= 0) return;
            _mortgageCreated = true;
            var loan = new Loan
            {
                id = Guid.NewGuid().ToString("N"), offerId = "mortgage", name = "Warehouse Mortgage",
                amount = amount, balance = amount, apr = FirstTimeBuildWeeklyRate * 52f, termWeeks = 0,
                weeklyRate = FirstTimeBuildWeeklyRate,
                minPrincipal = Mathf.CeilToInt(amount * FirstTimeBuildPrincipalPercent),
                startDay = Today, nextDueDay = Today + 7,
            };
            loan.weeklyPayment = loan.MinimumDue();
            _loans.Add(loan);
            SystemsLogWindow.LogSystem($"BANK: First-Time Build loan - ${amount:N0} at {FirstTimeBuildWeeklyRate:P1} a week. " +
                                       $"Minimum ${loan.weeklyPayment:N0} a week. Overpay from Capital > BANK.");
            OnLoansChanged?.Invoke();
        }

        /// <summary>Weeks until a fixed-rate loan is cleared at its current overpayment. Principal is a flat floor, so this is exact
        /// for the minimum and an upper bound once overpaying.</summary>
        public int WeeksRemaining(Loan loan)
        {
            if (loan == null || loan.balance <= 0.5f) return 0;
            if (!loan.IsFixedWeekly) return loan.WeeksLeft(loan.EffectiveWeeklyRate);
            int perWeek = loan.minPrincipal + loan.weeklyOverpayment;
            return perWeek <= 0 ? 999 : Mathf.CeilToInt(Mathf.CeilToInt(loan.balance) / (float)perWeek);
        }

        /// <summary>Sets the weekly overpayment for a loan (from the Bank window). Takes effect at the next weekly payment.</summary>
        public void SetOverpayment(Loan loan, int amount)
        {
            if (loan == null) return;
            loan.weeklyOverpayment = Mathf.Max(0, amount);
            SystemsLogWindow.LogSystem($"BANK: overpayment on the {loan.name} set to ${loan.weeklyOverpayment:N0} a week.");
            OnLoansChanged?.Invoke();
        }

        public bool HasActive(string offerId) => _loans.Any(l => l.offerId == offerId && l.balance > 0.5f);

        /// <summary>Why this offer can't be taken right now, or null when it can.</summary>
        public string WhyNot(LoanOffer offer)
        {
            if (CurrentBand < offer.Requires) return $"Requires {ReputationService.BandLabel(offer.Requires)} reputation";
            if (HasActive(offer.Id)) return "Already borrowed - pay it off first";
            return null;
        }

        public bool TakeLoan(LoanOffer offer)
        {
            if (offer == null || WhyNot(offer) != null || _money == null) return false;
            float apr = AprFor(CurrentBand);
            var loan = new Loan
            {
                id = Guid.NewGuid().ToString("N"), offerId = offer.Id, name = offer.Name,
                amount = offer.Amount, balance = offer.Amount, apr = apr, termWeeks = offer.TermWeeks,
                weeklyPayment = PaymentFor(offer.Amount, apr, offer.TermWeeks),
                startDay = Today, nextDueDay = Today + 7,
            };
            _loans.Add(loan);
            // Borrowed money is not revenue: move the balance without a ledger entry.
            _money.RestoreCapital(_money.CurrentCapital + offer.Amount);
            SystemsLogWindow.LogSystem($"BANK: {offer.Name} approved - ${offer.Amount:N0} at {apr:P0} APR, ${loan.weeklyPayment:N0} a week for {offer.TermWeeks} weeks.");
            OnLoansChanged?.Invoke();
            return true;
        }

        // ── Paying ─────────────────────────────────────────────────────────

        private void PayPrincipal(Loan loan, int amount)
        {
            amount = Mathf.Min(amount, Mathf.CeilToInt(loan.balance));
            if (amount <= 0) return;
            _money.RemoveCapital(amount, "Loan Principal");   // not in FinanceCategory.ExpenseOrder: debt repaid, not an expense
            loan.balance = Mathf.Max(0f, loan.balance - amount);
        }

        public bool PayExtra(Loan loan, int amount)
        {
            if (loan == null || _money == null || loan.balance <= 0.5f) return false;
            amount = Mathf.Min(amount, Mathf.CeilToInt(loan.balance));
            if (amount <= 0 || !_money.CanAfford(amount)) return false;
            PayPrincipal(loan, amount);
            if (loan.balance <= 0.5f) PaidOff(loan);
            else SystemsLogWindow.LogSystem($"BANK: paid ${amount:N0} extra on the {loan.name}. ${Mathf.CeilToInt(loan.balance):N0} left.");
            OnLoansChanged?.Invoke();
            return true;
        }

        public bool PayOff(Loan loan) => loan != null && PayExtra(loan, Mathf.CeilToInt(loan.balance));

        private void PaidOff(Loan loan)
        {
            loan.balance = 0f;
            SystemsLogWindow.LogRevenue($"BANK: the {loan.name} is PAID OFF. Nice.");
            _loans.Remove(loan);
        }

        public string WhyNoRefinance(Loan loan)
        {
            if (loan == null || loan.balance <= 0.5f) return "Nothing owed";
            if (loan.IsFixedWeekly) return "Fixed First-Time Build rate";
            if (CurrentBand < RefinanceBand) return $"Requires {ReputationService.BandLabel(RefinanceBand)} reputation";
            if (Today - loan.lastRefinanceDay < RefinanceCooldownDays) return $"Again on day {loan.lastRefinanceDay + RefinanceCooldownDays}";
            if (AprFor(CurrentBand) >= loan.apr - 0.0001f) return "No better rate yet";
            return null;
        }

        public bool Refinance(Loan loan)
        {
            if (WhyNoRefinance(loan) != null) return false;
            int weeks = Mathf.Max(1, loan.WeeksLeft(WeeklyRate(loan.apr)));
            float old = loan.apr;
            loan.apr = AprFor(CurrentBand);
            loan.weeklyPayment = PaymentFor(loan.balance, loan.apr, weeks);
            loan.lastRefinanceDay = Today;
            SystemsLogWindow.LogSystem($"BANK: {loan.name} refinanced {old:P0} -> {loan.apr:P0}. New payment ${loan.weeklyPayment:N0} a week.");
            OnLoansChanged?.Invoke();
            return true;
        }

        // ── The weekly cycle ───────────────────────────────────────────────

        private void HandleDayChanged()
        {
            if (_money == null) ServiceLocator.TryGet(out _money);
            if (_money == null) return;
            int today = Today;
            bool changed = false;
            foreach (var loan in _loans.ToList())
            {
                if (loan.balance <= 0.5f) { _loans.Remove(loan); changed = true; continue; }
                if (today < loan.nextDueDay) continue;
                loan.nextDueDay += 7;
                changed = true;

                float r = loan.EffectiveWeeklyRate;
                int interest = Mathf.CeilToInt(loan.balance * r);
                int principal;
                if (loan.IsFixedWeekly)
                    principal = Mathf.Min(loan.minPrincipal, Mathf.CeilToInt(loan.balance));
                else
                    principal = Mathf.Min(Mathf.Max(0, loan.weeklyPayment - interest), Mathf.CeilToInt(loan.balance));
                int due = interest + principal;

                if (!_money.CanAfford(due))
                {
                    // MISSED: the interest is added to what is owed, plus the late fee; reputation takes the hit.
                    int fee = Mathf.CeilToInt(Mathf.Max(loan.weeklyPayment, due) * MissedPaymentFeePercent);
                    loan.balance += interest + fee;
                    loan.missedPayments++;
                    if (ServiceLocator.TryGet(out ReputationService rep) && rep != null)
                        rep.Add(-MissedPaymentReputation, $"Missed a {loan.name} payment");
                    SystemsLogWindow.LogWarning($"BANK: MISSED the ${due:N0} {loan.name} payment - ${fee:N0} late fee added, reputation -{MissedPaymentReputation}.");
                    continue;
                }

                if (interest > 0)
                {
                    _money.RemoveCapital(interest, FinanceCategory.LeaseMortgage, "Loan Interest");
                    loan.interestPaid += interest;
                }
                PayPrincipal(loan, principal);
                if (loan.balance <= 0.5f) { PaidOff(loan); continue; }

                // Overpayment: the player's extra principal, only when the cash is there after the minimum. Skipped quietly otherwise
                // (it is optional, so it never counts as a missed payment).
                if (loan.IsFixedWeekly && loan.weeklyOverpayment > 0)
                {
                    int extra = Mathf.Min(loan.weeklyOverpayment, Mathf.CeilToInt(loan.balance));
                    if (_money.CanAfford(extra))
                    {
                        PayPrincipal(loan, extra);
                        if (loan.balance <= 0.5f) PaidOff(loan);
                    }
                }
            }
            if (changed) OnLoansChanged?.Invoke();
        }

        // ── Persistence ────────────────────────────────────────────────────

        public LoanSnapshot Export() => new LoanSnapshot { loans = _loans.Select(Clone).ToList(), mortgageCreated = _mortgageCreated };

        public void Import(LoanSnapshot snap)
        {
            _loans.Clear();
            _mortgageCreated = snap != null && snap.mortgageCreated;
            if (snap?.loans != null) _loans.AddRange(snap.loans.Where(l => l != null).Select(Clone));
            OnLoansChanged?.Invoke();
        }

        private static Loan Clone(Loan l) => JsonUtility.FromJson<Loan>(JsonUtility.ToJson(l));
    }
}
