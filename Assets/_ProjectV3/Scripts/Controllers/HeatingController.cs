// ChemLabSim v3 — Heating Controller
// MonoBehaviour that drives frame-by-frame burner heating by delegating
// to HeatingService (pure physics).
//
// Usage:
//   1. Attach to any GameObject (or add via script).
//   2. Wire _reactionController in the Inspector (or via Init call).
//   3. Call BeginHeating(state, powerWatts) from any input handler.
//   4. Call StopHeating() to turn the burner off.
//
// Events published:
//   HeatingStartedEvent  — once when burner turns on
//   HeatingTickEvent     — every Update() while heating
//   HeatingStoppedEvent  — once when burner turns off
//
// Physics contract:
//   Heat is applied via SimulationStepper.AddExternalHeat(kJ) when a
//   simulation is running (ensures the stepper's own temperature clamp
//   and pressure model stay consistent).  When no stepper is active,
//   temperature is written directly to the ReactionState reference.
//
// Effects driven automatically (no extra wiring needed):
//   Arrhenius rate  — recalculated by SimulationStepper each frame
//   Boiling         — reported via HeatingTickEvent.IsBoiling
//   Evaporation     — HeatingStepResult.EvaporationRateMolPerSec
//   Visual binding  — HeatGlowIntensity/BubbleIntensity populated in
//                     SimulationBridge → ChemFxState → ContainerFillController

using UnityEngine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    public class HeatingController : V3ControllerBase
    {
        // ── Inspector ─────────────────────────────────────────────────────
        [SerializeField]
        [Tooltip("Optional reference to ReactionController. Used to access the active " +
                 "SimulationStepper so heat is applied inside the stepper's Update cycle.")]
        private ReactionController _reactionController;

        // ── Session state ─────────────────────────────────────────────────
        private ReactionState _state;
        private float         _powerWatts;
        private float         _maxSafeTempC;
        private bool          _allowExplosion;
        private bool          _active;
        private float         _totalEnergyKJ;

        // ── Public state ──────────────────────────────────────────────────
        /// <summary>True while the burner is actively heating.</summary>
        public bool IsHeating => _active;

        /// <summary>Current power setting (W).</summary>
        public float PowerWatts => _powerWatts;

        /// <summary>Cumulative energy delivered this session (kJ).</summary>
        public float TotalEnergyKJ => _totalEnergyKJ;

        // ════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Start heating.  Calling this while already active replaces
        /// the current session immediately.
        /// </summary>
        /// <param name="state">ReactionState of the container to heat.</param>
        /// <param name="powerWatts">Burner power (W). Must be positive.</param>
        /// <param name="maxSafeTempC">Hard temperature ceiling (°C).
        ///   Defaults to <see cref="HeatingService.DefaultMaxSafeTempC"/> (500 °C).</param>
        /// <param name="allowExplosion">When true, the safety cap is disabled.
        ///   Use only in controlled scenarios with explicit player warning.</param>
        public void BeginHeating(
            ReactionState state,
            float powerWatts,
            float maxSafeTempC  = HeatingService.DefaultMaxSafeTempC,
            bool allowExplosion = false)
        {
            if (state == null)
            {
                Debug.LogWarning("[HeatingController] BeginHeating called with null state.");
                return;
            }

            if (powerWatts <= 0f)
            {
                Debug.LogWarning("[HeatingController] powerWatts must be > 0.");
                return;
            }

            // Cancel any running session silently before starting new one
            _active = false;

            _state          = state;
            _powerWatts     = powerWatts;
            _maxSafeTempC   = maxSafeTempC;
            _allowExplosion = allowExplosion;
            _totalEnergyKJ  = 0f;
            _active         = true;

            EventBus.Publish(new HeatingStartedEvent { PowerWatts = _powerWatts });
        }

        /// <summary>
        /// Adjust burner power mid-session without restarting it.
        /// </summary>
        public void SetPower(float powerWatts)
        {
            _powerWatts = Mathf.Max(0f, powerWatts);
        }

        /// <summary>
        /// Turn the burner off and publish <see cref="HeatingStoppedEvent"/>.
        /// </summary>
        public void StopHeating()
        {
            if (!_active) return;
            PublishStopped();
            ClearSession();
        }

        // ════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ════════════════════════════════════════════════════════════════

        private void Update()
        {
            if (!_active) return;

            // Sync to the latest live state from the active stepper if available.
            // This ensures that reactions progressing in parallel keep consistent state.
            SimulationStepper stepper = GetActiveStepper();
            if (stepper != null && stepper.State != null)
                _state = stepper.State;

            if (_state == null)
            {
                Debug.LogWarning("[HeatingController] State lost — stopping heater.");
                StopHeating();
                return;
            }

            float dt = Time.deltaTime;

            HeatingStepResult result = HeatingService.Step(
                _state, _powerWatts, dt, _maxSafeTempC, _allowExplosion);

            // ── Apply heat ────────────────────────────────────────────────
            if (stepper != null && stepper.IsRunning)
            {
                // Route through stepper — enforces its own clamp + pressure model
                stepper.AddExternalHeat(result.HeatAppliedKJ);
            }
            else if (result.HeatAppliedKJ > 0f)
            {
                // No active simulation — write directly, apply safety clamp
                float dT = result.DeltaTempC;
                float maxT = _allowExplosion ? 2000f : _maxSafeTempC;
                _state.TemperatureC = Mathf.Clamp(_state.TemperatureC + dT, -200f, maxT);
            }

            _totalEnergyKJ += result.HeatAppliedKJ;

            EventBus.Publish(new HeatingTickEvent
            {
                PowerWatts    = _powerWatts,
                DeltaTempC    = result.DeltaTempC,
                CurrentTempC  = _state.TemperatureC,
                TotalEnergyKJ = _totalEnergyKJ,
                IsBoiling     = result.IsBoiling
            });

            // Auto-stop once the safety cap is hit to prevent spamming the event
            if (result.HitSafetyCap && !_allowExplosion)
            {
                Debug.Log($"[HeatingController] Safety cap reached at {_maxSafeTempC:F1} °C — stopping heater.");
                StopHeating();
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  INTERNAL HELPERS
        // ════════════════════════════════════════════════════════════════

        private SimulationStepper GetActiveStepper()
        {
            if (_reactionController == null) return null;
            var s = _reactionController.ActiveStepper;
            return (s != null && s.IsRunning) ? s : null;
        }

        private void PublishStopped()
        {
            EventBus.Publish(new HeatingStoppedEvent
            {
                TotalEnergyKJ = _totalEnergyKJ,
                FinalTempC    = _state != null ? _state.TemperatureC : 0f
            });
        }

        private void ClearSession()
        {
            _active = false;
            _state  = null;
        }

        protected override void OnTeardown()
        {
            if (_active)
                StopHeating();
        }
    }
}
