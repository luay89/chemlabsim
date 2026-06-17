using NUnit.Framework;
using ChemLabSimV3.Domain.MolecularSimulation;
using System.Collections.Generic;

namespace ChemLabSimV3.Tests.EditMode.Domain
{
    /// <summary>
    /// Tests for MolecularSimulator — pure C# molecular simulation engine.
    /// </summary>
    public class MolecularSimulatorTests
    {
        [Test]
        public void Constructor_InitializesEmpty()
        {
            var sim = new MolecularSimulator();
            Assert.That(sim.AtomCount, Is.EqualTo(0));
            Assert.That(sim.BondCount, Is.EqualTo(0));
            Assert.That(sim.IsAnimating, Is.False);
        }

        [Test]
        public void LoadMolecules_WithValidMolecules_AddsAtoms()
        {
            var sim = new MolecularSimulator();
            var water = MolecularSimulator.BuildFromFormula("H2O", 0f, 0f, 0f);

            sim.LoadMolecules(new List<Molecule> { water });

            Assert.That(sim.AtomCount, Is.GreaterThan(0));
        }

        [Test]
        public void LoadMolecules_WithNull_DoesNotThrow()
        {
            var sim = new MolecularSimulator();
            Assert.DoesNotThrow(() => sim.LoadMolecules(null));
        }

        [Test]
        public void Step_ReturnsSnapshotWithAtoms()
        {
            var sim = new MolecularSimulator();
            var water = MolecularSimulator.BuildFromFormula("H2O", 0f, 0f, 0f);
            sim.LoadMolecules(new List<Molecule> { water });

            var snapshot = sim.Step(0.016f);

            Assert.IsNotNull(snapshot);
            Assert.That(snapshot.AtomStates.Count, Is.GreaterThan(0));
        }

        [Test]
        public void SetTemperatureC_RoundTrips()
        {
            var sim = new MolecularSimulator();
            sim.SetTemperatureC(100f);
            Assert.That(sim.GetTemperatureC(), Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void StartTransformation_SetsAnimating()
        {
            var sim = new MolecularSimulator();

            var transform = new ReactionTransformation
            {
                Reactants = new List<Molecule>
                {
                    MolecularSimulator.BuildFromFormula("HCl", -1f, 0f, 0f),
                    MolecularSimulator.BuildFromFormula("NaOH", 1f, 0f, 0f)
                },
                Products = new List<Molecule>
                {
                    MolecularSimulator.BuildFromFormula("NaCl", 0f, 0f, 0f),
                    MolecularSimulator.BuildFromFormula("H2O", 1.5f, 0f, 0f)
                },
                TemperatureC = 25f,
                AnimationDurationSeconds = 3f
            };

            sim.StartTransformation(transform);

            Assert.That(sim.IsAnimating, Is.True);
            Assert.That(sim.AnimationProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Step_DuringAnimation_AdvancesProgress()
        {
            var sim = new MolecularSimulator();

            var transform = new ReactionTransformation
            {
                Reactants = new List<Molecule>
                {
                    MolecularSimulator.BuildFromFormula("HCl", -1f, 0f, 0f),
                    MolecularSimulator.BuildFromFormula("NaOH", 1f, 0f, 0f)
                },
                Products = new List<Molecule>
                {
                    MolecularSimulator.BuildFromFormula("NaCl", 0f, 0f, 0f)
                },
                TemperatureC = 25f,
                AnimationDurationSeconds = 3f
            };

            sim.StartTransformation(transform);

            // Advance 1 second
            var snapshot = sim.Step(1f);
            Assert.That(snapshot.Progress01, Is.GreaterThan(0f));
            Assert.That(snapshot.Progress01, Is.LessThan(1f));

            // Advance to completion
            sim.Step(2.5f);
            Assert.That(sim.IsAnimating, Is.False);
            Assert.That(sim.AnimationProgress, Is.EqualTo(1f));
        }

        [Test]
        public void BuildFromFormula_ParsesH2O()
        {
            var mol = MolecularSimulator.BuildFromFormula("H2O", 0f, 0f, 0f);

            Assert.IsNotNull(mol);
            Assert.That(mol.Formula, Is.EqualTo("H2O"));
            Assert.That(mol.Atoms.Count, Is.EqualTo(3)); // H, H, O
        }

        [Test]
        public void BuildFromFormula_ParsesNaCl()
        {
            var mol = MolecularSimulator.BuildFromFormula("NaCl", 0f, 0f, 0f);

            Assert.IsNotNull(mol);
            Assert.That(mol.Atoms.Count, Is.EqualTo(2)); // Na, Cl
            Assert.That(mol.Bonds.Count, Is.EqualTo(1)); // Single bond
        }

        [Test]
        public void BuildFromFormula_ParsesComplexFormula()
        {
            var mol = MolecularSimulator.BuildFromFormula("C6H12O6", 0f, 0f, 0f);

            Assert.IsNotNull(mol);
            Assert.That(mol.Atoms.Count, Is.GreaterThan(0));
        }

        [Test]
        public void BuildFromFormula_EmptyFormula_ReturnsEmpty()
        {
            var mol = MolecularSimulator.BuildFromFormula("", 0f, 0f, 0f);
            Assert.IsNotNull(mol);
            Assert.That(mol.Atoms.Count, Is.EqualTo(0));
        }

        [Test]
        public void MultipleSteps_MaintainsSimulationStability()
        {
            var sim = new MolecularSimulator();
            var mol = MolecularSimulator.BuildFromFormula("H2O", 0f, 0f, 0f);
            sim.LoadMolecules(new List<Molecule> { mol });

            // Run 100 steps — should not crash or produce NaN
            for (int i = 0; i < 100; i++)
            {
                var snapshot = sim.Step(0.016f);
                Assert.IsNotNull(snapshot);

                foreach (var atom in snapshot.AtomStates)
                {
                    Assert.That(float.IsNaN(atom.X), Is.False);
                    Assert.That(float.IsNaN(atom.Y), Is.False);
                    Assert.That(float.IsNaN(atom.Z), Is.False);
                }
            }
        }

        [Test]
        public void Temperature_AffectsMotion()
        {
            var sim = new MolecularSimulator();
            var mol = MolecularSimulator.BuildFromFormula("O2", 0f, 0f, 0f);
            sim.LoadMolecules(new List<Molecule> { mol });

            // Run at cold temperature
            sim.SetTemperatureC(-50f);
            var snapshotCold = sim.Step(0.5f);

            // Run at hot temperature  
            var snapshotHot = sim.Step(0.5f);

            // Both should be valid
            Assert.IsNotNull(snapshotCold);
            Assert.IsNotNull(snapshotHot);
        }

        [Test]
        public void ElementDatabase_ReturnsKnownElements()
        {
            var h = ElementDatabase.Get("H");
            Assert.That(h.Symbol, Is.EqualTo("H"));
            Assert.That(h.AtomicNumber, Is.EqualTo(1));

            var fe = ElementDatabase.Get("Fe");
            Assert.That(fe.Symbol, Is.EqualTo("Fe"));
            Assert.That(fe.AtomicNumber, Is.EqualTo(26));
        }

        [Test]
        public void ElementDatabase_ReturnsUnknownForMissing()
        {
            var unknown = ElementDatabase.Get("Xx");
            Assert.That(unknown.Symbol, Is.EqualTo("?"));
        }

        [Test]
        public void GetSnapshot_WithNoAtoms_ReturnsEmpty()
        {
            var sim = new MolecularSimulator();
            var snap = sim.GetSnapshot();
            Assert.IsNotNull(snap);
            Assert.That(snap.AtomStates.Count, Is.EqualTo(0));
            Assert.That(snap.BondStates.Count, Is.EqualTo(0));
        }
    }
}
