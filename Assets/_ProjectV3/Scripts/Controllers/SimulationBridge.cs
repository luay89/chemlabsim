// ChemLabSim v3 — Simulation Bridge
// Links the live ReactionState to the visual layer.
// Subscribes to SimulationTickEvent and converts the evolving
// ReactionState into a ChemFxState every frame — driving ALL visuals
// from the live simulation rather than static output.
//
// This replaces ChemFXController's one-shot conversion for sim-driven visuals.
// ChemFXController remains for legacy one-shot ChemistryProcessedEvent.

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    public class SimulationBridge : MonoBehaviour
    {
        /// <summary>Most recent ChemFxState produced from live simulation.</summary>
        public ChemFxState CurrentState { get; private set; }

        /// <summary>True while a simulation is feeding data.</summary>
        public bool IsActive { get; private set; }

        private float _peakTemperatureC;
        private float _lastGasMoles;
        private float _lastElapsedTime;
        private float _lastEvaporatedMoles;
    /// <summary>Latest stir intensity received from StirringChangedEvent [0–1].</summary>
    private float _currentStirIntensity;

        private void OnEnable()
        {
            EventBus.Subscribe<SimulationStartedEvent>(OnSimStarted);
            EventBus.Subscribe<SimulationTickEvent>(OnTick);
            EventBus.Subscribe<SimulationCompletedEvent>(OnSimCompleted);
                EventBus.Subscribe<StirringChangedEvent>(OnStirringChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SimulationStartedEvent>(OnSimStarted);
            EventBus.Unsubscribe<SimulationTickEvent>(OnTick);
            EventBus.Unsubscribe<SimulationCompletedEvent>(OnSimCompleted);
                EventBus.Unsubscribe<StirringChangedEvent>(OnStirringChanged);
        }

        private void OnStirringChanged(StirringChangedEvent evt)
        {
            _currentStirIntensity = evt.Intensity;
        }

        private void OnSimStarted(SimulationStartedEvent evt)
        {
            IsActive = true;
            _peakTemperatureC = evt.State.TemperatureC;
            _lastGasMoles = evt.State.TotalGasMoles;
            _lastElapsedTime = evt.State.ElapsedTime;
            _lastEvaporatedMoles = evt.State.EvaporatedGasMoles;
        }

        private void OnTick(SimulationTickEvent evt)
        {
            var rs = evt.State;
            if (rs == null) return;

            // Track peak temperature for visual intensity
            if (Mathf.Abs(rs.TemperatureC) > Mathf.Abs(_peakTemperatureC))
                _peakTemperatureC = rs.TemperatureC;

            var state = BuildChemFxState(rs);
            CurrentState = state;

            // Publish for ALL visual consumers (particles, container, graph, etc.)
            EventBus.Publish(new ChemFxTriggeredEvent { State = state });

            // Also publish a ChemistryProcessedEvent snapshot for views
            // that consume the static output format (ChemResultView, etc.)
            var snapshot = SimulationStepper.BuildSnapshotOutput(rs);
            snapshot.Visuals = rs.Visuals;
            EventBus.Publish(new ChemistryProcessedEvent(snapshot));
        }

        private void OnSimCompleted(SimulationCompletedEvent evt)
        {
            // Final state update
            if (evt.State != null)
            {
                var finalState = BuildChemFxState(evt.State);
                CurrentState = finalState;
                EventBus.Publish(new ChemFxTriggeredEvent { State = finalState });
            }

            IsActive = false;
        }

        // ════════════════════════════════════════════════════════
        //  STATE CONVERSION
        // ════════════════════════════════════════════════════════

        private ChemFxState BuildChemFxState(ReactionState rs)
        {
            // Temperature delta from initial ambient
            float tempDelta = rs.TemperatureC - rs.AmbientTemperatureC;

            // Gas moles from products
            float gasMoles = rs.TotalGasProductMoles;

            // Gas production rate from live ReactionState deltas (mol/s)
            float dt = Mathf.Max(1e-4f, rs.ElapsedTime - _lastElapsedTime);
            float currentGas = rs.TotalGasMoles;
            float gasProductionRate = Mathf.Max(0f, (currentGas - _lastGasMoles) / dt);
            _lastGasMoles = currentGas;
            _lastElapsedTime = rs.ElapsedTime;

            // Evaporation rate (mol/s) from EvaporatedGasMoles delta
            float currentEvap = rs.EvaporatedGasMoles;
            float evaporationRate = Mathf.Max(0f, (currentEvap - _lastEvaporatedMoles) / dt);
            _lastEvaporatedMoles = currentEvap;

            // Solid product moles (for precipitate)
            bool hasPrecipitate = false;
            if (rs.Products != null)
            {
                for (int i = 0; i < rs.Products.Length; i++)
                {
                    if (rs.Products[i].Phase == Phase.Solid && rs.Products[i].Moles > 0.01f)
                    {
                        hasPrecipitate = true;
                        break;
                    }
                }
            }

            var vfx = rs.Visuals;

            LayerHeights layerHeights = PhaseInteractionModel.ComputeLayerHeights(
                rs,
                rs.VolumeLiters,
                gasProductionRate);

            float solidFill = layerHeights.solidHeight;
            float liquidFill = layerHeights.liquidHeight;
            float foamFill = layerHeights.foamHeight;

            Color liquidColor;
            if (!string.IsNullOrWhiteSpace(vfx.ColorHex) && ColorUtility.TryParseHtmlString(vfx.ColorHex, out var parsedLiquid))
                liquidColor = new Color(parsedLiquid.r, parsedLiquid.g, parsedLiquid.b, 0.82f);
            else
                liquidColor = new Color(0.30f, 0.60f, 0.85f, 0.82f);

            float precipStrength = Mathf.Clamp01(solidFill / 0.2f);
            Color solidColor = Color.Lerp(new Color(0.38f, 0.34f, 0.30f, 0.75f), new Color(0.86f, 0.84f, 0.78f, 0.96f), precipStrength);

            float foamAlpha = Mathf.Clamp01(foamFill / 0.18f);
            Color foamColor = new Color(0.96f, 0.98f, 1.00f, Mathf.Lerp(0.15f, 0.85f, foamAlpha));

            float liquidAnim = Mathf.Clamp(0.35f + Mathf.Abs(rs.CurrentRate) * 2.0f + Mathf.Abs(tempDelta) * 0.02f, 0.2f, 4.0f);
            float solidAnim = Mathf.Clamp(0.15f + Mathf.Abs(rs.CurrentRate) * 0.4f, 0.05f, 1.2f);
            float foamAnim = Mathf.Clamp(0.30f + gasProductionRate * 2.5f, 0.1f, 5.0f);

            // ── Heating visuals ────────────────────────────────────────────
            // HeatGlowIntensity: ramps 0→1 as temperature rises 5→100 °C above ambient
            float heatGlowIntensity = Mathf.Clamp01(Mathf.Max(0f, tempDelta - 5f) / 95f);

            // HeatDistortion: heat-shimmer strength, ramps with temperature delta
            float heatDistortion = Mathf.Clamp01(Mathf.Max(0f, tempDelta) / 120f);

            // IsBoiling / BoilingPointC
            float boilingPt = HeatingService.FindLowestLiquidBoilingPointC(rs);
            bool isBoiling  = rs.TemperatureC >= boilingPt;

            // BubbleIntensity: boiling drives strong bubble visual; gas production adds to it
            float boilingFactor  = isBoiling
                ? Mathf.Clamp01((rs.TemperatureC - boilingPt) / 20f + 0.6f)
                : Mathf.Clamp01((rs.TemperatureC - boilingPt * 0.8f) / (boilingPt * 0.2f + 1f)) * 0.4f;
            float bubbleIntensity = Mathf.Clamp01(boilingFactor + Mathf.Clamp01(gasProductionRate / 3f));

            return new ChemFxState
            {
                Found             = true,
                IsFailure         = rs.StopReason == StopReason.ConditionsFailed
                                  || rs.StopReason == StopReason.OverpressureExplosion,
                ReactionId        = rs.Reaction?.id ?? string.Empty,

                // Continuous parameters — ALL driven by live ReactionState
                ReactionRate      = rs.CurrentRate,
                CompletionPercent = rs.CompletionPercent,
                EnthalpyKJ        = rs.EnthalpyKJ,
                IsExothermic      = rs.IsExothermic,
                TemperatureDelta  = tempDelta,
                ArrheniusRate     = rs.ArrheniusRate,
                EquilibriumExtent = rs.EquilibriumExtent,

                // Discrete effects — from visual hints + live chemistry
                HasGas            = vfx.GasParticles || gasMoles > 0.01f,
                HasPrecipitate    = vfx.Precipitate || hasPrecipitate,
                HasColorChange    = vfx.ColorChange,
                HasHeat           = vfx.HeatGlow || Mathf.Abs(tempDelta) > 2f,
                HasGlow           = vfx.Glow || Mathf.Abs(rs.EnthalpyKJ) > 30f,
                HasSparks         = vfx.Sparks && rs.Progress > 0.1f,
                HasSmoke          = vfx.Smoke && rs.Progress > 0.05f,
                HasFoam           = vfx.Foam && rs.Progress > 0.05f,
                HasFrost          = vfx.Frost || (tempDelta < -5f),

                // Color
                TargetColorHex    = vfx.ColorHex ?? string.Empty,
                GlowColorHex      = rs.IsExothermic ? "#FF8020" : "#4DA6FF",

                // Gas/Pressure — live values
                GasMolesProduced  = gasMoles,
                PressureAtm       = rs.PressureAtm,

                // Phase breakdown — for scientific fill calculation
                Phases            = rs.phases,
                ContainerVolumeLiters = rs.VolumeLiters,

                // Pre-computed fill — avoids per-frame dictionary iteration in views
                LiquidFillFraction = liquidFill,
                SolidFillFraction  = solidFill,
                FoamFillFraction   = foamFill,
                LayerHeights       = layerHeights,

                // Layered rendering params (all derived from live ReactionState)
                LiquidLayerColor   = liquidColor,
                SolidLayerColor    = solidColor,
                FoamLayerColor     = foamColor,
                LiquidAnimSpeed    = liquidAnim,
                SolidAnimSpeed     = solidAnim,
                FoamAnimSpeed      = foamAnim,

                // Heating visuals
                HeatGlowIntensity  = heatGlowIntensity,
                HeatDistortion     = heatDistortion,
                BubbleIntensity    = bubbleIntensity,
                IsBoiling          = isBoiling,
                EvaporationRate    = evaporationRate,

                    // Stirring visuals
                    StirIntensity      = _currentStirIntensity,
                    VortexIntensity    = _currentStirIntensity,
                    StirWobbleX        = StirringService.Evaluate(_currentStirIntensity).WobbleAmplitude,
                    StirWobbleZ        = StirringService.Evaluate(_currentStirIntensity).WobbleAmplitude * 0.75f,

                // Substances snapshot
                Substances        = BuildSubstanceList(rs),
                BalancedEquation  = rs.BalancedEquation,
                Conditions        = rs.Conditions
            };
        }

        private static List<SubstanceState> BuildSubstanceList(ReactionState rs)
        {
            var list = new List<SubstanceState>();

            if (rs.Reactants != null)
            {
                for (int i = 0; i < rs.Reactants.Length; i++)
                {
                    var r = rs.Reactants[i];
                    list.Add(new SubstanceState
                    {
                        Formula              = r.Formula,
                        Phase                = r.Phase,
                        MolesInitial         = r.InitialMoles,
                        MolesFinal           = r.Moles,
                        MolesConsumed        = r.InitialMoles - r.Moles,
                        ConcentrationMolPerL = r.Concentration(rs.VolumeLiters),
                        IsReactant           = true,
                        IsProduct            = false,
                        IsLimitingReagent    = i == rs.LimitingIndex,
                        IsExcess             = i != rs.LimitingIndex
                    });
                }
            }

            if (rs.Products != null)
            {
                for (int i = 0; i < rs.Products.Length; i++)
                {
                    var p = rs.Products[i];
                    list.Add(new SubstanceState
                    {
                        Formula              = p.Formula,
                        Phase                = p.Phase,
                        MolesInitial         = 0f,
                        MolesFinal           = p.Moles,
                        MolesConsumed        = 0f,
                        ConcentrationMolPerL = p.Concentration(rs.VolumeLiters),
                        IsReactant           = false,
                        IsProduct            = true,
                        IsLimitingReagent    = false,
                        IsExcess             = false
                    });
                }
            }

            return list;
        }
    }
}
