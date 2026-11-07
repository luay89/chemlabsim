// ChemLabSim v3 — amount of one reagent in a mix request (plain data, no logic)
// Filled by LabInputController from the student's input and materials.json; turned into
// moles by the engine (AmountEvaluator). 0 / empty means "not known".

namespace ChemLabSimV3.Data
{
    public struct ReagentAmountInput
    {
        public string Formula;
        public double Value;
        public string Unit;                      // "g", "mL" or "mol"
        public string State;                     // material state: solid / liquid / aqueous / gas
        public double DensityGPerMl;             // pure liquids (literature value), 0 = unknown
        public double StockConcentrationM;       // solutions (bottle label), 0 = not set
        public string StockLabel;                // e.g. "1.0 M bench stock"
    }
}
