// ChemLabSim v3 — Chemical State System
// Represents the physical/chemical state of a substance in the lab:
// phase, concentration, volume, moles, molar mass, temperature.
// Pure data — used by Stoichiometry, Kinetics, and Equilibrium solvers.

using System;

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum Phase
    {
        Solid,
        Liquid,
        Gas,
        Aqueous
    }

    /// <summary>
    /// Immutable snapshot of a chemical substance's state in the simulation.
    /// Created per-reagent at the start of each evaluation.
    /// </summary>
    public struct ChemState
    {
        public string Formula;
        public Phase Phase;
        public float Moles;
        public float ConcentrationMolPerL;
        public float VolumeLiters;
        public float MolarMassGPerMol;
        public float TemperatureC;

        /// <summary>Mass = moles × molar mass (grams).</summary>
        public float MassGrams => Moles * MolarMassGPerMol;

        /// <summary>Whether the substance is in a condensed phase (solid/liquid/aqueous).</summary>
        public bool IsCondensed => Phase != Phase.Gas;

        /// <summary>Temperature in Kelvin for thermodynamic calculations.</summary>
        public float TemperatureK => TemperatureC + 273.15f;

        /// <summary>Recalculate concentration from current moles and volume.</summary>
        public ChemState WithUpdatedConcentration()
        {
            var copy = this;
            copy.ConcentrationMolPerL = copy.VolumeLiters > 0f
                ? copy.Moles / copy.VolumeLiters
                : 0f;
            return copy;
        }

        /// <summary>Create a state with reduced moles (after partial consumption).</summary>
        public ChemState Consume(float molesUsed)
        {
            var copy = this;
            copy.Moles = Math.Max(0f, copy.Moles - molesUsed);
            return copy.WithUpdatedConcentration();
        }

        /// <summary>Parse a phase string from JSON ("s", "l", "g", "aq") into Phase enum.</summary>
        public static Phase ParsePhase(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return Phase.Aqueous;

            switch (state.Trim().ToLowerInvariant())
            {
                case "s":     return Phase.Solid;
                case "l":     return Phase.Liquid;
                case "g":     return Phase.Gas;
                case "aq":    return Phase.Aqueous;
                case "solid": return Phase.Solid;
                case "liquid":return Phase.Liquid;
                case "gas":   return Phase.Gas;
                default:      return Phase.Aqueous;
            }
        }

        /// <summary>Phase label for display.</summary>
        public static string PhaseLabel(Phase p)
        {
            switch (p)
            {
                case Phase.Solid:   return "s";
                case Phase.Liquid:  return "l";
                case Phase.Gas:     return "g";
                case Phase.Aqueous: return "aq";
                default:            return "?";
            }
        }
    }
}
