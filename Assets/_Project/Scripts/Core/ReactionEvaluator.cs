using System;
using System.Collections.Generic;
using UnityEngine;

public enum ReactionMedium
{
    Neutral,
    Acidic,
    Basic
}

public enum ReactionStatus
{
    Success,
    Fail,
    Partial
}

public struct ReactionEvaluationInput
{
    public ReactionEntry reaction;
    public float stirring01;
    public float grinding01;
    public float temperatureC;
    public ReactionMedium medium;
    public bool hasCatalyst;

    public ReactionEvaluationInput(
        ReactionEntry reaction,
        float stirring01,
        float grinding01,
        float temperatureC,
        ReactionMedium medium,
        bool hasCatalyst)
    {
        this.reaction = reaction;
        this.stirring01 = stirring01;
        this.grinding01 = grinding01;
        this.temperatureC = temperatureC;
        this.medium = medium;
        this.hasCatalyst = hasCatalyst;
    }
}

public struct ReactionEvaluationResult
{
    public bool IsValid;
    public ReactionStatus Status;
    public string Summary;

    public bool MediumMismatch;
    public bool ActivationNotReached;
    public bool CatalystApplied;
    public bool LowContactQuality;
    public bool LowTemperature;

    public float ContactFactor;
    public float ActivationThresholdC;
    public float Rate01;

    public List<string> DetailedReasons;

    public ReactionEvaluationResult(
        bool isValid,
        ReactionStatus status,
        string summary,
        bool mediumMismatch,
        bool activationNotReached,
        bool catalystApplied,
        bool lowContactQuality,
        bool lowTemperature,
        float contactFactor,
        float activationThresholdC,
        float rate01,
        List<string> detailedReasons)
    {
        IsValid = isValid;
        Status = status;
        Summary = summary ?? string.Empty;
        MediumMismatch = mediumMismatch;
        ActivationNotReached = activationNotReached;
        CatalystApplied = catalystApplied;
        LowContactQuality = lowContactQuality;
        LowTemperature = lowTemperature;
        ContactFactor = contactFactor;
        ActivationThresholdC = activationThresholdC;
        Rate01 = rate01;
        DetailedReasons = detailedReasons ?? new List<string>();
    }
}

public static class ReactionEvaluator
{
    private const float MinReasonableTemperatureC = -100f;
    private const float MaxReasonableTemperatureC = 1000f;
    private const float PartialContactThreshold = 0.85f;
    private const float StrongContactThreshold = 1.20f;
    private const float PartialTemperatureWindowC = 15f;

    public static ReactionEvaluationResult Evaluate(ReactionEvaluationInput input)
    {
        var reasons = new List<string>();

        if (input.reaction == null)
        {
            reasons.Add("Reaction data is missing.");
            return CreateFailInvalid("Reaction data is missing.", reasons);
        }

        if (!IsKnownMedium(input.medium))
        {
            reasons.Add("Reaction medium is invalid.");
            return CreateFailInvalid("Reaction medium is invalid.", reasons);
        }

        if (float.IsNaN(input.stirring01) || float.IsInfinity(input.stirring01) ||
            float.IsNaN(input.grinding01) || float.IsInfinity(input.grinding01) ||
            float.IsNaN(input.temperatureC) || float.IsInfinity(input.temperatureC))
        {
            reasons.Add("Input values are not valid numbers.");
            return CreateFailInvalid("Input values are invalid.", reasons);
        }

        float stirring = Mathf.Clamp01(input.stirring01);
        float grinding = Mathf.Clamp01(input.grinding01);

        if (input.temperatureC < MinReasonableTemperatureC || input.temperatureC > MaxReasonableTemperatureC)
        {
            reasons.Add($"Temperature {input.temperatureC:0.#}°C is outside the supported range.");
            return CreateFailInvalid("Temperature is outside the supported range.", reasons);
        }

        if (!MediumMatches(input.reaction.requiredMedium, input.medium))
        {
            reasons.Add($"Required medium is '{NormalizeMediumLabel(input.reaction.requiredMedium)}' but actual medium is '{MediumLabel(input.medium)}'.");
            return new ReactionEvaluationResult(
                true,
                ReactionStatus.Fail,
                "Reaction failed due to medium mismatch.",
                mediumMismatch: true,
                activationNotReached: false,
                catalystApplied: false,
                lowContactQuality: false,
                lowTemperature: false,
                contactFactor: 0f,
                activationThresholdC: 0f,
                rate01: 0f,
                detailedReasons: reasons
            );
        }

        float contactFactor =
            Mathf.Lerp(0.6f, 1.6f, stirring) *
            Mathf.Lerp(0.6f, 1.6f, grinding);

        bool lowContactQuality = contactFactor < PartialContactThreshold;

        float threshold = input.reaction.activationTempC;
        bool catalystApplied = false;

        if (input.hasCatalyst && input.reaction.catalystAllowed)
        {
            threshold -= input.reaction.catalystDeltaTempC;
            catalystApplied = true;
            reasons.Add("Catalyst lowered the activation threshold.");
        }

        float effectiveTemperature = input.temperatureC;
        bool belowThreshold = effectiveTemperature < threshold;
        bool lowTemperature = belowThreshold;

        if (belowThreshold)
        {
            float tempGap = threshold - effectiveTemperature;

            if (tempGap <= PartialTemperatureWindowC)
            {
                reasons.Add("Temperature is slightly below the activation threshold.");
                if (lowContactQuality)
                    reasons.Add("Contact quality is also low, which slows the reaction further.");

                return new ReactionEvaluationResult(
                    true,
                    ReactionStatus.Partial,
                    BuildPartialSummary(lowTemperature: true, lowContactQuality, catalystApplied),
                    mediumMismatch: false,
                    activationNotReached: true,
                    catalystApplied: catalystApplied,
                    lowContactQuality: lowContactQuality,
                    lowTemperature: true,
                    contactFactor: contactFactor,
                    activationThresholdC: threshold,
                    rate01: 0.15f,
                    detailedReasons: reasons
                );
            }

            reasons.Add("Temperature is too low to reach activation energy.");
            return new ReactionEvaluationResult(
                true,
                ReactionStatus.Fail,
                "Reaction failed because activation temperature was not reached.",
                mediumMismatch: false,
                activationNotReached: true,
                catalystApplied: catalystApplied,
                lowContactQuality: lowContactQuality,
                lowTemperature: true,
                contactFactor: contactFactor,
                activationThresholdC: threshold,
                rate01: 0f,
                detailedReasons: reasons
            );
        }

        float tempFactor = Mathf.InverseLerp(threshold, threshold + 30f, effectiveTemperature);
        float normalizedContact = Mathf.Clamp01(contactFactor / 1.6f);
        float rate = Mathf.Clamp01((0.35f * tempFactor) + (0.65f * normalizedContact));

        if (lowContactQuality)
        {
            reasons.Add("Reaction conditions are valid, but contact between reactants is weak.");
            return new ReactionEvaluationResult(
                true,
                ReactionStatus.Partial,
                BuildPartialSummary(lowTemperature: false, lowContactQuality: true, catalystApplied),
                mediumMismatch: false,
                activationNotReached: false,
                catalystApplied: catalystApplied,
                lowContactQuality: true,
                lowTemperature: false,
                contactFactor: contactFactor,
                activationThresholdC: threshold,
                rate01: Mathf.Min(rate, 0.55f),
                detailedReasons: reasons
            );
        }

        if (contactFactor >= StrongContactThreshold)
            reasons.Add("Good stirring and grinding improved reactant contact.");

        if (catalystApplied)
            reasons.Add("Catalyst support improved reaction conditions.");

        return new ReactionEvaluationResult(
            true,
            ReactionStatus.Success,
            BuildSuccessSummary(catalystApplied, contactFactor),
            mediumMismatch: false,
            activationNotReached: false,
            catalystApplied: catalystApplied,
            lowContactQuality: false,
            lowTemperature: false,
            contactFactor: contactFactor,
            activationThresholdC: threshold,
            rate01: rate,
            detailedReasons: reasons
        );
    }

    public static bool MediumMatches(string requiredMedium, ReactionMedium actual)
    {
        if (string.IsNullOrWhiteSpace(requiredMedium))
            return true;

        string req = requiredMedium.Trim().ToLowerInvariant();

        switch (req)
        {
            case "neutral":
                return actual == ReactionMedium.Neutral;

            case "acidic":
                return actual == ReactionMedium.Acidic;

            case "basic":
                return actual == ReactionMedium.Basic;

            default:
                return false;
        }
    }

    public static string MediumLabel(ReactionMedium medium)
    {
        switch (medium)
        {
            case ReactionMedium.Neutral:
                return "Neutral";
            case ReactionMedium.Acidic:
                return "Acidic";
            case ReactionMedium.Basic:
                return "Basic";
            default:
                return "Unknown";
        }
    }

    private static bool IsKnownMedium(ReactionMedium medium)
    {
        return medium == ReactionMedium.Neutral ||
               medium == ReactionMedium.Acidic ||
               medium == ReactionMedium.Basic;
    }

    private static string NormalizeMediumLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Any";

        string normalized = value.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "neutral": return "Neutral";
            case "acidic": return "Acidic";
            case "basic": return "Basic";
            default: return value.Trim();
        }
    }

    private static ReactionEvaluationResult CreateFailInvalid(string summary, List<string> reasons)
    {
        return new ReactionEvaluationResult(
            false,
            ReactionStatus.Fail,
            string.IsNullOrWhiteSpace(summary) ? "Reaction evaluation failed." : summary,
            mediumMismatch: false,
            activationNotReached: false,
            catalystApplied: false,
            lowContactQuality: false,
            lowTemperature: false,
            contactFactor: 0f,
            activationThresholdC: 0f,
            rate01: 0f,
            detailedReasons: reasons
        );
    }

    private static string BuildPartialSummary(bool lowTemperature, bool lowContactQuality, bool catalystApplied)
    {
        if (lowTemperature && lowContactQuality)
            return catalystApplied
                ? "Reaction partially occurred. Catalyst helped, but temperature and contact quality are still limiting."
                : "Reaction partially occurred due to low temperature and weak contact between reactants.";

        if (lowTemperature)
            return catalystApplied
                ? "Reaction partially occurred. Catalyst helped, but temperature is still slightly low."
                : "Reaction partially occurred because temperature is slightly below the ideal activation level.";

        if (lowContactQuality)
            return catalystApplied
                ? "Reaction partially occurred. Catalyst helped, but contact between reactants is still weak."
                : "Reaction partially occurred because stirring or grinding is not strong enough.";

        return "Reaction partially occurred.";
    }

    private static string BuildSuccessSummary(bool catalystApplied, float contactFactor)
    {
        if (catalystApplied && contactFactor >= StrongContactThreshold)
            return "Reaction occurred successfully with catalyst support and strong reactant contact.";

        if (catalystApplied)
            return "Reaction occurred successfully with catalyst support.";

        if (contactFactor >= StrongContactThreshold)
            return "Reaction occurred successfully with strong reactant contact.";

        return "Reaction occurred successfully.";
    }
}