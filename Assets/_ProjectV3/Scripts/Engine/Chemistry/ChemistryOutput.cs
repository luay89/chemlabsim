// ChemLabSim v3 — Chemistry Output
// Rich output model for the next-gen chemistry engine.
// Extends the base ReactionOutput with stoichiometric, thermodynamic,
// and equilibrium data for detailed UI display and visual binding.

using System.Collections.Generic;

namespace ChemLabSimV3.Engine.Chemistry
{
    /// <summary>Per-substance final state after the reaction.</summary>
    public struct SubstanceState
    {
        public string Formula;
        public Phase Phase;
        public float MolesInitial;
        public float MolesFinal;
        public float MolesConsumed;
        public float ConcentrationMolPerL;
        public bool IsReactant;
        public bool IsProduct;
        public bool IsLimitingReagent;
        public bool IsExcess;
    }

    /// <summary>
    /// Complete output from the ChemistryEngine, containing all simulation
    /// data needed by the UI and visual binding layer.
    /// </summary>
    public struct ChemistryOutput
    {
        // -- Identity --
        public bool Found;
        public string ReactionId;
        public string ReactionName;
        public string BalancedEquation;

        // -- Overall Status --
        public ReactionStatus Status;
        public float CompletionPercent;
        public string Summary;

        // -- Stoichiometry --
        public string LimitingReagent;
        public float MaxExtent;
        public float ActualExtent;
        public List<SubstanceState> Substances;

        // -- Thermodynamics --
        public float EnthalpyKJ;
        public bool IsExothermic;
        public float RateMultiplier;
        public float EffectiveEaKJ;
        public string ThermoSummary;

        // -- Equilibrium --
        public bool IsReversible;
        public float Keq;
        public float EquilibriumExtent;
        public string EquilibriumShift;
        public string EquilibriumSummary;

        // -- Conditions --
        public List<ConditionResult> Conditions;
        public float ConditionRate;

        // -- Safety --
        public List<string> GhsCodes;
        public List<string> SafetyWarnings;
        public string SafetyNotes;

        // -- Descriptive --
        public string Observation;
        public string Explanation;
        public string ConditionNotes;

        // -- Visual --
        public VisualHints Visuals;

        // -- Input Echo --
        public List<string> ReagentFormulas;
        public float TemperatureC;
        public float PressureAtm;

        /// <summary>Factory for a "no reaction found" result.</summary>
        public static ChemistryOutput NotFound(List<string> reagents)
        {
            return new ChemistryOutput
            {
                Found             = false,
                ReactionId        = string.Empty,
                ReactionName      = string.Empty,
                BalancedEquation  = string.Empty,
                Status            = ReactionStatus.Fail,
                CompletionPercent = 0f,
                Summary           = "No known reaction for this combination.",
                LimitingReagent   = string.Empty,
                MaxExtent         = 0f,
                ActualExtent      = 0f,
                Substances        = new List<SubstanceState>(),
                EnthalpyKJ        = 0f,
                IsExothermic      = false,
                RateMultiplier    = 0f,
                EffectiveEaKJ     = 0f,
                ThermoSummary     = string.Empty,
                IsReversible      = false,
                Keq               = 0f,
                EquilibriumExtent = 0f,
                EquilibriumShift  = "none",
                EquilibriumSummary = string.Empty,
                Conditions        = new List<ConditionResult>(),
                ConditionRate     = 0f,
                GhsCodes          = new List<string>(),
                SafetyWarnings    = new List<string>(),
                SafetyNotes       = string.Empty,
                Observation       = string.Empty,
                Explanation       = string.Empty,
                ConditionNotes    = string.Empty,
                Visuals           = default,
                ReagentFormulas   = reagents ?? new List<string>(),
                TemperatureC      = 0f,
                PressureAtm       = 1f
            };
        }
    }
}
