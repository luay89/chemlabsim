// ChemLabSim v3 — the periodic table data (118 elements)
// Read from Resources/Data/elements.json, which tools/elements/build_elements.py generates from
// a stored PubChem snapshot and the project's CIAAW atomic weights. This is the only element
// dataset; materials (substances) and reactions stay in their own databases and are linked by
// formula (an element record alone is never something that can react).
//
// Every property carries a status — standard, listed, predicted, estimated,
// reference_dependent, unknown, not_applicable, not_sourced — and a note, so missing or
// uncertain values are shown as such and never filled in.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    [Serializable]
    public class ElementProperty
    {
        public string status = "unknown";
        public float value;
        public string text = string.Empty;
        public string unit = string.Empty;
        public string conditions = string.Empty;
        public string source = string.Empty;
        public string note = string.Empty;
        public string url = string.Empty;     // the source's page for this element, when there is one

        /// <summary>A number or a text the source gives (whatever its certainty).</summary>
        public bool HasValue => status == "standard" || status == "listed" || status == "predicted"
                             || status == "estimated" || status == "reference_dependent";
        public bool IsNumber => HasValue && string.IsNullOrEmpty(text);
    }

    [Serializable]
    public class ElementRecord
    {
        public string id, symbol, name, nameKey, category, categorySource, categoryNote, block;
        public int z, period, group, row, col;
        public ElementProperty standardAtomicWeight, referenceIsotopeMass, electronConfiguration,
                               stateAtRoomConditions, density, meltingPoint, boilingPoint, electronegativity,
                               firstIonizationEnergy, electronAffinity, atomicRadius, oxidationStates, discovery,
                               occurrence, uses, safety, crustalAbundance, oceanicAbundance;
    }

    [Serializable]
    public class ElementSource
    {
        public string id, citation, url, retrieved;
        public string notice = string.Empty;   // a notice the source requires to be reproduced with its content
    }

    [Serializable]
    public class ElementDataFile
    {
        public int schemaVersion;
        public List<ElementSource> sources = new List<ElementSource>();
        public List<ElementRecord> elements = new List<ElementRecord>();
    }

    public class ElementTable
    {
        public const string ResourcePath = "Data/elements";

        /// <summary>Category keys in display order, with their English labels.</summary>
        public static readonly (string Key, string Label)[] Categories =
        {
            ("alkali_metal", "Alkali metals"), ("alkaline_earth_metal", "Alkaline earth metals"),
            ("transition_metal", "Transition metals"), ("post_transition_metal", "Post-transition metals"),
            ("metalloid", "Metalloids"), ("other_nonmetal", "Other nonmetals"), ("halogen", "Halogens"),
            ("noble_gas", "Noble gases"), ("lanthanide", "Lanthanides"), ("actinide", "Actinides"),
        };

        private readonly Dictionary<int, ElementRecord> byZ = new Dictionary<int, ElementRecord>();
        private readonly Dictionary<string, ElementRecord> bySymbol = new Dictionary<string, ElementRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ElementSource> sources = new Dictionary<string, ElementSource>();

        public IReadOnlyList<ElementRecord> All { get; }

        private ElementTable(ElementDataFile file)
        {
            var list = file?.elements ?? new List<ElementRecord>();
            All = list.OrderBy(e => e.z).ToList();
            foreach (var e in All)
            {
                byZ[e.z] = e;
                bySymbol[e.symbol] = e;
            }
            foreach (var s in file?.sources ?? new List<ElementSource>()) sources[s.id] = s;
        }

        public static ElementTable FromJson(string json) => new ElementTable(JsonUtility.FromJson<ElementDataFile>(json));

        private static ElementTable cached;

        /// <summary>The shipped dataset (loaded once, on first use).</summary>
        public static ElementTable Load()
        {
            if (cached != null) return cached;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            cached = asset != null ? FromJson(asset.text) : new ElementTable(null);
            if (asset == null) Debug.LogWarning("[ElementTable] Resources/" + ResourcePath + ".json is missing.");
            return cached;
        }

        public bool TryGet(int z, out ElementRecord e) => byZ.TryGetValue(z, out e);
        public bool TryGet(string symbol, out ElementRecord e) => bySymbol.TryGetValue(symbol ?? string.Empty, out e);
        public bool TryGetSource(string id, out ElementSource s) => sources.TryGetValue(id ?? string.Empty, out s);

        public static string CategoryLabel(string key)
        {
            foreach (var c in Categories) if (c.Key == key) return c.Label;
            return key;
        }

        /// <summary>The category of one element ("Noble gas", "Alkali metal").</summary>
        public static string CategorySingular(string key)
        {
            switch (key)
            {
                case "alkali_metal": return "Alkali metal";
                case "alkaline_earth_metal": return "Alkaline earth metal";
                case "transition_metal": return "Transition metal";
                case "post_transition_metal": return "Post-transition metal";
                case "metalloid": return "Metalloid";
                case "other_nonmetal": return "Other nonmetal";
                case "halogen": return "Halogen";
                case "noble_gas": return "Noble gas";
                case "lanthanide": return "Lanthanide";
                case "actinide": return "Actinide";
                default: return key;
            }
        }

        /// <summary>
        /// Elements matching a query: an atomic number ("26"), a symbol ("Fe", exact, case-insensitive)
        /// or part of the English name ("iro"). Empty query = all. Category null = all.
        /// </summary>
        public List<ElementRecord> Search(string query, string category = null)
        {
            string q = (query ?? string.Empty).Trim();
            IEnumerable<ElementRecord> result = All;
            if (!string.IsNullOrEmpty(category)) result = result.Where(e => e.category == category);
            if (q.Length == 0) return result.ToList();

            if (int.TryParse(q, NumberStyles.Integer, CultureInfo.InvariantCulture, out int z))
                return result.Where(e => e.z == z).ToList();
            return result.Where(e => string.Equals(e.symbol, q, StringComparison.OrdinalIgnoreCase)
                                  || e.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                         .OrderBy(e => string.Equals(e.symbol, q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                         .ThenBy(e => e.z)
                         .ToList();
        }

        /// <summary>Elements in a chemical formula (from the formula parser the engine uses).</summary>
        public static IEnumerable<string> SymbolsIn(string formula)
            => string.IsNullOrWhiteSpace(formula) ? Enumerable.Empty<string>() : ConservationValidator.ParseFormula(formula).Keys;
    }
}
