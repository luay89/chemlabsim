// ChemLabSim v3 — pH where it can be calculated, and an explicit "unsupported" elsewhere
//
// Supported (25 °C, ideal solution — activities are not modelled, so concentrated
// solutions are flagged as approximate):
//   • water                                      pH 7.00
//   • a strong acid / strong base solution       [H+] or [OH−] from the bottle concentration
//       strong acids: HCl, HBr, HI, HNO3          (1 H+)
//       strong bases: NaOH, KOH, LiOH (1 OH−), Ba(OH)2, Ca(OH)2 (2 OH−)
//   • H2SO4: first proton strong, second Ka2 = 1.0 × 10⁻² (pKa2 1.99)
//   • acetic acid: Ka = 1.75 × 10⁻⁵ (pKa 4.756), solved exactly (quadratic)
//   • after a strong acid + strong base neutralisation with known amounts: the excess
//     H+ or OH− in the total volume (volumes taken as additive; solids add no volume)
// Sources: Kw = 1.0 × 10⁻¹⁴ at 25 °C; Ka values from CRC Handbook of Chemistry and Physics,
// 97th ed., "Dissociation Constants of Inorganic Acids and Bases" / "… of Organic Acids".
// Anything else (salt hydrolysis, buffers, weak acid + base, redox, non-aqueous mixtures)
// returns Unsupported with the reason — never a guessed number.

using System;
using System.Collections.Generic;
using System.Linq;

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum PhStatus { Calculated, Unsupported }

    public struct PhResult
    {
        public PhStatus Status;
        public double Ph;          // NaN when unsupported
        public string Basis;       // how it was obtained, or why it is not available
        public bool Approximate;   // concentrated solution: activities ignored

        public bool IsCalculated => Status == PhStatus.Calculated;

        public static PhResult Calculated(double ph, string basis, bool approximate = false)
            => new PhResult { Status = PhStatus.Calculated, Ph = ph, Basis = basis, Approximate = approximate };

        public static PhResult Unsupported(string why)
            => new PhResult { Status = PhStatus.Unsupported, Ph = double.NaN, Basis = why };
    }

    public static class PhModel
    {
        public const double Kw = 1.0e-14;                 // 25 °C
        public const double KaAceticAcid = 1.75e-5;      // CRC, 25 °C
        public const double Ka2SulfuricAcid = 1.0e-2;    // CRC, 25 °C (pKa2 1.99)
        private const double DiluteLimitM = 0.1;          // above this, ideal-solution pH is approximate

        private static readonly Dictionary<string, int> StrongAcids = new Dictionary<string, int>
        { { "HCl", 1 }, { "HBr", 1 }, { "HI", 1 }, { "HNO3", 1 } };

        private static readonly Dictionary<string, int> StrongBases = new Dictionary<string, int>
        { { "NaOH", 1 }, { "KOH", 1 }, { "LiOH", 1 }, { "Ba(OH)2", 2 }, { "Ca(OH)2", 2 } };

        // Salts of a strong acid with a strong base: they do not change the pH.
        private static readonly HashSet<string> NeutralSalts = new HashSet<string>
        {
            "NaCl", "KCl", "LiCl", "NaBr", "KBr", "NaI", "KI", "NaNO3", "KNO3", "LiNO3",
            "BaCl2", "CaCl2", "Ba(NO3)2", "Ca(NO3)2", "LiBr", "LiI"
        };

        /// <summary>pH of one solution from its concentration (mol/L).</summary>
        public static PhResult ForSolution(string formula, double concentrationM)
        {
            if (formula == "H2O")
                return PhResult.Calculated(7.0, "pure water at 25 °C: [H+] = [OH−] = 1.0 × 10⁻⁷ M");
            if (double.IsNaN(concentrationM) || concentrationM <= 0)
                return PhResult.Unsupported($"the concentration of {formula} is not known");

            bool approx = concentrationM > DiluteLimitM;
            if (StrongAcids.TryGetValue(formula, out int h))
                return FromH(h * concentrationM, $"strong acid, fully dissociated: [H+] = {h} × {concentrationM:0.###} M", approx);
            if (StrongBases.TryGetValue(formula, out int oh))
                return FromOH(oh * concentrationM, $"strong base, fully dissociated: [OH−] = {oh} × {concentrationM:0.####} M", approx);
            if (formula == "H2SO4")
                return FromH(SulfuricAcidH(concentrationM, 0.0), $"H2SO4: first H+ complete, second Ka2 = 1.0 × 10⁻²", approx);
            if (formula == "CH3COOH")
                return FromH(WeakAcidH(concentrationM, KaAceticAcid), "weak acid, Ka = 1.75 × 10⁻⁵ (solved exactly)", approx);

            return PhResult.Unsupported($"no acid–base model for {formula} (e.g. salt hydrolysis is not modelled)");
        }

        /// <summary>
        /// pH after a strong acid + strong base reaction, from the student's amounts.
        /// Needs the volume of every solution (mL), so the total volume is known.
        /// </summary>
        public static PhResult AfterReaction(ReactionEntry reaction, AmountEvaluation amounts)
        {
            if (reaction == null || amounts == null || !amounts.Success)
                return PhResult.Unsupported("the amounts of the reagents are not complete");

            double totalMl = 0, h = 0, oh = 0;
            foreach (var line in amounts.Lines)
            {
                string f = line.Input.Formula;
                string state = (line.Input.State ?? string.Empty).ToLowerInvariant();
                bool solution = state == "aqueous" || state == "aq" || state == "liquid" || state == "l";
                if (solution)
                {
                    if (line.Input.Unit != "mL")
                        return PhResult.Unsupported("a solution was measured without a volume, so the final volume is unknown");
                    totalMl += line.Input.Value;
                }
                if (StrongAcids.TryGetValue(f, out int nh)) h += nh * line.Moles.Moles;
                else if (StrongBases.TryGetValue(f, out int noh)) oh += noh * line.Moles.Moles;
                else if (f == "H2SO4") h += 2 * line.Moles.Moles;
                else if (f != "H2O")
                    return PhResult.Unsupported($"{f} is not a strong acid or base, so the pH after the reaction is not modelled");
            }

            if (h <= 0 || oh <= 0)
                return PhResult.Unsupported("this is not an acid + base neutralisation");
            if (totalMl <= 0)
                return PhResult.Unsupported("the final volume is unknown");
            bool sulfuric = amounts.Lines.Any(l => l.Input.Formula == "H2SO4");
            foreach (var p in reaction.products ?? new List<ReactionChemical>())
                if (p.formula != "H2O" && !NeutralSalts.Contains(p.formula) && p.formula != "Na2SO4" && p.formula != "K2SO4")
                    return PhResult.Unsupported($"the product {p.formula} may change the pH (hydrolysis is not modelled)");

            double litres = totalMl / 1000.0;
            const string Basis = "excess strong acid/base in the combined volume (volumes taken as additive)";
            if (Math.Abs(h - oh) <= 1e-9 * Math.Max(h, oh))
                return PhResult.Calculated(7.0, "exact neutralisation of a strong acid by a strong base");
            if (h > oh)
            {
                if (sulfuric)
                    return PhResult.Unsupported("excess H2SO4 leaves HSO4− in solution; that mixture is not modelled");
                double c = (h - oh) / litres;
                return FromH(c, Basis, c > DiluteLimitM);
            }
            double cOH = (oh - h) / litres;
            return FromOH(cOH, Basis, cOH > DiluteLimitM);
        }

        /// <summary>H+ per formula unit of a strong acid (0 when it is not one of the modelled strong acids).</summary>
        public static int StrongAcidProtons(string formula)
            => formula != null && StrongAcids.TryGetValue(formula, out int n) ? n : 0;

        /// <summary>OH− per formula unit of a strong base (0 when it is not one of the modelled strong bases).</summary>
        public static int StrongBaseHydroxides(string formula)
            => formula != null && StrongBases.TryGetValue(formula, out int n) ? n : 0;

        /// <summary>
        /// pH of a solution holding only fully dissociated H+/OH− and spectator ions, from the net
        /// strong acid concentration (mol/L; negative = net strong base). Water's ions included, so
        /// the result is continuous through neutrality: [H+] = (d + √(d² + 4Kw)) / 2.
        /// For a net base the same is solved for [OH−], which avoids cancellation in d + √(…).
        /// </summary>
        public static double PhFromNetStrongAcid(double netMolar)
            => netMolar >= 0 ? -Math.Log10(WithWater(netMolar)) : 14.0 + Math.Log10(WithWater(-netMolar));

        // ── Equilibria ─────────────────────────────────────────────────

        /// <summary>[H+] of a weak acid HA, exact: x² / (C − x) = Ka.</summary>
        public static double WeakAcidH(double c, double ka)
            => (-ka + Math.Sqrt(ka * ka + 4 * ka * c)) / 2.0;

        /// <summary>[H+] of H2SO4 (C) plus extra strong H+ (h0): first proton complete, second with Ka2.</summary>
        public static double SulfuricAcidH(double c, double h0)
        {
            // (c + h0 + x) x / (c − x) = Ka2  →  x² + (c + h0 + Ka2) x − Ka2 c = 0
            double b = c + h0 + Ka2SulfuricAcid;
            double x = (-b + Math.Sqrt(b * b + 4 * Ka2SulfuricAcid * c)) / 2.0;
            return c + h0 + x;
        }

        // Water's own ions are included, so a very dilute acid never comes out basic:
        // [H+] = (c + √(c² + 4Kw)) / 2 for c mol/L of added H+ (and the same for OH−).
        private static PhResult FromH(double c, string basis, bool approx)
            => PhResult.Calculated(-Math.Log10(WithWater(c)), basis, approx);

        private static PhResult FromOH(double c, string basis, bool approx)
            => PhResult.Calculated(14.0 + Math.Log10(WithWater(c)), basis, approx);

        private static double WithWater(double c) => (c + Math.Sqrt(c * c + 4 * Kw)) / 2.0;
    }
}
