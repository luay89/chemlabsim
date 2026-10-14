// ChemLabSim v3 — what a temperature reading is based on
// The live simulation (SimulationStepper) turns the reaction enthalpy into a temperature
// change. Only reactions with a stored enthalpyKJPerMol have a real ΔH; for the others the
// stepper uses the observed temperature change of the reaction record (temperature_delta),
// so that reading is an estimate. Without a running reaction the thermometer shows the
// temperature set on the hot plate.

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum TemperatureBasis { SetPoint, Estimated, Calculated }

    public static class Thermometry
    {
        public static TemperatureBasis BasisFor(ReactionEntry reaction)
        {
            if (reaction == null) return TemperatureBasis.SetPoint;
            return reaction.enthalpyKJPerMol != 0f ? TemperatureBasis.Calculated : TemperatureBasis.Estimated;
        }

        public static string Label(TemperatureBasis basis)
        {
            switch (basis)
            {
                case TemperatureBasis.Calculated: return "calculated from ΔH";
                case TemperatureBasis.Estimated:  return "estimated from observed ΔT";
                default:                          return "set on the hot plate";
            }
        }
    }
}
