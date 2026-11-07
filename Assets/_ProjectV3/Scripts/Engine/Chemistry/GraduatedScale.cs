// ChemLabSim v3 — the volume scale printed on a piece of glassware
// A vessel with a straight (cylindrical) inside has height ∝ volume, so one number — the height
// of the capacity mark — fixes the whole scale. The marks are generated from the capacity and
// steps, never placed by eye. Used by the 3D glassware (marks, numbers, liquid height).
//
//   beaker 100 mL            marks every 10 mL, numbers every 20 mL, "approx." (beakers are
//                            not measuring glassware)
//   graduated cylinder 50 mL marks every 1 mL, longer every 5 mL, numbers every 10 mL
//   burette 50 mL            marks every 1 mL, numbers every 10 mL, read from 0 at the top

using System;
using System.Collections.Generic;

namespace ChemLabSimV3.Engine.Chemistry
{
    public struct ScaleMark
    {
        public double Ml;         // the value printed at this mark (a burette counts down from the top)
        public double Height;     // height above the zero-volume level, in the vessel's units
        public bool Major;        // longer mark
        public bool Labelled;     // number printed next to it
    }

    public class GraduatedScale
    {
        public string Vessel { get; }
        public double CapacityMl { get; }
        public double MinorMl { get; }
        public double MajorMl { get; }
        public double LabelMl { get; }
        public double CapacityHeight { get; }   // height of the capacity mark above the zero level
        public bool Approximate { get; }        // beaker-style marks
        public bool TopDown { get; }            // burette: 0 at the top, capacity at the bottom

        public GraduatedScale(string vessel, double capacityMl, double minorMl, double majorMl, double labelMl,
                              double capacityHeight, bool approximate = false, bool topDown = false)
        {
            if (!(capacityMl > 0) || !(minorMl > 0) || !(majorMl > 0) || !(labelMl > 0) || !(capacityHeight > 0))
                throw new ArgumentException("Scale values must be positive.");
            Vessel = vessel;
            CapacityMl = capacityMl;
            MinorMl = minorMl;
            MajorMl = majorMl;
            LabelMl = labelMl;
            CapacityHeight = capacityHeight;
            Approximate = approximate;
            TopDown = topDown;
        }

        public static GraduatedScale Beaker100(double capacityHeight)
            => new GraduatedScale("100 mL beaker", 100, 10, 20, 20, capacityHeight, approximate: true);

        public static GraduatedScale Cylinder50(double capacityHeight)
            => new GraduatedScale("50 mL graduated cylinder", 50, 1, 5, 10, capacityHeight);

        public static GraduatedScale Burette50(double capacityHeight)
            => new GraduatedScale("50 mL burette", 50, 1, 5, 10, capacityHeight, topDown: true);

        /// <summary>Every mark from the first step to the capacity (no mark at zero).</summary>
        public List<ScaleMark> Marks()
        {
            var marks = new List<ScaleMark>();
            int count = (int)Math.Round(CapacityMl / MinorMl);
            for (int i = TopDown ? 0 : 1; i <= count; i++)
            {
                double v = i * MinorMl;   // a burette's printed value is the volume run out from the top
                double height = TopDown ? CapacityHeight * (1.0 - v / CapacityMl) : CapacityHeight * v / CapacityMl;
                marks.Add(new ScaleMark
                {
                    Ml = v,
                    Height = height,
                    Major = IsMultiple(v, MajorMl),
                    Labelled = IsMultiple(v, LabelMl)
                });
            }
            return marks;
        }

        /// <summary>
        /// Height of the liquid surface holding this volume above the zero level (for a burette the
        /// zero level is its capacity mark, so pass the volume still inside); NaN for an invalid volume.
        /// </summary>
        public double HeightOf(double volumeMl)
        {
            if (double.IsNaN(volumeMl) || double.IsInfinity(volumeMl) || volumeMl < 0) return double.NaN;
            return CapacityHeight * volumeMl / CapacityMl;
        }

        public bool Exceeds(double volumeMl) => volumeMl > CapacityMl + 1e-9;

        private static bool IsMultiple(double v, double step)
        {
            double k = v / step;
            return Math.Abs(k - Math.Round(k)) < 1e-6;
        }
    }
}
