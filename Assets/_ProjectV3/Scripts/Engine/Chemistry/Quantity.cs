// ChemLabSim v3 — Quantities and conversion to amount of substance (mol)
//
//   g   (solid or pure liquid)  n = m / M                 needs molar mass M
//   mL  (solution, "aq")        n = c · V / 1000          needs the bottle's concentration c (mol/L)
//   mL  (pure liquid)           n = ρ · V / M             needs density ρ (g/mL) and M
//   mol (any state)             n = value
//
// Anything else is refused with a reason instead of being guessed: grams of a *solution*
// (the solute mass is unknown without its concentration and density), the volume of a
// *powder*, and gas volumes (no gas-amount model yet). Missing data gives an explicit
// "missing" status — values are never assumed.
// Intrinsic properties (M, ρ) and configured bottle properties (c) are kept apart in
// SubstanceSpec so the caller always knows which kind of value was used.

using System;

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum QuantityUnit { Gram, Milliliter, Mole }

    public enum MatterState { Unknown, Solid, Liquid, Aqueous, Gas }

    public enum ConversionStatus
    {
        Ok,
        InvalidQuantity,       // ≤ 0, NaN or infinite
        MissingMolarMass,
        MissingDensity,
        MissingConcentration,
        UnsupportedUnit,       // the unit does not make sense for this state
        UnknownState
    }

    /// <summary>A measured amount, validated on creation.</summary>
    public readonly struct Quantity
    {
        public readonly double Value;
        public readonly QuantityUnit Unit;

        private Quantity(double value, QuantityUnit unit) { Value = value; Unit = unit; }

        public static bool TryCreate(double value, QuantityUnit unit, out Quantity quantity, out string error)
        {
            quantity = default;
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                error = "The amount is not a number.";
                return false;
            }
            if (value <= 0.0)
            {
                error = "The amount must be greater than zero.";
                return false;
            }
            if (!Enum.IsDefined(typeof(QuantityUnit), unit))
            {
                error = "Unknown unit.";
                return false;
            }
            quantity = new Quantity(value, unit);
            error = string.Empty;
            return true;
        }

        public static string Symbol(QuantityUnit unit)
            => unit == QuantityUnit.Gram ? "g" : unit == QuantityUnit.Milliliter ? "mL" : "mol";

        public override string ToString() => $"{Value:0.###} {Symbol(Unit)}";
    }

    /// <summary>What is known about one substance. NaN means "not known" — never a default.</summary>
    public struct SubstanceSpec
    {
        public string Formula;
        public MatterState State;

        // Intrinsic properties of the substance.
        public double MolarMassGPerMol;       // from the formula (MolarMass) or NaN
        public double DensityGPerMl;          // pure liquids only, from a cited source, or NaN

        // Configured property of the bottle on the shelf (not of the substance).
        public double StockConcentrationMolPerL;   // solutions only, or NaN

        public static SubstanceSpec For(string formula, MatterState state)
        {
            var mm = MolarMass.Of(formula);
            return new SubstanceSpec
            {
                Formula = formula,
                State = state,
                MolarMassGPerMol = mm.Success ? mm.GramsPerMole : double.NaN,
                DensityGPerMl = double.NaN,
                StockConcentrationMolPerL = double.NaN
            };
        }

        /// <summary>"solid"/"s", "liquid"/"l", "aqueous"/"aq", "gas"/"g" (case-insensitive).</summary>
        public static MatterState ParseState(string state)
        {
            switch ((state ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "solid": case "s": return MatterState.Solid;
                case "liquid": case "l": return MatterState.Liquid;
                case "aqueous": case "aq": return MatterState.Aqueous;
                case "gas": case "g": return MatterState.Gas;
                default: return MatterState.Unknown;
            }
        }
    }

    public struct MolesResult
    {
        public ConversionStatus Status;
        public double Moles;      // NaN unless Status == Ok
        public string Message;

        public bool Success => Status == ConversionStatus.Ok;

        public static MolesResult Ok(double n) => new MolesResult { Status = ConversionStatus.Ok, Moles = n, Message = string.Empty };
        public static MolesResult Fail(ConversionStatus s, string why) => new MolesResult { Status = s, Moles = double.NaN, Message = why };
    }

    public static class QuantityConverter
    {
        /// <summary>Units that make sense for a state (the UI offers only these).</summary>
        public static QuantityUnit[] UnitsFor(MatterState state)
        {
            switch (state)
            {
                case MatterState.Solid:   return new[] { QuantityUnit.Gram, QuantityUnit.Mole };
                case MatterState.Liquid:  return new[] { QuantityUnit.Milliliter, QuantityUnit.Gram, QuantityUnit.Mole };
                case MatterState.Aqueous: return new[] { QuantityUnit.Milliliter, QuantityUnit.Mole };
                case MatterState.Gas:     return new[] { QuantityUnit.Mole };
                default:                     return new QuantityUnit[0];
            }
        }

        public static MolesResult ToMoles(Quantity q, SubstanceSpec s)
        {
            if (double.IsNaN(q.Value) || double.IsInfinity(q.Value) || q.Value <= 0.0)
                return MolesResult.Fail(ConversionStatus.InvalidQuantity, "The amount must be a positive number.");
            if (s.State == MatterState.Unknown)
                return MolesResult.Fail(ConversionStatus.UnknownState, $"The state of {s.Formula} is unknown.");
            if (Array.IndexOf(UnitsFor(s.State), q.Unit) < 0)
                return MolesResult.Fail(ConversionStatus.UnsupportedUnit, UnsupportedReason(q.Unit, s));

            switch (q.Unit)
            {
                case QuantityUnit.Mole:
                    return MolesResult.Ok(q.Value);

                case QuantityUnit.Gram:
                    if (!Known(s.MolarMassGPerMol))
                        return MolesResult.Fail(ConversionStatus.MissingMolarMass, $"The molar mass of {s.Formula} is not available.");
                    return MolesResult.Ok(q.Value / s.MolarMassGPerMol);

                default:   // Milliliter
                    if (s.State == MatterState.Aqueous)
                    {
                        if (!Known(s.StockConcentrationMolPerL))
                            return MolesResult.Fail(ConversionStatus.MissingConcentration,
                                $"The concentration of the {s.Formula} solution is not set, so its volume cannot be turned into moles.");
                        return MolesResult.Ok(s.StockConcentrationMolPerL * q.Value / 1000.0);
                    }
                    if (!Known(s.DensityGPerMl))
                        return MolesResult.Fail(ConversionStatus.MissingDensity, $"The density of liquid {s.Formula} is not available.");
                    if (!Known(s.MolarMassGPerMol))
                        return MolesResult.Fail(ConversionStatus.MissingMolarMass, $"The molar mass of {s.Formula} is not available.");
                    return MolesResult.Ok(s.DensityGPerMl * q.Value / s.MolarMassGPerMol);
            }
        }

        private static bool Known(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0.0;

        private static string UnsupportedReason(QuantityUnit unit, SubstanceSpec s)
        {
            if (s.State == MatterState.Aqueous && unit == QuantityUnit.Gram)
                return $"{s.Formula} is a solution: measure it in mL (the mass of solution is not the mass of {s.Formula}).";
            if (s.State == MatterState.Solid && unit == QuantityUnit.Milliliter)
                return $"{s.Formula} is a solid: weigh it in g (the volume of a powder depends on how it is packed).";
            if (s.State == MatterState.Gas)
                return $"{s.Formula} is a gas: only an amount in mol is supported (no gas-volume model yet).";
            return $"{Quantity.Symbol(unit)} is not a valid unit for {s.Formula}.";
        }
    }
}
