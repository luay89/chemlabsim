// ChemLabSim v3 — Molecular Simulator
// Pure C# — no Unity dependencies.
// Simulates molecular motion, bond vibrations, and reaction transformations.

using System;
using System.Collections.Generic;

namespace ChemLabSimV3.Domain.MolecularSimulation
{
    /// <summary>
    /// Simulates molecular movement using a simple molecular dynamics approach.
    /// Manages atom positions, bond constraints, and reaction transformations.
    /// </summary>
    public class MolecularSimulator
    {
        private readonly Random _rng = new Random();
        private readonly Dictionary<string, Atom> _atoms = new Dictionary<string, Atom>();
        private readonly List<Bond> _bonds = new List<Bond>();

        private ReactionTransformation _currentTransformation;
        private float _animationProgress;
        private bool _isAnimating;

        // Physics parameters
        private float _temperatureK = 298f;
        private const float Kb = 1.0f; // Scaled Boltzmann constant for simulation
        private const float Damping = 0.95f;
        private const float BondSpringK = 0.5f;
        private const float RepulsionK = 5.0f;
        private const float RepulsionRange = 200f;

        // ── Properties ──

        public bool IsAnimating => _isAnimating;
        public float AnimationProgress => _animationProgress;
        public ReactionTransformation CurrentTransformation => _currentTransformation;
        public int AtomCount => _atoms.Count;
        public int BondCount => _bonds.Count;

        // ── Temperature Control ──

        public void SetTemperatureC(float celsius)
        {
            _temperatureK = celsius + 273.15f;
        }

        public float GetTemperatureC()
        {
            return _temperatureK - 273.15f;
        }

        // ── Molecule Management ──

        /// <summary>
        /// Load molecules into the simulation as the starting state.
        /// </summary>
        public void LoadMolecules(IEnumerable<Molecule> molecules)
        {
            Clear();
            if (molecules == null) return;

            foreach (var mol in molecules)
            {
                if (mol?.Atoms == null) continue;

                foreach (var atom in mol.Atoms)
                {
                    if (!string.IsNullOrEmpty(atom.Id))
                        _atoms[atom.Id] = atom;
                }

                if (mol.Bonds != null)
                {
                    foreach (var bond in mol.Bonds)
                    {
                        if (bond != null &&
                            _atoms.ContainsKey(bond.AtomAId) &&
                            _atoms.ContainsKey(bond.AtomBId))
                        {
                            _bonds.Add(bond);
                        }
                    }
                }
            }

            // Give atoms initial random velocities for thermal motion
            foreach (var atom in _atoms.Values)
            {
                atom.Vx = (float)(_rng.NextDouble() - 0.5) * 2f;
                atom.Vy = (float)(_rng.NextDouble() - 0.5) * 2f;
                atom.Vz = (float)(_rng.NextDouble() - 0.5) * 2f;
            }
        }

        /// <summary>
        /// Start a reaction transformation animation.
        /// Transition atoms from reactant positions to product positions.
        /// </summary>
        public void StartTransformation(ReactionTransformation transformation)
        {
            _currentTransformation = transformation;
            _animationProgress = 0f;
            _isAnimating = true;

            // Clear existing atoms/bonds and load reactant structures
            Clear();

            if (transformation?.Reactants != null)
            {
                LoadMolecules(transformation.Reactants);
            }

            // Set simulation temperature from reaction
            if (transformation != null)
            {
                SetTemperatureC(transformation.TemperatureC);
            }
        }

        /// <summary>
        /// Advance the simulation by one time step.
        /// Returns snapshot of current atom/bond states.
        /// </summary>
        public MolecularSimulationSnapshot Step(float deltaTime)
        {
            if (_isAnimating)
            {
                _animationProgress += deltaTime / (_currentTransformation?.AnimationDurationSeconds ?? 3f);
                if (_animationProgress >= 1f)
                {
                    _animationProgress = 1f;
                    _isAnimating = false;
                }

                AnimateTransformation();
            }

            // Apply physics
            ApplyBrownianMotion(deltaTime);
            ApplyBondForces(deltaTime);
            ApplyRepulsionForces(deltaTime);
            ApplyDamping();
            IntegratePositions(deltaTime);
            ConstrainBounds();

            return GetSnapshot();
        }

        /// <summary>
        /// Get the current state of all atoms and bonds.
        /// </summary>
        public MolecularSimulationSnapshot GetSnapshot()
        {
            var snapshot = new MolecularSimulationSnapshot
            {
                Progress01 = _animationProgress,
                AtomStates = new List<AtomState>(),
                BondStates = new List<BondState>()
            };

            foreach (var atom in _atoms.Values)
            {
                if (atom == null) continue;
                var elem = atom.Element;
                snapshot.AtomStates.Add(new AtomState
                {
                    Id = atom.Id,
                    ElementSymbol = atom.ElementSymbol,
                    X = atom.X,
                    Y = atom.Y,
                    Z = atom.Z,
                    Radius = atom.RadiusPm / 100f, // Scale for rendering
                    ColorHex = elem.ColorHex,
                    Alpha = 1f
                });
            }

            foreach (var bond in _bonds)
            {
                if (bond == null) continue;
                if (!_atoms.ContainsKey(bond.AtomAId) || !_atoms.ContainsKey(bond.AtomBId))
                    continue;

                snapshot.BondStates.Add(new BondState
                {
                    AtomAId = bond.AtomAId,
                    AtomBId = bond.AtomBId,
                    Order = (int)bond.Order,
                    Alpha = 1f
                });
            }

            return snapshot;
        }

        // ── Private: Physics ──

        private void Clear()
        {
            _atoms.Clear();
            _bonds.Clear();
        }

        private void ApplyBrownianMotion(float dt)
        {
            float thermalEnergy = Math.Max(0.1f, _temperatureK / 1000f);

            foreach (var atom in _atoms.Values)
            {
                // Random thermal kick
                atom.Vx += (float)(_rng.NextDouble() - 0.5) * thermalEnergy * dt * 50f;
                atom.Vy += (float)(_rng.NextDouble() - 0.5) * thermalEnergy * dt * 50f;
                atom.Vz += (float)(_rng.NextDouble() - 0.5) * thermalEnergy * dt * 50f;
            }
        }

        private void ApplyBondForces(float dt)
        {
            foreach (var bond in _bonds)
            {
                if (!_atoms.TryGetValue(bond.AtomAId, out var a)) continue;
                if (!_atoms.TryGetValue(bond.AtomBId, out var b)) continue;

                float dx = b.X - a.X;
                float dy = b.Y - a.Y;
                float dz = b.Z - a.Z;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                if (dist < 0.001f) continue;

                // Equilibrium bond length depends on bond order
                float eqLength = ((a.RadiusPm + b.RadiusPm) / 100f) * 0.6f;
                int order = (int)bond.Order;
                eqLength *= (1f - (order - 1) * 0.08f); // Double/triple bonds are shorter

                float displacement = dist - eqLength;
                float forceMag = displacement * BondSpringK;

                float fx = dx / dist * forceMag;
                float fy = dy / dist * forceMag;
                float fz = dz / dist * forceMag;

                a.Vx += fx * dt;
                a.Vy += fy * dt;
                a.Vz += fz * dt;
                b.Vx -= fx * dt;
                b.Vy -= fy * dt;
                b.Vz -= fz * dt;
            }
        }

        private void ApplyRepulsionForces(float dt)
        {
            var atomsList = new List<Atom>(_atoms.Values);

            for (int i = 0; i < atomsList.Count; i++)
            {
                for (int j = i + 1; j < atomsList.Count; j++)
                {
                    var a = atomsList[i];
                    var b = atomsList[j];

                    float dx = b.X - a.X;
                    float dy = b.Y - a.Y;
                    float dz = b.Z - a.Z;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                    if (dist < 0.001f || dist > RepulsionRange) continue;

                    float minDist = (a.RadiusPm + b.RadiusPm) / 80f;
                    if (dist < minDist)
                    {
                        float overlap = minDist - dist;
                        float forceMag = overlap * RepulsionK / (dist * dist + 0.1f);

                        float fx = dx / dist * forceMag;
                        float fy = dy / dist * forceMag;
                        float fz = dz / dist * forceMag;

                        a.Vx -= fx * dt;
                        a.Vy -= fy * dt;
                        a.Vz -= fz * dt;
                        b.Vx += fx * dt;
                        b.Vy += fy * dt;
                        b.Vz += fz * dt;
                    }
                }
            }
        }

        private void ApplyDamping()
        {
            foreach (var atom in _atoms.Values)
            {
                atom.Vx *= Damping;
                atom.Vy *= Damping;
                atom.Vz *= Damping;
            }
        }

        private void IntegratePositions(float dt)
        {
            float maxVelocity = 15f;

            foreach (var atom in _atoms.Values)
            {
                // Clamp velocity
                float speed = (float)Math.Sqrt(
                    atom.Vx * atom.Vx + atom.Vy * atom.Vy + atom.Vz * atom.Vz);
                if (speed > maxVelocity)
                {
                    float scale = maxVelocity / speed;
                    atom.Vx *= scale;
                    atom.Vy *= scale;
                    atom.Vz *= scale;
                }

                atom.X += atom.Vx * dt;
                atom.Y += atom.Vy * dt;
                atom.Z += atom.Vz * dt;
            }
        }

        private void ConstrainBounds()
        {
            const float bounds = 5f;
            foreach (var atom in _atoms.Values)
            {
                if (atom.X > bounds) { atom.X = bounds; atom.Vx *= -0.5f; }
                if (atom.X < -bounds) { atom.X = -bounds; atom.Vx *= -0.5f; }
                if (atom.Y > bounds) { atom.Y = bounds; atom.Vy *= -0.5f; }
                if (atom.Y < -bounds) { atom.Y = -bounds; atom.Vy *= -0.5f; }
                if (atom.Z > bounds) { atom.Z = bounds; atom.Vz *= -0.5f; }
                if (atom.Z < -bounds) { atom.Z = -bounds; atom.Vz *= -0.5f; }
            }
        }

        // ── Private: Animation ──

        private void AnimateTransformation()
        {
            if (_currentTransformation?.Products == null) return;
            if (_currentTransformation.Products.Count == 0) return;

            float t = _animationProgress;

            // At t=1, load product molecules
            if (t >= 1f)
            {
                Clear();
                LoadMolecules(_currentTransformation.Products);
                return;
            }

            // During animation, gradually apply velocity toward product positions
            // This creates a smooth transition effect
            if (t > 0.1f && t < 1f)
            {
                float influence = Math.Min(1f, (t - 0.1f) / 0.5f);

                foreach (var product in _currentTransformation.Products)
                {
                    if (product?.Atoms == null) continue;

                    foreach (var targetAtom in product.Atoms)
                    {
                        if (_atoms.TryGetValue(targetAtom.Id, out var currentAtom))
                        {
                            // Pull toward target position
                            float dx = targetAtom.X - currentAtom.X;
                            float dy = targetAtom.Y - currentAtom.Y;
                            float dz = targetAtom.Z - currentAtom.Z;

                            currentAtom.Vx += dx * influence * 0.3f;
                            currentAtom.Vy += dy * influence * 0.3f;
                            currentAtom.Vz += dz * influence * 0.3f;
                        }
                    }
                }
            }
        }

        // ── Factory Methods ──

        /// <summary>
        /// Build a simple molecule model from a formula like "H2O" or "NaCl".
        /// Returns a Molecule with atoms positioned in a basic geometric arrangement.
        /// </summary>
        public static Molecule BuildFromFormula(string formula, float centerX, float centerY, float centerZ)
        {
            var mol = new Molecule { Formula = formula };
            int atomIndex = 0;

            // Parse simple formula (element + optional count)
            var elements = ParseFormula(formula);

            if (elements.Count == 1)
            {
                // Single element — place at center
                var (sym, count) = elements[0];
                for (int i = 0; i < count; i++)
                {
                    string id = $"{formula}_{atomIndex}";
                    float angle = (float)i / count * MathF.PI * 2;
                    float r = 0.3f;
                    mol.Atoms.Add(new Atom(id, sym,
                        centerX + MathF.Cos(angle) * r,
                        centerY + MathF.Sin(angle) * r,
                        centerZ + (i - count / 2f) * 0.1f));
                    atomIndex++;
                }
            }
            else if (elements.Count == 2)
            {
                // Diatomic or simple molecule — linear or bent
                var (symA, countA) = elements[0];
                var (symB, countB) = elements[1];

                // Center atom A
                string idA = $"{formula}_0";
                mol.Atoms.Add(new Atom(idA, symA, centerX - 0.5f, centerY, centerZ));

                // Atom B
                for (int i = 0; i < countB; i++)
                {
                    string idB = $"{formula}_{atomIndex}";
                    float angle = (i == 0) ? 0f : MathF.PI * 2 / countB * i;
                    mol.Atoms.Add(new Atom(idB, symB,
                        centerX + 0.5f + MathF.Cos(angle) * 0.2f,
                        centerY + MathF.Sin(angle) * 0.2f,
                        centerZ));
                    mol.Bonds.Add(new Bond(idA, idB));
                    atomIndex++;
                }
            }
            else
            {
                // More complex: simple layout
                float xOffset = -0.5f;
                foreach (var (sym, count) in elements)
                {
                    for (int i = 0; i < count; i++)
                    {
                        string id = $"{formula}_{atomIndex}";
                        mol.Atoms.Add(new Atom(id, sym,
                            centerX + xOffset,
                            centerY + (i - (count - 1f) / 2f) * 0.4f,
                            centerZ));
                        if (atomIndex > 0)
                        {
                            // Bond to previous atom
                            string prevId = $"{formula}_{atomIndex - 1}";
                            mol.Bonds.Add(new Bond(prevId, id));
                        }
                        atomIndex++;
                        xOffset += 0.6f;
                    }
                }
            }

            return mol;
        }

        private static List<(string symbol, int count)> ParseFormula(string formula)
        {
            var result = new List<(string, int)>();
            if (string.IsNullOrEmpty(formula)) return result;

            int i = 0;
            while (i < formula.Length)
            {
                // Read element symbol (uppercase + optional lowercase)
                if (!char.IsUpper(formula[i])) { i++; continue; }

                string symbol = formula[i].ToString();
                i++;
                while (i < formula.Length && char.IsLower(formula[i]))
                {
                    symbol += formula[i];
                    i++;
                }

                // Read count
                int count = 0;
                while (i < formula.Length && char.IsDigit(formula[i]))
                {
                    count = count * 10 + (formula[i] - '0');
                    i++;
                }
                if (count == 0) count = 1;

                result.Add((symbol, count));
            }

            return result;
        }
    }
}
