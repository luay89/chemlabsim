// ChemLabSim v3 — Stirring Controller
// MonoBehaviour that drives real-time stirring effects by delegating to
// StirringService (pure physics).
//
// Usage:
//   1. Attach to any GameObject (or add via script).
//   2. Wire _reactionController in the Inspector.
//   3. Call SetIntensity(0..1) from any input handler (slider, circular motion, etc.)
//   4. Call Stop() to turn stirring off.
//
// Physics contract:
//   Stirring maps stirIntensity → grindingFactor on SimulationStepper each frame.
//   Because PhaseInteractionModel.GetContactFactor scales both k_f and k_r equally,
//   the equilibrium position (Keq) is preserved — only the rate of reaching it changes.
//
// Events published each frame that intensity changes:
//   StirringChangedEvent { Intensity, IsActive, RateMultiplier, DissolutionMultiplier }
//
// Visual outputs (via StirringChangedEvent + SimulationBridge→ChemFxState):
//   VortexIntensity  — drives particle vortex / whirlpool FX
//   WobbleAmplitude  — drives ContainerFillController liquid wobble continuously

using UnityEngine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    public class StirringController : V3ControllerBase
    {
        // ── Inspector ─────────────────────────────────────────────────────
        [SerializeField]
        [Tooltip("Reference to ReactionController — used to access the active " +
                 "SimulationStepper so grinding factor is applied in-simulation.")]
        private ReactionController _reactionController;

        /// <summary>How quickly intensity changes propagate (lerp speed per second).
        /// Higher = more responsive; lower = smoother ramp.</summary>
        [SerializeField] [Range(1f, 20f)] private float _rampSpeed = 6f;

        // ── State ─────────────────────────────────────────────────────────
        private float _targetIntensity;
        private float _currentIntensity;
        private float _lastPublishedIntensity = -1f; // tracks last event to avoid spam

        // ── Public state ──────────────────────────────────────────────────
        /// <summary>Current smoothed stir intensity [0–1].</summary>
        public float CurrentIntensity => _currentIntensity;

        /// <summary>True while any stirring is active.</summary>
        public bool IsStirring => _currentIntensity > 0.005f;

        // ════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Set a new target stir intensity (0 = off, 1 = maximum).
        /// The actual intensity smoothly ramps to the target at <see cref="_rampSpeed"/>.
        /// </summary>
        public void SetIntensity(float intensity)
        {
            _targetIntensity = Mathf.Clamp01(intensity);
        }

        /// <summary>Stop stirring (target intensity → 0).</summary>
        public void Stop()
        {
            _targetIntensity = 0f;
        }

        // ════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ════════════════════════════════════════════════════════════════

        private void Update()
        {
            float dt = Time.deltaTime;

            // ── Ramp current toward target ────────────────────────────────
            float t = Mathf.Clamp01(dt * _rampSpeed);
            _currentIntensity = Mathf.Lerp(_currentIntensity, _targetIntensity, t);

            // Snap to zero below threshold to avoid lingering near-zero values
            if (_currentIntensity < 0.005f && _targetIntensity <= 0f)
                _currentIntensity = 0f;

            // ── Evaluate physics ─────────────────────────────────────────
            StirringStepResult result = StirringService.Evaluate(_currentIntensity);

            // ── Apply grinding factor to live simulation ──────────────────
            SimulationStepper stepper = GetActiveStepper();
            if (stepper != null)
                stepper.SetGrindingFactor(result.GrindingFactor);

            // ── Publish event when intensity meaningfully changes ─────────
            float delta = Mathf.Abs(_currentIntensity - _lastPublishedIntensity);
            if (delta > 0.01f || (IsStirring != (_lastPublishedIntensity > 0.005f)))
            {
                EventBus.Publish(new StirringChangedEvent
                {
                    Intensity             = _currentIntensity,
                    IsActive              = IsStirring,
                    RateMultiplier        = result.RateMultiplier,
                    DissolutionMultiplier = result.DissolutionMultiplier
                });
                _lastPublishedIntensity = _currentIntensity;
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

        protected override void OnTeardown()
        {
            // Restore zero grinding factor when destroyed
            SimulationStepper stepper = GetActiveStepper();
            if (stepper != null)
                stepper.SetGrindingFactor(0f);
        }
    }
}
