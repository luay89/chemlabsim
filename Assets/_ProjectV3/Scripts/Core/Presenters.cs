using ChemLabSimV3.Domain.Events;
using ChemLabSimV3.Infrastructure.Logging;

namespace ChemLabSimV3.Application.Implementation.Presenters
{
    /// <summary>Minimal reaction presenter.</summary>
    public class ReactionPresenter
    {
        private readonly object _useCase;
        private readonly object _view;
        private readonly IDomainEventBus _eventBus;
        private readonly UnityLogger _logger;

        public ReactionPresenter(object useCase, object view, IDomainEventBus eventBus, UnityLogger logger)
        {
            _useCase = useCase;
            _view = view;
            _eventBus = eventBus;
            _logger = logger;
        }

        public void OnReactionEvaluated(object output)
        {
            _logger?.Log("[ReactionPresenter] Reaction evaluated");
        }
    }

    /// <summary>Minimal progress presenter.</summary>
    public class ProgressPresenter
    {
        private readonly object _view;
        private readonly IDomainEventBus _eventBus;
        private readonly UnityLogger _logger;

        public ProgressPresenter(object view, IDomainEventBus eventBus, UnityLogger logger)
        {
            _view = view;
            _eventBus = eventBus;
            _logger = logger;
        }
    }
}
