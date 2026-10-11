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

        public NegotiationResult Submit(ContractTerms ask)
        {
            Round++;
            float score = Score(ask);
            var result = new NegotiationResult { Appetite = score / Mathf.Max(0.01f, Budget) };

            // Nothing harder than market rate on any row = always a yes.
            bool noHarderThanStandard = true;
            foreach (var spec in TermCatalog.All)
                if (spec.Push(spec.Get(ask)) > spec.Push(spec.Get(Standard)) + 0.001f) { noHarderThanStandard = false; break; }

            if (noHarderThanStandard || score <= Budget)
            {
                result.Outcome = NegotiationOutcome.Accepted;
                result.Message = Pick(AcceptLines);
                Finished = true;
            }
            else if (score <= Budget + CounterWindow)
            {
                result.Outcome = NegotiationOutcome.Countered;
                result.Counter = ScaleBackTo(ask, score);
                LastCounter = result.Counter;
                result.Message = Pick(CounterLines);
            }
            else
            {
                Patience--;
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
        ContractTerms ScaleBackTo(ContractTerms ask, float askScore)
        {
            float k = askScore - StandardScore <= 0.0001f ? 0f : Mathf.Clamp01((Budget - StandardScore) / (askScore - StandardScore));
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
        public static NegotiationSession Begin(ContractData contract, int reputation, float relationship, int difficulty)
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
            s.Budget = BudgetFor(reputation, relationship, difficulty, seed);
            s.Init();
            return s;
        }

        /// <summary>How much of a concession budget this customer has: 0.34 for a stranger on Supervisor, up to
        /// about 0.8 for a customer who adores us.</summary>
        public static float BudgetFor(int reputation, float relationship, int difficulty, int seed)
        {
            float rep01 = Mathf.Clamp01(reputation / (float)ReputationService.MaxScore);
            float diff = difficulty == 0 ? 0.08f : difficulty >= 2 ? -0.03f : 0f;
            float mood = ((seed % 7) - 3) * 0.01f;   // -0.03 .. +0.03, fixed per customer
            return Mathf.Clamp(0.34f + 0.40f * rep01 + 0.14f * (Mathf.Clamp01(relationship) - 0.5f) * 2f + diff + mood, 0.30f, 0.85f);
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
