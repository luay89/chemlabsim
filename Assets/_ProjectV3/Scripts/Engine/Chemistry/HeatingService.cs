// ChemLabSim v3 — Heating Service
// Pure static service (no MonoBehaviour, no Unity update loop) that
// models a single frame of external heat applied to a container.
//
// Physics:
//   Q      = P × Δt              (watts × seconds → joules → /1000 → kJ)
//   ΔT     = Q / (m × Cp)        (kJ / kJ·K⁻¹ → K = °C)
//   Arrhenius rate is recalculated automatically by SimulationStepper
//   the following frame once TemperatureC has been updated.
//
// Boiling / evaporation:
//   Boiling  → T ≥ lowest liquid-phase BoilingPointC (default 100 °C for water)
//   EvapRate → proportional to excess power above the threshold for boiling
//
// Safety:
//   MaxSafeTempC  cap — prevents runaway unless allowExplosion = true
//   Closed containers + gas accumulation are handled by the stepper's
//   pressure model; this service only raises temperature.

using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    /// <summary>
    /// Summary of one frame of external heating.
    /// The result is computed by <see cref="HeatingService.Step"/> but the
    /// caller is responsible for applying <see cref="HeatAppliedKJ"/> to
    /// the container (via <c>SimulationStepper.AddExternalHeat</c> or direct).
    /// </summary>
    public struct HeatingStepResult
    {
        /// <summary>Energy added this frame (kJ). Apply via AddExternalHeat.</summary>
        public float HeatAppliedKJ;
        /// <summary>Corresponding temperature rise (°C). Read-only information.</summary>
        public float DeltaTempC;
        /// <summary>True when projected temperature meets or exceeds the boiling point.</summary>
        public bool IsBoiling;
        /// <summary>Estimated evaporation rate (mol/s) from boiling/near-boiling.</summary>
        public float EvaporationRateMolPerSec;
        /// <summary>True when the safety temperature cap prevented full heating this frame.</summary>
        public bool HitSafetyCap;
        /// <summary>Lowest liquid-phase boiling point found among species (°C).</summary>
        public float BoilingPointC;
    }

    /// <summary>
    /// Stateless heating-physics service. Call <see cref="Step"/> every frame from
    /// <c>HeatingController.Update()</c>.
    /// </summary>
    public static class HeatingService
    {
        // ── Constants ────────────────────────────────────────────────────────
        /// <summary>Default upper temperature bound before safety cap triggers (°C).</summary>
        public const float DefaultMaxSafeTempC = 500f;

        /// <summary>Default water boiling point used when no species data is available.</summary>
        public const float DefaultBoilingPointC = 100f;

        /// <summary>Base evaporation coefficient: mol/s per watt of excess power.</summary>
        private const float EvapCoeffMolPerSecPerW = 2e-4f;

        // ════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Compute one frame of external heating.
        ///
        ///   Q  = powerWatts × dt / 1000  (kJ)
        ///   ΔT = Q / HeatCapacityKJPerK
        ///
        /// The result is NOT applied to the state — the caller must call
        /// <c>SimulationStepper.AddExternalHeat(result.HeatAppliedKJ)</c>.
        /// </summary>
        /// <param name="state">Live reaction state of the container being heated.</param>
        /// <param name="powerWatts">Burner power (W). Ignored if ≤ 0.</param>
        /// <param name="dt">Frame delta time (s). Clamped to [0, 0.1].</param>
        /// <param name="maxSafeTempC">Hard temperature ceiling (°C).
        ///   Heat is reduced so the temperature never exceeds this value
        ///   unless <paramref name="allowExplosion"/> is true.</param>
        /// <param name="allowExplosion">When false (default) the safety cap is enforced.
        ///   When true, full heat is applied even past the cap.</param>
        public static HeatingStepResult Step(
            ReactionState state,
            float powerWatts,
            float dt,
            float maxSafeTempC = DefaultMaxSafeTempC,
            bool allowExplosion = false)
        {
            if (state == null)
                return default;

            dt = Mathf.Clamp(dt, 0f, 0.1f);

            if (dt <= 0f || powerWatts <= 0f)
                return new HeatingStepResult
                {
                    BoilingPointC = FindLowestLiquidBoilingPointC(state)
                };

            // ── Q = P × Δt (J → kJ) ─────────────────────────────────────
            float heatKJ = powerWatts * dt / 1000f;

            // ── ΔT = Q / Cp ──────────────────────────────────────────────
            float cp   = Mathf.Max(state.HeatCapacityKJPerK, 1e-4f);
            float rawDT = heatKJ / cp;

            // ── Safety cap ───────────────────────────────────────────────
            bool hitCap = false;
            float cappedDT = rawDT;
            if (!allowExplosion)
            {
                float headroom = maxSafeTempC - state.TemperatureC;
                if (headroom <= 0f)
                {
                    // Already at or above cap — apply no heat
                    cappedDT = 0f;
                    heatKJ   = 0f;
                    hitCap   = true;
                }
                else if (rawDT > headroom)
                {
                    // Clip so the temperature lands exactly on the cap
                    cappedDT = headroom;
                    heatKJ   = cappedDT * cp;
                    hitCap   = true;
                }
            }

            // ── Boiling check ────────────────────────────────────────────
            float boilingPt     = FindLowestLiquidBoilingPointC(state);
            float projectedTempC = state.TemperatureC + cappedDT;
            bool isBoiling       = projectedTempC >= boilingPt;

            // ── Evaporation rate ─────────────────────────────────────────
            // Below boiling: slow surface evaporation proportional to warmth.
            // At/above boiling: excess energy drives phase-change evaporation.
            float evapRate = 0f;
            if (isBoiling)
            {
                // Energy used to overcome latent heat of vaporisation
                // (excess above threshold ΔT → boiling point)
                float thresholdDT = Mathf.Max(0f, boilingPt - state.TemperatureC);
                float excessDT    = Mathf.Max(0f, cappedDT - thresholdDT);
                float excessKJ    = excessDT * cp;
                // Latent heat of water ≈ 40.7 kJ/mol — simplified here as scaling factor
                evapRate = excessKJ / 0.04107f;  // mol/s at this dt — properly scaled below
                evapRate = evapRate / Mathf.Max(dt, 1e-4f);
                evapRate = Mathf.Min(evapRate, 5f); // cap at 5 mol/s for open containers
            }
            else
            {
                float warmthFactor = boilingPt > 0f
                    ? Mathf.Clamp01(projectedTempC / boilingPt)
                    : 0f;
                evapRate = powerWatts * EvapCoeffMolPerSecPerW * warmthFactor;
            }

            return new HeatingStepResult
            {
                HeatAppliedKJ           = heatKJ,
                DeltaTempC              = cappedDT,
                IsBoiling               = isBoiling,
                EvaporationRateMolPerSec = evapRate,
                HitSafetyCap            = hitCap,
                BoilingPointC           = boilingPt
            };
        }

        /// <summary>
        /// True when the mixture temperature meets or exceeds the lowest
        /// liquid-phase boiling point of any species in the state.
        /// </summary>
        public static bool IsBoiling(ReactionState state)
        {
            if (state == null) return false;
            return state.TemperatureC >= FindLowestLiquidBoilingPointC(state);
        }

        /// <summary>
        /// Returns the lowest finite boiling point (°C) among all liquid-phase species
        /// currently in <paramref name="state"/>. Falls back to water (100 °C) if none found.
        /// </summary>
        public static float FindLowestLiquidBoilingPointC(ReactionState state)
        {
            float lowest = float.MaxValue;

            if (state.Reactants != null)
                for (int i = 0; i < state.Reactants.Length; i++)
                {
                    var sp = state.Reactants[i];
                    if (sp != null && sp.Phase == Phase.Liquid
                        && !float.IsNaN(sp.BoilingPointC)
                        && sp.Moles > 0f)
                        lowest = Mathf.Min(lowest, sp.BoilingPointC);
                }

            if (state.Products != null)
                for (int i = 0; i < state.Products.Length; i++)
                {
                    var sp = state.Products[i];
                    if (sp != null && sp.Phase == Phase.Liquid
                        && !float.IsNaN(sp.BoilingPointC)
                        && sp.Moles > 0f)
                        lowest = Mathf.Min(lowest, sp.BoilingPointC);
                }

            return lowest < float.MaxValue ? lowest : DefaultBoilingPointC;
        }
    }
}
