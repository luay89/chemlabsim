// ChemLabSim v3 — heating a pure substance: sensible heat, melting, boiling, cooling
// A physical change only: the substance stays the same; no reaction is involved.
//
// Energy balance per step (open beaker, 101.325 kPa):
//   Q = (P − k·(T − T_room)) · Δt        P = heater power reaching the sample (student's choice)
//                                          k = heat-loss coefficient (illustrative model value)
//   Q goes, in order, into: melting at the melting point (Q = m·ΔH_fus), warming the liquid
//   (Q = m·c·ΔT), boiling at the boiling point (Q = m·ΔH_vap; the vapour leaves the open beaker).
//   While a phase change is under way the temperature stays at the transition temperature.
//   With the heater off (Q < 0) the liquid cools towards room temperature; it freezes only if
//   room temperature is below the melting point.
// Assumptions (shown to the student): pure substance; constant heat capacity; the beaker's own
// heat capacity is ignored; the temperature is uniform; evaporation below the boiling point is
// NOT modelled (no mass is lost before boiling); the vapour does not condense back.
// Data comes only from MaterialThermalData (materials.json); missing data → unsupported.

using System;

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum SamplePhase { Solid, Melting, Liquid, Boiling, Freezing, Empty }

    public class HeatingSample
    {
        public string Formula;
        public MaterialThermalData Data;
        public double SolidG, LiquidG, VaporizedG;
        public double TemperatureC;
        public double HeatInJ, HeatLostJ, TimeS;
        public SamplePhase Phase;
        public double LiquidDensityGPerMl;

        public double TotalG => SolidG + LiquidG;
        public double LiquidMl => LiquidDensityGPerMl > 0 ? LiquidG / LiquidDensityGPerMl : double.NaN;
        public double SolidMl => Data != null && Data.solidDensityGPerMl > 0 ? SolidG / Data.solidDensityGPerMl : double.NaN;
    }

    public static class PhaseHeatingModel
    {
        public const double ReferencePressureKPa = 101.325;
        public const double RoomTemperatureC = 25.0;
        public const double DefaultHeatLossWPerK = 0.2;   // illustrative: an uninsulated 100 mL beaker
        private const double MaxStepS = 0.05;
        private const double Eps = 1e-9;

        /// <summary>Whether a material can be heated in this model, and if not, why.</summary>
        public static bool IsSupported(ChemicalMaterial mat, out string why)
        {
            var d = mat?.thermal;
            if (mat == null) { why = "choose a substance as reagent A"; return false; }
            if (d == null || !d.available)
            { why = $"no sourced heat-capacity and latent-heat data for {mat.formula} in this lab"; return false; }
            if (!(d.heatCapacityLiquidJPerGK > 0) || !(d.enthalpyVaporizationJPerG > 0))
            { why = $"the liquid data for {mat.formula} is incomplete"; return false; }
            if (!(mat.densityGPerMl > 0)) { why = $"no density for {mat.formula}, so its volume cannot be shown"; return false; }
            why = string.Empty;
            return true;
        }

        /// <summary>Whether this material can start frozen at its melting point (needs ΔH_fus and the solid's density).</summary>
        public static bool CanStartFrozen(ChemicalMaterial mat)
            => mat?.thermal != null && mat.thermal.available && mat.thermal.enthalpyFusionJPerG > 0
               && mat.thermal.solidDensityGPerMl > 0;

        /// <summary>
        /// A sample of <paramref name="massG"/> grams at <paramref name="startC"/>; frozen = all solid at the
        /// melting point. Returns null (with the reason) when the start is outside what the data covers.
        /// </summary>
        public static HeatingSample Create(ChemicalMaterial mat, double massG, double startC, bool frozen, out string why)
        {
            if (!IsSupported(mat, out why)) return null;
            var d = mat.thermal;
            if (double.IsNaN(massG) || double.IsInfinity(massG) || massG <= 0) { why = "the amount must be a positive number"; return null; }
            if (double.IsNaN(startC) || double.IsInfinity(startC)) { why = "the starting temperature is not a number"; return null; }
            var s = new HeatingSample { Formula = mat.formula, Data = d, LiquidDensityGPerMl = mat.densityGPerMl };
            if (frozen)
            {
                if (!CanStartFrozen(mat)) { why = $"no data for solid {mat.formula}"; return null; }
                s.SolidG = massG;
                s.TemperatureC = d.meltingPointC;
                s.Phase = SamplePhase.Solid;
                return s;
            }
            if (startC < d.meltingPointC) { why = $"below {d.meltingPointC:0.##} °C {mat.formula} is solid, and no data for the solid is available"; return null; }
            if (startC > d.boilingPointC) { why = $"above {d.boilingPointC:0.##} °C {mat.formula} would be a gas at {ReferencePressureKPa} kPa"; return null; }
            s.LiquidG = massG;
            s.TemperatureC = startC;
            s.Phase = SamplePhase.Liquid;
            return s;
        }

        /// <summary>Advances the sample by dtS seconds. powerW ≥ 0 (0 = heater off).</summary>
        public static void Step(HeatingSample s, double dtS, double powerW, double heatLossWPerK = DefaultHeatLossWPerK,
                                double roomC = RoomTemperatureC)
        {
            if (s == null || s.Phase == SamplePhase.Empty) return;
            if (double.IsNaN(dtS) || dtS <= 0 || double.IsInfinity(dtS)) return;
            if (double.IsNaN(powerW) || powerW < 0) powerW = 0;
            while (dtS > Eps && s.Phase != SamplePhase.Empty)
            {
                double h = Math.Min(dtS, MaxStepS);
                dtS -= h;
                double lossJ = heatLossWPerK * (s.TemperatureC - roomC) * h;
                // Exchange with the room can at most bring the liquid to room temperature in one step:
                // without this cap a few milligrams of liquid (nearly boiled dry) overshoot and diverge.
                if (s.SolidG <= Eps && s.LiquidG > Eps)
                {
                    double toRoomJ = Math.Abs(s.TemperatureC - roomC) * s.LiquidG * s.Data.heatCapacityLiquidJPerGK;
                    lossJ = Math.Max(-toRoomJ, Math.Min(toRoomJ, lossJ));
                }
                s.HeatInJ += powerW * h;
                s.HeatLostJ += lossJ;
                s.TimeS += h;
                Apply(s, powerW * h - lossJ, powerW > 0);
            }
        }

        private static void Apply(HeatingSample s, double q, bool heaterOn)
        {
            var d = s.Data;
            if (q > 0)
            {
                // Melting
                if (s.SolidG > Eps)
                {
                    double melt = Math.Min(s.SolidG, q / d.enthalpyFusionJPerG);
                    s.SolidG -= melt;
                    s.LiquidG += melt;
                    q -= melt * d.enthalpyFusionJPerG;
                    s.TemperatureC = d.meltingPointC;
                    s.Phase = s.SolidG > Eps ? SamplePhase.Melting : SamplePhase.Liquid;
                    if (s.SolidG <= Eps) s.SolidG = 0;
                    if (q <= Eps) return;
                }
                // Warming the liquid up to the boiling point
                if (s.LiquidG > Eps && s.TemperatureC < d.boilingPointC)
                {
                    double toBoil = s.LiquidG * d.heatCapacityLiquidJPerGK * (d.boilingPointC - s.TemperatureC);
                    if (q < toBoil)
                    {
                        s.TemperatureC += q / (s.LiquidG * d.heatCapacityLiquidJPerGK);
                        s.Phase = SamplePhase.Liquid;
                        return;
                    }
                    q -= toBoil;
                    s.TemperatureC = d.boilingPointC;
                }
                // Boiling: latent heat, the temperature stays at the boiling point
                if (s.LiquidG > Eps)
                {
                    double boil = Math.Min(s.LiquidG, q / d.enthalpyVaporizationJPerG);
                    s.LiquidG -= boil;
                    s.VaporizedG += boil;
                    s.Phase = SamplePhase.Boiling;
                }
                if (s.LiquidG <= Eps && s.SolidG <= Eps)
                {
                    s.LiquidG = 0;
                    s.Phase = SamplePhase.Empty;   // nothing left to heat; an empty beaker is not modelled
                }
                return;
            }

            // Net heat loss: the liquid cools; boiling stops at once.
            if (s.LiquidG > Eps)
            {
                double c = s.LiquidG * d.heatCapacityLiquidJPerGK;
                double t = s.TemperatureC + q / c;
                bool canFreeze = d.enthalpyFusionJPerG > 0 && d.solidDensityGPerMl > 0;
                if (t <= d.meltingPointC && canFreeze)
                {
                    double left = q + (s.TemperatureC - d.meltingPointC) * c;   // still negative
                    double freeze = Math.Min(s.LiquidG, -left / d.enthalpyFusionJPerG);
                    s.LiquidG -= freeze;
                    s.SolidG += freeze;
                    s.TemperatureC = d.meltingPointC;
                    s.Phase = s.LiquidG > Eps ? SamplePhase.Freezing : SamplePhase.Solid;
                    return;
                }
                s.TemperatureC = Math.Max(t, d.meltingPointC);
                s.Phase = SamplePhase.Liquid;
                return;
            }
            // Only solid left and it is losing heat: with no heat capacity for the solid it stays at the melting point.
            if (s.SolidG > Eps) s.Phase = SamplePhase.Solid;
        }

        /// <summary>
        /// Whether the sample still changes with the heater in this state: heating, or (heater off)
        /// moving towards room temperature, or ice melting from the room's heat.
        /// </summary>
        public static bool NeedsStepping(HeatingSample s, bool heaterOn, double roomC = RoomTemperatureC)
        {
            if (s == null || s.Phase == SamplePhase.Empty) return false;
            if (heaterOn) return true;
            if (s.SolidG > Eps && roomC > s.Data.meltingPointC) return true;
            return s.LiquidG > Eps && Math.Abs(s.TemperatureC - roomC) > 0.01;
        }

        /// <summary>Plain-English description of the current state (what happens and why).</summary>
        public static string Explain(HeatingSample s, bool heaterOn)
        {
            if (s == null) return string.Empty;
            var d = s.Data;
            string f = s.Formula;
            switch (s.Phase)
            {
                case SamplePhase.Solid:
                    return $"Solid {f} at its melting point ({d.meltingPointC:0.##} °C). Heat will melt it before the temperature rises.";
                case SamplePhase.Melting:
                    return $"Melting: heat breaks up the solid (latent heat of fusion {d.enthalpyFusionJPerG:0.#} J/g), " +
                           $"so the temperature stays at {d.meltingPointC:0.##} °C until all the solid has melted. Physical change: it is still {f}.";
                case SamplePhase.Boiling:
                    return $"Boiling at {d.boilingPointC:0.##} °C ({d.referencePressureKPa:0.###} kPa assumed): bubbles of vapour form inside the liquid. " +
                           $"The heat goes into vaporisation (latent heat {d.enthalpyVaporizationJPerG:0.#} J/g), so the temperature stays constant while the liquid boils away. Physical change: the vapour is still {f}.";
                case SamplePhase.Freezing:
                    return $"Freezing at {d.meltingPointC:0.##} °C: the liquid gives out its latent heat as it turns solid.";
                case SamplePhase.Empty:
                    return $"All the liquid has boiled away. Heating an empty beaker is not modelled, so heating stopped.";
                default:
                    if (heaterOn)
                        return $"Heating the liquid: Q = m·c·ΔT with c = {d.heatCapacityLiquidJPerGK:0.###} J/(g·K). " +
                               $"Below {d.boilingPointC:0.##} °C some molecules still escape from the surface (evaporation), " +
                               "but its rate is not modelled here, so no volume is lost until it boils.";
                    if (s.TemperatureC > RoomTemperatureC + 0.05)
                        return $"Heater off: the liquid cools towards room temperature ({RoomTemperatureC:0} °C) as it loses heat to the surroundings (cooling rate illustrative).";
                    if (s.TemperatureC < RoomTemperatureC - 0.05)
                        return $"Heater off: the liquid warms towards room temperature ({RoomTemperatureC:0} °C), taking heat from the surroundings (rate illustrative).";
                    return $"Liquid {f} at room temperature.";
            }
        }

        public const string Assumptions =
            "Assumptions: 101.325 kPa; pure substance; constant heat capacity; uniform temperature; the beaker's own heat capacity ignored; " +
            "liquid volume shown at its 25 °C density (thermal expansion not modelled); " +
            "heat loss k·(T − 25 °C) with an illustrative k = 0.2 W/K; evaporation below the boiling point not modelled; vapour leaves the open beaker.";
    }
}
