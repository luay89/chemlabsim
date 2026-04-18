// ChemLabSim v3 — Achievement Controller
// Tracks unlock conditions and publishes AchievementUnlockedEvent.
// No UI, no PlayerPrefs, no persistence — events only.
//
// Migration source: LabController.UpdateAchievements(), TryUnlockAchievement(),
//   achievement constants, unlockedAchievements HashSet.

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;
using ChemLabSimV3.Services;

namespace ChemLabSimV3.Controllers
{
    public class AchievementController : V3ControllerBase
    {
        // -- Achievement IDs -----------------------------------
        private const string AchievFirstReaction  = "First Successful Reaction";
        private const string AchievReachLevel2    = "Reach Level 2";
        private const string Achiev5Experiments   = "Complete 5 Experiments";
        private const string AchievFirstChallenge = "Complete First Challenge";
        private const string AchievScore100       = "Reach 100 Score";
        private const string AchievUseCatalyst    = "Use Catalyst Correctly";

        // -- State ---------------------------------------------
        private readonly HashSet<string> unlockedAchievements = new HashSet<string>();

        // -- Read-only accessor --------------------------------
        public IReadOnlyCollection<string> UnlockedAchievements => unlockedAchievements;
        public int TotalAchievements => 6;

        // -- Lifecycle -----------------------------------------

        protected override void OnInitialize()
        {
            unlockedAchievements.Clear();
            var save = ServiceLocator.Get<SaveService>();
            if (save != null && save.HasSave())
            {
                foreach (string a in save.GetSaveData().achievements.unlockedIds)
                    unlockedAchievements.Add(a);
            }
            EventBus.Subscribe<ReactionEvaluatedEvent>(HandleReactionEvaluated);
            EventBus.Subscribe<ProgressUpdatedEvent>(HandleProgressUpdated);
            EventBus.Subscribe<ChallengeCompletedEvent>(HandleChallengeCompleted);
            Debug.Log("[AchievementController] Initialized.");
        }

        protected override void OnTeardown()
        {
            EventBus.Unsubscribe<ReactionEvaluatedEvent>(HandleReactionEvaluated);
            EventBus.Unsubscribe<ProgressUpdatedEvent>(HandleProgressUpdated);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(HandleChallengeCompleted);
        }

        // -- Event Handlers ------------------------------------

        private void HandleReactionEvaluated(ReactionEvaluatedEvent evt)
        {
            var result = evt.Result;

            if (!DidReact(result))
                return;

            // First Successful Reaction
            if (!HasAchievement(AchievFirstReaction))
            {
                TryUnlockAchievement(AchievFirstReaction);
                return;
            }

            // Use Catalyst Correctly
            if (result.CatalystApplied && !HasAchievement(AchievUseCatalyst))
            {
                TryUnlockAchievement(AchievUseCatalyst);
                return;
            }
        }

        private void HandleProgressUpdated(ProgressUpdatedEvent evt)
        {
            var state = evt.State;

            // Reach Level 2
            if (state.CurrentLevel >= 2 && !HasAchievement(AchievReachLevel2))
            {
                TryUnlockAchievement(AchievReachLevel2);
                return;
            }

            // Complete 5 Experiments
            if (state.TotalExperiments >= 5 && !HasAchievement(Achiev5Experiments))
            {
                TryUnlockAchievement(Achiev5Experiments);
                return;
            }

            // Reach 100 Score
            if (state.SessionScore >= 100 && !HasAchievement(AchievScore100))
            {
                TryUnlockAchievement(AchievScore100);
                return;
            }
        }

        private void HandleChallengeCompleted(ChallengeCompletedEvent evt)
        {
            // Complete First Challenge
            if (!HasAchievement(AchievFirstChallenge))
            {
                TryUnlockAchievement(AchievFirstChallenge);
            }
        }

        // -- Helpers -------------------------------------------

        private void TryUnlockAchievement(string achievementName)
        {
            if (unlockedAchievements.Add(achievementName))
            {
                Debug.Log($"[AchievementController] Achievement unlocked — {achievementName}");
                EventBus.Publish(new AchievementUnlockedEvent
                {
                    AchievementId = achievementName,
                    DisplayName = achievementName
                });
            }
        }

        private bool HasAchievement(string achievementName)
        {
            return unlockedAchievements.Contains(achievementName);
        }

        private static bool DidReact(ReactionEvaluationResult eval)
        {
            return eval.Status == ReactionStatus.Success || eval.Status == ReactionStatus.Partial;
        }
    }
}
