// ChemLabSim v3 — Energy Model
// Pure static math for the thermodynamic feedback loop:
//   1. ΔH → ΔT:  Enthalpy changes temperature over time
//   2. T → rate:  Temperature feeds back into Arrhenius rate
//   3. Gas → P:   Ideal gas law for pressure evolution
//   4. Dissipation: Heat loss to environment
//
// No state, no side effects — called by SimulationStepper each frame.

using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    public static class EnergyModel
    {
        // Universal gas constant
        private const float R_KJ  = 0.008314f;  // kJ·mol⁻¹·K⁻¹
        private const float R_ATM = 0.08206f;    // L·atm·mol⁻¹·K⁻¹

        private const float MinTempK = 1f;
        private const float MaxExponent = 20f;

        // Default heat capacity for aqueous solution (kJ·mol⁻¹·K⁻¹)
        // Approximation: Cp ≈ 4.184 J/(g·K) × 18 g/mol / 1000 ≈ 0.0753 kJ/(mol·K)
        // For a 1L solution (~55.5 mol water): ~4.18 kJ/K
        private const float DefaultHeatCapacityKJPerK = 4.18f;

        // Heat dissipation rate constant (K/s per K difference)
        private const float DefaultDissipationRate = 0.05f;

        // ════════════════════════════════════════════════════════
        //  1. ENTHALPY → TEMPERATURE CHANGE
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Calculate temperature change from enthalpy release/absorption.
        /// ΔT = -(ΔH × molesReacted) / Cp_total
        /// Negative ΔH (exothermic) → positive ΔT (heating).
        /// </summary>
        /// <param name="enthalpyKJ">ΔH in kJ/mol (negative = exothermic).</param>
        /// <param name="molesReactedThisStep">Moles of reaction extent advanced this step.</param>
        /// <param name="heatCapacityKJPerK">Total heat capacity of the system (kJ/K).
        /// If ≤ 0, uses default (aqueous 1L).</param>
        public static float CalcTemperatureStep(
            float enthalpyKJ,
            float molesReactedThisStep,
            float heatCapacityKJPerK = 0f)
        {
            if (Mathf.Approximately(molesReactedThisStep, 0f))
                return 0f;

            float cp = heatCapacityKJPerK > 0f ? heatCapacityKJPerK : DefaultHeatCapacityKJPerK;

            // q = ΔH × n_reacted
            // ΔT = -q / Cp  (negative sign: exothermic releases heat → temp rises)
            float q = enthalpyKJ * molesReactedThisStep;
            return -q / cp;
        }

        /// <summary>
        /// Convert a direct heat exchange value (kJ) to temperature change (°C).
        /// Positive heat raises temperature, negative heat lowers temperature.
        /// </summary>
        public static float CalcTemperatureFromHeat(
            float heatKJ,
            float heatCapacityKJPerK = 0f)
        {
            float cp = heatCapacityKJPerK > 0f ? heatCapacityKJPerK : DefaultHeatCapacityKJPerK;
            if (cp <= 0f) return 0f;
            return heatKJ / cp;
        }

        // ════════════════════════════════════════════════════════
        //  2. TEMPERATURE → RATE (ARRHENIUS FEEDBACK)
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Arrhenius rate multiplier at current temperature relative to reference.
        /// k(T) / k(T_ref) = exp(-Ea/R × (1/T - 1/T_ref))
        /// </summary>
        /// <param name="eaKJ">Effective activation energy (kJ/mol).</param>
        /// <param name="tempK">Current temperature (K).</param>
        /// <param name="refTempK">Reference/activation temperature (K).</param>
        /// <returns>Rate multiplier. 1.0 at T=T_ref, >1 when hotter, <1 when colder.</returns>
        public static float ArrheniusRate(float eaKJ, float tempK, float refTempK)
        {
            tempK = Mathf.Max(tempK, MinTempK);
            refTempK = Mathf.Max(refTempK, MinTempK);

            if (eaKJ <= 0f) return 1f;

            float exponent = -(eaKJ / R_KJ) * (1f / tempK - 1f / refTempK);
            exponent = Mathf.Clamp(exponent, -MaxExponent, MaxExponent);

            return Mathf.Clamp(Mathf.Exp(exponent), 0f, 5f);
        }

        // ════════════════════════════════════════════════════════
        //  3. HEAT DISSIPATION TO ENVIRONMENT
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Newton's law of cooling: dT/dt = -k(T - T_ambient).
        /// Uses explicit Euler form for this frame:
        ///   heatLoss = k(T - T_ambient)
        ///   ΔT = -heatLoss * dt
        /// Returns the temperature change (°C) for this time step.
        /// </summary>
        /// <param name="currentTempC">Current mixture temperature (°C).</param>
        /// <param name="ambientC">Ambient/room temperature (°C).</param>
        /// <param name="dissipationRate">Cooling constant (1/s). Default ~0.05.</param>
        /// <param name="dt">Time step (seconds).</param>
        public static float CalcDissipation(
            float currentTempC,
            float ambientC,
            float dt,
            float dissipationRate = 0f)
        {
            float k = dissipationRate > 0f ? dissipationRate : DefaultDissipationRate;
            float diff = currentTempC - ambientC;

            // Explicit Euler cooling step:
            // heatLoss = k * (T - T_amb)
            // ΔT = -heatLoss * dt
            float heatLoss = k * diff;
            return -heatLoss * dt;
        }

        // ════════════════════════════════════════════════════════
        //  4. GAS PRESSURE (IDEAL GAS LAW)
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Calculate pressure from ideal gas law: P = nRT/V.
        /// Only counts gas-phase species.
        /// </summary>
        /// <param name="gasMoles">Total moles of gas in the system.</param>
        /// <param name="tempK">Temperature in Kelvin.</param>
        /// <param name="volumeL">Volume of gas headspace (L).</param>
        /// <returns>Pressure in atm from gas moles only.</returns>
        public static float CalcPressure(
            float gasMoles,
            float tempK,
            float volumeL)
        {
            if (gasMoles <= 0f || volumeL <= 0f)
                return 0f;

            tempK = Mathf.Max(tempK, MinTempK);

            // P = nRT/V
            return (gasMoles * R_ATM * tempK) / volumeL;
        }

        // ════════════════════════════════════════════════════════
        //  5. EQUILIBRIUM CONSTRAINT
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Calculate the equilibrium-limited extent fraction.
        /// As progress → equilibriumExtent, rate → 0 (reaction settles).
        /// Uses a smooth decay: rateFactor = (1 - progress/eqExtent)^2
        /// </summary>
        /// <param name="currentProgress">Current progress 0–1.</param>
        /// <param name="equilibriumExtent">Equilibrium extent 0–1.</param>
        /// <returns>Rate dampening factor 0–1.</returns>
        public static float EquilibriumDamping(float currentProgress, float equilibriumExtent)
        {
            if (equilibriumExtent <= 0f) return 0f;
            if (equilibriumExtent >= 1f) return 1f; // irreversible

            float ratio = Mathf.Clamp01(currentProgress / equilibriumExtent);

            // Quadratic decay: approaches zero smoothly
            float remaining = 1f - ratio;
            return remaining * remaining;
        }

        // ════════════════════════════════════════════════════════
        //  6. DERIVE ACTIVATION ENERGY
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Derive Ea from activation temperature if not explicitly provided.
        /// Ea ≈ R × T_activation × ln(A), with ln(A) ≈ 20.
        /// </summary>
        public static float DeriveEa(float activationTempC, float explicitEaKJ)
        {
            if (explicitEaKJ > 0f) return explicitEaKJ;
            float activationK = Mathf.Max(activationTempC + 273.15f, MinTempK);
            return R_KJ * activationK * 20f;
        }

        /// <summary>
        /// Apply catalyst Ea reduction.
        /// </summary>
        public static float ApplyCatalyst(float eaKJ, float catalystDeltaC, float activationK)
        {
            if (catalystDeltaC <= 0f) return eaKJ;

            float reduction = R_KJ * activationK * (catalystDeltaC / Mathf.Max(activationK, 1f));
            reduction = Mathf.Max(reduction, catalystDeltaC * 0.15f);
            return Mathf.Max(eaKJ - reduction, 0f);
        }
    }
}
