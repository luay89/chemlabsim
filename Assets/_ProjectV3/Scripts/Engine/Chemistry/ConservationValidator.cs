// ChemLabSim v3 — Conservation Validator
// Validates physical conservation laws for chemical reactions:
//
//   1. ATOM BALANCE: Each element must appear equally on both sides
//      2H₂ + O₂ → 2H₂O : H:4=4 ✓, O:2=2 ✓
//
//   2. MASS BALANCE: Total mass of reactants ≈ total mass of products
//      Uses molarMass fields from ReactionChemical (if set).
//      2×18 + 1×32 = 68, 2×18 = 36 ✗ (must use stoich × molarMass)
//
//   3. CHARGE BALANCE: Net ionic charge is conserved
//      Na⁺ + Cl⁻ → NaCl : (+1) + (-1) = 0 ✓
//
// Used at reaction initialization to catch data entry errors and
// display warnings in the scientific report.
//
// Formula Parser: handles H2O, Ca(OH)2, Fe2(SO4)3, MgCl2
// Does NOT handle: isotopes, radicals, charges embedded in formulas (use charge field).

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    public enum ValidationSeverity { Pass, Warning, Error }

    public struct ValidationIssue
    {
        public ValidationSeverity Severity;
        public string Law;    // "AtomBalance", "MassBalance", "ChargeBalance"
        public string Detail; // Human-readable description
    }

    public struct ValidationReport
    {
        /// <summary>True if no errors found (warnings allowed).</summary>
        public bool IsValid;

        /// <summary>True if no issues at all.</summary>
        public bool IsClean;

        public List<ValidationIssue> Issues;

        /// <summary>One-line summary for display.</summary>
        public string Summary;

        public static ValidationReport Clean() => new ValidationReport
        {
            IsValid = true,
            IsClean = true,
            Issues  = new List<ValidationIssue>(),
            Summary = "All conservation laws satisfied."
        };
    }

    public static class ConservationValidator
    {
        // ═══════════════════════════════════════════════════════
        //  PRIMARY API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Full validation of a ReactionEntry.
        /// Runs atom balance, mass balance (if molarMass available), and charge balance.
        /// </summary>
        public static ValidationReport Validate(ReactionEntry reaction)
        {
            var report = new ValidationReport
            {
                Issues = new List<ValidationIssue>()
            };

            if (reaction == null)
            {
                report.IsValid = false;
                report.IsClean = false;
                report.Summary = "Null reaction entry.";
                return report;
            }

            CheckAtomBalance(reaction, report.Issues);
            CheckMassBalance(reaction, report.Issues);
            CheckChargeBalance(reaction, report.Issues);

            bool hasError   = false;
            bool hasWarning = false;

            for (int i = 0; i < report.Issues.Count; i++)
            {
                if (report.Issues[i].Severity == ValidationSeverity.Error)   hasError = true;
                if (report.Issues[i].Severity == ValidationSeverity.Warning) hasWarning = true;
            }

            report.IsValid = !hasError;
            report.IsClean = !hasError && !hasWarning;

            if (hasError)
                report.Summary = $"Conservation violation(s) in '{reaction.name_en ?? reaction.id}'.";
            else if (hasWarning)
                report.Summary = $"Warnings for '{reaction.name_en ?? reaction.id}'. Data may be incomplete.";
            else
                report.Summary = "All conservation laws satisfied.";

            return report;
        }

        // ═══════════════════════════════════════════════════════
        //  ATOM BALANCE
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Verify that each element appears the same number of times
        /// on both sides of the equation (stoich-weighted).
        /// </summary>
        public static void CheckAtomBalance(ReactionEntry reaction, List<ValidationIssue> issues)
        {
            var reactantAtoms = CountAtoms(reaction.reactants);
            var productAtoms  = CountAtoms(reaction.products);

            // Compare
            var allElements = new HashSet<string>(reactantAtoms.Keys);
            allElements.UnionWith(productAtoms.Keys);

            foreach (string element in allElements)
            {
                reactantAtoms.TryGetValue(element, out float rCount);
                productAtoms.TryGetValue(element,  out float pCount);

                float diff = Mathf.Abs(rCount - pCount);
                if (diff > 0.01f)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Law      = "AtomBalance",
                        Detail   = $"Element {element}: reactants={rCount:F0}, products={pCount:F0} (Δ={diff:F1})"
                    });
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        //  MASS BALANCE
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Verify mass conservation using molarMass fields.
        /// Only runs if molarMass is populated for at least half the species.
        /// </summary>
        public static void CheckMassBalance(ReactionEntry reaction, List<ValidationIssue> issues)
        {
            if (reaction.reactants == null || reaction.products == null) return;

            float reactantMass = SumMass(reaction.reactants);
            float productMass  = SumMass(reaction.products);

            // If either side has no mass data, skip (incomplete data → warning only)
            bool hasReactantData = HasMassData(reaction.reactants);
            bool hasProductData  = HasMassData(reaction.products);

            if (!hasReactantData || !hasProductData)
            {
                if (hasReactantData || hasProductData)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Law      = "MassBalance",
                        Detail   = "molarMass missing for some species — mass balance skipped."
                    });
                }
                return;
            }

            float diff = Mathf.Abs(reactantMass - productMass);
            float tolerance = Mathf.Max(reactantMass, productMass) * 0.01f; // 1% tolerance

            if (diff > tolerance)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Law      = "MassBalance",
                    Detail   = $"Reactant mass={reactantMass:F2} g/mol, product mass={productMass:F2} g/mol (Δ={diff:F2})"
                });
            }
        }

        // ═══════════════════════════════════════════════════════
        //  CHARGE BALANCE
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Verify that net ionic charge is equal on both sides.
        /// Uses the charge field in ReactionChemical.
        /// Only runs if any charge data is present.
        /// </summary>
        public static void CheckChargeBalance(ReactionEntry reaction, List<ValidationIssue> issues)
        {
            if (reaction.reactants == null || reaction.products == null) return;

            bool anyChargeData = HasChargeData(reaction.reactants) || HasChargeData(reaction.products);
            if (!anyChargeData) return; // No charge data — skip

            float reactantCharge = SumCharge(reaction.reactants);
            float productCharge  = SumCharge(reaction.products);

            float diff = Mathf.Abs(reactantCharge - productCharge);
            if (diff > 0.01f)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Law      = "ChargeBalance",
                    Detail   = $"Net charge: reactants={reactantCharge:+0;-0;0}, products={productCharge:+0;-0;0} (Δ={diff:F0})"
                });
            }
        }

        // ═══════════════════════════════════════════════════════
        //  FORMULA PARSER
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Parse a chemical formula into an element → count dictionary.
        /// Supports: H2O, Ca(OH)2, Fe2(SO4)3, NaCl, K2Cr2O7
        /// </summary>
        public static Dictionary<string, float> ParseFormula(string formula)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(formula)) return result;

            // Remove phase suffixes: (s), (l), (g), (aq)
            string clean = Regex.Replace(formula.Trim(), @"\([slgaq]+\)$", string.Empty);

            ParseSegment(clean, 1f, result);
            return result;
        }

        // ═══════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════

        private static void ParseSegment(string segment, float multiplier,
            Dictionary<string, float> result)
        {
            int i = 0;
            while (i < segment.Length)
            {
                char c = segment[i];

                if (c == '(')
                {
                    // Find matching closing paren
                    int depth = 1;
                    int start = i + 1;
                    i++;
                    while (i < segment.Length && depth > 0)
                    {
                        if (segment[i] == '(') depth++;
                        else if (segment[i] == ')') depth--;
                        i++;
                    }
                    string inner = segment.Substring(start, i - start - 1);

                    // Read subscript after )
                    float sub = ReadNumber(segment, ref i);
                    ParseSegment(inner, multiplier * sub, result);
                }
                else if (char.IsUpper(c))
                {
                    // Read element symbol
                    int start = i;
                    i++;
                    while (i < segment.Length && char.IsLower(segment[i]))
                        i++;

                    string element = segment.Substring(start, i - start);

                    // Read subscript
                    float sub = ReadNumber(segment, ref i);
                    AddAtoms(result, element, sub * multiplier);
                }
                else
                {
                    i++; // Skip unknown chars (e.g., '+', '-', charge indicators)
                }
            }
        }

        private static float ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && char.IsDigit(s[i]))
                i++;

            if (i == start) return 1f;
            return float.Parse(s.Substring(start, i - start));
        }

        private static void AddAtoms(Dictionary<string, float> dict, string element, float count)
        {
            if (dict.TryGetValue(element, out float existing))
                dict[element] = existing + count;
            else
                dict[element] = count;
        }

        private static Dictionary<string, float> CountAtoms(List<ReactionChemical> chemicals)
        {
            var total = new Dictionary<string, float>(StringComparer.Ordinal);
            if (chemicals == null) return total;

            for (int i = 0; i < chemicals.Count; i++)
            {
                var c = chemicals[i];
                if (c == null || string.IsNullOrWhiteSpace(c.formula)) continue;

                float stoich = c.stoich > 0f ? c.stoich : 1f;
                var atoms = ParseFormula(c.formula);

                foreach (var kv in atoms)
                    AddAtoms(total, kv.Key, kv.Value * stoich);
            }

            return total;
        }

        private static float SumMass(List<ReactionChemical> chemicals)
        {
            if (chemicals == null) return 0f;
            float total = 0f;
            for (int i = 0; i < chemicals.Count; i++)
            {
                var c = chemicals[i];
                if (c == null) continue;
                float stoich = c.stoich > 0f ? c.stoich : 1f;
                total += stoich * c.molarMass;
            }
            return total;
        }

        private static float SumCharge(List<ReactionChemical> chemicals)
        {
            if (chemicals == null) return 0f;
            float total = 0f;
            for (int i = 0; i < chemicals.Count; i++)
            {
                var c = chemicals[i];
                if (c == null) continue;
                float stoich = c.stoich > 0f ? c.stoich : 1f;
                total += stoich * c.charge;
            }
            return total;
        }

        private static bool HasMassData(List<ReactionChemical> chemicals)
        {
            if (chemicals == null) return false;
            int nonZero = 0;
            for (int i = 0; i < chemicals.Count; i++)
                if (chemicals[i] != null && chemicals[i].molarMass > 0f) nonZero++;
            return nonZero >= (chemicals.Count + 1) / 2; // At least half
        }

        private static bool HasChargeData(List<ReactionChemical> chemicals)
        {
            if (chemicals == null) return false;
            for (int i = 0; i < chemicals.Count; i++)
                if (chemicals[i] != null && chemicals[i].charge != 0) return true;
            return false;
        }
    }
}
