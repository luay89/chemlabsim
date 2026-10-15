// ChemLabSim v3 — amounts of a mix → moles → limiting reactant and yield
// Converts every ReagentAmountInput with QuantityConverter, then runs LimitingReactant.
// If any amount cannot be converted (missing concentration, wrong unit …) the evaluation
// stops there and says which reagent and why — the limiting reactant is never computed
// from partial data.

using System.Collections.Generic;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Engine.Chemistry
{
    public struct AmountLine
    {
        public ReagentAmountInput Input;
        public MolesResult Moles;
    }

    public class AmountEvaluation
    {
        public List<AmountLine> Lines = new List<AmountLine>();
        public LimitingReactantResult Stoichiometry = new LimitingReactantResult
        {
            Status = StoichiometryStatus.InsufficientData
        };

        public bool Success => Stoichiometry != null && Stoichiometry.Success;
    }

    public static class AmountEvaluator
    {
        public static AmountEvaluation Evaluate(ReactionEntry reaction, IList<ReagentAmountInput> amounts)
        {
            var evaluation = new AmountEvaluation();
            if (amounts == null || amounts.Count == 0)
            {
                evaluation.Stoichiometry.Message = "No amounts were given.";
                return evaluation;
            }

            var moles = new Dictionary<string, double>();
            string firstProblem = null;
            foreach (var input in amounts)
            {
                MolesResult converted;
                if (!TryUnit(input.Unit, out var unit))
                    converted = MolesResult.Fail(ConversionStatus.UnsupportedUnit, $"Unknown unit '{input.Unit}'.");
                else if (!Quantity.TryCreate(input.Value, unit, out var quantity, out string error))
                    converted = MolesResult.Fail(ConversionStatus.InvalidQuantity, $"{input.Formula}: {error}");
                else
                    converted = QuantityConverter.ToMoles(quantity, ToSpec(input));

                evaluation.Lines.Add(new AmountLine { Input = input, Moles = converted });
                if (converted.Success) moles[input.Formula] = converted.Moles;
                else if (firstProblem == null) firstProblem = converted.Message;
            }

            if (firstProblem != null)
            {
                evaluation.Stoichiometry = new LimitingReactantResult
                {
                    Status = StoichiometryStatus.InsufficientData,
                    Message = firstProblem
                };
                return evaluation;
            }

            evaluation.Stoichiometry = LimitingReactant.Calculate(reaction, moles);
            return evaluation;
        }

        /// <summary>Intrinsic and configured properties from the request; 0 becomes "unknown" (NaN).</summary>
        public static SubstanceSpec ToSpec(ReagentAmountInput input)
        {
            var spec = SubstanceSpec.For(input.Formula, SubstanceSpec.ParseState(input.State));
            spec.DensityGPerMl = input.DensityGPerMl > 0 ? input.DensityGPerMl : double.NaN;
            spec.StockConcentrationMolPerL = input.StockConcentrationM > 0 ? input.StockConcentrationM : double.NaN;
            return spec;
        }

        public static bool TryUnit(string symbol, out QuantityUnit unit)
        {
            switch ((symbol ?? string.Empty).Trim())
            {
                case "g": unit = QuantityUnit.Gram; return true;
                case "mL": unit = QuantityUnit.Milliliter; return true;
                case "mol": unit = QuantityUnit.Mole; return true;
                default: unit = QuantityUnit.Mole; return false;
            }
        }
    }
}
