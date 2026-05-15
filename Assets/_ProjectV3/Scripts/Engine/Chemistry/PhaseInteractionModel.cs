// ChemLabSim v3 — Phase Interaction Model
// Governs HOW reactants in different phases interact:
//   Solid + Liquid  → dissolution-limited (Ksp / solubility)
//   Solid + Gas     → surface-area-limited (contact factor low)
//   Liquid + Liquid → free mixing (contact factor 1.0)
//   Gas + Gas       → free mixing (contact factor 1.0)
//   Solid + Solid   → contact-area-only (low, improves with grinding)
//
// Also handles:
//   Precipitation: [X][Y] exceeds Ksp → precipitate forms
//   Solubility cap: dissolved moles cannot exceed limit
//
// All methods are pure (no state mutations) except UpdatePrecipitates
// which modifies SpeciesState.Phase in-place.

using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    public struct LayerHeights
    {
        public float liquidHeight;
        public float solidHeight;
        public float foamHeight;
    }

    public struct PhaseInteraction
    {
        /// <summary>Rate multiplier from phase compatibility (0–1).</summary>
        public float ContactFactor;

        /// <summary>True if solid dissolution is the rate-limiting step.</summary>
        public bool DissolutionLimited;

        /// <summary>Max moles that can dissolve this step (dissolution rate).</summary>
        public float MaxDissolutionMolesPerStep;

        /// <summary>Any new precipitate formulas detected this step.</summary>
        public List<string> NewPrecipitates;

        /// <summary>Net heat exchanged by phase transitions (kJ). Positive heats mixture, negative cools.</summary>
        public float PhaseHeatKJ;

        /// <summary>Total gas moles vented this step (open containers).</summary>
        public float EscapedGasMoles;

        /// <summary>Total moles evaporated into vapor this step.</summary>
        public float EvaporatedMoles;
    }

    public static class PhaseInteractionModel
    {
        // ── Contact factors (0–1 rate multiplier based on phase combination) ──
        private const float FactorSolidSolid   = 0.15f; // Very limited unless ground
        private const float FactorSolidLiquid  = 0.70f; // Dissolution-limited
        private const float FactorSolidGas     = 0.30f; // Surface only
        private const float FactorLiquidLiquid = 1.00f; // Full mixing
        private const float FactorLiquidGas    = 0.85f; // Slightly limited
        private const float FactorGasGas       = 1.00f; // Full mixing

        // ── Dissolution model ─────────────────────────────────
        /// <summary>Fraction of solid that can dissolve per second (base rate).</summary>
        private const float BaseDissolutionRatePerSec = 0.05f;
        private const float BaseEvaporationRatePerSec = 0.003f;
        private const float DefaultLatentFusionKJPerMol = 6f;
        private const float DefaultLatentVaporizationKJPerMol = 40f;

        // ─────────────────────────────────────────────────────
        //  CONTACT FACTOR
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Calculate the rate multiplier from phase compatibility of the reactants.
        /// Mixed phases limit reaction rate (solid must dissolve, surface area matters).
        /// </summary>
        /// <param name="reactants">Active reactant species.</param>
        /// <param name="grindingFactor">Stirring/grinding quality (0–1, 1 = fully stirred).</param>
        public static float GetContactFactor(SpeciesState[] reactants, float grindingFactor = 0.5f)
        {
            if (reactants == null || reactants.Length == 0) return 1f;
            if (reactants.Length == 1) return 1f;

            float minFactor = 1f;

            // Find worst-case phase pair
            for (int i = 0; i < reactants.Length; i++)
            {
                for (int j = i + 1; j < reactants.Length; j++)
                {
                    float pairFactor = GetPairFactor(reactants[i].Phase, reactants[j].Phase);
                    minFactor = Mathf.Min(minFactor, pairFactor);
                }
            }

            // Grinding/stirring improves solid contact factor
            float grinding = Mathf.Clamp01(grindingFactor);
            if (HasSolid(reactants))
            {
                // Grinding raises floor from FactorSolidSolid to up to FactorSolidLiquid
                minFactor = Mathf.Lerp(minFactor, 1f, grinding * 0.5f);
            }

            return Mathf.Clamp01(minFactor);
        }

        // ─────────────────────────────────────────────────────
        //  DISSOLUTION
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Calculate how many moles of solid can dissolve into liquid this step.
        /// Returns 0 if solubility limit is already exceeded.
        /// </summary>
        /// <param name="solidMoles">Current moles of solid reactant.</param>
        /// <param name="dissolvedMoles">Moles already dissolved (product or intermediate).</param>
        /// <param name="solubilityLimitMolPerL">Max dissolved mol/L. 0 = unlimited.</param>
        /// <param name="volumeL">Solution volume (L).</param>
        /// <param name="dt">Time step (s).</param>
        public static float CalcDissolutionMoles(
            float solidMoles,
            float dissolvedMoles,
            float solubilityLimitMolPerL,
            float volumeL,
            float dt)
        {
            if (solidMoles <= 0f) return 0f;

            // Check solubility limit
            if (solubilityLimitMolPerL > 0f && volumeL > 0f)
            {
                float maxDissolvedMoles = solubilityLimitMolPerL * volumeL;
                if (dissolvedMoles >= maxDissolvedMoles)
                    return 0f; // Already saturated
            }

            // Base dissolution rate
            float maxThisStep = solidMoles * BaseDissolutionRatePerSec * dt;
            maxThisStep = Mathf.Min(maxThisStep, solidMoles); // Can't dissolve more than exists

            // Constrain to available headroom in solution
            if (solubilityLimitMolPerL > 0f && volumeL > 0f)
            {
                float maxDissolvedMoles = solubilityLimitMolPerL * volumeL;
                float headroom = Mathf.Max(0f, maxDissolvedMoles - dissolvedMoles);
                maxThisStep = Mathf.Min(maxThisStep, headroom);
            }

            return Mathf.Max(0f, maxThisStep);
        }

        // ─────────────────────────────────────────────────────
        //  PRECIPITATION (KSP)
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Check if a product should precipitate based on Ksp.
        /// Returns true if [cation]^m × [anion]^n > Ksp.
        /// </summary>
        /// <param name="cationConc">Cation concentration (mol/L).</param>
        /// <param name="anionConc">Anion concentration (mol/L).</param>
        /// <param name="cationOrder">Stoichiometric order of cation (default 1).</param>
        /// <param name="anionOrder">Stoichiometric order of anion (default 1).</param>
        /// <param name="ksp">Solubility product constant.</param>
        public static bool ShouldPrecipitate(
            float cationConc,
            float anionConc,
            float ksp,
            float cationOrder = 1f,
            float anionOrder = 1f)
        {
            if (ksp <= 0f) return false; // No Ksp defined → no precipitation check

            float ionProduct = Mathf.Pow(Mathf.Max(cationConc, 0f), cationOrder)
                             * Mathf.Pow(Mathf.Max(anionConc, 0f), anionOrder);

            return ionProduct > ksp;
        }

        /// <summary>
        /// Calculate moles of precipitate that should form when ion product exceeds Ksp.
        /// Excess moles above saturation precipitate out.
        /// </summary>
        public static float CalcPrecipitatedMoles(
            float totalMoles,
            float volumeL,
            float ksp)
        {
            if (ksp <= 0f || volumeL <= 0f || totalMoles <= 0f) return 0f;

            // At Ksp: [X]_sat = sqrt(Ksp) for 1:1 salt
            float satConc = Mathf.Sqrt(ksp);
            float satMoles = satConc * volumeL;
            float excess = totalMoles - satMoles;

            return Mathf.Max(0f, excess);
        }

        /// <summary>
        /// Scan all product species and convert excess-dissolved ones to solid phase
        /// if Ksp is defined for the reaction.
        /// Modifies SpeciesState.Phase in-place.
        /// </summary>
        public static bool UpdatePrecipitates(ReactionState state)
        {
            if (state?.Reaction == null) return false;

            float ksp = state.Reaction.ksp;
            if (ksp <= 0f) return false;

            bool precipitateFormed = false;
            float volumeL = Mathf.Max(state.VolumeLiters, 0.001f);

            for (int i = 0; i < state.Products.Length; i++)
            {
                var p = state.Products[i];
                if (p == null || p.Phase == Phase.Solid) continue;
                if (p.Phase != Phase.Aqueous && p.Phase != Phase.Liquid) continue;

                float conc = p.Moles / volumeL;
                float satConc = Mathf.Sqrt(ksp);

                if (conc > satConc)
                {
                    // Amount to precipitate = excess above saturation
                    float satMoles = satConc * volumeL;
                    float precipMoles = Mathf.Max(0f, p.Moles - satMoles);
                    p.Moles -= precipMoles;
                    // In a real system we'd spawn a new solid SpeciesState
                    // Here we just flag precipitation via the visual system
                    precipitateFormed = true;
                }
            }

            return precipitateFormed;
        }

        /// <summary>
        /// Enforce species-specific solubility limits (mol/L).
        /// Excess dissolved moles are moved to precipitated inventory.
        /// </summary>
        public static bool EnforceSolubility(ReactionState state)
        {
            if (state == null) return false;

            bool changed = false;
            float volumeL = Mathf.Max(state.VolumeLiters, 1e-6f);

            changed |= EnforceSolubilityOnArray(state.Reactants, volumeL);
            changed |= EnforceSolubilityOnArray(state.Products, volumeL);

            return changed;
        }

        /// <summary>
        /// Apply temperature-driven phase transitions and latent heat exchange.
        /// Returns heat transferred to the bulk mixture (kJ):
        ///   melting/vaporization consume heat (negative),
        ///   freezing/condensation release heat (positive).
        /// </summary>
        public static float ApplyThermalPhaseTransitions(ReactionState state, float dt)
        {
            if (state == null) return 0f;

            float totalHeatKJ = 0f;
            totalHeatKJ += ApplyThermalOnArray(state.Reactants, state, state.TemperatureC, dt);
            totalHeatKJ += ApplyThermalOnArray(state.Products, state, state.TemperatureC, dt);
            return totalHeatKJ;
        }

        /// <summary>
        /// Evaporation from liquid/aqueous species.
        /// Removes moles from condensed phase and adds to state.EvaporatedGasMoles.
        /// Open containers evaporate faster than closed containers.
        /// </summary>
        public static float ApplyEvaporation(ReactionState state, float dt)
        {
            if (state == null || dt <= 0f) return 0f;

            float area = Mathf.Max(0.001f, state.SurfaceAreaM2);
            float openFactor = state.IsClosedContainer ? 0.2f : 1f;

            // Requested model:
            // evaporationRate = k * surfaceArea * exp(temperature / 100)
            float expArg = Mathf.Clamp(state.TemperatureC / 100f, -2f, 6f);
            float evapRate = BaseEvaporationRatePerSec * area * Mathf.Exp(expArg) * openFactor;
            float evaporated = 0f;

            evaporated += EvaporateArray(state.Reactants, evapRate, dt);
            evaporated += EvaporateArray(state.Products, evapRate, dt);

            state.EvaporatedGasMoles += evaporated;
            return evaporated;
        }

        /// <summary>
        /// Gas handling: in open containers, vent a fraction of gas each step.
        /// Returns vented moles.
        /// </summary>
        public static float ApplyGasEscape(ReactionState state, float dt)
        {
            if (state == null || state.IsClosedContainer || dt <= 0f) return 0f;

            float escapeRate = Mathf.Max(0f, state.GasEscapeRatePerSec);
            if (escapeRate <= 0f) return 0f;

            float escaped = 0f;
            escaped += EscapeGasFromArray(state.Reactants, escapeRate, dt);
            escaped += EscapeGasFromArray(state.Products, escapeRate, dt);

            if (state.EvaporatedGasMoles > 0f)
            {
                float vent = Mathf.Min(state.EvaporatedGasMoles, state.EvaporatedGasMoles * escapeRate * dt);
                state.EvaporatedGasMoles -= vent;
                escaped += vent;
            }

            return escaped;
        }

        // ─────────────────────────────────────────────────────
        //  FULL INTERACTION RESULT
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Full phase interaction evaluation: returns contact factor + dissolution info.
        /// </summary>
        public static PhaseInteraction Evaluate(
            ReactionState state,
            float grindingFactor,
            float dt)
        {
            var result = new PhaseInteraction
            {
                NewPrecipitates = new List<string>()
            };

            result.ContactFactor = GetContactFactor(state.Reactants, grindingFactor);

            bool hasSolid = HasSolid(state.Reactants);
            bool hasLiquid = HasLiquid(state.Reactants);

            result.DissolutionLimited = hasSolid && hasLiquid;

            if (result.DissolutionLimited && state.Reactants != null)
            {
                float totalDissolvable = 0f;
                for (int i = 0; i < state.Reactants.Length; i++)
                {
                    if (state.Reactants[i]?.Phase == Phase.Solid)
                        totalDissolvable += state.Reactants[i].Moles;
                }

                result.MaxDissolutionMolesPerStep = CalcDissolutionMoles(
                    totalDissolvable, 0f,
                    state.Reaction?.solubilityLimitMolPerL ?? 0f,
                    state.VolumeLiters, dt);
            }

            result.PhaseHeatKJ = ApplyThermalPhaseTransitions(state, dt);
            EnforceSolubility(state);
            result.EvaporatedMoles = ApplyEvaporation(state, dt);
            result.EscapedGasMoles = ApplyGasEscape(state, dt);

            return result;
        }

        // ─────────────────────────────────────────────────────
        //  PRIVATE HELPERS
        // ─────────────────────────────────────────────────────

        private static float GetPairFactor(Phase a, Phase b)
        {
            if (a == Phase.Liquid && b == Phase.Liquid) return FactorLiquidLiquid;
            if (a == Phase.Gas   && b == Phase.Gas)    return FactorGasGas;

            if ((a == Phase.Solid && b == Phase.Solid))   return FactorSolidSolid;
            if ((a == Phase.Solid && (b == Phase.Liquid || b == Phase.Aqueous)) ||
                ((a == Phase.Liquid || a == Phase.Aqueous) && b == Phase.Solid))
                return FactorSolidLiquid;
            if ((a == Phase.Solid && b == Phase.Gas) || (a == Phase.Gas && b == Phase.Solid))
                return FactorSolidGas;
            if ((a == Phase.Liquid && b == Phase.Gas) || (a == Phase.Gas && b == Phase.Liquid))
                return FactorLiquidGas;
            if ((a == Phase.Aqueous || b == Phase.Aqueous)) return FactorLiquidLiquid;

            return 0.5f; // Unknown combination
        }

        private static bool HasSolid(SpeciesState[] species)
        {
            if (species == null) return false;
            for (int i = 0; i < species.Length; i++)
                if (species[i]?.Phase == Phase.Solid) return true;
            return false;
        }

        private static bool HasLiquid(SpeciesState[] species)
        {
            if (species == null) return false;
            for (int i = 0; i < species.Length; i++)
            {
                var ph = species[i]?.Phase;
                if (ph == Phase.Liquid || ph == Phase.Aqueous) return true;
            }
            return false;
        }

        private static bool EnforceSolubilityOnArray(SpeciesState[] species, float volumeL)
        {
            if (species == null) return false;

            bool changed = false;
            for (int i = 0; i < species.Length; i++)
            {
                var s = species[i];
                if (s == null) continue;
                if (s.SolubilityMolPerL <= 0f) continue;
                if (s.Phase != Phase.Aqueous && s.Phase != Phase.Liquid) continue;

                // Requested model:
                // maxDissolved = solubility * volume
                // if current > maxDissolved => precipitate only the excess
                float maxDissolved = s.SolubilityMolPerL * volumeL;
                if (s.Moles <= maxDissolved) continue;

                float current = s.Moles;
                float excess = current - maxDissolved;

                // Keep dissolved concentration capped, move excess to precipitated inventory.
                s.Moles = Mathf.Max(0f, maxDissolved);
                s.PrecipitatedMoles += Mathf.Max(0f, excess);

                // Only mark phase as fully solid if no dissolved amount remains.
                // Otherwise keep as aqueous/liquid (with precipitated side inventory tracked).
                if (s.Moles <= 1e-9f)
                    s.Phase = Phase.Solid;

                changed = true;
            }

            return changed;
        }

        private static float ApplyThermalOnArray(SpeciesState[] species, ReactionState state, float tempC, float dt)
        {
            if (species == null || dt <= 0f) return 0f;

            float heatKJ = 0f;
            float phaseRate = 0.25f;

            for (int i = 0; i < species.Length; i++)
            {
                var s = species[i];
                if (s == null || s.Moles <= 0f) continue;

                bool hasMelting = !float.IsNaN(s.MeltingPointC);
                bool hasBoiling = !float.IsNaN(s.BoilingPointC);

                if (s.Phase == Phase.Solid && hasMelting && tempC >= s.MeltingPointC)
                {
                    float m = Mathf.Min(s.Moles, s.Moles * phaseRate * dt);
                    if (m > 0f)
                    {
                        s.Phase = Phase.Liquid;
                        float latent = s.LatentFusionKJPerMol > 0f ? s.LatentFusionKJPerMol : DefaultLatentFusionKJPerMol;
                        heatKJ -= m * latent;
                    }
                }
                else if ((s.Phase == Phase.Liquid || s.Phase == Phase.Aqueous) && hasBoiling && tempC >= s.BoilingPointC)
                {
                    float massConverted = Mathf.Min(s.Moles, s.Moles * phaseRate * dt);
                    if (massConverted > 0f)
                    {
                        float latent = s.LatentVaporizationKJPerMol > 0f
                            ? s.LatentVaporizationKJPerMol
                            : DefaultLatentVaporizationKJPerMol;

                        // Match expected flow:
                        //   energyUsed = latentHeat * massConverted
                        //   state.energy -= energyUsed
                        //   ConvertLiquidToGas(species, amount)
                        float energyUsed = latent * massConverted;
                        heatKJ -= energyUsed;
                        ConvertLiquidToGas(s, massConverted, state);
                    }
                }
                else if (s.Phase == Phase.Gas && hasBoiling && tempC < s.BoilingPointC - 1f)
                {
                    float m = Mathf.Min(s.Moles, s.Moles * phaseRate * dt);
                    if (m > 0f)
                    {
                        s.Phase = Phase.Liquid;
                        float latent = s.LatentVaporizationKJPerMol > 0f ? s.LatentVaporizationKJPerMol : DefaultLatentVaporizationKJPerMol;
                        heatKJ += m * latent;
                    }
                }
                else if ((s.Phase == Phase.Liquid || s.Phase == Phase.Aqueous) && hasMelting && tempC < s.MeltingPointC - 1f)
                {
                    float m = Mathf.Min(s.Moles, s.Moles * phaseRate * dt);
                    if (m > 0f)
                    {
                        s.Phase = Phase.Solid;
                        float latent = s.LatentFusionKJPerMol > 0f ? s.LatentFusionKJPerMol : DefaultLatentFusionKJPerMol;
                        heatKJ += m * latent;
                    }
                }
            }

            return heatKJ;
        }

        private static void ConvertLiquidToGas(SpeciesState species, float amount, ReactionState state)
        {
            if (species == null || state == null || amount <= 0f) return;
            if (species.Phase != Phase.Liquid && species.Phase != Phase.Aqueous) return;

            float converted = Mathf.Min(species.Moles, amount);
            if (converted <= 0f) return;

            species.Moles -= converted;
            state.EvaporatedGasMoles += converted;

            if (species.Moles <= 1e-9f)
            {
                species.Moles = 0f;
                species.Phase = Phase.Gas;
            }
        }

        private static float EvaporateArray(SpeciesState[] species, float evapRate, float dt)
        {
            if (species == null || evapRate <= 0f || dt <= 0f) return 0f;

            float total = 0f;
            for (int i = 0; i < species.Length; i++)
            {
                var s = species[i];
                if (s == null || s.Moles <= 0f) continue;
                if (s.Phase != Phase.Liquid && s.Phase != Phase.Aqueous) continue;

                float d = Mathf.Min(s.Moles, s.Moles * evapRate * dt);
                if (d <= 0f) continue;

                s.Moles -= d;
                total += d;
            }

            return total;
        }

        private static float EscapeGasFromArray(SpeciesState[] species, float escapeRate, float dt)
        {
            if (species == null || escapeRate <= 0f || dt <= 0f) return 0f;

            float total = 0f;
            for (int i = 0; i < species.Length; i++)
            {
                var s = species[i];
                if (s == null || s.Moles <= 0f) continue;
                if (s.Phase != Phase.Gas) continue;

                float d = Mathf.Min(s.Moles, s.Moles * escapeRate * dt);
                if (d <= 0f) continue;

                s.Moles -= d;
                total += d;
            }

            return total;
        }

        // ════════════════════════════════════════════════════════
        //  FILL CALCULATION
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Scientific layered height solver driven by live ReactionState.phases.
        ///
        /// liquidVolume = Σ(liquidMoles × molarVolume)
        /// solidVolume  = Σ(solidMoles  × molarVolume)
        /// liquidHeight = liquidVolume / containerVolume
        /// solidHeight  = solidVolume  / containerVolume
        /// foamHeight   = clamp(gasProductionRate × foamFactor, 0..maxFoamHeight)
        ///
        /// Constraints:
        /// - all heights clamped to [0,1]
        /// - if solidVolume &lt; solidVolumeEpsilon, solidHeight = 0
        /// - gas does NOT affect liquidHeight
        /// </summary>
        public static LayerHeights ComputeLayerHeights(
            ReactionState state,
            float containerVolume,
            float gasProductionRate,
            float foamFactor = 0.25f,
            float maxFoamHeight = 0.25f,
            float solidVolumeEpsilon = 1e-6f)
        {
            var result = new LayerHeights();
            if (state?.phases == null || containerVolume <= 0f)
                return result;

            float liquidVolume = 0f;
            float solidVolume = 0f;

            foreach (var kv in state.phases)
            {
                var phase = kv.Value;
                if (phase == null) continue;

                float molarVolume = phase.MolarVolumeLPerMol > 0f ? phase.MolarVolumeLPerMol : 0f;

                liquidVolume += Mathf.Max(0f, phase.liquidMoles) * molarVolume;
                solidVolume += Mathf.Max(0f, phase.solidMoles) * molarVolume;
            }

            if (solidVolume < Mathf.Max(0f, solidVolumeEpsilon))
                solidVolume = 0f;

            float liquidHeight = liquidVolume / containerVolume;
            float solidHeight = solidVolume / containerVolume;
            float foamHeight = gasProductionRate * foamFactor;

            if (float.IsNaN(liquidHeight) || float.IsInfinity(liquidHeight)) liquidHeight = 0f;
            if (float.IsNaN(solidHeight) || float.IsInfinity(solidHeight)) solidHeight = 0f;
            if (float.IsNaN(foamHeight) || float.IsInfinity(foamHeight)) foamHeight = 0f;

            result.liquidHeight = Mathf.Clamp01(liquidHeight);
            result.solidHeight = Mathf.Clamp01(solidHeight);
            result.foamHeight = Mathf.Clamp01(Mathf.Clamp(foamHeight, 0f, maxFoamHeight));
            return result;
        }

        /// <summary>
        /// Computes the liquid fill fraction [0,1] for a container.
        /// Gas and precipitated solids are excluded — only liquid moles contribute.
        /// </summary>
        public static float ComputeLiquidFill(ReactionState state, float containerVolume)
        {
            return ComputeLayerHeights(state, containerVolume, 0f).liquidHeight;
        }

        /// <summary>
        /// Computes the solid/precipitate fill fraction [0,1] from ReactionState phase data.
        /// Uses runtime per-species molar volumes and includes precipitated inventory.
        /// </summary>
        public static float ComputeSolidFill(ReactionState state, float containerVolume)
        {
            return ComputeLayerHeights(state, containerVolume, 0f).solidHeight;
        }

        /// <summary>
        /// Computes foam band height from live gas production rate.
        /// </summary>
        /// <param name="gasProductionRate">Gas production moles/sec from runtime state.</param>
        /// <param name="foamFactor">Linear conversion factor from gas rate to foam height.</param>
        /// <param name="maxFoamHeight">Maximum foam band height.</param>
        public static float ComputeFoamHeight(float gasProductionRate, float foamFactor = 0.25f, float maxFoamHeight = 0.25f)
        {
            float foam = gasProductionRate * foamFactor;

            if (float.IsNaN(foam) || float.IsInfinity(foam))
                foam = 0f;

            return Mathf.Clamp(foam, 0f, maxFoamHeight);
        }
    }
}
