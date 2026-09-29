// ChemLabSim v3 — Molecular Renderer View
// Unity view layer. Renders atoms as 3D spheres with bonds as cylinders.
// Drives the molecular simulation and displays the reaction animation.

using UnityEngine;
using System.Collections.Generic;
using ChemLabSimV3.Domain.MolecularSimulation;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Views
{
    public class MolecularRendererView : MonoBehaviour
    {
        [Header("Rendering Settings")]
        [SerializeField] private float atomScale = 0.3f;
        [SerializeField] private float bondThickness = 0.03f;
        [SerializeField] private Material atomMaterial;
        [SerializeField] private Material bondMaterial;
        [SerializeField] private bool showLabels = true;

        [Header("Camera")]
        [SerializeField] private Camera renderCamera;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -6f);

        // ── Runtime State ──
        private MolecularSimulator _simulator;
        private GameObject _container;
        private Dictionary<string, GameObject> _atomSpheres;
        private Dictionary<string, GameObject> _bondCylinders;
        private Dictionary<string, TextMesh> _atomLabels;
        private Material _defaultAtomMat;
        private Material _defaultBondMat;

        // Atom colors cache
        private static readonly Dictionary<string, Color> ElementColors = new Dictionary<string, Color>
        {
            { "H", Color.white },
            { "C", new Color(0.25f, 0.25f, 0.25f) },
            { "N", new Color(0.19f, 0.31f, 0.97f) },
            { "O", Color.red },
            { "F", new Color(0.56f, 0.88f, 0.31f) },
            { "Na", new Color(0.67f, 0.36f, 0.95f) },
            { "Mg", new Color(0.54f, 1f, 0f) },
            { "Al", new Color(0.75f, 0.65f, 0.65f) },
            { "Si", new Color(0.94f, 0.78f, 0.63f) },
            { "P", new Color(1f, 0.50f, 0f) },
            { "S", new Color(1f, 1f, 0.19f) },
            { "Cl", new Color(0.12f, 0.94f, 0.12f) },
            { "Fe", new Color(0.88f, 0.40f, 0.20f) },
            { "Cu", new Color(0.78f, 0.50f, 0.20f) },
            { "Zn", new Color(0.44f, 0.82f, 0.91f) },
            { "Ag", new Color(0.75f, 0.75f, 0.75f) },
            { "I", new Color(0.58f, 0f, 0.58f) },
            { "Au", new Color(1f, 0.84f, 0f) },
            { "Br", new Color(0.65f, 0.16f, 0.16f) },
            { "Hg", new Color(0.72f, 0.72f, 0.84f) },
            { "Pb", new Color(0.34f, 0.35f, 0.38f) },
        };

        private void Awake()
        {
            _simulator = new MolecularSimulator();
            _atomSpheres = new Dictionary<string, GameObject>();
            _bondCylinders = new Dictionary<string, GameObject>();
            _atomLabels = new Dictionary<string, TextMesh>();

            _container = new GameObject("MolecularSimulation");
            _container.transform.SetParent(transform, false);

            CreateDefaultMaterials();

            // Create render camera if none assigned
            if (renderCamera == null)
            {
                var camGO = new GameObject("MolSimCamera");
                camGO.transform.SetParent(_container.transform, false);
                camGO.transform.localPosition = cameraOffset;
                renderCamera = camGO.AddComponent<Camera>();
                renderCamera.clearFlags = CameraClearFlags.Depth;
                renderCamera.orthographic = true;
                renderCamera.orthographicSize = 4f;
                renderCamera.nearClipPlane = 0.1f;
                renderCamera.farClipPlane = 20f;
                renderCamera.depth = 1;
                renderCamera.cullingMask = 1 << gameObject.layer;
            }

            SubscribeToEvents();
        }

        private void OnEnable()
        {
            if (_simulator != null)
            {
                // Load default demo molecule if no reaction is active
                if (_simulator.AtomCount == 0)
                {
                    LoadDemoMolecules();
                }
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (_simulator == null) return;

            // Step the simulation
            float dt = Time.deltaTime;
            var snapshot = _simulator.Step(dt);

            // Update visual representations
            UpdateAtomVisuals(snapshot);
            UpdateBondVisuals(snapshot);

            // Slow rotation for better viewing
            _container.transform.Rotate(Vector3.up, dt * 15f, Space.World);
        }

        // ── Event Subscription ──

        private void SubscribeToEvents()
        {
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Subscribe<ChemistryProcessedEvent>(OnChemistryProcessed);
        }

        private void UnsubscribeFromEvents()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Unsubscribe<ChemistryProcessedEvent>(OnChemistryProcessed);
        }

        private void OnReactionEvaluated(ReactionEvaluatedEvent evt)
        {
            if (evt.Input.reaction == null) return;

            // Build molecules from reaction data
            var transformation = BuildTransformationFromReaction(evt.Input.reaction);
            if (transformation != null)
            {
                _simulator.StartTransformation(transformation);
                ClearVisuals();
            }
        }

        private void OnChemistryProcessed(ChemistryProcessedEvent evt)
        {
            if (evt.Output.Substances == null) return;

            // Update based on chemistry output (temperature, phase changes)
            _simulator.SetTemperatureC(evt.Output.TemperatureC);
        }

        // ── Visual Update ──

        private void UpdateAtomVisuals(MolecularSimulationSnapshot snapshot)
        {
            if (snapshot?.AtomStates == null) return;

            foreach (var atomState in snapshot.AtomStates)
            {
                if (atomState == null) continue;

                Vector3 pos = new Vector3(atomState.X, atomState.Y, atomState.Z);

                if (_atomSpheres.TryGetValue(atomState.Id, out var sphere))
                {
                    sphere.transform.localPosition = pos;
                    float size = atomState.Radius * atomScale;
                    sphere.transform.localScale = new Vector3(size, size, size);

                    // Update color
                    if (TryGetElementColor(atomState.ElementSymbol, out Color color))
                    {
                        var renderer = sphere.GetComponent<MeshRenderer>();
                        if (renderer != null)
                        {
                            var mat = renderer.material;
                            mat.color = new Color(color.r, color.g, color.b, atomState.Alpha);
                        }
                    }

                    // Update label
                    if (_atomLabels.TryGetValue(atomState.Id, out var label) && label != null)
                    {
                        label.transform.localPosition = pos + Vector3.up * (atomState.Radius * atomScale + 0.15f);
                        label.text = showLabels ? atomState.ElementSymbol : "";
                    }
                }
                else
                {
                    CreateAtomSphere(atomState, pos);
                }
            }

            // Remove stale atoms
            var currentIds = new HashSet<string>();
            foreach (var a in snapshot.AtomStates)
            {
                if (a != null && a.Id != null) currentIds.Add(a.Id);
            }
            RemoveStaleVisuals(_atomSpheres, currentIds);
            RemoveStaleVisuals(_atomLabels, currentIds);
        }

        private void UpdateBondVisuals(MolecularSimulationSnapshot snapshot)
        {
            if (snapshot?.BondStates == null) return;

            // Create a set of current bond keys
            var currentBonds = new HashSet<string>();
            foreach (var bondState in snapshot.BondStates)
            {
                if (bondState == null) continue;
                string key = GetBondKey(bondState.AtomAId, bondState.AtomBId);
                currentBonds.Add(key);

                if (_bondCylinders.TryGetValue(key, out var cylinder))
                {
                    UpdateBondCylinder(cylinder, bondState);
                }
                else
                {
                    CreateBondCylinder(key, bondState);
                }
            }

            // Remove stale bonds
            RemoveStaleVisuals(_bondCylinders, currentBonds);
        }

        private void UpdateBondCylinder(GameObject cylinder, BondState bondState)
        {
            if (!_atomSpheres.TryGetValue(bondState.AtomAId, out var aGO) ||
                !_atomSpheres.TryGetValue(bondState.AtomBId, out var bGO))
                return;

            Vector3 aPos = aGO.transform.localPosition;
            Vector3 bPos = bGO.transform.localPosition;
            Vector3 mid = (aPos + bPos) / 2f;
            Vector3 dir = bPos - aPos;
            float length = dir.magnitude;

            if (length < 0.001f) return;

            cylinder.transform.localPosition = mid;
            cylinder.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            cylinder.transform.localScale = new Vector3(
                bondThickness * bondState.Order,
                length,
                bondThickness * bondState.Order);

            // Update alpha
            var renderer = cylinder.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = renderer.material;
                Color c = mat.color;
                mat.color = new Color(c.r, c.g, c.b, bondState.Alpha);
            }
        }

        // ── Creation Helpers ──

        private void CreateAtomSphere(AtomState atomState, Vector3 pos)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Atom_{atomState.Id}";
            sphere.transform.SetParent(_container.transform, false);
            sphere.transform.localPosition = pos;

            float size = atomState.Radius * atomScale;
            sphere.transform.localScale = new Vector3(size, size, size);

            var renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = Instantiate(_defaultAtomMat);
                if (TryGetElementColor(atomState.ElementSymbol, out Color color))
                {
                    renderer.material.color = color;
                }
            }

            _atomSpheres[atomState.Id] = sphere;

            // Create label
            if (showLabels)
            {
                var labelGO = new GameObject($"Label_{atomState.Id}");
                labelGO.transform.SetParent(_container.transform, false);
                labelGO.transform.localPosition = pos + Vector3.up * (atomState.Radius * atomScale + 0.15f);

                var textMesh = labelGO.AddComponent<TextMesh>();
                textMesh.text = atomState.ElementSymbol;
                textMesh.fontSize = 40;
                textMesh.alignment = TextAlignment.Center;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.color = Color.white;
                textMesh.characterSize = 0.02f;

                _atomLabels[atomState.Id] = textMesh;
            }
        }

        private void CreateBondCylinder(string key, BondState bondState)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = $"Bond_{key}";
            cylinder.transform.SetParent(_container.transform, false);

            var renderer = cylinder.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = Instantiate(_defaultBondMat);
                renderer.material.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            }

            _bondCylinders[key] = cylinder;
            UpdateBondCylinder(cylinder, bondState);
        }

        // ── Utility ──

        private void ClearVisuals()
        {
            foreach (var go in _atomSpheres.Values)
                if (go != null) Destroy(go);
            _atomSpheres.Clear();

            foreach (var go in _bondCylinders.Values)
                if (go != null) Destroy(go);
            _bondCylinders.Clear();

            foreach (var go in _atomLabels.Values)
                if (go != null) Destroy(go);
            _atomLabels.Clear();
        }

        private void RemoveStaleVisuals<T>(Dictionary<string, T> dict, HashSet<string> currentKeys) where T : Object
        {
            var toRemove = new List<string>();
            foreach (var kvp in dict)
            {
                if (!currentKeys.Contains(kvp.Key))
                {
                    if (kvp.Value is GameObject go) Destroy(go);
                    else if (kvp.Value is Component comp) Destroy(comp.gameObject);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove)
                dict.Remove(key);
        }

        private static string GetBondKey(string a, string b)
        {
            return string.CompareOrdinal(a, b) < 0 ? $"{a}_{b}" : $"{b}_{a}";
        }

        private static bool TryGetElementColor(string symbol, out Color color)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                color = Color.gray;
                return false;
            }
            return ElementColors.TryGetValue(symbol, out color);
        }

        private void CreateDefaultMaterials()
        {
            if (atomMaterial != null)
            {
                _defaultAtomMat = atomMaterial;
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard");
                _defaultAtomMat = new Material(shader)
                {
                    color = Color.white,
                    enableInstancing = true
                };
            }

            if (bondMaterial != null)
            {
                _defaultBondMat = bondMaterial;
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard");
                _defaultBondMat = new Material(shader)
                {
                    color = new Color(0.6f, 0.6f, 0.6f),
                    enableInstancing = true
                };
            }
        }

        private void LoadDemoMolecules()
        {
            // Load H2O and NaCl as demo
            var water = MolecularSimulator.BuildFromFormula("H2O", -1.5f, 0f, 0f);
            var nacl = MolecularSimulator.BuildFromFormula("NaCl", 1.5f, 0f, 0f);

            var molecules = new List<Molecule> { water, nacl };
            _simulator.LoadMolecules(molecules);
        }

        /// <summary>Display static molecules for the given reagent formulas.</summary>
        public void DisplayReagents(List<string> reagentFormulas)
        {
            if (reagentFormulas == null || reagentFormulas.Count == 0 || _simulator == null) return;

            var molecules = new List<Molecule>();
            float xPos = -(reagentFormulas.Count - 1) * 0.8f;

            for (int i = 0; i < reagentFormulas.Count; i++)
            {
                string formula = reagentFormulas[i];
                if (string.IsNullOrEmpty(formula)) continue;

                var mol = MolecularSimulator.BuildFromFormula(formula, xPos, 0f, 0f);
                if (mol != null) molecules.Add(mol);
                xPos += 1.6f;
            }

            if (molecules.Count == 0) return;

            _simulator.LoadMolecules(molecules);
            ClearVisuals();
        }

        /// <summary>Play a reaction animation between reactants and products.</summary>
        public void DisplayReaction(
            List<string> reactantFormulas,
            List<string> productFormulas,
            float temperatureC)
        {
            var transform = BuildTransformationFromFormulas(reactantFormulas, productFormulas, temperatureC);
            if (transform == null || _simulator == null) return;

            _simulator.StartTransformation(transform);
            ClearVisuals();
        }

        private static ReactionTransformation BuildTransformationFromFormulas(
            List<string> reactantFormulas,
            List<string> productFormulas,
            float temperatureC)
        {
            var transform = new ReactionTransformation
            {
                Reactants = new List<Molecule>(),
                Products = new List<Molecule>(),
                TemperatureC = temperatureC,
                AnimationDurationSeconds = 3f
            };

            float xPos = -2f;
            if (reactantFormulas != null)
            {
                for (int i = 0; i < reactantFormulas.Count; i++)
                {
                    string formula = reactantFormulas[i];
                    if (string.IsNullOrEmpty(formula)) continue;

                    var mol = MolecularSimulator.BuildFromFormula(formula, xPos, 0f, 0f);
                    if (mol != null) transform.Reactants.Add(mol);
                    xPos += 1.5f;
                }
            }

            xPos = 2f;
            if (productFormulas != null)
            {
                for (int i = 0; i < productFormulas.Count; i++)
                {
                    string formula = productFormulas[i];
                    if (string.IsNullOrEmpty(formula)) continue;

                    var mol = MolecularSimulator.BuildFromFormula(formula, xPos, 0f, 0f);
                    if (mol != null) transform.Products.Add(mol);
                    xPos += 1.5f;
                }
            }

            if (transform.Reactants.Count == 0 && transform.Products.Count == 0)
                return null;

            return transform;
        }

        /// <summary>
        /// Build a reaction transformation from a ReactionEntry.
        /// Parses reactant and product formulas into molecular structures.
        /// </summary>
        private ReactionTransformation BuildTransformationFromReaction(ReactionEntry reaction)
        {
            if (reaction == null) return null;

            var transform = new ReactionTransformation
            {
                Reactants = new List<Molecule>(),
                Products = new List<Molecule>(),
                TemperatureC = reaction.activationTempC,
                HasCatalyst = reaction.catalystAllowed,
                AnimationDurationSeconds = 3f
            };

            // Parse reactants from formula strings
            string reactantA = reaction.GetReactantA();
            string reactantB = reaction.GetReactantB();
            string product = reaction.product;

            // Handle new format (reactants[] array)
            if (reaction.reactants != null && reaction.reactants.Count > 0)
            {
                float xPos = -2f;
                foreach (var reactant in reaction.reactants)
                {
                    if (reactant != null && !string.IsNullOrEmpty(reactant.formula))
                    {
                        var mol = MolecularSimulator.BuildFromFormula(reactant.formula, xPos, 0f, 0f);
                        if (mol != null) transform.Reactants.Add(mol);
                        xPos += 1.5f;
                    }
                }
            }

            // Parse products
            if (reaction.products != null && reaction.products.Count > 0)
            {
                float xPos = 2f;
                foreach (var prod in reaction.products)
                {
                    if (prod != null && !string.IsNullOrEmpty(prod.formula))
                    {
                        var mol = MolecularSimulator.BuildFromFormula(prod.formula, xPos, 0f, 0f);
                        if (mol != null) transform.Products.Add(mol);
                        xPos += 1.5f;
                    }
                }
            }

            if (transform.Reactants.Count == 0 && transform.Products.Count == 0)
                return null;

            return transform;
        }
    }

}
