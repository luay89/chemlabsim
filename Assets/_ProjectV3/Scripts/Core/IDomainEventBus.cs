// ChemLabSim v3 — Domain Event Bus Interface
// Pure event pub/sub contract — implementation-agnostic.

using System;

namespace ChemLabSimV3.Domain.Events
{
    /// <summary>
    /// Pure domain event bus interface.
    /// Decouples publishers from subscribers.
    /// All communication through this interface only.
    /// </summary>
    public interface IDomainEventBus
    {
        /// <summary>Subscribe to a domain event type.</summary>
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;

        /// <summary>Unsubscribe from a domain event type.</summary>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;

        /// <summary>Publish a domain event to all subscribers.</summary>
        void Publish<TEvent>(TEvent evt) where TEvent : IDomainEvent;

        /// <summary>Get current subscription count for a specific event type (debug only).</summary>
        int GetSubscriberCount<TEvent>() where TEvent : IDomainEvent;

        /// <summary>Clear all subscriptions (use with caution — typically for testing).</summary>
        void Clear();
    }
}
