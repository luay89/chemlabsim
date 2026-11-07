// ChemLabSim v3 — what a temperature reading is based on
// The live simulation (SimulationStepper) turns the reaction enthalpy into a temperature
// change. Only reactions with a stored enthalpyKJPerMol have a ΔH; even then the stepper
// runs a model vessel (fixed volume, water-like heat capacity) in stoichiometric proportions,
// not the student's amounts, so the reading is "simulated", never "calculated". For the other
// reactions the stepper uses the observed temperature change of the reaction record
// (temperature_delta), so that reading is an estimate. Without a running reaction the
// thermometer shows the temperature set on the hot plate.
// "Calculated" is used only where the energy balance is fully known from the student's amounts:
// a strong acid + strong base neutralisation (Calorimetry), with its assumptions stated.

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum TemperatureBasis { SetPoint, Estimated, Simulated, Calculated }

    public static class Thermometry
    {
        public static TemperatureBasis BasisFor(ReactionEntry reaction)
        {
            if (reaction == null) return TemperatureBasis.SetPoint;
            return reaction.enthalpyKJPerMol != 0f ? TemperatureBasis.Simulated : TemperatureBasis.Estimated;
        }

        public static string Label(TemperatureBasis basis)
        {
            switch (basis)
            {
                case TemperatureBasis.Simulated:  return "simulated from ΔH (model vessel)";
                case TemperatureBasis.Calculated: return "calculated from ΔH_neut and your volumes; no heat loss";
                case TemperatureBasis.Estimated:  return "estimated from observed ΔT";
                default:                          return "set on the hot plate";
            }
        }
    }
}
