// ChemLabSim v3 — Reaction Pathway System
// Models multi-step reactions with intermediate states.
//
// A reaction pathway is a chain of reaction steps:
//   Step 1: A + B → C + D        (main reaction)
//   Step 2: C + E → F            (triggered when step 1 reaches threshold)
//   Step 3: D + F → G + H2O      (triggered when step 2 completes)
//
// Real chemistry examples:
//   Combustion: C3H8 → CO2 + H2O via radical intermediates
//   Acid-base then precipitation: HCl + NaOH → NaCl → NaCl + AgNO3 → AgCl↓
//   Thermal decomposition chains: CaCO3 → CaO + CO2
//
// Pathway data can be encoded directly in ReactionEntry via:
//   nextReactionId — ID of the next step
//   chainThreshold — progress level (0–1) at which next step activates
//
// PathwayTracker is the runtime manager (not MonoBehaviour — owned by SimulationStepper).

using System.Collections.Generic;
using UnityEngine;
using ChemLabSimV3.Events;

namespace ChemLabSimV3.Engine.Chemistry
{
    /// <summary>One step in a reaction chain.</summary>
    public class PathwayStep
    {
        /// <summary>The reaction for this step.</summary>
        public ReactionEntry Reaction;

        /// <summary>Progress of the preceding step (0–1) at which this step activates.</summary>
        public float TriggerThreshold;

        /// <summary>Delay in simulation seconds before this step starts after trigger.</summary>
        public float DelaySeconds;

        /// <summary>Whether this step has been triggered (may still be in delay period).</summary>
        public bool Triggered;

        /// <summary>Whether this step is actively running.</summary>
        public bool Active;

        /// <summary>Countdown timer for the delay period (seconds).</summary>
        public float DelayRemaining;

        /// <summary>Current simulation state for this step (null until activated).</summary>
        public ReactionState State;
    }

    /// <summary>An active multi-step reaction pathway.</summary>
    public class ReactionPathway
    {
        public readonly List<PathwayStep> Steps = new List<PathwayStep>();
        public int CurrentStepIndex;
        public bool IsComplete;

        /// <summary>Currently active step.</summary>
        public PathwayStep CurrentStep =>
            CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

        /// <summary>Whether any step is currently running.</summary>
        public bool AnyActive
        {
            get
            {
                for (int i = 0; i < Steps.Count; i++)
                    if (Steps[i].Active) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// Builds and updates reaction pathways at runtime.
    /// Owned and ticked by SimulationStepper.
    /// </summary>
    public class PathwayTracker
    {
        private readonly ReactionPathway _pathway;
        private readonly ReactionRegistry _registry;

        public ReactionPathway Pathway => _pathway;
        public bool HasPathway => _pathway.Steps.Count > 1;

        public PathwayTracker(ReactionEntry rootReaction, ReactionRegistry registry)
        {
            _registry = registry;
            _pathway = BuildPathway(rootReaction);
        }

        // ═══════════════════════════════════════════════════════
        //  BUILD
        // ═══════════════════════════════════════════════════════

        private ReactionPathway BuildPathway(ReactionEntry root)
        {
            var pathway = new ReactionPathway();
            var visited = new HashSet<string>();

            // Walk the chain via nextReactionId
            ReactionEntry current = root;
            while (current != null && !visited.Contains(current.id ?? string.Empty))
            {
                if (!string.IsNullOrEmpty(current.id))
                    visited.Add(current.id);

                pathway.Steps.Add(new PathwayStep
                {
                    Reaction         = current,
                    TriggerThreshold = current.chainThreshold > 0f ? current.chainThreshold : 1f,
                    DelaySeconds     = 0f,
                    Triggered        = pathway.Steps.Count == 0, // First step starts immediately
                    Active           = pathway.Steps.Count == 0,
                    DelayRemaining   = 0f,
                    State            = null
                });

                // Follow chain
                current = !string.IsNullOrEmpty(current.nextReactionId) && _registry != null
                    ? _registry.FindById(current.nextReactionId)
                    : null;
            }

            return pathway;
        }

        // ═══════════════════════════════════════════════════════
        //  TICK
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Update pathway state. Returns a new PathwayStep if a new step just activated,
        /// null otherwise. Called by SimulationStepper every frame.
        /// </summary>
        /// <param name="currentState">The currently running step's ReactionState.</param>
        /// <param name="dt">Real dt (NOT scaled — used for delay timers).</param>
        public PathwayStep Tick(ReactionState currentState, float dt)
        {
            if (_pathway.IsComplete || !HasPathway) return null;

            int idx = _pathway.CurrentStepIndex;
            if (idx >= _pathway.Steps.Count) return null;

            var current = _pathway.Steps[idx];

            // Sync state reference to current step
            if (currentState != null)
                current.State = currentState;

            // Check if next step should be triggered
            int nextIdx = idx + 1;
            if (nextIdx >= _pathway.Steps.Count)
            {
                // This is the last step — check if it's complete
                if (currentState != null && currentState.IsComplete)
                    _pathway.IsComplete = true;
                return null;
            }

            var next = _pathway.Steps[nextIdx];

            if (next.Triggered && !next.Active)
            {
                // In delay period
                next.DelayRemaining -= dt;
                if (next.DelayRemaining <= 0f)
                {
                    next.Active = true;
                    _pathway.CurrentStepIndex = nextIdx;

                    Debug.Log($"[Pathway] Step {nextIdx} activated: {next.Reaction?.name_en ?? next.Reaction?.id}");
                    return next;
                }
            }

            if (!next.Triggered && currentState != null)
            {
                // Check trigger threshold
                if (currentState.Progress >= next.TriggerThreshold)
                {
                    next.Triggered = true;
                    next.DelayRemaining = next.DelaySeconds;

                    if (next.DelaySeconds <= 0f)
                    {
                        next.Active = true;
                        _pathway.CurrentStepIndex = nextIdx;
                        Debug.Log($"[Pathway] Step {nextIdx} immediately activated: {next.Reaction?.name_en ?? next.Reaction?.id}");
                        return next;
                    }
                }
            }

            return null;
        }

        // ═══════════════════════════════════════════════════════
        //  QUERY
        // ═══════════════════════════════════════════════════════

        /// <summary>Human-readable summary of the pathway.</summary>
        public string GetSummary()
        {
            if (!HasPathway) return string.Empty;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _pathway.Steps.Count; i++)
            {
                var step = _pathway.Steps[i];
                string name = step.Reaction?.name_en ?? step.Reaction?.id ?? $"Step {i}";
                string status = step.Active ? "[ACTIVE]" : step.Triggered ? "[QUEUED]" : "[PENDING]";
                sb.Append($"Step {i + 1}: {name} {status}");
                if (i < _pathway.Steps.Count - 1)
                    sb.Append(" → ");
            }
            return sb.ToString();
        }
    }
}
