// ChemLabSim v3 — Multi-Reaction System
// Runs multiple simultaneous reactions on a shared species pool.
//
// In real chemistry, the same mixture can undergo several reactions at once:
//   • Parallel reactions  (A→B and A→C at the same time)
//   • Competitive reactions (first-order vs second-order pathways)
//   • Series reactions  (A→B→C, but both steps active simultaneously)
//
// Algorithm:
//   1. Each ReactionTrack holds its own extent and rate bookkeeping.
//   2. All tracks share ONE SpeciesPool (formula → moles).
//   3. Every step: compute dξ_i for each track using EquilibriumEngine.
//   4. Normalize: if Σ(νi,A × dξ_i) > n_A for any species A, scale all dξ proportionally.
//   5. Apply all dξ_i simultaneously to the pool.
//
// Proportional normalization ensures:
//   - No species can be consumed below zero.
//   - Each reaction receives its fair share relative to its rate.
//   - Conservation is guaranteed at every step.
//
// Integration with SimulationStepper:
//   SimulationStepper creates a MultiReactionSystem when MultiReactionSelector
//   finds competing reactions. Each tick, it calls MultiReactionSystem.Step(),
//   then updates the primary ReactionState from the dominant track's progress.

using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    // ═══════════════════════════════════════════════════════════
    //  PER-REACTION TRACK
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Runtime tracking for one reaction within a multi-reaction system.
    /// </summary>
    public class ReactionTrack
    {
        public ReactionEntry Reaction;

        /// <summary>Contribution weight (from MultiReactionSelector score, 0–1).</summary>
        public float Weight;

        /// <summary>Accumulated extent (mol) for this reaction.</summary>
        public float Extent;

        /// <summary>Maximum possible extent from initial pool amounts.</summary>
        public float MaxExtent;

        public bool IsComplete;

        // Diagnostic fields (updated each step)
        public float ForwardRate;
        public float ReverseRate;
        public float NetRate;
        public float ReactionQuotient;
        public float CurrentKeq;

        public float Progress => MaxExtent > 0f ? Mathf.Clamp01(Extent / MaxExtent) : 0f;
    }

    // ═══════════════════════════════════════════════════════════
    //  STEP RESULT
    // ═══════════════════════════════════════════════════════════

    public struct MultiStepResult
    {
        /// <summary>Net mole change per species this step (+= produced, -= consumed).</summary>
        public Dictionary<string, float> MoleDeltas;

        /// <summary>True if at least one reaction is still actively progressing.</summary>
        public bool AnyActive;

        /// <summary>Total extent advanced by the dominant (first) reaction this step.</summary>
        public float DominantExtentDelta;
    }

    // ═══════════════════════════════════════════════════════════
    //  MULTI-REACTION SYSTEM
    // ═══════════════════════════════════════════════════════════

    public class MultiReactionSystem
    {
        // ── Shared species pool ─────────────────────────────────
        /// <summary>Shared moles pool: formula (case-insensitive) → moles.</summary>
        public readonly Dictionary<string, float> Pool =
            new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);

        public float VolumeL = 1f;

        // ── Reaction tracks ─────────────────────────────────────
        public readonly List<ReactionTrack> Tracks = new List<ReactionTrack>();

        // ── Cached concentration arrays (updated every step) ────
        private float[] _tempReactantConcs;
        private float[] _tempProductConcs;

        // ═══════════════════════════════════════════════════════
        //  SETUP
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Add a reaction to the system with its competition weight.
        /// Populates the shared pool with stoichiometric initial moles
        /// for any species not already present.
        /// </summary>
        public void AddReaction(ReactionEntry reaction, float weight, float keq = 0f)
        {
            if (reaction == null) return;

            // Populate pool with stoichiometric initial moles (νi × 1 mol scale)
            if (reaction.reactants != null)
            {
                for (int i = 0; i < reaction.reactants.Count; i++)
                {
                    var r = reaction.reactants[i];
                    if (r == null || string.IsNullOrEmpty(r.formula)) continue;
                    if (!Pool.ContainsKey(r.formula))
                        Pool[r.formula] = Mathf.Max(r.stoich > 0f ? r.stoich : 1f, 0.01f);
                }
            }

            var track = new ReactionTrack
            {
                Reaction    = reaction,
                Weight      = Mathf.Clamp01(weight),
                Extent      = 0f,
                CurrentKeq  = keq > 0f ? keq : (reaction.isReversible ? reaction.equilibriumConstant : 0f),
                IsComplete  = false
            };
            track.MaxExtent = ComputeMaxExtent(reaction);

            Tracks.Add(track);
        }

        // ═══════════════════════════════════════════════════════
        //  STEP
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Advance all reactions by one time step.
        ///
        /// For each active track:
        ///   dξ_i = EquilibriumEngine.RateToExtent(r_net_i, V, dt)
        ///
        /// Then normalize so no species is over-consumed, and apply all dξ_i.
        /// </summary>
        /// <param name="kfBase">Base Arrhenius rate constant k_f at current temperature.</param>
        /// <param name="scaledDt">Simulation-scaled time step (dt × timeScale).</param>
        /// <param name="tempC">Current temperature (for Keq van't Hoff adjustment).</param>
        public MultiStepResult Step(float kfBase, float scaledDt, float tempC)
        {
            var result = new MultiStepResult
            {
                MoleDeltas = new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase),
                AnyActive  = false
            };

            if (Tracks.Count == 0) return result;

            float invV = VolumeL > 0f ? 1f / VolumeL : 0f;

            // ── Propose dξ per track ────────────────────────────
            var proposed = new float[Tracks.Count];

            for (int t = 0; t < Tracks.Count; t++)
            {
                var track = Tracks[t];
                if (track.IsComplete || track.Reaction == null) continue;

                // Adjust Keq for current temperature
                if (track.Reaction.isReversible && track.Reaction.equilibriumConstant > 0f)
                {
                    track.CurrentKeq = EquilibriumEngine.AdjustKeq(
                        track.Reaction.equilibriumConstant,
                        track.Reaction.enthalpyKJPerMol,
                        tempC);
                }

                // Concentrations from shared pool
                float[] rConcs = GetReactantConcs(track.Reaction, invV);
                float[] pConcs = GetProductConcs(track.Reaction, invV);
                float[] rOrders = GetOrders(track.Reaction, isReactants: true);
                float[] pOrders = GetOrders(track.Reaction, isReactants: false);

                float kf = kfBase * track.Weight;

                var eqState = EquilibriumEngine.Compute(
                    kf, track.CurrentKeq,
                    rConcs, rOrders,
                    pConcs, pOrders,
                    track.Reaction.isReversible);

                track.ForwardRate       = eqState.ForwardRate;
                track.ReverseRate       = eqState.ReverseRate;
                track.NetRate           = eqState.NetRate;
                track.ReactionQuotient  = eqState.ReactionQuotient;

                if (eqState.AtEquilibrium)
                {
                    track.IsComplete = true;
                    continue;
                }

                float dExt = EquilibriumEngine.RateToExtent(eqState.NetRate, VolumeL, scaledDt);
                if (dExt > 0f)
                {
                    proposed[t] = dExt;
                    result.AnyActive = true;
                }
            }

            // ── Normalize: prevent over-consumption ─────────────
            proposed = NormalizeExtents(proposed);

            // ── Apply all extents ────────────────────────────────
            for (int t = 0; t < Tracks.Count; t++)
            {
                float dExt = proposed[t];
                if (dExt <= 0f) continue;

                var rx = Tracks[t].Reaction;

                // Consume reactants
                if (rx.reactants != null)
                    for (int i = 0; i < rx.reactants.Count; i++)
                    {
                        var r = rx.reactants[i];
                        if (r == null) continue;
                        float stoich = r.stoich > 0f ? r.stoich : 1f;
                        float delta  = stoich * dExt;
                        Pool[r.formula] = Mathf.Max(0f, GetMoles(r.formula) - delta);
                        AddDelta(result.MoleDeltas, r.formula, -delta);
                    }

                // Produce products
                if (rx.products != null)
                    for (int i = 0; i < rx.products.Count; i++)
                    {
                        var p = rx.products[i];
                        if (p == null) continue;
                        float stoich = p.stoich > 0f ? p.stoich : 1f;
                        float delta  = stoich * dExt;
                        Pool[p.formula] = GetMoles(p.formula) + delta;
                        AddDelta(result.MoleDeltas, p.formula, delta);
                    }

                Tracks[t].Extent += dExt;
                if (t == 0) result.DominantExtentDelta = dExt;
            }

            // Update MaxExtent for tracks (pool changed)
            for (int t = 0; t < Tracks.Count; t++)
                if (!Tracks[t].IsComplete)
                    Tracks[t].MaxExtent = Mathf.Max(Tracks[t].MaxExtent, ComputeMaxExtent(Tracks[t].Reaction));

            return result;
        }

        // ═══════════════════════════════════════════════════════
        //  POOL QUERY
        // ═══════════════════════════════════════════════════════

        public float GetMoles(string formula)
        {
            Pool.TryGetValue(formula ?? string.Empty, out float m);
            return Mathf.Max(0f, m);
        }

        public float GetConcentration(string formula) =>
            VolumeL > 0f ? GetMoles(formula) / VolumeL : 0f;

        // ═══════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════

        private float[] NormalizeExtents(float[] proposed)
        {
            // Compute what each species would end up with
            float scale = 1f;

            for (int t = 0; t < Tracks.Count; t++)
            {
                float dExt = proposed[t];
                if (dExt <= 0f) continue;

                var rx = Tracks[t].Reaction;
                if (rx.reactants == null) continue;

                for (int i = 0; i < rx.reactants.Count; i++)
                {
                    var r = rx.reactants[i];
                    if (r == null || r.stoich <= 0f) continue;

                    float need = r.stoich * dExt;
                    float have = GetMoles(r.formula);

                    if (need > have && dExt > 0f)
                    {
                        float s = have / need;
                        if (s < scale) scale = s;
                    }
                }
            }

            if (scale < 1f)
                for (int t = 0; t < proposed.Length; t++)
                    proposed[t] *= scale;

            return proposed;
        }

        private float ComputeMaxExtent(ReactionEntry rx)
        {
            if (rx?.reactants == null) return 0f;
            float min = float.MaxValue;

            for (int i = 0; i < rx.reactants.Count; i++)
            {
                var r = rx.reactants[i];
                if (r == null || r.stoich <= 0f) continue;
                float e = GetMoles(r.formula) / r.stoich;
                if (e < min) min = e;
            }

            return min == float.MaxValue ? 0f : Mathf.Max(0f, min);
        }

        private float[] GetReactantConcs(ReactionEntry rx, float invV)
        {
            int len = rx.reactants?.Count ?? 0;
            var concs = new float[len];
            for (int i = 0; i < len; i++)
            {
                var r = rx.reactants[i];
                concs[i] = r != null ? GetMoles(r.formula) * invV : 0f;
            }
            return concs;
        }

        private float[] GetProductConcs(ReactionEntry rx, float invV)
        {
            int len = rx.products?.Count ?? 0;
            var concs = new float[len];
            for (int i = 0; i < len; i++)
            {
                var p = rx.products[i];
                concs[i] = p != null ? GetMoles(p.formula) * invV : 0f;
            }
            return concs;
        }

        private static float[] GetOrders(ReactionEntry rx, bool isReactants)
        {
            var chemicals = isReactants ? rx.reactants : rx.products;
            if (chemicals == null) return new float[0];

            var orders = new float[chemicals.Count];
            for (int i = 0; i < chemicals.Count; i++)
            {
                float stoich = chemicals[i]?.stoich ?? 1f;
                orders[i] = stoich > 0f ? stoich : 1f;
            }

            // Override with explicit reactant orders
            if (isReactants && rx.reactantOrders != null)
                for (int i = 0; i < Mathf.Min(rx.reactantOrders.Count, orders.Length); i++)
                    if (rx.reactantOrders[i] > 0f)
                        orders[i] = rx.reactantOrders[i];

            return orders;
        }

        private static void AddDelta(Dictionary<string, float> dict, string key, float delta)
        {
            if (key == null) return;
            dict.TryGetValue(key, out float existing);
            dict[key] = existing + delta;
        }
    }
}
