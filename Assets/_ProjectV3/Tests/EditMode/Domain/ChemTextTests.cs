using System.Collections.Generic;
using NUnit.Framework;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    public class ChemTextTests
    {
        [TestCase("H2O", "H<sub>2</sub>O")]
        [TestCase("Cu(OH)2", "Cu(OH)<sub>2</sub>")]
        [TestCase("Ca3(PO4)2", "Ca<sub>3</sub>(PO<sub>4</sub>)<sub>2</sub>")]
        [TestCase("MnO2", "MnO<sub>2</sub>")]
        [TestCase("Zn", "Zn")]
        [TestCase("(C2H4)n", "(C<sub>2</sub>H<sub>4</sub>)<sub>n</sub>")]
        [TestCase("Fe(SCN)2+", "Fe(SCN)<sup>2+</sup>")]
        [TestCase("NaCl", "NaCl")]
        public void Formula_UsesSubAndSuperscripts(string formula, string expected)
        {
            Assert.That(ChemText.Formula(formula), Is.EqualTo(expected));
        }

        [Test]
        public void Equation_HasCoefficientsAndArrow()
        {
            var r = new ReactionEntry
            {
                reactants = new List<ReactionChemical>
                {
                    new ReactionChemical { formula = "CuSO4", state = "aq", stoich = 1 },
                    new ReactionChemical { formula = "NaOH", state = "aq", stoich = 2 },
                },
                products = new List<ReactionChemical>
                {
                    new ReactionChemical { formula = "Cu(OH)2", state = "s", stoich = 1 },
                    new ReactionChemical { formula = "Na2SO4", state = "aq", stoich = 1 },
                }
            };
            string eq = ChemText.Equation(r, withStates: false);
            Assert.That(eq, Is.EqualTo("CuSO<sub>4</sub> + 2NaOH → Cu(OH)<sub>2</sub> + Na<sub>2</sub>SO<sub>4</sub>"));
        }
    }
}
