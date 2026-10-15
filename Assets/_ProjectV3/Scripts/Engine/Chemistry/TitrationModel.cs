// ChemLabSim v3 — acid–base titration curve (Stage 6)
//
// Supported scope (stated in the UI): a strong acid titrated with a strong base, or a strong
// base titrated with a strong acid — the strong acids and bases that PhModel models
// (HCl, HBr, HI, HNO3 / NaOH, KOH, LiOH, Ba(OH)2, Ca(OH)2). Their salts do not hydrolyse.
//
// Model (25 °C, ideal solution, volumes additive, complete reaction):
//   n(H+) and n(OH−) from the volumes and concentrations,
//   d = (n(H+) − n(OH−)) / V_total,   [H+] = (d + √(d² + 4Kw)) / 2,   pH = −log[H+]
//   equivalence volume  V_eq = ν_analyte · C_analyte · V_analyte / (ν_titrant · C_titrant)
// The exact water term keeps the curve continuous through the equivalence point (pH 7.00).
// Points where |d| > 0.1 M are flagged approximate (activities are not modelled).
//
// Not supported, with the reason: weak acids/bases (need a buffer / Ka model of the curve),
// polyprotic H2SO4 (second Ka), acid + acid, base + base, unknown concentrations.

using System;
using System.Collections.Generic;

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum TitrationRegion { Start, BeforeEquivalence, Equivalence, AfterEquivalence }

    public struct TitrationPoint
    {
        public double AddedMl;
        public double Ph;
        public bool Approximate;
        public TitrationRegion Region;
    }

    public class TitrationSetup
    {
        public string Analyte;            // in the beaker
        public double AnalyteMolar;       // mol/L
        public double AnalyteMl;          // mL in the beaker
        public string Titrant;            // in the burette
        public double TitrantMolar;       // mol/L
    }

    public class TitrationModel
    {
        private const double DiluteLimitM = 0.1;
        private const double EquivalenceToleranceMl = 1e-6;

        public bool Supported { get; private set; }
        public string Reason { get; private set; } = string.Empty;   // why not supported
        public TitrationSetup Setup { get; private set; }
        public bool AcidInBeaker { get; private set; }
        public double EquivalenceMl { get; private set; } = double.NaN;

        private double analyteEquivalents;   // mol of H+ (acid) or OH− (base) in the beaker
        private int titrantPerFormula;

        public static TitrationModel Create(TitrationSetup s)
        {
            var m = new TitrationModel { Setup = s };
            if (s == null) return m.Refuse("no titration was set up");
            if (string.IsNullOrWhiteSpace(s.Analyte) || string.IsNullOrWhiteSpace(s.Titrant))
                return m.Refuse("choose the solution in the beaker (reagent A) and the titrant (reagent B)");
            if (!Positive(s.AnalyteMolar)) return m.Refuse($"the concentration of {s.Analyte} is not known");
            if (!Positive(s.TitrantMolar)) return m.Refuse($"the concentration of {s.Titrant} is not known");
            if (!Positive(s.AnalyteMl)) return m.Refuse("the volume in the beaker must be a positive number of mL");

            int acidA = PhModel.StrongAcidProtons(s.Analyte), baseA = PhModel.StrongBaseHydroxides(s.Analyte);
            int acidT = PhModel.StrongAcidProtons(s.Titrant), baseT = PhModel.StrongBaseHydroxides(s.Titrant);
            if (acidA == 0 && baseA == 0) return m.Refuse(NotModelled(s.Analyte));
            if (acidT == 0 && baseT == 0) return m.Refuse(NotModelled(s.Titrant));
            if ((acidA > 0) == (acidT > 0))
                return m.Refuse(acidA > 0 ? "both solutions are acids; titrate an acid with a base"
                                          : "both solutions are bases; titrate a base with an acid");

            m.AcidInBeaker = acidA > 0;
            m.analyteEquivalents = (m.AcidInBeaker ? acidA : baseA) * s.AnalyteMolar * s.AnalyteMl / 1000.0;
            m.titrantPerFormula = m.AcidInBeaker ? baseT : acidT;
            m.EquivalenceMl = m.analyteEquivalents / (m.titrantPerFormula * s.TitrantMolar) * 1000.0;
            m.Supported = true;
            return m;
        }

        /// <summary>The point after adding a volume of titrant (mL). Unsupported setups give no point.</summary>
        public bool TryPointAt(double addedMl, out TitrationPoint point, out string error)
        {
            point = default;
            error = string.Empty;
            if (!Supported) { error = Reason; return false; }
            if (double.IsNaN(addedMl) || double.IsInfinity(addedMl) || addedMl < 0)
            {
                error = "the added volume must be zero or a positive number of mL";
                return false;
            }

            double titrantEquivalents = titrantPerFormula * Setup.TitrantMolar * addedMl / 1000.0;
            double litres = (Setup.AnalyteMl + addedMl) / 1000.0;
            double netAcid = (AcidInBeaker ? analyteEquivalents - titrantEquivalents
                                           : titrantEquivalents - analyteEquivalents) / litres;
            point = new TitrationPoint
            {
                AddedMl = addedMl,
                Ph = PhModel.PhFromNetStrongAcid(netAcid),
                Approximate = Math.Abs(netAcid) > DiluteLimitM,
                Region = RegionAt(addedMl)
            };
            return true;
        }

        public TitrationRegion RegionAt(double addedMl)
        {
            if (addedMl <= 0) return TitrationRegion.Start;
            if (Math.Abs(addedMl - EquivalenceMl) <= EquivalenceToleranceMl) return TitrationRegion.Equivalence;
            return addedMl < EquivalenceMl ? TitrationRegion.BeforeEquivalence : TitrationRegion.AfterEquivalence;
        }

        /// <summary>The model curve from 0 to maxMl, sampled every stepMl (plus the equivalence point).</summary>
        public List<TitrationPoint> Curve(double maxMl, double stepMl)
        {
            var points = new List<TitrationPoint>();
            if (!Supported || !(stepMl > 0) || !(maxMl > 0)) return points;
            bool eqAdded = false;
            for (double v = 0; v <= maxMl + 1e-9; v += stepMl)
            {
                if (!eqAdded && v > EquivalenceMl && EquivalenceMl <= maxMl)
                {
                    if (TryPointAt(EquivalenceMl, out var eq, out _)) points.Add(eq);
                    eqAdded = true;
                }
                if (TryPointAt(v, out var p, out _)) points.Add(p);
                if (Math.Abs(v - EquivalenceMl) <= EquivalenceToleranceMl) eqAdded = true;
            }
            return points;
        }

        private TitrationModel Refuse(string why)
        {
            Supported = false;
            Reason = why;
            return this;
        }

        private static bool Positive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;

        private static string NotModelled(string formula)
        {
            if (formula == "H2SO4") return "H2SO4 releases its second H+ only partly (Ka2); that titration curve is not modelled";
            if (formula == "CH3COOH") return "CH3COOH is a weak acid; its curve needs a buffer model, which is not implemented";
            return $"{formula} is not a strong acid or strong base, so its titration curve is not modelled";
        }
    }
}
