// ChemLabSim v3 — Chemistry FX Controller
// Subscribes to ChemistryProcessedEvent and translates the rich
// ChemistryOutput into a ChemFxState for next-gen visual views.
// Also publishes ChemFxTriggeredEvent for downstream consumers.

using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    public class ChemFXController : V3ControllerBase
    {
        public ChemFxState CurrentState { get; private set; }

        protected override void OnInitialize()
        {
            EventBus.Subscribe<ChemistryProcessedEvent>(OnChemistryProcessed);
            Debug.Log("[ChemFXController] Initialized.");
        }

        protected override void OnTeardown()
        {
            EventBus.Unsubscribe<ChemistryProcessedEvent>(OnChemistryProcessed);
        }

        private void OnChemistryProcessed(ChemistryProcessedEvent evt)
        {
            var state = BuildChemFxState(evt.Output);
            CurrentState = state;
            EventBus.Publish(new ChemFxTriggeredEvent { State = state });
        }

        private static ChemFxState BuildChemFxState(ChemistryOutput output)
        {
            var state = new ChemFxState
            {
                Found             = output.Found,
                IsFailure         = !output.Found || output.Status == ReactionStatus.Fail,
                ReactionId        = output.ReactionId,

                ReactionRate      = output.ConditionRate,
                CompletionPercent = output.CompletionPercent,
                EnthalpyKJ        = output.EnthalpyKJ,
                IsExothermic      = output.IsExothermic,
                TemperatureDelta  = output.Visuals.TemperatureDelta,
                ArrheniusRate     = output.RateMultiplier,
                EquilibriumExtent = output.EquilibriumExtent,

                HasGas            = output.Visuals.GasParticles,
                HasPrecipitate    = output.Visuals.Precipitate,
                HasColorChange    = output.Visuals.ColorChange,
                HasHeat           = output.Visuals.HeatGlow,
                HasGlow           = output.Visuals.Glow,
                HasSparks         = output.Visuals.Sparks,
                HasSmoke          = output.Visuals.Smoke,
                HasFoam           = output.Visuals.Foam,
                HasFrost          = output.Visuals.Frost,

                TargetColorHex    = output.Visuals.ColorHex ?? string.Empty,
                GlowColorHex      = output.IsExothermic ? "#FF8020" : "#4DA6FF",

                GasMolesProduced  = CalcGasMoles(output),
                PressureAtm       = output.PressureAtm,

                Substances        = output.Substances,
                BalancedEquation  = output.BalancedEquation,
                Conditions        = output.Conditions
            };

            return state;
        }

        private static float CalcGasMoles(ChemistryOutput output)
        {
            float total = 0f;
            if (output.Substances == null) return 0f;
            for (int i = 0; i < output.Substances.Count; i++)
            {
                var s = output.Substances[i];
                if (s.IsProduct && s.Phase == Phase.Gas)
                    total += s.MolesFinal;
            }
            return total;
        }
    }
}
