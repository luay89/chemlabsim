// ChemLabSim v3 — Time-Based Pour Use Case
// Application-layer implementation of ITimeBasedPourUseCase. Performs
// per-frame mass transfer between two registered vessels and (optionally)
// asks the ChemistryEngine to recompute the resulting mixture so colour,
// temperature and reaction state stay live while pouring is in progress.
//
// Layering rules:
//   • Pure C# — no UnityEngine, no MonoBehaviour, no Mathf, no Debug.
//   • Depends only on Domain (events, ChemistryEngine contract) and the
//     Presentation interface it implements.
//   • Thread-safety: a single coarse lock guards the vessel registry so
//     pour ticks coming from Unity's main thread interleave safely with
//     RegisterVessel calls from setup code.

using System;
using System.Collections.Generic;
using ChemLabSimV3.Data;
using ChemLabSimV3.Domain.Events;
using ChemLabSimV3.Engine.Chemistry;
using ChemLabSimV3.Presentation.Presenters;

namespace ChemLabSimV3.Application.UseCases
{
    /// <summary>
    /// Tracks vessel volumes and transfers fluid between them on every tick.
    /// Register each vessel up-front with <see cref="RegisterVessel"/>; the
    /// presenter then drives <see cref="ExecutePour"/> from Update().
    /// </summary>
    public sealed class TimeBasedPourUseCase : ITimeBasedPourUseCase
    {
        /// <summary>Default pour rate in millilitres per second, used when a vessel does not specify one.</summary>
        public const float DefaultPourRateMlPerSecond = 50f;

        private readonly ChemistryEngine _chemistryEngine; // optional
        private readonly IDomainEventBus _eventBus;        // optional
        private readonly Dictionary<string, VesselSnapshot> _vessels;
        private readonly object _sync = new object();

        public TimeBasedPourUseCase(ChemistryEngine chemistryEngine, IDomainEventBus eventBus)
        {
            _chemistryEngine = chemistryEngine; // null is allowed — engine update step is skipped
            _eventBus = eventBus;               // null is allowed — events are dropped silently
            _vessels = new Dictionary<string, VesselSnapshot>(StringComparer.Ordinal);
        }

        // ── Vessel registry API ──────────────────────────────────────────────

        /// <summary>
        /// Register (or replace) a vessel snapshot. Call at scene setup for
        /// every source bottle and for the target container.
        /// </summary>
        /// <param name="vesselId">Stable id; must match the presenter / trigger zone ids.</param>
        /// <param name="reagentId">Reagent formula or empty for an initially-empty target.</param>
        /// <param name="initialVolumeMl">Starting volume in millilitres (negative values clamp to 0).</param>
        /// <param name="capacityMl">Maximum capacity in millilitres (0 = unbounded).</param>
        /// <param name="pourRateMlPerSecond">Pour rate when this vessel acts as the source (0 = use default).</param>
        public void RegisterVessel(
            string vesselId,
            string reagentId,
            float initialVolumeMl,
            float capacityMl,
            float pourRateMlPerSecond)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                throw new ArgumentException("vesselId must not be null or empty.", nameof(vesselId));

            var snapshot = new VesselSnapshot
            {
                VesselId = vesselId,
                ReagentId = reagentId ?? string.Empty,
                CurrentVolumeMl = initialVolumeMl > 0f ? initialVolumeMl : 0f,
                CapacityMl = capacityMl > 0f ? capacityMl : 0f,
                PourRateMlPerSecond = pourRateMlPerSecond > 0f ? pourRateMlPerSecond : DefaultPourRateMlPerSecond,
                AccumulatedReagents = new Dictionary<string, float>(StringComparer.Ordinal)
            };

            if (!string.IsNullOrEmpty(snapshot.ReagentId) && snapshot.CurrentVolumeMl > 0f)
                snapshot.AccumulatedReagents[snapshot.ReagentId] = snapshot.CurrentVolumeMl;

            lock (_sync)
            {
                _vessels[vesselId] = snapshot;
            }
        }

        /// <summary>Returns the current volume of <paramref name="vesselId"/> in millilitres, or 0 if unknown.</summary>
        public float GetVolumeMl(string vesselId)
        {
            if (string.IsNullOrEmpty(vesselId)) return 0f;
            lock (_sync)
            {
                return _vessels.TryGetValue(vesselId, out var v) ? v.CurrentVolumeMl : 0f;
            }
        }

        // ── ITimeBasedPourUseCase ────────────────────────────────────────────

        /// <summary>
        /// Transfer one tick of fluid from <paramref name="sourceVesselId"/> to
        /// <paramref name="targetContainerId"/>. Returns true while the source
        /// still has fluid remaining; false signals the presenter to stop.
        /// </summary>
        public bool ExecutePour(string sourceVesselId, string targetContainerId, float deltaSeconds)
        {
            // Input validation — pure C# guards, no Unity assertions.
            if (string.IsNullOrWhiteSpace(sourceVesselId)) return false;
            if (string.IsNullOrWhiteSpace(targetContainerId)) return false;
            if (deltaSeconds <= 0f) return true;          // nothing to do this frame, but pour is still active
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return true;

            VesselSnapshot sourceCopy;
            VesselSnapshot targetCopy;
            float addedMl;

            lock (_sync)
            {
                if (!_vessels.TryGetValue(sourceVesselId, out var source))
                    return false; // unknown source — nothing to pour
                if (!_vessels.TryGetValue(targetContainerId, out var target))
                    return false; // unknown target — nothing to receive

                if (source.CurrentVolumeMl <= 0f)
                    return false; // already empty

                // Δ = rate × dt, clamped by what the source has and what the target can hold.
                float requested = source.PourRateMlPerSecond * deltaSeconds;
                float availableInSource = source.CurrentVolumeMl;
                float spaceInTarget = target.CapacityMl > 0f
                    ? Math.Max(0f, target.CapacityMl - target.CurrentVolumeMl)
                    : float.MaxValue;

                addedMl = Math.Min(requested, Math.Min(availableInSource, spaceInTarget));
                if (addedMl <= 0f)
                    return source.CurrentVolumeMl > 0f; // can't transfer (target full) but source may still have fluid

                source.CurrentVolumeMl -= addedMl;
                target.CurrentVolumeMl += addedMl;

                AccumulateReagent(ref target, source.ReagentId, addedMl);

                _vessels[sourceVesselId] = source;
                _vessels[targetContainerId] = target;

                sourceCopy = source;
                targetCopy = target;
            }

            // Always create a MixRequest and process chemistry if engine is available
            ChemLabSimV3.Engine.Chemistry.ChemistryOutput? chemistryOutput = null;
            if (_chemistryEngine != null)
            {
                try
                {
                    var reagents = new List<string>(targetCopy.AccumulatedReagents?.Keys ?? new List<string>());
                    var mixRequest = new ChemLabSimV3.Data.MixRequest(
                        reagentNames: reagents,
                        medium: ChemLabSimV3.Data.ReactionMedium.Neutral,
                        temperature: 25f,
                        stirring: 0f,
                        grinding: 0f,
                        hasCatalyst: false);
                    chemistryOutput = _chemistryEngine.Process(mixRequest);
                }
                catch (Exception)
                {
                    // Swallow engine errors — a corrupt mixture must never break the pour loop.
                    chemistryOutput = null;
                }
            }

            // Publish events, now with chemistry output if available
            PublishContentsChanged(sourceCopy, -addedMl, isPouring: true, chemistryOutput);
            PublishContentsChanged(targetCopy, +addedMl, isPouring: true, chemistryOutput);

            return sourceCopy.CurrentVolumeMl > 0f;
        }

        // ── Internals ────────────────────────────────────────────────────────

        private static void AccumulateReagent(ref VesselSnapshot target, string reagentId, float addedMl)
        {
            if (string.IsNullOrEmpty(reagentId) || addedMl <= 0f) return;

            if (target.AccumulatedReagents.TryGetValue(reagentId, out float existing))
                target.AccumulatedReagents[reagentId] = existing + addedMl;
            else
                target.AccumulatedReagents[reagentId] = addedMl;

            // The dominant reagent (largest accumulated volume) becomes the
            // vessel's primary ReagentId — used by listeners for tint, label, etc.
            string dominant = target.ReagentId;
            float dominantVolume = -1f;
            foreach (var pair in target.AccumulatedReagents)
            {
                if (pair.Value > dominantVolume)
                {
                    dominantVolume = pair.Value;
                    dominant = pair.Key;
                }
            }
            target.ReagentId = dominant ?? string.Empty;
        }

        // No longer needed: chemistry is now always run in ExecutePour and output is propagated.

        private void PublishContentsChanged(VesselSnapshot v, float deltaMl, bool isPouring, ChemistryOutput chemistryOutput)
        {
            if (_eventBus == null) return;
            var evt = new VesselContentsChangedEvent(
                vesselId: v.VesselId,
                reagentId: v.ReagentId,
                currentVolumeMl: v.CurrentVolumeMl,
                deltaVolumeMl: deltaMl,
                capacityMl: v.CapacityMl,
                isPouring: isPouring,
                chemistryOutput: chemistryOutput);
            _eventBus.Publish(evt);
        }

        // ── Vessel snapshot (internal state) ─────────────────────────────────

        /// <summary>
        /// Mutable per-vessel state. Held by value inside the dictionary; the
        /// dictionary itself is the single source of truth.
        /// </summary>
        private struct VesselSnapshot
        {
            public string VesselId;
            public string ReagentId;
            public float CurrentVolumeMl;
            public float CapacityMl;
            public float PourRateMlPerSecond;
            public Dictionary<string, float> AccumulatedReagents;
        }
    }
}
