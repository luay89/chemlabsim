using ChemLabSimV3.Domain.Events;

namespace ChemLabSimV3.Production
{
    /// <summary>Minimal reaction evaluated domain event.</summary>
    public class ReactionEvaluatedDomainEvent : DomainEventBase
    {
        public override string EventType => "ReactionEvaluated";
    }
}
