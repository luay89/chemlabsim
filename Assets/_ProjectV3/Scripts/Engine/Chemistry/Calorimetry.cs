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
//
// Any other reaction in water — from its sourced standard reaction enthalpy (Hess's law, see
// ReactionEntry.enthalpyKJPerMol) and the extent ξ the student's amounts allow:
//   q = −ΔH° · ξ        ΔT = q / (V_liquid · ρ · c)
// with the same assumptions, plus: the standard ΔH° (ideal, dilute solutions) stands for the
// bench solutions, and gases or solids carry no heat away. Not for heated reactions (the hot
// plate's heat is not in the balance), electrolysis (nor the current's), a liquid other than
// water, H2SO4 (its heat of dilution at bench strength is large), or a result that would boil
// or freeze the water.

using System;
using System.Collections.Generic;
using ChemLabSimV3.Data;

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

        /// <summary>True when the reaction carries a ΔH° calculated from sourced data.</summary>
        public static bool HasSourcedEnthalpy(ReactionEntry reaction)
            => reaction != null && !string.IsNullOrEmpty(reaction.enthalpySource);

        /// <summary>Why a reaction has no ΔH°, in the words of the data tool.</summary>
        public static string MissingEnthalpyReason(ReactionEntry reaction)
            => reaction != null && !string.IsNullOrEmpty(reaction.enthalpyMissing)
                ? reaction.enthalpyMissing
                : "no sourced reaction enthalpy";

        /// <summary>Heat given out (kJ, negative = taken in) when the reaction runs to extent ξ (mol).</summary>
        public static double HeatReleasedKJ(ReactionEntry reaction, double extentMol)
            => -reaction.enthalpyKJPerMol * extentMol;

        /// <summary>
        /// Temperature change (K, negative = cooling) of a reaction in water from its sourced ΔH°
        /// and the student's amounts; false with the reason when this model does not apply.
        /// </summary>
        public static bool TryReaction(ReactionEntry reaction, AmountEvaluation amounts, out double riseK, out string why)
        {
            riseK = double.NaN;
            if (!HasSourcedEnthalpy(reaction)) { why = MissingEnthalpyReason(reaction); return false; }
            if (reaction.requiresHeating) { why = "the reaction is heated; the hot plate's heat is not in this balance"; return false; }
            if (ElectricityCondition.IsElectrolysis(reaction)) { why = "electrolysis is driven by the current, which is not in this balance"; return false; }
            if (amounts == null || !amounts.Success || !(amounts.Stoichiometry.Extent >= 0))
            { why = "the amounts are not complete"; return false; }

            var inputs = new List<ReagentAmountInput>();
            foreach (var line in amounts.Lines)
            {
                string f = line.Input.Formula;
                if (f == "H2SO4") { why = "the heat of dilution of sulfuric acid is not modelled"; return false; }
                if (SubstanceSpec.ParseState(line.Input.State) == MatterState.Liquid && f != "H2O")
                { why = $"{f} is a liquid other than water; its heat capacity is not modelled"; return false; }
                inputs.Add(line.Input);
            }

            var volume = VesselVolume.Total(inputs);
            if (!volume.HasLiquid) { why = volume.Known ? "there is no water in the beaker to take up the heat" : volume.Basis; return false; }

            riseK = HeatReleasedKJ(reaction, amounts.Stoichiometry.Extent) * 1000.0
                    / (volume.Ml * SolutionDensityGPerMl * SpecificHeatJPerGK);
            why = string.Empty;
            return !double.IsNaN(riseK) && !double.IsInfinity(riseK);
        }

        /// <summary>False when start + ΔT leaves liquid water (0–100 °C), where this model stops.</summary>
        public static bool StaysLiquid(double startC, double riseK, out string why)
        {
            double end = startC + riseK;
            why = end >= 100.0 ? "the calculated heat would boil the water, which this model does not follow"
                : end <= 0.0 ? "the calculated cooling would freeze the water, which this model does not follow"
                : string.Empty;
            return why.Length == 0;
        }
    }
}
