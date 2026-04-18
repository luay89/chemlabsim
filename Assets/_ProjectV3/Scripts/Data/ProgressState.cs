// ChemLabSim v3 — ProgressState DTO
// Pure data carrier for session progression state.
// No logic, no UI, no persistence — just the snapshot.

namespace ChemLabSimV3.Data
{
    public struct ProgressState
    {
        public int SessionScore;
        public int TotalExperiments;
        public int SuccessfulExperiments;
        public int InvalidExperiments;
        public int BestScore;
        public int SuccessfulExperimentsInLevel;
        public int CurrentLevel;
        public string CurrentLessonTitle;
        public int NextLevelRequirement;
    }
}
