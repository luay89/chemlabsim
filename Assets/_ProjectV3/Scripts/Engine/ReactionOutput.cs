// ChemLabSim v3 — Reaction Output Models
// Clean output from the ReactionEngine: products, observation, explanation,
// energy change, condition breakdown, and visual effect hints.
// Pure data — no Unity UI types, no formatting.

using System.Collections.Generic;

namespace ChemLabSimV3.Engine
{
    /// <summary>A single reaction product with formula, physical state, and stoichiometry.</summary>
    public struct ProductInfo
    {
        public string Formula;
        public string State;
        public float Stoich;
    }

    /// <summary>
    /// Visual effect hints resolved by the engine.
    /// Consumed by FXController to drive particle systems and animations.
    /// </summary>
    public struct VisualHints
    {
        public bool ColorChange;
        public string ColorHex;
        public bool GasParticles;
        public bool HeatGlow;
        public bool Precipitate;
        public bool Sparks;
        public bool Smoke;
        public bool Foam;
        public bool Frost;
        public bool Glow;
        public float TemperatureDelta;
    }

    /// <summary>
    /// Complete output of a single reaction evaluation.
    /// Contains everything downstream consumers need: identity, status,
    /// products, scientific text, energy data, conditions, safety, and visuals.
    /// </summary>
    public struct ReactionOutput
    {
        // -- Identity --
        public bool Found;
        public string ReactionId;
        public string ReactionName;
        public string ReactionType;

        // -- Status --
        public ReactionStatus Status;
        public float Rate;
        public string Summary;

        // -- Products --
        public List<ProductInfo> Products;

        // -- Scientific --
        public string Observation;
        public string Explanation;
        public string ConditionNotes;

        // -- Energy --
        public float EnergyChange;
        public bool IsExothermic;

        // -- Condition Breakdown --
        public List<ConditionResult> Conditions;

        // -- Safety --
        public List<string> GhsCodes;
        public List<string> SafetyWarnings;
        public string SafetyNotes;

        // -- Visual Hints --
        public VisualHints Visuals;

        // -- Echo (for UI display) --
        public List<string> ReagentFormulas;

        /// <summary>Creates a "not found" output for unmatched reagent sets.</summary>
        public static ReactionOutput NotFound(List<string> reagents)
        {
            return new ReactionOutput
            {
                Found          = false,
                Status         = ReactionStatus.Fail,
                Rate           = 0f,
                Summary        = "No matching reaction found for the selected reagents.",
                ReagentFormulas = reagents ?? new List<string>(),
                Products       = new List<ProductInfo>(),
                Conditions     = new List<ConditionResult>(),
                GhsCodes       = new List<string>(),
                SafetyWarnings = new List<string>(),
                Observation    = string.Empty,
                Explanation    = string.Empty,
                ConditionNotes = string.Empty,
                SafetyNotes    = string.Empty,
                ReactionId     = string.Empty,
                ReactionName   = string.Empty,
                ReactionType   = string.Empty
            };
        }
    }
}
