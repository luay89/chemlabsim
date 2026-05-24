// ChemLabSim v3 — ChemFxState
// Rich snapshot of chemistry visuals derived from ChemistryOutput / live ReactionState.
// Built by ChemFXController and SimulationBridge, consumed by visual View components
// (ContainerFillController, HeatDistortionController, ChemParticleController, ReactionGraphView, ...).

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;

namespace ChemLabSimV3.Data
{
    public struct ChemFxState
    {
        // Identity
        public bool Found;
        public bool IsFailure;
        public string ReactionId;

        // Continuous reaction parameters
        public float ReactionRate;
        public float CompletionPercent;
        public float EnthalpyKJ;
        public bool  IsExothermic;
        public float TemperatureDelta;
        public float ArrheniusRate;
        public float EquilibriumExtent;

        // Discrete effect flags
        public bool HasGas;
        public bool HasPrecipitate;
        public bool HasColorChange;
        public bool HasHeat;
        public bool HasGlow;
        public bool HasSparks;
        public bool HasSmoke;
        public bool HasFoam;
        public bool HasFrost;

        // Color hints
        public string TargetColorHex;
        public string GlowColorHex;

        // Gas / pressure
        public float GasMolesProduced;
        public float PressureAtm;

        // Substance + condition snapshot
        public List<SubstanceState> Substances;
        public string BalancedEquation;
        public List<ConditionResult> Conditions;

        // Phase breakdown
        public Dictionary<string, PhaseState> Phases;
        public float ContainerVolumeLiters;

        // Pre-computed fill fractions
        public float LiquidFillFraction;
        public float SolidFillFraction;
        public float FoamFillFraction;
        public LayerHeights LayerHeights;

        // Layered rendering colors / animation
        public Color SolidLayerColor;
        public Color LiquidLayerColor;
        public Color FoamLayerColor;
        public float SolidAnimSpeed;
        public float LiquidAnimSpeed;
        public float FoamAnimSpeed;

        // Heating visuals
        public float HeatGlowIntensity;
        public float HeatDistortion;
        public float BubbleIntensity;
        public bool  IsBoiling;
        public float EvaporationRate;

        // Stirring visuals
        public float StirIntensity;
        public float VortexIntensity;
        public float StirWobbleX;
        public float StirWobbleZ;
    }
}
