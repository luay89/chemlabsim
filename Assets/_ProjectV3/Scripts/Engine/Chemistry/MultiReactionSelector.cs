// ChemLabSim v3 — Multi-Reaction Selector
// Handles competing reactions for the same set of reagents.
//
// In real chemistry, the same reagents can undergo multiple reactions:
//   - Thermodynamic product (most stable, favored at equilibrium)
//   - Kinetic product (fastest, favored at low temperature)
//   - Side reactions (occur simultaneously at lower rates)
//
// This system:
//   1. Finds ALL reactions matching the given reagents
//   2. Scores each reaction based on current conditions
//   3. Selects the dominant reaction dynamically
//   4. Allows competing reactions to run at suppressed rates
//
// Score = conditionRate × arrheniusFactor × phaseCompatibility × selectivityBias

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Engine;

namespace ChemLabSimV3.Engine.Chemistry
{
    /// <summary>A reaction candidate with its computed score under current conditions.</summary>
    public struct ReactionCandidate
    {
        /// <summary>The reaction definition.</summary>
        public ReactionEntry Reaction;

        /// <summary>Composite score for this reaction under current conditions (0–1).</summary>
        public float Score;

        /// <summary>Rate factor from the condition pipeline.</summary>
        public float ConditionRate;

        /// <summary>Arrhenius temperature factor.</summary>
        public float ArrheniusFactor;

        /// <summary>Phase interaction contact factor.</summary>
        public float PhaseFactor;

        /// <summary>True if this is the dominant (highest-scored) reaction.</summary>
        public bool IsDominant;

        /// <summary>Effective rate for this competing reaction (dominant gets full rate,
        /// others get a share of the remaining rate budget).</summary>
        public float EffectiveRate;
    }

    public static class MultiReactionSelector
    {
        /// <summary>
        /// Minimum score a competing reaction must have relative to the dominant
        /// to be included as an active side reaction (0.15 = 15% of dominant score).
        /// </summary>
        private const float CompetingThreshold = 0.15f;

        /// <summary>Maximum number of simultaneous competing reactions.</summary>
        private const int MaxCompeting = 3;

        // ═══════════════════════════════════════════════════════
        //  PRIMARY API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Find all reactions matching the given reagents in the registry,
        /// score them under current conditions, and return them ranked.
        /// </summary>
        /// <param name="registry">Reaction registry (all known reactions).</param>
        /// <param name="reagentFormulas">Current reagent selection.</param>
        /// <param name="conditions">Lab conditions (temperature, medium, etc.).</param>
        /// <param name="grindingFactor">Stirring/grinding quality for phase factor (0–1).</param>
        public static List<ReactionCandidate> Rank(
            ReactionRegistry registry,
            IList<string> reagentFormulas,
            ConditionInput conditions,
            float grindingFactor = 0.5f)
        {
            var candidates = new List<ReactionCandidate>();

            if (registry == null || reagentFormulas == null || reagentFormulas.Count < 2)
                return candidates;

            // Collect all reactions that use AT LEAST the given reagents
            var all = registry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var r = all[i];
                if (!ReactantsMatch(r, reagentFormulas)) continue;

                float condRate = EstimateConditionRate(r, conditions);
                float arrhenius = CalcArrheniusFactor(r, conditions.TemperatureC);
                float phase = EstimatePhaseFactor(r, grindingFactor);
                float score = condRate * arrhenius * phase;

                candidates.Add(new ReactionCandidate
                {
                    Reaction       = r,
                    Score          = score,
                    ConditionRate  = condRate,
                    ArrheniusFactor = arrhenius,
                    PhaseFactor    = phase,
                    IsDominant     = false,
                    EffectiveRate  = 0f
                });
            }

            if (candidates.Count == 0)
                return candidates;

            // Sort descending by score
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Limit to MaxCompeting
            if (candidates.Count > MaxCompeting)
                candidates.RemoveRange(MaxCompeting, candidates.Count - MaxCompeting);

            // Mark dominant
            float dominantScore = candidates[0].Score;
            var dominantCandidate = candidates[0];
            dominantCandidate.IsDominant = true;
            dominantCandidate.EffectiveRate = dominantScore;
            candidates[0] = dominantCandidate;

            // Assign competing rates: each competing reaction gets
            //   effectiveRate = score × (1 - dominantScore) / sum(competingScores)
            // This ensures total rate budget ≤ 1.0
            float competingBudget = 1f - Mathf.Clamp01(dominantScore);
            float totalCompetingScore = 0f;

            for (int i = 1; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c.Score < dominantScore * CompetingThreshold)
                {
                    // Below threshold — remove from competition
                    candidates.RemoveRange(i, candidates.Count - i);
                    break;
                }
                totalCompetingScore += c.Score;
            }

            if (totalCompetingScore > 0f)
            {
                for (int i = 1; i < candidates.Count; i++)
                {
                    var c = candidates[i];
                    c.EffectiveRate = (c.Score / totalCompetingScore) * competingBudget;
                    candidates[i] = c;
                }
            }

            return candidates;
        }

        /// <summary>
        /// Select the dominant reaction from the registry for the given reagents.
        /// Returns null if no reaction found.
        /// </summary>
        public static ReactionEntry SelectDominant(
            ReactionRegistry registry,
            IList<string> reagentFormulas,
            ConditionInput conditions)
        {
            var ranked = Rank(registry, reagentFormulas, conditions);
            return ranked.Count > 0 ? ranked[0].Reaction : null;
        }

        // ═══════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════

        private static bool ReactantsMatch(ReactionEntry reaction, IList<string> reagentFormulas)
        {
            if (reaction == null) return false;

            var rxnReactants = reaction.GetReactantFormulas();
            if (rxnReactants == null || rxnReactants.Count != reagentFormulas.Count)
                return false;

            // Order-insensitive match
            var sortedA = new List<string>(rxnReactants);
            var sortedB = new List<string>(reagentFormulas);
            sortedA.Sort(System.StringComparer.OrdinalIgnoreCase);
            sortedB.Sort(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < sortedA.Count; i++)
            {
                if (!string.Equals(sortedA[i], sortedB[i], System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        /// <summary>Simple condition rate estimate without running the full pipeline.</summary>
        private static float EstimateConditionRate(ReactionEntry r, ConditionInput c)
        {
            float rate = 1f;

            // Temperature check
            float tempK = c.TemperatureC + 273.15f;
            float activationK = r.activationTempC + 273.15f;
            if (tempK < activationK * 0.9f)
                rate *= 0.1f; // Cold
            else if (tempK >= activationK)
                rate *= 1.0f;
            else
                rate *= 0.5f; // Partial

            // Catalyst check
            if (r.requiresCatalyst && !c.HasCatalyst)
                rate *= 0.05f;

            // Medium check
            if (!string.IsNullOrEmpty(r.requiredMedium))
            {
                string reqMedium = r.requiredMedium.ToLowerInvariant();
                string hasMedium = c.Medium.ToString().ToLowerInvariant();
                if (!string.Equals(reqMedium, hasMedium, System.StringComparison.OrdinalIgnoreCase))
                    rate *= 0.1f;
            }

            return Mathf.Clamp01(rate);
        }

        private static float CalcArrheniusFactor(ReactionEntry r, float tempC)
        {
            float eaKJ = EnergyModel.DeriveEa(r.activationTempC, r.activationEnergyKJ);
            float tempK = Mathf.Max(tempC + 273.15f, 1f);
            float refK  = Mathf.Max(r.activationTempC + 273.15f, 1f);
            return EnergyModel.ArrheniusRate(eaKJ, tempK, refK);
        }

        private static float EstimatePhaseFactor(ReactionEntry r, float grindingFactor)
        {
            if (r.reactants == null || r.reactants.Count == 0) return 1f;

            bool hasSolid = false, hasLiquid = false, hasGas = false;
            for (int i = 0; i < r.reactants.Count; i++)
            {
                var phase = ChemState.ParsePhase(r.reactants[i]?.state);
                if (phase == Phase.Solid)   hasSolid = true;
                if (phase == Phase.Liquid || phase == Phase.Aqueous) hasLiquid = true;
                if (phase == Phase.Gas)     hasGas = true;
            }

            if (hasSolid && hasSolid && !hasLiquid && !hasGas)
                return Mathf.Lerp(0.15f, 0.5f, grindingFactor);
            if (hasSolid && hasLiquid)
                return 0.70f;
            if (hasSolid && hasGas)
                return 0.30f;

            return 1.0f;
        }
    }
}
