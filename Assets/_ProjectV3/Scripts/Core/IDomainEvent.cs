// ChemLabSim v3 — Domain Event Interface
// Marker interface for all domain events.
// Every event in the system must implement this.

namespace ChemLabSimV3.Domain.Events
{
    /// <summary>
    /// Marker interface for all domain events.
    /// Events are immutable value objects that represent something that happened in the domain.
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>Unique identifier for this event instance.</summary>
        string EventId { get; }

        /// <summary>When this event occurred (UTC milliseconds).</summary>
        long TimestampMs { get; }

        /// <summary>Type of event for routing/filtering.</summary>
        string EventType { get; }
    }

    /// <summary>Base class for implementing IDomainEvent with common fields.</summary>
    public abstract class DomainEventBase : IDomainEvent
    {
        public string EventId { get; }
        public long TimestampMs { get; }
        public abstract string EventType { get; }

        protected DomainEventBase()
        {
            EventId = System.Guid.NewGuid().ToString();
            TimestampMs = System.DateTime.UtcNow.Ticks / 10000;
        }
    }
}
