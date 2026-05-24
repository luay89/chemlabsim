// ChemLabSim v3 — Simulation Stepper
// Frame-by-frame chemistry simulation engine.
//
// Each Update():
//   1. Calculate current rate from Arrhenius at evolving temperature
//   2. Apply equilibrium damping (rate → 0 as progress → equilibrium)
//   3. Determine moles reacted this dt
//   4. Check limiting reagent depletion
//   5. Consume reactants, produce products
//   6. Apply ΔH → temperature change (energy model feedback)
//   7. Apply heat dissipation to ambient
//   8. Recalculate gas pressure (ideal gas law)
//   9. Update progress
//  10. Publish SimulationTickEvent
//
// Time is compressed: real reactions (μs–hours) play out in ~3–10 seconds.

using System;
using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Engine.Chemistry
{
    public class SimulationStepper : MonoBehaviour
    {
        // ── Configuration ──────────────────────────────────────
        /// <summary>Simulation time scale. Higher = faster visual playback.
        /// At 1.0, the reaction rate (mol/s) is used directly.
        /// Typical values: 2–10 for educational pacing.</summary>
        [SerializeField] private float _timeScale = 5f;

        /// <summary>Minimum dt per step to prevent micro-steps.</summary>
        private const float MinDt = 0.001f;

        /// <summary>Maximum dt per step to prevent instability.</summary>
        private const float MaxDt = 0.05f;

        /// <summary>Progress threshold to consider reaction complete (0.999).</summary>
        private const float CompletionThreshold = 0.999f;

        /// <summary>Rate below this is considered negligible.</summary>
        private const float NegligibleRate = 0.0001f;

        /// <summary>Default moles per reagent if not specified.</summary>
        private const float DefaultMoles = 1f;

        /// <summary>Default volume in liters.</summary>
        private const float DefaultVolume = 1f;

        /// <summary>Default ambient temperature (°C).</summary>
        private const float AmbientTempC = 25f;

        // ── Runtime state ──────────────────────────────────────
        private ReactionState _state;
        private bool _running;
        private float _referenceActivationK;
        private float _grindingFactor;
        private PathwayTracker _pathway;
        private ReactionRegistry _registry;
        private ConcentrationTracker _concTracker;
        private MultiReactionSystem _multiSystem;

        /// <summary>Current live reaction state. Null if no simulation active.</summary>
        public ReactionState State => _state;

        /// <summary>True while simulation is actively stepping.</summary>
        public bool IsRunning => _running;

        // ════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize and start a new simulation from a reaction definition
        /// and lab conditions.
        /// </summary>
        public void StartSimulation(
            ReactionEntry reaction,
            MixRequest request,
            PipelineResult conditions,
            ReactionRegistry registry = null)
        {
            if (reaction == null)
            {
                Debug.LogWarning("[SimStepper] Cannot start: null reaction.");
                return;
            }

            _registry = registry;

            // Grinding/stirring factor for phase contact (0-1)
            _grindingFactor = Mathf.Clamp01((request.Stirring + request.Grinding) * 0.5f);

            // Validate conservation laws before starting
            var validation = ConservationValidator.Validate(reaction);
            if (!validation.IsClean)
                Debug.LogWarning($"[SimStepper] {validation.Summary}");

            _state = InitializeState(reaction, request, conditions);
            _running = true;

            // Build reaction pathway (chain reaction support)
            _pathway = new PathwayTracker(reaction, _registry);
            if (_pathway.HasPathway)
                Debug.Log($"[SimStepper] Pathway: {_pathway.GetSummary()}");

            // Build multi-reaction system if competing reactions exist
            _multiSystem = null;
            if (_registry != null)
            {
                var condInput = new ConditionInput
                {
                    TemperatureC = request.Temperature,
                    Medium       = request.Medium,
                    Stirring     = request.Stirring,
                    Grinding     = request.Grinding,
                    HasCatalyst  = request.HasCatalyst,
                    PressureAtm  = request.PressureAtm > 0f ? request.PressureAtm : 1f
                };
                var ranked = MultiReactionSelector.Rank(_registry, request.ReagentNames, condInput, _grindingFactor);

                if (ranked.Count > 1)
                {
                    _multiSystem = new MultiReactionSystem { VolumeL = DefaultVolume };
                    for (int i = 0; i < ranked.Count; i++)
                    {
                        var c = ranked[i];
                        float keq = c.Reaction.isReversible
                            ? EquilibriumEngine.AdjustKeq(
                                c.Reaction.equilibriumConstant,
                                c.Reaction.enthalpyKJPerMol,
                                request.Temperature)
                            : 0f;
                        _multiSystem.AddReaction(c.Reaction, c.Score, keq);
                    }
                    Debug.Log($"[SimStepper] Multi-reaction: {ranked.Count} competing reactions.");
                }
            }

            Debug.Log($"[SimStepper] Started: {_state.BalancedEquation} " +
                      $"(maxExtent={_state.MaxExtent:F3}, eqExtent={_state.EquilibriumExtent:F2}, keq={_state.CurrentKeq:F4})");

            EventBus.Publish(new SimulationStartedEvent { State = _state });
        }

        /// <summary>Stop the current simulation.</summary>
        public void Stop()
        {
            if (!_running) return;

            _running = false;
            if (_state != null)
            {
                _state.IsComplete = true;
                _state.StopReason = StopReason.ManualStop;
            }

            EventBus.Publish(new SimulationCompletedEvent
            {
                State = _state,
                Reason = StopReason.ManualStop
            });
        }

        public void SetTimeScale(float scale) => _timeScale = Mathf.Max(0.1f, scale);

        /// <summary>
        /// Inject external heat into the running simulation (e.g. from a lab burner).
        /// The temperature change is applied immediately and clamped to the stepper's
        /// own [-200, 2000] °C range so the pressure model stays consistent.
        /// No-op if the simulation is not running.
        /// </summary>
        /// <param name="deltaKJ">Energy to add in kJ. Positive = heating, negative = cooling.</param>
        public void AddExternalHeat(float deltaKJ)
        {
            if (_state == null || !_running) return;
            float dT = EnergyModel.CalcTemperatureFromHeat(deltaKJ, _state.HeatCapacityKJPerK);
            _state.TemperatureC = Mathf.Clamp(_state.TemperatureC + dT, -200f, 2000f);
        }

        /// <summary>
        /// Override the grinding / stirring factor from an external tool.
        /// 0 = no contact (solid-solid unreacted), 1 = maximum surface contact.
        /// </summary>
        public void SetGrindingFactor(float f) => _grindingFactor = Mathf.Clamp01(f);

        // ════════════════════════════════════════════════════════
        //  FRAME UPDATE
        // ════════════════════════════════════════════════════════

        private void Update()
        {
            if (!_running || _state == null) return;

            float dt = Mathf.Clamp(Time.deltaTime, MinDt, MaxDt);
            Step(dt);
        }

        // ════════════════════════════════════════════════════════
        //  SIMULATION STEP
        // ════════════════════════════════════════════════════════

        private void Step(float dt)
        {
            var s = _state;
            s.ElapsedTime += dt;

            float scaledDt = dt * _timeScale;

            // ── 1. Arrhenius rate k_f(T) ────────────────────────
            s.ArrheniusRate = EnergyModel.ArrheniusRate(
                s.EffectiveEaKJ, s.TemperatureK, _referenceActivationK);

            // Effective rate constant = Arrhenius × conditions
            float kf = s.ArrheniusRate * s.ConditionRate;

            // ── 2. Phase contact factor ──────────────────────────
            // Solid-solid contact requires grinding; Liquid-gas has partial contact, etc.
            float phaseFactor = PhaseInteractionModel.GetContactFactor(s.Reactants, _grindingFactor);
            kf *= phaseFactor;

            float dExtent;

            if (_multiSystem != null && _multiSystem.Tracks.Count > 1)
            {
                // ── MULTI-REACTION PATH ──────────────────────────
                // All reactions share a species pool and are stepped proportionally.
                var result = _multiSystem.Step(kf, scaledDt, s.TemperatureC);

                if (!result.AnyActive)
                {
                    CompleteSimulation(s, StopReason.EquilibriumReached);
                    return;
                }

                // Sync dominant reaction's species back into ReactionState
                SyncFromMultiSystem(s);

                dExtent = result.DominantExtentDelta;
                s.Extent += dExtent;
            }
            else
            {
                // ── SINGLE-REACTION PATH ─────────────────────────

                // 3. Update cached concentrations [mol/L] for rate law
                _concTracker.Update(s.Reactants, s.Products, s.VolumeLiters);

                // 4. Kinetic equilibrium state:
                //    r_f = k_f × Π([R_i])^α_i   (mol/L/s)
                //    r_r = k_f/Keq × Π([P_j])^β_j (mol/L/s)
                //    Stop when Q ≈ Keq (2% tolerance)
                var eqState = EquilibriumEngine.Compute(
                    kf, s.CurrentKeq,
                    _concTracker.Reactant, _concTracker.ReactantOrders,
                    _concTracker.Product,  _concTracker.ProductOrders,
                    s.Reaction.isReversible);

                s.ForwardRate       = eqState.ForwardRate;
                s.ReverseRate       = eqState.ReverseRate;
                s.CurrentRate       = Mathf.Max(0f, eqState.NetRate);
                s.ReactionQuotient  = eqState.ReactionQuotient;
                s.IsAtEquilibrium   = eqState.AtEquilibrium;

                // 5. Equilibrium reached — stop simulation
                if (eqState.AtEquilibrium)
                {
                    CompleteSimulation(s, StopReason.EquilibriumReached);
                    return;
                }

                // 6. Convert net rate (mol/L/s) → Δξ (mol)
                //    Δξ = r_net × V × Δt
                dExtent = EquilibriumEngine.RateToExtent(eqState.NetRate, s.VolumeLiters, scaledDt);

                // 7. Stoichiometric clamp: Δξ ≤ n_i / νi for ALL reactants
                dExtent = StoichiometricSolver.ClampExtent(s.Reactants, dExtent);

                if (dExtent < NegligibleRate * scaledDt && s.ElapsedTime > 0.5f)
                {
                    CompleteSimulation(s, s.Progress >= CompletionThreshold
                        ? StopReason.LimitingReagentDepleted
                        : StopReason.EquilibriumReached);
                    return;
                }

                // 8. Apply stoichiometric extent:
                //    n_i -= νi × Δξ  (reactants)
                //    n_j += νj × Δξ  (products)
                StoichiometricSolver.Apply(s.Reactants, s.Products, dExtent);
                s.Extent += dExtent;
            }

            // ── 9–13. Thermodynamics ─────────────────────────────
            if (!StepThermodynamics(s, dExtent, scaledDt, dt))
                return;

            // ── 14. Keq(T) via van't Hoff ────────────────────────
            if (s.Reaction.isReversible && s.Reaction.equilibriumConstant > 0f)
                s.CurrentKeq = EquilibriumEngine.AdjustKeq(
                    s.Reaction.equilibriumConstant, s.EnthalpyKJ, s.TemperatureC);

            // ── 15. Completion checks ─────────────────────────────
            if (s.Progress >= CompletionThreshold)
            {
                CompleteSimulation(s, StopReason.LimitingReagentDepleted);
                return;
            }

            if (s.Reactants.Length > 0 &&
                s.Reactants[s.LimitingIndex].Moles <= NegligibleRate)
            {
                CompleteSimulation(s, StopReason.LimitingReagentDepleted);
                return;
            }

            // Keep phase dictionary synced for lookups/UI
            RebuildPhaseMap(s);

            // ── 16. Publish tick ──────────────────────────────────
            EventBus.Publish(new SimulationTickEvent { State = s });
        }

        // ════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ════════════════════════════════════════════════════════

        private ReactionState InitializeState(
            ReactionEntry reaction,
            MixRequest request,
            PipelineResult conditions)
        {
            var s = new ReactionState();
            s.Reaction = reaction;
            s.BalancedEquation = Stoichiometry.BuildEquation(reaction);
            s.TemperatureC = request.Temperature;
            s.AmbientTemperatureC = AmbientTempC;
            s.PressureAtm = request.PressureAtm > 0f ? request.PressureAtm : 1f;
            s.VolumeLiters = DefaultVolume;
            s.IsClosedContainer = request.IsClosedContainer;
            s.HeadspaceVolumeLiters = request.HeadspaceVolumeLiters > 0f ? request.HeadspaceVolumeLiters : DefaultVolume;
            s.SurfaceAreaM2 = request.SurfaceAreaM2 > 0f ? request.SurfaceAreaM2 : 0.02f;
            s.HeatTransferCoefficient = request.HeatTransferCoefficient > 0f ? request.HeatTransferCoefficient : 0.05f;
            s.GasEscapeRatePerSec = request.GasEscapeRatePerSec > 0f ? request.GasEscapeRatePerSec : 0.5f;
            s.MaxPressureAtm = request.MaxPressureAtm > 0f ? request.MaxPressureAtm : 4f;
            s.HasExploded = false;
            s.HeatCapacityKJPerK = Mathf.Max(0.5f, 4.18f * s.VolumeLiters);
            s.EvaporatedGasMoles = 0f;
            s.ElapsedTime = 0f;
            s.Extent = 0f;
            s.IsComplete = false;
            s.StopReason = StopReason.None;

            // Conditions
            s.ConditionRate = conditions.OverallRate;
            s.Conditions = conditions.Conditions;

            // Enthalpy
            s.EnthalpyKJ = reaction.enthalpyKJPerMol;
            if (Mathf.Approximately(s.EnthalpyKJ, 0f) && reaction.visual_effects != null)
                s.EnthalpyKJ = -reaction.visual_effects.temperature_delta * 4.184f;
            s.IsExothermic = s.EnthalpyKJ < 0f;

            // Activation energy
            float activationK = Mathf.Max(reaction.activationTempC + 273.15f, 1f);
            _referenceActivationK = activationK;
            s.EffectiveEaKJ = EnergyModel.DeriveEa(reaction.activationTempC, reaction.activationEnergyKJ);

            if (request.HasCatalyst && reaction.catalystAllowed)
                s.EffectiveEaKJ = EnergyModel.ApplyCatalyst(
                    s.EffectiveEaKJ, reaction.catalystDeltaTempC, activationK);

            // Build reactant species — initial moles = stoichiometric coefficient × 1 mol
            // This ensures [R_i]₀ = νi / V mol/L and ξ_max = 1 mol for all species.
            var reactants = reaction.reactants;
            s.Reactants = new SpeciesState[reactants != null ? reactants.Count : 0];
            for (int i = 0; i < s.Reactants.Length; i++)
            {
                var r = reactants[i];
                float stoich = (r != null && r.stoich > 0f) ? r.stoich : 1f;
                s.Reactants[i] = new SpeciesState
                {
                    Formula     = r?.formula ?? string.Empty,
                    Phase       = ChemState.ParsePhase(r?.state),
                    StoichCoeff = stoich,
                    MeltingPointC = r != null ? r.meltingPointC : float.NaN,
                    BoilingPointC = r != null ? r.boilingPointC : float.NaN,
                    LatentFusionKJPerMol = r != null ? r.latentFusionKJPerMol : 0f,
                    LatentVaporizationKJPerMol = r != null ? r.latentVaporizationKJPerMol : 0f,
                    SolubilityMolPerL = ResolveSolubilityMolPerL(r),
                    MolarVolumeLPerMol = (r != null && r.molarVolumeLPerMol > 0f) ? r.molarVolumeLPerMol : 0.018f,
                    IsReactant  = true,
                    IsProduct   = false
                };
            }
            // Stoichiometric initialization: n_i(0) = νi
            StoichiometricSolver.InitializeStoichiometricMoles(s.Reactants, scale: 1f);

            // Build product species (start at 0 mol)
            var products = reaction.products;
            s.Products = new SpeciesState[products != null ? products.Count : 0];
            for (int i = 0; i < s.Products.Length; i++)
            {
                var p = products[i];
                float stoich = (p != null && p.stoich > 0f) ? p.stoich : 1f;
                s.Products[i] = new SpeciesState
                {
                    Formula      = p?.formula ?? string.Empty,
                    Phase        = ChemState.ParsePhase(p?.state),
                    StoichCoeff  = stoich,
                    MeltingPointC = p != null ? p.meltingPointC : float.NaN,
                    BoilingPointC = p != null ? p.boilingPointC : float.NaN,
                    LatentFusionKJPerMol = p != null ? p.latentFusionKJPerMol : 0f,
                    LatentVaporizationKJPerMol = p != null ? p.latentVaporizationKJPerMol : 0f,
                    SolubilityMolPerL = ResolveSolubilityMolPerL(p),
                    MolarVolumeLPerMol = (p != null && p.molarVolumeLPerMol > 0f) ? p.molarVolumeLPerMol : 0.018f,
                    InitialMoles = 0f,
                    Moles        = 0f,
                    IsReactant   = false,
                    IsProduct    = true
                };
            }

            // Limiting reagent and max extent
            FindLimitingReagent(s);

            // Keq at initial temperature
            float keqRef = reaction.isReversible ? reaction.equilibriumConstant : 0f;
            s.CurrentKeq = EquilibriumEngine.AdjustKeq(keqRef, s.EnthalpyKJ, s.TemperatureC);

            // Equilibrium extent (via van't Hoff + Le Chatelier) for reference display
            float deltaGas = EquilibriumSolver.CalcDeltaGasMoles(reaction);
            var eq = EquilibriumSolver.Solve(keqRef, s.EnthalpyKJ,
                s.TemperatureC, s.PressureAtm, deltaGas);
            s.EquilibriumExtent = eq.EquilibriumExtent;

            // Concentration tracker — caches [mol/L] per species, updated every step
            _concTracker = new ConcentrationTracker(s.Reactants, s.Products, s.VolumeLiters, reaction);

            // Initialize quick phase lookup dictionary
            RebuildPhaseMap(s);

            // Visual hints (initial)
            var tempOutput = BuildSnapshotOutput(s);
            s.Visuals = VisualBindingLayer.Resolve(reaction, tempOutput);

            return s;
        }

        // ════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════

        private static void FindLimitingReagent(ReactionState s)
        {
            int idx = StoichiometricSolver.FindLimitingIndex(s.Reactants);
            s.LimitingIndex   = idx;
            s.LimitingReagent = s.Reactants.Length > 0 ? s.Reactants[idx].Formula : string.Empty;
            s.MaxExtent       = StoichiometricSolver.FindMaxExtent(s.Reactants);
        }

        private static float ResolveSolubilityMolPerL(ReactionChemical chem)
        {
            if (chem == null) return 0f;

            if (chem.solubilityMolPerL > 0f)
                return chem.solubilityMolPerL;

            if (chem.solubilityGPer100mL > 0f && chem.molarMass > 0f)
            {
                // g/100mL × 10 = g/L ; divide by g/mol => mol/L
                return (chem.solubilityGPer100mL * 10f) / chem.molarMass;
            }

            return 0f;
        }

        private static void RebuildPhaseMap(ReactionState s)
        {
            if (s == null) return;

            if (s.phases == null)
                s.phases = new Dictionary<string, PhaseState>(StringComparer.OrdinalIgnoreCase);
            else
                s.phases.Clear();

            if (s.Reactants != null)
                for (int i = 0; i < s.Reactants.Length; i++)
                    AddOrAccumulatePhaseState(s.phases, s.Reactants[i], s.VolumeLiters);

            if (s.Products != null)
                for (int i = 0; i < s.Products.Length; i++)
                    AddOrAccumulatePhaseState(s.phases, s.Products[i], s.VolumeLiters);
        }

        private static void AddOrAccumulatePhaseState(Dictionary<string, PhaseState> map, SpeciesState species, float volumeL)
        {
            if (map == null || species == null || string.IsNullOrWhiteSpace(species.Formula)) return;

            if (!map.TryGetValue(species.Formula, out var ps))
                ps = new PhaseState();

            float moles = Mathf.Max(0f, species.Moles);
            switch (species.Phase)
            {
                case Phase.Solid:  ps.solidMoles  += moles; break;
                case Phase.Liquid: ps.liquidMoles += moles; break;
                case Phase.Gas:    ps.gasMoles    += moles; break;
            }

            // Precipitated side inventory contributes to the bottom solid layer.
            if (species.PrecipitatedMoles > 0f)
                ps.solidMoles += species.PrecipitatedMoles;

            // Molar volume is a species property — always take from latest species entry
            ps.MolarVolumeLPerMol = species.MolarVolumeLPerMol;

            map[species.Formula] = ps;
        }

        /// <summary>
        /// Sync the dominant reaction's species moles from the shared MultiReactionSystem pool
        /// back into ReactionState.Reactants[] and Products[] for display and completion checks.
        /// </summary>
        private void SyncFromMultiSystem(ReactionState s)
        {
            if (_multiSystem == null || s.Reaction == null) return;

            if (s.Reactants != null && s.Reaction.reactants != null)
                for (int i = 0; i < s.Reactants.Length && i < s.Reaction.reactants.Count; i++)
                {
                    string f = s.Reaction.reactants[i]?.formula;
                    if (f != null) s.Reactants[i].Moles = _multiSystem.GetMoles(f);
                }

            if (s.Products != null && s.Reaction.products != null)
                for (int i = 0; i < s.Products.Length && i < s.Reaction.products.Count; i++)
                {
                    string f = s.Reaction.products[i]?.formula;
                    if (f != null) s.Products[i].Moles = _multiSystem.GetMoles(f);
                }
        }

        private void CompleteSimulation(ReactionState s, StopReason reason)
        {
            s.IsComplete = true;
            s.StopReason = reason;
            _running = false;

            // Final tick
            EventBus.Publish(new SimulationTickEvent { State = s });
            EventBus.Publish(new SimulationCompletedEvent
            {
                State = s,
                Reason = reason
            });

            Debug.Log($"[SimStepper] Complete: {reason} " +
                      $"(progress={s.CompletionPercent:F1}%, T={s.TemperatureC:F1}°C, " +
                      $"P={s.PressureAtm:F2} atm, elapsed={s.ElapsedTime:F2}s)");
        }

        // Returns false if the step must abort (e.g. explosion).
        private bool StepThermodynamics(ReactionState s, float dExtent, float scaledDt, float dt)
        {
            // ── 9. Phase transitions ─────────────────────────────
            HandlePhaseTransitions(s, scaledDt);

            // ── 10. Pathway — chain reaction trigger ─────────────
            if (_pathway != null)
            {
                var nextStep = _pathway.Tick(s, dt);
                if (nextStep?.Reaction != null)
                    Debug.Log($"[SimStepper] Chain: activating '{nextStep.Reaction.id}'");
            }

            // ── 11. ΔH → temperature feedback ────────────────────
            s.TemperatureC += EnergyModel.CalcTemperatureStep(s.EnthalpyKJ, dExtent);

            // ── 12. Heat dissipation ──────────────────────────────
            s.TemperatureC += EnergyModel.CalcDissipation(
                s.TemperatureC, s.AmbientTemperatureC, dt, s.HeatTransferCoefficient);
            s.TemperatureC = Mathf.Clamp(s.TemperatureC, -200f, 2000f);

            // ── 13. Pressure ──────────────────────────────────────
            UpdatePressure(s);
            if (s.IsClosedContainer && s.PressureAtm > s.MaxPressureAtm)
            {
                TriggerExplosion(s);
                return false;
            }

            return true;
        }

        private void HandlePhaseTransitions(ReactionState s, float scaledDt)
        {
            PhaseInteractionModel.UpdatePrecipitates(s);
            var phaseStep = PhaseInteractionModel.Evaluate(s, _grindingFactor, scaledDt);
            if (!Mathf.Approximately(phaseStep.PhaseHeatKJ, 0f))
                s.TemperatureC += EnergyModel.CalcTemperatureFromHeat(phaseStep.PhaseHeatKJ, s.HeatCapacityKJPerK);

            // Limiting species can shift after precipitation/evaporation/venting
            FindLimitingReagent(s);
        }

        private static void HandleEvaporation(ReactionState s, float dt)
        {
            PhaseInteractionModel.ApplyEvaporation(s, dt);
        }

        private static void HandleSolubility(ReactionState s)
        {
            PhaseInteractionModel.EnforceSolubility(s);
        }

        private static void UpdatePressure(ReactionState s)
        {
            float volumeL = Mathf.Max(0.01f, s.HeadspaceVolumeLiters);
            s.PressureAtm = EnergyModel.CalcPressure(s.TotalGasMoles, s.TemperatureK, volumeL);
        }

        private void TriggerExplosion(ReactionState s)
        {
            if (s == null) return;

            s.HasExploded = true;
            Debug.LogError($"[SimStepper] OVERPRESSURE: {s.PressureAtm:F2} atm exceeded max {s.MaxPressureAtm:F2} atm.");
            CompleteSimulation(s, StopReason.OverpressureExplosion);
        }

        /// <summary>
        /// Build a ChemistryOutput snapshot from current ReactionState
        /// (used for VisualBindingLayer and legacy compatibility).
        /// </summary>
        public static ChemistryOutput BuildSnapshotOutput(ReactionState s)
        {
            var substances = new List<SubstanceState>();

            if (s.Reactants != null)
            {
                for (int i = 0; i < s.Reactants.Length; i++)
                {
                    var r = s.Reactants[i];
                    substances.Add(new SubstanceState
                    {
                        Formula              = r.Formula,
                        Phase                = r.Phase,
                        MolesInitial         = r.InitialMoles,
                        MolesFinal           = r.Moles,
                        MolesConsumed        = r.InitialMoles - r.Moles,
                        ConcentrationMolPerL = r.Concentration(s.VolumeLiters),
                        IsReactant           = true,
                        IsProduct            = false,
                        IsLimitingReagent    = i == s.LimitingIndex,
                        IsExcess             = i != s.LimitingIndex
                    });
                }
            }

            if (s.Products != null)
            {
                for (int i = 0; i < s.Products.Length; i++)
                {
                    var p = s.Products[i];
                    substances.Add(new SubstanceState
                    {
                        Formula              = p.Formula,
                        Phase                = p.Phase,
                        MolesInitial         = 0f,
                        MolesFinal           = p.Moles,
                        MolesConsumed        = 0f,
                        ConcentrationMolPerL = p.Concentration(s.VolumeLiters),
                        IsReactant           = false,
                        IsProduct            = true,
                        IsLimitingReagent    = false,
                        IsExcess             = false
                    });
                }
            }

            return new ChemistryOutput
            {
                Found             = true,
                ReactionId        = s.Reaction?.id ?? string.Empty,
                ReactionName      = s.Reaction?.name_en ?? string.Empty,
                BalancedEquation  = s.BalancedEquation,
                Status            = s.IsComplete ? ReactionStatus.Success : ReactionStatus.Partial,
                CompletionPercent = s.CompletionPercent,
                Summary           = $"Progress: {s.CompletionPercent:F1}% | Rate: {s.CurrentRate:F3} | T: {s.TemperatureC:F1}°C",
                LimitingReagent   = s.LimitingReagent,
                MaxExtent         = s.MaxExtent,
                ActualExtent      = s.Extent,
                Substances        = substances,
                EnthalpyKJ        = s.EnthalpyKJ,
                IsExothermic      = s.IsExothermic,
                RateMultiplier    = s.ArrheniusRate,
                EffectiveEaKJ     = s.EffectiveEaKJ,
                ThermoSummary     = string.Empty,
                IsReversible      = s.Reaction != null && s.Reaction.isReversible,
                Keq               = s.CurrentKeq,
                EquilibriumExtent = s.EquilibriumExtent,
                EquilibriumShift  = string.Empty,
                EquilibriumSummary = string.Empty,
                Conditions        = s.Conditions ?? new List<ConditionResult>(),
                ConditionRate     = s.ConditionRate,
                GhsCodes          = s.Reaction?.safety?.ghs_icons ?? new List<string>(),
                SafetyWarnings    = s.Reaction?.safety?.warnings_en ?? new List<string>(),
                SafetyNotes       = s.Reaction?.safety_notes ?? string.Empty,
                Observation       = s.Reaction?.observation_en ?? string.Empty,
                Explanation       = s.Reaction?.explanation_en ?? string.Empty,
                ConditionNotes    = s.Reaction?.condition_notes ?? string.Empty,
                Visuals           = s.Visuals,
                ReagentFormulas   = BuildReagentList(s),
                TemperatureC      = s.TemperatureC,
                PressureAtm       = s.PressureAtm
            };
        }

        private static List<string> BuildReagentList(ReactionState s)
        {
            var list = new List<string>();
            if (s.Reactants != null)
                for (int i = 0; i < s.Reactants.Length; i++)
                    list.Add(s.Reactants[i].Formula);
            return list;
        }
    }
}
