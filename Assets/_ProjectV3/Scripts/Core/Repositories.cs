using System.Collections.Generic;
using ChemLabSimV3.Infrastructure.Logging;

namespace ChemLabSimV3.Data.Repositories
{
    /// <summary>Minimal achievement repository.</summary>
    public class AchievementRepository
    {
        private readonly UnityLogger _logger;

        public AchievementRepository(UnityLogger logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            _logger?.Log("[AchievementRepository] Initialized");
        }

        public IEnumerable<object> GetAll()
        {
            return new List<object>();
        }
    }

    /// <summary>Minimal quiz repository.</summary>
    public class QuizRepository
    {
        private readonly UnityLogger _logger;

        public QuizRepository(UnityLogger logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            _logger?.Log("[QuizRepository] Initialized");
        }

        public IEnumerable<object> GetAll()
        {
            return new List<object>();
        }
    }

    /// <summary>Minimal challenge repository.</summary>
    public class ChallengeRepository
    {
        private readonly UnityLogger _logger;

        public ChallengeRepository(UnityLogger logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            _logger?.Log("[ChallengeRepository] Initialized");
        }

        public IEnumerable<object> GetAll()
        {
            return new List<object>();
        }
    }

    /// <summary>Minimal lesson repository.</summary>
    public class LessonRepository
    {
        private readonly UnityLogger _logger;

        public LessonRepository(UnityLogger logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            _logger?.Log("[LessonRepository] Initialized");
        }

        public IEnumerable<object> GetAll()
        {
            return new List<object>();
        }
    }
}
