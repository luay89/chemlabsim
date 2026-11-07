using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>The shipped periodic-table data: complete, consistent, honest about what is missing.</summary>
    public class ElementTableTests
    {
        private static ElementTable table;

        [OneTimeSetUp]
        public void Load()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "_ProjectV3", "Resources", "Data", "elements.json");
            table = ElementTable.FromJson(File.ReadAllText(path));
        }

        private static readonly HashSet<string> Statuses = new HashSet<string>
        { "standard", "listed", "predicted", "estimated", "reference_dependent", "unknown", "not_applicable", "not_sourced" };

        private static IEnumerable<(string Name, ElementProperty P)> Props(ElementRecord e) => new[]
        {
            ("standardAtomicWeight", e.standardAtomicWeight), ("referenceIsotopeMass", e.referenceIsotopeMass),
            ("electronConfiguration", e.electronConfiguration), ("stateAtRoomConditions", e.stateAtRoomConditions),
            ("density", e.density), ("meltingPoint", e.meltingPoint), ("boilingPoint", e.boilingPoint),
            ("electronegativity", e.electronegativity), ("firstIonizationEnergy", e.firstIonizationEnergy),
            ("electronAffinity", e.electronAffinity), ("atomicRadius", e.atomicRadius), ("oxidationStates", e.oxidationStates),
            ("discovery", e.discovery), ("occurrence", e.occurrence), ("uses", e.uses), ("safety", e.safety),
            ("crustalAbundance", e.crustalAbundance), ("oceanicAbundance", e.oceanicAbundance),
        };

        [Test]
        public void All118_ExactlyOnce_UniqueSymbolsAndIds()
        {
            Assert.That(table.All.Select(e => e.z), Is.EqualTo(Enumerable.Range(1, 118)));
            Assert.That(table.All.Select(e => e.symbol).Distinct().Count(), Is.EqualTo(118));
            Assert.That(table.All.Select(e => e.id).Distinct().Count(), Is.EqualTo(118));
            Assert.That(table.All.All(e => e.id == $"el_{e.z:000}"), Is.True);
            Assert.That(table.All.All(e => !string.IsNullOrEmpty(e.name) && e.nameKey == $"element.{e.symbol.ToLowerInvariant()}.name"), Is.True);
        }

        [Test]
        public void Positions_AreValidAndUnique()
        {
            var cells = table.All.Select(e => (e.row, e.col)).ToList();
            Assert.That(cells.Distinct().Count(), Is.EqualTo(118));
            Assert.That(table.All.All(e => e.row >= 1 && e.row <= 10 && e.col >= 1 && e.col <= 18), Is.True);
            Assert.That(table.All.Where(e => e.row == 8), Is.Empty);   // gap row between the main table and the f-block
            // Lanthanides 57–71 in row 9, actinides 89–103 in row 10, both without a group number.
            Assert.That(table.All.Where(e => e.row == 9).Select(e => e.z), Is.EqualTo(Enumerable.Range(57, 15)));
            Assert.That(table.All.Where(e => e.row == 10).Select(e => e.z), Is.EqualTo(Enumerable.Range(89, 15)));
            Assert.That(table.All.Where(e => e.row >= 9).All(e => e.group == 0 && e.block == "f"), Is.True);
            // Spot checks.
            Assert.That(Cell("H"), Is.EqualTo((1, 1)));
            Assert.That(Cell("He"), Is.EqualTo((1, 18)));
            Assert.That(Cell("Fe"), Is.EqualTo((4, 8)));
            Assert.That(Cell("Hf"), Is.EqualTo((6, 4)));
            Assert.That(Cell("Og"), Is.EqualTo((7, 18)));
            Assert.That(table.TryGet("Fe", out var fe) && fe.period == 4 && fe.group == 8, Is.True);
        }

        private static (int, int) Cell(string symbol) { table.TryGet(symbol, out var e); return (e.row, e.col); }

        [Test]
        public void StandardAtomicWeights_AreTheProjectsCiaawTable()
        {
            int standard = 0;
            foreach (var e in table.All)
            {
                bool has = AtomicWeights.TryGet(e.symbol, out double w);
                if (has)
                {
                    standard++;
                    Assert.That(e.standardAtomicWeight.status, Is.EqualTo("standard"), e.symbol);
                    Assert.That(e.standardAtomicWeight.value, Is.EqualTo(w).Within(1e-4), e.symbol);
                    Assert.That(e.referenceIsotopeMass.status, Is.EqualTo("not_applicable"), e.symbol);
                }
                else
                {
                    // No standard atomic weight: never invented; a reference-isotope mass (u) instead, if listed.
                    Assert.That(e.standardAtomicWeight.status, Is.EqualTo("not_applicable"), e.symbol);
                    Assert.That(e.referenceIsotopeMass.unit, Is.EqualTo("u"), e.symbol);
                }
            }
            Assert.That(standard, Is.EqualTo(84));
            Assert.That(table.TryGet("Tc", out var tc) && tc.standardAtomicWeight.status == "not_applicable", Is.True);
        }

        [Test]
        public void EveryProperty_HasAKnownStatus_AndMissingValuesSayWhy()
        {
            foreach (var e in table.All)
            foreach (var (name, p) in Props(e))
            {
                Assert.That(p, Is.Not.Null, $"{e.symbol}.{name}");
                Assert.That(Statuses.Contains(p.status), Is.True, $"{e.symbol}.{name}: {p.status}");
                if (!p.HasValue)
                {
                    Assert.That(p.value, Is.EqualTo(0f), $"{e.symbol}.{name} has a number but no value status");
                    Assert.That(p.note, Is.Not.Empty, $"{e.symbol}.{name} is missing without a reason");
                }
                else
                {
                    Assert.That(p.source, Is.Not.Empty, $"{e.symbol}.{name} has a value without a source");
                }
            }
        }

        [Test]
        public void UnitsAndReferenceConditions()
        {
            foreach (var e in table.All)
            {
                Assert.That(e.meltingPoint.unit, Is.EqualTo("K"));
                Assert.That(e.boilingPoint.unit, Is.EqualTo("K"));
                Assert.That(e.density.unit, Is.EqualTo("g/cm³"));
                if (e.meltingPoint.HasValue) StringAssert.Contains("101.325 kPa", e.meltingPoint.conditions);
            }
            // Values not at 1 atm are flagged, not shown as normal melting/boiling points.
            Assert.That(table.TryGet("He", out var he), Is.True);
            Assert.That(he.meltingPoint.status, Is.EqualTo("reference_dependent"));
            StringAssert.Contains("does not solidify", he.meltingPoint.note);
            table.TryGet("As", out var arsenic);
            Assert.That(arsenic.boilingPoint.status, Is.EqualTo("reference_dependent"));
            // Elements never made in bulk: estimates are labelled, states are predictions.
            table.TryGet("Fm", out var fm);
            Assert.That(fm.meltingPoint.status, Is.EqualTo("estimated"));
            table.TryGet("Og", out var og);
            Assert.That(og.stateAtRoomConditions.status, Is.EqualTo("predicted"));
            Assert.That(og.electronConfiguration.status, Is.EqualTo("predicted"));
        }

        [Test]
        public void Search_ByNameSymbolNumber_AndCategoryFilter()
        {
            Assert.That(table.Search("26").Select(e => e.symbol), Is.EqualTo(new[] { "Fe" }));
            Assert.That(table.Search("fe").First().symbol, Is.EqualTo("Fe"));          // exact symbol first
            Assert.That(table.Search("iron").Select(e => e.symbol), Is.EqualTo(new[] { "Fe" }));
            Assert.That(table.Search("gen").Select(e => e.symbol), Is.SupersetOf(new[] { "H", "N", "O" }));
            Assert.That(table.Search("").Count, Is.EqualTo(118));
            Assert.That(table.Search("999"), Is.Empty);
            Assert.That(table.Search("zzz"), Is.Empty);
            Assert.That(table.Search("", "halogen").Select(e => e.symbol), Is.EqualTo(new[] { "F", "Cl", "Br", "I", "At", "Ts" }));
            Assert.That(table.Search("", "noble_gas").Count, Is.EqualTo(7));
            Assert.That(table.Search("", "lanthanide").Count, Is.EqualTo(15));
            Assert.That(table.Search("", "actinide").Count, Is.EqualTo(15));
            Assert.That(table.All.All(e => ElementTable.Categories.Any(c => c.Key == e.category)), Is.True);
        }

        [System.Serializable]
        private class MaterialList { public List<ChemicalMaterial> items; }

        [Test]
        public void EveryLabMaterial_IsMadeOfKnownElements()
        {
            // Materials (substances) link to elements through their formula; no material can
            // refer to an element the table does not have.
            string json = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "_Project", "DataSrc", "materials.json"));
            var mats = JsonUtility.FromJson<MaterialList>("{\"items\":" + json + "}").items;
            foreach (var m in mats)
            {
                var symbols = ElementTable.SymbolsIn(m.formula).ToList();
                Assert.That(symbols, Is.Not.Empty, m.formula);
                foreach (var s in symbols) Assert.That(table.TryGet(s, out _), Is.True, $"{m.formula}: {s}");
            }
            // An element is not a compound containing it: Na (the metal) and NaCl are different materials.
            Assert.That(mats.Count(m => m.formula == "Na"), Is.EqualTo(1));
            Assert.That(mats.Where(m => ElementTable.SymbolsIn(m.formula).Contains("Na")).Count(), Is.GreaterThan(1));
        }

        [Test]
        public void DetailText_ShowsStatus_AndOnlyRealLabLinks()
        {
            var mats = new List<ChemicalMaterial>
            {
                new ChemicalMaterial { formula = "Fe", name_en = "Iron", state = "solid" },
                new ChemicalMaterial { formula = "FeCl3", name_en = "Iron(III) chloride", state = "aqueous" },
                new ChemicalMaterial { formula = "NaCl", name_en = "Sodium chloride", state = "aqueous" },
            };
            table.TryGet("Fe", out var fe);
            string text = ChemLabSimV3.Views.Layout.PeriodicTableView.Describe(fe, table, mats, null);
            StringAssert.Contains("Iron", text);
            StringAssert.Contains("55.845", text);
            StringAssert.Contains("CIAAW standard", text);
            StringAssert.Contains("As the element", text);
            StringAssert.DoesNotContain("Flame test", text);          // Fe is not in the flame-test table
            StringAssert.Contains("USES & APPLICATIONS", text);
            StringAssert.Contains("Source: Jefferson Lab", text);     // every quoted text names its source
            StringAssert.Contains("Transition metal  ·", text);       // singular for one element

            table.TryGet("Na", out var na);
            StringAssert.Contains("Flame test: <b>intense yellow-orange</b>", ChemLabSimV3.Views.Layout.PeriodicTableView.Describe(na, table, mats, null));
            table.TryGet("Og", out var og);
            string ogText = ChemLabSimV3.Views.Layout.PeriodicTableView.Describe(og, table, mats, null);
            StringAssert.Contains("predicted", ogText);
            StringAssert.Contains("The pure element is not a material in this lab", ogText);
        }

        [Test]
        public void QuotedTexts_HaveSourceAndLink_AndGapsSayWhy()
        {
            var textSources = new HashSet<string> { "jlab", "lanl" };
            foreach (var e in table.All)
            {
                foreach (var p in new[] { e.uses, e.occurrence })
                {
                    if (p.HasValue)
                    {
                        Assert.That(textSources.Contains(p.source), Is.True, e.symbol);
                        Assert.That(p.url, Does.StartWith("https://"), e.symbol);
                        Assert.That(p.text.Length, Is.GreaterThan(20), e.symbol);
                    }
                    else Assert.That(p.note, Is.Not.Empty, e.symbol);
                }
                if (e.safety.HasValue)
                {
                    Assert.That(e.safety.source, Is.EqualTo("eu_clp"), e.symbol);
                    Assert.That(e.safety.text, Does.Match(@"H\d{3}"), e.symbol);   // GHS hazard statement codes
                }
                else StringAssert.Contains("does not mean it is safe", e.safety.note, e.symbol);
            }
            Assert.That(table.All.Count(e => e.uses.HasValue), Is.GreaterThanOrEqualTo(100));

            // Regression: a CLP entry for a compound containing the element (an azo dye was listed for
            // Cl) must not be shown as the element's classification.
            foreach (var e in table.All.Where(x => x.safety.HasValue))
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(e.safety.text, "<b>(.*?)</b>"))
                {
                    string form = m.Groups[1].Value.ToLowerInvariant();
                    string name = e.name.ToLowerInvariant();
                    bool ok = form.Contains(name) || (name == "aluminum" && form.Contains("aluminium"))
                              || (name == "sulfur" && form.Contains("sulphur")) || (name == "cesium" && form.Contains("caesium"));
                    Assert.That(ok, Is.True, $"{e.symbol}: '{form}' is not a form of {e.name}");
                }
            table.TryGet("Cl", out var cl);
            Assert.That(cl.safety.status, Is.EqualTo("unknown"));

            table.TryGet("Hg", out var hg);
            StringAssert.Contains("H330", hg.safety.text);                   // mercury: fatal if inhaled
            table.TryGet("Mg", out var mg);
            StringAssert.Contains("magnesium powder (pyrophoric)", mg.safety.text);   // classification is per form
            table.TryGet("Tc", out var tc);
            Assert.That(tc.crustalAbundance.status, Is.EqualTo("not_applicable"));   // not found in nature
            table.TryGet("Fe", out var fe);
            StringAssert.Contains("<sup>", fe.crustalAbundance.text);         // 5.63×10⁴ kept as a power of ten
            StringAssert.Contains("milligrams per kilogram", fe.crustalAbundance.text);
        }

        [Test]
        public void LanlText_CarriesTheNoticeLanlRequires()
        {
            Assert.That(table.TryGetSource("lanl", out var lanl), Is.True);
            StringAssert.Contains("this Notice and any statement of authorship are reproduced on all copies", lanl.notice);
            table.TryGet("Fe", out var fe);                                  // Fe quotes LANL ("occurrence")
            Assert.That(fe.occurrence.source, Is.EqualTo("lanl"));
            StringAssert.Contains("Triad National Security", ChemLabSimV3.Views.Layout.PeriodicTableView.Describe(fe, table, new List<ChemicalMaterial>(), null));
        }

        [Test]
        public void SourcesAreListed()
        {
            Assert.That(table.TryGetSource("pubchem_pt", out var pc) && pc.url.StartsWith("https://"), Is.True);
            Assert.That(table.TryGetSource("ciaaw_2021", out _), Is.True);
        }
    }
}
