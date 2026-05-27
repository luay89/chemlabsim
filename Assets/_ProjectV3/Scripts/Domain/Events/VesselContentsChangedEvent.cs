// ChemLabSim v3 — Vessel Contents Changed Event
// Pure domain event fired by TimeBasedPourUseCase every frame mass is
// transferred. UI counters, fluid-height shaders, and notebook listeners
// subscribe to this to react to live pouring.
//
// Pure C# — no UnityEngine dependency.

namespace ChemLabSimV3.Domain.Events
{
    /// <summary>
    /// Published when a vessel's contents change (typically as part of a
    /// continuous pour). Both source and target vessels emit one event per
    /// transfer tick so subscribers can update each independently.
    /// </summary>
    public sealed class VesselContentsChangedEvent : DomainEventBase
    {
        public override string EventType => "VesselContentsChanged";

        /// <summary>Identifier of the vessel whose contents changed.</summary>
        public string VesselId { get; }

        /// <summary>Reagent formula / id currently dominating the vessel (may be empty).</summary>
        public string ReagentId { get; }

        /// <summary>Volume currently in the vessel, in millilitres.</summary>
        public float CurrentVolumeMl { get; }

        /// <summary>Volume delta applied this tick (positive = added, negative = drained), in millilitres.</summary>
        public float DeltaVolumeMl { get; }

        /// <summary>Original capacity of the vessel, in millilitres (0 if unknown).</summary>
        public float CapacityMl { get; }

        /// <summary>
        /// True when this event was published as part of an active pour.
        /// Subscribers can use it to differentiate continuous updates from one-shot edits.
        /// </summary>
        public bool IsPouring { get; }

        public VesselContentsChangedEvent(
            string vesselId,
            string reagentId,
            float currentVolumeMl,
            float deltaVolumeMl,
            float capacityMl,
            bool isPouring)
        {
            VesselId = vesselId ?? string.Empty;
            ReagentId = reagentId ?? string.Empty;
            CurrentVolumeMl = currentVolumeMl;
            DeltaVolumeMl = deltaVolumeMl;
            CapacityMl = capacityMl;
            IsPouring = isPouring;
        }
    }
}
