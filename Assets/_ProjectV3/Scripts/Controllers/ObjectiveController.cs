// ChemLabSim v3 — Objective Controller
// Per-level lesson objectives. Resets on LevelUp, publishes ObjectiveAssignedEvent + ObjectiveCompletedEvent.
// No UI, no PlayerPrefs — events only.
//
// Migration source: LabController.ObjectiveTitles[], UpdateObjectiveProgress(),
//   GetObjectiveForCurrentLevel(), objectiveCompleted.

using UnityEngine;
using ChemLabSimV3.Core;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;
using ChemLabSimV3.Services;

namespace ChemLabSimV3.Controllers
{
    public class ObjectiveController : V3ControllerBase
    {
        // -- Objective Definitions -----------------------------
        private static readonly string[] ObjectiveTitles =
        {
            "Perform one valid successful reaction.",
            "Complete a reaction using the correct medium.",
            "Complete a reaction with strong contact or proper catalyst use.",
            "Complete an advanced successful reaction under correct conditions."
        };

        // -- State ---------------------------------------------
        private ObjectiveState currentObjective;
        private int currentLevel;

        // -- Read-only accessor --------------------------------
        public ObjectiveState CurrentObjective => currentObjective;

        // -- Lifecycle -----------------------------------------

        protected override void OnInitialize()
        {
            var save = ServiceLocator.Get<SaveService>();
            var data = (save != null && save.HasSave()) ? save.GetSaveData() : null;
            currentLevel = data != null ? Mathf.Clamp(data.objective.level, 1, 4) : 1;
            AssignObjectiveForLevel(currentLevel);
            if (data != null && data.objective.completed)
            {
                currentObjective.Completed = true;
                EventBus.Publish(new ObjectiveCompletedEvent
                {
                    ObjectiveId = currentObjective.Id
                });
            }

            EventBus.Subscribe<ReactionEvaluatedEvent>(HandleReactionEvaluated);
            EventBus.Subscribe<LevelUpEvent>(HandleLevelUp);
            Debug.Log("[ObjectiveController] Initialized.");
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

            if (!DidReact(result))
                return;

            if (currentObjective.Completed)
                return;

            bool met = false;

            switch (currentLevel)
            {
                case 1: // Any valid successful reaction
                    met = true;
                    break;
                case 2: // Correct medium
                    met = !result.MediumMismatch;
                    break;
                case 3: // Strong contact or catalyst
                    met = result.ContactFactor >= 1.2f || result.CatalystApplied;
                    break;
                case 4: // Advanced success under correct conditions
                    met = !result.MediumMismatch && !result.ActivationNotReached && result.ContactFactor >= 1.0f;
                    break;
                default:
                    met = true;
                    break;
            }

            if (met)
            {
                currentObjective.Completed = true;
                Debug.Log($"[ObjectiveController] Objective completed — {currentObjective.Title}");
                EventBus.Publish(new ObjectiveCompletedEvent
                {
                    ObjectiveId = currentObjective.Id
                });
            }
        }

        private void HandleLevelUp(LevelUpEvent evt)
        {
            currentLevel = evt.NewLevel;
            AssignObjectiveForLevel(currentLevel);
        }

        // -- Helpers -------------------------------------------

        private void AssignObjectiveForLevel(int level)
        {
            string title = GetObjectiveTitleForLevel(level);
            currentObjective = new ObjectiveState
            {
                Id = $"objective_level_{level}",
                Title = title,
                Level = level,
                Completed = false
            };

            Debug.Log($"[ObjectiveController] Objective assigned — {title} (level {level})");
            EventBus.Publish(new ObjectiveAssignedEvent
            {
                ObjectiveId = currentObjective.Id,
                Title = title,
                Level = level
            });
        }

        private static string GetObjectiveTitleForLevel(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, ObjectiveTitles.Length - 1);
            return ObjectiveTitles[index];
        }

        private static bool DidReact(ReactionEvaluationResult eval)
        {
            return eval.Status == ReactionStatus.Success || eval.Status == ReactionStatus.Partial;
        }
    }
}
