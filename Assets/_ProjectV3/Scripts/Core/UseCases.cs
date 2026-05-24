using ChemLabSimV3.Data.Repositories;
using ChemLabSimV3.Infrastructure.Logging;
using ChemLabSimV3.Engine;

namespace ChemLabSimV3.Application.Implementation.UseCases
{
    /// <summary>Minimal evaluate reaction use case.</summary>
    public class EvaluateReactionUseCase
    {
        private readonly object _reactionRepo;
        private readonly object _reactionEngine;
        private readonly UnityLogger _logger;

        public EvaluateReactionUseCase(object reactionRepo, object reactionEngine, UnityLogger logger)
        {
            _reactionRepo = reactionRepo;
            _reactionEngine = reactionEngine;
            _logger = logger;
        }
    }

    /// <summary>Minimal apply conditions use case.</summary>
    public class ApplyConditionsUseCase
    {
        private readonly object _pipeline;
        private readonly UnityLogger _logger;

        public ApplyConditionsUseCase(object pipeline, UnityLogger logger)
        {
            _pipeline = pipeline;
            _logger = logger;
        }
    }

    /// <summary>Minimal generate quiz use case.</summary>
    public class GenerateQuizUseCase
    {
        private readonly QuizRepository _quizRepo;
        private readonly UnityLogger _logger;

        public GenerateQuizUseCase(QuizRepository quizRepo, UnityLogger logger)
        {
            _quizRepo = quizRepo;
            _logger = logger;
        }
    }

    /// <summary>Minimal save progress use case.</summary>
    public class SaveProgressUseCase
    {
        private readonly object _saveService;
        private readonly UnityLogger _logger;

        public SaveProgressUseCase(object saveService, UnityLogger logger)
        {
            _saveService = saveService;
            _logger = logger;
        }
    }

    /// <summary>Minimal load progress use case.</summary>
    public class LoadProgressUseCase
    {
        private readonly object _saveService;
        private readonly UnityLogger _logger;

        public LoadProgressUseCase(object saveService, UnityLogger logger)
        {
            _saveService = saveService;
            _logger = logger;
        }
    }

    /// <summary>Minimal evaluate achievements use case.</summary>
    public class EvaluateAchievementsUseCase
    {
        private readonly AchievementRepository _achievementRepo;
        private readonly UnityLogger _logger;

        public EvaluateAchievementsUseCase(AchievementRepository achievementRepo, UnityLogger logger)
        {
            _achievementRepo = achievementRepo;
            _logger = logger;
        }
    }
}
