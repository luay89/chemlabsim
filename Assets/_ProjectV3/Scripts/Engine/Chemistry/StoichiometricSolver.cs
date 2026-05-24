// ChemLabSim v3 — Stoichiometric Solver
// Enforces molar ratio constraints strictly and computes limiting-reactant logic.
//
// For a balanced reaction:  νA A + νB B → νC C + νD D
//
//   Extent ξ (mol):  Δn_i = νi × Δξ  (negative for reactants, positive for products)
//   Limiting reagent: index i* = argmin(n_i / νi) over reactants
//   Maximum extent:  ξ_max = min_i( n_i / νi )
//   Progress:        p = ξ / ξ_max  ∈ [0, 1]
//
// Initialization rule:
//   Start each reactant at n_i(0) = νi × scale (stoichiometric amounts).
//   This gives [X_i]₀ = νi / V mol/L and ensures ξ_max = scale for ALL species.
//   Setting scale = 1 mol → progress is numerically equal to extent (ξ_max = 1).
//
// Usage:
//   StoichiometricSolver.InitializeStoichiometricMoles(s.Reactants);
//   float dξ = StoichiometricSolver.ClampExtent(s.Reactants, proposed_dξ);
//   StoichiometricSolver.Apply(s.Reactants, s.Products, dξ);

using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    /// <summary>Stoichiometric accounting summary computed from current species moles.</summary>
    public struct StoichiometricBudget
    {
        /// <summary>Maximum feasible extent from current moles: ξ_max = min(n_i / νi).</summary>
        public float MaxExtent;

        /// <summary>Index of the limiting reactant in the Reactants array.</summary>
        public int LimitingIndex;

        /// <summary>Formula of the limiting reactant.</summary>
        public string LimitingFormula;

        /// <summary>
        /// Excess fraction for each reactant relative to the limiting reagent.
        /// 0 = exactly stoichiometric, >0 = excess, −1 = this IS the limiting reagent.
        /// </summary>
        public float[] ExcessFractions;
    }

    public static class StoichiometricSolver
    {
        private const float MinStoich = 1e-6f;

        // ═══════════════════════════════════════════════════════
        //  INITIALIZATION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Set initial moles equal to stoichiometric coefficients × scale.
        ///   n_i(0) = νi × scale
        ///
        /// With V = 1 L and scale = 1:
        ///   [X_i]₀ = νi mol/L
        ///   ξ_max  = 1 mol (same for every species by definition)
        ///
        /// This ensures progress is numerically meaningful and the rate law
        /// starts at kf × Π(νi)^orderi rather than kf (all at 1 mol/L).
        /// </summary>
        public static void InitializeStoichiometricMoles(SpeciesState[] reactants, float scale = 1f)
        {
            if (reactants == null) return;
            scale = Mathf.Max(scale, MinStoich);

            for (int i = 0; i < reactants.Length; i++)
            {
                var r = reactants[i];
                if (r == null) continue;
                float n = Mathf.Max(r.StoichCoeff, MinStoich) * scale;
                r.InitialMoles = n;
                r.Moles = n;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  LIMITING REAGENT
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Find the maximum feasible extent from current reactant moles.
        /// ξ_max = min_i( n_i / νi )
        /// </summary>
        public static float FindMaxExtent(SpeciesState[] reactants)
        {
            if (reactants == null || reactants.Length == 0) return 0f;

            float min = float.MaxValue;
            for (int i = 0; i < reactants.Length; i++)
            {
                var r = reactants[i];
                if (r == null || r.StoichCoeff < MinStoich) continue;
                float e = r.Moles / r.StoichCoeff;
                if (e < min) min = e;
            }

            return min == float.MaxValue ? 0f : Mathf.Max(0f, min);
        }

        /// <summary>
        /// Find the index of the limiting reactant: argmin_i( n_i / νi ).
        /// </summary>
        public static int FindLimitingIndex(SpeciesState[] reactants)
        {
            if (reactants == null || reactants.Length == 0) return 0;

            float min = float.MaxValue;
            int idx = 0;

            for (int i = 0; i < reactants.Length; i++)
            {
                var r = reactants[i];
                if (r == null || r.StoichCoeff < MinStoich) continue;
                float e = r.Moles / r.StoichCoeff;
                if (e < min) { min = e; idx = i; }
            }

            return idx;
        }

        /// <summary>
        /// Compute a full stoichiometric budget: max extent, limiting index,
        /// and excess fractions for each reactant.
        /// </summary>
        public static StoichiometricBudget ComputeBudget(SpeciesState[] reactants)
        {
            var budget = new StoichiometricBudget();
            if (reactants == null || reactants.Length == 0)
                return budget;

            budget.LimitingIndex = FindLimitingIndex(reactants);

            var lim = reactants[budget.LimitingIndex];
            float limExtent = (lim != null && lim.StoichCoeff >= MinStoich)
                ? lim.Moles / lim.StoichCoeff
                : 0f;

            budget.MaxExtent      = limExtent;
            budget.LimitingFormula = lim?.Formula ?? string.Empty;
            budget.ExcessFractions = new float[reactants.Length];

            for (int i = 0; i < reactants.Length; i++)
            {
                if (i == budget.LimitingIndex) { budget.ExcessFractions[i] = -1f; continue; }

                var r = reactants[i];
                if (r == null || r.StoichCoeff < MinStoich) continue;

                float extent_i = r.Moles / r.StoichCoeff;
                budget.ExcessFractions[i] = limExtent > 0f
                    ? (extent_i - limExtent) / limExtent
                    : 0f;
            }

            return budget;
        }

        // ═══════════════════════════════════════════════════════
        //  PER-STEP ENFORCEMENT
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Clamp proposed dξ so that no reactant goes below zero.
        ///   dξ ≤ n_i / νi  for all i
        ///
        /// This is the strict per-step stoichiometric enforcement.
        /// </summary>
        public static float ClampExtent(SpeciesState[] reactants, float dExtent)
        {
            if (reactants == null || dExtent <= 0f) return 0f;

            for (int i = 0; i < reactants.Length; i++)
            {
                var r = reactants[i];
                if (r == null || r.StoichCoeff < MinStoich) continue;
                float maxDExtent = r.Moles / r.StoichCoeff;
                if (dExtent > maxDExtent) dExtent = maxDExtent;
            }

            return Mathf.Max(0f, dExtent);
        }

        /// <summary>
        /// Apply dξ to species arrays:
        ///   Reactants:  n_i -= νi × dξ   (enforced non-negative)
        ///   Products:   n_j += νj × dξ
        /// </summary>
        public static void Apply(SpeciesState[] reactants, SpeciesState[] products, float dExtent)
        {
            if (dExtent <= 0f) return;

            if (reactants != null)
                for (int i = 0; i < reactants.Length; i++)
                {
                    var r = reactants[i];
                    if (r == null) continue;
                    r.Moles = Mathf.Max(0f, r.Moles - r.StoichCoeff * dExtent);
                }

            if (products != null)
                for (int i = 0; i < products.Length; i++)
                {
                    var p = products[i];
                    if (p == null) continue;
                    p.Moles += p.StoichCoeff * dExtent;
                }
        }

        // ═══════════════════════════════════════════════════════
        //  SNAPSHOT HELPERS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Compute the fraction of extent consumed: ξ / ξ₀_max.
        /// ξ₀_max = ξ_max at t=0 = min_i(n_i(0) / νi).
        /// </summary>
        public static float CalcProgress(float extent, SpeciesState[] reactants)
        {
            if (reactants == null || reactants.Length == 0) return 0f;

            float xMax0 = 0f;
            for (int i = 0; i < reactants.Length; i++)
            {
                var r = reactants[i];
                if (r == null || r.StoichCoeff < MinStoich) continue;
                float e0 = r.InitialMoles / r.StoichCoeff;
                if (i == 0 || e0 < xMax0) xMax0 = e0;
            }

            return xMax0 > 0f ? Mathf.Clamp01(extent / xMax0) : 0f;
        }
    }
}
