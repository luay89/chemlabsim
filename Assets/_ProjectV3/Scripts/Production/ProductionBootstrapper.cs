// ChemLabSim v3 — Production Bootstrap
// Initializes all layers: Infrastructure, Application, Presenters.
// Sets up dependency injection using ServiceLocator.
// Called during game startup (replaces manual wiring).

using UnityEngine;
using ChemLabSimV3.Application.Implementation.Presenters;
using ChemLabSimV3.Application.Implementation.UseCases;
using ChemLabSimV3.Core;
using ChemLabSimV3.Data;
using ChemLabSimV3.Data.Repositories;
using ChemLabSimV3.Domain.Events;
using ChemLabSimV3.Engine;
using ChemLabSimV3.Infrastructure.Audio;
using ChemLabSimV3.Infrastructure.EventBus;
using ChemLabSimV3.Infrastructure.Logging;
using ChemLabSimV3.Infrastructure.Persistence;
using ChemLabSimV3.Views;

namespace ChemLabSimV3.Production
{
    /// <summary>
    /// Production bootstrap — initializes the complete system.
    /// Follows the dependency injection pattern:
    /// 1. Create services (logging, persistence, etc.)
    /// 2. Create repositories
    /// 3. Create use cases
    /// 4. Create presenters
    /// 5. Wire event subscriptions
    /// </summary>
    public class ProductionBootstrapper
    {
        private readonly UnityLogger _logger;
        private readonly DomainEventBus _eventBus;
        private readonly ReactionDatabaseAdapter _reactionRepo;
        private readonly AchievementRepository _achievementRepo;
        private readonly QuizRepository _quizRepo;
        private readonly PlayerPrefsSaveService _saveService;

        public ProductionBootstrapper()
        {
            // 1. Core infrastructure services
            _logger = new UnityLogger { DebugMode = false };
            _eventBus = new DomainEventBus(_logger);

            // 2. Repositories (read-only data access)
            _reactionRepo = InitializeReactionRepository();
            _achievementRepo = new AchievementRepository(_logger);
            _achievementRepo.Initialize();
            _quizRepo = new QuizRepository(_logger);
            _quizRepo.Initialize();

            // 3. Services (cross-cutting concerns)
            _saveService = new PlayerPrefsSaveService(_logger);
            var audioService = new UnityAudioService(_logger);

            // 4. Register in ServiceLocator for later access
            ServiceLocator.Register(_logger);
            ServiceLocator.Register(_eventBus);
            ServiceLocator.Register(_reactionRepo);
            ServiceLocator.Register(_achievementRepo);
            ServiceLocator.Register(_quizRepo);
            ServiceLocator.Register(_saveService);
            ServiceLocator.Register(audioService);

            _logger.Log("[ProductionBootstrapper] Infrastructure initialized");
        }

        /// <summary>
        /// Bootstrap the application layer (use cases).
        /// </summary>
        public void BootstrapApplicationLayer()
        {
            var reactionEngine = AppManager.Instance?.ReactionDatabase != null
                ? new ReactionEngine(AppManager.Instance.ReactionDatabase)
                : null;

            if (reactionEngine == null)
            {
                _logger.LogError("[ProductionBootstrapper] ReactionEngine initialization failed");
                return;
            }

            // Create use cases
            var evaluateReactionUseCase = new EvaluateReactionUseCase(_reactionRepo, reactionEngine, _logger);
            var conditionPipeline = ChemLabSimV3.Engine.ConditionPipeline.CreateDefault();
            var applyConditionsUseCase = new ApplyConditionsUseCase(conditionPipeline, _logger);
            var generateQuizUseCase = new GenerateQuizUseCase(_quizRepo, _logger);
            var saveProgressUseCase = new SaveProgressUseCase(_saveService, _logger);
            var loadProgressUseCase = new LoadProgressUseCase(_saveService, _logger);
            var evaluateAchievementsUseCase = new EvaluateAchievementsUseCase(_achievementRepo, _logger);

            // Register use cases
            ServiceLocator.Register(evaluateReactionUseCase);
            ServiceLocator.Register(applyConditionsUseCase);
            ServiceLocator.Register(generateQuizUseCase);
            ServiceLocator.Register(saveProgressUseCase);
            ServiceLocator.Register(loadProgressUseCase);
            ServiceLocator.Register(evaluateAchievementsUseCase);
            ServiceLocator.Register(conditionPipeline);
            ServiceLocator.Register(reactionEngine);

            _logger.Log("[ProductionBootstrapper] Application layer (use cases) initialized");
        }

        /// <summary>
        /// Bootstrap the presentation layer (presenters + views).
        /// This should be called after views are instantiated in the scene.
        /// </summary>
        public void BootstrapPresentationLayer(
            ReactionResultView reactionResultView,
            ProgressView progressView)
        {
            var evaluateUseCase = ServiceLocator.Get<EvaluateReactionUseCase>();
            if (evaluateUseCase == null)
            {
                _logger.LogError("[ProductionBootstrapper] Evaluate use case not registered");
                return;
            }

            // Create presenters
            var reactionPresenter = new ReactionPresenter(
                evaluateUseCase,
                reactionResultView,
                _eventBus,
                _logger);

            var progressPresenter = new ProgressPresenter(
                progressView,
                _eventBus,
                _logger);

            // Wire event subscriptions
            _eventBus.Subscribe<ReactionEvaluatedDomainEvent>(reactionPresenter.OnReactionEvaluated);

            // Register presenters
            ServiceLocator.Register(reactionPresenter);
            ServiceLocator.Register(progressPresenter);

            _logger.Log("[ProductionBootstrapper] Presentation layer (presenters) initialized");
        }

        /// <summary>
        /// Initialize reaction repository from AppManager database.
        /// </summary>
        private ReactionDatabaseAdapter InitializeReactionRepository()
        {
            var db = AppManager.Instance?.ReactionDatabase;
            if (db == null || db.reactions == null)
            {
                _logger.LogError("[ProductionBootstrapper] Reaction database not available");
                return null;
            }

            return new ReactionDatabaseAdapter(db, _logger);
        }
    }

    /// <summary>
    /// Adapter for the existing ReactionDB to expose repository-like queries.
    /// This bridges the legacy system with the new architecture.
    /// </summary>
    public class ReactionDatabaseAdapter
    {
        private readonly ReactionDB _db;
        private readonly UnityLogger _logger;

        public ReactionDatabaseAdapter(ReactionDB db, UnityLogger logger)
        {
            _db = db ?? throw new System.ArgumentNullException(nameof(db));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        public ReactionEntry GetById(string reactionId)
        {
            if (string.IsNullOrEmpty(reactionId) || _db.reactions == null)
                return null;

            foreach (var reaction in _db.reactions)
            {
                if (reaction.id == reactionId)
                    return reaction;
            }

            return null;
        }

        public System.Collections.Generic.IEnumerable<ReactionEntry> FindByReagents(System.Collections.Generic.IEnumerable<string> reagentNames)
        {
            var result = new System.Collections.Generic.List<ReactionEntry>();
            if (_db.reactions == null)
                return result;

            var reagentList = new System.Collections.Generic.List<string>(reagentNames);
            foreach (var reaction in _db.reactions)
            {
                string reagentA = reaction.GetReactantA();
                string reagentB = reaction.GetReactantB();

                if (!string.IsNullOrWhiteSpace(reagentA) && !string.IsNullOrWhiteSpace(reagentB))
                {
                    if ((reagentList.Contains(reagentA) && reagentList.Contains(reagentB)) ||
                        (reagentList.Contains(reagentB) && reagentList.Contains(reagentA)))
                    {
                        result.Add(reaction);
                    }
                }
            }

            return result;
        }

        public System.Collections.Generic.IEnumerable<ReactionEntry> GetAll()
        {
            return _db.reactions ?? new System.Collections.Generic.List<ReactionEntry>();
        }

        public int Count => _db.reactions?.Count ?? 0;
    }
}
