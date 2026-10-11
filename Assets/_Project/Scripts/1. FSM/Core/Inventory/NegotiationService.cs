using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Inventory
{
    public enum NegotiationOutcome { Accepted, Countered, Rejected, WalkedAway }

    /// <summary>How a customer weighs the six terms. Same rules for everyone - this only changes which
    /// rows they dig their heels in on, so the player learns to read the company, not a single formula.</summary>
    public enum CustomerStyle { Balanced, PennyPincher, Clockwatcher }

    public class NegotiationResult
    {
        public NegotiationOutcome Outcome;
        public ContractTerms Counter;   // set when Outcome == Countered
        public string Message;
        public float Appetite;          // score / budget for the offer just made (1 = right at their limit)
        public ContractTerms FinalTerms; // set when Accepted: what actually gets signed (the ask, or a sweetened ask on a critical roll)
        public int Roll, RollTotal;      // the d20 and the total with modifiers; 0 = no dice were used
        public string RollNote;          // "14 +2 reputation +1 partnership = 17"
        public bool CriticalSuccess, CriticalFail;
    }

    /// <summary>
    /// One sitting at the table with one customer.
    ///
    /// THE MODEL (Tad, 2026-10-11: "a spreadsheet game"). Every term has a push value from 0 (their opening
    /// offer) to 1 (our maximum ask). The customer weighs those pushes by what they care about and adds
    /// them into a single SCORE for the whole package. They have a concession BUDGET set by how much they
    /// want us - our reputation, our history with them, the difficulty. Score under budget: deal. A little
    /// over: they counter by scaling the package back to fit. Way over: no, and they lose patience. So the
    /// game is allocating a limited budget across six rows - ask hard where it pays, give where it's cheap.
    ///
    /// Always safe: no term harder than Standard is an automatic yes, so the player can never be shut out
    /// of an account by a fumbled negotiation, only out of a BETTER deal.
    /// </summary>
    public class NegotiationSession
    {
        public const int MaxPatience = 3;
        /// <summary>How far past the budget a package can be and still earn a counter-offer rather than a no.</summary>
        public const float CounterWindow = 0.14f;

        public ContractData Contract;
        public string CompanyName;
        public CustomerStyle Style;
        public float[] Weights;
        public float Budget;

        // Where the budget came from, for the "your standing" readout in the negotiation window.
        public int Reputation;
        public int Difficulty;
        public float Relationship;
        public bool HasHistory;
        public float BaseBudget, RepBonus, RelBonus, DiffBonus, MoodBonus;

        public int Patience = MaxPatience;
        public int Round;
        public ContractTerms Opening;
        public ContractTerms Standard;
        public ContractTerms LastCounter;
        public bool Finished;
        public readonly List<string> Log = new();

        float StandardScore;

        public void Init()
        {
            Opening = TermCatalog.OpeningOffer();
            Standard = TermCatalog.StandardTerms();
            StandardScore = Score(Standard);
        }

        public float Score(ContractTerms t)
        {
            float s = 0f;
            for (int i = 0; i < TermCatalog.All.Count; i++)
            {
                var spec = TermCatalog.All[i];
                s += Weights[i] * Mathf.Clamp(spec.Push(spec.Get(t)), 0f, 1.2f);
            }
            return s;
        }

        /// <summary>score/budget for the UI gauge: under 1 = they'll sign, up to about 1.3 = counter, beyond = no.</summary>
        public float Appetite(ContractTerms t) => Budget <= 0.01f ? 9f : Score(t) / Budget;

        // ── The dice ─────────────────────────────────────────────────────
        //
        // A d20 is rolled for every offer. Modifiers come from the player's standing: reputation band (+0..+4), the partnership with
        // this customer (-2 strained .. +2 preferred) and the difficulty (+1 Clerk .. -1 Manager). Natural 1 (or a total of 3 or
        // less) is an automatic loss for the round; natural 20 (or 24+) is a critical - they accept AND throw in a sweetener.
        // Anything in between nudges this round's budget up or down by 1.2% a point around an average roll of 11, so the dice
        // bend the odds without overriding the maths of the package.
        public const int CritFailTotal = 3, CritSuccessTotal = 24;

        public int RollModifier(out string breakdown)
        {
            int rep = Mathf.Clamp(Reputation / 250, 0, 4);
            int partner = !HasHistory ? 0 : Relationship >= 0.9f ? 2 : Relationship >= 0.7f ? 1 : Relationship >= 0.4f ? 0 : -2;
            int diff = Difficulty == 0 ? 1 : Difficulty >= 2 ? -1 : 0;
            breakdown = $"{Sg(rep)} reputation  {Sg(partner)} partnership  {Sg(diff)} difficulty";
            return rep + partner + diff;
        }

        static string Sg(int v) => v >= 0 ? $"+{v}" : v.ToString();

        /// <summary>
        /// The EXACT roll needed for this package - not an estimate. Everything is a fixed equation; the d20 is the only random number.
        /// The roll moves their budget by 1.2% per point of (roll + modifiers) above or below 11, capped at +-14%, and the deal needs
        /// budget >= score. So with s = score - budget:  s <= -0.14 any roll works; otherwise total >= 11 + ceil(s / 0.012), and a total
        /// of 24 (or a natural 20) always wins no matter how far over. A natural 1 or a total of 3 or less always loses.
        /// </summary>
        public void RollNeeded(ContractTerms ask, out int dealRoll, out int counterRoll, out int modifier, out string text)
        {
            modifier = RollModifier(out _);
            bool free = true;
            foreach (var spec in TermCatalog.All)
                if (spec.Push(spec.Get(ask)) > spec.Push(spec.Get(Standard)) + 0.001f) { free = false; break; }

            float s = Score(ask) - Budget;
            int dealTotal = free || s <= -0.14f ? CritFailTotal + 1 : Mathf.Min(CritSuccessTotal, 11 + Mathf.CeilToInt(s / 0.012f - 0.0001f));
            int counterTotal = free || s - CounterWindow <= -0.14f ? CritFailTotal + 1 : Mathf.Min(CritSuccessTotal, 11 + Mathf.CeilToInt((s - CounterWindow) / 0.012f - 0.0001f));
            dealTotal = Mathf.Max(CritFailTotal + 1, dealTotal);          // a total of 3 or less is an automatic loss whatever the maths says
            counterTotal = Mathf.Max(CritFailTotal + 1, Mathf.Min(counterTotal, dealTotal));
            dealRoll = Mathf.Clamp(dealTotal - modifier, 2, 20);        // a natural 1 never works, a natural 20 always does
            counterRoll = Mathf.Clamp(counterTotal - modifier, 2, 20);

            if (free) { dealRoll = 1; counterRoll = 1; }   // Standard-or-easier is accepted outright - even a natural 1 cannot lose it

            string mod = modifier >= 0 ? $"+{modifier}" : modifier.ToString();
            if (free) text = "Standard terms or easier: a deal on any roll.";
            else if (dealRoll <= 2) text = $"Deal on any roll but a 1 (you add {mod}).";
            else if (dealRoll >= 20) text = $"Deal only on a natural 20 (you add {mod}).";
            else text = $"Deal on a roll of {dealRoll} or more (you add {mod}).";
            if (!free && dealRoll > 2 && counterRoll < dealRoll) text += counterRoll <= 2 ? "  A counter on anything else but a 1." : $"  A counter on {counterRoll}+.";
        }

        public NegotiationResult Submit(ContractTerms ask, int roll = 0)
        {
            Round++;
            float score = Score(ask);
            var result = new NegotiationResult { Appetite = score / Mathf.Max(0.01f, Budget), FinalTerms = ask };

            float budget = Budget;
            if (roll > 0)
            {
                int mod = RollModifier(out string breakdown);
                int total = roll + mod;
                result.Roll = roll; result.RollTotal = total;
                result.RollNote = $"d20 {roll}  {breakdown}  = {total}";
                result.CriticalFail = roll == 1 || total <= CritFailTotal;
                result.CriticalSuccess = roll == 20 || total >= CritSuccessTotal;
                budget = Mathf.Max(0.05f, Budget + Mathf.Clamp((total - 11) * 0.012f, -0.14f, 0.14f));
            }

            // Nothing harder than market rate on any row = always a yes.
            bool noHarderThanStandard = true;
            foreach (var spec in TermCatalog.All)
                if (spec.Push(spec.Get(ask)) > spec.Push(spec.Get(Standard)) + 0.001f) { noHarderThanStandard = false; break; }

            if (noHarderThanStandard || score <= budget && !result.CriticalFail)
            {
                result.Outcome = NegotiationOutcome.Accepted;
                result.Message = Pick(AcceptLines);
                Finished = true;
            }
            else if (result.CriticalSuccess)
            {
                // Natural charm: they sign the ask AND give ground on every row the player pushed.
                result.Outcome = NegotiationOutcome.Accepted;
                result.FinalTerms = Sweeten(ask);
                result.Message = "Natural charm! They love your style - and they'll give you even more than you asked.";
                Finished = true;
            }
            else if (!result.CriticalFail && score <= budget + CounterWindow)
            {
                result.Outcome = NegotiationOutcome.Countered;
                result.Counter = ScaleBackTo(ask, score, budget);
                LastCounter = result.Counter;
                result.Message = Pick(CounterLines);
            }
            else
            {
                Patience--;
                if (result.CriticalFail) Log.Add("Critical fail - they bristled at your tone.");
                if (Patience <= 0)
                {
                    result.Outcome = NegotiationOutcome.WalkedAway;
                    result.Message = Pick(WalkLines);
                    Finished = true;
                }
                else
                {
                    result.Outcome = NegotiationOutcome.Rejected;
                    result.Message = $"{Pick(RejectLines)} {WorstOffender(ask)}";
                }
            }

            Log.Add($"Round {Round}: {result.Outcome} - {result.Message}");
            return result;
        }

        /// <summary>The counter: the player's package pulled back toward Standard just far enough to land on the
        /// customer's budget. Rounded toward Standard so the counter never sneaks back over the line.</summary>
        /// <summary>A critical success: every row the player pushed beyond Standard moves a quarter of the way to the player's maximum.</summary>
        ContractTerms Sweeten(ContractTerms ask)
        {
            var t = ask.Clone();
            foreach (var spec in TermCatalog.All)
            {
                float v = spec.Get(ask);
                if (spec.Push(v) <= spec.Push(spec.Get(Standard)) + 0.001f) continue;   // only rows they were already pushed on
                spec.Set(t, spec.Snap(v + (spec.Limit - v) * 0.25f));
            }
            return t;
        }

        ContractTerms ScaleBackTo(ContractTerms ask, float askScore, float budget)
        {
            float k = askScore - StandardScore <= 0.0001f ? 0f : Mathf.Clamp01((budget - StandardScore) / (askScore - StandardScore));
            var counter = ask.Clone();
            foreach (var spec in TermCatalog.All)
            {
                float std = spec.Get(Standard), want = spec.Get(ask);
                float v = std + (want - std) * k;
                float snapped = spec.Snap(v);
                // Snap toward Standard, never past the exact scaled value.
                bool askedHigher = want > std;
                if (askedHigher && snapped > v) snapped -= spec.Step;
                if (!askedHigher && snapped < v) snapped += spec.Step;
                spec.Set(counter, spec.Snap(snapped));
            }
            return counter;
        }

        string WorstOffender(ContractTerms ask)
        {
            TermSpec worst = null; float worstExcess = 0f;
            for (int i = 0; i < TermCatalog.All.Count; i++)
            {
                var spec = TermCatalog.All[i];
                float excess = Weights[i] * (spec.Push(spec.Get(ask)) - spec.Push(spec.Get(Standard)));
                if (excess > worstExcess) { worstExcess = excess; worst = spec; }
            }
            return worst == null ? "" : $"\"{worst.Label} is where this falls apart.\"";
        }

        string Pick(string[] lines) => lines[Mathf.Abs((CompanyName ?? "x").GetHashCode() + Round * 7919) % lines.Length];

        static readonly string[] AcceptLines =
        {
            "Done. Pleasure doing business.", "That works for us. Where do we sign?", "Fair enough. You've got a deal."
        };
        static readonly string[] CounterLines =
        {
            "We're close. Here's what we can live with.", "Can't go that far, but meet us here and we'll sign.",
            "Almost. Pull it back a little and it's a deal."
        };
        static readonly string[] RejectLines =
        {
            "No chance.", "You're not serious.", "We have other warehouses, you know."
        };
        static readonly string[] WalkLines =
        {
            "We're done here. Don't call us.", "This isn't working. We'll take our freight elsewhere.",
            "Enough. Good day."
        };
    }

    public static class NegotiationService
    {
        /// <summary>Days a customer who walked out won't sit down with us again.</summary>
        public const int WalkAwayCooldownDays = 3;

        /// <summary>
        /// Opens negotiations. relationship is 0-1 (0.5 = never worked together); difficulty is the
        /// PlayerPrefs value (0 Clerk, 1 Supervisor, 2 Manager).
        /// </summary>
        public static NegotiationSession Begin(ContractData contract, int reputation, float relationship, int difficulty, bool hasHistory = false)
        {
            string name = contract != null && contract.Customer != null ? contract.Customer.CompanyName : "Customer";
            int seed = Mathf.Abs((contract != null ? contract.ContractId : name).GetHashCode());
            var style = (CustomerStyle)(seed % 3);

            var s = new NegotiationSession
            {
                Contract = contract,
                CompanyName = name,
                Style = style,
                Weights = WeightsFor(style),
            };
            BudgetParts(reputation, relationship, difficulty, seed, out s.RepBonus, out s.RelBonus, out s.DiffBonus, out s.MoodBonus);
            s.BaseBudget = BaseBudget;
            s.Reputation = reputation;
            s.Difficulty = difficulty;
            s.Relationship = relationship;
            s.HasHistory = hasHistory;
            s.Budget = BudgetFor(reputation, relationship, difficulty, seed);
            s.Init();
            return s;
        }

        /// <summary>How much of a concession budget this customer has: 0.34 for a stranger on Supervisor, up to
        /// about 0.8 for a customer who adores us.</summary>
        public const float BaseBudget = 0.34f;

        public static void BudgetParts(int reputation, float relationship, int difficulty, int seed,
                                       out float repBonus, out float relBonus, out float diffBonus, out float mood)
        {
            float rep01 = Mathf.Clamp01(reputation / (float)ReputationService.MaxScore);
            repBonus = 0.40f * rep01;
            relBonus = 0.14f * (Mathf.Clamp01(relationship) - 0.5f) * 2f;
            diffBonus = difficulty == 0 ? 0.08f : difficulty >= 2 ? -0.03f : 0f;
            mood = ((seed % 7) - 3) * 0.01f;   // -0.03 .. +0.03, fixed per customer
        }

        public static float BudgetFor(int reputation, float relationship, int difficulty, int seed)
        {
            BudgetParts(reputation, relationship, difficulty, seed, out float rep, out float rel, out float diff, out float mood);
            return Mathf.Clamp(BaseBudget + rep + rel + diff + mood, 0.30f, 0.85f);
        }

        /// <summary>Partnership tier with one customer, from how satisfied their last account was.</summary>
        public static string PartnershipLabel(bool hasHistory, float relationship)
        {
            if (!hasHistory) return "New - no history yet";
            if (relationship >= 0.9f) return "Preferred partner";
            if (relationship >= 0.7f) return "Trusted";
            if (relationship >= 0.4f) return "Steady";
            return "Strained";
        }

        /// <summary>Weights in TermCatalog order: markup, pick fee, pallet fee, notice, short-notice fee, late fee. Sum to 1.</summary>
        public static float[] WeightsFor(CustomerStyle style) => style switch
        {
            CustomerStyle.PennyPincher => new[] { 0.30f, 0.22f, 0.20f, 0.08f, 0.08f, 0.12f },
            CustomerStyle.Clockwatcher => new[] { 0.10f, 0.08f, 0.10f, 0.28f, 0.14f, 0.30f },
            _ => new[] { 0.20f, 0.15f, 0.18f, 0.17f, 0.12f, 0.18f },
        };

        public static string StyleLabel(CustomerStyle style) => style switch
        {
            CustomerStyle.PennyPincher => "Penny-pincher - fights hardest over price",
            CustomerStyle.Clockwatcher => "Clockwatcher - fights hardest over timing and late fees",
            _ => "Balanced - no strong feelings",
        };

        /// <summary>Rough revenue and margin per day under these terms, for the spreadsheet footer.</summary>
        public static void EstimateDaily(ContractData contract, ContractTerms t, float avgCostPerCase, float avgCasesPerPallet,
                                         out float revenue, out float margin)
        {
            float cases = contract != null ? contract.EstimatedCasesPerDay : 0f;
            float pallets = avgCasesPerPallet > 0.5f ? cases / avgCasesPerPallet : 0f;
            float goods = cases * avgCostPerCase;
            margin = goods * t.MarkupPercent + cases * t.PickFeePerCase + pallets * t.PalletFee;
            revenue = goods + margin;
        }
    }
}
