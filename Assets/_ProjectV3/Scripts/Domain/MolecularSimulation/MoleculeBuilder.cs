// ChemLabSim v3 — Molecule Builder
// Pure C# — no Unity dependencies.
// Builds accurate 3D molecular models for common chemical species.
// Uses known bond angles and geometries (VSEPR theory).

using System;
using System.Collections.Generic;

namespace ChemLabSimV3.Domain.MolecularSimulation
{
    /// <summary>
    /// Builds accurate 3D molecular models for known molecules.
    /// Falls back to generic formula-based layout for unknown molecules.
    /// </summary>
    public static class MoleculeBuilder
    {
        private static readonly Dictionary<string, Func<float, float, float, Molecule>> Builders
            = new Dictionary<string, Func<float, float, float, Molecule>>
        {
            { "H2O",      BuildWater },
            { "CO2",      BuildCarbonDioxide },
            { "NH3",      BuildAmmonia },
            { "CH4",      BuildMethane },
            { "NaCl",     BuildSodiumChloride },
            { "HCl",      BuildHydrogenChloride },
            { "NaOH",     BuildSodiumHydroxide },
            { "O2",       BuildDioxygen },
            { "N2",       BuildDinitrogen },
            { "H2",       BuildDihydrogen },
            { "Cl2",      BuildDichlorine },
            { "C2H4",     BuildEthene },
            { "C2H5OH",   BuildEthanol },
            { "CH3COOH",  BuildAceticAcid },
            { "CH3OH",    BuildMethanol },
            { "H2SO4",    BuildSulfuricAcid },
            // HNO3 (Nitric Acid) - uses generic fallback
            { "C6H12O6",  BuildGlucose },
            { "C2H2",     BuildEthyne },
            { "C6H6",     BuildBenzene },
            { "CO",       BuildCarbonMonoxide },
            { "NO2",      BuildNitrogenDioxide },
            { "SO2",      BuildSulfurDioxide },
            { "SO3",      BuildSulfurTrioxide },
            { "Fe2O3",    BuildIronOxide },
            { "CaCO3",    BuildCalciumCarbonate },
            { "AgCl",     BuildSilverChloride },
            { "CuSO4",    BuildCopperSulfate },
            { "H2O2",     BuildHydrogenPeroxide },
            { "MgO",      BuildMagnesiumOxide },
            { "Al2O3",    BuildAluminumOxide },
            { "SiO2",     BuildSiliconDioxide },
            { "KMnO4",    BuildPotassiumPermanganate },
            { "NaHCO3",   BuildSodiumBicarbonate },
            { "CH3COONa", BuildSodiumAcetate },
            { "C12H22O11",BuildSucrose },
        };

        /// <summary>
        /// Build a molecule from its formula. Uses known structures when available,
        /// falls back to generic layout.
        /// </summary>
        public static Molecule Build(string formula, float cx, float cy, float cz)
        {
            if (string.IsNullOrEmpty(formula))
                return new Molecule { Formula = "", Atoms = new List<Atom>(), Bonds = new List<Bond>() };

            formula = formula.Trim();

            if (Builders.TryGetValue(formula, out var builder))
            {
                var mol = builder(cx, cy, cz);
                mol.Formula = formula;
                return mol;
            }

            // Fallback: generic layout
            return MolecularSimulator.BuildFromFormula(formula, cx, cy, cz);
        }

        // ═══════════════════════════════════════════════════════════
        //  KNOWN MOLECULAR STRUCTURES
        // ═══════════════════════════════════════════════════════════

        // ── Water: H₂O (bent, 104.5°) ──
        private static Molecule BuildWater(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            float bondLen = 0.7f;
            float angle = 104.5f * (float)(System.Math.PI / 180.0) / 2f;

            var o = new Atom("O", "O", cx, cy, cz);
            var h1 = new Atom("H1", "H", cx - bondLen * (float)System.Math.Sin(angle), cy + bondLen * (float)System.Math.Cos(angle), cz);
            var h2 = new Atom("H2", "H", cx + bondLen * (float)System.Math.Sin(angle), cy + bondLen * (float)System.Math.Cos(angle), cz);

            mol.Atoms.AddRange(new[] { o, h1, h2 });
            mol.Bonds.AddRange(new[] { new Bond("O", "H1"), new Bond("O", "H2") });
            return mol;
        }

        // ── Carbon Dioxide: CO₂ (linear, 180°) ──
        private static Molecule BuildCarbonDioxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var c = new Atom("C", "C", cx, cy, cz);
            var o1 = new Atom("O1", "O", cx - 0.7f, cy, cz);
            var o2 = new Atom("O2", "O", cx + 0.7f, cy, cz);
            mol.Atoms.AddRange(new[] { c, o1, o2 });
            mol.Bonds.AddRange(new[] { new Bond("C", "O1", BondOrder.Double), new Bond("C", "O2", BondOrder.Double) });
            return mol;
        }

        // ── Ammonia: NH₃ (trigonal pyramidal, 107°) ──
        private static Molecule BuildAmmonia(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var n = new Atom("N", "N", cx, cy + 0.2f, cz);
            float r = 0.7f;
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2f * (float)System.Math.PI / 3f;
                var h = new Atom($"H{i}", "H", cx + r * (float)System.Math.Cos(a), cy - 0.3f, cz + r * (float)System.Math.Sin(a));
                mol.Atoms.Add(h);
                mol.Bonds.Add(new Bond("N", h.Id));
            }
            mol.Atoms.Insert(0, n);
            return mol;
        }

        // ── Methane: CH₄ (tetrahedral, 109.5°) ──
        private static Molecule BuildMethane(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var c = new Atom("C", "C", cx, cy, cz);
            mol.Atoms.Add(c);
            float r = 0.7f;
            var dirs = new[] {
                (1f, 1f, 1f), (1f, -1f, -1f), (-1f, 1f, -1f), (-1f, -1f, 1f)
            };
            for (int i = 0; i < 4; i++)
            {
                float len = r / (float)System.Math.Sqrt(3f);
                var h = new Atom($"H{i}", "H", cx + dirs[i].Item1 * len, cy + dirs[i].Item2 * len, cz + dirs[i].Item3 * len);
                mol.Atoms.Add(h);
                mol.Bonds.Add(new Bond("C", h.Id));
            }
            return mol;
        }

        // ── Sodium Chloride: NaCl (ionic pair) ──
        private static Molecule BuildSodiumChloride(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Na", "Na", cx - 0.6f, cy, cz));
            mol.Atoms.Add(new Atom("Cl", "Cl", cx + 0.6f, cy, cz));
            mol.Bonds.Add(new Bond("Na", "Cl"));
            return mol;
        }

        // ── Hydrogen Chloride: HCl ──
        private static Molecule BuildHydrogenChloride(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("H", "H", cx - 0.6f, cy, cz));
            mol.Atoms.Add(new Atom("Cl", "Cl", cx + 0.6f, cy, cz));
            mol.Bonds.Add(new Bond("H", "Cl"));
            return mol;
        }

        // ── Sodium Hydroxide: NaOH ──
        private static Molecule BuildSodiumHydroxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Na", "Na", cx - 0.8f, cy, cz));
            mol.Atoms.Add(new Atom("O", "O", cx + 0.2f, cy, cz));
            mol.Atoms.Add(new Atom("H", "H", cx + 0.8f, cy + 0.3f, cz));
            mol.Bonds.Add(new Bond("Na", "O"));
            mol.Bonds.Add(new Bond("O", "H"));
            return mol;
        }

        // ── Dioxygen: O₂ (double bond) ──
        private static Molecule BuildDioxygen(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("O1", "O", cx - 0.5f, cy, cz));
            mol.Atoms.Add(new Atom("O2", "O", cx + 0.5f, cy, cz));
            mol.Bonds.Add(new Bond("O1", "O2", BondOrder.Double));
            return mol;
        }

        // ── Dinitrogen: N₂ (triple bond) ──
        private static Molecule BuildDinitrogen(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("N1", "N", cx - 0.45f, cy, cz));
            mol.Atoms.Add(new Atom("N2", "N", cx + 0.45f, cy, cz));
            mol.Bonds.Add(new Bond("N1", "N2", BondOrder.Triple));
            return mol;
        }

        // ── Dihydrogen: H₂ ──
        private static Molecule BuildDihydrogen(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("H1", "H", cx - 0.35f, cy, cz));
            mol.Atoms.Add(new Atom("H2", "H", cx + 0.35f, cy, cz));
            mol.Bonds.Add(new Bond("H1", "H2"));
            return mol;
        }

        // ── Dichlorine: Cl₂ ──
        private static Molecule BuildDichlorine(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Cl1", "Cl", cx - 0.6f, cy, cz));
            mol.Atoms.Add(new Atom("Cl2", "Cl", cx + 0.6f, cy, cz));
            mol.Bonds.Add(new Bond("Cl1", "Cl2"));
            return mol;
        }

        // ── Ethene: C₂H₄ (planar, 120°) ──
        private static Molecule BuildEthene(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var c1 = new Atom("C1", "C", cx - 0.4f, cy, cz);
            var c2 = new Atom("C2", "C", cx + 0.4f, cy, cz);
            mol.Atoms.AddRange(new[] { c1, c2 });
            mol.Bonds.Add(new Bond("C1", "C2", BondOrder.Double));

            var hPositions = new[] {
                (-0.6f, 0.5f), (-0.6f, -0.5f), (0.6f, 0.5f), (0.6f, -0.5f)
            };
            for (int i = 0; i < 4; i++)
            {
                var h = new Atom($"H{i}", "H", cx + hPositions[i].Item1, cy + hPositions[i].Item2, cz);
                mol.Atoms.Add(h);
                mol.Bonds.Add(new Bond(i < 2 ? "C1" : "C2", h.Id));
            }
            return mol;
        }

        // ── Ethanol: C₂H₅OH ──
        private static Molecule BuildEthanol(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var c1 = new Atom("C1", "C", cx - 0.5f, cy, cz);
            var c2 = new Atom("C2", "C", cx + 0.5f, cy, cz);
            var o = new Atom("O", "O", cx + 1.2f, cy - 0.3f, cz);
            var oh = new Atom("OH", "H", cx + 1.6f, cy - 0.6f, cz);
            mol.Atoms.AddRange(new[] { c1, c2, o, oh });
            mol.Bonds.Add(new Bond("C1", "C2"));
            mol.Bonds.Add(new Bond("C2", "O"));
            mol.Bonds.Add(new Bond("O", "OH"));

            // Methyl H's
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2f * (float)System.Math.PI / 3f;
                var h = new Atom($"H1_{i}", "H", cx - 0.5f + 0.5f * (float)System.Math.Cos(a), cy + 0.5f * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(h);
                mol.Bonds.Add(new Bond("C1", h.Id));
            }
            // Methylene H's
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 0.5f : -0.5f;
                var h = new Atom($"H2_{i}", "H", cx + 0.5f, cy + s * 0.5f, cz + 0.3f);
                mol.Atoms.Add(h);
                mol.Bonds.Add(new Bond("C2", h.Id));
            }
            return mol;
        }

        // ── Acetic Acid: CH₃COOH ──
        private static Molecule BuildAceticAcid(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var c1 = new Atom("C1", "C", cx - 0.5f, cy, cz);
            var c2 = new Atom("C2", "C", cx + 0.5f, cy, cz);
            var o1 = new Atom("O1", "O", cx + 1.0f, cy + 0.4f, cz);
            var o2 = new Atom("O2", "O", cx + 1.0f, cy - 0.4f, cz);
            var oh = new Atom("OH", "H", cx + 1.4f, cy - 0.7f, cz);
            mol.Atoms.AddRange(new[] { c1, c2, o1, o2, oh });
            mol.Bonds.Add(new Bond("C1", "C2"));
            mol.Bonds.Add(new Bond("C2", "O1", BondOrder.Double));
            mol.Bonds.Add(new Bond("C2", "O2"));
            mol.Bonds.Add(new Bond("O2", "OH"));
            // Methyl H's
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2f * (float)System.Math.PI / 3f;
                var h = new Atom($"H{i}", "H", cx - 0.5f + 0.5f * (float)System.Math.Cos(a), cy + 0.5f * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(h);
                mol.Bonds.Add(new Bond("C1", h.Id));
            }
            return mol;
        }

        // ── Sulfuric Acid: H₂SO₄ ──
        private static Molecule BuildSulfuricAcid(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var s = new Atom("S", "S", cx, cy, cz);
            mol.Atoms.Add(s);
            float r = 0.6f;
            for (int i = 0; i < 4; i++)
            {
                float a = i * (float)System.Math.PI / 2f;
                var o = new Atom($"O{i}", "O", cx + r * (float)System.Math.Cos(a), cy + r * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(o);
                mol.Bonds.Add(new Bond("S", o.Id, i < 2 ? BondOrder.Double : BondOrder.Single));
            }
            // Add H's to single-bonded O's
            var h1 = new Atom("H1", "H", cx + r * (float)System.Math.Cos(2 * (float)System.Math.PI * 2 / 4) + 0.5f, cy + r * (float)System.Math.Sin(2 * (float)System.Math.PI * 2 / 4), cz);
            var h2 = new Atom("H2", "H", cx + r * (float)System.Math.Cos(2 * (float)System.Math.PI * 3 / 4), cy + r * (float)System.Math.Sin(2 * (float)System.Math.PI * 3 / 4) - 0.5f, cz);
            mol.Atoms.AddRange(new[] { h1, h2 });
            mol.Bonds.Add(new Bond("O2", "H1"));
            mol.Bonds.Add(new Bond("O3", "H2"));
            return mol;
        }

        // ── Glucose: C₆H₁₂O₆ (simplified ring) ──
        private static Molecule BuildGlucose(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            // Simplified 6-carbon ring
            float r = 0.45f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * (float)System.Math.PI / 3f - (float)System.Math.PI / 6f;
                var c = new Atom($"C{i}", "C", cx + r * (float)System.Math.Cos(a), cy + r * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(c);
                if (i > 0)
                    mol.Bonds.Add(new Bond($"C{i - 1}", $"C{i}"));
            }
            mol.Bonds.Add(new Bond("C5", "C0")); // Close the ring
            // Add O's (simplified)
            for (int i = 0; i < 6; i++)
            {
                float a = i * (float)System.Math.PI / 3f - (float)System.Math.PI / 6f;
                var o = new Atom($"O{i}", "O",
                    cx + (r + 0.3f) * (float)System.Math.Cos(a),
                    cy + (r + 0.3f) * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(o);
                mol.Bonds.Add(new Bond($"C{i}", o.Id));
            }
            return mol;
        }

        // ── Benzene: C₆H₆ (aromatic ring) ──
        private static Molecule BuildBenzene(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            float r = 0.4f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * (float)System.Math.PI / 3f;
                var c = new Atom($"C{i}", "C", cx + r * (float)System.Math.Cos(a), cy + r * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(c);
                if (i > 0)
                    mol.Bonds.Add(new Bond($"C{i - 1}", $"C{i}",
                        i % 2 == 1 ? BondOrder.Double : BondOrder.Single));
                var h = new Atom($"H{i}", "H",
                    cx + (r + 0.35f) * (float)System.Math.Cos(a),
                    cy + (r + 0.35f) * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(h);
                mol.Bonds.Add(new Bond($"C{i}", $"H{i}"));
            }
            mol.Bonds.Add(new Bond("C5", "C0", BondOrder.Single));
            return mol;
        }

        // ── Simple diatomic/molecular helpers ──

        private static Molecule BuildCarbonMonoxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("C", "C", cx - 0.4f, cy, cz));
            mol.Atoms.Add(new Atom("O", "O", cx + 0.4f, cy, cz));
            mol.Bonds.Add(new Bond("C", "O", BondOrder.Triple));
            return mol;
        }

        private static Molecule BuildNitrogenDioxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var n = new Atom("N", "N", cx, cy, cz);
            mol.Atoms.Add(n);
            mol.Atoms.Add(new Atom("O1", "O", cx - 0.5f, cy + 0.4f, cz));
            mol.Atoms.Add(new Atom("O2", "O", cx + 0.5f, cy - 0.4f, cz));
            mol.Bonds.Add(new Bond("N", "O1", BondOrder.Double));
            mol.Bonds.Add(new Bond("N", "O2", BondOrder.Single));
            return mol;
        }

        private static Molecule BuildSulfurDioxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var s = new Atom("S", "S", cx, cy, cz);
            mol.Atoms.Add(s);
            mol.Atoms.Add(new Atom("O1", "O", cx - 0.5f, cy + 0.4f, cz));
            mol.Atoms.Add(new Atom("O2", "O", cx + 0.5f, cy + 0.4f, cz));
            mol.Bonds.Add(new Bond("S", "O1", BondOrder.Double));
            mol.Bonds.Add(new Bond("S", "O2", BondOrder.Double));
            return mol;
        }

        private static Molecule BuildSulfurTrioxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var s = new Atom("S", "S", cx, cy, cz);
            mol.Atoms.Add(s);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2f * (float)System.Math.PI / 3f;
                var o = new Atom($"O{i}", "O", cx + 0.6f * (float)System.Math.Cos(a), cy + 0.6f * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(o);
                mol.Bonds.Add(new Bond("S", o.Id, BondOrder.Double));
            }
            return mol;
        }

        private static Molecule BuildEthyne(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("C1", "C", cx - 0.3f, cy, cz));
            mol.Atoms.Add(new Atom("C2", "C", cx + 0.3f, cy, cz));
            mol.Atoms.Add(new Atom("H1", "H", cx - 0.7f, cy, cz));
            mol.Atoms.Add(new Atom("H2", "H", cx + 0.7f, cy, cz));
            mol.Bonds.Add(new Bond("C1", "C2", BondOrder.Triple));
            mol.Bonds.Add(new Bond("H1", "C1"));
            mol.Bonds.Add(new Bond("H2", "C2"));
            return mol;
        }

        private static Molecule BuildIronOxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Fe1", "Fe", cx - 0.5f, cy, cz));
            mol.Atoms.Add(new Atom("Fe2", "Fe", cx + 0.5f, cy, cz));
            mol.Atoms.Add(new Atom("O1", "O", cx, cy + 0.5f, cz));
            mol.Atoms.Add(new Atom("O2", "O", cx - 0.3f, cy - 0.4f, cz));
            mol.Atoms.Add(new Atom("O3", "O", cx + 0.3f, cy - 0.4f, cz));
            mol.Bonds.Add(new Bond("Fe1", "O1"));
            mol.Bonds.Add(new Bond("Fe1", "O2"));
            mol.Bonds.Add(new Bond("Fe2", "O1"));
            mol.Bonds.Add(new Bond("Fe2", "O3"));
            return mol;
        }

        private static Molecule BuildCalciumCarbonate(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Ca", "Ca", cx - 0.6f, cy, cz));
            var c = new Atom("C", "C", cx + 0.3f, cy, cz);
            mol.Atoms.Add(c);
            mol.Atoms.Add(new Atom("O1", "O", cx + 0.8f, cy, cz));
            mol.Atoms.Add(new Atom("O2", "O", cx + 0.1f, cy + 0.5f, cz));
            mol.Atoms.Add(new Atom("O3", "O", cx + 0.1f, cy - 0.5f, cz));
            mol.Bonds.Add(new Bond("Ca", "C"));
            mol.Bonds.Add(new Bond("C", "O1", BondOrder.Double));
            mol.Bonds.Add(new Bond("C", "O2"));
            mol.Bonds.Add(new Bond("C", "O3"));
            return mol;
        }

        private static Molecule BuildSilverChloride(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Ag", "Ag", cx - 0.65f, cy, cz));
            mol.Atoms.Add(new Atom("Cl", "Cl", cx + 0.65f, cy, cz));
            mol.Bonds.Add(new Bond("Ag", "Cl"));
            return mol;
        }

        private static Molecule BuildCopperSulfate(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Cu", "Cu", cx - 0.6f, cy, cz));
            var s = new Atom("S", "S", cx + 0.2f, cy, cz);
            mol.Atoms.Add(s);
            for (int i = 0; i < 4; i++)
            {
                float a = i * (float)System.Math.PI / 2f;
                var o = new Atom($"O{i}", "O", cx + 0.2f + 0.4f * (float)System.Math.Cos(a), cy + 0.4f * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(o);
                mol.Bonds.Add(new Bond("S", o.Id));
            }
            mol.Bonds.Add(new Bond("Cu", "S"));
            return mol;
        }

        private static Molecule BuildHydrogenPeroxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("O1", "O", cx - 0.35f, cy + 0.2f, cz));
            mol.Atoms.Add(new Atom("O2", "O", cx + 0.35f, cy - 0.2f, cz));
            mol.Atoms.Add(new Atom("H1", "H", cx - 0.65f, cy + 0.5f, cz));
            mol.Atoms.Add(new Atom("H2", "H", cx + 0.65f, cy - 0.5f, cz));
            mol.Bonds.Add(new Bond("O1", "O2"));
            mol.Bonds.Add(new Bond("H1", "O1"));
            mol.Bonds.Add(new Bond("H2", "O2"));
            return mol;
        }

        private static Molecule BuildMagnesiumOxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Mg", "Mg", cx - 0.55f, cy, cz));
            mol.Atoms.Add(new Atom("O", "O", cx + 0.55f, cy, cz));
            mol.Bonds.Add(new Bond("Mg", "O"));
            return mol;
        }

        private static Molecule BuildAluminumOxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Al1", "Al", cx - 0.5f, cy, cz));
            mol.Atoms.Add(new Atom("Al2", "Al", cx + 0.5f, cy, cz));
            mol.Atoms.Add(new Atom("O1", "O", cx, cy + 0.5f, cz));
            mol.Atoms.Add(new Atom("O2", "O", cx - 0.3f, cy - 0.4f, cz));
            mol.Atoms.Add(new Atom("O3", "O", cx + 0.3f, cy - 0.4f, cz));
            mol.Bonds.Add(new Bond("Al1", "O1"));
            mol.Bonds.Add(new Bond("Al1", "O2"));
            mol.Bonds.Add(new Bond("Al2", "O1"));
            mol.Bonds.Add(new Bond("Al2", "O3"));
            return mol;
        }

        private static Molecule BuildMethanol(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var c = new Atom("C", "C", cx - 0.4f, cy, cz);
            var o = new Atom("O", "O", cx + 0.4f, cy, cz);
            var oh = new Atom("OH", "H", cx + 0.8f, cy + 0.3f, cz);
            mol.Atoms.AddRange(new[] { c, o, oh });
            mol.Bonds.Add(new Bond("C", "O"));
            mol.Bonds.Add(new Bond("O", "OH"));
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2f * (float)System.Math.PI / 3f;
                var h = new Atom($"H{i}", "H", cx - 0.4f + 0.4f * (float)System.Math.Cos(a), cy + 0.4f * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(h);
                mol.Bonds.Add(new Bond("C", h.Id));
            }
            return mol;
        }

        // ── Remaining builders ──

        private static Molecule BuildSiliconDioxide(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            var si = new Atom("Si", "Si", cx, cy, cz);
            mol.Atoms.Add(si);
            for (int i = 0; i < 2; i++)
            {
                float a = i * (float)System.Math.PI;
                var o = new Atom($"O{i}", "O", cx + 0.6f * (float)System.Math.Cos(a), cy + 0.6f * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(o);
                mol.Bonds.Add(new Bond("Si", o.Id, BondOrder.Double));
            }
            return mol;
        }

        private static Molecule BuildPotassiumPermanganate(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("K", "K", cx - 0.7f, cy, cz));
            var mn = new Atom("Mn", "Mn", cx + 0.2f, cy, cz);
            mol.Atoms.Add(mn);
            for (int i = 0; i < 4; i++)
            {
                float a = i * (float)System.Math.PI / 2f;
                var o = new Atom($"O{i}", "O", cx + 0.2f + 0.5f * (float)System.Math.Cos(a), cy + 0.5f * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(o);
                mol.Bonds.Add(new Bond("Mn", o.Id, BondOrder.Double));
            }
            mol.Bonds.Add(new Bond("K", "Mn"));
            return mol;
        }

        private static Molecule BuildSodiumBicarbonate(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Na", "Na", cx - 0.8f, cy, cz));
            var c = new Atom("C", "C", cx + 0.1f, cy, cz);
            mol.Atoms.Add(c);
            mol.Atoms.Add(new Atom("O1", "O", cx + 0.6f, cy, cz));
            mol.Atoms.Add(new Atom("O2", "O", cx - 0.2f, cy + 0.5f, cz));
            mol.Atoms.Add(new Atom("O3", "O", cx - 0.2f, cy - 0.5f, cz));
            mol.Atoms.Add(new Atom("H", "H", cx - 0.2f, cy - 0.9f, cz));
            mol.Bonds.Add(new Bond("Na", "C"));
            mol.Bonds.Add(new Bond("C", "O1", BondOrder.Double));
            mol.Bonds.Add(new Bond("C", "O2"));
            mol.Bonds.Add(new Bond("C", "O3"));
            mol.Bonds.Add(new Bond("O3", "H"));
            return mol;
        }

        private static Molecule BuildSodiumAcetate(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            mol.Atoms.Add(new Atom("Na", "Na", cx - 1.0f, cy, cz));
            var c1 = new Atom("C1", "C", cx - 0.3f, cy, cz);
            var c2 = new Atom("C2", "C", cx + 0.4f, cy, cz);
            mol.Atoms.AddRange(new[] { c1, c2 });
            mol.Atoms.Add(new Atom("O1", "O", cx + 0.9f, cy + 0.4f, cz));
            mol.Atoms.Add(new Atom("O2", "O", cx + 0.9f, cy - 0.4f, cz));
            mol.Bonds.Add(new Bond("Na", "C1"));
            mol.Bonds.Add(new Bond("C1", "C2"));
            mol.Bonds.Add(new Bond("C2", "O1", BondOrder.Double));
            mol.Bonds.Add(new Bond("C2", "O2"));
            // Methyl H's on C1
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2f * (float)System.Math.PI / 3f;
                var h = new Atom($"H{i}", "H", cx - 0.3f + 0.4f * (float)System.Math.Cos(a), cy + 0.4f * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(h);
                mol.Bonds.Add(new Bond("C1", h.Id));
            }
            return mol;
        }

        private static Molecule BuildSucrose(float cx, float cy, float cz)
        {
            var mol = new Molecule();
            // Simplified: two connected glucose-like rings
            float r = 0.35f;
            // Ring 1 (left)
            for (int i = 0; i < 6; i++)
            {
                float a = i * (float)System.Math.PI / 3f + (float)System.Math.PI / 6f;
                var c = new Atom($"C1_{i}", "C", cx - 0.6f + r * (float)System.Math.Cos(a), cy + r * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(c);
                if (i > 0) mol.Bonds.Add(new Bond($"C1_{i - 1}", $"C1_{i}"));
            }
            mol.Bonds.Add(new Bond("C1_5", "C1_0"));
            // Ring 2 (right)
            for (int i = 0; i < 6; i++)
            {
                float a = i * (float)System.Math.PI / 3f - (float)System.Math.PI / 6f;
                var c = new Atom($"C2_{i}", "C", cx + 0.6f + r * (float)System.Math.Cos(a), cy + r * (float)System.Math.Sin(a), cz);
                mol.Atoms.Add(c);
                if (i > 0) mol.Bonds.Add(new Bond($"C2_{i - 1}", $"C2_{i}"));
            }
            mol.Bonds.Add(new Bond("C2_5", "C2_0"));
            // Bridge bond between rings
            mol.Bonds.Add(new Bond("C1_0", "C2_0"));
            return mol;
        }

        // Uses System.Math instead of UnityEngine.Mathf (pure C#)
    }
}
