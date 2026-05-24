// ChemLabSim v3 — Thermodynamics Module
// Provides Arrhenius rate approximation and enthalpy (ΔH) calculations.
// All functions are pure/static — no state, no side effects.
//
// Arrhenius: k ∝ A·exp(-Ea/RT)
//   We use a simplified model suitable for educational simulation:
//   rate_factor = exp(-Ea_scaled * (1/T - 1/T_activation))
//
// ΔH is taken from the reaction JSON field (enthalpyKJPerMol).
// Positive = endothermic, Negative = exothermic (chemistry convention).

using System;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    /// <summary>Result of thermodynamic calculations for a single reaction.</summary>
    public struct ThermoResult
    {
        /// <summary>Arrhenius-derived rate multiplier (0–1+). Values near 0 mean
        /// the reaction is kinetically frozen; values ≥1 mean full speed.</summary>
        public float RateMultiplier;

        /// <summary>Enthalpy change in kJ/mol. Negative = exothermic.</summary>
        public float EnthalpyKJ;

        /// <summary>True if the reaction releases heat (ΔH &lt; 0).</summary>
        public bool IsExothermic;

        /// <summary>Effective activation energy in kJ/mol after catalyst adjustment.</summary>
        public float EffectiveEaKJ;

        /// <summary>Temperature in Kelvin used for the calculation.</summary>
        public float TemperatureK;

        /// <summary>Human-readable summary for the UI.</summary>
        public string Summary;
    }

    public static class Thermodynamics
    {
        // Universal gas constant (kJ·mol⁻¹·K⁻¹)
        private const float R_KJ = 0.008314f;

        // Default activation energy when not specified in JSON (kJ/mol)
        private const float DefaultEaKJ = 50f;

        // Clamp to prevent numerical explosion
        private const float MaxExponent = 20f;
        private const float MinTempK = 1f;

        /// <summary>
        /// Calculate Arrhenius rate multiplier and enthalpy for a reaction.
        /// </summary>
        /// <param name="activationTempC">Activation temperature from JSON (°C).
        /// Converted to Ea via the relation Ea ≈ R·T_activation·ln(A), or used
        /// directly as the reference temperature for the simplified model.</param>
        /// <param name="actualTempC">Actual lab temperature (°C).</param>
        /// <param name="catalystApplied">Whether a catalyst is active.</param>
        /// <param name="catalystDeltaC">Temperature-equivalent catalyst effect (°C).</param>
        /// <param name="enthalpyKJ">Enthalpy of reaction from JSON (kJ/mol).
        /// Negative = exothermic. Falls back to temperature_delta if unset.</param>
        /// <param name="eaKJ">Optional explicit activation energy (kJ/mol).
        /// If ≤ 0, a default is derived from activationTempC.</param>
        public static ThermoResult Calculate(
            float activationTempC,
            float actualTempC,
            bool catalystApplied,
            float catalystDeltaC,
            float enthalpyKJ,
            float eaKJ = 0f)
        {
            float tempK = Mathf.Max(actualTempC + 273.15f, MinTempK);
            float activationK = Mathf.Max(activationTempC + 273.15f, MinTempK);

            // Resolve activation energy
            float ea = eaKJ > 0f ? eaKJ : DeriveEa(activationK);

            // Apply catalyst: lowers Ea
            if (catalystApplied && catalystDeltaC > 0f)
            {
                // Convert delta-C to approximate Ea reduction
                // Using: ΔEa ≈ R · T_activation · (catalystDelta / T_activation)
                float eaReduction = R_KJ * activationK * (catalystDeltaC / Mathf.Max(activationK, 1f));
                // Simplified: just use a fraction proportional to delta
                eaReduction = Mathf.Max(eaReduction, catalystDeltaC * 0.15f);
                ea = Mathf.Max(ea - eaReduction, 0f);
            }

            // Arrhenius: rate ∝ exp(-Ea/R · (1/T - 1/T_ref))
            float invT    = 1f / tempK;
            float invTRef = 1f / activationK;
            float exponent = -(ea / R_KJ) * (invT - invTRef);
            exponent = Mathf.Clamp(exponent, -MaxExponent, MaxExponent);

            float rateMultiplier = Mathf.Exp(exponent);

            // At T ≥ T_activation, rateMultiplier ≥ 1.0 (exponent ≥ 0)
            // At T < T_activation, rateMultiplier < 1.0 (exponential decay)
            // Clamp upper end for educational clarity
            rateMultiplier = Mathf.Clamp(rateMultiplier, 0f, 2f);

            // Enthalpy
            bool exothermic = enthalpyKJ < 0f;

            string summary = BuildSummary(rateMultiplier, enthalpyKJ, exothermic, ea, catalystApplied);

            return new ThermoResult
            {
                RateMultiplier = rateMultiplier,
                EnthalpyKJ     = enthalpyKJ,
                IsExothermic   = exothermic,
                EffectiveEaKJ  = ea,
                TemperatureK   = tempK,
                Summary        = summary
            };
        }

        /// <summary>
        /// Derive an approximate activation energy from a reference temperature.
        /// Uses a simple linear mapping: Ea ≈ scaling_factor × T_activation.
        /// This gives educationally reasonable values:
        ///   25°C → ~50 kJ/mol,  500°C → ~130 kJ/mol
        /// </summary>
        private static float DeriveEa(float activationK)
        {
            // Ea ≈ R × T × ln(A), with ln(A) ≈ 20 for typical reactions
            return R_KJ * activationK * 20f;
        }

        private static string BuildSummary(float rate, float dH, bool exo, float ea, bool cat)
        {
            string rateDesc;
            if (rate >= 1.5f)      rateDesc = "very fast";
            else if (rate >= 1.0f) rateDesc = "fast";
            else if (rate >= 0.5f) rateDesc = "moderate";
            else if (rate >= 0.1f) rateDesc = "slow";
            else                   rateDesc = "negligible";

            string energyDesc = exo
                ? $"exothermic (\u0394H = {dH:0.#} kJ/mol, releases heat)"
                : $"endothermic (\u0394H = {dH:0.#} kJ/mol, absorbs heat)";

            if (Mathf.Approximately(dH, 0f))
                energyDesc = "thermally neutral (\u0394H \u2248 0)";

            string catNote = cat ? " Catalyst lowers the effective activation energy." : "";

            return $"Reaction rate is {rateDesc} (Ea = {ea:0.#} kJ/mol). Energy change is {energyDesc}.{catNote}";
        }
    }
}
