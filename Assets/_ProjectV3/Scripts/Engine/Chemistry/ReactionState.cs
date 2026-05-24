// ChemLabSim v3 — Reaction State (Mutable Runtime State)
// Holds the evolving state of a chemical reaction during simulation.
// Updated every frame by SimulationStepper. Read by SimulationBridge
// to drive visuals continuously.
//
// This replaces the one-shot ChemistryOutput for real-time simulation.
// ChemistryOutput remains available as a snapshot format.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChemLabSimV3.Engine.Chemistry
{
    [Serializable]
    public class PhaseState
    {
        public float solidMoles;
        public float liquidMoles;
        public float gasMoles;
        /// <summary>Liquid molar volume in L/mol. Used for fill calculation.</summary>
        public float MolarVolumeLPerMol;
    }

    /// <summary>
    /// Per-species mutable state tracked during simulation.
    /// </summary>
    [Serializable]
    public class SpeciesState
    {
        public string Formula;
        public Phase Phase;
        public float StoichCoeff;
        public float InitialMoles;
        public float Moles;
        public bool IsReactant;
        public bool IsProduct;
        public float MeltingPointC;
        public float BoilingPointC;
        public float LatentFusionKJPerMol;
        public float LatentVaporizationKJPerMol;
        public float SolubilityMolPerL;
        public float PrecipitatedMoles;
        /// <summary>Liquid molar volume in L/mol (from species data). Default ~0.018.</summary>
        public float MolarVolumeLPerMol;

        /// <summary>Concentration in current volume (mol/L).</summary>
        public float Concentration(float volumeL)
        {
            return volumeL > 0f ? Moles / volumeL : 0f;
        }

        /// <summary>Fraction consumed (reactant) or produced (product).</summary>
        public float FractionChanged
        {
            get
            {
                if (InitialMoles <= 0f) return Moles > 0f ? 1f : 0f;
                if (IsReactant) return 1f - Moles / InitialMoles;
                return Moles / InitialMoles; // product: fraction of theoretical
            }
        }
    }

    /// <summary>
    /// Mutable runtime state of an active chemical reaction.
    /// Evolves every frame via SimulationStepper.
    /// </summary>
    public class ReactionState
    {
        // ── Species ────────────────────────────────────────────
        public SpeciesState[] Reactants;
        public SpeciesState[] Products;

        // Per-formula phase map for quick lookups / UI bindings
        public Dictionary<string, PhaseState> phases;

        // ── Thermodynamic state (evolving) ─────────────────────
        /// <summary>Current mixture temperature (°C). Changes from ΔH feedback.</summary>
        public float TemperatureC;

        /// <summary>Ambient temperature (°C). Heat dissipates toward this.</summary>
        public float AmbientTemperatureC;

        /// <summary>Current pressure (atm). Changes from gas evolution.</summary>
        public float PressureAtm;

        /// <summary>Reaction volume (L). Fixed for now.</summary>
        public float VolumeLiters;

        /// <summary>Headspace gas volume used for pressure calculations (L).</summary>
        public float HeadspaceVolumeLiters;

        /// <summary>Open beaker (false) or closed container (true).</summary>
        public bool IsClosedContainer;

        /// <summary>Effective liquid surface area exposed to air (m²).</summary>
        public float SurfaceAreaM2;

        /// <summary>Heat transfer coefficient for dissipation (1/s).</summary>
        public float HeatTransferCoefficient;

        /// <summary>Fraction of gas vented per second in open containers.</summary>
        public float GasEscapeRatePerSec;

        /// <summary>Maximum safe pressure for closed container before rupture (atm).</summary>
        public float MaxPressureAtm;

        /// <summary>Set true if a closed container exceeded max pressure.</summary>
        public bool HasExploded;

        /// <summary>Total heat capacity used for temperature updates (kJ/K).</summary>
        public float HeatCapacityKJPerK;

        /// <summary>Moles transferred to vapor phase through evaporation.</summary>
        public float EvaporatedGasMoles;

        // ── Progress ───────────────────────────────────────────
        /// <summary>Current reaction extent (moles of "reaction events").
        /// 0 = not started, MaxExtent = complete.</summary>
        public float Extent;

        /// <summary>Maximum possible extent (from limiting reagent stoichiometry).</summary>
        public float MaxExtent;

        /// <summary>Reaction progress 0–1 (Extent / MaxExtent).</summary>
        public float Progress => MaxExtent > 0f ? Mathf.Clamp01(Extent / MaxExtent) : 0f;

        /// <summary>Completion percentage 0–100.</summary>
        public float CompletionPercent => Progress * 100f;

        /// <summary>True when reaction has reached equilibrium or limiting reagent depleted.</summary>
        public bool IsComplete;

        /// <summary>Reason the simulation stopped.</summary>
        public StopReason StopReason;

        /// <summary>Wall-clock time the simulation has been running (seconds).</summary>
        public float ElapsedTime;

        // ── Rates (updated each step) ──────────────────────────
        /// <summary>Current instantaneous rate (extent per second, scaled).</summary>
        public float CurrentRate;

        /// <summary>Current Arrhenius multiplier at this temperature.</summary>
        public float ArrheniusRate;

        /// <summary>Absolute forward rate r_f (mol/L/s) from law of mass action.</summary>
        public float ForwardRate;

        /// <summary>Absolute reverse rate r_r (mol/L/s). Zero for irreversible reactions.</summary>
        public float ReverseRate;

        /// <summary>
        /// Reaction quotient Q = Π[products]^ν / Π[reactants]^ν.
        /// Compare against CurrentKeq to assess equilibrium direction.
        /// </summary>
        public float ReactionQuotient;

        /// <summary>True when Q ≈ Keq within 2% tolerance.</summary>
        public bool IsAtEquilibrium;

        /// <summary>Equilibrium extent (0–1). Reaction cannot exceed this.</summary>
        public float EquilibriumExtent;

        /// <summary>Effective Keq at current temperature (van't Hoff adjusted).</summary>
        public float CurrentKeq;

        // ── Reaction reference ─────────────────────────────────
        /// <summary>The reaction definition driving this simulation.</summary>
        public ReactionEntry Reaction;

        /// <summary>Pre-built balanced equation string.</summary>
        public string BalancedEquation;

        /// <summary>Formula of the limiting reagent.</summary>
        public string LimitingReagent;

        /// <summary>Index of limiting reagent in Reactants array.</summary>
        public int LimitingIndex;

        // ── Enthalpy ───────────────────────────────────────────
        /// <summary>ΔH in kJ/mol (negative = exothermic).</summary>
        public float EnthalpyKJ;

        /// <summary>True if exothermic.</summary>
        public bool IsExothermic;

        /// <summary>Activation energy kJ/mol (effective, after catalyst).</summary>
        public float EffectiveEaKJ;

        // ── Condition pipeline result ──────────────────────────
        /// <summary>Base rate from condition pipeline (0–1).</summary>
        public float ConditionRate;

        /// <summary>Individual condition results for display.</summary>
        public List<ConditionResult> Conditions;

        // ── Visual binding data ────────────────────────────────
        /// <summary>Visual hints (gas, precipitate, color, etc.).</summary>
        public VisualHints Visuals;

        // ── Helpers ────────────────────────────────────────────

        /// <summary>Total gas moles currently present in products.</summary>
        public float TotalGasProductMoles
        {
            get
            {
                float total = 0f;
                if (Products == null) return 0f;
                for (int i = 0; i < Products.Length; i++)
                {
                    if (Products[i].Phase == Phase.Gas)
                        total += Products[i].Moles;
                }
                return total;
            }
        }

        /// <summary>Total gas moles across reactants + products + evaporated vapor.</summary>
        public float TotalGasMoles
        {
            get
            {
                float total = EvaporatedGasMoles;

                if (Reactants != null)
                    for (int i = 0; i < Reactants.Length; i++)
                        if (Reactants[i].Phase == Phase.Gas)
                            total += Reactants[i].Moles;

                if (Products != null)
                    for (int i = 0; i < Products.Length; i++)
                        if (Products[i].Phase == Phase.Gas)
                            total += Products[i].Moles;

                return total;
            }
        }

        /// <summary>Total moles in the system (reactants + products).</summary>
        public float TotalMoles
        {
            get
            {
                float total = 0f;
                if (Reactants != null)
                    for (int i = 0; i < Reactants.Length; i++)
                        total += Reactants[i].Moles;
                if (Products != null)
                    for (int i = 0; i < Products.Length; i++)
                        total += Products[i].Moles;
                return total;
            }
        }

        /// <summary>Temperature in Kelvin.</summary>
        public float TemperatureK => TemperatureC + 273.15f;
    }

    public enum StopReason
    {
        None,
        LimitingReagentDepleted,
        EquilibriumReached,
        ConditionsFailed,
        OverpressureExplosion,
        ManualStop
    }
}
