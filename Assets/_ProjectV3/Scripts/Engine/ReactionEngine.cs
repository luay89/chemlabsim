// ChemLabSim v3 — Reaction Engine
// Main orchestrator: Registry → ConditionPipeline → ReactionOutput → VisualHints.
// Pure C# — no MonoBehaviour, no UI. Fully testable and scene-independent.
//
// Usage:
//   var engine = new ReactionEngine(reactionDB);
//   ReactionOutput output = engine.Process(mixRequest);
//
// The pipeline is pluggable: call AddCondition() to extend evaluation
// without modifying existing conditions (Open-Closed Principle).

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Engine
{
    public class ReactionEngine
    {
        private readonly ReactionRegistry _registry;
        private readonly ConditionPipeline _pipeline;

        private const float MinReasonableTemperatureC = -100f;
        private const float MaxReasonableTemperatureC = 1000f;

        public ReactionRegistry Registry => _registry;

        // -- Construction --

        public ReactionEngine(ReactionDB db) : this(new ReactionRegistry(db)) { }

        public ReactionEngine(ReactionRegistry registry)
        {
            _registry = registry ?? new ReactionRegistry(null);
            _pipeline = ConditionPipeline.CreateDefault();
            Debug.Log($"[ReactionEngine] Ready with {_registry.Count} reactions.");
        }

        /// <summary>Add a custom condition to the evaluation pipeline.</summary>
        public void AddCondition(ICondition condition)
        {
            _pipeline.Add(condition);
        }

        // -- Public API --

        /// <summary>
        /// Process a mix request through the full engine pipeline:
        /// validate → find reaction → pre-compute → evaluate conditions → build output → resolve visuals.
        /// </summary>
        public ReactionOutput Process(MixRequest request)
        {
            // 1. Input validation
            if (request.ReagentNames == null || request.ReagentNames.Count < 2)
                return ReactionOutput.NotFound(request.ReagentNames);

            if (float.IsNaN(request.Temperature) || float.IsInfinity(request.Temperature) ||
                request.Temperature < MinReasonableTemperatureC ||
                request.Temperature > MaxReasonableTemperatureC)
            {
                var invalid = ReactionOutput.NotFound(request.ReagentNames);
                invalid.Summary = "Temperature value is outside the supported range.";
                return invalid;
            }

            // 2. Find matching reaction
            var reaction = _registry.Find(request.ReagentNames);
            if (reaction == null)
            {
                var output = ReactionOutput.NotFound(request.ReagentNames);
                if (_registry.NeedsMoreReagents(request.ReagentNames))
                    output.Summary = "The selected set looks incomplete. Some reactions need 3 or 4 reactants.";
                return output;
            }

            // 3. Pre-compute derived values
            float stirring = Mathf.Clamp01(request.Stirring);
            float grinding = Mathf.Clamp01(request.Grinding);
            float contactFactor =
                Mathf.Lerp(0.6f, 1.6f, stirring) *
                Mathf.Lerp(0.6f, 1.6f, grinding);

            float effectiveActivation = reaction.activationTempC;
            if (request.HasCatalyst && reaction.catalystAllowed)
                effectiveActivation -= reaction.catalystDeltaTempC;

            var condInput = new ConditionInput
            {
                TemperatureC         = request.Temperature,
                Stirring             = stirring,
                Grinding             = grinding,
                Medium               = request.Medium,
                HasCatalyst          = request.HasCatalyst,
                EffectiveActivationC = effectiveActivation,
                ContactFactor        = contactFactor,
                PressureAtm          = request.PressureAtm > 0f ? request.PressureAtm : 1f
            };

            // 4. Evaluate conditions
            PipelineResult pipeResult = _pipeline.Evaluate(reaction, condInput);

            // 5. Build output
            float energyDelta = reaction.visual_effects?.temperature_delta ?? 0f;

            var result = new ReactionOutput
            {
                Found          = true,
                ReactionId     = reaction.id ?? string.Empty,
                ReactionName   = reaction.name_en ?? string.Empty,
                ReactionType   = reaction.reactionType ?? string.Empty,

                Status         = pipeResult.OverallStatus,
                Rate           = pipeResult.OverallRate,
                Summary        = pipeResult.Summary,

                Observation    = reaction.observation_en ?? string.Empty,
                Explanation    = reaction.explanation_en ?? string.Empty,
                ConditionNotes = reaction.condition_notes ?? string.Empty,

                EnergyChange   = energyDelta,
                IsExothermic   = energyDelta > 0f,

                Conditions     = pipeResult.Conditions,
                ReagentFormulas = request.ReagentNames,
                Products       = BuildProducts(reaction),

                GhsCodes       = reaction.safety?.ghs_icons ?? new List<string>(),
                SafetyWarnings = reaction.safety?.warnings_en ?? new List<string>(),
                SafetyNotes    = reaction.safety_notes ?? string.Empty
            };

            // 6. Resolve visual effect hints
            result.Visuals = VisualDirector.Resolve(reaction, result);

            return result;
        }

        // -- Helpers --

        private static List<ProductInfo> BuildProducts(ReactionEntry reaction)
        {
            var list = new List<ProductInfo>();

            if (reaction.products != null)
            {
                for (int i = 0; i < reaction.products.Count; i++)
                {
                    var p = reaction.products[i];
                    if (p == null || string.IsNullOrWhiteSpace(p.formula)) continue;

                    list.Add(new ProductInfo
                    {
                        Formula = p.formula.Trim(),
                        State   = p.state ?? string.Empty,
                        Stoich  = p.stoich
                    });
                }
            }

            // Fallback to legacy flat field
            if (list.Count == 0 && !string.IsNullOrWhiteSpace(reaction.product))
            {
                list.Add(new ProductInfo
                {
                    Formula = reaction.product.Trim(),
                    State   = string.Empty,
                    Stoich  = 1f
                });
            }

            return list;
        }
    }
}
