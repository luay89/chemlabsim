// ChemLabSim v3 — acid–base indicators for the titration (Stage 6)
// Transition ranges and colours from the CRC Handbook of Chemistry and Physics, "Acid–Base
// Indicators" table:
//   methyl orange      pH 3.1 – 4.4   red → yellow-orange
//   bromothymol blue   pH 6.0 – 7.6   yellow → blue
//   phenolphthalein    pH 8.2 – 10.0  colourless → pink
// Inside the range the colour is blended linearly (an illustration: the real change follows the
// indicator's own acid–base ratio, which looks gradual to the eye across the same range).

using System;

namespace ChemLabSimV3.Engine.Chemistry
{
    public struct IndicatorEntry
    {
        public string Name;
        public double LowPh, HighPh;
        public string AcidColourName, BaseColourName;
        public float[] AcidRgba, BaseRgba;   // illustrative sRGB + alpha (colourless = faint)
    }

    public static class Indicators
    {
        public const string Source = "CRC Handbook of Chemistry and Physics, Acid–Base Indicators";

        public static readonly IndicatorEntry[] All =
        {
            new IndicatorEntry
            {
                Name = "phenolphthalein", LowPh = 8.2, HighPh = 10.0,
                AcidColourName = "colourless", BaseColourName = "pink",
                AcidRgba = new[] { 0.82f, 0.90f, 1.00f, 0.30f }, BaseRgba = new[] { 0.95f, 0.25f, 0.62f, 0.70f }
            },
            new IndicatorEntry
            {
                Name = "bromothymol blue", LowPh = 6.0, HighPh = 7.6,
                AcidColourName = "yellow", BaseColourName = "blue",
                AcidRgba = new[] { 0.95f, 0.85f, 0.15f, 0.65f }, BaseRgba = new[] { 0.15f, 0.35f, 0.95f, 0.70f }
            },
            new IndicatorEntry
            {
                Name = "methyl orange", LowPh = 3.1, HighPh = 4.4,
                AcidColourName = "red", BaseColourName = "yellow-orange",
                AcidRgba = new[] { 0.90f, 0.12f, 0.12f, 0.70f }, BaseRgba = new[] { 1.00f, 0.70f, 0.15f, 0.65f }
            },
        };

        /// <summary>0 at or below the acid end of the range, 1 at or above the base end.</summary>
        public static double BaseFraction(IndicatorEntry ind, double ph)
        {
            if (double.IsNaN(ph)) return double.NaN;
            return Math.Max(0.0, Math.Min(1.0, (ph - ind.LowPh) / (ind.HighPh - ind.LowPh)));
        }

        public static float[] ColourAt(IndicatorEntry ind, double ph)
        {
            double t = BaseFraction(ind, ph);
            if (double.IsNaN(t)) return null;
            var c = new float[4];
            for (int i = 0; i < 4; i++) c[i] = (float)(ind.AcidRgba[i] + (ind.BaseRgba[i] - ind.AcidRgba[i]) * t);
            return c;
        }

        public static string ColourNameAt(IndicatorEntry ind, double ph)
        {
            if (double.IsNaN(ph)) return "unknown";
            if (ph <= ind.LowPh) return ind.AcidColourName;
            if (ph >= ind.HighPh) return ind.BaseColourName;
            return $"changing ({ind.AcidColourName} → {ind.BaseColourName})";
        }
    }
}
