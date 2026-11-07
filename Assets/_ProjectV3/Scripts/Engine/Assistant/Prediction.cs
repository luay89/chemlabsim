// ChemLabSim v3 — "Predict first": what actually happened, to compare with the student's guess
// Pure C# — no Unity dependencies. Three questions, each answered from the engine's data:
//   heat   the temperature change calculated from the amounts (Calorimetry) when there is one,
//          otherwise the sign of the sourced ΔH°, otherwise the record's observed ΔT;
//          "hardly any" below 1 K calculated, 5 kJ/mol ΔH° or 2 °C observed
//   solid  a product in the solid state (a precipitate, a deposited metal, a solid residue)
//   gas    a product in the gas state
// A reaction that did not run gives "hardly any" heat, no solid and no gas.

using System;
using System.Globalization;
using System.Linq;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Engine.Assistant
{
    public enum HeatAnswer { None = -1, GivesOut, TakesIn, HardlyAny }

    public struct PredictionAnswers
    {
        public HeatAnswer Heat;
        public bool? Solid;
        public bool? Gas;
        public bool Complete => Heat != HeatAnswer.None && Solid.HasValue && Gas.HasValue;
    }

    public struct PredictionOutcome
    {
        public bool Reacted;
        public HeatAnswer Heat;
        public string HeatWhy;
        public bool Solid;
        public string SolidWhy;
        public bool Gas;
        public string GasWhy;
    }

    public static class Prediction
    {
        public const double HardlyAnyRiseK = 1.0;
        public const double HardlyAnyEnthalpyKJ = 5.0;
        public const double HardlyAnyObservedC = 2.0;

        /// <summary>What happened, from the reaction the engine ran (null = none found).</summary>
        public static PredictionOutcome Outcome(ReactionEntry reaction, bool proceeded, double calculatedRiseK)
        {
            if (reaction == null || !proceeded)
            {
                string why = reaction == null ? "these substances do not react" : "the reaction did not start";
                return new PredictionOutcome
                {
                    Reacted = false,
                    Heat = HeatAnswer.HardlyAny, HeatWhy = why,
                    Solid = false, SolidWhy = why,
                    Gas = false, GasWhy = why,
                };
            }

            var o = new PredictionOutcome { Reacted = true };
            if (!double.IsNaN(calculatedRiseK))
            {
                o.Heat = Math.Abs(calculatedRiseK) < HardlyAnyRiseK ? HeatAnswer.HardlyAny
                       : calculatedRiseK > 0 ? HeatAnswer.GivesOut : HeatAnswer.TakesIn;
                o.HeatWhy = "calculated from your amounts: " + Signed(calculatedRiseK) + " °C";
            }
            else if (Calorimetry.HasSourcedEnthalpy(reaction))
            {
                double dh = reaction.enthalpyKJPerMol;
                o.Heat = Math.Abs(dh) < HardlyAnyEnthalpyKJ ? HeatAnswer.HardlyAny
                       : dh < 0 ? HeatAnswer.GivesOut : HeatAnswer.TakesIn;
                o.HeatWhy = "ΔH° = " + Signed(dh) + " kJ per mol of reaction";
            }
            else
            {
                double dt = reaction.visual_effects != null ? reaction.visual_effects.temperature_delta : 0.0;
                o.Heat = Math.Abs(dt) < HardlyAnyObservedC ? HeatAnswer.HardlyAny
                       : dt > 0 ? HeatAnswer.GivesOut : HeatAnswer.TakesIn;
                o.HeatWhy = "observed change about " + Signed(dt) + " °C";
            }

            var solid = reaction.products?.Where(p => p != null && p.state == "s").Select(p => p.formula).ToList();
            o.Solid = solid != null && solid.Count > 0;
            o.SolidWhy = o.Solid ? "solid product: " + string.Join(", ", solid) : "no product is a solid";

            var gas = reaction.products?.Where(p => p != null && p.state == "g").Select(p => p.formula).ToList();
            o.Gas = gas != null && gas.Count > 0;
            o.GasWhy = o.Gas ? "gas given off: " + string.Join(", ", gas) : "no product is a gas";
            return o;
        }

        /// <summary>How many of the three answers match (0–3).</summary>
        public static int Score(PredictionAnswers a, PredictionOutcome o)
            => (a.Heat == o.Heat ? 1 : 0) + (a.Solid == o.Solid ? 1 : 0) + (a.Gas == o.Gas ? 1 : 0);

        public static string Text(HeatAnswer h)
        {
            switch (h)
            {
                case HeatAnswer.GivesOut: return "gives out heat (warmer)";
                case HeatAnswer.TakesIn:  return "takes in heat (colder)";
                case HeatAnswer.HardlyAny: return "hardly any heat change";
                default: return "no answer";
            }
        }

        private static string Signed(double v)
            => (v > 0 ? "+" : v < 0 ? "−" : "") + Math.Abs(v).ToString(Math.Abs(v) >= 100 ? "0" : "0.#", CultureInfo.InvariantCulture);
    }
}
