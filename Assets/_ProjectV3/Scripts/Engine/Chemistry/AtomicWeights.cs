// ChemLabSim v3 — Standard atomic weights
// Source: IUPAC Commission on Isotopic Abundances and Atomic Weights (CIAAW),
//         "Standard atomic weights of the elements 2021", abridged / conventional values
//         (Pure Appl. Chem. 2022, 94(5), 573–600; https://www.ciaaw.org/atomic-weights.htm).
// Values are in g/mol. Elements without a standard atomic weight (no stable isotope with a
// characteristic terrestrial composition: Tc, Pm, Po, At, Rn, Fr, Ra, Ac and elements after U
// except Th, Pa) are deliberately absent, so a molar mass is never invented for them.

using System.Collections.Generic;

namespace ChemLabSimV3.Engine.Chemistry
{
    public static class AtomicWeights
    {
        public const string Source = "IUPAC CIAAW standard atomic weights 2021 (abridged)";

        private static readonly Dictionary<string, double> Weights = new Dictionary<string, double>(System.StringComparer.Ordinal)
        {
            { "H", 1.008 },    { "He", 4.0026 },  { "Li", 6.94 },    { "Be", 9.0122 },  { "B", 10.81 },
            { "C", 12.011 },   { "N", 14.007 },   { "O", 15.999 },   { "F", 18.998 },   { "Ne", 20.180 },
            { "Na", 22.990 },  { "Mg", 24.305 },  { "Al", 26.982 },  { "Si", 28.085 },  { "P", 30.974 },
            { "S", 32.06 },    { "Cl", 35.45 },   { "Ar", 39.95 },   { "K", 39.098 },   { "Ca", 40.078 },
            { "Sc", 44.956 },  { "Ti", 47.867 },  { "V", 50.942 },   { "Cr", 51.996 },  { "Mn", 54.938 },
            { "Fe", 55.845 },  { "Co", 58.933 },  { "Ni", 58.693 },  { "Cu", 63.546 },  { "Zn", 65.38 },
            { "Ga", 69.723 },  { "Ge", 72.630 },  { "As", 74.922 },  { "Se", 78.971 },  { "Br", 79.904 },
            { "Kr", 83.798 },  { "Rb", 85.468 },  { "Sr", 87.62 },   { "Y", 88.906 },   { "Zr", 91.224 },
            { "Nb", 92.906 },  { "Mo", 95.95 },   { "Ru", 101.07 },  { "Rh", 102.91 },  { "Pd", 106.42 },
            { "Ag", 107.87 },  { "Cd", 112.41 },  { "In", 114.82 },  { "Sn", 118.71 },  { "Sb", 121.76 },
            { "Te", 127.60 },  { "I", 126.90 },   { "Xe", 131.29 },  { "Cs", 132.91 },  { "Ba", 137.33 },
            { "La", 138.91 },  { "Ce", 140.12 },  { "Pr", 140.91 },  { "Nd", 144.24 },  { "Sm", 150.36 },
            { "Eu", 151.96 },  { "Gd", 157.25 },  { "Tb", 158.93 },  { "Dy", 162.50 },  { "Ho", 164.93 },
            { "Er", 167.26 },  { "Tm", 168.93 },  { "Yb", 173.05 },  { "Lu", 174.97 },  { "Hf", 178.49 },
            { "Ta", 180.95 },  { "W", 183.84 },   { "Re", 186.21 },  { "Os", 190.23 },  { "Ir", 192.22 },
            { "Pt", 195.08 },  { "Au", 196.97 },  { "Hg", 200.59 },  { "Tl", 204.38 },  { "Pb", 207.2 },
            { "Bi", 208.98 },  { "Th", 232.04 },  { "Pa", 231.04 },  { "U", 238.03 },
        };

        /// <summary>Standard atomic weight in g/mol; false when the element has none (or is not an element).</summary>
        public static bool TryGet(string symbol, out double gramsPerMole)
        {
            gramsPerMole = double.NaN;
            return symbol != null && Weights.TryGetValue(symbol, out gramsPerMole);
        }
    }
}
