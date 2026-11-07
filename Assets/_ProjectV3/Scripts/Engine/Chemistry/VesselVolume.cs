// ChemLabSim v3 — how much liquid is in a vessel, from the amounts the student gave
// Only volumes that follow from the data are reported:
//   solution or liquid measured in mL      that volume
//   solution measured in mol               V = n / c   (needs the bottle concentration)
//   pure liquid measured in g              V = m / ρ   (needs the density)
//   solid                                  adds no liquid volume (its own volume is not modelled)
//   gas                                    adds no liquid volume
// Anything else (no concentration, no density, unknown state, invalid amount) is "unknown", never
// a guess. A mixture's volume is the sum of the liquids' volumes — volumes taken as additive, the
// same assumption the pH and calorimetry models state. Precipitates, dissolved solids, gases and
// volume contraction are not modelled.

using System.Collections.Generic;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Engine.Chemistry
{
    public struct VolumeReading
    {
        public bool Known;
        public double Ml;        // valid when Known
        public string Basis;     // how it was obtained, or why it is unknown
        public bool HasLiquid => Known && Ml > 0;

        public static VolumeReading Of(double ml, string basis) => new VolumeReading { Known = true, Ml = ml, Basis = basis };
        public static VolumeReading Unknown(string why) => new VolumeReading { Known = false, Ml = double.NaN, Basis = why };
    }

    public static class VesselVolume
    {
        public static VolumeReading Of(ReagentAmountInput a)
        {
            string f = string.IsNullOrEmpty(a.Formula) ? "the reagent" : a.Formula;
            var state = SubstanceSpec.ParseState(a.State);
            bool valid = !double.IsNaN(a.Value) && !double.IsInfinity(a.Value) && a.Value > 0;

            switch (state)
            {
                case MatterState.Solid: return VolumeReading.Of(0, $"{f} is a solid (its volume is not counted)");
                case MatterState.Gas:   return VolumeReading.Of(0, $"{f} is a gas (no liquid volume)");
                case MatterState.Aqueous:
                case MatterState.Liquid:
                    if (!valid) return VolumeReading.Unknown($"the amount of {f} is not a valid number");
                    switch (a.Unit)
                    {
                        case "mL":
                            return VolumeReading.Of(a.Value, $"{f}: measured volume");
                        case "mol" when state == MatterState.Aqueous && a.StockConcentrationM > 0:
                            return VolumeReading.Of(a.Value / a.StockConcentrationM * 1000.0, $"{f}: V = n / c");
                        case "g" when state == MatterState.Liquid && a.DensityGPerMl > 0:
                            return VolumeReading.Of(a.Value / a.DensityGPerMl, $"{f}: V = m / ρ");
                        default:
                            return VolumeReading.Unknown($"the volume of {f} cannot be worked out from {a.Unit} without more data");
                    }
                default:
                    return VolumeReading.Unknown($"the state of {f} is not known");
            }
        }

        /// <summary>Total liquid volume of a mixture (volumes taken as additive); unknown if any part is.</summary>
        public static VolumeReading Total(IEnumerable<ReagentAmountInput> amounts)
        {
            double total = 0;
            int liquids = 0, others = 0;
            if (amounts == null) return VolumeReading.Unknown("no amounts were given");
            foreach (var a in amounts)
            {
                var v = Of(a);
                if (!v.Known) return v;
                if (v.Ml > 0) liquids++; else others++;
                total += v.Ml;
            }
            string basis = liquids > 1 ? "volumes taken as additive" : "measured volume";
            if (others > 0 && liquids > 0) basis += "; solid/gas volume not counted";
            return VolumeReading.Of(total, basis);
        }
    }
}
