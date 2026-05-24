// ChemLabSim v3 — Dynamic Equilibrium Engine
// True kinetic equilibrium: forward vs reverse rates.
//
// Classical kinetics model:
//   rate_f = kf × [A]^a × [B]^b  (law of mass action, forward)
//   rate_r = kr × [C]^c × [D]^d  (law of mass action, reverse)
//   net_rate = rate_f - rate_r
//
// Thermodynamic constraint: kf / kr = Keq
//   So kr = kf / Keq
//
// Advantages over simple extent damping:
//   1. Reaction can actually reverse when products accumulate
//   2. Equilibrium is emergent — system finds the exact Keq ratio
//   3. Le Chatelier shifts fall out naturally (add product → reverse spikes)
//   4. Temperature changes Keq (van't Hoff) AND k (Arrhenius) simultaneously

using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    public struct KineticRates
    {
        /// <summary>Forward reaction rate (absolute, mol/L/s scaled).</summary>
        public float Forward;

        /// <summary>Reverse reaction rate (absolute, mol/L/s scaled).</summary>
        public float Reverse;

        /// <summary>Net rate = Forward - Reverse. Negative means reverse-dominant.</summary>
        public float Net;

        /// <summary>Normalized net factor (0-1) for use in extent calculation.
        /// Positive → forward, negative → reverse, clamped to [-1, 1].</summary>
        public float NormalizedNet;

        /// <summary>Current reaction quotient Q = [products]/[reactants].</summary>
        public float ReactionQuotient;

        /// <summary>Whether Q ≈ Keq (within tolerance). True = equilibrium reached.</summary>
        public bool AtEquilibrium;
    }

    public static class DynamicEquilibrium
    {
        /// <summary>Rate below which reverse reaction is negligible for irreversible reactions.</summary>
        private const float ReverseThresholdIrreversible = 0f;

        /// <summary>Q/Keq ratio tolerance for declaring equilibrium.</summary>
        private const float EquilibriumTolerance = 0.02f;

        /// <summary>Maximum absolute rate value for normalization.</summary>
        private const float MaxAbsoluteRate = 100f;

        // ═══════════════════════════════════════════════════════
        //  PRIMARY API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Calculate forward and reverse kinetic rates for a reaction at its current state.
        /// Integrates law of mass action with Arrhenius temperature correction and Keq.
        /// </summary>
        /// <param name="state">Live reaction state (moles, temperature, volume).</param>
        /// <param name="arrheniusK">Arrhenius rate multiplier at current temperature.</param>
        /// <returns>Full rate breakdown including net and equilibrium status.</returns>
        public static KineticRates CalcRates(ReactionState state, float arrheniusK)
        {
            var result = new KineticRates();
            var reaction = state.Reaction;

            if (reaction == null || state.Reactants == null)
                return result;

            float volumeL = Mathf.Max(state.VolumeLiters, 0.001f);

            // Forward rate: k_f × Π[reactants]^order
            result.Forward = RateLaw.CalcAbsoluteForwardRate(
                state.Reactants, volumeL, arrheniusK, reaction);

            // Reverse rate: only for reversible reactions
            float keq = Mathf.Max(state.CurrentKeq, 0f);
            if (reaction.isReversible && keq > 0f && state.Products != null)
            {
                result.Reverse = RateLaw.CalcAbsoluteReverseRate(
                    state.Products, volumeL, arrheniusK, keq, reaction);
            }
            else
            {
                result.Reverse = ReverseThresholdIrreversible;
            }

            // Net rate
            result.Net = result.Forward - result.Reverse;

            // Reaction quotient Q
            result.ReactionQuotient = RateLaw.CalcReactionQuotient(
                state.Reactants, state.Products, volumeL, reaction);

            // Check equilibrium: Q ≈ Keq
            result.AtEquilibrium = reaction.isReversible &&
                keq > 0f &&
                RateLaw.IsAtEquilibrium(result.ReactionQuotient, keq, EquilibriumTolerance);

            // Normalize to 0-1 range for extent calculation
            // Reference = initial forward rate (arrheniusK × initial concentration product)
            float refRate = CalcInitialForwardRate(state, arrheniusK);
            if (refRate > 0f)
            {
                result.NormalizedNet = Mathf.Clamp(result.Net / refRate, -1f, 1f);
            }
            else
            {
                result.NormalizedNet = Mathf.Clamp01(result.Net / Mathf.Max(MaxAbsoluteRate, 0.001f));
            }

            return result;
        }

        /// <summary>
        /// Normalized kinetic rate factor based purely on concentrations and equilibrium (−1 to 1).
        /// Arrhenius temperature factor is applied OUTSIDE this method in SimulationStepper.
        ///
        /// Irreversible: Π([X]/[X0])^order  (1→0 as reactants deplete)
        /// Reversible:   (forward_conc - reverse_conc/Keq) / forward_conc_initial  (→0 at equilibrium)
        /// </summary>
        public static float CalcNetFactor(ReactionState state)
        {
            if (state?.Reaction == null) return 0f;

            float volumeL = Mathf.Max(state.VolumeLiters, 0.001f);

            // Forward concentration term (Arrhenius factor = 1 for normalization)
            float fwdAbs = RateLaw.CalcAbsoluteForwardRate(
                state.Reactants, volumeL, 1f, state.Reaction);
            float fwdRef = CalcInitialForwardRate(state, 1f);

            if (fwdRef <= 0f) return 0f;

            if (!state.Reaction.isReversible)
                return Mathf.Clamp01(fwdAbs / fwdRef);

            // Reverse concentration term: k_r × Π[products]^order = (k_f/Keq) × Π[products]
            float keq = Mathf.Max(state.CurrentKeq, 1e-12f);
            float revAbs = RateLaw.CalcAbsoluteReverseRate(
                state.Products, volumeL, 1f, keq, state.Reaction);

            // Net normalized: positive = forward dominant, negative = reverse dominant
            float net = fwdAbs - revAbs;
            return Mathf.Clamp(net / fwdRef, -1f, 1f);
        }

        /// <summary>
        /// Calculate the equilibrium concentrations using ICE table approach.
        /// Useful for validation and display.
        /// [C]_eq = x × stoich_C, where x satisfies Keq = [products]^p / [reactants]^r
        /// For A ⇌ B (1:1): x = [A0] × Keq / (1 + Keq)
        /// </summary>
        public static float CalcEquilibriumExtentFromICE(
            float initialMoles,
            float volumeL,
            float keq,
            float reactantStoich = 1f,
            float productStoich = 1f)
        {
            if (keq <= 0f || volumeL <= 0f) return 1f;   // irreversible

            float c0 = initialMoles / volumeL;

            // Simplified 1:1 ICE: [product] = c0 × Keq/(1+Keq)
            // For non-1:1, this is an approximation (full solver would require numerical root finding)
            float fraction = keq / (1f + keq);
            return Mathf.Clamp01(fraction);
        }

        // ═══════════════════════════════════════════════════════
        //  INTERNAL HELPERS
        // ═══════════════════════════════════════════════════════

        private static float CalcInitialForwardRate(ReactionState state, float arrheniusK)
        {
            if (state.Reactants == null) return arrheniusK;

            float product = 1f;
            float volumeL = Mathf.Max(state.VolumeLiters, 0.001f);

            for (int i = 0; i < state.Reactants.Length; i++)
            {
                var r = state.Reactants[i];
                if (r == null) continue;

                float order = RateLaw.GetOrder(state.Reaction, i, r.StoichCoeff);
                if (order <= 0f) continue;

                float initConc = Mathf.Max(r.InitialMoles, 1e-9f) / volumeL;
                product *= Mathf.Pow(initConc, order);
            }

            return arrheniusK * product;
        }
    }
}
