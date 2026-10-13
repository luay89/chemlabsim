using System.Collections.Generic;
using NUnit.Framework;
using ChemLabSimV3.Engine;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>Contact factor: grinding matters for solids only; fluids mix by diffusion.</summary>
    public class ContactModelTests
    {
        private static ReactionEntry With(params string[] states)
        {
            var r = new ReactionEntry { reactants = new List<ReactionChemical>() };
            foreach (var s in states)
                r.reactants.Add(new ReactionChemical { formula = "X", state = s });
            return r;
        }

        [Test]
        public void SolutionsOnly_WithoutStirring_AreNotWeak()
        {
            float cf = ContactModel.Compute(With("aq", "l"), 0f, 0f);
            Assert.That(cf, Is.EqualTo(1.0f).Within(1e-4f));
        }

        [Test]
        public void SolutionsOnly_IgnoreGrinding()
        {
            var r = With("aq", "aq");
            Assert.That(ContactModel.Compute(r, 0.5f, 0f), Is.EqualTo(ContactModel.Compute(r, 0.5f, 1f)).Within(1e-4f));
        }

        [Test]
        public void SolidReactant_UsesStirringAndGrinding()
        {
            var r = With("s", "aq");
            Assert.That(ContactModel.Compute(r, 0f, 0f), Is.EqualTo(0.36f).Within(1e-4f));
            Assert.That(ContactModel.Compute(r, 1f, 1f), Is.EqualTo(2.56f).Within(1e-4f));
        }

        [Test]
        public void UnknownStates_AreTreatedAsSolid()
        {
            Assert.That(ContactModel.HasSolidReactant(new ReactionEntry()), Is.True);
        }
    }
}
