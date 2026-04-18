// ChemLabSim v3 — Save Service
// Centralises all persistence. No other class touches PlayerPrefs.
// Subscribes to state-changing events, keeps an in-memory SaveData snapshot,
// and writes it to PlayerPrefs as a single JSON string.
//
// Public API:  SaveGame(), LoadGame(), ResetSave(), BuildSaveSnapshot(), ApplyLoadedState()
// Controllers call none of these — SaveService auto-saves via events and
// controllers pull restored state via GetSaveData() on init.

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Data;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Services
{
    public class SaveService : IService
    {
        private const string SaveKey = "ChemLabV3_SaveData";

        private SaveData data;
        private bool loaded;

        // -- IService ------------------------------------------

        public void Initialize()
        {
            data = LoadGame();
            loaded = data != null;
            if (!loaded) data = new SaveData();

            EventBus.Subscribe<ProgressUpdatedEvent>(OnProgressUpdated);
            EventBus.Subscribe<AchievementUnlockedEvent>(OnAchievementUnlocked);
            EventBus.Subscribe<ChallengeAssignedEvent>(OnChallengeAssigned);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ObjectiveAssignedEvent>(OnObjectiveAssigned);
            EventBus.Subscribe<ObjectiveCompletedEvent>(OnObjectiveCompleted);
            EventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            EventBus.Subscribe<NotebookUpdatedEvent>(OnNotebookUpdated);

            Debug.Log($"[SaveService] Initialized. Existing save: {loaded}");
        }

        public void Dispose()
        {
            SaveGame();

            EventBus.Unsubscribe<ProgressUpdatedEvent>(OnProgressUpdated);
            EventBus.Unsubscribe<AchievementUnlockedEvent>(OnAchievementUnlocked);
            EventBus.Unsubscribe<ChallengeAssignedEvent>(OnChallengeAssigned);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ObjectiveAssignedEvent>(OnObjectiveAssigned);
            EventBus.Unsubscribe<ObjectiveCompletedEvent>(OnObjectiveCompleted);
            EventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
            EventBus.Unsubscribe<NotebookUpdatedEvent>(OnNotebookUpdated);

            Debug.Log("[SaveService] Disposed. Final save performed.");
        }

        // -- Public API ----------------------------------------

        /// <summary>True if a valid save existed at boot time.</summary>
        public bool HasSave() => loaded;

        /// <summary>Returns the current in-memory save snapshot (never null).</summary>
        public SaveData GetSaveData() => data;

        /// <summary>Persist the current in-memory snapshot to disk immediately.</summary>
        public void SaveGame()
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        /// <summary>Read save data from disk. Returns null if no save or corrupt.</summary>
        public SaveData LoadGame()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
                return null;

            string json = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                var loaded = JsonUtility.FromJson<SaveData>(json);
                return loaded;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SaveService] Failed to parse save data, starting fresh: {ex.Message}");
                return null;
            }
        }

        /// <summary>Delete all persisted data and reset to defaults.</summary>
        public void ResetSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            data = new SaveData();
            loaded = false;
            Debug.Log("[SaveService] Save data reset.");
        }

        /// <summary>Build a SaveData snapshot from explicit state (for manual save points).</summary>
        public static SaveData BuildSaveSnapshot(
            ProgressState progress,
            IReadOnlyCollection<string> achievements,
            ChallengeState challenge,
            ObjectiveState objective,
            IReadOnlyList<NotebookEntry> notebookEntries,
            int notebookEntryCounter,
            int languageIndex)
        {
            var snap = new SaveData();

            // Progress
            snap.progress.sessionScore = progress.SessionScore;
            snap.progress.totalExperiments = progress.TotalExperiments;
            snap.progress.successfulExperiments = progress.SuccessfulExperiments;
            snap.progress.invalidExperiments = progress.InvalidExperiments;
            snap.progress.bestScore = progress.BestScore;
            snap.progress.successfulExperimentsInLevel = progress.SuccessfulExperimentsInLevel;
            snap.progress.currentLevel = progress.CurrentLevel;
            snap.progress.currentLessonTitle = progress.CurrentLessonTitle;

            // Achievements
            snap.achievements.unlockedIds.Clear();
            foreach (string a in achievements)
                snap.achievements.unlockedIds.Add(a);

            // Challenge
            snap.challenge.id = challenge.Id ?? "";
            snap.challenge.level = challenge.Level;
            snap.challenge.completed = challenge.Completed;

            // Objective
            snap.objective.id = objective.Id ?? "";
            snap.objective.level = objective.Level;
            snap.objective.completed = objective.Completed;

            // Notebook
            snap.notebook.entries.Clear();
            foreach (var e in notebookEntries)
            {
                snap.notebook.entries.Add(new NotebookEntrySave
                {
                    number = e.Number,
                    reagentSummary = e.ReagentSummary,
                    outcomeKey = e.OutcomeKey,
                    mediumName = e.MediumName,
                    temperatureC = e.TemperatureC
                });
            }
            snap.notebook.entryCounter = notebookEntryCounter;

            snap.languageIndex = languageIndex;
            return snap;
        }

        /// <summary>Replace the current in-memory snapshot and persist it.</summary>
        public void ApplyLoadedState(SaveData snapshot)
        {
            data = snapshot ?? new SaveData();
            loaded = true;
            SaveGame();
        }

        // -- Event Handlers (auto-save on state change) --------

        private void OnProgressUpdated(ProgressUpdatedEvent evt)
        {
            var s = evt.State;
            data.progress.sessionScore = s.SessionScore;
            data.progress.totalExperiments = s.TotalExperiments;
            data.progress.successfulExperiments = s.SuccessfulExperiments;
            data.progress.invalidExperiments = s.InvalidExperiments;
            data.progress.bestScore = s.BestScore;
            data.progress.successfulExperimentsInLevel = s.SuccessfulExperimentsInLevel;
            data.progress.currentLevel = s.CurrentLevel;
            data.progress.currentLessonTitle = s.CurrentLessonTitle;
            SaveGame();
        }

        private void OnAchievementUnlocked(AchievementUnlockedEvent evt)
        {
            if (!data.achievements.unlockedIds.Contains(evt.AchievementId))
                data.achievements.unlockedIds.Add(evt.AchievementId);
            SaveGame();
        }

        private void OnChallengeAssigned(ChallengeAssignedEvent evt)
        {
            data.challenge.id = evt.ChallengeId;
            data.challenge.level = evt.Level;
            data.challenge.completed = false;
            SaveGame();
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            data.challenge.completed = true;
            SaveGame();
        }

        private void OnObjectiveAssigned(ObjectiveAssignedEvent evt)
        {
            data.objective.id = evt.ObjectiveId;
            data.objective.level = evt.Level;
            data.objective.completed = false;
            SaveGame();
        }

        private void OnObjectiveCompleted(ObjectiveCompletedEvent evt)
        {
            data.objective.completed = true;
            SaveGame();
        }

        private void OnLanguageChanged(LanguageChangedEvent evt)
        {
            data.languageIndex = evt.LanguageIndex;
            SaveGame();
        }

        private void OnNotebookUpdated(NotebookUpdatedEvent evt)
        {
            if (evt.Entries == null) return;
            data.notebook.entries.Clear();
            foreach (var e in evt.Entries)
            {
                data.notebook.entries.Add(new NotebookEntrySave
                {
                    number = e.Number,
                    reagentSummary = e.ReagentSummary,
                    outcomeKey = e.OutcomeKey,
                    mediumName = e.MediumName,
                    temperatureC = e.TemperatureC
                });
            }
            data.notebook.entryCounter = evt.EntryCounter;
            SaveGame();
        }
    }
}
