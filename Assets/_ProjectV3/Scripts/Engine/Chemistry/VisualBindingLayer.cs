// ChemLabSim v3 — Visual Binding Layer
// Chemistry-aware visual resolution that maps chemical properties to visual effects.
// Extends the base VisualDirector approach with data from the chemistry engine:
//   - Phase changes → precipitate / gas / frost effects
//   - Temperature / enthalpy → heat glow / frost
//   - Gas evolution from stoichiometry → gas particles
//   - Concentration changes → color intensity
//
// Pure static logic — no MonoBehaviour, no particle systems.

using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    public static class VisualBindingLayer
    {
        private const float HeatThreshold = 2f;
        private const float StrongExothermKJ = -40f;
        private const float StrongEndothermKJ = 40f;
        private const float GasProductionMinMoles = 0.01f;

        /// <summary>
        /// Resolve visual hints from a ChemistryOutput, using both the reaction
        /// JSON visual_effects and chemistry-derived data.
        /// </summary>
        public static VisualHints Resolve(ReactionEntry reaction, ChemistryOutput output)
        {
            var hints = new VisualHints();

            if (!output.Found || output.Status == ReactionStatus.Fail)
                return hints;

            // --- JSON-defined base effects (same as VisualDirector) ---
            var vfx = reaction?.visual_effects;
            if (vfx != null)
            {
                if (!string.IsNullOrEmpty(vfx.color_change))
                {
                    hints.ColorChange = true;
                    hints.ColorHex    = vfx.color_change;
                }

                float td = vfx.temperature_delta;
                hints.TemperatureDelta = td;
                if (td >= HeatThreshold || td <= -HeatThreshold)
                    hints.HeatGlow = true;

                if (vfx.precipitate) hints.Precipitate = true;
                if (vfx.glow)        hints.Glow        = true;
                if (vfx.sparks)      hints.Sparks       = true;
                if (vfx.smoke)       hints.Smoke        = true;
                if (vfx.foam)        hints.Foam         = true;
                if (vfx.frost)       hints.Frost        = true;
                if (vfx.gas)         hints.GasParticles = true;
            }

            // --- Chemistry-derived enhancements ---

            // Gas evolution from products
            if (!hints.GasParticles && HasGasProduct(output.Substances))
                hints.GasParticles = true;

            // Precipitate from solid products forming in solution
            if (!hints.Precipitate && HasPrecipitate(output.Substances))
                hints.Precipitate = true;

            // Enthalpy-based thermal effects
            if (output.IsExothermic && output.EnthalpyKJ < StrongExothermKJ)
            {
                hints.HeatGlow = true;
                // Estimate temperature delta from enthalpy if not already set
                if (Mathf.Approximately(hints.TemperatureDelta, 0f))
                    hints.TemperatureDelta = Mathf.Abs(output.EnthalpyKJ) * 0.1f;
            }

            if (!output.IsExothermic && output.EnthalpyKJ > StrongEndothermKJ)
            {
                hints.Frost = true;
                if (Mathf.Approximately(hints.TemperatureDelta, 0f))
                    hints.TemperatureDelta = -output.EnthalpyKJ * 0.1f;
            }

            // Scale visuals by completion (partial reactions get dimmer effects)
            float intensity = Mathf.Clamp01(output.CompletionPercent / 100f);
            if (intensity < 0.15f)
            {
                // Very low completion → suppress most visuals
                hints.GasParticles = false;
                hints.Sparks       = false;
                hints.Foam         = false;
            }

            return hints;
        }

        private static bool HasGasProduct(List<SubstanceState> substances)
        {
            if (substances == null) return false;
            for (int i = 0; i < substances.Count; i++)
            {
                if (substances[i].IsProduct &&
                    substances[i].Phase == Phase.Gas &&
                    substances[i].MolesFinal > GasProductionMinMoles)
                    return true;
            }
            return false;
        }

        private static bool HasPrecipitate(List<SubstanceState> substances)
        {
            if (substances == null) return false;
            for (int i = 0; i < substances.Count; i++)
            {
                if (substances[i].IsProduct &&
                    substances[i].Phase == Phase.Solid &&
                    substances[i].MolesFinal > 0f)
                    return true;
            }
            return false;
        }
    }
}
