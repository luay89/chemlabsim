// ChemLabSim v3 — chemistry text formatting for TextMeshPro
// "Cu(OH)2"              → Cu(OH)<sub>2</sub>
// "Fe(SCN)2+"            → Fe(SCN)<sup>2+</sup>
// reactants/products     → CuSO<sub>4</sub>(aq) + 2NaOH(aq) → Cu(OH)<sub>2</sub>(s) + Na<sub>2</sub>SO<sub>4</sub>(aq)
// Digits right after an element, ")" or "]" are subscripts; a trailing charge is a superscript.

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ChemLabSimV3.Data
{
    public static class ChemText
    {
        private const string StateStyle = "<size=80%><color=#9FB3C8>({0})</color></size>";
        public const string Arrow = " → ";

        /// <summary>Formula with sub/superscripts as TMP rich text.</summary>
        public static string Formula(string formula)
        {
            if (string.IsNullOrEmpty(formula)) return string.Empty;

            string body = formula, charge = string.Empty;
            int end = formula.Length;
            if (end > 1 && (formula[end - 1] == '+' || formula[end - 1] == '-'))
            {
                int start = end - 1;
                while (start > 0 && char.IsDigit(formula[start - 1])) start--;
                if (start > 0)   // keep at least one character of the formula itself
                {
                    body = formula.Substring(0, start);
                    charge = formula.Substring(start);
                }
            }

            var sb = new StringBuilder(body.Length + 16);
            int i = 0;
            while (i < body.Length)
            {
                char c = body[i];
                bool sub = (char.IsDigit(c) || c == 'n') && i > 0 &&
                           (char.IsLetter(body[i - 1]) || body[i - 1] == ')' || body[i - 1] == ']');
                if (c == 'n' && i > 0 && char.IsLetter(body[i - 1])) sub = false;   // "Mn", "Zn", "Sn"
                if (c == 'n' && i > 0 && (body[i - 1] == ')' || body[i - 1] == ']')) sub = true; // "(C2H4)n"
                if (sub)
                {
                    int j = i;
                    while (j < body.Length && (char.IsDigit(body[j]) || (j == i && body[j] == 'n'))) j++;
                    sb.Append("<sub>").Append(body, i, j - i).Append("</sub>");
                    i = j;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            if (charge.Length > 0) sb.Append("<sup>").Append(charge).Append("</sup>");
            return sb.ToString();
        }

        /// <summary>One side of an equation: "2NaOH(aq) + CuSO4(aq)" with coefficients and states.</summary>
        public static string Side(List<ReactionChemical> chemicals, bool withStates = true)
        {
            if (chemicals == null) return string.Empty;
            var parts = new List<string>();
            foreach (var chem in chemicals)
            {
                if (chem == null || string.IsNullOrEmpty(chem.formula)) continue;
                int coeff = Mathf.RoundToInt(chem.stoich);
                string term = (coeff > 1 ? coeff.ToString() : string.Empty) + Formula(chem.formula);
                if (withStates && !string.IsNullOrEmpty(chem.state))
                    term += string.Format(StateStyle, chem.state);
                parts.Add(term);
            }
            return string.Join(" + ", parts);
        }

        /// <summary>Balanced equation of a reaction, or empty when it has no rich reactant data.</summary>
        public static string Equation(ReactionEntry reaction, bool withStates = true)
        {
            if (reaction == null) return string.Empty;
            string lhs = Side(reaction.reactants, withStates);
            string rhs = Side(reaction.products, withStates);
            if (string.IsNullOrEmpty(lhs) && string.IsNullOrEmpty(rhs)) return string.Empty;
            return lhs + Arrow + rhs;
        }
    }
}
