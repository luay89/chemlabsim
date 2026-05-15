// ChemLabSim v3 — Liquid Transfer Controller
// MonoBehaviour that drives frame-by-frame liquid transfer between two
// containers by delegating to LiquidTransferService (pure physics).
//
// Usage:
//   1. Attach to any GameObject in the scene (or add via script).
//   2. Call BeginTransfer(source, target, flowRate, containerId1, containerId2)
//      from any input handler or automation script.
//   3. Call StopTransfer() or let it stop automatically when source empties.
//
// Events published:
//   LiquidTransferStartedEvent  — once when transfer begins
//   LiquidTransferTickEvent     — every Update() while transferring
//   LiquidTransferStoppedEvent  — once when transfer ends
//
// The controller does NOT own the ReactionState objects; the caller
// (e.g. ReactionController) retains ownership.  The controller only
// mutates them via LiquidTransferService.Step().

using UnityEngine;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Controllers
{
    public class LiquidTransferController : V3ControllerBase
    {
        // ── Transfer session state ────────────────────────────────────────
        private ReactionState _source;
        private ReactionState _target;
        private float         _flowRateLPerSec;
        private string        _sourceId;
        private string        _targetId;
        private bool          _active;
        private float         _totalTransferred;

        // ── Public state ─────────────────────────────────────────────────
        /// <summary>True while a transfer session is in progress.</summary>
        public bool IsTransferring => _active;

        /// <summary>Cumulative volume transferred in the current session (L).</summary>
        public float TotalTransferredLiters => _totalTransferred;

        // ════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Start a new pour session.  Calling this while already active
        /// replaces the current session immediately.
        /// </summary>
        /// <param name="source">ReactionState of the container being poured from.</param>
        /// <param name="target">ReactionState of the container receiving liquid.</param>
        /// <param name="flowRateLPerSec">Desired pour rate in L/s (clamped > 0).</param>
        /// <param name="sourceContainerId">Scene identifier for event payloads.</param>
        /// <param name="targetContainerId">Scene identifier for event payloads.</param>
        public void BeginTransfer(
            ReactionState source,
            ReactionState target,
            float flowRateLPerSec,
            string sourceContainerId = "source",
            string targetContainerId = "target")
        {
            if (source == null || target == null)
            {
                Debug.LogWarning("[LiquidTransferController] BeginTransfer called with null container.");
                return;
            }

            if (flowRateLPerSec <= 0f)
            {
                Debug.LogWarning("[LiquidTransferController] flowRateLPerSec must be positive.");
                return;
            }

            // Cancel any running session silently (no StoppedEvent for replaced sessions)
            _active = false;

            _source           = source;
            _target           = target;
            _flowRateLPerSec  = flowRateLPerSec;
            _sourceId         = sourceContainerId ?? "source";
            _targetId         = targetContainerId ?? "target";
            _totalTransferred = 0f;
            _active           = true;

            EventBus.Publish(new LiquidTransferStartedEvent
            {
                SourceContainerId = _sourceId,
                TargetContainerId = _targetId,
                FlowRateLPerSec   = _flowRateLPerSec
            });
        }

        /// <summary>
        /// Stop the current pour session voluntarily.
        /// Publishes <see cref="LiquidTransferStoppedEvent"/>.
        /// </summary>
        public void StopTransfer()
        {
            if (!_active) return;
            PublishStopped(sourceExhausted: false);
            ClearSession();
        }

        // ════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ════════════════════════════════════════════════════════════════

        private void Update()
        {
            if (!_active) return;

            float dt = Time.deltaTime;

            LiquidTransferResult result = LiquidTransferService.Step(
                _source, _target, _flowRateLPerSec, dt);

            // Skip/error — stop gracefully
            if (result.SkipReason != null && result.TransferredVolumeLiters <= 0f)
            {
                bool exhausted = result.SourceExhausted;
                PublishStopped(exhausted);
                ClearSession();
                return;
            }

            _totalTransferred += result.TransferredVolumeLiters;

            EventBus.Publish(new LiquidTransferTickEvent
            {
                SourceContainerId    = _sourceId,
                TargetContainerId    = _targetId,
                DeltaVolumeLiters    = result.TransferredVolumeLiters,
                TotalTransferredLiters = _totalTransferred
            });

            // Source ran out this frame
            if (result.SourceExhausted)
            {
                PublishStopped(sourceExhausted: true);
                ClearSession();
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  INTERNAL HELPERS
        // ════════════════════════════════════════════════════════════════

        private void PublishStopped(bool sourceExhausted)
        {
            EventBus.Publish(new LiquidTransferStoppedEvent
            {
                SourceContainerId      = _sourceId,
                TargetContainerId      = _targetId,
                TotalTransferredLiters = _totalTransferred,
                SourceExhausted        = sourceExhausted
            });
        }

        private void ClearSession()
        {
            _active  = false;
            _source  = null;
            _target  = null;
        }

        protected override void OnTeardown()
        {
            // Ensure clean stop if the GameObject is destroyed mid-pour
            if (_active)
                StopTransfer();
        }
    }
}
