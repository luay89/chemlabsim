// ChemLabSim v3 — what a temperature reading is based on
// Two different heats are kept apart:
//   • The live simulation (SimulationStepper) warms an illustrative model vessel (fixed volume,
//     water-like heat capacity, stoichiometric proportions) so the 3D bench looks like the lab.
//     It is driven by the observed temperature change of the reaction record (temperature_delta),
//     so its reading is an estimate. Only a record without visual data would fall back to ΔH°,
//     and that reading is "simulated", never "calculated".
//   • The sourced standard reaction enthalpy ΔH° (enthalpyKJPerMol, Hess's law) is used where the
//     student's amounts allow an energy balance (Calorimetry); that reading is "calculated", with
//     its assumptions stated.
// Without a running reaction the thermometer shows the temperature set on the hot plate.

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum TemperatureBasis { SetPoint, Estimated, Simulated, Calculated, CalculatedFromEnthalpy }

    public static class Thermometry
    {
        public static TemperatureBasis BasisFor(ReactionEntry reaction)
        {
            if (reaction == null) return TemperatureBasis.SetPoint;
            return reaction.visual_effects == null && reaction.enthalpyKJPerMol != 0f
                ? TemperatureBasis.Simulated
                : TemperatureBasis.Estimated;
        }

        /// <summary>
        /// Heat per mol of reaction (kJ, negative = given out) that drives the model vessel of the
        /// live simulation: the record's observed temperature change when it has visual data
        /// (−ΔT × 4.184 kJ, i.e. ΔT in the 1 L water-like vessel), otherwise the stored ΔH°.
        /// </summary>
        public static float ModelVesselEnthalpyKJ(ReactionEntry reaction)
        {
            if (reaction == null) return 0f;
            if (reaction.visual_effects != null) return -reaction.visual_effects.temperature_delta * 4.184f;
            return reaction.enthalpyKJPerMol;
        }

        public static string Label(TemperatureBasis basis)
        {
            switch (basis)
            {
                case TemperatureBasis.Simulated:              return "simulated from ΔH (model vessel)";
                case TemperatureBasis.Calculated:             return "calculated from ΔH_neut and your volumes; no heat loss";
                case TemperatureBasis.CalculatedFromEnthalpy: return "calculated from ΔH° and your amounts; no heat loss";
                case TemperatureBasis.Estimated:              return "estimated from observed ΔT";
                default:                                      return "set on the hot plate";
            }
        }
    }
}
