// ChemLabSim v3 — Chemistry Engine
// Next-gen orchestrator layered on top of the existing ReactionEngine.
// Pipeline: Registry → State init → Conditions → Thermodynamics → Equilibrium
//           → Stoichiometry → Output → Visual Binding.
//
// Designed as an optional upgrade path: the base ReactionEngine still works
// independently. ChemistryEngine adds stoichiometric/thermodynamic depth.

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Engine.Chemistry
{
    public class ChemistryEngine
    {
        private readonly ReactionRegistry _registry;
        private readonly ConditionPipeline _pipeline;

        private const float AmbientTempC = 25f;
        private const float MinTempC = -100f;
        private const float MaxTempC = 1000f;
        private const float DefaultMoles = 1f;
        private const float DefaultVolume = 1f; // 1 L

        public ReactionRegistry Registry => _registry;

        // -- Construction --

        public ChemistryEngine(ReactionDB db) : this(new ReactionRegistry(db)) { }

        public ChemistryEngine(ReactionRegistry registry)
        {
            _registry = registry ?? new ReactionRegistry(null);
            _pipeline = ConditionPipeline.CreateDefault();
            Debug.Log($"[ChemistryEngine] Ready with {_registry.Count} reactions.");
        }

        public void AddCondition(ICondition condition)
        {
            _pipeline.Add(condition);
        }

        // -- Public API --

        /// <summary>
        /// Process a mix request through the full chemistry pipeline.
        /// Returns a rich ChemistryOutput with stoichiometry, thermodynamics,
        /// equilibrium, and visual binding data.
        /// </summary>
        public ChemistryOutput Process(MixRequest request)
        {
            // 1. Validate
            if (request.ReagentNames == null || request.ReagentNames.Count < 2)
                return ChemistryOutput.NotFound(request.ReagentNames);

            if (float.IsNaN(request.Temperature) || float.IsInfinity(request.Temperature) ||
                request.Temperature < MinTempC || request.Temperature > MaxTempC)
            {
                var bad = ChemistryOutput.NotFound(request.ReagentNames);
                bad.Summary = "Temperature value is outside the supported range.";
                return bad;
            }

            // 2. Find reaction
            var reaction = _registry.Find(request.ReagentNames);
            if (reaction == null)
            {
                var nf = ChemistryOutput.NotFound(request.ReagentNames);
                if (_registry.NeedsMoreReagents(request.ReagentNames))
                    nf.Summary = "The selected set looks incomplete. Some reactions need 3 or 4 reactants.";
                return nf;
            }

            // 3. Initialize reactant states
            var reactantStates = BuildReactantStates(reaction, request.Temperature);

            // 4. Pre-compute derived values for condition pipeline
            float stirring = Mathf.Clamp01(request.Stirring);
            float grinding = Mathf.Clamp01(request.Grinding);
            float contactFactor = Mathf.Lerp(0.6f, 1.6f, stirring) *
                                  Mathf.Lerp(0.6f, 1.6f, grinding);

            float effectiveActivation = reaction.activationTempC;
            if (request.HasCatalyst && reaction.catalystAllowed)
                effectiveActivation -= reaction.catalystDeltaTempC;

            float pressure = request.PressureAtm > 0f ? request.PressureAtm : 1f;

            var condInput = new ConditionInput
            {
                TemperatureC         = request.Temperature,
                Stirring             = stirring,
                Grinding             = grinding,
                Medium               = request.Medium,
                HasCatalyst          = request.HasCatalyst,
                EffectiveActivationC = effectiveActivation,
                ContactFactor        = contactFactor,
                PressureAtm          = pressure
            };

            // 5. Evaluate condition pipeline
            PipelineResult pipeResult = _pipeline.Evaluate(reaction, condInput);

            // 6. Thermodynamics
            float enthalpyKJ = reaction.enthalpyKJPerMol;
            // Fallback: use temperature_delta as rough enthalpy proxy
            if (Mathf.Approximately(enthalpyKJ, 0f) && reaction.visual_effects != null)
                enthalpyKJ = -reaction.visual_effects.temperature_delta * 4.184f; // cal→kJ approximation

            var thermo = Thermodynamics.Calculate(
                activationTempC: reaction.activationTempC,
                actualTempC:     request.Temperature,
                catalystApplied: request.HasCatalyst && reaction.catalystAllowed,
                catalystDeltaC:  reaction.catalystDeltaTempC,
                enthalpyKJ:      enthalpyKJ,
                eaKJ:            reaction.activationEnergyKJ
            );

            // 7. Equilibrium
            float deltaGas = EquilibriumSolver.CalcDeltaGasMoles(reaction);
            float keqRef = reaction.isReversible ? reaction.equilibriumConstant : 0f;

            var equilibrium = EquilibriumSolver.Solve(
                keqRef:        keqRef,
                enthalpyKJ:    enthalpyKJ,
                tempC:         request.Temperature,
                pressureAtm:   pressure,
                deltaGasMoles: deltaGas
            );

            // 8. Combined rate factor
            //    = conditionRate × arrheniusRate × equilibriumExtent
            float combinedRate = pipeResult.OverallRate
                               * thermo.RateMultiplier
                               * equilibrium.EquilibriumExtent;
            combinedRate = Mathf.Clamp01(combinedRate);

            // 9. Stoichiometry
            var stoichResult = Stoichiometry.Solve(reaction, reactantStates, combinedRate);

            // 10. Build substance states
            var substances = BuildSubstances(reaction, reactantStates, stoichResult);

            // 11. Build output
            string equation = Stoichiometry.BuildEquation(reaction);

            var output = new ChemistryOutput
            {
                Found             = true,
                ReactionId        = reaction.id ?? string.Empty,
                ReactionName      = reaction.name_en ?? string.Empty,
                BalancedEquation  = equation,

                Status            = pipeResult.OverallStatus,
                CompletionPercent = stoichResult.CompletionPercent,
                Summary           = BuildSummary(pipeResult, thermo, equilibrium, stoichResult),

                LimitingReagent   = stoichResult.LimitingFormula,
                MaxExtent         = stoichResult.MaxExtent,
                ActualExtent      = stoichResult.ActualExtent,
                Substances        = substances,

                EnthalpyKJ        = thermo.EnthalpyKJ,
                IsExothermic      = thermo.IsExothermic,
                RateMultiplier    = thermo.RateMultiplier,
                EffectiveEaKJ     = thermo.EffectiveEaKJ,
                ThermoSummary     = thermo.Summary,

                IsReversible      = equilibrium.IsReversible,
                Keq               = equilibrium.Keq,
                EquilibriumExtent = equilibrium.EquilibriumExtent,
                EquilibriumShift  = equilibrium.ShiftDirection,
                EquilibriumSummary = equilibrium.Summary,

                Conditions        = pipeResult.Conditions,
                ConditionRate     = pipeResult.OverallRate,

                GhsCodes          = reaction.safety?.ghs_icons ?? new List<string>(),
                SafetyWarnings    = reaction.safety?.warnings_en ?? new List<string>(),
                SafetyNotes       = reaction.safety_notes ?? string.Empty,

                Observation       = reaction.observation_en ?? string.Empty,
                Explanation       = reaction.explanation_en ?? string.Empty,
                ConditionNotes    = reaction.condition_notes ?? string.Empty,

                Visuals           = default,
                ReagentFormulas   = request.ReagentNames,
                TemperatureC      = request.Temperature,
                PressureAtm       = pressure
            };

            // 12. Resolve visuals via binding layer
            output.Visuals = VisualBindingLayer.Resolve(reaction, output);

            return output;
        }

        // -- Helpers --

        private static List<ChemState> BuildReactantStates(ReactionEntry reaction, float tempC)
        {
            var states = new List<ChemState>();
            if (reaction.reactants == null) return states;

            for (int i = 0; i < reaction.reactants.Count; i++)
            {
                var r = reaction.reactants[i];
                if (r == null) continue;

                states.Add(new ChemState
                {
                    Formula              = r.formula ?? string.Empty,
                    Phase                = ChemState.ParsePhase(r.state),
                    Moles                = DefaultMoles,
                    ConcentrationMolPerL = DefaultMoles / DefaultVolume,
                    VolumeLiters         = DefaultVolume,
                    MolarMassGPerMol     = 0f, // Could be looked up from a molar mass table
                    TemperatureC         = tempC
                });
            }

            return states;
        }

        private static List<SubstanceState> BuildSubstances(
            ReactionEntry reaction,
            List<ChemState> reactantStates,
            StoichResult stoich)
        {
            var list = new List<SubstanceState>();

            // Reactants
            if (reaction.reactants != null)
            {
                int count = System.Math.Min(reaction.reactants.Count, reactantStates.Count);
                for (int i = 0; i < count; i++)
                {
                    var r = reaction.reactants[i];
                    float consumed = i < stoich.ReactantMolesConsumed.Count
                        ? stoich.ReactantMolesConsumed[i] : 0f;
                    float remaining = i < stoich.ReactantMolesRemaining.Count
                        ? stoich.ReactantMolesRemaining[i] : reactantStates[i].Moles;

                    list.Add(new SubstanceState
                    {
                        Formula              = r?.formula ?? string.Empty,
                        Phase                = ChemState.ParsePhase(r?.state),
                        MolesInitial         = reactantStates[i].Moles,
                        MolesFinal           = remaining,
                        MolesConsumed        = consumed,
                        ConcentrationMolPerL = remaining / Mathf.Max(DefaultVolume, 0.001f),
                        IsReactant           = true,
                        IsProduct            = false,
                        IsLimitingReagent    = i == stoich.LimitingIndex,
                        IsExcess             = i != stoich.LimitingIndex
                    });
                }
            }

            // Products
            for (int i = 0; i < stoich.ProductYields.Count; i++)
            {
                var py = stoich.ProductYields[i];
                list.Add(new SubstanceState
                {
                    Formula              = py.Formula,
                    Phase                = py.Phase,
                    MolesInitial         = 0f,
                    MolesFinal           = py.ActualMoles,
                    MolesConsumed        = 0f,
                    ConcentrationMolPerL = py.ActualMoles / Mathf.Max(DefaultVolume, 0.001f),
                    IsReactant           = false,
                    IsProduct            = true,
                    IsLimitingReagent    = false,
                    IsExcess             = false
                });
            }

            return list;
        }

        private static string BuildSummary(
            PipelineResult pipe,
            ThermoResult thermo,
            EquilibriumResult eq,
            StoichResult stoich)
        {
            var parts = new List<string>();

            parts.Add($"Completion: {stoich.CompletionPercent:0.#}%");

            if (!string.IsNullOrEmpty(stoich.LimitingFormula))
                parts.Add($"Limiting reagent: {stoich.LimitingFormula}");

            parts.Add(thermo.Summary);

            if (eq.IsReversible)
                parts.Add(eq.Summary);

            return string.Join(" | ", parts);
        }
    }
}
