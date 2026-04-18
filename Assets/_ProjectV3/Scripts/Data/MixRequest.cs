// ChemLabSim v3 — MixRequest DTO
// Pure data carrier for mix inputs — no UI dependencies.
// Built by UIController (or tests) and passed to ReactionController.

using System.Collections.Generic;

namespace ChemLabSimV3.Data
{
    public struct MixRequest
    {
        public List<string> ReagentNames;
        public ReactionMedium Medium;
        public float Temperature;
        public float Stirring;
        public float Grinding;
        public bool HasCatalyst;

        public MixRequest(
            List<string> reagentNames,
            ReactionMedium medium,
            float temperature,
            float stirring,
            float grinding,
            bool hasCatalyst)
        {
            ReagentNames = reagentNames;
            Medium = medium;
            Temperature = temperature;
            Stirring = stirring;
            Grinding = grinding;
            HasCatalyst = hasCatalyst;
        }
    }
}
