// ChemLabSim v3 — Achievement State DTO
// Plain data carrier. No logic, no UI, no persistence.

namespace ChemLabSimV3.Data
{
    public struct AchievementState
    {
        public string Id;
        public string DisplayName;
        public bool Unlocked;
    }
}
