// ChemLabSim v3 — Concentration Tracker
// Maintains cached mol/L concentrations for all species, updated every simulation step.
//
// Why cache concentrations?
//   The rate law requires [X] = n_X / V many times per step.
//   Caching avoids repeated division and ensures a single consistent snapshot
//   per tick (important for simultaneous reaction evaluation in MultiReactionSystem).
//
// Usage (SimulationStepper):
//   // Once, at initialization:
//   _concTracker = new ConcentrationTracker(s.Reactants, s.Products, s.VolumeLiters);
//
//   // Every step, BEFORE computing rates:
//   _concTracker.Update(s.Reactants, s.Products, s.VolumeLiters);
//   float kA = _concTracker.Reactant[0];   // [A] mol/L
//   float kB = _concTracker.Reactant[1];   // [B] mol/L

using System;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    public class ConcentrationTracker
    {
        // ── Cached concentration snapshots ─────────────────────
        /// <summary>Current reactant concentrations [mol/L], indexed like Reactants[].</summary>
        public readonly float[] Reactant;

        /// <summary>Current product concentrations [mol/L], indexed like Products[].</summary>
        public readonly float[] Product;

        /// <summary>Initial reactant concentrations [mol/L] at t=0 (reference for normalization).</summary>
        public readonly float[] ReactantInitial;

        /// <summary>Reactant orders for the rate law (copied from ReactionEntry).</summary>
        public readonly float[] ReactantOrders;

        /// <summary>Product orders for the reverse rate law (stoich coefficients as default).</summary>
        public readonly float[] ProductOrders;

        // ═══════════════════════════════════════════════════════
        //  CONSTRUCTION
        // ═══════════════════════════════════════════════════════

        public ConcentrationTracker(
            SpeciesState[] reactants,
            SpeciesState[] products,
            float volumeL,
            ReactionEntry reaction = null)
        {
            int rLen = reactants?.Length ?? 0;
            int pLen = products?.Length ?? 0;

            Reactant        = new float[rLen];
            Product         = new float[pLen];
            ReactantInitial = new float[rLen];
            ReactantOrders  = BuildReactantOrders(reaction, reactants);
            ProductOrders   = BuildProductOrders(products);

            // Capture t=0 concentrations
            if (volumeL > 0f && reactants != null)
            {
                for (int i = 0; i < rLen; i++)
                {
                    var r = reactants[i];
                    if (r == null) continue;
                    ReactantInitial[i] = r.InitialMoles / volumeL;
                }
            }

            Update(reactants, products, volumeL);
        }

        // ═══════════════════════════════════════════════════════
        //  UPDATE (call every step, before rate computation)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Refresh all cached concentrations from current moles and volume.
        /// Must be called ONCE per step before any rate calculation.
        /// </summary>
        public void Update(SpeciesState[] reactants, SpeciesState[] products, float volumeL)
        {
            float invV = volumeL > 0f ? 1f / volumeL : 0f;

            if (reactants != null)
            {
                int len = Mathf.Min(reactants.Length, Reactant.Length);
                for (int i = 0; i < len; i++)
                    Reactant[i] = Mathf.Max(reactants[i]?.Moles ?? 0f, 0f) * invV;
            }

            if (products != null)
            {
                int len = Mathf.Min(products.Length, Product.Length);
                for (int i = 0; i < len; i++)
                    Product[i] = Mathf.Max(products[i]?.Moles ?? 0f, 0f) * invV;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════

        public float GetReactant(int i) =>
            i >= 0 && i < Reactant.Length ? Reactant[i] : 0f;

        public float GetProduct(int i) =>
            i >= 0 && i < Product.Length ? Product[i] : 0f;

        public float GetReactantInitial(int i) =>
            i >= 0 && i < ReactantInitial.Length ? ReactantInitial[i] : 0f;

        public float GetReactantOrder(int i) =>
            i >= 0 && i < ReactantOrders.Length ? ReactantOrders[i] : 1f;

        public float GetProductOrder(int i) =>
            i >= 0 && i < ProductOrders.Length ? ProductOrders[i] : 1f;

        // ═══════════════════════════════════════════════════════
        //  ORDER RESOLUTION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Build reactant order array. Uses explicit reactantOrders from reaction data
        /// if available, otherwise falls back to stoichiometric coefficients.
        /// </summary>
        private static float[] BuildReactantOrders(ReactionEntry reaction, SpeciesState[] reactants)
        {
            int len = reactants?.Length ?? 0;
            var orders = new float[len];

            for (int i = 0; i < len; i++)
            {
                float stoich = reactants?[i]?.StoichCoeff ?? 1f;
                orders[i] = stoich > 0f ? stoich : 1f;
            }

            // Override with explicit orders from reaction data
            if (reaction?.reactantOrders != null)
            {
                for (int i = 0; i < Mathf.Min(reaction.reactantOrders.Count, len); i++)
                {
                    float explicit_order = reaction.reactantOrders[i];
                    if (explicit_order > 0f)
                        orders[i] = explicit_order;
                }
            }

            return orders;
        }

        /// <summary>
        /// Build product order array for reverse rate law.
        /// Uses stoichiometric coefficients as the standard default.
        /// </summary>
        private static float[] BuildProductOrders(SpeciesState[] products)
        {
            int len = products?.Length ?? 0;
            var orders = new float[len];

            for (int i = 0; i < len; i++)
            {
                float stoich = products?[i]?.StoichCoeff ?? 1f;
                orders[i] = stoich > 0f ? stoich : 1f;
            }

            return orders;
        }
    }
}
