// ChemLabSim v3 — Challenge Controller
// Per-level challenges. Resets on LevelUp, publishes ChallengeAssignedEvent + ChallengeCompletedEvent.
// No UI, no PlayerPrefs — events only.
//
// Migration source: LabController.ChallengeDefinition[], UpdateChallengeProgress(),
//   GetChallengeForCurrentLevel(), challengeCompleted, ChallengeRewardPoints.

using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;
using ChemLabSimV3.Services;

namespace ChemLabSimV3.Controllers
{
    public class ChallengeController : V3ControllerBase
    {
        // -- Challenge Definitions -----------------------------
        private struct ChallengeDefinition
        {
            public string Title;
            public int Level;
        }

        private static readonly ChallengeDefinition[] ChallengeDefinitions =
        {
            new ChallengeDefinition { Title = "Complete a successful reaction without catalyst", Level = 1 },
            new ChallengeDefinition { Title = "Use the correct medium in one attempt", Level = 2 },
            new ChallengeDefinition { Title = "Reach a strong contact factor (>=1.2)", Level = 3 },
            new ChallengeDefinition { Title = "Complete two successful reactions in a row", Level = 4 }
        };

        private const int ChallengeRewardPoints = 10;

        // -- State ---------------------------------------------
        private ChallengeState currentChallenge;
        private int currentLevel;
        private int consecutiveStreak;

        // -- Read-only accessor --------------------------------
        public ChallengeState CurrentChallenge => currentChallenge;

        // -- Lifecycle -----------------------------------------

        protected override void OnInitialize()
        {
            consecutiveStreak = 0;
            var save = ServiceLocator.Get<SaveService>();
            var data = (save != null && save.HasSave()) ? save.GetSaveData() : null;
            currentLevel = data != null ? Mathf.Clamp(data.challenge.level, 1, 4) : 1;
            AssignChallengeForLevel(currentLevel);
            if (data != null && data.challenge.completed)
            {
                currentChallenge.Completed = true;
                EventBus.Publish(new ChallengeCompletedEvent
                {
                    ChallengeId = currentChallenge.Id,
                    RewardPoints = ChallengeRewardPoints
                });
            }

            EventBus.Subscribe<ReactionEvaluatedEvent>(HandleReactionEvaluated);
            EventBus.Subscribe<LevelUpEvent>(HandleLevelUp);
            Debug.Log("[ChallengeController] Initialized.");
        }

        protected override void OnTeardown()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(HandleReactionEvaluated);
            EventBus.Unsubscribe<LevelUpEvent>(HandleLevelUp);
        }

        // -- Event Handlers ------------------------------------

        private void HandleReactionEvaluated(ReactionEvaluatedEvent evt)
        {
            var result = evt.Result;

            // Track consecutive streak for level 4 challenge
            if (DidReact(result))
                consecutiveStreak++;
            else
                consecutiveStreak = 0;

            if (!DidReact(result))
                return;

            if (currentChallenge.Completed)
                return;

            bool passed = false;

            switch (currentLevel)
            {
                case 1: // Success without catalyst
                    passed = !result.CatalystApplied;
                    break;
                case 2: // Correct medium (no mismatch)
                    passed = !result.MediumMismatch;
                    break;
                case 3: // Strong contact factor
                    passed = result.ContactFactor >= 1.2f;
                    break;
                case 4: // Two in a row (consecutive streak >= 2)
                    passed = consecutiveStreak >= 2;
                    break;
                default:
                    passed = true;
                    break;
            }

            if (passed)
            {
                currentChallenge.Completed = true;
                Debug.Log($"[ChallengeController] Challenge completed — {currentChallenge.Title}");
                EventBus.Publish(new ChallengeCompletedEvent
                {
                    ChallengeId = currentChallenge.Id,
                    RewardPoints = ChallengeRewardPoints
                });
            }
        }

        private void HandleLevelUp(LevelUpEvent evt)
        {
            currentLevel = evt.NewLevel;
            consecutiveStreak = 0;
            AssignChallengeForLevel(currentLevel);
        }

        // -- Helpers -------------------------------------------

        private void AssignChallengeForLevel(int level)
        {
            string title = GetChallengeTitleForLevel(level);
            currentChallenge = new ChallengeState
            {
                Id = $"challenge_level_{level}",
                Title = title,
                Level = level,
                Completed = false,
                RewardPoints = ChallengeRewardPoints
            };

            Debug.Log($"[ChallengeController] Challenge assigned — {title} (level {level})");
            EventBus.Publish(new ChallengeAssignedEvent
            {
                ChallengeId = currentChallenge.Id,
                Title = title,
                Level = level
            });
        }

        private static string GetChallengeTitleForLevel(int level)
        {
            for (int i = 0; i < ChallengeDefinitions.Length; i++)
            {
                if (ChallengeDefinitions[i].Level == level)
                    return ChallengeDefinitions[i].Title;
            }

            // Fallback: last challenge
            return ChallengeDefinitions.Length > 0
                ? ChallengeDefinitions[ChallengeDefinitions.Length - 1].Title
                : "Complete a successful reaction";
        }

        private static bool DidReact(ReactionEvaluationResult eval)
        {
            return eval.Status == ReactionStatus.Success || eval.Status == ReactionStatus.Partial;
        }
    }
}
