// ChemLabSim v3 — Equilibrium Solver
// Handles reversible reactions using a simplified Le Chatelier model.
// For irreversible reactions (Keq not specified), returns full forward extent.
//
// Model:
//   Keq = [products]^stoich / [reactants]^stoich  (at standard conditions)
//   Temperature shifts Keq via van't Hoff: ln(K2/K1) = -ΔH/R × (1/T2 - 1/T1)
//   Pressure shifts Keq for gas-phase reactions (Δn_gas > 0 → high P favors reactants)
//
// Educational simplification:
//   We compute an equilibrium_extent (0–1) that represents how far the forward
//   reaction proceeds before equilibrium is established. This maps cleanly to
//   the existing rate/completion system.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    public struct EquilibriumResult
    {
        /// <summary>Fraction of reaction proceeding forward at equilibrium (0–1).</summary>
        public float EquilibriumExtent;

        /// <summary>Effective Keq at the given temperature and pressure.</summary>
        public float Keq;

        /// <summary>True if the reaction is reversible (has a finite Keq).</summary>
        public bool IsReversible;

        /// <summary>Direction shift from Le Chatelier: "forward", "backward", "none".</summary>
        public string ShiftDirection;

        /// <summary>Human-readable equilibrium description.</summary>
        public string Summary;
    }

    public static class EquilibriumSolver
    {
        // Reference temperature for Keq values (25°C = 298.15 K)
        private const float TRefK = 298.15f;
        private const float R_KJ = 0.008314f;
        private const float MinTempK = 1f;
        private const float MaxKeq = 1e10f;
        private const float MinKeq = 1e-10f;

        /// <summary>
        /// Solve equilibrium for a reaction.
        /// </summary>
        /// <param name="keqRef">Equilibrium constant at 25°C. If ≤ 0, reaction is irreversible.</param>
        /// <param name="enthalpyKJ">ΔH in kJ/mol (negative = exothermic).</param>
        /// <param name="tempC">Actual lab temperature (°C).</param>
        /// <param name="pressureAtm">Lab pressure in atm. Default 1.0.</param>
        /// <param name="deltaGasMoles">Change in gas moles (product gas moles - reactant gas moles).
        /// Positive = more gas produced. Used for pressure effect.</param>
        public static EquilibriumResult Solve(
            float keqRef,
            float enthalpyKJ,
            float tempC,
            float pressureAtm = 1f,
            float deltaGasMoles = 0f)
        {
            // Irreversible: no equilibrium constraint
            if (keqRef <= 0f)
            {
                return new EquilibriumResult
                {
                    EquilibriumExtent = 1f,
                    Keq               = float.PositiveInfinity,
                    IsReversible      = false,
                    ShiftDirection    = "forward",
                    Summary           = "Reaction is irreversible \u2014 proceeds to completion."
                };
            }

            float tempK = Mathf.Max(tempC + 273.15f, MinTempK);

            // Van't Hoff: adjust Keq for temperature
            //   ln(K2/K1) = -(ΔH/R) × (1/T2 - 1/T1)
            float vhExponent = -(enthalpyKJ / R_KJ) * (1f / tempK - 1f / TRefK);
            vhExponent = Mathf.Clamp(vhExponent, -20f, 20f);
            float keqT = keqRef * Mathf.Exp(vhExponent);

            // Pressure effect (Le Chatelier):
            //   If Δn_gas > 0, increasing pressure shifts equilibrium backward
            //   Kp = Keq × (P)^(-Δn)  simplified
            if (Mathf.Abs(deltaGasMoles) > 0.001f && pressureAtm > 0f)
            {
                float pressureFactor = Mathf.Pow(pressureAtm, -deltaGasMoles);
                keqT *= pressureFactor;
            }

            keqT = Mathf.Clamp(keqT, MinKeq, MaxKeq);

            // Convert Keq to equilibrium extent (simplified)
            //   For a reaction A → B with Keq = [B]/[A]:
            //     extent = Keq / (1 + Keq)
            //   This gives: Keq=1 → 50%, Keq=100 → ~99%, Keq=0.01 → ~1%
            float extent = keqT / (1f + keqT);
            extent = Mathf.Clamp01(extent);

            // Determine shift direction
            string shift = DetermineShift(keqRef, keqT);

            string summary = BuildSummary(keqT, extent, shift, enthalpyKJ, tempC, pressureAtm);

            return new EquilibriumResult
            {
                EquilibriumExtent = extent,
                Keq               = keqT,
                IsReversible      = true,
                ShiftDirection    = shift,
                Summary           = summary
            };
        }

        /// <summary>
        /// Calculate Δn_gas from a reaction's products and reactants.
        /// Only counts species with state "g".
        /// </summary>
        public static float CalcDeltaGasMoles(ReactionEntry reaction)
        {
            float productGas  = 0f;
            float reactantGas = 0f;

            if (reaction.products != null)
            {
                for (int i = 0; i < reaction.products.Count; i++)
                {
                    var p = reaction.products[i];
                    if (p != null && IsGas(p.state))
                        productGas += p.stoich;
                }
            }

            if (reaction.reactants != null)
            {
                for (int i = 0; i < reaction.reactants.Count; i++)
                {
                    var r = reaction.reactants[i];
                    if (r != null && IsGas(r.state))
                        reactantGas += r.stoich;
                }
            }

            return productGas - reactantGas;
        }

        private static bool IsGas(string state)
        {
            if (string.IsNullOrWhiteSpace(state)) return false;
            string s = state.Trim().ToLowerInvariant();
            return s == "g" || s == "gas";
        }

        private static string DetermineShift(float keqRef, float keqActual)
        {
            float ratio = keqActual / Mathf.Max(keqRef, MinKeq);
            if (ratio > 1.05f) return "forward";
            if (ratio < 0.95f) return "backward";
            return "none";
        }

        private static string BuildSummary(float keq, float extent, string shift,
            float dH, float tempC, float pressure)
        {
            string extentStr = $"{extent * 100f:0.#}%";

            string shiftDesc;
            if (shift == "forward")
                shiftDesc = "Conditions favor the forward reaction.";
            else if (shift == "backward")
                shiftDesc = "Conditions shift equilibrium toward reactants.";
            else
                shiftDesc = "Equilibrium is near standard conditions.";

            return $"Reversible reaction at {extentStr} completion (Keq = {keq:0.###}). {shiftDesc}";
        }
    }
}
