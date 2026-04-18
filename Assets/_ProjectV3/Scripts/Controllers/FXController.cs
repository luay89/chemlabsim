// ChemLabSim v3 — FX Controller
// Decides which visual effects to trigger after a reaction.
// Publishes FxTriggeredEvent with FxState → ReactionFxView (view layer) plays particles.
// No ParticleSystems, no Materials, no scene references inside this controller.
//
// Migration source: LabController.PlayReactionFx() decision logic only.

using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    public class FXController : V3ControllerBase
    {
        public FxState CurrentState { get; private set; }

        // -- Lifecycle -----------------------------------------

        protected override void OnInitialize()
        {
            EventBus.Subscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Subscribe<ReactionNotFoundEvent>(OnReactionNotFound);
            Debug.Log("[FXController] Initialized.");
        }

        protected override void OnTeardown()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnReactionNotFound);
        }

        // -- Event Handlers ------------------------------------

        private void OnReactionEvaluated(ReactionEvaluatedEvent evt)
        {
            PublishState(BuildFxState(evt.Result, evt.Input));
        }

        private void OnReactionNotFound(ReactionNotFoundEvent evt)
        {
            PublishState(new FxState { StopAll = true, PlayFail = true });
        }

        // -- FX Decision Logic (from v2 PlayReactionFx) -------

        private static FxState BuildFxState(ReactionEvaluationResult eval, ReactionEvaluationInput input)
        {
            var state = new FxState { StopAll = true };

            if (!eval.IsValid)
            {
                state.PlayFail = true;
                return state;
            }

            bool reacted = eval.Status == ReactionStatus.Success ||
                           eval.Status == ReactionStatus.Partial;

            if (!reacted)
            {
                state.PlayFail = true;
                return state;
            }

            state.PlaySuccess = true;

            var vfx = input.reaction?.visual_effects;

            // Gas-producing reaction — bubbles
            if (input.reaction != null && input.reaction.GetProducesGas())
                state.PlayGas = true;

            // Catalyst applied
            if (eval.CatalystApplied)
                state.PlayCatalyst = true;

            // Activation reached → heat effect (skip negligible delta)
            if (!eval.ActivationNotReached)
            {
                float td = vfx?.temperature_delta ?? 0f;
                state.TemperatureDelta = td;
                if (Mathf.Abs(td) >= 2f)
                    state.PlayHeat = true;
            }

            // Precipitate
            if (vfx != null && vfx.precipitate)
                state.PlayPrecipitate = true;

            // Color change
            if (vfx != null && !string.IsNullOrEmpty(vfx.color_change))
            {
                state.PlayColorChange = true;
                state.ColorChangeHex = vfx.color_change;
            }

            // Extended VFX (glow, sparks, smoke, foam, frost)
            if (vfx != null)
            {
                if (vfx.glow)   state.PlayGlow   = true;
                if (vfx.sparks) state.PlaySparks = true;
                if (vfx.smoke)  state.PlaySmoke  = true;
                if (vfx.foam)   state.PlayFoam   = true;
                if (vfx.frost)  state.PlayFrost  = true;
            }

            return state;
        }

        // -- Publish -------------------------------------------

        private void PublishState(FxState state)
        {
            CurrentState = state;
            EventBus.Publish(new FxTriggeredEvent { State = state });
        }
    }
}
