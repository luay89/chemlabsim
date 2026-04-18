// ChemLabSim v3 — SaveData
// Single serializable snapshot of all persistent v3 state.
// Owned exclusively by SaveService. Controllers never touch this directly.
// Composed of typed sub-models for clarity.

using System.Collections.Generic;

namespace ChemLabSimV3.Data
{
    [System.Serializable]
    public class SaveData
    {
        public ProgressSaveData progress = new ProgressSaveData();
        public AchievementSaveData achievements = new AchievementSaveData();
        public ChallengeSaveData challenge = new ChallengeSaveData();
        public ObjectiveSaveData objective = new ObjectiveSaveData();
        public NotebookSaveData notebook = new NotebookSaveData();
        public int languageIndex;
    }

    // -- Progress ------------------------------------------

    [System.Serializable]
    public class ProgressSaveData
    {
        public int sessionScore;
        public int totalExperiments;
        public int successfulExperiments;
        public int invalidExperiments;
        public int bestScore;
        public int successfulExperimentsInLevel;
        public int currentLevel = 1;
        public string currentLessonTitle = "Basic Reactions";
    }

    // -- Achievements --------------------------------------

    [System.Serializable]
    public class AchievementSaveData
    {
        public List<string> unlockedIds = new List<string>();
    }

    // -- Challenge -----------------------------------------

    [System.Serializable]
    public class ChallengeSaveData
    {
        public string id = "";
        public int level = 1;
        public bool completed;
    }

    // -- Objective -----------------------------------------

    [System.Serializable]
    public class ObjectiveSaveData
    {
        public string id = "";
        public int level = 1;
        public bool completed;
    }

    // -- Notebook ------------------------------------------

    [System.Serializable]
    public class NotebookSaveData
    {
        public List<NotebookEntrySave> entries = new List<NotebookEntrySave>();
        public int entryCounter;
    }

    [System.Serializable]
    public struct NotebookEntrySave
    {
        public int number;
        public string reagentSummary;
        public string outcomeKey;
        public string mediumName;
        public float temperatureC;
    }
}
