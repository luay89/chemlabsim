// ChemLabSim v3 — flame test for metal ions (Stage 6)
//
// A salt on a clean wire loop is held in a hot non-luminous flame; some metal ions give the
// flame a characteristic colour. The table below is the standard qualitative-analysis table:
//   colours   Vogel's Textbook of Macro and Semimicro Qualitative Inorganic Analysis (5th ed.),
//             "dry tests: flame tests"
//   lines     NIST Atomic Spectra Database (strong lines, nm); for Ca, Sr, Ba and Cu most of the
//             visible colour comes from molecular bands (MOH / MCl) in the flame, so the band
//             region is given instead of a single line.
// Fidelity: the on-screen colour is an illustrative sRGB rendering of the described colour —
// real flame colours depend on the flame, the anion and impurities (traces of sodium mask
// most other colours; potassium is viewed through cobalt-blue glass for that reason).
//
// Results:
//   Characteristic     one flame-colouring metal ion (others in the formula give no colour)
//   NoColour           only metal ions that give no characteristic colour (Mg, Al, Zn)
//   Unsupported        a pure metal (tests use a salt), no metal ion, more than one colouring
//                      ion, or a metal that is not in this table — never a guessed colour

using System.Collections.Generic;
using System.Linq;

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum FlameTestStatus { Characteristic, NoColour, Unsupported }

    public struct FlameColourEntry
    {
        public string Element;
        public string Ion;
        public string ColourName;
        public string Emission;      // what produces the colour
        public float R, G, B;        // illustrative sRGB
    }

    public struct FlameTestResult
    {
        public FlameTestStatus Status;
        public string Formula;
        public FlameColourEntry Entry;   // valid when Status == Characteristic
        public string Explanation;       // what the student reads (also the reason when unsupported)

        public bool HasColour => Status == FlameTestStatus.Characteristic;
    }

    public static class FlameTest
    {
        public const string Source = "Vogel, Qualitative Inorganic Analysis (5th ed.); NIST Atomic Spectra Database";
        public const string Fidelity = "illustrative colour — real flames vary with the flame and impurities";

        private static readonly Dictionary<string, FlameColourEntry> Table = new Dictionary<string, FlameColourEntry>
        {
            { "Li", E("Li", "Li⁺", "crimson red",  "Li atomic line at 670.8 nm",                      0.86f, 0.08f, 0.24f) },
            { "Na", E("Na", "Na⁺", "intense yellow-orange", "Na D lines at 589.0 and 589.6 nm",        1.00f, 0.76f, 0.10f) },
            { "K",  E("K",  "K⁺",  "lilac",        "K lines at 766.5/769.9 nm (deep red) and 404.4 nm (violet); seen through cobalt-blue glass", 0.78f, 0.60f, 1.00f) },
            { "Ca", E("Ca", "Ca²⁺", "orange-red (brick red)", "CaOH bands near 554 and 622 nm",        1.00f, 0.40f, 0.15f) },
            { "Sr", E("Sr", "Sr²⁺", "crimson",     "SrOH bands near 606 and 682 nm, Sr line at 460.7 nm", 1.00f, 0.08f, 0.10f) },
            { "Ba", E("Ba", "Ba²⁺", "pale (apple) green", "Ba line at 553.5 nm and BaOH/BaCl bands 487–524 nm", 0.65f, 1.00f, 0.35f) },
            { "Cu", E("Cu", "Cu²⁺", "blue-green",  "CuCl and CuOH bands (blue 420–450 nm, green 510–555 nm)", 0.20f, 0.90f, 0.75f) },
        };

        // Metal ions that give no characteristic flame colour in a Bunsen flame.
        private static readonly HashSet<string> NoColourMetals = new HashSet<string> { "Mg", "Al", "Zn" };

        private static readonly HashSet<string> NonMetals = new HashSet<string>
        { "H", "He", "B", "C", "N", "O", "F", "Ne", "Si", "P", "S", "Cl", "Ar", "As", "Se", "Br", "Kr", "Te", "I", "Xe" };

        public static IEnumerable<FlameColourEntry> Entries => Table.Values;

        public static FlameTestResult Test(string formula)
        {
            string f = (formula ?? string.Empty).Trim();
            if (f.Length == 0) return Unsupported(f, "no sample was chosen");

            var atoms = ConservationValidator.ParseFormula(f);
            if (atoms.Count == 0 || !MolarMass.Of(f).Success)
                return Unsupported(f, $"'{f}' is not a formula that can be tested");

            var metals = atoms.Keys.Where(e => !NonMetals.Contains(e)).ToList();
            if (metals.Count == 0)
                return Unsupported(f, $"{f} contains no metal ion; flame tests identify metal ions");
            if (atoms.Count == 1)
                return Unsupported(f, $"{f} is a pure metal; a flame test is done on one of its salts (e.g. the chloride) on a clean wire loop");

            var unknown = metals.Where(m => !Table.ContainsKey(m) && !NoColourMetals.Contains(m)).ToList();
            if (unknown.Count > 0)
                return Unsupported(f, $"{string.Join(", ", unknown)} is not in this flame-test table");

            var colouring = metals.Where(Table.ContainsKey).ToList();
            if (colouring.Count > 1)
                return Unsupported(f, $"{f} contains {string.Join(" and ", colouring)}; their colours overlap (sodium's yellow masks the others)");
            if (colouring.Count == 0)
                return new FlameTestResult
                {
                    Status = FlameTestStatus.NoColour,
                    Formula = f,
                    Explanation = $"{string.Join(", ", metals)} ions give no characteristic flame colour; the flame stays its normal colour."
                };

            var entry = Table[colouring[0]];
            return new FlameTestResult
            {
                Status = FlameTestStatus.Characteristic,
                Formula = f,
                Entry = entry,
                Explanation = $"{entry.Ion} colours the flame {entry.ColourName}: heated electrons fall back to lower levels " +
                              $"and emit light of set wavelengths ({entry.Emission})."
            };
        }

        private static FlameTestResult Unsupported(string f, string why)
            => new FlameTestResult { Status = FlameTestStatus.Unsupported, Formula = f, Explanation = why };

        private static FlameColourEntry E(string el, string ion, string name, string emission, float r, float g, float b)
            => new FlameColourEntry { Element = el, Ion = ion, ColourName = name, Emission = emission, R = r, G = g, B = b };
    }
}
