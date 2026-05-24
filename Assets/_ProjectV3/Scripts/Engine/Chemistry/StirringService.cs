// ChemLabSim v3 — Stirring Service
// Pure static service (no MonoBehaviour) modelling the physical effects
// of stirring / mixing on a ReactionState.
//
// Scientific contract:
//   Stirring ONLY increases the rate at which equilibrium is approached.
//   It does NOT shift the equilibrium position.
//   Mechanism: better phase contact (higher contact factor) and faster
//   dissolution of solids — both scale k_f and k_r equally, so Keq is unchanged.
//
// Model:
//   rateMultiplier      = 1 + K_STIR × stirIntensity
//   dissolutionMult     = 1 + K_DISSOLVE × stirIntensity
//   grindingFactor      = stirIntensity  (maps directly onto PhaseInteractionModel.GetContactFactor)
//
// Visual outputs:
//   vortexIntensity  = stirIntensity   (drives shader vortex / spin FX)
//   wobbleAmplitude  = 0.04 × stirIntensity   (drives liquid wobble)

using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    /// <summary>Result of one stirring evaluation (stateless, computed every frame).</summary>
    public struct StirringStepResult
    {
        /// <summary>
        /// Rate multiplier to apply to k_f (and k_r equally, preserving Keq).
        /// Value ≥ 1.  Applied via SimulationStepper.SetGrindingFactor().
        /// </summary>
        public float RateMultiplier;

        /// <summary>
        /// Dissolution rate multiplier for solid-phase species.
        /// 1 = base, >1 = faster dissolution.
        /// </summary>
        public float DissolutionMultiplier;

        /// <summary>
        /// Direct grinding factor [0–1] for PhaseInteractionModel.GetContactFactor.
        /// Equals stirIntensity.
        /// </summary>
        public float GrindingFactor;

        /// <summary>Vortex visual intensity [0–1] for the shader / particle system.</summary>
        public float VortexIntensity;

        /// <summary>Liquid surface wobble amplitude [0, 0.12] driven by stir speed.</summary>
        public float WobbleAmplitude;
    }

    /// <summary>
    /// Stateless stirring-physics service.
    /// Evaluate this every frame from StirringController.Update().
    /// </summary>
    public static class StirringService
    {
        // ── Tuning constants ─────────────────────────────────────────────
        /// <summary>
        /// Stirring rate gain (k_stir).
        /// rateMultiplier = 1 + K_STIR × intensity
        /// At full intensity → ×1.8 reaction rate.
        /// </summary>
        private const float K_STIR = 0.8f;

        /// <summary>
        /// Dissolution rate gain (k_dissolve).
        /// dissolutionMult = 1 + K_DISSOLVE × intensity
        /// At full intensity → 1.5× dissolution.
        /// </summary>
        private const float K_DISSOLVE = 0.5f;

        /// <summary>Maximum wobble amplitude pushed to the shader [0..1 UV range].</summary>
        private const float MaxWobbleAmplitude = 0.12f;

        // ════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Compute the stirring effect for one frame.
        /// The returned result is NOT applied automatically — the caller
        /// (StirringController) passes <see cref="StirringStepResult.GrindingFactor"/>
        /// to <c>SimulationStepper.SetGrindingFactor()</c>.
        /// </summary>
        /// <param name="stirIntensity">Stir intensity 0–1 (0 = off, 1 = max).</param>
        public static StirringStepResult Evaluate(float stirIntensity)
        {
            float i = Mathf.Clamp01(stirIntensity);

            return new StirringStepResult
            {
                RateMultiplier      = 1f + K_STIR    * i,
                DissolutionMultiplier = 1f + K_DISSOLVE * i,
                GrindingFactor      = i,
                VortexIntensity     = i,
                WobbleAmplitude     = MaxWobbleAmplitude * i
            };
        }

        /// <summary>
        /// True when the mix has reached or exceeded the stirred equilibrium —
        /// i.e. stirring is no longer accelerating an already-complete reaction.
        /// (Informational only; does NOT stop the stepper.)
        /// </summary>
        public static bool IsReactionSaturated(ReactionState state)
        {
            return state != null && state.IsAtEquilibrium;
        }
    }
}
