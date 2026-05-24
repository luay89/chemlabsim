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
        public float PressureAtm;
        public bool IsClosedContainer;
        public float HeadspaceVolumeLiters;
        public float SurfaceAreaM2;
        public float HeatTransferCoefficient;
        public float GasEscapeRatePerSec;
        public float MaxPressureAtm;

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
            PressureAtm = 1f;
            IsClosedContainer = false;
            HeadspaceVolumeLiters = 1f;
            SurfaceAreaM2 = 0.02f;
            HeatTransferCoefficient = 0.05f;
            GasEscapeRatePerSec = 0.5f;
            MaxPressureAtm = 4f;
        }
    }
}
