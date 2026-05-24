// ChemLabSim v3 — Liquid Transfer Service
// Pure physics service (no MonoBehaviour) that performs one frame of
// liquid transfer between two ReactionState containers.
//
// Scientific laws enforced:
//   • Conservation of mass      — moles only move, never created/destroyed
//   • Conservation of composition — each species transferred proportionally
//   • Thermal mixing equation   — T_final = (m1·Cp1·T1 + m2·Cp2·T2) / (m1·Cp1 + m2·Cp2)
//   • Volume constraint         — cannot transfer more than currently available
//
// Usage (call every frame from a MonoBehaviour):
//   LiquidTransferResult result =
//       LiquidTransferService.Step(source, target, flowRateLPerSec, dt);

using System;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    /// <summary>
    /// Describes what happened during one transfer step.
    /// </summary>
    public struct LiquidTransferResult
    {
        /// <summary>Volume actually moved this frame (L). May be less than requested
        /// when the source is nearly empty.</summary>
        public float TransferredVolumeLiters;

        /// <summary>True when the source container became empty this frame.</summary>
        public bool SourceExhausted;

        /// <summary>Reason no transfer occurred, or null on success.</summary>
        public string SkipReason;
    }

    /// <summary>
    /// Stateless service that applies one frame of liquid transfer.
    /// All mutations happen directly on the supplied <see cref="ReactionState"/>
    /// objects — no visual state is touched.
    /// </summary>
    public static class LiquidTransferService
    {
        // ── Constants ────────────────────────────────────────────────────────
        /// <summary>Minimum volume left in source before transfer stops (L).
        /// Prevents numeric issues as volume → 0.</summary>
        private const float MinSourceVolumeL = 1e-5f;

        /// <summary>Minimum moles before a species is considered absent.</summary>
        private const float MoleEpsilon = 1e-9f;

        // ════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Transfer liquid from <paramref name="source"/> to <paramref name="target"/>
        /// for one simulation frame.
        ///
        /// Steps performed:
        ///  1. Compute requested volume = flowRate × dt, clamp to available source volume.
        ///  2. For each species in source.Reactants + source.Products, transfer moles
        ///     proportionally (fraction = deltaVolume / sourceVolume).
        ///  3. Apply thermal mixing equation to both containers.
        ///  4. Update VolumeLiters and HeatCapacityKJPerK on both containers.
        /// </summary>
        /// <param name="source">Container liquid is poured from.</param>
        /// <param name="target">Container liquid is poured into.</param>
        /// <param name="flowRateLPerSec">Desired flow rate in L/s (must be positive).</param>
        /// <param name="dt">Frame delta time in seconds (clamped 0–0.1 internally).</param>
        /// <returns>Summary of what happened this frame.</returns>
        public static LiquidTransferResult Step(
            ReactionState source,
            ReactionState target,
            float flowRateLPerSec,
            float dt)
        {
            // ── Guard: invalid inputs ─────────────────────────────────────
            if (source == null || target == null)
                return Skip("Null container");

            if (flowRateLPerSec <= 0f)
                return Skip("Flow rate must be positive");

            // Clamp dt to prevent instability at very large frames
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            if (dt <= 0f)
                return Skip("Zero dt");

            // ── Guard: source is empty ────────────────────────────────────
            float sourceVol = source.VolumeLiters;
            if (sourceVol <= MinSourceVolumeL)
                return new LiquidTransferResult
                {
                    TransferredVolumeLiters = 0f,
                    SourceExhausted = true,
                    SkipReason = "Source empty"
                };

            // ── Step 1: Volume to transfer this frame ─────────────────────
            float requested = flowRateLPerSec * dt;
            // Cannot take more than what is safely available
            float available  = Mathf.Max(0f, sourceVol - MinSourceVolumeL);
            float deltaVol   = Mathf.Min(requested, available);

            if (deltaVol <= 0f)
                return new LiquidTransferResult
                {
                    TransferredVolumeLiters = 0f,
                    SourceExhausted = sourceVol <= MinSourceVolumeL,
                    SkipReason = "Nothing to transfer"
                };

            // ── Step 2: Transfer moles proportionally ─────────────────────
            // Fraction of source volume being removed
            float fraction = deltaVol / sourceVol; // (0, 1]

            TransferSpeciesArray(source.Reactants, ref target.Reactants, fraction);
            TransferSpeciesArray(source.Products,  ref target.Products,  fraction);

            // ── Step 3: Thermal mixing ────────────────────────────────────
            // Use heat capacity as thermal mass proxy: m·Cp = HeatCapacityKJPerK
            float srcMass   = Mathf.Max(source.HeatCapacityKJPerK, 1e-4f);
            float tgtMass   = Mathf.Max(target.HeatCapacityKJPerK, 1e-4f);
            float srcT      = source.TemperatureC;
            float tgtT      = target.TemperatureC;

            // Thermal mass being transferred from source
            float deltaMass = srcMass * fraction;

            // New source temperature: losing a fraction of its thermal mass
            // (remaining mass stays at same temperature — no work done yet)
            // The source just gets smaller; its intensive temperature is unchanged
            // until the next dissipation step.  So we leave source.TemperatureC alone.

            // New target temperature: mixing equation
            //   T_final = (m_target * T_target + m_poured * T_source) / (m_target + m_poured)
            float newTargetT = (tgtMass * tgtT + deltaMass * srcT) / (tgtMass + deltaMass);
            target.TemperatureC = newTargetT;

            // ── Step 4: Update volumes ────────────────────────────────────
            source.VolumeLiters = Mathf.Max(0f, sourceVol - deltaVol);
            target.VolumeLiters = target.VolumeLiters + deltaVol;

            // ── Step 5: Recompute heat capacities (4.18 kJ/K per litre) ──
            // This mirrors SimulationStepper's init: HeatCapacity = 4.18 * volume
            source.HeatCapacityKJPerK = Mathf.Max(0.5f, 4.18f * source.VolumeLiters);
            target.HeatCapacityKJPerK = Mathf.Max(0.5f, 4.18f * target.VolumeLiters);

            bool exhausted = source.VolumeLiters <= MinSourceVolumeL;
            return new LiquidTransferResult
            {
                TransferredVolumeLiters = deltaVol,
                SourceExhausted = exhausted
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  INTERNAL HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Move <paramref name="fraction"/> of each species moles from
        /// <paramref name="srcArray"/> to the matching species in
        /// <paramref name="dstArray"/> (matched by Formula).
        /// Species present in source but absent from destination are added
        /// in-place by copying and zeroing the transferred amount from source.
        /// </summary>
        private static void TransferSpeciesArray(
            SpeciesState[] srcArray,
            ref SpeciesState[] dstArray,
            float fraction)
        {
            if (srcArray == null || srcArray.Length == 0) return;

            for (int i = 0; i < srcArray.Length; i++)
            {
                SpeciesState srcSp = srcArray[i];
                if (srcSp == null) continue;

                float molesToMove = srcSp.Moles * fraction;
                if (molesToMove <= MoleEpsilon) continue;

                // Remove from source
                srcSp.Moles = Mathf.Max(0f, srcSp.Moles - molesToMove);

                // Also transfer any precipitated moles proportionally
                float precipToMove = srcSp.PrecipitatedMoles * fraction;
                srcSp.PrecipitatedMoles = Mathf.Max(0f, srcSp.PrecipitatedMoles - precipToMove);

                // Find matching species in destination (by formula)
                SpeciesState dstSp = FindByFormula(dstArray, srcSp.Formula);
                if (dstSp == null)
                {
                    dstSp = AppendSpecies(ref dstArray, srcSp);
                }

                dstSp.Moles             += molesToMove;
                dstSp.PrecipitatedMoles += precipToMove;
            }
        }

        private static SpeciesState AppendSpecies(ref SpeciesState[] array, SpeciesState template)
        {
            if (template == null) return null;

            int oldLen = array != null ? array.Length : 0;
            int newLen = oldLen + 1;

            Array.Resize(ref array, newLen);
            array[oldLen] = new SpeciesState
            {
                Formula                 = template.Formula,
                Phase                   = template.Phase,
                StoichCoeff             = template.StoichCoeff,
                InitialMoles            = 0f,
                Moles                   = 0f,
                IsReactant              = template.IsReactant,
                IsProduct               = template.IsProduct,
                MeltingPointC           = template.MeltingPointC,
                BoilingPointC           = template.BoilingPointC,
                LatentFusionKJPerMol    = template.LatentFusionKJPerMol,
                LatentVaporizationKJPerMol = template.LatentVaporizationKJPerMol,
                SolubilityMolPerL       = template.SolubilityMolPerL,
                PrecipitatedMoles       = 0f,
                MolarVolumeLPerMol      = template.MolarVolumeLPerMol
            };

            return array[oldLen];
        }

        private static SpeciesState FindByFormula(SpeciesState[] array, string formula)
        {
            if (array == null || string.IsNullOrEmpty(formula)) return null;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != null &&
                    string.Equals(array[i].Formula, formula, StringComparison.OrdinalIgnoreCase))
                    return array[i];
            }
            return null;
        }

        private static LiquidTransferResult Skip(string reason) =>
            new LiquidTransferResult { SkipReason = reason };
    }
}
