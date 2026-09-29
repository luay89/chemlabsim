// ChemLabSim v3 — Reaction Evaluation Adapter
// Maps ReactionEngine output to legacy ReactionEvaluationInput/Result types
// so existing controllers and views keep working during engine migration.

using System.Collections.Generic;
using ChemLabSimV3.Data;

namespace ChemLabSimV3.Engine
{
    public static class ReactionEvaluationAdapter
    {
        private const float PartialContactThreshold = 0.85f;

        public static ReactionEvaluationInput ToLegacyInput(MixRequest request, ReactionEntry reaction)
        {
            return new ReactionEvaluationInput(
                reaction,
                request.Stirring,
                request.Grinding,
                request.Temperature,
                request.Medium,
                request.HasCatalyst);
        }

        public static ReactionEvaluationResult ToLegacyResult(ReactionEngineResult engineResult)
        {
            var output = engineResult.Output;

            if (!output.Found)
            {
                var reasons = BuildReasonList(output.Conditions);
                if (reasons.Count == 0 && !string.IsNullOrEmpty(output.Summary))
                    reasons.Add(output.Summary);

                return new ReactionEvaluationResult(
                    isValid: false,
                    status: ReactionStatus.Fail,
                    summary: string.IsNullOrWhiteSpace(output.Summary)
                        ? "No matching reaction found for the selected reagents."
                        : output.Summary,
                    mediumMismatch: false,
                    activationNotReached: false,
                    catalystApplied: false,
                    lowContactQuality: false,
                    lowTemperature: false,
                    contactFactor: 0f,
                    activationThresholdC: 0f,
                    rate01: 0f,
                    detailedReasons: reasons);
            }

            bool mediumMismatch = IsConditionFailed(output.Conditions, "Medium");
            bool activationNotReached = IsConditionFailed(output.Conditions, "Temperature")
                                     || IsConditionPartial(output.Conditions, "Temperature");
            bool lowTemperature = activationNotReached;
            bool lowContactQuality = IsConditionPartial(output.Conditions, "SurfaceArea")
                                  || engineResult.ConditionInput.ContactFactor < PartialContactThreshold;
            bool catalystApplied = engineResult.Reaction != null
                                && engineResult.Reaction.catalystAllowed
                                && engineResult.ConditionInput.HasCatalyst;

            return new ReactionEvaluationResult(
                isValid: true,
                status: output.Status,
                summary: output.Summary ?? string.Empty,
                mediumMismatch: mediumMismatch,
                activationNotReached: activationNotReached,
                catalystApplied: catalystApplied,
                lowContactQuality: lowContactQuality,
                lowTemperature: lowTemperature,
                contactFactor: engineResult.ConditionInput.ContactFactor,
                activationThresholdC: engineResult.ConditionInput.EffectiveActivationC,
                rate01: output.Rate,
                detailedReasons: BuildReasonList(output.Conditions));
        }

        private static List<string> BuildReasonList(List<ConditionResult> conditions)
        {
            var reasons = new List<string>();
            if (conditions == null) return reasons;

            for (int i = 0; i < conditions.Count; i++)
            {
                var reason = conditions[i].Reason;
                if (!string.IsNullOrWhiteSpace(reason))
                    reasons.Add(reason);
            }

            return reasons;
        }

        private static bool IsConditionFailed(List<ConditionResult> conditions, string name)
        {
            return TryGetCondition(conditions, name, out var result) && result.Failed;
        }

        private static bool IsConditionPartial(List<ConditionResult> conditions, string name)
        {
            return TryGetCondition(conditions, name, out var result) && result.IsPartial;
        }

        private static bool TryGetCondition(List<ConditionResult> conditions, string name, out ConditionResult result)
        {
            result = default;
            if (conditions == null) return false;

            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i].Name == name)
                {
                    result = conditions[i];
                    return true;
                }
            }

            return false;
        }
    }
}
