// ChemLabSim v3 — Visual Director
// Maps reaction data + engine output → VisualHints.
// Pure static logic — no particle systems or materials here.
// FXController consumes VisualHints to drive the actual VFX views.

namespace ChemLabSimV3.Engine
{
    public static class VisualDirector
    {
        private const float HeatThreshold = 2f;

        /// <summary>
        /// Resolve which visual effects should play for a given reaction output.
        /// Only produces visuals for reactions that actually occurred (Success or Partial).
        /// </summary>
        public static VisualHints Resolve(ReactionEntry reaction, ReactionOutput output)
        {
            var hints = new VisualHints();

            if (!output.Found || output.Status == ReactionStatus.Fail)
                return hints;

            var vfx = reaction?.visual_effects;
            if (vfx == null)
                return hints;

            // Color change
            if (!string.IsNullOrEmpty(vfx.color_change))
            {
                hints.ColorChange = true;
                hints.ColorHex    = vfx.color_change;
            }

            // Gas particles (bubbles)
            if (reaction.GetProducesGas() || vfx.gas)
                hints.GasParticles = true;

            // Heat glow (significant temperature delta)
            float td = vfx.temperature_delta;
            hints.TemperatureDelta = td;
            if (td >= HeatThreshold || td <= -HeatThreshold)
                hints.HeatGlow = true;

            // Precipitate formation
            if (vfx.precipitate)
                hints.Precipitate = true;

            // Extended effects
            if (vfx.glow)   hints.Glow   = true;
            if (vfx.sparks) hints.Sparks  = true;
            if (vfx.smoke)  hints.Smoke   = true;
            if (vfx.foam)   hints.Foam    = true;
            if (vfx.frost)  hints.Frost   = true;

            return hints;
        }
    }
}
