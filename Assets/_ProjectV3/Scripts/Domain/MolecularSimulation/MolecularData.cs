// ChemLabSim v3 — Molecular Simulation Data Models
// Pure C# — no Unity dependencies.
// Defines atoms, bonds, molecules, and reaction transformation data.

using System.Collections.Generic;

namespace ChemLabSimV3.Domain.MolecularSimulation
{
    /// <summary>
    /// Known element data: symbol, atomic number, atomic radius (pm), color (hex), group.
    /// </summary>
    public static class ElementDatabase
    {
        public static ElementData Get(string symbol)
        {
            return Data.TryGetValue(symbol ?? "", out var e) ? e : Unknown;
        }

        private static readonly ElementData Unknown = new ElementData
        {
            Symbol = "?", AtomicNumber = 0, RadiusPm = 40,
            ColorHex = "#CCCCCC", Group = "Unknown"
        };

        private static readonly Dictionary<string, ElementData> Data = new Dictionary<string, ElementData>
        {
            { "H",  new ElementData { Symbol="H",  AtomicNumber=1,  RadiusPm=25,  ColorHex="#FFFFFF", Group="Nonmetal" }},
            { "C",  new ElementData { Symbol="C",  AtomicNumber=6,  RadiusPm=70,  ColorHex="#404040", Group="Nonmetal" }},
            { "N",  new ElementData { Symbol="N",  AtomicNumber=7,  RadiusPm=65,  ColorHex="#3050F8", Group="Nonmetal" }},
            { "O",  new ElementData { Symbol="O",  AtomicNumber=8,  RadiusPm=60,  ColorHex="#FF0D0D", Group="Nonmetal" }},
            { "F",  new ElementData { Symbol="F",  AtomicNumber=9,  RadiusPm=50,  ColorHex="#90E050", Group="Halogen" }},
            { "Na", new ElementData { Symbol="Na", AtomicNumber=11, RadiusPm=180, ColorHex="#AB5CF2", Group="AlkaliMetal" }},
            { "Mg", new ElementData { Symbol="Mg", AtomicNumber=12, RadiusPm=150, ColorHex="#8AFF00", Group="AlkalineEarth" }},
            { "Al", new ElementData { Symbol="Al", AtomicNumber=13, RadiusPm=125, ColorHex="#BFA6A6", Group="Metal" }},
            { "Si", new ElementData { Symbol="Si", AtomicNumber=14, RadiusPm=110, ColorHex="#F0C8A0", Group="Metalloid" }},
            { "P",  new ElementData { Symbol="P",  AtomicNumber=15, RadiusPm=100, ColorHex="#FF8000", Group="Nonmetal" }},
            { "S",  new ElementData { Symbol="S",  AtomicNumber=16, RadiusPm=100, ColorHex="#FFFF30", Group="Nonmetal" }},
            { "Cl", new ElementData { Symbol="Cl", AtomicNumber=17, RadiusPm=100, ColorHex="#1FF01F", Group="Halogen" }},
            { "K",  new ElementData { Symbol="K",  AtomicNumber=19, RadiusPm=220, ColorHex="#8F40D4", Group="AlkaliMetal" }},
            { "Ca", new ElementData { Symbol="Ca", AtomicNumber=20, RadiusPm=180, ColorHex="#3DFF00", Group="AlkalineEarth" }},
            { "Cr", new ElementData { Symbol="Cr", AtomicNumber=24, RadiusPm=140, ColorHex="#8A99C7", Group="TransitionMetal" }},
            { "Mn", new ElementData { Symbol="Mn", AtomicNumber=25, RadiusPm=140, ColorHex="#9C7AC7", Group="TransitionMetal" }},
            { "Fe", new ElementData { Symbol="Fe", AtomicNumber=26, RadiusPm=140, ColorHex="#E06633", Group="TransitionMetal" }},
            { "Cu", new ElementData { Symbol="Cu", AtomicNumber=29, RadiusPm=135, ColorHex="#C88033", Group="TransitionMetal" }},
            { "Zn", new ElementData { Symbol="Zn", AtomicNumber=30, RadiusPm=135, ColorHex="#71D0E7", Group="TransitionMetal" }},
            { "Br", new ElementData { Symbol="Br", AtomicNumber=35, RadiusPm=115, ColorHex="#A62929", Group="Halogen" }},
            { "Ag", new ElementData { Symbol="Ag", AtomicNumber=47, RadiusPm=160, ColorHex="#C0C0C0", Group="TransitionMetal" }},
            { "I",  new ElementData { Symbol="I",  AtomicNumber=53, RadiusPm=140, ColorHex="#940094", Group="Halogen" }},
            { "Ba", new ElementData { Symbol="Ba", AtomicNumber=56, RadiusPm=215, ColorHex="#00C900", Group="AlkalineEarth" }},
            { "Au", new ElementData { Symbol="Au", AtomicNumber=79, RadiusPm=135, ColorHex="#FFD700", Group="TransitionMetal" }},
            { "Hg", new ElementData { Symbol="Hg", AtomicNumber=80, RadiusPm=150, ColorHex="#B8B8D0", Group="TransitionMetal" }},
            { "Pb", new ElementData { Symbol="Pb", AtomicNumber=82, RadiusPm=180, ColorHex="#575961", Group="Metal" }},
        };
    }

    public struct ElementData
    {
        public string Symbol;
        public int AtomicNumber;
        public float RadiusPm;
        public string ColorHex;
        public string Group;
    }

    /// <summary>
    /// A single atom in the molecular simulation.
    /// </summary>
    public class Atom
    {
        public string Id { get; set; }
        public string ElementSymbol { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Vx { get; set; }
        public float Vy { get; set; }
        public float Vz { get; set; }
        public float RadiusPm { get; set; }
        public int Charge { get; set; }  // Formal charge

        public ElementData Element => ElementDatabase.Get(ElementSymbol);

        public Atom(string id, string element, float x, float y, float z)
        {
            Id = id;
            ElementSymbol = element;
            X = x; Y = y; Z = z;
            Vx = Vy = Vz = 0f;
            RadiusPm = ElementDatabase.Get(element).RadiusPm;
        }
    }

    /// <summary>
    /// A chemical bond between two atoms.
    /// </summary>
    public class Bond
    {
        public string AtomAId { get; set; }
        public string AtomBId { get; set; }
        public BondOrder Order { get; set; }

        public Bond(string atomA, string atomB, BondOrder order = BondOrder.Single)
        {
            AtomAId = atomA;
            AtomBId = atomB;
            Order = order;
        }
    }

    public enum BondOrder
    {
        Single = 1,
        Double = 2,
        Triple = 3,
        Aromatic = 5
    }

    /// <summary>
    /// A molecule composed of atoms and bonds.
    /// </summary>
    public class Molecule
    {
        public string Formula { get; set; }
        public string Name { get; set; }
        public List<Atom> Atoms { get; set; }
        public List<Bond> Bonds { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float CenterZ { get; set; }

        public Molecule()
        {
            Atoms = new List<Atom>();
            Bonds = new List<Bond>();
        }
    }

    /// <summary>
    /// Describes a transformation of molecules during a reaction.
    /// Reactant molecules break bonds and rearrange into product molecules.
    /// </summary>
    public class ReactionTransformation
    {
        public List<Molecule> Reactants { get; set; }
        public List<Molecule> Products { get; set; }
        public List<BondChange> BondChanges { get; set; }
        public float TemperatureC { get; set; }
        public bool HasCatalyst { get; set; }
        public float AnimationDurationSeconds { get; set; } = 3f;
    }

    /// <summary>
    /// Describes a bond that breaks or forms during the reaction.
    /// </summary>
    public class BondChange
    {
        public string AtomAElement { get; set; }
        public string AtomBElement { get; set; }
        public BondChangeType ChangeType { get; set; }
        public BondOrder OldOrder { get; set; }
        public BondOrder NewOrder { get; set; }
    }

    public enum BondChangeType
    {
        Break,
        Form,
        ChangeOrder
    }

    /// <summary>
    /// Snapshot of the molecular simulation at a point in time.
    /// </summary>
    public class MolecularSimulationSnapshot
    {
        public List<AtomState> AtomStates { get; set; }
        public List<BondState> BondStates { get; set; }
        public float Progress01 { get; set; }  // 0 = reactants, 1 = products
    }

    public class AtomState
    {
        public string Id { get; set; }
        public string ElementSymbol { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Radius { get; set; }
        public string ColorHex { get; set; }
        public float Alpha { get; set; } = 1f;
    }

    public class BondState
    {
        public string AtomAId { get; set; }
        public string AtomBId { get; set; }
        public int Order { get; set; }
        public float Alpha { get; set; } = 1f;
    }
}
