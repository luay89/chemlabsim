// ChemLabSim v3 — Guided experiment data (Resources/Data/guided_experiments.json)
// Pure C# — no Unity dependencies. Serializable fields so JsonUtility can read the file.
// The texts are English-first and stored with the experiment; every step is checked against the
// live lab state (LabSnapshot), never against a score.

using System;
using System.Collections.Generic;

namespace ChemLabSimV3.Domain.GuidedExperiments
{
    [Serializable]
    public class GuidedExperimentCatalog
    {
        public List<GuidedExperimentDef> experiments = new List<GuidedExperimentDef>();
    }

    /// <summary>A complete guided experiment.</summary>
    [Serializable]
    public class GuidedExperimentDef
    {
        public string id;
        public string title;
        public string summary;            // what the student will find out
        public string category;           // e.g. "Acid-Base", "Precipitation", "Gas"
        public int difficultyLevel = 1;   // 1-5
        public int estimatedMinutes = 3;
        public string prerequisiteExperimentId;
        public List<string> requiredReagents = new List<string>();
        public List<ExperimentStep> steps = new List<ExperimentStep>();
        public string conclusion;         // what the student has learned
    }

    /// <summary>A single step: what to do, how it is checked, and what to look at once it is done.</summary>
    [Serializable]
    public class ExperimentStep
    {
        public string instruction;
        public string hint;
        public string action;             // a StepActionType name (JsonUtility reads enums as numbers)
        public StepValidation validation = new StepValidation();
        public string observe;            // what to look at once the step is done
        public string explanation;        // why it happens

        /// <summary>The action, or Observe when the name is unknown (a step is never silently skipped).</summary>
        public StepActionType ActionType
        {
            get => Enum.TryParse(action, out StepActionType t) ? t : StepActionType.Observe;
            set => action = value.ToString();
        }
    }

    /// <summary>Type of action the student must perform.</summary>
    public enum StepActionType
    {
        SelectReagents,   // reagents as listed (order does not matter)
        SetTemperature,   // hot-plate temperature in range
        SetGrinding,      // grinding at least a value
        ToggleCatalyst,   // catalyst on/off as required
        ToggleCurrent,    // DC current on/off as required
        Mix,              // press Mix; the result must match
        Observe           // read and press Next
    }

    /// <summary>
    /// What the lab must show for the step to be done. Unset values are not checked: NaN for numbers,
    /// empty for text, and −1 for the switches (0 = off, 1 = on).
    /// </summary>
    [Serializable]
    public class StepValidation
    {
        public List<string> expectedReagents = new List<string>();
        public float minTemperature = float.NaN;
        public float maxTemperature = float.NaN;
        public float minGrinding = float.NaN;
        public int catalyst = -1;
        public int current = -1;
        public string expectedReactionId;       // the mix must run this reaction
        public string expectedResultStatus;     // "Success", "Partial", "Fail", "NotFound" or empty (any)
    }

    /// <summary>The lab state a step is checked against (built by the view from the live controls).</summary>
    public struct LabSnapshot
    {
        public IReadOnlyList<string> Reagents;  // selected formulas, empty slots left out
        public float TemperatureC;
        public float Grinding;
        public bool Catalyst;
        public bool Current;

        // Last mix since the step started (MixedSinceStepStart = false: no mix yet).
        public bool MixedSinceStepStart;
        public string LastReactionId;           // null when no reaction was found
        public string LastResultStatus;         // "Success", "Partial", "Fail" or "NotFound"
    }

    /// <summary>Outcome of checking a step: done, or what is still missing.</summary>
    public struct StepCheck
    {
        public bool Done;
        public string Waiting;                  // what is still missing (empty when done)

        public static StepCheck Ok => new StepCheck { Done = true, Waiting = string.Empty };
        public static StepCheck Wait(string why) => new StepCheck { Done = false, Waiting = why };
    }

    /// <summary>Runtime state of an experiment in progress.</summary>
    public class ExperimentSession
    {
        public string ExperimentId;
        public int CurrentStepIndex;
        public bool IsCompleted;
        public DateTime StartedAt;
        public DateTime? CompletedAt;
    }
}
