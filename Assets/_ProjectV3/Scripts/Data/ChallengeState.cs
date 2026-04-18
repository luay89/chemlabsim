// ChemLabSim v3 — Challenge State DTO
// Plain data carrier. No logic, no UI, no persistence.

namespace ChemLabSimV3.Data
{
    public struct ChallengeState
    {
        public string Id;
        public string Title;
        public int Level;
        public bool Completed;
        public int RewardPoints;
    }
}
