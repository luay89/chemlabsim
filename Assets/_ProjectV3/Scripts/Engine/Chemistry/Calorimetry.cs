// ChemLabSim v3 — temperature rise of a strong acid + strong base neutralisation
//
//   H⁺(aq) + OH⁻(aq) → H₂O(l)    ΔH = −55.815 kJ/mol
//   from ΔfH°(H₂O, l) = −285.830 and ΔfH°(OH⁻, aq) = −230.015 kJ/mol, ΔfH°(H⁺, aq) = 0
//   (CODATA Key Values for Thermodynamics, as tabulated in the CRC Handbook)
//
//   ΔT = n(H₂O formed) · 55 815 J/mol / (V_total · ρ · c)
// Assumptions (stated with every reading): no heat lost to the beaker or the air, the solution
// has the density (1.00 g/mL) and specific heat (4.184 J g⁻¹ K⁻¹) of water, both solutions start
// at the same temperature, volumes are additive. Only the strong acids/bases of PhModel; H2SO4
// is excluded (its second proton adds its own heat), as is everything else.

using System;
using System.Collections.Generic;

namespace ChemLabSimV3.Engine.Chemistry
{
    public static class Calorimetry
    {
        public const double NeutralisationJPerMol = 55815.0;    // released per mol of water formed
        public const double SolutionDensityGPerMl = 1.00;       // assumption: like water
        public const double SpecificHeatJPerGK = 4.184;         // assumption: like water
        public const string Assumptions = "no heat loss, solution taken as water (1.00 g/mL, 4.184 J/g·K)";

        /// <summary>Temperature rise (K) when n mol of water form in a total volume (mL).</summary>
        public static double NeutralisationRiseK(double waterFormedMol, double totalMl)
        {
            if (!(waterFormedMol >= 0) || !(totalMl > 0) || double.IsInfinity(totalMl)) return double.NaN;
            return waterFormedMol * NeutralisationJPerMol / (totalMl * SolutionDensityGPerMl * SpecificHeatJPerGK);
        }

        /// <summary>
        /// Temperature rise of a mix from the student's amounts, when it is a strong acid + strong
        /// base neutralisation with every solution measured by volume.
        /// </summary>
        public static bool TryNeutralisation(ReactionEntry reaction, AmountEvaluation amounts, out double riseK, out string why)
        {
            riseK = double.NaN;
            if (reaction == null || amounts == null || !amounts.Success) { why = "the amounts are not complete"; return false; }

            double totalMl = 0, h = 0, oh = 0;
            foreach (var line in amounts.Lines)
            {
                string f = line.Input.Formula;
                int nh = PhModel.StrongAcidProtons(f), noh = PhModel.StrongBaseHydroxides(f);
                if (nh == 0 && noh == 0 && f != "H2O") { why = $"{f} is not a strong acid or base"; return false; }
                if (line.Input.Unit != "mL") { why = "every solution must be measured by volume"; return false; }
                totalMl += line.Input.Value;
                h += nh * line.Moles.Moles;
                oh += noh * line.Moles.Moles;
            }
            if (h <= 0 || oh <= 0) { why = "this is not an acid + base neutralisation"; return false; }

            riseK = NeutralisationRiseK(Math.Min(h, oh), totalMl);
            why = string.Empty;
            return !double.IsNaN(riseK);
        }
    }
}
