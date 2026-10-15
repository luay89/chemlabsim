// ChemLabSim v3 — Limiting reactant and theoretical yield from real amounts
//
// For a balanced reaction  νA A + νB B → νC C + …  and available amounts nA, nB (mol):
//   extent each reactant allows  ξ_i = n_i / ν_i
//   limiting reactant            argmin ξ_i
// The arithmetic is done by the existing Stoichiometry.Solve (rate factor 1 = theoretical);
// this class adds what it does not check: a balanced equation, valid amounts for every
// reactant, exact-proportion ties, and grams of product.
//   maximum extent               ξ = min ξ_i
//   excess left over             n_i − ν_i ξ
//   theoretical yield            ν_p ξ  mol,   × M_p  g   (when M_p is known)
//
// Results are only produced when the equation is atom-balanced (water of the solution
// counted via solvent_water) and every reactant has a valid amount; otherwise the status
// says what is missing. Reactants within 1e-6 (relative) of the minimum are all "used up
// exactly": a stoichiometric mixture has no excess and no single limiting reactant.

using System;
using System.Collections.Generic;
using System.Linq;

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum StoichiometryStatus { Ok, NoReaction, Unbalanced, InsufficientData }

    public enum ReactantRole { Limiting, Excess, ExactlyUsed }

    public struct ReactantShare
    {
        public string Formula;
        public float Coefficient;
        public double AvailableMol;
        public double UsedMol;
        public double LeftOverMol;
        public ReactantRole Role;
    }

    public struct ProductAmount
    {
        public string Formula;
        public string State;
        public float Coefficient;
        public double TheoreticalMol;
        public double TheoreticalGrams;   // NaN when the molar mass is unavailable
        public string Note;
    }

    public class LimitingReactantResult
    {
        public StoichiometryStatus Status;
        public string Message = string.Empty;
        public double Extent = double.NaN;
        public bool IsStoichiometric;                       // all reactants used up together
        public List<string> LimitingFormulas = new List<string>();
        public List<ReactantShare> Reactants = new List<ReactantShare>();
        public List<ProductAmount> Products = new List<ProductAmount>();

        public bool Success => Status == StoichiometryStatus.Ok;
    }

    public static class LimitingReactant
    {
        private const double TieTolerance = 1e-6;

        /// <param name="availableMoles">Formula → amount in mol (from QuantityConverter).</param>
        public static LimitingReactantResult Calculate(ReactionEntry reaction, IReadOnlyDictionary<string, double> availableMoles)
        {
            var result = new LimitingReactantResult();
            if (reaction?.reactants == null || reaction.reactants.Count == 0)
                return Fail(result, StoichiometryStatus.NoReaction, "There is no reaction to calculate.");

            if (!IsBalanced(reaction, out string balanceError))
                return Fail(result, StoichiometryStatus.Unbalanced,
                    $"The stored equation is not balanced ({balanceError}), so amounts cannot be compared.");

            var missing = new List<string>();
            var states = new List<ChemState>(reaction.reactants.Count);
            foreach (var r in reaction.reactants)
            {
                double n = double.NaN;
                bool has = r != null && availableMoles != null && availableMoles.TryGetValue(r.formula, out n);
                if (!has || double.IsNaN(n) || double.IsInfinity(n) || n <= 0.0 || r.stoich <= 0f)
                    missing.Add(r?.formula ?? "?");
                states.Add(new ChemState { Formula = r?.formula ?? string.Empty, Phase = ChemState.ParsePhase(r?.state), Moles = has ? (float)n : 0f });
            }
            if (missing.Count > 0)
                return Fail(result, StoichiometryStatus.InsufficientData,
                    $"No valid amount for {string.Join(", ", missing)}.");

            // Theoretical case: the reaction runs to completion (rate factor 1).
            var solved = Stoichiometry.Solve(reaction, states, 1f);
            double extent = solved.MaxExtent;
            result.Extent = extent;

            int exact = 0;
            for (int i = 0; i < reaction.reactants.Count; i++)
            {
                var r = reaction.reactants[i];
                double n = availableMoles[r.formula];
                bool tied = Math.Abs(n / r.stoich - extent) <= TieTolerance * Math.Max(extent, 1e-12);
                if (tied) exact++;
                result.Reactants.Add(new ReactantShare
                {
                    Formula = r.formula,
                    Coefficient = r.stoich,
                    AvailableMol = n,
                    UsedMol = solved.ReactantMolesConsumed[i],
                    LeftOverMol = tied ? 0.0 : solved.ReactantMolesRemaining[i],
                    Role = tied ? ReactantRole.Limiting : ReactantRole.Excess
                });
            }

            result.IsStoichiometric = exact == reaction.reactants.Count;
            if (result.IsStoichiometric)
            {
                for (int i = 0; i < result.Reactants.Count; i++)
                {
                    var share = result.Reactants[i];
                    share.Role = ReactantRole.ExactlyUsed;
                    result.Reactants[i] = share;
                }
            }
            else
            {
                result.LimitingFormulas = result.Reactants.Where(x => x.Role == ReactantRole.Limiting).Select(x => x.Formula).ToList();
            }

            foreach (var y in solved.ProductYields)
            {
                var mm = MolarMass.Of(y.Formula);
                var source = reaction.products.First(p => p != null && p.formula != null && p.formula.Trim() == y.Formula);
                result.Products.Add(new ProductAmount
                {
                    Formula = y.Formula,
                    State = source.state,
                    Coefficient = y.Stoich,
                    TheoreticalMol = y.TheoreticalMoles,
                    TheoreticalGrams = mm.Success ? y.TheoreticalMoles * mm.GramsPerMole : double.NaN,
                    Note = mm.Success ? string.Empty : mm.Error
                });
            }

            result.Status = StoichiometryStatus.Ok;
            result.Message = result.IsStoichiometric
                ? "The amounts are in exact stoichiometric proportion: every reactant is used up, none is left over."
                : $"Limiting reactant: {string.Join(", ", result.LimitingFormulas)}.";
            return result;
        }

        /// <summary>Atom balance via ConservationValidator, counting the solution's water when declared.</summary>
        public static bool IsBalanced(ReactionEntry reaction, out string error)
        {
            var check = reaction;
            if (reaction.solvent_water > 0)
            {
                check = new ReactionEntry
                {
                    id = reaction.id,
                    name_en = reaction.name_en,
                    reactants = new List<ReactionChemical>(reaction.reactants)
                    {
                        new ReactionChemical { formula = "H2O", state = "l", stoich = reaction.solvent_water }
                    },
                    products = reaction.products
                };
            }

            var issues = new List<ValidationIssue>();
            ConservationValidator.CheckAtomBalance(check, issues);
            var problem = issues.FirstOrDefault(i => i.Severity == ValidationSeverity.Error);
            error = problem.Detail ?? string.Empty;
            return !issues.Any(i => i.Severity == ValidationSeverity.Error);
        }

        private static LimitingReactantResult Fail(LimitingReactantResult r, StoichiometryStatus status, string message)
        {
            r.Status = status;
            r.Message = message;
            return r;
        }
    }
}
