using System;

namespace ChemLabSimV3.Engine.SimulationV1
{
    /// <summary>
    /// Lightweight V1 runtime state for a single real-time reaction.
    /// Required fields are intentionally kept as simple floats.
    /// </summary>
    [Serializable]
    public class ReactionState
    {
        public float progress;
        public float temperature;
        public float reactionRate;
        public float gasAmount;
        public float colorShift;
    }
}
