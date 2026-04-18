// ChemLabSim v3 — Objective State DTO
// Plain data carrier. No logic, no UI, no persistence.

namespace ChemLabSimV3.Data
{
    public struct ObjectiveState
    {
        public string Id;
        public string Title;
        public int Level;
        public bool Completed;
    }
}
