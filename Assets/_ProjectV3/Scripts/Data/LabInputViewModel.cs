// ChemLabSim v3 — Lab Input ViewModel
// Pure data carrier representing the current state of all lab input controls.
// Built by LabInputController from UI control values, converted to MixRequest for ReactionController.
// No UI dependencies, no logic.

using System.Collections.Generic;

namespace ChemLabSimV3.Data
{
    public struct LabInputViewModel
    {
        /// <summary>Selected reagent formulas (2–4 slots, empty string = no selection).</summary>
        public string ReagentA;
        public string ReagentB;
        public string ReagentC;
        public string ReagentD;

        /// <summary>Medium index: 0=Neutral, 1=Acidic, 2=Basic.</summary>
        public int MediumIndex;

        /// <summary>Temperature in °C (0–100).</summary>
        public float Temperature;

        /// <summary>Stirring intensity (0–1).</summary>
        public float Stirring;

        /// <summary>Grinding intensity (0–1).</summary>
        public float Grinding;

        /// <summary>Whether catalyst is applied.</summary>
        public bool HasCatalyst;

        /// <summary>Build a MixRequest from the current input state.</summary>
        public MixRequest ToMixRequest()
        {
            var reagents = new List<string>();
            if (!string.IsNullOrWhiteSpace(ReagentA)) reagents.Add(ReagentA.Trim());
            if (!string.IsNullOrWhiteSpace(ReagentB)) reagents.Add(ReagentB.Trim());
            if (!string.IsNullOrWhiteSpace(ReagentC)) reagents.Add(ReagentC.Trim());
            if (!string.IsNullOrWhiteSpace(ReagentD)) reagents.Add(ReagentD.Trim());

            return new MixRequest
            {
                ReagentNames = reagents,
                Medium = (ReactionMedium)MediumIndex,
                Temperature = Temperature,
                Stirring = Stirring,
                Grinding = Grinding,
                HasCatalyst = HasCatalyst
            };
        }
    }
}
