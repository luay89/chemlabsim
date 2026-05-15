// ChemLabSim v3 — Rate Law (Law of Mass Action)
// Computes concentration-dependent reaction rate factors.
//
// Rate = k(T) × [A]^orderA × [B]^orderB × ...
//
// k(T) is the Arrhenius temperature factor (from EnergyModel).
// Orders default to stoichiometric coefficients (valid for elementary reactions).
//
// The returned factor is NORMALIZED (0–1):
//   At t=0 (full initial concentrations) → 1.0
//   As reactants are consumed → approaches 0.0
//   This integrates cleanly with the existing rate pipeline.

using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    public static class RateLaw
    {
        private const float MinConcentration = 1e-9f;

        // ═══════════════════════════════════════════════════════
        //  PRIMARY API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Calculate the normalized concentration factor for the forward reaction.
        /// Factor = Π( [X_i] / [X_i,0] )^order_i   (each reactant's depletion)
        /// Starts at 1.0, falls toward 0 as reactants are consumed.
        /// </summary>
        /// <param name="reactants">Live reactant species (moles + initial moles).</param>
        /// <param name="volumeL">Current reaction volume (L).</param>
        /// <param name="reaction">Reaction entry (for explicit reaction orders if set).</param>
        public static float CalcConcentrationFactor(
            SpeciesState[] reactants,
            float volumeL,
            ReactionEntry reaction = null)
        {
            if (reactants == null || reactants.Length == 0) return 1f;
            if (volumeL <= 0f) return 0f;

            float factor = 1f;

            for (int i = 0; i < reactants.Length; i++)
            {
                var r = reactants[i];
                if (r == null) continue;

                float order = GetOrder(reaction, i, r.StoichCoeff);
                if (order <= 0f) continue;

                float concNow = Mathf.Max(r.Moles, 0f) / volumeL;
                float concInit = Mathf.Max(r.InitialMoles, MinConcentration) / volumeL;

                // Normalized concentration ratio (0–1)
                float ratio = Mathf.Clamp01(concNow / Mathf.Max(concInit, MinConcentration));
                factor *= Mathf.Pow(ratio, order);
            }

            return Mathf.Clamp01(factor);
        }

        /// <summary>
        /// Calculate the absolute forward rate (mol/L/s) using law of mass action.
        /// rate = k × Π([X_i])^order_i
        /// Returns absolute rate for use in kinetic equilibrium calculations.
        /// </summary>
        public static float CalcAbsoluteForwardRate(
            SpeciesState[] reactants,
            float volumeL,
            float arrheniusK,
            ReactionEntry reaction = null)
        {
            if (reactants == null || reactants.Length == 0) return arrheniusK;
            if (volumeL <= 0f) return 0f;

            float product = 1f;

            for (int i = 0; i < reactants.Length; i++)
            {
                var r = reactants[i];
                if (r == null) continue;

                float order = GetOrder(reaction, i, r.StoichCoeff);
                if (order <= 0f) continue;

                float conc = Mathf.Max(r.Moles, 0f) / volumeL;
                product *= Mathf.Pow(Mathf.Max(conc, MinConcentration), order);
            }

            return arrheniusK * product;
        }

        /// <summary>
        /// Calculate the absolute reverse rate (mol/L/s) using law of mass action.
        /// k_r = k_f / Keq (from thermodynamics)
        /// rate_r = (k_f / Keq) × Π([product_j])^order_j
        /// </summary>
        public static float CalcAbsoluteReverseRate(
            SpeciesState[] products,
            float volumeL,
            float arrheniusK,
            float keq,
            ReactionEntry reaction = null)
        {
            if (products == null || products.Length == 0) return 0f;
            if (volumeL <= 0f) return 0f;
            if (keq <= 0f) return 0f;

            float kr = arrheniusK / Mathf.Max(keq, 1e-12f);
            float product = 1f;

            for (int i = 0; i < products.Length; i++)
            {
                var p = products[i];
                if (p == null) continue;

                // Product order = stoich (standard assumption)
                float order = p.StoichCoeff > 0f ? p.StoichCoeff : 1f;
                float conc = Mathf.Max(p.Moles, 0f) / volumeL;
                product *= Mathf.Pow(Mathf.Max(conc, MinConcentration), order);
            }

            return kr * product;
        }

        // ═══════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Get reaction order for reactant at index i.
        /// Uses explicit reactantOrders if defined, otherwise falls back to stoich coefficient.
        /// </summary>
        public static float GetOrder(ReactionEntry reaction, int reactantIndex, float stoichDefault)
        {
            if (reaction?.reactantOrders != null &&
                reactantIndex < reaction.reactantOrders.Count)
            {
                float explicitOrder = reaction.reactantOrders[reactantIndex];
                if (explicitOrder >= 0f) return explicitOrder;
            }

            return stoichDefault > 0f ? stoichDefault : 1f;
        }

        /// <summary>
        /// Compute the reaction quotient Q = Π[products]^p / Π[reactants]^r.
        /// Q < Keq → reaction proceeds forward.
        /// Q > Keq → reaction proceeds in reverse.
        /// </summary>
        public static float CalcReactionQuotient(
            SpeciesState[] reactants,
            SpeciesState[] products,
            float volumeL,
            ReactionEntry reaction = null)
        {
            if (volumeL <= 0f) return 0f;

            float numerator = 1f;
            float denominator = 1f;

            if (products != null)
            {
                for (int i = 0; i < products.Length; i++)
                {
                    var p = products[i];
                    if (p == null) continue;
                    float order = p.StoichCoeff > 0f ? p.StoichCoeff : 1f;
                    float conc = Mathf.Max(p.Moles, MinConcentration) / volumeL;
                    numerator *= Mathf.Pow(conc, order);
                }
            }

            if (reactants != null)
            {
                for (int i = 0; i < reactants.Length; i++)
                {
                    var r = reactants[i];
                    if (r == null) continue;
                    float order = GetOrder(reaction, i, r.StoichCoeff);
                    if (order <= 0f) continue;
                    float conc = Mathf.Max(r.Moles, MinConcentration) / volumeL;
                    denominator *= Mathf.Pow(conc, order);
                }
            }

            if (denominator <= 0f) return float.MaxValue;
            return numerator / denominator;
        }

        /// <summary>
        /// Check whether the reaction is at equilibrium (Q ≈ Keq).
        /// </summary>
        public static bool IsAtEquilibrium(float q, float keq, float tolerance = 0.02f)
        {
            if (keq <= 0f) return false;
            return Mathf.Abs(q - keq) / Mathf.Max(keq, 1e-6f) < tolerance;
        }
    }
}
