// ChemLabSim v3 — NotebookEntry
// Data carrier for a single experiment history record.
// Built by NotebookController from ReactionEvaluatedEvent, displayed by NotebookView.

namespace ChemLabSimV3.Data
{
    public struct NotebookEntry
    {
        /// <summary>1-based experiment number in the session.</summary>
        public int Number;

        /// <summary>Formatted reagent names, e.g. "HCl + NaOH".</summary>
        public string ReagentSummary;

        /// <summary>Outcome key: "success", "partial", "fail", "invalid".</summary>
        public string OutcomeKey;

        /// <summary>Medium label, e.g. "Acidic".</summary>
        public string MediumName;

        /// <summary>Temperature at time of mix.</summary>
        public float TemperatureC;
    }
}
