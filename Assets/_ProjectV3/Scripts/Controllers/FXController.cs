// ChemLabSim v3 — FX Controller
// Decides which visual effects to trigger after a reaction.
// Publishes FxTriggeredEvent with FxState → ReactionFxView (view layer) plays particles.
// No ParticleSystems, no Materials, no scene references inside this controller.
//
// Migration source: LabController.PlayReactionFx() decision logic only.

using System;
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
            EventBus.Subscribe<EnvironmentChangedEvent>(OnEnvironmentChanged);
            Debug.Log("[FXController] Initialized.");
        }

        protected override void OnTeardown()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(OnReactionEvaluated);
            EventBus.Unsubscribe<ReactionNotFoundEvent>(OnReactionNotFound);
            EventBus.Unsubscribe<EnvironmentChangedEvent>(OnEnvironmentChanged);
        }

        // -- Event Handlers ------------------------------------

        private void OnReactionEvaluated(ReactionEvaluatedEvent evt)
        {
            // Defensive guard: a "valid" evaluation with a null reaction reference
            // would otherwise mask real data-corruption bugs (e.g. mis-decrypted
            // reactions.bytes). Warn loudly but fall through to a safe fail state
            // instead of crashing the FX pipeline.
            if (evt.Result.IsValid && evt.Input.reaction == null)
            {
                Debug.LogWarning("[FXController] ReactionEvaluatedEvent marked valid but Input.reaction is null. " +
                                 "Check reactions.bytes integrity (Tools/Security/Encrypt Reactions JSON -> bytes).");
                PublishState(new FxState { StopAll = true, PlayFail = true });
                return;
            }

            try
            {
                PublishState(BuildFxState(evt.Result, evt.Input));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FXController] BuildFxState threw: {ex.Message}\n{ex.StackTrace}");
                PublishState(new FxState { StopAll = true, PlayFail = true });
            }
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

            // Color change — pre-validate hex so view layer never receives garbage.
            if (vfx != null && !string.IsNullOrEmpty(vfx.color_change))
            {
                if (ColorUtility.TryParseHtmlString(vfx.color_change, out _))
                {
                    state.PlayColorChange = true;
                    state.ColorChangeHex = vfx.color_change;
                }
                else
                {
                    Debug.LogWarning($"[FXController] Invalid color_change hex '{vfx.color_change}' " +
                                     $"on reaction '{input.reaction?.id ?? "<unknown>"}' — skipping color FX.");
                }
            }

            // Extended VFX (glow, sparks, smoke, foam, frost)
            if (vfx != null)
            {
                if (vfx.glow)   state.PlayGlow   = true;
                if (vfx.sparks) state.PlaySparks = true;
                if (vfx.smoke)  state.PlaySmoke  = true;
                if (vfx.foam)   state.PlayFoam   = true;
                if (vfx.frost)  state.PlayFrost  = true;

                // ── Enhanced VFX (Phase 2) ──
                // Dissolution: solid disappearing into liquid
                if (vfx.dissolution)
                    state.PlayDissolution = true;

                // Crystallization: crystals forming from solution
                if (vfx.crystallization)
                    state.PlayCrystallization = true;

                // Effervescence level
                if (!string.IsNullOrEmpty(vfx.effervescence) && vfx.effervescence != "none")
                {
                    state.EffervescenceLevel = vfx.effervescence switch
                    {
                        "mild"     => 1,
                        "vigorous" => 2,
                        "violent"  => 3,
                        _          => 0
                    };
                    // Also ensure gas bubbles play for effervescent reactions
                    if (state.EffervescenceLevel > 0)
                        state.PlayGas = true;
                }

                // Vortex (stirring-related visual)
                if (vfx.vortex)
                    state.PlayVortex = true;

                // Vapor/steam
                if (vfx.vapor)
                    state.PlayVapor = true;

                // Turbidity (liquid becomes cloudy)
                if (vfx.turbidity)
                    state.PlayTurbidity = true;

                // Electrical effect (electrochemistry)
                if (vfx.electrical)
                    state.PlayElectrical = true;

                // Precipitate color override
                if (!string.IsNullOrEmpty(vfx.precipitate_color))
                    state.PrecipitateColorHex = vfx.precipitate_color;
            }

            return state;
        }

        // -- Publish -------------------------------------------

        private void PublishState(FxState state)
        {
            CurrentState = state;
            EventBus.Publish(new FxTriggeredEvent { State = state });
        }

        // ============================================================
        //  Continuous environmental VFX (temperature / stirring sliders)
        // ============================================================

        // Threshold constants — chosen to match the task spec.
        private const float HeatStartC      = 50f;   // °C at which steam/glow begin
        private const float HeatFullC       = 100f;  // °C at which heat FX peak
        private const float StirringStart   = 0.20f; // fraction at which vortex engages
        private const float MaxSteamRate    = 30f;   // particles/sec at full heat
        private const float MaxDistortion   = 0.80f; // normalised screen distortion
        private const float MinSpinDegPerS  = 60f;   // vortex spin at threshold
        private const float MaxSpinDegPerS  = 240f;  // vortex spin at full stirring

        public EnvironmentFxChangedEvent CurrentEnvironmentFx { get; private set; }

        private void OnEnvironmentChanged(EnvironmentChangedEvent evt)
        {
            var fx = ComputeEnvironmentFx(evt.Temperature, evt.Stirring);
            CurrentEnvironmentFx = fx;
            EventBus.Publish(fx);
        }

        /// <summary>
        /// Pure threshold mapping for the temperature/stirring sliders. Kept
        /// internal-static so it can be unit-tested without instantiating a
        /// MonoBehaviour and so the rules live in one place.
        /// </summary>
        internal static EnvironmentFxChangedEvent ComputeEnvironmentFx(float temperatureC, float stirring)
        {
            // Temperature → heat/steam/distortion (kicks in above 50 °C).
            float heat = Mathf.Clamp01((temperatureC - HeatStartC) / (HeatFullC - HeatStartC));
            float steam = heat * MaxSteamRate;
            float distortion = heat * MaxDistortion;

            // Stirring → vortex spin (engages above 20%).
            float spin = 0f;
            bool showVortex = stirring > StirringStart;
            if (showVortex)
            {
                float t = Mathf.InverseLerp(StirringStart, 1f, Mathf.Clamp01(stirring));
                spin = Mathf.Lerp(MinSpinDegPerS, MaxSpinDegPerS, t);
            }

            return new EnvironmentFxChangedEvent
            {
                HeatIntensity     = heat,
                SteamEmissionRate = steam,
                ScreenDistortion  = distortion,
                VortexSpinSpeed   = spin,
                ShowVortex        = showVortex
            };
        }
    }
}
