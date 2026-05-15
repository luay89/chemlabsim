// ChemLabSim v3 — Stoichiometry Solver
// Mole-based calculations: limiting reagent, theoretical yield,
// actual yield (adjusted by reaction rate), % completion, and byproducts.
// Pure static math — no state, no side effects.

using System;
using System.Collections.Generic;

namespace ChemLabSimV3.Engine.Chemistry
{
    /// <summary>Per-product yield result.</summary>
    public struct ProductYield
    {
        public string Formula;
        public Phase Phase;
        public float Stoich;
        public float TheoreticalMoles;
        public float ActualMoles;
    }

    /// <summary>Complete result of a stoichiometric calculation.</summary>
    public struct StoichResult
    {
        /// <summary>Index of the limiting reagent in the reactants list.</summary>
        public int LimitingIndex;
        public string LimitingFormula;

        /// <summary>Maximum fraction of the reaction that can proceed (0–1).</summary>
        public float MaxExtent;

        /// <summary>Actual extent after applying rate/condition factor (0–1).</summary>
        public float ActualExtent;

        /// <summary>Completion percentage displayed to the student (0–100).</summary>
        public float CompletionPercent;

        /// <summary>Moles of each reactant consumed.</summary>
        public List<float> ReactantMolesConsumed;

        /// <summary>Moles of each reactant remaining after reaction.</summary>
        public List<float> ReactantMolesRemaining;

        /// <summary>Calculated product yields.</summary>
        public List<ProductYield> ProductYields;
    }

    public static class Stoichiometry
    {
        /// <summary>
        /// Solve stoichiometry for a reaction given reactant states and a rate factor.
        /// <para><paramref name="rateFactor"/>: 0–1 from the condition pipeline (how completely
        /// the reaction proceeds under current lab conditions).</para>
        /// </summary>
        public static StoichResult Solve(
            ReactionEntry reaction,
            IList<ChemState> reactantStates,
            float rateFactor)
        {
            var result = new StoichResult
            {
                LimitingIndex          = -1,
                LimitingFormula        = string.Empty,
                MaxExtent              = 0f,
                ActualExtent           = 0f,
                CompletionPercent      = 0f,
                ReactantMolesConsumed  = new List<float>(),
                ReactantMolesRemaining = new List<float>(),
                ProductYields          = new List<ProductYield>()
            };

            if (reaction == null || reactantStates == null || reactantStates.Count == 0)
                return result;

            var reactants = reaction.reactants;
            if (reactants == null || reactants.Count == 0)
                return result;

            int count = Math.Min(reactants.Count, reactantStates.Count);

            // 1. Find limiting reagent
            //    extent = availableMoles / stoichCoeff
            //    The reagent with the smallest extent limits the reaction.
            float minExtent = float.MaxValue;
            int limitingIdx = 0;

            for (int i = 0; i < count; i++)
            {
                float stoich = reactants[i] != null ? reactants[i].stoich : 1f;
                if (stoich <= 0f) stoich = 1f;

                float available = reactantStates[i].Moles;
                float extent = available / stoich;

                if (extent < minExtent)
                {
                    minExtent = extent;
                    limitingIdx = i;
                }
            }

            result.LimitingIndex   = limitingIdx;
            result.LimitingFormula = reactants[limitingIdx]?.formula ?? string.Empty;
            result.MaxExtent       = Math.Max(0f, minExtent);

            // 2. Apply rate factor to get actual extent
            float clampedRate = Math.Max(0f, Math.Min(1f, rateFactor));
            result.ActualExtent = result.MaxExtent * clampedRate;
            result.CompletionPercent = clampedRate * 100f;

            // 3. Calculate reactant consumption
            for (int i = 0; i < count; i++)
            {
                float stoich = reactants[i] != null ? reactants[i].stoich : 1f;
                if (stoich <= 0f) stoich = 1f;

                float consumed = result.ActualExtent * stoich;
                float remaining = Math.Max(0f, reactantStates[i].Moles - consumed);

                result.ReactantMolesConsumed.Add(consumed);
                result.ReactantMolesRemaining.Add(remaining);
            }

            // 4. Calculate product yields
            if (reaction.products != null)
            {
                for (int i = 0; i < reaction.products.Count; i++)
                {
                    var p = reaction.products[i];
                    if (p == null || string.IsNullOrWhiteSpace(p.formula)) continue;

                    float pStoich = p.stoich > 0f ? p.stoich : 1f;
                    float theoretical = result.MaxExtent * pStoich;
                    float actual = result.ActualExtent * pStoich;

                    result.ProductYields.Add(new ProductYield
                    {
                        Formula         = p.formula.Trim(),
                        Phase           = ChemState.ParsePhase(p.state),
                        Stoich          = pStoich,
                        TheoreticalMoles = theoretical,
                        ActualMoles     = actual
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Build a balanced equation string with stoichiometric coefficients.
        /// Example: "2 Mg(s) + O₂(g) → 2 MgO(s)"
        /// </summary>
        public static string BuildEquation(ReactionEntry reaction)
        {
            if (reaction == null) return string.Empty;

            var lhs = new List<string>();
            var rhs = new List<string>();

            if (reaction.reactants != null)
            {
                for (int i = 0; i < reaction.reactants.Count; i++)
                {
                    var r = reaction.reactants[i];
                    if (r == null || string.IsNullOrWhiteSpace(r.formula)) continue;
                    lhs.Add(FormatTerm(r.formula.Trim(), r.stoich, r.state));
                }
            }

            if (reaction.products != null)
            {
                for (int i = 0; i < reaction.products.Count; i++)
                {
                    var p = reaction.products[i];
                    if (p == null || string.IsNullOrWhiteSpace(p.formula)) continue;
                    rhs.Add(FormatTerm(p.formula.Trim(), p.stoich, p.state));
                }
            }

            string left = lhs.Count > 0 ? string.Join(" + ", lhs) : "?";
            string right = rhs.Count > 0 ? string.Join(" + ", rhs) : "?";

            return $"{left} \u2192 {right}";
        }

        private static string FormatTerm(string formula, float stoich, string state)
        {
            string coeff = stoich > 1f ? $"{stoich:0.##} " : "";
            string phase = !string.IsNullOrWhiteSpace(state) ? $"({state})" : "";
            return $"{coeff}{formula}{phase}";
        }
    }
}
