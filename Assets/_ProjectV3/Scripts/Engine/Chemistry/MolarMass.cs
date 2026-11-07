// ChemLabSim v3 — Molar mass from a chemical formula
// M = Σ (atom count × standard atomic weight), with the atom counts from the existing
// ConservationValidator.ParseFormula. That parser is lenient (it skips characters it does
// not understand), so the formula is validated first: an unparseable formula, a charge, a
// polymer "n" or an element without a standard atomic weight gives a failed result with the
// reason, never a partial number.

using System.Text.RegularExpressions;

namespace ChemLabSimV3.Engine.Chemistry
{
    public struct MolarMassResult
    {
        public bool Success;
        public double GramsPerMole;   // NaN when not successful
        public string Error;          // why it could not be computed

        public static MolarMassResult Ok(double m) => new MolarMassResult { Success = true, GramsPerMole = m, Error = string.Empty };
        public static MolarMassResult Fail(string why) => new MolarMassResult { Success = false, GramsPerMole = double.NaN, Error = why };
    }

    public static class MolarMass
    {
        // Element symbols, digits and parentheses only — no charges, phases or variable counts.
        private static readonly Regex Allowed = new Regex(@"^[A-Z(][A-Za-z0-9()]*$", RegexOptions.CultureInvariant);

        public static MolarMassResult Of(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return MolarMassResult.Fail("No formula.");

            string f = formula.Trim();
            if (f.EndsWith("+") || f.EndsWith("-"))
                return MolarMassResult.Fail($"'{f}' is an ion, not a substance that can be weighed.");
            if (Regex.IsMatch(f, @"\)n$"))
                return MolarMassResult.Fail($"'{f}' is a polymer with no fixed chain length, so it has no single molar mass.");
            if (!Allowed.IsMatch(f) || !ParenthesesBalanced(f) || Regex.IsMatch(f, @"[a-z]{2}|\(\)|(^|[^A-Z])[a-z]"))
                return MolarMassResult.Fail($"'{f}' is not a valid chemical formula.");

            var atoms = ConservationValidator.ParseFormula(f);
            if (atoms.Count == 0)
                return MolarMassResult.Fail($"'{f}' contains no elements.");

            double total = 0.0;
            foreach (var pair in atoms)
            {
                if (!AtomicWeights.TryGet(pair.Key, out double weight))
                    return MolarMassResult.Fail($"'{pair.Key}' in '{f}' is not an element with a standard atomic weight.");
                total += weight * pair.Value;
            }
            return MolarMassResult.Ok(total);
        }

        private static bool ParenthesesBalanced(string f)
        {
            int depth = 0;
            foreach (char c in f)
            {
                if (c == '(') depth++;
                else if (c == ')' && --depth < 0) return false;
            }
            return depth == 0;
        }
    }
}
