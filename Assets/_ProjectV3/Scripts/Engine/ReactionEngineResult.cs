// ChemLabSim v3 — Reaction Engine Detailed Result
// Full output from ReactionEngine.ProcessDetailed(), including the matched
// reaction entry and pipeline evaluation needed by SimulationStepper.

namespace ChemLabSimV3.Engine
{
    /// <summary>
    /// Complete result of processing a mix request through ReactionEngine,
    /// including data required to start a live SimulationStepper run.
    /// </summary>
    public struct ReactionEngineResult
    {
        public ReactionOutput Output;
        public ReactionEntry Reaction;
        public PipelineResult Pipeline;
        public ConditionInput ConditionInput;

        public bool Found => Output.Found;
    }
}
